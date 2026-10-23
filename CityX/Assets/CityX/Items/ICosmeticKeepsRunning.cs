namespace CityX.Items
{
    /// <summary>
    /// An item whose own component stays on when it is shown on another player's copy (NetworkedEquipmentSync switches every other script on
    /// a copy's model off): clothing keeps up what the body under it needs each frame (the hooded cloak's sleeve spread). Its pickup and the
    /// rest stay dead with its colliders.
    /// </summary>
    public interface ICosmeticKeepsRunning
    {
    }
}
