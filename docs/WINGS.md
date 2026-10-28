# Angel wings: winged flight

Owner, 2026-10-28: "I want you to make winged flying starting with angel wings. make them large angel wings and try to make them look as best
you can, I will make another wings model if meshy when i can if it doesn't come out that good. I was watching footage of winged flying
characters in lego games like hawkeye's flying in lego batman 3, or archangel's flying in lego marvel, and I was very impressed with it and
how the rhythm at which the wings flap always seems to line up with how you are moving. I want to try to recreate this." Then: "just start
with basic angel wings like this il make better models for wings later" (Pit's wings from Kid Icarus), and "no lavender tint just white angel
wings". And: "the jetpack movement could use some work as well ... lets try to improve both" -- the flight itself is shared with the jetpack
(docs/FLIGHT.md).

## What the research said

No Lego source describes the mechanism (TT Games' animation is in a closed format; the owner offered to download a game, not needed). What
the impression comes from, and what was built on:

- **Cycle multiplexing** (DreamWorks, SIGGRAPH 2019, on the Dragons films): a single looping flap cycle scrubbed by a PHASE and scaled by a
  STRENGTH, both driven by the flight, rather than blending separate hover/fly/glide clips, which "isn't good enough for organic motion
  across flight modes". That is the whole mechanism here (`WingBeat`).
- **The beat keeps the beat with the flight** (a student dragon game's devlog): the flap rate must follow what the body does. Here the rate is
  set by hovering / climbing / descending / speed, with beats added while speed is gained.
- **Glide whenever not climbing** (a hobbyist bird devlog): the wings sweep back and hold at speed.
- **Tap and hold** (a Minecraft angel-wings mod): quick taps for small lifts, a hold for a sustained climb; the flaps synced to the client and
  server. Here Space held climbs, and other players see the beat from the synced flight state.

Sources: [Flap Flap Away: animation cycle multiplexing](https://research.dreamworks.com/wp-content/uploads/2019/10/talk_multiplexing_stage2.pdf),
[wingflap devlog](https://babel.speldesign.uu.se/?p=10823), [glide devlog](https://gamesandwutnot.itch.io/flyinghome/devlog/804788/added-glide),
[Eyezo's Angel Wings](https://www.curseforge.com/minecraft/mc-mods/eyezos-angel-wings).

## What was built

| | |
|---|---|
| The item | `angel_wings` ("Angel Wings"), on the back slot with the jetpack and the parachute (one at a time). `/equip wings`, `/unequip back` |
| The model | `AngelWingsBuilder`, drawn in code (a stand-in): each wing a bird's three-bone arm out from the shoulder blade (humerus 0.48 m, forearm 0.74, hand 0.42) with a feather hanging off it by its own bone -- 10 primaries off the hand (the longest 1.15 m), 12 secondaries off the forearm, 4 tertials off the humerus, and over their bases two rows of shorter coverts -- and a sleeve of down along the leading edge. A feather is a tapered strip, rounded at the tip, the trailing vane wider, drooping a little, both faces drawn (the cel line draws each feather's edge). Plain white. 4,408 triangles, 152 bones, one skinned mesh. The wing reaches about 2.5 m from the spine: over 5 m across |
| Flying | docs/FLIGHT.md (`FlightModel`, shared with the jetpack): Space in the air takes off; then nothing held hovers, Space climbs (7 m/s; 10 on Shift with nothing else held), Ctrl sinks (9 m/s), WASD flies at 10 m/s (17 on Shift) with inertia (12 m/s²), flying forward follows the camera up and down; touching the ground lands. The Flight Speed stat scales every speed (now in the game) |
| The beat | `WingBeat`: one looping stroke (the downstroke the quicker 45%), its phase advancing at 1.1 beats a second hovering, 1.8 climbing, 0.6 descending, and sliding from 1.1 down to 0.22 (a glide) as the speed goes from a third of cruising to cruising; +0.12 beats a second for each m/s² of speed gained; a take-off begins a downstroke at once at the strongest beat; a climb begun in an upstroke finishes it three times quicker so its first downstroke comes with the key. Strength 0.8 hovering, 1 climbing, 0.3 gliding, 0.35 descending. On the ground the wings fold in 0.6 s and breathe at 0.25 a second; they open in 0.25 s |
| The body with the beat | Hovering, the body rises and sinks 0.35 m/s with the beat (up on the downstroke) and holds its height; climbing, the climb surges 35% faster on the downstroke and slower on the upstroke. This is what makes the beat and the movement one thing |
| The pose | `WingPose`, every frame after the Animator: the stroke is 38 degrees either side of a slight dihedral at the shoulder, the elbow and the wrist each a tenth of a cycle behind (the wing whips); each feather twists against the stroke, more toward the tip (the vanes bend); the fan opens in flight and closes folded; gliding sweeps the hand back and lifts the wing; descending tucks it back and half closes it. Folded, the humerus stands up and back, the forearm folds back down along it and the hand on down past the hip. The left wing is the right's mirror |
| The lean | Flying forward the body tips 45% of the way the jetpack's Superman does (standing looking up, about 40 degrees nose-down level, further looking down), banks 25 degrees into a strafe |
| Sounds | Made in code (`WingSounds`): a whump of air at each downstroke (louder the deeper), a bigger one with a rush at take-off, a rustle on folding |
| Other players | The wings on their copy of the body (`ICosmeticMountable`), opening and beating by the same rules from what they can see: the synced flight parameter (`JetpackThrusting`, true for any flight gear) and the copy's own movement (climbing above 1.5 m/s up, descending below 2.5 m/s down). `ICosmeticKeepsRunning`: the beat script runs on the copy |

## Not yet

A proper take-off animation on the body (the arms, a crouch), wind on the clothes, the wings casting a visible shadow on the body (they cast
one on the world), any attack with the wings, a stamina cost. The pose code is the owner's model's too: any wing mesh with the same bone
names gets the same beat.

## Tests and pictures

`FlightModelTests`, `WingBeatTests` (the rates by state, beats added gaining speed, the take-off's downstroke, the hurried upstroke, the
quicker downstroke, the tuck and the fold), `WingPoseTests` (open reaches out, folded stands behind the shoulder hand down, the stroke lifts
the tip more than the elbow, the glide and the tuck, the feathers' fan and twist, the mirror), `AngelWingsPlayTests` (on the real body:
folded on the back and open wide mirrored on taking off; the beat following the flight; the bob and the surge; Ctrl landing and folding; a
copy carrying and beating them; the Flight Speed stat). `AngelWingsCapture` (Explicit, `-Graphics`) renders `Logs/CityShots/wings-*.png`:
folded from behind and the side, hovering at the bottom and the top of a beat, from behind, six moments of a beat, a climb, and a glide from
the side, the front and above.
