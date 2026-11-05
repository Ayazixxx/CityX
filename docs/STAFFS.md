# Magic staffs

The owner's idea (GAME_DESIGN.md, Staffs, magic weapons and enchants): a hotbar spell is one cast; a staff's attack is the spell on the click,
every shot paying mana. Built 2026-10-27, three of them, as the owner asked one after another:

- **Arcane Staff**: "if you click it shots a ball that does 1 cursed orb upon landing, but you can holding down with the staff to do a charged shot
  that goes up from 1 to 2 to 3 orbs ... remember you dont get infinite shots with the staff it costs mana still" (docs/ARCANE_ORB.md).
- **Fireball Staff**: "when you hold click with that one it does a charged shot the explodes into a bigger ball of fire the longer you charge it,
  come up with a reasonable maximum the how long you can charge it for."
- **Ice Shard Staff**: "when you click it shoots 1 iceshard but when you hold click it just shoots consecutively like an automatic gun", and
  "dont make it too OP".
- The models: "just have it be a basic model for now il get to it when i can make more meshy models."

## What every staff shares

| | |
|---|---|
| The item | `StaffItem` (Weapons/Spells), a primary-slot weapon: `/equip staff`, `/equip firestaff`, `/equip icestaff` |
| The hold | in the right hand, its shaft through the fist past the thumb (where a one-handed sword's handle runs), the fingers closed on it. Carried upright at the side, the forearm forward; while it charges or shoots, the arm thrusts it out before the chest, its head leaning 25 degrees forward, and the body turns to the crosshair as with a gun (`PlayerLocomotion.Staff.cs`, two-bone IK and the hand turned to stand the shaft) |
| The click | left-click is the staff's (right-click stays the shield's). What a click and a hold do is the spell's, on the body (`IStaffCaster`: `ArcaneOrbCaster`, `FireballCaster`, `IceShardCaster`), so every shot reaches other players as that spell's does. A charge (`StaffCharge`) starts on the press, grows while held, and the shot leaves 8 frames (0.13 s) after the release, as Shadow Flare's does, the staff up by then |
| The aim | from the staff's head to what the crosshair is ON: the first thing the camera's line meets past the body (`PlayerInputReader.CrosshairDirection`), every staff alike. Aimed at a point 15 m ahead of the camera instead, as at first, a shot hit beside anything nearer or farther, the camera sitting up and off to the right of the hand (owner, 2026-10-29: "the beam staff was hitting slightly to the side of the crosshair"). The Healing staff picks the ally nearest the crosshair instead |
| The charge's look | a glow at the staff's head swelling with the charge, and a light (`StaffChargeGlow`), on every player's screen |
| Mana | every shot pays; a charge stops growing at what the mana pays for, and one cannot start without enough for the smallest shot. Tab lock-on is off with a staff, as with a gun |
| The models | `StaffBuilder`, drawn in code (turned shapes): a 2.4 m shaft with a ferrule, a leather wrap at the grip, a cup at the head holding the crystal, glowing in the spell's colour. Arcane: dark purple wood, brass, a magenta orb. Fireball: dark red wood, an iron brazier, an orange ember. Ice Shard: pale wood, silver, a faceted shard of ice. Water: sea-green wood, steel, a pale blue orb |

## Each staff

