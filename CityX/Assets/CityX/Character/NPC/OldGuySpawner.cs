using CityX.Core;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Places Old Guy at the right MOMENT -- ported from main.gd's own
    /// _spawn_old_guy_when_playable. Immediately for a loaded character (no
    /// cutscene to stay out of), and only once the intro cutscene has handed
    /// control back for a genuinely new one.
    ///
    /// DEFERRING RATHER THAN HIDING-THEN-SHOWING, and rather than just
    /// spawning him further away: he must not appear in any cutscene shot,
    /// and the cutscene's own camera covers too much ground for placement
    /// alone to guarantee that. Not existing yet is the only version of
    /// "not visible" with no edge cases -- no frame where a half-faded body
    /// is caught by the fall, nothing to get wrong. This is the fix for a
    /// real reported bug: the previous port instantiated Old Guy directly
    /// into the baked scene, so he stood there, fully visible, for the
    /// whole of the player's own falling entrance.
    ///
    /// READS PlaySession.IsNewCharacter IN Awake() WHEN AWAKE ACTUALLY RUNS,
    /// NOT IN Start() -- Unity gives no ordering guarantee between different
    /// components' own Start() calls, and IntroCutsceneStarter.Start()
    /// clears that flag to false the instant it runs (see its own doc
    /// comment on why: "cleared once used"). Capturing it in Awake() instead
    /// means this component's own read is guaranteed to happen before
    /// IntroCutsceneStarter's Start() could ever have cleared it, since
    /// every object's Awake() runs before any object's Start() does -- there
    /// is no other ordering this can safely rely on across two components on
    /// two different GameObjects.
    ///
    /// Bind() ALSO CAPTURES IT, GUARDED, AS A FALLBACK -- confirmed by an
    /// EditMode test failure that a plain `new GameObject().AddComponent()`
    /// inside a synchronous NUnit [Test] method does NOT reliably invoke
    /// Awake() before the test's own next line runs (Unity's EditMode test
    /// runner does not pump whatever queues Awake() the way an actual frame
    /// does). A real running game still gets the race-safe Awake() capture,
    /// since Awake() genuinely does run there before any Start(); the
    /// fallback only ever does anything in the one context where Awake()
    /// didn't already -- which is also the one context (an EditMode test
    /// calling Bind() directly, with nothing else's Start() racing it) where
    /// capturing this late is provably safe.
    /// </summary>
    public sealed class OldGuySpawner : MonoBehaviour
    {
        [Tooltip("The OldGuy prefab to instantiate.")]
        public GameObject OldGuyPrefab;

        [Tooltip("Where to spawn him -- computed once at scene-build time from the player's own start position.")]
        public Vector3 SpawnPosition;

        public Quaternion SpawnRotation = Quaternion.identity;

        [Tooltip("Waited on for a genuinely new character; spawns immediately for anyone else.")]
        public IntroCutsceneStarter IntroStarter;

        /// <summary>
        /// The instance this spawner itself created, or null if it hasn't
        /// (yet). Exists so a test can confirm what THIS component did
        /// without a scene-wide GameObject.Find("OldGuy") -- which cannot
        /// tell this spawner's own output apart from an unrelated object
        /// that happens to share the name, baked-scene leftovers included.
        /// </summary>
        public GameObject SpawnedInstance { get; private set; }

        private bool _isNewCharacterEntrance;
        private bool _isNewCharacterEntranceCaptured;
        private bool _bound;

        private void Awake()
        {
            CaptureIsNewCharacterEntrance();
        }

        private void CaptureIsNewCharacterEntrance()
        {
            if (_isNewCharacterEntranceCaptured)
            {
                return;
            }
            _isNewCharacterEntranceCaptured = true;
            _isNewCharacterEntrance = PlaySession.Character != null && PlaySession.IsNewCharacter;
        }

        private void Start()
        {
            Bind();
        }

        /// <summary>
        /// Public and idempotent so a test can call it by hand -- Start()
        /// never runs outside Play mode, the same reasoning
        /// PlayerLocomotion.Bind's own doc comment gives.
        /// </summary>
        public void Bind()
        {
            if (_bound)
            {
                return;
            }
            _bound = true;
            CaptureIsNewCharacterEntrance();

            if (!_isNewCharacterEntrance)
            {
                Spawn();
                return;
            }
            if (IntroStarter != null)
            {
                IntroStarter.EntranceFinished += OnEntranceFinished;
            }
            else
            {
                // Nothing will ever signal completion -- spawn now rather
                // than leave him permanently missing.
                Spawn();
            }
        }

        private void OnEntranceFinished()
        {
            if (IntroStarter != null)
            {
                IntroStarter.EntranceFinished -= OnEntranceFinished;
            }
            Spawn();
        }

        private void Spawn()
        {
            if (OldGuyPrefab == null)
            {
                return;
            }
            var oldGuy = Instantiate(OldGuyPrefab, SpawnPosition, SpawnRotation);
            oldGuy.name = "OldGuy";
            SpawnedInstance = oldGuy;
        }
    }
}
