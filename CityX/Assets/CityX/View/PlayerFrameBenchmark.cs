#if CITYX_BENCH
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using CityX.Character;
using CityX.World;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Scripting;
using Debug = UnityEngine.Debug;

namespace CityX.View
{
    /// <summary>
    /// The frame benchmark, INSIDE A BUILT PLAYER -- the editor's numbers include its own overhead (rendering
    /// submitted on the main thread, profiler hooks), so the real answer to "how fast is the game" has to come
    /// from the game itself.
    ///
    /// COMPILED ONLY WHEN CITYX_BENCH IS DEFINED (the benchmark build sets it; see PerfBuild), so a normal build
    /// contains none of this. Started by the -cityxbench command-line flag: settles the city, times a phase
    /// standing still, a phase sprinting down the streets and a phase flying over them at jetpack speed, writes
    /// bench-result.txt beside the game and quits. Each phase also reports the garbage collections in it and how
    /// long they stopped the game (a watchdog thread, see <see cref="Watchdog"/>).
    /// </summary>
    public sealed class PlayerFrameBenchmark : MonoBehaviour
    {
        private static bool HasFlag(string flag)
        {
            foreach (string argument in Environment.GetCommandLineArgs())
            {
                if (string.Equals(argument, flag, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (HasFlag("-cityxbench"))
            {
                new GameObject("FrameBenchmark").AddComponent<PlayerFrameBenchmark>();
            }
        }

        private enum Motion
        {
            Standing,
            Sprinting,
            Flying,
        }

        private sealed class Phase
        {
            public string Name;
            public readonly List<float> Frames = new List<float>();
            public double Gpu, CpuMain, CpuRender;
            public int TimingSamples;
            public float Seconds;

            // Garbage collection: frames a collection finished in (by GC.CollectionCount), the managed heap, and the phase's span on the
            // watchdog's clock so its stops can be told apart by phase and by frame.
            public int Collections;
            public readonly List<int> CollectedFrames = new List<int>();
            public readonly List<long> FrameStartTicks = new List<long>();
            public readonly List<long> FrameEndTicks = new List<long>();
            public long UsedStart, UsedEnd, UsedPeak, ReservedStart, ReservedEnd;
            public long AllocatedAtLeast;
            public long StartTicks, EndTicks;
            public int ChunksStreamedIn;
            public double WorstStreamerMs;

            // The chunk meshes' arrays used again (ChunkArrayPool): new ones made in the phase, and what is held for reuse at its end.
            public long PoolMade, PoolHeld;
        }

        private static readonly FrameTiming[] Timings = new FrameTiming[1];

        private static string Arg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            }
            return null;
        }

        private static float ArgSeconds(string name, float fallback) => float.TryParse(Arg(name), out float value) ? value : fallback;

        /// <summary>
        /// What-ifs from the command line, applied to a COPY of the pipeline asset and to the streamer, so the
        /// real settings on disk are never touched: -bench-msaa N, -bench-shadow METRES, -bench-shadowres PIXELS,
        /// -bench-cascades N, -bench-scale S, -bench-radius CHUNKS, -bench-threads N, -bench-gcslice MS (the incremental
        /// collector's time slice per frame), -bench-standing SECONDS, -bench-running SECONDS, -bench-flying SECONDS,
        /// -bench-flyspeed M/S.
        /// </summary>
        private static string ApplyOverrides()
        {
            var text = new StringBuilder();
            var pipeline = (QualitySettings.renderPipeline != null ? QualitySettings.renderPipeline : UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline)
                as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            if (pipeline != null)
            {
                var copy = UnityEngine.Object.Instantiate(pipeline);
                if (int.TryParse(Arg("-bench-msaa"), out int msaa)) { copy.msaaSampleCount = msaa; text.Append($" MSAA={msaa}"); }
                if (float.TryParse(Arg("-bench-shadow"), out float shadow)) { copy.shadowDistance = shadow; text.Append($" shadowDistance={shadow}"); }
                if (int.TryParse(Arg("-bench-shadowres"), out int shadowRes)) { copy.mainLightShadowmapResolution = shadowRes; text.Append($" shadowRes={shadowRes}"); }
                if (int.TryParse(Arg("-bench-cascades"), out int cascades)) { copy.shadowCascadeCount = cascades; text.Append($" cascades={cascades}"); }
                if (float.TryParse(Arg("-bench-scale"), out float scale)) { copy.renderScale = scale; text.Append($" renderScale={scale}"); }
                QualitySettings.renderPipeline = copy;
            }
            if (float.TryParse(Arg("-bench-gcslice"), out float sliceMs))
            {
                GarbageCollector.incrementalTimeSliceNanoseconds = (ulong)(sliceMs * 1000000.0f);
                text.Append($" gcSlice={sliceMs}ms");
            }
            var streamer = FindFirstObjectByType<CityStreamer>();
            if (streamer != null)
            {
                if (int.TryParse(Arg("-bench-radius"), out int radius)) { streamer.RadiusChunks = radius; text.Append($" radius={radius}"); }
                if (int.TryParse(Arg("-bench-threads"), out int threads)) { streamer.GeneratorThreads = threads; text.Append($" threads={threads}"); }
                if (int.TryParse(Arg("-bench-fullradius"), out int fullRadius)) { streamer.FullDetailRadiusChunks = fullRadius; text.Append($" fullRadius={fullRadius}"); }
                text.Append($" [streamer radius {streamer.RadiusChunks}, full detail radius {streamer.FullDetailRadiusChunks}, generator threads {streamer.WorkerThreads}]");
            }
            return text.Length == 0 ? " none" : text.ToString();
        }

        private Watchdog _watchdog;

        private IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            yield return null;
            yield return null;

            PlayerLocomotion locomotion = null;
            for (int i = 0; i < 600 && locomotion == null; i++)
            {
                locomotion = FindFirstObjectByType<PlayerLocomotion>();
                yield return null;
            }
            var sb = new StringBuilder();
            sb.AppendLine($"PLAYER BUILD  GPU {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType}), CPU {SystemInfo.processorType} x{SystemInfo.processorCount}");
            sb.AppendLine($"Screen {Screen.width}x{Screen.height}, vSync {QualitySettings.vSyncCount}, quality {QualitySettings.names[QualitySettings.GetQualityLevel()]}, targetFrameRate {Application.targetFrameRate}");
            if (locomotion == null)
            {
                sb.AppendLine("no player found");
                Finish(sb);
                yield break;
            }
            sb.AppendLine("Overrides:" + ApplyOverrides());
            sb.AppendLine($"Garbage collector: mode {GarbageCollector.GCMode}, incremental {GarbageCollector.isIncremental}, " +
                          $"time slice {GarbageCollector.incrementalTimeSliceNanoseconds / 1000000.0:0.0} ms a frame");

            var reader = locomotion.GetComponent<PlayerInputReader>();
            if (reader != null) reader.enabled = false;   // nobody is at the keyboard: drive the player

            // This build holds the city scene alone, with no network manager, so the animator sync would throw
            // an exception every frame (thousands in the log), and the room-light switches' network relay asks for a manager every frame
            // and logs that there is none (FishNet's InstanceFinder). Not part of the game as played: switch them off.
            foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                string type = behaviour.GetType().Name;
                if (type == "NetworkedAnimatorSync" || type == "RoomLightsNetwork" || type == "BalconyDoorsNetwork")
                {
                    behaviour.enabled = false;
                }
            }

