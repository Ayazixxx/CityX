# Self Smite: Pikachu's Thunder

## 2026-10-15: built to the owner's ask

> "make a spell called "self smite" and a giant yellow thunder bolt strikes you. it is like pikachu's down special from smash bros and i want you
> to time it and make it function exactly like it as well. use this animation for the spell speed it up or slow it down as needed make the
> thunder bolt appear as he throws his hands down ... make it a hotbar spell btw and add it to the Electromancer skill tree"
>
> "make the bolt appear from SUPER high in the sky ... around the height lightning strikes appear in real life"
>
> "no need to make hotbar spell icons ... just set it to the letter the spell starts with"

The first spell of the **Electromancer** tree (its root; one skill point). On the bar: **key 9** out of the box (`SkillCatalog.DefaultBar`), and
a character from the menu learns it in the C menu and drags it to any slot. Its icon is a bold **S** until the owner draws one
(`SkillIcons.LetterFor`: any skill with no drawn icon shows its name's first letter on the bar, its star and the dragged ghost).

## The timeline (`Character/Abilities/SelfSmiteMove.cs`)

Thunder's frame data from Super Smash Bros. Ultimate, at 60 frames a second. Frame N runs from (N-1)/60 s to N/60 s after the key.

| Frame | Thunder | Self Smite |
|---|---|---|
| 2 | the thundercloud forms over Pikachu | the cloud forms 1.5 km up (0.017 s) |
| 13 | the bolt leaves the cloud, hurting what it passes (6-8%) | the bolt leaves (0.2 s); 18 damage to whatever it passes, within 1.5 m |
| 33 | it reaches Pikachu | it lands on the caster's head (**0.55 s**) |
| 34-36 | the burst round Pikachu (15%, a strong launch) | the burst: 40 damage within 4 m of the caster's middle, thrown away and up (13 m/s out, 8 up) |
| 34-43 | Pikachu cannot be hit | the caster cannot be hit (to 0.717 s) |
| 75 | Pikachu can act again | the caster is free (1.233 s) |

Pikachu stands still for all of it, and so does the caster: rooted (`PlayerLocomotion.MovementRooted`), every key but the mouse ignored
(`PlayerInputReader`, like the push-ups). Thunder has no cooldown of its own; a spell here does: **5 s**, shortened by Cooldown Reduction. Spell
Power raises both hits 2% a point and Armor Penetration goes with them, as with every spell.

**Knocked out of it** (a blow that staggers, a fall into a ragdoll, death): the caster is free at once; before the strike the bolt still comes down
where they stood, with no burst.

## The bolt from the clouds (`CloudLayer`, `SelfSmiteBolt`, `SelfSmiteBoltShape`)

- **The cloud layer is 1.5 km above the ground** under the caster (`CloudLayer.HeightMetres`; real thunderclouds' bases are 1 to 2 km up). The
  world goes up for ever, so somebody flying within 200 m of that height or above it gets their cloud 1.5 km above themselves. Acid Rain is to use
  the same layer.
- The bolt falls 1.5 km in a third of a second, **slowing as it nears** (height = cloud x (1 - fall)^4): the camera sits a few metres behind the
  caster and sees only the last tens of metres, and those take the last nine frames, so the bolt is seen coming, as Thunder's is.
- Shape: a jagged trunk with kinks spaced finely at the bottom (0.7 m) and ever wider going up (x1.35 a step, about two dozen points), drawn wider
  higher up (0.32 m at the caster, about 6 m at the cloud) so it reads all the way. Four forks near the bottom flicker after the strike. A new path
  every three frames and a new brightness every frame: lightning stutters.
- **Drawn in code**: camera-facing ribbons built into one mesh a frame (a white-hot core and a yellow glow, additive, brighter than white so the
  bloom takes them), the cloud as soft dark puffs lit from inside while the bolt leaves; one 32 x 32 soft spot texture made at runtime. A yellow
  point light floods the street at the strike (cubic fade over 0.45 s), **the whole sky flashes** (`SelfSmiteBolt.Struck`, which `RainEffect`
  answers with its storm flash, in any weather), and `thunder_strike.ogg` cracks.

## The animation

The owner's `selfsmite.fbx` (`Character/Animations/selfsmite.fbx`, Animator state `SelfSmite`, `PlayerRigBuilder.AddSelfSmiteState`). Measured on
the rig: the hands go up over the head by 1.0 s of the clip's own time and are thrown down fastest at 1.15-1.3 s, into a crouch. Played at
**2.31x** (`SelfSmiteMove.ClipSpeed` = 1.27 / 0.55) the hands come down as the bolt lands, and the clip's 2.97 s end at 1.28 s, about when the
caster is free. The torso follows the clip as it does a sword swing's (`PlayerLocomotion.SpellClipOwnsTheTorso`).

## Multiplayer

`IReplicatedAbility` id `self-smite`, fired once as it is cast. Every other player's copy plays the same animation under the same cloud, bolt,
burst, light, sky flash and thunder, hurting nothing (a copy is never rooted: it moves with the network). The damage is the caster's machine's;
a hit on another player's copy goes to its owner like every hit, and so now does **the throw** (`IRemoteDamageRelay.TryRelayKnockdown`,
`NetworkedDamage`, `PlayerVitals.TriggerLaunchKnockdown`): the victim is thrown on their own machine, and the copy on the attacker's still falls so
they see it. Combo finishers go the same way now (before, only the attacker's copy of the victim ever fell).

## Tests

`SelfSmiteTests` (EditMode: the frame data, the clip speed, the fall, the cloud layer, the bolt's shape, the throw, the catalog, the letter icon),
`SelfSmitePlayTests` (key 9 on the real rig: rooted, the bolt's fall and the burst hurt the right targets at the right moments, untouchable at
the strike, never hurt by itself, free again; a stun cancels it; the cooldown; a throw on a copy is relayed), `SkillMenuPlayTests` (the letter on
the bar, the star and the drag ghost), `NetworkedAbilitySyncPlayTests` (B's copy of A shows it). Look at it with `SelfSmiteCapture` (Explicit,
`-Graphics`): day and night, from behind and wide.
