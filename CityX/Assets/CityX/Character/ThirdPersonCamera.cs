using UnityEngine;
using UnityEngine.InputSystem;

namespace CityX.Character
{
    /// <summary>
    /// Orbit camera behind the player, with a spring arm that pulls in when
    /// something is between the camera and the head.
    ///
    /// The collision arm is not polish. This city is narrow canyons and 14m
    /// streets; without it the camera spends most of its time inside a wall,
    /// and you would be judging the buildings from the inside.
    /// </summary>
    public sealed class ThirdPersonCamera : MonoBehaviour
    {
        public Transform Target;

        [Tooltip(
            "Optional. While this is limp, the camera follows its own Torso rigidbody " +
            "instead of Target -- see ApplyCameraTransform's own doc comment.")]
        public Ragdoll Ragdoll;

        /// <summary>
        /// EXPLICIT ASK: "make the camera follow the ragdoll when /sleep is
        /// used and the ragdoll is moved." Nothing moves the player's own
        /// root transform while limp (Ragdoll's own doc comment: "only the
        /// individual Rigidbodies do"), so Target alone leaves the camera
        /// frozen wherever the body went limp while physics carries the
        /// actual ragdoll somewhere else entirely -- down a slope, pushed,
        /// tumbling from a crash. A small fixed height rather than
        /// PivotHeightFraction * standing height: that fraction assumes a
        /// STANDING body (roughly chest height), which would aim the pivot
        /// well above a body lying flat on the ground.
        /// </summary>
        public const float RagdollPivotHeight = 0.4f;

        [Tooltip(
            "Where the camera pivots on the body, as a fraction of standing height. " +
            "0.73 puts it at 1.6m on a 2.2m body, matching player.gd's own " +
            "BASE_CAMERA_PIVOT_HEIGHT -- a fraction rather than a metre value so it stays " +
            "sensible when the height is changed.")]
        public float PivotHeightFraction = 0.73f;

        public float Distance = 4.5f;
        public float LookDegreesPerPixel = 0.12f;

        /// <summary>
        /// EXPLICIT BUG REPORT: "its not letting me all the way straight up
        /// it cuts off at a certain point. make it so I can look all the way
        /// up and down." Was -35/70 -- barely past the horizon looking up,
        /// and short of straight down too.
        ///
        /// The true +/-90, not just short of it -- Jetpack.
        /// JetpackMinPitchDegrees/MaxPitchDegrees already settled this exact
        /// question for its own equipped-flight widening: this camera builds
        /// its rotation from a plain Quaternion.Euler(pitch, yaw, 0) rather
        /// than a LookRotation(forward, up), so there is no forward-
        /// parallel-to-up degeneracy at the pole the way a margin would
        /// otherwise guard against -- yaw is simply visually meaningless for
        /// the one frame the camera sits exactly at the pole, same as any
        /// first/third-person camera, and never a real bug.
        /// </summary>
        public float MinPitch = -90.0f;
        public float MaxPitch = 90.0f;

        [Tooltip(
            "Metres to the right of the spine the camera sits, Fortnite/GTA-style, so the body " +
            "is off to one side of frame instead of dead centre. Reported directly: standing " +
            "square behind the head put anything small and close in front of the player -- a " +
            "dropped pistol on the ground being the concrete case -- right behind their own " +
            "silhouette, with nothing on screen to actually judge alignment against.")]
        public float ShoulderOffset = 0.85f;

        /// <summary>
        /// EXPLICIT BUG REPORT: "the pistol aimed in pose is like positioned
        /// to the left slightly make it point forward." The gun is held in
        /// the RIGHT hand, roughly along the body's own forward line (see
        /// PlayerLocomotion.ApplyGunAimPose) -- but the ordinary hip-fire
        /// ShoulderOffset above pushes the CROSSHAIR (frame centre, which
        /// PistolWeapon.Fire's own hitscan actually uses) even further right
        /// again, past where the raised arm reaches. The gun visually reads
        /// as left-of-crosshair not because it points wrong, but because the
        /// crosshair itself sits further right than the gun barrel while
        /// aiming. Pulling the camera in closer to centred (not all the way
        /// to 0 -- a real ADS view still favours the gun side a little, and
        /// 0 would put the pivot back inside the head) brings the two back
        /// into visual agreement. Tune by eye once played for real.
        /// </summary>
        public float AimShoulderOffset = 0.25f;

        [Tooltip("Metres per second ShoulderOffset eases toward AimShoulderOffset when the aim state changes -- mirrors AimZoomDegreesPerSecond's own shape for FOV.")]
        public float AimShoulderOffsetMetresPerSecond = 3.0f;

        [Tooltip("Layers the spring arm collides with. Should include the city.")]
        public LayerMask CollisionLayers = ~0;

