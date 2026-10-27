using System.IO;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Writes a captured image as a PNG. Through reflection: the image-conversion module is not among this test assembly's references, so the
    /// captures have each called it this way; new ones call this.
    /// </summary>
    public static class CapturePng
    {
        public static void Write(Texture2D image, string path)
        {
            System.Type conversion = System.Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            var encode = conversion.GetMethod("EncodeToPNG", new[] { typeof(Texture2D) });
            File.WriteAllBytes(path, (byte[])encode.Invoke(null, new object[] { image }));
        }
    }
}
