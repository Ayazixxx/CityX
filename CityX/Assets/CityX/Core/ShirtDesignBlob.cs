using System;

namespace CityX.Core
{
    /// <summary>
    /// A SHIRT'S PRINTED DESIGN AS BYTES (docs/HEAT_PRESS.md): the picture on its front and the one on its back, each a PNG (either may be
    /// missing), behind a short header. What the server keeps on disk and sends to whoever needs it, named by its content
    /// (<see cref="DesignStore.IdOf"/>). Checked on the way in: a design from another machine is only kept when it is one of these.
    /// </summary>
    public static class ShirtDesignBlob
    {
        private static readonly byte[] Magic = { (byte)'C', (byte)'X', (byte)'S', (byte)'D' };

        public const byte Version = 1;

        /// <summary>The header: the magic, the version, then each side's length (4 bytes each).</summary>
        public const int HeaderBytes = 4 + 1 + 4 + 4;

        /// <summary>The most one side's picture may weigh: a photograph on the whole print area fits.</summary>
        public const int MaxSideBytes = 640 * 1024;

        private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        /// <summary>The design's bytes; an empty or null side is left blank.</summary>
        public static byte[] Pack(byte[] frontPng, byte[] backPng)
        {
            frontPng = frontPng ?? Array.Empty<byte>();
            backPng = backPng ?? Array.Empty<byte>();
            var blob = new byte[HeaderBytes + frontPng.Length + backPng.Length];
            Buffer.BlockCopy(Magic, 0, blob, 0, Magic.Length);
            blob[4] = Version;
            WriteInt(blob, 5, frontPng.Length);
            WriteInt(blob, 9, backPng.Length);
            Buffer.BlockCopy(frontPng, 0, blob, HeaderBytes, frontPng.Length);
            Buffer.BlockCopy(backPng, 0, blob, HeaderBytes + frontPng.Length, backPng.Length);
            return blob;
        }

        /// <summary>The two pictures back out of a design; false for anything that is not a sane one (each side empty or a PNG, in bounds).</summary>
        public static bool TryUnpack(byte[] blob, out byte[] frontPng, out byte[] backPng)
        {
            frontPng = backPng = null;
            if (blob == null || blob.Length < HeaderBytes || blob.Length > MaxBytes || blob[4] != Version)
            {
                return false;
            }
            for (int i = 0; i < Magic.Length; i++)
            {
                if (blob[i] != Magic[i]) return false;
            }
            int front = ReadInt(blob, 5), back = ReadInt(blob, 9);
            if (front < 0 || back < 0 || front > MaxSideBytes || back > MaxSideBytes || HeaderBytes + front + back != blob.Length)
            {
                return false;
            }
            frontPng = new byte[front];
            backPng = new byte[back];
            Buffer.BlockCopy(blob, HeaderBytes, frontPng, 0, front);
            Buffer.BlockCopy(blob, HeaderBytes + front, backPng, 0, back);
            return IsPngOrEmpty(frontPng) && IsPngOrEmpty(backPng) && (front > 0 || back > 0);
        }

        /// <summary>The most a whole design weighs.</summary>
        public const int MaxBytes = HeaderBytes + 2 * MaxSideBytes;

        public static bool IsSane(byte[] blob) => TryUnpack(blob, out _, out _);

        private static bool IsPngOrEmpty(byte[] picture)
        {
            if (picture.Length == 0)
            {
                return true;
            }
            if (picture.Length < PngSignature.Length)
            {
                return false;
            }
            for (int i = 0; i < PngSignature.Length; i++)
            {
                if (picture[i] != PngSignature[i]) return false;
            }
            return true;
        }

        private static void WriteInt(byte[] into, int at, int value)
        {
            into[at] = (byte)value;
            into[at + 1] = (byte)(value >> 8);
            into[at + 2] = (byte)(value >> 16);
            into[at + 3] = (byte)(value >> 24);
        }

        private static int ReadInt(byte[] from, int at) => from[at] | (from[at + 1] << 8) | (from[at + 2] << 16) | (from[at + 3] << 24);
    }
}