            _watchdog = new Watchdog();
            _watchdog.Start();

            // Let the first ring of the city stream in before timing anything.
            float settle = 0.0f;
            while (settle < 6.0f)
            {
                locomotion.Tick(new LocomotionInput(), Time.deltaTime);
                settle += Time.unscaledDeltaTime;
                yield return null;
            }

            float exactSeconds = ArgSeconds("-bench-exact", 3.0f);

            var standing = new Phase { Name = "STANDING STILL" };
            yield return Measure(locomotion, standing, ArgSeconds("-bench-standing", 10.0f), Motion.Standing);
            Report(sb, standing, _watchdog);
            yield return MeasureExactly(sb, locomotion, exactSeconds, Motion.Standing);
            Census(sb, "(standing)");

            var running = new Phase { Name = "SPRINTING DOWN THE STREETS (the city streams in around the player)" };
            yield return Measure(locomotion, running, ArgSeconds("-bench-running", 30.0f), Motion.Sprinting);
            Report(sb, running, _watchdog);
            yield return MeasureExactly(sb, locomotion, exactSeconds, Motion.Sprinting);
            Census(sb, "(after sprinting)");

            float flySpeed = ArgSeconds("-bench-flyspeed", 22.0f);
            var flying = new Phase { Name = $"FLYING OVER THE CITY AT {flySpeed:0} M/S, {FlyHeight:0} M UP (a boosted jetpack; the city streams in fast)" };
            yield return Measure(locomotion, flying, ArgSeconds("-bench-flying", 30.0f), Motion.Flying, flySpeed);
            Report(sb, flying, _watchdog);
            yield return MeasureExactly(sb, locomotion, exactSeconds, Motion.Flying, flySpeed);
            Census(sb, "(after flying)");

