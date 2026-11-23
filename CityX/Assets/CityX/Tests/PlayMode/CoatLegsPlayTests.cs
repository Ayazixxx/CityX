using System.Collections;
using System.Collections.Generic;
using System.Text;
using CityX.Character;
using CityX.Core;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// A long coat's skirt stays outside the legs through every melee weapon's swings (owner, 2026-11-21: "when swinging with pretty much any melee
    /// weapon the knee clips through the jacket", then "do what you can with minimal or preferably no alteration to the animations", "make it look
    /// good ingame"). The real game scene and the real player, a long coat worn (the parka), every melee weapon equipped in turn
    /// and swung by the mouse as a player would: light swings standing and walking, and a held heavy one. No part of the coat may be inside a thigh's
    /// lower half or a shin, through its outer cloth (where a leg would be seen through it), by more than the cloth's give for longer than a flicker.
    /// </summary>
    [TestFixture(ItemRegistry.GreenFurHoodedParka)]
    public sealed class CoatLegsPlayTests : InputTestFixture
    {
        private readonly string _coat;

        public CoatLegsPlayTests(string coatItemId)
        {
            _coat = coatItemId;
        }

        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";

        /// <summary>How far into a leg the coat's soft outer cloth may come and still read as lying on it.</summary>
        private const float LiningGive = 0.02f;

        /// <summary>
        /// A leg in the coat this many frames running or fewer (a twentieth of a second at 60 a second) is a flicker: the skirt bones are pushed off the
        /// legs a frame at a time (SkirtCloth), and a knee snapping up in a swing can outrun them for a frame or two. Longer, it is seen.
        /// </summary>
        private const int Flicker = 3;

        /// <summary>The thigh is checked from this far down it: above, the coat's waist sits round the hip joint as fitted.</summary>
        private const float ThighFrom = 0.35f;

        private Scene _scene;
        private Keyboard _keyboard;
        private Mouse _mouse;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _mouse = InputSystem.AddDevice<Mouse>();
        }

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            if (_scene.IsValid() && _scene.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        /// <summary>One leg's two pieces checked: the thigh's lower part and the shin, each with its skin's radius at either end.</summary>
        private struct Piece
        {
            public Transform From, To;
            public float FromAt, RadiusFrom, RadiusTo;
            public string Name;
        }

        private static List<Piece> Legs(Animator body)
        {
            float knee = (FusedBodyShape.LegRadius + FusedBodyShape.AnkleRadius) * 0.5f;
            var pieces = new List<Piece>();
            foreach ((HumanBodyBones hip, HumanBodyBones shin, HumanBodyBones foot, string side) in new[]
            {
                (HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, "left"),
                (HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, "right"),
            })
            {
                pieces.Add(new Piece { From = body.GetBoneTransform(hip), To = body.GetBoneTransform(shin), FromAt = ThighFrom,
                    RadiusFrom = FusedBodyShape.LegRadius, RadiusTo = knee, Name = side + " thigh" });
                pieces.Add(new Piece { From = body.GetBoneTransform(shin), To = body.GetBoneTransform(foot), FromAt = 0.0f,
                    RadiusFrom = knee, RadiusTo = FusedBodyShape.AnkleRadius, Name = side + " shin" });
            }
            return pieces;
        }

        /// <summary>How deep the coat's deepest point is inside a leg's skin now, and which piece.</summary>
        private static float Deepest(SkinnedMeshRenderer worn, Mesh baked, List<Piece> legs, out string where) => Deepest(worn, baked, legs, out where, out _);

        private static float Deepest(SkinnedMeshRenderer worn, Mesh baked, List<Piece> legs, out string where, out int vertex)
        {
            worn.BakeMesh(baked, true);
            Matrix4x4 toWorld = Matrix4x4.TRS(worn.transform.position, worn.transform.rotation, Vector3.one);
            Vector3[] vertices = baked.vertices;
            Vector3[] normals = baked.normals;
            float deepest = float.MinValue;
            where = "";
            vertex = -1;
            foreach (Piece piece in legs)
            {
                Vector3 a = piece.From.position, b = piece.To.position;
                Vector3 start = Vector3.Lerp(a, b, piece.FromAt);
                Vector3 along = b - start;
                float length2 = Mathf.Max(along.sqrMagnitude, 1e-8f);
                for (int v = 0; v < vertices.Length; v++)
                {
                    Vector3 p = toWorld.MultiplyPoint3x4(vertices[v]);
                    float t = Mathf.Clamp01(Vector3.Dot(p - start, along) / length2);
                    float radius = Mathf.Lerp(Mathf.Lerp(piece.RadiusFrom, piece.RadiusTo, piece.FromAt), piece.RadiusTo, t);
                    Vector3 off = p - (start + along * t);
                    float inside = radius - off.magnitude;
                    // Only the coat's outer cloth counts: its lining pressed into a leg behind cloth still outside it is not seen.
                    bool outer = Vector3.Dot(toWorld.MultiplyVector(normals[v]), off) > 0.0f;
                    if (outer && inside > deepest)
                    {
                        deepest = inside;
                        where = piece.Name;
                        vertex = v;
                    }
                }
            }
            return deepest;
        }

        [UnityTest]
        [Timeout(900000)]
        public IEnumerator NoKneeOrShinComesThroughTheCoatInAnyMeleeSwing()
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);
            PlayerInputReader input = null;
            foreach (GameObject root in _scene.GetRootGameObjects())
            {
                if (input == null) input = root.GetComponentInChildren<PlayerInputReader>(true);
            }
            Assert.IsNotNull(input, "no player in the real scene");
            GameObject player = input.gameObject;
            var locomotion = player.GetComponent<PlayerLocomotion>();
            for (int i = 0; i < 600 && !locomotion.IsGrounded; i++)
            {
                yield return null;
            }
            Time.captureFramerate = 60;
            var vitals = player.GetComponent<PlayerVitals>();
            vitals.InfiniteHealth = true;
            vitals.InfiniteStamina = true;
            var inventory = player.GetComponent<Inventory>();
            Assert.IsTrue(inventory.EquipItemId(_coat, player), $"the {_coat} could not be put on");
            yield return null;
            Animator body = player.GetComponentInChildren<Animator>();
            var worn = TransformSearch.FindDeep(player.transform, "CombinedGarmentSkin").GetComponent<SkinnedMeshRenderer>();
            List<Piece> legs = Legs(body);
            var baked = new Mesh();

            var weapons = new List<string> { null };   // the fists first
            var ids = new List<string>(ItemRegistry.Ids);
            ids.Sort(System.StringComparer.Ordinal);
            foreach (string id in ids)
            {
                GameObject prefab = inventory.PrefabForItem(id);
                if (prefab != null && (prefab.GetComponent<ISwordBlade>() != null || prefab.GetComponent<BottleWeapon>() != null))   // (every blade, and the chain)
                {
                    weapons.Add(id);
                }
            }

            var report = new StringBuilder();
            var through = new List<string>();
            float worstStanding = float.MinValue;
            try
            {
                worstStanding = Deepest(worn, baked, legs, out _);
                foreach (string weapon in weapons)
                {
                    if (weapon != null && !inventory.EquipItemId(weapon, player))
                    {
                        through.Add($"{weapon}: could not be equipped");
                        continue;
                    }
                    yield return Frames(10);
                    float deepest = float.MinValue;
                    string where = "", doing = "";
                    int watched = 0, clipping = 0, run = 0, longest = 0;

                    IEnumerator Watch(string what, int frames)
                    {
                        for (int f = 0; f < frames; f++)
                        {
                            yield return null;
                            float d = Deepest(worn, baked, legs, out string piece, out int vertex);
                            watched++;
                            run = d > LiningGive ? run + 1 : 0;
                            clipping += run > 0 ? 1 : 0;
                            longest = Mathf.Max(longest, run);
                            if (d > deepest)
                            {
                                deepest = d;
                                BoneWeight w = worn.sharedMesh.boneWeights[vertex];
                                Transform hip = body.GetBoneTransform(piece.StartsWith("left") ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg);
                                Transform knee = body.GetBoneTransform(piece.StartsWith("left") ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg);
                                Vector3 kneeFromHip = player.transform.InverseTransformDirection(knee.position - hip.position);
                                where = $"{piece}, the coat's point at {worn.sharedMesh.vertices[vertex]:F2} (bones {w.boneIndex0}:{w.weight0:F2} {w.boneIndex1}:{w.weight1:F2} {w.boneIndex2}:{w.weight2:F2}), knee from hip {kneeFromHip:F2}, frame {f}";
                                doing = what;
                            }
                        }
                    }

                    for (int swing = 0; swing < 4; swing++)
                    {
                        Press(_mouse.leftButton);
                        yield return Watch("light swings", 2);
                        Release(_mouse.leftButton);
                        yield return Watch("light swings", 20);
                    }
                    yield return Watch("light swings", 40);
                    Press(_mouse.leftButton);
                    yield return Watch("a held heavy swing", 45);
                    Release(_mouse.leftButton);
                    yield return Watch("a held heavy swing", 70);
                    Press(_keyboard.wKey);
                    for (int swing = 0; swing < 3; swing++)
                    {
                        Press(_mouse.leftButton);
                        yield return Watch("light swings walking", 2);
                        Release(_mouse.leftButton);
                        yield return Watch("light swings walking", 22);
                    }
                    Release(_keyboard.wKey);
                    yield return Watch("light swings walking", 40);

                    string name = weapon ?? "fists";
                    report.AppendLine($"  {name}: through in {clipping} of {watched} frames, at most {longest} running; {deepest * 100.0f:0.0} cm into the {where} at the deepest ({doing})");
                    if (longest > Flicker)
                    {
                        through.Add($"{name}: {longest} frames running, {deepest * 100.0f:0.0} cm into the {where} at the deepest ({doing})");
                    }
                    if (weapon != null)
                    {
                        string slot = inventory.EquippedItemId(Inventory.PrimarySlot) == weapon ? Inventory.PrimarySlot : Inventory.SecondarySlot;
                        if (inventory.UnequipToSlot(slot, player))
                        {
                            inventory.RemoveFromStorage(weapon, 1, out _);
                        }
                        else
                        {
                            inventory.ClearEquipSlotWithoutReturning(slot);
                        }
                    }
                    var ragdoll = player.GetComponent<Ragdoll>();
                    for (int i = 0; i < 400 && ragdoll != null && ragdoll.IsLimp; i++)
                    {
                        yield return null;
                    }
                }
            }
            finally
            {
                Object.Destroy(baked);
            }
            Debug.Log($"{_coat} and legs, standing: {worstStanding * 100.0f:0.0} cm at the deepest; through {weapons.Count} melee weapons:\n{report}");
            Assert.Greater(weapons.Count, 5, "too few melee weapons were found to swing");
            Assert.IsEmpty(through, "a leg came through the coat: " + string.Join("; ", through));
#else
            Assert.Ignore("needs the editor to load the scene.");
            yield break;
#endif
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return null;
            }
        }
    }
}
