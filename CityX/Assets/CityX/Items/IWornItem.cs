namespace CityX.Items
{
    /// <summary>
    /// Something worn on a body (a helmet, a hat, shoes, a shield) that says which item it is, so a fallen body's gear can be listed and taken
    /// off it (docs/LOOTING.md, bodies).
    /// </summary>
    public interface IWornItem
    {
        string WornItemId { get; }

        /// <summary>On a body now, not lying on the ground.</summary>
        bool IsWorn { get; }
    }
}
