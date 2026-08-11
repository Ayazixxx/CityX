using UnityEngine;

namespace CityX.UI
{
    /// <summary>
    /// Drifts the title screen's camera over the night city.
    ///
    /// THE REAL CITY, NOT A BACKDROP. The Godot original built a hand-listed
    /// skyline just for the menu, because its city was expensive to generate.
    /// This one is a pure function of a seed and streams itself, so the title
    /// screen can simply fly over the actual game world -- which means it can
    /// never drift out of sync with how the game looks, and there is no second
    /// skyline to maintain.
    /// </summary>
    public sealed class TitleBackdrop : MonoBehaviour
    {
        [Tooltip("Metres per second the camera drifts. Slow -- this is a mood, not a flight.")]
        public float DriftSpeed = 3.5f;

        [Tooltip("Degrees per second of slow turn, so the skyline keeps changing.")]
        public float TurnSpeed = 1.2f;

        [Tooltip("How far the camera rises and falls, in metres.")]
        public float BobHeight = 4.0f;

        [Tooltip("Seconds for one rise and fall.")]
        public float BobPeriod = 22.0f;

        private Vector3 _origin;
        private float _elapsed;

        private void Start()
        {
            _origin = transform.position;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;

            // Forward along the camera's own heading, so the turn curves the
            // path rather than sliding the camera sideways along a fixed line.
            transform.Rotate(Vector3.up, TurnSpeed * Time.deltaTime, Space.World);

            Vector3 forward = transform.forward;
            forward.y = 0.0f;
            _origin += forward.normalized * (DriftSpeed * Time.deltaTime);

            float bob = Mathf.Sin(_elapsed / BobPeriod * Mathf.PI * 2.0f) * BobHeight;
            transform.position = _origin + Vector3.up * bob;
        }
    }
}
