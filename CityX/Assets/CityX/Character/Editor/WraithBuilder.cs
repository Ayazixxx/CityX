using System.Collections.Generic;
using CityX.Character;
using UnityEditor;
using UnityEngine;

namespace CityX.CharacterEditor
{
    /// <summary>
    /// Builds Wraith.prefab (owner, 2026-10-26: "make a new enemy called "Wraith" ... the top half of him is the same dimensions as the
    /// playermodel ... his legs wisp away into nothing like a ghost. he is also somewhat transparent and glowing"):
    ///
    ///  * THE BODY is the player's own fused body mesh (FusedBody.asset) cut off at the hips: every triangle wholly above <see cref="CutY"/>,
    ///    with a vertex alpha that is solid from <see cref="FadeTopY"/> down to nothing at the cut, so the cut edge and the tops of the thighs
    ///    melt away rather than end. It keeps the body's own bone weights and bind poses, skinned to the player's skeleton
    ///    (BodyRigBuilder.BuildSkeleton) put into the arms-down rest the body is bound in (BodyRig.RestBindMatrix), so the Wraith can pose it
    ///    (WraithPose) without an Animator.
    ///  * THE HEAD is the player's own skull (Head.asset) on the head bone, under its own name, so the owner's modelled head (its toothy
    ///    grin, made in Meshy) can replace the mesh on that one object later.
    ///  * THE WISPS are ribbons of smoke made here: from a ring round the hips, twisting down and inward to a point, wide at the top and thin
    ///    at the tip, fading out along their length; the ghost shader sways them. They hang on the hips bone, so they go where the body does.
    ///  * THE SURFACE is Shaders/CityXGhost on two materials: the body's and the head's, still; the wisps', swaying.
    ///  * A point light glows from its chest, and a capsule collider stands in for its torso.
    ///
    /// Spawned only by /spawn wraith.
    /// </summary>
    public static class WraithBuilder
    {
        public const string PrefabPath = "Assets/CityX/Character/Wraith.prefab";
        private const string BodyMeshPath = "Assets/CityX/Character/FusedBody.asset";
        private const string HeadMeshPath = "Assets/CityX/Character/Head.asset";
        public const string WraithBodyPath = "Assets/CityX/Character/WraithBody.asset";
        public const string WispsPath = "Assets/CityX/Character/WraithWisps.asset";
        private const string GhostMaterialPath = "Assets/CityX/Character/Materials/WraithGhost.mat";
        private const string WispMaterialPath = "Assets/CityX/Character/Materials/WraithWisps.mat";
        public const string ShaderName = "CityX/Ghost";

        /// <summary>The object the wisps hang on (under the hips bone) and the glow's.</summary>
        public const string WispsName = "Wisps";
        public const string GlowName = "Glow";

        // THE FACE (owner, 2026-10-26: "glowing red eyes and 1 row of solid white sharp teeth ... try to place the teeth and the eyes in the
        // exact or similar placement to the image", then "eyes dont need to glow just make them solid red"), measured off the owner's picture
        // on a head of FusedBodyShape.HeadRadius: all in the skull's frame (its centre, facing +z). The parts hang under one "Face" object on
        // the skull, the teeth under a "Jaw" pivot at the middle of the lip, for the bite to come: a bite swings the jaw and lunges the head.
        public const string FaceName = "Face";
        public const string EyeLeftName = "EyeLeft";
        public const string EyeRightName = "EyeRight";
        public const string LipName = "Lip";
        public const string JawName = "Jaw";
        public const string TeethName = "Teeth";

        /// <summary>The eyes: this far out from the middle and up from the centre, this big, sunk this far into the skull.</summary>
        public const float EyeOffsetX = 0.21f;
        public const float EyeY = 0.0875f;
        public const float EyeRadius = 0.055f;
        public const float EyeSink = 0.02f;
        private const int EyeLatitudes = 10;
        private const int EyeLongitudes = 16;

        /// <summary>The lip the teeth hang from: this far below the centre, and the row's reach either side (degrees round the skull).</summary>
        public const float LipY = -0.0625f;
        public const float TeethHalfAngleDegrees = 44.0f;
        public const int TeethCount = 9;
        public const int ToothSides = 10;

