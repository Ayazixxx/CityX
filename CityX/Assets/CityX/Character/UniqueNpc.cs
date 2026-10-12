using UnityEngine;
using UnityEngine.InputSystem;

namespace CityX.Character
{
    /// <summary>One line of a DialogueOption a player can pick.</summary>
    public readonly struct DialogueOption
    {
        public readonly string Text;
        public readonly string Id;

        public DialogueOption(string text, string id)
        {
            Text = text;
            Id = id;
        }
    }

    /// <summary>
    /// One line's worth of script: {Text} advances on Enter, {Text,
    /// Options} waits for a choice instead. Mirrors ui/dialogue_box.gd's
    /// own Dictionary shape.
    ///
    /// LIVES IN CITYX.CHARACTER, NOT CITYX.UI, along with
    /// <see cref="IDialogueBox"/> below -- CityX.UI depends on
    /// CityX.Character, never the reverse (see ClaimSystem's own comment
    /// on the same rule), so an NPC building its own dialogue content
    /// cannot reference the UI-layer DialogueBox type directly. Matches
    /// how IEquippableWeapon already solves the identical problem for
    /// Weapons: the interface and its data types live on the low side of
    /// the dependency, and the concrete UI implementation on the high
    /// side implements it.
    /// </summary>
    public readonly struct DialogueNode
    {
        public readonly string Text;
        public readonly DialogueOption[] Options;

        public DialogueNode(string text, DialogueOption[] options = null)
        {
            Text = text;
            Options = options ?? System.Array.Empty<DialogueOption>();
        }
    }

    /// <summary>What UniqueNpc needs from a dialogue box, without depending on CityX.UI to get it.</summary>
    public interface IDialogueBox
    {
        bool IsOpen { get; }
        event System.Action Finished;
        event System.Action<string> OptionChosen;
        void Open(string speaker, DialogueNode[] nodes);
        void Close();
    }

    /// <summary>
    /// A named, hand-authored NPC: talks when you press E next to it, and
    /// can be sent to walk somewhere (see WalkTo). Ported from
    /// npc/unique_npc.gd.
    ///
    /// NOT A COMBATANT. ApplyHit is deliberately a no-op: these are story
    /// characters, and "I punched the guy who was going to tell me
    /// something and now he's a ragdoll" is a worse outcome than not being
    /// able to hit him.
    ///
    /// THE INTERACT TRIGGER IS RADIO.CS'S OWN ESTABLISHED PATTERN -- a
    /// proximity SphereCollider trigger plus polling the E key in Update
    /// while someone is inside it. "Someone" is loosely "anything with a
    /// CharacterController," the exact same looseness Radio.cs and
    /// BuildingDoor.cs already both carry -- harmless there and here for
    /// the same reason: only the player ever actually presses a key.
    ///
    /// INPUT GATING FOLLOWS MapPanelView/InventoryPanelView/PlayerChat'S
    /// OWN PRECEDENT, not a shared counter -- none exists yet in this port
    /// (see MapPanelView's own comment on why). This is now the fourth
    /// consumer of the same disable-the-two-components pattern, which is
    /// exactly the point that comment names as "the point at which that
    /// should become a real shared gate instead" -- flagged rather than
    /// silently worked around, since building that gate now would be a
    /// cross-cutting change to three files that already ship and work.
    /// A dialogue simply refuses to open while input is already gated by
    /// something else (checked once, not mutually against every other
    /// panel by name), and does not itself guard the other three against
    /// opening while IT holds the gate.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class UniqueNpc : MonoBehaviour, IDamageable
    {
        public const float InteractRange = 2.5f;
        public const float WalkSpeed = 2.4f;
        public const float WalkArriveDistance = 0.8f;
        public const float WalkTimeoutSeconds = 120.0f;
        public const float TurnDegreesPerSecond = 360.0f;
        public const float Gravity = 9.8f;

        [Tooltip("Overrides SpeakerName() as the dialogue speaker.")]
        public string SpeakerNameOverride = "";

        [Tooltip("Plain single-line dialogue. An NPC with branching conversation overrides DialogueNodes() instead.")]
        public string[] Lines = System.Array.Empty<string>();

        [Tooltip("Instantiated once, lazily, the first time this NPC talks.")]
        public GameObject DialogueBoxPrefab;

        /// <summary>Fired when WalkTo finishes, whether by arriving or by timing out.</summary>
        public event System.Action WalkFinished;

        protected CharacterController Controller;
        protected Animator BodyAnimator;

        private Transform _hips;
        private Transform _spine;
        private IDialogueBox _dialogue;
        private Transform _nearbyPlayer;
        private bool _gatedInput;
        private PlayerInputReader _inputReader;
        private ThirdPersonCamera _orbitCamera;

        private Vector3 _walkTarget = Vector3Inf;
        private Vector3 _walkFacing = Vector3Inf;
        private float _walkTimeRemaining;
        private float _verticalVelocity;
        private readonly NpcSteering _steering = new NpcSteering();

        private static readonly Vector3 Vector3Inf =
            new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);

