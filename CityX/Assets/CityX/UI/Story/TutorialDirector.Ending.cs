using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.Core;
using CityX.Items;
using CityX.View;
using CityX.Weapons;
using CityX.World;
using CityX.World.Plan;
using UnityEngine;

namespace CityX.UI
{
    /// <summary>
    /// THE ENDING (docs/TUTORIAL.md; owner, 2026-11-02: just as Old Guy says "alright i think we're safe now", bullets rain in through the
    /// windows and kill you both): a volley from outside through the den's windows, the player and Old Guy shot down, then black, and the
    /// player stands at the hub. Who fired, and the rescuer between, are still the owner's to decide (2026-11-16: "just have the screen fade
    /// to black and unfade then you appear at the hub").
    /// </summary>
    public sealed partial class TutorialDirector
    {
        public const float VolleySeconds = 2.4f;
        public const float SecondsBetweenShots = 0.07f;
        public const float PlayerShotAt = 0.6f;
        public const float OldGuyShotAt = 0.75f;

        /// <summary>How long the two lie there before the screen goes, seconds.</summary>
        public const float LyingSeconds = 1.5f;

        public const float FadeSeconds = 1.2f;
        public const float BlackSeconds = 1.0f;

        /// <summary>How far outside the window the shooters stand, metres.</summary>
        public const float ShooterMetres = 25.0f;

        /// <summary>How far each shot strays from its shooter's spot, metres.</summary>
        public const float ShooterSpread = 2.0f;

        public const float ShotVolume = 0.9f;
        public const float ShotHearingMetres = 150.0f;

        /// <summary>How high over the hub's floor the player is set down, metres.</summary>
        public const float ArriveAbove = 0.3f;

        private const float ChestHeight = 1.6f;

        /// <summary>The ending is playing.</summary>
        public bool EndingPlaying { get; private set; }

        private IEnumerator Ending()
        {
            EndingPlaying = true;
            OldGuy oldGuy = _oldGuy;
            PlayerVitals player = Player;
            List<(HousePane Pane, Vector3 Shooter)> windows = DenWindows();
            AudioClip shot = GunshotSound(player);

            float started = Time.time;
            float nextShot = started;
            int fired = 0;
            bool playerDown = false, oldGuyDown = false;
            while (Time.time - started < VolleySeconds)
            {
                float t = Time.time - started;
                while (Time.time >= nextShot)
                {
                    nextShot += SecondsBetweenShots;
                    (HousePane pane, Vector3 shooter) = windows[fired % windows.Count];
                    Vector3 from = shooter + Random.insideUnitSphere * ShooterSpread;
                    Vector3 to = ShotTarget(fired, player, oldGuy);
                    if (pane != null && !pane.Shattered)
                    {
                        pane.TakeBlow(pane.transform.position, (to - from).normalized, BlowStrength.Heavy);
                    }
                    BulletTracer.Spawn(from, to);
                    if (shot != null)
                    {
                        PositionalSound.Play(shot, from, ShotVolume, Random.Range(0.92f, 1.08f), 0.0f, ShotHearingMetres);
                    }
                    fired++;
                }
                if (!playerDown && t >= PlayerShotAt && player != null)
                {
                    playerDown = true;
                    ShootDown(player.transform.position, windows[0].Shooter);
                    if (_hud != null)
                    {
                        _hud.DeathScreenHeld = true;
                    }
                    player.TakeDamage(player.MaxHealth * 10.0f);
                }
                if (!oldGuyDown && t >= OldGuyShotAt && oldGuy != null)
                {
                    oldGuyDown = true;
                    ShootDown(oldGuy.transform.position, windows[0].Shooter);
                    oldGuy.Collapse(windows[0].Shooter);
                }
                yield return null;
            }

            yield return new WaitForSeconds(LyingSeconds);
            ScreenBlack.FadeTo(1.0f, FadeSeconds);
            yield return new WaitForSecondsRealtime(FadeSeconds + BlackSeconds * 0.5f);

            ArriveAtTheHub(player);
            if (oldGuy != null)
            {
                oldGuy.Progress.Finish();
                Destroy(oldGuy.gameObject);
            }
            Follow(null);
            if (_objective != null)
            {
                _objective.Show("", "");
            }
            MarkTheTutorialDone();

            yield return new WaitForSecondsRealtime(BlackSeconds * 0.5f);
            ScreenBlack.FadeTo(0.0f, FadeSeconds);
            PlayerChat chat = FindFirstObjectByType<PlayerChat>();
            if (chat != null && chat.Banner != null)
            {
                chat.Banner.ShowMessage(TutorialScript.HubBanner);
            }
            EndingPlaying = false;
        }

