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

1. **The empty belt.** The body waits 6 m past the screen's left edge, alive (idling): held right, it is about three seconds before it
   comes into sight (owner, 2026-11-10: "make the player model placed a little bit farther to the left off screen so it takes about +2
   seconds for him to appear onto the screen where holding D"; `WaitsFurtherOff`). The prompt is a key cap in the middle of the screen, white, flashing, shown after 2 s: "→", or "D" once a WASD key is pressed and the arrow again on an arrow key (owner, 2026-11-10; `BeltIntro.PulsePrompt`); it fades away in 0.3 s while the belt is run, and comes back if it is let go before the body has arrived.
2. **Rolling in.** A / D, the arrows, or a controller's stick or pad run the belt either way at up to 2.4 m/s (it takes 0.4 s to get
   there). The body rides it; held right, the belt slows to set it down exactly in the middle; let go, the belt stops where it is; left
   runs it back, never past where it waited.
3. **Making.** The belt stops, the tools fade in over 0.6 s, and the creator works as before. The body is **alive**: it stands and idles,
   and is painted as it stands. The button in the top right corner with a T-posing figure on it (lit while the arms are up), or the T key,
   **lifts its arms**: every bone eases to the T-pose over 0.55 s (`CreatorLife`), and the same button lets them down again the same way.
   Only those two do (owner, 2026-11-10: "i dont like the auto tpose when you try to draw. make a t pose icon in the top right and make
   it a button that toggles it").
4. **Rolling out.** After the name is confirmed and the server has made the character, the tools fade out, the body turns back to the
   front and comes alive, and the belt carries it right at full run.
5. **The wider view.** 1.2 m past the right edge of the screen the view changes to a camera 15 m back that shows the belt's end, 6 m on.
6. **Over the roller, a little lean** (owner, 2026-11-10: "feel like he needs a little tilt it still feels like his left leg hovers in
   air and visible goes limp, looks very game engine like not like a cool cutscene"). Alive and idling to the last, feet planted. As
   the front foot goes over the top of the roller, the whole body leans about the back ankle just as far as keeps the front foot on
   the roller as it curves away (`BeltIntro.LeanOverTheRoller`): the lean comes from the roller, not from a script. The belt never
   slows, before or after (owner: "make it keep going at the same speed even after it throws him off").
7. **Handed to physics mid-lean** (owner: "make him fully loose after frame 19"; with a drawing of it pitched out over the drop, "thats
   like the last frame his right leg touches the roller after that he falls"). At 12 degrees (`HandOverAtDegrees`; further, following
   the roller at the full run would spin it round) the body goes limp moving exactly as it was, along with the belt and turning over
   its back foot (`Ragdoll.LetGoTurning`; each bone set where it is drawn, or physics' smoothing drew it a step behind), so nothing
   jumps. Its back foot is snagged (`Ragdoll.Pin`, a hinge over the drop only) and the snag rides the belt to the top of the roller and
   stops there: the body swings out over it loose, and pitched 45 degrees over (or 1.5 s on) is let go. The joints measure their limits
   from the idle (`Ragdoll.MeasureLimitsFromThisPose`): from the T-pose it woke in, the hanging arms were wrenched 46 degrees at the
   elbows in eight frames (owner: "the player puts his hands on his waist right as the conveyer belt rolls them off").
   Tried before, and why not: the feet held on the belt by IK round the drum and the body limp past the end (a leg went through the
   end, then "looks terrible"); a scripted tip ("feels animated it should be ragdoll physics"); the belt slowed to a crawl for the end
   (owner wanted the same speed); springs on the joints (on a body this heavy they held at a hundredth of their strength); one stiff
   body stood on its feet and pinned at the back one (a statue, the front leg held out over nothing, then limp all at once: "game
   engine like"). Two faults found on the way: the ragdoll has no feet, so on the ends of its shins it stood 12 cm into the belt, and
   the shins stuck there were shoved out each step until the legs crossed. Round ends on its feet fix both.
8. **Black.** 6 m under the belt, or 2.5 s after being let go, the screen fades to black (`Core/ScreenBlack`, 0.6 s); then the world
   loads behind it, and the fall from the sky (`IntroCutscene`, docs/INTRO.md) fades up out of the black over 1.2 s as it begins.

## How it is put together

- **The view.** The creator's camera stands where Godot's did (1.6 m up, 4 m in front, 75 degrees), pitched so the lower edge of the
  belt's frame rests on the bottom of the screen with nothing under it, on any screen shape (owner, 2026-11-10: "make the default camera
  angle throughout the whole thing have the bottom black line of the conveyer belt resting at the bottom of the screen"). Zooming out
  with the wheel still shows the grey under it.

- **One body, start to finish.** The creator's body keeps its whole ragdoll now (`CharacterCreatorBuilder.KeepOnlyTheRagdoll`): the
  painted body itself is what falls, with nothing swapped at the cut. The `Ragdoll` holds every bone still until it is let go.
- **Painted alive** (owner, 2026-11-10: "make it so you can draw on your character when its not tposed"). The brush and its atlas know
  the body as it stood when the creator woke, arms out (docs/CHARACTER_CREATOR.md). Alive, the ray goes against the body baked in this
  frame's pose (`CharacterCreator.LivePaint`, `MeshRay`, every triangle, once a frame only while the pointer asks), and the hit is carried
  by its place on its triangle to that triangle as the brush knows it; the ring is drawn on the body as it is on the screen.
- **The tools have nothing behind them** (owner, 2026-11-10: "get rid of this transparent rectangle behind the tools panel. just make it
  only the buttons and tools"): the column and its action bar are clear, here only (`ClearTheColumn`; ReDraw keeps its panel), and still
  stop a stroke starting between two buttons. The words outside the buttons take a dark edge, to read on the sky and on the belt.
- **The feet are stuck to the belt** while it is alive (owner, 2026-11-10: "make the bottom of his feet actually stuck to the conveyer
  belt with IK ... it appears as if his feet are gliding back and forth on the ground"). The idle clip moves the feet up to 15 cm with the
  hips; `PlantedFeet` plays the clip once round when the body first comes alive, sets each foot on the middle of its sway, sole flat on
  the belt, and bends the legs back onto those spots after the Animator every frame (`CreatorLife`), the hips still swaying over them.
  Held in the body's own space, so they ride the belt and turn with the turntable. Holding still, the body is in the exact pose it is
  painted in, feet and all, so they ease over to that; at the belt's end `CreatorLife.LetGo` hands them to the ragdoll with the rest.
- **The belt** (`ConveyorBelt`) builds itself when it wakes: 60 m of rubber slats (laid out again from how far the belt has run, and
  turning down round the drum at its end), a grey steel frame with hubs that turn (grey at the owner's word, 2026-11-10: "make the yellow part of the conveyer belt grey"), and a top and a roller that are bodies of their own, the top running at the belt's speed and the roller turning with it (`ConveyorBelt.Speed`), in grippy rubber, so what stands on them is carried. The roller is round where the slats are drawn round it (owner: "are we sure the hitbox of the roller is even round?"; `TheRollerIsRoundAndWhereTheSlatsGoRoundIt`). **The sky**
  (`BeltSky`): one wall of pale cloud grey behind it (owner, 2026-11-10: "make the background white no blue", then "make it slightly grey background like cloud grey"; it was a blue sky). Both are on the creator's own layer, the only one its two cameras see.
- **Nothing else in the sky** (owner, 2026-11-10, of the first pictures: "I want no clouds in the sky, no ladder holding up the conveyer
  belt and no building remanence left in the background"): the first build had clouds under the belt and legs going down out of sight,
  both taken out. The buildings in those pictures were the title's skyline caught by the picture's own camera, not the game's.
- **Clear air.** The title scene's night haze is laid over every camera's picture and turned the daylight sky black; the creator's two
  cameras draw without it (`View/Lighting/ClearAirCamera`).
- **The world waits for the ride.** `CharacterSelect.Play` hands the loading of the world to `Departure` when one is set and the
  character is new; `BeltIntro.RollOut` plays the ride and calls it in the black.
- **The sound** (owner, 2026-11-10, with a recording: "make this noise play when going right or left on the conveyer belt, and at the
  end when the conveyer belt rolls your character off"). Replaced the same day by a second recording (owner: "replace the conveyer belt noise with this one"; "no use the volume of
  the new loop", louder than the first): it fades in and out at its ends and runs over a minute, so the game plays twenty seconds of its
  steady middle as a loop with no seam, its loudest sample at 0.9 (`UI/Audio/conveyor_belt_loop.ogg`, cut by
  `SourceArt/BeltIntro/belt_loop.py` from the recording kept beside it). It is heard as hard as the belt runs (`BeltRide.Running`,
  `BeltIntro.HumVolume` at the full run): up and down with the belt's speed either way, silent while the character is made, on as the
  belt carries the body off and past its fall, and out with the picture as that goes black. From the wider view it is heard at
  `WideHumShare` of that, the camera being further off (owner, 2026-11-10: "make the conveyer belt 10% quieter and make it a bit quieter
  when it cuts to the far away angle for the roll off").
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
