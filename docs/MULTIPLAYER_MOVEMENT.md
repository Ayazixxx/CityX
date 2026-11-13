# How another player's body moves on your screen

Owner, 2026-11-13, after the first online duel with a friend on the same Wi-Fi: "it worked but it was just really kinda choppy and laggy
and the animations werent really synced up so well. at some point the characters stop being synced up and when i looked where he was on
his screen i didnt see him at the same place on my screen."

Three causes were found, each fixed here.

## 1. The stutter: FishNet's NetworkTransform (replaced)

A body moves once a frame, at whatever frame rate the game runs. FishNet's `NetworkTransform` looked at it on its own clock, thirty times
a second, and the other machine treated each look as exactly a thirtieth of a second after the last. They are not: a look lands on
whichever frame comes next, so one step holds two frames of movement and the next one or three, and the copy walks fast, slow, fast,
slow. Below thirty frames a second some looks see no movement at all, and the copy stops and jumps. It also sent only what had changed
since the last packet, so a lost packet could leave a copy standing beside where its owner stood until the owner next moved that way.

Measured (`NetworkedPlayerStateSyncPlayTests.Movement`, a body walked at a steady 4 m/s for a few seconds, the copy watched on another
client; a stall is a frame at under half the pace, a lurch one at over one and a half times it):

| Owner's frame rate | Before: stalls + lurches | Before: behind | Now: stalls + lurches | Now: behind |
|---|---|---|---|---|
| 60 | 16 of 299 frames | 217 ms | 0 of 240 | 117 ms |
| 45 | 79 of 225 | 193 ms | 3 of 181 | 156 ms |
| 25 to 30 | 66 of 125 | 290 ms | 1 of 120 | 242 ms |
| 90 | 444 of 450 | 150 ms | 0 of 360 | 98 ms |

Now, over a line 30 ms late that loses 5% of packets and reorders 2%: 3 of 241 frames at 60 fps, none at 45; over one that loses 30%: 1
of 240; and in every case the copy, and the server's own, ends within a millimetre of where the owner stands. (These tests go client to
server to client, two hops, all in one process, so their "behind" is longer than a host and a friend see, who have one hop. The odd stall
left is a long frame of the test process itself.)

### What replaced it

`Character/Network/NetworkedBodyMotion` (on every player's body and every networked enemy; added by the builders next to the
NetworkObject) and the plain class `BodyMotionPlayback`:

- **The owner's machine says where the body stands with the time on its own clock** (`BodyMoment`: time, place, turn, size), after the
  frame's movement, up to 60 times a second while it moves. Because each moment carries its time, it does not matter when it was sent or
  arrived.
- **Whole, and unreliably.** Every packet has the place, turn and size in full, so a lost one is made good by the next. A body standing
  still is told again 5 times a second with the very same numbers, so no copy is left where a lost packet put it. Setting off again, the
  owner first says "it stood here until the last frame", so a copy does not start sliding from the last time it was told.
- **The server checks** (real numbers, a real rotation, a sane size, at most 150 a second) and passes it on, and plays the body back
  itself too: what it measures (who is in view of whom, who is near enough to hit) is measured from where bodies stand.
- **Every other machine shows the body 0.1 s in the past** (`BodyMotionPlayback.DelaySeconds`), between the two moments either side of
  that time. The playback runs a little faster when it has fallen behind (2% per 10 ms), slows as it runs out of moments (inside the
  last 40 ms) instead of hitting the newest one and stopping dead, jumps ahead when half a second behind (a stall), never runs past the
  newest moment, and puts a teleported body there instead of sliding it (two moments further apart than 150 m/s).
- While a body stands, the playback is taken back unseen to a full delay behind, so the first step is shown smoothly.

The session's clock ticks 60 times a second now (`NetworkBootstrap.TicksPerSecond`; FishNet's default was 30). FishNet sends and reads
only on a tick, so at 30 each hop waited up to a thirtieth of a second at both ends. Every copy of the game must use the same rate (both
players run the same build).

The walk and run blend, crouch, aim and the rest of the continuous animation state (`NetworkedAnimatorSync`, 20 a second) now also go
unreliably: sent reliably, each lost one held up every swing and hit behind it until it had been sent again.

## 2. Escape froze the game in a session (fixed)

The pause menu stopped the clock (`Time.timeScale = 0`) whatever the mode. In a session that froze the pausing player's own game: their
body stood frozen on the other screen and every other body froze on theirs, wherever each then was, until they closed the menu. The
console already had the right rule; both now use `NetworkBootstrap.PlaysAlone`. Alone, Escape pauses as before. In a shared world the
menu only takes the keys (`PlayerInputReader.Hold`): the body stands, the world goes on, and the body can be hit.

## 3. The game stopped when its window was not in front (fixed)

`runInBackground` was off, so alt-tabbing or clicking another screen stopped the whole game, the session with it: frozen on the other
screen, then dropped after a few seconds. A session now runs on behind other windows (`NetworkBootstrap.Begin`); alone, the game still
stops when it is not in front.

## Not done yet

- **The delay is fixed at 0.1 s.** It could follow how steady the line is (shorter on a good one).
- **One-shot animations are not delayed with the body.** A swing or a jump is shown when it arrives, the body's place 0.1 s later; on a
  lunge the pose leads the movement by that much.
- **About 3 KB a second a moving body, uncompressed.** Fine for a handful of players; a crowded shard will want smaller packets and a
  slower pace for far bodies.
- Two real machines have only been tried once, before these fixes.

## Tests

`BodyMotionPlaybackTests` (EditMode: steady however unevenly told, a lost packet, out of order, the delay, never past the newest, setting
off after standing, a teleport, numbers no body could have), `NetworkedPlayerStateSyncPlayTests.Movement` (two clients and a server over
real sockets, in real time: four frame rates, a lossy line, standing in place afterwards, the server's copy too),
`NetworkBootstrapPlayTests.ASharedWorldRunsOnBehindOtherWindowsAndItsPauseMenuOnlyTakesTheKeys`,
`EquipmentReplicationGuardTests.TheNetworkedBodyIsMovedByItsOwnersMomentsAlone` (NetworkTransform must not come back),
`EnemyReplicationGuardTests`.
