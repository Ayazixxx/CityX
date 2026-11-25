using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

namespace CityX.UI
{
    /// <summary>
    /// A PICTURE OFF THIS COMPUTER (docs/HEAT_PRESS.md; owner, 2026-11-24: "make it use images off your computer"): Windows' own Open window,
    /// for PNG and JPG pictures, and the picture read into a texture. The window runs on a thread of its own, so the game keeps running (and
    /// keeps its place in a session) while it is open; what was picked is handed back on the game's own thread (<see cref="Poll"/>). Only on
    /// Windows, the game's platform; elsewhere it says so.
    /// </summary>
    public static class PictureFile
    {
        /// <summary>The largest picture file read, bytes: bigger is not a picture worth the wait.</summary>
        public const int MaxFileBytes = 40 * 1024 * 1024;

        /// <summary>A picture is brought down to this many pixels on its longer side as it is read: more than a shirt can show.</summary>
        public const int MaxSide = 1024;

        public const string Filter = "Pictures (PNG, JPG)\0*.png;*.jpg;*.jpeg\0\0";

        private static readonly ConcurrentQueue<string> Picked = new ConcurrentQueue<string>();
        private static int _open;

        /// <summary>Whether the Open window is up.</summary>
        public static bool IsOpen => Volatile.Read(ref _open) != 0;

        public static bool Supported => Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor;

        /// <summary>Opens Windows' Open window for a picture (nothing if one is up already). The path picked comes out of <see cref="Poll"/>.</summary>
        public static bool Ask(string title)
        {
            if (!Supported || Interlocked.CompareExchange(ref _open, 1, 0) != 0)
            {
                return false;
            }
            IntPtr owner = GetActiveWindow();
            var thread = new Thread(() =>
            {
                try
                {
                    CoInitializeEx(IntPtr.Zero, CoinitApartmentThreaded);
                    string path = Show(owner, title);
                    Picked.Enqueue(path ?? string.Empty);   // empty: closed without picking
                    CoUninitialize();
                }
                catch (Exception)
                {
                    Picked.Enqueue(string.Empty);
                }
                finally
                {
                    Volatile.Write(ref _open, 0);
                }
            }) { IsBackground = true, Name = "PictureFile" };
            thread.Start();
            return true;
        }

        /// <summary>The path picked since last asked: null while nothing came back, empty when the window was closed without picking.</summary>
        public static string Poll() => Picked.TryDequeue(out string path) ? path : null;

        /// <summary>
        /// Reads a picture file into a texture (rows bottom up), brought down to <see cref="MaxSide"/> on its longer side; null, with why, when
        /// it cannot be read.
        /// </summary>
        public static Texture2D Read(string path, out string problem)
        {
            problem = null;
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists)
                {
                    problem = "That file is gone.";
                    return null;
                }
                if (info.Length > MaxFileBytes)
                {
                    problem = "That picture is too big a file (over 40 MB).";
                    return null;
                }
                var picture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = Path.GetFileName(path) };
                if (!picture.LoadImage(File.ReadAllBytes(path)))
                {
                    UnityEngine.Object.DestroyImmediate(picture);
                    problem = "That file is not a picture the game can read: use a PNG or a JPG.";
                    return null;
                }
                return Smaller(picture);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                problem = "That file could not be opened.";
                return null;
            }
        }

        /// <summary>The picture brought down to <see cref="MaxSide"/> (through the graphics card); itself when small enough.</summary>
        private static Texture2D Smaller(Texture2D picture)
        {
            int longest = Mathf.Max(picture.width, picture.height);
            if (longest <= MaxSide)
            {
                return picture;
            }
            float scale = MaxSide / (float)longest;
            int width = Mathf.Max(2, Mathf.RoundToInt(picture.width * scale)), height = Mathf.Max(2, Mathf.RoundToInt(picture.height * scale));
            RenderTexture target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture before = RenderTexture.active;
            var small = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = picture.name };
            try
            {
                Graphics.Blit(picture, target);
                RenderTexture.active = target;
                small.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                small.Apply(false);
            }
            finally
            {
                RenderTexture.active = before;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(picture);
            }
            return small;
        }

        // ---- Windows' Open window (comdlg32) ----

        private const int CoinitApartmentThreaded = 2;
        private const int OfnPathMustExist = 0x800, OfnFileMustExist = 0x1000, OfnNoChangeDir = 0x8, OfnExplorer = 0x80000;
        private const int MaxPath = 4096;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct OpenFileName
        {
            public int lStructSize;
            public IntPtr hwndOwner;
            public IntPtr hInstance;
            public string lpstrFilter;
            public string lpstrCustomFilter;
            public int nMaxCustFilter;
            public int nFilterIndex;
            public IntPtr lpstrFile;
            public int nMaxFile;
            public string lpstrFileTitle;
            public int nMaxFileTitle;
            public string lpstrInitialDir;
            public string lpstrTitle;
            public int Flags;
            public short nFileOffset;
            public short nFileExtension;
            public string lpstrDefExt;
            public IntPtr lCustData;
            public IntPtr lpfnHook;
            public string lpTemplateName;
            public IntPtr pvReserved;
            public int dwReserved;
            public int FlagsEx;
        }

        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetOpenFileNameW(ref OpenFileName ofn);

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        [DllImport("ole32.dll")]
        private static extern int CoInitializeEx(IntPtr reserved, int coInit);

        [DllImport("ole32.dll")]
        private static extern void CoUninitialize();

        private static string Show(IntPtr owner, string title)
        {
            IntPtr buffer = Marshal.AllocHGlobal(MaxPath * 2);
            try
            {
                Marshal.Copy(new byte[MaxPath * 2], 0, buffer, MaxPath * 2);
                var dialog = new OpenFileName
                {
                    lStructSize = Marshal.SizeOf<OpenFileName>(),
                    hwndOwner = owner,
                    lpstrFilter = Filter,
                    nFilterIndex = 1,
                    lpstrFile = buffer,
                    nMaxFile = MaxPath,
                    lpstrInitialDir = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                    lpstrTitle = title,
                    Flags = OfnPathMustExist | OfnFileMustExist | OfnNoChangeDir | OfnExplorer,
                };
                return GetOpenFileNameW(ref dialog) ? Marshal.PtrToStringUni(buffer) : null;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }
}
