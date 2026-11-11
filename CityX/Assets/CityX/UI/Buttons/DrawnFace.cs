using UnityEngine;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// A drawing as a button of the new style (owner, 2026-11-11, with drawings of SINGLEPLAYER, MULTIPLAYER and SETTINGZ: "I want you to
    /// replace the singleplayer, multiplayer, and settings buttons with these new drawings i made"; "when you hover over the buttons a red aura
    /// starts emanating from them as well as the slight grow"; "make the red aura animated kind of like the bloodrage spell, instead of just a
    /// red outerglow"). Pointed at, red embers rise off the letters as the Bloodrage aura's rise off a body (<see cref="EmberGraphic"/>) over a
    /// low red haze that flickers, while the button grows a little (<see cref="ButtonLook"/>); let go, the risen embers finish. Held down, the
    /// drawing darkens. The drawing and its haze are images of one size, one over the other (SourceArt/TitleMenu/menu_buttons.py makes them).
    /// </summary>
    public sealed class DrawnFace : MonoBehaviour, IButtonFace
    {
        /// <summary>How the haze flickers: how far it dips, and how fast (two waves, a slow and a quick, as a flame's light).</summary>
        public const float HazeDip = 0.35f;

        public const float HazeSlowRate = 1.7f;
        public const float HazeQuickRate = 6.3f;

        /// <summary>How much darker the drawing goes, fully held down.</summary>
        public const float PressedDarker = 0.25f;

        public Image Drawing;

        [Tooltip("The low red haze round the drawing, pointed at.")]
        public Image Aura;

        [Tooltip("The embers rising off the drawing, pointed at.")]
        public EmberGraphic Embers;

        private float _highlight;
        private float _pressed = -1.0f;
        private float _clock;

        public float Highlight
        {
            set => _highlight = value;
        }

        public float Pressed
        {
            set
            {
                if (!Mathf.Approximately(_pressed, value) && Drawing != null)
                {
                    _pressed = value;
                    Drawing.color = Color.Lerp(Color.white, Color.black, value * PressedDarker);
                }
            }
        }

        /// <summary>How strongly the haze shows now, 0 to 1.</summary>
        public float AuraShown => Aura != null ? Aura.color.a : 0.0f;

        private void Update()
        {
            Step(Time.unscaledDeltaTime);
        }

        /// <summary>The haze and the embers, <paramref name="seconds"/> on.</summary>
        public void Step(float seconds)
        {
            _clock = _highlight > 0.0f ? _clock + seconds : 0.0f;
            float flicker = 1.0f - HazeDip * (0.5f + 0.25f * Mathf.Sin(_clock * HazeSlowRate * 2.0f * Mathf.PI)
                + 0.25f * Mathf.Sin(_clock * HazeQuickRate * 2.0f * Mathf.PI + 1.3f));
            if (Aura != null)
            {
                Color colour = Aura.color;
                colour.a = _highlight * flicker;
                Aura.color = colour;   // (marks the image to be drawn again only if it changed)
            }
            if (Embers != null)
            {
                Embers.Strength = _highlight;
                Embers.Step(seconds);
            }
        }

        /// <summary>
        /// A drawing button <paramref name="height"/> tall, as wide as the drawing is, its embers soft <paramref name="emberDot"/>s coming off
        /// <paramref name="emberSources"/> (points of the drawing, as shares of it); nothing wired: the caller adds what it does.
        /// </summary>
        public static Button Make(Transform parent, string name, Sprite drawing, Sprite aura, Texture emberDot, Vector2[] emberSources,
            float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            float aspect = drawing != null ? drawing.rect.width / drawing.rect.height : 3.5f;
            rect.sizeDelta = new Vector2(height * aspect, height);
            var face = go.AddComponent<DrawnFace>();
            face.Aura = Layer(go.transform, "Aura", aura);
            var embers = new GameObject("Embers", typeof(RectTransform));
            embers.transform.SetParent(go.transform, false);
            UiKit.Stretch((RectTransform)embers.transform);
            face.Embers = embers.AddComponent<EmberGraphic>();
            face.Embers.Dot = emberDot;
            face.Embers.Sources = emberSources ?? System.Array.Empty<Vector2>();
            face.Embers.raycastTarget = false;
            face.Drawing = Layer(go.transform, "Drawing", drawing);
            face.Drawing.raycastTarget = true;   // the drawing's box takes the pointer
            face.Step(0.0f);
            var button = go.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = face.Drawing;
            go.AddComponent<ButtonLook>();
            return button;
        }

        private static Image Layer(Transform parent, string name, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            UiKit.Stretch((RectTransform)go.transform);
            Image image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }
    }
}
