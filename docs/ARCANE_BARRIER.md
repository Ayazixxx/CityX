# Arcane Barrier

Owner, 2026-10-27: "make a spell called "arcane barrier" and it works like bartolomeo from one piece's devil fruit ability and i kind of want
it to function like the version of it in the game one piece burning blood with the barrier ability. appears as a large square in front of you
that functions as a shield as has some amount of health until it breaks. ours is a magenta wall of aura as opposed to the glass barrier look of
the one piece version. im thinking at first you can deploy it and you have to stand still with it". Then: "i dont want the arcane barrier to
shatter make it fade away, and make sure its transparent enough to see through".

## What was built (2026-10-28; the first version: stand still behind it)

| | |
|---|---|
| Where | The Arcanist tree, beside Arcane Orb at its root (`SkillCatalog.ArcaneBarrier`, `arcane-barrier`). Learnt, it goes on the bar like any spell; it ranks up for ever |
| The key | Pressed, the wall rises before the body, the body turned to where the camera looks. Held, it stays: the caster stands still behind it and does nothing else (`PlayerInputReader` holds the body, as Self Smite does; the camera still turns). Let go, it fades |
| The wall | `ArcaneBarrierWall`, drawn in code: a 3.2 m square (a good deal taller than the 2.6 m body) 1.05 m before the feet, its bottom a little into the ground. A faint magenta fill you see the street through (`ArcaneVisuals.WallAlpha`: 0.2 in the middle, additive), brightening to a thin bright rim; soft bands drifting up its face; motes rising off its bottom edge; a soft light; a low hum. A child of the body, so Grow and Shrink size it with the body |
| The arms | Both pushed out before the chest, hands close together, palms out against the wall, fingers up (`PlayerLocomotion.Barrier.cs`, two-bone IK after the other arm poses) |
| What it stops | Whatever comes at its face (`ArcaneBarrierRules.Covers`: from in front of it, the line to the caster's chest crossing the wall or passing within 35 cm of its edge): bullets, spells, blades, blasts. It takes the blow off its own health; the caster takes nothing, is not staggered, and a blade bounces off it as off a held guard (`HitResponse.Guarded`). Its collider stops a shot or a ball physically, and whatever strikes it goes to the caster's body (`PlayerVitals.Barrier`, asked first). From behind, beside or over it, it stops nothing. An Arcane Orb ball on it hurts the wall and leaves no cursed orbs |
| Struck | A ring spreads where it was hit and the face flares; a soft thud (no glass). As its health runs out it dims (to 55%) and flickers |
| Broken | It flares once as it gives out, then **fades away** where it stood over a second, swelling a little, its motes flung off, the chord sinking. It never shatters: no pieces, no glass. The collider goes at once |
| Numbers (mine) | Health 250 at rank 1 (an AK takes most of a second to burn through it; two and a half players), +10% each rank. 25 mana to raise, 10 a second to hold (mana comes back at 4 a second, so a full bar holds it about 12 s). It drops when the mana runs out. 3 s before it can rise again after letting go, 8 s after it breaks |
| Other players | See it rise before the caster, the arms out, each blow on it and its fade (`IReplicatedAbility` events `Raised`, `Struck` with the health left, `Lowered`, `Broken`). Their copy of the wall has its collider too, so their shots stop on it on their own machine; the blow goes to the caster's machine, which takes it off the wall's health. Their blade bounces off it on their own machine too (the copy's `PlayerVitals.Barrier` answers whether it covers) |

## The variations (my suggestion, waiting for the owner)

Ranks only make it stronger (health). New behaviours as their own stars further up the Arcanist tree, so you choose what kind of barrier user
you become: **Barrier Walk** (move slowly while holding it), **Barrier Crash** (click to ram it forward, damage by the health it has left), and
**Arcane Wall** (a separate spell: plant it, it stands about 10 s, switch weapons freely).

## Tests and pictures

`ArcaneBarrierTests` (what its face covers and doesn't, where a blow shows, health by rank, mana, the face see-through with a bright rim) and
`ArcaneBarrierPlayTests` (raised before the body turned to the camera, the arms out, held still; a shot on its face stops on the wall and hurts
only it, a blade bounces off; from behind it stops nothing; broken it fades where it stood and never comes apart; let go it fades quickly, unheld
it goes; out of mana it drops; a copy sees it rise with its collider, take each blow and fade; an arcane ball on it leaves no orbs).
`ArcaneBarrierCapture` (Explicit, `-Graphics`) writes `Logs/CityShots/barrier-*.png`: the caster's own view with someone seen through it, the
front, the side, at night, struck, and fading.
