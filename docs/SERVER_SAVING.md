# Characters saved on the server

Owner, 2026-10-22: "ok do server side saving". Until now each player's own PC kept their characters (`characters.json` in the game's
data folder) and decided their progress, so a character could be edited by hand, lived on one PC only, and a player joining someone
else's game brought their own.

## The plan

1. **Characters live on the server (doing now).** The machine running the world keeps every player's characters; the character screen
   lists, makes, deletes and picks them through it; the server saves progress while playing. Singleplayer is a server on your own PC,
   so it plays as before. A friend joining your game makes a character on your server (owner agreed, 2026-10-22).
2. **The server decides, not just stores (next).** XP, level-ups, spending points and items decided on the server, so a cheater cannot
   report made-up progress (`docs/GAME_DESIGN.md`, Technical foundations 1).

## Who a player is

- `PlayerKey`: a random key made the first time the game runs, kept in `player.key` in the data folder. A stand-in until the game is
  on Steam: **the owner wants identity to be the player's Steam account** (2026-10-22), which replaces the key in one place.
- The server files characters under it (`<server data>/accounts/<key>/characters.json`, `CharacterDatabase`) and checks it against
  the admin list. A key that is not 32 lowercase hex digits reaches nothing on disk.

## Admins

Owner, 2026-10-22: "i only want me and other admins to have access to console right now anyone with access to the game would be an
admin". `<server data>/admins.json` (`CharacterDatabase.Admins`) is written the first time a server reads it, with
`EveryoneIsAdmin: true`; to lock the console down, set it to false and put the admins' keys in `Keys`. An unreadable list makes no one
an admin, never everyone.

## What a player may do (`CharacterService`)

- Only their own characters: list, play, delete, save.
- A new character keeps only its name and look (skin, paint, hair); its id, level, stats, skills, clothes and playtime are a new one's,
  whatever the creator sent. At most `MaxCharactersPerPlayer` (20).
- A save takes the progress, look, clothes and playtime, never a new id, name or creation time. (It still trusts the reported
  progress: step 2.)

## Moving in

The first time a server on this PC runs for this player, the characters in the old `characters.json` are copied into their folder.
The old file stays where it was, as the backup; the copy happens once (`CharacterDatabase.MoveInLocalCharacters`).

## Where things are written

`DataFolder.Path`: Unity's per-user data folder, unless the environment variable `CITYX_DATA_FOLDER` names another (a dedicated
server's own disk; the tests' scratch folder). Characters, settings, the player key and a local server's data all go under it.

## Tests were filling the real save (found 2026-10-22)

Tests that played a character (the redraw creator, the HUD portrait) let it save into the owner's real `characters.json`: 2,566
characters, 5 MB, by 2026-10-22. Cleaned once by hand: everything named as only tests name characters ("Redraw Test", "Redraw Mouse
Test", "Flours", "Portrait", the three "FlowTest" ones) was taken out, 11 kept, the original backed up beside it as
`characters.json.before-test-cleanup-2026-10-22`. From now on `ScratchDataFixture` (one per test assembly) points every run at a scratch
folder through `CITYX_DATA_FOLDER`. Two traps met on the way: a static field set in a set-up fixture is wiped by the code reload Unity
does on entering play mode, and a set-up fixture outside the tests' namespace never ran for the play tests at all (so the existing
`ClassicCityScaleFixture`, which is outside every namespace, may not be pinning the classic city for the play tests either; not checked).
`ScratchDataPlayTests` guards it.

## How the game reaches the server

- **`ICharacterSource`** (Core): list, create, delete, play (the server's copy back, and whether you are an admin), save. Answers come
  through callbacks. `CharacterSources.Current` is the one in use.
- **This PC's own server** (`LocalCharacterSource`, `LocalServer`): singleplayer and hosting. This PC is the server, so it is asked
  directly and answers at once; singleplayer starts no network for it (the title's network only runs when hosting or joining, as before).
- **A server joined across the network** (`RemoteCharacterSource`, Net): the title puts it in when a join succeeds and takes it out on
  Back. Each question is a `CharacterRequest` broadcast carrying this copy's player key; `CharacterServer` (beside the title's
  NetworkManager, built by `TitleScreenBuilder`) answers from the host's storage. A list comes back one character per message (a painted
  character is up to about 7 KB).
- **The character screen** (`CharacterSelect`) lists, makes, deletes and plays through `CharacterSources.Current`; the character played
  is the server's copy (`PlaySession.Begin(served, isNew, isAdmin)`).
- **Saving while playing** (`CharacterIdentity.Write`, every 30 s and on quit) goes through `CharacterSources.Current.Save`.
- **The console** opens only while `PlaySession.IsAdmin` (true when the world is opened directly in the editor, with no menu).

## Step 2: the server checks a save's progress (2026-10-22)

Enemies run on each player's own machine (not networked yet), so the server cannot see a kill happen and cannot decide XP itself. What it
can do, and does: every save's progress must add up against the last save it accepted from that character (`ProgressRules`, installed
as `CharacterService.Rules` when the game loads). Judging the change, not the whole, holds for any character however its numbers got
where they are (an old save, a console edit).

- **XP** gained fits the time played: at most 100 a second plus one burst of 1,000 (kills give 20-80; a spell can take a crowd).
  The XP towards the next level is less than that level needs.
- **Stat and skill points**, spent and unspent together, grow by no more than the levels gained bring (5 stat points a level, a skill
  point every second level). Resetting skills for their points back passes.
- **Every skill newly learnt** is a real one, with what it requires learnt too. A save from before the skill tree is not checked on
  its first save after (it kept its skills for free).
