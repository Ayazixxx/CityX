using System.Collections.Generic;
using CityX.Character;
using CityX.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CityX.View
{
    /// <summary>
    /// E (or the gamepad's left face button) at a light switch flips it. One of these in the scene (on the CityStreamer), not a component on every
    /// switch: there is a switch in every room of every building, and they are found from the plan (World.RoomLighting.PlansFor) only when the key
    /// is pressed. The switch flips in every building; the room lights up only where the building has power (World.PowerGrid) -- elsewhere it
    /// clicks and nothing happens.
    /// </summary>
    public sealed class LightSwitchInteractor : MonoBehaviour
    {
        /// <summary>How far from the switch the player may stand (horizontally) to reach it.</summary>
        public const float ReachMetres = 2.4f;

        /// <summary>How far above or below the switch the player's own switch height may be (a different floor is not reachable).</summary>
        public const float ReachUpDown = 1.6f;

        private PlayerInputReader _player;
        private float _sinceLookup = 99.0f;
        private AudioClip _click;

        /// <summary>The last switch flipped (for tests and the HUD), and whether its building had power.</summary>
        public RoomKey LastFlipped { get; private set; }
        public bool LastHadPower { get; private set; }

        private void Update()
        {
            bool pressed = (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                || (Gamepad.current != null && Gamepad.current.buttonWest.wasPressedThisFrame);
            if (!pressed)
            {
                return;
            }
            Transform player = LocalPlayer();
            if (player == null)
            {
                return;
            }
            Camera camera = Camera.main;
            Vector3 look = camera != null ? camera.transform.forward : player.forward;
            Vector3 eye = camera != null ? camera.transform.position : player.position + Vector3.up * 2.2f;
            TryFlip(player.position, eye, look);
        }

        /// <summary>
        /// Flips the switch nearest the player that is within reach, on their floor and in front of where they are looking. Public so a test can
        /// drive it without a keyboard. False when there is none.
        /// </summary>
        public bool TryFlip(Vector3 feet, Vector3 eye, Vector3 look)
        {
            CityWorld world = CurrentWorld.Instance;
            if (world == null || !TryFind(world, feet, eye, look, out BuildingSite site, out RoomLightPlan plan))
            {
                return false;
            }
            RoomLights.Flip(plan.Key);
            LastFlipped = plan.Key;
            LastHadPower = PowerGrid.IsOn(site);
            site.LocalToWorld(plan.Switch.x, plan.Switch.z, out double wx, out double wz);
            PlayClick(new Vector3((float)wx, plan.Switch.y, (float)wz), RoomLights.IsOn(plan.Key));
            return true;
        }

        /// <summary>The switch a player at <paramref name="feet"/>, looking along <paramref name="look"/>, would flip. Pure apart from the plan lookup.</summary>
        public static bool TryFind(CityWorld world, Vector3 feet, Vector3 eye, Vector3 look, out BuildingSite found, out RoomLightPlan best)
        {
            found = default;
            best = null;
            float bestDistance = float.MaxValue;
            // The building the player stands in (a little margin: they may be right by the front door).
            if (!world.TryBuildingAt(feet + Vector3.up * 0.5f, out BuildingSite site, 0.5))
            {
                return false;
            }
            foreach (RoomLightPlan plan in RoomLighting.PlansFor(site))
            {
                site.LocalToWorld(plan.Switch.x, plan.Switch.z, out double wx, out double wz);
                var at = new Vector3((float)wx, plan.Switch.y, (float)wz);
                if (Mathf.Abs(at.y - (feet.y + RoomLighting.SwitchHeight)) > ReachUpDown)
                {
                    continue;
                }
                float flat = new Vector2(at.x - feet.x, at.z - feet.z).magnitude;
                if (flat > ReachMetres || flat >= bestDistance)
                {
                    continue;
                }
                if (!BuildingDoor.IsLookingToward(eye, look, at))
                {
                    continue;
                }
                bestDistance = flat;
                best = plan;
                found = site;
            }
            return best != null;
        }

        private Transform LocalPlayer()
        {
            _sinceLookup += Time.unscaledDeltaTime;
            if ((_player == null || !_player.isActiveAndEnabled) && _sinceLookup > 0.5f)
            {
                _sinceLookup = 0.0f;
                _player = null;
                foreach (PlayerInputReader reader in FindObjectsByType<PlayerInputReader>(FindObjectsSortMode.None))
                {
                    // A copy of another player's body has its input switched off (NetworkedPlayerIdentity): the one with input is ours.
                    if (reader.isActiveAndEnabled)
                    {
                        _player = reader;
                        break;
                    }
                }
            }
            return _player != null ? _player.transform : null;
        }

        /// <summary>A switch's click: a short tick, a little lower when it goes off. Made here (no sound asset needed).</summary>
        private void PlayClick(Vector3 at, bool on)
        {
            if (_click == null)
            {
                const int rate = 44100;
                var samples = new float[rate / 25];
                var random = new System.Random(7);
                for (int i = 0; i < samples.Length; i++)
                {
                    float t = i / (float)samples.Length;
                    float envelope = Mathf.Exp(-t * 18.0f);
                    samples[i] = ((float)random.NextDouble() * 2.0f - 1.0f) * envelope * 0.6f
                        + Mathf.Sin(i * 2.0f * Mathf.PI * 2400.0f / rate) * envelope * 0.25f;
                }
                _click = AudioClip.Create("LightSwitchClick", samples.Length, 1, rate, false);
                _click.SetData(samples, 0);
            }
            PositionalSound.Play(_click, at, 0.7f, on ? 1.0f : 0.85f, 0.0f, 12.0f);
        }
    }
}
