using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// ACID RAIN FROM THE CLOUDS (owner, 2026-10-15: "same with acid rain make that appear from the height of the clouds"): the storm's ceiling is
    /// the cloud layer Self Smite's bolt comes from, a flier under it is in the storm, and the drops start just above whoever is watching.
    /// </summary>
    public sealed class AcidRainCloudTests
    {
        [Test]
        public void TheCloudsAreTheCloudLayerAndEveryoneUnderThemIsInTheStorm()
        {
            Assert.AreEqual(CloudLayer.HeightMetres, AcidRain.CloudHeight, 1e-3f);
            Vector3 centre = new Vector3(10.0f, 4.0f, -20.0f);
            Assert.IsTrue(AcidRain.IsUnder(centre, centre + new Vector3(20.0f, 1.8f, 0.0f)), "on the street");
            Assert.IsTrue(AcidRain.IsUnder(centre, centre + new Vector3(20.0f, 300.0f, 0.0f)), "a jetpack 300 m up is still under the clouds");
            Assert.IsFalse(AcidRain.IsUnder(centre, centre + new Vector3(20.0f, 1600.0f, 0.0f)), "above the clouds is above the rain");
            Assert.IsFalse(AcidRain.IsUnder(centre, centre + new Vector3(AcidRain.Radius + 1.0f, 2.0f, 0.0f)), "outside the circle");
            Assert.Greater(AcidRain.CloudRadius, AcidRain.Radius * 10.0f, "a storm cloud is far wider than its shower");
        }

        [Test]
        public void TheDropsStartJustAboveWhoeverWatchesNeverLowerThanTheRooftopsNorInTheClouds()
        {
            const float centre = 50.0f;
            Assert.AreEqual(centre + AcidRain.RainFromHeight, AcidRain.RainStartY(centre, centre + 1.7f), 1e-3f, "from the street: as before");
            Assert.AreEqual(centre + 300.0f + AcidRain.RainAboveViewer, AcidRain.RainStartY(centre, centre + 300.0f), 1e-3f,
                "a flier is rained on from just above");
            Assert.Less(AcidRain.RainStartY(centre, centre + 5000.0f), centre + AcidRain.CloudHeight, "never from inside the clouds");
            Assert.AreEqual(centre + AcidRain.RainFromHeight, AcidRain.RainStartY(centre, centre - 30.0f), 1e-3f, "a viewer below the centre");
        }
    }
}
