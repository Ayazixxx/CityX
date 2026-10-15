using CityX.Character;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// SELF SMITE, THE PURE PARTS (owner, 2026-10-15: Pikachu's Thunder, "time it and make it function exactly like it"; the bolt from cloud
    /// height; the hands thrown down as it lands; on the bar, in the Electromancer tree). The timeline against Thunder's frame data, the clip's
    /// speed, the bolt's fall and shape, the cloud layer, the throw, and the skill map.
    /// </summary>
    public sealed class SelfSmiteTests
    {
        private const float Frame = 1.0f / 60.0f;

        [Test]
        public void TheTimelineIsThundersFrameData()
        {
            Assert.AreEqual(1 * Frame, SelfSmiteMove.CloudSeconds, 1e-5f, "the cloud forms on frame 2");
            Assert.AreEqual(12 * Frame, SelfSmiteMove.BoltSeconds, 1e-5f, "the bolt leaves the cloud on frame 13");
            Assert.AreEqual(0.55f, SelfSmiteMove.StrikeSeconds, 1e-5f, "the bolt reaches the caster at the end of frame 33");
            Assert.AreEqual(36 * Frame, SelfSmiteMove.BlastEndSeconds, 1e-5f, "the burst hurts on frames 34 to 36");
            Assert.AreEqual(43 * Frame, SelfSmiteMove.IntangibleEndSeconds, 1e-5f, "intangible to frame 43");
            Assert.AreEqual(74 * Frame, SelfSmiteMove.FreeSeconds, 1e-5f, "free to act on frame 75");

            Assert.IsFalse(SelfSmiteMove.BlastHurts(SelfSmiteMove.StrikeSeconds - Frame));
            Assert.IsTrue(SelfSmiteMove.BlastHurts(SelfSmiteMove.StrikeSeconds + Frame * 0.5f));
            Assert.IsTrue(SelfSmiteMove.BlastHurts(SelfSmiteMove.StrikeSeconds + Frame * 2.5f));
            Assert.IsFalse(SelfSmiteMove.BlastHurts(SelfSmiteMove.StrikeSeconds + Frame * 3.5f));
            Assert.IsFalse(SelfSmiteMove.IsIntangible(0.5f), "the wind-up can be hit, as Thunder's can");
            Assert.IsTrue(SelfSmiteMove.IsIntangible(0.6f));
            Assert.IsFalse(SelfSmiteMove.IsIntangible(0.72f));
            Assert.IsTrue(SelfSmiteMove.HoldsTheCaster(1.2f));
            Assert.IsFalse(SelfSmiteMove.HoldsTheCaster(1.24f));
        }

        [Test]
        public void TheHandsAreThrownDownAsTheBoltLandsAndTheClipEndsAboutWhenTheCasterIsFree()
        {
            Assert.AreEqual(SelfSmiteMove.StrikeSeconds, SelfSmiteMove.ClipHandsDownSeconds / SelfSmiteMove.ClipSpeed, 1e-4f);
            Assert.That(SelfSmiteMove.ClipSpeed, Is.InRange(1.5f, 3.0f), "a sensible speed-up of the owner's clip");
            float clipEnds = SelfSmiteMove.ClipSeconds / SelfSmiteMove.ClipSpeed;
            Assert.AreEqual(SelfSmiteMove.FreeSeconds, clipEnds, 0.1f, "the clip should end about when the caster is free");
        }

        [Test]
        public void TheBoltFallsFromTheCloudsAndIsSeenComingOverItsLastFrames()
        {
            const float cloud = 1500.0f;
            Assert.AreEqual(cloud, SelfSmiteMove.TipHeightAbove(0.1f, cloud), 1e-3f, "still in the cloud before frame 13");
            Assert.AreEqual(0.0f, SelfSmiteMove.TipHeightAbove(SelfSmiteMove.StrikeSeconds, cloud), 1e-3f, "on the caster at the strike");
            float last = cloud;
            int framesInView = 0;
            for (float t = SelfSmiteMove.BoltSeconds; t <= SelfSmiteMove.StrikeSeconds + 1e-4f; t += Frame)
            {
                float tip = SelfSmiteMove.TipHeightAbove(t, cloud);
                Assert.LessOrEqual(tip, last + 1e-3f, $"the bolt went back up at {t:0.000} s");
                last = tip;
                if (tip > 0.0f && tip < 50.0f)
                {
                    framesInView++;
                }
            }
            Assert.GreaterOrEqual(framesInView, 7, "the last 50 m (what the camera behind the caster sees) went by too fast to see");
        }

        [Test]
        public void TheCloudIsAKilometreAndAHalfUpAndOverAnyoneFlyingThatHigh()
        {
            Assert.AreEqual(1500.0f, CloudLayer.BaseOver(0.0f, 1.0f), 1e-3f);
            Assert.AreEqual(1620.0f, CloudLayer.BaseOver(120.0f, 160.0f), 1e-3f, "measured from the ground under, not the sea");
            Assert.AreEqual(1400.0f + 1500.0f, CloudLayer.BaseOver(0.0f, 1400.0f), 1e-3f, "flying near the clouds puts them overhead again");
            for (float y = 0.0f; y < 6000.0f; y += 7.0f)
            {
                Assert.GreaterOrEqual(CloudLayer.BaseOver(0.0f, y) - y, CloudLayer.MinClearanceMetres - 1e-3f, $"the cloud too close over {y} m");
            }
        }

        [Test]
        public void TheBoltRunsFromTheCasterToTheCloudJaggedButNeverWandersOff()
        {
            var points = new Vector3[SelfSmiteBoltShape.MaxPoints];
            Vector3 bottom = new Vector3(10.0f, 2.5f, -4.0f);
            Vector3 top = new Vector3(10.0f, 1502.5f, -4.0f);
            int count = SelfSmiteBoltShape.Trunk(bottom, top, 42, points);
            Assert.That(count, Is.InRange(15, SelfSmiteBoltShape.MaxPoints), "about two dozen points, finer at the bottom");
            Assert.AreEqual(bottom, points[0], "it ends exactly on the caster");
            Assert.AreEqual(top, points[count - 1], "it starts exactly in the cloud");
            Assert.LessOrEqual(points[1].y - points[0].y, SelfSmiteBoltShape.FirstStepMetres + 1e-3f, "the bottom is jagged finest: the camera is right there");
            for (int i = 1; i < count; i++)
            {
                float rise = points[i].y - points[i - 1].y;
                Assert.Greater(rise, 0.0f, $"point {i} is not above the one before");
                float stray = new Vector2(points[i].x - bottom.x, points[i].z - bottom.z).magnitude;
                Assert.LessOrEqual(stray, rise * SelfSmiteBoltShape.MaxRoughness * 1.5f + 1e-3f, $"point {i} wandered off");
            }

            var again = new Vector3[SelfSmiteBoltShape.MaxPoints];
            Assert.AreEqual(count, SelfSmiteBoltShape.Trunk(bottom, top, 42, again));
            CollectionAssert.AreEqual(points, again, "the same seed should draw the same bolt");
            SelfSmiteBoltShape.Trunk(bottom, top, 43, again);
            CollectionAssert.AreNotEqual(points, again, "another seed should draw another bolt");

            var fork = new Vector3[SelfSmiteBoltShape.ForkPoints];
            for (int f = 0; f < SelfSmiteBoltShape.Forks; f++)
            {
                int forkCount = SelfSmiteBoltShape.Fork(points, count, 42, f, fork);
                Assert.AreEqual(SelfSmiteBoltShape.ForkPoints, forkCount);
                Assert.IsTrue(System.Array.IndexOf(points, fork[0]) >= 0, "a fork starts on the trunk");
                Assert.Less(fork[forkCount - 1].y, fork[0].y, "a fork runs down");
                Assert.Greater(fork[forkCount - 1].y, bottom.y - 1.0f, "a fork stays above the ground the caster stands on");
            }
        }

        [Test]
        public void TheBoltIsDrawnWiderHigherUpSoItReadsAllTheWayToTheCloud()
        {
            Assert.AreEqual(SelfSmiteBoltShape.CoreWidthAtBottom, SelfSmiteBoltShape.WidthAt(0.0f), 1e-5f);
            Assert.Greater(SelfSmiteBoltShape.WidthAt(1500.0f), 5.0f, "a kilometre and a half away, a thin line would vanish");
        }

        [Test]
        public void TheBurstThrowsAwayAndUp()
        {
            Vector3 centre = new Vector3(0.0f, 1.3f, 0.0f);
            Vector3 launch = SelfSmiteMove.Launch(centre, new Vector3(3.0f, 0.0f, 0.0f));
            Assert.Greater(launch.x, 10.0f, "away from the caster");
            Assert.AreEqual(0.0f, launch.z, 1e-4f);
            Assert.Greater(launch.y, 5.0f, "and up");
            Assert.Greater(SelfSmiteMove.Launch(centre, centre).magnitude, 10.0f, "someone right on top still goes somewhere");
        }

        [Test]
        public void AThrowSentFromAnotherPlayersMachineIsKeptSane()
        {
            Vector3 burst = SelfSmiteMove.Launch(Vector3.zero, Vector3.right);
            Assert.AreEqual(burst, NetworkedDamage.SaneKnockdown(burst), "a real burst's throw goes through untouched");
            Assert.LessOrEqual(NetworkedDamage.SaneKnockdown(new Vector3(0.0f, 5000.0f, 0.0f)).magnitude, NetworkedDamage.MaxKnockdownSpeed + 1e-3f,
                "a forged throw into orbit is slowed down");
            Assert.AreEqual(Vector3.zero, NetworkedDamage.SaneKnockdown(new Vector3(float.NaN, 1.0f, 0.0f)), "a garbled throw does nothing");
        }

        [Test]
        public void SelfSmiteIsASpellInTheElectromancerTreeOnKeyNine()
        {
            SkillNode node = SkillCatalog.Find(SkillCatalog.SelfSmite);
            Assert.IsNotNull(node);
            Assert.AreEqual("Self Smite", node.Name);
            Assert.AreEqual(SkillKind.Spell, node.Kind);
            Assert.IsTrue(node.GoesOnBar, "a hotbar spell (owner)");
            Assert.IsEmpty(node.Requires, "the Electromancer tree's root");
            SkillTree tree = SkillCatalog.FindTree(node.TreeId);
            Assert.AreEqual("Electromancer", tree.Name, "spelt as the owner spells it");
            Assert.IsTrue(tree.InGame);
            Assert.AreEqual(8, System.Array.IndexOf(SkillCatalog.DefaultBar, SkillCatalog.SelfSmite), "on key 9 out of the box");
            Assert.AreEqual(SelfSmiteAbility.Id, SkillCatalog.SelfSmite, "the skill and the ability share an id");
        }

        [Test]
        public void ASkillWithNoIconYetShowsItsFirstLetter()
        {
            Assert.AreEqual("S", SkillIcons.LetterFor(SkillCatalog.SelfSmite));
            Assert.IsNull(SkillIcons.LetterFor(SkillCatalog.Fireball), "the fireball has a drawn icon");
            Assert.IsNull(SkillIcons.LetterFor("electromancer-1"), "an empty star keeps its question mark");
            Assert.IsNull(SkillIcons.LetterFor(null));
        }
    }
}
