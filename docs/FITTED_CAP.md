# The fitted cap (2026-11-24)

Owner, 2026-11-24, with a picture of a fitted cap seen from below, its crown circled in red and its brim outlined: "make a basic fitted cap
model we can make a bunch of colorways for use this links as guide to help you build it but make it fitted to our playermodel's round head like
the red circle image i attached" (the guide: a set of fitted baseball caps on Sketchfab).

Item per colourway (`Items/FittedCapColourways`): the first is `black_fitted_cap`, "Black Fitted Cap", on the head (the helmet slot), Common.
`/spawn cap` (the first), `/spawn blackcap`.

## The model

Made in code (`Wearables/Editor/FittedCapBuilder`), about the head's centre (the Head bone's pivot), 2,248 triangles:

- **A round crown**: the head is a ball, and seen from above the crown is a circle, as the owner drew it. Six panels hug the head 1.2 cm off
  it, each puffed out 7 mm at its middle so the outline draws the seams between them. The band sits above the eyes in front (10 cm above
  the head's centre) and low behind (5 cm below it), as a cap is worn.
- **The brim** out in front, as wide as the crown where it meets it: its sides run straight forward and come round at the front (27 cm out
  at the middle), sloping down a little and its sides a little more, as far down as it reaches out (so it does not hang at the sides). 9 mm
  thick, its underside in its own colour.
- **A button** on top.
- The hair is hidden under it, as under the Farmer Hat (every style but the shortest came through a crown that hugs the head).

All the measures are named at the top of the builder.

## A new colourway

One line in `FittedCapColourways.All`: an id, a name, a console word, and three colours (the crown and the brim's top, the brim's underside,
the button). Then rebuild the scene and the title (the builder, the item list, the console and every body's item prefabs read the list).

## Tests

`FittedCapTests` (every colourway an item on the head with its prefab and its three colours; the crown round from above and just off the
head everywhere; the band above the eyes and lower behind; the brim out in front). Pictures: `FittedCapCapture` (`fittedcap-*.png`: front,
three-quarters, side, from above behind, straight down, close, and with every hairstyle).
