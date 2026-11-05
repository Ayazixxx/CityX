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

## Stairs and playing with a friend

Both were done earlier today and are already in `git log`. Nobody has tried joining a friend on two real PCs yet.
