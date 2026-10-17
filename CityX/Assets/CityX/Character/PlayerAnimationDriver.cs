using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Plays the swing animation the attack chain asked for.
    ///
    /// A LISTENER RATHER THAN A SECOND STATE MACHINE. The chain already knows
    /// which swing is playing and when; expressing that again as animator
    /// triggers and conditions would create two state machines that have to
    /// agree about where in a combo you are. They would not, and the symptom
    /// would be a punch that lands while the character plays a different swing
    /// -- exactly the class of drift the Godot build had when the same state
    /// lived in both character.gd and player.gd.
    ///
    /// So the animator gets told, by name, and has no opinion.
    /// </summary>
    [RequireComponent(typeof(MeleeAttacker))]
    public sealed class PlayerAnimationDriver : MonoBehaviour
    {
        [Tooltip("Cross-fade time into a swing. Short -- a punch should start now, not ease in.")]
        public float SwingBlendSeconds = 0.06f;

        [Tooltip("Cross-fade time handing the arms back to the walk cycle when a sword swing or guard ends.")]
        public float UpperBodyReleaseBlendSeconds = 0.15f;

        // The sword's swings and guard live on a masked UPPER-BODY layer so the
        // legs keep walking under them (see PlayerRigBuilder.AddSwordUpperBodyLayer).
        // These names are that layer's contract with the controller.
        public const string UpperBodyLayerName = "UpperBody";

        /// <summary>The upper-body layer's empty default state: playing it hands the arms back to the base layer.</summary>
        public const string UpperEmptyStateName = "UpperEmpty";

        /// <summary>Per-state speed multiplier for upper-body states -- NOT Animator.speed, which would also race the legs.</summary>
        public const string UpperSpeedParameterName = "UpperSpeed";

        private static readonly int UpperSpeedHash = Animator.StringToHash(UpperSpeedParameterName);

        private Animator _animator;
        private MeleeAttacker _melee;
        private int _upperLayer = -2;   // -2 = not looked up yet, -1 = this controller has none
        private int _torsoLayer = -1;
        private int _armsLayer = -1;
        private FistPoseDriver _fist;
        private SwordPoseDriver _sword;
        private FlinchPoseDriver _flinch;
        private GunReloadDriver _gunReload;
        private PlayerVitals _vitals;

        /// <summary>The masked layers the poison breath's scream plays on: torso and head, and the arms (separate, so a sword swing can keep them).</summary>
        public const string TorsoLayerName = "ScreamTorso";

        public const string ScreamArmsLayerName = "ScreamArms";

        /// <summary>
        /// Which layer plays this state: the upper-body layer if it has a
        /// state by that name (the sword's swings, guard and "UpperEmpty"),
        /// otherwise the base layer (0). Asked of the Animator itself, so an
        /// older controller with no upper-body layer simply plays everything
        /// on the base layer as before.
        /// </summary>
        public int LayerFor(string stateName)
        {
            if (_animator == null || string.IsNullOrEmpty(stateName))
            {
                return 0;
            }
            if (_upperLayer == -2)
            {
                _upperLayer = _animator.GetLayerIndex(UpperBodyLayerName);
                _torsoLayer = _animator.GetLayerIndex(TorsoLayerName);
                _armsLayer = _animator.GetLayerIndex(ScreamArmsLayerName);
            }
            int hash = Animator.StringToHash(stateName);
            if (_upperLayer > 0 && _animator.HasState(_upperLayer, hash))
            {
                return _upperLayer;
            }
            // The poison breath's scream: a torso+head layer and an arms layer of its own (see PlayerRigBuilder.AddScreamLayers).
            if (_torsoLayer > 0 && _animator.HasState(_torsoLayer, hash))
            {
                return _torsoLayer;
            }
            if (_armsLayer > 0 && _animator.HasState(_armsLayer, hash))
            {
                return _armsLayer;
            }
            return 0;
        }

        /// <summary>True when this body's Animator has a state of that name (on whichever layer owns it), or it is a procedural one (a punch, a sword attack or guard).</summary>
        public bool HasState(string stateName)
        {
            if (FistStrikes.Has(stateName) || SwordStrikes.Has(stateName) || HitReactions.Has(stateName) || GunReload.Has(stateName))
            {
                return true;
            }
            return _animator != null && !string.IsNullOrEmpty(stateName)
                && _animator.HasState(LayerFor(stateName), Animator.StringToHash(stateName));
        }

        /// <summary>True while the named state is playing (or being blended into) on whichever layer owns it; for a sword attack or guard, while the pose driver plays it.</summary>
        public bool IsPlaying(string stateName)
        {
            if (SwordStrikes.TryGet(stateName, out SwordStrikeKind swordKind))
            {
                return _sword != null && _sword.IsStriking && _sword.Kind == swordKind;
            }
            if (SwordStrikes.TryGetGuard(stateName, out bool guardOneHanded))
            {
                return _sword != null && _sword.IsGuarding && _sword.OneHanded == guardOneHanded;
            }
            if (SwordStrikes.TryGetRebound(stateName, out bool reboundOneHanded))
            {
                return _sword != null && _sword.IsRebounding && _sword.OneHanded == reboundOneHanded;
            }
            if (HitReactions.TryGet(stateName, out HitDirection hitDirection, out bool stagger))
            {
                return _flinch != null && _flinch.IsPlaying && _flinch.Direction == hitDirection && _flinch.IsStagger == stagger;
            }
            if (GunReload.TryGet(stateName, out bool reloadEmpty))
            {
                return _gunReload != null && _gunReload.IsPlaying && _gunReload.IsEmptyReload == reloadEmpty;
            }
            if (_animator == null)
            {
                return false;
            }
            int layer = LayerFor(stateName);
            return _animator.GetCurrentAnimatorStateInfo(layer).IsName(stateName)
                || _animator.GetNextAnimatorStateInfo(layer).IsName(stateName);
        }

        /// <summary>
        /// Raised with the Animator state name every time THIS body starts a
        /// swing or block itself (never for one merely replayed from the
        /// network) -- NetworkedAnimatorSync listens so other players' copies
        /// of this body can play the same state.
        /// </summary>
        public event System.Action<string> StatePlayed;

        private void Awake()
        {
            _animator = GetComponentInChildren<Animator>();
            _melee = GetComponent<MeleeAttacker>();
            // The unarmed strikes are procedural (FistStrikes), played by this on every body: the owner's and a network copy's.
            _fist = GetComponent<FistPoseDriver>();
            if (_fist == null)
            {
                _fist = gameObject.AddComponent<FistPoseDriver>();
            }
            // So are the sword attacks and guards (SwordStrikes), on every body the same way.
            _sword = GetComponent<SwordPoseDriver>();
            if (_sword == null)
            {
                _sword = gameObject.AddComponent<SwordPoseDriver>();
            }
            // And the recoil from a blow (HitReactions), played here when this body is hit, replayed on its copies.
            _flinch = GetComponent<FlinchPoseDriver>();
            if (_flinch == null)
            {
                _flinch = gameObject.AddComponent<FlinchPoseDriver>();
            }
            // And a gun's reload (GunReload): a baked motion the gun in hand lends, played on the owner's body and its copies alike.
            _gunReload = GetComponent<GunReloadDriver>();
            if (_gunReload == null)
            {
                _gunReload = gameObject.AddComponent<GunReloadDriver>();
            }
            _vitals = GetComponent<PlayerVitals>();
        }

        /// <summary>The procedural sword poser on this body (an attack's progress, a guard held), for tests and captures.</summary>
        public SwordPoseDriver SwordPoser => _sword;

        /// <summary>The reload poser on this body.</summary>
        public GunReloadDriver GunReloader => _gunReload;

        /// <summary>The hit-reaction poser on this body.</summary>
        public FlinchPoseDriver Flincher => _flinch;

        /// <summary>
        /// This body was hit (its own vitals say so): the recoil away from the blow, a stagger when the blow took the controls away, a flinch
        /// otherwise -- announced like any other state so other players see it.
        /// </summary>
        private void OnHit(Vector3 fromPosition)
        {
            if (_vitals == null || _vitals.IsDead)
            {
                return;
            }
            Vector3 local = transform.InverseTransformPoint(fromPosition);
            PlayLocalState(HitReactions.NameFor(HitReactions.DirectionOf(local), _vitals.IsStunned));
        }

        /// <summary>Raised when THIS body changes a playing state's speed (a hit-stop freezing a swing and letting it go), so other players' copies freeze too.</summary>
        public event System.Action<string, float> StateSpeedChanged;

        /// <summary>A speed change replayed from the network: clamped like a remote play, never re-announced.</summary>
        public void SetRemoteStateSpeed(string stateName, float speed)
        {
            if (string.IsNullOrEmpty(stateName) || float.IsNaN(speed))
            {
                return;
            }
            ApplyStateSpeed(stateName, Mathf.Clamp(speed, 0.0f, MaxRemoteSpeed));
        }

        private void OnEnable()
        {
            if (_melee != null)
            {
                _melee.SwingStarted += OnSwingStarted;
                _melee.BlockThrown += OnBlockThrown;
            }
            if (_vitals != null)
            {
                _vitals.Hit += OnHit;
            }
        }

        private void OnDisable()
        {
            if (_melee != null)
            {
                _melee.SwingStarted -= OnSwingStarted;
                _melee.BlockThrown -= OnBlockThrown;
            }
            if (_vitals != null)
            {
                _vitals.Hit -= OnHit;
            }
        }

        /// <summary>
        /// The block pose. Told by name for the same reason the swings are: the
        /// window it corresponds to is the vitals' state, and expressing it a
        /// second time as an animator condition is how a body ends up guarding
        /// while the damage says it was not.
        /// </summary>
        private void OnBlockThrown()
        {
            if (_animator == null)
            {
                return;
            }
            // WHICH BLOCK POSE, DECIDED HERE, the same "told by name" split
            // this whole class already uses -- MeleeAttacker.PlayBlock
            // doesn't know or care what a block looks like, only that one
            // was thrown; this is the one place that already asks
            // EquippedWeapon anything for animation purposes.
            string moveset = _melee != null ? _melee.EquippedWeapon?.MovesetId : null;
            string blockState = moveset == ComboChain.Sword ? "SwordBlock" : "Block";
            _animator.speed = 1.0f;
            PlayState(blockState);
            StatePlayed?.Invoke(blockState);
        }

        private void OnSwingStarted(ComboStage stage)
        {
            if (_animator == null || string.IsNullOrEmpty(stage.Animation))
            {
                return;
            }

            // Melee Speed (2026-10-14): the swing runs faster, and other players' copies are told how fast.
            float speed = _melee != null ? _melee.SwingSpeed : 1.0f;
            bool procedural = FistStrikes.Has(stage.Animation) || SwordStrikes.Has(stage.Animation);
            _animator.speed = procedural ? 1.0f : speed;
            PlayState(stage.Animation);
            if (Mathf.Approximately(speed, 1.0f))
            {
                StatePlayed?.Invoke(stage.Animation);
                return;
            }
            ApplyStateSpeedToPose(stage.Animation, speed);
            TimedStatePlayed?.Invoke(stage.Animation, speed, 0.0f);
        }

        /// <summary>A procedural strike's clock (fists, sword, a reload): its pose driver runs at <paramref name="speed"/>. False for anything else.</summary>
        private bool ApplyStateSpeedToPose(string stateName, float speed)
        {
            if (FistStrikes.Has(stateName))
            {
                _fist?.SetSpeed(speed);
                return true;
            }
            if (SwordStrikes.Has(stateName))
            {
                _sword?.SetSpeed(speed);
                return true;
            }
            if (GunReload.Has(stateName))
            {
                _gunReload?.SetSpeed(speed);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Like <see cref="StatePlayed"/> but carrying HOW the state is played:
        /// the animator speed and the clip offset (seconds in) it starts from.
        /// The soulslike sword stretches and trims its swing clips to fit its
        /// own timing, so another player's copy has to play the same fitted
        /// swing or it would see a differently-paced one.
        /// </summary>
        public event System.Action<string, float, float> TimedStatePlayed;

        /// <summary>A gun shot by the player this body is a copy of (GunReload.FireName, from the network): the mounted gun shows it.</summary>
        public event System.Action RemoteGunFired;

        /// <summary>A blow the player this body is a copy of stopped outright with a block (BlockDeflectEffect.StateName, from the network).</summary>
        public event System.Action RemoteDeflected;

        /// <summary>Tells other players' copies of this body about something that already happened here (a gun shot), without playing anything.</summary>
        public void AnnounceState(string stateName)
        {
            if (!string.IsNullOrEmpty(stateName))
            {
                TimedStatePlayed?.Invoke(stateName, 1.0f, 0.0f);
            }
        }

        /// <summary>
        /// Plays a state on THIS body and announces it (so other players'
        /// copies replay it) -- for a caller that decides its own animation
        /// rather than going through MeleeAttacker's chain: the soulslike
        /// sword combat. Same effect as a swing starting, minus the chain.
        /// </summary>
        /// <param name="speed">Animator playback speed for as long as this state plays (1 = the clip's own pace).</param>
        /// <param name="startSeconds">How far into the clip to start, so the boring lead-in can be skipped.</param>
        public void PlayLocalState(string stateName, float speed = 1.0f, float startSeconds = 0.0f)
        {
            if (_animator == null || string.IsNullOrEmpty(stateName))
            {
                return;
            }
            ApplySpeed(stateName, speed);
            PlayState(stateName, startSeconds);
            TimedStatePlayed?.Invoke(stateName, speed, startSeconds);
        }

        /// <summary>
        /// Changes how fast the state now playing runs, on THIS body only (a hit-stop holds it at 0 and lets go),
        /// with the same base-layer / upper-body-layer split as a fresh play.
        /// </summary>
        public void SetStateSpeed(string stateName, float speed)
        {
            if (string.IsNullOrEmpty(stateName))
            {
                return;
            }
            ApplyStateSpeed(stateName, speed);
            StateSpeedChanged?.Invoke(stateName, speed);
        }

        private void ApplyStateSpeed(string stateName, float speed)
        {
            // A procedural attack (or reload) has its own clock; the legs go on walking under a hit-stop.
            if (ApplyStateSpeedToPose(stateName, speed))
            {
                return;
            }
            if (_animator == null)
            {
                return;
            }
            ApplySpeed(stateName, speed);
        }

        /// <summary>
        /// The playback speed for a state. An UPPER-BODY state gets it as its
        /// own per-state multiplier and leaves Animator.speed at 1, so the legs
        /// keep walking at their own pace under a swing that is being stretched
        /// or squeezed to fit its timing; a base-layer state has only the global
        /// Animator.speed to use.
        /// </summary>
        private void ApplySpeed(string stateName, float speed)
        {
            if (LayerFor(stateName) > 0)
            {
                _animator.speed = 1.0f;
                _animator.SetFloat(UpperSpeedHash, speed);
            }
            else
            {
                _animator.speed = speed;
            }
        }

        /// <summary>The playback speeds a remote copy will accept -- a garbled packet cannot freeze or blur an animator.</summary>
        public const float MinRemoteSpeed = 0.1f;

        public const float MaxRemoteSpeed = 4.0f;

        /// <summary>
        /// Plays a swing/block state that another player's copy of this body
        /// just played, WITHOUT re-announcing it (that would echo it straight
        /// back over the network). Ignored if this Animator has no state by
        /// that name, so a garbled or stale name from the wire can't do
        /// anything.
        /// </summary>
        public void PlayRemoteState(string stateName)
        {
            PlayRemoteState(stateName, 1.0f, 0.0f);
        }

        /// <summary>Same, with the fitted speed and clip offset the owner used.</summary>
        public void PlayRemoteState(string stateName, float speed, float startSeconds)
        {
            if (_animator == null || string.IsNullOrEmpty(stateName))
            {
                return;
            }
            if (stateName == GunReload.FireName)
            {
                RemoteGunFired?.Invoke();   // the copy's mounted gun flashes, cracks, throws brass
                return;
            }
            if (stateName == BlockDeflectEffect.StateName)
            {
                RemoteDeflected?.Invoke();   // the copy's guard throws the same sparks and rings the same ting
                return;
            }
            if (FistStrikes.Has(stateName) || SwordStrikes.Has(stateName) || HitReactions.Has(stateName) || GunReload.Has(stateName))
            {
                PlayState(stateName);   // a copy of someone else's body throws the same procedural strike (or raises the same guard, or flinches, or reloads)
                // ...at the owner's pace (Melee Speed, Reload Speed: 2026-10-14). A garbled speed is ignored.
                if (!float.IsNaN(speed) && !Mathf.Approximately(speed, 1.0f))
                {
                    ApplyStateSpeedToPose(stateName, Mathf.Clamp(speed, MinRemoteSpeed, MaxRemoteSpeed));
                }
                return;
            }
            if (!_animator.HasState(LayerFor(stateName), Animator.StringToHash(stateName)))
            {
                return;
            }
            if (float.IsNaN(speed) || float.IsNaN(startSeconds))
            {
                return;
            }
            ApplySpeed(stateName, Mathf.Clamp(speed, MinRemoteSpeed, MaxRemoteSpeed));
            PlayState(stateName, Mathf.Max(0.0f, startSeconds));
        }

        private void PlayState(string stateName, float startSeconds = 0.0f)
        {
            // A blow's recoil lies over whatever the body is doing: it cancels nothing (a swing that kept its poise goes on under it).
            if (HitReactions.TryGet(stateName, out HitDirection hitDirection, out bool stagger))
            {
                _flinch?.Begin(hitDirection, stagger);
                return;
            }
            if (stateName == SwordStrikes.ParryName)
            {
                _sword?.Parry();
                return;
            }
            // A gun's reload: a baked motion on the upper body (GunReloadDriver), the legs walking on under it.
            if (GunReload.TryGet(stateName, out bool reloadEmpty))
            {
                _fist?.Cancel();
                _sword?.Cancel();
                _gunReload?.Begin(reloadEmpty);
                return;
            }
            // An unarmed strike has no clip: the fists and torso are posed by FistPoseDriver (and the legs go on walking).
            if (FistStrikes.TryGet(stateName, out FistKind fistKind))
            {
                _sword?.Cancel();
                _fist?.Begin(fistKind);
                return;
            }
            // Nor has a sword attack or guard: SwordPoseDriver poses the sword arm, the blade and the torso.
            if (SwordStrikes.TryGet(stateName, out SwordStrikeKind swordKind))
            {
                _fist?.Cancel();
                _sword?.Begin(swordKind);
                return;
            }
            if (SwordStrikes.TryGetGuard(stateName, out bool guardOneHanded))
            {
                _fist?.Cancel();
                _sword?.BeginGuard(guardOneHanded);
                return;
            }
            // A blade thrown back off a guard (SoulsSwordCombat's rebound): posed like an attack, on this body and its copies.
            if (SwordStrikes.TryGetRebound(stateName, out bool reboundOneHanded))
            {
                _fist?.Cancel();
                _sword?.BeginRebound(reboundOneHanded);
                return;
            }
            // Anything else takes the arms back from the guard. The upper body's empty state is how a sword attack or guard is ended (the
            // ready stance lingers after it); any other state (a clip) drops the sword pose at once.
            _fist?.Cancel();
            if (stateName == UpperEmptyStateName)
            {
                _sword?.Release();
            }
            else
            {
                _sword?.Cancel();
            }

            int layer = LayerFor(stateName);
            if (!_animator.HasState(layer, Animator.StringToHash(stateName)))
            {
                return;   // e.g. "UpperEmpty" on a controller that predates the upper-body layer
            }

            // EXPLICIT BUG REPORT, root-caused directly with the sword spin
            // attack's own diagnostic render: a real swing thrown before the
            // Animator has ever actually evaluated a single frame crossfades
            // FROM the raw imported bind pose (a T-pose) rather than from a
            // real idle pose -- confirmed directly, rendered, on the very
            // first frame a fresh spin attack was triggered. Same fix, same
            // reasoning GrappleSwing.Attach/DevConsoleView.Open already
            // apply for the identical hazard: a forced zero-delta Update
            // guarantees a real evaluation has happened before anything
            // else touches this Animator, so the blend below always starts
            // from a genuine pose rather than whatever the raw import left
            // bones at.
            _animator.Update(0.0f);

            // CrossFadeInFixedTime, not CrossFade: the normalised version scales
            // the blend by the CURRENT state's length, so the same call blends
            // over a different real duration depending on whether you were
            // idling or sprinting when you threw the punch.
            //
            // Handing the arms BACK to the walk cycle blends slower than a
            // swing snaps in.
            float blend = stateName == UpperEmptyStateName ? UpperBodyReleaseBlendSeconds : SwingBlendSeconds;
            _animator.CrossFadeInFixedTime(stateName, blend, layer, startSeconds);
        }
    }
}
