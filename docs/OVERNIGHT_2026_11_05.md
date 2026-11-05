# The night of 2026-11-05: what changed

One commit per job, so any of them can be taken back on its own. Nothing is uploaded to GitHub. All numbers below are my first guesses unless they say otherwise. Tell me which feel wrong.

## The Wraith

Your Meshy model's head and body, your player's arms, a tail that wears away into gas. It floats about until it notices you, then comes up behind you and bites your neck with the recording you gave me, with blood at the neck.

**Try:** `/spawn wraith`, then turn your back on it. Face it and it stops and circles you. Turn away and it comes in.

**My guesses:** the bite does 30 and then 6 about twice a second for 1.9 seconds (about 50 of your 100). It sneaks at 3.2 m/s, and you walk at 4.

**Known rough spot:** the join where its body meets the player's arms has a jagged edge, because the two meshes were cut apart. Tell me if it bothers you.

## The slingshot's string

Right click now only brings it up, with the bands slack and the pebble hanging between the prongs. Holding left click pulls them back to your cheek. The hand waits part way forward while you aim, since the arm can't reach the fork from there.

**Try:** `/equip slingshot`, hold right click, then hold left click as well.

## Character creator zoom

The scroll wheel now zooms toward whatever the mouse is over: head, feet, a hand. Scrolling out always brings the whole body back to the middle. It also comes in closer than before. ReDraw has it too.

**Try:** create a character, put the mouse on the feet and scroll in.

## The boomerang

The Terraria rules, using its real numbers scaled to our body size. It flies straight for half a second, then turns home. Anything it hits on the way out turns it straight back. On the way home it goes through walls and hits everyone it passes. Hits always push away from you. Only one can be out, and your hand is empty until you catch it.

**Try:** `/equip boomerang`, then click. Throw it at a wall, at `/spawn swordsman`, and across a street with a building between you and it on the way home.

**My guesses, not Terraria's:** turning back on a hit going out is from my memory of the game, because the wiki doesn't say. Aiming at what the crosshair is on, cracking a window it hits, and its size (70 cm tip to tip, so you can see it in the air) are mine.

**First go:** the model is a stand-in drawn in code, and the throwing arm is code-posed rather than a Blender clip. Tell me if it looks stiff.

## Snow

A new weather option. Flakes fall and drift on the wind, and none fall under roofs. Snow builds up in patches on whatever faces up, then melts when it stops. Deep snow slows every body by 15% and costs grip. Bodies leave footprints that fill back in after 45 seconds, with a crunch, and their breath shows. In a blizzard the sky greys and the fog closes in. Fire burns weaker in it.

**Try:** `/weather deepsnow` for a blizzard over streets already buried, `/weather snow light` for a dusting, `/weather clear` to let it melt, `/weather dry` to end it at once.

**Also changed:** `/weather` used to change only the machine it was typed on, and a friend had no rain at all. Now rain and snow are the same for every player. Nobody has tried that on two real PCs yet.

**My guesses:** every number. Snow doesn't lie on the characters themselves; say if you want it to.

**Known rough spot:** lane paint under deep snow shows as thin dashed lines, from the cel look's outlines.

## Traffic lights and STOP signs

Every junction in the city is now governed the way American cities do it. Where two main roads meet there are traffic lights. Where a side street meets a main road, the side street has a STOP sign and the main road never stops, except at some crossings that get lights, kept a few blocks apart. Where two side streets cross: lights in the densest core, an all-way stop (a sign on every corner, with an ALL WAY plate) where it is busy, a STOP on one street elsewhere, and nothing on the quietest corners. In your city that is 254 junctions with lights out of 1,834, which is about what a real American city has.

The lights run on real timings: a 90 second cycle on main roads and 60 on side streets, 3 to 4 seconds of yellow, a second or two of all-red, the main road getting the longer green. Lights down one road are offset so a car at the road's speed meets green after green. Walk signals show the white walking figure, then a flashing orange hand, then a steady hand. Late at night the smaller junctions switch to flashing yellow and red, as they do in real towns.