        private static readonly int SpeedParameter = Animator.StringToHash("Speed");
        private static readonly int GroundedParameter = Animator.StringToHash("Grounded");

        public bool IsWalkingSomewhere => !float.IsPositiveInfinity(_walkTarget.x);

        public bool IsTalking => _dialogue != null && _dialogue.IsOpen;

        /// <summary>Mirrors PistolWeapon.HasNearbyPlayer -- lets a test see OnTriggerEnter's result without a real key press.</summary>
        public bool HasNearbyPlayer => _nearbyPlayer != null;

        // --- IDamageable: story characters never take damage. ---
        bool IDamageable.IsAlive => true;

        HitResponse IDamageable.ApplyHit(float amount, Vector3 fromPosition, float hitstunSeconds)
        {
            return HitResponse.Ignored;
        }

        private bool _bound;

        protected virtual void Awake()
        {
            Bind();
        }

        /// <summary>
        /// The real setup Awake() normally does. PUBLIC AND SEPARATE FROM
        /// AWAKE, matching Radio.Bind()'s own established precedent --
        /// Awake never runs on an AddComponent in an EditMode test (no
        /// scene load, nothing pumps the player loop), so a test has to
        /// call this by hand to get an object as real as the one the game
        /// actually plays with. Idempotent, so a subclass calling it again
        /// after its own base.Awake() already ran costs nothing.
        /// </summary>
        public void Bind()
        {
            if (_bound)
            {
                return;
            }
            _bound = true;

            Controller = GetComponent<CharacterController>();
            BodyAnimator = GetComponentInChildren<Animator>();
            _hips = BodyAnimator != null ? BodyAnimator.GetBoneTransform(HumanBodyBones.Hips) : null;
            _spine = BodyAnimator != null ? BodyAnimator.GetBoneTransform(HumanBodyBones.Spine) : null;
            _steering.ConfigureFrom(Controller);

            var trigger = gameObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = InteractRange;
        }

        /// <summary>
        /// See PlayerLocomotion's own identical LateUpdate for why (hips and
        /// spine both -- freezing hips alone just moved the reported wiggle
        /// up to the torso).
        /// </summary>
        protected virtual void LateUpdate()
        {
            if (_hips != null)
            {
                _hips.localRotation = Quaternion.identity;
            }
            if (_spine != null)
            {
                _spine.localRotation = Quaternion.identity;
            }
        }

        protected virtual void Start()
        {
            _inputReader = Object.FindFirstObjectByType<PlayerInputReader>();
            _orbitCamera = Object.FindFirstObjectByType<ThirdPersonCamera>();
        }

        private void OnTriggerEnter(Collider other)
        {
            // EXCLUDES THIS NPC'S OWN CONTROLLER, NOT JUST A NULL CHECK.
            // Bind() puts the interact trigger on the same GameObject as the
            // CharacterController RequireComponent demands, and with no
            // Rigidbody grouping them Unity treats the two as separate
            // bodies -- this trigger genuinely fires against its own
            // controller the moment physics first evaluates it. A plain
            // null check let that self-hit through: _nearbyPlayer == this
            // NPC's own transform means distance-to-self is permanently
            // 0.00, which is also permanently inside InteractRange, so the
            // stale-reference self-heal in HandleInteractInput can never
            // clear it -- E starts a conversation from literally anywhere,
            // forever, confirmed directly from CityX-NPC-DIAG log output.
            CharacterController controller = other.GetComponentInParent<CharacterController>();
            if (controller != null && controller != Controller)
            {
                _nearbyPlayer = other.transform;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!CityX.Character.InteractionReach.IsBodyLeaving(other))
            {
                return;   // a limb's collider swinging out of the trigger is not the player walking away
            }
            if (_nearbyPlayer != null && other.transform.IsChildOf(_nearbyPlayer.root))
            {
                _nearbyPlayer = null;
                // Walking off mid-sentence ends the conversation rather than
                // leaving a box on screen for an NPC no longer in front of you.
                if (_dialogue != null && _dialogue.IsOpen)
                {
                    _dialogue.Close();
                }
            }
        }

