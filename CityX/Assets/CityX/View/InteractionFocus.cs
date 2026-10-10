using System;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// ONE thing answers the interact key: the one nearest the crosshair. A door and the light switch beside it both used to act on the same press
    /// (owner, 2026-10-09: "I keep accidentally opening and closing the door when I'm only trying to do the light switch"). Now, on the frame E is
    /// pressed, every interactable in reach OFFERS itself with how far off the crosshair it is, and once every Update has run the closest one alone
    /// acts. Doors (BuildingDoor) and switches (LightSwitchInteractor) take part.
    /// </summary>
    public static class InteractionFocus
    {
        private static int _frame = -1;
        private static float _bestDegrees;
        private static Action _best;
        private static InteractionFocusRunner _runner;

        /// <summary>How far off the crosshair a point is, in degrees, seen from the main camera (or from <paramref name="fallbackEye"/>).</summary>
        public static float DegreesOff(Vector3 target, Vector3 fallbackEye, Vector3 fallbackLook)
        {
            Camera camera = Camera.main;
            Vector3 eye = camera != null ? camera.transform.position : fallbackEye;
            Vector3 look = camera != null ? camera.transform.forward : fallbackLook;
            Vector3 to = target - eye;
            return to.sqrMagnitude < 1e-6f ? 0.0f : Vector3.Angle(look, to);
        }

        /// <summary>Offers an action for this frame's press; the offer nearest the crosshair is the one that runs.</summary>
        public static void Offer(float degreesOffCrosshair, Action act)
        {
            if (act == null)
            {
                return;
            }
            if (_frame != Time.frameCount)
            {
                Resolve();   // a press from an earlier frame that nothing resolved yet
                _frame = Time.frameCount;
            }
            if (_best == null || degreesOffCrosshair < _bestDegrees)
            {
                _best = act;
                _bestDegrees = degreesOffCrosshair;
            }
            EnsureRunner();
        }

        /// <summary>Runs the winning offer, if any, and forgets the rest. Called after every Update (InteractionFocusRunner.LateUpdate); public for tests.</summary>
        public static void Resolve()
        {
            Action act = _best;
            _best = null;
            _bestDegrees = float.MaxValue;
            act?.Invoke();
        }

        private static void EnsureRunner()
        {
            if (_runner != null || !Application.isPlaying)
            {
                return;   // outside Play mode nothing runs LateUpdate: a test calls Resolve itself
            }
            var go = new GameObject("InteractionFocus") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.Object.DontDestroyOnLoad(go);
            _runner = go.AddComponent<InteractionFocusRunner>();
        }
    }
}