| | Click | Hold | Mana | Pace |
|---|---|---|---|---|
| Arcane | a ball that leaves 1 cursed orb | 2 orbs from 0.28 s, 3 from 0.43 s (Shadow Flare's frames) | 8 an orb | 0.63 s between shots |
| Fireball | a plain fireball (25) | grows for up to 1.5 s (about a Mega Man charged shot): up to 2.5 times the ball and its damage (62), its burst burning everyone within up to 4 m (all of the hit's damage at the middle, 30% at the edge) | 10, up to 30 full | 0.5 s (the bar's fireball waits 1.5 s) |
| Ice Shard | one shard | one every 0.25 s for as long as it is held, wandering up to 1.5 degrees | 4 a shard | about 32 damage a second, eight seconds from a full bar: well under a gun's |
| Water | the jet, while held | a jet of water (16 m, GTA V's fire truck hose): whoever it is held on is thrown limp along it, pushed on along the ground while down, and hurt 3 every 0.3 s | 6 a second | about 10 damage a second; the push is the point |
| Flame | the fire, while held | the flamethrower's stream of fire (9 m, a 10 degree cone): everyone in it burns 36 a second (a tick every 0.15 s), no stagger, no blood; half as fierce and a quarter shorter out in the rain | 8 a second | a flamethrower that pays mana instead of fuel |
| Arcane Beam | the beam, while held | a straight magenta beam (24 m) to the first thing on the crosshair's line: that burns 5 every 0.2 s (25 a second, magic), nothing beside it or behind it; a corpse still with parts to lose is taken apart by it | 10 a second | the long reach of the staffs, one target at a time |
| Healing | the beam, while held | Mercy's Caduceus Staff without the damage boost: a gold beam ties itself to the other player nearest the crosshair (within 20 m and 15 degrees) and heals them 3 every 0.2 s (15 a second); it stays on them while the healer looks away, until let go, past 21 m, or out of sight for 1.3 s; never the healer, never an enemy | 5 a second while tied | docs/HEALING.md |

All the numbers but Shadow Flare's frames are mine. The staffs ignore the hotbar spells' ranks (the spell need not be learnt).

**The held staffs on an empty bar, and tapped** (Water, Flame, Frost, Arcane Beam, Healing; 2026-11-05). A held staff that runs out of mana
stays off until the mana to hold it for a second is back (`StreamFeed.RestartSeconds`, mine: 6 mana for the water staff, a second and a half
of waiting), whether the click is kept down or pressed again; then it runs in one stretch until it is dry again. And a body's next tick
keeps its beat through a release (`StreamTicks` for the jet and the fire, whose ticks are each body's own; `SpellTicks.Rest` for the
beams and the frost, which have one clock). Before, mana trickling back started the stream again every few frames and every start landed
a tick at once, so an empty staff hit several times as hard as a full one, harder the higher the frame rate; tapping the click did the same
on a full bar for a frame's mana a tick. `HeldStreamTests` (EditMode), `HeldStreamManaPlayTests` (the five staffs on the real body).

## Other players

Each staff's model is held by a copy (`ICosmeticMountable`, via `NetworkedEquipmentSync`). Its shots, charge and thrust are its spell's
events (`IReplicatedAbility`): `ArcaneOrbCaster` (`BallFired` with B 1, `ChargeStarted`, `ChargeStopped`), `FireballCaster` (kind 0 with its size and
N 1, `StaffChargeStarted`, `StaffChargeStopped`), `IceShardCaster` (`StaffShardFired`). A copy holds the staff out while it shows them.

## Not yet

- The owner's own models (Meshy, after 2026-10-31).
- Staffs boosting their element's spells (GAME_DESIGN.md: the owner's idea, not built). The Magic Wand and the Magic Staff, with no element, are docs/MAGIC_WAND.md.
- A staff's melee (a bonk) or right-click.

## Tests

`StaffTests` (EditMode: the charge's timing; the fireball's charge, mana and size), `StaffPlayTests` (each staff equipped in the right hand and seen
on a copy; the arcane click 1 orb and the hold 2 and 3; the fireball's charge grows the ball and its blast; the ice staff's hold keeps shooting at its
pace; mana paid and refused), the `EquipmentReplicationGuardTests`. `StaffCapture` (Explicit, -Graphics) renders `Logs/CityShots/staff-*.png`.

## The water staff (2026-10-28)

Owner: "make an item just called "water staff" for now. Have it function like the hose at the top of firetrucks in gta 5 do. it just shoots
water and pushes and ragdolls whoever is the target of it. our will also do a bit of damage not much though just like 3 damage consecutively as
you are spraying". `water_staff`, `/equip waterstaff` (or `hose`).

- **The jet** (`WaterJetCaster`, `WaterJetRules`): held, a jet from the staff's head along the crosshair, 16 m, half a metre wide (a hose, not a
  cone). It drinks 6 mana a second and stops when the mana is gone, or the click is let go.
