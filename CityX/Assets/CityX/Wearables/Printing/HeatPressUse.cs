using CityX.Character;
using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// The heat press names itself when looked at, and E opens it (docs/HEAT_PRESS.md): the same look as the dropped items and the fuse box's
    /// lever (HoverTarget), so the press is used only when it is the thing looked at. Quiet while it is pressing.
    /// </summary>
    [RequireComponent(typeof(HeatPress))]
    public sealed class HeatPressUse : HoverTarget
    {
        public const string Name = "Heat Press";
        public const string UseKeys = "[E] Print a shirt";

        private HeatPress _press;

        private HeatPress Press => _press != null ? _press : (_press = GetComponent<HeatPress>());

        /// <summary>The pad's middle, where the eye goes to use it.</summary>
        public override Vector3 Centre => transform.TransformPoint(new Vector3(0.0f, HeatPressShape.PadTop + 0.1f, 0.0f));

        public override string Label => Name;

        public override string Keys => UseKeys;

        public override float ReachMetres => 2.6f;

        public override float LookDot => 0.7f;

        public override bool Available => Press != null && !Press.Pressing;

        public override void Use(GameObject player) => Press?.RequestUse(player);
    }
}