        protected virtual void Update()
        {
            DriveWalk(Time.deltaTime);
            HandleInteractInput();
        }

        private void HandleInteractInput()
        {
            if (_nearbyPlayer == null)
            {
                return;
            }
            // Re-checked here rather than trusted from the trigger alone: a
            // collider disabled while still inside the trigger volume never
            // fires OnTriggerExit -- exactly what happens the instant the
            // player dies, since Ragdoll.Activate() turns their
            // CharacterController off mid-frame (see its own comment).
            // Without this, dying once within InteractRange of this NPC
            // leaves _nearbyPlayer permanently stale, and E would start a
            // conversation from anywhere in the world, forever after,
            // including from beyond the grave.
            if (InteractionReach.IsBeyond(Vector3.Distance(_nearbyPlayer.position, transform.position), InteractRange))
            {
                _nearbyPlayer = null;
                return;
            }
            if (Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame)
            {
                return;
            }
            // E only ever STARTS a conversation -- Enter advances it, inside
            // DialogueBox itself.
            if (IsTalking)
            {
                return;
            }
            // Refuses to open while input is already gated by something
            // else (map, inventory, chat, another dialogue) -- see this
            // class's own doc comment on why that is one check rather than
            // an explicit guard against each panel by name.
            if (_inputReader != null && !_inputReader.enabled)
            {
                return;
            }
            BeginTalking();
        }

        // --- walking ---

        /// <summary>
        /// Sends this NPC walking to <paramref name="destination"/> under
        /// its own power, rounding anything in the way.
        /// <paramref name="faceDirection"/> is which way to turn on
        /// arrival; pass Vector3.positiveInfinity (the default) to keep
        /// whatever heading the last stride left it on.
        /// </summary>
        public void WalkTo(Vector3 destination, Vector3? faceDirection = null)
        {
            _walkTarget = destination;
            _walkFacing = faceDirection ?? Vector3Inf;
            _walkTimeRemaining = WalkTimeoutSeconds;
            _steering.Reset();
        }

        public void CancelWalk()
        {
            if (!IsWalkingSomewhere)
            {
                return;
            }
            _walkTarget = Vector3Inf;
            _walkFacing = Vector3Inf;
            StopWalking();
        }

        private void DriveWalk(float deltaTime)
        {
            // Gravity owns the vertical for every frame, walking or not --
            // a real controller settles onto whatever ground it was
            // dropped above rather than hovering.
            if (Controller.isGrounded && _verticalVelocity <= 0.0f)
            {
                _verticalVelocity = -2.0f;
            }
            else
            {
                _verticalVelocity -= Gravity * deltaTime;
            }

            if (!IsWalkingSomewhere)
            {
                Controller.Move(new Vector3(0.0f, _verticalVelocity * deltaTime, 0.0f));
                return;
            }

            Vector3 toTarget = _walkTarget - transform.position;
            toTarget.y = 0.0f;
            float distance = toTarget.magnitude;
            _walkTimeRemaining -= deltaTime;
            if (distance <= WalkArriveDistance || _walkTimeRemaining <= 0.0f)
            {
                FinishWalk();
                return;
            }

            Vector3 direction = _steering.Steer(transform, toTarget / distance, deltaTime);
            Vector3 motion = direction * WalkSpeed;
            motion.y = _verticalVelocity;
            Controller.Move(motion * deltaTime);

            if (direction.sqrMagnitude > 0.0001f)
            {
                Quaternion target = Quaternion.LookRotation(direction, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, target, TurnDegreesPerSecond * deltaTime);
            }

            if (BodyAnimator != null && BodyAnimator.enabled)
            {
                BodyAnimator.SetFloat(SpeedParameter, WalkSpeed);
                BodyAnimator.SetBool(GroundedParameter, Controller.isGrounded);
            }
        }

        private void FinishWalk()
        {
            _walkTarget = Vector3Inf;
            StopWalking();
            if (!float.IsPositiveInfinity(_walkFacing.x))
            {
                FaceAlong(_walkFacing);
                _walkFacing = Vector3Inf;
            }
            WalkFinished?.Invoke();
        }

        private void StopWalking()
        {
            _steering.Reset();
            if (BodyAnimator != null && BodyAnimator.enabled)
            {
                BodyAnimator.SetFloat(SpeedParameter, 0.0f);
            }
        }