- **Whoever is in it** is thrown limp along it (8 m/s, lifted 3 m/s: a knockdown, up again after a moment as a finisher's is), pushed on along
  the ground at 25 m/s² while down, shoved at 6 m/s if they cannot be thrown (just back up, unhurtable for the moment), and hurt 3 every 0.3 s
  (10 a second, a spell: Spell Power and Armor Penetration apply, magic resistance too), with no blood (`DamageContext.WithoutBlood`: water
  does not cut). Civilians are only hurt: their ragdoll has no getting
  up. A knocked-down player is thrown on their own machine too (the knockdown relay, as every knockdown).
- **The look** (`WaterJet`, `WaterSounds`), drawn in code after GTA V's cannon (owner: "try to make it look like the water from the hose on
  the firetruck in gta"; then "less of a beam and more of a wider splash ... there will be other beam attacks i make later"): a short solid
  white core at the nozzle, a tube rebuilt every frame along a sagging arc (30% of gravity) with foam streaks scrolling down it, gone by 40% of
  the reach; from there the water is a widening cone of white drops shed with the flow (900 a second, fanning out the further they have
  flown) and a mist of soft puffs off the arc's second half; and where it lands, drops thrown out and up and a cloud of mist rolling off the
  ground. Then a burst of
  spray wherever it lands, the hiss of it. The staff is thrust out as with every
  staff.
- **Other players** see the jet on the wearer's copy of the staff, pointed where they point it (told ten times a second), stopping with theirs.
- Tests: `WaterJetTests`, `WaterStaffPlayTests`; `WaterStaffCapture` renders `Logs/CityShots/water-staff-*.png`.

## The flame staff (2026-10-28)

Owner: "make a fire staff that functions like a flamethrower but costs mana". `flame_staff`, `/equip flamestaff` (or `flame`, `torch`); called
the Flame Staff because "fire staff" was already the Fireball Staff's name in the console.

- **The stream** (`FlameStreamCaster`, `FlameStreamRules`): held, the flamethrower's stream of fire from the staff's head along the crosshair, 9 m
  (the flamethrower's 7: a staff throws it a little further) in a 10 degree cone. Everyone in it burns 36 a second, a tick every 0.15 s for as long as it is held
  on them (a spell: Spell Power and Armor Penetration apply, magic resistance stops a share), with no stagger, as the flamethrower (a stream
  that locked its target every frame would never let it move), and no blood (`DamageContext.WithoutBlood`: fire does not cut). It drinks 8 mana a second and goes out when the mana is gone, or the click is let go.
  Out in the rain it is half as fierce and a quarter shorter, like the flamethrower (`Weather.FlameStrength`); under a roof it is unaffected.
- **It sets the ground alight** (docs/FIRE.md, since the fire mechanics): where the stream meets the world bare pavement burns a few seconds,
  and any gasoline under its length catches and burns along (`FireScene.IgniteAlongStream`, ten times a second).
- **The look** (`FlameStreamLook`): the flamethrower's own three-layer fire (a hot core, the orange flame round it, smoke off it) and its
  flickering light, on a prefab of its own (`FlamethrowerBuilder.BuildStaffFlame`, `StaffFlame.prefab`) that the caster puts at the staff's head
  and points along the crosshair. The staff is thrust out as with every staff. The model: charred black wood, iron, an ember in a brazier.
- **Other players** see the fire on the wearer's copy of the staff, pointed where they point it (told ten times a second), going out with theirs.
- Tests: `FlameStreamTests`, `FlameStaffPlayTests`; `FlameStaffCapture` renders `Logs/CityShots/flame-staff-*.png`.

## The Arcane Beam Staff (2026-10-29)

Owner: "make a staff called "arcane beam staff" that has a beam like attack similar to how the water staff looked at the beginning. its just a
magenta arcane beam that consecutively does damage to whatever its targeting and costs mana". `arcane_beam_staff`, `/equip beamstaff` (or
`arcanebeam`, `beam`). The water staff's first look (a solid beam, before the owner asked for the spray) was never committed, so the beam is
built new in the same spirit: a beam of light, not a jet.

- **The beam** (`ArcaneBeamCaster`, `ArcaneBeamRules`): held, a straight beam from the staff's head along the crosshair to the first thing it
  meets (a 15 cm sphere cast, 24 m): that, if it can be hurt, takes 5 magic damage every 0.2 s (25 a second; Spell Power, Armor Penetration and
  magic resistance apply), and nothing beside it or behind it does. No stagger. A corpse with a part still to lose is struck too
  (docs/DISMEMBERMENT.md). It drinks 10 mana a second and stops when the mana is gone or the click is let go. The ticks count the frames' own
  time (`SpellTicks`), so they land the same however the frames fall.
- **The look** (`StaffBeam`, its arcane style): two line renderers (a white-hot core and a wide soft magenta glow, both painted with bands of
  light that run down the beam and a quick pulse in their width), a flare at the staff's head, and where it lands a flare, sparks and a magenta
  light; the arcane barrier's hum, played higher. The model: a magenta crystal shard in a silver cup on dark wood.
- **Other players** see the beam on the wearer's copy of the staff, pointed where they point it (told ten times a second), running on their
  screen to what it meets there, and stopping with theirs.
- Tests: `BeamStaffRulesTests`, `ArcaneBeamStaffPlayTests`; `ArcaneBeamCapture` renders `Logs/CityShots/arcane-beam-*.png`.

## The Staff of Healing (2026-10-29)

Owner: "create staff of healing that functions like mercy's staff from overwatch but without the damage boost part". `healing_staff`,
`/equip healstaff` (or `healing`, `heal`). docs/HEALING.md has the beam's rules; the look is `StaffBeam`'s healing style (gold, narrower,
slow); the model, white wood, gold and a warm yellow orb.
