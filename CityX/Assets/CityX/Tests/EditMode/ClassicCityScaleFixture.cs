using CityX.World.Plan;
using NUnit.Framework;

/// <summary>
/// The tests were written against the classic city (see CityScale). The game itself starts at a larger scale by default, so the
/// tests pin the classic one for the whole assembly; CityScaleTests builds the larger plans explicitly.
/// </summary>
[SetUpFixture]
public sealed class ClassicCityScaleFixture
{
    [OneTimeSetUp]
    public void PinClassic()
    {
        CityScale.Active = CityScale.Classic;
    }
}
