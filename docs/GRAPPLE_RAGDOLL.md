# The grapple swing as a ragdoll (attempt 7)

Owner, 2026-10-28: "improve upon the grapple hook mechanics. try making it so the body goes ragdoll while swinging but the rope is connected to
the hand. i think that will look pretty cool and unique" and, told of the six failures, "we tried the grapple swing a bunch of times and it
always glitched but now ive set you to fable 5.1 please try again".

## Why the six before tore the body apart, and what is different

Every earlier attempt (three historical, 2026-09-02, two on 2026-09-18) kept the swing the controller computes (`GrappleSwing`'s pendulum on
the CharacterController) and tried to make a ragdoll -- one arm, or the whole body jointed to the rope's anchor -- follow it. A kinematic
anchor dragged through a real arc yanks whatever hangs from it, and the limbs flew off at 75-158 m/s.

Now **physics is the swing**. The hook lands and the body goes limp exactly as a knockdown does (`Ragdoll.Activate`: the Animator, the
PlayerLocomotion and the CharacterController off, every body dynamic with the swing's velocity), and the rope is a `ConfigurableJoint` on the
torso's rigidbody at the right shoulder, tied to the hook point in the world: its linear limit is the rope's length, soft (spring 15000,
damper 600) so a slack rope goes taut without a snap, and projected back past 15 cm (`RopeProjectionMetres`: the solver alone let the heavy,
fast body drift metres past a soft limit). The load goes into the torso and not the arm because, hung from the forearm's own body (the
ragdoll's arm is two bodies; the hand bone only ends the forearm), the body's whole weight came down the arm's two joints and the solver let
them part by 30-45 cm. The arm holds the rope line instead: the forearm is pulled toward the hook every tick (`ArmReachAccel`), so the hand
reaches up the rope the hook draws. PhysX swings the hanging body under its own gravity and the rope's pull. Nothing kinematic is ever moved
and the root is never moved while the body is limp (a dynamic body's parent moving teleports it). `GrappleSwing.Rope.cs`.

## How it plays

| | |
|---|---|
| Fire | as before: left-click with the hook equipped, right-click to aim |
| When it hangs | only off the ground with the rope taut (`TautWithin` 0.3 m of its length): hooked from the street the body runs with the rope on its feet, the classic way, and goes limp the moment it swings off the ground, with the speed it had (`RopeDue`). Hooked in the air (off a roof, a jump) it hangs at once |
| On the rope | WASD pushes the torso (`SwingAccel`), Shift reels in and Ctrl pays out (the joint's limit follows the rope's length, 2.5-60 m), no body of the ragdoll goes over `RopedMaxSpeed` (18 m/s) |
| Let go | left-click, or Space for a boosted release (`ReleaseBoost` of height on every body): the rope goes and the body flies on limp; on the ground it lies `LandSettleSeconds` and stands up by itself (the ragdoll's own stand-up). Fall damage as a knocked-down body's |
| Dying on the rope | drops it |
| The rope | drawn by the hook in the hand (GrapplingHookItem), which rides the hand bone on the ragdoll's forearm: from the hook's muzzle to the hook point while the wearer is limp (`RopeOrigin`; upright it comes from the chest as before) |
| The camera | follows the limp body as it does a knocked-down one |
| The switch | `/grapple classic` puts the old controller swing back (`GrappleSwing.RagdollMode`); `/grapple ragdoll` the new. A body with no ragdoll (a test rig) swings the classic way |

Not yet: other players' copies do not hang (a copy keeps its animated pose while the owner swings limp); a limp swing through a car or water.

## Tests

`RopeSwingPlayTests` (the body hangs limp and swings three seconds with the player pushing: the rope on the torso at the right shoulder, no
body over the speed cap, no joint stretched past 25 cm, the rope's pull no more than 60 cm past its length, the hand above the torso; reel and
payout; let go it flies limp, lands and stands up by itself; dying drops the rope; the classic swing behind the switch). Measured on
2026-10-28: fastest body 16 m/s, worst joint stretch 20 cm, the rope 49 cm past its length at the bottom of the swing at most. The older
`GrappleSwing*PlayTests` run the classic swing (they set the switch). Pictures: `RopeSwingCapture` (Logs/CityShots/rope-swing-*.png).
