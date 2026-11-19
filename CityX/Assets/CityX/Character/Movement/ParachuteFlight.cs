using UnityEngine;

namespace CityX.Character
{
    /// <summary>Where a parachute is in its jump (docs/PARACHUTE.md).</summary>
    public enum ChuteState
    {
        /// <summary>On the back. The body falls as any body does, and the fall is timed.</summary>
        Packed,

        /// <summary>Fallen long enough: spread out face down in the air, steered with the keys, and Space opens it.</summary>
        Freefall,

        /// <summary>The pilot chute thrown, the lines paying out, the canopy filling: the fall is braked hard.</summary>
        Opening,

        /// <summary>Flying under the canopy.</summary>
        Open,
    }

    /// <summary>How a canopy came down.</summary>
    public enum ChuteLanding
    {
        /// <summary>Flared: barely sinking, a step or two and standing.</summary>
        Soft,

        /// <summary>Sinking as it glides: a hard step, no harm done.</summary>
        Firm,

        /// <summary>Diving, turning low or still opening: knocked down and hurt.</summary>
        Hard,
    }

    /// <summary>One frame's keys and what the body did, for <see cref="ParachuteFlight.Tick"/>.</summary>
    public struct ChuteInput
    {
        /// <summary>The feet are off the ground.</summary>
        public bool Airborne;

        /// <summary>Space went down this frame.</summary>
        public bool SpacePressed;

        /// <summary>WASD: x the turn (D positive), y the pitch (W positive).</summary>
        public Vector2 Move;

        /// <summary>The body's velocity as it really moved last frame (what the fall timer and the start of a freefall read).</summary>
        public Vector3 BodyVelocity;

        /// <summary>The way the body faces now, degrees.</summary>
        public float HeadingDegrees;

        /// <summary>The frame before was ticked too. False after a frame the body was on a wall, on a rope or knocked down: the jump starts over.</summary>
        public bool Continuous;

        /// <summary>The body ran into a wall last frame.</summary>
        public bool HitWall;
    }

    /// <summary>
    /// HOW A PARACHUTE FLIES THE BODY (owner, 2026-11-19: "make a parachute with good parachuting mechanics, press space to open it when your
    /// falling in mid air. make so it doesnt just activate off jumping and pressing space quickly ... you have to be falling in mid air for like 2
    /// seconds before you can activate parachute ... research other games parachuting physics and implement what you learn"; docs/PARACHUTE.md).
    /// <list type="bullet">
    /// <item>PACKED, a fall is timed. After <see cref="ReadySeconds"/> of falling the body spreads out face down (FREEFALL): the drag of the air
    /// holds it near a skydiver's terminal speed, A and D turn it, W dives (faster, and forward), S spreads it flat (slower).</item>
    /// <item>Space in freefall OPENS it: a moment for the pilot chute, then the canopy fills and brakes the fall hard over a second.</item>
    /// <item>OPEN, the canopy glides about 2.8 metres forward for every one it sinks. A and D pull a toggle and turn it (a turn sinks it
    /// faster); W pulls the front risers and dives; S pulls both toggles: slower forward and down. Pulled from a fast glide, S is a FLARE: the
    /// speed the canopy is losing becomes lift for a moment, which is how it is landed softly. Space after the first moments cuts it away.</item>
    /// <item>On the ground it lands soft, firm or hard (<see cref="ChuteLanding"/>) by how fast it was sinking; it collapses running into a
    /// wall at speed.</item>
    /// </list>
    /// Pure: the item feeds it a frame and reads the velocity to give the body back, with what the pose and the canopy need.
    /// </summary>
    public sealed class ParachuteFlight
    {
        // --- the wait (the owner's number) ---

        /// <summary>Seconds of falling before the parachute can be opened: a jump off the ground never gets near it.</summary>
        public const float ReadySeconds = 2.0f;

        /// <summary>Going down faster than this counts as falling, metres a second.</summary>
        public const float FallingSpeed = 0.5f;

        // --- freefall: a skydiver's numbers, a little quicker to settle (real: about 55 m/s belly down, 80+ head down) ---

