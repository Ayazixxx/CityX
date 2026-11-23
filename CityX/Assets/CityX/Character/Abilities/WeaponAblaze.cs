using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A weapon ablaze (the Fire Enhancement; owner, 2026-10-29: "and sets your sword ablaze too"): flames licking up all along a held sword's
    /// blade, or round the hand for anything else (fists, a gun, a bow), with embers and a flickering orange light. The flames are the fire's own
    /// (<see cref="FireScene.BurnAlong"/>, the CityX/Fire tongues in world space), so a swing leaves a trail of fire behind the blade. A staff
    /// in hand shows none: the enhancement has no effect on staffs.
    /// </summary>
    public sealed class WeaponAblaze : MonoBehaviour
    {
        /// <summary>The flames' strength (the fire's heat, 0 to 1), and the light's.</summary>
        public const float Strength = 0.8f;

        /// <summary>The light: soft (it is right against the body, and the game's lights grow with the inverse square of the distance), and this far above the flames' middle, metres.</summary>
        public const float LightIntensity = 0.35f;

        public const float LightRange = 3.0f;
        public const float LightLift = 0.3f;

        /// <summary>
        /// Glowing steel (a chain) is a light of its own, by night as by day (owner, 2026-11-22: "make it glow at night too make it an actual
        /// light source ingaame"): brighter than the flames' and reaching further, there being three metres of it.
        /// </summary>
        public const float GlowingSteelLightIntensity = 0.9f;

        public const float GlowingSteelLightRange = 5.5f;

        /// <summary>
        /// Round the hand, the flames run this far along its forearm and past its fingers, metres (at a body's own size), and this far over it: the
        /// fire's flames fade where they touch a surface, and ones in the hand would not show.
        /// </summary>
        public const float HandReach = 0.12f;

        public const float HandLift = 0.1f;

        private Transform _body;
        private Animator _animator;
        private Light _light;
        private bool _on;
        private float _flicker;
        private bool _glowsItself;

        /// <summary>Where the flames burn this frame (for tests): along From to To.</summary>
        public Vector3 From { get; private set; }

        public Vector3 To { get; private set; }

        /// <summary>The flames burn this frame (on, and not on a staff).</summary>
        public bool Burning { get; private set; }

        public static WeaponAblaze Make(Transform body)
        {
            var go = new GameObject("WeaponAblaze");
            var ablaze = go.AddComponent<WeaponAblaze>();
            ablaze._body = body;
            ablaze._animator = body.GetComponentInChildren<Animator>();
            var lightObject = new GameObject("Light");
            lightObject.transform.SetParent(go.transform, false);
            ablaze._light = lightObject.AddComponent<Light>();
            ablaze._light.type = LightType.Point;
            ablaze._light.color = FireLook.LightColour;
            ablaze._light.shadows = LightShadows.None;
            ablaze._light.enabled = false;
            return ablaze;
        }

        public void Show(bool on)
        {
            _on = on;
        }

        private void LateUpdate()
        {
            if (_body == null)
            {
                Destroy(gameObject);
                return;
            }
            Burning = _on && Place();
            _light.enabled = Burning;
            if (!Burning)
            {
                return;
            }
            float scale = Mathf.Max(0.01f, _body.lossyScale.y);
            _flicker += Time.deltaTime;
            Vector3 middle = Vector3.Lerp(From, To, 0.5f);
            transform.position = middle + Vector3.up * LightLift * scale;
            _light.range = (_glowsItself ? GlowingSteelLightRange : LightRange) * scale;
            _light.intensity = (_glowsItself ? GlowingSteelLightIntensity : LightIntensity) * (0.75f + 0.25f * Mathf.PerlinNoise(_flicker * 11.0f, 0.7f));
            if (!_glowsItself)
            {
                FireScene.BurnAlong(From, To, Strength, scale, Time.deltaTime);
            }
        }

        /// <summary>The held sword's blade, or the right hand when it holds no sword; false with a staff in hand.</summary>
        private bool Place()
        {
            if (_body.GetComponentInChildren<IEquippableStaff>() != null)
            {
                return false;
            }
            var blade = _body.GetComponentInChildren<ISwordBlade>();
            // A chain is not set ablaze: its steel glows and sheds sparks, its own doing (ChainWeapon). Here, only its light, from the middle of
            // where its links are (wound on, the fist).
            _glowsItself = blade != null && blade.IsChain;
            if (_glowsItself)
            {
                From = blade.BladeBaseWorld;
                To = blade.TrailTipWorld;
                return true;
            }
            if (blade != null && !_glowsItself)
            {
                From = blade.BladeBaseWorld;
                To = blade.BladeTipWorld;
                return true;
            }
            Transform hand = _animator != null && _animator.isHuman ? _animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            if (hand == null)
            {
                return false;
            }
            float reach = HandReach * _body.lossyScale.y;
            Transform elbow = _animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            Vector3 along = elbow != null ? (hand.position - elbow.position).normalized : _body.forward;
            Vector3 over = hand.position + Vector3.up * HandLift * _body.lossyScale.y;
            From = over - along * reach;
            To = over + along * reach;
            return true;
        }
    }
}