        /// <summary>
        /// A tooth: its root's radius, how long the middle ones and the end ones are, and its root this far OUT of the skull's surface: sunk
        /// in, the see-through skull drew over the roots and greyed them, as if behind glass.
        /// </summary>
        public const float ToothBaseRadius = 0.028f;
        public const float ToothLengthMiddle = 0.32f;
        public const float ToothLengthEnd = 0.18f;
        public const float ToothSink = -0.004f;

        /// <summary>A tooth hangs down leaning this much outward (along the skull's surface), its tip curled this far back in.</summary>
        public const float ToothLean = 0.2f;
        public const float ToothCurl = 0.03f;

        /// <summary>
        /// The dark line of the mouth behind the teeth's roots: from here to here (up the skull), this far round, this far proud (enough to
        /// stand clear of the see-through skull, which otherwise draws over it and washes it out). Its top shows above the teeth's roots as
        /// the mouth's opening (lower, the roots hid all of it).
        /// </summary>
        public const float LipTopY = -0.02f;
        public const float LipBottomY = -0.05f;
        public const float LipHalfAngleDegrees = 48.0f;
        public const float LipProud = 0.012f;
        private const int LipSegments = 24;

        public static readonly Color EyeColour = new Color(0.78f, 0.04f, 0.05f);
        public static readonly Color ToothColour = new Color(0.96f, 0.96f, 0.94f);
        public static readonly Color LipColour = new Color(0.12f, 0.07f, 0.08f);
        private const float EyeSmoothness = 0.7f;
        private const float ToothSmoothness = 0.55f;

        private const string EyeMaterialPath = "Assets/CityX/Character/Materials/WraithEye.mat";
        private const string ToothMaterialPath = "Assets/CityX/Character/Materials/WraithTeeth.mat";
        private const string LipMaterialPath = "Assets/CityX/Character/Materials/WraithLip.mat";
        public const string EyeMeshPath = "Assets/CityX/Character/WraithEye.asset";
        public const string TeethMeshPath = "Assets/CityX/Character/WraithTeeth.asset";
        public const string LipMeshPath = "Assets/CityX/Character/WraithLip.asset";

        /// <summary>Nothing of the body below here (metres up a standing body): the pelvis's underside and the tops of the thighs go.</summary>
        public const float CutY = 0.75f;

        /// <summary>
        /// The torso is solid from here (the chest) down to where it starts to turn to smoke, gone at the cut; the arms, which hang beside
        /// the smoke, stay solid further down, to just over the hands. The ghost shader erodes the surface into streaks between.
        /// </summary>
        public const float FadeTopTorsoY = 1.55f;
        public const float FadeTopArmsY = 1.05f;

        /// <summary>How far out from the body's middle a point counts as the arms rather than the torso (blended between these).</summary>
        public const float ArmsFromX = 0.27f;
        public const float ArmsFullX = 0.42f;

        // The wisps: veils of smoke the torso's surface comes apart into: how many, how finely each is cut along its length, where on the
        // torso the highest starts (every third one lower), the torso's radius they start at, where their tips end, their width at the top
        // (wider than their share of the torso, so they overlap) and the tip, how far round the body they twist on the way down, how long
        // they stay wide before gathering to the point (the power the radius falls by) and how far they billow out first.
        public const int WispCount = 8;
        public const int WispSegments = 26;
        public const float WispTopY = 1.4f;
        public const float WispStartStep = 0.13f;
        public const float WispTipY = -0.05f;
        public const float WispTopRadius = 0.3f;
        public const float WispTopWidth = 0.38f;
        public const float WispTipWidth = 0.03f;
        public const float WispTwistTurns = 0.35f;
        public const float WispGather = 1.8f;
        public const float WispBulge = 0.25f;

        /// <summary>A veil starts this much as solid as the torso it grows out of: thinner smoke than the body, feathered at its sides.</summary>
        public const float WispSolidShare = 0.7f;

        /// <summary>The wisps' material sways their vertices this far (metres) at the tips; the body's not at all.</summary>
        public const float WispSway = 0.07f;

        // The wisps are thinner smoke than the body: less solid, a softer rim, a little less glow.
        private const float WispAlpha = 0.45f;
        private const float WispRimAlpha = 0.6f;
        private const float WispGlow = 1.1f;

