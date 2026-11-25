using System;
using System.IO;
using System.Security.Cryptography;

namespace CityX.Core
{
    /// <summary>
    /// EVERY PRINTED DESIGN THIS MACHINE KNOWS, KEPT ON DISK FOR GOOD (docs/HEAT_PRESS.md; owner, 2026-11-24: "I want the design to stay in the game
    /// forever unless the item is lost somehow ... i want other peoples designs to stay on the shirts so you can come across random shirts that
    /// others designed"). A design (<see cref="ShirtDesignBlob"/>) is named by its own content, so the same design always has the same id on
    /// every machine and an id can never point at another picture: a shirt carries only the id. The server keeps every design pressed on it;
    /// a client keeps the ones it has been sent, in its own data folder beside its own server's.
    /// </summary>
    public sealed class DesignStore
    {
        /// <summary>An id's length: the first 32 hex digits of the design's SHA-256.</summary>
        public const int IdLength = 32;

        public const string Extension = ".design";

        /// <summary>This machine's store, under its server's data (<see cref="CharacterDatabase.LocalServerRoot"/>).</summary>
        public static DesignStore Local => _local != null && _local.Root == LocalRoot ? _local : (_local = new DesignStore(LocalRoot));

        private static DesignStore _local;

        private static string LocalRoot => Path.Combine(CharacterDatabase.LocalServerRoot, "designs");

        public string Root { get; }

        public DesignStore(string root)
        {
            Root = root;
        }

        /// <summary>A design's id: its content's fingerprint.</summary>
        public static string IdOf(byte[] blob)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(blob ?? Array.Empty<byte>());
                var id = new char[IdLength];
                const string digits = "0123456789abcdef";
                for (int i = 0; i < IdLength / 2; i++)
                {
                    id[i * 2] = digits[hash[i] >> 4];
                    id[i * 2 + 1] = digits[hash[i] & 15];
                }
                return new string(id);
            }
        }

        /// <summary>Whether <paramref name="id"/> could be a design's id (so it is safe as a file name and in a shown item id).</summary>
        public static bool IsValidId(string id)
        {
            if (id == null || id.Length != IdLength)
            {
                return false;
            }
            foreach (char c in id)
            {
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
            }
            return true;
        }

        private string PathOf(string id) => Path.Combine(Root, id + Extension);

        public bool Has(string id) => IsValidId(id) && File.Exists(PathOf(id));

        /// <summary>Keeps a design; its id, or null when it is not a sane design.</summary>
        public string Keep(byte[] blob)
        {
            if (!ShirtDesignBlob.IsSane(blob))
            {
                return null;
            }
            string id = IdOf(blob);
            string path = PathOf(id);
            if (!File.Exists(path))
            {
                Directory.CreateDirectory(Root);
                string temporary = path + ".tmp";
                File.WriteAllBytes(temporary, blob);
                if (File.Exists(path)) File.Delete(temporary);
                else File.Move(temporary, path);
            }
            return id;
        }

        /// <summary>A design kept here, checked again on the way out (a file damaged on disk is not handed on).</summary>
        public bool TryLoad(string id, out byte[] blob)
        {
            blob = null;
            if (!Has(id))
            {
                return false;
            }
            try
            {
                blob = File.ReadAllBytes(PathOf(id));
            }
            catch (IOException)
            {
                return false;
            }
            if (IdOf(blob) != id || !ShirtDesignBlob.IsSane(blob))
            {
                blob = null;
                return false;
            }
            return true;
        }
    }
}