        public const float Gravity = 9.8f;

        /// <summary>The speed the air holds a body spread face down at, metres a second.</summary>
        public const float TerminalSpeed = 50.0f;

        /// <summary>Diving (W) head low: faster.</summary>
        public const float DiveTerminalSpeed = 68.0f;

        /// <summary>Spread flat (S): slower.</summary>
        public const float SpreadTerminalSpeed = 40.0f;

        /// <summary>How fast a spread body drifts forward, and a diving one tracks, metres a second.</summary>
        public const float DriftSpeed = 2.0f;

        public const float TrackSpeed = 16.0f;

        /// <summary>Seconds for the forward speed to settle on a new one (the air takes a running start off a body in a few).</summary>
        public const float FreefallSpeedSeconds = 1.5f;

        /// <summary>A and D turn a body in freefall this fast, degrees a second.</summary>
        public const float FreefallTurnRate = 90.0f;

        // --- the opening ---

        /// <summary>The hand goes to the pilot chute and throws it: nothing slows yet.</summary>
        public const float PullSeconds = 0.3f;

        /// <summary>From the throw until the canopy is flying: the lines pay out, it fills, the fall is braked.</summary>
        public const float InflateSeconds = 0.9f;

        public const float OpeningSeconds = PullSeconds + InflateSeconds;

        // --- the canopy (real ram-air canopies glide about 3 to 1 at 9-12 m/s) ---

        /// <summary>Hands up, flying straight: forward and sinking, metres a second.</summary>
        public const float GlideSpeed = 11.0f;

        public const float GlideSink = 4.0f;

        /// <summary>The front risers pulled (W): diving.</summary>
        public const float DiveSpeed = 16.0f;

        public const float DiveSink = 7.5f;

        /// <summary>Both toggles down (S), held: slow forward, slow down; held long, more than a flare, which is how it lands softly.</summary>
        public const float BrakeSpeed = 4.5f;

        public const float BrakeSink = 3.4f;

        /// <summary>Seconds for the forward speed to follow the hands: a canopy surges and slows over most of a second.</summary>
        public const float AirspeedSeconds = 0.8f;

        /// <summary>Seconds for the sink to follow.</summary>
        public const float SinkSeconds = 0.35f;

        /// <summary>A toggle pulled all the way turns it this fast, degrees a second; diving, faster.</summary>
        public const float TurnRate = 60.0f;

        public const float DivingTurnRate = 90.0f;

        /// <summary>A turn sinks it faster: this much more at a full toggle, metres a second (the low turn is how canopy pilots get hurt).</summary>
        public const float TurnSink = 3.0f;

        /// <summary>The FLARE: each metre a second the canopy is losing becomes this much less sink.</summary>
        public const float FlareLift = 0.8f;

        /// <summary>A flare can carry it up a little, no more, metres a second.</summary>
        public const float MostClimb = 1.0f;

        /// <summary>How fast a hand pulls a toggle or lets it up, a share a second.</summary>
        public const float TogglePullRate = 5.0f;

        /// <summary>The canopy banks with its turn (and the body swings out under it) no further than this, degrees.</summary>
        public const float MostBank = 30.0f;

        // --- the landing ---

        /// <summary>Sinking slower than this it is a soft landing, faster than <see cref="HardLandingSink"/> a hard one, metres a second.</summary>
        public const float SoftLandingSink = 3.0f;

        public const float HardLandingSink = 6.5f;

        /// <summary>A hard landing hurts this much, and this much more for each metre a second past it.</summary>
        public const float HardLandingDamage = 15.0f;

        public const float HardLandingDamagePerSink = 6.0f;

        /// <summary>Into a wall faster than this, metres a second, the canopy collapses.</summary>
        public const float CollapseSpeed = 6.0f;

        /// <summary>Space does not cut it away until it has been flying this long, seconds (a second press of the opening is not a cut-away).</summary>
        public const float CutAwayAfter = 0.6f;

        public ChuteState State { get; private set; }

        /// <summary>Seconds the body has been falling, packed.</summary>
        public float FallSeconds { get; private set; }

