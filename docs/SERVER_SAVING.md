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

## Status

- Done: storage, rules, admin list, move-in, the local and network paths, the character screen, in-game saving, the console's admin check.
  Tests: `CharacterServiceTests`, `LocalServerTests`, `CharacterServerPlayTests` (a player joining over real sockets lists, makes,
  plays, saves and deletes), and the character screen and identity play tests now going through the server.
- Not yet: a dedicated server has no title, so nothing adds a `CharacterServer` to it (it has no player spawn path either,
  `MULTIPLAYER_ARCHITECTURE.md`); the console's commands themselves still run on the player's own copy; step 2 (the server deciding).
