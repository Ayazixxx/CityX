using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// THE CHAIN (owner, 2026-11-22, with a video of four games' chains and whips; docs/CHAIN.md): its four attacks and their clips (both arms the
    /// clips'), which way each throws the chain, the two lashes back and forth with no heavy and no guard, the swing sounds never one twice
    /// running, the coils round the wrist and the palm with the thumb left free, the free length hanging, thrown and paid out of the fist, and
    /// the built chain.
    /// </summary>
    public sealed class ChainTests
    {
        private sealed class Worn : ISwordBlade
        {
            public Vector3 BladeBaseWorld => Vector3.zero;

            public Vector3 BladeTipWorld => Vector3.forward;

            public bool IsOneHanded => true;

            public bool IsChain => true;

            public float GetMeleeDamage() => ChainRules.BaseDamage;
        }

        private static readonly SwordStrikeKind[] Chain =
        {
            SwordStrikeKind.ChainLash, SwordStrikeKind.ChainBackhand, SwordStrikeKind.ChainAir, SwordStrikeKind.ChainGround,
        };

        private static SwordFrame At(SwordStrikeKind kind, float seconds) => SwordStrikes.Evaluate(kind, seconds / SwordStrikes.Seconds(kind));

        [Test]
        public void FourAttacksEachAClipWithBothArmsInIt()
        {
            BakedMotion motion = SwordStrikes.ChainMotion;
            Assert.IsNotNull(motion, "no ChainMotion: run SourceArt/Chain/chain_build.py");
            foreach (string arm in new[] { "arm_r", "forearm_r", "hand_r", "arm_l", "forearm_l", "hand_l" })
            {
                Assert.IsTrue(System.Linq.Enumerable.Contains(motion.Bones, arm), "the chain's clips do not pose " + arm);
            }
            foreach (SwordStrikeKind kind in Chain)
            {
                Assert.IsTrue(SwordStrikes.IsChain(kind) && SwordStrikes.IsOneHanded(kind), kind + " is not a chain's, in one hand");
                Assert.IsTrue(SwordStrikes.TryGet(SwordStrikes.NameOf(kind), out SwordStrikeKind back) && back == kind, kind + "'s name does not travel");
                Assert.AreSame(motion, SwordStrikes.MotionFor(kind));
                Assert.IsTrue(SwordStrikes.HasClip(kind), kind + " has no clip");
                Assert.AreEqual(SwordStrikes.Seconds(kind), motion.SegmentSeconds(SwordStrikes.NameOf(kind)), 1.0f / 60.0f + 1e-3f, kind + "'s clip is not as long as the attack");
                Assert.AreEqual(0.0f, SwordStrikes.Evaluate(kind, 0.5f).FreeHandWeight, "the free arm is posed over the clip's");
            }
            Assert.AreEqual(1.0f, SwordStrikes.ClipLegs(SwordStrikeKind.ChainAir, 7.0f), "in the air the legs are given to a walk");
            Assert.AreEqual(0.0f, SwordStrikes.StepMetres(SwordStrikeKind.ChainAir));
        }

        [Test]
        public void TheLashComesFromOverTheRightShoulderDownToTheLeftAndTheBackhandMirrorsIt()
        {
            foreach (SwordStrikeKind kind in new[] { SwordStrikeKind.ChainLash, SwordStrikeKind.ChainBackhand })
            {
                float side = kind == SwordStrikeKind.ChainLash ? 1.0f : -1.0f;
                SwordStrikes.StrikeTiming t = SwordStrikes.TimingOf(kind);
                Vector3 wound = At(kind, t.WindUp + t.Hold).Blade, landing = At(kind, t.Contact).Blade, through = At(kind, t.Contact + t.Follow).Blade;
                TestContext.WriteLine($"{kind}: thrown {wound.ToString("0.00")} wound up, {landing.ToString("0.00")} landing, {through.ToString("0.00")} carried through");
                Assert.Greater(wound.y, 0.3f, kind + ": wound up, the chain is not flung up");
                Assert.Less(wound.z, -0.3f, kind + ": wound up, the chain is not flung back");
                Assert.Greater(wound.x * side, 0.2f, kind + ": wound up on the wrong side");
                Assert.Greater(landing.z, 0.97f, kind + " does not land straight ahead");
                Assert.Less(through.y, -0.5f, kind + " is not carried down");
                Assert.Less(through.x * side, -0.5f, kind + " is not carried across to the other side");
            }
        }

        [Test]
        public void InTheAirAndOntoSomeoneDownItComesStraightDownAhead()
        {
            foreach (SwordStrikeKind kind in new[] { SwordStrikeKind.ChainAir, SwordStrikeKind.ChainGround })
            {
                SwordStrikes.StrikeTiming t = SwordStrikes.TimingOf(kind);
                Vector3 wound = At(kind, t.WindUp + t.Hold).Blade, landing = At(kind, t.Contact).Blade;
                Assert.Greater(wound.y, 0.4f, kind + ": wound up, the chain is not up");
                Assert.Less(landing.y, -0.5f, kind + " does not come down");
                Assert.Greater(landing.z, 0.4f, kind + " does not come down ahead");
                Assert.Less(Mathf.Abs(landing.x), 0.1f, kind + " comes down to one side");
            }
            // Onto the ground: the line from the fist meets it where a body would lie, inside the reach that picks this blow.
            SwordStrikes.StrikeTiming ground = SwordStrikes.TimingOf(SwordStrikeKind.ChainGround);
            SwordFrame frame = At(SwordStrikeKind.ChainGround, ground.Contact);
            Vector3 fist = SwordStrikes.HandPoint(frame) + Vector3.down * frame.HipDrop;
            float meets = fist.z + frame.Blade.z * (fist.y / -frame.Blade.y);
            TestContext.WriteLine($"onto the ground the chain meets it {meets:0.00} m ahead");
            Assert.That(meets, Is.InRange(0.8f, ChainRules.DownedReachMetres), "the blow onto the ground lands where nobody lies");
        }

        [Test]
        public void TwoLashesBackAndForthNoHeavyNoGuardAndTheLongestReach()
        {
            var go = new GameObject("Chain");
            try
            {
                var combat = go.AddComponent<SoulsSwordCombat>();
                combat.Equip(new Worn());
                Assert.AreEqual(2, combat.LightChainLength, "the reference's swings alternate, two of them");
                Assert.AreEqual(SwordStrikes.ChainLashName, combat.LightAttack(0).Animation, "the combo does not start right to left");
                Assert.AreEqual(SwordStrikes.ChainBackhandName, combat.LightAttack(1).Animation);
                Assert.AreEqual(SwordStrikes.ChainLashName, combat.OpenerAt(7.5f).Animation, "a chain has no running attack");
                Assert.Greater(combat.Reach, combat.ReachMetres * StaffRules.ReachShare, "a chain does not reach past a staff");
                combat.SetBlockHeld(true);
                Assert.IsFalse(combat.IsBlocking, "a chain has no guard");
                // A landed lash stuns for as long as the next takes to arrive, as the swords' lights do.
                float gap = combat.ChainLight1.Recovery * combat.ChainLight1.ChainAt + combat.ChainLight2.Startup;
                Assert.Greater(combat.ChainLight1.HitstunSeconds, gap, "the backhand arrives after the lash's stun has worn off");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ASwingsSoundIsNeverTheOneBeforeItAndIsLoudestAsTheBlowLands()
        {
            Assert.AreEqual(4, ChainSounds.Cold.Count);
            Assert.GreaterOrEqual(ChainSounds.Hot.Count, 1, "no recording of the burning chain");
            Assert.IsNotNull(Resources.Load<AudioClip>(ChainSounds.IgniteClipName), "no recording of the chain catching fire");
            foreach (ChainSounds.Set set in new[] { ChainSounds.Cold, ChainSounds.Hot })
            {
                int count = set.Count;
                Assert.AreEqual(count, set.LoudestAtSeconds.Length);
                for (int last = 0; last < count; last++)
                {
                    var heard = new System.Collections.Generic.HashSet<int>();
                    for (int roll = 0; roll < Mathf.Max(1, count - 1); roll++)
                    {
                        int next = ChainSounds.After(last, roll, count);
                        Assert.IsTrue(count == 1 || next != last, "the same recording twice running");
                        heard.Add(next);
                    }
                    Assert.AreEqual(Mathf.Max(1, count - 1), heard.Count, "after recording " + last + " not every other one can follow");
                }
                for (int i = 0; i < count; i++)
                {
                    Assert.IsNotNull(Resources.Load<AudioClip>(set.ClipNames[i]), set.ClipNames[i] + " is not in Resources");
                    float contact = SwordStrikes.TimingOf(SwordStrikeKind.ChainLash).Contact;
                    float loudest = ChainSounds.StartSeconds(set, i, contact) + set.LoudestAtSeconds[i];
                    Assert.AreEqual(Mathf.Max(contact, set.LoudestAtSeconds[i]), loudest, 1e-4f, set.ClipNames[i] + " is not loudest as the lash lands (or as soon after as it can be)");
                }
            }
        }

        [Test]
        public void WoundRoundTheWristAndOnceRoundThePalmWithTheThumbFree()
        {
            Assert.LessOrEqual(ChainWrap.PalmLinks, ChainWrap.FreeLinks, "more links round the hand than the chain has");
            Assert.GreaterOrEqual(ChainWrap.PalmLinks, 12);
            Assert.That(ChainWrap.FreeLinks * ChainWrap.Pitch, Is.EqualTo(ChainRules.SwungLength).Within(ChainWrap.Pitch));

            // The wrist's coil: round the forearm above the wrist, off the skin.
            for (int i = 0; i < ChainWrap.WristLinks; i++)
            {
                ChainWrap.WristLink(i, Vector3.zero, out Vector3 at, out _);
                Assert.That(at.x, Is.InRange(-0.26f, -0.01f), "wrist link " + i + " is not on the forearm above the wrist");
                Assert.Greater(new Vector2(at.y, at.z).magnitude, FusedBodyShape.WristRadius + ChainWrap.BarRadius, "wrist link " + i + " is in the arm");
            }

            // The hand's: each link a pitch from the last, outside the hand, clear of the thumb, and ending in one turn straight round the palm
            // between the thumb's root and the fingers'.
            FusedBodyShape.Digit thumb = FusedBodyShape.Digits[4];
            Vector3 last = Vector3.zero;
            float lowest = float.MaxValue, highest = float.MinValue, nearestThumb = float.MaxValue;
            int perTurn = Mathf.RoundToInt(2.0f * Mathf.PI * (FusedBodyShape.PalmHalfWidth + FusedBodyShape.PalmHalfThickness) * 0.5f / ChainWrap.Pitch);
            for (int slot = 0; slot < ChainWrap.PalmLinks; slot++)
            {
                ChainWrap.PalmLink(slot, out Vector3 at, out _);
                if (slot > 0)
                {
                    Assert.That(Vector3.Distance(at, last), Is.EqualTo(ChainWrap.Pitch).Within(ChainWrap.Pitch * 0.2f), "hand link " + slot + " is not a link's pitch from the one before");
                }
                last = at;
                nearestThumb = Mathf.Min(nearestThumb, Mathf.Min(FromSegment(at, thumb.Root, thumb.Knuckle), FromSegment(at, thumb.Knuckle, thumb.Tip)));
                if (slot >= ChainWrap.PalmLinks - perTurn)
                {
                    lowest = Mathf.Min(lowest, at.x);
                    highest = Mathf.Max(highest, at.x);
                }
            }
            TestContext.WriteLine($"{ChainWrap.WristLinks} links round the wrist, {ChainWrap.PalmLinks} round the hand; the last turn runs from {lowest:0.000} to {highest:0.000} m down the hand; "
                + $"nearest the thumb's middle line {nearestThumb:0.000} m");
            Assert.Less(highest - lowest, 0.035f, "the last turn is not straight round the palm");
            Assert.Greater(lowest, thumb.Root.x + FusedBodyShape.ThumbRadius, "the turn round the palm is not under the thumb");
            Assert.Less(highest, FusedBodyShape.PalmLength, "the turn round the palm is on the fingers");
            Assert.Greater(nearestThumb, FusedBodyShape.ThumbRadius, "a link runs through the thumb");

            // The far end comes off first and goes on last.
            Assert.AreEqual(1.0f, ChainWrap.Wound(1.0f, ChainWrap.PalmLinks - 1));
            Assert.AreEqual(0.0f, ChainWrap.Wound(0.0f, 0));
            Assert.Less(ChainWrap.Wound(0.5f, ChainWrap.PalmLinks - 1), ChainWrap.Wound(0.5f, 0));
        }

        private static float FromSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector3 along = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(point - a, along) / along.sqrMagnitude);
            return Vector3.Distance(point, a + along * t);
        }

        private static ChainLash NewLash() => new ChainLash(ChainWrap.FreeLinks, ChainWrap.Pitch, ChainWrap.BarRadius * 2.0f);

        /// <summary>The chain paid out of the fist over the first fifth of a second, as it is in the game, and stepped on for <paramref name="seconds"/>.</summary>
        private static void Run(ChainLash lash, Vector3 fist, Vector3 thrown, float pull, float paidOut, float seconds)
        {
            for (float t = 0.0f; t < seconds; t += 1.0f / 60.0f)
            {
                lash.Step(fist, thrown, pull, Mathf.Min(paidOut, 1.0f + paidOut * t / 0.2f), 0.0f, 1.0f / 60.0f);
            }
        }

        [Test]
        public void LetAloneItHangsFromTheFistAndLiesOnTheGround()
        {
            ChainLash lash = NewLash();
            // From a fist held high it hangs straight down, its own length long.
            var fist = new Vector3(0.0f, 5.0f, 0.0f);
            lash.Gather(fist);
            Run(lash, fist, Vector3.down, 0.0f, lash.Links, 3.0f);
            float length = 0.0f;
            for (int i = 1; i < lash.Links; i++)
            {
                length += Vector3.Distance(lash.At[i], lash.At[i - 1]);
                Assert.Less(new Vector2(lash.At[i].x, lash.At[i].z).magnitude, 0.2f, "link " + i + " did not hang under the fist");
            }
            Vector3 end = lash.At[lash.Links - 1];
            TestContext.WriteLine($"hanging: {length:0.00} m of chain, its far end {fist.y - end.y:0.00} m under the fist");
            Assert.That(length, Is.EqualTo((lash.Links - 1) * lash.Pitch).Within(0.2f), "the chain stretched or bunched");
            Assert.That(fist.y - end.y, Is.EqualTo((lash.Links - 1) * lash.Pitch).Within(0.2f), "the chain does not hang straight");
            Assert.AreEqual(fist, lash.At[0]);

            // From a hip-high fist its end lies on the ground, none of it in the ground.
            fist = new Vector3(0.0f, 1.2f, 0.0f);
            lash.Gather(fist);
            Run(lash, fist, Vector3.down, 0.0f, lash.Links, 3.0f);
            for (int i = 1; i < lash.Links; i++)
            {
                Assert.GreaterOrEqual(lash.At[i].y, lash.LinkRadius - 1e-3f, "link " + i + " is in the ground");
            }
            Assert.LessOrEqual(lash.At[lash.Links - 1].y, lash.LinkRadius + 0.02f, "three metres of chain from a hip-high fist does not reach the ground");
        }

        [Test]
        public void ThrownItGoesOutAlongTheLineAndBowsBackThroughASwing()
        {
            ChainLash lash = NewLash();
            var fist = new Vector3(0.0f, 5.0f, 0.0f);
            lash.Gather(fist);
            Run(lash, fist, Vector3.forward, 1.0f, lash.Links, 0.5f);
            Vector3 end = lash.At[lash.Links - 1] - fist;
            TestContext.WriteLine($"thrown ahead: its far end {end.ToString("0.00")} from the fist");
            Assert.Greater(end.z, (lash.Links - 1) * lash.Pitch * 0.93f, "held thrown ahead, the chain is not out along the line");

            // Swung round to the right in a tenth of a second: the far end is behind the near.
            for (int frame = 1; frame <= 6; frame++)
            {
                Vector3 thrown = Quaternion.AngleAxis(15.0f * frame, Vector3.up) * Vector3.forward;
                lash.Step(fist, thrown, 1.0f, lash.Links, 0.0f, 1.0f / 60.0f);
            }
            float near = Mathf.Atan2(lash.At[6].x - fist.x, lash.At[6].z - fist.z) * Mathf.Rad2Deg;
            float far = Mathf.Atan2(lash.At[lash.Links - 1].x - fist.x, lash.At[lash.Links - 1].z - fist.z) * Mathf.Rad2Deg;
            TestContext.WriteLine($"swung 90 degrees round: the near links at {near:0}, the far end at {far:0}");
            Assert.Greater(near, far + 15.0f, "through a swing the far end does not trail the near");
            Assert.Greater(far, 20.0f, "the far end did not come round at all");
        }

        [Test]
        public void TheLinksNotPaidOutStayInTheFistAndASwingPullsOnlyWhileItLasts()
        {
            ChainLash lash = NewLash();
            var fist = new Vector3(0.0f, 5.0f, 0.0f);
            lash.Gather(fist);
            float paid = ChainWrap.PalmLinks;
            Run(lash, fist, Vector3.down, 0.0f, paid, 1.0f);
            int first = lash.FirstOut(paid);
            Assert.AreEqual(lash.Links - ChainWrap.PalmLinks, first);
            for (int i = 0; i <= first; i++)
            {
                Assert.AreEqual(fist, lash.At[i], "link " + i + " is out of the fist before it was paid out");
            }
            Assert.That(fist.y - lash.At[lash.Links - 1].y, Is.EqualTo((ChainWrap.PalmLinks - 1) * lash.Pitch).Within(0.2f), "the paid-out part does not hang its own length");

            SwordStrikes.StrikeTiming t = SwordStrikes.TimingOf(SwordStrikeKind.ChainLash);
            float Pull(float seconds) => ChainLash.Pull(t.WindUp, t.Hold, t.Strike, t.Follow, t.Settle, seconds);
            Assert.AreEqual(0.0f, Pull(0.0f));
            Assert.AreEqual(ChainLash.WindUpPull, Pull(t.WindUp), 1e-4f);
            Assert.AreEqual(1.0f, Pull(t.WindUp + t.Hold), 1e-4f);
            Assert.AreEqual(1.0f, Pull(t.Contact + t.Follow));
            Assert.AreEqual(0.0f, Pull(t.Contact + t.Follow + t.Settle), 1e-4f);
        }

        [Test]
        public void TheBuiltChainIsOneSkinOfPlainSteelLinksWornInOneHand()
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(WeaponsEditor.ChainBuilder.PrefabPath);
            Assert.IsNotNull(prefab, "no Chain.prefab -- rebuild the scene");
            var chain = prefab.GetComponent<ChainWeapon>();
            Assert.IsTrue(chain.IsChain && chain.IsOneHanded);
            Assert.AreEqual(ChainWrap.TotalLinks, chain.Links.Length);
            var skin = prefab.GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.AreEqual(ChainWrap.TotalLinks, skin.bones.Length, "not a bone a link");
            Assert.LessOrEqual(skin.sharedMesh.triangles.Length / 3, 7000, "too many triangles for a held thing");
            // A link is a long oval, a little under twice as long as it is wide, of bar about a seventh of its width thick (the owner's photographs).
            Assert.That(ChainWrap.LinkLength / ChainWrap.LinkWidth, Is.InRange(1.5f, 2.2f));
            Assert.That(ChainWrap.BarRadius * 2.0f / ChainWrap.LinkWidth, Is.InRange(0.2f, 0.35f));

            Assert.IsTrue(ItemRegistry.TryGet(ItemRegistry.Chain, out ItemDefinition definition));
            Assert.AreEqual(EquipSlots.Primary, definition.EquipSlot);
            ItemCard card = ItemCards.Describe(ItemRegistry.Chain, prefab);
            Assert.AreEqual("Chain", card.Kind);
            Assert.AreEqual(ItemCards.OneHand, card.Hands);
            Assert.AreEqual("Blunt", card.Hits);
#endif
        }
    }
}
