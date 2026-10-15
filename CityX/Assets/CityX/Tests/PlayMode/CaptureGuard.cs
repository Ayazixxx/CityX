using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// A RENDER CAPTURE IS SKIPPED WITHOUT A GRAPHICS DEVICE, not run: a regex -Filter runs [Explicit] captures too, and Camera.Render with no
    /// graphics device crashed Unity outright (2026-10-15, ShootaCapture) or failed on RenderTexture.Create (StepUpCapture). Every capture calls
    /// this first.
    /// </summary>
    public static class CaptureGuard
    {
        public static void NeedsGraphics()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Assert.Ignore("renders screenshots: needs a graphics device (run with -Graphics)");
            }
        }
    }
}