        /// <summary>
        /// NEVER PULLS CLOSER THAN THIS, even against a wall right on top of
        /// the pivot -- a live bug report, confirmed directly with a
        /// screenshot: swinging near buildings (which is to say, swinging
        /// at all, since every anchor is on one) let this clamp down to its
        /// old 0.4m, which combined with the camera's own 0.15m near clip
        /// plane left almost no headroom -- an outstretched limb (the
        /// swing's own arm reach pose, in particular) or a nearby wall
        /// corner would end up partly inside the near clip plane or so
        /// close the lens shows nothing but a warped close-up, which is
        /// exactly what "gets distorted to lengths that shouldn't be
        /// possible" and "the camera keeps moving, the character stays
        /// still" describe -- the character never moved or deformed at
        /// all; the CAMERA was the thing sitting somewhere absurd. 1.5m
        /// still lets the camera work in this city's own narrow alleys
        /// (see this class's own doc comment) without ever getting close
        /// enough to clip through anything it is trying to look at.
        /// </summary>
        public float MinDistance = 1.5f;

        [Tooltip(
            "Field of view while aiming (see IsAiming). Narrower than the ordinary field of " +
            "view, which is read from whatever the Camera component already starts at rather " +
            "than assumed here, so this only ever has to describe how much CLOSER aiming zooms.")]
        public float AimFieldOfView = 40.0f;

        [Tooltip("Degrees of field of view crossed per second when the aim state changes.")]
        public float AimZoomDegreesPerSecond = 90.0f;

        /// <summary>
        /// Set every frame by PlayerInputReader -- see its own TickCombat,
        /// the one place that already knows whether a gun is out and right
        /// is held, or a melee weapon's own WeaponAimInput.IsAiming says so.
        /// This component stays a plain camera rig with no opinion on which
        /// input or which equipped item means "aiming"; it only zooms.
        /// </summary>
        public bool IsAiming;

        private float _yaw;
        private float _pitch = 12.0f;
        private PlayerLocomotion _player;
        private Camera _camera;
        private float _restFieldOfView;
        private float _currentShoulderOffset;

        /// <summary>
        /// Settable so IntroCutscene can hand the camera back on the same
        /// yaw/pitch it was left on when it re-enables this component --
        /// otherwise the very next LateUpdate would snap to whatever
        /// leftover _yaw/_pitch this component last held from before the
        /// cutscene disabled it, popping the view. Degrees, same convention
        /// LateUpdate's own Quaternion.Euler(pitch, yaw, 0) already uses.
        /// </summary>
        public float Yaw
        {
            get => _yaw;
            set => _yaw = value;
        }

        public float Pitch
        {
            get => _pitch;
            set => _pitch = value;
        }

        private void Start()
        {
            if (Target != null)
            {
                _player = Target.GetComponent<PlayerLocomotion>();
            }
            _camera = GetComponent<Camera>();
            if (_camera != null)
            {
                _restFieldOfView = _camera.fieldOfView;
            }
            _currentShoulderOffset = ShoulderOffset;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // POSITIONED HERE TOO, NOT ONLY IN LateUpdate. EXPLICIT BUG
            // REPORT, confirmed directly in CityMassingSceneBuilder: this
            // component lands on the SAME camera GameObject the FlyCamera
            // editor preview already positioned at a street-level walking
            // view for opening the scene without pressing play -- that
            // leftover transform is nowhere near the "4.5m behind the
            // shoulder" shot this component is actually meant to produce,
            // and until now nothing corrected it until the first LateUpdate
            // ran. Ordinarily that is one invisible frame. But
            // DevConsoleView.Open() (opened immediately on spawning in, to
            // gear up before playing -- the reported repro, every time)
            // disables this component from its own Update(), which runs
            // BEFORE LateUpdate in the same frame -- Unity skips a
            // component's remaining scheduled calls for a frame the instant
            // it is disabled, so LateUpdate never ran even once. The camera
            // was then stuck rendering from that raw, leftover position for
            // as long as the console stayed open -- coincident with the
            // player's own spawn point, looking wherever the editor preview
            // last faced -- which is a point-blank, warped close-up of
            // whatever part of the body happened to be in front of the
            // lens: exactly "opening the console to spawn the grappler"
            // looked like. Calling the same positioning logic once here
            // closes the window entirely rather than racing to win it.
            ApplyCameraTransform();
        }

        /// <summary>
        /// LateUpdate, not Update: the player moves in Update, and a camera that
        /// reads its position in the same phase is a frame behind, which reads
        /// as the body swimming against the camera.
        /// </summary>
        private void LateUpdate()
        {
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 delta = mouse.delta.ReadValue();
                _yaw += delta.x * LookDegreesPerPixel;
                _pitch = Mathf.Clamp(
                    _pitch - delta.y * LookDegreesPerPixel, MinPitch, MaxPitch);
            }