            Finish(sb);
        }

        /// <summary>
        /// How much is allocated, exactly: the same motion carried on for a few seconds with the collector switched off, so the heap only
        /// grows and its growth is what was allocated (with it on, a collection hides some of it). The main thread's own share is counted on
        /// its own. Then the collector is switched back on and the heap collected, outside every phase's numbers.
        /// </summary>
        private static IEnumerator MeasureExactly(StringBuilder sb, PlayerLocomotion locomotion, float seconds, Motion motion, float flySpeed = 0.0f)
        {
            if (seconds <= 0.0f)
            {
                yield break;
            }
            var streamer = FindFirstObjectByType<CityStreamer>();
            int generatedBefore = streamer != null ? streamer.ChunksGenerated : 0;
            int streamedBefore = streamer != null ? streamer.ChunksStreamedIn : 0;
            GarbageCollector.GCMode = GarbageCollector.Mode.Disabled;
            long used = UsedManagedBytes();
            long mainBefore = GC.GetAllocatedBytesForCurrentThread();
            var window = new Phase();
            yield return Measure(locomotion, window, seconds, motion, flySpeed);
            long grew = UsedManagedBytes() - used;
            long main = GC.GetAllocatedBytesForCurrentThread() - mainBefore;
            GarbageCollector.GCMode = GarbageCollector.Mode.Enabled;
            int generated = streamer != null ? streamer.ChunksGenerated - generatedBefore : 0;
            int streamed = streamer != null ? streamer.ChunksStreamedIn - streamedBefore : 0;
            sb.AppendLine($"Exactly, with the collector off for {window.Seconds:0.0} s more: allocated {Mb(grew)} MB " +
                          $"({grew / (1024.0 * 1024.0) / Mathf.Max(0.01f, window.Seconds):0} MB/s), the main thread {Mb(main)} MB of it" +
                          $"{(main == 0 ? " (or this runtime does not count per thread)" : "")}; chunks generated {generated}, put on screen {streamed}");
            GC.Collect();   // what piled up meanwhile, before the next phase
            yield return null;
            yield return null;
        }

        private void OnDestroy()
        {
            _watchdog?.Stop();
        }

        /// <summary>How high over the ground under it the flying phase goes (the streamer works on where it is, not how high).</summary>
        private const float FlyHeight = 40.0f;

        private static long UsedManagedBytes()
        {
            long used = Profiler.GetMonoUsedSizeLong();
            return used > 0 ? used : GC.GetTotalMemory(false);
        }

