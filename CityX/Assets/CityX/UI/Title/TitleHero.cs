using System.Collections.Generic;
using CityX.Character;
using CityX.Core;
using UnityEngine;

namespace CityX.UI
{
    /// <summary>
    /// The character last played, standing on the title's rooftop in their idle with their primary weapon in hand (owner, 2026-11-11: "on the
    /// right of the screen is your most recently played character standing on top of a building in idle stance, holding their primary weapon
    /// equipped").
    ///
    /// A WHOLE PLAYER'S BODY, NOT A STILL PREVIEW like the character list's: only a live body breathes in its idle and holds a weapon the way
    /// the game does (each weapon's own hold, from its own clips). It is kept switched off in the scene and told it is nobody's to play
    /// (<see cref="CharacterIdentity.StandBy"/>) before it wakes, so it counts no playtime and saves nothing however long the title is up, even
    /// with a character still chosen from the last game.
    /// </summary>
    public sealed class TitleHero : MonoBehaviour
    {
        [Tooltip("The body: a player's (Player.prefab, with an inventory), switched off in the scene until it is given a character.")]
        public GameObject Body;

        /// <summary>The character standing there, or null (none made yet).</summary>
        public CharacterSave Showing { get; private set; }

        private void Start()
        {
            CharacterSources.Current.List(characters =>
            {
                if (this != null && Showing == null)
                {
                    Stand(MostRecent(characters));
                }
            });
        }

        /// <summary>The character played last, or null for none.</summary>
        public static CharacterSave MostRecent(IReadOnlyList<CharacterSave> characters)
        {
            CharacterSave latest = null;
            if (characters == null)
            {
                return null;
            }
            foreach (CharacterSave character in characters)
            {
                if (character != null && (latest == null || character.LastPlayedUnixSeconds > latest.LastPlayedUnixSeconds))
                {
                    latest = character;
                }
            }
            return latest;
        }

        /// <summary>Wakes the body as <paramref name="character"/>: their look, their clothes, their primary weapon in hand. Null leaves it off.</summary>
        public void Stand(CharacterSave character)
        {
            if (Body == null || character == null || Showing != null)
            {
                return;
            }
            var identity = Body.GetComponent<CharacterIdentity>();
            if (identity != null)
            {
                identity.StandBy();
            }
            Body.SetActive(true);
            Showing = character;
            if (identity != null)
            {
                identity.ApplyLook(character);
                identity.PutOnClothes(character);
            }
        }
    }
}
