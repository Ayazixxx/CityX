namespace CityX.Character
{
    /// <summary>
    /// The bottle's hold-right-to-aim, left-to-throw-or-swing sequencing,
    /// ported from player.gd's own equipped-bottle branch. A plain C# class
    /// rather than a MonoBehaviour, the same reasoning GrabCarry.Tick uses --
    /// it takes button edges as parameters instead of reading Mouse.current
    /// itself, so the sequencing is testable without any Unity input object.
    /// </summary>
    public sealed class WeaponAimInput
    {
        public enum Action
        {
            None,
            Melee,
            Throw,
            CancelAim,
        }

        public bool IsAiming { get; private set; }

        public Action Tick(bool rightPressedThisFrame, bool rightReleasedThisFrame, bool leftPressedThisFrame)
        {
            if (IsAiming)
            {
                if (leftPressedThisFrame)
                {
                    IsAiming = false;
                    return Action.Throw;
                }
                if (rightReleasedThisFrame)
                {
                    IsAiming = false;
                    return Action.CancelAim;
                }
                return Action.None;
            }

            if (rightPressedThisFrame)
            {
                IsAiming = true;
                return Action.None;
            }
            if (leftPressedThisFrame)
            {
                return Action.Melee;
            }
            return Action.None;
        }
    }
}
