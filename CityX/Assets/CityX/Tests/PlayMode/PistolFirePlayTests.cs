using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The shot sound's own per-shot pitch jitter -- "still choppy if you
    /// spam shots" even after PlayOneShot already fixed shots cutting each
    /// other off (see PistolWeapon.Fire's own comment on why overlapping
    /// identical waveforms is a separate problem from that one).
    /// </summary>
    public sealed class PistolFirePlayTests
    {
        [Test]
        public void RandomShotPitchStaysWithinTheStatedJitterRange()
        {
            float min = 1.0f - PistolWeapon.ShotPitchJitter;
            float max = 1.0f + PistolWeapon.ShotPitchJitter;
            for (int i = 0; i < 100; i++)
            {
                float pitch = PistolWeapon.RandomShotPitch();
                Assert.GreaterOrEqual(pitch, min);
                Assert.LessOrEqual(pitch, max);
            }
        }

        /// <summary>
        /// Not a claim about the distribution, just that it is genuinely
        /// re-rolled -- catches the shape of bug where the jitter is
        /// computed once and never actually varies.
        /// </summary>
        [Test]
        public void RandomShotPitchVariesAcrossCalls()
        {
            float first = PistolWeapon.RandomShotPitch();
            for (int i = 0; i < 100; i++)
            {
                if (!Mathf.Approximately(PistolWeapon.RandomShotPitch(), first))
                {
                    return;
                }
            }
            Assert.Fail($"100 calls all returned the exact same pitch ({first})");
        }

        /// <summary>
        /// The wiring, not just the pure jitter function -- confirms Fire
        /// actually reaches ShotPlayer.pitch rather than computing a jitter
        /// value nobody uses.
        /// </summary>
        [Test]
        public void FiringSetsTheShotPlayersPitchWithinTheJitterRange()
        {
            var pistolObject = new GameObject("Pistol");
            pistolObject.AddComponent<SphereCollider>().isTrigger = true;
            var pistol = pistolObject.AddComponent<PistolWeapon>();

            var shotPlayer = pistolObject.AddComponent<AudioSource>();
            shotPlayer.clip = AudioClip.Create("fake-shot", 100, 1, 44100, false);
            pistol.ShotPlayer = shotPlayer;

            try
            {
                pistol.Fire(Vector3.zero, Vector3.forward, aiming: false, wielder: null);

                float min = 1.0f - PistolWeapon.ShotPitchJitter;
                float max = 1.0f + PistolWeapon.ShotPitchJitter;
                Assert.GreaterOrEqual(shotPlayer.pitch, min);
                Assert.LessOrEqual(shotPlayer.pitch, max);
            }
            finally
            {
                Object.DestroyImmediate(pistolObject);
            }
        }
    }
}
