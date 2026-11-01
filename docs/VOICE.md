# Proximity voice

GAME_DESIGN "Social" (owner): proximity voice that fades with distance and is muffled through walls; faction radio heard anywhere. Built
2026-11-01 (owner: "set up proximity chat"): the proximity part. Faction radio waits for factions.

## How it works

| Step | Where | What |
|---|---|---|
| Talk | `PlayerVoiceCapture` (Character/Player) | Push-to-talk on **V**, or Open Mic in the settings; the input device from the settings. The microphone records at 16 kHz (or the nearest rate it can; `VoiceFramer` resamples) and is read in 10 ms blocks into 20 ms frames. |
| Code | `VoiceCodec` (Character/Voice) | IMA ADPCM, 4 bits a sample: a frame is 165 bytes (a 5-byte header: frame number, the coder's state at its start), about 66 kbit/s while talking, nothing while silent. Each frame decodes on its own, so a lost frame is 20 ms of silence and no more. |
| Send | `NetworkedVoice` (Character/Network) | Owner -> server, unreliable. The server checks the rate (60 frames/s at most) and the frame's shape, then sends it only to players whose bodies are within the hearing range plus 5 m (for moving between frames), as a TargetRpc each. A copy's microphone is switched off. |
| Wait | `VoiceJitterBuffer` | Three frames (60 ms) wait before playback starts, so uneven arrival does not chop the voice; never more than 200 ms behind (the oldest is dropped); run dry, silence, and it waits again. |
| Hear | `VoicePlayback` (Character/Voice) | A 3D sound at the speaker's head (so it comes from their side), playing only while they talk (silent voices would hold the mixer's few voices). Volume and muffling are the game's own (`VoiceHearing`), from this machine's player's head to the speaker's: full within 4 m, fading to nothing at 40 m (about 1.3x a real shout's carry, as everything in the game); each wall between (a ray, counted every 0.1 s, bodies and glass shards ignored) keeps 45% of the volume and cuts the highs (1.4 kHz through one wall, 700 Hz through more), so a voice through a wall keeps its boom and loses its words. Changes ease in, so passing a door does not click. |

## Why our own codec, not a voice package

No account, licence or download needed, and it is small enough to test whole. The cost is bandwidth: ADPCM is about four times Opus. If voice
traffic matters on full servers, swap the codec for Opus (Concentus, a pure C# port, MIT) behind the same frame shape.

## Still to do

- Faction radio (heard anywhere by your faction), once factions exist.
- Mute and block per player, and the report button (GAME_DESIGN "Social": required by law and the platforms).
- A talking icon over a speaker's head and on the HUD (`VoicePlayback.IsSpeaking` is there for it).
- A voice volume slider (the master volume covers it for now).

## Tests

- `VoiceTests` (EditMode): the codec's quality, a lost frame costing only itself, nonsense refused; the fade with distance and walls; the
  jitter buffer's wait, cap and silence; the framer's frames and its 48 kHz resampling.
- `VoicePlaybackPlayTests`: plays while frames come and stops after; quieter through a wall and far away.
- `NetworkedPlayerStateSyncPlayTests.WhatOnePlayerSaysIsHeardByANearPlayerAndNotByAFarOne`: over real sockets, B hears A near, and the
  server sends B nothing once B is out of earshot; a copy's microphone is off.
