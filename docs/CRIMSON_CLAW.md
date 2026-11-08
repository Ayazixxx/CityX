# Crimson Claw

A hovering monster: a head with a mouth of teeth, a body that trails off in tendrils, and two claws. `/spawn crimsonclaw`. One of the test
enemies (not content yet). Code: `Character/NPC/CrimsonClaw.cs` (mind, swipe, death), `CrimsonClaw.Grab.cs` (the grab and throw),
`ClawStrikeMotion.cs` and `ClawGrabMotion.cs` (their motions), `Character/Editor/CrimsonClawBuilder.cs` (the prefab).

Drawn at `ModelScale` 2.4 (owner, 2026-11-08: "make the crimson claw 10% bigger", 2 to 2.2; then, so its fist can hold a player, "i dont
mind increasing the size of the crimson claw as a whole slightly": 2.4, see the grab). Its reach, its collision box and its steering's probe
all scale with it.

## The model (2026-11-07)

The owner's Meshy model (`Meshy_AI_Crimson_Claw_Monster_20260731_generate.fbx`, 476,748 triangles, no texture coordinates), the same
design the Godot port used. `CityX/SourceArt/CrimsonClaw/claw_patch.py` (Blender) makes the game's copy, `NPC/Models/CrimsonClaw.glb`:

- The head is a shell round a hollow, open underneath behind the lower jaw. Four holes went through its wall: one under each side spike
  and two on top of the head, over the brow. Each is closed on both faces of the wall with a disc faired into the surface round it (owner:
  "make it look smooth so you could never tell they were there"). The side spikes are never cut or moved ("avoid touching the two spikes").
- Cut to 20,000 triangles (the side spikes kept whole), in three pieces: `Body`, `LeftClaw`, `RightClaw`. `LeftClaw` sits on the model's
  +X side in Unity (the name is the Godot copy's).
- Its **eyes** are marked last, on the cut model (below).

## The eyes

Its eyes are the two long slits across its face under the brow, ringed with small spikes (owner, 2026-11-08, with a picture: "the glow
should be coming from the two circled areas not clipping through any of the spikes and not the two red dots on top of the head"; on
2026-11-07 I had taken the two holes on top for its eyes and made their patches glow, which showed as two red dots on its head).

The slits go through the head's wall into its hollow. What is seen deep inside through them glows: material `CrimsonClawEyes`,
`CrimsonClaw.EyesGlow`, HDR red, so the game's bloom spreads it ("make it glow red from within the head only visible through the eyes").
`claw_patch.py` finds them by looks straight into the face, from the front and round to 60 degrees each side: a slit is where a look goes
12 cm deeper than the face 5 cm above and below it. From each spot in a slit it looks in from 9 x 11 directions; a face met 10 cm past the
slit's lips glows, the gaps between looks are filled, and a face glows only if every corner of it is 7 cm in from the lips and within the
slits' heights, so the spikes and lips round the slits stay skin; and only if it is shut in, most looks out of it over its own side (not
downward, where the head is open underneath) meeting the head again: where a slit wraps round the side of the head, the head's outside
there passed for deep inside and showed red at the corner of the eye (owner, 2026-11-08: "a little bit of red glow poking through the
corner of the eye"). It is done after the cut to 20,000: done before, the cut stretched
triangles from inside out over the brow and the spikes, and the red came out on them.

Only the eyes give off light; nothing on it is a light. Dead, they go out over `EyesFadeSeconds` (1.5 s; "the glow fades away upon death"),
on a copy of their material of its own, so other Claws keep theirs. Gone out they are dark red.

## The swipe

Phases and their times are unchanged (wind-up 0.18 s, the tell 0.35 s with its glint, the swipe 0.22 s when it can hit, recovery 0.28 s).
Since 2026-11-07 (owner: "make it look more fluid less robotic") the whole body moves (`ClawStrikeMotion`):

- the striking claw rears back and up, coils tighter through the tell, whips down and in across its front past where it aims, and settles;
- the other claw braces back and swings a little the other way;
- the head and body turn the striking side back, then turn and lunge into the blow.

Every part follows one smooth curve through its keys (`KeyedMotion`), so nothing stops dead or jumps between phases. The tell used to
quiver, 1.2 degrees 22 times a second; it read as a glitch (owner, 2026-11-08: "it looks right and left super rapidly like 20 times per
second") and is gone.

Each claw turns about its own **wrist** (the middle of its back end, `CrimsonClawBuilder.WristOf`). The pivots used to be fixed points
from the Godot port that, in this import, sat behind the body on the other claw's side: every swipe flung the claw round a two-metre loop,
out and back, away from its target. Which way a claw swipes comes from which side it sits on, not its name.

**Reach** (owner, 2026-11-08: "he was swinging right next to me and should have been hitting but it wasnt reaching my hitbox"): it swung at
anything within 3 m but hit only within 2.5. Now `ClawReachMetres` is how far its claws really reach in front of it (1.15 of the model,
about 2.5 m; a test measures the built claw against it), it swings at what is within that, and a blow lands within that plus half a
body's width (`TargetBodyRadius`, 0.45 m), within 60 degrees either side of where it faces measured to the near edge of the body. A dash
round to its side during the tell still slips it.

**Pressing in** (owner, 2026-11-08: "i feel like i can just hold s and not get hit by him as he is swinging but i hit him if i swing"):
through its wind-up and tell it edges forward at 2.5 m/s (a walk backward is 3), and lunges 1.2 m with the swipe, while its target is more
than 1.65 m off (never into them). Backing off at a walk it now catches you; a dash still gets clear.

## The grab and throw (2026-11-08)

Owner: "can you make an attack where the crimson claw grabs you and throws you", then "i wanted it to be a 1 handed grab... you can use the
swing attack animation but he just grabs his and the raises his arm and throws him", then a drawing of the hold ("make the body fit into
the claw like make him grip it with his thumb", "you are ragdolled when thrown"). Every fourth attack, when its target can be thrown (a body
with a ragdoll this machine runs), its swipe is a grab (2026-11-08: "it should be mostly swing attacks", "should not do the grab or the throw
twice in a row": three swipes at you on your feet come between two grabs; swipes at you lying down after a throw do not count) (`CrimsonClaw.Grab.cs`):

- The same wind-up, tell, glint and swing as a swipe. What the swing reaches (as a swipe would hit it) is caught instead of cut; a
  dash's dodge slips it, and a grab that reaches nothing is a swipe that missed.
- **Held in its fist like a doll** (`PlayerVitals.Seize`, `Ragdoll.Seize`): the body is the ragdoll's, as a knockdown's (no keys, the walker
  and its Animator off), but stiff: every bone stays kinematic, the arms let down, and the whole body is carried upright by its root with
  the middle of its torso in the fist (`Ragdoll.HoldMiddleAt`), eased in from where it stood over 0.3 s. It faces where the Claw faces.
- The arm lifts high with it (lifted, not tipped up: tipped up about its wrist, which is far back under its head, the arm carried the
  body back over the head), rears back a little, and throws it overhand (`ClawGrabMotion`, which runs on from wherever the swipe had
  every part at the catch).
- **Let go, it goes limp for real** (`Ragdoll.LetGo`): it flies ahead of it at 13 m/s and up at 6, tumbling head over heels (the whole
  body spun as one about its torso, each limb a little off it: thrown all at one speed, it sailed off stiff), 20 damage, and gets up 2.5 s
  later as from any knockdown. Killed (or gone) while holding, the Claw drops it, limp.

**The fit** (`CrimsonClaw.FistHollow`; owner: "try to find a way to angle it best so he is seen as within the hand with minimal clipping
through"). A stand-in of the player's body at its real sizes (the torso, shoulders, arms let down, head, legs) was tried in the claw's mesh
in Blender at every angle of the fist (tipped and rolled -30 to 30 degrees, turned -20 to 20), every facing of the body (every 30
degrees) and every place of it round the fist's hollow, at sizes 2.2, 2.4 and 2.6. Each was scored on how much of the chest the talons
wrap round within about 20 cm, less how much of the body and claw lie inside each other, with the head and legs kept clear:

| size | talons round the chest | body inside the claw |
|---|---|---|
| 2.2 | 15/16 | 7.2% |
| 2.4 (chosen: "slightly" bigger) | 15/16 | 6.2% |
| 2.6 | 15/16 | 4.8% |

At 2.4 the fist is tipped 15 degrees back and turned 20 out, the body facing where the Claw faces, its torso's middle at (0.11, 0.06,
0.48) of the model from the wrist. The hold keys of `ClawGrabMotion` turn the claw and head together to that angle. Scripts: `SourceArt/CrimsonClaw/claw_fit.py` (the search) and `claw_fit_look.py` (pictures of a fit).

The Claw's collision box is off from the catch until the grab ends: switched on as it let go, the box stopped the thrown body dead
inside it, and it landed at its feet.

Multiplayer: the Claw itself is still local to each machine (`docs/MULTIPLAYER_STATE.md`), and only the player's own machine can seize
its body (`Seize` refuses a mirrored copy). Everyone sees the player held and thrown, because the owner's limp pose, held stiff or
flying, is streamed to every copy (RagdollMirror).

## Moving and facing

It steers round walls (`NpcSteering`) but never round the body it chases: the player read as a wall to the steering's probe, 6.6 m long at
chest height, so it sidestepped one way and back while it faced each way it stepped. Now it always faces what it chases, turning at most
360 degrees a second (it used to snap round).

## Pictures and tests

Pictures: `CrimsonClawCapture` (Explicit, `-Graphics`): the swipe every 1/20 s from the front and the side (`crimsonclaw-attack-*.png`),
the grab every 1/10 s with close-ups of the hold (`crimsonclaw-grab-*.png`), the eyes by day and by night, from below, and going out (`crimsonclaw-eyes-*.png`).

Tests:
- `CrimsonClawTests`: the tell, the dash, back at rest after a swipe, the swipe cuts in across its front, its reach (lands within, misses
  beyond, matches the model), smooth turning, only throwable targets grabbed, the eyes glow and go out, only the eyes glow.
- `ClawStrikeMotionTests`, `ClawGrabMotionTests`: rest to rest (the grab from wherever the swipe caught), no jumps, the poses of each phase.
- `NpcSteeringTests`: what it chases is not in its way.
- `CrimsonClawGrabPlayTests` (a real player body): caught, held stiff and upright in its fist, thrown limp ahead of it and hurt, up again;
  a dash slips it; killed while holding, it drops the player.
