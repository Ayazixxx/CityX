# The character list on the conveyor belt

Owner, 2026-11-11: "build the conveyer belt character select", on top of the conveyor belt a character is made on (docs/BELT_INTRO.md),
and, earlier the same day: "make it so a create new button is on screen always and when you press it, instead of the press d or > empty
screen popping starting, it just rolls in an empty character to the center of the screen for you and you start the customizing"; "make it
so i can still boot from the first start scene where you control the conveyer belt somehow (for test purposes) because that is meant to be
the first boot screen when no other characters have been made yet". How choosing a character works was left to me ("you decide how the
select character should work"). It replaced the five pedestals in the night city (2026-10-26, kept in git).

## What it is

- **The row.** SINGLEPLAYER (or joining a server) opens the creator's own belt in its cloud-grey sky, seen from where the creator sees it,
  with the player's characters standing on it in a row, most recently played first, in the middle. Each
  one stands as it was saved: painted, haired, dressed (no weapon in hand yet), idling at the title's pace (`TitleHero.IdleSpeed`), each
  from its own point in the clip so the row does not breathe as one, the soles held on one spot of the belt (`PlantedFeet`). The one beside
  the middle is in sight on each side, near the edge; the ones past them are not. They stand about 4.7 m apart on a 16:9 screen, fitted to
  the screen's shape (`CharacterSelect.RowSpacing`): midway between the edge of the row's view and the edge of the closed-in view beside
  the card, so the next one is wholly in sight in the row and wholly out of it beside the card (in the first pictures it stood in the
  card's way, the bin on its head). The chosen one's name and level float over its head while the belt
  stands still on it.
- **Rolling.** D, the right arrow, a controller's stick or pad right, or the arrow key cap at the right edge, rolls the next one in from the
  right (the belt runs left), as the pedestals did (owner, 2026-10-26: the next one on the right). A / the left arrow the other way. The belt
  speeds up, runs and slows to set the next one down in the middle exactly, about a second a character; held, it rolls on from one to the
  next; it stops at the ends of the row. Clicking a character beside the middle rolls it in. The belt is heard as it runs, with the
  creator's sound (`BeltIntro.Sound`).
- **Choosing.** Clicking the middle one, or Enter (a controller's A), closes the view in on it over 0.45 s (`CharacterSelect.InspectView`):
  it stands large on the left, and its card is on the right: name, level and play time, stats, the little bin to delete it, PLAY (the green
  arrow) and BACK (the red curl). Enter again plays. BACK, Escape or a controller's B draws the view back to the row; from the row they go
  to the menu.
- **Deleting.** The bin asks first (the old KEEP / DELETE check). Deleted, the character goes from the belt and its place stays empty
  while the screen is up (on a belt, a gap closes only by everything on it moving, which would make everyone jump); the belt rolls the next
  one (or else the one before) to the middle. Opened again, the row is closed up.
- **CREATE NEW** is in the top right corner whatever else is up (the gold arrow). Pressed, the view draws back if it had closed in, and
  once the belt stands still the creator takes the belt over (`TitleScreen.BringNewCharacter`, `BeltIntro.Bring`): it runs right by
  itself at 5 m/s (`BeltIntro.BringSpeed`), the row riding off the screen's right, and a plain white body comes in behind the leftmost
  character in sight, far enough behind (`BeltIntro.EdgeOfScreen + BeyondTheEdge`) that the whole row is off the screen by the time the body
  is set down in the middle. No key, no prompt; about two and a half seconds, a little over three with a character in sight on the left. The characters
  out of sight on the left are taken away first: they would come through the middle ahead of the new body. Then the creator is exactly as
  after rolling a body in by hand: the tools fade in, and so on to the end of the belt (docs/BELT_INTRO.md). BACK out of the creator stands
  the row again.
- **The first boot** is unchanged: with no character made, the title opens on the empty belt with its key prompt (`BeltOnFirstStart`). To
  try it with characters made: **F8** on the title (in the editor and development builds), or **CityX > Play Title As First Start** in the
  editor's menu (`TitleFirstStartMenu`: it opens the title scene and plays it, with a one-time request in PlayerPrefs that
  `TitleScreen.OnEnable` takes).

## How it is put together

- **One stage, two panels.** The row stands on the creator's stage (its belt, sky, lights and camera): `CharacterSelectBuilder` adds a
  `BeltLineup` to it and gives the character list its own `CreatorStage` relay to the same stage. `CreatorStage` counts the panels using a
  stage and keeps it up while either is, and `TitleScreen.Show` puts the new panel up before it takes the old one down, so between the list
  and the creator the stage never goes down and comes up again (its belt and bodies would start over). The list's relay comes first on its
  panel: a figure made under a stage still down would wake later, after it was stripped, and start the sounds its strip took away.
- **Who runs the belt.** On the list, `CharacterSelect` (its belt half in `CharacterSelect.Belt.cs`) steps the pure `BeltLine` and adds
  what it runs to `ConveyorBelt.Travel`; in the creator, `BeltIntro` does (from the list's travel on, `_travelBase`). The figures are placed
  from `ConveyorBelt.Travel` alone (`BeltLineup.Along`: slot × spacing + travel − `Origin`), so they ride whichever is running it.
- **The creator's own body** waits off the screen's left on the list (`BeltIntro.Empty`), alive, unseen; the creator itself (`Creator.enabled`)
  is off, so it neither turns, zooms nor paints. `BeltIntro.Arrive` clears the row once the new body is in the middle.
- **Figures** are a player's body stripped to one that only stands and idles (`BeltLineup.StripWorldComponents`, the old pedestals' list),
  painted and dressed as the pedestals were (`ApplyCosmetics`, `PutOnClothes`), on the stage's layer, with a `LineupFigure` for the idle and
  the feet. Those in sight are made when the row opens; the rest one a frame, nearest first, so a long row does not stall.
- **Clicking a figure** is a box from its feet to the top of its head, 1.1 m wide, on the screen (`BeltLineup.SlotUnder`): the figures have no
  colliders.
- **Around it:** no vignette and no rain over the belt (`TitleAmbience` mood Silent), and the title's city camera draws nothing, as for the
  creator.

## Not built yet

- The weapon in hand on the row (the rooftop's character holds theirs).
- Rolling while closed in on one.
- PLAY for a character already made loads the world at once, as before: no ride off the belt.

## Tests

`BeltLineTests` (the rolling: set down exactly, never past, top speed, turned back, the ends), `BeltLineupPlayTests` (a figure on the stage's
layer, alive with its arms down and its soles still, silent, nothing to collide or save; clothes; hair colour; the next on the right and the
row riding the belt; an emptied slot), `BeltSelectPlayTests` (the real title scene with real keys and pointer: the row with the chosen one in
the middle; D rolling the next in with the belt heard; held to the last; Enter's card and Escape back; clicking; CREATE NEW bringing a body
in behind the row with nobody between it and the middle and the tools up; CREATE NEW from the card; deleting; back to the menu; back out of
the creator; the first-start request and F8), `TitleScreenWiringTests.TheCharacterListStandsOnTheCreatorsBeltWithCreateNewAlwaysUp`,
`TitleFlowPlayTests`. Pictures: `CharacterSelectCapture` (Explicit, `-Graphics`: `Logs/CityShots/select-NN-*.png`).
