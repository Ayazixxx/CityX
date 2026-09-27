using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

namespace CityX.ViewEditor
{
    /// <summary>
    /// Reads a recorded Unity profile (.raw) and says where the time went: the markers that cost the most per
    /// frame, by their own time and by their total, on the main thread and the render thread.
    ///
    /// The frame benchmark (FrameBenchmarkPlayTests) records one when CITYX_BENCH_PROFILE=1; then:
    ///   Unity.exe -batchmode -quit -projectPath CityX -executeMethod CityX.ViewEditor.ProfileReport.AnalyseFromEnvironment
    /// with CITYX_PROFILE_FILE set to the .raw. The report is printed and written beside it as .txt.
    /// A measured breakdown beats guessing which system is slow -- and this project's recurring finding is that
    /// the slow thing is not where anyone thought.
    /// </summary>
    public static class ProfileReport
    {
        private sealed class Row
        {
            public string Name;
            public double SelfMs;
            public double TotalMs;
            public long Calls;
            public int Frames;
        }

        [MenuItem("CityX/Profile Report (from CITYX_PROFILE_FILE)")]
        public static void AnalyseFromEnvironment()
        {
            string path = Environment.GetEnvironmentVariable("CITYX_PROFILE_FILE");
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                Debug.LogError($"CityX: set CITYX_PROFILE_FILE to a recorded .raw profile (got '{path}')");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            string report = Analyse(path);
            File.WriteAllText(Path.ChangeExtension(path, ".txt"), report);
            Debug.Log("CityX PROFILE:\n" + report);
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        public static string Analyse(string rawPath)
        {
            if (!ProfilerDriver.LoadProfile(rawPath, false))
            {
                return "could not load " + rawPath;
            }

            int first = ProfilerDriver.firstFrameIndex;
            int last = ProfilerDriver.lastFrameIndex;
            var sb = new StringBuilder();
            sb.AppendLine($"Profile {Path.GetFileName(rawPath)}: frames {first}..{last} ({last - first + 1})");

            // Skip the first few frames (warm-up) and any past the end.
            int from = Mathf.Min(last, first + 5);
            foreach (string threadName in new[] { "Main Thread", "Render Thread" })
            {
                var rows = new Dictionary<string, Row>();
                double frameMs = 0.0;
                int frames = 0;
                for (int frame = from; frame <= last; frame++)
                {
                    int threadIndex = FindThread(frame, threadName);
                    if (threadIndex < 0)
                    {
                        continue;
                    }
                    using (HierarchyFrameDataView view = ProfilerDriver.GetHierarchyFrameDataView(
                        frame, threadIndex, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName,
                        HierarchyFrameDataView.columnSelfTime, false))
                    {
                        if (view == null || !view.valid)
                        {
                            continue;
                        }
                        frames++;
                        frameMs += view.frameTimeMs;
                        Visit(view, view.GetRootItemID(), rows, frame);
                    }
                }

                if (frames == 0)
                {
                    sb.AppendLine($"\n== {threadName}: no data");
                    continue;
                }

                sb.AppendLine($"\n== {threadName}: average frame {frameMs / frames:0.0} ms over {frames} frames");
                sb.AppendLine("Top by SELF time (ms per frame):");
                var list = new List<Row>(rows.Values);
                list.Sort((a, b) => b.SelfMs.CompareTo(a.SelfMs));
                for (int i = 0; i < Mathf.Min(28, list.Count); i++)
                {
                    sb.AppendLine($"  {list[i].SelfMs / frames,7:0.00}  self   total {list[i].TotalMs / frames,7:0.00}   calls/frame {list[i].Calls / (double)frames,7:0.0}   {list[i].Name}");
                }
                sb.AppendLine("Top by TOTAL time (ms per frame):");
                list.Sort((a, b) => b.TotalMs.CompareTo(a.TotalMs));
                for (int i = 0; i < Mathf.Min(22, list.Count); i++)
                {
                    sb.AppendLine($"  {list[i].TotalMs / frames,7:0.00}  total  self {list[i].SelfMs / frames,7:0.00}   calls/frame {list[i].Calls / (double)frames,7:0.0}   {list[i].Name}");
                }
            }
            return sb.ToString();
        }

        private static int FindThread(int frame, string threadName)
        {
            for (int t = 0; t < 32; t++)
            {
                using (HierarchyFrameDataView view = ProfilerDriver.GetHierarchyFrameDataView(
                    frame, t, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnSelfTime, false))
                {
                    if (view == null || !view.valid)
                    {
                        if (t > 0) return -1;
                        continue;
                    }
                    if (view.threadName == threadName)
                    {
                        return t;
                    }
                }
            }
            return -1;
        }

        private static void Visit(HierarchyFrameDataView view, int id, Dictionary<string, Row> rows, int frame)
        {
            var children = new List<int>();
            view.GetItemChildren(id, children);
            foreach (int child in children)
            {
                string name = view.GetItemName(child);
                if (!rows.TryGetValue(name, out Row row))
                {
                    row = new Row { Name = name };
                    rows[name] = row;
                }
                row.SelfMs += view.GetItemColumnDataAsFloat(child, HierarchyFrameDataView.columnSelfTime);
                row.TotalMs += view.GetItemColumnDataAsFloat(child, HierarchyFrameDataView.columnTotalTime);
                row.Calls += (long)view.GetItemColumnDataAsFloat(child, HierarchyFrameDataView.columnCalls);
                Visit(view, child, rows, frame);
            }
        }
    }
}
