using System.Collections.Generic;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The unarmed strikes on paper, Souls-style (docs/FIST_COMBAT.md, docs/MELEE_COMBOS.md), and the full-body clips that throw them (made in
    /// Blender, CityX/SourceArt/FistCombo), read the way the skeleton plays them: every blow starts and ends standing ready, has its fist at
    /// full reach on the frame it LANDS, turns the body into it from the hips up with the head kept on what is ahead, strides with the lead
    /// foot and leaves the rear foot where it stood while the game carries the body past it.
    /// </summary>
    public sealed class FistStrikesTests
    {
        private static readonly FistKind[] All = { FistKind.RightStraight, FistKind.LeftStraight, FistKind.Overhand, FistKind.Uppercut };

        private static BakedMotion Motion()
        {
            BakedMotion motion = FistStrikes.Motion;
            Assert.IsNotNull(motion, "the punches' clips are missing -- build them from Blender (CityX/SourceArt/FistCombo/fist_build.py)");
            return motion;
        }

        /// <summary>The frame of a blow's clip <paramref name="share"/> of the way through it.</summary>
        private static int Frame(BakedMotion motion, FistKind kind, float share)
        {
            string name = FistStrikes.NameOf(kind);
            return motion.SegmentStart(name) + Mathf.Min(motion.SegmentFrames(name) - 1, Mathf.RoundToInt(share * motion.SegmentFrames(name)));
        }

        private static Dictionary<string, Vector3> Joints(BakedMotion motion, FistKind kind, float share, out Dictionary<string, Quaternion> turns)
        {
            int frame = Frame(motion, kind, share);
            turns = BakedMotionPoses.WorldTurns(motion, frame);
            return BakedMotionPoses.Joints(motion, frame, turns);
        }

        private static Dictionary<string, Vector3> Joints(BakedMotion motion, FistKind kind, float share)
        {
            return Joints(motion, kind, share, out _);
        }

        /// <summary>The side the blow is thrown from, and the other one: "r" and "l" for all but the left straight.</summary>
        private static string Thrower(FistKind kind) => FistStrikes.UsesRightHand(kind) ? "r" : "l";

        private static string Other(FistKind kind) => FistStrikes.UsesRightHand(kind) ? "l" : "r";

        /// <summary>Which way a right-handed blow goes, as a sign on x: a left-handed one is its mirror.</summary>
        private static float Hand(FistKind kind) => FistStrikes.UsesRightHand(kind) ? 1.0f : -1.0f;

        private static float ArmLength(Dictionary<string, Vector3> joints, string side)
        {
            return Vector3.Distance(joints["arm_" + side], joints["forearm_" + side]) + Vector3.Distance(joints["forearm_" + side], joints["hand_" + side]);
        }

        /// <summary>How far to the right a bone faces, degrees (0 straight ahead).</summary>
        private static float YawOf(Quaternion turn)
        {
            Vector3 facing = Vector3.ProjectOnPlane(turn * Vector3.forward, Vector3.up);
            return Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg;
        }

        // ---- the clips ----

        [Test]
        public void TheClipsAreOnTheGamesOwnBonesAndEachIsAsLongAsItsBlow()
        {
            BakedMotion motion = Motion();
            CollectionAssert.AreEquivalent(BakedMotionPoses.BodyBonesWithoutFingers(), motion.Bones, "the clips' bones are not BodyRig's");
            Assert.AreEqual(BakedMotion.RootBone, motion.Bones[0]);
            foreach (FistKind kind in All)
            {
                string name = FistStrikes.NameOf(kind);
                Assert.IsTrue(motion.HasSegment(name), $"no clip for {kind}");
                Assert.AreEqual(FistStrikes.Seconds(kind), motion.SegmentSeconds(name), 1e-4f, $"{kind}'s clip is not as long as the blow: it would land on another frame");
            }
        }

        [Test]
        public void EveryFrameIsAWholePoseAndNothingSnaps()
        {
            BakedMotion motion = Motion();
            for (int f = 0; f < motion.FrameCount; f++)
            {
                for (int b = 0; b < motion.Bones.Count; b++)
                {
                    Quaternion q = motion.RotationAt(f, b);
                    float norm = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
                    Assert.AreEqual(1.0f, norm, 1e-3f, $"frame {f}, {motion.Bones[b]}");
                }
                Assert.Less(motion.HipsAt(f).magnitude, 1.5f, $"frame {f}: the hips are nowhere near the body");
            }
            foreach (FistKind kind in All)
            {
                string name = FistStrikes.NameOf(kind);
                int first = motion.SegmentStart(name);
                float worst = BakedMotionPoses.WorstStep(motion, first, first + motion.SegmentFrames(name) - 1, false, out string where);
                // A fist going out turns its arm some forty degrees in a frame; an elbow flipping over turns it a hundred and more.
                Assert.Less(worst, 75.0f, $"{kind}: {where} turns {worst:0} degrees in one frame");
            }
        }

        [Test]
        public void EveryBlowStartsAndEndsStandingReady()
        {
            BakedMotion motion = Motion();
            foreach (FistKind kind in All)
            {
                foreach (float share in new[] { 0.0f, 1.0f })
                {
                    Dictionary<string, Vector3> joints = Joints(motion, kind, share, out Dictionary<string, Quaternion> turns);
                    string when = share <= 0.0f ? "starts" : "ends";
                    Assert.AreEqual(BodyRig.HipY - FistStrikes.ReadyHipDrop, joints["hips"].y, 0.02f, $"{kind} {when} with the hips somewhere else than the ready stance has them");
                    Assert.Less(new Vector2(joints["hips"].x, joints["hips"].z).magnitude, 0.03f, $"{kind} {when} with the hips off its feet");
                    Assert.Less(Mathf.Abs(YawOf(turns["spine"])), 8.0f, $"{kind} {when} with the body still turned");
                    foreach (string side in new[] { "l", "r" })
                    {
                        Assert.Less(joints["hand_" + side].y, joints["arm_" + side].y - 0.6f, $"{kind} {when} with a hand up: the walk's arms hang");
                        Assert.Less(Mathf.Abs(joints["foot_" + side].z), 0.12f, $"{kind} {when} with a foot out: the next blow starts from square");
                    }
                }
            }
        }

        [Test]
        public void TheFistIsAtFullReachAheadOfTheBodyOnTheFrameItLands()
        {
            BakedMotion motion = Motion();
            foreach (FistKind kind in All)
            {
                string side = Thrower(kind);
                Dictionary<string, Vector3> joints = Joints(motion, kind, FistStrikes.ContactFraction(kind));
                float reach = Vector3.Distance(joints["arm_" + side], joints["hand_" + side]) / ArmLength(joints, side);
                if (kind == FistKind.Uppercut)
                {
                    // Driven up under a chin, the elbow under the fist: bent, and the fist above its own shoulder.
                    Assert.Greater(joints["hand_" + side].y, joints["arm_" + side].y + 0.08f, "the uppercut lands below the shoulder: it is no uppercut");
                    Assert.Less(joints["forearm_" + side].y, joints["hand_" + side].y - 0.15f, "the uppercut's elbow is not under its fist");
                    Assert.Greater(joints["hand_" + side].z, 0.45f, "the uppercut lands against its own chest");
                }
                else
                {
                    Assert.Greater(reach, 0.97f, $"{kind} lands with the arm bent ({reach:0.00} of its length)");
                    Assert.Greater(joints["hand_" + side].z, 1.0f, $"{kind} lands beside the body, not ahead of it");
                }
                // The other fist is not left hanging: it is snatched back to the ribs, or thrown out behind.
                Assert.Greater(Vector3.Distance(joints["hand_" + Other(kind)], joints["arm_" + Other(kind)]), 0.25f, $"{kind}: the other hand is in its own shoulder");
            }
        }

        [Test]
        public void TheStraightIsDrawnBackFirstTheOverhandComesDownFromHighAndTheUppercutRisesFromLow()
        {
            BakedMotion motion = Motion();
            Dictionary<string, Vector3> wound = Joints(motion, FistKind.RightStraight, 0.22f);
            // Behind the middle of the body, the elbow further back still. (Its own shoulder is wound back too: it is not the measure.)
            Assert.Less(wound["hand_r"].z, wound["hips"].z - 0.1f, "the straight is not drawn back before it goes out");
            Assert.Less(wound["forearm_r"].z, wound["hand_r"].z - 0.1f, "the straight is drawn back with the elbow in front of the fist");
            Assert.Less(wound["hand_r"].y, wound["arm_r"].y - 0.2f, "the straight is wound up at the shoulder, not at the ribs");

            Dictionary<string, Vector3> high = Joints(motion, FistKind.Overhand, 0.45f);
            Dictionary<string, Vector3> down = Joints(motion, FistKind.Overhand, FistStrikes.ContactFraction(FistKind.Overhand));
            Assert.Greater(high["hand_r"].y, high["arm_r"].y + 0.3f, "the overhand is not wound up high: it has no tell");
            Assert.Less(down["hand_r"].y, down["arm_r"].y, "the overhand lands at shoulder height: it reads as a straight");

            Dictionary<string, Vector3> low = Joints(motion, FistKind.Uppercut, 0.26f);
            Dictionary<string, Vector3> up = Joints(motion, FistKind.Uppercut, FistStrikes.ContactFraction(FistKind.Uppercut));
            Assert.Less(low["hand_r"].y, low["hips"].y + 0.4f, "the uppercut does not drop its fist first");
            Assert.Less(low["hips"].y, BodyRig.HipY - 0.15f, "the uppercut does not dip");
            Assert.Greater(up["hand_r"].y - low["hand_r"].y, 0.8f, "the uppercut does not rise");
            Assert.Greater(up["hips"].y, low["hips"].y + 0.15f, "the uppercut's legs do not drive it up");
        }

        [Test]
        public void TheBodyTurnsIntoTheBlowFromTheHipsUpAndTheHeadStaysOnWhatIsAhead()
        {
            BakedMotion motion = Motion();
            foreach (FistKind kind in All)
            {
                float toward = -Hand(kind);   // a right-handed blow turns the body to the left
                Joints(motion, kind, FistStrikes.ContactFraction(kind), out Dictionary<string, Quaternion> landing);
                float hips = YawOf(landing["hips"]) * toward, chest = YawOf(landing["spine"]) * toward, head = YawOf(landing["head"]) * toward;
                Assert.Greater(hips, 15.0f, $"{kind}: the hips do not turn into the blow ({hips:0} degrees)");
                Assert.Greater(chest, hips + 8.0f, $"{kind}: the shoulders do not turn further than the hips ({chest:0} against {hips:0} degrees)");
                Assert.Less(Mathf.Abs(head), chest * 0.5f, $"{kind}: the head turns away with the shoulders ({head:0} of {chest:0} degrees)");

                // Wound up against the blow first: the shoulders turned the other way.
                float windUp = kind == FistKind.Overhand ? 0.45f : kind == FistKind.Uppercut ? 0.26f : 0.22f;
                Joints(motion, kind, windUp, out Dictionary<string, Quaternion> wound);
                Assert.Less(YawOf(wound["spine"]) * toward, -15.0f, $"{kind} is not wound up against itself before it goes");
            }
        }

        [Test]
        public void TheLeftStraightIsTheRightOneMirrored()
        {
            BakedMotion motion = Motion();
            foreach (float share in new[] { 0.0f, 0.22f, 0.46f, 0.8f })
            {
                Dictionary<string, Vector3> right = Joints(motion, FistKind.RightStraight, share);
                Dictionary<string, Vector3> left = Joints(motion, FistKind.LeftStraight, share);
                foreach (string joint in new[] { "hand", "forearm", "foot", "shin" })
                {
                    Vector3 mirrored = left[joint + "_l"];
                    mirrored.x = -mirrored.x;
                    Assert.Less(Vector3.Distance(right[joint + "_r"], mirrored), 0.02f, $"{share:0.00} of the way: the left straight's {joint} is not the right one's mirrored");
                }
            }
        }

        [Test]
        public void TheLeadFootStridesAndTheRearFootStaysWhereItStoodWhileTheBodyIsCarriedPastIt()
        {
            BakedMotion motion = Motion();
            foreach (FistKind kind in All)
            {
                string rear = "foot_" + Thrower(kind), lead = "foot_" + Other(kind);
                float seconds = FistStrikes.Seconds(kind), contact = FistStrikes.ContactFraction(kind);
                float rearStood = Joints(motion, kind, 0.0f)[rear].z;
                for (float share = 0.0f; share <= contact; share += 0.04f)
                {
                    // The ankle's place on the ground: where it is in the clip, and how far the game has carried the body by then.
                    float onTheGround = Joints(motion, kind, share)[rear].z + FistStrikes.Carried(kind, share * seconds);
                    // (Rolling up onto its ball, the ankle goes a few centimetres forward over toes that have not moved.)
                    Assert.AreEqual(rearStood, onTheGround, 0.07f, $"{kind}, {share:0.00} of the way: the rear foot slides {onTheGround - rearStood:0.00} m along the ground");
                }
                Dictionary<string, Vector3> landing = Joints(motion, kind, contact);
                float stride = landing[lead].z + FistStrikes.CarryMetres(kind);
                Assert.Greater(stride, FistStrikes.CarryMetres(kind) * 0.9f, $"{kind}: the lead foot has not stridden by the time the blow lands ({stride:0.00} m)");
                Assert.Greater(landing[lead].z - landing[rear].z, 0.25f, $"{kind} lands with the feet together");
                Assert.Less(landing[lead].y, 0.2f, $"{kind} lands with the lead foot still in the air");
            }
        }

        [Test]
        public void TheWeightGoesBackAsABlowIsWoundUpAndForwardAsItLands()
        {
            BakedMotion motion = Motion();
            foreach (FistKind kind in new[] { FistKind.RightStraight, FistKind.LeftStraight, FistKind.Overhand })
            {
                float windUp = kind == FistKind.Overhand ? 0.45f : 0.22f;
                Assert.Less(Joints(motion, kind, windUp)["hips"].z, -0.03f, $"{kind}: the weight does not go back as it is wound up");
                Dictionary<string, Vector3> landing = Joints(motion, kind, FistStrikes.ContactFraction(kind));
                Assert.Greater(landing["hips"].z, 0.03f, $"{kind}: the weight does not go forward as it lands");
                Assert.Greater(landing["spine"].z - landing["hips"].z + (landing["neck"].z - landing["spine"].z), 0.15f, $"{kind}: the body does not lean into the blow");
            }
        }

        // ---- what the game and the clips agree on ----

        [Test]
        public void TheBodyIsCarriedFromTheStrideToTheLandingAndNoFurther()
        {
            foreach (FistKind kind in All)
            {
                float seconds = FistStrikes.Seconds(kind), lands = seconds * FistStrikes.ContactFraction(kind);
                Assert.AreEqual(0.0f, FistStrikes.Carried(kind, 0.0f), 1e-5f, $"{kind} carries the body before it has begun");
                Assert.AreEqual(FistStrikes.CarryMetres(kind), FistStrikes.Carried(kind, lands), 1e-4f, $"{kind} has not carried the body its stride by the time it lands");
                Assert.AreEqual(FistStrikes.CarryMetres(kind), FistStrikes.Carried(kind, seconds), 1e-4f, $"{kind} goes on carrying the body after it has landed");
                float before = 0.0f;
                for (float t = 0.0f; t <= seconds; t += 0.01f)
                {
                    float carried = FistStrikes.Carried(kind, t);
                    Assert.GreaterOrEqual(carried, before - 1e-5f, $"{kind} carries the body back at {t:0.00} s");
                    before = carried;
                }
                Assert.That(FistStrikes.CarryMetres(kind), Is.InRange(0.2f, 0.6f), $"{kind}: a stride, not a shuffle and not a leap (the leg is 0.77 m)");
            }
            // The overhand's long wind-up stands still: it is the tell.
            Assert.AreEqual(0.0f, FistStrikes.Carried(FistKind.Overhand, FistStrikes.Seconds(FistKind.Overhand) * 0.35f), 1e-5f, "the overhand creeps forward while it is wound up");
        }

        /// <summary>The stance places no hand (owner, 2026-11-03: placed, they sat on the hips with the elbows out): the arms stay the walk cycle's.</summary>
        [Test]
        public void TheReadyStanceIsALittleCrouchedWithTheFeetSquare()
        {
            FistFrame ready = FistStrikes.Ready();
            Assert.IsTrue(ready.Active, "the stance is not applied at all");
            Assert.Greater(ready.HipDrop, 0.0f, "the ready stance does not crouch at all");
            Assert.AreEqual(0.0f, ready.StanceLead, 0.001f, "the ready stance is not square");
            Assert.Greater(ready.Footwork, 0.99f, "the feet are not planted in the ready stance");
        }

        [Test]
        public void ContactFallsInsideTheBlowAndTimingsAreSane()
        {
            foreach (FistKind kind in All)
            {
                float contact = FistStrikes.ContactFraction(kind);
                Assert.That(contact, Is.InRange(0.3f, 0.7f), $"{kind}");
                Assert.That(FistStrikes.Seconds(kind), Is.InRange(0.4f, 0.8f), $"{kind}: each punch is a commitment, not a snap");
                Assert.IsTrue(FistStrikes.Has(FistStrikes.NameOf(kind)), $"{kind} has no animation name");
            }
            Assert.Greater(FistStrikes.Seconds(FistKind.Overhand), FistStrikes.Seconds(FistKind.RightStraight), "the overhand's wind-up is no tell");
        }

        /// <summary>Souls' two-hit string: right, left, and the left closes the chain; the heavies are outside it.</summary>
        [Test]
        public void TheUnarmedChainIsARightThenALeftStraight()
        {
            ComboStage[] chain = ComboChain.For(ComboChain.Unarmed);
            Assert.AreEqual(2, chain.Length);
            Assert.AreEqual(FistStrikes.RightStraightName, chain[0].Animation);
            Assert.AreEqual(FistStrikes.LeftStraightName, chain[1].Animation);
            Assert.IsFalse(chain[0].IsFinisher);
            Assert.IsTrue(chain[1].IsFinisher, "the left straight does not close the chain");
            Assert.IsFalse(chain[1].KnocksDown, "a light knocks players down");
            Assert.AreEqual(FistStrikes.OverhandName, CombatTuning.HeavyPunch.Animation);
            Assert.IsTrue(CombatTuning.HeavyPunch.KnocksDown, "the overhand is no knockdown");
            Assert.AreEqual(FistStrikes.UppercutName, CombatTuning.UppercutPunch.Animation);
        }

        /// <summary>
        /// Two hits are guaranteed, the third is not (docs/MELEE_COMBOS.md): the first straight's stun outlasts the gap to the second's contact, and
        /// the second's does not outlast its own recovery.
        /// </summary>
        [Test]
        public void TwoPunchesAreGuaranteedAndTheThirdIsNot()
        {
            ComboStage[] chain = ComboChain.For(ComboChain.Unarmed);
            float first = FistStrikes.Seconds(FistKind.RightStraight);
            float second = FistStrikes.Seconds(FistKind.LeftStraight);
            float firstContact = first * FistStrikes.ContactFraction(FistKind.RightStraight);
            float earliestSecondStart = first * (1.0f - MeleeAttacker.ChainCancelFraction);
            float secondContact = earliestSecondStart + second * FistStrikes.ContactFraction(FistKind.LeftStraight);
            float gap = secondContact - firstContact;
            Assert.GreaterOrEqual(CombatTuning.HitstunFor(chain[0]), gap, $"the first straight's stun ({CombatTuning.HitstunFor(chain[0]):F2} s) does not cover the gap to the second ({gap:F2} s)");

            float secondRecovery = second * (1.0f - FistStrikes.ContactFraction(FistKind.LeftStraight));
            Assert.Less(CombatTuning.HitstunFor(chain[1]), secondRecovery, "the second straight's stun locks the target through the recovery: a third would be guaranteed");
        }

        /// <summary>The whole string does less than a sword's and costs stamina (the "too op" of 2026-11-03).</summary>
        [Test]
        public void TheFistsAreWeakerThanASwordAndCostStamina()
        {
            ComboStage[] chain = ComboChain.For(ComboChain.Unarmed);
            float total = 0.0f;
            foreach (ComboStage stage in chain)
            {
                total += CombatTuning.DamageFor(stage, 0);
                Assert.LessOrEqual(CombatTuning.DamageFor(stage, 0), CombatTuning.PunchDamage * 0.5f, $"{stage.Animation} hits as hard as a sword");
            }
            Assert.AreEqual(CombatTuning.PunchDamage, total, 0.001f, "the two straights together are one sword blow");
            Assert.AreEqual(CombatTuning.PunchDamage, CombatTuning.DamageFor(CombatTuning.HeavyPunch, 0), 0.001f, "the overhand is one sword blow");
            Assert.Greater(CombatTuning.PunchStaminaCost, 0.0f);
            Assert.Greater(CombatTuning.HeavyPunchStaminaCost, CombatTuning.PunchStaminaCost);
            Assert.Less(MeleeAttacker.FistReachMetres, 2.0f, "the fists reach as far as a sword");
        }
    }
}
