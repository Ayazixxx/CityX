# Controller (plan, 2026-11-23)

Owner: "id like to have controller integration", "how would the hotbar spells works on controller", "yeah thats what i was thinking like you
hold r1 or l1 and then it unlocks some sort of mechanism where you can select from there". PC first; consoles only if the game does well on PC.

**Status (2026-11-25, owner: "make it playable on controller"): first pass built**: walking, the camera, fighting, the hotbar, using things
in the world, driving and the menus answer a controller, as laid out below. Not yet: the character creator's cursor, button prompts in the
controller's buttons, aim assist, the lock-on's flick to the next target, slots 9 and 0's chord being the owner's call. Button names below are
Xbox / PlayStation.

How it is built: `Character/Player/GameControls` is the one place the game asks for its controls ("jump went down", "the stick says
forward"); it answers from the keyboard and mouse exactly as they were read before, or from a controller, whichever is used.
`PadLayout` is what a button means when it means more than one thing (a shoulder held, B tapped or held, X held, L3), pure and tested.
`UI/Menus/PadMenuSelection` chooses a menu's first button while a controller is in use, so the D-pad and A work in every uGUI menu.
Where the plan below and the build differ: B tapped dashes on the let-go (a tap is only known then), and driving uses the triggers too
(RT on, LT back, A the handbrake).

## The spells: hold a shoulder, press a face button

- **Hold LB / L1**: the left half of the hotbar comes up on screen as a cross of four slots (slots 1 to 4). **A, B, X, Y / Cross, Circle, Square,
  Triangle** cast them. Let go and the cross goes.
- **Hold RB / R1**: the right half, slots 5 to 8, the same way.
- **Hold both**: slots 9 and 0, on A and B. (Or the controller bar stays at 8: the owner's call.)
- The left stick keeps moving the body all the time; nothing about casting stops you.
- **Aimed spells** (Glacier Spikes, Octaslash): the shoulder and face button ready it as the number key does now, the right stick aims, RT casts.
- **Drinks** on the hotbar work the same: hold the shoulder, press the button, the drink comes out and is drunk.
- Which spells sit in which slots is set in the skill menu, as now (it already moves with a controller).

## Everything else

| Stick / button | On foot | Note |
|---|---|---|
| Left stick | Move | push hard to run |
| Left stick click (L3) | Sprint (toggle) | |
| Right stick | Camera | |
| Right stick click (R3) | Lock on (Tab) | flick the right stick to change target |
| RT / R2 | Attack, shoot (left click) | held: heavy attack, as holding the click |
| LT / L2 | Aim, block (right click) | |
| A / Cross | Jump (Space) | held in the air: parachute, jetpack |
| B / Circle | Dash (Shift tap) | |
| B / Circle held | Crouch (Ctrl) | also: descend when flying, drop off a ladder |
| X / Square | Interact (E) | doors, seats, cars, light switches, people |
| X / Square held | Reload (R) | |
| Y / Triangle | Swap weapon (scroll wheel) | primary and secondary |
| D-pad up | Shoulder swap (Q) | |
| D-pad down | Torch / flashlight (F) | |
| D-pad left (held) | Push to talk (V) | |
| D-pad right | Inventory (I) | |
| D-pad down on an item in reach | Item card (F on an item) | the torch only when there is no item in reach, as F now |
| View / touchpad press | The C menu: Quests, Player (skills, loadout, stats, items), Social, Map | the hub, where most console games put theirs |
| Menu / Options | Pause (Esc) | |

Emotes are chat commands now (/pushups); on a controller they can go on a wheel later.

## Menus

Every menu moves with the left stick or D-pad, A to choose, B to go back. The skill menu, the belt character select and the intro already
do; the inventory, the map, the player menu, the loot screen and the pause menu need it; the character creator gets a cursor (below).

## The character creator

Owner, 2026-11-23: "make the controller work in the character creator as well". The creator is mostly pointing: painting the body on the PAINT
tab, the colour pickers, the hair and face tiles, the sliders. So on a controller it gets a **cursor**, a pointer the left stick moves (slower
while a button is held, for fine painting), with:

| Stick / button | In the creator |
|---|---|
| Left stick | Move the cursor (the brush on the PAINT tab) |
| A / Cross | Click; held, paint or drag a slider (the left mouse button) |
| Right stick | Turn the character left and right, tilt the view (the drag beside the body) |
| LT / RT (L2 / R2) | Zoom out / in (the scroll wheel) |
| LB / RB (L1 / R1) | The tab to the left / right: PAINT, HAIR, FACE |
| X / Square | The paint bucket / the brush (the tool column's two) |
| Y / Triangle | Undo |
| D-pad left / right | Brush size smaller / bigger (the [ and ] keys) |
| B / Circle | Back (Esc) |
| Menu / Options | Done, play (Enter) |

Typed colour codes open the console's on-screen keyboard. The belt (rolling characters in and off) already moves with a controller.

## How it is built

The game reads the keyboard and mouse directly in about 44 places. They all change to ask one shared input layer ("is jump pressed?"), which
answers from the keyboard and mouse or the controller, whichever was touched last. The on-screen button prompts (E to open, R to take all)
show the controller's buttons when one is in use. Everything stays as it is on keyboard and mouse.

Order: the shared input layer with the keyboard and mouse exactly as now (tests prove nothing changed), then walking, fighting and the
camera on the controller, then the spells, then the menus, then the prompts.

## Open for the owner

1. The layout above: any button to move?
2. Slots 9 and 0: both shoulders held together, or a controller bar of 8?
3. ~~Aim assist~~ decided (owner, 2026-11-23: "sure there can be slight aim assist that you can turn off in settings"): a slight pull toward
   a target near the crosshair, controller only, with an on/off switch in Settings (on by default).
4. ~~Rumble~~ decided (owner: "no rumble"): none.
