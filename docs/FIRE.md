# Fire and the jerry can

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
blob two cells across laid a centimetre over the ground, so neighbours overlap into one burned patch, darkening with the burn and staying
`FireRules.ScorchLifeSeconds`). Gasoline shows as a dark puddle that burns off with the fuel. Terrain takes no damage.

**The look** (`FireLook`, `FireSprites`): four particle systems shared by every burning cell and fed by hand each frame. Flipbook flames
standing upright (vertical billboards of a 16-frame sheet drawn in code: a tongue that sways, wobbles, breaks into tongues at its tip and
has licks of brighter fire through it, white-yellow at its core and red at its edge; it loops because its time runs round a circle in noise
space), premultiplied rather than additive so they stay orange against a bright sky (rendered both ways on 2026-10-29: additive tongues washed
out pink over the sky), with a smaller hot core tongue at most flames' base; an additive orange glow on the ground under them; embers that
rise, drift and fall; slow dark smoke above. All soft particles (they fade where they meet the street). Six flickering point lights park
over the hottest clusters. Cells past 60 m from the camera emit half, past 130 m nothing; at most `FireRules.MaxBurningCells` burn at once.

**Bodies.** Whoever stands in a burning cell this machine lit burns: `FireRules.DamagePerSecond` in ticks (a spell, no stagger, no blood),
and keeps burning `FireRules.AfterburnSeconds` after leaving at `FireRules.AfterburnDamagePerSecond`, flames on the torso. Water puts a
burning body out: standing in water, the water staff's jet, rain over time. Fire lit from a copy of another player's event (cosmetic) burns
nobody on this machine: the lighter's machine deals the damage and it reaches other players through their copies, as every hit does.

**Lighting it.** `FireScene.Ignite(point, radius, onlyFuel, striker)` from: the Flame Staff's stream (where it meets the world, and any
gasoline under its length), the flamethrower, a fireball's burst, Self Smite's bolt (gasoline only: lightning lights fuel, not pavement) and a
bullet's impact (gasoline only). `FireScene.Douse(point, radius)` from the water staff's landing.

**The jerry can** (`JerryCanItem`, `/equip jerrycan`): a red steel can with an X pressed into its sides, a top handle and a spout, drawn in
code (`JerryCanBuilder`), carried by the handle in the right hand, hanging down the fingers; hold left-click to pour: the can tips over the
fingers (`PourTiltDegrees`, about the palm's axis, so its spout corner drops) and an amber stream falls from the spout to the ground ahead
and lays gasoline where it lands, `FireRules.PourLitresPerSecond` out of `FireRules.CanLitres`. Empty, it pours nothing. Copies of other
players tip and pour with their owner.

**Other players** see it all (`FireSync`, an IReplicatedAbility on the player body): every pour, ignition and dousing this machine does is
sent as an event and replayed as cosmetic fire on every other machine, so everyone runs the same fire from the same events (the spread's
randomness drifts a little between machines; it is only the look).

## Tests

`FireFieldTests` (EditMode): a lit trail burns along its length; bare pavement burns out and spreads no further; gasoline leaves scorched
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
(waits for building destruction); the flame staff's stream drawn with the new flame sheet (owner: "use it for the fire staff too once you
have it looking good"). The fire's particle materials are made at runtime: a player build that strips unused shader variants would need
them as assets (the soft-particle variant exists through Self Smite's glow asset; the premultiplied one does not yet).
