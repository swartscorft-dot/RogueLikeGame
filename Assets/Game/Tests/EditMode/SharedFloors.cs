using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

[assembly: Roguelike.Rules.Tests.SharedFloorsCount]

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// The floors the long sweeps read, for the test run (work order 155: "generate once, check many times"; the full EditMode run
    /// took 3 hours 20 minutes, and the order's measurement found the generator's own time some 88 % of the rule tests' time: 2,876 s of
    /// 3,285 in the .NET run, 297,336 floors generated, 245,445 of them different). Two things, neither of which changes a floor or a
    /// check: a floor another test of the run already generated is read again (<see cref="Generate"/>), and the floors a sweep is
    /// about to walk are generated a batch at a time on <see cref="Threads"/> threads (<see cref="Prefetch"/>, <see cref="PrefetchRuns"/>,
    /// and the guess of a sweep's next seeds, <see cref="Guess"/>), the sweep then reading them in its own order, checking each as
    /// before. Only the tests' assembly holds it; the game never reads it.
    ///
    /// A floor is kept under its <see cref="FloorKey"/>: the seed, the floor asked for, the exit room the floor before ended in, and
    /// the setting, which is everything else that changes what <c>LevelGenerator.Generate</c> gives: every field of the values object,
    /// read whole by reflection down to the last number of its tables (<see cref="Setting"/>), and every static field of the rules'
    /// own types that is not a constant nor read-only (the switches such as <see cref="LevelValues.TimedChests"/>, the tests' seams
    /// such as <c>LevelGenerator.PassagesWarmUpForTests</c> and the template library's override; <see cref="Statics"/>). Two settings
    /// are the same only when their whole texts are the same, so no two settings share a floor by a hash's chance. The stages'
    /// warm-ups are never asked through here: a test that throws draws away generates its own floors, as do the tests that time the
    /// generator. A batch is generated only while the calling test waits (nothing runs behind a test's back while it changes a
    /// switch), each floor on a worker that sees the same statics as the caller (a [ThreadStatic] seam set by the test, which a worker
    /// would not see, leaves the batch to the caller's own thread); a floor whose generating throws is not kept, so the sweep's own read
    /// generates it again and fails with the generator's own words at its own place in the walk.
    ///
    /// The floors handed out are the cache's own objects: a test that reads one may not change it. Each floor's whole contents (every
    /// field of it and of all it holds, by reflection, but for the rooms' lazily kept working values) are folded into a number when it is
    /// kept and folded again each time it is handed out once more: a floor some test changed fails the reading test loudly
    /// (<see cref="ChangedMessage"/>), never quietly. The tests that change their floors generate their own, as before.
    /// <see cref="Generate"/> keeps a floor for the run (for the walks that later tests read again: the pins' two thousand floors and
    /// hundred runs, the defaults' thousand seeds a floor); <see cref="Once"/> hands a floor out and lets it go (for a sweep's own seeds,
    /// which no other test walks).
    ///
    /// Memory: a floor of version 25 holds about 26 kB in the .NET run (measured by work order 155 on 500 floors of the defaults); the cache
    /// holds at most <see cref="MostFloors"/> (about 1 GB at that size; the machine has 32 GB and two Unity runs share it); past that the
    /// floors read longest ago are dropped first and generated again if they are asked for again: only time, never a wrong floor. It is
    /// emptied when the run's last fixture of this namespace has finished (<see cref="SharedFloorsRunEnd"/>).
    /// </summary>
    public static class SharedFloors
    {
        /// <summary>The most floors held at once (see the class's summary): about 1 GB at 26 kB a floor.</summary>
        public const int MostFloors = 40000;

        /// <summary>
        /// The threads a batch of floors is generated on (work order 155): 6, a quarter of the machine's 24 logical processors, so two
        /// full Unity runs at once take half of it and leave the rest to the other jobs. 1: every floor on the test's own thread, one at
        /// a time, as before the order (the floors are the same either way: the generator draws everything from the seed).
        /// </summary>
        public static int Threads = 6;

        /// <summary>The most floors one guess generates ahead of a sweep (eight times <see cref="Threads"/>): what a sweep's end can cost in floors it never reads.</summary>
        public static int MostAhead => Math.Max(1, Threads) * 8;

        /// <summary>A floor's key: the seed, the floor asked for, the exit room the floor before ended in (null for none), the setting's number.</summary>
        public readonly struct FloorKey : IEquatable<FloorKey>
        {
            public readonly ulong Seed;
            public readonly int Floor;
            public readonly string ExitBefore;
            public readonly int Setting;

            public FloorKey(ulong seed, int floor, string exitBefore, int setting)
            {
                Seed = seed;
                Floor = floor;
                ExitBefore = exitBefore;
                Setting = setting;
            }

            public bool Equals(FloorKey o) => Seed == o.Seed && Floor == o.Floor && Setting == o.Setting && string.Equals(ExitBefore, o.ExitBefore, StringComparison.Ordinal);
            public override bool Equals(object obj) => obj is FloorKey o && Equals(o);
            public override int GetHashCode() => (((Seed.GetHashCode() * 397) ^ Floor) * 397 ^ Setting) * 397 ^ (ExitBefore == null ? 0 : StringComparer.Ordinal.GetHashCode(ExitBefore));
        }

        sealed class Held
        {
            public LevelLayout Floor;
            public ulong Print;      // its whole contents folded when it was kept
            public bool HandedOut;   // a test has read it (from then on each reading folds it again)
            public LinkedListNode<FloorKey> Node; // its place in the order of reading, the longest ago first
        }

        static readonly Dictionary<FloorKey, Held> s_floors = new Dictionary<FloorKey, Held>();
        static readonly LinkedList<FloorKey> s_order = new LinkedList<FloorKey>();
        static readonly Dictionary<string, int> s_settings = new Dictionary<string, int>(StringComparer.Ordinal);
        static readonly object s_lock = new object();
        static FieldInfo[] s_statics;

        /// <summary>The run's counts so far: floors generated (on the test's thread and in batches), floors read from the set, floors dropped for the ceiling, the most held at once.</summary>
        public static int Generated { get; private set; }
        public static int InBatches { get; private set; }
        public static int ReadFromTheSet { get; private set; }
        public static int Dropped { get; private set; }
        public static int GuessedUnread { get; private set; }
        public static int MostHeld { get; private set; }
        public static long MostBytes { get; private set; }

        // The test now running: the floors and rooms handed to it (SharedFloorsCount prints them once at its end).
        internal static int s_testFloors, s_testRooms, s_testGenerated;

        /// <summary>The words of the failure when a floor of the set was changed by a test that read it.</summary>
        public const string ChangedMessage = "A floor of SharedFloors was changed by a test that read it: that test must generate its own floors";

        /// <summary>
        /// The floor <c>LevelGenerator.Generate(seed, floor, values, exitBefore: exitBefore)</c> gives, with the rules' switches as they
        /// stand now, kept for the run: the set's floor when it holds one with the same key, else a new one. Read only.
        /// </summary>
        public static LevelLayout Generate(ulong seed, int floor, LevelValues values = null, string exitBefore = null) => Get(seed, floor, values, exitBefore, true);

        /// <summary>As <see cref="Generate"/>, but the floor is let go once handed out: for a sweep's own seeds, which no later test reads again.</summary>
        public static LevelLayout Once(ulong seed, int floor, LevelValues values = null, string exitBefore = null) => Get(seed, floor, values, exitBefore, false);

        static LevelLayout Get(ulong seed, int floor, LevelValues values, string exitBefore, bool keep)
        {
            values = values ?? new LevelValues();
            int setting = SettingOf(values);
            var key = new FloorKey(seed, floor, exitBefore, setting);
            LevelLayout level = Take(key, keep, out bool fromSet);
            if (level == null)
            {
                Guess(key, values, keep);
                level = Take(key, keep, out fromSet);
            }
            if (level == null)
            {
                level = LevelGenerator.Generate(seed, floor, values, exitBefore: exitBefore);
                lock (s_lock) Generated++;
                if (keep) Keep(key, level, PrintOf(level), true);
            }
            Interlocked.Increment(ref s_testFloors);
            Interlocked.Add(ref s_testRooms, level.Rooms.Count);
            if (!fromSet) Interlocked.Increment(ref s_testGenerated);
            return level;
        }

        /// <summary>The set's floor of this key, folded again if a test read it before, and let go unless it is kept; null if the set holds none.</summary>
        static LevelLayout Take(FloorKey key, bool keep, out bool fromSet)
        {
            fromSet = false;
            Held held;
            bool check;
            lock (s_lock)
            {
                if (!s_floors.TryGetValue(key, out held)) return null;
                s_order.Remove(held.Node);
                if (keep) s_order.AddLast(held.Node);
                else s_floors.Remove(key);
                check = held.HandedOut;
                held.HandedOut = true;
                ReadFromTheSet++;
            }
            if (check && PrintOf(held.Floor) != held.Print)
                throw new InvalidOperationException(ChangedMessage + " (seed " + LevelSeed.ToText(key.Seed) + " floor " + key.Floor + ")");
            fromSet = true;
            return held.Floor;
        }

        static void Keep(FloorKey key, LevelLayout level, ulong print, bool handedOut)
        {
            lock (s_lock)
            {
                if (s_floors.ContainsKey(key)) return;
                while (s_floors.Count >= MostFloors)
                {
                    FloorKey oldest = s_order.First.Value;
                    s_order.RemoveFirst();
                    if (!s_floors[oldest].HandedOut) GuessedUnread++;
                    s_floors.Remove(oldest);
                    Dropped++;
                }
                s_floors[key] = new Held { Floor = level, Print = print, HandedOut = handedOut, Node = s_order.AddLast(key) };
                if (s_floors.Count > MostHeld)
                {
                    MostHeld = s_floors.Count;
                    if (MostHeld % 1000 == 0) MostBytes = Math.Max(MostBytes, GC.GetTotalMemory(false));
                }
            }
        }

        // ---------------- Batches ----------------

        /// <summary>
        /// Generates, a batch at a time on <see cref="Threads"/> threads, every floor of the list the set does not hold yet, and keeps
        /// them for the sweep that is about to read them (with <see cref="Generate"/> or <see cref="Once"/>, in its own order). Each
        /// worker draws the same floor the caller's thread would (the same seed, values and switches); a floor whose generating throws is
        /// left out. Returns only when every floor of the batch is made.
        /// </summary>
        public static void Prefetch(LevelValues values, IEnumerable<(ulong Seed, int Floor)> floors)
        {
            values = values ?? new LevelValues();
            int setting = SettingOf(values);
            Batch(values, floors.Select(f => new FloorKey(f.Seed, f.Floor, null, setting)).ToList(), null);
        }

        /// <summary>
        /// As <see cref="Prefetch"/> for runs: each run seed's floors 1 to <paramref name="floors"/>, each told the exit room the floor
        /// before ended in (<c>RunSeeds.FloorSeed</c>, <see cref="WayDowns.Shown"/>), a run on one worker, the runs side by side.
        /// </summary>
        public static void PrefetchRuns(LevelValues values, IEnumerable<ulong> runSeeds, int floors)
        {
            values = values ?? new LevelValues();
            int setting = SettingOf(values);
            Batch(values, runSeeds.Where(r => !HoldsRun(r, floors, setting)).Select(r => new FloorKey(r, floors, null, setting)).ToList(), floors);
        }

        /// <summary>The set holds every floor of the run already (walked through the floors it holds, each telling the next its exit room).</summary>
        static bool HoldsRun(ulong runSeed, int floors, int setting)
        {
            string before = null;
            lock (s_lock)
                for (int floor = 1; floor <= floors; floor++)
                {
                    if (!s_floors.TryGetValue(new FloorKey(RunSeeds.FloorSeed(runSeed, floor), floor, before, setting), out Held held)) return false;
                    before = WayDowns.Shown(held.Floor);
                }
            return true;
        }

        static void Batch(LevelValues values, List<FloorKey> keys, int? runFloors)
        {
            if (Threads <= 1 || keys.Count == 0) return;
            string statics = Statics();
            lock (s_lock) keys = keys.Distinct().Where(k => runFloors != null || !s_floors.ContainsKey(k)).ToList();
            if (keys.Count == 0) return;
            var made = new List<(FloorKey key, LevelLayout level, ulong print)>[keys.Count];
            bool sameStatics = true;
            Parallel.For(0, keys.Count, new ParallelOptions { MaxDegreeOfParallelism = Threads }, i =>
            {
                if (!Volatile.Read(ref sameStatics)) return;
                if (Statics() != statics) { Volatile.Write(ref sameStatics, false); return; } // a [ThreadStatic] seam the worker does not see
                var list = new List<(FloorKey, LevelLayout, ulong)>();
                FloorKey k = keys[i];
                try
                {
                    if (runFloors == null) list.Add((k, LevelGenerator.Generate(k.Seed, k.Floor, values), 0UL));
                    else
                    {
                        string before = null;
                        for (int floor = 1; floor <= runFloors.Value; floor++)
                        {
                            var fk = new FloorKey(RunSeeds.FloorSeed(k.Seed, floor), floor, before, k.Setting);
                            LevelLayout l = LevelGenerator.Generate(fk.Seed, floor, values, exitBefore: before);
                            list.Add((fk, l, 0UL));
                            before = WayDowns.Shown(l);
                        }
                    }
                }
                catch (Exception)
                {
                    // Not kept: the sweep's own read generates it again and fails there, with the generator's words.
                }
                for (int j = 0; j < list.Count; j++) list[j] = (list[j].Item1, list[j].Item2, PrintOf(list[j].Item2));
                made[i] = list;
            });
            if (!sameStatics) return;
            foreach (var list in made)
            {
                if (list == null) continue;
                foreach ((FloorKey key, LevelLayout level, ulong print) in list)
                {
                    Keep(key, level, print, false);
                    lock (s_lock)
                    {
                        Generated++;
                        InBatches++;
                    }
                }
            }
        }

        // ---------------- The guess ----------------

        sealed class Stream
        {
            public FloorKey Last;
            public long SeedStep;
            public int FloorStep;
            public int Steady;   // misses in a row that kept the same step
            public int Ahead;    // floors the next guess generates
        }

        static readonly Dictionary<int, Stream> s_streams = new Dictionary<int, Stream>();

        /// <summary>
        /// The guess of a sweep's next floors: when a floor the set does not hold is asked for (an exit room before none), and the two
        /// asked for before it of the same setting stepped by the same seed and floor as this one, the floors further along that step
        /// (seed + i·step, floor + i·step, floors 1 to the act's last) are generated as a batch: <see cref="Threads"/> at first, twice as
        /// many each time the step holds, at most <see cref="MostAhead"/>. A sweep that stops costs at most that many floors it never
        /// reads; a guess never changes a floor, only when it is made.
        /// </summary>
        static void Guess(FloorKey key, LevelValues values, bool keep)
        {
            if (Threads <= 1 || key.ExitBefore != null) return;
            var keys = new List<FloorKey>();
            lock (s_lock)
            {
                if (!s_streams.TryGetValue(key.Setting * 2 + (keep ? 1 : 0), out Stream st))
                    s_streams[key.Setting * 2 + (keep ? 1 : 0)] = st = new Stream { Last = key, Ahead = Threads };
                else
                {
                    long seedStep = unchecked((long)(key.Seed - st.Last.Seed));
                    int floorStep = key.Floor - st.Last.Floor;
                    bool same = seedStep == st.SeedStep && floorStep == st.FloorStep && (seedStep != 0 || floorStep != 0) && st.Last.ExitBefore == null;
                    st.Steady = same ? st.Steady + 1 : 0;
                    if (!same) st.Ahead = Threads;
                    st.SeedStep = seedStep;
                    st.FloorStep = floorStep;
                    st.Last = key;
                    if (st.Steady >= 1)
                    {
                        for (int i = 1; i <= st.Ahead; i++)
                        {
                            int floor = key.Floor + i * floorStep;
                            if (floor < 1 || floor > values.FloorsPerAct) break;
                            var k = new FloorKey(unchecked(key.Seed + (ulong)(i * seedStep)), floor, null, key.Setting);
                            if (!s_floors.ContainsKey(k)) keys.Add(k);
                            st.Last = k; // the next miss of the sweep is one step past the guess
                        }
                        st.Ahead = Math.Min(MostAhead, st.Ahead * 2);
                    }
                }
            }
            if (keys.Count == 0) return;
            keys.Insert(0, key);
            Batch(values, keys, null);
        }

        /// <summary>
        /// A run of <paramref name="floors"/> floors of one run seed, each floor told the exit room the floor before ended in, as the
        /// pins' runs (<c>RunSeeds.FloorSeed</c>, <see cref="WayDowns.Shown"/>), kept for the run.
        /// </summary>
        public static List<LevelLayout> Run(ulong runSeed, LevelValues values, int floors)
        {
            var list = new List<LevelLayout>();
            string before = null;
            for (int floor = 1; floor <= floors; floor++)
            {
                LevelLayout l = Generate(RunSeeds.FloorSeed(runSeed, floor), floor, values, before);
                list.Add(l);
                before = WayDowns.Shown(l);
            }
            return list;
        }

        /// <summary>The run's counts: the line the run's end prints.</summary>
        public static string Counts()
        {
            lock (s_lock)
                return string.Format(CultureInfo.InvariantCulture,
                                     "SHAREDFLOORS the run: generated {0} ({1} in batches on {2} threads), read from the set {3}, dropped for the ceiling {4} ({5} of them never read), " +
                                     "held now {6}, the most held {7} (of {8}), settings {9}, the most managed memory seen {10:0} MB",
                                     Generated, InBatches, Threads, ReadFromTheSet, Dropped, GuessedUnread, s_floors.Count, MostHeld, MostFloors, s_settings.Count, MostBytes / 1048576.0);
        }

        /// <summary>Empties the cache (the run's end).</summary>
        public static void Clear()
        {
            lock (s_lock)
            {
                s_floors.Clear();
                s_order.Clear();
                s_streams.Clear();
            }
        }

        // ---------------- The setting ----------------

        /// <summary>The setting's number: the same for two values objects (and switches) only when their whole texts are the same.</summary>
        public static int SettingOf(LevelValues values)
        {
            string text = Setting(values) + "|" + Statics();
            lock (s_lock)
            {
                if (!s_settings.TryGetValue(text, out int id))
                {
                    id = s_settings.Count;
                    s_settings[text] = id;
                }
                return id;
            }
        }

        /// <summary>Every field of the values object, and of every object it holds, by reflection, in a text: numbers to the last bit.</summary>
        public static string Setting(LevelValues values)
        {
            var sb = new StringBuilder(32768);
            Write(sb, values, new Dictionary<object, int>(Same.Instance), false);
            return sb.ToString();
        }

        /// <summary>
        /// Every static field of the rules' own types (the namespace <c>Roguelike.Rules</c>) that is neither a constant nor read-only: the
        /// switches a test may set (the static bools, numbers, words and structs; a [ThreadStatic] one as this thread sees it) and, for a
        /// field that holds an object, which object it holds (a test's seam such as the template library's override). The compiler's own
        /// caches of lambdas are not read: they fill as the rules run and change nothing.
        /// </summary>
        public static string Statics()
        {
            if (s_statics == null)
                s_statics = typeof(LevelGenerator).Assembly.GetTypes()
                    .Where(t => t.Namespace == "Roguelike.Rules" && !t.ContainsGenericParameters)
                    .SelectMany(t => t.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    .Where(f => !f.IsLiteral && !f.IsInitOnly && f.GetCustomAttribute<CompilerGeneratedAttribute>() == null
                                && f.Name.IndexOf('<') < 0 && f.DeclaringType.FullName.IndexOf('<') < 0 // not the compiler's own caches of lambdas
                                && !Memos.Contains(f.DeclaringType.Name + "." + f.Name))
                    .OrderBy(f => f.DeclaringType.FullName, StringComparer.Ordinal).ThenBy(f => f.Name, StringComparer.Ordinal)
                    .ToArray();
            var sb = new StringBuilder(4096);
            foreach (FieldInfo f in s_statics)
            {
                sb.Append(f.DeclaringType.FullName).Append('.').Append(f.Name).Append('=');
                object o = f.GetValue(null);
                if (o == null || IsPlain(o.GetType())) WritePlain(sb, o);
                else if (o.GetType().IsValueType) Write(sb, o, new Dictionary<object, int>(Same.Instance), false); // a struct by its fields
                else sb.Append(o.GetType().FullName).Append('#').Append(RuntimeHelpers.GetHashCode(o).ToString(CultureInfo.InvariantCulture));
                sb.Append(';');
            }
            return sb.ToString();
        }

        /// <summary>
        /// The rules' own memos, read by <see cref="Statics"/> no more: each is the same pure function of its argument, kept for the next
        /// call (the props' places around a wanted one by its radius, the mood's rings round a large prop), and changes no floor.
        /// </summary>
        static readonly HashSet<string> Memos = new HashSet<string> { "PropPlacer.s_offsets", "PropPlacer.s_offsetsFor", "RoomMoodPlan.s_rings" };

        /// <summary>The static fields <see cref="Statics"/> reads, by name.</summary>
        public static IEnumerable<string> StaticNames()
        {
            Statics();
            return s_statics.Select(f => f.DeclaringType.FullName + "." + f.Name);
        }

        // ---------------- A floor's contents ----------------

        /// <summary>
        /// A text of everything a floor holds: every field of the layout and of all it holds, by reflection, but for the rooms' lazily
        /// kept working values (<see cref="IsWorking"/>), which the rules fill when asked and which change nothing a test reads.
        /// </summary>
        public static string Contents(LevelLayout level)
        {
            var sb = new StringBuilder(1 << 17);
            Write(sb, level, new Dictionary<object, int>(Same.Instance), true);
            return sb.ToString();
        }

        /// <summary>The contents folded into a number (FNV-1a over the text).</summary>
        public static ulong PrintOf(LevelLayout level)
        {
            string text = Contents(level);
            ulong h = 14695981039346656037UL;
            foreach (char c in text)
            {
                h ^= c;
                h *= 1099511628211UL;
            }
            return h;
        }

        /// <summary>A room's working values, kept lazily and dropped when its cells change: its solid cells' list and its carved faces.</summary>
        static bool IsWorking(FieldInfo f) => f.DeclaringType == typeof(LevelRoom) && (f.Name == "_solidList" || f.Name == "ShapeCache");

        // ---------------- The writer ----------------

        static bool IsPlain(Type t) => t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal);

        static void WritePlain(StringBuilder sb, object o)
        {
            switch (o)
            {
                case null: sb.Append('~'); return;
                case float f: sb.Append('f').Append(BitConverter.ToInt32(BitConverter.GetBytes(f), 0).ToString(CultureInfo.InvariantCulture)); return;
                case double d: sb.Append('d').Append(BitConverter.DoubleToInt64Bits(d).ToString(CultureInfo.InvariantCulture)); return;
                case string s: sb.Append('"').Append(s.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(s).Append('"'); return;
                case IFormattable x: sb.Append(x.ToString(null, CultureInfo.InvariantCulture)); return;
                default: sb.Append(o); return;
            }
        }

        static string TextOf(object o, bool contents)
        {
            var k = new StringBuilder();
            Write(k, o, new Dictionary<object, int>(Same.Instance), contents);
            return k.ToString();
        }

        static void Write(StringBuilder sb, object o, Dictionary<object, int> seen, bool contents)
        {
            if (o == null) { sb.Append('~'); return; }
            Type t = o.GetType();
            if (IsPlain(t)) { WritePlain(sb, o); return; }
            if (o is Type type) { sb.Append("type:").Append(type.FullName); return; }
            if (!t.IsValueType)
            {
                if (seen.TryGetValue(o, out int at)) { sb.Append('@').Append(at.ToString(CultureInfo.InvariantCulture)); return; }
                seen[o] = seen.Count;
            }
            if (o is Delegate del)
            {
                sb.Append("delegate[");
                foreach (Delegate one in del.GetInvocationList())
                {
                    sb.Append(one.Method.DeclaringType?.FullName).Append("::").Append(one.Method.Name).Append('(');
                    Write(sb, one.Target, seen, contents);
                    sb.Append(')');
                }
                sb.Append(']');
                return;
            }
            if (o is IDictionary dict)
            {
                // A dictionary's entries in the order of their keys' texts, so that two equal dictionaries filled in another order read the same.
                var entries = new List<KeyValuePair<string, object>>();
                foreach (DictionaryEntry e in dict) entries.Add(new KeyValuePair<string, object>(TextOf(e.Key, contents), e.Value));
                sb.Append(t.Name).Append('{');
                foreach (var e in entries.OrderBy(x => x.Key, StringComparer.Ordinal))
                {
                    sb.Append(e.Key).Append("=>");
                    Write(sb, e.Value, seen, contents);
                    sb.Append(',');
                }
                sb.Append('}');
                return;
            }
            bool set = t.IsGenericType && t.GetGenericTypeDefinition() == typeof(HashSet<>);
            if (o is IEnumerable list && (t.IsArray || o is IList || set))
            {
                sb.Append(t.Name).Append('[');
                if (set)
                    foreach (string item in list.Cast<object>().Select(x => TextOf(x, contents)).OrderBy(x => x, StringComparer.Ordinal)) sb.Append(item).Append(',');
                else
                    foreach (object item in list)
                    {
                        Write(sb, item, seen, contents);
                        sb.Append(',');
                    }
                sb.Append(']');
                return;
            }
            sb.Append(t.FullName).Append('{');
            for (Type at = t; at != null && at != typeof(object) && at != typeof(ValueType); at = at.BaseType)
                foreach (FieldInfo f in FieldsOf(at))
                {
                    if (contents && IsWorking(f)) continue;
                    sb.Append(f.Name).Append('=');
                    Write(sb, f.GetValue(o), seen, contents);
                    sb.Append(';');
                }
            sb.Append('}');
        }

        static readonly Dictionary<Type, FieldInfo[]> s_fields = new Dictionary<Type, FieldInfo[]>();

        static FieldInfo[] FieldsOf(Type t)
        {
            lock (s_fields)
            {
                if (!s_fields.TryGetValue(t, out FieldInfo[] fields))
                {
                    fields = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                              .OrderBy(f => f.Name, StringComparer.Ordinal).ToArray();
                    s_fields[t] = fields;
                }
                return fields;
            }
        }

        sealed class Same : IEqualityComparer<object>
        {
            public static readonly Same Instance = new Same();
            public new bool Equals(object a, object b) => ReferenceEquals(a, b);
            public int GetHashCode(object o) => RuntimeHelpers.GetHashCode(o);
        }
    }

    /// <summary>
    /// Prints once, at the end of each test that read floors from <see cref="SharedFloors"/>, how many floors and rooms it examined
    /// (the proof of work order 155 that a reworked test walks exactly the floors it walked before), and how many of them came from the
    /// run's set. Put on the whole assembly; a test that never asks the cache prints nothing.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class SharedFloorsCountAttribute : Attribute, ITestAction
    {
        public ActionTargets Targets => ActionTargets.Test;

        public void BeforeTest(ITest test)
        {
            SharedFloors.s_testFloors = 0;
            SharedFloors.s_testRooms = 0;
            SharedFloors.s_testGenerated = 0;
        }

        public void AfterTest(ITest test)
        {
            if (SharedFloors.s_testFloors == 0) return;
            TestContext.WriteLine(string.Format(CultureInfo.InvariantCulture, "SHAREDFLOORS {0}: examined {1} floors, {2} rooms; {3} of them generated on its own thread, {4} read from the run's set (its own batches and earlier tests')",
                                                test.FullName, SharedFloors.s_testFloors, SharedFloors.s_testRooms, SharedFloors.s_testGenerated,
                                                SharedFloors.s_testFloors - SharedFloors.s_testGenerated));
        }
    }

    /// <summary>Empties <see cref="SharedFloors"/> when the run's last fixture of this namespace has finished, and writes its counts once.</summary>
    [SetUpFixture]
    public sealed class SharedFloorsRunEnd
    {
        [OneTimeTearDown]
        public void RunEnd()
        {
            if (SharedFloors.Generated > 0) TestContext.Progress.WriteLine(SharedFloors.Counts());
            SharedFloors.Clear();
        }
    }
}
