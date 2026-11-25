using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using Roguelike.Rules;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 090, item 4: the managed garbage a frame makes in the files work order 081 was told to leave (RunHud.cs and its
    /// partial files, RunHudMenus.cs, CombatEffects.cs, HitFlash.cs), in a room fight of a fixed seed (the first combat room of
    /// run <see cref="RunSeed"/>, woken by her standing in it, her shooting at it, the run's HUD and the fight's effects up) and
    /// under the pause menu over it.
    ///
    /// Two measures. The whole frame, by the Profiler's own recorder for GC allocations ("GC Allocated In Frame", every managed
    /// allocation of the frame: the game's, the engine's and the Editor's and test runner's around it in a batch run), over
    /// <see cref="WholeFrames"/> frames. And each caller of those files alone (its Update, LateUpdate, and its OnGUI called by
    /// hand inside an IMGUI container for a layout and a repaint event, as the engine calls it each frame: a batch run sends no
    /// OnGUI), its allocations counted by the profiler's GC.Alloc samples over <see cref="Calls"/> calls (as the test framework's
    /// own "allocates nothing" constraint counts them) and its bytes by the same "GC Allocated In Frame" of frames with
    /// <see cref="Calls"/> calls of it more against the frames around them (the runtime's own count for a thread gives nothing in
    /// this player). The engine's own IMGUI calls are measured beside them, for what is left. The test prints all of it and fails
    /// when the callers' bytes a frame come back above their ceilings (a little over what was measured after the fix).
    /// </summary>
    public class FrameGarbageTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        const ulong RunSeed = 0x0900090UL;
        const int WholeFrames = 120;
        const int Calls = 100;
        const int MostFrames = 600;

        // The ceilings, bytes a frame of the callers of the four files together (work order 090: a little above the result after
        // the fix, so that the test fails if the garbage comes back). Measured on 2026-11-23: a fight's frame 4521 B before, 531 B
        // after (what is left is the engine's own, about 36 B for each label it draws in a repaint: eleven); the pause menu's
        // 20635 B before, 7847 B after (the same, 199 labels).
        const long FightCeiling = 700;
        // 2026-11-25: 9000 before. The pause menu has two more settings lines (work order 121's Camera angle, 125's Anti-aliasing);
        // a line is 6 allocations and about 232 B of the engine's own in a repaint. Measured on the merged build: 9421 B, 237
        // allocations (it was 8957 B, 225 allocations with neither line). The ceiling is again a little above what was measured.
        const long PauseCeiling = 9500;
        /// <summary>Work order 095: her sheet on Tab with a skill's panel open (Quick Shot's), the same way: 5491 B measured on 2026-11-23 before the values lost their dark copy.</summary>
        const long SheetCeiling = 6500;

        PlayerCharacter _player;
        PlayerHealth _health;
        GeneratedLevels _levels;
        RunHud _hud;
        CombatEffects _effects;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            InterfaceStyle.Shared.Kinetic = true;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null; // the real mouse and keyboard must not steer a test
            _health = _player.GetComponent<PlayerHealth>();
            _health.Untouchable = true;
            _levels = GeneratedLevels.Ensure();
            _levels.RunValues.SkillTiers = false;
            _hud = RunHud.Ensure();
            _effects = CombatEffects.Ensure();
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            if (_health != null) _health.Untouchable = false;
            foreach (MonoBehaviour m in new MonoBehaviour[] { _effects, _hud, _levels })
                if (m != null) Object.Destroy(m.gameObject);
            _effects = null;
            _hud = null;
            _levels = null;
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        // ---------------- The test ----------------

        [UnityTest]
        public IEnumerator ARoomFight_AndThePauseOverIt_TheFilesOf081sListMakeLittleGarbageAFrame()
        {
            Assume.That(Imgui.Available, Is.True, "the engine's IMGUI entry points");
            var log = new StringBuilder();
            log.AppendLine("FRAMEGARBAGE work order 090, run " + LevelSeed.ToText(RunSeed) + ", the first combat room");

            // The fight: she stands inside the first combat room, past its line; it wakes; she shoots at the nearest of it.
            GeneratedLevel level = _levels.StartRun(RunSeed);
            Assert.That(level, Is.Not.Null);
            yield return null;
            LevelRoom room = level.Layout.Rooms.First(r => r.Kind == RoomKind.Combat);
            List<Enemy> own = level.EnemiesByRoom[room.Order];
            Vector3 middle = level.ToWorld(room.Middle(level.Layout.CellSize));
            if (NavMesh.SamplePosition(middle, out NavMeshHit hit, 4f, NavMesh.AllAreas)) middle = hit.position;
            _player.Teleport(new Vector3(middle.x, 0f, middle.z));
            yield return Frames(() => _levels.CurrentRoom == room && own.Any(e => e != null && e.IsAwake));
            Assume.That(own.Any(e => e != null && e.IsAwake), Is.True, "the room woke");
            yield return Fighting(own, 2f);
            Assume.That(own.Count(e => e != null && !e.IsDead), Is.GreaterThan(0), "the fight still on");
            Assume.That(_hud.RunBoxLines.Count, Is.GreaterThan(0), "the run box up");

            long fightWhole = 0;
            yield return WholeFrame(own, v => fightWhole = v, log, "a room fight");
            long fight = 0;
            yield return Callers(log, "a room fight", own, v => fight = v);

            // The pause menu over the fight.
            Assert.That(_hud.Escape(), Is.True, "paused");
            for (int i = 0; i < 5; i++) yield return null;
            Assume.That(_levels.Paused, Is.True);
            long pauseWhole = 0;
            yield return WholeFrame(null, v => pauseWhole = v, log, "the pause menu");
            long pause = 0;
            yield return Callers(log, "the pause menu", own, v => pause = v);
            _hud.Escape();

            // Work order 095: her sheet over the fight, a skill's panel open, the mouse still.
            Assert.That(_hud.OpenSheet(), Is.True, "the sheet up");
            _hud.HoverSheetSkill(1);
            for (int i = 0; i < 5; i++) yield return null;
            long sheet = 0;
            yield return Callers(log, "the sheet", own, v => sheet = v);
            _hud.CloseSheet();

            log.AppendLine(string.Format(Inv, "FRAMEGARBAGE in all: a fight's frame {0} B by the callers of the four files ({1} B the whole frame); the pause menu's {2} B ({3} B the whole frame); the sheet's {4} B",
                                         fight, fightWhole, pause, pauseWhole, sheet));
            Debug.Log(log.ToString());
            TestContext.WriteLine(log.ToString());
            _levels.BackToPracticeRoom();
            Assert.That(fight, Is.LessThanOrEqualTo(FightCeiling), "a fight's frame: the garbage came back\n" + log);
            Assert.That(pause, Is.LessThanOrEqualTo(PauseCeiling), "the pause menu's frame: the garbage came back\n" + log);
            Assert.That(sheet, Is.LessThanOrEqualTo(SheetCeiling), "the sheet's frame: too much garbage\n" + log);
        }

        // ---------------- The measures ----------------

        /// <summary>The whole frame's managed allocations, by the profiler's recorder, over <see cref="WholeFrames"/> frames: the mean, the median and the most.</summary>
        IEnumerator WholeFrame(List<Enemy> shootAt, Action<long> mean, StringBuilder log, string when)
        {
            var bytes = new List<long>();
            using (ProfilerRecorder rec = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1))
            {
                yield return null;
                yield return null;
                for (int i = 0; i < WholeFrames; i++)
                {
                    if (shootAt != null && i % 10 == 0) ShootAtNearest(shootAt);
                    yield return null;
                    if (rec.Valid) bytes.Add(rec.LastValue);
                }
                if (!rec.Valid) log.AppendLine("FRAMEGARBAGE " + when + ": the profiler's \"GC Allocated In Frame\" is not given here");
            }
            if (bytes.Count == 0)
            {
                mean(-1);
                yield break;
            }
            bytes.Sort();
            long m = (long)Math.Round(bytes.Average());
            mean(m);
            log.AppendLine(string.Format(Inv, "FRAMEGARBAGE {0}, the whole frame (GC Allocated In Frame, {1} frames): mean {2} B, median {3} B, most {4} B",
                                         when, bytes.Count, m, bytes[bytes.Count / 2], bytes[bytes.Count - 1]));
        }

        /// <summary>
        /// Each caller of the four files alone: its allocations a call (the profiler's GC.Alloc samples, <see cref="Calls"/> calls)
        /// and its bytes a call (the profiler's "GC Allocated In Frame" of frames with <see cref="Calls"/> calls of it more, less the
        /// frames around them without, the medians of <see cref="LoadedFrames"/> each, over the calls), into the log, a line each;
        /// gives the bytes a frame of them all (each Update and LateUpdate once, each OnGUI for a layout and a repaint event, as the
        /// engine calls it). Then, apart and not in the sum, the engine's own IMGUI calls the drawing is made of, for what is left.
        /// </summary>
        IEnumerator Callers(StringBuilder log, string when, List<Enemy> enemies, Action<long> frameBytes)
        {
            var rows = new List<(string name, double bytes, double allocs, bool inSum)>();
            double boxBytes = 0, boxAllocs = 0;
            IEnumerator Row(string name, Action call, bool box = false, bool inSum = true)
            {
                call(); // once first: what is made once (a style, a cache) is not a frame's
                double allocs = Allocations(call);
                double bytes = 0;
                yield return Bytes(call, b => bytes = b);
                rows.Add((name, Math.Max(0, bytes - (box ? boxBytes : 0)), Math.Max(0, allocs - (box ? boxAllocs : 0)), inSum));
            }
            // The container's own calls (by reflection) measured round an OnGUI that does nothing, and taken off the OnGUI rows.
            Action nothing = () => { };
            Action emptyGui = () => Imgui.Call(nothing, EventType.Layout);
            emptyGui();
            boxAllocs = Allocations(emptyGui);
            yield return Bytes(emptyGui, b => boxBytes = b);

            yield return Row("RunHud.Update", Method(_hud, "Update"));
            yield return Row("RunHud.LateUpdate (Tick)", _hud.Tick);
            Action hudGui = Method(_hud, "OnGUI");
            yield return Row("RunHud.OnGUI, a layout event", () => Imgui.Call(hudGui, EventType.Layout), box: true);
            yield return Row("RunHud.OnGUI, a repaint event", () => Imgui.Call(hudGui, EventType.Repaint), box: true);
            yield return Row("CombatEffects.LateUpdate", Method(_effects, "LateUpdate"));
            var flashes = new List<Action>();
            foreach (Enemy e in enemies)
                if (e != null && e.TryGetComponent(out HitFlash f)) flashes.Add(Method(f, "Update"));
            yield return Row("HitFlash.Update, all " + flashes.Count + " of the room's enemies", () => { for (int i = 0; i < flashes.Count; i++) flashes[i](); });

            // The engine's IMGUI calls alone, one event each (what the HUD and the menus are drawn with): apart, not in the sum.
            var content = new GUIContent("Volume");
            GUIStyle text = InterfaceStyle.Text(false, 22, TextAnchor.MiddleLeft);
            var r = new Rect(10f, 10f, 200f, 30f);
            Action drawTexture = () => GUI.DrawTexture(r, Texture2D.whiteTexture);
            Action label = () => GUI.Label(r, content, text);
            Action button = () => GUI.Button(r, GUIContent.none, GUIStyle.none);
            Action matrix = () =>
            {
                Matrix4x4 m = GUI.matrix;
                GUI.matrix = m * Matrix4x4.identity;
                GUI.matrix = m;
            };
            yield return Row("(engine) GUI.DrawTexture, a repaint", () => Imgui.Call(drawTexture, EventType.Repaint), box: true, inSum: false);
            yield return Row("(engine) GUI.Label, a repaint", () => Imgui.Call(label, EventType.Repaint), box: true, inSum: false);
            yield return Row("(engine) GUI.Label, a layout", () => Imgui.Call(label, EventType.Layout), box: true, inSum: false);
            yield return Row("(engine) GUI.Button, a repaint", () => Imgui.Call(button, EventType.Repaint), box: true, inSum: false);
            yield return Row("(engine) GUI.Button, a layout", () => Imgui.Call(button, EventType.Layout), box: true, inSum: false);
            yield return Row("(engine) GUI.matrix set and put back, a repaint", () => Imgui.Call(matrix, EventType.Repaint), box: true, inSum: false);
            // A label's words set anew each call, as the shared content of InterfaceStyle.Label and GUIContent.Temp are.
            bool flip = false;
            var shared = new GUIContent("Volume");
            Action setText = () => shared.text = (flip = !flip) ? "Volume" : "Damage numbers";
            Action labelNewText = () =>
            {
                shared.text = (flip = !flip) ? "Volume" : "Damage numbers";
                GUI.Label(r, shared, text);
            };
            Action labelString = () => GUI.Label(r, (flip = !flip) ? "Volume" : "Damage numbers", text);
            yield return Row("(engine) GUIContent.text set to other words", setText, inSum: false);
            yield return Row("(engine) GUI.Label, a layout, other words each call", () => Imgui.Call(labelNewText, EventType.Layout), box: true, inSum: false);
            yield return Row("(engine) GUI.Label, a repaint, other words each call", () => Imgui.Call(labelNewText, EventType.Repaint), box: true, inSum: false);
            yield return Row("(engine) GUI.Label(string), a repaint, other words", () => Imgui.Call(labelString, EventType.Repaint), box: true, inSum: false);

            long frame = 0;
            foreach (var row in rows)
            {
                log.AppendLine(string.Format(Inv, "FRAMEGARBAGE {0}: {1,-50} {2,8:0} B a call, {3,6:0.##} allocations a call", when, row.name, row.bytes, row.allocs));
                if (row.inSum) frame += (long)Math.Round(row.bytes);
            }
            log.AppendLine(string.Format(Inv, "FRAMEGARBAGE {0}: the four files' callers together, {1} B a frame (the container round an OnGUI, taken off: {2:0} B, {3:0.##} allocations)",
                                         when, frame, boxBytes, boxAllocs));
            frameBytes(frame);
        }

        const int LoadedFrames = 5;

        /// <summary>A call's bytes, by the profiler's "GC Allocated In Frame": frames with <see cref="Calls"/> calls of it more, against the frames around them.</summary>
        static IEnumerator Bytes(Action call, Action<double> perCall)
        {
            var quiet = new List<long>();
            var loaded = new List<long>();
            using (ProfilerRecorder rec = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1))
            {
                yield return null;
                for (int i = 0; i < LoadedFrames; i++)
                {
                    yield return null;
                    quiet.Add(rec.LastValue);
                    for (int k = 0; k < Calls; k++) call();
                    yield return null;
                    loaded.Add(rec.LastValue);
                }
            }
            quiet.Sort();
            loaded.Sort();
            perCall(Math.Max(0L, loaded[loaded.Count / 2] - quiet[quiet.Count / 2]) / (double)Calls);
        }

        /// <summary>A call's allocations, by the profiler's GC.Alloc samples on this thread (as the test framework's own constraint counts them), over <see cref="Calls"/> calls.</summary>
        static double Allocations(Action call)
        {
            Recorder r = Recorder.Get("GC.Alloc");
            r.enabled = false;
            r.FilterToCurrentThread();
            r.enabled = true;
            try
            {
                for (int i = 0; i < Calls; i++) call();
            }
            finally
            {
                r.enabled = false;
                r.CollectFromAllThreads();
            }
            return r.sampleBlockCount / (double)Calls;
        }

        // ---------------- Helpers ----------------

        static Action Method(object target, string name)
        {
            MethodInfo m = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            Assert.That(m, Is.Not.Null, target.GetType().Name + "." + name);
            return (Action)Delegate.CreateDelegate(typeof(Action), target, m);
        }

        float _nextShot;

        void ShootAtNearest(List<Enemy> enemies)
        {
            if (Time.time < _nextShot) return;
            Enemy nearest = null;
            float best = float.MaxValue;
            foreach (Enemy e in enemies)
            {
                if (e == null || e.IsDead) continue;
                float d = (e.transform.position - _player.Position).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    nearest = e;
                }
            }
            if (nearest == null) return;
            _player.Shoot(nearest.transform.position);
            _nextShot = Time.time + 0.25f;
        }

        IEnumerator Fighting(List<Enemy> enemies, float realSeconds)
        {
            float end = Time.realtimeSinceStartup + realSeconds;
            while (Time.realtimeSinceStartup < end)
            {
                ShootAtNearest(enemies);
                yield return null;
            }
        }

        static IEnumerator Frames(Func<bool> condition)
        {
            for (int i = 0; i < MostFrames && !condition(); i++) yield return null;
        }

        /// <summary>
        /// One OnGUI called inside an IMGUI container for one event (as <see cref="ImguiByHand"/> does it, work order 068), the
        /// container's own setting up and putting away outside the call that is measured: <see cref="Call"/> runs the container
        /// round the given call; the reflection's allocations are made before and after it.
        /// </summary>
        static class Imgui
        {
            static Type s_state;
            static MethodInfo s_beginContainer, s_endContainer, s_beginGui, s_endGui;
            static object s_stateObject;
            static object[] s_beginArgs, s_endArgs, s_containerArgs;
            static readonly Event s_layout = new Event { type = EventType.Layout, mousePosition = new Vector2(-1000f, -1000f) };
            static readonly Event s_repaint = new Event { type = EventType.Repaint, mousePosition = new Vector2(-1000f, -1000f) };

            public static bool Available => Find();

            static bool Find()
            {
                if (s_beginContainer != null) return true;
                const BindingFlags Any = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
                Type gu = typeof(GUIUtility);
                s_state = gu.Assembly.GetType("UnityEngine.ObjectGUIState");
                foreach (MethodInfo m in gu.GetMethods(Any))
                {
                    ParameterInfo[] p = m.GetParameters();
                    if (m.Name == "BeginContainer" && p.Length == 1 && p[0].ParameterType == s_state) s_beginContainer = m;
                    if (m.Name == "EndContainer" && p.Length == 0) s_endContainer = m;
                    if (m.Name == "BeginGUI" && p.Length == 3) s_beginGui = m;
                    if (m.Name == "EndGUI" && p.Length == 1) s_endGui = m;
                }
                if (s_state == null || s_beginContainer == null || s_endContainer == null || s_beginGui == null || s_endGui == null)
                {
                    s_beginContainer = null;
                    return false;
                }
                s_stateObject = Activator.CreateInstance(s_state, true);
                s_containerArgs = new[] { s_stateObject };
                s_beginArgs = Args(s_beginGui, 0, 0, 1);
                s_endArgs = Args(s_endGui, 1);
                return true;
            }

            /// <summary>Calls <paramref name="onGui"/> for one event inside a container (the container's own calls are measured round an empty call by the caller, and taken off).</summary>
            public static void Call(Action onGui, EventType type)
            {
                Find();
                s_beginContainer.Invoke(null, s_containerArgs);
                s_beginGui.Invoke(null, s_beginArgs);
                Event saved = Event.current;
                Event.current = type == EventType.Layout ? s_layout : s_repaint;
                s_layout.type = EventType.Layout;
                s_repaint.type = EventType.Repaint;
                try
                {
                    GUI.depth = 0;
                    onGui();
                }
                finally
                {
                    try { s_endGui.Invoke(null, s_endArgs); } catch (Exception) { }
                    s_endContainer.Invoke(null, null);
                    Event.current = saved;
                }
            }

            static object[] Args(MethodInfo m, params int[] values)
            {
                ParameterInfo[] p = m.GetParameters();
                var a = new object[p.Length];
                for (int i = 0; i < p.Length; i++)
                {
                    Type t = p[i].ParameterType;
                    int value = i < values.Length ? values[i] : 0;
                    if (t == typeof(int)) a[i] = value;
                    else if (t == typeof(bool)) a[i] = value != 0;
                    else if (t.IsEnum) a[i] = Enum.ToObject(t, value);
                    else a[i] = t.IsValueType ? Activator.CreateInstance(t) : null;
                }
                return a;
            }
        }
    }
}
