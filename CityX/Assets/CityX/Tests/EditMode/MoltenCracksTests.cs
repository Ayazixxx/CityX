using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The molten cracks a fireball burns into what it hits (MoltenCracks, docs/FIRE.md "The fire spells"): a network running out from the
    /// middle that never reaches its quad's edge, no two alike, white-hot at the burst and cooling through the fire's bands to nothing.
    /// </summary>
    public sealed class MoltenCracksTests
    {
        private const int Size = MoltenCracks.TextureSize;

        [Test]
        public void TheCracksRunOutFromTheMiddleAndNeverReachTheQuadsEdge()
        {
            Color[] cracks = MoltenCracks.Paint(Size, 811);
            float middle = 0.0f;
            float far = 0.0f;
            float edge = 0.0f;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float r = new Vector2((x + 0.5f) / Size * 2.0f - 1.0f, (y + 0.5f) / Size * 2.0f - 1.0f).magnitude;
                    float a = cracks[y * Size + x].a;
                    if (r < 0.1f) middle = Mathf.Max(middle, a);
                    if (r > 0.5f && r < 0.85f) far = Mathf.Max(far, a);
                    if (r > 0.99f) edge = Mathf.Max(edge, a);
                }
            }
            Assert.Greater(middle, 0.9f, "no crack where it struck");
            Assert.Greater(far, 0.9f, "the cracks stop short of running out");
            Assert.Less(edge, 0.02f, "the cracks run off the quad's edge, which would show its square");
        }

        [Test]
        public void EachVariantIsANetworkOfCracksNotABlob()
        {
            Color[] one = MoltenCracks.Paint(Size, 811);
            Color[] two = MoltenCracks.Paint(Size, 908);
            int solid = 0;
            int differ = 0;
            for (int i = 0; i < one.Length; i++)
            {
                if (one[i].a > 0.9f) solid++;
                if (Mathf.Abs(one[i].a - two[i].a) > 0.5f) differ++;
            }
            float share = solid / (float)one.Length;
            Assert.Greater(share, 0.02f, "hardly a crack in it");
            Assert.Less(share, 0.25f, "a solid blob, not cracks");
            Assert.Greater(differ, one.Length / 50, "two variants are the same cracks");
            Assert.Greater(one[(Size / 2) * Size + Size / 2].b, 0.9f, "a crack's middle is not white-hot under its tint");
        }

        [Test]
        public void TheCracksCoolFromWhiteHotThroughRedToNothing()
        {
            Assert.AreEqual(SpellFire.BandColours.Length - 1, SpellFire.BandOf(MoltenCracks.HeatAt(0.0f)), "not white-hot at the burst");
            int band = SpellFire.BandOf(MoltenCracks.HeatAt(0.0f));
            bool wasRed = false;
            for (float t = 0.0f; t < MoltenCracks.CoolSeconds; t += 0.05f)
            {
                int now = SpellFire.BandOf(MoltenCracks.HeatAt(t));
                Assert.LessOrEqual(now, band, $"the cracks grew hotter at {t:0.00} s");
                band = now;
                wasRed |= now == 1;
            }
            Assert.IsTrue(wasRed, "they never glowed red on the way down");
            Assert.AreEqual(0.0f, MoltenCracks.TintAt(MoltenCracks.CoolSeconds).a, "still glowing once cold");
            Assert.Greater(MoltenCracks.TintAt(0.0f).r, 1.0f, "too dim at its hottest for the bloom");
        }
    }
}
