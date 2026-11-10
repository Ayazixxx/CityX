# Cel shading

Owner, 2026-10-24: "I want to make my game cel shaded like jet set radio with thick black lines", "players and buildings and terrain,
everything". After the first renders: the lines "look okay not too thin" (kept at their first width).

The whole game is cel shaded, on from the first frame: the light on every lit surface is flat tones with hard edges, and thick black lines
are drawn round everything and along every sharp fold. `/celshade` turns it off and on (`/celshade off`, `/celshade on`); `/outlines <pixels>`
sets the lines' width.

## What the research says

- **Jet Set Radio** (Dreamcast, 2000) made cel shading famous: flat colours, a single shadow tone, black outlines on the characters. The
  look ages well because of the hard borders round the figures and the plain textures (Game Informer's retrospective). A developer of the
  later port describes the outlines as copies of the model drawn black round it.
- **Bomb Rush Cyberfunk** (Unity, the modern Jet Set Radio) is said to draw its lines as a post-process from the depth buffer, edge detection
  like Sobel, over low-poly models (a foro3d write-up; not confirmed by the developers).
- **Outline methods** (Alexander Ameye, "5 ways to draw an outline"): a black shell round each model (inverted hull) splits at sharp corners,
  which a city of boxes is all corners; edge detection over the finished frame's depth and normals needs nothing per model and holds at
  corners. Its known artefacts and fixes: false lines on surfaces seen at a grazing angle (fixed below by measuring the depth so that any flat
  surface is clean, at any angle) and depth steps that grow with distance (measured against the depth there).

## How it works

All of it is one global shader keyword, `CITYX_TOON`, and a few global numbers (`View/CelShading.cs`). Nothing per material, so it reaches
everything, including whatever is made later. With the keyword off every surface draws exactly as before, pixel for pixel
(`CelShadingPlayTests.WithTheLookOffCityXLitIsUrpsLit`).

**The light** (`View/Shaders/CityXToonLighting.hlsl`), on every lit surface:
- **The sun or the moon** lights a surface fully or not at all: the lit side a flat 0.8 of the light, the shade side 0.08, split where the
  surface turns away from it (N.L 0.12) or falls in shadow, the edge a pixel or two wide. The sky's ambient lights both sides as before.
- **A lamp** (a street light, a room light, a flashlight) is a soft area of light. On a body or a prop the side that faces it is one flat
  tone (split at the sun's edge, the sun's 0.8), the side that faces away gets none, and the whole fades with distance as the light does.
  On the walls, roofs and ground it is URP's own soft light: a pool that fades out, and on a wet street a soft reflection. (Until
  2026-11-09 a lamp lit in three flat steps, which drew three rings round every street lamp and every flashlight beam; owner: "it keeps
  reflecting as circles instead of just an area of light like most light sources in video games do".)
- **A shiny surface** (wet in the rain, metal) gets one hard highlight from the sun or the moon, and on a body from a lamp too; no soft sheen.
- The soft dark corners of the screen-space ambient occlusion are left out, and normal maps (the bricks, the paving) do not light the
  surface: a cel surface is flat.

It goes through two shaders, which between them draw every lit surface in the game:
- **`CityX/Lit`** (`View/Shaders/CityXLit.shader`): URP's Lit shader copied whole, its one lighting call going through the cel light. Every
  material that was on URP's Lit (characters, clothes, weapons, props, the ground and pavement, glass, the imported map) moved onto it with
  every property it had. Code makes materials with `CityX.Core.LitShader`, never URP's Lit by name (`CelShadingTests` guards both).
- **`CityX/Facade`**: the walls, roofs and ground of every building, the sun and the moon through the cel light, lamps soft.

**The lines** (`View/Shaders/CityXOutline.shader`, put on both renderers by `View/Editor/OutlineSetup.cs` with the scene build): a full-screen
pass after the opaque world, before the glass and the air, from the depth and the normals the frame already has (the ambient occlusion
already makes them):
- an **outline** where the depth jumps: something in front of something further off. Measured on the inverse of the depth, which on any
  flat surface changes evenly across the screen, so a street seen end on or a wall seen edge on never lights up, only a real step; and
  relative to the depth there, so it holds near and far;
- a **fold** where the surface turns sharply (more than 45 degrees: a building's corner, a kerb, a window's reveal); a body's curves and a
  hill's slope stay clean;
- 2 pixels to each side of the edge on a picture 1080 tall, scaled to the screen; fading out between 70 and 220 m so the far city is not a
  black mesh; under the haze, so far lines melt into the air like everything else.

**Effects stay out of the lines.** A spark, a glint or a streak must be truly see-through: blend factors, no depth written, drawn after
the opaque world (`Character/Combat/SeeThroughMaterial`, as URP's inspector sets a transparent particle material). Given only the
surface-type number, a material still draws as a solid, writes depth and normals, and the lines trace it: the clash sparks and the
enemies' attack glint came out black until 2026-10-24.

## Where it shows

Everywhere a camera draws through the game's renderer: the city, the HUD portrait, the title screen, the character creator and select
screens, and the editor's Scene view (`CelShadingInEditor`). Night stays as dark as before (the lines are black on black there).

## Tests

- `CelShadingTests` (EditMode): no material is left on URP's Lit and no code asks for it; CityX/Lit has every property and pass of URP's Lit;
  the lines' pass is on both renderers after the opaque world with depth and normals; the look is one switch with its numbers set.
- `CelShadingPlayTests` (-Graphics): a white ball lit from the side is two flat tones in a black line with the look on, smooth with no line
  with it off, and with it off CityX/Lit draws exactly as URP's Lit.
- `DevConsoleViewTests.CelShadeAndOutlinesDriveTheCelLook`.
- `CelShadingCapture` (Explicit, -Graphics): the city by day, at sunset and at midnight, from the street, close on the player and from above,
  with the look off and on: `Logs/CityShots/cel-<off|on>-<time>-<view>.png`.

## History

- 2026-08 (Godot): a full-screen posterize pass, rejected ("doesn't look right").
- 2026-08 (Unity): `CityX/ToonLit`, a separate shader swapped onto the opaque materials by `/celshade`; it missed the facades (their own
  shader) and had no lines. Replaced by this and deleted.
