# Fire Enhancement

## 2026-10-29: built to the owner's ask

> "after that i want to make the first "enhance weapon" skill starting with the fire enhancement, it just makes your next swing deal fire
> damage, same with your next bullet, next arrow, etc. and for no we'll just make it have no effect on staffs"
>
> "and sets your sword ablaze too forgot to mention that"

The first of the "enchant spells that give your next hit, or your next bullet, an element" in docs/GAME_DESIGN.md (Staffs, magic weapons
and enchants). A Pyromancer spell, next to Fireball in the skill tree; put it on the bar from the skill menu.

## What it does (`FireEnhancementAbility`, numbers in `FireEnhancementRules`)

- **Cast** (15 mana): the fire waits on your weapon for its attack, up to 30 s, then goes out. It does not stack (cast again while it
  waits: nothing); 1 s from one cast to the next.
- **The next attack takes it**:
  - **a swing** (fists, the bat, the bottle, a sword's cut or heavy, the sword spin): every blow that swing lands burns. A held heavy keeps
    it until its blow. A swing that misses spends it too: it was the next swing.
  - **a bullet** (the pistol, the AK-47, the crossbow): that shot burns what it hits; a burning shot that meets anything flares there and
    lights gasoline.
  - **an arrow** (the bow): it flies burning, burns what it hits and lights gasoline where it lands; stuck, it burns on a few seconds.
  - **a staff**: nothing. The fire neither shows on a staff nor goes to its attack; it waits for a weapon that is not a staff.
- **What a burning blow does**: the blow's own damage, then 15 fire damage (a spell's: Spell Power raises it; no stagger, no blood), and
  the target is set alight for 5 s (`FireScene.SetAlight`: the flames on the torso and the afterburn's 4 a second that burning gasoline
  leaves, put out by water as any burning body is).
- **The weapon ablaze** (`WeaponAblaze`): while the fire waits, and through the swing that takes it, flames lick up all along a sword's
  blade (round the hand for anything else) with embers and a flickering orange light. They are the fire's own tongues (`FireLook.Lick`, the
  CityX/Fire shader) left in the world, so a swing leaves a trail of fire behind the blade.

## Other players (docs/MULTIPLAYER_STATE.md)

An ability event (`IReplicatedAbility`, "fire-enhancement"): **Lit** (the weapon of their copy of you burns), **Out**, and **Burned**
(the body nearest the point sent is set alight on their screen, for show: the burning's damage is dealt on your machine and reaches that
body through its copy, as every blow does). A bow's burning arrow flies burning on their screens too (`Archery`, N = 1).

## Tests

`FireEnhancementRulesTests` (the numbers, one shot takes it, a staff never does, it goes out), `FireEnhancementPlayTests` (the sword
ablaze along its blade, its cut burning the dummy and then spent, the next bullet, the staff, another player's copy); pictures
`FireEnhancementCapture` (`fire-enhance-*.png`).

## Not yet

- The other elements (ice, lightning, poison...), and what each does to a body.
- Staffs (the owner: "for now").