        /// <summary>Every third shot at the player, every third at Old Guy, the rest round the den.</summary>
        private Vector3 ShotTarget(int shot, PlayerVitals player, OldGuy oldGuy)
        {
            if (shot % 3 == 0 && player != null)
            {
                return player.transform.position + Vector3.up * ChestHeight + Random.insideUnitSphere * 0.3f;
            }
            if (shot % 3 == 1 && oldGuy != null)
            {
                return oldGuy.transform.position + Vector3.up * ChestHeight + Random.insideUnitSphere * 0.3f;
            }
            OldGuysHouse.Room den = OldGuysHouse.RoomNamed("Old Guy's den");
            OldGuysHouse.Area floor = den.Floors[0];
            var local = new Vector3(Random.Range(floor.X0, floor.X1), den.FloorY + Random.Range(0.3f, 3.5f), Random.Range(floor.Z0, floor.Z1));
            return _house != null ? _house.transform.TransformPoint(local) : local;
        }

        private static void ShootDown(Vector3 feet, Vector3 shooter)
        {
            Vector3 chest = feet + Vector3.up * ChestHeight;
            BloodBurst.Spawn(chest, (chest - shooter).normalized, 1.0f);
        }

        /// <summary>
        /// The den's windows, each with a shooter's spot straight out from it; a spot outside the den's back wall when the house has none
        /// (it always has: the plan tests hold the den to having windows).
        /// </summary>
        private List<(HousePane, Vector3)> DenWindows()
        {
            var windows = new List<(HousePane, Vector3)>();
            if (_house == null || _house.Built == null)
            {
                Vector3 fallback = Player != null ? Player.transform.position + Vector3.forward * ShooterMetres : Vector3.zero;
                windows.Add((null, fallback));
                return windows;
            }
            OldGuysHouse.Room den = OldGuysHouse.RoomNamed("Old Guy's den");
            OldGuysHouse.Area floor = den.Floors[0];
            const float edge = 0.6f;
            foreach (HousePane pane in _house.Built.GetComponentsInChildren<HousePane>())
            {
                Vector3 local = _house.transform.InverseTransformPoint(pane.transform.position);
                if (local.y < den.FloorY || local.y > den.CeilingY
                    || local.x < floor.X0 - edge || local.x > floor.X1 + edge || local.z < floor.Z0 - edge || local.z > floor.Z1 + edge)
                {
                    continue;
                }
                Vector3 outward = pane.AlongLocalX
                    ? new Vector3(0.0f, 0.0f, local.z > (floor.Z0 + floor.Z1) * 0.5f ? 1.0f : -1.0f)
                    : new Vector3(local.x > (floor.X0 + floor.X1) * 0.5f ? 1.0f : -1.0f, 0.0f, 0.0f);
                windows.Add((pane, _house.transform.TransformPoint(local + outward * ShooterMetres)));
            }
            if (windows.Count == 0)
            {
                Vector3 behind = new Vector3(floor.Centre.x, den.FloorY + ChestHeight, floor.Z1 + ShooterMetres);
                windows.Add((null, _house.transform.TransformPoint(behind)));
            }
            return windows;
        }

        /// <summary>The rifle's shot, from the player's own list of items (every body lists the same).</summary>
        private static AudioClip GunshotSound(PlayerVitals player)
        {
            Inventory inventory = player != null ? player.GetComponent<Inventory>() : null;
            GameObject rifle = inventory != null ? inventory.PrefabForItem(ItemRegistry.AK47) : null;
            PistolWeapon gun = rifle != null ? rifle.GetComponent<PistolWeapon>() : null;
            return gun != null && gun.ShotPlayer != null ? gun.ShotPlayer.clip : null;
        }

        /// <summary>Back on their feet in the middle of the hub, facing out toward where they came from; where they fell, in a world without one.</summary>
        private void ArriveAtTheHub(PlayerVitals player)
        {
            if (player == null)
            {
                return;
            }
            player.Revive();
            if (_hud != null)
            {
                _hud.DeathScreenHeld = false;
            }
            if (!TryHubSpot(out Vector3 spot))
            {
                return;
            }
            Vector3 toward = _house != null ? _house.transform.position - spot : Vector3.forward;
            toward.y = 0.0f;
            Quaternion facing = toward.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(toward.normalized) : player.transform.rotation;

            CharacterController controller = player.GetComponent<CharacterController>();
            if (controller != null)
            {
                controller.enabled = false;
            }
            player.transform.SetPositionAndRotation(spot, facing);
            if (controller != null)
            {
                controller.enabled = true;
            }
            Physics.SyncTransforms();
            player.ResetFallTracking(spot.y);
            ThirdPersonCamera orbit = FindFirstObjectByType<ThirdPersonCamera>();
            if (orbit != null)
            {
                orbit.Yaw = facing.eulerAngles.y;
            }
        }

        /// <summary>The middle of the hub's island, from the world's plan; false in a world that has no hub.</summary>
        public static bool TryHubSpot(out Vector3 spot)
        {
            SkyHubPlan hub = CurrentWorld.Instance != null && CurrentWorld.Instance.Plan != null ? CurrentWorld.Instance.Plan.SkyHub : default;
            spot = new Vector3((float)hub.CentreX, (float)hub.IslandTop + ArriveAbove, (float)hub.CentreZ);
            return hub.Found;
        }

        private static void MarkTheTutorialDone()
        {
            CharacterIdentity ours = CharacterIdentity.Ours;
            if (ours != null && ours.Character != null)
            {
                ours.Character.TutorialDone = true;
                ours.Write();
            }
        }
    }
}