        /// <summary>Seconds in the state it is in.</summary>
        public float StateSeconds { get; private set; }

        /// <summary>True while it moves the body (freefall, opening, open): the keys and Space are its.</summary>
        public bool DrivesBody => State != ChuteState.Packed;

        /// <summary>The velocity to give the body this frame, while it drives it.</summary>
        public Vector3 Velocity { get; private set; }

        /// <summary>The way the body should face, degrees.</summary>
        public float HeadingDegrees { get; private set; }

        /// <summary>How fast it is turning, degrees a second (right positive).</summary>
        public float TurnRateNow { get; private set; }

        /// <summary>Open: forward through the air and down, metres a second.</summary>
        public float Airspeed { get; private set; }

        public float Sink { get; private set; }

        /// <summary>The left and right toggles, 0 up to 1 pulled down all the way; the front risers (W), 0 to 1.</summary>
        public float LeftToggle { get; private set; }

        public float RightToggle { get; private set; }

        public float FrontRisers { get; private set; }

        /// <summary>How far W (positive) or S (negative) has the body in freefall, eased, -1 to 1.</summary>
        public float FreefallPitch { get; private set; }

        /// <summary>The canopy's bank into its turn, degrees (right positive).</summary>
        public float BankDegrees { get; private set; }

        /// <summary>This frame's tick began the opening.</summary>
        public bool OpenedThisFrame { get; private set; }

        /// <summary>This frame's tick put it away: landed, cut away, collapsed.</summary>
        public bool ClosedThisFrame { get; private set; }

        /// <summary>This frame's tick landed it (<see cref="Landing"/> says how).</summary>
        public bool LandedThisFrame { get; private set; }

        public ChuteLanding Landing { get; private set; }

        /// <summary>How fast it was sinking when it landed, metres a second.</summary>
        public float LandingSink { get; private set; }

        /// <summary>This frame's tick cut it away or collapsed it in the air.</summary>
        public bool LostThisFrame { get; private set; }

        private Vector3 _openedFrom;
        private float _airspeedWas;

        /// <summary>One frame.</summary>
        public void Tick(in ChuteInput input, float deltaTime)
        {
            OpenedThisFrame = ClosedThisFrame = LandedThisFrame = LostThisFrame = false;
            if (deltaTime <= 0.0f)
            {
                return;
            }
            if (!input.Continuous && State != ChuteState.Packed)
            {
                PutAway();   // knocked down, caught on a wall: whatever was out is gone, as a ragdoll would tear it
            }
            if (!input.Continuous)
            {
                FallSeconds = 0.0f;
            }
            if (!input.Airborne)
            {
                if (State == ChuteState.Opening || State == ChuteState.Open)
                {
                    Land(State == ChuteState.Open ? Sink : -input.BodyVelocity.y, State == ChuteState.Opening);
                }
                else if (State == ChuteState.Freefall)
                {
                    PutAway();   // a fall like any other: fall damage measures it
                }
                FallSeconds = 0.0f;
                return;
            }
            StateSeconds += deltaTime;
            switch (State)
            {
                case ChuteState.Packed:
                    TickPacked(input, deltaTime);
                    break;
                case ChuteState.Freefall:
                    if (input.SpacePressed)
                    {
                        Begin(ChuteState.Opening);
                        _openedFrom = Velocity;
                        OpenedThisFrame = true;
                        TickOpening(input, deltaTime);
                        break;
                    }
                    TickFreefall(input, deltaTime);
                    break;
                case ChuteState.Opening:
                    if (input.HitWall && HorizontalSpeed(input.BodyVelocity) > CollapseSpeed)
                    {
                        Lose();
                        break;
                    }
                    TickOpening(input, deltaTime);
                    break;
                case ChuteState.Open:
                    if ((input.SpacePressed && StateSeconds >= CutAwayAfter) || (input.HitWall && Airspeed > CollapseSpeed))
                    {
                        Lose();
                        break;
                    }
                    TickOpen(input, deltaTime);
                    break;
            }
        }

