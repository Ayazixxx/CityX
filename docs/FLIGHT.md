# Flight feel: research and what was built

Requested 2026-09-25: "research 3rd person player flight mechanics to improve
upon the jetpack flying ... like superhero game flying mechanics ... implement
what you learn into the jetpack we created", with Shift left as the boost.

The jetpack's SPEEDS, CONTROLS and BODY LEAN were tuned over many rounds of
explicit feedback and are deliberately untouched. This adds the layer around
them that the research says makes flying feel fast: how the camera, the sound
and the air react to speed.

## What the research says

Being honest about the sources: a few pages refused to load (403) and a few
threads had little detail, so some points below come from search summaries
rather than a full read. They agree with each other, which is the reason to
trust them.

- **Sense of speed** ([Adding the Feeling of Speed](https://elliotdev.gg/adding-the-feeling-of-speed/),
  [GameDev.net forum](https://gamedev.net/forums/topic/552271-giving-a-feel-of-speed-in-the-game/4551854/),
  [Epic forum: sense of speed](https://forums.unrealengine.com/t/how-to-give-the-player-a-sense-of-speed/102784)).
  Widening the field of view with speed is called one of the simplest and most
  effective cues; the closer to the normal FOV, the more precise the input, so it
  is a trade-off. The camera should sit on a virtual spring - it falls back a
  little as you boost and lags slightly. Speed lines that scale with speed, wind
  that gets louder and higher, and a little camera shake (kept small, since it
  disorients). Also: handling gets less responsive at high speed as a natural
  brake.
- **Spider-Man (Insomniac)** ([Design Analysis](https://www.gamedeveloper.com/design/marvel-s-spider-man-design-analysis),
  [NME](https://www.nme.com/features/how-the-web-swinging-of-marvels-spider-man-makes-its-superhero-fantasy-soar-3287136)).
  Built around "flow, fluidity and momentum"; the physics is *cheated* for feel
  (the swing arc is corrected to keep you moving straight, the web shortens near
  the ground so you do not lose flow, walls turn into wall-runs instead of
  crashes). Lesson: assists that protect momentum matter more than realism.
- **Lego Batman 3** ([Steam thread](https://steamcommunity.com/app/313690/discussions/0/611696927927742555/),
  [GameFAQs guide](https://gamefaqs.gamespot.com/pc/800924-lego-batman-3-beyond-gotham/faqs/70836),
  [TrueAchievements](https://www.trueachievements.com/forum/viewthread.aspx?tid=783491)).
  Double-tap jump to take off, hold to descend; players say its flight is a clear
  improvement over the earlier Lego games and works much better on a controller
  than a keyboard. Superman was the character people flew with. The "boost" is a
  distinct event, not just a higher number - which is why the boost here now
  gets a brief punch.
- **Avengers** ([Steam proposal](https://steamcommunity.com/app/997070/discussions/0/2944747978657842838/),
  [Screen Rant](https://screenrant.com/marvels-avengers-game-thor-flight-potential-improvement/)).
  A complaint worth heeding: a *toggle* for flight/hover on Shift confused PC
  players, who wanted "hold to fly, release to hover", and flying felt too slow.
  Here Shift stays a plain hold, as you asked.
- **Anthem** ([wiki](https://anthem.fandom.com/wiki/Thermal_Cooling),
  [review](https://wccftech.com/review/anthem-flying-high-it-doesnt-last/)).
  Hover is a real state (stop mid-air), and managing flight is a skill (heat).
  Noted; not built - the jetpack has no heat and hover already exists.
- **Design problem** ([NeoGAF thread](https://www.neogaf.com/threads/superhero-games-with-flight-as-a-core-ability-for-characters-are-fundamentally-flawed.1358400/),
  [UE5 Superman prototype](https://tjatomica.itch.io/superman-ue5)).
  Flight has no natural obstacles, so it needs open space and things to react
  to; a prototype uses W/A/S/D to move, Shift to dash, Ctrl to dive.

## What was built

| Idea | Where |
|---|---|
| The view widens with speed | `FlightFeel.FovBonus` -> `ThirdPersonCamera.FlightFovBonus` |
| The camera falls back on a spring | `FlightFeel.DistanceBonus` -> `ThirdPersonCamera.FlightDistanceBonus` |
| Boost is an event: a brief extra punch that fades | `FlightFeel.KickEnvelope` (Shift starts it) |
| Streaks of air rushing past | `Weapons/JetpackSpeedLines.cs` |
| Wind quickens with speed | `FlightFeel.WindPitch` on the existing wind loop |
| Off on the ground and on a crash/ragdoll | `Jetpack.TickFlightFeel` / `ResetFlightFeel` |
| Live tuning | `/flight` in the dev console |

`/flight` lists the numbers; `/flight fov 20`, `/flight kick 10`, `/flight
pullback 2`, `/flight streaks 0` (turns the lines off) and so on set them.
They are public static on `FlightFeel`, so tuning is flying and looking, not
rebuilding.

## Not built (ideas, if it still does not feel like Superman)

- Banking into camera turns (the body rolls when you swing the camera). Left out
  because roll is A/D-driven and was tuned by explicit feedback.
- Momentum: a longer carry when you let go, instead of stopping quickly.
- Spider-Man style assists near the ground and walls.
- Camera shake on the boost (kept out; the research warns it disorients).

None of this has been flown by a person yet. The tests prove the numbers and
the camera wiring; whether it FEELS right is what `/flight` is for.

## Update 2026-10-15: subtler speed lines

Owner: "make the lines that appear when flying through the sky ... a little more subtle. research how other games do it". What the research found:
games keep speed lines at the screen's edges and the middle clear, at low opacity, and only when really fast (a kart game's radial shader
fades them out toward the centre; Spider-Man sells speed mostly with things whipping past rather than streaks).

- Fewer: 40 streaks a second at full speed (70 before), none below 40% of the speed range (25% before).
- Kept to the edges: the ring they appear in starts 4.5 m off the line of sight (3.5 before).
- Thinner (0.02 m, 0.03 before), shorter (velocity scale 0.04, 0.05 before) and fainter: alpha 0.25 at the ring's outer edge (0.5 before),
  a third of that at its inner edge, so the ones nearest the middle are the faintest (`JetpackSpeedLines.AlphaAt`).
- Still tunable live: `/flight streaks <n>`. Test: `FlightFeelTests.TheSpeedLinesAreFewFaintAndKeptToTheEdges`; `SpeedLinesCapture` (Explicit,
  -Graphics) renders `speed-lines-full.png` and `speed-lines-three-quarters.png`.
