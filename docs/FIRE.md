# Fire and the gas can

Owner, 2026-10-28: "lets work on fire mechanics. i want you to make a gas can as well i think that will be a good way to test them. I want you
to make the gas can function like the gta v jerry can and look like it as well, it lets you pour gasoline then you can light it on fire, in
our game any fire spell lights up gasoline and probably electric spells as well. make the fire look really good and function like the gta v
fire where it kinda spreads but doesnt really damage terrain just kind of leaves a black burned spot, but we will figure out how fire factors
into the building destruction later when we make building destruction. research how to make AAA level video game fire in unity. and use it
for the fire staff too once you have it looking good"; then "doesnt need to look exactly like the jerry can with all the details just make it
red and in the same shape for now".

## What was looked at

- **GTA V's jerry can and fire** ([Jerry Can](https://gta.wiki/w/Jerry_Can), [Fire](https://gta.fandom.com/wiki/Fire)): hold fire to pour a
  trail on the ground; shooting the trail or any flame lights it and the fire runs along it; pouring on one spot for over 2 s makes a bigger,
  more flammable puddle; fire spreads more on fuels and wood; a fire truck's water puts it out; the can holds 20 litres.
- **Far Cry 2's fire** (the UMD lecture slides on its propagation): the world is a grid of cells with a fuel and a heat each; a burning cell
  looks only at its neighbours; fronts expand and merge; surface type decides how fast a cell catches and how long it burns.
- **Fire in Unity** (Unity Learn's fire tutorial, z4gon's fire VFX, Unity's particle shader docs, Polycount and the Unvanquished procedural
  particle tutorial): the look that reads as real fire is layered particle systems, not one cone: an animated flipbook flame (a sheet of 16
  frames, additive, vertically aligned), a glow layer, embers with a little gravity and turbulence, slow dark smoke above, a flickering point
  light, and soft particles so the flames do not cut into the ground. A flipbook made from seamless noise panned upward loops cleanly when
  its time runs round a circle in noise space.

## How it works

Everything is in `World/Fire/FireField.cs` (pure, unit-tested without a scene) and `Weapons/Fire/` (the scene side).

**The field.** The ground is a sparse grid of 0.75 m cells keyed by their column and their floor height (a roof and the street under it are
different cells). A cell holds **fuel** in seconds of burning, **litres** of gasoline, **heat** (0..1, how high it burns) and whether it has
**burned** (the black spot). Pouring sets gasoline on the cell under the stream (`FireRules.SecondsPerLitre`); a cell that holds over a
litre spills into its four neighbours, so pouring on one spot makes a puddle, as in GTA. Bare pavement has `FireRules.BareFuelSeconds` of
fuel when lit directly (a fireball, the flame staff): it burns a few seconds and dies.

