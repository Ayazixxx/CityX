using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// What a burst throws (docs/DISMEMBERMENT.md, owner 2026-11-05: "make them more human like"): pieces of the body itself, its own skin and
    /// skull outside and meat behind, with what was inside it (the eyes and jaw of a head, the bone of a forearm, the ribs and heart of a trunk);
    /// a whole body throws its head and the ends of its limbs as themselves; the stump of a burst limb shows the broken bone; the same seed
    /// throws the same pieces on every screen; and the pieces come down, smear the ground and lie still.
    /// </summary>
    public sealed partial class DismembermentPlayTests
    {
        private static List<GoreChunk> Pieces(float fromX = float.MinValue, float toX = float.MaxValue)
        {
            var pieces = new List<GoreChunk>();
            foreach (GoreChunk piece in Object.FindObjectsByType<GoreChunk>(FindObjectsSortMode.None))
            {
                float x = piece.transform.position.x;
                if (x >= fromX && x <= toX)
                {
                    pieces.Add(piece);
                }
            }
            return pieces;
        }

        /// <summary>Pieces that carry a patch of the named surface (the skin, the skull) with meat behind it.</summary>
        private static int PiecesOf(string surface, out Material drawnWith)
        {
            int count = 0;
            drawnWith = null;
            foreach (GoreChunk piece in Pieces())
            {
                Transform own = piece.transform.Find(surface);
                // A head or limb end flying off whole carries its skin with a stump, not meat.
                if (own == null || piece.GetComponentInChildren<StumpBleed>() != null)
                {
                    continue;
                }
                Assert.IsNotNull(piece.transform.Find("Meat"), $"a piece of the {surface} has no meat behind it");
                drawnWith = own.GetComponent<MeshRenderer>().sharedMaterial;
                count++;
            }
            return count;
        }

        [UnityTest]
        public IEnumerator ABurstHeadBreaksIntoPiecesOfItselfWithItsEyesAndJaw()
        {
            GameObject body = Body(Vector3.zero);
            yield return Stand(body);
            Material skull = Skull(body).sharedMaterial;
            Kill(body, BonePoint(body, HumanBodyBones.Neck, HumanBodyBones.Head, 1.4f), 60.0f, edged: false);
            Assert.GreaterOrEqual(PiecesOf(BodySurface.HeadName, out Material drawnWith), 4, "the head did not break into pieces of itself");
            Assert.AreEqual(skull, drawnWith, "a piece of the head is not drawn as the head was");
            Assert.IsNotNull(GameObject.Find("GoreEye"), "no eye flew out of the head");
            Assert.IsNotNull(GameObject.Find("GoreJaw"), "no jaw flew out of the head");
        }

        [UnityTest]
        public IEnumerator ABurstForearmBreaksIntoSkinAndMeatAndItsStumpShowsTheBrokenBone()
        {
            GameObject body = Body(Vector3.zero);
            yield return Stand(body);
            var dismemberer = body.GetComponent<Dismemberer>();
            Kill(body, BonePoint(body, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, 0.5f), 60.0f, edged: false);
            Assert.AreEqual(Dismemberment.Burst, dismemberer.Last);
            Assert.AreEqual(BodyPart.LeftLowerArm, dismemberer.LastPart);
            Assert.GreaterOrEqual(PiecesOf(BodySurface.SkinName, out _), 3, "the forearm did not break into pieces of its skin");
            Assert.IsNotNull(GameObject.Find("GoreForearm"), "no broken bone flew out of the forearm");
            Transform upperArm = body.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.LeftUpperArm);
            StumpBleed stump = upperArm.GetComponentInChildren<StumpBleed>();
            Assert.IsNotNull(stump, "no stump where the forearm was");
            Assert.IsNotNull(stump.transform.Find("BrokenBone"), "the stump of a burst forearm shows no broken bone");
        }

        [UnityTest]
        public IEnumerator AWholeBodyThrowsItsHeadAndLimbEndsAsThemselvesAndItsInsides()
        {
            GameObject body = Body(Vector3.zero);
            yield return Stand(body);
            Kill(body, BonePoint(body, HumanBodyBones.Hips, HumanBodyBones.Neck, 0.6f) + Vector3.forward * 0.15f, 120.0f, edged: false);
            var blownOff = new List<GameObject>();
            foreach (Debris debris in Object.FindObjectsByType<Debris>(FindObjectsSortMode.None))
            {
                if (debris.name.EndsWith("(blown off)"))
                {
                    blownOff.Add(debris.gameObject);
                }
            }
            Assert.AreEqual(5, blownOff.Count, "a whole body throws its head and one end of each limb");
            foreach (GameObject piece in blownOff)
            {
                Assert.Greater(piece.GetComponentsInChildren<MeshRenderer>().Length, 1, $"{piece.name} is invisible");
                Assert.IsNotNull(piece.GetComponentInChildren<StumpBleed>(), $"{piece.name} has no stump where it came off");
                Assert.IsNotNull(piece.GetComponent<Rigidbody>(), $"{piece.name} does not fly");
            }
            Assert.IsNotNull(GameObject.Find("Head (blown off)"), "the head did not fly off as itself");
            Assert.GreaterOrEqual(PiecesOf(BodySurface.SkinName, out _), 8, "the trunk did not break into pieces of its skin");
            foreach (string inside in new[] { "GoreRibSide", "GoreSpine", "GoreHeart", "GoreLung", "GoreGuts" })
            {
                Assert.IsNotNull(GameObject.Find(inside), $"no {inside} came out of the trunk");
            }
        }

        [UnityTest]
        public IEnumerator TheSameSeedThrowsTheSamePiecesOnEveryScreen()
        {
            GameObject here = Body(Vector3.zero);
            GameObject there = Body(new Vector3(40.0f, 0.0f, 0.0f));
            yield return Stand(here);
            yield return Stand(there);
            here.GetComponent<Dismemberer>().Show(Dismemberment.Burst, BodyPart.Head, Vector3.forward, 4321);
            there.GetComponent<Dismemberer>().Show(Dismemberment.Burst, BodyPart.Head, Vector3.forward, 4321);
            List<Vector3> mine = Offsets(Pieces(-20.0f, 20.0f), here.transform.position);
            List<Vector3> theirs = Offsets(Pieces(20.0f, 60.0f), there.transform.position);
            Assert.AreEqual(mine.Count, theirs.Count, "the two screens threw a different number of pieces");
            for (int i = 0; i < mine.Count; i++)
            {
                Assert.Less(Vector3.Distance(mine[i], theirs[i]), 0.01f, $"piece {i} is not where the other screen threw it");
            }
        }

        /// <summary>Where pieces are from a body, in a fixed order (objects found come in no order).</summary>
        private static List<Vector3> Offsets(List<GoreChunk> pieces, Vector3 from)
        {
            var offsets = new List<Vector3>();
            foreach (GoreChunk piece in pieces)
            {
                offsets.Add(piece.transform.position - from);
            }
            offsets.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y != b.y ? a.y.CompareTo(b.y) : a.z.CompareTo(b.z));
            return offsets;
        }

        /// <summary>
        /// Every piece of a whole body's burst stays where numbers are, flying and landing: one gone to infinity is drawn as an infinite brightness that the bloom spreads
        /// into a flash (owner, 2026-11-05: "a bunch of flashing lights coming from them ... like a flashlight type brightness").
        /// </summary>
        [UnityTest]
        public IEnumerator EveryPieceOfABurstStaysWhereNumbersAre()
        {
            GameObject body = Body(Vector3.zero);
            yield return Stand(body);
            // A light in its hand, as a flashlight's beam rides it.
            var torch = new GameObject("TestTorch");
            torch.transform.SetParent(body.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.LeftHand), false);
            torch.AddComponent<Light>().type = LightType.Spot;
            Kill(body, BonePoint(body, HumanBodyBones.Hips, HumanBodyBones.Neck, 0.6f) + Vector3.forward * 0.15f, 120.0f, edged: false);
            for (float t = 0.0f; t < 4.0f; t += Time.deltaTime)
            {
                yield return null;
                foreach (Rigidbody piece in Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None))
                {
                    // The burst's own: its pieces and the body (a pool parks spare glass far below, out of the way).
                    if (piece.GetComponent<Debris>() == null && piece.GetComponent<GoreChunk>() == null && !piece.transform.IsChildOf(body.transform))
                    {
                        continue;
                    }
                    Vector3 at = piece.position;
                    Assert.IsFalse(float.IsNaN(at.x + at.y + at.z) || float.IsInfinity(at.x + at.y + at.z) || at.magnitude > 1000.0f,
                        $"{piece.name} went off to {at} at {t:0.00} s");
                    if (!piece.isKinematic)
                    {
                        Assert.Less(piece.angularVelocity.magnitude, 200.0f, $"{piece.name} spun at {piece.angularVelocity.magnitude:0} rad/s at {t:0.00} s");
                    }
                }
                foreach (Light light in body.GetComponentsInChildren<Light>())
                {
                    Assert.IsFalse(light.enabled, $"{light.name} still shines from the burst body at {t:0.00} s");
                }
                foreach (Renderer drawn in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                {
                    if (drawn.GetComponentInParent<Debris>() == null && drawn.GetComponentInParent<GoreChunk>() == null && !drawn.transform.IsChildOf(body.transform))
                    {
                        continue;
                    }
                    Vector3 middle = drawn.bounds.center;
                    Assert.IsFalse(float.IsNaN(middle.x + middle.y + middle.z) || float.IsInfinity(middle.x + middle.y + middle.z),
                        $"{drawn.name} is drawn at {middle} at {t:0.00} s");
                }
            }
        }

        [UnityTest]
        public IEnumerator ThePiecesComeDownSmearTheGroundAndLieStill()
        {
            GameObject body = Body(Vector3.zero);
            yield return Stand(body);
            int smears = BloodSplats.Dropped;
            Kill(body, BonePoint(body, HumanBodyBones.Neck, HumanBodyBones.Head, 1.4f), 60.0f, edged: false);
            List<GoreChunk> pieces = Pieces();
            yield return Wait(5.0f);
            int landed = 0, resting = 0;
            foreach (GoreChunk piece in pieces)
            {
                if (piece == null)
                {
                    continue;
                }
                landed += piece.Landed ? 1 : 0;
                resting += piece.Resting ? 1 : 0;
                if (piece.Resting)
                {
                    Assert.IsTrue(piece.GetComponent<Rigidbody>().isKinematic, $"{piece.name} lies still but is still simulated");
                }
            }
            Assert.Greater(landed, pieces.Count / 2, "most pieces never came down");
            Assert.Greater(resting, pieces.Count / 2, "most pieces never came to rest");
            Assert.Greater(BloodSplats.Dropped, smears, "no piece left a smear where it landed");
            Assert.LessOrEqual(BloodSplats.Dropped - smears, Gore.PartSmears, "every piece smeared the ground: a burst may leave only a few");
        }
    }
}
