using System.IO;
using System.Text;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Watches the player's own visual rig every real frame during ORDINARY
    /// PLAY and, the instant anything looks distorted, dumps a full
    /// snapshot to a plain text log file.
    ///
    /// WHY THIS EXISTS. Roughly twenty synthetic diagnostics (scripted
    /// swings, erratic input, real city geometry, console freezes at every
    /// point in the swing/release cycle) all came back stable, and asking
    /// for a live description or a screen recording hasn't produced
    /// anything a diagnostic could act on either -- every approach so far
    /// has needed either a reproduction I control, or a human noticing and
    /// accurately describing something that happens unpredictably. This
    /// removes both: it runs the whole time, notices on its own, and
    /// writes down exactly what was true at that instant -- no attention,
    /// memory or description required. Play normally; if it happens, the
    /// log file already has it.
    ///
    /// CHECKS THE THINGS ROTATION ALONE CANNOT EXPLAIN, not bone angles --
    /// see HeadMesh's own doc comment: the head is a separate, non-skinned
    /// rigid sphere, and pure bone ROTATION cannot create a visible gap on
    /// this rig (confirmed directly: head-to-hips distance stays fixed
    /// regardless of rotation). A real "stretched" or "head detached" look
    /// needs something to change POSITION or SCALE in the chain instead --
    /// exactly the two things PlayerLocomotion.ApplyHeight
    /// (localScale) and ClampFeetAboveGround (localPosition) are the only
    /// two places in the whole project that ever touch on this rig. Also
    /// watches for a bone SNAPPING (an implausibly large rotation in one
    /// frame) as a second, independent signal, since a scripted pose bug
    /// would show up there instead if it turns out to be rotation-based
    /// after all.
    ///
    /// PURELY OBSERVATIONAL. Never writes to a bone, never disables
    /// anything, never changes behaviour -- this cannot be the cause of
    /// anything it reports, only a witness to it.
    ///
    /// RUNS LATE, ON PURPOSE -- see PlayerLocomotion's own
    /// [DefaultExecutionOrder] doc comment: this MUST observe
    /// PlayerLocomotion's (and GrappleSwing's) own already-corrected bone
    /// values, never a pre-correction raw one, or this "purely
    /// observational" witness can itself manufacture a false positive
    /// purely from Unity's own unguaranteed LateUpdate ordering.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class PoseDistortionWatchdog : MonoBehaviour
    {
        [Tooltip("Found automatically in Bind() if left null.")]
        public Animator Animator;

        public GrappleSwing Swing;
        public PlayerLocomotion Locomotion;

        /// <summary>
        /// Found automatically in Bind() if left null. Used ONLY to skip
        /// the bone-snap check below while limp -- EXPLICIT BUG, found
        /// live: a real, live, non-kinematic ragdoll's own ordinary joint
        /// physics (settling from depenetration forces, a violent first
        /// step, an actual tumble) can easily exceed MaxBoneDegreesPerSecond
        /// in a single frame, and confirmed directly, this is NOT a
        /// distortion -- it is exactly what real ragdoll physics looks
        /// like. This watchdog exists to catch ANIMATOR/pose bugs; once
        /// the Animator is no longer posing these bones at all (real
        /// Rigidbodies are), comparing their frame-to-frame rotation
        /// against a threshold calibrated for animated motion is a
        /// category error, not a meaningful check. The rig-offset/scale
        /// checks above are left running regardless -- those track the
        /// Animator's own transform, not physics-driven bones, and stay
        /// meaningful either way.
        /// </summary>
        public Ragdoll Ragdoll;

        /// <summary>
        /// A DELEGATE, NOT A DIRECT DevConsoleView REFERENCE -- DevConsoleView
        /// lives in CityX.UI, which depends on CityX.Character, not the
        /// other way around; a direct field here would be a backward
        /// reference. Wired by the scene builder as `() => console.IsOpen`.
        /// </summary>
        public System.Func<bool> IsConsoleOpen;

        /// <summary>
        /// How far the whole visual rig may sit from its own root before
        /// this counts as distorted -- ClampFeetAboveGround's own documented
        /// runaway climbed from single digits to over 1000m within about 20
        /// frames, so this only needs to be well past any legitimate
        /// correction (MaxGroundClampCorrection is 2m) to catch it early.
        /// </summary>
        public const float MaxRigOffsetMetres = 3.0f;

        /// <summary>
        /// How far the rig's own scale may drift from ApplyHeight's own
        /// expected ratio (StandingHeight / BodyRig.StandingHeight, 1.0 at
        /// the default) before this counts as distorted -- a doubled or
        /// halved character is unmistakably "stretched," not a rounding
        /// error.
        /// </summary>
        public const float MinScaleRatio = 0.5f;
        public const float MaxScaleRatio = 2.0f;

        /// <summary>
        /// How fast a watched bone may rotate, in degrees per second,
        /// before this counts as a snap rather than motion -- generous on
        /// purpose (a real swing's own reach eases at 240 deg/s, see
        /// GrappleSwing.ArmPoseDegreesPerSecond); this is meant to catch an
        /// instantaneous jump, not flag ordinary fast movement.
        /// </summary>
        // 1500 until 2026-10-13: the locomotion clips are paced to the body now (ClipCadence, up to 4x), and a sprint's arm pump at 1.7x passed it.
        // The corruption this exists to catch was 10627 deg/s. Kept equal to PlayerLocomotion.MaxPlausibleArmDegreesPerSecond.
        public const float MaxBoneDegreesPerSecond = 4500.0f;

        /// <summary>
        /// A FLOOR ON THE DIVISOR, not a real frame rate -- confirmed
        /// directly, live: batch-mode test runs render frames uncapped, so
        /// Time.deltaTime can be small enough that dividing by it turns a
        /// completely ordinary few-degree sway into an apparent "4722
        /// deg/s" false positive during an ordinary real swing. 1/240s is
        /// far faster than any real display ever runs, so this only ever
        /// clamps degenerate near-zero batch-mode frames, never a
        /// genuinely fast real one.
        /// </summary>
        private const float MinDeltaTimeForSnapCheck = 1.0f / 240.0f;

        /// <summary>Minimum real seconds between two log entries, so one continuous glitch writes one snapshot, not thousands.</summary>
        public const float CooldownSeconds = 2.0f;

        private Transform _head;
        private Transform _rightUpperArm;
        private Transform _rightLowerArm;
        private Transform _leftUpperArm;
        private Transform _leftLowerArm;
        private Transform _hips;

        private Quaternion _previousHead = Quaternion.identity;
        private Quaternion _previousRightUpperArm = Quaternion.identity;
        private Quaternion _previousRightLowerArm = Quaternion.identity;
        private Quaternion _previousLeftUpperArm = Quaternion.identity;
        private Quaternion _previousLeftLowerArm = Quaternion.identity;
        private bool _havePrevious;

        private float _cooldownRemaining;

        /// <summary>Public and idempotent, callable by hand in EditMode tests where Awake never runs -- the same idiom PlayerLocomotion.Bind/GrappleSwing.Bind already use for the exact same reason.</summary>
        public void Bind()
        {
            if (Animator == null)
            {
                Animator = GetComponentInChildren<Animator>();
            }
            if (Ragdoll == null)
            {
                Ragdoll = GetComponent<Ragdoll>();
            }
            if (Animator != null && Animator.isHuman)
            {
                _head = Animator.GetBoneTransform(HumanBodyBones.Head);
                _rightUpperArm = Animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                _rightLowerArm = Animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                _leftUpperArm = Animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                _leftLowerArm = Animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                _hips = Animator.GetBoneTransform(HumanBodyBones.Hips);
            }
        }

        private void Awake()
        {
            Bind();
        }

        private void LateUpdate()
        {
            if (_cooldownRemaining > 0.0f)
            {
                _cooldownRemaining -= Time.deltaTime;
            }
            if (Animator == null)
            {
                return;
            }

            float expectedScale = Locomotion != null
                ? Locomotion.StandingHeight / PlayerLocomotion.StandingHeightDefault
                : 1.0f;
            float actualScale = Animator.transform.localScale.x;
            float rigOffset = Animator.transform.localPosition.magnitude;

            string reason = null;
            if (rigOffset > MaxRigOffsetMetres)
            {
                reason = $"visual rig offset from root = {rigOffset:F2}m (limit {MaxRigOffsetMetres}m)";
            }
            else if (expectedScale > 0.0f &&
                     (actualScale < expectedScale * MinScaleRatio || actualScale > expectedScale * MaxScaleRatio))
            {
                reason = $"visual rig scale = {actualScale:F3} (expected ~{expectedScale:F3})";
            }
            else if (_havePrevious && (Ragdoll == null || !Ragdoll.IsLimp))
            {
                float degreesPerSecond = WorstBoneSnapDegrees() / Mathf.Max(Time.deltaTime, MinDeltaTimeForSnapCheck);
                if (degreesPerSecond > MaxBoneDegreesPerSecond)
                {
                    reason = $"{WorstBoneName} snapped {degreesPerSecond:F0} deg/s (limit {MaxBoneDegreesPerSecond})";
                }
            }

            RememberCurrentBoneRotations();

            if (reason != null && _cooldownRemaining <= 0.0f)
            {
                _cooldownRemaining = CooldownSeconds;
                WriteSnapshot(reason);
            }
        }

        /// <summary>The name of the bone that moved most in the last check, for the report.</summary>
        public string WorstBoneName { get; private set; } = "";

        private float WorstBoneSnapDegrees()
        {
            float worst = 0.0f;
            Consider(_head, _previousHead, ref worst);
            Consider(_rightUpperArm, _previousRightUpperArm, ref worst);
            Consider(_rightLowerArm, _previousRightLowerArm, ref worst);
            Consider(_leftUpperArm, _previousLeftUpperArm, ref worst);
            Consider(_leftLowerArm, _previousLeftLowerArm, ref worst);
            return worst;
        }

        private void Consider(Transform bone, Quaternion previous, ref float worst)
        {
            float delta = BoneDelta(bone, previous);
            if (delta > worst)
            {
                worst = delta;
                WorstBoneName = bone.name;
            }
        }

        private static float BoneDelta(Transform bone, Quaternion previous)
        {
            return bone != null ? Quaternion.Angle(previous, bone.localRotation) : 0.0f;
        }

        private void RememberCurrentBoneRotations()
        {
            if (_head != null) _previousHead = _head.localRotation;
            if (_rightUpperArm != null) _previousRightUpperArm = _rightUpperArm.localRotation;
            if (_rightLowerArm != null) _previousRightLowerArm = _rightLowerArm.localRotation;
            if (_leftUpperArm != null) _previousLeftUpperArm = _leftUpperArm.localRotation;
            if (_leftLowerArm != null) _previousLeftLowerArm = _leftLowerArm.localRotation;
            _havePrevious = true;
        }

        /// <summary>
        /// Everything a future session would need to actually chase this
        /// down: what triggered it, whether the swing/console/animator were
        /// involved, and every watched bone's own current rotation, all in
        /// one plain-text append so nothing needs Unity's own Console open
        /// to be read back later.
        /// </summary>
        private void WriteSnapshot(string reason)
        {
            var sb = new StringBuilder();
            sb.AppendLine("==================================================");
            sb.AppendLine($"CityX pose distortion watchdog -- {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Reason: {reason}");
            sb.AppendLine($"Frame: {Time.frameCount}");
            sb.AppendLine($"Swing.IsActive: {(Swing != null ? Swing.IsActive.ToString() : "no GrappleSwing wired")}");
            sb.AppendLine($"Console.IsOpen: {(IsConsoleOpen != null ? IsConsoleOpen.Invoke().ToString() : "no console hook wired")}");
            sb.AppendLine($"Animator.enabled: {Animator.enabled}");
            for (int layer = 0; layer < Animator.layerCount; layer++)
            {
                var playing = new System.Text.StringBuilder();
                foreach (AnimatorClipInfo clip in Animator.GetCurrentAnimatorClipInfo(layer))
                {
                    if (clip.clip != null && clip.weight > 0.01f) playing.Append($"{clip.clip.name} {clip.weight:0.00}; ");
                }
                foreach (AnimatorClipInfo clip in Animator.GetNextAnimatorClipInfo(layer))
                {
                    if (clip.clip != null && clip.weight > 0.01f) playing.Append($"-> {clip.clip.name} {clip.weight:0.00}; ");
                }
                if (playing.Length > 0)
                {
                    sb.AppendLine($"Layer {layer} ({Animator.GetLayerWeight(layer):0.00}){(Animator.IsInTransition(layer) ? " in transition" : "")}: {playing}");
                }
            }
            sb.AppendLine($"Animator.transform.localPosition: {Animator.transform.localPosition:F3}");
            sb.AppendLine($"Animator.transform.localScale: {Animator.transform.localScale:F3}");
            sb.AppendLine($"Player position: {transform.position:F2}");
            AppendBone(sb, "Hips", _hips);
            AppendBone(sb, "Head", _head);
            AppendBone(sb, "RightUpperArm", _rightUpperArm);
            AppendBone(sb, "RightLowerArm", _rightLowerArm);
            AppendBone(sb, "LeftUpperArm", _leftUpperArm);
            AppendBone(sb, "LeftLowerArm", _leftLowerArm);
            sb.AppendLine("==================================================");
            sb.AppendLine();

            string message = sb.ToString();
            Debug.LogWarning(message);
            try
            {
                File.AppendAllText(LogPath(), message);
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"CityX: pose distortion watchdog couldn't write its own log file: {exception.Message}");
            }
        }

        private static void AppendBone(StringBuilder sb, string name, Transform bone)
        {
            sb.AppendLine(bone != null
                ? $"{name}.localRotation: {bone.localRotation.eulerAngles:F1}"
                : $"{name}: not found");
        }

        private static string LogPath()
        {
#if UNITY_EDITOR
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string directory = Path.Combine(projectRoot, "Logs");
#else
            string directory = Application.persistentDataPath;
#endif
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, "pose_distortion_watchdog.log");
        }
    }
}
