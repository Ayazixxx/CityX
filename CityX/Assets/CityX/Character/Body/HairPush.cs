using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CityX.Character
{
    /// <summary>
    /// HAIR KEPT OUT OF THE SHIRT (owner, 2026-11-12: "is there a better fix for the shirt spikes stabbing through the shirt than them bending?"). Every body
    /// wearing hair (<see cref="CharacterHair"/>) is here; before each frame is drawn the upper-body pieces of the nearest of them (the torso, the shoulder bar,
    /// the neck column and each arm to its elbow, as the body is built: <see cref="FusedBodyShape.UpperBody"/>), posed as they are drawn, go to the shader as
    /// globals, and the hair's vertices are pushed out of them (View/Shaders/CityXHairPush.hlsl), so no lock goes into the shirt whatever the head and the
    /// arms do. Every screen works out its own from the bodies it draws, so other players' copies are kept out of their shirts too.
    /// </summary>
    public static class HairPush
    {
        /// <summary>The shader keyword the hair's material turns on: its vertices are pushed.</summary>
        public const string Keyword = "_CITYX_HAIR";

        /// <summary>The most bodies at once (the nearest the camera), and their pieces: CITYX_HAIR_PIECES_MAX in the shader.</summary>
        public const int MaxBodies = 8;

        public const int PiecesPerBody = 5;

        /// <summary>Which of a body's pieces is its neck column (FusedBodyShape.UpperBody): hair tucked under a collar is drawn in to its line.</summary>
        public const int NeckPiece = 2;

        public const int MaxPieces = MaxBodies * PiecesPerBody;

        /// <summary>
        /// How far over the body's pieces pushed hair is laid, metres on a body of the player's size: under the shirt, not on it (the T-shirts are kept at least
        /// 8 mm off the body at the collar and 12 mm elsewhere, SourceArt/TShirt/tshirt_build.py's COLLAR_CLEAR and CLEAR; with 3.5 cm the tips lay on the shirt as slivers).
        /// </summary>
        public const float ShirtRoom = 0.006f;

        private static readonly int AId = Shader.PropertyToID("_HairPushA");
        private static readonly int BId = Shader.PropertyToID("_HairPushB");
        private static readonly int CountId = Shader.PropertyToID("_HairPushCount");
        private static readonly int HeadsId = Shader.PropertyToID("_HairPushHeads");
        private static readonly int HeadCountId = Shader.PropertyToID("_HairPushHeadCount");

        /// <summary>
        /// The head sphere is made this much bigger, metres on a body of the player's size: hair on its scalp is not pressed into a board on the back, nor any into the head, so
        /// for a frame the board goes through the hair instead (owner, 2026-11-12: "there is a frame where the bald scalp is viewable ... i would much
        /// rather have a frame where the boomerang clips through the hair").
        /// </summary>
        public const float ScalpRoom = 0.01f;

        private static readonly int TuckRimId = Shader.PropertyToID("_HairTuckRim");
        private static readonly int TuckUpId = Shader.PropertyToID("_HairTuckUp");
        private static readonly int TuckForwardId = Shader.PropertyToID("_HairTuckForward");

        /// <summary>
        /// A HIGH COLLAR the hair is tucked under (owner, 2026-11-15: "make it so the kick hair doesnt clip through the midnight coat ... right now the back spikes are
        /// clipping through"): its rim, a point on the neck's line at the height of the collar's top behind the neck, and which way is up out of it and forward, all
        /// in the frame of the bone the collar is skinned to, so it goes where the collar goes. A coat's fur collar stands centimetres off the body round the lower
        /// head: hair in it is under the fur, but the locks hanging below it came out through the coat's back, where no push off the body reaches.
        /// </summary>
        private readonly struct Collar
        {
            public readonly Transform Bone;
            public readonly Vector3 Rim, Up, Forward;

            public Collar(Transform bone, Vector3 rim, Vector3 up, Vector3 forward)
            {
                Bone = bone;
                Rim = rim;
                Up = up;
                Forward = forward;
            }
        }

        private static readonly Dictionary<Animator, Collar> Collars = new Dictionary<Animator, Collar>();
        private static readonly List<Animator> Gone = new List<Animator>();
        private static readonly Vector4[] TuckRims = new Vector4[MaxBodies];
        private static readonly Vector4[] TuckUps = new Vector4[MaxBodies];
        private static readonly Vector4[] TuckForwards = new Vector4[MaxBodies];

        /// <summary>
        /// The body wears a high collar (skinned to <paramref name="bone"/>; the rim and the directions in its frame): the hair hanging below the rim behind and beside
        /// the neck is tucked in under it, onto the body (the shader: CityXHairTuck).
        /// </summary>
        public static void TuckUnder(Animator body, Transform bone, Vector3 rimInBone, Vector3 upInBone, Vector3 forwardInBone)
        {
            if (body != null && bone != null)
            {
                Collars[body] = new Collar(bone, rimInBone, upInBone.normalized, forwardInBone.normalized);
            }
        }

        public static void Untuck(Animator body)
        {
            if (body != null)
            {
                Collars.Remove(body);
            }
        }

        /// <summary>The body has a collar its hair is tucked under (for tests).</summary>
        public static bool Tucks(Animator body) => body != null && Collars.ContainsKey(body);

        private static readonly List<Animator> Bodies = new List<Animator>();
        private static readonly Vector4[] A = new Vector4[MaxPieces];
        private static readonly Vector4[] B = new Vector4[MaxPieces];
        private static readonly Vector4[] Heads = new Vector4[MaxBodies];
        private static readonly FusedBodyShape.Segment[] Pieces = new FusedBodyShape.Segment[PiecesPerBody];
        private static readonly List<Animator> Nearest = new List<Animator>(MaxBodies);
        private static Animator _measuring;
        private static readonly System.Func<HumanBodyBones, Vector3> BonePosition = BoneOfMeasuring;
        private static Vector3 _eye;
        private static readonly System.Comparison<Animator> ByDistance = (x, y) =>
            (x.transform.position - _eye).sqrMagnitude.CompareTo((y.transform.position - _eye).sqrMagnitude);
        private static bool _hooked;

        /// <summary>The bodies whose hair is kept out of them (for tests).</summary>
        public static int Count => Bodies.Count;

        /// <summary>A body that wears hair: its hair is kept out of it from now on.</summary>
        public static void Wear(Animator body)
        {
            if (body == null || !body.isHuman || Bodies.Contains(body))
            {
                return;
            }
            if (!_hooked)
            {
                _hooked = true;
                RenderPipelineManager.beginContextRendering += (context, cameras) => Upload(cameras.Count > 0 ? cameras[0] : null);
            }
            Bodies.Add(body);
        }

        public static void TakeOff(Animator body)
        {
            Bodies.Remove(body);
        }

        private static Vector3 BoneOfMeasuring(HumanBodyBones bone)
        {
            Transform t = _measuring.GetBoneTransform(bone);
            return t != null ? t.position : _measuring.transform.position;
        }

        /// <summary>Sends the nearest bodies' pieces to the shader, as they are posed now (done before each frame is drawn).</summary>
        public static void Upload(Camera camera)
        {
            Bodies.RemoveAll(b => b == null);
            if (Collars.Count > 0)
            {
                // Bodies gone with their collars on.
                Gone.Clear();
                foreach (Animator body in Collars.Keys)
                {
                    if (body == null) Gone.Add(body);
                }
                foreach (Animator body in Gone)
                {
                    Collars.Remove(body);
                }
            }
            Nearest.Clear();
            foreach (Animator body in Bodies)
            {
                if (body.isActiveAndEnabled) Nearest.Add(body);
            }
            if (Nearest.Count > MaxBodies)
            {
                _eye = camera != null ? camera.transform.position : Vector3.zero;
                Nearest.Sort(ByDistance);
                Nearest.RemoveRange(MaxBodies, Nearest.Count - MaxBodies);
            }
            int count = 0, heads = 0;
            foreach (Animator body in Nearest)
            {
                _measuring = body;
                float scale = body.transform.lossyScale.y;
                // Built as the body is (BodyRigBuilder.LimbCapsules): its shape positions at their own size, the radii at the reference height's.
                int made = FusedBodyShape.UpperBody(BonePosition, FusedBodyShape.ReferenceHeight * scale, Pieces);
                float room = ShirtRoom * scale;
                for (int i = 0; i < made; i++)
                {
                    A[count] = new Vector4(Pieces[i].A.x, Pieces[i].A.y, Pieces[i].A.z, Pieces[i].Radius + room);
                    B[count] = new Vector4(Pieces[i].B.x, Pieces[i].B.y, Pieces[i].B.z, Pieces[i].RadiusB + room);
                    count++;
                }
                // The head is a sphere round its bone (FusedBodyShape.HeadRadius on the rig of the player's size).
                Vector3 head = BonePosition(HumanBodyBones.Head);
                // Its collar, if it wears one, in the same place in the list as its head (and its pieces, PiecesPerBody to a body).
                if (Collars.TryGetValue(body, out Collar collar) && collar.Bone != null)
                {
                    Vector3 rim = collar.Bone.TransformPoint(collar.Rim);
                    TuckRims[heads] = new Vector4(rim.x, rim.y, rim.z, 1.0f);
                    TuckUps[heads] = collar.Bone.TransformDirection(collar.Up);
                    TuckForwards[heads] = collar.Bone.TransformDirection(collar.Forward);
                }
                else
                {
                    TuckRims[heads] = TuckUps[heads] = TuckForwards[heads] = Vector4.zero;
                }
                Heads[heads++] = new Vector4(head.x, head.y, head.z, (FusedBodyShape.HeadRadius + ScalpRoom) * scale);
            }
            _measuring = null;
            for (int i = count; i < MaxPieces; i++)
            {
                A[i] = B[i] = Vector4.zero;
            }
            for (int i = heads; i < MaxBodies; i++)
            {
                Heads[i] = TuckRims[i] = TuckUps[i] = TuckForwards[i] = Vector4.zero;
            }
            Shader.SetGlobalVectorArray(TuckRimId, TuckRims);
            Shader.SetGlobalVectorArray(TuckUpId, TuckUps);
            Shader.SetGlobalVectorArray(TuckForwardId, TuckForwards);
            Shader.SetGlobalVectorArray(AId, A);
            Shader.SetGlobalVectorArray(BId, B);
            Shader.SetGlobalFloat(CountId, count);
            Shader.SetGlobalVectorArray(HeadsId, Heads);
            Shader.SetGlobalFloat(HeadCountId, heads);
        }
    }
}
