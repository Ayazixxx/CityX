using System;
using CityX.World;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// CLIMBING A LADDER (docs/SEWERS.md; owner, 2026-11-09: "make the manhole ladder entrances ... i guess you will have to make ladder
    /// climbing so make that look good with hand ik"). Anyone can: it is not the Freerunner's Climb, and it costs no stamina.
    ///
    ///  * ON at the foot by walking into the ladder, or with E looking at it; at the top (an open manhole) with E, which turns the body
    ///    round and backs it down onto the rungs. E catches a ladder while falling past it.
    ///  * W and S climb; let go of them and the body finishes its step and rests with every hand and foot on a rung. Shift with S slides.
    ///  * OFF by stepping onto the floor at the foot, by climbing out at the top (the same pull-up a wall's top gets, PlayerClimbing), with
    ///    Space (a hop backwards) or Control (a drop).
    ///
    /// It owns the frame while on, as the wall climb does (PlayerInputReader). The pose is LadderGait's, from the body's height alone, so
    /// what other players need is only which ladder this body is on: <see cref="Fired"/> says so, again every second while it lasts (for
    /// whoever came into view since), and their copies put their own hands and feet on the rungs (PlayerLocomotion.Ladder.cs).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerLadder : MonoBehaviour, IReplicatedAbility
    {
        public const string Id = "ladder";

        /// <summary>Metres a second up, down, and sliding down.</summary>
        public const float ClimbSpeed = 1.5f;
        public const float DescendSpeed = 1.8f;
        public const float SlideSpeed = 6.5f;

        /// <summary>How far the body's middle hangs off the plane of the rungs.</summary>
        public const float Standoff = 0.36f;

        /// <summary>How near the ladder's line a body gets on from, and how near where it is stood at the top.</summary>
        public const float Reach = 1.0f;
        public const float TopReach = 2.2f;

        /// <summary>Walking at a ladder gets on it within this many degrees of straight at it, from this far off the rungs and this far to the side.</summary>
        public const float WalkOnDegrees = 50.0f;
        public const float WalkOnWithin = 0.9f;
        public const float WalkOnAside = 0.45f;

        /// <summary>Getting on at the foot takes this long; turning round and backing down from the top, this long.</summary>
        public const float GetOnSeconds = 0.25f;
        public const float TopGetOnSeconds = 1.1f;

        /// <summary>Backing down from the top ends hanging this far under it; climbing up, the pull-up out begins this far under it.</summary>
        public const float TopHang = 1.45f;

        /// <summary>Under a shut lid the head stops this far below the top.</summary>
        public const float UnderLid = 0.45f;

        /// <summary>A hop off: backwards and up, metres a second.</summary>
        public const float HopBack = 2.2f;
        public const float HopUp = 1.0f;

        /// <summary>How often a body on a ladder says so again, and how long a copy believes it with nothing new said.</summary>
        public const float SayAgainSeconds = 1.0f;
        public const float RemoteHoldSeconds = 2.5f;

        private enum Phase
        {
            Off,
            GettingOn,
            On,
        }

        private CharacterController _controller;
        private PlayerLocomotion _locomotion;
        private PlayerClimbing _climbing;
        private Ragdoll _ragdoll;

        private Phase _phase;
        private Ladder _ladder;
        private bool _fromTop;
        private float _elapsed;
        private Vector3 _from;
        private Quaternion _fromRotation;
        private float _travel;
        private bool _sliding;
        private bool _walkOnNeedsRelease;
        private float _sinceSaid;

        private bool _remoteOn;
        private Ladder _remoteLadder;
        private bool _remoteSliding;
        private float _remoteUntil;
        private float _remoteLastY;

        public string AbilityId => Id;

        public event Action<AbilityEvent> Fired;

        /// <summary>True from getting on to getting off: the ladder has the body (PlayerInputReader gives it the frame).</summary>
        public bool IsOn => _phase != Phase.Off;

        /// <summary>True while the body is still being brought onto the rungs.</summary>
        public bool IsGettingOn => _phase == Phase.GettingOn;

        public bool IsSliding => _phase == Phase.On && _sliding;

        public Ladder Current => _ladder;

        private float StandingHeight => _locomotion != null ? _locomotion.StandingHeight : BodyRig.StandingHeight;

        private void Awake()
        {
            Bind();
        }

        /// <summary>Finds the body's parts. Public because Awake never runs in EditMode; calling it twice is harmless.</summary>
        public void Bind()
        {
            _controller = GetComponent<CharacterController>();
            _locomotion = GetComponent<PlayerLocomotion>();
            _climbing = GetComponent<PlayerClimbing>();
            _ragdoll = GetComponent<Ragdoll>();
        }

        // ---------------------------------------------------------------- where things are on a ladder

        /// <summary>Where a body's feet are when it is on a ladder at a height.</summary>
        public static Vector3 HangPoint(in Ladder ladder, float y)
        {
            Vector3 p = ladder.Foot + ladder.Normal * Standoff;
            p.y = y;
            return p;
        }

        /// <summary>The top of a ladder as the ledge it is: the wall's edge behind the rungs, the street stood on behind it.</summary>
        public static LedgeProbe.Ledge TopLedge(in Ladder ladder)
        {
            Vector3 top = ladder.Top;
            return new LedgeProbe.Ledge
            {
                Lip = top - ladder.Normal * Ladders.OffWall,
                Stand = ladder.StandAtTop,
                Height = top.y,
                Normal = ladder.Normal,
            };
        }

        /// <summary>The highest a body's feet go: where the pull-up out begins, or with the head under a shut lid.</summary>
        private float Highest(in Ladder ladder) => ladder.Top.y - (ladder.TopOpen ? TopHang : StandingHeight + UnderLid);

        // ---------------------------------------------------------------- getting on

        /// <summary>
        /// The ladder this body could get on from where it is, and whether that would be from its top. From the foot or part-way: beside the
        /// ladder's line, on its own side of it. From the top: near where it is stood on, with the lid off.
        /// </summary>
        public bool Nearby(out Ladder ladder, out bool fromTop)
        {
            fromTop = false;
            if (IsOn || !Ladders.TryNearest(transform.position, TopReach, out ladder))
            {
                ladder = default;
                return false;
            }
            Vector3 p = transform.position;
            float over = p.y - ladder.Top.y;
            if (ladder.TopOpen && Mathf.Abs(over) < 0.8f && Flat(p - ladder.StandAtTop).magnitude < TopReach && Flat(p - ladder.Top).magnitude > 0.2f)
            {
                fromTop = true;
                return true;
            }
            Vector3 off = Flat(p - ladder.Foot);
            return p.y > ladder.Foot.y - 0.5f && p.y < Highest(ladder) + 0.3f && off.magnitude < Reach && Vector3.Dot(off, ladder.Normal) > -0.05f;
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0.0f, v.z);

        /// <summary>
        /// Called every frame on foot: walking into a ladder's foot gets on it. The keys must have been let go since the last time, so
        /// stepping off at the foot with W still held does not climb straight back on.
        /// </summary>
        public bool TryWalkOn(bool forwardHeld, Vector3 wishDirection)
        {
            if (!forwardHeld)
            {
                _walkOnNeedsRelease = false;
                return false;
            }
            if (_walkOnNeedsRelease || IsOn || (_climbing != null && (_climbing.IsClimbing || _climbing.IsToppingOut)))
            {
                return false;
            }
            if (!Nearby(out Ladder ladder, out bool fromTop) || fromTop)
            {
                return false;
            }
            Vector3 wish = Flat(wishDirection);
            Vector3 off = Flat(transform.position - ladder.Foot);
            if (wish.sqrMagnitude < 1e-4f || Vector3.Angle(wish, -ladder.Normal) > WalkOnDegrees || Vector3.Dot(off, ladder.Normal) > WalkOnWithin
                || Mathf.Abs(Vector3.Dot(off, Vector3.Cross(Vector3.up, ladder.Normal))) > WalkOnAside)
            {
                return false;
            }
            GetOn(ladder, false);
            return true;
        }

        /// <summary>Gets on a ladder: brought onto its rungs from where the body is, or from the top turned round and backed down onto them.</summary>
        public void GetOn(Ladder ladder, bool fromTop)
        {
            if (IsOn || _controller == null)
            {
                return;
            }
            _ladder = ladder;
            _fromTop = fromTop;
            _phase = Phase.GettingOn;
            _elapsed = 0.0f;
            _from = transform.position;
            _fromRotation = transform.rotation;
            _travel = 0.0f;
            _sliding = false;
            _controller.enabled = false;   // the body is put where the ladder has it; nothing else moves it
            _locomotion?.SetVerticalVelocity(0.0f);
            Say();
        }

        // ---------------------------------------------------------------- on it

        /// <summary>Owns the frame while on a ladder (PlayerInputReader).</summary>
        public void Tick(bool upHeld, bool downHeld, bool fastHeld, bool jumpPressed, bool dropPressed, float deltaTime)
        {
            if (_phase == Phase.Off)
            {
                return;
            }
            // The body's own animation stands still under the hands and feet the ladder places. (Not the wall climb's clip: held at its
            // end for climbing down, that one draws the body two metres over where it is.)
            _locomotion?.SettleAnimatorToIdle();
            Quaternion facing = Quaternion.LookRotation(-_ladder.Normal, Vector3.up);
            if (_phase == Phase.GettingOn)
            {
                TickGettingOn(facing, deltaTime);
                return;
            }
            if (jumpPressed || dropPressed)
            {
                LetGo(jumpPressed ? _ladder.Normal * HopBack + Vector3.up * HopUp : Vector3.zero);
                return;
            }

            float foot = _ladder.Foot.y, highest = Highest(_ladder);
            float y = transform.position.y;
            int direction = (upHeld ? 1 : 0) - (downHeld ? 1 : 0);
            _sliding = direction < 0 && fastHeld;
            if (direction > 0 && _ladder.TopOpen && y >= highest - 0.001f && _climbing != null)
            {
                // The top: the pull-up out onto the street, hands on the rim.
                End();
                _climbing.MantleOnto(TopLedge(_ladder));
                return;
            }
            if (direction != 0)
            {
                float speed = direction > 0 ? ClimbSpeed : _sliding ? SlideSpeed : DescendSpeed;
                y = Mathf.Clamp(y + direction * speed * deltaTime, foot, highest);
            }
            else
            {
                // Finish the step: on to the nearest height where every hand and foot has its rung.
                float rest = LadderGait.RestNear(LadderGait.RungsUp(y - foot), LadderGait.Floor, LadderGait.RungsUp(highest - foot));
                y = Mathf.MoveTowards(y, foot + Ladders.FirstRung + rest * Ladders.Pitch, ClimbSpeed * 0.6f * deltaTime);
            }
            _travel = Mathf.MoveTowards(_travel, direction, deltaTime * 6.0f);
            transform.SetPositionAndRotation(HangPoint(_ladder, y), Quaternion.RotateTowards(transform.rotation, facing, 540.0f * deltaTime));
            _locomotion?.SetLadder(_ladder, 1.0f, _travel, _sliding);
            if (direction < 0 && y <= foot + 0.001f)
            {
                LetGo(Vector3.zero);   // down onto the floor
                _walkOnNeedsRelease = true;
            }
        }

        private void TickGettingOn(Quaternion facing, float deltaTime)
        {
            float seconds = _fromTop ? TopGetOnSeconds : GetOnSeconds;
            _elapsed += deltaTime;
            float u = Mathf.Clamp01(_elapsed / seconds);
            Vector3 position;
            float limbs;
            if (_fromTop)
            {
                // To where the top is stood on, turning to face the ladder's wall; then the pull-up's own path run backwards: back over the
                // rim and down the rungs, the hands on the rim until the body hangs under it.
                const float turn = 0.3f;
                LedgeProbe.Ledge ledge = TopLedge(_ladder);
                Vector3 hang = HangPoint(_ladder, _ladder.Top.y - TopHang);
                if (u < turn)
                {
                    position = Vector3.Lerp(_from, ledge.Stand, Mathf.SmoothStep(0.0f, 1.0f, u / turn));
                    limbs = 0.0f;
                    _locomotion?.SetLedgeHands(0.0f, ledge.Lip, ledge.Normal);
                }
                else
                {
                    float back = (u - turn) / (1.0f - turn);
                    position = PlayerClimbing.MantlePath(hang, ledge, 1.0f - back);
                    limbs = Mathf.Clamp01((back - 0.35f) / 0.3f);
                    _locomotion?.SetLedgeHands(Mathf.Clamp01(back / 0.3f) * Mathf.Clamp01((1.0f - back) / 0.2f), ledge.Lip, ledge.Normal);
                }
            }
            else
            {
                float y = Mathf.Clamp(_from.y, _ladder.Foot.y, Highest(_ladder));
                position = Vector3.Lerp(_from, HangPoint(_ladder, y), Mathf.SmoothStep(0.0f, 1.0f, u));
                limbs = u;
            }
            transform.SetPositionAndRotation(position, Quaternion.Slerp(_fromRotation, facing, Mathf.SmoothStep(0.0f, 1.0f, Mathf.Clamp01(u * 2.0f))));
            _locomotion?.SetLadder(_ladder, limbs, _fromTop ? -1.0f : 0.0f);
            if (u >= 1.0f)
            {
                _phase = Phase.On;
                _locomotion?.SetLedgeHands(0.0f, Vector3.zero, Vector3.zero);
            }
        }

        // ---------------------------------------------------------------- getting off

        /// <summary>Lets go of the ladder: the body is the controller's again, falling or standing, with a push if it hopped off.</summary>
        public void LetGo(Vector3 push)
        {
            if (_phase == Phase.Off)
            {
                return;
            }
            End();
            if (_controller != null)
            {
                _controller.enabled = true;
                Physics.SyncTransforms();
                if (push.sqrMagnitude > 0.0f)
                {
                    _controller.Move(push * Time.deltaTime);
                }
            }
            _locomotion?.SetVerticalVelocity(push.y);
        }

        /// <summary>The ladder's part is over (the controller is whoever's takes the body next).</summary>
        private void End()
        {
            _phase = Phase.Off;
            _sliding = false;
            _locomotion?.SetLadder(_ladder, 0.0f);
            _locomotion?.SetLedgeHands(0.0f, Vector3.zero, Vector3.zero);
            Fired?.Invoke(new AbilityEvent(0, transform.position, Vector3.up));
        }

        private LadderPrompt _prompt;
        private PlayerInputReader _reader;

        private void Update()
        {
            // The body this machine's player plays names the ladders it could get on (LadderPrompt).
            if (_prompt == null)
            {
                if (_reader == null) _reader = GetComponent<PlayerInputReader>();
                if (_reader != null && _reader.enabled) _prompt = LadderPrompt.For(this);
            }
            if (_phase != Phase.Off)
            {
                // Knocked limp on a ladder: the ragdoll has the body.
                if (_ragdoll != null && _ragdoll.IsLimp)
                {
                    LetGo(Vector3.zero);
                    return;
                }
                _sinceSaid += Time.deltaTime;
                if (_sinceSaid >= SayAgainSeconds)
                {
                    Say();
                }
                return;
            }
            if (!_remoteOn)
            {
                return;
            }
            // A copy: on the ladder its owner said, until told otherwise or nothing has been said for a while.
            if (Time.time > _remoteUntil)
            {
                _remoteOn = false;
                _locomotion?.SetLadder(_remoteLadder, 0.0f);
                return;
            }
            float y = transform.position.y;
            float rate = Time.deltaTime > 0.0f ? (y - _remoteLastY) / Time.deltaTime : 0.0f;
            _remoteLastY = y;
            _locomotion?.SetLadder(_remoteLadder, 1.0f, Mathf.Clamp(rate / ClimbSpeed, -1.0f, 1.0f), _remoteSliding);
        }

        // ---------------------------------------------------------------- other players

        /// <summary>Says which ladder this body is on: its foot, which way its climber's back is, how high it goes; whether it is sliding.</summary>
        private void Say()
        {
            _sinceSaid = 0.0f;
            Fired?.Invoke(new AbilityEvent(1, _ladder.Foot, _ladder.Normal, _ladder.Height, _ladder.TopOpen ? 1.0f : 0.0f, _sliding ? 1 : 0));
        }

        public void PlayRemote(AbilityEvent evt)
        {
            if (evt.Kind != 1 || evt.Direction.sqrMagnitude < 0.25f || !(evt.A > 0.5f) || evt.A > 400.0f)
            {
                _remoteOn = false;
                _locomotion?.SetLadder(_remoteLadder, 0.0f);
                return;
            }
            Vector3 normal = Flat(evt.Direction);
            if (normal.sqrMagnitude < 1e-4f)
            {
                return;
            }
            _remoteLadder = new Ladder { Foot = evt.Origin, Normal = normal.normalized, Height = evt.A, TopOpen = evt.B > 0.5f };
            _remoteSliding = evt.N == 1;
            _remoteOn = true;
            _remoteUntil = Time.time + RemoteHoldSeconds;
            _remoteLastY = transform.position.y;
        }
    }
}