        private void FaceAlong(Vector3 direction)
        {
            var flat = new Vector3(direction.x, 0.0f, direction.z);
            if (flat.sqrMagnitude < 0.000001f)
            {
                return;
            }
            transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
        }

        // --- dialogue ---

        /// <summary>
        /// The conversation to open right now. The default turns
        /// <see cref="Lines"/> into one node per line; a subclass with
        /// branching or state overrides this (see OldGuy) and never
        /// touches Lines at all.
        /// </summary>
        protected virtual DialogueNode[] DialogueNodes()
        {
            var nodes = new DialogueNode[Lines.Length];
            for (int i = 0; i < Lines.Length; i++)
            {
                nodes[i] = new DialogueNode(Lines[i]);
            }
            return nodes;
        }

        /// <summary>What DialogueNodes() would return right now, without actually opening a conversation. Public so a test can check content directly.</summary>
        public DialogueNode[] PreviewDialogueNodes()
        {
            return DialogueNodes();
        }

        /// <summary>Called when a conversation ends normally. A plain NPC has nothing to react to.</summary>
        protected virtual void OnDialogueFinished()
        {
        }

        /// <summary>Called when the player picks a reply.</summary>
        protected virtual void OnDialogueOptionChosen(string id)
        {
        }

        /// <summary>
        /// Starts a conversation directly -- the same thing pressing E
        /// near this NPC does. Public so a test can drive it without
        /// needing a real trigger-enter physics event (OnTriggerEnter does
        /// not reliably fire in an EditMode test with no physics step
        /// running), and so nothing about starting a conversation is
        /// actually tied to E specifically.
        /// </summary>
        public void Talk()
        {
            BeginTalking();
        }

        private void BeginTalking()
        {
            DialogueNode[] nodes = DialogueNodes();
            if (nodes.Length == 0)
            {
                return;
            }
            EnsureDialogueBox();
            if (_dialogue == null)
            {
                return;
            }
            GateInput();
            _dialogue.Open(SpeakerName(), nodes);
        }

        /// <summary>
        /// Opens a follow-on stretch of conversation on the same box --
        /// what a subclass calls from OnDialogueOptionChosen to continue
        /// after a reply.
        /// </summary>
        protected void ContinueTalking(DialogueNode[] nodes)
        {
            if (nodes.Length == 0)
            {
                return;
            }
            EnsureDialogueBox();
            if (_dialogue == null)
            {
                return;
            }
            GateInput();
            _dialogue.Open(SpeakerName(), nodes);
        }

        private void EnsureDialogueBox()
        {
            if (_dialogue != null)
            {
                return;
            }
            if (DialogueBoxPrefab == null)
            {
                Debug.LogWarning($"CityX: {name} has no DialogueBoxPrefab wired -- it cannot talk.");
                return;
            }
            GameObject instance = Instantiate(DialogueBoxPrefab, transform);
            _dialogue = instance.GetComponent<IDialogueBox>();
            if (_dialogue == null)
            {
                Debug.LogWarning($"CityX: {name}'s DialogueBoxPrefab has no IDialogueBox component -- it cannot talk.");
                return;
            }
            _dialogue.Finished += HandleDialogueFinished;
            _dialogue.OptionChosen += HandleDialogueOptionChosen;
        }

        private void GateInput()
        {
            if (_gatedInput)
            {
                return;
            }
            if (_inputReader != null) _inputReader.enabled = false;
            if (_orbitCamera != null) _orbitCamera.enabled = false;
            _gatedInput = true;
        }

        private void ReleaseInputGate()
        {
            if (!_gatedInput)
            {
                return;
            }
            _gatedInput = false;
            if (_inputReader != null) _inputReader.enabled = true;
            if (_orbitCamera != null) _orbitCamera.enabled = true;
        }

        private void HandleDialogueFinished()
        {
            ReleaseInputGate();
            OnDialogueFinished();
        }

        private void HandleDialogueOptionChosen(string id)
        {
            // Released BEFORE the subclass runs: it will usually open the
            // next stretch straight away, which re-gates. Doing it in this
            // order keeps the gate held rather than flickering off and on.
            ReleaseInputGate();
            OnDialogueOptionChosen(id);
        }

        private void OnDestroy()
        {
            ReleaseInputGate();
        }

        public string SpeakerName()
        {
            return string.IsNullOrEmpty(SpeakerNameOverride) ? name : SpeakerNameOverride;
        }
    }
}
