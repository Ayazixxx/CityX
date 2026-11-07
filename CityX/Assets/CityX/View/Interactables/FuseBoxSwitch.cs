using CityX.Character;
using CityX.World;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// THE FUSE BOX'S LEVER in Old Guy's house (docs/OLD_GUYS_HOUSE.md; docs/GAME_DESIGN.md: "Old Guy has you turn its power back on, then
    /// you claim it"): looked at, it names itself under the crosshair; E throws it over, with a switch's click, and the house's power comes
    /// on (TutorialHousePower) for good. The lever turns on a hinge at its middle and flips in place on the panel from pointing up to pointing down; a lever found
    /// already thrown (the house rebuilt, or thrown by another player: the power is shared in a session) hangs down from the start.
    /// </summary>
    public sealed class FuseBoxSwitch : HoverTarget
    {
        public const string Name = "Fuse box";
        public const string UseKeys = "[E] Turn the power on";

        /// <summary>The lever flips this far about its middle (up to down), in this long.</summary>
        public const float ThrownDegrees = 180.0f;

        public const float ThrowSeconds = 0.3f;

        [Tooltip("The lever's mesh, turned about this hinge (its middle) when thrown.")]
        public Transform Lever;

        private float _degrees;

        public override Vector3 Centre => transform.position;

        public override string Label => Name;

        public override string Keys => UseKeys;

        public override bool Available => !IsThrown;

        public bool IsThrown => TutorialHousePower.Shared.IsOn;

        /// <summary>How far the lever has swung, degrees (for tests).</summary>
        public float LeverDegrees => _degrees;

        private void Start()
        {
            if (IsThrown)
            {
                Set(ThrownDegrees);
            }
        }

        private void Update()
        {
            float target = IsThrown ? ThrownDegrees : 0.0f;
            if (!Mathf.Approximately(_degrees, target))
            {
                Set(Mathf.MoveTowards(_degrees, target, ThrownDegrees / ThrowSeconds * Time.deltaTime));
            }
        }

        private void Set(float degrees)
        {
            _degrees = degrees;
            if (Lever != null)
            {
                Lever.localRotation = Quaternion.AngleAxis(degrees, Vector3.right);
            }
        }

        public override void Use(GameObject player)
        {
            if (IsThrown)
            {
                return;
            }
            TutorialHousePower.Shared.TurnOn();
            PositionalSound.Play(SwitchClick.Clip, transform.position, 0.9f, 0.8f, 0.0f, 14.0f);
        }
    }
}
