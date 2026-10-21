using System.Collections.Generic;
using System.Globalization;
using CityX.Character;
using CityX.Core;
using CityX.Items;
using CityX.Net;
using CityX.Vehicles;
using CityX.View;
using CityX.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using FlightFeel = CityX.Weapons.FlightFeel;

namespace CityX.UI
{
    public sealed partial class DevConsoleView
    {
        /// <summary>
        /// "/pistol pos|rot|scale|pose" -- same live-and-read-back idiom as
        /// "/car pose", for Requested
        /// "let me adjust the pistol position in his hand ingame using the
        /// console." Nudges PistolWeapon's own live grip fields directly
        /// (see that class's own doc comment on LiveGripPosition/Rotation/
        /// Scale), which UpdateHeldPose already applies every frame instead
        /// of the baked GripHandPosition/GripHandRotationEuler/GripScale
        /// constants -- so a change here shows up on the held gun
        /// immediately, with nothing to rebuild.
        /// </summary>
        private void PistolCommand(string sub, string a, string b, string c)
        {
            if (Vitals == null)
            {
                Log("no body here");
                return;
            }
            var pistol = Vitals.GetComponentInChildren<CityX.Weapons.PistolWeapon>();
            if (pistol == null)
            {
                Log("no pistol equipped -- /equip pistol first");
                return;
            }

            switch (sub)
            {
                case "pos":
                    if (!TryParseFloat(a, out float px) || !TryParseFloat(b, out float py) || !TryParseFloat(c, out float pz))
                    {
                        Log(DevCommands.UsageFor("pistol"));
                        return;
                    }
                    pistol.SetLiveGripPosition(new Vector3(px, py, pz));
                    Log($"pistol grip position set to {FormatVector3(new Vector3(px, py, pz))}");
                    return;

                case "rot":
                    if (!TryParseFloat(a, out float rx) || !TryParseFloat(b, out float ry) || !TryParseFloat(c, out float rz))
                    {
                        Log(DevCommands.UsageFor("pistol"));
                        return;
                    }
                    pistol.SetLiveGripRotation(new Vector3(rx, ry, rz));
                    Log($"pistol grip rotation set to {FormatVector3(new Vector3(rx, ry, rz))}");
                    return;

                case "scale":
                    if (!TryParseFloat(a, out float scale) || scale <= 0.0f)
                    {
                        Log(DevCommands.UsageFor("pistol"));
                        return;
                    }
                    pistol.SetLiveGripScale(scale);
                    Log($"pistol grip scale set to {scale:0.000}");
                    return;

                case "pose":
                    Log($"pos {FormatVector3(pistol.LiveGripPosition)}   " +
                        $"rot {FormatVector3(pistol.LiveGripRotation)}   scale {pistol.LiveGripScale:0.000}");
                    return;

                default:
                    Log(DevCommands.UsageFor("pistol"));
                    return;
            }
        }

        /// <summary>
        /// /gun -- live-tunes the third-person pistol's feel (recoil, kick,
        /// aim sensitivity and speed) without a rebuild, the same "tune live,
        /// then bake the winning numbers into the defaults" workflow as
        /// /pistol, /aim and /sword.
        /// </summary>
        private void GunCommand(string name, string value)
        {
            if (Vitals == null)
            {
                Log("no body here");
                return;
            }
            var pistol = Vitals.GetComponentInChildren<CityX.Weapons.PistolWeapon>();

            if (name == "shoulder")
            {
                if (OrbitCamera == null)
                {
                    Log("no camera here");
                    return;
                }
                OrbitCamera.ToggleShoulder();
                Log(OrbitCamera.ShoulderSide > 0.0f ? "camera over the right shoulder" : "camera over the left shoulder");
                return;
            }

            if (string.IsNullOrEmpty(name))
            {
                Log(pistol == null
                    ? "no pistol equipped -- /equip pistol first"
                    : $"recoil {pistol.RecoilPitchDegrees}   adsrecoil {pistol.AdsRecoilMultiplier}   shake {pistol.ShakeDegrees}   " +
                      $"kick {pistol.KickBackMetres}   kickangle {pistol.KickUpDegrees}   " +
                      $"recover {(OrbitCamera != null ? OrbitCamera.RecoilRecoverPerSecond : 0.0f)}   " +
                      $"sens {(OrbitCamera != null ? OrbitCamera.AimSensitivityMultiplier : 0.0f)}   " +
                      $"aimspeed {(_playerLocomotion != null ? _playerLocomotion.AimMoveSpeedMultiplier : 0.0f)}");
                return;
            }

            if (!TryParseFloat(value, out float number))
            {
                Log(DevCommands.UsageFor("gun"));
                return;
            }

            switch (name)
            {
                case "recoil": if (!NeedPistol(pistol)) return; pistol.RecoilPitchDegrees = number; break;
                case "adsrecoil": if (!NeedPistol(pistol)) return; pistol.AdsRecoilMultiplier = number; break;
                case "shake": if (!NeedPistol(pistol)) return; pistol.ShakeDegrees = number; break;
                case "kick": if (!NeedPistol(pistol)) return; pistol.KickBackMetres = number; break;
                case "kickangle": if (!NeedPistol(pistol)) return; pistol.KickUpDegrees = number; break;
                case "recover":
                    if (OrbitCamera == null) { Log("no camera here"); return; }
                    OrbitCamera.RecoilRecoverPerSecond = number;
                    break;
                case "sens":
                    if (OrbitCamera == null) { Log("no camera here"); return; }
                    OrbitCamera.AimSensitivityMultiplier = number;
                    break;
                case "aimspeed":
                    if (_playerLocomotion == null) { Log("no body here"); return; }
                    _playerLocomotion.AimMoveSpeedMultiplier = number;
                    break;
                default:
                    Log(DevCommands.UsageFor("gun"));
                    return;
            }
            Log($"{name} = {number.ToString(CultureInfo.InvariantCulture)}");
        }

