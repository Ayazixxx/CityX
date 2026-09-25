using CityX.Character;
using UnityEngine;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// The reticle's reactions to shooting, drawn over the existing crosshair
    /// (PlayerHud.Crosshair). Added by PlayerHud at runtime and built lazily
    /// the first time a gun is equipped -- no HUD prefab has to be rebuilt.
    ///
    /// FROM THE THIRD-PERSON-SHOOTER RESEARCH (docs/THIRD_PERSON_PISTOL.md):
    ///  * HIT MARKER. A hit must be CONFIRMED on the reticle itself, and a
    ///    kill must read differently from a hit (four diagonal ticks: white and
    ///    quick for a hit, red and longer for a kill).
    ///  * OBSTACLE PROMPT. When scenery is between the barrel and the target,
    ///    the crosshair turns red, so a shot never quietly goes into the wall
    ///    while the reticle looks clear.
    ///  * PLAYER STATUS. The reticle dims while reloading.
    ///  * A tiny fire pulse -- feedback that you fired, NOT a spread indicator.
    ///    There is deliberately no bloom: bullets land exactly on the crosshair
    ///    (an explicit ask), and a reticle that swelled with movement or fire
    ///    would be claiming otherwise. The research's own warning: never give
    ///    feedback that misleads about where the bullet goes.
    /// </summary>
    public sealed class GunReticleView : MonoBehaviour
    {
        public const float HitMarkerSeconds = 0.16f;
        public const float KillMarkerSeconds = 0.32f;
        public const float FirePulseSeconds = 0.10f;

        public static readonly Color NormalColour = new Color(1.0f, 1.0f, 1.0f, 0.85f);
        public static readonly Color ReloadingColour = new Color(1.0f, 1.0f, 1.0f, 0.35f);
        public static readonly Color BlockedColour = new Color(1.0f, 0.25f, 0.2f, 0.95f);
        public static readonly Color HitColour = new Color(1.0f, 1.0f, 1.0f, 1.0f);
        public static readonly Color KillColour = new Color(1.0f, 0.15f, 0.12f, 1.0f);

        private PlayerHud _hud;
        private GunFeel _feel;
        private RectTransform _crosshairRect;
        private Image[] _crosshairBars;
        private RectTransform _markerRoot;
        private Image[] _markerTicks;

        private float _markerRemaining;
        private float _markerDuration = HitMarkerSeconds;
        private Color _markerColour = HitColour;
        private float _pulseRemaining;

        /// <summary>The hit/kill marker is showing (read by tests).</summary>
        public bool MarkerVisible => _markerRemaining > 0.0f;

        /// <summary>The colour the marker is currently drawn in.</summary>
        public Color MarkerColour => _markerColour;

        /// <summary>The colour the crosshair bars are currently drawn in.</summary>
        public Color CrosshairColour => _crosshairBars != null && _crosshairBars.Length > 0
            ? _crosshairBars[0].color
            : NormalColour;

        private void Awake()
        {
            _hud = GetComponent<PlayerHud>();
        }

        private void OnDestroy()
        {
            Unhook();
        }

        private void Unhook()
        {
            if (_feel != null)
            {
                _feel.Shot -= OnShot;
                _feel = null;
            }
        }

        private void Update()
        {
            if (_hud == null || _hud.Crosshair == null)
            {
                return;
            }

            if (_feel == null && _hud.Target != null)
            {
                _feel = _hud.Target.GetComponent<GunFeel>();
                if (_feel != null)
                {
                    _feel.Shot += OnShot;
                }
            }

            EnsureBuilt();
            if (_crosshairRect == null)
            {
                return;
            }

            AnimateMarker(Time.deltaTime);
            Colour crosshairState = Colour.Normal;
            if (_feel != null && _feel.HasGun)
            {
                crosshairState = _feel.BarrelBlocked ? Colour.Blocked
                    : _feel.IsReloading ? Colour.Reloading
                    : Colour.Normal;
            }
            ApplyCrosshair(crosshairState, Time.deltaTime);
        }

        private enum Colour { Normal, Reloading, Blocked }

        private void EnsureBuilt()
        {
            if (_crosshairRect != null)
            {
                return;
            }

            _crosshairRect = _hud.Crosshair.GetComponent<RectTransform>();
            if (_crosshairRect == null)
            {
                return;
            }
            _crosshairBars = _crosshairRect.GetComponentsInChildren<Image>(includeInactive: true);

            var root = new GameObject("HitMarker", typeof(RectTransform));
            _markerRoot = root.GetComponent<RectTransform>();
            _markerRoot.SetParent(_crosshairRect, false);
            _markerRoot.anchorMin = _markerRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _markerRoot.anchoredPosition = Vector2.zero;
            _markerRoot.sizeDelta = Vector2.zero;

            _markerTicks = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                float sx = (i & 1) == 0 ? -1.0f : 1.0f;
                float sy = (i & 2) == 0 ? -1.0f : 1.0f;
                var tick = new GameObject("Tick" + i, typeof(RectTransform));
                var rect = tick.GetComponent<RectTransform>();
                rect.SetParent(_markerRoot, false);
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(2.5f, 9.0f);
                rect.anchoredPosition = new Vector2(sx, sy) * 13.0f;
                // Each tick points at the centre: a diagonal strip, so the four
                // together read as an X with the middle left open.
                rect.localRotation = Quaternion.Euler(0.0f, 0.0f, sx * sy > 0.0f ? -45.0f : 45.0f);
                Image image = tick.AddComponent<Image>();
                image.raycastTarget = false;
                image.color = new Color(1.0f, 1.0f, 1.0f, 0.0f);
                _markerTicks[i] = image;
            }
        }

        private void OnShot(GunShot shot)
        {
            if (!shot.Fired)
            {
                return;
            }

            _pulseRemaining = FirePulseSeconds;

            if (shot.Killed)
            {
                ShowMarker(KillColour, KillMarkerSeconds);
            }
            else if (shot.HitDamageable)
            {
                ShowMarker(HitColour, HitMarkerSeconds);
            }
        }

        private void ShowMarker(Color colour, float seconds)
        {
            _markerColour = colour;
            _markerDuration = seconds;
            _markerRemaining = seconds;
        }

        private void AnimateMarker(float deltaTime)
        {
            if (_markerTicks == null)
            {
                return;
            }

            if (_markerRemaining > 0.0f)
            {
                _markerRemaining = Mathf.Max(0.0f, _markerRemaining - deltaTime);
            }

            float t = _markerDuration > 0.0f ? _markerRemaining / _markerDuration : 0.0f;   // 1 -> 0
            Color colour = _markerColour;
            colour.a = _markerRemaining > 0.0f ? Mathf.Clamp01(t * 1.6f) : 0.0f;
            foreach (Image tick in _markerTicks)
            {
                tick.color = colour;
            }

            // The ticks fly outward as they fade.
            float scale = 1.0f + (1.0f - t) * 0.5f;
            _markerRoot.localScale = new Vector3(scale, scale, 1.0f);
        }

        private void ApplyCrosshair(Colour state, float deltaTime)
        {
            Color colour = state == Colour.Blocked ? BlockedColour
                : state == Colour.Reloading ? ReloadingColour
                : NormalColour;
            foreach (Image bar in _crosshairBars)
            {
                // The marker ticks are children of the crosshair too; leave them to AnimateMarker.
                if (_markerTicks != null && System.Array.IndexOf(_markerTicks, bar) >= 0)
                {
                    continue;
                }
                bar.color = colour;
            }

            if (_pulseRemaining > 0.0f)
            {
                _pulseRemaining = Mathf.Max(0.0f, _pulseRemaining - deltaTime);
            }
            float pulse = _pulseRemaining / FirePulseSeconds;
            float size = 1.0f + 0.25f * pulse;
            _crosshairRect.localScale = new Vector3(size, size, 1.0f);
        }
    }
}
