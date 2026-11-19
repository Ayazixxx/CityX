using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// SHOWS A PARACHUTE JUMP ON A BODY (docs/PARACHUTE.md), the wearer's and every other player's copy of it alike: the poses (spread face down
    /// in the fall, the pull, swung upright under the opening canopy, the hands on the toggles turning, braking, flaring), the canopy over the
    /// shoulders with its lines, and the sounds (the rush of a fall, the crack as it fills, the flutter under it).
    /// <list type="bullet">
    /// <item>On the wearer the worn Parachute hands it the jump every frame (<see cref="ShowOwn"/>); it tells other players whenever the jump's
    /// state or the keys flying it change (<see cref="IReplicatedAbility"/>: a few words a second at most).</item>
    /// <item>On a copy those words are all it has (<see cref="PlayRemote"/>): it eases the hands to the keys as the wearer's own do and reads the
    /// turn and the speed off the body as it moves.</item>
    /// </list>
    /// The canopy is the one in the worn (or shown) parachute; the poses are Blender's (SourceArt/Parachute, ParachuteMotion.json).
    /// </summary>
    [DefaultExecutionOrder(10)]
    public sealed class ParachuteRider : MonoBehaviour, IReplicatedAbility
    {
        public const string Id = "parachute";

        /// <summary>The one thing it says: the jump's state and the keys flying it (N = state + 4 x keys), the heading (A) and, put away, why (B).</summary>
        public const int StateChanged = 0;

        /// <summary>Put away by landing, or lost in the air (cut away, collapsed against a wall).</summary>
        public const float Landed = 0.0f, Lost = 1.0f;

        public const int KeyW = 1, KeyS = 2, KeyA = 4, KeyD = 8;

        public const string MotionResource = "Parachute/ParachuteMotion";

        public const string Box = "skydive_box", Track = "skydive_track", Pull = "chute_pull", Hang = "chute_hang",
            LeftToggle = "chute_left", RightToggle = "chute_right", Flare = "chute_flare", Dive = "chute_dive";

        /// <summary>How far the body is laid forward in a fall: spread, diving (W), flattened out (S), degrees from standing.</summary>
        public const float FreefallPitch = 82.0f, DivePitch = 105.0f, SpreadPitch = 70.0f;

        /// <summary>A body turning in a fall drops its inside shoulder this far at a full turn, degrees.</summary>
        public const float FreefallRoll = 25.0f;

        /// <summary>Laid face down it turns about its middle; hanging, about the shoulders, where the lines take it.</summary>
        public static readonly Vector3 FreefallPivot = new Vector3(0.0f, 1.3f, 0.0f);

        public static readonly Vector3 HangPivot = new Vector3(0.0f, 1.95f, 0.0f);

        /// <summary>Under the canopy the body swings forward as it brakes and back as it speeds up: degrees per m/s², at most, and how quickly.</summary>
        public const float SwingPerAcceleration = 2.5f;

        public const float MostSwing = 25.0f;

        public const float SwingSeconds = 0.35f;

        /// <summary>The pose comes onto the body, and goes off it after, this quickly, seconds.</summary>
        public const float EaseInSeconds = 0.25f, EaseOutSeconds = 0.2f;

        /// <summary>From the throw, the lines take this long to pay out, and the canopy this long to fill (both inside ParachuteFlight.InflateSeconds).</summary>
        public const float LinesSeconds = 0.35f, FillSeconds = 0.7f;

        /// <summary>The fabric cracks this long after the throw: the lines taut, the canopy catching the air.</summary>
        public const float CrackAfter = 0.3f;

        /// <summary>Put away, the canopy is seen collapsing (landed) or tumbling off (lost) this long.</summary>
        public const float CollapseSeconds = 0.9f;

        /// <summary>A canopy flying at a copy's speed: what its bank is worked out from.</summary>
        private const float SpeedSmoothing = 0.15f;

        private PlayerLocomotion _locomotion;
        private Animator _animator;
        private Parachute _worn;
        private BakedMotion _motion;
        private BakedPose _pose, _other, _scratch;
        private bool _holdsBody;
        private float _weight;

        private ChuteState _state;
        private float _stateSeconds;
        private int _keys;
        private bool _owned;
        private float _left, _right, _front, _pitch, _turnRate, _bank, _swing, _swingVelocity, _airspeedChange, _openedPitch;

        private Vector3 _lastPosition;
        private float _lastYaw;
        private Vector3 _velocity;
        private float _lastSpeed;
        private bool _measured;

        private bool _collapsing;
        private bool _collapseLanded;
        private float _collapseSeconds;
        private Vector3 _collapseFrom;
        private Quaternion _collapseTurn;
        private Vector3 _collapseDrift;
        private Vector3 _collapseScale;

        private AudioSource _wind, _flutter, _crack;
        private bool _cracked;

        public string AbilityId => Id;

        public event System.Action<AbilityEvent> Fired;

        /// <summary>The jump as this body shows it.</summary>
        public ChuteState State => _state;

        /// <summary>The body's rider, added if it has none (a body made in a test, or a scene's that was not rebuilt).</summary>
        public static ParachuteRider On(GameObject body)
        {
            if (body == null)
            {
                return null;
            }
            var rider = body.GetComponent<ParachuteRider>();
            return rider != null ? rider : body.AddComponent<ParachuteRider>();
        }

        private void Awake()
        {
            _locomotion = GetComponent<PlayerLocomotion>();
            _animator = GetComponentInChildren<Animator>();
            _lastPosition = transform.position;
            _lastYaw = transform.eulerAngles.y;
        }

        private void OnDisable()
        {
            LetGoOfTheBody();
            SilenceAll();
        }

        // --- what it is told ---

        /// <summary>The wearer's own jump, every frame it is worn (Parachute.Tick).</summary>
        public void ShowOwn(ParachuteFlight flight, Vector2 keys, float deltaTime)
        {
            _owned = true;
            int pressed = flight.DrivesBody ? KeysOf(keys) : 0;
            bool changed = flight.State != _state;
            if (changed)
            {
                Enter(flight.State, flight.LostThisFrame ? Lost : Landed);
            }
            if (changed || pressed != _keys)
            {
                _keys = pressed;
                Fired?.Invoke(new AbilityEvent(StateChanged, transform.position, transform.forward, flight.HeadingDegrees,
                    flight.LostThisFrame ? Lost : Landed, (int)flight.State + 4 * pressed));
            }
            _stateSeconds = flight.StateSeconds;
            _left = flight.LeftToggle;
            _right = flight.RightToggle;
            _front = flight.FrontRisers;
            _pitch = flight.FreefallPitch;
            _turnRate = flight.TurnRateNow;
            _bank = flight.BankDegrees;
            _airspeedChange = flight.AirspeedChange(deltaTime);
        }

        public void PlayRemote(AbilityEvent evt)
        {
            if (evt.Kind != StateChanged || evt.N < 0 || evt.N >= 64)
            {
                return;
            }
            var state = (ChuteState)(evt.N % 4);
            _keys = evt.N / 4;
            if (state != _state)
            {
                Enter(state, evt.B >= Lost - 0.5f ? Lost : Landed);
            }
        }

        /// <summary>The parachute came off the body: everything it showed goes at once.</summary>
        public void PutAway()
        {
            _state = ChuteState.Packed;
            _collapsing = false;
            _worn = null;
            LetGoOfTheBody();
            SilenceAll();
            enabled = true;
        }

        private void Enter(ChuteState state, float why)
        {
            ChuteState was = _state;
            if (state == ChuteState.Opening)
            {
                _openedPitch = LaidPitch(_pitch);
                _cracked = false;
            }
            if (state == ChuteState.Packed && (was == ChuteState.Opening || was == ChuteState.Open))
            {
                BeginCollapse(why < Lost - 0.5f);
            }
            if (was == ChuteState.Packed && state != ChuteState.Packed)
            {
                _collapsing = false;
                _worn = null;   // found again: the one shown now may be a new one
            }
            _state = state;
            _stateSeconds = 0.0f;
            enabled = true;
        }

        private static int KeysOf(Vector2 move) =>
            (move.y > 0.5f ? KeyW : 0) | (move.y < -0.5f ? KeyS : 0) | (move.x < -0.5f ? KeyA : 0) | (move.x > 0.5f ? KeyD : 0);

        private static Vector2 MoveOf(int keys) =>
            new Vector2(((keys & KeyD) != 0 ? 1.0f : 0.0f) - ((keys & KeyA) != 0 ? 1.0f : 0.0f), ((keys & KeyW) != 0 ? 1.0f : 0.0f) - ((keys & KeyS) != 0 ? 1.0f : 0.0f));

        // --- every frame ---

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            Measure(deltaTime);
            if (!_owned)
            {
                FollowTheKeys(deltaTime);
            }
            Pose(deltaTime);
            Sound();
            if (_state == ChuteState.Packed && !_collapsing && !_holdsBody && _weight <= 0.0f)
            {
                SilenceAll();
                _measured = false;
                enabled = false;
            }
        }

        /// <summary>How the body really moved: a copy's turn and speed are read off it, and the wind is the speed of the fall.</summary>
        private void Measure(float deltaTime)
        {
            if (deltaTime <= 0.0f)
            {
                return;
            }
            Vector3 position = transform.position;
            float yaw = transform.eulerAngles.y;
            if (_measured)
            {
                _velocity = Vector3.Lerp(_velocity, (position - _lastPosition) / deltaTime, 1.0f - Mathf.Exp(-deltaTime / SpeedSmoothing));
                if (!_owned)
                {
                    float rate = Mathf.DeltaAngle(_lastYaw, yaw) / deltaTime;
                    _turnRate = Mathf.Lerp(_turnRate, rate, 1.0f - Mathf.Exp(-deltaTime / SpeedSmoothing));
                }
            }
            _measured = true;
            _lastPosition = position;
            _lastYaw = yaw;
        }

        /// <summary>On a copy: the hands go where the wearer's keys put them, as fast as the wearer's own; the bank and the swing from how it moves.</summary>
        private void FollowTheKeys(float deltaTime)
        {
            if (_state != ChuteState.Packed)
            {
                _stateSeconds += deltaTime;
            }
            Vector2 move = MoveOf(_keys);
            float step = ParachuteFlight.TogglePullRate * deltaTime;
            if (_state == ChuteState.Open)
            {
                ParachuteFlight.WantedToggles(move, out float left, out float right, out float front);
                _left = Mathf.MoveTowards(_left, left, step);
                _right = Mathf.MoveTowards(_right, right, step);
                _front = Mathf.MoveTowards(_front, front, step);
            }
            else
            {
                _left = _right = _front = 0.0f;
            }
            _pitch = _state == ChuteState.Freefall ? Mathf.MoveTowards(_pitch, move.y, step * 0.5f) : _state == ChuteState.Opening ? _pitch : 0.0f;
            float speed = new Vector2(_velocity.x, _velocity.z).magnitude;
            _bank = _state == ChuteState.Open ? ParachuteFlight.BankFor(speed, _turnRate) : 0.0f;
            _airspeedChange = deltaTime > 0.0f ? (speed - _lastSpeed) / deltaTime : 0.0f;
            _lastSpeed = speed;
        }

        // --- the pose ---

        private bool Ready()
        {
            if (_motion == null)
            {
                _motion = BakedMotion.Load(MotionResource);
                if (_motion == null)
                {
                    return false;
                }
                _pose = _motion.NewPose();
                _other = _motion.NewPose();
                _scratch = _motion.NewPose();
            }
            return true;
        }

        /// <summary>How far a body spread in a fall is laid forward, degrees, for W (positive) or S (negative) as far as they are held.</summary>
        public static float LaidPitch(float pitch) =>
            pitch >= 0.0f ? Mathf.Lerp(FreefallPitch, DivePitch, pitch) : Mathf.Lerp(FreefallPitch, SpreadPitch, -pitch);

        private void Pose(float deltaTime)
        {
            bool shown = _state != ChuteState.Packed;
            _weight = Mathf.MoveTowards(_weight, shown ? 1.0f : 0.0f, deltaTime / (shown ? EaseInSeconds : EaseOutSeconds));
            if (_locomotion == null || !Ready())
            {
                return;
            }
            if (_weight <= 0.0f)
            {
                LetGoOfTheBody();
                return;
            }
            float swingWanted = _state == ChuteState.Open ? Mathf.Clamp(_airspeedChange * SwingPerAcceleration, -MostSwing, MostSwing) : 0.0f;
            _swing = Mathf.SmoothDamp(_swing, swingWanted, ref _swingVelocity, SwingSeconds, Mathf.Infinity, deltaTime);
            switch (_state)
            {
                case ChuteState.Freefall:
                    PoseFalling();
                    break;
                case ChuteState.Opening:
                    PoseOpening();
                    break;
                case ChuteState.Open:
                    PoseOpen();
                    break;
            }
            _pose.UpperBodyOnly = false;
            _pose.LegsWeight = 1.0f;
            _holdsBody = true;
            _locomotion.SetBakedPose(_pose, Mathf.SmoothStep(0.0f, 1.0f, _weight));
        }

        private void PoseFalling()
        {
            _motion.Sample(Box, 0.0f, false, _pose);
            _motion.Sample(Track, 0.0f, false, _other);
            Blend(_pose, _other, Mathf.Max(0.0f, _pitch));
            float roll = -Mathf.Clamp(_turnRate / ParachuteFlight.FreefallTurnRate, -1.0f, 1.0f) * FreefallRoll;
            _pose.Turn = Quaternion.AngleAxis(roll, Vector3.forward) * Quaternion.AngleAxis(LaidPitch(_pitch), Vector3.right);
            _pose.Pivot = FreefallPivot;
        }

        private void PoseOpening()
        {
            float pull = ParachuteFlight.PullSeconds;
            if (_stateSeconds < pull)
            {
                _motion.Sample(Pull, _stateSeconds, false, _pose);
                _pose.Turn = Quaternion.AngleAxis(_openedPitch, Vector3.right);
                _pose.Pivot = FreefallPivot;
                return;
            }
            // Taken by the canopy: swung up from face down, past hanging a little and back (EaseOutBack), the hands going up to the toggles.
            float u = Mathf.Clamp01((_stateSeconds - pull) / ParachuteFlight.InflateSeconds);
            _motion.Sample(Pull, pull, false, _pose);
            _motion.Sample(Hang, 0.0f, false, _other);
            Blend(_pose, _other, Mathf.SmoothStep(0.0f, 1.0f, Mathf.Clamp01(u * 1.6f)));
            _pose.Turn = Quaternion.AngleAxis(_openedPitch * (1.0f - EaseOutBack(u)), Vector3.right);
            _pose.Pivot = Vector3.Lerp(FreefallPivot, HangPivot, Mathf.SmoothStep(0.0f, 1.0f, u));
        }

        private void PoseOpen()
        {
            float both = Mathf.Min(_left, _right);
            _motion.Sample(Hang, 0.0f, false, _pose);
            BlendSegment(Flare, both);
            BlendSegment(LeftToggle, _left - both);
            BlendSegment(RightToggle, _right - both);
            BlendSegment(Dive, _front * (1.0f - both));
            _pose.Turn = Quaternion.AngleAxis(-_bank, Vector3.forward) * Quaternion.AngleAxis(_swing, Vector3.right);
            _pose.Pivot = HangPivot;
        }

        private void BlendSegment(string segment, float share)
        {
            if (share <= 0.001f)
            {
                return;
            }
            _motion.Sample(segment, 0.0f, false, _other);
            Blend(_pose, _other, share);
        }

        /// <summary><paramref name="into"/> moved toward <paramref name="toward"/> by <paramref name="share"/> (0 leaves it, 1 makes it the other).</summary>
        private void Blend(BakedPose into, BakedPose toward, float share)
        {
            _scratch.CopyBodyFrom(toward);
            _scratch.MixBodyFrom(into, Mathf.Clamp01(share));
            into.CopyBodyFrom(_scratch);
        }

        /// <summary>Past 1 and back: a pendulum's overshoot.</summary>
        public static float EaseOutBack(float u)
        {
            const float c1 = 1.70158f, c3 = c1 + 1.0f;
            float v = u - 1.0f;
            return 1.0f + c3 * v * v * v + c1 * v * v;
        }

        private void LetGoOfTheBody()
        {
            if (_holdsBody && _locomotion != null && _locomotion.HoldsBakedPose(_pose))
            {
                _locomotion.SetBakedPose(null, 0.0f);
            }
            _holdsBody = false;
            _weight = 0.0f;
        }

        // --- the canopy ---

        private ParachuteCanopy Canopy
        {
            get
            {
                if (_worn == null)
                {
                    _worn = GetComponentInChildren<Parachute>(true);
                }
                return _worn != null ? _worn.Canopy : null;
            }
        }

        private void LateUpdate()
        {
            ParachuteCanopy canopy = Canopy;
            if (canopy == null)
            {
                return;
            }
            if (_collapsing)
            {
                Collapse(canopy, Time.deltaTime);
                return;
            }
            bool canopyOut = _state == ChuteState.Open || (_state == ChuteState.Opening && _stateSeconds >= ParachuteFlight.PullSeconds);
            if (!canopyOut || _animator == null)
            {
                if (canopy.Showing)
                {
                    canopy.Hide();
                }
                return;
            }
            Transform leftShoulder = _animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            Transform rightShoulder = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            Transform leftHand = _animator.GetBoneTransform(HumanBodyBones.LeftHand);
            Transform rightHand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (leftShoulder == null || rightShoulder == null || leftHand == null || rightHand == null)
            {
                return;
            }
            Vector3 opened = Vector3.one;
            Quaternion turn = Quaternion.Euler(0.0f, transform.eulerAngles.y, 0.0f);
            if (_state == ChuteState.Opening)
            {
                float since = _stateSeconds - ParachuteFlight.PullSeconds;
                float lines = Mathf.SmoothStep(0.0f, 1.0f, Mathf.Clamp01(since / LinesSeconds));
                float fill = EaseOutBack(Mathf.Clamp01((since - LinesSeconds * 0.5f) / FillSeconds));
                opened = new Vector3(Mathf.LerpUnclamped(0.1f, 1.0f, fill), Mathf.Lerp(0.12f, 1.0f, lines), Mathf.LerpUnclamped(0.3f, 1.0f, fill));
            }
            else
            {
                turn *= Quaternion.AngleAxis(-_bank, Vector3.forward) * Quaternion.AngleAxis(_swing, Vector3.right);
            }
            Vector3 hang = 0.5f * (leftShoulder.position + rightShoulder.position);
            canopy.Place(hang, turn, opened, transform.lossyScale.y, leftShoulder.position, rightShoulder.position, leftHand.position, rightHand.position);
        }

        private void BeginCollapse(bool landed)
        {
            ParachuteCanopy canopy = Canopy;
            if (canopy == null || !canopy.Showing)
            {
                return;
            }
            _collapsing = true;
            _collapseLanded = landed;
            _collapseSeconds = 0.0f;
            _collapseFrom = canopy.transform.position;
            _collapseTurn = canopy.transform.rotation;
            _collapseScale = canopy.transform.lossyScale;
            Vector3 flat = new Vector3(_velocity.x, 0.0f, _velocity.z);
            // Landed, it carries on a little and comes down; let go in the air, it is left behind, rising away from the falling body.
            _collapseDrift = landed ? flat * 0.35f : Vector3.up * 6.0f - flat * 0.3f;
        }

        private void Collapse(ParachuteCanopy canopy, float deltaTime)
        {
            _collapseSeconds += deltaTime;
            float u = Mathf.Clamp01(_collapseSeconds / CollapseSeconds);
            if (u >= 1.0f)
            {
                _collapsing = false;
                canopy.Hide();
                return;
            }
            float t = _collapseSeconds;
            float drop = _collapseLanded ? CanopyShape.LineLength * _collapseScale.y * Mathf.SmoothStep(0.0f, 1.0f, u) : 0.0f;
            Vector3 position = _collapseFrom + _collapseDrift * t + Vector3.down * drop;
            Quaternion turn = _collapseTurn * Quaternion.AngleAxis((_collapseLanded ? 55.0f : 140.0f) * u, Vector3.right);
            Vector3 shape = _collapseLanded ? new Vector3(Mathf.Lerp(1.0f, 0.7f, u), Mathf.Lerp(1.0f, 0.15f, u), Mathf.Lerp(1.0f, 0.6f, u))
                                            : Vector3.one * Mathf.Lerp(1.0f, 0.35f, u);
            canopy.PlaceLoose(position, turn, Vector3.Scale(_collapseScale, shape));
        }

        // --- the sounds ---

        private void Sound()
        {
            if (_state == ChuteState.Packed)
            {
                Fade(_wind, 0.0f);
                Fade(_flutter, 0.0f);
                return;
            }
            EnsureSources();
            float speed = _velocity.magnitude;
            bool falling = _state == ChuteState.Freefall || (_state == ChuteState.Opening && _stateSeconds < ParachuteFlight.PullSeconds + CrackAfter);
            float rush = Mathf.Clamp01((speed - 12.0f) / 40.0f);
            Fade(_wind, falling ? 0.25f + 0.65f * rush : 0.12f);
            if (_wind != null)
            {
                _wind.pitch = Mathf.Lerp(0.8f, 1.3f, rush);
            }
            Fade(_flutter, falling ? 0.0f : 0.3f + 0.3f * Mathf.Clamp01(Mathf.Abs(_turnRate) / ParachuteFlight.TurnRate));
            if (_state == ChuteState.Opening && !_cracked && _stateSeconds >= ParachuteFlight.PullSeconds + CrackAfter)
            {
                _cracked = true;
                _crack?.PlayOneShot(ParachuteSounds.OpenCrack, 1.0f);
            }
        }

        private void EnsureSources()
        {
            if (_crack != null)
            {
                return;
            }
            var holder = new GameObject("ParachuteSounds");
            holder.transform.SetParent(transform, false);
            holder.transform.localPosition = Vector3.up * 1.6f;
            Parachute worn = _worn != null ? _worn : GetComponentInChildren<Parachute>(true);
            AudioClip wind = worn != null ? worn.WindClip : null;
            _wind = Source(holder, wind, true);
            _flutter = Source(holder, ParachuteSounds.Flutter, true);
            _crack = Source(holder, null, false);
        }

        private static AudioSource Source(GameObject holder, AudioClip clip, bool loop)
        {
            var source = holder.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = loop;
            source.playOnAwake = false;
            source.spatialBlend = 1.0f;
            source.minDistance = 4.0f;
            source.maxDistance = 120.0f;
            source.volume = 0.0f;
            return source;
        }

        private static void Fade(AudioSource source, float volume)
        {
            if (source == null)
            {
                return;
            }
            source.volume = Mathf.MoveTowards(source.volume, volume, 2.0f * Time.deltaTime);
            if (source.volume > 0.0f && !source.isPlaying && source.clip != null)
            {
                source.Play();
            }
            else if (source.volume <= 0.0f && source.isPlaying)
            {
                source.Stop();
            }
        }

        private void SilenceAll()
        {
            foreach (AudioSource source in new[] { _wind, _flutter })
            {
                if (source != null)
                {
                    source.volume = 0.0f;
                    source.Stop();
                }
            }
        }
    }
}