- **Playtime** grows no faster than real time.
- **Refused:** the server keeps the character's progress as it was and logs why; the look, clothes and hotbar are still taken.
  The time allowed counts from the last accepted save, so a player who briefly beat the speed limit is caught up as they play on.
- **Admins' console edits** (`/xp`, `/level`, `/skills`) set `PlaySession.AdminEdited`; the next save carries it, and the server
  takes it past the rules only if the player is on its admin list. So the rules apply to everyone, admins included, except a deliberate
  console edit.

Tests: `ProgressRulesTests` (playing passes; made-up XP, levels, points, free or unreachable skills and fast playtime are refused; a
console edit passes for an admin only), `CharacterServerPlayTests` (a made-up level sent over the network is refused, the new look
taken).

Not yet: XP from kills is still the player's copy's word, within the speed limit, until enemies run on the server (GAME_DESIGN,
Technical foundations 3); the bag was not saved at all until step 3 (below); a refused player keeps playing with their own numbers
until they next load the character (the server's copy); a dedicated server has no `CharacterServer` yet.

## Status

- Step 1 done: storage, rules, admin list, move-in, the local and network paths, the character screen, in-game saving, the console's admin check.
- Step 2 done as far as it can go before enemies run on the server (above).
- Step 3 done (2026-11-19): the whole bag saved with the character (3a), and a far player's bag checked against what the server handed them (3b): below.

## Step 3: the bag (2026-11-19)

Owner, 2026-11-19: "ok do server side saving next". Steps 1 and 2 were done; what was missing was the **bag**: a character kept only the
clothes it wore and its primary weapon (`Wearing`, `PrimaryWeapon`), so storage, the secondary weapon, a pair of pistols, the shield and every
gun's rounds were lost when the game closed.

### 3a: the whole bag is saved on the server

- `CharacterSave.Bag` (`BagEntry`, Core): every storage slot that holds something (its index, the item, how many) and every equipped slot
  (clothes, back, both weapon slots and a pair's left-hand pistols, the shield), each with its state (a gun's magazine and spare rounds, a
  bottle's cracks, a can's litres). `WeaponInHand` is the weapon slot that was in hand. `BagVersion` 1 marks a save that has a bag.
- Saving (`CharacterIdentity.Write`, every 30 s, when what is worn changes, and on quit) writes the bag (`Inventory.ToBag`), only once the
  saved bag is on the body (never an empty body's hands over what the character carries). The server keeps it (`CharacterService.Save`); a
  save from a copy of the game from before (no bag) leaves the kept one alone.
- Playing a character puts it all back (`Inventory.FromBag` through `CharacterIdentity.PutOnBag`): the body emptied, the storage slots as
  they were, the clothes on, each weapon put away in its own slot with its rounds, the one that was in hand drawn with its pair. A thing the
  game does not know is left out; one that cannot be where the bag says (a shirt in a weapon slot, a rifle beside a pistol, a storage slot
  named twice) goes into storage.
- A character from before (no bag yet, and every new character) is dressed as before from `Wearing` and `PrimaryWeapon` until its first
  save. The title's rooftop still shows only the clothes and the primary weapon (`PutOnClothes`).

Tests: `BagSaveTests` (EditMode: the round trip, a bag replaces what the body had, junk, the server's file and an older copy of the game),
`BagSavePlayTests` (a real body gets its bag back, the AK-47 in hand with its rounds, the pistols holstered as a pair, and saves what it
carries after a shot).

### 3b: the server checks a far player's bag

A player on someone else's server could report any bag at all (an AK-47 from nowhere, ninety grenades). Now the server keeps of a bag
saved from another machine only what can be carried and what that player can have got:

- **What it saw them get.** The server records every item it decides between players and hands one of them (`ItemsHanded`, through
  `LocalServer.HandTo` and `CharacterService.Hand`): a dropped item picked up (`DroppedItemsNetwork.OnTakeAtServer`) and a piece of a fallen
  enemy's gear taken (`NetworkedCorpseLoot`). It knows which character a connection plays from its play request (`CharacterServer`:
  `LocalServer.Plays`, forgotten when the connection leaves).
- **The rule** (`BagRules`, installed as `CharacterService.BagRules`): of each item, no more than the bag the server kept held plus what it
  handed since. A thing from nowhere is taken out and a stack grown from nowhere cut back; the server logs what it took out. A save that
  shows a handed item uses it up; one never shown (picked up and dropped again before a save) is forgotten after 150 s.
  Fewer things is always fine (thrown, broken, dropped, used up).
- **What can be carried**: a thing the game has, a stack no bigger than the item stacks, each storage slot and equipped slot used once, a
  weapon in a weapon slot, a pistol beside a pistol, clothes where they are worn, and no more rounds or litres than any item holds.
- **Not judged:** this machine's own player's bag (singleplayer, and the host's own character: the player owns that machine and its files
  anyway), and a save marked as an admin's console edit from a player on the admin list (`/equip`, and an admin picking up something lying only
  on their own machine, such as a `/spawn`ed item, mark the next save).
- **While everyone is an admin** (`admins.json` says `EveryoneIsAdmin: true`, as it does until the owner locks it down) a console edit passes
  for anyone, so the check bites once the admin list is set.

Not yet: rounds are judged only against what any gun can hold (there is no ammo to pick up yet, so a gun's rounds cannot be counted against
where they came from); the tutorial house's drawers and weapons are this machine's own (it is played alone, on the player's own server); a
player whose things were taken out keeps playing with them until they next load the character, as with refused progress.

Tests: `BagRulesTests` (the ledger, the rules, the server: a far player's AK-47 from nowhere taken out and a handed one kept once, an admin's
edit, this machine's own player), `CharacterServerPlayTests` (over real sockets: an AK-47 the server never handed is taken out, one it handed is
kept).
