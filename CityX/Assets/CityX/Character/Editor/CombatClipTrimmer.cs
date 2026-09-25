using UnityEditor;
using UnityEngine;

namespace CityX.CharacterEditor
{
    /// <summary>
    /// Trims the raw Mixamo mocap imports for combat clips down to the exact
    /// windows the Godot original bakes its own combat animations to --
    /// EXPLICIT BUG REPORT: "make the animations exactly the same they feel
    /// different in unity," narrowed down to "poses and timing," combat
    /// specifically.
    ///
    /// ROOT CAUSE: tests/scratch_mixamo_bake.gd (Godot side) deliberately
    /// windows every one-shot combat clip down to just its own windup-
    /// strike-recovery span, found via a hand-to-shoulder-distance peak
    /// scan, and its own comment states WHY in one sentence: keeping the
    /// clip close to player.gd's own PUNCH_COOLDOWN_SECONDS (0.35s), since
    /// character.gd's own _is_punching blocks ALL movement for the
    /// animation's FULL length, not just the cooldown. Unity's importer
    /// instead kept the ENTIRE raw mocap take for every one of these clips
    /// (2.1-4.8 real seconds), and MeleeAttacker's own gameplay lockout
    /// (SwingSeconds/HeavySwingSeconds, 0.35-0.55s) has no relationship to
    /// that length at all -- the hit lands and control returns while the
    /// untrimmed clip is still in its early guard-raise, and the character
    /// keeps visibly playing out over a second of leftover mocap AFTER the
    /// player already has control back. That mismatch between "how long the
    /// pose takes to actually reach the strike" and "how long the game
    /// waits" is what reads as poses/timing being wrong, independent of
    /// anything about retargeting quality itself.
    ///
    /// WINDOWS TAKEN DIRECTLY FROM scratch_mixamo_bake.gd/scratch_2hsw_bake.gd's
    /// own named constants (RIGHT_JAB_WINDOW, LEFT_JAB_WINDOW, HEAVY_PUNCH_
    /// WINDOW, BODY_BLOCK_WINDOW, CHARGED_ATTACK_WINDOW), converted from
    /// seconds to frames at the clips' own confirmed 30fps (every meta's
    /// lastFrame/known raw duration agrees to within rounding). 2hsw_attack/
    /// 2hsw_attack2 are untouched -- Godot never trims those either.
    /// </summary>
    public static class CombatClipTrimmer
    {
        private readonly struct Trim
        {
            public readonly string FbxPath;
            public readonly int FirstFrame;
            public readonly int LastFrame;

            public Trim(string fbxPath, int firstFrame, int lastFrame)
            {
                FbxPath = fbxPath;
                FirstFrame = firstFrame;
                LastFrame = lastFrame;
            }
        }

        private const string AnimationsFolder = "Assets/CityX/Character/Animations/";

        // Seconds -> frames at 30fps, rounded to the nearest frame -- a
        // frame either way (33ms) is below anything perceptible here.
        private static readonly Trim[] Trims =
        {
            // RIGHT_JAB_WINDOW := [0.52, 0.88]
            new Trim(AnimationsFolder + "boxing_right_jab.fbx", 16, 26),
            // LEFT_JAB_WINDOW := [0.253, 0.613]
            new Trim(AnimationsFolder + "boxing_left_jab.fbx", 8, 18),
            // HEAVY_PUNCH_WINDOW := [0.95, 1.55]
            new Trim(AnimationsFolder + "heavy_punch.fbx", 29, 47),
            // BODY_BLOCK_WINDOW := [0.75, 1.45]
            new Trim(AnimationsFolder + "body_block.fbx", 23, 44),
            // CHARGED_ATTACK_WINDOW := [1.5, 4.3] (scratch_2hsw_bake.gd)
            new Trim(AnimationsFolder + "2hsw_chargedattack.fbx", 45, 129),
        };

        [MenuItem("CityX/Trim Combat Clips To Match Godot")]
        public static void TrimAll()
        {
            int trimmed = 0;
            foreach (Trim trim in Trims)
            {
                if (!(AssetImporter.GetAtPath(trim.FbxPath) is ModelImporter importer))
                {
                    Debug.LogError($"CityX: no ModelImporter at {trim.FbxPath} -- skipped.");
                    continue;
                }

                ModelImporterClipAnimation[] clips = importer.clipAnimations;
                if (clips == null || clips.Length == 0)
                {
                    // No custom split yet -- defaultClipAnimations reflects
                    // the source take(s) before any trim has ever been
                    // applied; start from that instead of an empty array.
                    clips = importer.defaultClipAnimations;
                }
                if (clips == null || clips.Length == 0)
                {
                    Debug.LogError($"CityX: {trim.FbxPath} has no clip to trim -- skipped.");
                    continue;
                }

                // Every one of these FBX files imports exactly one take
                // (confirmed directly in each .meta) -- trim it in place
                // rather than searching by name.
                clips[0].firstFrame = trim.FirstFrame;
                clips[0].lastFrame = trim.LastFrame;
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
                trimmed++;
                Debug.Log($"CityX: trimmed {trim.FbxPath} to frames [{trim.FirstFrame}, {trim.LastFrame}] " +
                          $"({(trim.LastFrame - trim.FirstFrame) / 30.0f:0.00}s).");
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"CityX: trimmed {trimmed}/{Trims.Length} combat clips to match Godot's own windows.");

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(trimmed == Trims.Length ? 0 : 1);
            }
        }
    }
}
