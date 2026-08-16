using CityX.World;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// The first story NPC, and the front half of the tutorial. Ported from
    /// npc/old_guy.gd.
    ///
    /// He watches you land, offers to show you the ropes, and -- if you
    /// take him up on it -- tells you to claim a building. Once you
    /// actually claim one he walks over to it, asks to move in, then
    /// either lets himself in and potters about the ground floor, or
    /// wanders off.
    ///
    /// TUTORIAL STATE IS SESSION-ONLY, like the claim it waits on.
    /// Quitting and reloading puts him back to his opening line.
    ///
    /// TWO DELIBERATE SIMPLIFICATIONS FROM THE GODOT SOURCE, both named
    /// rather than silently dropped:
    ///
    /// 1. NO OUT-OF-VIEW STAGING TELEPORT. Godot's own MAX_WALK_SECONDS/
    /// STAGING_DISTANCE machinery moves him to a closer, camera-frustum-
    /// checked starting point before a genuinely long walk, so a building
    /// claimed on the far side of an infinite city never reads as several
    /// real minutes of nothing. This port always has him walk the whole
    /// real distance under UniqueNpc's own WalkTimeoutSeconds safety net
    /// instead -- a claim far from wherever he was left standing costs a
    /// longer wait, not a broken tutorial, but it is a real gap from the
    /// polish the reference has.
    ///
    /// 2. A SIMPLER STROLL. Godot's own indoor wander tests real room with
    /// a sphere-cast and a clear-run raycast, specifically to reject the
    /// staircase and a partition wall by testing rather than assuming
    /// where they are. This port picks a random point within a radius of
    /// home centre and only ground-probes it (see WanderSpot) -- it can
    /// occasionally send him toward a wall indoors, where UniqueNpc's own
    /// steering rounds ordinary obstacles but was built for the convex
    /// exteriors of city blocks, not a room's own interior partitions.
    /// </summary>
    public sealed class OldGuy : UniqueNpc
    {
        public enum Stage
        {
            /// <summary>Hasn't said his piece yet.</summary>
            Greeting,

            /// <summary>Turned down; talking again re-offers.</summary>
            Declined,

            /// <summary>Accepted, and told to claim a building.</summary>
            AwaitingClaim,

            /// <summary>Claimed one; he's walking over to it.</summary>
            Travelling,

            /// <summary>Standing outside the claimed building, with the ask still to come.</summary>
            Arrived,

            /// <summary>Said yes; he's letting himself in through the door.</summary>
            MovingIn,

            /// <summary>He lives here now, pottering about the ground floor.</summary>
            LivingInside,

            /// <summary>Said no; he's off wandering the streets.</summary>
            Wandering,
        }

        private const string OptionAccept = "accept";
        private const string OptionDecline = "decline";
        private const string OptionLetHimStay = "let_him_stay";
        private const string OptionTurnHimAway = "turn_him_away";

        private const float ArrivalDoorClearance = 3.5f;
        private const float ArrivalDropHeight = 1.0f;

        private const float LoiterMinSeconds = 4.0f;
        private const float LoiterMaxSeconds = 11.0f;
        private const float WanderRadius = 12.0f;
        private const int WanderAttempts = 10;
        private const float GroundProbeUp = 3.0f;
        private const float GroundProbeDown = 2.0f;

        private const float DoorApproachDistance = 2.2f;
        private const float DoorEntryDepth = 3.0f;
        private const float DoorSearchRadius = 12.0f;
        private const float IndoorWallMargin = 1.2f;

        public Stage CurrentStage { get; private set; } = Stage.Greeting;

        private Vector3 _homeCenter;
        private Vector3 _homeSize;
        private System.Collections.Generic.Queue<Vector3> _entryLegs;
        private float _loiterTimer;
        private ClaimSystem _claimSystem;
        private IOpenableDoor _homeDoor;

        protected override void Start()
        {
            base.Start();
            _claimSystem = Object.FindFirstObjectByType<ClaimSystem>();
        }

        protected override void Update()
        {
            base.Update();

            if (CurrentStage != Stage.LivingInside && CurrentStage != Stage.Wandering)
            {
                return;
            }
            // The walk itself is UniqueNpc's own job; this only decides
            // when to start the next one.
            if (IsWalkingSomewhere)
            {
                return;
            }
            _loiterTimer -= Time.deltaTime;
            if (_loiterTimer > 0.0f)
            {
                return;
            }
            _loiterTimer = Random.Range(LoiterMinSeconds, LoiterMaxSeconds);
            Vector3? spot = WanderSpot(CurrentStage == Stage.LivingInside ? _homeCenter : transform.position,
                CurrentStage == Stage.LivingInside ? Mathf.Max(_homeSize.x, _homeSize.z) * 0.5f - IndoorWallMargin : WanderRadius);
            if (spot.HasValue)
            {
                WalkTo(spot.Value);
            }
        }

        // --- dialogue content, verbatim from old_guy.gd ---

        protected override DialogueNode[] DialogueNodes()
        {
            switch (CurrentStage)
            {
                case Stage.AwaitingClaim:
                    return new[]
                    {
                        new DialogueNode("You gotta open your chat box and type /claim once your inside the building you like"),
                    };

                case Stage.Arrived:
                    return new[]
                    {
                        new DialogueNode("Ok good choice I like this building"),
                        new DialogueNode("By the way i'm homeless is it cool if I crash at your place for a bit",
                            new[]
                            {
                                new DialogueOption("Yes", OptionLetHimStay),
                                new DialogueOption("No", OptionTurnHimAway),
                            }),
                    };

                // Deliberately nothing -- catching him mid-walk, or once
                // he's moved in, or after he's wandered off, are all beats
                // a later pass continues into. Writing lines for them now
                // would mean inventing dialogue nobody asked for.
                case Stage.Travelling:
                case Stage.MovingIn:
                case Stage.LivingInside:
                case Stage.Wandering:
                    return System.Array.Empty<DialogueNode>();

                case Stage.Declined:
                    return new[] { OfferNode() };

                default:
                    return new[]
                    {
                        new DialogueNode("Dude I just saw you fall from the sky"),
                        OfferNode(),
                    };
            }
        }

        private static DialogueNode OfferNode()
        {
            return new DialogueNode("I can teach you the ropes around here if you'd like",
                new[]
                {
                    new DialogueOption("Sure", OptionAccept),
                    new DialogueOption("I'll figure things out myself", OptionDecline),
                });
        }

        protected override void OnDialogueOptionChosen(string id)
        {
            if (id == OptionAccept)
            {
                CurrentStage = Stage.AwaitingClaim;
                WatchForClaim();
                ContinueTalking(new[]
                {
                    new DialogueNode("Alright well first you're gonna want to claim one of these buildings around here, most of them should be empty."),
                    new DialogueNode("You gotta open your chat box and type /claim once your inside the building you like"),
                    new DialogueNode("I'll meet you there, now off with you."),
                });
                return;
            }
            if (id == OptionDecline)
            {
                CurrentStage = Stage.Declined;
                ContinueTalking(new[] { new DialogueNode("Oh alright then") });
                return;
            }
            if (id == OptionLetHimStay)
            {
                // No line for this one -- he just goes. The walk shows what
                // happens next better than a line of acknowledgement would.
                BeginMovingIn();
                return;
            }
            if (id == OptionTurnHimAway)
            {
                CurrentStage = Stage.Wandering;
                // The wandering itself starts once the box closes (see
                // OnDialogueFinished), not here -- walking off mid-sentence
                // would pull him out of earshot of his own last line.
                ContinueTalking(new[]
                {
                    new DialogueNode("Oh alright then no problem, I'll see you around I'm gonna go wander around"),
                });
            }
        }

        protected override void OnDialogueFinished()
        {
            if (CurrentStage == Stage.Wandering && !IsWalkingSomewhere)
            {
                _loiterTimer = 0.0f;
            }
        }

        // --- claiming and travelling ---

        /// <summary>
        /// Watches for the FIRST building the player claims, from here on.
        /// Only one ClaimSystem exists in this single-player port, so
        /// finding it once is enough -- Godot's own per-player
        /// _tutorial_player bookkeeping is a multiplayer concern this port
        /// does not have wired into the live game yet.
        /// </summary>
        private void WatchForClaim()
        {
            if (_claimSystem == null)
            {
                return;
            }
            _claimSystem.BuildingClaimed += OnPlayerClaimedBuildingOnce;
        }

        private void OnPlayerClaimedBuildingOnce(Vector3 groundCentre, Vector3 size)
        {
            // ONE_SHOT: he only turns up for the first building claimed.
            _claimSystem.BuildingClaimed -= OnPlayerClaimedBuildingOnce;
            OnPlayerClaimedBuilding(groundCentre, size);
        }

        /// <summary>
        /// Sets off for the building the player just claimed -- he WALKS
        /// there under his own power rather than appearing at it, so "I'll
        /// meet you there" means what it says. See this class's own doc
        /// comment on the out-of-view staging this port does not do.
        /// </summary>
        private void OnPlayerClaimedBuilding(Vector3 center, Vector3 size)
        {
            if (CurrentStage != Stage.AwaitingClaim)
            {
                return;
            }
            CurrentStage = Stage.Travelling;
            _homeCenter = center;
            _homeSize = size;
            var outside = center + new Vector3(0.0f, ArrivalDropHeight, size.z * 0.5f + ArrivalDoorClearance);

            // Which way to turn once he gets there: back toward the door
            // he's standing outside of, so he reads as waiting for you to
            // come out rather than staring off down the street.
            var face = new Vector3(center.x - outside.x, 0.0f, center.z - outside.z);
            WalkTo(outside, face);
            WalkFinishedOnce(OnArrivedAtBuilding);
        }

        private void OnArrivedAtBuilding()
        {
            CurrentStage = Stage.Arrived;
        }

        // --- moving in ---

        /// <summary>
        /// Everything after "Yes": open the front door if it's shut, then
        /// walk in through it. TWO LEGS, not one -- without the approach
        /// leg he'd aim from wherever he was standing straight at a point
        /// inside the room, which for any door that isn't dead centre of
        /// the wall means aiming at solid wall.
        /// </summary>
        private void BeginMovingIn()
        {
            CurrentStage = Stage.MovingIn;
            _homeDoor = FindDoorNear(HomeFrontCenter());
            if (_homeDoor != null)
            {
                _homeDoor.SetOpen(true);
            }

            Vector3 through = _homeDoor != null
                ? _homeDoor.Position
                : HomeFrontCenter();
            Vector3 inward = Vector3.back;

            _entryLegs = new System.Collections.Generic.Queue<Vector3>();
            _entryLegs.Enqueue(through - inward * DoorApproachDistance);
            _entryLegs.Enqueue(through + inward * DoorEntryDepth);
            WalkNextEntryLeg();
        }

        private void WalkNextEntryLeg()
        {
            if (_entryLegs == null || _entryLegs.Count == 0)
            {
                // He's in. From here on he lives here.
                CurrentStage = Stage.LivingInside;
                _loiterTimer = Random.Range(LoiterMinSeconds, LoiterMaxSeconds);
                return;
            }
            Vector3 leg = _entryLegs.Dequeue();
            WalkTo(leg);
            WalkFinishedOnce(WalkNextEntryLeg);
        }

        /// <summary>
        /// The nearest door within DoorSearchRadius of `point`. There is no
        /// door registry in this port (see BuildingDoor's own comment on
        /// why every interactable watches for itself) -- doors are found
        /// the same way any one-off lookup here has to be, by scanning
        /// every live MonoBehaviour once and filtering by the IOpenableDoor
        /// interface, the same FindObjectsByType-then-filter shape
        /// ThirdPersonCamera.Lockable already uses for IDamageable. A
        /// one-time lookup at arrival, not a per-frame cost.
        /// </summary>
        private static IOpenableDoor FindDoorNear(Vector3 point)
        {
            IOpenableDoor nearest = null;
            float nearestDistance = DoorSearchRadius;
            foreach (MonoBehaviour behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (behaviour is not IOpenableDoor door)
                {
                    continue;
                }
                float distance = FlatDistance(door.Position, point);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = door;
                }
            }
            return nearest;
        }

        private Vector3 HomeFrontCenter()
        {
            return _homeCenter + new Vector3(0.0f, 0.0f, _homeSize.z * 0.5f);
        }

        // --- wandering/strolling ---

        /// <summary>
        /// Somewhere new to walk to, within `radius` of `center` and
        /// ground-probed so a candidate over a stair hole or off a ledge
        /// is skipped. Null when nothing suitable came up in
        /// WanderAttempts tries, which simply means another beat of
        /// standing still. See this class's own doc comment on why this
        /// is simpler than Godot's own indoor room-clearance testing.
        /// </summary>
        private Vector3? WanderSpot(Vector3 center, float radius)
        {
            if (radius <= 0.0f)
            {
                return null;
            }
            for (int attempt = 0; attempt < WanderAttempts; attempt++)
            {
                Vector2 offset = Random.insideUnitCircle * radius;
                var candidate = new Vector3(center.x + offset.x, center.y, center.z + offset.y);
                Vector3? ground = GroundPositionAt(candidate);
                if (ground.HasValue)
                {
                    return ground;
                }
            }
            return null;
        }

        private static Vector3? GroundPositionAt(Vector3 point)
        {
            var from = new Vector3(point.x, point.y + GroundProbeUp, point.z);
            if (Physics.Raycast(from, Vector3.down, out RaycastHit hit,
                    GroundProbeUp + GroundProbeDown, ~0, QueryTriggerInteraction.Ignore))
            {
                return hit.point + Vector3.up * 0.15f;
            }
            return null;
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            return new Vector2(a.x - b.x, a.z - b.z).magnitude;
        }

        /// <summary>WalkFinished, unsubscribed after firing once -- this class's own WalkTo is always a single leg with a single next step.</summary>
        private void WalkFinishedOnce(System.Action handler)
        {
            void Wrapped()
            {
                WalkFinished -= Wrapped;
                handler();
            }
            WalkFinished += Wrapped;
        }
    }
}
