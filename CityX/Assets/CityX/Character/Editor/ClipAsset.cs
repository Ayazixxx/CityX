using System.IO;
using UnityEditor;
using UnityEngine;

namespace CityX.CharacterEditor
{
    /// <summary>
    /// Stores a generated clip as an asset, written INTO the clip already at that path rather than deleted and made anew. A deleted clip is gone
    /// from everything that still held it: the controller built against it silently lost its clip, and a body saved after the delete kept
    /// nothing in its place (the joining player's body shipped without its Bloodrage walk and run, found 2026-10-17).
    /// </summary>
    public static class ClipAsset
    {
        /// <summary>
        /// The clip as stored at <paramref name="path"/>, named after its file: <paramref name="clip"/> itself when the path was free, else the
        /// clip already there, now holding <paramref name="clip"/>'s content (<paramref name="clip"/> is destroyed).
        /// </summary>
        public static AnimationClip Store(AnimationClip clip, string path)
        {
            clip.name = Path.GetFileNameWithoutExtension(path);
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(clip, path);
                return clip;
            }
            existing.ClearCurves();
            EditorUtility.CopySerialized(clip, existing);
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(clip);
            return existing;
        }
    }
}