**Try:** you spawn at a set of lights. Stand there for a minute and a half and watch one full cycle. Walk a block or two in any direction to find STOP signs. `/time midnight` shows the flashing.

**Every player sees the same light at the same moment.** Nobody has tried it on two real PCs yet.

**My guesses:** how busy a place must be for lights or an all-way stop. Nothing drives yet, and civilians do not wait for the walk signal: the lights are part of the street, not yet rules for anyone.

**Not done yet:** the other half of what you asked, making the streets and sidewalks themselves feel American. That is its own job.

## The Wraith's glowing red eyes

Its eyes now give off their own red light. They stay bright red in the dark instead of going dim with the rest of it, and the game's bloom spreads a glow off them. You can pick the eyes out from across a street at night.

**Try:** `/time midnight`, then `/spawn wraith` and look it in the face. Then `/time noon` for the day look.

**Tried and dropped:** a red haze breathing round each eye. It showed beside the head but never on the ghost's own white face, so it looked like a mistake, and I took it out rather than keep fiddling. Say if you want the glow bigger or smaller: it is one number.

## Bugs fixed

Each of these was proven with a test that failed before the fix. Each is its own commit, so any one can be undone alone.

- **Poison gas did nothing while you stood in it.** The cloud re-poisons whoever is inside five times a second, and each new dose restarted the half-second countdown to the next hurt, so it never came. You only started losing health once you walked out. Now it hurts from the start: 3 health a second in the gas, and 10 more seconds after you leave, as it was meant to.
- **An empty staff hit harder than a full one.** With no mana and the click held, the Water, Flame, Frost, Arcane Beam and Healing staffs switched on again every few frames as mana trickled back, and every switch-on landed a hit at once. Measured: the Arcane Beam landed about 24 hits a second on an empty bar against 5 on a full one. Tapping the click fast did the same on a full bar, nearly for free. Now a staff that runs dry stays off until it has a second's worth of mana back, then runs in one stretch, and tapping hits no faster than holding. **My guess:** the one second. On the water staff that is a second and a half of waiting.
- **A dead body could stand back up.** If you were killed while knocked down, the knockdown's timer still ran out and the corpse got to its feet. If you were killed while getting up, the corpse finished getting up. And respawning after either left every animation playing at the get-up's fast pace. Now a dead body stays down, one killed while getting up falls again, and a respawn starts clean.
- **A punch landed after you had already been hit out of it.** A punch that was on its way still landed if you were staggered, knocked to the ground or even killed before it connected, and a second punch you had queued up went off in the middle of your stagger. Now getting hit cancels your punch, the same as it already did for a sword swing. This cuts both ways: an enemy's punch is cancelled when you hit him first.
- **Punches and sword cuts went through walls.** Anyone within reach got hit, wall or no wall: through a thin wall, a closed door or a window. Now a blow needs a clear path from your chest to where it lands. Low things (a bench, a car bonnet) do not block it.
- **A heavy punch knocked down someone who had blocked it.** If an Arcane Barrier, a held sword guard or a raised shield took the heavy punch, the body behind it was thrown to the ground anyway. Now only a heavy punch that actually lands knocks down. This is fixed for fights against enemies; between two players on different machines the same thing can still happen, and that is a separate fix.
- **The swordsman's cut carried on after you knocked him down.** If you threw him to the ground mid-swing, his swing paused and then finished, invisibly, once he was back on his feet. Now a knockdown ends his cut.
- **Swapping weapons reloaded your gun for free.** Scrolling to your other weapon and back gave the gun a full magazine and all its spare rounds, instantly. Now a gun keeps the rounds it had when you put it away, and when you drop it and pick it up again, for every player. A gun nobody has held yet still comes loaded. Rounds are not saved between sessions: guns start full when you launch the game.

**Try:** `/spawn swordsman`, breathe poison on him (key 4) and watch his health while he stands in the cloud. Hold a staff's click until the mana is gone and keep holding: it should cut out, wait, then come back in one burst. Fire half a magazine, scroll to another weapon and back, and check the ammo count.

## Stairs and playing with a friend

Both were done earlier today and are already in `git log`. Nobody has tried joining a friend on two real PCs yet.