        // Its colour: white with the faintest cold cast (owner: "make him a little less blue make him whiter"), the rim barely brighter, the
        // smoke inside the body a little stronger than the shader's default.
        public static readonly Color BodyColour = new Color(0.88f, 0.93f, 0.98f);
        public static readonly Color RimColour = new Color(1.05f, 1.05f, 1.08f);
        private const float BodyFlow = 0.45f;

        public static readonly Color GlowColour = new Color(0.8f, 0.9f, 1.0f);
        public const float GlowRange = 6.0f;
        private static readonly Vector3 GlowAt = new Vector3(0.0f, 1.55f, 0.1f);

        // The collider: the torso, hips to the top of the head.
        private static readonly Vector3 ColliderCentre = new Vector3(0.0f, 1.65f, 0.0f);
        private const float ColliderRadius = 0.4f;
        private const float ColliderHeight = 1.95f;

        [MenuItem("CityX/Rebuild Wraith")]
        public static GameObject Build()
        {
            var body = AssetDatabase.LoadAssetAtPath<Mesh>(BodyMeshPath);
            var head = AssetDatabase.LoadAssetAtPath<Mesh>(HeadMeshPath);
            if (body == null || head == null)
            {
                Debug.LogError("CityX: no fused body or head mesh -- rebuild the player rig before the Wraith.");
                return null;
            }

            GameObject root = BodyRigBuilder.BuildSkeleton("Wraith", out Dictionary<string, Transform> named);
            try
            {
                RestPose(named);
                Material ghost = GhostMaterial(GhostMaterialPath, "WraithGhost", 0.0f, UnityEngine.Rendering.CullMode.Back);
                ghost.SetFloat("_Flow", BodyFlow);
                Material wisps = GhostMaterial(WispMaterialPath, "WraithWisps", WispSway, UnityEngine.Rendering.CullMode.Off);
                wisps.SetFloat("_Alpha", WispAlpha);
                wisps.SetFloat("_RimAlpha", WispRimAlpha);
                wisps.SetFloat("_Glow", WispGlow);
                wisps.SetFloat("_EdgeFeather", 1.0f);

                List<HumanBodyBones> order = BodyRigBuilder.SkinBoneOrder();
                var bones = new Transform[order.Count];
                for (int i = 0; i < bones.Length; i++)
                {
                    bones[i] = named[NameOf(order[i])];
                }
                Mesh upper = SaveMesh(WraithBodyPath, UpperBody(body));
                var skin = new GameObject(BodySurface.SkinName);
                skin.transform.SetParent(root.transform, false);
                var skinRenderer = skin.AddComponent<SkinnedMeshRenderer>();
                skinRenderer.sharedMesh = upper;
                skinRenderer.bones = bones;
                skinRenderer.rootBone = named["hips"];
                skinRenderer.sharedMaterial = ghost;
                skinRenderer.quality = SkinQuality.Bone4;
                skinRenderer.updateWhenOffscreen = true;
                NoShadow(skinRenderer);

                var skull = new GameObject(BodySurface.HeadName);
                skull.transform.SetParent(named["head"], false);
                skull.transform.localPosition = HeadMesh.CentreOnBone;
                skull.AddComponent<MeshFilter>().sharedMesh = head;
                MeshRenderer headRenderer = skull.AddComponent<MeshRenderer>();
                headRenderer.sharedMaterial = ghost;
                NoShadow(headRenderer);
                Transform face = BuildFace(skull.transform, out Transform jaw);

                // The wisps are made in the body's frame and hung on the hips, so their origin is the body's.
                Mesh wispMesh = SaveMesh(WispsPath, Wisps());
                var tails = new GameObject(WispsName);
                tails.transform.SetParent(named["hips"], false);
                tails.transform.localPosition = -named["hips"].localPosition;
                tails.AddComponent<MeshFilter>().sharedMesh = wispMesh;
                MeshRenderer wispRenderer = tails.AddComponent<MeshRenderer>();
                wispRenderer.sharedMaterial = wisps;
                NoShadow(wispRenderer);

                var glowObject = new GameObject(GlowName);
                glowObject.transform.SetParent(root.transform, false);
                glowObject.transform.localPosition = GlowAt;
                Light glow = glowObject.AddComponent<Light>();
                glow.type = LightType.Point;
                glow.color = GlowColour;
                glow.intensity = Wraith.GlowIntensity;
                glow.range = GlowRange;
                glow.shadows = LightShadows.None;

                CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
                collider.center = ColliderCentre;
                collider.radius = ColliderRadius;
                collider.height = ColliderHeight;

                var wraith = root.AddComponent<Wraith>();
                wraith.Hips = named["hips"];
                wraith.Spine = named["spine"];
                wraith.Head = named["head"];
                wraith.ArmLeft = named["arm_l"];
                wraith.ArmRight = named["arm_r"];
                wraith.ForearmLeft = named["forearm_l"];
                wraith.ForearmRight = named["forearm_r"];
                wraith.HandLeft = named["hand_l"];
                wraith.HandRight = named["hand_r"];
                wraith.Renderers = new Renderer[] { skinRenderer, headRenderer, wispRenderer };
                wraith.MainCollision = collider;
                wraith.Glow = glow;
                wraith.Face = face;
                wraith.Jaw = jaw;

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: the Wraith written to {PrefabPath} -- {upper.triangles.Length / 3} body triangles, {wispMesh.triangles.Length / 3} in the wisps");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Every bone into the arms-down rest the body is bound in (BodyRig.RestBindMatrix): the skeleton is built in the T-pose a humanoid
        /// avatar needs, which the Wraith has no use for. Parents first, as BodyRig.Bones lists them.
        /// </summary>
        private static void RestPose(Dictionary<string, Transform> named)
        {
            foreach (BodyRig.Bone bone in BodyRig.Bones())
            {
                if (bone.Human == HumanBodyBones.LastBone || !named.TryGetValue(bone.Name, out Transform joint))
                {
                    continue;
                }
                Matrix4x4 rest = BodyRig.RestBindMatrix(bone.Human);
                joint.SetPositionAndRotation(rest.GetPosition(), rest.rotation);
            }
        }

        private static string NameOf(HumanBodyBones human)
        {
            foreach (BodyRig.Bone bone in BodyRig.Bones())
            {
                if (bone.Human == human)
                {
                    return bone.Name;
                }
            }
            throw new KeyNotFoundException($"CityX: the rig has no bone for {human}");
        }

        /// <summary>The body from the hips up, fading out toward the cut, with the body's own skinning.</summary>
        private static Mesh UpperBody(Mesh body)
        {
            Vector3[] vertices = body.vertices;
            Vector3[] normals = body.normals;
            Vector2[] uv = body.uv;
            BoneWeight[] weights = body.boneWeights;
            int[] triangles = body.triangles;

            var remap = new int[vertices.Length];
            for (int i = 0; i < remap.Length; i++)
            {
                remap[i] = -1;
            }
            var keptVertices = new List<Vector3>();
            var keptNormals = new List<Vector3>();
            var keptUv = new List<Vector2>();
            var keptColours = new List<Color>();
            var keptWeights = new List<BoneWeight>();
            var keptTriangles = new List<int>();
            for (int t = 0; t + 2 < triangles.Length; t += 3)
            {
                if (vertices[triangles[t]].y < CutY || vertices[triangles[t + 1]].y < CutY || vertices[triangles[t + 2]].y < CutY)
                {
                    continue;
                }
                for (int k = 0; k < 3; k++)
                {
                    int i = triangles[t + k];
                    if (remap[i] < 0)
                    {
                        remap[i] = keptVertices.Count;
                        keptVertices.Add(vertices[i]);
                        keptNormals.Add(normals[i]);
                        keptUv.Add(uv.Length > i ? uv[i] : Vector2.zero);
                        keptColours.Add(new Color(1.0f, 1.0f, 1.0f, BodyAlphaAt(vertices[i])));
                        keptWeights.Add(weights[i]);
                    }
                    keptTriangles.Add(remap[i]);
                }
            }

            var mesh = new Mesh
            {
                name = "WraithBody",
                indexFormat = keptVertices.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16,
            };
            mesh.SetVertices(keptVertices);
            mesh.SetNormals(keptNormals);
            mesh.SetUVs(0, keptUv);
            mesh.SetColors(keptColours);
            mesh.boneWeights = keptWeights.ToArray();
            mesh.SetTriangles(keptTriangles, 0);
            mesh.bindposes = body.bindposes;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// How solid the body is at <paramref name="point"/> (its vertex alpha): solid above the chest, turning to smoke down to nothing at the
        /// cut; the arms, out from the middle, solid down to just over the hands.
        /// </summary>
        public static float BodyAlphaAt(Vector3 point)
        {
            float arms = Mathf.SmoothStep(0.0f, 1.0f, Mathf.InverseLerp(ArmsFromX, ArmsFullX, Mathf.Abs(point.x)));
            float top = Mathf.Lerp(FadeTopTorsoY, FadeTopArmsY, arms);
            return Mathf.SmoothStep(0.0f, 1.0f, Mathf.InverseLerp(CutY, top, point.y));
        }

        /// <summary>
        /// The smoke the torso comes apart into: veils from the torso's own surface, each starting where the body is already turning to smoke
        /// (as solid there as the body is, so it grows out of it), billowing out a little, then twisting down and inward to a point; wide at
        /// the top and thin at the tip, fading out down its length (vertex alpha, which the ghost shader erodes into streaks) and on its own
        /// phase for the shader's sway (vertex colour r). Every third one starts lower and every second ends higher, so they do not all begin
        /// and end together. uv.y is how far down a veil a point is, which the sway grows with.
        /// </summary>
        private static Mesh Wisps()
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uv = new List<Vector2>();
            var colours = new List<Color>();
            var triangles = new List<int>();
            for (int ribbon = 0; ribbon < WispCount; ribbon++)
            {
                float startAngle = ribbon * (2.0f * Mathf.PI / WispCount) + 0.37f * ribbon;
                float topY = WispTopY - (ribbon % 3) * WispStartStep;
                float tipY = WispTipY + (ribbon % 2) * 0.1f;
                float topAlpha = BodyAlphaAt(new Vector3(0.0f, topY, 0.0f)) * WispSolidShare;
                float phase = (float)ribbon / WispCount;
                int first = vertices.Count;
                for (int step = 0; step <= WispSegments; step++)
                {
                    float s = (float)step / WispSegments;
                    float angle = startAngle + s * WispTwistTurns * 2.0f * Mathf.PI;
                    float radius = WispTopRadius * (1.0f + WispBulge * Mathf.Sin(s * Mathf.PI)) * (1.0f - Mathf.Pow(s, WispGather));
                    float y = Mathf.Lerp(topY, tipY, s);
                    var centre = new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
                    var across = new Vector3(-Mathf.Sin(angle), 0.0f, Mathf.Cos(angle)) * (Mathf.Lerp(WispTopWidth, WispTipWidth, s) * 0.5f);
                    var outward = new Vector3(Mathf.Cos(angle), 0.0f, Mathf.Sin(angle));
                    float alpha = topAlpha * Mathf.Pow(1.0f - s, 1.3f);
                    vertices.Add(centre - across);
                    vertices.Add(centre + across);
                    normals.Add(outward);
                    normals.Add(outward);
                    uv.Add(new Vector2(0.0f, s));
                    uv.Add(new Vector2(1.0f, s));
                    colours.Add(new Color(phase, 0.0f, 0.0f, alpha));
                    colours.Add(new Color(phase, 0.0f, 0.0f, alpha));
                    if (step > 0)
                    {
                        int a = first + (step - 1) * 2;
                        triangles.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 });
                    }
                }
            }
            var mesh = new Mesh { name = "WraithWisps" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv);
            mesh.SetColors(colours);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// The face on the skull: two solid red eyes sunk into it high and wide, the dark line of the mouth below the centre, and one row of
        /// sharp white teeth hanging from it under a jaw pivot. The eyes and the mouth are opaque and lit (CityX/Lit, cel shaded and outlined
        /// like everything solid), the teeth and the eyes gone with the body when it dies (Wraith shrinks the face away).
        /// </summary>
        private static Transform BuildFace(Transform skull, out Transform jaw)
        {
            float radius = FusedBodyShape.HeadRadius;
            var face = new GameObject(FaceName);
            face.transform.SetParent(skull, false);

            Material eyeMaterial = SolidMaterial(EyeMaterialPath, "WraithEye", EyeColour, EyeSmoothness);
            Mesh eyeMesh = SaveMesh(EyeMeshPath, Sphere("WraithEye", EyeRadius, EyeLatitudes, EyeLongitudes));
            foreach ((string name, float side) in new[] { (EyeLeftName, -1.0f), (EyeRightName, 1.0f) })
            {
                var eye = new GameObject(name);
                eye.transform.SetParent(face.transform, false);
                eye.transform.localPosition = EyeCentre(side);
                eye.AddComponent<MeshFilter>().sharedMesh = eyeMesh;
                MeshRenderer renderer = eye.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = eyeMaterial;
                NoShadow(renderer);
            }

            var lip = new GameObject(LipName);
            lip.transform.SetParent(face.transform, false);
            lip.AddComponent<MeshFilter>().sharedMesh = SaveMesh(LipMeshPath, Lip(radius));
            MeshRenderer lipRenderer = lip.AddComponent<MeshRenderer>();
            lipRenderer.sharedMaterial = SolidMaterial(LipMaterialPath, "WraithLip", LipColour, 0.2f);
            NoShadow(lipRenderer);

            // The jaw pivots at the middle of the lip, on the skull's surface; the teeth are made in the skull's frame and hung under it.
            var jawObject = new GameObject(JawName);
            jawObject.transform.SetParent(face.transform, false);
            jawObject.transform.localPosition = new Vector3(0.0f, LipY, LipRadius(radius));
            var teeth = new GameObject(TeethName);
            teeth.transform.SetParent(jawObject.transform, false);
            teeth.transform.localPosition = -jawObject.transform.localPosition;
            teeth.AddComponent<MeshFilter>().sharedMesh = SaveMesh(TeethMeshPath, Teeth(radius));
            MeshRenderer teethRenderer = teeth.AddComponent<MeshRenderer>();
            teethRenderer.sharedMaterial = SolidMaterial(ToothMaterialPath, "WraithTeeth", ToothColour, ToothSmoothness);
            NoShadow(teethRenderer);

            jaw = jawObject.transform;
            return face.transform;
        }

        /// <summary>Where an eye's centre sits in the skull's frame: on the surface at its offsets, sunk in by <see cref="EyeSink"/>.</summary>
        public static Vector3 EyeCentre(float side)
        {
            float radius = FusedBodyShape.HeadRadius;
            float x = side * EyeOffsetX;
            var surface = new Vector3(x, EyeY, Mathf.Sqrt(radius * radius - x * x - EyeY * EyeY));
            return surface * ((radius - EyeSink) / radius);
        }

        /// <summary>The radius of the skull's horizontal circle at the lip's height.</summary>
        public static float LipRadius(float radius) => Mathf.Sqrt(radius * radius - LipY * LipY);

        /// <summary>How long the tooth at <paramref name="share"/> of the way from the row's middle to its end is.</summary>
        public static float ToothLength(float share) => ToothLengthEnd + (ToothLengthMiddle - ToothLengthEnd) * Mathf.Pow(1.0f - Mathf.Clamp01(share), 1.2f);

        /// <summary>
        /// One row of teeth hanging from the lip: each a cone rooted in the skull at its place round the row, hanging down and leaning a little
        /// outward with the skull's surface, narrowing to a tip curled slightly back in; the middle ones long, the end ones short.
        /// </summary>
        private static Mesh Teeth(float radius)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            float ring = LipRadius(radius);
            for (int tooth = 0; tooth < TeethCount; tooth++)
            {
                float along = TeethCount > 1 ? (float)tooth / (TeethCount - 1) : 0.5f;
                float angle = Mathf.Lerp(-TeethHalfAngleDegrees, TeethHalfAngleDegrees, along) * Mathf.Deg2Rad;
                float share = Mathf.Abs(along - 0.5f) * 2.0f;
                var surface = new Vector3(Mathf.Sin(angle) * ring, LipY, Mathf.Cos(angle) * ring);
                Vector3 outward = surface.normalized;
                Vector3 root = surface * ((radius - ToothSink) / radius);
                Vector3 hang = (Vector3.down + outward * ToothLean).normalized;
                float length = ToothLength(share);
                Vector3 tip = root + hang * length - outward * ToothCurl;
                Vector3 across = Vector3.Cross(hang, outward).normalized;
                Vector3 through = Vector3.Cross(across, hang).normalized;

                int first = vertices.Count;
                foreach ((float at, float width) in new[] { (0.0f, ToothBaseRadius), (0.55f, ToothBaseRadius * 0.5f) })
                {
                    Vector3 centre = Vector3.Lerp(root, tip, at);
                    for (int side = 0; side < ToothSides; side++)
                    {
                        float round = side * (2.0f * Mathf.PI / ToothSides);
                        vertices.Add(centre + (across * Mathf.Cos(round) + through * Mathf.Sin(round)) * width);
                    }
                }
                vertices.Add(tip);
                int mid = first + ToothSides;
                int point = first + ToothSides * 2;
                for (int side = 0; side < ToothSides; side++)
                {
                    int next = (side + 1) % ToothSides;
                    triangles.AddRange(new[] { first + side, mid + side, first + next, first + next, mid + side, mid + next });
                    triangles.AddRange(new[] { mid + side, point, mid + next });
                }
            }
            var mesh = new Mesh { name = "WraithTeeth" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>The dark line of the mouth: a band round the skull's front just proud of it, the teeth's roots along its lower edge.</summary>
        private static Mesh Lip(float radius)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            float proud = radius + LipProud;
            for (int step = 0; step <= LipSegments; step++)
            {
                float angle = Mathf.Lerp(-LipHalfAngleDegrees, LipHalfAngleDegrees, (float)step / LipSegments) * Mathf.Deg2Rad;
                foreach (float y in new[] { LipTopY, LipBottomY })
                {
                    float ring = Mathf.Sqrt(proud * proud - y * y);
                    var point = new Vector3(Mathf.Sin(angle) * ring, y, Mathf.Cos(angle) * ring);
                    vertices.Add(point);
                    normals.Add(point.normalized);
                }
                if (step > 0)
                {
                    // Wound to face out of the skull (the lip's material culls back faces, unlike the wisps').
                    int a = (step - 1) * 2;
                    triangles.AddRange(new[] { a, a + 1, a + 2, a + 1, a + 3, a + 2 });
                }
            }
            var mesh = new Mesh { name = "WraithLip" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A plain sphere of <paramref name="radius"/>, as coarse as an eye needs.</summary>
        private static Mesh Sphere(string name, float radius, int latitudes, int longitudes)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            for (int lat = 0; lat <= latitudes; lat++)
            {
                float polar = Mathf.PI * lat / latitudes;
                for (int lon = 0; lon <= longitudes; lon++)
                {
                    float round = 2.0f * Mathf.PI * lon / longitudes;
                    var normal = new Vector3(Mathf.Sin(polar) * Mathf.Cos(round), Mathf.Cos(polar), Mathf.Sin(polar) * Mathf.Sin(round));
                    vertices.Add(normal * radius);
                    normals.Add(normal);
                }
            }
            int row = longitudes + 1;
            for (int lat = 0; lat < latitudes; lat++)
            {
                for (int lon = 0; lon < longitudes; lon++)
                {
                    // At the poles one of a quad's two triangles has two corners on the pole: nothing, left out.
                    int a = lat * row + lon;
                    if (lat > 0)
                    {
                        triangles.AddRange(new[] { a, a + 1, a + row });
                    }
                    if (lat < latitudes - 1)
                    {
                        triangles.AddRange(new[] { a + 1, a + row + 1, a + row });
                    }
                }
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A plain lit, opaque material at <paramref name="path"/> (CityX/Lit, cel shaded), kept where it exists.</summary>
        private static Material SolidMaterial(string path, string name, Color colour, float smoothness)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(CityX.Core.LitShader.Find()) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0.0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Writes the mesh at <paramref name="path"/>, updated in place when it exists so the prefab keeps its reference.</summary>
        private static Mesh SaveMesh(string path, Mesh mesh)
        {
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            existing.Clear();
            existing.indexFormat = mesh.indexFormat;
            existing.vertices = mesh.vertices;
            existing.normals = mesh.normals;
            existing.uv = mesh.uv;
            existing.colors = mesh.colors;
            existing.boneWeights = mesh.boneWeights;
            existing.triangles = mesh.triangles;
            existing.bindposes = mesh.bindposes;
            existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(mesh);
            return existing;
        }

        /// <summary>A ghost material at <paramref name="path"/>, kept where it exists so the prefab keeps its reference.</summary>
        private static Material GhostMaterial(string path, string name, float sway, UnityEngine.Rendering.CullMode cull)
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"CityX: no {ShaderName} shader -- the Wraith will be pink.");
            }
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (shader != null)
            {
                material.shader = shader;
            }
            material.SetFloat("_Sway", sway);
            material.SetFloat("_Cull", (float)cull);
            material.SetColor("_BaseColor", BodyColour);
            material.SetColor("_RimColor", RimColour);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void NoShadow(Renderer renderer)
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }
}
