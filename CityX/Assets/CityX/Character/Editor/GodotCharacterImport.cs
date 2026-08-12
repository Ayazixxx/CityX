using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using CityX.Character;
using CityX.Core;
using UnityEditor;
using UnityEngine;

namespace CityX.CharacterEditor
{
    /// <summary>
    /// Brings characters painted in the Godot build across into this one.
    ///
    /// The two paint atlases are the same picture: 384 by 448, seven 64-pixel
    /// rows, in the same order -- head, hips, neck, both arms, both legs. That
    /// is not a coincidence, it is PaintAtlas being a port of character.gd's
    /// own constants. So an import is a format change, not a re-authoring: read
    /// Godot's ConfigFile, pull the PNG out of each character's atlas_png, and
    /// write it back as the gzipped raw RGBA this build saves.
    ///
    /// WHAT SURVIVES EXACTLY AND WHAT DOES NOT, measured rather than hoped:
    ///
    ///   * The torso and both legs land pixel for pixel. PaintAtlas.Frame was
    ///     written to agree with fused_body.gd's own atan2(radial.z, radial.x)
    ///     on a vertical limb, and those three are vertical in both builds.
    ///   * The head lands pixel for pixel too, because HeadMesh is a port of
    ///     character.gd's _generate_sphere_verts down to the UV formula.
    ///   * THE ARMS COME OUT ROTATED AROUND THEIR OWN AXIS. Godot binds with
    ///     the arms hanging down; this rig binds in a T-pose, because Unity's
    ///     humanoid retargeting measures a rig against one. The angle round the
    ///     limb is therefore measured from a different reference direction. The
    ///     paint is all there, in the right band along the arm's length, turned
    ///     about it. There is no constant that fixes this without measuring it
    ///     against a real character, so it is reported rather than guessed at.
    ///
    /// Nothing is overwritten: every import creates a new character.
    /// </summary>
    public static class GodotCharacterImport
    {
        /// <summary>
        /// Where Godot put user:// for this project. The Godot build's own
        /// notes record the folder moving when the project was renamed, so the
        /// old names are searched too rather than only the current one.
        /// </summary>
        public static readonly string[] SearchNames = { "CITY X", "Slam City", "The Game" };

        private const string FileName = "characters.cfg";

        [MenuItem("CityX/Import Godot Characters")]
        public static void ImportMenu()
        {
            string path = Find();
            if (path == null)
            {
                Debug.LogError("CityX: no Godot characters.cfg found under " +
                               "%APPDATA%/Godot/app_userdata. Nothing to import.");
                return;
            }

            List<CharacterSave> imported = Import(File.ReadAllText(path), out List<string> notes);
            foreach (string note in notes)
            {
                Debug.LogWarning("CityX: " + note);
            }

            if (imported.Count == 0)
            {
                Debug.LogWarning($"CityX: {path} held no characters this build could read.");
                return;
            }

            foreach (CharacterSave character in imported)
            {
                CharacterStore.Add(character);
            }

            var names = new StringBuilder();
            foreach (CharacterSave character in imported)
            {
                names.Append(names.Length == 0 ? "" : ", ").Append(character.Name);
            }
            Debug.Log($"CityX: imported {imported.Count} character(s) from {path} -- {names}. " +
                      "Paint on the arms will be turned about the limb; see " +
                      "GodotCharacterImport for why.");
        }

        /// <summary>The newest characters.cfg Godot wrote, or null.</summary>
        public static string Find()
        {
            string roaming = System.Environment.GetFolderPath(
                System.Environment.SpecialFolder.ApplicationData);
            string best = null;
            System.DateTime newest = System.DateTime.MinValue;

            foreach (string project in SearchNames)
            {
                string path = Path.Combine(roaming, "Godot", "app_userdata", project, FileName);
                if (!File.Exists(path))
                {
                    continue;
                }
                System.DateTime written = File.GetLastWriteTimeUtc(path);
                if (written > newest)
                {
                    newest = written;
                    best = path;
                }
            }
            return best;
        }

