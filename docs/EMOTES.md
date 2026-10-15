# Chat emotes: /dance, /breakdance, /pushups

Typed in the chat (Enter, then the command). `PlayerChat.Dispatch` hands each to `PlayerInputReader`.

- **/dance**, **/breakdance**: a looping clip (Animator bools `Dancing`, `Breakdancing`, Any State in, back to Locomotion when cleared). WASD,
  Space, Shift or Ctrl ends them (`RequestsDanceCancel`); you are free at once.

## /pushups (2026-10-15)

Owner: "if you do /pushups in the text chat then it does idle to push up then does push up on repeat until you press and movement key or spell
or click (but you can move your mouse around) but once you move it does push up to idle then you can start moving freely again. but as your
doing pushups your xp goes up 1 xp per push up".

- **Clips**: the owner's three Mixamo animations, `Character/Animations/pushup_enter.fbx` (Idle To Push Up), `pushup.fbx` (Push Up, looped) and
  `pushup_exit.fbx` (Push Up To Idle), imported like the crouches: the drop of the hips is baked into the pose, measured from the feet
  (`CharacterImportSetup`, `lowers`).
- **The emote** (`Character/PushUpEmote.cs`, pure): going down, pushing up on repeat, getting up. It keeps time with the clips' own lengths, which
  `PlayerRigBuilder` writes into the reader (enter 2.6 s, a push-up 1.53 s, getting up 3.1 s). A push-up finished is +1 XP (`PlayerVitals.AddXp`).
  Called off by WASD, Space, Shift, Ctrl, a spell key 1 to 0 or a mouse button; a call while going down is kept, and it gets up once down. The body
  is held (no walking, no turning) until it stands again; the mouse still turns the camera. A blow, death or a fall ends it at once.
- **The Animator** follows the int `PushUpPhase` (0 standing, 1 going down, 2 pushing up, 3 getting up): Any State enters PushUpEnter only on 1,
  so it never restarts while it holds; each later phase moves it on (`PlayerRigBuilder.AddPushUpStates`).
- **Two things the body needed** (both seen in `PushUpCapture`, the side view): the lock that holds the hips upright for walking let the clips
  move only the arms and legs, the body standing -- the torso now follows the clips in full while a push-up phase plays
  (`PlayerLocomotion.PushUpOwnsTheBody`); and this body's arms are long for its legs, so the plank came out half a metre low with the palms through
  the floor -- the body is now tipped up about its toes (or its knees, kneeling) just enough to put the palms on the floor
  (`PlayerLocomotion.ApplyPushUpGrounding`): the shoulders rise from 0.25 m to 0.85 m on each push-up. Foot IK stays off meanwhile.
- **Other players** see it: `PushUpPhase` is sent with the other Animator parameters (`NetworkedAnimatorSync`), and the copy runs the same torso
  follow and grounding in its own LateUpdate.
- Tests: `PushUpEmoteTests`, `PushUpPlayTests` (real keys: reps for XP, mouse look free, W gets it up and walking only once standing, a click
  or a spell key calls it off, palms on the floor and the shoulders rising), `NetworkedPlayerStateSyncPlayTests.PushUpsOnOnePlayersBodyShowOnAnothersCopy`.
  `PushUpCapture` (Explicit, -Graphics) renders `pushup-NN.png` and logs the hands, feet, shoulders and hips.
- Not done: push-ups with something in hand keep the held item's pose code running over the arms.
