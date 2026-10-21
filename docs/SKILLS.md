# Skills: the skill map, learning, and the bar

## 2026-10-14: built to the owner's plan

The owner's asks, in order: "implement all the skills we have so far including the movements like dash, climb, super jump, (not vaulting we will
leave that as default) with the ability to add them as hotbar spells/abilities"; "the icons for the hotbar spells will be round or something
instead of square, similar to dc universe online"; "9 and 0 key should be usable"; no ultimate slot ("i made the idea of god tier abilities but not
ultimates"); "i want the skill tree to be scrollable like the skyrim skill tree but in our game it wouldnt be a fixed perspective. make it one big
menu"; "skyrim is right to have them all adjacent side by side but you should be able to click and drag across as well as scroll"; "just have the
hotbar at the bottom left for now"; "vaulting will be the first one you unlock in the tutorial so everyone has it and then the three paths you can
go in movement will be Dash, Climb, Super Jump"; "right now every character can learn every path"; "pulling yourself up onto a ledge naturally is
apart of climb"; movement does not go on the bar ("not the ones we included so far").

### The trees (`Character/Abilities/SkillCatalog.cs`)

One row, left to right, wrapping round: **Freerunner, Sword, Berserker, Pyromancer, Steam, Hydromancer, Cryomancer, Plaguebringer, Electromancer, Wizard**.

| Tree | Stars | Notes |
|---|---|---|
| Freerunner | Vault (root, everyone has it) -> Dash, Climb, Super Jump; Wall Stick above Climb | the owner's shape; Wall Stick's place is mine |
| Sword | Sword Spin -> Sword Wave, Stab Dash, Octaslash | the four need a sword in hand; Stab Dash (2026-10-18) is the owner's, docs/STAB_DASH.md |
| Berserker | Bloodrage | |
| Pyromancer | Fireball | its top star joins Steam |
| Steam | one star, high in the gap | a hybrid (fire + water), not in the game yet: drawn faded |
| Hydromancer | empty stars | not in the game yet: drawn faded |
| Cryomancer | Ice Shards | |
| Plaguebringer | Poison Breath -> Acid Rain | |
| Electromancer | Self Smite | the owner's tree (2026-10-15); Self Smite is Pikachu's Thunder, see docs/SELF_SMITE.md |
| Wizard | Grow, Shrink (side by side at the root) | the owner's tree (2026-10-15: "i was going to make one just called Wizard"); ten times as big or a tenth, 30 s each, see docs/SIZE_SPELLS.md |

Every tree also has **empty stars** ("Not designed yet") where later skills go; they are never learnable. Every tree shape except Freerunner's is
my layout, as are the trees' order and the two not in the game.

### Learning (`SkillBook`, `PlayerSkills`)

- A star needs everything below it learnt and **1 skill point** (the cost of deeper stars is open).
- **Skill points** are their own pool (PlayerVitals.SkillPoints, beside the stat points): one every second level, as Godot's
  `SKILL_POINT_EVERY_N_LEVELS`. A brand new character starts with vaulting and **1 point**, so the first path can be picked at once
  (`CharacterSave.StartingSkillPoints`; my number).
- **Reset** (two clicks) forgets everything but vaulting and gives every point back. Free for now; GAME_DESIGN.md wants respecs to cost more later.
- **A body with no character knows what the game had before the tree**, with the old number keys: pressing play on the city scene in the
  editor, and every test, plays as before. **A character made before the skill tree** (`SkillsVersion` 0) keeps the same, and its keys; a reset
  turns that into points to spend. That is every skill **but Super Jump** (`SkillBook.AsBeforeTheTree`): the charged jump was not in the game, and
  knowing it moves a jump from the press of Space to its release, which every test (and anyone used to the instant jump) would trip over.
- Saved with the character (`LearnedSkills`, `Hotbar`, `SkillPoints`, `SkillsVersion` in `CharacterSave`; `CharacterIdentity` reads and writes).

### What a skill gates (`PlayerInputReader`)

