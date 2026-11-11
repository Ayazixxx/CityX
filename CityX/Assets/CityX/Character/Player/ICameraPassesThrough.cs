namespace CityX.Character
{
    /// <summary>
    /// Something the following camera goes through as if it were air, rather than coming in close in front of it (ThirdPersonCamera; owner,
    /// 2026-11-10: "make the ingame camera move through the wraith as if it were air, not an object that would switch the camera angle if it
    /// blocks the view"). On the thing's own component; its colliders and its children's count as it.
    /// </summary>
    public interface ICameraPassesThrough
    {
    }
}
