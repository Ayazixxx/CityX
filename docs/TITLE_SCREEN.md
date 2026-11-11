# The title screen

Owner, 2026-11-11, with a mock-up (logo top left, the three menu words under it, a figure on a rooftop on the right, buildings behind):
"lets remake the title screen ... on the right of the screen is your most recently played character standing on top of a building in idle
stance, holding their primary weapon equipped. there is the city environment in the background, then i want the logo changed to this new
version but I want you to try to make a 3d version of the logo that looks like the file called 3dcityxlogo.jpg ... I also want the magenta
thin lined, graffiti style text in front that says "CITYX" to have like a neon sign effect where it flickers and that part looks like its a
led neon sign"; then "maybe just try building the same thing using the original drawing".

Built by `UI/Editor/TitleScreenBuilder` (the scene, the interface), `UI/Editor/TitleCityBuilder` (the city) and
`UI/Editor/TitleRooftopBuilder` (the character, their lamps, the logo); rebuild with `CityX.UIEditor.TitleScreenBuilder.Build`. Picture: `TitleRooftopCapture` (PlayMode, Explicit, `-Graphics`):
`Logs/CityShots/title-menu.png`, `-pointed-at`, `-neon-faltering`.

## The backdrop: the game's own city (`TitleCityBuilder`, `TitleStage`, `TitleViewpoint`)

Owner, 2026-11-11: "make the city in the background look like the ingame cities that generate ingame. instead of the crappy title screen
buildings, those were meant to be temporary"; "just make it run better on the title screen like theres no need for shatterable windows or
stuff like that just the look of the ingame buildings".

- The game's `CityStreamer` and `SkylineStreamer` (the same world seed, their defaults), in the city's own materials, sky (`CityXSky`) and
  grade (`CityGrade`, `CityNightGrade`), with the game's clear fog (600 m to the skyline's 2200 m), at night (the game's own
  `DayNightCycle`, frozen). Rain over it (`TitleRainLayer`), the vignette. The old row of box towers (`TitleSkylineBaker`) is gone.
- Lighter than the game: the city streams to where the skyline takes over (14 chunks) with rooms and fittings only 4 chunks out; none of
  the game's interactors (glass breaking, doors, light switches, the networked world) is in the scene. The camera never moves, so once the
  city round it is built (the nearest 49 chunks before the first picture, the rest behind) the streamer has nothing more to do.
- **The roof** (`TitleViewpoint`, pure): a plain box of a building 22 to 60 m high, at least 17 m a side, within 450 m of where the game
  starts, its roof its own; looking out over one of its four edges, scored by the towers rising above the roof ahead (nearer and taller
  count more); an edge with any of a building taller than the eye within 70 m in front (a wall in the face) only if every edge has
  one. Chosen from the plan, so the same every
  time for a world; a reroll of the seed chooses again.
