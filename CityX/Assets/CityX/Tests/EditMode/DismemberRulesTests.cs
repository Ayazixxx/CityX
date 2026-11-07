using System.Collections.Generic;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>When a killing blow takes a body apart, and which part (docs/DISMEMBERMENT.md), with no scene; and a skinned mesh cut by its bones.</summary>
    public sealed class DismemberRulesTests
    {
        /// <summary>A body standing at the origin, arms down, as BodyRig lays it out (near enough).</summary>
        private static readonly Dictionary<HumanBodyBones, Vector3> Standing = new Dictionary<HumanBodyBones, Vector3>
        {
            [HumanBodyBones.Hips] = new Vector3(0.0f, 1.2f, 0.0f),
            [HumanBodyBones.Spine] = new Vector3(0.0f, 1.45f, 0.0f),
            [HumanBodyBones.Chest] = new Vector3(0.0f, 1.7f, 0.0f),
            [HumanBodyBones.Neck] = new Vector3(0.0f, 2.05f, 0.0f),
            [HumanBodyBones.Head] = new Vector3(0.0f, 2.25f, 0.0f),
            [HumanBodyBones.LeftUpperArm] = new Vector3(-0.35f, 1.95f, 0.0f),
            [HumanBodyBones.LeftLowerArm] = new Vector3(-0.45f, 1.45f, 0.0f),
            [HumanBodyBones.LeftHand] = new Vector3(-0.55f, 0.95f, 0.0f),
            [HumanBodyBones.RightUpperArm] = new Vector3(0.35f, 1.95f, 0.0f),
            [HumanBodyBones.RightLowerArm] = new Vector3(0.45f, 1.45f, 0.0f),
            [HumanBodyBones.RightHand] = new Vector3(0.55f, 0.95f, 0.0f),
            [HumanBodyBones.LeftUpperLeg] = new Vector3(-0.15f, 1.15f, 0.0f),
            [HumanBodyBones.LeftLowerLeg] = new Vector3(-0.15f, 0.6f, 0.0f),
            [HumanBodyBones.LeftFoot] = new Vector3(-0.15f, 0.1f, 0.0f),
            [HumanBodyBones.RightUpperLeg] = new Vector3(0.15f, 1.15f, 0.0f),
            [HumanBodyBones.RightLowerLeg] = new Vector3(0.15f, 0.6f, 0.0f),
            [HumanBodyBones.RightFoot] = new Vector3(0.15f, 0.1f, 0.0f),
        };

        private static Vector3? Bone(HumanBodyBones bone) => Standing.TryGetValue(bone, out Vector3 at) ? at : (Vector3?)null;

        /// <summary>A killing blow on a body with <paramref name="healthLeft"/> that does <paramref name="overkill"/> more than that.</summary>
        private static Blow Killing(float healthLeft, float overkill, bool edged = false, bool bloodless = false)
        {
            return new Blow { Damage = healthLeft + overkill, HealthLeft = healthLeft, Edged = edged, Bloodless = bloodless, From = Vector3.back * 2.0f };
        }

        [Test]
        public void OnlyAKillingBlowThatDoesWayMoreThanWasLeftTakesAnythingOff()
        {
            Assert.AreEqual(Dismemberment.None, DismemberRules.Decide(Killing(20.0f, 19.0f, edged: true), 100.0f), "a blow under double what was left cut");
            Assert.AreEqual(Dismemberment.Sever, DismemberRules.Decide(Killing(20.0f, 20.0f, edged: true), 100.0f), "double what was left did not cut");
            Assert.AreEqual(Dismemberment.Burst, DismemberRules.Decide(Killing(20.0f, 20.0f), 100.0f));
            Assert.AreEqual(Dismemberment.None, DismemberRules.Decide(Killing(1.0f, DismemberRules.MinOverkill - 0.5f), 100.0f),
                "a tap on a body with almost nothing left burst it");
            Assert.AreEqual(Dismemberment.Burst, DismemberRules.Decide(Killing(1.0f, DismemberRules.MinOverkill), 100.0f));
            Assert.Greater(DismemberRules.OverkillNeeded(0.0f, 1000.0f), DismemberRules.OverkillNeeded(0.0f, 100.0f), "a tougher body needs a bigger blow");
        }

        [Test]
        public void AnEdgeCutsAndAnythingElseBurstsAndWaterOrFireTakesNothing()
        {
            Assert.AreEqual(Dismemberment.Sever, DismemberRules.Decide(Killing(10.0f, 80.0f, edged: true), 100.0f), "a sword's blow did not cut");
            Assert.AreEqual(Dismemberment.Burst, DismemberRules.Decide(Killing(10.0f, 80.0f), 100.0f), "a club's or a bullet's blow did not burst");
            Assert.AreEqual(Dismemberment.None, DismemberRules.Decide(Killing(10.0f, 80.0f, bloodless: true), 100.0f), "a jet of water took something off");
        }

        [Test]
        public void ACorpsesPartsTakeSoMuchAndOneTakesWhatHangsOffIt()
        {
            Assert.AreEqual(20.0f, DismemberRules.Toughness(BodyPart.Head, 100.0f), 1e-4f);
            Assert.AreEqual(15.0f, DismemberRules.Toughness(BodyPart.LeftLowerArm, 100.0f), 1e-4f);
            Assert.AreEqual(40.0f, DismemberRules.Toughness(BodyPart.Torso, 100.0f), 1e-4f);
            Assert.IsTrue(DismemberRules.Takes(BodyPart.LeftUpperArm, BodyPart.LeftLowerArm), "an arm off at the shoulder left its forearm");
            Assert.IsFalse(DismemberRules.Takes(BodyPart.LeftLowerArm, BodyPart.LeftUpperArm), "a forearm took the upper arm with it");
            Assert.IsFalse(DismemberRules.Takes(BodyPart.Head, BodyPart.LeftUpperArm));
            Assert.IsTrue(DismemberRules.Takes(BodyPart.Torso, BodyPart.RightLowerLeg), "the torso burst left a leg");
        }

        /// <summary>Armour holds a part on (owner, 2026-11-06, docs/ARMOUR.md): heavy three times as hard to take, light about 1.7.</summary>
        [Test]
        public void ArmourMakesAPartHarderToTakeOff()
        {
            Assert.AreEqual(3.0f, DismemberRules.ArmouredFactor(1.0f), 1e-4f);
            Assert.AreEqual(1.0f, DismemberRules.ArmouredFactor(0.0f), 1e-4f);
            Assert.AreEqual(60.0f, DismemberRules.Toughness(BodyPart.Head, 100.0f, 1.0f), 1e-4f, "a heavy helmet's head");
            Assert.AreEqual(Dismemberment.Sever, DismemberRules.Decide(Killing(20.0f, 20.0f, edged: true), 100.0f), "setup: double cuts bare");
            Assert.AreEqual(Dismemberment.None, DismemberRules.Decide(Killing(20.0f, 20.0f, edged: true), 100.0f, 1.0f), "double cut through heavy armour");
            Assert.AreEqual(Dismemberment.Sever, DismemberRules.Decide(Killing(20.0f, 60.0f, edged: true), 100.0f, 1.0f), "three times did not cut through heavy armour");
            float light = DismemberRules.ArmouredFactor(Items.ArmourRatings.LightShare);
            Assert.That(light, Is.InRange(1.5f, 2.0f));
        }

        [Test]
        public void EachSlotCoversItsParts()
        {
            Assert.IsTrue(DismemberRules.Covers(Items.EquipSlots.Helmet, BodyPart.Head));
            Assert.IsFalse(DismemberRules.Covers(Items.EquipSlots.Helmet, BodyPart.Torso));
            Assert.IsTrue(DismemberRules.Covers(Items.EquipSlots.Torso, BodyPart.LeftUpperArm));
            Assert.IsTrue(DismemberRules.Covers(Items.EquipSlots.Gloves, BodyPart.RightLowerArm));
            Assert.IsTrue(DismemberRules.Covers(Items.EquipSlots.Legs, BodyPart.LeftLowerLeg));
            Assert.IsTrue(DismemberRules.Covers(Items.EquipSlots.Shoes, BodyPart.RightLowerLeg));
            Assert.IsFalse(DismemberRules.Covers(Items.EquipSlots.Back, BodyPart.Torso), "a jetpack is no armour");
        }

        [Test]
        public void TheBlowIsReadFromWhatItsStrikerSaid()
        {
            var contact = new Vector3(1.0f, 2.0f, 3.0f);
            Blow blow;
            using (DamageContext.Cutting(true))
            using (new DamageContext.Scope(DamageKind.Physical, 0.0f, contact))
            {
                blow = Blow.Struck(30.0f, 10.0f, Vector3.back);
            }
            Assert.IsTrue(blow.Edged, "a blade's blow was not edged");
            Assert.IsFalse(blow.Bloodless);
            Assert.AreEqual(contact, blow.Contact);
            Assert.AreEqual(20.0f, blow.Overkill);

            using (DamageContext.WithoutBlood())
            {
                blow = Blow.Struck(30.0f, 10.0f, Vector3.back);
            }
            Assert.IsFalse(blow.Edged, "the edge outlived its scope");
            Assert.IsTrue(blow.Bloodless);
            Assert.IsNull(blow.Contact, "the contact outlived its scope");
        }

        [Test]
        public void ThePartHitIsTheOneNearestTheBlow()
        {
            Assert.AreEqual(BodyPart.Head, DismemberRules.PartAt(new Vector3(0.05f, 2.35f, 0.1f), Bone));
            Assert.AreEqual(BodyPart.LeftLowerArm, DismemberRules.PartAt(new Vector3(-0.55f, 1.2f, 0.05f), Bone));
            Assert.AreEqual(BodyPart.RightUpperArm, DismemberRules.PartAt(new Vector3(0.45f, 1.75f, 0.05f), Bone));
            Assert.AreEqual(BodyPart.RightLowerLeg, DismemberRules.PartAt(new Vector3(0.2f, 0.4f, 0.1f), Bone));
            Assert.AreEqual(BodyPart.Torso, DismemberRules.PartAt(new Vector3(0.0f, 1.6f, 0.2f), Bone));
            Assert.AreNotEqual(BodyPart.Head, DismemberRules.PartAt(new Vector3(0.05f, 2.35f, 0.1f), Bone, part => part != BodyPart.Head),
                "a blow where the head was landed on a head already gone");
        }

        [Test]
        public void AnEdgeOnTheTorsoCutsAtTheNearestJoint()
        {
            Assert.AreEqual(BodyPart.Head, DismemberRules.CutAt(BodyPart.Torso, new Vector3(0.0f, 1.95f, 0.2f), Bone), "a slash at the neck");
            Assert.AreEqual(BodyPart.LeftUpperArm, DismemberRules.CutAt(BodyPart.Torso, new Vector3(-0.3f, 1.85f, 0.2f), Bone), "a slash at the shoulder");
            Assert.AreEqual(BodyPart.RightUpperLeg, DismemberRules.CutAt(BodyPart.Torso, new Vector3(0.15f, 1.2f, 0.2f), Bone), "a slash at the hip");
            Assert.AreEqual(BodyPart.LeftLowerArm, DismemberRules.CutAt(BodyPart.LeftLowerArm, Vector3.zero, Bone), "a limb hit is cut where it was hit");
            Assert.AreNotEqual(BodyPart.Head, DismemberRules.CutAt(BodyPart.Torso, new Vector3(0.0f, 1.95f, 0.2f), Bone, part => part != BodyPart.Head),
                "a slash at the neck of a body without a head cut the head");
            Assert.AreEqual(BodyPart.Torso, DismemberRules.CutAt(BodyPart.Torso, new Vector3(0.0f, 1.6f, 0.2f), Bone, part => part == BodyPart.Torso),
                "with nothing left on the torso, an edge has nothing to cut at");
        }

        /// <summary>A strip of four quads up the y axis, the bottom two rows on bone 0 and the top two on bone 1.</summary>
        private static Mesh Strip()
        {
            var vertices = new List<Vector3>();
            var weights = new List<BoneWeight>();
            for (int row = 0; row <= 4; row++)
            {
                for (int column = 0; column <= 1; column++)
                {
                    vertices.Add(new Vector3(column, row, 0.0f));
                    weights.Add(new BoneWeight { boneIndex0 = row >= 3 ? 1 : 0, weight0 = 1.0f });
                }
            }
            var triangles = new List<int>();
            for (int row = 0; row < 4; row++)
            {
                int a = row * 2, b = a + 1, c = a + 2, d = a + 3;
                triangles.AddRange(new[] { a, c, b, b, c, d });
            }
            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.boneWeights = weights.ToArray();
            return mesh;
        }

        [Test]
        public void ASkinnedMeshIsCutWhereItsBonesTakeOver()
        {
            Mesh strip = Strip();
            bool[] going = MeshSevering.Going(strip.boneWeights, new HashSet<int> { 1 });
            MeshSevering.Cut cut = MeshSevering.Split(strip, strip, going, Vector3.up);
            Assert.IsNotNull(cut);
            Assert.AreEqual(8, strip.triangles.Length / 3, "setup");
            Assert.AreEqual(3, cut.RemovedTriangles, "the top quad and the half of the next whose corners are mostly on the cut-off bone");
            Assert.AreEqual(5 * 3, cut.Kept.triangles.Length, "what stays is the rest");
            Assert.AreEqual(3 * 3, cut.Piece.triangles.Length);
            Assert.LessOrEqual(cut.Piece.vertexCount, 6, "the piece carries only the corners it uses");
            Assert.AreEqual(strip.vertexCount, cut.Kept.vertexCount, "what stays keeps every vertex, so it keeps its skinning");
            Assert.AreEqual(2.5f, cut.Centre.y, 0.6f, "the cut runs where the bones take over");
            Assert.Greater(cut.Radius, 0.0f);
            Assert.IsNull(MeshSevering.Split(strip, strip, new bool[strip.vertexCount], Vector3.up), "nothing goes: no cut");
        }

        [Test]
        public void AMeshIsCarvedIntoItsPartsAtOnce()
        {
            // Rows 0 and 1 in group 0, row 2 in group 1, rows 3 and 4 in group 2; group 3 has nothing.
            Mesh strip = Strip();
            var groupOf = new int[strip.vertexCount];
            for (int v = 0; v < groupOf.Length; v++)
            {
                int row = v / 2;
                groupOf[v] = row <= 1 ? 0 : row == 2 ? 1 : 2;
            }
            MeshSevering.Carving carving = MeshSevering.Carve(strip, strip, groupOf, 4);
            Assert.AreEqual(3 * 3, carving.Pieces[0].triangles.Length, "the bottom quad and half the next, whose corners are mostly in group 0");
            Assert.AreEqual(2 * 3, carving.Pieces[1].triangles.Length);
            Assert.AreEqual(3 * 3, carving.Pieces[2].triangles.Length);
            Assert.IsNull(carving.Pieces[3], "a group with no triangle has no piece");
            Assert.AreEqual(2.0f, carving.Centres[1].y, 0.6f, "the middle group's cut runs round its own rows");
            Assert.Greater(carving.Radii[1], 0.0f);
        }

        /// <summary>
        /// A limb's cut is measured across the limb: the stump's cap is as wide as the limb, however long the triangles the cut zigzags along
        /// (owner, 2026-11-07: the cap was "way larger than the circumference of the limb").
        /// </summary>
        [Test]
        public void ACutIsAsWideAsTheLimbHoweverLongItsTriangles()
        {
            const float radius = 0.08f;
            const float rowSpacing = 0.15f;   // nearly twice the radius: a plain distance from the middle came out 1.4x the radius
            Mesh tube = Tube(radius, rowSpacing);
            MeshSevering.Cut cut = MeshSevering.Split(tube, tube, MeshSevering.Going(tube.boneWeights, new HashSet<int> { 1 }), Vector3.up);
            Assert.AreEqual(radius, cut.Radius, radius * 0.02f, "the cut's radius is not the limb's");
            Assert.AreEqual(2.5f * rowSpacing, cut.Centre.y, rowSpacing * 0.5f, "the cut is not where the bones take over");

            var groupOf = new int[tube.vertexCount];
            for (int v = 0; v < groupOf.Length; v++)
            {
                groupOf[v] = tube.boneWeights[v].boneIndex0;
            }
            MeshSevering.Carving carving = MeshSevering.Carve(tube, tube, groupOf, 2, new[] { Vector3.zero, Vector3.up });
            Assert.AreEqual(radius, carving.Radii[1], radius * 0.02f, "a carved piece's cut is not as wide as the limb");
        }

        /// <summary>A closed tube up the y axis, five rows of twelve corners, the bottom three rows on bone 0 and the top two on bone 1.</summary>
        private static Mesh Tube(float radius, float rowSpacing)
        {
            const int around = 12;
            var vertices = new List<Vector3>();
            var weights = new List<BoneWeight>();
            for (int row = 0; row <= 4; row++)
            {
                for (int i = 0; i < around; i++)
                {
                    float angle = i * Mathf.PI * 2.0f / around;
                    vertices.Add(new Vector3(Mathf.Cos(angle) * radius, row * rowSpacing, Mathf.Sin(angle) * radius));
                    weights.Add(new BoneWeight { boneIndex0 = row >= 3 ? 1 : 0, weight0 = 1.0f });
                }
            }
            var triangles = new List<int>();
            for (int row = 0; row < 4; row++)
            {
                for (int i = 0; i < around; i++)
                {
                    int a = row * around + i, b = row * around + (i + 1) % around, c = a + around, d = b + around;
                    triangles.AddRange(new[] { a, c, b, b, c, d });
                }
            }
            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.boneWeights = weights.ToArray();
            return mesh;
        }
    }
}
