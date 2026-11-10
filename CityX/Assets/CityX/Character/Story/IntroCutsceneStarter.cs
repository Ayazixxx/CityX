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
    ///
    /// A FRIEND JOINING SOMEONE ELSE'S WORLD (bug hunt N2, 2026-11-06): this sits on the scene's baked body, which on their machine is the
    /// host's. Their own body is spawned later and claimed (LocalPlayerBody), and the entrance waits for it and plays on it: before, it played
    /// on the host's body and then switched that body's input back on, so the friend's keys drove the host's copy.
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
                ScreenBlack.FadeTo(0.0f, 0.5f);   // no entrance to come up out of the black
                return;
            }
            PlaySession.IsNewCharacter = false;

            if (JoiningSomeoneElsesWorld())
            {
                PlayOnClaimedBody();
                return;
            }
            Play(gameObject);
        }

        /// <summary>The entrance on the body this machine plays once it is claimed (now, if it already is), not on this one. Public for a test.</summary>
        public void PlayOnClaimedBody()
        {
            if (LocalPlayerBody.Current != null)
            {
                OnBodyClaimed(LocalPlayerBody.Current);
                return;
            }
            LocalPlayerBody.Changed += OnBodyClaimed;
        }

        /// <summary>A client of a session this machine does not host: the scene's baked body here is the host's.</summary>
        private static bool JoiningSomeoneElsesWorld()
        {
            FishNet.Managing.NetworkManager manager = FishNet.InstanceFinder.NetworkManager;
            return manager != null && manager.IsClientStarted && !manager.IsServerStarted;
        }

        private void OnBodyClaimed(GameObject body)
        {
            if (body == null)
            {
                return;
            }
            LocalPlayerBody.Changed -= OnBodyClaimed;
            Controller = body.GetComponent<CharacterController>();
            InputReader = body.GetComponent<PlayerInputReader>();
            Animator = body.GetComponentInChildren<Animator>();
            Locomotion = body.GetComponent<PlayerLocomotion>();
            Play(body);
        }

        private void OnDestroy()
        {
            LocalPlayerBody.Changed -= OnBodyClaimed;
        }

        /// <summary>The entrance on <paramref name="body"/>: this machine's own.</summary>
        private void Play(GameObject body)
        {
            if (OrbitCamera == null || InputReader == null || CameraTransform == null
                || Animator == null)
            {
                Debug.LogWarning("CityX: IntroCutsceneStarter is missing a required reference -- " +
                                  "skipping the new character's entrance rather than risk a broken one.");
                ScreenBlack.FadeTo(0.0f, 0.5f);
                return;
            }

            InputReader.enabled = false;
            OrbitCamera.enabled = false;

            _cutscene = body.AddComponent<IntroCutscene>();
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
