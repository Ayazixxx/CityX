using System.Threading.Tasks;
using CityX.View;
using CityX.World.Plan;
using UnityEngine;

namespace CityX.UI
{
    /// <summary>
    /// The title's backdrop is the game's own city (owner, 2026-11-11: "make the city in the background look like the ingame cities that
    /// generate ingame ... just the look of the ingame buildings"): the game's streamer builds it round a real rooftop chosen from the plan
    /// (<see cref="TitleViewpoint"/>), and the camera, the character on the roof and their lamps are put there. The camera never moves, so once
    /// the city round it is built the streamer has nothing more to do: the title costs what drawing those buildings costs. Nothing on the title
    /// can break a window or open a door (none of the game's interactors is here).
    ///
    /// THE CITY'S PLAN IS WORKED OUT ON A WORKER (owner, 2026-11-11: "the title screen takes quiet long to boot"): it takes some seconds,
    /// and the streamers asked for it on their first frame, so the title stood still that long before its first picture. The city, the
    /// skyline and the rooftop are saved switched off; the title opens at once on the menu over black (<see cref="Curtain"/>), and once the
    /// plan is there they are switched on, placed, and the black fades away. The plan is kept, so the game itself does not work it out again.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class TitleStage : MonoBehaviour
    {
        [Tooltip("The game's city streamer, streaming round the camera.")]
        public CityStreamer City;

        [Tooltip("The game's skyline beyond the streamed ring.")]
        public SkylineStreamer Skyline;

        [Tooltip("The title's fixed camera.")]
        public Camera View;

        [Tooltip("The character on the roof with their lamps: put on the chosen roof.")]
        public Transform Rooftop;

        [Tooltip("Where the character's feet are seen (0,0 bottom left, 1,1 top right) on a 16:9 view, and how far from the camera, metres.")]
        public Vector2 FeetInView = new Vector2(0.76f, 0.10f);

        public float FeetDistance = 5.6f;

        [Tooltip("How far the camera looks down, degrees.")]
        public float Pitch = 2.6f;

        [Tooltip("How far the character is turned from facing the camera, degrees, toward the middle of the screen: a three-quarter view.")]
        public float HeroTurn = 28.0f;

        [Tooltip("Black over the city until it is there, under the menu.")]
        public CanvasGroup Curtain;

        /// <summary>How long the black takes to fade away once the city is there, seconds.</summary>
        public const float CurtainFadeSeconds = 0.6f;

        [Tooltip("Chunks built at once when the title opens, nearest first, before the first picture; the rest stream in behind.")]
        public int BuiltAtOnce = 49;

        /// <summary>The 16:9 view the placing is worked out on: wider screens see more city at the sides.</summary>
        private const float DesignAspect = 16.0f / 9.0f;

        /// <summary>The roof chosen, once placed.</summary>
        public TitleViewpoint.Pick Chosen { get; private set; }

        private Task _planning;

        /// <summary>The city, the rooftop and the camera are where they belong (the plan was ready).</summary>
        public bool Placed { get; private set; }

        private void Awake()
        {
            if (Curtain != null)
            {
                Curtain.alpha = 1.0f;
            }
            if (City == null)
            {
                return;
            }
            int seed = City.WorldSeed;
            _planning = Task.Run(() => PlanWorld.For(seed));   // PlanPrewarm may have it under way: For waits for that, never doing it twice
        }

        private void Update()
        {
            if (_planning != null && _planning.IsCompleted)
            {
                if (_planning.Exception != null)
                {
                    Debug.LogException(_planning.Exception);
                }
                _planning = null;
                City.gameObject.SetActive(true);   // its Awake makes its world from the plan now there
                if (Skyline != null)
                {
                    Skyline.gameObject.SetActive(true);
                }
                Place();
                if (Rooftop != null)
                {
                    Rooftop.gameObject.SetActive(true);
                }
                Placed = true;
            }
            if (Placed && Curtain != null && Curtain.alpha > 0.0f)
            {
                Curtain.alpha = Mathf.MoveTowards(Curtain.alpha, 0.0f, Time.deltaTime / CurtainFadeSeconds);
            }
        }

        /// <summary>Chooses the roof and puts the camera, the character and the city's viewer there, and builds the nearest of the city.</summary>
        public void Place()
        {
            if (City == null || View == null)
            {
                return;
            }
            if (City.World == null)
            {
                City.Rebuild();
            }
            Chosen = TitleViewpoint.Choose(City.World.Plan);
            if (!Chosen.Found)
            {
                Debug.LogWarning("CityX: no roof in this world for the title to stand on; the camera stays where it was.");
            }
            else
            {
                // Out over the edge, the character just in from it facing back: the city behind them.
                Quaternion look = Quaternion.LookRotation(Chosen.Out, Vector3.up) * Quaternion.Euler(Pitch, 0.0f, 0.0f);
                Vector3 feetFromEye = look * FeetFromEye(View.fieldOfView);
                View.transform.SetPositionAndRotation(Chosen.Feet - feetFromEye, look);
                if (Rooftop != null)
                {
                    // Lined up with the view (its +Z straight ahead, as the first rooftop was built from the camera), and the character facing
                    // the camera, turned toward the middle of the screen.
                    Rooftop.SetPositionAndRotation(Chosen.Feet, Quaternion.LookRotation(Chosen.Out, Vector3.up));
                    var hero = Rooftop.GetComponentInChildren<TitleHero>(true);
                    if (hero != null)
                    {
                        Vector3 toCamera = Vector3.ProjectOnPlane(View.transform.position - hero.transform.position, Vector3.up).normalized;
                        hero.transform.rotation = Quaternion.AngleAxis(HeroTurn, Vector3.up) * Quaternion.LookRotation(toCamera, Vector3.up);
                    }
                }
            }
            City.Viewer = View.transform;
            if (Skyline != null)
            {
                Skyline.Viewer = View.transform;
            }
            City.StreamAround(View.transform.position, BuiltAtOnce);
        }

        /// <summary>The character's feet from the eye, in the eye's own frame (x right, y up, z ahead), for a view this wide.</summary>
        private Vector3 FeetFromEye(float fieldOfView)
        {
            float halfHeight = FeetDistance * Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);
            return new Vector3((2.0f * FeetInView.x - 1.0f) * halfHeight * DesignAspect, (2.0f * FeetInView.y - 1.0f) * halfHeight, FeetDistance);
        }
    }
}
