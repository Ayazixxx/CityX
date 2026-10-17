using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// The /claim command's target: does the player own the building they
    /// are standing in yet, and if not, can they claim it right now. Ported
    /// from player.gd's try_claim_building()/_building_extents().
    ///
    /// NO NEW BUILDING-DETECTION LOGIC. Godot's own version reads an
    /// _indoor_areas stack it maintains itself; this port already has the
    /// exact same thing in PlayerAudio.CurrentBuilding (innermost building,
    /// or null outdoors), built for footstep/ambience switching. Reusing it
    /// here means claim detection is never a foot out of sync with what the
    /// player actually hears.
    ///
    /// NO DIRECT REFERENCE TO ClaimBanner. CityX.UI depends on
    /// CityX.Character, not the other way round, so this reports outcomes
    /// through a plain C# event instead -- CityX.UI is free to listen (see
    /// PlayerChat, which wires ClaimSystem.BannerMessage to ClaimBanner in
    /// its own Awake) without this assembly ever knowing UI exists.
    ///
    /// CLAIMED SPAWN POSITION IS TRACKED BUT NOT YET CONSUMED. Godot's
    /// respawn() reads _claimed_spawn_position when set. This port's
    /// PlayerVitals.Revive() does not reposition the player at all -- there
    /// is no respawn-position system here yet for any spawn, claimed or
    /// default. ClaimedSpawnPosition is exposed with the same shape Godot
    /// uses so a future respawn system has something to read, but wiring it
    /// in now would mean inventing that whole system as a side effect of
    /// this one, so it stays unconsumed and this gap is named rather than
    /// silently papered over.
    /// </summary>
    public sealed class ClaimSystem : MonoBehaviour
    {
        public const string SuccessText = "BUILDING ACQUIRED";
        public const string FailureText = "YOU MUST BE INSIDE A BUILDING";
        public const string AlreadyOwnedText = "YOU ALREADY OWN THIS BUILDING";

        public PlayerAudio Audio;
        public PlayerVitals Vitals;
        public AudioSource AcquiredSoundSource;

        [Tooltip("The scene's single camera, orbited during the cutscene.")]
        public Transform CameraTransform;

        [Tooltip("Disabled for the duration of the orbit, alongside the camera it drives.")]
        public ThirdPersonCamera OrbitCamera;

        [Tooltip("Disabled for the duration of the orbit, so the player can't walk off mid-cutscene.")]
        public PlayerInputReader InputReader;

        public bool HasClaimedBuilding { get; private set; }
        public Vector3 ClaimedSpawnPosition { get; private set; }
        public bool IsInCutscene => _cutscene != null;

        /// <summary>Fired on a successful claim with (groundCentre, size).</summary>
        public event System.Action<Vector3, Vector3> BuildingClaimed;

        /// <summary>Fired with the text a banner should show, for every outcome (success, failure, already-owned).</summary>
        public event System.Action<string> BannerMessage;

        private Transform _claimedBuilding;
        private ClaimCutscene _cutscene;

        /// <summary>
        /// Attempts to claim whatever building the player is currently
        /// standing in. Returns whether the claim went through -- a false
        /// result still shows a banner explaining why.
        /// </summary>
        public bool TryClaim()
        {
            if (IsInCutscene || (Vitals != null && Vitals.IsDead))
            {
                return false;
            }

            Transform area = Audio != null ? Audio.CurrentBuilding : null;
            if (area == null)
            {
                BannerMessage?.Invoke(FailureText);
                return false;
            }
            if (area == _claimedBuilding)
            {
                BannerMessage?.Invoke(AlreadyOwnedText);
                return false;
            }

            BoxCollider box = area.GetComponent<BoxCollider>();
            Vector3 size = box != null ? box.size : Vector3.one * 12.0f;
            Vector3 centre = area.position;
            var groundCentre = new Vector3(centre.x, centre.y - size.y * 0.5f, centre.z);

            _claimedBuilding = area;
            ClaimedSpawnPosition = transform.position;
            HasClaimedBuilding = true;

            BannerMessage?.Invoke(SuccessText);
            if (AcquiredSoundSource != null)
            {
                AcquiredSoundSource.Play();
            }
            StartCutscene(groundCentre, size);
            BuildingClaimed?.Invoke(groundCentre, size);
            return true;
        }

        private void StartCutscene(Vector3 groundCentre, Vector3 size)
        {
            if (CameraTransform == null)
            {
                return;
            }
            if (InputReader != null) InputReader.enabled = false;
            if (OrbitCamera != null) OrbitCamera.enabled = false;

            _cutscene = gameObject.AddComponent<ClaimCutscene>();
            _cutscene.Finished += OnCutsceneFinished;
            _cutscene.Begin(CameraTransform, transform.position, groundCentre, size);
        }

        private void OnCutsceneFinished()
        {
            if (InputReader != null) InputReader.enabled = true;
            if (OrbitCamera != null) OrbitCamera.enabled = true;

            if (_cutscene != null)
            {
                _cutscene.Finished -= OnCutsceneFinished;
                Destroy(_cutscene);
                _cutscene = null;
            }
        }
    }
}
