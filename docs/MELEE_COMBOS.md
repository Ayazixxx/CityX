# Melee combos: what Dark Souls does, and ours

Owner, 2026-11-03: "lets make combos", "no i dont want to use the same animations as everyone else", "make the fists balanced right now they are too op and i dont like the animations either", "research dark souls pvp and the dark souls combos for 1 handed two handeded etc". Earlier: Dark Souls style fits the game best, with combos in it; guns and magic feel decent, melee does not.

## Dark Souls PvP: why a duel feels fair (research, 2026-11-03)

Community guides and the wikis agree on what a Souls duel is made of; none of it is secret, all of it is rules the player can read off the screen:

- **Spacing is the whole game.** You stand just outside the other's reach to bait a swing, and punish the miss ("whiff punishing"). Every attack has a wind-up and a recovery you cannot cancel, so a swing is a promise: throw it at nothing and you are open ([Steam PvP guide](https://steamcommunity.com/sharedfiles/filedetails/?id=2103327069), [part 2](https://steamcommunity.com/sharedfiles/filedetails/?id=2306866874)).
- **Stamina is the clock.** The pool is about 160 and refills at about 45 a second, so about 3.6 s from empty ([Fextralife, stamina](https://darksouls3.wiki.fextralife.com/Stamina)). A light attack costs less than a heavy; a heavy weapon runs dry after about three swings. The strong player waits until the other has spent theirs, then goes. Never swing on empty.
- **The roll is the dodge, and it can be caught.** It has invulnerable frames at its start and a recovery at its end; a panicked roll after a hit is punished by a delayed follow-up ("roll catching"): the straight sword's R1, R1, R2 works because the R2's delay matches a medium roll ([Fextralife, Longsword](https://darksouls3.wiki.fextralife.com/Longsword)).
- **Trades are decided by hyper armour.** Big weapons cannot be interrupted during part of their swing (the end of it), so a greatsword takes the small weapon's hit and lands its own; a light weapon's whole plan is to never be where that lands ([Fextralife, combat](https://darksouls3.wiki.fextralife.com/Combat), [Claymore](https://darksouls3.wiki.fextralife.com/Claymore)). Poise is the health of that armour; broken, you can be staggered mid-swing.
- **Parry, backstab, guard break, riposte.** A parry window of a few frames, then a free critical; a backstab from behind with a delay before it; a kick drains a shield's stamina and can break the guard. Dead-angling: hitting with the side of the arc so it cannot be parried, unlocked and stepping sideways.
- **Nothing reads your inputs.** Every tell is in the animation. That is why a loss feels like your own fault. (The owner's own rule for enemies, docs/SWORDSMAN.md.)

## The chains (Dark Souls 3, the moveset templates)

From the [moveset templates](http://darksouls3.wikidot.com/move-sets-template). Every class has a two-hit R1 chain, a two-hit R2 chain (the first R2 chargeable), a fast rolling attack and a running attack; the templates give no frame data.

| Class | One-handed R1 | Two-handed R1 | R2 | Rolling | Running |
|---|---|---|---|---|---|
| Straight sword | left slash, right slash | right-down slash, left-up slash | thrust, right upward slash (2H: left slash, right slash) | fast thrust | right slash (2H: downward) |
| Greatsword | left-down, right-down | left-down, right-up (hyper armour on every 2H attack) | down, right-down (2H: left, right) | fast downward | right-down |
| Fists | left-down strike, right strike | (none given) | strong strike, uppercut | fast right-up strike | right strike |

PvP strings: straight sword R1, R1, R2 (the R2 catches the roll); some weapons' R1, weapon art, R1 are true combos (the target cannot escape). Fist weapons: the shortest range in the game, so the whole build leans on stamina and being light; a two-punch combo is guaranteed, after it the other player escapes every time; great stagger and shield damage; the off-hand fist parries ([Fextralife, fist weapons](https://darksouls.wiki.fextralife.com/Fist+Weapons), [forum](https://steamcommunity.com/app/374320/discussions/0/361787186435643128)).

## Ours, measured against that

The rules we already have (`SoulsSwordCombat`, docs/SOULSLIKE_COMBAT.md): stamina per swing, a parry in the guard's first 0.2 s, the dash with i-frames, hyper armour on heavies, a guard break by holding, backstab crits. The sword's numbers: one-handed lights 8, 8.5 and 10 damage for 16, 16 and 18 stamina (hitstun 0.45 s), heavy 16 for 30; two-handed lights 10, 10.5, 13 for 22, 22, 26, heavy 20 for 40; the knife 5.6 for 11. A player has 100 health and 130 stamina, regained at 15.6 a second (8.3 s from empty: a slower clock than Souls' 3.6).

**The fists break every one of those rules** (`ComboChain.Unarmed`, `CombatTuning`, `MeleeAttacker`, from Godot's player.gd):

| | Fists now | The one-handed sword | Souls' fists |
|---|---|---|---|
| Damage a hit | 10 (haymaker 15) | 8 to 10 (heavy 16) | the lowest class |
| Hits a second | up to 4 (the last 35% of each cancels into the next) | about 2 | fast |
| Stamina a hit | **none** | 16 | low but paid |
| Reach | **2.5 m, a 120 degree cone** | 2.2 m | the shortest in the game |
| Hitstun a hit | 0.45 s, longer than the gap to the next punch: a chain is a lock | 0.45 s | two hits guaranteed, then you escape |

So bare hands out-damage a sword per hit, swing twice as fast, reach further, cost nothing and lock the target for the chain: 55 damage in 1.7 s, a player dead in under three seconds with nothing to answer. That is the "too op".

## The fist balance (BUILT 2026-11-03: `FIST_COMBAT.md`)

Souls' logic for fists: fast and cheap, but short and weak, and only two hits guaranteed. The chain became Souls' own two-hit string rather than the
four-punch plan below, so the numbers moved with it; the plan is kept for the reasoning:

| | Right straight | Left straight | Overhand (hold) | Uppercut (hold again) |
|---|---|---|---|---|
| Damage | 5 | 5 | 10, and the knockdown | 8 |
| Stamina | 8 | 8 | 16 | 12 |
| Hitstun | 0.33 s | 0.25 s | none (the knockdown) | 0.4 s |

- The string does 10 (one sword blow) and must then be recovered from; the overhand 10: against the sword chain's 26.5 plus a 16 heavy, and the fists
  must be a metre closer to land any of it.
- **Reach 1.4 m** (an arm and the step into the blow) in a **90 degree cone**: the sword's 2.2 m keeps you off.
- Two punches are guaranteed (the first's stun outlasts the gap to the second), the third is not: as in Souls (the test proves the timings).
- No stamina, no punch (as the sword).

## The fist animations (BUILT 2026-11-03, our own: `FIST_COMBAT.md`)

Owner: not store packs, and then "make the fists animations like dark souls unarmed punching". Not a boxer's: a brawler's. What Souls' bare hands look like, from the game and the [moveset templates](http://darksouls3.wikidot.com/move-sets-template):

- **No guard.** The body stands as it always stands, arms down, fists closed, a little crouched: the first pass's boxing guard (fists up at the face, `FistStrikes.GuardOffset`) goes.
- **Lights are alternating straight punches, right then left**, each a committed lunge: the fist drawn back to the hip in a short wind-up, a step forward with the rear foot as it goes out, the torso leaning well into it, the arm straight at full reach at the contact, a hold there, then a slow pull back to the sides. Two punches look alike mirrored; that is the point.
- **The heavy (hold) is the big one**: the fist wound back and high over the shoulder for a long moment (the tell), then brought down and across with the whole body; our knockdown finisher. Held again in the window: **the uppercut**, from a dip, driven up.
- **Weight and feet carry it.** The step into each punch is a real step (the stance's feet planted, one steps: `SwordFootwork`, which the sword already uses, with a square stance for the fists), the rear heel coming up as the weight goes forward. Each punch is a half second of commitment, not a snap, and the recovery is where you are open.

Built as planned (`FistStrikes`, the renders in `Logs/CityShots/fist-sheet*.png`): right straight, left straight (the string; the left closes it and is
recovered from in full), the overhand on a hold with the uppercut on a second hold, no guard (the ready stance has the arms down), and the legs step
in the stance with `SwordFootwork`. All procedural, so other players' copies show it with nothing new sent.

## Then: the sword combos

**One-handed chain built 2026-11-04 (`SOULSLIKE_COMBAT.md`): slash, backhand, diagonal finisher.** Still to do: the heavy's place in it, a steer at the nearest enemy, the two-handed sword.

The same treatment for the one-handed sword (3 lights into a heavy, full-body, feet planted, a roll-catching delay on the heavy), with a steer at the nearest enemy, then the two-handed sword, then one system for every melee weapon. Owner's picks: Dark Souls pacing with combos.

## The sledgehammer (2026-11-06)

Owner, 2026-11-05: "make a sledgehammer (two handed) and its still a 3 hit combo but the third one he smashes it straight down vertically. make a low effort model i will replace later", then "use hand ik for the grip".

- **A two-handed blunt weapon on the greatsword's moves** (`ItemRegistry.Sledgehammer`, `SwordWeapon` with `OneHanded` off and `Hammer` on). The same forehand and backhand, guard, dash and heavy slam, and the two-handed hold, the left hand set on the grip by hand IK as on every two-handed weapon.
- **Its third light is the overhead smash** (`SwordStrikeKind.Smash2H`, `SoulsSwordCombat.HammerLight3`, "sword2h_smash"): both hands straight up over the head, then down the middle onto whatever is in front, the hips sinking. A light, landing 0.34 s after the click, with a longer recovery than the diagonal; 1.45x damage, 0.75 s stun, 40 poise: my numbers. The greatsword keeps its diagonal.
- **The stand-in model** (`SledgehammerBuilder`, rebuilt with the scene): a turned wooden handle 1.4 m long and an octagonal steel head across its top, in the greatsword's frame. Replace the model with a real one and keep the frame: the handle down the root's -Y, the head's top `Reach` above it.
- Equipped with /equip sledgehammer (or sledge, hammer); Common, my placement (not on GAME_DESIGN's rarity list); its card says "Hammer".

Tests: `SledgehammerTests` (two-handed, blunt, a handle and a head, the third light the smash and the greatsword's still the diagonal, straight down the middle, listed and carded, built for other players' copies, the console names), `SwordStrikesTests` (every strike, the smash among them), `SoulsCombatRhythmTests` (the backhand holds the victim until the smash lands). Pictures: `SwordStrikeCapture` (`strike-hammer-*.png`, `grip-hammer.png`).
