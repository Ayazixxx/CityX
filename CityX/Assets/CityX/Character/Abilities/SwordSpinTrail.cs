using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// The glowing swoosh round the body while the sword spin turns: a flat arc of light at the height of the sword arm, trailing behind
    /// the blade. It is a child of the body, so it turns with it -- the arc always sits behind the sword -- and only needs to be shown,
    /// faded and kept at the shoulder's height (the body rises and falls). Drawn from one shared material, so each fade is a
    /// per-renderer property block, not a new material.
    /// </summary>
    public sealed class SwordSpinTrail : MonoBehaviour
    {
        [Tooltip("How fast it brightens when a spin starts, per second.")]
        public float FadeInPerSecond = 16.0f;

        [Tooltip("How fast it dies away when the body stops turning, per second.")]
        public float FadeOutPerSecond = 5.0f;

        private static readonly int TintId = Shader.PropertyToID("_TintColor");

        private Renderer[] _renderers;
        private MaterialPropertyBlock _block;
        private Color _baseTint = new Color(0.85f, 0.85f, 0.85f, 1.0f);
        private float _intensity;
        private float _target;
        private Transform _shoulder;
        private Transform _root;
        private bool _shown = true;

        /// <summary>How bright it is now, 0 (hidden) to 1.</summary>
        public float Intensity => _intensity;

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _block = new MaterialPropertyBlock();
            if (_renderers.Length > 0 && _renderers[0].sharedMaterial != null && _renderers[0].sharedMaterial.HasProperty(TintId))
            {
                _baseTint = _renderers[0].sharedMaterial.GetColor(TintId);
            }
            Apply(force: true);
        }

        /// <summary>Keeps the swoosh at the height of this shoulder (the sword arm's), beside the body it rides on.</summary>
        public void Follow(Transform shoulder)
        {
            _shoulder = shoulder;
            _root = transform.parent;
        }

        /// <summary>Asks for it to be shown (1) or to die away (0).</summary>
        public void SetTarget(float target)
        {
            _target = Mathf.Clamp01(target);
        }

        private void LateUpdate()
        {
            float rate = _target > _intensity ? FadeInPerSecond : FadeOutPerSecond;
            _intensity = Mathf.MoveTowards(_intensity, _target, rate * Time.deltaTime);
            if (_shoulder != null && _root != null)
            {
                transform.position = new Vector3(_root.position.x, _shoulder.position.y, _root.position.z);
            }
            Apply(force: false);
        }

        private void Apply(bool force)
        {
            bool show = _intensity > 0.005f;
            if (show == _shown && !show && !force)
            {
                return;
            }
            _shown = show;
            if (_renderers == null)
            {
                return;
            }
            foreach (Renderer renderer in _renderers)
            {
                if (renderer == null)
                {
                    continue;
                }
                renderer.enabled = show;
                if (show)
                {
                    _block.SetColor(TintId, new Color(_baseTint.r * _intensity, _baseTint.g * _intensity, _baseTint.b * _intensity, 1.0f));
                    renderer.SetPropertyBlock(_block);
                }
            }
        }
    }
}
