using CityX.Character;
using CityX.Core;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// What the server passes on without a look (bug hunt M14, 2026-11-06): a player's look is kept for every player who joins, so it must be
    /// one a character could have; a real one always passes.
    /// </summary>
    public sealed class RelayChecksTests
    {
        private static readonly Color Skin = new Color(0.8f, 0.6f, 0.5f);
        private static readonly Color Brown = new Color(0.35f, 0.2f, 0.1f);

        [Test]
        public void ARealLookPasses()
        {
            Assert.IsTrue(NetworkedPlayerIdentity.AppearanceIsSane("dood", Skin, CharacterHair.SpikeX, Brown, new byte[15000]), "a saved character's look");
            Assert.IsTrue(NetworkedPlayerIdentity.AppearanceIsSane("bald", Skin, CharacterHair.None, Brown, null), "a bald unpainted one");
            Assert.IsTrue(NetworkedPlayerIdentity.AppearanceIsSane("old save", Skin, "", Brown, new byte[0]), "an old save with no hairstyle written");
            Assert.IsTrue(NetworkedPlayerIdentity.AppearanceIsSane(new string('a', CharacterSave.MaximumNameLength), Skin, CharacterHair.Hyde, Brown, null),
                "a name as long as a character's may be");
            foreach (string style in CharacterHair.ValidStyles)
            {
                Assert.IsTrue(NetworkedPlayerIdentity.AppearanceIsSane("x", Skin, style, Brown, null), $"the hairstyle {style}");
            }
        }

        [Test]
        public void AnImpossibleLookIsNotPassedOn()
        {
            Assert.IsFalse(NetworkedPlayerIdentity.AppearanceIsSane("x", Skin, CharacterHair.Hyde, Brown, new byte[NetworkedPlayerIdentity.MaxPaintBytes + 1]),
                "paint bigger than any could be");
            Assert.IsFalse(NetworkedPlayerIdentity.AppearanceIsSane(new string('a', CharacterSave.MaximumNameLength + 1), Skin, CharacterHair.Hyde, Brown, null),
                "a name longer than a character's may be");
            Assert.IsFalse(NetworkedPlayerIdentity.AppearanceIsSane("x", Skin, "not a hairstyle", Brown, null), "a hairstyle the game does not have");
            Assert.IsFalse(NetworkedPlayerIdentity.AppearanceIsSane("x", new Color(float.NaN, 0.0f, 0.0f), CharacterHair.Hyde, Brown, null), "a broken skin colour");
        }
    }
}
