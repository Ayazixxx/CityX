using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// SPANNING THE CROSSBOW (docs/ARCHERY.md; owner: "make a reload animation for the crossbow, maybe look at chivalry"). Chivalry's: the
    /// crossbow comes down off the shoulder and tips nose-down, the string is hauled back from the bow to the latch, a bolt is laid on the rail,
    /// and it comes back up. Keyed here over the gun's own reload timer (PistolWeapon.ReloadProgress, 2.5 s): the crossbow dips and tips in the
    /// hand (on top of the gun's own small reload dip) and the rifle pose lets it off the shoulder; the left hand leaves the fore-stock for the
    /// string at the bow, drags it back along the rail (the string's middle follows the hand), reaches back over the shoulder for a bolt and
    /// lays it (the bolt appears), and goes back to the fore-stock. The crossbow's own ratchet and snap play with it. Runs on copies too (the
    /// gun's reload state reaches them).
    /// </summary>
    public sealed class CrossbowSpan : MonoBehaviour
    {
        public PistolWeapon Weapon;

        [Tooltip("The string: tip, latch, tip. Its middle point follows the hand while it is hauled back.")]
        public LineRenderer String;

        /// <summary>The string's two ends and the latch, in the crossbow's frame (ArcheryBuilder lays them out).</summary>
        public Vector3 RightTip;

        public Vector3 LeftTip;

        public Vector3 Latch;

        /// <summary>Where the string sits at rest, undrawn: forward at the bow, and how far above the rail the hand holds it.</summary>
        public Vector3 StringForward;

        /// <summary>The fore-stock the left hand goes back to, and the quiver over the right shoulder, in the crossbow's frame.</summary>
        public Vector3 ForeStock;

        public static readonly Vector3 QuiverLocal = new Vector3(0.25f, 0.35f, -0.75f);

        /// <summary>The shares of the reload: lowering, hauling the string, laying the bolt, raising.</summary>
        public const float LowerEnd = 0.18f;
        public const float HaulEnd = 0.55f;
        public const float LayEnd = 0.82f;

        /// <summary>How far the crossbow dips in the hand and tips nose-down while spanned (on top of the gun's own reload dip).</summary>
        public static readonly Vector3 SpanDip = new Vector3(0.0f, -0.08f, 0.04f);

        public const float SpanTipDegrees = 35.0f;

        private PlayerLocomotion _locomotion;
        private bool _spanning;
        private float _lastProgress;

        /// <summary>How far through the spanning, 0..1, or 0 when not.</summary>
        public float Progress => _spanning ? _lastProgress : 0.0f;

        private void LateUpdate()
        {
            if (Weapon == null)
            {
                return;
            }
            bool reloading = Weapon.IsReloading;
            if (reloading && !_spanning)
            {
                Begin();
            }
            else if (!reloading && _spanning)
            {
                End();
            }
            if (!_spanning)
            {
                return;
            }
            float p = Mathf.Clamp01(Weapon.ReloadProgress);
            _lastProgress = p;

            // The crossbow dips and tips in the hand: in over the lowering, out over the raising.
            float down = p < LowerEnd ? Mathf.SmoothStep(0.0f, 1.0f, p / LowerEnd) : p > LayEnd ? 1.0f - Mathf.SmoothStep(0.0f, 1.0f, (p - LayEnd) / (1.0f - LayEnd)) : 1.0f;
            transform.localPosition += SpanDip * down;
            transform.localRotation = transform.localRotation * Quaternion.Euler(SpanTipDegrees * down, 0.0f, 0.0f);

            // The left hand: fore-stock -> the string at the bow -> hauled back to the latch -> the quiver -> the rail (laying the bolt) -> the fore-stock.
            Vector3 hand;
            float stringPull = 0.0f;
            if (p < LowerEnd)
            {
                hand = Vector3.Lerp(ForeStock, StringForward, Mathf.SmoothStep(0.0f, 1.0f, p / LowerEnd));
            }
            else if (p < HaulEnd)
            {
                stringPull = Mathf.SmoothStep(0.0f, 1.0f, (p - LowerEnd) / (HaulEnd - LowerEnd));
                hand = Vector3.Lerp(StringForward, Latch + Vector3.up * 0.02f, stringPull);
            }
            else if (p < LayEnd)
            {
                stringPull = 1.0f;
                float t = (p - HaulEnd) / (LayEnd - HaulEnd);
                hand = t < 0.5f
                    ? Vector3.Lerp(Latch + Vector3.up * 0.02f, QuiverLocal, Mathf.SmoothStep(0.0f, 1.0f, t * 2.0f))
                    : Vector3.Lerp(QuiverLocal, Latch + Vector3.up * 0.04f, Mathf.SmoothStep(0.0f, 1.0f, (t - 0.5f) * 2.0f));
            }
            else
            {
                stringPull = 1.0f;
                hand = Vector3.Lerp(Latch + Vector3.up * 0.04f, ForeStock, Mathf.SmoothStep(0.0f, 1.0f, (p - LayEnd) / (1.0f - LayEnd)));
            }
            _locomotion?.SetSupportHand(transform, hand, onlyWhenAiming: false);

            if (String != null)
            {
                String.positionCount = 3;
                String.useWorldSpace = false;
                String.SetPosition(0, RightTip);
                String.SetPosition(1, Vector3.Lerp(StringForward, Latch, stringPull));
                String.SetPosition(2, LeftTip);
            }
        }

        private void Begin()
        {
            _spanning = true;
            _locomotion = GetComponentInParent<PlayerLocomotion>();
            if (_locomotion != null)
            {
                _locomotion.RifleSpanning = true;
            }
            if (Application.isPlaying)
            {
                AudioSource.PlayClipAtPoint(ArcherySounds.Span, transform.position, 0.7f);
            }
        }

        private void End()
        {
            _spanning = false;
            _lastProgress = 0.0f;
            if (String != null)
            {
                String.SetPosition(1, Latch);
            }
            if (_locomotion != null)
            {
                _locomotion.RifleSpanning = false;
                _locomotion.SetSupportHand(transform, ForeStock, onlyWhenAiming: false);
            }
        }

        private void OnDisable()
        {
            if (_spanning)
            {
                End();
            }
        }
    }
}
