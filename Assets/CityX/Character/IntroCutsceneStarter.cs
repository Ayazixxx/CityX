using CityX.Core;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Gives a genuinely new character their entrance. Ported from main.gd's
    /// own is_new_character threading through _spawn_into_world.
    ///
    /// A SEPARATE COMPONENT FROM CHARACTERIDENTITY, even though both react to
    /// the same PlaySession, for the same reason ClaimSystem is not folded
    /// into PlayerVitals: CharacterIdentity's job is "make the body look like
    /// who was chosen," and needs only PlayerVitals to do it. This needs the
    /// camera rig, the input reader and the animator too -- wiring all of
    /// that onto CharacterIdentity would make it know about systems that have
    /// nothing to do with appearance.
    ///
    /// Start(), NOT Awake() -- Unity does not order different components'
    /// own Awake calls against each other, and this has to run strictly
    /// after CharacterIdentity.Awake() has already applied the chosen skin
    /// and paint, or the cutscene would show off a body still wearing the
    /// default appearance for its first several frames. Start() runs after
    /// every Awake() in the scene has already completed, which guarantees
    /// that ordering without the two components needing to know about each
    /// other directly.
    ///
    /// PlaySession.IsNewCharacter IS CLEARED IMMEDIATELY, whether or not the
    /// cutscene actually manages to start (see PlaySession's own doc comment:
    /// "Cleared once used") -- a scene reload or a death/respawn must never
    /// replay this, and a missing wiring reference should fail by silently
    /// skipping the entrance rather than by replaying it forever.
    /// </summary>
    public sealed class IntroCutsceneStarter : MonoBehaviour
    {
        public CharacterController Controller;
        public ThirdPersonCamera OrbitCamera;
        public PlayerInputReader InputReader;
        public Animator Animator;
        public Transform CameraTransform;
        public PlayerLocomotion Locomotion;
        public AudioClip RuinsMusic;
        public AudioClip GroundImpactSound;

        /// <summary>
        /// Fired once the new-character entrance has handed control back --
        /// whatever else in the scene needs to stay out of it (see
        /// OldGuySpawner's own doc comment) waits on this rather than
        /// polling IntroCutscene directly, the same "react to PlaySession,
        /// don't couple to each other's internals" separation this class's
        /// own doc comment already draws with CharacterIdentity.
        /// </summary>
        public event System.Action EntranceFinished;

        private IntroCutscene _cutscene;

        private void Start()
        {
            if (PlaySession.Character == null || !PlaySession.IsNewCharacter)
            {
                return;
            }
            PlaySession.IsNewCharacter = false;

            if (OrbitCamera == null || InputReader == null || CameraTransform == null
                || Animator == null)
            {
                Debug.LogWarning("CityX: IntroCutsceneStarter is missing a required reference -- " +
                                  "skipping the new character's entrance rather than risk a broken one.");
                return;
            }

            InputReader.enabled = false;
            OrbitCamera.enabled = false;

            _cutscene = gameObject.AddComponent<IntroCutscene>();
            _cutscene.RuinsMusic = RuinsMusic;
            _cutscene.GroundImpactSound = GroundImpactSound;
            _cutscene.Finished += OnFinished;
            _cutscene.Begin(Controller, OrbitCamera, Animator, CameraTransform, Locomotion);
        }

        private void OnFinished()
        {
            if (InputReader != null) InputReader.enabled = true;
            if (OrbitCamera != null) OrbitCamera.enabled = true;

            if (_cutscene != null)
            {
                _cutscene.Finished -= OnFinished;
                Destroy(_cutscene);
                _cutscene = null;
            }

            EntranceFinished?.Invoke();
        }
    }
}