**Spread.** Each tick a burning cell drains its fuel and its heat rises over `FireRules.FlareSeconds`. A gasoline cell lights each neighbour
with `FireRules.SpreadPerSecond` chance a second (so a trail burns at about 3 m/s), gasoline neighbours readily, bare neighbours at
`FireRules.BareCatchShare` of that. Bare cells never light other bare cells: the fire's footprint is the fuel plus a fringe, never the whole
street. A neighbour is found by a floor probe (a ray down near the source's height): no floor within `FireRules.StepMetres` of the source's
height means no spread, so a fire never jumps off a roof edge. A cell under water cannot burn; rain (CurrentWeather) drains a burning cell
twice as fast and bare cells do not catch in it.

**Burned out.** A cell with no fuel left goes out and is marked burned: a black scorch (`FireMarks`, the blood splats' decal: a dark ragged
blob two cells across laid a centimetre over the ground, so neighbours overlap into one burned patch, darkening with the burn, staying a
minute (`FireRules.ScorchLifeSeconds`; owner: "make the burn marks fade after 1 minute") and fading over the next fifteen seconds).
Gasoline shows as a dark puddle that burns off with the fuel. Terrain takes no damage.

**The look** (`FireLook`, `FireSprites`, `View/Shaders/CityXFire.shader`; owner, 2026-10-29, of the first flipbook version: "right now it
just looks like a bunch of triangles.. make it look like real life fire like gta or red dead, even tf2 has pretty good fire"). Four particle
systems shared by every burning cell and fed by hand each frame. The flames and the smoke are quads a shader of our own draws every pixel
of, the way the realtime-VFX crowd builds fire (an Unreal campfire breakdown, Godot's fire shaders, Source's particle fires): a soft
teardrop mask, bent and split into tongues by a coarse noise rising through it and eaten at its edges by a fine one (subtract the noise, then
smoothstep, so the edges tear into licks instead of being cut), coloured by a black-body ramp (red falls off slowest, green faster, blue
fastest: dark red, orange, yellow, white-hot in the core) with values past 1 so the game's bloom makes the core glow. Each particle hands
the shader its own random number and its age through vertex streams, so no two flames burn alike and each is born fast and burns away from
its tips. The noise is a tileable fractal made in code once a session (R fine, G coarse), so it scrolls up through the flames forever
without a seam. It is worked out on another thread as the game starts: made when the first fire was lit, it stopped the game for
123 ms; now the first fire takes about 2 ms to make it (the gradients' sines and cosines are also looked up, no longer worked out per
pixel). Flames are premultiplied (they add their light and hide only part of what is behind them, so they glow at night and stay
orange against a bright sky, where an additive flame washed out pink); the smoke is lit orange by the fire while young and cools to grey.
Under them an additive orange glow on the ground and embers that rise, drift and fall, bright enough for the bloom. All fade where they meet
the street or a wall (soft particles) and close to the camera. Six flickering point lights park over the hottest clusters. Cells past 60 m
from the camera emit half, past 130 m nothing; at most `FireRules.MaxBurningCells` burn at once. The two materials are assets in Resources
(`FireMaterialsBuilder`) so a build keeps the shader; FireSprites copies them and puts the noise on the copies.

**Bodies.** Whoever stands in a burning cell this machine lit burns: `FireRules.DamagePerSecond` in ticks (a spell, no stagger, no blood),
and keeps burning `FireRules.AfterburnSeconds` after leaving at `FireRules.AfterburnDamagePerSecond`, flames on the torso. Water puts a
burning body out: standing in water, the water staff's jet, rain over time. Fire lit from a copy of another player's event (cosmetic) burns
nobody on this machine: the lighter's machine deals the damage and it reaches other players through their copies, as every hit does.

**Lighting it.** `FireScene.Ignite(point, radius, onlyFuel, striker)` from: the Flame Staff's stream (where it meets the world, and any
gasoline under its length), the flamethrower, Self Smite's bolt (gasoline only: lightning lights fuel, not pavement), a bullet's impact
(gasoline only), the fireball (owner, 2026-11-08: "make the gasoline ignite from gunshot and fire spells": gasoline only, 1.5 m round its burst, its whole
blast when charged, on the floor under a body it strikes; 2026-10-29: "fireball doesnt need to light the ground on fire") and a weapon ablaze
(the Fire Enhancement: gasoline near either end of its flames). A copy of another player's ball or weapon lights nothing itself: `FireSync` brings it.
`FireScene.Douse(point, radius)` from the water staff's landing.

**The gas can** ("Gas Can", item id `jerry_can`, `JerryCanItem`; `/equip gascan`, `can` or `jerrycan`; renamed from Jerry Can by the owner 2026-10-29): a red steel can with an X pressed into its sides, a top handle and a spout, drawn in
code (`JerryCanBuilder`), carried by the handle in the right hand, hanging down the fingers; hold left-click to pour: the can tips over the
fingers (`PourTiltDegrees`, about the palm's axis, so its spout corner drops) and an amber stream falls from the spout to the ground ahead
and lays gasoline where it lands, `FireRules.PourLitresPerSecond` out of `FireRules.CanLitres`. Empty, it pours nothing. Copies of other
players tip and pour with their owner.

**The stream's look (2026-10-31; the owner, on a Tuatara Games post faking liquid streams with a spun, deformed tube: "i guess we could do it
for the pour of the gasoline"):** it was a row of thrown drops, which read as dots. Now it is one tube of mesh (`Weapons/Gear/PourStream`,
pure; 21 rings of 8) along the same fall the drops took (the ballistic arc from the spout that lands at the gasoline's cell after
`DropSeconds`), 1.8 cm across at the spout thinning to 1.1 cm, a wave of thickening running down it 2.5 times a second and its foot swaying a
centimetre, rebuilt in place every frame it shows; solid amber on CityX/Lit, so the cel look draws it flat with its black line. Where it lands a
splash of small drops is thrown out and up (the particles the stream used to be). `PourStreamTests` (the fall sags under gravity between spout
and landing, the tube follows it and thins, the wave moves with time); `JerryCanPlayTests` (the stream spans spout to ground while pouring,
gone when the can is empty).

**Other players** see it all (`FireSync`, an IReplicatedAbility on the player body): every pour, ignition and dousing this machine does is
sent as an event and replayed as cosmetic fire on every other machine, so everyone runs the same fire from the same events (the spread's
randomness drifts a little between machines; it is only the look).

## Tests

`FireSpritesTests` (EditMode): the noise tiles without a seam and fills its range; the flames and the smoke are the fire shader's with the
noise on them; the materials are assets. `FireFieldTests` (EditMode): a lit trail burns along its length; bare pavement burns out and spreads no further; gasoline leaves scorched
cells and a fringe; water puts it out and wet ground will not catch; rain blocks bare fire and burns fuel faster; fire does not jump off a
roof edge; pouring on one spot makes a puddle; a cell under water never burns; another machine's fire is cosmetic until this one lights it.
`FirePlayTests` (PlayMode): the flame staff lights a poured trail and the fire runs along it; standing in it burns and keeps burning after
leaving; water puts a burning body out; another player's pour and fire show here and burn nobody until this machine lights it, and this
machine's acts are sent. `JerryCanPlayTests`: it pours ahead until empty, its gasoline catches, a copy carries it in the same hand. The
tests put the staff and the can straight into Player.prefab's hand (`OnEquip`): that body has no inventory list, and NetworkedPlayer.prefab
logs a FishNet error outside a session. `FireCapture` (Explicit, `-Graphics`): the can carried and pouring, a lit trail by day and at
night, the flames close, somebody burning, the scorch after (Logs/CityShots/fire-*.png).

## Not yet

Heat distortion over the fire (a refraction pass); fire on walls and furniture; the can blowing up when shot or burnt; buildings burning
(waits for building destruction); the flame staff's stream drawn with the fire shader (owner: "use it for the fire staff too once you have
it looking good"; a first try with the old flipbook sheet looked like red tents and was dropped: a jet needs its own shape, stretched along
the flow).