        /// <summary>
        /// Parses the file's text into characters. Separated from the menu item
        /// so a test can drive it without a real Godot install.
        /// </summary>
        public static List<CharacterSave> Import(string text, out List<string> notes)
        {
            notes = new List<string>();
            var made = new List<CharacterSave>();

            foreach (Dictionary<string, string> section in Sections(text))
            {
                string name = Unquote(Value(section, "name")) ?? "Nobody";

                CharacterSave character = CharacterSave.Create(name, CharacterSave.DefaultSkin);
                character.Level = (int)Number(section, "level", 1.0);
                character.PlaytimeSeconds = Number(section, "playtime_seconds", 0.0);
                character.HairStyle = Unquote(Value(section, "hair_style")) ?? "none";

                byte[] png = Bytes(Value(section, "atlas_png"));
                if (png == null || png.Length == 0)
                {
                    notes.Add($"{name} had no painted atlas -- imported plain.");
                }
                else if (!Repaint(character, png, name, notes))
                {
                    notes.Add($"{name}'s atlas would not decode -- imported plain.");
                }

                made.Add(character);
            }

            return made;
        }

        /// <summary>
        /// Turns Godot's PNG into this build's own paint. Sized-checked first:
        /// an atlas of a different shape would put an arm's paint on a leg, and
        /// a plain body is a better outcome than a scrambled one.
        /// </summary>
        private static bool Repaint(CharacterSave character, byte[] png, string name,
            List<string> notes)
        {
            var image = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!ImageConversion.LoadImage(image, png, markNonReadable: false))
                {
                    return false;
                }

                if (image.width != PaintAtlas.Width || image.height != PaintAtlas.Height)
                {
                    notes.Add($"{name}'s atlas is {image.width}x{image.height}, not " +
                              $"{PaintAtlas.Width}x{PaintAtlas.Height} -- imported plain rather " +
                              "than stretched onto the wrong rows.");
                    return false;
                }

                var paint = new BodyPaint(Color.white);
                if (!paint.Adopt(image.GetPixels32()))
                {
                    return false;
                }

                character.SetPaint(paint.Encode());
                return true;
            }
            finally
            {
                Object.DestroyImmediate(image);
            }
        }

        // --- Godot's ConfigFile, which is an INI with typed values ---

        private static IEnumerable<Dictionary<string, string>> Sections(string text)
        {
            Dictionary<string, string> current = null;

            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim('\r', ' ', '\t');
                if (line.Length == 0)
                {
                    continue;
                }

                if (line[0] == '[')
                {
                    if (current != null)
                    {
                        yield return current;
                    }
                    current = new Dictionary<string, string>();
                    continue;
                }

                int equals = line.IndexOf('=');
                if (current == null || equals <= 0)
                {
                    continue;
                }
                current[line.Substring(0, equals).Trim()] = line.Substring(equals + 1).Trim();
            }

            if (current != null)
            {
                yield return current;
            }
        }

        private static string Value(Dictionary<string, string> section, string key)
        {
            return section.TryGetValue(key, out string found) ? found : null;
        }

        private static string Unquote(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length < 2
                || value[0] != '"' || value[value.Length - 1] != '"')
            {
                return value;
            }
            return value.Substring(1, value.Length - 2);
        }

        /// <summary>
        /// INVARIANT CULTURE, DELIBERATELY. Godot writes 1023.383333 with a
        /// dot whatever the machine's locale is, and a comma-decimal locale
        /// would read that as a thousands separator and import a playtime of
        /// 1023383333 seconds -- about thirty years.
        /// </summary>
        private static double Number(Dictionary<string, string> section, string key,
            double fallback)
        {
            string value = Value(section, key);
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture,
                out double parsed) ? parsed : fallback;
        }

        /// <summary>Godot's PackedByteArray(1, 2, 3) literal.</summary>
        public static byte[] Bytes(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            int open = value.IndexOf('(');
            int close = value.LastIndexOf(')');
            if (open < 0 || close <= open)
            {
                return null;
            }

            string inside = value.Substring(open + 1, close - open - 1);
            if (inside.Trim().Length == 0)
            {
                return System.Array.Empty<byte>();
            }

            string[] parts = inside.Split(',');
            var bytes = new byte[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                if (!int.TryParse(parts[i].Trim(), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int parsed))
                {
                    return null;
                }
                bytes[i] = (byte)Mathf.Clamp(parsed, 0, 255);
            }
            return bytes;
        }
    }
}
