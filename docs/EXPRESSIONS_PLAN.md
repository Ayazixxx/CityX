# Character creator modes and drawn expressions: the plan

Owner, 2026-11-21: "I want to plan out two modes for character customization. Simple and Advanced. Simple (or if you can come up with a better
word) will look more like what we have now. in advanced, you will be able to customize expressions. so you will be able to make how the eyes
look when closed for a blink effect, or maybe a taking damage face or a attacking face like an angry expression, and maybe a dead face. that
was the faces can be animated simply by the player ... it will just be extra stuff later for the people who love customizing their character".

**Nothing here is built.** It is for later, after the roadmap's current items. Owner's decisions are marked **(owner)**; everything else is my
proposal and stays open until the owner says so.

## The two modes

- **(owner)** Two modes in the creator. The first looks like the creator now (DRAW, HAIR, FACE tabs). The second adds expressions.
- **(owner)** The second is for players who love customizing; nobody has to touch it.
- Proposal: one switch at the top of the creator's left column; switching back never loses anything drawn in the other mode. A character made
  in the simple mode simply has no expressions, and plays with the one face it has (as every character does today).
- Names (owner asked for a better word than "Simple"): see the chat; not decided.

## Expressions

- **(owner)** The player draws their own face for each expression. Named so far: **eyes closed (blink)**, **taking damage**, **attacking (angry)**,
  **dead**. The owner said "maybe" to some of these: the list is the owner's to settle.
- Proposal, drawing one: picking an expression copies the character's normal face onto the head as a starting point, with the normal face shown
  faintly underneath to line things up (onion skin, as animation tools do). Only the head is drawn on in this tab, with the same brush, bucket,
  eyedropper, mirror and undo as DRAW. A PLAY button next to each shows it in motion on the body in the creator (a blink, a flinch, a swing).
- Proposal, an expression never drawn falls back to the normal face, so a half-finished set still plays correctly.
- Proposal, the premade face parts planned for the FACE tab (docs/FACE_PARTS_PLAN.md) would later come with their own expression versions, so
  players who pick parts instead of drawing get expressions too. Not needed for the first version.

## When each one shows in the game (proposal)

All from things every player's copy of a body already knows, so other players see the expressions with nothing extra sent while playing:

| Expression | Shows | For |
|---|---|---|
| Eyes closed | a blink every few seconds, at random | about a tenth of a second |
| Taking damage | when hit (the hit reactions every copy plays) | about half a second |
| Attacking | while a melee swing or a spell cast is under way | the swing |
| Dead | while dead or knocked out (the ragdoll) | until up again |

If two apply at once, the stronger one wins: dead over everything, then taking damage, then attacking, then blinking.

### Blinking (proposal; owner, 2026-11-21: "we would have to plan out how blinking would work and what faces would be swapped to when")

- The face snaps to the eyes-closed picture and back, with no in-between frames: a drawn 2D face reads best as a cartoon swap.
- How often: a wait of 2 to 6 seconds, picked at random each time, then closed for about 0.12 s. About one blink in five is a double blink (two
  quick ones 0.2 s apart), which reads as alive rather than mechanical.
- Every body picks its own waits, so a crowd never blinks together. Nothing about blinks is sent between players: each player's game blinks
  every body it shows on its own timer. A blink is too short for anyone to notice it is not in step.
- No blinking while another expression shows (the angry or hurt face would otherwise flash to the normal closed eyes). The next blink waits
  until the face is back to normal.
- Not drawn: no blinking at all (the eyes just stay as painted), so a character without the picture never flickers.

### What swaps to what, and when (proposal)

The face is always exactly one picture. Each frame it is the strongest of these that applies:

1. **Dead**: from the moment of death or a knockout (the ragdoll) until the body is up again or respawns. Stays through the whole fall.
2. **Taking damage**: from each hit the body takes, for 0.5 s; another hit in that time starts the 0.5 s again. Shown even mid-swing.
3. **Attacking**: from the start of a melee wind-up to the end of its follow-through, and while a spell is cast. Ends as soon as the swing does.
4. **Eyes closed**: the blink, only when nothing above applies.
5. **Normal**: the face from the DRAW tab, the rest of the time.

Every swap is instant, both ways. An expression not drawn is skipped and the next one down shows, so a character with only a dead face drawn
shows it when dead and its normal face otherwise.

Still the owner's to say: whether aiming or firing a gun counts as attacking; whether the hurt face should also show while stunned or
staggered; how long the hurt face stays.

## How it works underneath (proposal)

- The head is its own mesh with its own renderer (`HeadMesh`), painted from row 0 of the paint atlas (768 x 256 texels). Each expression is one
  more head picture of that size, saved with the character next to the paint. Switching expression is swapping which picture the head
  draws, on the head alone: no repainting, cheap enough for every player on screen.
- Size: a drawn head picture compresses to tens of kilobytes; four expressions add well under the 200 KB a busy painted body already sends
  once when a player appears. Sent once with the rest of the look (`NetworkedPlayerIdentity`), never during play.
- Old characters load with no expressions. ReDraw in game gets the same tab, so expressions can be changed without a new character.
- Tests: a never-drawn expression shows the normal face; each trigger shows its expression on the player and on another player's copy
  (`NetworkedPlayerStateSyncPlayTests`); the saved size stays small; captures of each expression on the body.

## Open questions for the owner

1. The names of the two modes.
2. The first set of expressions: the four above, or fewer to start?
3. Should a player be able to add more expressions later (for emotes, for example), or a fixed set?
4. Whole head or face only: may an expression change the whole head (for example, a different look at the back), or only the front?
5. Blink: random as proposed, or should the player set how often?
6. The swap rules above: does the order (dead, hurt, attacking, blink) feel right, and which actions count as attacking?
