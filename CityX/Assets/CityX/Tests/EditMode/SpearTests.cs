using System.Linq;
using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CityX.Tests
{
    /// <summary>
    /// The spear's rules and data (owner, 2026-11-18: "it should be mostly thrusting stabs but some swinging as well maybe the heavy attacks are
    /// swings, also should be usable with shields"; docs/SPEAR.md): the lights are thrusts (the point goes out along the shaft's own line), the
    /// heavy is a sweep, the reach is the longest, each thrust quicker and lighter than a sword's cut, its clips are there, and the built spear.
    /// </summary>
    public sealed class SpearTests
    {
        private sealed class Blade : ISwordBlade
        {
            public bool Spear;

            public Vector3 BladeBaseWorld => Vector3.zero;

            public Vector3 BladeTipWorld => Vector3.up;

            public bool IsSpear => Spear;

            public float GetMeleeDamage() => SpearRules.BaseDamage;
        }

        private static readonly SwordStrikeKind[] Thrusts = { SwordStrikeKind.SpearJab, SwordStrikeKind.SpearThrust, SwordStrikeKind.SpearLunge, SwordStrikeKind.SpearRun };

        private const float ShaftAhead = 1.75f;   // SpearBuilder.Tip

        private static Vector3 Tip(SwordStrikeKind kind, float t01)
        {
            // In the body's own frame: the shoulder as the frame has it turned, the hand from it, the shaft from the hand.
            SwordFrame frame = SwordStrikes.Evaluate(kind, t01);
            return SwordStrikes.RightShoulder(frame.TwistDegrees) + SwordStrikes.TipOffset(frame, SwordStrikes.ArmMetres, ShaftAhead);
        }

        [Test]
        public void TheSpearsAttacksAreNamedAndReadBack()
        {
            foreach (SwordStrikeKind kind in Thrusts.Append(SwordStrikeKind.SpearSweep))
            {
                Assert.IsTrue(SwordStrikes.IsSpear(kind));
                Assert.IsTrue(SwordStrikes.TryGet(SwordStrikes.NameOf(kind), out SwordStrikeKind back) && back == kind, kind.ToString());
                CollectionAssert.Contains(SwordStrikes.All, kind);
                Assert.IsFalse(SwordStrikes.IsOneHanded(kind), "a spear's attack is posed with both hands on it when the other is free");
            }
            Assert.IsFalse(SwordStrikes.IsSpear(SwordStrikeKind.Sweep2H));
            Assert.IsFalse(SwordStrikes.IsSpear(SwordStrikeKind.Thrust1H), "the knife's stab is not a spear's");
            Assert.IsTrue(SwordStrikes.IsHeavy(SwordStrikeKind.SpearSweep));
            Assert.IsTrue(SwordStrikes.IsRunningAttack(SwordStrikeKind.SpearRun));
        }

        /// <summary>Owner: "mostly thrusting stabs". A thrust's point goes out the way the shaft points, on one straight line, and stops there.</summary>
        [Test]
        public void TheLightsAreThrustsThePointGoingStraightOutAlongTheShaft()
        {
            foreach (SwordStrikeKind kind in Thrusts)
            {
                SwordStrikes.StrikeTiming timing = SwordStrikes.TimingOf(kind);
                float from = (timing.WindUp + timing.Hold) / timing.Total, to = timing.Contact / timing.Total;
                Vector3 start = Tip(kind, from), end = Tip(kind, to);
                Vector3 travel = end - start;
                Assert.Greater(travel.magnitude, 0.55f, $"{kind}: the point hardly goes out");
                Vector3 shaft = SwordStrikes.Evaluate(kind, to).Blade;
                Assert.Less(Vector3.Angle(travel, shaft), 20.0f, $"{kind}: the point does not travel along the shaft: that is a cut");
                Assert.Less(Vector3.Angle(shaft, Vector3.forward), 20.0f, $"{kind}: the thrust does not land in front");
                // Straight: no moment of the strike is far off the line from where the point started to where it lands.
                for (int i = 1; i < 8; i++)
                {
                    Vector3 at = Tip(kind, Mathf.Lerp(from, to, i / 8.0f)) - start;
                    float off = (at - Vector3.Project(at, travel)).magnitude;
                    Assert.Less(off, 0.12f, $"{kind}: the point wanders {off:0.00} m off its line {i}/8 of the way out");
                }
                // And it stops: a thrust has no carry-through.
                float follow = (timing.Contact + timing.Follow) / timing.Total;
                Assert.Less(Vector3.Distance(Tip(kind, follow), end), 0.15f, $"{kind}: the point carries on past where it landed");
                // The shoulders stay turned to the right: the fighter is side on, the left side leading.
                Assert.Greater(SwordStrikes.Evaluate(kind, to).TwistDegrees, 5.0f, $"{kind}: the body squares up as the thrust lands");
            }
        }

        /// <summary>Owner: "some swinging as well maybe the heavy attacks are swings".</summary>
        [Test]
        public void TheHeavyIsASweepRoundTheFrontFromLeftToRight()
        {
            SwordStrikeKind kind = SwordStrikeKind.SpearSweep;
            SwordStrikes.StrikeTiming timing = SwordStrikes.TimingOf(kind);
            Assert.GreaterOrEqual(timing.Hold, 0.3f, "the heavy is not held wound up where it can be seen coming");
            Vector3 wound = SwordStrikes.Evaluate(kind, (timing.WindUp + timing.Hold) / timing.Total).Blade;
            Vector3 lands = SwordStrikes.Evaluate(kind, timing.Contact / timing.Total).Blade;
            Vector3 ends = SwordStrikes.Evaluate(kind, (timing.Contact + timing.Follow) / timing.Total).Blade;
            Assert.Less(wound.x, -0.7f, "it is not wound up across the left");
            Assert.Less(Vector3.Angle(lands, Vector3.forward), 15.0f, "it does not come across the front");
            Assert.Greater(ends.x, 0.6f, "it does not carry on round to the right");
            Assert.Greater(Vector3.Angle(wound, ends), 110.0f, "the sweep is a short one");
            foreach (Vector3 blade in new[] { wound, lands, ends })
            {
                Assert.Less(Mathf.Abs(blade.y), 0.35f, "the sweep is not level");
            }
        }

        [Test]
        public void AThrustIsQuickerAndLighterThanASwordsCutAndReachesFurther()
        {
            var body = new GameObject("Combat");
            try
            {
                var combat = body.AddComponent<SoulsSwordCombat>();
                combat.Equip(new Blade { Spear = true });
                Assert.AreEqual(SwordStrikes.SpearJabName, combat.LightAttack(0).Animation);
                Assert.AreEqual(SwordStrikes.SpearThrustName, combat.LightAttack(1).Animation);
                Assert.AreEqual(SwordStrikes.SpearLungeName, combat.LightAttack(2).Animation);
                Assert.AreEqual(3, combat.LightChainLength);
                Assert.AreEqual(SwordStrikes.SpearSweepName, combat.HeavyAttack.Animation);
                Assert.IsTrue(combat.HeavyAttack.BreaksGuard, "the heavy breaks a guard, as every heavy");
                Assert.AreEqual(SwordStrikes.SpearRunName, combat.OpenerAt(SwordStrikes.RunOpenerFromSpeed + 1.0f).Animation, "at a sprint, the running lunge");
                Assert.AreEqual(SwordStrikes.SpearJabName, combat.OpenerAt(0.0f).Animation);
                Assert.AreEqual(combat.ReachMetres * SpearRules.ReachShare, combat.Reach, 1e-4f);
                Assert.Greater(SpearRules.ReachShare, 1.2f, "a spear's reach is its point");

                Assert.Less(combat.SpearLight1.ContactAt, combat.OneHandLight1.ContactAt, "a jab lands no sooner than a sword's cut");
                Assert.GreaterOrEqual(combat.SpearLight1.ContactAt, 0.28f, "a jab too quick for an opponent to read");
                Assert.Less(combat.SpearLight1.DamageScale * SpearRules.BaseDamage, combat.OneHandLight1.DamageScale * CombatTuning.OneHandedSwordDamage,
                    "a jab hits as hard as a cut and from further off");
                Assert.Less(combat.SpearLight1.HitstunSeconds, combat.OneHandLight1.HitstunSeconds, "a point staggers as long as an edge");
                Assert.IsTrue(combat.SpearLight3.LandsHeavily, "the lunge is the chain's finisher");
                Assert.Greater(combat.SpearLight3.Recovery, combat.SpearLight3.HitstunSeconds, "the lunge's target is not free before the lunger is: it could be spammed");

                combat.Equip(new Blade { Spear = false });
                Assert.AreEqual(SwordStrikes.Sweep2HName, combat.LightAttack(0).Animation, "a greatsword took the spear's moves");
            }
            finally
            {
                Object.DestroyImmediate(body);
            }
        }

        [Test]
        public void EverySpearAttackHasItsFullBodyClipAsLongAsTheAttack()
        {
            foreach (SwordStrikeKind kind in Thrusts.Append(SwordStrikeKind.SpearSweep))
            {
                Assert.IsTrue(SwordStrikes.HasClip(kind), $"{kind} has no clip: rebuild SourceArt/SwordMoves");
                BakedMotion motion = SwordStrikes.MotionFor(kind);
                Assert.AreEqual(SwordStrikes.Seconds(kind), motion.SegmentSeconds(SwordStrikes.NameOf(kind)), 0.02f, $"{kind}: the clip and the attack are not the same length");
            }
        }

        [Test]
        public void TheFrontHandSlidesItIsNotFixedToTheShaft()
        {
            Assert.Greater(SpearRules.FrontHandFurthest, SpearRules.FrontHandNearest + 0.4f, "the front hand has no room to slide");
            Assert.Greater(SpearRules.FrontHandNearest, 0.15f, "the hands would sit on top of each other");
            Assert.Greater(SpearRules.FrontHandFromShoulder.z, 0.2f, "the front hand does not stay ahead of its shoulder");
        }

#if UNITY_EDITOR
        [Test]
        public void TheBuiltSpearIsASpearWithItsOwnHoldAndAStandInModel()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/Spear.prefab");
            Assert.IsNotNull(prefab, "no spear prefab: rebuild the scene (CityMassingSceneBuilder.Build)");
            SwordWeapon weapon = prefab.GetComponent<SwordWeapon>();
            Assert.IsNotNull(weapon);
            Assert.AreEqual(ItemRegistry.Spear, weapon.ItemId);
            Assert.IsTrue(ItemRegistry.TryGet(ItemRegistry.Spear, out ItemDefinition item) && item.EquipSlot == EquipSlots.Primary);
            Assert.IsTrue(weapon.IsSpear);
            Assert.IsFalse(weapon.IsOneHanded, "it is carried by its own pose, not the one-handed sword's clips");
            Assert.IsTrue(weapon.HasOwnHold, "it has the sword's hold: the shaft would stand square to the forearm");
            Assert.AreEqual(SpearRules.BaseDamage, weapon.GetMeleeDamage());
            Assert.Greater(weapon.BladeLength, 1.5f, "its point is no further out than a sword's");
            Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
            foreach (MeshFilter part in prefab.GetComponentsInChildren<MeshFilter>())
            {
                Bounds b = part.sharedMesh.bounds;
                bounds.Encapsulate(part.transform.localPosition + Vector3.Scale(b.min, part.transform.localScale));
                bounds.Encapsulate(part.transform.localPosition + Vector3.Scale(b.max, part.transform.localScale));
            }
            Assert.AreEqual(2.4f, bounds.size.y, 0.05f, "the spear is not about as long as its bearer is tall");
            Assert.Less(bounds.min.y, -0.5f, "no shaft behind the hand: it would be held by its very end");
            Assert.Less(prefab.GetComponentsInChildren<MeshFilter>().Sum(f => f.sharedMesh.triangles.Length / 3), 600);
        }
#endif
    }
}
