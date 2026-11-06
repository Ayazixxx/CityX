using CityX.Character;
using UnityEngine;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// The creator's tabs along the top of its panel, PAINT, HAIR and FACE (docs/CHARACTER_CREATOR.md), one page showing at a time. A page
    /// is hidden by its CanvasGroup rather than switched off, so everything on it has woken and wired itself (ButtonAction, the pickers)
    /// whichever page is up. The body is painted only on the PAINT tab: on the others a drag on it turns it, as a drag beside it does.
    /// </summary>
    public sealed class CreatorTabs : MonoBehaviour
    {
        public const int Paint = 0, Hair = 1, Face = 2;

        public CharacterCreator Creator;

        [Tooltip("Each tab's page, in tab order.")]
        public CanvasGroup[] Pages;

        [Tooltip("Each tab's caption and the line under the one that is up.")]
        public Text[] Labels;

        public Image[] Underlines;

        private static readonly Color Lit = CreatorKit.Gold;
        private static readonly Color Unlit = CreatorKit.Hint;

        /// <summary>The tab that is up.</summary>
        public int Current { get; private set; }

        private void Awake()
        {
            Show(Current);
        }

        /// <summary>Puts the tab <paramref name="index"/> up and its page on show.</summary>
        public void Show(int index)
        {
            if (Pages == null || index < 0 || index >= Pages.Length)
            {
                return;
            }
            Current = index;
            for (int i = 0; i < Pages.Length; i++)
            {
                bool up = i == index;
                if (Pages[i] != null)
                {
                    Pages[i].alpha = up ? 1.0f : 0.0f;
                    Pages[i].interactable = up;
                    Pages[i].blocksRaycasts = up;
                }
                if (Labels != null && i < Labels.Length && Labels[i] != null)
                {
                    Labels[i].color = up ? Lit : Unlit;
                }
                if (Underlines != null && i < Underlines.Length && Underlines[i] != null)
                {
                    Underlines[i].enabled = up;
                }
            }
            if (Creator != null)
            {
                Creator.PaintEnabled = index == Paint;
            }
        }
    }
}
