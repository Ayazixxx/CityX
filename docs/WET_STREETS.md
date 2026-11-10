# Wet streets mirror the city

Owner, 2026-11-06, of the graphics ideas offered: "lets try 1 2 3 and 5 leave 4 for later" (2 being reflections on wet streets); and
2026-11-09: "improve upon ... graphics and smoothness". Before this a wet street was only darker and shinier (docs/RAIN.md): it showed a
blurred sky and nothing of the city. Now, in the rain, the street shows the lamps, the lit windows, the traffic lights and the sky in the
water lying on it.

## How other games do it

Screen-space reflections: for each shiny pixel, the mirrored line of sight is walked across the picture already drawn, and where it meets
something, that something's colour is the reflection. It is the standard for wet streets (GTA V, Cyberpunk 2077, Watch Dogs) because it
costs the same whatever the city holds and needs nothing baked. Its known limit is that it can only show what is on the screen: what is
behind the camera, or hidden, is not in the picture to be found. Games fall back on a blurred sky there, as CityX does.

## How CityX does it

Most engines run the reflections as a pass over the whole finished picture, from a buffer that says how shiny each pixel is. CityX draws
forward, with no such buffer, so the **ground itself looks its reflection up while it is drawn** (`Shaders/CityXWetMirror.hlsl`, called from
the city's surface shader), in **the picture of the frame before**:

- `WetMirrorFeature` (a renderer feature, put on by `WetMirrorSetup`) keeps each frame's picture before post-processing, at half size
  with its smaller sizes under it, and how far from the eye each pixel is (`Shaders/CityXMirrorMemory.shader`), with the matrix the frame was
  drawn with. Only the player's own camera is remembered, and only while the city is wet: on a dry day nothing is kept and the ground's
  shader takes one branch.
- The ground walks the mirrored line of sight through the world in 20 steps, each 1.33 times the last (30 cm to 68 m, 270 m in all), and
  asks the remembered picture at each whether the line has gone behind what is drawn there. The first time it has, the crossing is found
  again between the last two steps (seven halvings), and the picture's colour there is the reflection. A line that passes behind something
  thin (a lamp post, a body) without touching it carries on. A line that leaves the picture reflects nothing, and one that reaches the end
  of its steps reflects where it points, far off (the sky, or a tower too far to step to).
- **Nothing that faces up can be met.** The remembered distance is negative where the surface there faces up (`CityXMirrorMemory.shader`
  works it out from the depths round the pixel, any of the four corners: at the edge of a painted line one pair of neighbours reads as a
  wall a pixel wide). A line mirrored off the ground is climbing, and a climbing line cannot meet the top of anything. Without this the road
  mirrored itself: looked along, one row of a far street's distances is metres from the next, a line just over the road counted as behind
  it on every other row, and thin dark lines ran across the road. More steps, finer halving and a height tolerance did not cure that;
  colouring each pixel by what its line had found showed it.
- **Which ground**: what faces up, outside (the surface's own indoors mark: nothing in a room or under the ground), and holds water (not
  bare earth); none where snow lies. So a floor seen through a doorway stays dry, with no screen-space guess about it.
- **How much**: by the angle, as water does (Schlick, 5% head-on, all of it along the surface), so the street far ahead is a mirror and the
  ground at the feet barely shines. **Puddles** (patches a few metres across, more of them the wetter the city) mirror sharply; the film of
  water between them mirrors 45% as much, read 2.5 sizes down the picture, which is the blur of wet asphalt.
- Because it is done as the ground is drawn, the glass, the rain and the fire drawn afterwards lie over the reflection as they should, and
  the anti-aliasing treats it like any other surface. It is one frame late; at a walk or a sprint that does not show.

Also fixed in passing: it no longer "rains" indoors. The wet look (darker, shinier) was applied to every surface of the city, rooms and
tunnels included; now only to what is outside.

## The switch and the cost

Settings > GRAPHICS > **REFLECTIONS** (docs/GRAPHICS_SETTINGS.md), on in HIGH, off in MEDIUM and LOW. The cost is only paid while the city is
wet: measured on the owner's laptop at 2560x1600 (`-bench-effects`), 0.19 ms a frame at night in heavy rain, standing in a street, and nothing
measurable when dry.

## Limits

- It can only show what is on the screen. What is behind the camera, or hidden behind something, is not in the picture to be found: the
  ground keeps its own dull shine of the sky there.
- Where a reflected line only just clips the edge of something far off, its steps (tens of metres apart by then) hit or miss from pixel to
  pixel, which shows as fine grain along that edge, seen when zoomed in.
- A bright road marking has a faint smear next to it.

## Tests

`WetMirrorPlayTests` (needs `-Graphics`): a white block shows in the wet ground in front of it; nothing when dry (and no picture is kept), on a
floor indoors or on bare earth. (No test holds the stripes off: one that measured a road's rows under an empty sky gave different answers in
a test's small render texture than in the game's own picture, and was taken out; the pictures are the check.) `GraphicsSettingsTests` and
`GraphicsSettingsSceneWiringPlayTests`: the row and the presets. Pictures: `TrafficCapture.RenderTheWetStreets` (`wet-*-off.png`, `wet-*-on.png`).
When the bench's `-bench-effects` reaches the rain it saves the real screen to `bench-rain.bmp`.
