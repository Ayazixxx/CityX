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
| Light blows | 8 and 8.5 (10 base) | 5.6 and 6 (7 base): about **90%** of the sword's damage a second |
| Light stagger | 0.45 s | **none**: a knife's light does not stop what its target is doing, so it cannot trade blows with a sword (the sword's stuns until its next cut lands: `SoulsCombatRhythmTests`) and barely shoves (the shove follows the stagger) |
| Light stamina | 16 | 11 |
| Heavy (hold the click) | the overhead cut | **a lunging stab** (the thrust pose) that closes a metre, guard-breaking, 11 damage |
| Guard | 1 stamina a point of damage | **1.6**: a thin blade breaks sooner |
| Backstab | **1.5x** | **3x** |

**The backstab** is every one-handed blade's (the sword, the crowbar, the bat a little; the knife most): a blow on a living body from its
back third (within 60 degrees of straight behind it) is a critical hit -- the multiplied damage, a stagger of at least 0.6 s even from a
knife's light, twice the hit-stop, and a **gold** damage number a third bigger (`DamageContext.Critically`, `DamageDealt.Critical`). Face to
face it never happens: it takes a dash round someone, or catching them busy with someone else. The two-handed weapons have none.

The chain is the one-handed sword's first two cuts, back and forth: the flat slash and the backhand (`SwordStrikes`), the knife's run faster. The kitchen
knife is edged, so an overkill blow cuts the part it hits off (docs/DISMEMBERMENT.md).

## The model (`KitchenKnifeBuilder`)

A stand-in drawn in code until the owner's Meshy model: a flat steel chef's blade (26 cm on the 2.6 m body, straight spine, curved belly,
thick at the spine and thin at the edge) on a turned dark wooden handle with a steel bolster, in the one-handed sword's frame
(`SwordWeapon`: the root at the bolster, the blade up +Y, the edge on -X). `/equip knife` (or `kitchenknife`).

## The Butter Knife (2026-10-30)

Made for Old Guy's kitchen counter (docs/OLD_GUYS_HOUSE.md; owner: "butter knife on the counter"), where it lies beside the knife block. A **dull
knife** (`SwordWeapon.Dull`): the kitchen knife's moves, speed, reach and backstab, but its blows land as `KnifeRules.DullDamage` (4, the kitchen
knife's 7) and it has no edge, so it cuts nothing off. The stand-in model comes from the same builder (`KitchenKnifeBuilder.BuildButterKnife`): a short
blade (17.5 cm) with a rounded tip and a blunt edge, all steel. `/equip butterknife` (or `butter`). The numbers are mine.

## Other players (docs/MULTIPLAYER_STATE.md)

The knife (the butter knife too) is a `SwordWeapon` (`ICosmeticMountable`): another player's copy holds it, and its swings are announced by name as the sword's
are. The damage (a backstab's included) is dealt on the striker's machine and reaches the body through its copy, as every blow does.

## Tests

`KnifeRulesTests` (EditMode: speed, reach, damage a second against the sword, no stagger, a cheaper swing and a thinner guard, where a
backstab comes from), `KnifePlayTests` (a knife's light lands without staggering where a sword's does; from behind, three times the blow,
a stagger and a critical number, a sword's 1.5 times; the swing over in 1/1.3 of the sword's time; the kitchen knife's prefab).
