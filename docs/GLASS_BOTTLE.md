# Glass bottles (2026-11-23)

Owner: "the glass bottle we have now is really outdated ... improve upon how the player holds it in hand and the throw, and the glass break.
try making a few different shaped glass bottles based on common glass bottle shapes that are throwable and use the one we have now for
inspiration"; then, as it was built: "make him carry it like the demoman and be able to swing and have a broken bottle that does pierce
damage. so it starts as a blunt", "hold right click to aim and throw it", "the bottle breaks upon landing on an enemy or object or terrain",
"when swung and hits anything while in hand it turns into the half broken version with sharp points ... make your own fracture pattern ...
tf2 does the same mechanic with melee bottles. but when thrown the whole thing shatters it doesnt leave a half you can pick up".

What it replaced: a brown eight-sided cylinder drawn in code (the owner's model had never been brought over from the Godot game), held by its
base, swung with a punch, thrown by vanishing from the hand and appearing in the air before the chest, and breaking in a puff of sixteen
squares.

## The bottles (`Items/GlassBottleKinds`, `Weapons/BottleShape`)

| Item | Console | Shape |
|---|---|---|
| `glass_bottle` "Glass Bottle" | `bottle` | the owner's Meshy model (a broad-shouldered spirit bottle), amber; welded and smoothed |
| `beer_bottle` "Beer Bottle" | `beer` | a longneck: brown glass, a cream label, a gold crown cap |
| `wine_bottle` "Wine Bottle" | `wine` | a claret bottle: dark green, high shoulders, a pale label, a red foil |
| `soda_bottle` "Soda Bottle" | `soda` | a returnable soda bottle: pale sea-green, a waist, a red crown cap |

`/equip` or `/spawn` any of them. All Common, one to a slot (a bottle carries its own state). Every one is 2.4 times a real bottle's size (the
owner's size for the first), as the hands are. Opaque glossy glass with the game's black line: see-through glass gets no line and reads as a
smudge in the cel look.

**A new bottle**: a line in `GlassBottleKinds.All` and a `BottleShape` (its profile as (radius, height) points of a real bottle standing on
its base, its glass, label and cap, where its body and neck end), then the scene rebuild and the title rebuild. Everything else follows.

## How it plays (`BottleWeapon`)

| | |
|---|---|
| Carried | by the neck in the right fist, the forearm out before the chest and the bottle standing up out of the thumb's side ("like the demoman") |
| Left-click | swung: the one-handed sword's own swings and heavy (`SoulsSwordCombat`), 15 base damage, blunt |
| It hits anything | a body (the swing's own hit) or a wall, the ground, any loose thing (the bottle is traced through the swing's hitting frames): it **breaks**. Its body bursts into its own pieces and what is left in the hand is the neck and half the bottle ending in sharp points |
| Broken | 22 base damage, "Pierce" on its card (hit types do nothing yet: `HitTypes`). Right-click is the guard, as any one-handed weapon's. After ten more landed blows it breaks down to the neck, which does nothing (the owner's old ruling: it stays in the hand, inert) |
| Whole, right-click held | the aim: the body turns side-on, the arm goes up and back with the bottle laid back; let go, it comes back to the carry |
| A click from the aim | thrown: the arm comes over, the bottle leaves the hand 0.2 s in, from where the hand is, toward where the crosshair pointed 20 m out, at 16 m/s, tumbling end over end. Clicked before the arm is back, it throws as soon as it is |
| In the air | it shatters **whole** on whatever it first touches. 18 damage to a body; a window it hits is taken out. Nothing is left to pick up |

Numbers are the Godot game's (15 / 22 / 18, ten blows, 16 m/s); the swings' own are the one-handed sword's.

## How it is made

- **Models** (`BottleBuilder`, `BottleMesh`, pure): the whole bottle turned from its profile (or the owner's model); the half and the neck
  are that same bottle cut off at a ring of eight uneven points (`BottleMesh.Fracture`: long beside short, the sides curved in a little, up
  to 8 cm long in the game), what is left of the label torn with it; the pieces are that same bottle again cut into 14 (the neck whole, the
  foot whole, the walls in shards: each the glass nearest one of a scatter of points). So a broken bottle is always the bottle it was.
  Everything is in the HELD frame: the fist's middle on the neck at the origin, the bottle out along +Y to its base.
- **The hold**: `SwordWeapon.FistPoint` and the one-handed sword's rotation: the neck across the palm, the bottle out past the thumb.
- **The carry** (`PlayerLocomotion.RaisedCarry.cs`, `IRaisedCarry`): the right arm, shoulder to wrist, held in the first frame of the
  bottle's moves while a bottle is in the hand; it gives way to a swing, a guard or the aim as they come in. The rest of the body walks,
  runs and stands as ever.
- **The moves** (`CityX/SourceArt/Bottle/bottle_moves.py`, built by `bottle_moves_build.py`; the punches' solver and planted feet): two
  full-body clips in `Character/Resources/Bottle/BottleMotion.json`: `bottle_ready` (the carry to the aim; its last frame held) and
  `bottle_throw` (a coil, over the top, the release, the arm on down across the body, standing). `BottlePoseDriver` plays them after the
  Animator; standing they are the whole body's, on the move the legs are the walk's. Keep `BottlePoseDriver.ReleaseSeconds` in step with
  the throw's RELEASE key. Rebuild:

      blender --background --factory-startup --python CityX/SourceArt/Bottle/bottle_moves_build.py -- --render <folder> --export CityX/Assets/CityX/Character/Resources/Bottle/BottleMotion.json

- **The shatter** (`BottleShatter`): the bottle's own pieces, each a body, put where the bottle was: thrown out from its middle, keeping
  some of its speed along what it hit, spinning, bouncing, lying about for five seconds and shrinking away; a spray of bright glints with
  them. Broken in the hand, only the pieces past the break fly.
- **Input** (`PlayerInputReader.TickSwungThrow`): a swung weapon that can also be thrown takes right-click for its aim while it can be
  thrown; otherwise the sword's input runs unchanged.

## Other players

`BottleThrower` (an `IReplicatedAbility`, "bottle", on every body: `PlayerAbilityWiring`): Aimed, ThrowStarted (their copy of the body plays
the same moves), Thrown (their copy of the bottle flies the same way and shatters where it lands, hurting no one) and Broke (their copy of
the bottle in the hand breaks, its pieces flying). The swings travel as every sword swing does.

## Tests

`GlassBottleTests` (every kind an item with its shape, three models, thrown bottle and pieces; the fracture's points; the half and the
pieces are the bottle; the hold), `BottleWeaponTests` (the three states), `GlassBottlePlayTests` (the carry; broken on a body and hitting
harder; broken on a wall; whole after a swing at the air; the aim, the release from the hand, the tumble, the hit and the whole shatter;
the aim let go; shattering on a wall; another player's copy). Pictures: `GlassBottleCapture` (`bottle-*.png`: the row, in the hand, the aim
and throw frame by frame, the shatter).

## Not done

- The grenade still leaves the hand the old way (no arm move): it could take this throw.
- A broken bottle swings as a whole one does (no stab of its own); hit types are only words on the card.
- No liquid, and no sound of its own for breaking in the hand (it uses the thrown bottle's).
- The melee break is told to other players but a bottle dropped and picked up by another keeps its state only through the item's data.
