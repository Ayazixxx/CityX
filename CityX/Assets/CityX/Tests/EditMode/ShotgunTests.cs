using System.Collections.Generic;
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
    /// The pump shotgun's rules and data (owner, 2026-11-18; docs/SHOTGUN.md): the fixed pellet pattern, the damage thinning with distance, the
    /// profile (pellets, the hand-worked pump, the tube), the reload's pieces by name and in the baked motion, and the built prefab.
    /// </summary>
    public sealed class ShotgunTests
    {
        [Test]
        public void ThePatternIsFixedWithOnePelletDownTheMiddleAndTheRestOnTwoRings()
        {
            const int count = 9;
            const float cone = 3.2f;
            var offsets = Enumerable.Range(0, count).Select(i => PelletPattern.Offset(i, count, cone)).ToList();
            Assert.AreEqual(Vector2.zero, offsets[0], "the first pellet is not on the crosshair");
            Assert.AreEqual(2, PelletPattern.InnerRing(count));
            Assert.AreEqual(2, offsets.Count(o => Mathf.Abs(o.magnitude - cone * PelletPattern.InnerRingShare) < 1e-4f), "the inner ring");
            Assert.AreEqual(6, offsets.Count(o => Mathf.Abs(o.magnitude - cone) < 1e-4f), "the outer ring, on the cone's edge");
            for (int a = 0; a < count; a++)
            {
                Assert.AreEqual(offsets[a], PelletPattern.Offset(a, count, cone), "the pattern is not the same every shot");
                for (int b = a + 1; b < count; b++)
                {
                    Assert.Greater(Vector2.Distance(offsets[a], offsets[b]), 0.5f, $"pellets {a} and {b} fly together");
                }
            }
            Assert.AreEqual(Vector2.zero, PelletPattern.Offset(0, 1, cone), "a bullet is its own middle");
            Assert.AreEqual(Vector2.zero, PelletPattern.Offset(3, count, 0.0f), "no cone, no spread");
        }

        [Test]
        public void APelletFliesItsOffsetOffTheMiddleWhicheverWayTheGunPoints()
        {
            foreach (Vector3 forward in new[] { Vector3.forward, new Vector3(1.0f, 0.3f, -0.4f).normalized, Vector3.up })
            {
                Assert.Less(Vector3.Angle(forward, PelletPattern.Direction(forward, Vector2.zero)), 1e-3f);
                for (int pellet = 1; pellet < 9; pellet++)
                {
                    Vector2 offset = PelletPattern.Offset(pellet, 9, 3.2f);
                    Assert.AreEqual(offset.magnitude, Vector3.Angle(forward, PelletPattern.Direction(forward, offset)), 0.05f, $"pellet {pellet} off {forward}");
                }
            }
            Vector3 up = PelletPattern.Direction(Vector3.forward, new Vector2(0.0f, 2.0f));
            Assert.Greater(up.y, 0.0f, "an offset up went down");
            Vector3 right = PelletPattern.Direction(Vector3.forward, new Vector2(2.0f, 0.0f));
            Assert.Greater(right.x, 0.0f, "an offset right went left");
        }

        [Test]
        public void DamageIsWholeUpCloseAndFallsInAStraightLineToItsFloor()
        {
            FirearmProfile shotgun = FirearmProfile.Shotgun();
            Assert.AreEqual(1.0f, shotgun.DamageShareAt(0.0f));
            Assert.AreEqual(1.0f, shotgun.DamageShareAt(shotgun.FalloffFullMetres));
            float half = (shotgun.FalloffFullMetres + shotgun.FalloffFloorMetres) * 0.5f;
            Assert.AreEqual((1.0f + shotgun.FalloffFloorShare) * 0.5f, shotgun.DamageShareAt(half), 1e-4f);
            Assert.AreEqual(shotgun.FalloffFloorShare, shotgun.DamageShareAt(shotgun.FalloffFloorMetres), 1e-4f);
            Assert.AreEqual(shotgun.FalloffFloorShare, shotgun.DamageShareAt(500.0f), 1e-4f);
            Assert.AreEqual(1.0f, FirearmProfile.AK47().DamageShareAt(100.0f), "a rifle's bullets lost damage with distance");
            Assert.AreEqual(1.0f, FirearmProfile.Glock().DamageShareAt(50.0f));
        }

        /// <summary>
        /// Owner, 2026-11-19: "the shotgun should do more damage close up the enemies are feeling like bullet sponges, 100 health is a low level
        /// enemy", "guns like shotguns and ak47 are going to be hard to find they should kill low level enemies very quick". A low-level enemy
        /// (the body's own 100 health) falls to two AK rounds, or to half a shotgun's pattern up close.
        /// </summary>
        [Test]
        public void TheRareGunsKillALowLevelEnemyFast()
        {
            FirearmProfile shotgun = FirearmProfile.Shotgun();
            int halfThePattern = (shotgun.Pellets + 1) / 2;
            Assert.GreaterOrEqual(halfThePattern * shotgun.Damage * shotgun.DamageShareAt(shotgun.FalloffFullMetres), PlayerVitals.BaseMaxHealth,
                "half a shotgun's pattern up close leaves a low-level enemy standing");
            Assert.GreaterOrEqual(2.0f * FirearmProfile.AK47().Damage, PlayerVitals.BaseMaxHealth, "a low-level enemy takes more than two AK rounds");
        }

        [Test]
        public void TheShotgunIsNinePelletsSixShellsAPumpAfterEveryShotAndATubeLoadedByHand()
        {
            FirearmProfile shotgun = FirearmProfile.Shotgun();
            Assert.AreEqual(ItemRegistry.Shotgun, shotgun.ItemId);
            Assert.IsTrue(ItemRegistry.TryGet(ItemRegistry.Shotgun, out ItemDefinition item), "the shotgun is not an item");
            Assert.AreEqual(EquipSlots.Primary, item.EquipSlot);
            Assert.AreEqual(9, shotgun.Pellets);
            Assert.AreEqual(6, shotgun.MagazineSize);
            Assert.IsFalse(shotgun.Automatic);
            Assert.AreEqual(FirearmKind.Rifle, shotgun.Kind, "it is shouldered and held in both hands, as a rifle");
            Assert.IsTrue(shotgun.ShellByShell);
            Assert.IsTrue(shotgun.SupportHandOnAction, "the support hand works the pump");
            Assert.AreEqual(70.0f, shotgun.FireRatePerSecond * 60.0f, 3.0f, "about the Nova's 68 shots a minute");
            Assert.Less(shotgun.AimedConeShare, 1.0f, "aiming does not tighten the pattern");
            Assert.Greater(shotgun.Damage * shotgun.Pellets, FirearmProfile.AK47().Damage * 2.0f, "a whole pattern up close is not the hardest single shot there is");
            Assert.Less(shotgun.Range, FirearmProfile.AK47().Range, "a shotgun reaches as far as a rifle");

            // The pump waits for the shot, goes back, comes home, all before the next shot can go; the case leaves as it reaches the rear.
            Assert.AreEqual(0.0f, shotgun.ActionBackAt(shotgun.ActionDelaySeconds * 0.5f), "the pump moved with the shot");
            Assert.AreEqual(1.0f, shotgun.ActionBackAt(shotgun.EjectDelaySeconds), 1e-3f, "the case does not leave as the pump reaches the rear");
            Assert.AreEqual(0.0f, shotgun.ActionBackAt(shotgun.ActionDelaySeconds + shotgun.ActionCycleSeconds + 0.01f), "the pump never came home");
            Assert.Less(shotgun.ActionDelaySeconds + shotgun.ActionCycleSeconds, shotgun.FireCooldownSeconds, "the next shot could go before the pump is home");
            // The other guns' actions cycle with the shot as they always did.
            FirearmProfile glock = FirearmProfile.Glock();
            Assert.AreEqual(0.0f, glock.EjectDelaySeconds);
            Assert.AreEqual(1.0f, glock.ActionBackAt(glock.ActionCycleSeconds * 0.35f), 1e-3f);
        }

        [Test]
        public void TheReloadsPiecesAreNamedAndOnlyTheFirstEasesInAndOnlyTheLastEasesOut()
        {
            Assert.IsTrue(GunReload.TryGetStage(GunReload.ShellsStartName, out GunReload.Stage start));
            Assert.IsTrue(GunReload.TryGetStage(GunReload.ShellName, out GunReload.Stage shell));
            Assert.IsTrue(GunReload.TryGetStage(GunReload.ShellsEndName, out GunReload.Stage end));
            Assert.IsTrue(GunReload.TryGetStage(GunReload.ShellsEndEmptyName, out GunReload.Stage endEmpty));
            Assert.IsTrue(start.EaseIn && !start.EaseOut);
            Assert.IsTrue(!shell.EaseIn && !shell.EaseOut, "a shell between two others eased in or out: the arms would pump between shells");
            Assert.IsTrue(!end.EaseIn && end.EaseOut);
            Assert.IsTrue(!endEmpty.EaseIn && endEmpty.EaseOut && endEmpty.Empty);
            Assert.IsTrue(GunReload.Has(GunReload.ShellName));
            Assert.IsTrue(GunReload.TryGetStage(GunReload.TacticalName, out GunReload.Stage magazine) && magazine.EaseIn && magazine.EaseOut);
        }

        [Test]
        public void TheReloadMotionHasItsFourPiecesEachEndingWhereTheNextBeginsAndTheShellInTheHandBetweenTheGrabAndTheSeat()
        {
            FirearmProfile shotgun = FirearmProfile.Shotgun();
            BakedMotion motion = BakedMotion.Load(shotgun.MotionResource);
            Assert.IsNotNull(motion, "no reload motion -- run CityX/SourceArt/Shotgun/shotgun_build_all.py");
            string[] pieces = { GunReload.ShellsStartSegment, GunReload.ShellSegment, GunReload.ShellsEndSegment, GunReload.ShellsEndEmptySegment };
            foreach (string piece in pieces)
            {
                Assert.IsTrue(motion.HasSegment(piece), piece);
            }
            Assert.IsTrue(motion.HasFingers);
            CollectionAssert.IsSupersetOf(motion.Props, new[] { "Pump", "Shell", "Trigger" });
            Assert.AreEqual(shotgun.ShellStartSeconds, motion.SegmentSeconds(GunReload.ShellsStartSegment), 0.02f);
            Assert.AreEqual(shotgun.ShellSeconds, motion.SegmentSeconds(GunReload.ShellSegment), 0.02f, "about half a second a shell");
            Assert.AreEqual(shotgun.ShellEndSeconds, motion.SegmentSeconds(GunReload.ShellsEndSegment), 0.02f);
            Assert.AreEqual(shotgun.ShellEndEmptySeconds, motion.SegmentSeconds(GunReload.ShellsEndEmptySegment), 0.02f);

            // Each piece hands over to the next in the same pose (the left hand's arm is what moves most).
            int End(string piece) => motion.SegmentStart(piece) + motion.SegmentFrames(piece);
            var joins = new[]
            {
                (End(GunReload.ShellsStartSegment), motion.SegmentStart(GunReload.ShellSegment), "the start into a shell"),
                (End(GunReload.ShellSegment), motion.SegmentStart(GunReload.ShellSegment), "a shell into the next"),
                (End(GunReload.ShellSegment), motion.SegmentStart(GunReload.ShellsEndSegment), "the last shell into the end"),
                (End(GunReload.ShellSegment), motion.SegmentStart(GunReload.ShellsEndEmptySegment), "the last shell into the pump"),
            };
            foreach ((int from, int to, string what) in joins)
            {
                for (int bone = 0; bone < motion.Bones.Count; bone++)
                {
                    Assert.Less(Quaternion.Angle(motion.RotationAt(from, bone), motion.RotationAt(to, bone)), 0.5f, $"{what}: {motion.Bones[bone]} jumps");
                }
            }

            List<KeyValuePair<string, float>> events = motion.EventsIn(GunReload.ShellSegment);
            float grab = events.First(e => GunReload.EventKind(e.Key) == GunReload.GrabEvent).Value;
            float seat = events.First(e => GunReload.EventKind(e.Key) == GunReload.SeatEvent).Value;
            Assert.Less(grab, seat);
            int shell = motion.Props.ToList().IndexOf("Shell");
            int shellStart = motion.SegmentStart(GunReload.ShellSegment);
            Assert.IsFalse(motion.PropVisibleAt(shellStart, shell), "a shell shows before one is taken");
            Assert.IsTrue(motion.PropVisibleAt(shellStart + Mathf.RoundToInt((grab + seat) * 0.5f * motion.FramesPerSecond), shell), "no shell in the hand on its way to the gun");
            Assert.IsFalse(motion.PropVisibleAt(End(GunReload.ShellSegment), shell), "the shell still shows after it went into the tube");

            // The pump is pulled back and shoved home only where the gun had been run dry.
            int pump = motion.Props.ToList().IndexOf("Pump");
            float Back(string piece)
            {
                float rest = motion.PropPositionAt(motion.SegmentStart(piece), pump).z, least = rest;
                for (int f = motion.SegmentStart(piece); f <= End(piece); f++)
                {
                    least = Mathf.Min(least, motion.PropPositionAt(f, pump).z);
                }
                return rest - least;
            }
            Assert.AreEqual(shotgun.ActionTravel, Back(GunReload.ShellsEndEmptySegment), 0.005f, "the dry gun's pump is not pulled its whole travel");
            Assert.Less(Back(GunReload.ShellsEndSegment), 0.001f);
            Assert.IsTrue(motion.EventsIn(GunReload.ShellsEndEmptySegment).Any(e => GunReload.EventKind(e.Key) == GunReload.RackEvent));
        }

#if UNITY_EDITOR
        /// <summary>
        /// Owner, 2026-11-19: "replace the shotgun blast noise with shotgunblast.mp3 and replace the shotgun cocking sound with the other one".
        /// The shot is its own recording at its own pitch; the pump's is played from its pull, and the pump is timed to the recording: back as
        /// the pull is heard, home as the push is heard, after a shot and in a dry gun's reload alike.
        /// </summary>
        [Test]
        public void TheShotAndThePumpAreTheOwnersRecordingsAndThePumpMovesInTimeWithIt()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/Shotgun.prefab");
            Assert.IsNotNull(prefab, "Shotgun.prefab is missing -- CityX > Rebuild Firearms");
            var gun = prefab.GetComponent<PistolWeapon>();
            var blast = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/CityX/Weapons/Audio/shotgun_blast.mp3");
            var pump = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/CityX/Weapons/Audio/shotgun_pump.mp3");
            Assert.IsNotNull(blast);
            Assert.IsNotNull(pump);
            Assert.AreSame(blast, gun.ShotPlayer.clip, "the shot is not the owner's blast");
            Assert.IsTrue(gun.ShotPlayerPool.All(voice => voice.clip == blast), "a shot voice plays something else");
            Assert.AreEqual(1.0f, gun.Profile.ShotPitch, "the blast is pitched away from the recording");
            Assert.AreSame(pump, gun.Profile.RackSound, "the pump is not the owner's recording");

            // The recording's pull lands 0.17 s in and its push 0.25 s after: measured on the file (docs/SHOTGUN.md).
            const float pull = 0.17f, push = 0.42f;
            FirearmProfile profile = gun.Profile;
            Assert.AreEqual(pull, profile.RackSoundBackAt, 0.005f);
            float back = profile.ActionCycleSeconds * profile.ActionBackShare;
            Assert.AreEqual(push - pull, profile.ActionCycleSeconds - back, 0.02f, "after a shot the pump is not home as the push is heard");
            BakedMotion motion = gun.Motion;
            float rack = motion.EventsIn(GunReload.ShellsEndEmptySegment).First(e => GunReload.EventKind(e.Key) == GunReload.RackEvent).Value;
            int forend = motion.Props.ToList().IndexOf("Pump");
            int start = motion.SegmentStart(GunReload.ShellsEndEmptySegment);
            float home = -1.0f;
            for (int f = start + Mathf.RoundToInt(rack * motion.FramesPerSecond); f <= start + motion.SegmentFrames(GunReload.ShellsEndEmptySegment); f++)
            {
                if (motion.PropPositionAt(f, forend).z >= motion.PropPositionAt(start, forend).z - 0.002f)
                {
                    home = (f - start) / (float)motion.FramesPerSecond;
                    break;
                }
            }
            float rearmost = motion.PropPositionAt(start + Mathf.RoundToInt(rack * motion.FramesPerSecond), forend).z;
            Assert.AreEqual(motion.PropPositionAt(start, forend).z - profile.ActionTravel, rearmost, 0.005f, "the rack is not where the forend is at the rear");
            Assert.AreEqual(push - pull, home - rack, 0.03f, "in a dry gun's reload the pump is not home as the push is heard");

            // One shell going in (owner, the same day): its click 0.38 s in, played from each shell's start, lands where the thumb seats it;
            // and it is over before the next shell's piece begins.
            var shell = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/CityX/Weapons/Audio/shotgun_shell.mp3");
            Assert.IsNotNull(shell);
            Assert.AreSame(shell, profile.ShellSound, "the shell going in is not the owner's recording");
            const float click = 0.38f;
            float seat = motion.EventsIn(GunReload.ShellSegment).First(e => GunReload.EventKind(e.Key) == GunReload.SeatEvent).Value;
            Assert.AreEqual(click, seat, 0.03f, "the shell's click is not heard as the thumb seats it");
            Assert.Less(shell.length, motion.SegmentSeconds(GunReload.ShellSegment), "one shell's sound runs on into the next");
        }

        [Test]
        public void TheBuiltShotgunHasThePartsItsShotAndReloadMove()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/Shotgun.prefab");
            Assert.IsNotNull(prefab, "Shotgun.prefab is missing -- CityX > Rebuild Firearms");
            var gun = prefab.GetComponent<PistolWeapon>();
            Assert.IsNotNull(gun);
            Assert.AreEqual(ItemRegistry.Shotgun, gun.ItemId);
            Assert.IsNotNull(gun.ActionPart, "no pump");
            Assert.IsNotNull(gun.MagazinePart, "no shell");
            Assert.IsNotNull(gun.TriggerPart, "no trigger");
            Assert.AreEqual("Pump", gun.ActionPart.name);
            Assert.AreEqual("Shell", gun.MagazinePart.name);
            BakedMotion motion = gun.Motion;
            Assert.IsNotNull(motion);
            int pump = motion.Props.ToList().IndexOf("Pump");
            Assert.Less(Vector3.Distance(motion.PropPositionAt(motion.SegmentStart(GunReload.ShellsStartSegment), pump), gun.ActionPart.localPosition), 0.005f,
                "the reload's pump does not start where the model's is");
            Assert.Less(Vector3.Distance(gun.MuzzleFlashObject.transform.localPosition, gun.Profile.MuzzleLocal), 1e-4f, "the muzzle flash is not at the muzzle");
            // The pump's rest is ahead of the receiver by more than its travel: pulled back it does not go into the trigger hand.
            Assert.Greater(gun.ActionPart.localPosition.z - gun.Profile.ActionTravel, 0.3f);
            int triangles = prefab.GetComponentsInChildren<MeshFilter>(true).Sum(f => f.sharedMesh.triangles.Length / 3);
            Assert.Less(triangles, 2500, "the shotgun is heavier than a held gun should be");
            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    Assert.IsNotNull(material, renderer.name + " has a missing material (it would render pink)");
                    Assert.IsNull(material.mainTexture, renderer.name + ": guns are flat-coloured, no textures");
                }
            }
        }
#endif
    }
}
