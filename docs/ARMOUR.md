# Armour

Owner, 2026-11-06:
- "there should certainly be stronger armor than others"
- "No heavy armor slowing you down sounds like an annoying system we should avoid things that are annoying like carrying a lot of items slowing you down"
- "research how other games of similar genres do their strongest armor. and make sure it isnt too op for pvp"
- "the fights should feel fun and last long enough that you can utilize your hotbar or different weapons and strategies you have earned"
- Then "ok go ahead" to the numbers below, which were my suggestion.

## How it works

- **Worn pieces have an armour rating** (`Items/ArmourRatings.cs`). The rating depends on the slot the piece covers and its tier (Light, Medium, Heavy). Clothes are tier None and protect nothing.
- **The best piece in each slot:** helmet 14, top 27, legs 16, gloves 5, shoes 5. A full heavy set totals 67. A Light piece gives 35% of its slot's best, a Medium piece 65%, a Heavy piece all of it.
- **The rating adds to the Armor stat** (docs/STATS.md). Together they stop rating / (rating + 100) of a blow or a bullet, the stat's old rule. A full heavy set alone stops about 40%. Gear does nothing against spells, which magic resistance handles.
- **The caps** (`Character/Stats/ArmourRules.cs`):
  - Armour and magic resistance never stop more than **half of a player's blow**, or **two thirds of anything else's** (enemies, the world).
  - So a fight between two fully armoured players still takes a few exchanges, time to use the hotbar, and never stalls.
  - The attacker's penetration lowers the rating before the cap, as it always did.
- **Nothing slows you down.** There is no armour weight and no carry weight (owner).
- **The sledgehammer ignores 35% of armour** (`ArmourRules.HammerPierce`), on top of the wielder's Armor Penetration. It goes through block chance by the same share, as all penetration does. This is what sets it apart from the greatsword it shares its moves with. The card says "Ignores 35% of armour".
- **The inspect card** shows a piece's tier as its style ("Heavy Armour") and its line "Armour: 14 (stops 12%)". Clothes show no line.

## Which pieces are armour

- Only the **Knight Helmet** (Heavy, 14) so far. Every other worn item is clothing.
- New armour pieces get a tier in `ArmourRatings.Tiers`, and their rating follows from their slot.

## Multiplayer

- A blow is decided on the machine of the body it hits (owner-authoritative), where that body's inventory is, so the gear is read there.
- A hit forwarded from another player's machine carries a "struck by a player" flag (`NetworkedDamage.Flags`, `DamageContext.ByPlayer`), so the right cap applies.
- `DamageContext.Strike` sets the flag from its striker: a player's body (`PlayerBodies.IsPlayer`) or not.
- Tested over real sockets: `NetworkedPlayerStateSyncPlayTests.APlayersBlowThroughTheRelayIsCappedAsAPlayersBlow`.

## Tests

- `ArmourTests`:
  - the best set stops about 40%
  - the tiers rise, and clothes give nothing
  - the caps, and penetration before the cap
  - the hammer's pierce and its card
  - a worn helmet stops blows and bullets, not spells
  - the cap covers the stat and the gear together
  - an enemy striker gets the enemy cap
  - the helmet's card
- `StatsTests` keeps the stat's own rule under the caps.
- Picture: `ItemInspectCapture` (`inspect-card-armour.png`).

## Armour and dismemberment (built 2026-11-06)

Owner: "probably having armor makes you harder to be dismembered and if you do still get dismembered badly we could have the armor just come off the body and turn into its object form".

- **Armour holds a part on** (`DismemberRules.ArmouredFactor`, `Covers`; `Dismemberer.ArmourOn`).
  - A part under armour needs 1 + 2 x the armour's tier share more to come off: three times as much under heavy armour, about 1.7 times under light. My numbers.
  - This applies to both a killing blow's overkill and the blows a corpse's part takes.
  - What each slot covers: a helmet the head; a top the torso and upper arms; gloves the forearms; legs the legs; shoes the shins.
- **Gear on a part that comes off anyway falls off as the item** (`Dismemberer.PopWornOff`):
  - This applies to anything worn, not only armour.
  - It does not go with the part as a still copy or burst with it. It lies there as the real item, for anyone to pick up.
  - The machine that decides the body drops it, for everyone in a session (DroppedItems). Every copy of the body takes it off.
  - It leaves the body's loot list.
  - A player's own gear stays theirs.

## Looting fallen enemies (built 2026-11-06)

docs/LOOTING.md, Bodies.
