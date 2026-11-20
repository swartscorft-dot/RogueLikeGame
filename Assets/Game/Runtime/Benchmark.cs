using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Roguelike.Rules;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace Roguelike
{
    /// <summary>
    /// The benchmark (work order 032, 2026-11-20): started by <see cref="Argument"/> on the command line of a built player
    /// (or of the Editor, or the environment variable <see cref="EnvironmentSwitch"/>, which the Editor's menu item sets for
    /// its Play mode), it plays the scenarios of <see cref="BenchmarkStage"/> one after the other with nobody at the keys,
    /// each for <see cref="MeasureSeconds"/> after <see cref="SettleSeconds"/> of settling, measures every frame, writes
    /// the numbers to one JSON file and one text file under the system's temp folder (<see cref="OutputFolder"/>, never
    /// the project), and quits.
    ///
    /// Fixed conditions: vertical sync off, no frame rate cap, a 1920 by 1080 window in a player, the quality level the
    /// project ships with (left as it is). They are written into the results with the machine's CPU and GPU.
    ///
    /// Each frame it takes: the time from the start of one frame's Updates to the next (its own Update runs first of all,
    /// <see cref="DefaultExecutionOrderAttribute"/>); the time all Updates and all LateUpdates took (from its own, first,
    /// to <see cref="BenchmarkFrameEnd"/>'s, last); the <see cref="FrameTimingManager"/>'s CPU main thread, render thread
    /// and GPU times when the build has frame timing stats; and Unity's counters by <see cref="ProfilerRecorder"/>: the
    /// render counters, the managed memory allocated in the frame, and in a development build the main thread's time and
    /// the player loop's markers. A counter that is not in the build, or gives only zeros, is written as not working.
    ///
    /// Its first frames of each scenario log every material on a renderer, with its shader's name and whether it is
    /// supported (the pink error shader is <see cref="ErrorShaderName"/>): the proof that the build has the shaders the
    /// code finds by name.
    ///
    /// Arguments after <see cref="Argument"/>: -benchmarkLabel text, -benchmarkScenarios 1,2,3,4, -benchmarkSeconds 15,
    /// -benchmarkSettle 3, -benchmarkOut folder, -benchmarkOff name,name (optimisations off, <see cref="PerformanceSwitches"/>),
    /// -benchmarkDiag nogui,nokit,noprops,noshadows,noenemydraw (things taken away for one run, to see what they cost),
    /// -benchmarkListCounters (in a development build: writes every counter and marker the build has, beside the results).
    /// </summary>
    [DefaultExecutionOrder(-32000)]
    public sealed class Benchmark : MonoBehaviour
    {
        public const string Argument = "-benchmark";
        public const string EnvironmentSwitch = "ROGUELIKE_BENCHMARK";
        public const string ErrorShaderName = "Hidden/InternalErrorShader";
        public const int WindowWidth = 1920;
        public const int WindowHeight = 1080;

        static readonly CultureInfo s_Inv = CultureInfo.InvariantCulture;

        /// <summary>Raised when a run has written its results, with the JSON file's path: the Editor leaves Play mode on it.</summary>
        public static event Action<string> Finished;

        /// <summary>The one running, or null.</summary>
        public static Benchmark Current { get; private set; }

        public float SettleSeconds = 3f;
        public float MeasureSeconds = 15f;
        public string Label = "";
        public string OutputFolder = DefaultOutputFolder;
        public readonly List<BenchmarkScenario> Scenarios = new List<BenchmarkScenario>
        {
            BenchmarkScenario.PracticeIdle, BenchmarkScenario.Floor1Combat, BenchmarkScenario.Floor5Combat, BenchmarkScenario.Floor5Boss,
        };
        public readonly HashSet<string> Diagnostics = new HashSet<string>();
        public bool ListCounters;
        /// <summary>Quits the player when done (not in the Editor, nor in a test).</summary>
        public bool QuitWhenDone = true;

        public static string DefaultOutputFolder => Path.Combine(Path.GetTempPath(), "RoguelikeBenchmark");

        /// <summary>The path of the JSON results once written; empty before.</summary>
        public string ResultsPath { get; private set; } = "";
        public BenchmarkResults Results { get; private set; }
        public BenchmarkStage Stage { get; private set; }
        public bool Done { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Current = null;
            Finished = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartIfAsked()
        {
            var args = new List<string>(Environment.GetCommandLineArgs());
            string fromEnvironment = Environment.GetEnvironmentVariable(EnvironmentSwitch);
            if (!string.IsNullOrEmpty(fromEnvironment))
            {
                Environment.SetEnvironmentVariable(EnvironmentSwitch, null); // once: the next Play is an ordinary one
                args.Add(Argument);
                args.AddRange(fromEnvironment.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
            }
            if (!args.Contains(Argument)) return;
            Begin(args);
        }

        /// <summary>Starts a run with these arguments (see the class); the one running if there is one.</summary>
        public static Benchmark Begin(IList<string> args)
        {
            if (Current != null) return Current;
            var go = new GameObject("Benchmark");
            DontDestroyOnLoad(go);
            var b = go.AddComponent<Benchmark>();
            go.AddComponent<BenchmarkFrameEnd>().Owner = b;
            b.Read(args ?? Array.Empty<string>());
            Current = b;
            return b;
        }

        void Read(IList<string> args)
        {
            string Value(string name)
            {
                for (int i = 0; i < args.Count - 1; i++)
                    if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
                return null;
            }
            if (Value("-benchmarkLabel") is string label) Label = label;
            if (Value("-benchmarkOut") is string folder) OutputFolder = folder;
            if (float.TryParse(Value("-benchmarkSeconds"), NumberStyles.Float, s_Inv, out float seconds) && seconds > 0f) MeasureSeconds = seconds;
            if (float.TryParse(Value("-benchmarkSettle"), NumberStyles.Float, s_Inv, out float settle) && settle >= 0f) SettleSeconds = settle;
            if (Value("-benchmarkScenarios") is string list)
            {
                Scenarios.Clear();
                foreach (string part in list.Split(','))
                    if (int.TryParse(part.Trim(), out int n) && Enum.IsDefined(typeof(BenchmarkScenario), n)) Scenarios.Add((BenchmarkScenario)n);
            }
            if (Value("-benchmarkOff") is string off)
                foreach (string name in off.Split(','))
                    if (!PerformanceSwitches.Set(name.Trim(), false)) Debug.LogWarning("Benchmark: no optimisation called " + name);
            if (Value("-benchmarkDiag") is string diag)
                foreach (string name in diag.Split(',')) Diagnostics.Add(name.Trim().ToLowerInvariant());
            foreach (string a in args)
                if (string.Equals(a, "-benchmarkListCounters", StringComparison.OrdinalIgnoreCase)) ListCounters = true;
        }

        void Start() => StartCoroutine(RunAll());

        void OnDestroy()
        {
            Stage?.End();
            StopRecorders();
            if (Current == this) Current = null;
        }

        // ---------------- The run ----------------

        IEnumerator RunAll()
        {
            PlayerInputDriver.ReadRealDevices = false; // nobody at the keys: the mouse in the window steers nothing
            QualitySettings.vSyncCount = 0;
            Application.runInBackground = true; // a player without the focus would stop: another window may take it
            Application.targetFrameRate = -1;
            if (!Application.isEditor) Screen.SetResolution(WindowWidth, WindowHeight, FullScreenMode.Windowed);
            float wait = Time.realtimeSinceStartup + 1f;
            while (Time.realtimeSinceStartup < wait) yield return null;

            Results = new BenchmarkResults
            {
                Label = Label,
                StartedUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", s_Inv),
                InEditor = Application.isEditor,
                DevelopmentBuild = Debug.isDebugBuild,
                UnityVersion = Application.unityVersion,
                Cpu = SystemInfo.processorType,
                CpuCores = SystemInfo.processorCount,
                Gpu = SystemInfo.graphicsDeviceName,
                GraphicsApi = SystemInfo.graphicsDeviceVersion,
                SystemMemoryMb = SystemInfo.systemMemorySize,
                Window = Screen.width + "x" + Screen.height,
                FullScreenMode = Screen.fullScreenMode.ToString(),
                DisplayHz = Screen.currentResolution.refreshRateRatio.value,
                VSyncCount = QualitySettings.vSyncCount,
                TargetFrameRate = Application.targetFrameRate,
                Quality = QualitySettings.names[QualitySettings.GetQualityLevel()] + " (level " + QualitySettings.GetQualityLevel() + ")",
                RenderPipeline = GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline.name : "built-in",
                FrameTimingStats = FrameTimingManager.IsFeatureEnabled(),
                SettleSeconds = SettleSeconds,
                MeasureSeconds = MeasureSeconds,
            };
            Results.Switches.AddRange(PerformanceSwitches.Describe());
            foreach (string d in Diagnostics) Results.Notes.Add("diagnostic: " + d);
            // The scene's agents as the game starts (a player build logs "Failed to create agent" at load: are they on it now?).
            foreach (UnityEngine.AI.NavMeshAgent agent in FindObjectsByType<UnityEngine.AI.NavMeshAgent>(FindObjectsSortMode.None))
                Results.Notes.Add("at the start, " + agent.name + "'s NavMesh agent is on the NavMesh: " + agent.isOnNavMesh);
            Debug.Log("Benchmark: shaders");
            LookUpShaders();
            Debug.Log("Benchmark: counters");
            StartRecorders();
            if (ListCounters && Debug.isDebugBuild) WriteCounterList();

            foreach (BenchmarkScenario scenario in Scenarios)
            {
                Debug.Log("Benchmark: staging " + scenario);
                Stage = BenchmarkStage.Begin(scenario);
                if (Stage == null)
                {
                    Results.Notes.Add(scenario + ": no character in the scene");
                    continue;
                }
                yield return null;
                yield return null; // every enemy has had its first frame
                ApplyDiagnostics();
                SeeMaterials(scenario + " first frames");
                NoteReadability();
                float settleEnd = Time.realtimeSinceStartup + SettleSeconds;
                while (Time.realtimeSinceStartup < settleEnd) yield return null;
                SeeMaterials(scenario + " settled");
                Debug.Log("Benchmark: measuring " + scenario);

                int shotsBefore = Stage.Player.BasicShotsFired, hitsBefore = Stage.Player.BasicShotHits;
                ClearSamples();
                _skipFrames = 1; // the frame that looked at the materials is not one of the measured
                _measuring = true;
                float measureStart = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - measureStart < MeasureSeconds) yield return null;
                _measuring = false;
                float took = Time.realtimeSinceStartup - measureStart;
                SeeMaterials(scenario + " end");
                BenchmarkScenarioResult result = Summarise(scenario, took);
                result.ShotsFired = Stage.Player.BasicShotsFired - shotsBefore;
                result.ShotHits = Stage.Player.BasicShotHits - hitsBefore;
                Results.Scenarios.Add(result);
                Debug.Log(string.Format(s_Inv, "Benchmark {0}: {1} frames, mean {2:0.00} ms ({3:0} fps), p95 {4:0.00}, p99 {5:0.00}, worst {6:0.00} ms; {7} fighting, {8} awake, {9} on the floor",
                                        scenario, result.Frames, result.FrameMs.Mean, result.FrameMs.Mean > 0 ? 1000.0 / result.FrameMs.Mean : 0,
                                        result.FrameMs.P95, result.FrameMs.P99, result.FrameMs.Max, result.EnemiesFighting, result.EnemiesAwake, result.EnemiesOnFloor));
                Stage.End();
            }
            Stage = null;
            WriteResults();
            Done = true;
            Finished?.Invoke(ResultsPath);
            if (QuitWhenDone && !Application.isEditor) Application.Quit(0);
        }

        void LookUpShaders()
        {
            foreach (string name in new[] { PropFactory.ShaderName, "Universal Render Pipeline/Lit" })
            {
                Shader s = Shader.Find(name);
                string line = name + ": " + (s == null ? "NOT FOUND (Shader.Find gives null)" : "found, supported " + s.isSupported);
                Results.ShaderLookups.Add(line);
                Debug.Log("Benchmark shader lookup: " + line);
            }
        }

        bool _readabilityNoted;

        /// <summary>
        /// Once, in the first scenario with a floor: whether the meshes the code reads on the CPU can be read in this build
        /// (Unity's cube, whose top face the floors' tiling is read from; an enemy's skinned mesh, which its height is
        /// measured on; a kit piece's mesh). In the Editor every mesh can be read; in a player only one imported with
        /// Read/Write.
        /// </summary>
        void NoteReadability()
        {
            if (_readabilityNoted || Stage == null || Stage.Level == null) return;
            _readabilityNoted = true;
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Results.Notes.Add("Unity's cube mesh readable: " + cube.GetComponent<MeshFilter>().sharedMesh.isReadable);
            Destroy(cube);
            foreach (Enemy e in Stage.Level.Enemies)
            {
                if (e == null || e.Look == null || e.Look.Skin == null || e.Look.Skin.sharedMesh == null) continue;
                Results.Notes.Add("an enemy's skinned mesh readable (" + e.Look.Skin.sharedMesh.name + "): " + e.Look.Skin.sharedMesh.isReadable);
                break;
            }
            if (Stage.Level.KitPieces.Count > 0 && Stage.Level.KitPieces[0].Object.GetComponentInChildren<MeshFilter>() is MeshFilter f && f.sharedMesh != null)
                Results.Notes.Add("a kit piece's mesh readable (" + f.sharedMesh.name + "): " + f.sharedMesh.isReadable);
        }

        /// <summary>Every material on a renderer now, by its name, shader and keywords; logged the first time each is seen.</summary>
        void SeeMaterials(string where)
        {
            var counts = new Dictionary<string, BenchmarkMaterial>();
            foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                foreach (Material m in r.sharedMaterials)
                {
                    if (m == null) continue;
                    string shader = m.shader != null ? m.shader.name : "(none)";
                    string keywords = string.Join(" ", m.shaderKeywords);
                    string key = m.name + "|" + shader + "|" + keywords;
                    if (!counts.TryGetValue(key, out BenchmarkMaterial seen))
                    {
                        counts[key] = seen = new BenchmarkMaterial
                        {
                            Material = m.name, Shader = shader, Supported = m.shader != null && m.shader.isSupported,
                            ErrorShader = shader == ErrorShaderName, Keywords = keywords, FirstSeenIn = where,
                        };
                    }
                    seen.Renderers++;
                }
            }
            foreach (KeyValuePair<string, BenchmarkMaterial> pair in counts)
            {
                BenchmarkMaterial known = Results.Materials.Find(x => x.Material + "|" + x.Shader + "|" + x.Keywords == pair.Key);
                if (known != null)
                {
                    known.Renderers = Math.Max(known.Renderers, pair.Value.Renderers);
                    continue;
                }
                Results.Materials.Add(pair.Value);
                BenchmarkMaterial m = pair.Value;
                Debug.Log(string.Format(s_Inv, "Benchmark material ({0}): {1}  shader {2}  supported {3}{4}  keywords [{5}]  on {6} renderers",
                                        where, m.Material, m.Shader, m.Supported, m.ErrorShader ? "  PINK ERROR SHADER" : "", m.Keywords, m.Renderers));
            }
        }

        // ---------------- Diagnostics: one thing taken away, to see what it costs ----------------

        void ApplyDiagnostics()
        {
            if (Diagnostics.Count == 0) return;
            GeneratedLevel level = Stage.Level;
            if (Diagnostics.Contains("nogui"))
                foreach (MonoBehaviour b in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                    if (b is SkillBar || b is RunHud || b is TimingFeedback || b is BossHealthBar || b is DebugReadout || b is PlayerSigns || b is EnemyBars)
                        b.enabled = false;
            if (Diagnostics.Contains("nokit") && level != null && level.KitRoot != null) level.KitRoot.gameObject.SetActive(false);
            if (Diagnostics.Contains("noprops") && level != null)
                foreach (GeneratedLevel.PlacedProp p in level.Props)
                    if (p.Object != null)
                        foreach (Renderer r in p.Object.GetComponentsInChildren<Renderer>()) r.enabled = false;
            if (Diagnostics.Contains("noshadows"))
                foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None)) l.shadows = LightShadows.None;
            if (Diagnostics.Contains("noenemydraw"))
                foreach (Enemy e in Enemy.All)
                    if (e.Look != null)
                        foreach (Renderer r in e.Look.Renderers) r.enabled = false;
        }

        // ---------------- Measuring ----------------

        bool _measuring;
        int _skipFrames;
        readonly Stopwatch _clock = Stopwatch.StartNew();
        long _frameStart = -1, _updateStart, _lateStart;
        double _updateMs, _lateMs;
        bool _haveSections;
        long _monoBefore = -1;
        int _gcBefore;

        readonly List<double> _frameMs = new List<double>(8192);
        readonly List<Series> _series = new List<Series>();
        readonly List<Counter> _counters = new List<Counter>();
        readonly FrameTiming[] _timings = new FrameTiming[1];
        Series _sUpdate, _sLate, _sCpuFrame, _sMain, _sRender, _sWait, _sGpu, _sHeap;

        /// <summary>One number taken once a frame, made before the measuring so that nothing is made while it measures.</summary>
        sealed class Series
        {
            public string Name, Source, Unit;
            public readonly List<double> Values = new List<double>(8192);
        }

        sealed class Counter
        {
            public string Name, Source, Unit;
            public ProfilerRecorder Recorder;
            public double Scale = 1;
            public Series Series;
        }

        Series NewSeries(string name, string source, string unit)
        {
            var s = new Series { Name = name, Source = source, Unit = unit };
            _series.Add(s);
            return s;
        }

        void MakeSeries()
        {
            _series.Clear();
            _sUpdate = NewSeries("Scripts: every Update", "Stopwatch, first to last Update", "ms");
            _sLate = NewSeries("Scripts: every LateUpdate", "Stopwatch, first to last LateUpdate", "ms");
            const string ftm = "FrameTimingManager";
            _sCpuFrame = NewSeries("CPU frame", ftm, "ms");
            _sMain = NewSeries("CPU main thread", ftm, "ms");
            _sRender = NewSeries("CPU render thread", ftm, "ms");
            _sWait = NewSeries("CPU main thread waiting for present", ftm, "ms");
            _sGpu = NewSeries("GPU frame", ftm, "ms");
            _sHeap = NewSeries("Managed heap growth per frame (no GC that frame)", "Profiler.GetMonoUsedSizeLong", "KB");
        }

        static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

        void Update()
        {
            long now = _clock.ElapsedTicks;
            if (_measuring && _skipFrames > 0)
            {
                _skipFrames--;
                FrameTimingManager.CaptureFrameTimings();
            }
            else if (_measuring && _frameStart >= 0)
            {
                _frameMs.Add(Ms(now - _frameStart));
                if (_haveSections)
                {
                    _sUpdate.Values.Add(_updateMs);
                    _sLate.Values.Add(_lateMs);
                }
                for (int i = 0; i < _counters.Count; i++)
                {
                    Counter c = _counters[i];
                    if (c.Recorder.Valid) c.Series.Values.Add(c.Recorder.LastValue * c.Scale);
                }
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, _timings) > 0)
                {
                    _sCpuFrame.Values.Add(_timings[0].cpuFrameTime);
                    _sMain.Values.Add(_timings[0].cpuMainThreadFrameTime);
                    _sRender.Values.Add(_timings[0].cpuRenderThreadFrameTime);
                    _sWait.Values.Add(_timings[0].cpuMainThreadPresentWaitTime);
                    _sGpu.Values.Add(_timings[0].gpuFrameTime);
                }
                long mono = Profiler.GetMonoUsedSizeLong();
                int gc = GC.CollectionCount(0);
                if (_monoBefore >= 0 && gc == _gcBefore && mono >= _monoBefore) _sHeap.Values.Add((mono - _monoBefore) / 1024.0);
                _monoBefore = mono;
                _gcBefore = gc;
            }
            else if (!_measuring) FrameTimingManager.CaptureFrameTimings(); // keeps its history going
            _frameStart = now;
            _updateStart = now;
        }

        /// <summary>Called by <see cref="BenchmarkFrameEnd"/>, the last Update of the frame.</summary>
        internal void UpdatesEnded()
        {
            _updateMs = Ms(_clock.ElapsedTicks - _updateStart);
        }

        void LateUpdate() => _lateStart = _clock.ElapsedTicks;

        /// <summary>Called by <see cref="BenchmarkFrameEnd"/>, the last LateUpdate of the frame.</summary>
        internal void LateUpdatesEnded()
        {
            _lateMs = Ms(_clock.ElapsedTicks - _lateStart);
            _haveSections = true;
            Stage?.Keep();
        }

        void ClearSamples()
        {
            _frameMs.Clear();
            foreach (Series s in _series) s.Values.Clear();
            _monoBefore = -1;
        }

        /// <summary>The counters asked for by name; each found in the build's list of what it has, or noted as missing.</summary>
        static readonly (string name, string unit, double scale)[] s_Wanted =
        {
            ("Draw Calls Count", "count", 1), ("Batches Count", "count", 1), ("SetPass Calls Count", "count", 1),
            ("Triangles Count", "count", 1), ("Vertices Count", "count", 1), ("Shadow Casters Count", "count", 1),
            ("Visible Skinned Meshes Count", "count", 1),
            ("GC Allocated In Frame", "KB", 1.0 / 1024), ("GC Allocation In Frame Count", "count", 1),
            ("Main Thread", "ms", 1e-6), ("Render Thread", "ms", 1e-6),
            ("PlayerLoop", "ms", 1e-6), ("Update.ScriptRunBehaviourUpdate", "ms", 1e-6),
            ("PreLateUpdate.ScriptRunBehaviourLateUpdate", "ms", 1e-6), ("PreUpdate.AIUpdatePostScript", "ms", 1e-6),
            ("FixedUpdate.PhysicsFixedUpdate", "ms", 1e-6), ("PreLateUpdate.DirectorUpdateAnimationBegin", "ms", 1e-6),
            ("PreLateUpdate.DirectorUpdateAnimationEnd", "ms", 1e-6), ("PostLateUpdate.UpdateAllSkinnedMeshes", "ms", 1e-6),
            ("PostLateUpdate.UpdateAllRenderers", "ms", 1e-6), ("PostLateUpdate.FinishFrameRendering", "ms", 1e-6),
            ("PostLateUpdate.PresentAfterDraw", "ms", 1e-6), ("PlayableGraph.Evaluate", "ms", 1e-6),
            ("Animators.Update", "ms", 1e-6), ("MeshSkinning.Update", "ms", 1e-6), ("GUI.Repaint", "ms", 1e-6),
            ("GUIUtility.ProcessEvent", "ms", 1e-6), ("Inl_RenderCameraStack", "ms", 1e-6),
            ("Shadows.RenderShadowMap", "ms", 1e-6), ("NavMeshManager", "ms", 1e-6), ("GC.Collect", "ms", 1e-6),
        };

        /// <summary>
        /// The counters every build may have, by category and name: started with <see cref="ProfilerRecorder.StartNew(ProfilerCategory, string, int, ProfilerRecorderOptions)"/>;
        /// one the build lacks gives an invalid recorder, written as not working.
        /// </summary>
        static readonly (ProfilerCategory category, string name, string unit, double scale)[] s_Counters =
        {
            (ProfilerCategory.Render, "Draw Calls Count", "count", 1), (ProfilerCategory.Render, "Batches Count", "count", 1),
            (ProfilerCategory.Render, "SetPass Calls Count", "count", 1), (ProfilerCategory.Render, "Triangles Count", "count", 1),
            (ProfilerCategory.Render, "Vertices Count", "count", 1), (ProfilerCategory.Render, "Shadow Casters Count", "count", 1),
            (ProfilerCategory.Render, "Visible Skinned Meshes Count", "count", 1),
            (ProfilerCategory.Memory, "GC Allocated In Frame", "KB", 1.0 / 1024), (ProfilerCategory.Memory, "GC Allocation In Frame Count", "count", 1),
            (ProfilerCategory.Internal, "Main Thread", "ms", 1e-6),
        };

        void StartRecorders()
        {
            StopRecorders();
            MakeSeries();
            foreach ((ProfilerCategory category, string name, string unit, double scale) in s_Counters)
            {
                ProfilerRecorder recorder = ProfilerRecorder.StartNew(category, name, 1);
                string source = "ProfilerRecorder (" + category.Name + ")";
                _counters.Add(new Counter { Name = name, Source = source, Unit = unit, Recorder = recorder, Scale = scale, Series = NewSeries(name, source, unit) });
            }
            // The player loop's markers, in a development build only (a release build has none): found by name in the
            // build's own list, whatever their category.
            if (!Debug.isDebugBuild) return;
            var available = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(available);
            var byName = new Dictionary<string, ProfilerRecorderHandle>();
            foreach (ProfilerRecorderHandle h in available)
            {
                ProfilerRecorderDescription d = ProfilerRecorderHandle.GetDescription(h);
                if (!byName.ContainsKey(d.Name)) byName[d.Name] = h;
            }
            foreach ((string name, string unit, double scale) in s_Wanted)
            {
                bool taken = false;
                foreach (Counter c in _counters) taken |= c.Name == name;
                if (taken) continue;
                if (!byName.TryGetValue(name, out ProfilerRecorderHandle handle))
                {
                    Results.Notes.Add("marker not in this build: " + name);
                    continue;
                }
                ProfilerRecorderDescription d = ProfilerRecorderHandle.GetDescription(handle);
                var recorder = new ProfilerRecorder(handle, 1, ProfilerRecorderOptions.Default);
                recorder.Start();
                string source = "ProfilerRecorder (" + d.Category.Name + ")";
                _counters.Add(new Counter { Name = name, Source = source, Unit = unit, Recorder = recorder, Scale = scale, Series = NewSeries(name, source, unit) });
            }
        }

        void StopRecorders()
        {
            foreach (Counter c in _counters) c.Recorder.Dispose();
            _counters.Clear();
        }

        void WriteCounterList()
        {
            var available = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(available);
            var lines = new List<string>();
            foreach (ProfilerRecorderHandle h in available)
            {
                ProfilerRecorderDescription d = ProfilerRecorderHandle.GetDescription(h);
                lines.Add(d.Category.Name + "\t" + d.Name + "\t" + d.UnitType + "\t" + d.DataType);
            }
            lines.Sort(StringComparer.Ordinal);
            Directory.CreateDirectory(OutputFolder);
            File.WriteAllLines(Path.Combine(OutputFolder, "counters-" + (Debug.isDebugBuild ? "development" : "release") + (Application.isEditor ? "-editor" : "") + ".txt"), lines);
        }

        BenchmarkScenarioResult Summarise(BenchmarkScenario scenario, float took)
        {
            BenchmarkStage stage = Stage;
            var r = new BenchmarkScenarioResult
            {
                Scenario = (int)scenario + " " + scenario,
                Floor = stage.Level != null ? stage.Level.Layout.Floor + " (seed " + stage.Level.Layout.SeedText + ")" : "practice room",
                Room = stage.Room != null ? (stage.Room.Order + 1) + " (" + stage.Room.Kind.ToString().ToLowerInvariant() + (stage.Room.Elite ? ", elite" : "") + ")" : "",
                Frames = _frameMs.Count,
                Seconds = took,
                PacksCalled = stage.Boss != null ? stage.Boss.PacksCalled : 0,
                EnemiesOnFloor = stage.Level != null ? stage.Level.Enemies.Count : 0,
            };
            foreach (Enemy e in stage.Fighting)
                if (e != null && !e.IsDead) r.EnemiesFighting++;
            if (stage.Level != null)
                foreach (Enemy e in stage.Level.Enemies)
                {
                    if (e != null && e.IsAwake) r.EnemiesAwake++;
                    if (e != null && e.Look != null && e.Look.InSight && !e.IsDead) r.EnemiesPosed++;
                }
            r.FrameMs = Stat("Frame time", "Stopwatch, start of one frame's Updates to the next", "ms", _frameMs);
            foreach (Series s in _series) r.Counters.Add(Stat(s.Name, s.Source, s.Unit, s.Values));
            return r;
        }

        static BenchmarkStat Stat(string name, string source, string unit, List<double> values)
        {
            var sorted = new List<double>(values);
            sorted.Sort();
            bool any = false;
            foreach (double v in values)
                if (v != 0) { any = true; break; }
            return new BenchmarkStat
            {
                Name = name, Source = source, Unit = unit, Works = any, Samples = values.Count,
                Mean = FrameStats.Mean(values), P95 = FrameStats.PercentileOfSorted(sorted, 95), P99 = FrameStats.PercentileOfSorted(sorted, 99),
                Max = FrameStats.Max(values),
            };
        }

        void WriteResults()
        {
            Directory.CreateDirectory(OutputFolder);
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", s_Inv);
            string name = "bench-" + stamp + (Label.Length > 0 ? "-" + Safe(Label) : "") + (Application.isEditor ? "-editor" : "");
            ResultsPath = Path.Combine(OutputFolder, name + ".json");
            File.WriteAllText(ResultsPath, JsonUtility.ToJson(Results, true), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(OutputFolder, name + ".txt"), Text(Results), new UTF8Encoding(false));
            Debug.Log("Benchmark results: " + ResultsPath);
        }

        static string Safe(string s)
        {
            var b = new StringBuilder();
            foreach (char c in s) b.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_');
            return b.ToString();
        }

        /// <summary>The results as plain text: the conditions, then a block per scenario.</summary>
        public static string Text(BenchmarkResults r)
        {
            var b = new StringBuilder();
            b.AppendLine("Benchmark (work order 032) " + r.Label + "  " + r.StartedUtc + " UTC" + (r.InEditor ? "  IN THE EDITOR" : "") + (r.DevelopmentBuild ? "  development build" : ""));
            b.AppendLine("Unity " + r.UnityVersion + "   CPU " + r.Cpu + " (" + r.CpuCores + " threads)   GPU " + r.Gpu + " (" + r.GraphicsApi + ")   RAM " + r.SystemMemoryMb + " MB");
            b.AppendLine("window " + r.Window + " " + r.FullScreenMode + " on a " + r.DisplayHz.ToString("0.#", s_Inv) + " Hz display   vSync " + r.VSyncCount + "   target fps " + r.TargetFrameRate + "   quality " + r.Quality + "   pipeline " + r.RenderPipeline + "   frame timing stats " + r.FrameTimingStats);
            b.AppendLine("settle " + r.SettleSeconds + " s, measure " + r.MeasureSeconds + " s   switches: " + string.Join(", ", r.Switches));
            foreach (string s in r.ShaderLookups) b.AppendLine("shader " + s);
            foreach (string n in r.Notes) b.AppendLine("note: " + n);
            foreach (BenchmarkScenarioResult s in r.Scenarios)
            {
                b.AppendLine();
                b.AppendLine(string.Format(s_Inv, "{0}   floor {1}   room {2}   fighting {3}, awake {4}, on the floor {5} ({9} posed), packs called {6}, shots {7} ({8} hits)",
                                           s.Scenario, s.Floor, s.Room, s.EnemiesFighting, s.EnemiesAwake, s.EnemiesOnFloor, s.PacksCalled, s.ShotsFired, s.ShotHits, s.EnemiesPosed));
                b.AppendLine(string.Format(s_Inv, "  frames {0} in {1:0.0} s   mean {2:0.000} ms ({3:0.0} fps)   p95 {4:0.000}   p99 {5:0.000}   worst {6:0.000} ms",
                                           s.Frames, s.Seconds, s.FrameMs.Mean, s.FrameMs.Mean > 0 ? 1000 / s.FrameMs.Mean : 0, s.FrameMs.P95, s.FrameMs.P99, s.FrameMs.Max));
                foreach (BenchmarkStat c in s.Counters)
                    b.AppendLine(c.Works
                        ? string.Format(s_Inv, "  {0,-50} {1,10:0.###} mean {2,10:0.###} p95 {3,10:0.###} p99 {4,10:0.###} max  {5}  [{6}]", c.Name, c.Mean, c.P95, c.P99, c.Max, c.Unit, c.Source)
                        : string.Format(s_Inv, "  {0,-50} not working in this build  [{1}]", c.Name, c.Source));
            }
            b.AppendLine();
            b.AppendLine("materials seen:");
            foreach (BenchmarkMaterial m in r.Materials)
                b.AppendLine(string.Format(s_Inv, "  {0}  shader {1}  supported {2}{3}  [{4}]  {5} renderers  ({6})", m.Material, m.Shader, m.Supported,
                                           m.ErrorShader ? "  PINK" : "", m.Keywords, m.Renderers, m.FirstSeenIn));
            return b.ToString();
        }
    }
}
