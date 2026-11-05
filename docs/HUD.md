# The player HUD's unit frame

## 2026-10-14: World of Warcraft style, a close portrait, and mana

Owner, with two of World of Warcraft's unit frames to go by: "zoom in the player hud portrait in the top left so that it is just the head and a
small glimpse of the shoulders visible ... make it look like the world of warcraft version ours looks like crap. also figure out a way to include
a mana bar in the mix without it being too bunched up. make it so the mana bar doesnt appear until you unlock a spell. but i guess for now just
have it on screen from the beginning."

**The frame** (built by `PlayerHudBuilder.BuildStatusFrame`, art by `HudArt`, driven by `PlayerHud`):

- A round portrait, 104 design pixels (the canvas is 1152 x 648), in a gold ring shaded like a metal tube lit from the top left, with a dark line
  along both rims and a soft shadow inside it so the face sits in a hollow. The level is on a small dark badge in its own ring at the lower left.
- To its right a dark plate with a gold edge, its left end tucked under the ring, holding a column: the character's name in WoW's yellow, the
  health bar with its percentage, the mana bar with its percentage, and a thin stamina bar without text. The column sizes itself to its rows, so
  with no mana the plate closes up instead of leaving a gap.
- Bars are glossy (a grey gradient with a band of shine, tinted by each bar's colour) in a dark well. Health is green, a warm yellow from 75%
  and red from 25% (`PlayerHud.ColourForHealth`); mana is blue; stamina is white with a tinge of blue.
- Percentages read as WoW's do: 100 only when full and 0 only when empty (`PlayerHud.PercentFor`).
- The art is drawn in code when the HUD first appears and shared, never stored: a 256-pixel texture saved as an asset is half a megabyte.

**The portrait** (`PlayerHud.Frame`): from the top of the hair down to 1.3 head-radii under the head's centre (the chin and the tops of the
shoulders; it was 2.3, the chest), with a little margin for the circle, from 24 degrees round to the character's right so the face turns toward
the bars, lit from up and to the left of the camera. Near-black behind it (`PortraitBackgroundColour`).