            // NO ESCAPE HANDLING HERE. There used to be a bare "Escape frees the
            // cursor" fallback, from before there was any menu to open with it.
            // PauseMenuView owns Escape exclusively now, and it matters that
            // this component does not also react to the same key: re-reading
            // Escape here would race PauseMenuView's own Close(), which also
            // sets Cursor.lockState in the same frame -- whichever ran last
            // would win by accident, sometimes leaving the cursor free right
            // after the menu closed.

            ApplyCameraTransform();
        }

        /// <summary>
        /// The whole camera-placement job -- pivot, orbit rotation, shoulder
        /// offset, wall-collision pull-in, field of view -- pulled out of
        /// LateUpdate so Start can run it once immediately (see Start's own
        /// doc comment for why a single un-positioned frame matters here).
        /// </summary>
        private void ApplyCameraTransform()
        {
            if (Target == null)
            {
                return;
            }

            bool followingRagdoll = Ragdoll != null && Ragdoll.IsLimp && Ragdoll.Torso != null;

            Vector3 pivot;
            if (followingRagdoll)
            {
                // .transform.position, NOT the Rigidbody's own .position --
                // the latter is the physics-synced value, which can lag a
                // frame behind whatever the Transform was just set to
                // (Ragdoll.RepositionToRagdollRest reads the same
                // .transform.position for the identical reason). A camera
                // reacting to where the body visually is should never wait
                // on a physics sync it has no other reason to depend on.
                pivot = Ragdoll.Torso.transform.position + Vector3.up * RagdollPivotHeight;
            }
            else
            {
                float height = _player != null ? _player.StandingHeight : 1.8f;
                pivot = Target.position + Vector3.up * (height * PivotHeightFraction);
            }

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0.0f);

            // OFFSET THE DESTINATION, NOT THE COLLISION ORIGIN. The spherecast
            // below still starts from the centred chest pivot -- see its own
            // comment on why that specifically has to be inside the capsule --
            // and simply aims at the shifted point instead, so a wall directly
            // behind the player's own centreline still pulls the shoulder-shot
            // camera in correctly rather than missing it because the cast
            // started somewhere already past the collider.
            _currentShoulderOffset = Mathf.MoveTowards(
                _currentShoulderOffset, IsAiming ? AimShoulderOffset : ShoulderOffset,
                AimShoulderOffsetMetresPerSecond * Time.deltaTime);
            Vector3 shoulderPivot = pivot + rotation * Vector3.right * _currentShoulderOffset;
            Vector3 wanted = shoulderPivot - rotation * Vector3.forward * Distance;

            // Pull in if a wall is in the way. The sphere radius keeps the near
            // plane out of the surface rather than flush against it, which is
            // what causes see-through corners.
            //
            // SphereCastAll rather than SphereCast because the cast STARTS
            // INSIDE THE PLAYER'S OWN CAPSULE -- the pivot is at chest height on
            // the body. A plain cast reports that capsule as the nearest hit and
            // pins the camera at minimum distance forever, which looks like the
            // camera being broken rather than like a collision bug.
            // The shoulder offset makes pivot-to-wanted slightly longer than
            // Distance itself (the hypotenuse of Distance and ShoulderOffset)
            // -- casting only as far as the flat Distance would leave the
            // last few centimetres in front of the actual camera unchecked.
            float wantedDistance = Vector3.Distance(pivot, wanted);
            Vector3 direction = (wanted - pivot).normalized;
            RaycastHit[] hits = Physics.SphereCastAll(
                pivot, 0.25f, direction, wantedDistance, CollisionLayers, QueryTriggerInteraction.Ignore);

            float nearest = wantedDistance;
            foreach (RaycastHit hit in hits)
            {
                if (hit.transform.IsChildOf(Target) || hit.transform == Target)
                {
                    continue;
                }
                // A zero distance means the sphere began already overlapping,
                // which carries no useful direction information.
                if (hit.distance > 0.01f && hit.distance < nearest)
                {
                    nearest = hit.distance;
                }
            }

            if (nearest < wantedDistance)
            {
                wanted = pivot + direction * Mathf.Max(MinDistance, nearest - 0.1f);
            }

            transform.position = wanted;
            transform.rotation = rotation;

            if (_camera != null)
            {
                float target = IsAiming ? AimFieldOfView : _restFieldOfView;
                _camera.fieldOfView = Mathf.MoveTowards(
                    _camera.fieldOfView, target, AimZoomDegreesPerSecond * Time.deltaTime);
            }
        }
    }
}
