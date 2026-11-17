# The knife

Owner, 2026-10-29: "how should knife mechanics work. i feel like it should be quicker than a one handed sword"; on the reach, "i dont
think the reach should be that much shorter than the 1 handed sword though"; "backstabs would be a cool critical hit thing"; settled on
"i think knives should do less damage on average, maybe have like 10% less reach and be around 30% faster swing speed"; then "make a low
effort kitchen knife model but make the mechanics good, when i get my meshy credits i will replace it with a good model".

## What games do (the research, 2026-10-29)

| Game | Knife against sword |
|---|---|
| Skyrim ([UESP](https://en.uesp.net/wiki/Skyrim:Weapons), [sneak](https://en.uesp.net/wiki/Skyrim:Sneak)) | daggers reach 0.7, speed 1.3, **stagger 0** (a sword's 1, 1, 0.75); damage 55-70% of a sword's (Iron 4 vs 7, Ebony 9 vs 13). Sneak attacks 3x for any one-handed weapon (6x with a perk), daggers up to **15x** (Assassin's Blade) |
| Elden Ring ([daggers](https://eldenring.wiki.fextralife.com/Daggers), [criticals](https://eldenring.wiki.gg/wiki/Critical_Attack)) | very fast, very short, little poise damage, guard boost 15 (poor); **critical 130** (Misericorde 140) against most straight swords' 100-110 |
| Dark Souls 3 ([dagger](https://darksouls3.wiki.fextralife.com/Dagger), [long sword](https://darksouls3.wiki.fextralife.com/Long+sword)) | the dagger's backstab about **twice** a long sword's (390 against 200); its skill a quick dodge; a straight sword trades through a dagger's lights |

Every one of them: quicker, lighter blows, little or no stagger, a weak guard, and the reward in the critical hit from behind.

## What it is (`KnifeRules`, `Backstab`)

| | One-handed sword | Knife |
|---|---|---|
| Reach | 2.2 m (`SoulsSwordCombat.ReachMetres`) | 90%: 1.98 m |
| Swing speed | 1x | **1.3x** -- wind-up, cut, recovery and lunge (`KnifeRules.SwingSpeed`, on top of Melee Speed) |
| Light blows | 9.6 and 10.2 (12 base) | 7.2 and 7.7 (9 base; 7 until the owner raised it on 2026-11-15, and the sword to 12 with it: "raise the sword damage to fill that 17% damage per second gap", "round up to 12 damage per hit"): **97.5%** of the sword's damage a second |
| Light stagger | 0.45 s | **none**: a knife's light does not stop what its target is doing, so it cannot trade blows with a sword (the sword's stuns until its next cut lands: `SoulsCombatRhythmTests`) and barely shoves (the shove follows the stagger) |
| Light stamina | 16 | 11 |
| Heavy (hold the click) | the overhead cut | **the icepick stab** (below), guard-breaking, 11 damage |
| Guard | 1 stamina a point of damage | **1.6**: a thin blade breaks sooner |
| Backstab | **1.5x** | **3x** |

**The backstab** is every one-handed blade's (the sword, the crowbar, the bat a little; the knife most): a blow on a living body from its
back third (within 60 degrees of straight behind it) is a critical hit -- the multiplied damage, a stagger of at least 0.6 s even from a
knife's light, twice the hit-stop, and a **gold** damage number a third bigger (`DamageContext.Critically`, `DamageDealt.Critical`). Face to
face it never happens: it takes a dash round someone, or catching them busy with someone else. The two-handed weapons have none.

The chain is the one-handed sword's first two cuts, back and forth: the flat slash and the backhand (`SwordStrikes`), the knife's run faster. The kitchen
knife is edged, so an overkill blow cuts the part it hits off (docs/DISMEMBERMENT.md).

## The heavy: the icepick stab (2026-11-15)

Owner: "make it so when you hold click with the knife, it does a downward stab motion holding the knife upside down like the image attached"
(a masked killer, knife raised overhead point down), then "make the flip to upside down quick and natural research if there are any game dev
tricks for doings this".

**What games do with a flip into a reverse grip** (the research; thin, it is rarely written up): Counter-Strike's knives spin over in the hand
as they are drawn and inspected, a flick rather than a turn ([tradeit.gg](https://tradeit.gg/blog/best-cs2-knife-animations/)); engines change a
held thing's grip by swapping the hold it is attached by rather than animating the hand round it, the animation hiding the swap
([Roblox devforum](https://devforum.roblox.com/t/how-do-i-change-the-grip-of-a-tool-while-its-animating/932545)). What was used from that:

- **The spin happens IN the fist** (`SwordWeapon.HoldReversed`): half a turn about the handle's middle in the fist, over the knuckles (the
  blade passes out in front of the fingers, never back through the wrist), so the fist keeps hold of the same stretch of handle and the
  blade, out past the thumb in the hold, comes out past the little finger. The edge ends facing the forearm (an edge-in reverse grip).
- **The hand is not turned to undo it**: the stab's keys are written for the knife's own hold (`SwordStrikes.Upturned`), and the pose turns
  the hand for that hold whatever the spin (`PlayerLocomotion.ApplySwordPose` takes `ISwordBlade.GripFlipInHand` off), so the spin shows.
- **Hidden in the raise**: it starts 0.08 s into the wind-up, while the arm is snatched up (the stab takes the body over in 60% of its
  wind-up, `SwordStrikes.TakeoverSeconds`), and takes 0.12 s of the attack's clock (0.09 s at the knife's pace, about four frames).
- **A flick, not a turn** (`SwordStrikes.GripFlip`): an ease-out-back, fast off the mark, about 9 degrees past upside down and back.
- **Spun back** once the stab has landed and its pose has been held, in the recovery; cut short (a dash, a ragdoll, another attack), it spins back as quickly.

**The stab** (`SwordStrikeKind.Thrust1H`, still named `sword1h_thrust` on the network): the fist goes up beside the head, out to the right where
the game's camera sees it, the elbow out, the knife point down at the one in front, the free hand reaching ahead as if to grab; held a beat; then
driven down in front, the point at a standing body's chest, the body bending over the blow and the free arm thrown back. The spin runs on every
body the attack plays on (`SwordPoseDriver`), so another player's copy, told only the attack's name, spins it too.

Tests: `KnifeStabTests` (EditMode: only the stab turns the grip; quick, a flick, upside down from the top of the raise until the stab has
landed, back before the end; raised above the head point down, driven down in front at chest height; the fist holds the same stretch of handle
the other way up, the spin over the knuckles), `KnifeStabPlayTests` (on the real body and on a copy: the fist above the head with the knife upside
down in it and its point down, the point driven down in front, the knife back in its hold after). Picture: `KnifeCapture.RenderTheStab`
(`Logs/CityShots/knife-stab*.png`).

## The model (`KitchenKnifeBuilder`)

A stand-in drawn in code until the owner's Meshy model: a flat steel chef's blade (26 cm on the 2.6 m body, straight spine, curved belly,
thick at the spine and thin at the edge) on a turned dark wooden handle with a steel bolster, in the one-handed sword's frame
(`SwordWeapon`: the root at the bolster, the blade up +Y, the edge on -X). `/equip knife` (or `kitchenknife`).

**Size and hold set by the owner (2026-11-15).** The owner made it bigger and placed it in the fist by hand in the editor, then sent the
Transform's numbers: the parts sit under a `Model` child scaled 2.11 (`KitchenModelScale`; the blade now reaches 55 cm and a swing reaches as
far), and the knife has its own hold (`SwordWeapon.SetOwnHold`: position (0.109, -0.04, 0.17), rotation (-85.6, -5.15, -178.1) in the right
hand bone's space) instead of the one shared by the combat sword, the crowbar and the butter knife. To change it again: in Play Mode select
`KitchenKnife(Clone)` in the Hierarchy, adjust its Transform, and put the numbers into `KitchenKnifeBuilder` (not "Save this hold", which
writes the shared one-handed hold).

## The Butter Knife (2026-10-30)

Made for Old Guy's kitchen counter (docs/OLD_GUYS_HOUSE.md; owner: "butter knife on the counter"), where it lies beside the knife block. A **dull
knife** (`SwordWeapon.Dull`): the kitchen knife's moves, speed, reach and backstab, but its blows land as `KnifeRules.DullDamage` (4, the kitchen
knife's 9) and it has no edge, so it cuts nothing off. The stand-in model comes from the same builder (`KitchenKnifeBuilder.BuildButterKnife`): a short
blade (17.5 cm) with a rounded tip and a blunt edge, all steel. `/equip butterknife` (or `butter`). The numbers are mine.

## Other players (docs/MULTIPLAYER_STATE.md)

The knife (the butter knife too) is a `SwordWeapon` (`ICosmeticMountable`): another player's copy holds it, and its swings are announced by name as the sword's
are. The damage (a backstab's included) is dealt on the striker's machine and reaches the body through its copy, as every blow does.

## Tests

`KnifeRulesTests` (EditMode: speed, reach, damage a second against the sword, no stagger, a cheaper swing and a thinner guard, where a
backstab comes from), `KnifePlayTests` (a knife's light lands without staggering where a sword's does; from behind, three times the blow,
a stagger and a critical number, a sword's 1.5 times; the swing over in 1/1.3 of the sword's time; the kitchen knife's prefab).

## Crazy Guy: an enemy with the kitchen knife (2026-11-16)

Owner: "make a hostile npcs called "Crazy Guy" and use my character named "guycrazy" for the playermodel. make him wield a kitchen knife and utilize
its advantages". `/spawn crazyguy`. The swordsman's make (`DuelistBuilder`, shared with `SwordsmanBuilder`: the player's own body, `SwordsmanBrain`,
the blade combat a player has) wearing the owner's guycrazy: its paint, kick hair, bred jeans and black-and-white sneakers (its save copied from the built
game's account to `Character/NpcLooks/GuyCrazy.json`; the clothes put on at spawn on every machine, so other players' copies wear them), with the kitchen
knife. Shared online like the swordsman (`NetworkedCrazyGuy.prefab`, `EnemyReplicationGuardTests`).

He fights by `DuelistStyle.Knife` (the swordsman by `DuelistStyle.Swordsman`, the mind's old constants, unchanged): the knife is quick, short, its
slashes stagger no one, its guard thin and its backstab the biggest (3x). So he
- fights close (2.6 m ring, the knife's reach) and fast (pauses of 0.35 to 0.9 s, presses at the knife's swing speed), in flurries of two to four cuts;
- hardly ever guards (5% of spells) and dashes more (out of a slow blow 70%, away after a hit 50%, out after a flurry 60%);
- goes for the back only with a clear shot at it (owner, 2026-11-17: "only goes for that when he has a clear shot"): already round past the target's
  side (`DuelistMind.ClearShotDegrees`, 110 degrees from its face, `Senses.RoundTargetDegrees`) he holds his cut and steps round to the back; behind it,
  in reach, he cuts at once with a quick cut whatever the target is doing; anywhere further in front he fights face on, cutting where he stands
  (until 2026-11-17 he always worked round, cutting from in front only after two seconds of trying);
- dashes ROUND a slow blow, past its side toward the back, instead of away;
- steps in to cut once his pause is over, instead of waiting for the target to come into reach as the swordsman does;
- now and then the icepick stab (the knife's heavy), never from behind.
Against a player standing still (`CrazyGuyPlayTests`): face to face, his cuts land mostly from the front; started past its side, he takes the backstab. `KnifeDuelistTests`, `SwordsmanLookTests`;
`CrazyGuyCapture` (Explicit, -Graphics). Not in the world's spawns yet (only `/spawn`).
