# The Magic Wand and the Magic Staff

Owner, 2026-10-30: "make a magic wand maybe look at hogward legacy or something for inspiration though it doesnt really need to do much. its
just a little stick you hold that when you use hotbar spells will be casted from the wand. im thinking this will be a basic non elemental magic
wand that just gives a slight boost to spells casted while holding it, maybe 5% more damage 10% less mana usage. clicking with it will do some
basic projectile attack like it just shoots a little speck of dust at a low firerate like 1 per second. there will probably need to be a basic
non elemental magic staff as well that does the same spec of dust attack but maybe it does slightly more damage at a slightly lower firerate";
then "call them "Magic Wand" and "Magic Staff"". `magic_wand` (`/equip wand`), `magic_staff` (`/equip magicstaff`).

## The wand (`MagicWand`, `PlayerLocomotion.Wand.cs`)

| | |
|---|---|
| The hold | Hogwarts Legacy's: in the right hand, across the fist on a slant (50 degrees from the thumb's line toward the knuckles) as a pen is held (`MagicWand.HeldRotation`). Held straight out along the thumb as a staff is, a wand pointed down a straight arm needed the wrist bent square, and the hand spun about the arm at random (owner, 2026-11-01, with a photo of their own arm pointing a marker: "the arm is twisted all weird when casting", "you gotta rotate the hand"); now pointed, the arm is straight (0.995 of its length, a few degrees outside and under the aim), the back of the hand up and the wrist bent only by what is left over (`TurnWandHand`). Carried, the arm hangs relaxed at the side and the wand points ahead and a little down, the wrist barely bent (2026-11-01: the old carry put the hand before the hip, the elbow out and back and the wand at the floor: "like his arm is bent the wrong way"). To shoot or cast, the arm comes straight out from the shoulder at what the crosshair is on, the wand running on along it, the body turning to face it (the owner, 2026-11-01, with Hogwarts Legacy's casting: "just have him extend his arm out toward whatever he is aiming at"; `PlayerLocomotion.Wand.cs`) |
| Spells through it | while it is held, the Fireball, Ice Shards and Arcane Orb leave its **tip**, aimed at what the crosshair is on (`PlayerInputReader.SpellLaunch`); the wand snaps out with the cast (`PointWandNow` poses the arm at once, so the spell starts where the frame shows the tip). Poison Breath still comes from the mouth, Acid Rain and Self Smite from the sky, the rest from the body |
| The boost | it is the body's spell focus while held (`ISpellFocus`, `PlayerVitals.SpellFocus`): a spell cast then hurts (or heals) **5% more** (2.5 Spell Power points on top of the stat's, `PlayerVitals.SpellPower`) and pays **10% less mana** (`PlayerVitals.TrySpendMana`); put away, nothing |
| The click | a speck of dust: one at once, then one a second while held; a click a moment early shoots as soon as it can |
| The model | a stand-in drawn in code (`MagicWandBuilder`) until the owner's: a dark carved handle (a round pommel, a ring, the grip swelling a little, a ring under the thumb), a bronze collar, a lighter shaft tapering to a point; 46 cm (a real wand's 35 at the game's 1.3x) |

## The staff (`MagicStaff`)

Every staff's hold and model (`StaffBuilder.BuildMagic`: light oak, bronze, a pale gold orb). Its click is the same speck from its head, a little
harder and a little slower. It boosts no spell (the owner asked that of the wand only).

## The speck (`MagicSpeckCaster`, `MagicSpeck`, `MagicSpeckRules`)

| | Wand | Staff |
|---|---|---|
| Damage | 10 (a punch's) | 13 |
| Pace | 1 s | 1.25 s |
| A second | 10 | 10.4 |
| Size | 1x | 1.35x |

- Free: no mana. A basic attack (Hogwarts Legacy's basic cast is free too), so it is what is left when the mana runs out.
- A white spark in a soft white glow (gold at first; owner: "make the speck the magic wand and magic staff shoot white instead of yellow")
  with a short streak and a few motes, flying true at 45 m/s for 1.4 s (63 m), then fizzling out.
- Magic: Spell Power (the stat; the wand's own boost is for spells), Armor Penetration and magic resistance apply. A 0.15 s flinch, no stagger,
  no blood (dust does not cut); a sparkle where it lands, a glassy flick as it leaves.
- The numbers are mine.

The white glows, streaks, motes and bursts are the arcane spells' code in another colour (`GlowPalette`, `ArcaneVisuals`, `ArcaneBurst`).

## Mana (docs/SKILLS.md)

Every spell pays mana; the Fireball 10 since 2026-10-30. Through the wand: 9.

## Other players (docs/MULTIPLAYER_STATE.md)

The wand and the staff are `ICosmeticMountable` (`StaffItem`): a copy holds the model, the wand carried or pointed as its owner's. Each speck is
`MagicSpeckCaster`'s event (`IReplicatedAbility`, kind 0 the wand's, 1 the staff's): the copy shows it flying from where it left, hurting nobody,
the wand pointed or the staff thrust out. A spell cast through the wand is the spell's own event, from the tip.

## Tests

`MagicWandRulesTests` (EditMode: the paces and damage, the boost and the mana), `MagicWandPlayTests` (the wand held, pointed and the focus, and on
a copy; held, a speck from its tip once a second landing on what the crosshair is on; the staff's harder and slower; a fireball with the wand in
hand 5% harder for 9 mana; a copy showing a speck with its wand pointed), `MagicWandRealInputPlayTests` (key 1 in the real scene: the fireball
from the wand's tip, for less mana), the replication guard tests. `MagicWandCapture` (Explicit, -Graphics) renders `Logs/CityShots/magic-*.png`.
