# The conveyor belt: making a character

Owner, 2026-11-10: "I want to create a new title screen/intro to the game ... when you boot up the game for the first time, it shows a
flat conveyer belt with nothing on it just empty, and there is text or a prompt that says Press D or →. when you press A or D or the left
and right arrow keys, the conveyer belt begins moving in that direction ... when you hold right, it rolls out a plain white playermodel
from the left of the screen ... doing his idle animation. once you have rolled him to the center of the screen the conveyer belt stops, and
the character customization tools appear onto the screen, make this fade in ... i want him to be 'live' during the customization ... have a
t pose mode where he actually lifts his arms into the t pose and moves into place instead of just snapping into position. then once you
complete your character. the conveyer belt start moving to the right again ... once your character reaches the edge of the screen. the
camera angle changes to a more zoomed out angle ... and it keeps rolling right until it rolls your character off the conveyer belt (it
ragdolls in midair) and then as it falls down off screen, the whole screen fades to black. and then fades out of black to the fall
cutscene we have already made ... basically its a conveyer belt in the sky you make your character on that rolls them off at the end."

The inspiration was the opening of an Eddsworld film; the owner: "use my description more than the video as reference the video isnt
exactly what i want it was just the inspiration for it". Nothing of that film's look is copied: the belt, the sky and the body are the
game's own.

## When it plays

- **A start with nobody made yet**: no logo and no menu, the empty belt (`TitleScreen.OnEnable`, `BeltOnFirstStart`).
- **CREATE NEW on the character list** (`TitleScreen.BeginCreator`).
- With characters made the title screen is as it was. (What a returning player's title looks like is still to be talked through.)
- `TitleScreen.Show(Panel.Creator)` alone, which the creator's tests and pictures use, shows the creator as it always was: the body in
  the middle holding still, the tools up, no ride (`BeltIntro.Stand`).

## The beats (`UI/Creator/BeltIntro`, the rules in `BeltRide`)

1. **The empty belt.** The body waits 1.2 m past the screen's left edge, alive (idling). The prompt PRESS D OR → pulses.
2. **Rolling in.** A / D, the arrows, or a controller's stick or pad run the belt either way at up to 2.4 m/s (it takes 0.4 s to get
   there). The body rides it; held right, the belt slows to set it down exactly in the middle; let go, the belt stops where it is; left
   runs it back, never past where it waited.
3. **Making.** The belt stops, the tools fade in over 0.6 s, and the creator works as before. The body is **alive**: it stands and idles.
   Alive it cannot be painted (below), so a press on it with a paint tool, the T-POSE button or the T key **lifts its arms**: every bone
   eases to the pose it is painted in over 0.55 s (`CreatorLife`), and RELAX lets them down again the same way.
4. **Rolling out.** After the name is confirmed and the server has made the character, the tools fade out, the body turns back to the
   front and comes alive, and the belt carries it right at full run.
5. **The wider view.** 1.2 m past the right edge of the screen the view changes to a camera 15 m back that shows the belt's end, 6 m on.
6. **Let go.** At the end the body goes limp (`Ragdoll.Activate`, moving as fast as the belt) and falls.
7. **Black.** 6 m under the belt, or 2.5 s after being let go, the screen fades to black (`Core/ScreenBlack`, 0.6 s); then the world
   loads behind it, and the fall from the sky (`IntroCutscene`, docs/INTRO.md) fades up out of the black over 1.2 s as it begins.

## How it is put together

- **One body, start to finish.** The creator's body keeps its whole ragdoll now (`CharacterCreatorBuilder.KeepOnlyTheRagdoll`): the
  painted body itself is what falls, with nothing swapped at the cut. The `Ragdoll` holds every bone still until it is let go.
- **Alive against holding still.** The brush and the click target are the body as it stood when the creator woke, arms out
  (docs/CHARACTER_CREATOR.md). So paint only lands while it holds exactly that pose (`CharacterCreator.HoldingStill`); alive, a press
  on it asks it to hold still (`WantedStill`) and a press beside it turns it, as always.
- **The feet are stuck to the belt** while it is alive (owner, 2026-11-10: "make the bottom of his feet actually stuck to the conveyer
  belt with IK ... it appears as if his feet are gliding back and forth on the ground"). The idle clip moves the feet up to 15 cm with the
  hips; `PlantedFeet` plays the clip once round when the body first comes alive, sets each foot on the middle of its sway, sole flat on
  the belt, and bends the legs back onto those spots after the Animator every frame (`CreatorLife`), the hips still swaying over them.
  Held in the body's own space, so they ride the belt and turn with the turntable. Holding still, the body is in the exact pose it is
  painted in, feet and all, so they ease over to that; at the belt's end `CreatorLife.LetGo` hands them to the ragdoll with the rest.
- **The belt** (`ConveyorBelt`) builds itself when it wakes: 60 m of rubber slats (laid out again from how far the belt has run, and
  turning down round the drum at its end), a grey steel frame with hubs that turn (grey at the owner's word, 2026-11-10: "make the yellow part of the conveyer belt grey"), and the colliders the limp body lands on. **The sky**
  (`BeltSky`): one wall of open sky behind it. Both are on the creator's own layer, the only one its two cameras see.
- **Nothing else in the sky** (owner, 2026-11-10, of the first pictures: "I want no clouds in the sky, no ladder holding up the conveyer
  belt and no building remanence left in the background"): the first build had clouds under the belt and legs going down out of sight,
  both taken out. The buildings in those pictures were the title's skyline caught by the picture's own camera, not the game's.
- **Clear air.** The title scene's night haze is laid over every camera's picture and turned the daylight sky black; the creator's two
  cameras draw without it (`View/Lighting/ClearAirCamera`).
- **The world waits for the ride.** `CharacterSelect.Play` hands the loading of the world to `Departure` when one is set and the
  character is new; `BeltIntro.RollOut` plays the ride and calls it in the black.
- **The sound** (owner, 2026-11-10, with a recording: "make this noise play when going right or left on the conveyer belt, and at the
  end when the conveyer belt rolls your character off"). The recording fades in over its first four seconds, longer than a roll-in
  takes, so the game plays its steady part as a loop with no seam (`UI/Audio/conveyor_belt_loop.ogg`, cut by
  `SourceArt/BeltIntro/belt_loop.py` from the recording kept beside it). It is heard as hard as the belt runs (`BeltRide.Running`,
  `BeltIntro.HumVolume` at the full run): up and down with the belt's speed either way, silent while the character is made, on as the
  belt carries the body off and past its fall, and out with the picture as that goes black.
- **The black** lasts across the change of scene (`ScreenBlack` is not destroyed with the title) and clears itself after 30 s if nothing
  does.

## Not built yet (the owner's list)

- **The robot arm**: on finishing, the body goes plain white again and a long arm with a marker draws the design back onto it and puts
  the hair on, before the ride off. Needs the strokes kept in order as they are made.
- **Reactions**: the body liking or disliking what is put on it.
- **What a returning player's title screen is.**

## Tests

`BeltRideTests` (the rules of the ride), `BeltIntroPlayTests` (in the real title scene with real keys: the empty belt, rolling in either way, the tools fading in, the arms lifting before paint lands, the ride off, the wider view, the drop, the black, the world waiting for the ride, a first start, the belt heard while it runs, the feet stuck to it),
`BeltIntroCapture` (Explicit, `-Graphics`: `Logs/CityShots/belt-NN-*.png`, every beat).
