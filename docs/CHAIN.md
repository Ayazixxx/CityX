# The chain (2026-11-22)

Owner, with a video of four games' chains and whips (`Downloads/chainmovesetbuilder.mp4`: Delsin's chain in inFamous Second Son 0-14.6 s,
Dark Souls whip 14.6-22 s, Hunter x Hunter's mid-air swing 22-24 s, a downward whip at a body 24-26.5 s): a one-handed chain "like delsin's",
two alternating diagonal swings starting right to left, a swing in the air, a downward one onto a body on the ground, worn wrapped round the
wrist "and down to around the palm when he is just chilling", and "the chain can appear shorter [in] your hand and then extend really longer
for the attacks". `/equip chain`. Rare (my placement). A branch of One-Handed Melee (docs/GAME_DESIGN.md).

## How it plays

| | |
|---|---|
| Left-click | The lash (from over the right shoulder down to the lower left), then the backhand (its mirror), back and forth for as long as the clicks come. No finisher. |
| In the air | The swing brought straight down out of the air (Hunter x Hunter's). |
| Somebody lying ahead | Limp or dead, within 3.2 m, and nobody standing there: the swing whipped down onto them (GTA's corpse attacks). A standing enemy is swung at as ever. |
| Held click / right-click | Nothing yet: a chain has no heavy and no guard (my choice; the owner has not asked for either). |
| Reach | 1.6 x a sword's, the longest melee weapon. Damage 10 x 0.95 a lash, 10 x 1.2 in the air, 10 x 1.5 onto the ground (my numbers, `SoulsSwordCombat.Chain*`, `ChainRules`). |

The body walks and stands as an empty-handed one does while it is worn.

## How it looks

- **Worn:** wound two and a half turns round the forearm above the wrist, a loose end ring at the top, and on down round the hand, slanting
  above the thumb, ending in ONE straight loop round the palm under the thumb (owner, shown one and two: "two is too much") (`ChainWrap`).
- **Out:** the first click unwinds the hand's coil (far end first) and pays the rest out through the fist, 3 m in all; 2.5 s after the last
  swing it is drawn back in and wound on again (`ChainWeapon`). The free length is a simple chain simulation (`ChainLash`): a swing pulls
  each link toward the line the attack throws it along, the far links toward where the line was a moment earlier, so it bows back like a whip;
  otherwise it hangs, falls, keeps its length and lies on the ground. Steel meeting the ground fast throws sparks (`BlockDeflectEffect.Scrape`,
  a hard blow `GroundStrike` at 70%: owner, "lower it by 30%").
- **Links:** long ovals of round bar, each turned a quarter turn from the last (between the owner's two photographs), one skinned mesh with a
  bone a link (`ChainBuilder`), 4,400 triangles.
- **The body's moves** are full-body Blender clips with both arms (`SourceArt/Chain/chain_pose.py`, `chain_build.py` ->
  `Resources/Chain/ChainMotion.json`), on the punches' key format. On the backhand the other arm stays before the body, its elbow bent forward
  (owner: "just have it maybe elbow bent a bit but forward"), and the step is short and the hips turn less, so a long coat's back does not
  show the leg through it.

## With the Fire Enhancement

After many rounds of pictures the owner settled it by playing (2026-11-22): no flames, no haze, no sparks thrown about.
- The links go white-hot (their own glowing material) with three bands of light drawn along them, white on the steel, orange, red outside,
  ON the chain, not trailing behind it ("look at delsin's his is like white in the center", "less spread than his more around the chain").
- It is a light: the WeaponAblaze light at the chain, 5.5 m, by night as by day ("make it an actual light source").
- No white streak; instead a trail of embers laid evenly over everything the chain sweeps, each burning about as long as a swing
  ("make the trail last like almost the length of the swing") (`FireLook.Trail.cs`).

## Sounds

- Swings: the owner's four recordings (`Resources/Chain/ChainSwing1-4.ogg`; the wind cut from the first two and the dead air from the fourth's
  start by `SourceArt/Chain/chain_sounds.py`), never the same twice running, started so the loudest moment is the blow (`ChainSounds`).
- Burning: `ChainSwingHot1.ogg` on each swing (more to come: a file and a line in `ChainSounds.Hot`), `ChainIgnite.ogg` as the
  enhancement takes the chain.

## Other players

Everything shown is worked out on the body it is on from what that body does (the attack's name is sent as every swing's is), so a copy's chain
comes off, swings, burns and winds on with nothing else sent (`ICosmeticMountable`, `NetworkedPlayerWiring`). Its script keeps running on the copy (`ICosmeticKeepsRunning`): until 2026-11-26 the copy switched it off with the rest of a copy's item scripts, and other players saw it stay wound on the wrist through every swing.

## Tests

`ChainTests` (the four attacks and their clips, which way each throws the chain, the two lashes, no heavy or guard, the sounds, the coils, the
chain simulation, the built chain), `ChainPlayTests` (worn, thrown and lashed past a blade's reach, wound back on, the air swing, the swing onto
a body, the hot chain's light, another player's copy), `CoatLegsPlayTests` now swings it too. Pictures: `ChainCapture` (`chain-*.png`).

## Not done

- A heavy attack, a guard, the running attack; balance untested by play beyond the owner's first go.
- The game's fire elsewhere is to be redone (owner: "our games fire looks terrible i think we need a total revamp on the fire").
