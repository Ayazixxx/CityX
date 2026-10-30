using System.Collections.Generic;
using CityX.Character;
using UnityEngine;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// THE DAMAGE NUMBERS: every blow this machine's player lands shows its damage at the spot it hit, Fortnite's way (DamageNumberLook: the
    /// pop, the rise, the colour by the share of the bar). Only your own blows, and only on your own screen: the numbers are for whoever dealt
    /// them, so nothing is sent to other players (each sees their own). Told by <see cref="DamageDealt.Landed"/>.
    ///
    /// Its own overlay canvas just under the HUD's (a canvas under another one takes the parent's sorting and fades), scaled like the HUD.
    /// </summary>
    [DefaultExecutionOrder(1000)]   // after the follow camera's LateUpdate has put the camera where this frame is drawn from
    public sealed class DamageNumbers : MonoBehaviour
    {
        /// <summary>Under the HUD (40) and every menu.</summary>
        public const int SortingOrder = 30;

        /// <summary>More than this on screen at once and the oldest goes.</summary>
        public const int MaxShown = 24;

        private static readonly Vector2 DesignResolution = new Vector2(1152.0f, 648.0f);

        [Tooltip("Whose blows are shown. Empty: this machine's player (the body its input drives).")]
        public Transform Player;

        [Tooltip("The camera the numbers are placed for. Empty: the main camera.")]
        public Camera View;

        private sealed class Number
        {
            public Text Label;
            public IDamageable Target;
            public Vector3 Point;
            public float Amount;
            public float Share;
            public bool Heavy;
            public bool Critical;
            public float Age;
            public float SinceLastHit;
            public float Side;
            public Vector2 Spread;
        }

        private readonly List<Number> _shown = new List<Number>();
        private readonly Stack<Text> _spare = new Stack<Text>();
        private RectTransform _root;
        private Canvas _canvas;

        /// <summary>The numbers on screen now, oldest first. For tests.</summary>
        public IEnumerable<Text> ShownLabels
        {
            get
            {
                foreach (Number number in _shown)
                {
                    yield return number.Label;
                }
            }
        }

        public int ShownCount => _shown.Count;

        private void Awake()
        {
            Canvas canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
            }
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas = canvas;
            canvas.sortingOrder = SortingOrder;
            CanvasScaler scaler = gameObject.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = gameObject.AddComponent<CanvasScaler>();
            }
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = DesignResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            _root = GetComponent<RectTransform>();
        }

        private void OnEnable()
        {
            DamageDealt.Landed += OnLanded;
        }

        private void OnDisable()
        {
            DamageDealt.Landed -= OnLanded;
        }

        /// <summary>Whether a blow struck by <paramref name="striker"/> is this screen's to show.</summary>
        public bool IsMine(GameObject striker)
        {
            if (striker == null)
            {
                return false;
            }
            if (Player != null)
            {
                return striker.transform == Player || striker.transform.IsChildOf(Player);
            }
            var input = striker.GetComponentInParent<PlayerInputReader>();
            return input != null && input.enabled;
        }

        private void OnLanded(DamageDealt hit)
        {
            if (!IsMine(hit.Striker))
            {
                return;
            }
            foreach (Number shown in _shown)
            {
                if (shown.Target == hit.Target && shown.SinceLastHit <= DamageNumberLook.MergeWithinSeconds
                    && shown.Age <= DamageNumberLook.MergeForSeconds)
                {
                    shown.Amount += hit.Amount;
                    shown.Share = DamageDealt.ShareOf(hit.Target, shown.Amount);
                    shown.Heavy |= hit.Heavy;
                    shown.Critical |= hit.Critical;
                    shown.SinceLastHit = 0.0f;
                    Style(shown);
                    return;
                }
            }
            if (_shown.Count >= MaxShown)
            {
                Retire(0);
            }
            float side = Random.Range(-1.0f, 1.0f);
            var number = new Number
            {
                Label = TakeLabel(),
                Target = hit.Target,
                Point = hit.Point,
                Amount = hit.Amount,
                Share = hit.ShareOfBar,
                Heavy = hit.Heavy,
                Critical = hit.Critical,
                Side = side,
                Spread = new Vector2(side, Random.Range(-0.3f, 0.3f)) * DamageNumberLook.SpreadPixels,
            };
            Style(number);
            _shown.Add(number);
            Place(number);
        }

        private static void Style(Number number)
        {
            number.Label.text = DamageNumberLook.TextFor(number.Amount);
            number.Label.color = DamageNumberLook.ColourFor(number.Share, number.Critical);
            number.Label.fontSize = Mathf.RoundToInt(DamageNumberLook.SizeFor(number.Share, number.Heavy, number.Critical));
        }

        private Text TakeLabel()
        {
            Text label = _spare.Count > 0 ? _spare.Pop() : MakeLabel();
            label.gameObject.SetActive(true);
            return label;
        }

        private Text MakeLabel()
        {
            Text label = UiKit.MakeText(transform, "DamageNumber", string.Empty, DamageNumberLook.SmallSize, Color.white);
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            // A dark edge so a white number reads on a bright wall or sky.
            Outline outline = label.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.0f, 0.0f, 0.0f, 0.85f);
            outline.effectDistance = new Vector2(2.0f, -2.0f);
            var rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(160.0f, 60.0f);
            return label;
        }

        private void Retire(int index)
        {
            Text label = _shown[index].Label;
            label.gameObject.SetActive(false);
            _spare.Push(label);
            _shown.RemoveAt(index);
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            for (int i = _shown.Count - 1; i >= 0; i--)
            {
                Number number = _shown[i];
                number.Age += dt;
                number.SinceLastHit += dt;
                if (number.Age >= DamageNumberLook.LifeSeconds || number.Label == null)
                {
                    Retire(i);
                    continue;
                }
                Place(number);
            }
        }

        /// <summary>The camera the canvas is drawn by: none for the overlay the game uses; a render test draws it through its own camera.</summary>
        private Camera CanvasCamera => _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;

        /// <summary>Puts a number where its hit is on screen, risen and drifted for its age, popped and faded.</summary>
        private void Place(Number number)
        {
            Camera view = View != null ? View : Camera.main;
            if (view == null)
            {
                number.Label.enabled = false;
                return;
            }
            Vector3 screen = view.WorldToScreenPoint(number.Point);
            float scale = screen.z > 0.0f ? DamageNumberLook.DistanceScale(screen.z) : 0.0f;
            if (scale <= 0.0f
                || !RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, CanvasCamera, out Vector2 local))
            {
                number.Label.enabled = false;
                return;
            }
            number.Label.enabled = true;
            RectTransform rect = number.Label.rectTransform;
            rect.anchoredPosition = local + number.Spread + DamageNumberLook.OffsetAt(number.Age, number.Side);
            rect.localScale = Vector3.one * (scale * DamageNumberLook.ScaleAt(number.Age));
            Color colour = number.Label.color;
            colour.a = DamageNumberLook.AlphaAt(number.Age);
            number.Label.color = colour;
        }
    }
}
