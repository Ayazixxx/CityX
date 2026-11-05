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
- **Playing with a friend stopped your character from saving.** Every player's body on your screen thought it was your character. Each one counted playtime into your save, so with a friend nearby the clock ran double, the server rejected the save as impossible, and it kept rejecting it for the rest of the session. The friend's body also wore your saved clothes on your screen. Now only your own body is your character. This could never show up before today, because a friend could not join at all.
- **Quit To Title threw away up to half a minute of progress.** The game autosaves every 30 seconds and Quit To Title saved nothing, so a level, a skill just bought or points just spent could be gone when you came back. Now it saves first.
- **Redrawing your character healed you and rolled you back.** Confirming a redraw put your whole save back on the body: full health, and your level, points and skills as they were at the last autosave. Now a redraw changes your look and nothing else, and saves it at once.
- **The world remembered the last character.** Lights you switched on, balcony doors you opened and Old Guy's house (lit, planks and windows broken) stayed that way after you quit to the title, so a new character found the tutorial house already open. And if you played alone first and then joined a friend, you saw those changes and they did not. Now every new game starts clean.
- **The jerry can refilled itself.** Same fault as the guns: put an empty can away, take it out, and it was full again. Now it keeps what is left in it, also when dropped.
- **Swapping weapons reloaded your gun for free.** Scrolling to your other weapon and back gave the gun a full magazine and all its spare rounds, instantly. Now a gun keeps the rounds it had when you put it away, and when you drop it and pick it up again, for every player. A gun nobody has held yet still comes loaded. Rounds are not saved between sessions: guns start full when you launch the game.

**Try:** `/spawn swordsman`, breathe poison on him (key 4) and watch his health while he stands in the cloud. Hold a staff's click until the mana is gone and keep holding: it should cut out, wait, then come back in one burst. Fire half a magazine, scroll to another weapon and back, and check the ammo count.

## American streets: rounded corners (first piece)

The kerb now curves round every street corner, as it does on a real American street, instead of meeting at a sharp point. The road fills the corner, and the traffic-light poles and STOP signs stand on the pavement behind the curve. Bigger roads get a wider curve.

**Try:** walk to any junction and look at the corners; the top-down picture I sent shows it best.

**The rest of the streets** came later the same day: see "American streets: lights, concrete and what stands at the kerb" below. No street-name signs, as you said.

**For you:** `CityX/SourceArt/Wraith/WraithAsBuilt.obj` is the Wraith as the game builds it, arms attached, for you to smooth in Blender.

## Checked and found clean

I added a test that puts every item in the game on the real player, holds and releases its click, taps reload and right-click, puts it away, and then taps every spell key. Nothing threw an error.

## Stairs and playing with a friend

Both were done earlier today and are already in `git log`. Nobody has tried joining a friend on two real PCs yet.

## American streets: lights, concrete and what stands at the kerb (late morning)

- **Street lights** stand along every driven street: a tall pole on the pavement with its arm and lamp reaching over the road, alternating sides, about every 40 m. Only about one street in three has power. On those the lamps glow at night and the nearest ones throw a warm orange pool of light on the road. The rest stand dark with grey glass, which keeps the nights mostly black. The one-in-three share is my guess and easy to change.
- **The sidewalk is concrete** now: big flags about 2 m square with dark joints, running square to each street. Before, the joints were a small slab pattern laid by the compass, so on most streets they ran at a slant.
- **Along the kerb:** a bin, a blue mailbox and newspaper boxes at many corners, one fire hydrant per street, bare dead trees in iron grates along about one street in ten (rare, as you asked), and parking meters with white stall marks painted in the road along others. These replace the old random hydrant, bin and cabinet that could stand anywhere across the walk.
- **Red kerb** either side of each hydrant, and no parking stall in it (picture sent).

**Try:** walk any street by day, then type `/time midnight` and walk it again.

**Still to come for the streets:** the gutter strip at the road's edge, yellow bumpy mats at the crossings, manholes and drains.

## More bugs fixed

- **A bow drawn when you got hit fired by itself.** If you were staggered or knocked down mid-draw, the bow stayed drawn and loosed the arrow the moment you got control back, even if you had let go. Now getting hit lets the draw down. Staff charges and the flamethrower stop the same way.
- **A grenade at a Giant's feet did nothing.** The blast was measured to a point twelve metres up the Giant's body, out of range. A player grown with Grow had the same luck. Now the blast reaches a big body at its own height.
- **Grenades and the Giant's slam knocked down people they never hurt.** A player dashing through the blast, or protected by an Arcane Barrier, a sword guard or a shield, took no damage but was still thrown to the ground. Now only those actually hurt are thrown.
- **A Giant knocked out of its leap slammed the instant it stood up**, with no crouch to warn you. Now it stands a moment and crouches again before the next leap.
- **The sword wave flew from a body lying on the ground.** If you were staggered, knocked down or killed in the split second after pressing 7, the crescent still left the blade. Now it doesn't.
- **Most spell kills gave no XP.** Only fists, swords, guns, arrows, grenades and Self Smite paid you for a kill. Kills by the wand's speck, the boomerang, the sword wave, acid rain, the staffs' beams and streams, and fire paid nothing. Now every kill pays whoever struck it, exactly once.

**Try:** `/spawn giant`, throw a grenade at its feet, and watch its health bar.

## Menus (early afternoon)

- **Opening chat or any menu in mid-air froze you in the air** until you closed it. The menus switched the controls off, and with them the only thing that moves the body. Now a menu just holds the keys: you keep falling and land. Two menus open at once also no longer give the keys back too early, and enemies no longer ignore you while you read a menu.
- **Escape on a menu page also paused the game.** Closing the stats, loadout, book, C menu or any other page with Escape opened the pause menu in the same press, sometimes with no cursor. Now Escape closes only the page.

**Try:** jump, open chat in the air, and watch the body land. Open the C menu and press Escape.

## Crowds (performance)

- **The crowd test was broken**: it had been timing an empty street. The networked body it clones has gained parts since October that switched each clone off. Fixed: it measures real crowds again, and has a quick mode for before-and-after checks.
- **Today's numbers on your laptop, in the editor:** 25 players add about 20 ms a frame, and 50 add about 42 ms.
- **Lighter far-away bodies did not help, so I did not keep them.** I tried Unity's built-in lower-detail versions of the body (86,000 triangles down to 6,500). The crowd cost stayed the same within run-to-run noise. So what a body costs seems to be how many separate pieces are drawn (skin, head, hair, clothes, shoes, weapon, each drawn again for shadows), plus its scripts and posing, rather than how many triangles it has.
- **The crowd's shadows are about a fifth of the cost** (50 players: 34 ms without them, 42 ms with).
- **What would help next** (my suggestions): fewer separate pieces per body, no shadows from far-away bodies, and far-away bodies posed and animated less often. These are bigger changes worth deciding on together.
