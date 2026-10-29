using System;
using System.Collections.Generic;
using CityX.World;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>What this machine did to the fire, for other players (FireSync): a pour, a lighting, a dousing.</summary>
    public enum FireAct
    {
        Pour = 0,
        Ignite = 1,
        Douse = 2,
    }

    public readonly struct FireEvent
    {
        public readonly FireAct Act;
        public readonly Vector3 Point;

        /// <summary>Litres poured, or the radius lit or doused.</summary>
        public readonly float Amount;

        /// <summary>A lighting that takes only fuel (a bullet, lightning), not bare ground.</summary>
        public readonly bool OnlyFuel;

        public FireEvent(FireAct act, Vector3 point, float amount, bool onlyFuel)
        {
            Act = act;
            Point = point;
            Amount = amount;
            OnlyFuel = onlyFuel;
        }
    }

    /// <summary>
    /// THE FIRE IN THE WORLD (docs/FIRE.md): one object, made the first time anything pours or lights, that owns the ground's
    /// <see cref="FireField"/> (its floor found by a ray down, its rain from CurrentWeather, its water from CurrentWater), draws it
    /// (<see cref="FireLook"/>, <see cref="FireMarks"/>) and burns whoever stands in it (FireScene.Bodies.cs). Anything that lights, pours or
    /// douses calls the static methods here; what this machine does itself is raised as <see cref="Happened"/> for FireSync to send, and what
    /// other machines did arrives back as cosmetic fire (it shows, it burns nobody here).
    /// </summary>
    public sealed partial class FireScene : MonoBehaviour
    {
        /// <summary>Pours are sent this often at most, the litres since added up (a pour is many small ticks).</summary>
        public const float PourSendSeconds = 0.1f;

        public static FireScene Instance { get; private set; }

        /// <summary>Raised for what this machine did (never for another machine's replayed fire).</summary>
        public static event Action<FireEvent> Happened;

        private FireField _field;
        private FireLook _look;
        private readonly List<GameObject> _lighters = new List<GameObject>();
        private float _pourPending;
        private Vector3 _pourPoint;
        private float _pourDue;

        public FireField Field => _field;

        /// <summary>How many cells burn right now (none without a fire scene).</summary>
        public static int BurningCount => Instance != null ? Instance._field.BurningCount : 0;

        /// <summary>
        /// The fire scene, made on first use. Only while playing: in the editor's edit mode Awake never runs (the flamethrower's and the
        /// staffs' edit-mode tests tick their streams there), so there is no fire to light and nothing is made.
        /// </summary>
        public static FireScene Ensure()
        {
            if (!Application.isPlaying)
            {
                return null;
            }
            if (Instance == null)
            {
                var go = new GameObject("FireScene");
                Instance = go.AddComponent<FireScene>();
            }
            return Instance;
        }

        private void Awake()
        {
            Instance = this;
            _field = new FireField(new System.Random(), FloorUnder, point => CurrentWeather.RainOn(point), point => CurrentWater.DepthAt(point) > 0.05f);
            _look = new FireLook(transform);
            FireMarks.Watch(_field);
        }

        private void OnDestroy()
        {
            FireMarks.Clear();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>Gasoline onto the ground under <paramref name="point"/>. False where there is no floor to take it.</summary>
        public static bool Pour(Vector3 point, float litres, bool cosmetic = false)
        {
            FireScene scene = Ensure();
            if (scene == null || !scene._field.Pour(point, litres))
            {
                return false;
            }
            if (!cosmetic)
            {
                scene._pourPending += litres;
                scene._pourPoint = point;
            }
            return true;
        }

        /// <summary>
        /// Lights the ground within <paramref name="radius"/> of <paramref name="point"/>: the gasoline there and, unless
        /// <paramref name="onlyFuel"/>, the bare ground (which burns a few seconds). <paramref name="lighter"/> is who gets the credit for
        /// whoever it burns. Returns how many cells caught.
        /// </summary>
        public static int Ignite(Vector3 point, float radius, bool onlyFuel, GameObject lighter, bool cosmetic = false)
        {
            FireScene scene = Ensure();
            if (scene == null)
            {
                return 0;
            }
            int lit = scene._field.Ignite(point, radius, onlyFuel, cosmetic, scene.IdOf(lighter));
            if (lit > 0 && !cosmetic)
            {
                Happened?.Invoke(new FireEvent(FireAct.Ignite, point, radius, onlyFuel));
            }
            return lit;
        }

        /// <summary>
        /// A stream of fire (the flame staff's, the flamethrower's) sets the ground alight every <see cref="FireRules.IgniteTickSeconds"/>:
        /// the first thing it meets within its reach (bare ground burns briefly) and any gasoline under its length. <paramref name="due"/> is
        /// the caller's own timer; <paramref name="body"/> the one holding it, which the stream never counts as the ground.
        /// </summary>
        public static void IgniteAlongStream(Vector3 from, Vector3 direction, float reach, Transform body, GameObject lighter, ref float due, float deltaTime)
        {
            due -= deltaTime;
            if (due > 0.0f || direction.sqrMagnitude < 0.0001f || !Application.isPlaying)
            {
                return;
            }
            due = FireRules.IgniteTickSeconds;
            direction.Normalize();
            if (BloodSplats.Surface(from, direction, reach, body, out RaycastHit hit))
            {
                Ignite(hit.point, FireRules.StreamIgniteRadius, false, lighter);
                reach = hit.distance;
            }
            for (float along = 1.0f; along < reach; along += 1.0f)
            {
                Ignite(from + direction * along, FireRules.StreamIgniteRadius, true, lighter);
            }
        }

        /// <summary>Water on the ground within <paramref name="radius"/> of <paramref name="point"/>: the fire there goes out and the ground will not catch for a while.</summary>
        public static int Douse(Vector3 point, float radius, bool cosmetic = false)
        {
            if (Instance == null)
            {
                return 0;
            }
            int doused = Instance._field.Douse(point, radius);
            if (doused > 0 && !cosmetic)
            {
                Happened?.Invoke(new FireEvent(FireAct.Douse, point, radius, false));
            }
            return doused;
        }

        /// <summary>Takes every fire, mark and burning body away (a test, a new world).</summary>
        public static void Clear()
        {
            if (Instance == null)
            {
                return;
            }
            Instance._field.Clear();
            Instance._look.Clear();
            Instance._burning.Clear();
            Instance._pourPending = 0.0f;
            FireMarks.Clear();
            FireMarks.Watch(Instance._field);
        }

        private int IdOf(GameObject lighter)
        {
            if (lighter == null)
            {
                return 0;
            }
            int index = _lighters.IndexOf(lighter);
            if (index < 0)
            {
                index = _lighters.Count;
                _lighters.Add(lighter);
            }
            return index + 1;
        }

        private GameObject LighterOf(int id)
        {
            return id > 0 && id <= _lighters.Count ? _lighters[id - 1] : null;
        }

        /// <summary>The floor under a column near a height: the first surface a ray down meets that is not a body or something loose, and lies flat enough to hold fire.</summary>
        private static bool FloorUnder(float x, float z, float nearY, out float floorY)
        {
            var origin = new Vector3(x, nearY + FireRules.ProbeAboveMetres, z);
            if (BloodSplats.Surface(origin, Vector3.down, FireRules.ProbeReachMetres, null, out RaycastHit hit) && hit.normal.y > 0.5f)
            {
                floorY = hit.point.y;
                return true;
            }
            floorY = 0.0f;
            return false;
        }

        private void Update()
        {
            float deltaTime = Mathf.Min(Time.deltaTime, 0.1f);
            _field.Tick(deltaTime);
            FlushPour(deltaTime);
            Camera viewer = Camera.main;
            _look.Tick(_field.Burning, deltaTime, viewer != null ? viewer.transform.position : transform.position);
            FireMarks.Tick(_field);
            TickBodies(deltaTime);
        }

        private void FlushPour(float deltaTime)
        {
            _pourDue -= deltaTime;
            if (_pourDue > 0.0f || _pourPending <= 0.0f)
            {
                return;
            }
            _pourDue = PourSendSeconds;
            Happened?.Invoke(new FireEvent(FireAct.Pour, _pourPoint, _pourPending, false));
            _pourPending = 0.0f;
        }
    }
}