- `TitleStage` (after the streamer's Awake) puts the character 4.2 m in from that edge and 2.4 m from the left one, the title's own rooftop there lined up with the view (below), the camera behind them so their feet are at 76%
  across and 10% up a 16:9 view 5.6 m away, looking out over the edge 2.6 degrees down (the first rooftop's).
- While the character list or the creator (each with its own stage and camera) is up, the title camera draws nothing: their stage lights
  would light the city, and drawing it under them is work for nothing.

The rain is off for now, streaks and sound (owner, 2026-11-11: "maybe get rid of the rain for now it will help run a little easier"):
`TitleScreenBuilder.TitleRainOn` puts it back. The spot is searched for again more loosely (any box of a building within 900 m), and
failing everything over where the game starts, so the title stands somewhere in any world: in the city the game plays it once found no
plain roof of the right height and the camera stayed in the middle of nowhere, a wall filling the screen.

The night was kept over the mock-up's light blue sky because the neon is what the logo is about, and neon reads in the dark (ask the owner
if a day sky is wanted after all).

## The logo (`SourceArt/TitleLogo`)

`logo_build.py` builds it in Blender from the owner's drawing (`logo_drawing.png`; the reference `logo_3d_reference.jpg` beside it) and
exports `Assets/CityX/UI/Models/CityXLogo.fbx`:

    "C:/Program Files/Blender Foundation/Blender 4.2/blender.exe" --background --factory-startup --python logo_build.py -- --export "<project>/CityX/Assets/CityX/UI/Models/CityXLogo.fbx" [--render <dir>] [--masks <dir>]

- The drawing's four flat colours are masks: the letters (everything drawn but the white tag), the red outline, the magenta neon, the
  white tag. Their outlines are traced, smoothed and filled as curves.
- **Letters**: the whole letter shape as a slab, its front red (the drawing's outline), its sides black.
- **Face**: the black inside the outline, standing 2 cm proud of the slab and chiselled up to a ridge along each stroke's middle (the
  height at a point is its distance in from the edge, up to a cap: a roof over the stroke), shaded smooth within 35 degrees so each facet
  reads as one plane, as the reference's carved letters do.
- **Neon** and **Tag**: the magenta strokes and the white "CITYX" as rounded ribbons floating in front of the highest ridge.
- The slot left of the Y, outlined red in the drawing but left empty, is filled black (owner, 2026-11-11: "fill in this gap to the left of
  the Y with black it was unintentionally left empty with red outline", with a paint-over keeping the red lines on both sides of it):
  `FILLED_GAPS`, a point in the slot and a red stroke closing it off from the hole beside it.
- About 23k triangles in all.

In the game (`TitleLogo`): held in the top left corner of the view whatever the screen's shape (`TitleLogo.Place`: its top left front corner
at 3% in, 4% down, 40% of the view tall, 6 m ahead, turned 14 degrees so it reads as a solid thing), the materials the game's lit shader
(cel shaded and outlined like everything). The neon is a failing sign (`NeonFlicker`): lit, it hums 5%; every 2.5 to 7 s a fault, a stutter
(2 to 5 quick flashes), a drop-out (dark up to half a second, then stuttering back) or a brown-out (sagging to a third and back over a
second or so). Its emission and a magenta light on the letters follow it together; the game's grade's bloom makes it glow.
It shows on the idle screen and the menu only: every other panel is a box in the middle of the screen
reaching up into its corner.

## The rooftop (`TitleHero`)

The character played last (the highest `LastPlayedUnixSeconds`) stands on a roof just below the camera on the right of the screen, in their
idle, dressed, with their primary weapon in their hands. It is a whole player's body (`Player.prefab`, without its input, with an
inventory and hair), because only a live body breathes in its idle and holds each weapon the way the game does. It is saved switched off
and woken only after `CharacterIdentity.StandBy`, so it is nobody's to play: it counts no playtime and saves nothing, even with a character
still chosen from the game just left (`TitleHeroPlayTests`). Nobody made yet, the roof stands empty (and the belt opens anyway). It goes
while the character list or the creator, each with its own stage, is up.

**The primary weapon is now kept with the character** (`CharacterSave.PrimaryWeapon`): `CharacterIdentity` puts it back in the hands on the
next play and saves whatever is in the primary slot.

They stand on a roof of the title's own over the spot the title chose (above), 8 m over every roof within 80 m so none of the real city comes up through it or beside it (laid level with a real roof, the two buildings clipped into each other): the first rooftop the
owner saw, kept (owner, 2026-11-11: "i like the positioning on this screenshot. also i like the wall behind him. keep this building in
this positioning"): a dark slab with a parapet behind them and along its left edge, a stair housing behind them on the right and an aerial.
They are turned 28 degrees from facing the camera toward the middle of the screen, with two lamps of their own: a warm key from the front
left and a cold rim from behind.
The idle runs at half its speed and the feet are held flat on one spot through it (`PlantedFeet`, as the creator's body on its belt;
owner, 2026-11-11: "his feet glide to the left and right over the floor ... slow the idle animation down by 50%", "the bottom of the feet
should be glued to the floor").

## The menu

The title opens straight on it: the old "PRESS ANY KEY TO START" first screen is gone (owner, 2026-11-11), and Escape on the menu does
nothing.

SINGLEPLAYER, MULTIPLAYER and SETTINGZ down the left under the logo, each the owner's own drawing (owner, 2026-11-11: "replace the
singleplayer, multiplayer, and settings buttons with these new drawings i made"; the drawings in `SourceArt/TitleMenu`, which
`menu_buttons.py` cuts, pads and gives each a soft red haze of its shape, into `Assets/CityX/UI/Textures/Menu`). Pointed at
(`UI/Buttons/DrawnFace`, `ButtonLook` shared with the arrows): the word grows 6%, the red haze comes in flickering, and red embers rise off
the letters as the Bloodrage aura's rise off a body (`Embers`, `EmberGraphic`: 55 a second off points sampled inside the drawing, orange to
red to dark, swelling a little then dying away, in the Bloodrage aura's own dot), owner: "make the red aura animated kind of like the
bloodrage spell, instead of just a red outerglow". Let go, the risen embers finish. Held down, the drawing darkens.