        private bool NeedPistol(CityX.Weapons.PistolWeapon pistol)
        {
            if (pistol != null)
            {
                return true;
            }
            Log("no pistol equipped -- /equip pistol first");
            return false;
        }

        /// <summary>
        /// /sword -- live-tunes the soulslike sword combat (SoulsSwordCombat)
        /// without a rebuild: every timing, cost and damage number is a public
        /// field, so this lists them all and sets any one by name. Same "tune
        /// live, then bake the winning numbers into the defaults" workflow as
        /// /pistol and /aim.
        /// </summary>
        private void SwordCommand(string name, string value)
        {
            SoulsSwordCombat combat = _playerLocomotion != null
                ? _playerLocomotion.GetComponent<SoulsSwordCombat>()
                : null;
            if (combat == null || !combat.IsEquipped)
            {
                Log("no sword equipped -- /equip spikesword first");
                return;
            }

            if (string.IsNullOrEmpty(name))
            {
                Log(combat.Describe());
                return;
            }
            if (!TryParseFloat(value, out float number))
            {
                Log(DevCommands.UsageFor("sword"));
                return;
            }
            combat.TrySet(name, number, out string message);
            Log(message);
        }

        /// <summary>
        /// Live tuning for how flying feels (see FlightFeel) -- so the numbers can be
        /// set by flying and looking rather than by rebuilding and guessing, the
        /// same reason /aim and /sword exist.
        /// </summary>
        private void FlightCommand(string name, string value)
        {
            if (string.IsNullOrEmpty(name))
            {
                Log($"flight feel: fov {FlightFeel.FovGainDegrees}, kick {FlightFeel.BoostKickFovDegrees}, " +
                    $"kickseconds {FlightFeel.BoostKickSeconds}, pullback {FlightFeel.PullBackMetres}, " +
                    $"kickpullback {FlightFeel.BoostKickPullBackMetres}, smooth {FlightFeel.SmoothSeconds}, " +
                    $"streaks {FlightFeel.MaxStreaksPerSecond}, start {FlightFeel.StartSpeed}, full {FlightFeel.FullSpeed}");
                return;
            }
            if (!TryParseFloat(value, out float number))
            {
                Log(DevCommands.UsageFor("flight"));
                return;
            }

            switch (name.ToLowerInvariant())
            {
                case "fov": FlightFeel.FovGainDegrees = number; break;
                case "kick": FlightFeel.BoostKickFovDegrees = number; break;
                case "kickseconds": FlightFeel.BoostKickSeconds = Mathf.Max(0.0f, number); break;
                case "pullback": FlightFeel.PullBackMetres = number; break;
                case "kickpullback": FlightFeel.BoostKickPullBackMetres = number; break;
                case "smooth": FlightFeel.SmoothSeconds = Mathf.Max(0.01f, number); break;
                case "streaks": FlightFeel.MaxStreaksPerSecond = Mathf.Max(0.0f, number); break;
                case "start": FlightFeel.StartSpeed = number; break;
                case "full": FlightFeel.FullSpeed = Mathf.Max(number, FlightFeel.StartSpeed + 0.1f); break;
                default:
                    Log(DevCommands.UsageFor("flight"));
                    return;
            }
            Log($"flight {name} = {number}");
        }

        /// <summary>
        /// "/camera height|distance|shoulder|fov &lt;value&gt;", "/camera reset" or a bare "/camera": the follow camera's framing, live, to try
        /// other framings in play (owner, 2026-10-17: "i feel like the camera pov needs to be adjusted so i can see more of whats going on in
        /// front of me"). For the session only; the defaults are ThirdPersonCamera's.
        /// </summary>
        private void CameraCommand(string sub, string a)
        {
            if (OrbitCamera == null)
            {
                Log("no camera here");
                return;
            }
            if (sub == null)
            {
                Log($"camera: height {OrbitCamera.FramingHeight:0.00} m over the shoulder, distance {OrbitCamera.Distance:0.00} m, " +
                    $"shoulder {OrbitCamera.ShoulderOffset:0.00} m, fov {OrbitCamera.RestFieldOfView:0}");
                return;
            }
            if (sub == "reset")
            {
                OrbitCamera.FramingHeight = ThirdPersonCamera.DefaultFramingHeight;
                OrbitCamera.Distance = ThirdPersonCamera.DefaultDistance;
                OrbitCamera.ShoulderOffset = ThirdPersonCamera.DefaultShoulderOffset;
                OrbitCamera.RestFieldOfView = ThirdPersonCamera.DefaultFieldOfView;
                Log("camera back to its defaults");
                return;
            }
            if (!TryParseFloat(a, out float value))
            {
                Log(DevCommands.UsageFor("camera"));
                return;
            }
            switch (sub)
            {
                case "height": OrbitCamera.FramingHeight = value; break;
                case "distance": OrbitCamera.Distance = Mathf.Max(0.5f, value); break;
                case "shoulder": OrbitCamera.ShoulderOffset = value; break;
                case "fov": OrbitCamera.RestFieldOfView = Mathf.Clamp(value, 30.0f, 120.0f); break;
                default:
                    Log(DevCommands.UsageFor("camera"));
                    return;
            }
            Log($"camera {sub} set to {value:0.00}");
        }