**Its pose** is the plain one, always. The first portrait is taken as the world loads and its pose kept: every later portrait (a helmet put on,
a redraw) is taken in it, from the same spot. Weapons taken out never retake it. The owner kept seeing the fists up in it (2026-10-24: "there
should be no fists up pose in the portrait or any poses besides the plain one"): a punch thrown as the world loaded raised the guard for
three seconds, the first portrait caught it, and every later one copied it. Now the first pose is kept only when the body stands plain
(`PlayerLocomotion.ShowsPlainPose`: the walk/idle cycle, all but still, nothing posed over it, nothing in the hands). Any other shot only
stands in. Once the body is plain, the idle is started again (so it is at the moment a load catches it) and the portrait taken again and
kept. An ordinary load is plain from the start and is shot exactly as before (`PlayerHudPortraitCityPlayTests`).

**Mana** (`PlayerVitals`): `Mana`, `MaxMana` (100), `ManaRegenPerSecond` (4, not while dead), `TrySpendMana`, refilled on respawn.
`ManaUnlocked` shows or hides the bar; it is true for everyone for now, and is meant to turn on with the first spell unlocked (GAME_DESIGN.md:
Mana appears with your first spell). Arcane Orb and the staffs spend it (2026-10-27, docs/ARCANE_ORB.md, docs/STAFFS.md: my numbers); the older spells do not yet: what each spell costs is to be decided by how strong it is (owner).

**Tests**: `PlayerHudTests` (every piece wired, the percentages, the art painted on every piece), `PlayerHudPlayTests` (the mana bar follows mana
and hides without it, the health percentage, the character's name), `PlayerHudPortraitPlayTests` (with -Graphics: a portrait taken with the fists up is taken again once they are down; the head fills the middle of
the portrait, the shoulders only the bottom), `PlayerVitalsProgressionTests` (mana spent, refused, regained, not while dead, refilled on
respawn). `PlayerHudLookCapture` (Explicit, -Graphics) renders `Logs/CityShots/hud-frame-*.png`, each with a close-up.

## 2026-10-14: the bar of skills, bottom left

Ten round slots on the keys 1 to 9 and 0 in the bottom-left corner (owner: round icons like DC Universe Online, "just have the hotbar at the
bottom left for now"): `HotbarView`, added by `PlayerHud.Awake` and built in code. See docs/SKILLS.md.

**Not done**: a portrait of a target (WoW's target frame), buff and debuff icons under the frame, a rested or combat glow, numbers alongside the
percentages on hover, cooldown sweeps on the bar's slots.

## Update 2026-10-15: the HUD steps aside for the C menu

Owner: "make the hotbar and portrait in the top left fade away when you open the c menu". While the C menu or any page it opens is up (the
Player fly-out's Loadout, Stats, Skills and Items, Quests and Social, the map), the portrait frame (`StatusFrame`, top left) and the hotbar
(bottom left) fade out over 0.2 s and come back the same way when it shuts (`UI/Hud/MenuHudFade.cs`, added by `PlayerHud.Awake` like the hotbar,
so no scene rebuild: the frame is found by name and given a CanvasGroup; the hotbar reads the same visibility into its own, which already hid
it for the chat). Real time, so a slowed game cannot slow the fade. Test: `MenuHudFadePlayTests` (half-faded mid-way, gone, back).

ReDraw counts too (owner, 2026-10-17: "the portrait in the top left as well as the hotbar skills should go away on this redraw menu"): it is
reached from the C menu after that has closed, so `MenuHudFade.AnyMenuOpen` also asks `PlayerRedrawStarter.Active`, and while ReDraw is up
the crosshair fades too (`CrosshairVisibility`, a CanvasGroup on `PlayerHud.Crosshair`). Test: `RedrawRealInputPlayTests`.

## The crosshair is a dot (2026-10-22)

Owner: "change the crosshair to a white transparent dot". The white plus (two 16 x 2 bars) is one round dot, 5 design pixels across (about 8
on a 1080p screen), white at 55% (`GunReticleView.NormalColour`, which colours it every frame; 20% while reloading, red when the barrel is
blocked, the hit marker's ticks round it as before). Its picture is a 32-pixel circle with mipmaps (`CircleSprite.Dot`,
`UI/CrosshairDotTexture.asset`): the portrait's 256-pixel circle has none to shrink that far with. Built by `PlayerHudBuilder` with the
scene. Test: `PlayerHudTests.TheCrosshairIsASmallWhiteDotYouCanSeeThrough`.

## Menus hold the keys (2026-11-05)

Every menu that takes the keys (chat, the console, the C menu and its pages, the inventory, the map, the redraw, an item's card, the loot
window, the graffiti menu) holds the body's reader while it is open (`PlayerInputReader.Hold` / `Release`, PlayerInputReader.Holds.cs)
instead of switching it off. Switched off, the reader stopped ticking the body too, so a menu opened in mid-air hung the body there;
two menus did not know of each other, and closing one gave the keys back under the other; and a body whose reader was off counted as
nobody's, so enemies stopped coming for a player reading a menu. Held, the body goes on falling and landing with no keys read; on a wall
or a rope it stays as it is. What asked whether a menu has the keys asks `TakesKeys`. Tests: `MenusHoldTheKeysPlayTests`,
`PlayerChatTests.ClosingOneMenuLeavesTheKeysWithTheOtherStillOpen`.

The Escape that closes a page is not also a pause (2026-11-05): the pause menu leaves Escape alone while a menu holds the keys
(`PlayerInputReader.AnyHeld`) and in the frame one gives them back (`KeysGivenBackOnFrame`), whichever of the two read the press first.
A menu destroyed while open lets go of the keys by itself. Test: `EscapeClosesOnlyTheMenuPlayTests`.
