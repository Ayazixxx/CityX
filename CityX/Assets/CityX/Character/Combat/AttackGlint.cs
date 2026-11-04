using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// AN ENEMY'S TELL MADE PLAIN (owner, 2026-10-24: "the telegraph of the enemies attack is either very short or non existent"): a star glints
    /// on the blade or the claw as an enemy's attack holds at the top of its wind-up, with a soft ting, so the strike can be seen and heard
    /// coming and dashed. Shown by the enemy's brain when its swing begins its tell (SoulsSwordCombat.Telegraphed, CrimsonClaw's wind-up).
    /// Drawn in code: four streaks crossing, facing the camera, flaring and fading; the sparks' material (BlockDeflectEffect).
    /// </summary>
    public sealed class AttackGlint : MonoBehaviour
    {
        /// <summary>The name a networked enemy's tell is announced by, so every other player's copy of it glints too (PlayerAnimationDriver.RemoteGlinted).</summary>
        public const string StateName = "attack_glint";

        /// <summary>How long the glint shows, seconds: flaring out, then fading.</summary>
        public const float Seconds = 0.32f;

        /// <summary>The longest arm of the star at its brightest, metres.</summary>
        public const float ArmMetres = 0.5f;

        /// <summary>The short arms (the diagonals), as a share of the long ones.</summary>
        public const float ShortArmShare = 0.45f;

        /// <summary>A spike's width at its middle, metres.</summary>
        public const float LineWidth = 0.06f;

        /// <summary>The ting's loudness: a cue, softer than a blade meeting a guard.</summary>
        public const float Volume = 0.3f;

        private static readonly Color GlintColour = new Color(1.0f, 0.95f, 0.7f);

        private static readonly AnimationCurve Taper = new AnimationCurve(new Keyframe(0.0f, 0.0f), new Keyframe(0.5f, 1.0f), new Keyframe(1.0f, 0.0f));

        /// <summary>How long the flash of a blade striking a guard shows, its arm and its width: bigger and quicker than a tell.</summary>
        public const float FlashSeconds = 0.16f;

        public const float FlashArmMetres = 0.9f;

        public const float FlashLineWidth = 0.1f;

        /// <summary>How many glints have been shown (for the tests).</summary>
        public static int Shown { get; private set; }

        private LineRenderer[] _arms;
        private Vector3 _point;
        private float _elapsed;
        private float _seconds;
        private float _armMetres;
        private float _lineWidth;

        /// <summary>A glint at <paramref name="point"/>, and the ting.</summary>
        public static void Show(Vector3 point)
        {
            Shown++;
            if (!Application.isPlaying)
            {
                return;
            }
            Spawn(point, Seconds, ArmMetres, LineWidth);
            AudioSource.PlayClipAtPoint(BlockDeflectEffect.Ting(), point, Volume);
        }

        /// <summary>The same star as the flash of a blade striking a guard: silent, the clash has its own sound.</summary>
        public static void Flash(Vector3 point)
        {
            if (Application.isPlaying)
            {
                Spawn(point, FlashSeconds, FlashArmMetres, FlashLineWidth);
            }
        }

        private static void Spawn(Vector3 point, float seconds, float armMetres, float lineWidth)
        {
            var glint = new GameObject("AttackGlint").AddComponent<AttackGlint>();
            glint._seconds = seconds;
            glint._armMetres = armMetres;
            glint._lineWidth = lineWidth;
            glint.Begin(point);
        }

        private void Begin(Vector3 point)
        {
            _point = point;
            transform.position = point;
            _arms = new LineRenderer[4];
            for (int i = 0; i < _arms.Length; i++)
            {
                var arm = new GameObject("Arm").AddComponent<LineRenderer>();
                arm.transform.SetParent(transform, false);
                arm.material = BlockDeflectEffect.SparkMaterial();
                arm.useWorldSpace = true;
                arm.positionCount = 3;
                arm.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                arm.receiveShadows = false;
                arm.widthCurve = Taper;   // a spike: wide at the middle, a point at each end
                _arms[i] = arm;
            }
            Draw();
        }

        private void LateUpdate()
        {
            _elapsed += Time.deltaTime;
            if (_elapsed >= _seconds)
            {
                Destroy(gameObject);
                return;
            }
            Draw();
        }

        /// <summary>The star in the camera's plane: up-down and across long, the diagonals short, flaring out then shrinking as it fades.</summary>
        private void Draw()
        {
            Camera camera = Camera.main;
            Vector3 right = camera != null ? camera.transform.right : Vector3.right;
            Vector3 up = camera != null ? camera.transform.up : Vector3.up;
            float t = Mathf.Clamp01(_elapsed / _seconds);
            float size = Mathf.Sin(t * Mathf.PI) * _armMetres;   // out and back in
            float fade = 1.0f - t * t;
            Color colour = new Color(GlintColour.r, GlintColour.g, GlintColour.b, fade);
            Vector3[] directions =
            {
                up, right, (up + right).normalized * ShortArmShare, (up - right).normalized * ShortArmShare,
            };
            for (int i = 0; i < _arms.Length; i++)
            {
                LineRenderer arm = _arms[i];
                arm.SetPosition(0, _point - directions[i] * size);
                arm.SetPosition(1, _point);
                arm.SetPosition(2, _point + directions[i] * size);
                arm.widthMultiplier = _lineWidth * (0.5f + fade * 0.5f);
                arm.startColor = arm.endColor = colour;
            }
        }
    }
}