        /// <summary>
        /// Live-tunes the aim camera's shoulder offset and the body's own
        /// extra yaw while aiming -- Requested: "let me adjust the pov
        /// and model rotation manually ... set up something using ingame
        /// console," after a guess-rebuild-screenshot loop on the same two
        /// numbers wasn't landing right. Both targets are already plain
        /// mutable fields (ThirdPersonCamera.AimShoulderOffset,
        /// PlayerLocomotion.AimYawOffsetDegrees) rather than consts, so this
        /// writes them directly -- no separate "Live" shadow field needed
        /// the way the pistol's own const grip constants required.
        ///
        /// NEITHER CHANGE SURVIVES A SCENE RELOAD. AimShoulderOffset is
        /// also baked into CityMassing.unity's own serialized default (see
        /// pistol_aim_pov_fortnite_offset memory) -- once a number here
        /// looks right, it still has to be typed into ThirdPersonCamera.cs's
        /// own default and the scene rebuilt, the same "tune live, then
        /// bake the winning number" workflow /pistol already
        /// uses.
        /// </summary>
        private void AimCommand(string sub, string a)
        {
            switch (sub)
            {
                case "offset":
                    if (OrbitCamera == null)
                    {
                        Log("no camera here");
                        return;
                    }
                    if (!TryParseFloat(a, out float offset))
                    {
                        Log(DevCommands.UsageFor("aim"));
                        return;
                    }
                    OrbitCamera.AimShoulderOffset = offset;
                    Log($"aim shoulder offset set to {offset:0.000}");
                    return;

                case "yaw":
                    // ROTATES THE WHOLE CHARACTER -- BODY, ARM, AND HEAD
                    // TOGETHER, AS ONE RIGID UNIT. Requested, repeated:
                    // "give me a command to rotate the entire body head
                    // body and arms." This used to set ONLY the body root's
                    // own offset (AimYawOffsetDegrees), leaving the arm/head
                    // -- which target the camera's own convergence point,
                    // independent of body yaw, see ApplyGunAimPose's own
                    // comment -- pointing the old direction, which read as
                    // the character visibly TWISTING rather than turning.
                    // Setting both fields to the SAME value here keeps
                    // body/arm/head in agreement at every value, exactly
                    // the single-knob behaviour asked for. /aim armyaw
                    // still exists underneath for the rare case of nudging
                    // JUST the arm/head independently of the body.
                    if (_playerLocomotion == null)
                    {
                        Log("no body here");
                        return;
                    }
                    if (!TryParseFloat(a, out float yaw))
                    {
                        Log(DevCommands.UsageFor("aim"));
                        return;
                    }
                    _playerLocomotion.AimYawOffsetDegrees = yaw;
                    _playerLocomotion.ArmAimYawOffsetDegrees = yaw;
                    Log($"aim yaw offset set to {yaw:0.0} degrees (body, arm, and head together)");
                    return;

                case "armyaw":
                    // THE ARM/HEAD KNOB ALONE, without moving the body --
                    // see "yaw" above (the one most people want) for the
                    // combined version. Kept separate for nudging just the
                    // gun's own angle independently of body rotation.
                    if (_playerLocomotion == null)
                    {
                        Log("no body here");
                        return;
                    }
                    if (!TryParseFloat(a, out float armYaw))
                    {
                        Log(DevCommands.UsageFor("aim"));
                        return;
                    }
                    _playerLocomotion.ArmAimYawOffsetDegrees = armYaw;
                    Log($"arm aim yaw offset set to {armYaw:0.0} degrees (arm and head only)");
                    return;

                case "pose":
                    Log($"offset {(OrbitCamera != null ? OrbitCamera.AimShoulderOffset : 0.0f):0.000}   " +
                        $"yaw {(_playerLocomotion != null ? _playerLocomotion.AimYawOffsetDegrees : 0.0f):0.0}   " +
                        $"armyaw {(_playerLocomotion != null ? _playerLocomotion.ArmAimYawOffsetDegrees : 0.0f):0.0}");
                    return;

                default:
                    Log(DevCommands.UsageFor("aim"));
                    return;
            }
        }
    }
}
