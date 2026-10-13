using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Lays <see cref="BodyPostureSettings"/> over the animation on one body: the head tipped up, the legs moved back under the torso.
    ///
    /// AFTER THE ANIMATOR, BEFORE EVERYTHING ELSE that poses the body in LateUpdate (PlayerLocomotion's aim, climbing and foot IK), so those
    /// work from the adjusted pose. Only while the Animator is live: it rewrites the head every frame, so a turn added here never piles up;
    /// a frozen body (the creator's T-pose, a ragdoll) is left alone.
    ///
    /// The legs move by their thigh joints' positions, which the humanoid Animator never writes (no translation DoF), so the offset is set
    /// from the rest position each frame rather than added.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class BodyPosture : MonoBehaviour
    {
        private Animator _animator;
        private Transform _head;
        private Transform _thighL;
        private Transform _thighR;
        private Vector3 _thighLRest;
        private Vector3 _thighRRest;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            if (_animator == null || !_animator.isHuman)
            {
                enabled = false;
                return;
            }
            _head = _animator.GetBoneTransform(HumanBodyBones.Head);
            _thighL = _animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            _thighR = _animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            if (_thighL != null) _thighLRest = _thighL.localPosition;
            if (_thighR != null) _thighRRest = _thighR.localPosition;
        }

        private void LateUpdate()
        {
            if (_animator == null || !_animator.enabled)
            {
                return;
            }
            Apply();
        }

        /// <summary>
        /// Lays the posture over the pose the bones are in now. Called by LateUpdate after the Animator; public so a capture that poses the
        /// body by hand (a clip sampled straight onto it) can show the posture too. Call once per freshly posed frame.
        /// </summary>
        public void Apply()
        {
            if (_animator == null)
            {
                Awake();
            }
            BodyPostureSettings settings = BodyPostureSettings.Current;
            if (_head != null && (settings.HeadPitchUp != 0.0f || settings.HeadTurnRight != 0.0f || settings.HeadTiltRight != 0.0f))
            {
                // The head bone's own axes are the body's (a T-pose rig, identity at rest): -x lifts the face, +y turns it right, -z tilts it
                // toward the right shoulder. The hair is parented to the head bone, so it turns with it.
                _head.localRotation *= Quaternion.Euler(-settings.HeadPitchUp, settings.HeadTurnRight, -settings.HeadTiltRight);
            }
            // Back is -z in the hips' space (the body faces +z). Set from rest each frame: the Animator never writes these.
            Vector3 back = Vector3.back * settings.LegsBack;
            if (_thighL != null) _thighL.localPosition = _thighLRest + back;
            if (_thighR != null) _thighR.localPosition = _thighRRest + back;
        }
    }
}
