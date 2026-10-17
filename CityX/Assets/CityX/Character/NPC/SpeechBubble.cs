using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Floating text over a character's head for a chat message. Ported
    /// from character.gd's speech-bubble handling (a billboarded,
    /// no-depth-test Label3D there; a billboarded TextMesh here).
    /// </summary>
    public sealed class SpeechBubble : MonoBehaviour
    {
        public const float DurationSeconds = 6.0f;
        public const float HeightOffset = 0.35f;

        [Tooltip("Followed every frame while a message is showing.")]
        public Transform Anchor;

        [Tooltip("World-space height of the anchor's head, so the bubble sits just above it.")]
        public float AnchorHeadHeight = 1.8f;

        private TextMesh _text;
        private float _remaining;

        private void Awake()
        {
            Bind();
        }

        /// <summary>
        /// Builds the child TextMesh if it doesn't exist yet. Public and
        /// idempotent so EditMode tests, which never run Awake, can call it
        /// directly -- same reasoning as Radio.Bind().
        /// </summary>
        public void Bind()
        {
            if (_text != null)
            {
                return;
            }
            var go = new GameObject("Text");
            go.transform.SetParent(transform, false);
            _text = go.AddComponent<TextMesh>();
            _text.fontSize = 48;
            _text.characterSize = 0.09f;
            _text.anchor = TextAnchor.LowerCenter;
            _text.alignment = TextAlignment.Center;
            _text.color = Color.white;
            go.SetActive(false);
        }

        public bool IsShowing => _remaining > 0.0f;

        public void Show(string text)
        {
            Bind();
            _text.text = text;
            _remaining = DurationSeconds;
            _text.gameObject.SetActive(true);
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>
        /// The countdown advanced by one step. Public, and taking its own
        /// deltaTime rather than reading Time.deltaTime, so a test can
        /// fast-forward through the whole duration without waiting on real
        /// wall-clock seconds -- same reasoning as PlayerVitals.TickStamina.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_remaining <= 0.0f)
            {
                return;
            }
            _remaining -= deltaTime;
            if (_remaining <= 0.0f)
            {
                _text.gameObject.SetActive(false);
                return;
            }
            Transform anchor = Anchor != null ? Anchor : transform.parent;
            if (anchor != null)
            {
                transform.position = anchor.position + Vector3.up * (AnchorHeadHeight + HeightOffset);
            }
        }

        private void LateUpdate()
        {
            if (_text == null || !_text.gameObject.activeSelf)
            {
                return;
            }
            Camera cam = Camera.main;
            if (cam == null)
            {
                return;
            }
            Vector3 toCamera = transform.position - cam.transform.position;
            if (toCamera.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(toCamera, Vector3.up);
            }
        }
    }
}