| Skill | Without it |
|---|---|
| Spells and sword techniques | their key does nothing: a number key casts whatever is in its slot of the bar, once learnt |
| Dash | double-tapping Shift does nothing. **The sword's roll is not gated**: it is the sword fighter's dodge, part of sword combat |
| Climb | no wall is ever grabbed (Space held, or Shift + W into a tall wall): no climbing, no pulling up onto ledges, no climbing in through high windows |
| Wall Stick | no sticking to walls |
| Super Jump | Space jumps at once at the base height. **With it**, Space held crouches and charges (up to 1.5 s) and the jump goes on release, up to 15 times as high (a full charge rose 15.6 m against 1.07 m in the test). The charged jump had been kept out of the game for this ("keep it in the vault") |
| Vault | always available |

**A super jump never hurts its own landing**: the fall is counted from no higher than where it took off (`PlayerVitals.ForgiveFallAbove`), so a
leap back to the same street is free and one off a roof costs what stepping off would. Without this every full leap cost about a fifth of the
health (it rises past the 12.8 m safe fall).

### The bar (`Hotbar`, `UI/Hud/HotbarView.cs`)

- Ten round slots on **1 to 9 and 0**, all equal, in the **bottom-left corner** of the HUD: a gold ring, a dark face, the skill's icon in pale
  gold, the key at the lower right. An empty slot is a dim ring; a sword technique with no sword in hand is greyed; a slot flashes when its key
  goes down. It steps aside while the chat line (same corner) is open.
- A skill sits in one slot at most; a newly learnt spell goes in the first empty slot. Movement never goes on the bar.
- Built when the HUD wakes, from code-drawn art (`HudArt.Disc`, `HudArt.Ring`) and code-drawn icons (`UI/Menus/SkillIcons.cs`): no image files, no
  prefab rebuild.

### The menu (`UI/Menus/SkillMenuView.cs`, `UI/Menus/SkillMapLayout.cs`)

C, Player, Skills. A night sky, the trees side by side, each growing up from its name. **Drag** anywhere to move (sideways and up); the **wheel**
slides along the row; **Ctrl + wheel** or the **- / +** buttons zoom; **Show all** fits the whole row. The row **wraps round** like Skyrim's
(drawn twice, side by side, the two copies slid to cover the view). Click a star for its **card** (bottom right: what it does, what it needs, a
**Learn** button). Put a learnt spell on the bar by **dragging the star onto a slot**, clicking it then a slot, or clicking it and pressing 1 to 0;
**right-click** a slot to empty it. Escape or C closes it. On a controller: left stick moves, right stick zooms, bumpers step tree to tree, A picks
the star in the middle (and learns it on a second press), Y puts it on the next slot, B closes.

Star colours: gold face = learnt, gold ring = ready to learn, grey = locked, small grey "?" = not designed yet; lines turn gold between learnt stars.

### Multiplayer

What is learnt and the bar are the owner's own (nothing to show anyone). What a skill does already replicates (docs/ABILITIES_MULTIPLAYER.md).
New: the Super Jump's **charging crouch** (Animator `ChargingJump`) is sent with the other animation parameters, so other players see the crouch,
then the jump. The menu and the bar follow `PlayerSkills.Local`: the body this machine controls (a player who joined a server takes over their own
body's, `NetworkedPlayerIdentity.OnStartClient`).

### Console

`/skills` lists what is learnt, the points and the bar; `/skills points 5`; `/skills reset`; `/skills all`; `/skills new` (vaulting and one point).

### Tests

`SkillTreeTests` (the map's data, the rules, the bar, the save, skill points, the forgiven landing), `SkillMapLayoutTests` (the row, the two copies,
wrapping, stepping tree to tree, show all), `SkillMenuPlayTests` (learning, the bar, dragging a star onto a slot, wrapping, zoom, reset, closing,
the HUD's bar), `SkillLocksRealInputPlayTests` (real keys in the real city: a new character's keys, a spell moved to 0, Dash, Climb and Super Jump
only once learnt, a super jump's landing). `SkillMenuCapture` (Explicit, -Graphics) renders `Logs/CityShots/skills-*.png`.

### Not done

- Costs beyond 1 point, deeper stars, real skills for the empty stars, upgrade ranks (GAME_DESIGN.md: Infinite upgrades).
- One bar per weapon (my suggestion, undecided). Cooldown sweeps on the slots (each ability reports readiness differently).
- The tutorial that teaches vaulting (vaulting is simply known from the start).
- A joining player's HUD and C menu are still built for the scene's body; the skill menu and the bar follow their own body.
