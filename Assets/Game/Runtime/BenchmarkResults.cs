using System;
using System.Collections.Generic;

namespace Roguelike
{
    /// <summary>
    /// What one run of the benchmark wrote (work order 032, 2026-11-20): the conditions it ran under, and per scenario
    /// the frame times and Unity's own counters, with which counters gave numbers in this build. Written as JSON with
    /// <c>JsonUtility</c>, one file per run, under the system's temp folder.
    /// </summary>
    [Serializable]
    public sealed class BenchmarkResults
    {
        public string Label = "";
        public string StartedUtc = "";
        public bool InEditor;
        public bool DevelopmentBuild;
        public string UnityVersion = "";
        public string Cpu = "";
        public int CpuCores;
        public string Gpu = "";
        public string GraphicsApi = "";
        public int SystemMemoryMb;
        public string Window = "";
        public string FullScreenMode = "";
        /// <summary>The display's refresh rate: a window without tearing shows no more frames a second than this.</summary>
        public double DisplayHz;
        public int VSyncCount;
        public int TargetFrameRate;
        public string Quality = "";
        public string RenderPipeline = "";
        public bool FrameTimingStats;
        public float SettleSeconds;
        public float MeasureSeconds;
        /// <summary>The optimisations as they were (work order 032): "name=on" or "name=off", one each.</summary>
        public List<string> Switches = new List<string>();
        public List<string> ShaderLookups = new List<string>();
        public List<BenchmarkMaterial> Materials = new List<BenchmarkMaterial>();
        public List<BenchmarkScenarioResult> Scenarios = new List<BenchmarkScenarioResult>();
        public List<string> Notes = new List<string>();
    }

    /// <summary>One kind of material seen on a renderer while a scenario ran: its shader and whether that is supported.</summary>
    [Serializable]
    public sealed class BenchmarkMaterial
    {
        public string Material = "";
        public string Shader = "";
        public bool Supported;
        public bool ErrorShader;
        public string Keywords = "";
        public int Renderers;
        public string FirstSeenIn = "";
    }

    /// <summary>One scenario's numbers.</summary>
    [Serializable]
    public sealed class BenchmarkScenarioResult
    {
        public string Scenario = "";
        public string Floor = "";
        public string Room = "";
        public int EnemiesFighting;
        public int EnemiesAwake;
        public int EnemiesOnFloor;
        /// <summary>Enemies of the floor whose model was posed at the scenario's end (work order 032: in the camera's sight).</summary>
        public int EnemiesPosed;
        public int PacksCalled;
        public int ShotsFired;
        public int ShotHits;
        public int Frames;
        public float Seconds;
        public BenchmarkStat FrameMs = new BenchmarkStat();
        public List<BenchmarkStat> Counters = new List<BenchmarkStat>();
    }

    /// <summary>A number taken once a frame, summed up: mean, 95th and 99th percentile, the largest; and whether the source gave any.</summary>
    [Serializable]
    public sealed class BenchmarkStat
    {
        public string Name = "";
        public string Source = "";
        public string Unit = "";
        /// <summary>False when the counter was not there in this build or gave nothing but zeros.</summary>
        public bool Works;
        public int Samples;
        public double Mean;
        public double P95;
        public double P99;
        public double Max;
    }
}
