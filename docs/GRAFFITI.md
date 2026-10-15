# Graffiti: the spray can

Owner, 2026-10-15: *"add a graffiti spray paint can item ... go up to walls and right click with it equip in hand and it opens some free draw
menu that just lets you adjust color and brush size ... have the drawing stay on that wall and visible to other players online. it doesnt need
to stay after log off right now ... but plan for that in the future. make the light bounce off properly ingame as if it was apart of the wall
texture."*

## How it plays

1. `/equip spraycan` (or `spray`, `graffiti`). The can is in the right hand, held like the bottle; its cap shows the colour you last used.
2. Look at a wall within about 3 m and **right-click**. The graffiti canvas opens: the wall in front of you, photographed straight on, 2.6 m
   across (about a storey), with any graffiti already there on it.
3. **Drag** on the wall to spray (the can hisses). Pick the colour on the square and the hue strip (the character creator's controls), and
   the brush with the - and + buttons or the `[` and `]` keys (1 is a fine line, 24 a fat fill).
4. **DONE** (or Enter) puts it on the wall. **CANCEL** (or Esc) leaves the wall as it was. **CLEAR** wipes the canvas.
5. Right-click the same spot again to carry the same piece on, rather than start a second one beside it.

A right-click at the sky, the floor, a roof, a person or anything out of reach opens nothing.

**Paint sticks to the wall only.** When the canvas opens, 16,384 rays (one every 2 cm, `GraffitiWall.WallMask`, through the job system,
a millisecond or two) find the wall's own face across the square. Where the face is not there, the canvas is **hatched** and paint does not
stick: past the wall's corner, on a pillar, pipe or sign standing out of the wall (the piece would be hidden behind it), in a window's hole,
or off the edge of a curved wall the flat piece would stand away from. People and loose things in front of the wall, the painter's own
arm included, are looked past. Only the paint that stuck is sent to other players.

## What other games do (research, 2026-10-15)

| Game | How it works | What we took |
|---|---|---|
| **Source engine sprays** (Half-Life 2 deathmatch, Team Fortress 2, Counter-Strike: Source) | each player has a small custom image; the server sends it to everyone else; it is projected onto the wall as a decal, which is lit by the wall's own lighting | the image travels from player to server to everyone; lit like the wall |
| **Counter-Strike: Global Offensive / 2 graffiti** | ready-made designs from items, placed as decals | (not freehand: not what was asked) |
| **Rust signs and canvases** | freehand painting in a paint screen with brushes and colours; the server keeps the picture and sends it to players who come near; a sign keeps its picture | the paint screen, and the server keeping every piece and sending it to whoever joins |
| **Splatoon** | ink painted into textures laid over the level, lit by the scene | paint in a texture of its own, lit |
| **Unity's URP Decal Projector** | a box that projects a texture onto whatever is inside it; the decal changes the surface before lighting | considered, not used: see below |

### Why a lit panel and not a decal projector

A piece is a flat square laid **1.2 cm proud of the wall**, drawn with URP's **Lit** shader (see-through where there is no paint). The wall
and the panel have the same normal, so the sun, the street lights, the room lights and the shadows fall on the paint exactly as on the wall
under it; the paint has a little sheen of its own (smoothness 0.28), as spray paint does.

A URP Decal Projector would need the decal renderer feature switched on for the whole game (an extra pass every frame, for every camera),
and it paints everything inside its box: a player walking past the wall would get the graffiti printed on their back. On a flat wall the
panel looks the same and costs one draw.

## Numbers

- A piece: 2.6 m square, its paint 256 x 256 pixels (1 cm a pixel), `GraffitiWall.PieceSize`.
- Its paint is sent and kept as raw RGBA, gzipped: a simple tag is a few KB; the most allowed is 96 KB (`GraffitiCanvas.MaxEncodedBytes`).
- On the graphics card a piece is a DXT5 texture with mipmaps, 87 KB, and keeps no copy in memory (`GraffitiPanel.Upload`). One draw each.
- The brush: soft-edged puffs of paint, a third of the brush apart along a stroke, each covering what is under it (`GraffitiCanvas.Spray`).

## Multiplayer

`GraffitiNetwork` (on the city streamer, beside the light switches' `RoomLightsNetwork`): FishNet **broadcasts**, not RPCs on a body, so a
piece painted across the city is there when you get there.

- DONE puts the piece up on your own machine at once and sends it to the server.
- The server checks it: a sane piece (`GraffitiWall.IsSane`: finite numbers, a real wall direction, 0.5 to 6 m, paint at most 96 KB),
  the painter's own body within 12 m of it (`MaxReachMetres`), and no more than one piece a second from one painter (`MinSecondsBetween`).
  It keeps it and sends it to every player.
- A player who joins is sent every piece already up.
- The same wall spot has the same id on every machine (`GraffitiWall.IdFor`), so repainting a piece replaces it everywhere.
- The can in your hand is a held item like the others: other players see it (`ICosmeticMountable`).

## Keeping it after log-off (planned, not built)

Everything needed to save a piece is already in one plain record, `GraffitiPiece` (id, centre, wall direction, size, paint bytes):

- **Save**: write `GraffitiWall.All()` out; on a server, per city chunk, so a chunk's pieces load with it.
- **Load**: `GraffitiWall.Put` each piece read back; the server then sends them to whoever joins, as now.
- A piece is pinned to a wall spot by position. The city is a pure function of the world seed, so the wall is there again after a restart.
  **A seed reroll moves every wall**: saved graffiti must be thrown away with the old seed (store the seed with the save).
- Worth adding then: who painted it, when, and a limit per player.

## Code

| What | Where |
|---|---|
| The paint (pure: spray, stroke, the wall mask, encode/decode) | `Character/GraffitiCanvas.cs` |
| The pieces on this machine, ids, the checks, where the wall is (`WallMask`) | `Character/GraffitiWall.cs` |
| One piece on a wall | `Character/GraffitiPanel.cs` |
| Multiplayer | `Character/GraffitiNetwork.cs` |
| The can | `Weapons/SprayCanItem.cs`, model by `Weapons/Editor/SprayCanBuilder.cs` (also `GraffitiPaint.mat`) |
| Right-click with the can | `PlayerInputReader.SprayRequested` |
| The canvas screen | `UI/GraffitiMenuView.cs` (built in code by the HUD) |
| Tests | `GraffitiTests` (EditMode), `GraffitiPlayTests`, `NetworkedGraffitiPlayTests`; render: `GraffitiCapture` (with `-Graphics`) |
