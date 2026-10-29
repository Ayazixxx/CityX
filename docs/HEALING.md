# Healing: the Staff of Healing and the Healing Aura

Owner, 2026-10-29: "create staff of healing that functions like mercy's staff from overwatch but without the damage boost part"; then "add a
healing spell that makes a circle on the floor around you that moves with you and heals yourself and others that go in the circle slowly"
and "make the healing aura green".

## What was looked at

Mercy's Caduceus Staff (Overwatch 2; the numbers move between patches): held, it ties a beam to the ally nearest the crosshair (distance and
facing both weigh in), 15 m (and about half a metre for hurtboxes); it heals 55 a second at Overwatch 2's launch, 45 since Season 3 (2023;
67.5 on allies under half health); the beam stays on the ally while Mercy looks elsewhere, and holds about 1.3 s out of sight. Its secondary
fire (the damage boost) and her passive self-heal are left out. Sources: PC Gamer and TechRadar on the Season 3 change, a Workshop range test,
a fan wiki on the out-of-sight grace, Blizzard's hero page.

## Healing a body

`PlayerVitals.Heal`: up to its whole health, nothing when dead. On another player's copy the heal goes to the owner's body instead
(`NetworkedDamage.TryRelayHeal`: the server checks it as it checks a blow, no more than 40 a second from one sender and the healer's body
near), so the owner's own vitals heal and everyone sees it in the health they share.

## The Staff of Healing

`healing_staff` (`HealingStaff`, `HealingBeamCaster`, `HealingBeamRules`; docs/STAFFS.md for what every staff shares).

| | |
|---|---|
| Who | the other living player nearest the crosshair: within 20 m (Mercy's 15 on a body 1.3 times life size) and 15 degrees of it, in sight. Never the healer, never an enemy or a body that is no player's (`PlayerBodies.IsPlayer`) |
| Holding on | while the click is held, however the healer looks; it lets go past 21 m, after 1.3 s out of sight, or when they die. Let go, the next hold picks again |
| How much | 3 every 0.2 s: 15 a second before Spell Power (Mercy's share of a bar trimmed for this game's lower damage) |
| Mana | 5 a second while it is on someone (Mercy's is free; every staff here pays a little) |
| The look | `StaffBeam`'s healing style: a gold beam from the staff's head to their chest, a white-gold core, slow bands, a soft drone; gold motes and a warm light where it lands |
| Other players | told whom (as where their chest is, ten times a second; each copy ties its beam to the body nearest) and when it lets go |

## The Healing Aura

The Cleric's first spell (a new tree, `SkillCatalog.HealingAura`; on the bar from the skill menu). `HealingAuraAbility`, `HealingAuraRules`,
`HealingAuraCircle`.

| | |
|---|---|
| Cast | a green circle 4.5 m round is laid on the ground under the caster (found below their feet each frame) and goes where they go, for 12 s; then 30 s from the cast before it can be cast again |
| Heals | every 0.5 s, the caster and every living player whose feet stand in it (within 3 m above or below) 2.5: 5 a second, slowly, before Spell Power. A giant's circle is a giant's size |
| The look | drawn in code: a bright ring at the rim (at the radius; the glow fades out inside the square it is painted on, `RimShare`, or its cut edges showed) with a glow falling inward and a faint fill, an inner ring of eight marks turning slowly, green motes rising out of it, a soft green light; it grows in and fades out |
| Other players | told it started (and how long it lasts) and when it ended; their copy shows the circle and heals nobody (the caster's machine heals) |

## Tests

`BeamStaffRulesTests` (whom the beam ties to, when it lets go), `HealingStaffPlayTests` (a wounded player under the crosshair healed a second's
worth, a body that is no player's not; the beam staying on while the healer looks away and letting go past its reach; out of sight a moment
then letting go; a copy's beam tied to the same body), `HealingAuraRulesTests`, `HealingAuraPlayTests` (the circle on the ground following the
caster; the caster and a player in it healed about 5 a second, one outside not; its time and cooldown; a copy's circle healing nobody).
Pictures: `HealingCapture` (`Logs/CityShots/healing-*.png`).

## Not yet

Healing numbers floating up; a sound for the aura; the aura on non-player allies (civilians); Mercy's damage boost (left out on purpose).
