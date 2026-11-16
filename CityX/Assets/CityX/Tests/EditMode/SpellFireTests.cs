using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The fire spells' look in numbers (SpellFire, docs/FIRE.md "The fire spells"): the core heating as it flies, the wake as thick at any
    /// frame rate, a charged burst bigger but never wild, the stream's fire dying at a wall, and the heat in five bold bands.
    /// </summary>
    public sealed class SpellFireTests
    {
        [Test]
        public void TheCoreLeavesWarmAndArrivesWhiteHot()
        {
            Assert.AreEqual(0.0f, SpellFire.CoreHeatAt(0.0f), 1e-5f);
            float last = 0.0f;
            for (float t = 0.05f; t <= SpellFire.CoreHeatSeconds; t += 0.05f)
            {
                float heat = SpellFire.CoreHeatAt(t);
                Assert.Greater(heat, last, $"the core cooled at {t:0.00} s");
                last = heat;
            }
            Assert.AreEqual(1.0f, SpellFire.CoreHeatAt(SpellFire.CoreHeatSeconds), 1e-4f, "not fully hot by the end of its heating");
            Assert.AreEqual(1.0f, SpellFire.CoreHeatAt(5.0f), 1e-5f, "a long flight over-heated it");
        }

        [Test]
        public void TheWakeIsAsThickAtAnyFrameRate()
        {
            float debt = 0.0f;
            int once = SpellFire.WakePuffs(2.0f, 1.0f, ref debt);
            debt = 0.0f;
            int inSteps = 0;
            for (int i = 0; i < 40; i++)
            {
                inSteps += SpellFire.WakePuffs(0.05f, 1.0f, ref debt);
            }
            Assert.AreEqual(once, inSteps, 1, "the wake is thinner or thicker when the frames are shorter");
            Assert.AreEqual(Mathf.RoundToInt(2.0f / SpellFire.WakeSpacingMetres), once, 1, "a puff every spacing");
            debt = 0.0f;
            Assert.Less(SpellFire.WakePuffs(2.0f, 2.5f, ref debt), once, "a charged ball's puffs are bigger and so fewer to a metre");
            debt = 0.0f;
            Assert.AreEqual(0, SpellFire.WakePuffs(-1.0f, 1.0f, ref debt), "a ball standing still left a wake");
        }

        [Test]
        public void AChargedBallBurstsBiggerButNeverWild()
        {
            Assert.AreEqual(SpellFire.BurstPuffs, SpellFire.Scaled(SpellFire.BurstPuffs, 1.0f));
            Assert.AreEqual(SpellFire.BurstPuffs * 2, SpellFire.Scaled(SpellFire.BurstPuffs, FireballCaster.StaffMostSize), "a full charge throws twice the fire");
            Assert.AreEqual(SpellFire.BurstPuffs * 2, SpellFire.Scaled(SpellFire.BurstPuffs, 9.0f), "past a full charge it kept growing");
            Assert.Greater(SpellFire.ScorchMetres(FireballCaster.StaffMostSize), SpellFire.ScorchMetres(1.0f));
            Assert.Greater(SpellFire.ShakeMetres(FireballCaster.StaffMostSize), SpellFire.ShakeMetres(1.0f));
            Assert.Greater(SpellFire.ShakeReach(FireballCaster.StaffMostSize), SpellFire.ShakeReach(1.0f));
            Assert.Less(SpellFire.ShakeMetres(1.0f), 0.06f, "a plain fireball shakes the camera more than a nudge");
            Assert.Less(SpellFire.ShakeMetres(9.0f), Explosion.ShakeMetres * 0.5f, "a fireball shakes like a grenade");
        }

        [Test]
        public void AStreamsPuffDiesWhereTheStreamMeetsAWall()
        {
            float full = FlameStreamRules.Range / SpellFire.JetSpeed;
            Assert.AreEqual(3.0f, SpellFire.JetLife(full, 3.0f) * SpellFire.JetSpeed, 1e-4f, "a puff flew past the wall 3 m off");
            Assert.AreEqual(full, SpellFire.JetLife(full, 100.0f), 1e-5f, "with nothing in the way it lived past its reach");
            Assert.Greater(SpellFire.JetLife(full, 0.0f), 0.0f, "a staff against a wall shows no fire at all");
            Assert.Greater(SpellFire.JetLife(full, -1.0f), 0.0f);
        }

        [Test]
        public void TheHeatComesInFiveBoldBandsHotterNeverDarker()
        {
            Assert.AreEqual(-1, SpellFire.BandOf(0.0f), "cold gas burns");
            Assert.AreEqual(0.0f, SpellFire.Band(0.0f).a, "cold gas has a colour");
            Assert.AreEqual(SpellFire.BandColours.Length - 1, SpellFire.BandOf(1.0f), "the hottest is not white-hot");
            int bands = 0;
            int last = -1;
            float lastBrightness = 0.0f;
            for (float heat = 0.0f; heat <= 1.0f; heat += 0.005f)
            {
                int band = SpellFire.BandOf(heat);
                Assert.GreaterOrEqual(band, last, $"hotter fell to a cooler band at {heat:0.000}");
                if (band != last)
                {
                    bands++;
                    last = band;
                }
                Color colour = SpellFire.Band(heat);
                float brightness = colour.r * 0.3f + colour.g * 0.59f + colour.b * 0.11f;
                Assert.GreaterOrEqual(brightness, lastBrightness - 1e-5f, $"hotter got darker at {heat:0.000}");
                lastBrightness = brightness;
            }
            Assert.AreEqual(SpellFire.BandColours.Length, bands, "not five bands");
            // The white-hot stays fire coloured: the reference's #fff3d0, as bright as the fire spells' white is, tone-mapped to grey.
            Color white = SpellFire.BandColours[4];
            Assert.Greater(white.r - white.b, 0.35f, "the white-hot is so pale it greys out");
        }
    }
}
