using System.Collections.Generic;
using CityX.Character;
using CityX.Core;
using CityX.Items;
using CityX.Wearables;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The fingerless gloves (docs/FINGERLESS_GLOVES.md): made on the player's own hands, they cover the palm and the back of each hand, end half
    /// way up every finger and a hand's length up the forearm, stand just off the skin, follow only the hands' and forearms' bones; every
    /// colourway is an item in the gloves slot with its own prefab and colour.
    /// </summary>
    public sealed class FingerlessGlovesTests
    {
        private GameObject _player;
        private Mesh _gloves;
        private SkinnedMeshRenderer _skin;

        [OneTimeSetUp]
        public void MakeTheGloves()
        {
            _player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"));
            _skin = TransformSearch.FindDeep(_player.transform, BodySurface.SkinName).GetComponent<SkinnedMeshRenderer>();
            _gloves = FingerlessGloves.GlovesFor(_skin);
        }

        [OneTimeTearDown]
        public void TakeAway()
        {
            Object.DestroyImmediate(_player);
        }

        private int IndexOf(HumanBodyBones bone)
        {
            foreach (BodyRig.Bone each in BodyRig.Bones())
            {
                if (each.Human == bone)
                {
                    return System.Array.FindIndex(_skin.bones, b => b != null && b.name == each.Name);
                }
            }
            return -1;
        }

        private Vector3 Joint(HumanBodyBones bone) => _skin.sharedMesh.bindposes[IndexOf(bone)].inverse.MultiplyPoint3x4(Vector3.zero);

        [Test]
        public void TheyAreBoundLikeTheBodyAndFollowOnlyTheHandsAndForearms()
        {
            Assert.IsNotNull(_gloves);
            Assert.AreEqual(_skin.sharedMesh.bindposes.Length, _gloves.bindposes.Length, "not bound to the body's bones");
            var allowed = new HashSet<int>();
            // The upper arms a little too: the body blends them in down the forearm, past the cuff's edge.
            foreach (HumanBodyBones bone in new[]
            {
                HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm,
                HumanBodyBones.LeftHand, HumanBodyBones.RightHand,
            })
            {
                allowed.Add(IndexOf(bone));
            }
            for (var bone = HumanBodyBones.LeftThumbProximal; bone <= HumanBodyBones.RightLittleDistal; bone++)
            {
                int index = IndexOf(bone);
                if (index >= 0)
                {
                    allowed.Add(index);
                }
            }
            foreach (BoneWeight w in _gloves.boneWeights)
            {
                foreach ((int bone, float weight) in new[] { (w.boneIndex0, w.weight0), (w.boneIndex1, w.weight1), (w.boneIndex2, w.weight2), (w.boneIndex3, w.weight3) })
                {
                    if (weight > 0.05f)
                    {
                        Assert.IsTrue(allowed.Contains(bone), $"a glove point follows {_skin.bones[bone].name}");
                    }
                }
            }
        }

        [Test]
        public void TheyCoverTheHandsAndStopHalfWayUpTheFingersAndAHandsLengthUpTheArm()
        {
            Vector3[] points = _gloves.vertices;
            Assert.Greater(points.Length, 500);
            foreach (bool left in new[] { true, false })
            {
                Vector3 wrist = Joint(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                Vector3 elbow = Joint(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
                Vector3 up = (elbow - wrist).normalized;
                Vector3 root = Joint(left ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
                Vector3 knuckle = Joint(left ? HumanBodyBones.LeftMiddleIntermediate : HumanBodyBones.RightMiddleIntermediate);
                Vector3 along = (knuckle - root).normalized;
                float furthestUp = float.MinValue, furthestAlong = float.MinValue;
                foreach (Vector3 p in points)
                {
                    if ((p.x < 0.0f) != left)
                    {
                        continue;
                    }
                    furthestUp = Mathf.Max(furthestUp, Vector3.Dot(p - wrist, up));
                    if (Vector3.Distance(p, root) < 0.08f)
                    {
                        furthestAlong = Mathf.Max(furthestAlong, Vector3.Dot(p - root, along));
                    }
                }
                Assert.AreEqual(GloveShell.CuffLength, furthestUp, 0.01f, "the cuff does not end a hand's length up the arm");
                float first = Vector3.Distance(root, knuckle);
                Assert.Less(furthestAlong, first * (GloveShell.FingerCover + 0.15f), "the middle finger is covered too far: a glove with fingers");
                Assert.Greater(furthestAlong, first * (GloveShell.FingerCover - 0.15f), "the middle finger is hardly covered");
            }
        }

        [Test]
        public void TheLeatherStandsJustOffTheSkin()
        {
            Vector3[] skin = _skin.sharedMesh.vertices;
            var near = new Dictionary<Vector3Int, List<Vector3>>();
            foreach (Vector3 p in skin)
            {
                Vector3Int cell = Vector3Int.FloorToInt(p * 50.0f);
                if (!near.TryGetValue(cell, out List<Vector3> list))
                {
                    near[cell] = list = new List<Vector3>();
                }
                list.Add(p);
            }
            int far = 0;
            Vector3[] points = _gloves.vertices;
            for (int i = 0; i < points.Length; i += 7)
            {
                float closest = float.MaxValue;
                Vector3Int cell = Vector3Int.FloorToInt(points[i] * 50.0f);
                for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                for (int z = -1; z <= 1; z++)
                {
                    if (near.TryGetValue(cell + new Vector3Int(x, y, z), out List<Vector3> list))
                    {
                        foreach (Vector3 p in list)
                        {
                            closest = Mathf.Min(closest, Vector3.Distance(p, points[i]));
                        }
                    }
                }
                // To the skin's nearest POINT, which on the forearm can be a centimetre or two from where the leather is over it.
                far += closest > 0.025f ? 1 : 0;
            }
            Assert.AreEqual(0, far, "the gloves stand off the hands");
        }

        [Test]
        public void EveryColourwayIsAPairOfGlovesInTheGlovesSlotWithItsOwnColour()
        {
            var names = new HashSet<string>();
            foreach (FingerlessGloveColourways.Colourway colourway in FingerlessGloveColourways.All)
            {
                Assert.IsTrue(ItemRegistry.TryGet(colourway.ItemId, out ItemDefinition definition), colourway.ItemId);
                Assert.AreEqual(EquipSlots.Gloves, definition.EquipSlot);
                Assert.IsTrue(definition.IsWearable);
                Assert.AreEqual(colourway.DisplayName, definition.DisplayName);
                Assert.IsTrue(names.Add(colourway.Alias), $"two colourways are called {colourway.Alias}");
                Assert.AreEqual(colourway.ItemId, FingerlessGloveColourways.ItemIdForAlias(colourway.Alias));

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FingerlessGloveColourways.PrefabPath(colourway.ItemId));
                Assert.IsNotNull(prefab, $"{colourway.ItemId} has no prefab: run CityX/Rebuild Fingerless Gloves");
                var gloves = prefab.GetComponent<FingerlessGloves>();
                Assert.IsNotNull(gloves);
                Assert.IsNotNull(gloves.Material);
                Color made = gloves.Material.GetColor("_BaseColor");
                Assert.Less(Mathf.Abs(made.r - colourway.Colour.r) + Mathf.Abs(made.g - colourway.Colour.g) + Mathf.Abs(made.b - colourway.Colour.b), 0.01f,
                    $"{colourway.ItemId} is not its colour");
                Assert.IsNotNull(prefab.GetComponentInChildren<MeshFilter>().sharedMesh, "nothing lies on the ground");
            }
            Assert.AreEqual(FingerlessGloveColourways.BlackItemId, FingerlessGloveColourways.ItemIdForAlias("gloves"));
        }
    }
}
