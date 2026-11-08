# The Wraith Staff

Owner, 2026-11-08, with Meshy's "Crimson Oracle Staff": "make an item called "Wraith Staff" it will be a rare drop from the wraiths like a
3% drop chance, and it summons 1 wraith that follows you around and fights for you, when he dies you can summon another. make the red orb
the same color as the wraith's eyes and make it glow as well. also lower it to a reasonable face count." Then: "have the enemies view the
summon as an opponent as well".

## The item

`wraith_staff`, "Wraith Staff", a weapon in the main hand, **Very Rare** (my placement: it is not on the owner's rarity list in
GAME_DESIGN.md; the wings are Very Rare too). Held as every staff is (`StaffItem`). Its card says what it casts, the summon's health and
the mana.

**Where it comes from:** a wild Wraith that dies leaves one, one time in 33 (`Wraith.StaffDropChance`, 3%). The machine that runs the
Wraith rolls (a copy rolls nothing), and in a session the drop is shared, as a swordsman's sword is (`DroppedItems`, docs/DROPPED_ITEMS.md).
A summoned Wraith never drops one.

## The model

`CityX/SourceArt/WraithStaff/wraith_staff_build.py` (Blender, headless) makes `Weapons/WraithStaff.glb` and `WraithStaff.jpg` from the
Meshy FBX (101,932 triangles, a 4096 colour map; the zip stays in Downloads):

- The orb is the red of the colour map: those faces become their own part.
- Stood as the game's staffs stand (`StaffBuilder`): the long axis up, 2.36 m long like theirs, the grip at the origin and the orb's
  middle 1.29 m above it (`StaffBuilder.OrbHeight`), where spells leave a staff.
- The staff is cut to 5,300 triangles and its paint baked across onto one 512 map (32 KB); the orb to 700, with no map. 6,000 in all,
  tools/model_qa.py's "fine" line for a weapon.

`WraithStaffBuilder` makes the prefab: the staff painted, the orb in the Wraith's eyes' own red and glow (`WraithBuilder.EyeColour`,
`EyeGlowColour`, HDR, so the bloom spreads it), and a small red light of its own at the orb (owner, 2026-11-08: "a bit of red light reflect
off of it not too much just enough that it is noticeable in the dark": a point light, 0.8 strong, reaching 3 m, no shadows). Pictures: `WraithStaffCapture.ShowTheStaffInTheHand` (`wraithstaff-held-*.png`).

## The summon

A click (left button) with the staff summons a Wraith 2.5 m ahead of you for 40 mana (`WraithSummonCaster`), while none of yours is about:
one at a time; when it dies (or you do) another can be summoned.

The summoned Wraith is the same Wraith on your side (`Wraith.Summon.cs`):

- **Follows you:** it keeps to your right shoulder, a little behind, at up to 8 m/s, and is with you again at once if left 40 m behind.
- **Fights for you:** it hunts the nearest enemy within 16 m of you (an enemy pack's member: Crimson Claw, swordsman, Shoota, giant; or a
  wild Wraith), with its own lunge and bite (30, then 6 every 0.45 s). Unlike a wild one it goes straight in from any side (an enemy
  fighting it always turns to face it, so waiting for its back it would only circle: `WraithHunt.Bold`), and rests only 2 s between bites
  (a wild one, 8).
- **Sturdier:** 120 health (a wild one dies to one pistol round, 18).
- **On your side:** no player's blow hurts it (`Opponents.SameSide`, in `DamageContext.Strike`), lock-on skips it, a turned-aside bullet
  does not fly into it, and its kills pay you their XP.
- Bitten, it does not bleed (a ghost). It goes when you die.

## Enemies fight it

Every enemy fights the nearest **opponent**: a player's body or a summon (`IOpponent`, `Opponents`). The Crimson Claw, the swordsman, the
Shoota, the giant and wild Wraiths all search `Opponents.NearestAlive` (the Claw looks again every second, so a summon come nearer than
its target is fought instead). A move only a player can take stays a player's: the Claw's grab only ever catches a player, and the
swordsman reads a player's sword and fists, which a summon has not. Their blows land on it as on anyone (the giant's slam hurts all in
reach). Pictures: `WraithStaffCapture.FilmTheSummonFollowingAndFighting` (`wraithstaff-summon-*.png`).

## Multiplayer

In a session the summon is the networked kind `wraith_summon` (`NetworkedWraithSummon.prefab`, built by `NetworkedEnemyBuilder`): the
staff asks the server for it (`EnemySession.TrySpawn`), the server spawns it owned by you, and your machine runs it, its master your body
(`NetworkedWraith` finds the player its owner owns). Every other machine's copy knows it is a summon from its prefab
(`Wraith.StartsSummoned`): their enemies fight it and their players' blows do not land. Its moves, bite, blood and health go to the others
as a wild Wraith's do. Offline it is made here from `Wraith.prefab`. Others see the staff thrust out and hear the call
(`WraithSummonCaster.PlayRemote`).

## Tests

- `WraithStaffTests`: the item and its rarity, the 3% chance, the model cut down and held as the other staffs are, the orb alone glowing
  the Wraith's eyes' red, a summon spared its side's blows.
- `WraithHuntTests.ABoldOneComesStraightOnAtWhatFacesItAndLunges`.
- `WraithSummonPlayTests`: one summon at a time and another once it is gone (with its mana), it keeps up with you, it fights a Crimson Claw
  near you and the Claw fights it, it goes when you die, a wild Wraith's staff falls.
- `NetworkedPlayerStateSyncPlayTests.ASummonIsOnItsSummonersSideOnEveryMachine`; `EnemyReplicationGuardTests` lists the kind.