        private void TickPacked(in ChuteInput input, float deltaTime)
        {
            if (input.BodyVelocity.y < -FallingSpeed)
            {
                FallSeconds += deltaTime;
            }
            else if (input.BodyVelocity.y > FallingSpeed)
            {
                FallSeconds = 0.0f;   // still going up: a jump, a launch
            }
            if (FallSeconds >= ReadySeconds)
            {
                Begin(ChuteState.Freefall);
                Velocity = input.BodyVelocity;
                HeadingDegrees = input.HeadingDegrees;
                TurnRateNow = 0.0f;
                FreefallPitch = 0.0f;
            }
        }

        private void TickFreefall(in ChuteInput input, float deltaTime)
        {
            FreefallPitch = Mathf.MoveTowards(FreefallPitch, Mathf.Clamp(input.Move.y, -1.0f, 1.0f), TogglePullRate * 0.5f * deltaTime);
            Turn(Mathf.Clamp(input.Move.x, -1.0f, 1.0f) * FreefallTurnRate, deltaTime);

            float dive = Mathf.Max(0.0f, FreefallPitch);
            float spread = Mathf.Max(0.0f, -FreefallPitch);
            float terminal = Mathf.Lerp(Mathf.Lerp(TerminalSpeed, DiveTerminalSpeed, dive), SpreadTerminalSpeed, spread);
            // The air's drag grows with the square of the speed and balances the body's weight at its terminal speed: under it the fall
            // quickens, over it (a dive let go) it slows.
            float down = -Velocity.y;
            float share = down / terminal;
            down += Gravity * (1.0f - share * Mathf.Abs(share)) * deltaTime;

            Vector3 flat = new Vector3(Velocity.x, 0.0f, Velocity.z);
            Vector3 wanted = Forward(HeadingDegrees) * Mathf.Lerp(DriftSpeed, TrackSpeed, dive) * (1.0f - spread);
            flat = Vector3.Lerp(flat, wanted, 1.0f - Mathf.Exp(-deltaTime / FreefallSpeedSeconds));
            Velocity = flat + Vector3.down * down;
        }

        private void TickOpening(in ChuteInput input, float deltaTime)
        {
            Turn(0.0f, deltaTime);
            // The canopy's own flight starts as it fills, and the fall the body brought is braked out over the inflation: hardest in its middle.
            float filled = Mathf.SmoothStep(0.0f, 1.0f, Mathf.Clamp01((StateSeconds - PullSeconds) / InflateSeconds));
            Airspeed = GlideSpeed * filled;
            Sink = GlideSink;
            _airspeedWas = Airspeed;
            Vector3 canopy = Forward(HeadingDegrees) * GlideSpeed + Vector3.down * GlideSink;
            Velocity = Vector3.Lerp(_openedFrom, canopy, filled);
            EaseToggles(0.0f, 0.0f, 0.0f, deltaTime);
            if (StateSeconds >= OpeningSeconds)
            {
                Begin(ChuteState.Open);
                Airspeed = GlideSpeed;
                Sink = GlideSink;
                _airspeedWas = Airspeed;
            }
        }

        private void TickOpen(in ChuteInput input, float deltaTime)
        {
            WantedToggles(input.Move, out float left, out float right, out float front);
            EaseToggles(left, right, front, deltaTime);

            // Both toggles down is the brakes; one further down than the other turns it.
            float brake = Mathf.Min(LeftToggle, RightToggle);
            float steer = RightToggle - LeftToggle;
            float dive = FrontRisers * (1.0f - brake);
            float wantedSpeed = Mathf.Lerp(Mathf.Lerp(GlideSpeed, BrakeSpeed, brake), DiveSpeed, dive);
            float wantedSink = Mathf.Lerp(Mathf.Lerp(GlideSink, BrakeSink, brake), DiveSink, dive);

            _airspeedWas = Airspeed;
            Airspeed = Mathf.Lerp(Airspeed, wantedSpeed, 1.0f - Mathf.Exp(-deltaTime / AirspeedSeconds));
            // THE FLARE: the speed being lost is lift, for as long as there is speed to lose.
            float losing = Mathf.Max(0.0f, Airspeed - wantedSpeed);
            wantedSink += TurnSink * Mathf.Abs(steer) - FlareLift * losing;
            wantedSink = Mathf.Max(wantedSink, -MostClimb);
            Sink = Mathf.Lerp(Sink, wantedSink, 1.0f - Mathf.Exp(-deltaTime / SinkSeconds));

            Turn(steer * Mathf.Lerp(TurnRate, DivingTurnRate, dive), deltaTime);
            BankDegrees = BankFor(Airspeed, TurnRateNow);
            Velocity = Forward(HeadingDegrees) * Airspeed + Vector3.down * Sink;
        }

