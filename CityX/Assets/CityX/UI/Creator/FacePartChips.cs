using UnityEngine;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// The FACE tab's chips (FacePage): the one pressed lights up and its kind's panel shows (that kind's parts, or the line saying it has
    /// none yet), the others hidden by their CanvasGroups. It wires its chips itself when it wakes, as ButtonAction does: the title
    /// screen's creator is built by an editor builder, and a listener added there is not saved with the scene.
    /// </summary>
    public sealed class FacePartChips : MonoBehaviour
    {
        public static readonly Color LitFace = CreatorKit.Gold;
        public static readonly Color LitLabel = new Color(0.08f, 0.08f, 0.10f, 1.0f);

        [Tooltip("One chip per kind of part, in FacePartCatalog order.")]
        public Button[] Chips = new Button[0];

        [Tooltip("Each chip's background, in the same order.")]
        public Image[] ChipFaces = new Image[0];

        [Tooltip("Each chip's caption, in the same order.")]
        public Text[] ChipLabels = new Text[0];

        [Tooltip("Each kind's panel, in the same order: its parts, or the line saying it has none yet.")]
        public CanvasGroup[] Panels = new CanvasGroup[0];

        /// <summary>The kind showing, as an index into the catalog's categories.</summary>
        public int Current { get; private set; }

        private void Awake()
        {
            for (int i = 0; i < Chips.Length; i++)
            {
                int kind = i;
                if (Chips[i] != null)
                {
                    Chips[i].onClick.AddListener(() => Show(kind));
                }
            }
            Show(Current);
        }

        /// <summary>Lights the chip of <paramref name="kind"/> and shows its panel. The builder calls it too, so the saved page starts lit.</summary>
        public void Show(int kind)
        {
            if (kind < 0 || kind >= Panels.Length)
            {
                return;
            }
            Current = kind;
            for (int i = 0; i < Panels.Length; i++)
            {
                bool up = i == kind;
                if (Panels[i] != null)
                {
                    Panels[i].alpha = up ? 1.0f : 0.0f;
                    Panels[i].interactable = up;
                    Panels[i].blocksRaycasts = up;
                }
                if (i < ChipFaces.Length && ChipFaces[i] != null)
                {
                    ChipFaces[i].color = up ? LitFace : CreatorKit.Idle;
                }
                if (i < ChipLabels.Length && ChipLabels[i] != null)
                {
                    ChipLabels[i].color = up ? LitLabel : CreatorKit.Paper;
                }
            }
        }
    }
}