        private static IEnumerator Measure(PlayerLocomotion locomotion, Phase phase, float seconds, Motion motion, float flySpeed = 0.0f)
        {
            if (seconds <= 0.0f)
            {
                yield break;
            }
            var streamer = FindFirstObjectByType<CityStreamer>();
            int streamedBefore = streamer != null ? streamer.ChunksStreamedIn : 0;

            // Flying: the body is carried along a straight line at the speed asked for, at a height over the ground under it. Nothing a
            // real flight does matters to the streaming but where the viewer is, and the camera follows the body.
            Transform body = locomotion.transform;
            var controller = locomotion.GetComponent<CharacterController>();
            Vector3 heading = new Vector3(1.0f, 0.0f, 0.35f).normalized;
            Vector3 flyAt = body.position;
            if (motion == Motion.Flying && controller != null)
            {
                controller.enabled = false;
            }

            float elapsed = 0.0f;
            float yaw = 0.0f;
            long used = UsedManagedBytes();
            phase.UsedStart = used;
            long poolMadeBefore = ChunkArrayPool.MadeBytes;
            phase.UsedPeak = used;
            phase.ReservedStart = Profiler.GetMonoHeapSizeLong();
            int collections = GC.CollectionCount(0);
            int collectionsAtStart = collections;
            phase.StartTicks = Stopwatch.GetTimestamp();
            long frameStart = phase.StartTicks;
            while (elapsed < seconds)
            {
                float dt = Time.unscaledDeltaTime;
                switch (motion)
                {
                    case Motion.Sprinting:
                        if (((int)(elapsed / 5.0f)) % 2 == 1) yaw += 40.0f * dt;
                        locomotion.Tick(new LocomotionInput { Move = Vector2.up, Sprint = true, YawDegrees = yaw }, Time.deltaTime);
                        break;
                    case Motion.Flying:
                        flyAt += heading * (flySpeed * dt);
                        CityWorld world = CurrentWorld.Instance;
                        float ground = world != null ? (float)world.Plan.GroundHeight(flyAt.x, flyAt.z) : 0.0f;
                        flyAt.y = ground + FlyHeight;
                        body.position = flyAt;
                        break;
                    default:
                        locomotion.Tick(new LocomotionInput(), Time.deltaTime);
                        break;
                }
                yield return null;
                elapsed += dt;
                phase.Frames.Add(dt * 1000.0f);

                long frameEnd = Stopwatch.GetTimestamp();
                phase.FrameStartTicks.Add(frameStart);
                phase.FrameEndTicks.Add(frameEnd);
                frameStart = frameEnd;
                int now = GC.CollectionCount(0);
                if (now != collections)
                {
                    phase.CollectedFrames.Add(phase.Frames.Count - 1);
                    collections = now;
                }
                long usedNow = UsedManagedBytes();
                if (usedNow > used)
                {
                    phase.AllocatedAtLeast += usedNow - used;
                }
                used = usedNow;
                phase.UsedPeak = Math.Max(phase.UsedPeak, used);
                if (streamer != null)
                {
                    phase.WorstStreamerMs = Math.Max(phase.WorstStreamerMs, streamer.LastUpdateMs);
                }

                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, Timings) > 0)
                {
                    phase.Gpu += Timings[0].gpuFrameTime;
                    phase.CpuMain += Timings[0].cpuMainThreadFrameTime;
                    phase.CpuRender += Timings[0].cpuRenderThreadFrameTime;
                    phase.TimingSamples++;
                }
            }
            phase.EndTicks = Stopwatch.GetTimestamp();
            phase.Seconds = elapsed;
            phase.Collections = GC.CollectionCount(0) - collectionsAtStart;
            phase.UsedEnd = used;
            phase.ReservedEnd = Profiler.GetMonoHeapSizeLong();
            phase.ChunksStreamedIn = streamer != null ? streamer.ChunksStreamedIn - streamedBefore : 0;
            phase.PoolMade = ChunkArrayPool.MadeBytes - poolMadeBefore;
            phase.PoolHeld = ChunkArrayPool.PooledBytes;

            if (motion == Motion.Flying && controller != null)
            {
                controller.enabled = true;
            }
        }

        private static string Percentile(List<float> sortedMs, float p)
        {
            int index = Mathf.Clamp(Mathf.CeilToInt(p * sortedMs.Count) - 1, 0, sortedMs.Count - 1);
            return sortedMs[index].ToString("0.0");
        }

        private static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

        private static string Mb(long bytes) => (bytes / (1024.0 * 1024.0)).ToString("0");

        private static void Report(StringBuilder sb, Phase phase, Watchdog watchdog)
        {
            if (phase.Frames.Count == 0)
            {
                return;
            }
            var sorted = new List<float>(phase.Frames);
            sorted.Sort();
            float total = 0;
            int over17 = 0, over33 = 0, over100 = 0;
            foreach (float f in phase.Frames)
            {
                total += f;
                if (f > 16.7f) over17++;
                if (f > 33.4f) over33++;
                if (f > 100.0f) over100++;
            }
            int count = Mathf.Max(1, phase.Frames.Count);
            sb.AppendLine($"--- {phase.Name}");
            sb.AppendLine($"Frames: {phase.Frames.Count} over {phase.Seconds:0.0}s = {phase.Frames.Count / Mathf.Max(0.01f, phase.Seconds):0} fps average");
            sb.AppendLine($"Frame ms: avg {total / count:0.0}, median {Percentile(sorted, 0.5f)}, p95 {Percentile(sorted, 0.95f)}, p99 {Percentile(sorted, 0.99f)}, worst {sorted[sorted.Count - 1]:0.0}");
            sb.AppendLine($"Under 60fps: {over17} frames ({100.0f * over17 / count:0.0}%), over 33ms: {over33}, over 100ms: {over100}");
            if (phase.TimingSamples > 0)
            {
                int t = phase.TimingSamples;
                sb.AppendLine($"Frame timing (ms): GPU {phase.Gpu / t:0.0}, CPU main thread {phase.CpuMain / t:0.0}, CPU render thread {phase.CpuRender / t:0.0}");
            }
            sb.AppendLine($"Streaming: {phase.ChunksStreamedIn} chunks put on screen, the streamer's worst frame {phase.WorstStreamerMs:0.0} ms; " +
                          $"chunk mesh arrays: {Mb(phase.PoolMade)} MB made new, {Mb(phase.PoolHeld)} MB held for reuse at the end ({ChunkArrayPool.Held()}); " +
                          $"build buffers held {Mb(CityStreamer.ScratchHeld.bytes)} MB in {CityStreamer.ScratchHeld.sets} sets");

            // The garbage collections, and how long they stood the game still.
            double every = phase.Collections > 0 ? phase.Seconds / phase.Collections : 0.0;
            sb.AppendLine($"Garbage collections: {phase.Collections}{(phase.Collections > 0 ? $" (one every {every:0.0} s)" : "")}; managed heap used " +
                          $"{Mb(phase.UsedStart)} -> {Mb(phase.UsedEnd)} MB (peak {Mb(phase.UsedPeak)}), reserved {Mb(phase.ReservedStart)} -> " +
                          $"{Mb(phase.ReservedEnd)} MB; allocated at least {Mb(phase.AllocatedAtLeast)} MB ({phase.AllocatedAtLeast / (1024.0 * 1024.0) / Mathf.Max(0.01f, phase.Seconds):0} MB/s)");

            List<(long start, long length)> stops = watchdog.StopsBetween(phase.StartTicks, phase.EndTicks);
            int over4 = 0, over16 = 0, withCollection = 0;
            double stoppedMs = 0.0, longest = 0.0, longestWithCollection = 0.0;
            var collectedFrames = new HashSet<int>(phase.CollectedFrames);
            foreach ((long start, long length) in stops)
            {
                double ms = Ms(length);
                stoppedMs += ms;
                longest = Math.Max(longest, ms);
                if (ms >= 4.0) over4++;
                if (ms >= 16.0) over16++;
                if (OverlapsCollectedFrame(phase, collectedFrames, start, start + length))
                {
                    withCollection++;
                    longestWithCollection = Math.Max(longestWithCollection, ms);
                }
            }
            sb.AppendLine($"Game stopped (a watchdog thread's clock standing still >= {Watchdog.MinimumGapMs} ms): {stops.Count} times, {stoppedMs:0} ms in all, " +
                          $"longest {longest:0.0} ms; >= 4 ms: {over4}, >= 16 ms: {over16}; in a frame a collection finished in: {withCollection} " +
                          $"(longest {longestWithCollection:0.0} ms){(watchdog.Overflowed ? " [watchdog log full]" : "")}");
            if (phase.CollectedFrames.Count > 0)
            {
                var frames = new StringBuilder();
                foreach (int frame in phase.CollectedFrames)
                {
                    double stoppedInFrame = 0.0;
                    foreach ((long start, long length) in stops)
                    {
                        if (start < phase.FrameEndTicks[frame] && start + length > phase.FrameStartTicks[frame])
                        {
                            stoppedInFrame = Math.Max(stoppedInFrame, Ms(length));
                        }
                    }
                    frames.Append($" {phase.Frames[frame]:0.0} ms (stopped {stoppedInFrame:0.0});");
                }
                sb.AppendLine($"Frames a collection finished in:{frames}");
            }
            var slow = new StringBuilder();
            int listed = 0;
            for (int i = 0; i < phase.Frames.Count && listed < 12; i++)
            {
                if (phase.Frames[i] > 33.4f)
                {
                    slow.Append($" {phase.Frames[i]:0.0}{(collectedFrames.Contains(i) ? " (collection)" : "")};");
                    listed++;
                }
            }
            if (listed > 0)
            {
                sb.AppendLine($"Frames over 33 ms:{slow}");
            }
        }

        private static bool OverlapsCollectedFrame(Phase phase, HashSet<int> collectedFrames, long start, long end)
        {
            foreach (int frame in collectedFrames)
            {
                if (start < phase.FrameEndTicks[frame] && end > phase.FrameStartTicks[frame])
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// A thread that does nothing but read the clock. A garbage collection stops every thread the runtime knows (it suspends them,
        /// wherever they are), this one included, so a gap in its readings is how long the game stood still. A thread can also lose its
        /// core for a moment, which is why gaps are matched against the frames a collection finished in. Allocates nothing while it runs.
        /// </summary>
        private sealed class Watchdog
        {
            public const double MinimumGapMs = 1.0;

            private readonly long[] _starts = new long[8192];
            private readonly long[] _lengths = new long[8192];
            private int _count;
            private volatile bool _stop;
            private Thread _thread;

            public bool Overflowed => Volatile.Read(ref _count) >= _starts.Length;

            public void Start()
            {
                _thread = new Thread(Run) { IsBackground = true, Name = "CityX bench stop watchdog" };
                _thread.Start();
            }

            public void Stop()
            {
                _stop = true;
            }

            private void Run()
            {
                long minimum = (long)(Stopwatch.Frequency * MinimumGapMs / 1000.0);
                long last = Stopwatch.GetTimestamp();
                while (!_stop)
                {
                    Thread.SpinWait(20);   // a pause instruction or so: leaves the core's other half (hyperthreading) to whoever shares it
                    long now = Stopwatch.GetTimestamp();
                    if (now - last >= minimum)
                    {
                        int n = _count;
                        if (n < _starts.Length)
                        {
                            _starts[n] = last;
                            _lengths[n] = now - last;
                            Volatile.Write(ref _count, n + 1);
                        }
                    }
                    last = now;
                }
            }

            /// <summary>The stops that began between two clock readings (from the main thread, while the watchdog runs).</summary>
            public List<(long start, long length)> StopsBetween(long from, long to)
            {
                int n = Volatile.Read(ref _count);
                var stops = new List<(long, long)>();
                for (int i = 0; i < n; i++)
                {
                    if (_starts[i] >= from && _starts[i] < to)
                    {
                        stops.Add((_starts[i], _lengths[i]));
                    }
                }
                return stops;
            }
        }

        /// <summary>
        /// What the renderer is being asked to draw: every active mesh renderer in the scene and the ones inside
        /// the camera's frustum, with their triangles and submesh draws. Frame time that follows the amount of
        /// geometry rather than the resolution says where to cut.
        /// </summary>
        private static void Census(StringBuilder sb, string label)
        {
            Camera camera = Camera.main;
            Plane[] planes = camera != null ? GeometryUtility.CalculateFrustumPlanes(camera) : null;
            int renderers = 0, visibleRenderers = 0, draws = 0, visibleDraws = 0, transparentDraws = 0;
            long triangles = 0, visibleTriangles = 0, vertices = 0, visibleVertices = 0;
            var shadowCasters = 0;
            int emptyDraws = 0;
            foreach (MeshRenderer renderer in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                var filter = renderer.GetComponent<MeshFilter>();
                Mesh mesh = filter != null ? filter.sharedMesh : null;
                if (mesh == null) continue;

                long tris = 0;
                int submeshes = Mathf.Min(mesh.subMeshCount, renderer.sharedMaterials.Length);
                int nonEmpty = 0;
                for (int s = 0; s < submeshes; s++)
                {
                    long count = mesh.GetIndexCount(s);
                    tris += count / 3;
                    if (count > 0) nonEmpty++;
                }
                emptyDraws += submeshes - nonEmpty;
                bool visible = planes == null || GeometryUtility.TestPlanesAABB(planes, renderer.bounds);

                renderers++;
                draws += submeshes;
                triangles += tris;
                vertices += mesh.vertexCount;
                if (renderer.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off) shadowCasters++;
                if (visible)
                {
                    visibleRenderers++;
                    visibleDraws += submeshes;
                    visibleTriangles += tris;
                    visibleVertices += mesh.vertexCount;
                }
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (visible && material != null && material.renderQueue >= 3000) transparentDraws++;
                }
            }
            sb.AppendLine($"Census {label}: {emptyDraws} empty submesh draws; {renderers} mesh renderers ({shadowCasters} cast shadows), {draws} submesh draws, {triangles / 1000}k triangles, {vertices / 1000}k vertices");
            sb.AppendLine($"  in the camera frustum: {visibleRenderers} renderers, {visibleDraws} draws ({transparentDraws} transparent), {visibleTriangles / 1000}k triangles, {visibleVertices / 1000}k vertices");
        }

        private void Finish(StringBuilder sb)
        {
            _watchdog?.Stop();
            string path = Path.Combine(Application.dataPath, "..", "bench-result.txt");
            File.WriteAllText(path, sb.ToString());
            Debug.Log("CityX BENCH:\n" + sb);
            Application.Quit();
        }
    }
}
#endif
