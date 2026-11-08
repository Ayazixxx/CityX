# Crimson Claw

A hovering monster: a head with a mouth of teeth, a body that trails off in tendrils, and two claws. `/spawn crimsonclaw`. One of the test
enemies (not content yet). Code: `Character/NPC/CrimsonClaw.cs` (mind, attack, death), `ClawStrikeMotion.cs` (the swipe's motion),
`Character/Editor/CrimsonClawBuilder.cs` (the prefab).

## The model (2026-11-07)

The owner's Meshy model (`Meshy_AI_Crimson_Claw_Monster_20260731_generate.fbx`, 476,748 triangles, no texture coordinates), the same
design the Godot port used. `CityX/SourceArt/CrimsonClaw/claw_patch.py` (Blender) makes the game's copy, `NPC/Models/CrimsonClaw.glb`:

- The head is a shell round a hollow, open underneath behind the lower jaw. Four holes went through its wall: one under each side spike
  and the two in the brow sockets. Each is closed on both faces of the wall with a disc faired into the surface round it (owner: "make it
  look smooth so you could never tell they were there"). The side spikes are never cut or moved ("avoid touching the two spikes").
- The brow-socket discs are its **eyes**: a material of their own, `CrimsonClawEyes`.
- Cut to 20,000 triangles (the eyes' outlines and the side spikes kept whole), in three pieces: `Body`, `LeftClaw`, `RightClaw`.
  `LeftClaw` sits on the model's +X side in Unity (the name is the Godot copy's).

## The eyes

They glow red as if lit from inside the head (owner: "make it glow red from within the head only visible through the eyes"):
`CrimsonClaw.EyesGlow`, HDR red, so the game's bloom spreads it. Only the eyes give off light; nothing on it is a light. Dead, they go out
over `EyesFadeSeconds` (1.5 s; "the glow fades away upon death"), on a copy of their material of its own, so other Claws keep theirs.
Gone out they are dark red.

## The swipe

Phases and their times are unchanged (wind-up 0.18 s, the tell 0.35 s with its glint, the swipe 0.22 s when it can hit, recovery 0.28 s).
Since 2026-11-07 (owner: "make it look more fluid less robotic") the whole body moves (`ClawStrikeMotion`):

- the striking claw rears back and up, coils tighter through the tell with a slight quiver, whips down and in across its front past where
  it aims, and settles;
- the other claw braces back and swings a little the other way;
- the head and body turn the striking side back, then turn and lunge into the blow.

Every part follows one smooth curve through its keys, so nothing stops dead or jumps between phases (it used to ease each phase on its own,
turning one claw on one axis only).

Each claw turns about its own **wrist** (the middle of its back end, `CrimsonClawBuilder.WristOf`). The pivots used to be fixed points
from the Godot port that, in this import, sat behind the body on the other claw's side: every swipe flung the claw round a two-metre loop,
out and back, away from its target. Which way a claw swipes comes from which side it sits on, not its name.

Pictures: `CrimsonClawCapture` (Explicit, `-Graphics`): the attack every 1/20 s from the front and the side
(`crimsonclaw-attack-*.png`), the eyes by day and by night and going out (`crimsonclaw-eyes-*.png`).

Tests: `CrimsonClawTests` (the tell, the dash, back at rest after a swipe, the swipe cuts in across its front, the eyes glow and go out,
only the eyes glow), `ClawStrikeMotionTests` (rest to rest, no jumps, drawn back through the tell, follow-through, the body's part, mirror).
