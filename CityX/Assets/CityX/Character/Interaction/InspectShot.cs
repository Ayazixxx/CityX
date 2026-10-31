using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// WHERE THE CAMERA GOES TO LOOK AT AN ITEM, and where its appraisal card stands (docs/ITEM_INSPECT.md). The card is a sheet beside the
    /// item on the viewer's right, upright, leaning to face the camera, as Sword Art Online's hangs beside the sword. The camera swings round
    /// to the card's side (so the body that stood behind the item is out of the way), looks down a little, and comes in close enough that the
    /// card's writing is legible (the owner: "zooms into it a bit so its legible"). The move between the camera's own place and this one is
    /// eased (<see cref="Blend"/>), never a cut ("make it smoothly transition into it not just snap"). Plain maths, tested without a scene.
    /// </summary>
    public readonly struct InspectShot
    {
        /// <summary>The card's size in metres (the player is 2.6 m tall): a sheet about the size of a door's window.</summary>
        public const float CardWidth = 0.9f;

        public const float CardHeight = 1.15f;

        /// <summary>The gap between the item's middle and the card's near edge, and how high the card's bottom sits over the item.</summary>
        public const float CardGap = 0.3f;

        public const float CardLift = 0.1f;

        /// <summary>How far the camera stands from the point between the item and the card: at 50 degrees the card fills near half the height.</summary>
        public const float Distance = 2.7f;

        /// <summary>How far round to the card's side the camera swings from where it looked from, and how far down it looks.</summary>
        public const float SwingDegrees = 40.0f;

        public const float LookDownDegrees = 18.0f;

        /// <summary>The nearest the camera is pulled in to by a wall behind it.</summary>
        public const float LeastDistance = 1.2f;

        public readonly Vector3 Focus;
        public readonly Vector3 CameraPosition;
        public readonly Quaternion CameraRotation;
        public readonly Vector3 CardPosition;
        public readonly Quaternion CardRotation;
        public readonly float CameraDistance;

        private InspectShot(Vector3 focus, Vector3 cameraPosition, Quaternion cameraRotation, Vector3 cardPosition, Quaternion cardRotation, float distance)
        {
            Focus = focus;
            CameraPosition = cameraPosition;
            CameraRotation = cameraRotation;
            CardPosition = cardPosition;
            CardRotation = cardRotation;
            CameraDistance = distance;
        }

        /// <summary>The shot of the item at <paramref name="itemCentre"/> for a camera that looked at it from <paramref name="viewFrom"/>.</summary>
        public static InspectShot For(Vector3 itemCentre, Vector3 viewFrom, float distance = Distance)
        {
            Vector3 approach = itemCentre - viewFrom;
            approach.y = 0.0f;
            Vector3 forward = approach.sqrMagnitude > 1e-4f ? approach.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 cardPosition = itemCentre + right * (CardWidth * 0.5f + CardGap) + Vector3.up * (CardHeight * 0.5f + CardLift);

            Vector3 cameraFlat = Quaternion.AngleAxis(-SwingDegrees, Vector3.up) * forward;
            Vector3 cameraRight = Vector3.Cross(Vector3.up, cameraFlat);
            Vector3 cameraForward = Quaternion.AngleAxis(LookDownDegrees, cameraRight) * cameraFlat;
            Vector3 focus = Vector3.Lerp(itemCentre, cardPosition, 0.55f);
            Vector3 cameraPosition = focus - cameraForward * Mathf.Max(LeastDistance, distance);
            Quaternion cameraRotation = Quaternion.LookRotation(cameraForward, Vector3.up);
            // A canvas reads from along its own forward: the card's forward runs from the camera through it, so it faces the camera, leaning.
            Quaternion cardRotation = Quaternion.LookRotation(cardPosition - cameraPosition, Vector3.up);
            return new InspectShot(focus, cameraPosition, cameraRotation, cardPosition, cardRotation, Mathf.Max(LeastDistance, distance));
        }

        /// <summary>The move's easing: still at both ends, quickest in the middle (a smoothstep), so neither end snaps.</summary>
        public static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3.0f - 2.0f * t);
        }

        /// <summary>The camera <paramref name="t"/> of the way (0 to 1, eased here) from one place to another.</summary>
        public static void Blend(Vector3 fromPosition, Quaternion fromRotation, Vector3 toPosition, Quaternion toRotation, float t,
            out Vector3 position, out Quaternion rotation)
        {
            float eased = Ease(t);
            position = Vector3.LerpUnclamped(fromPosition, toPosition, eased);
            rotation = Quaternion.Slerp(fromRotation, toRotation, eased);
        }
    }
}