        /// <summary>
        /// Where the keys want the hands under the canopy: S pulls both toggles down (the brakes), A the left, D the right, W the front risers;
        /// each 0 up to 1 down.
        /// </summary>
        public static void WantedToggles(Vector2 move, out float left, out float right, out float front)
        {
            float turn = Mathf.Clamp(move.x, -1.0f, 1.0f);
            float pitch = Mathf.Clamp(move.y, -1.0f, 1.0f);
            float brakes = Mathf.Max(0.0f, -pitch);
            left = Mathf.Max(brakes, Mathf.Max(0.0f, -turn));
            right = Mathf.Max(brakes, Mathf.Max(0.0f, turn));
            front = Mathf.Max(0.0f, pitch);
        }

        /// <summary>How far a canopy flying at <paramref name="airspeed"/> banks turning at <paramref name="turnRate"/> degrees a second: the lean that holds a turn.</summary>
        public static float BankFor(float airspeed, float turnRate) =>
            Mathf.Clamp(Mathf.Atan(airspeed * turnRate * Mathf.Deg2Rad / Gravity) * Mathf.Rad2Deg, -MostBank, MostBank);

        /// <summary>The canopy's forward speed change last frame, metres a second squared (the body swings under it with it).</summary>
        public float AirspeedChange(float deltaTime) => deltaTime > 0.0f ? (Airspeed - _airspeedWas) / deltaTime : 0.0f;

        private void Turn(float wantedRate, float deltaTime)
        {
            TurnRateNow = Mathf.Lerp(TurnRateNow, wantedRate, 1.0f - Mathf.Exp(-deltaTime / 0.25f));
            HeadingDegrees = Mathf.Repeat(HeadingDegrees + TurnRateNow * deltaTime, 360.0f);
        }

        private void EaseToggles(float left, float right, float front, float deltaTime)
        {
            float step = TogglePullRate * deltaTime;
            LeftToggle = Mathf.MoveTowards(LeftToggle, left, step);
            RightToggle = Mathf.MoveTowards(RightToggle, right, step);
            FrontRisers = Mathf.MoveTowards(FrontRisers, front, step);
        }

        private void Land(float sink, bool stillOpening)
        {
            LandingSink = sink;
            Landing = stillOpening || sink > HardLandingSink ? ChuteLanding.Hard : sink <= SoftLandingSink ? ChuteLanding.Soft : ChuteLanding.Firm;
            LandedThisFrame = true;
            PutAway();
        }

        /// <summary>How much a landing hurts (only a hard one does).</summary>
        public static float DamageOf(ChuteLanding landing, float sink) =>
            landing == ChuteLanding.Hard ? HardLandingDamage + Mathf.Max(0.0f, sink - HardLandingSink) * HardLandingDamagePerSink : 0.0f;

        private void Lose()
        {
            LostThisFrame = true;
            PutAway();
        }

        private void PutAway()
        {
            bool wasOut = State != ChuteState.Packed;
            Begin(ChuteState.Packed);
            FallSeconds = 0.0f;
            LeftToggle = RightToggle = FrontRisers = 0.0f;
            BankDegrees = 0.0f;
            TurnRateNow = 0.0f;
            FreefallPitch = 0.0f;
            ClosedThisFrame = wasOut;
        }

        private void Begin(ChuteState state)
        {
            State = state;
            StateSeconds = 0.0f;
        }

        private static float HorizontalSpeed(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        public static Vector3 Forward(float headingDegrees) => Quaternion.Euler(0.0f, headingDegrees, 0.0f) * Vector3.forward;
    }
}
