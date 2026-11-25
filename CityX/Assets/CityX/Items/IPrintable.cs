namespace CityX.Items
{
    /// <summary>
    /// Something a design can be printed on (a T-shirt at the heat press, docs/HEAT_PRESS.md). It shows the design, worn or lying on the
    /// ground, and carries it in its instance data (<see cref="ItemInstanceData.DesignId"/>); another player's copy of it is shown the design
    /// by id (Inventory.ShownDesign).
    /// </summary>
    public interface IPrintable
    {
        /// <summary>The design on it now; empty for none.</summary>
        string DesignId { get; }

        /// <summary>Shows <paramref name="designId"/> on it (empty: plain). A design not on this machine yet shows once it has been fetched.</summary>
        void ShowDesign(string designId);
    }
}
