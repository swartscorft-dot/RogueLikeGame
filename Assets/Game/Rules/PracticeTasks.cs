using System;
using System.Collections.Generic;
using System.Text;

namespace Roguelike.Rules
{
    /// <summary>
    /// The practice room's tasks (work order 133, 2026-11-25). A friend's note from his first run of the shared build: "Tutorial på
    /// charactern?"; asked what he wants in the first round, Kristian ticked "Øvingsrom med oppgaver" (the practice room leads a new
    /// player through the shot, the dash, each skill and ducking, one step at a time). The numbers are first guesses of the order.
    /// </summary>
    [Serializable]
    public sealed class PracticeTasksValues
    {
        /// <summary>
        /// The switch (work order 133; his tick of 2026-11-25, "Øvingsrom med oppgaver"): on, the practice room leads a new player
        /// through her kit one task at a time, by itself the first time and again on its key. Off, the practice room is as before
        /// the order: nothing is installed, drawn, read or saved.
        /// </summary>
        public bool On = true;

        /// <summary>Metres she must walk by the move keys for the first task (a few steps; a dash does not count).</summary>
        public float MoveMetres = 4f;
        /// <summary>Basic shots that must hit a dummy for the shooting task.</summary>
        public int ShotHits = 3;
        /// <summary>Dashes for the dash task.</summary>
        public int Dashes = 1;
        /// <summary>Marks the spender must take off the dummy in one hit for the marks task (more than one, so the build-up is seen).</summary>
        public int MarksToSpend = 2;
        /// <summary>Seconds a ticked plate stays before the next task comes: a short beat.</summary>
        public float TickBeat = 1.1f;
        /// <summary>Seconds in the practice room, nothing else up, before the first plate comes by itself.</summary>
        public float FirstDelay = 1f;
        /// <summary>Metres from her that the low prop of the duck task is put down.</summary>
        public float PropDistance = 3f;
        /// <summary>Metres the low prop keeps from every dummy.</summary>
        public float PropClearOfDummies = 2.5f;

        /// <summary>
        /// The tasks in the first run (work order 133's follow-up; Kristian, 2026-11-25: "Første start går ditt, men et tilpasset
        /// øvingsrom som ser ut som et run, sånn at neste rom blir første vanlige rom"): on, a player's first run (the saved flag not
        /// set) opens in floor 1's own entrance room dressed as the teaching room (targets that do not fight back, its doors held shut
        /// until the tasks are finished or ended), and the grey practice room no longer starts them by itself. Off, the grey room's
        /// version stands alone, exactly as the first build of the order. Under <see cref="On"/>.
        /// </summary>
        public bool InTheFirstRun = true;
        /// <summary>The targets put in the teaching room: how many, the project's own crypt statue, metres from her start, metres apart.</summary>
        public int Targets = 3;
        public PropModel TargetModel = PropModel.Statue;
        public float TargetDistance = 6f;
        public float TargetsApart = 3f;
        /// <summary>Metres a target or the low prop keeps from the teaching room's walls, and from the room's own props (their colliders).</summary>
        public float FromWalls = 1.6f;
        public float ClearOfProps = 1.2f;

        /// <summary>
        /// The producer's addition (the follow-up's second round, 2026-11-25: "a tutorial whose first target cannot be found fails"),
        /// to be told to the owner as such: while a task waits for a hit on a statue, each statue wears one thin teal ring on the floor
        /// under its feet (the interface's teal, not the duck's orange); gone when no task needs a target. Off, no ring. Under
        /// <see cref="InTheFirstRun"/>.
        /// </summary>
        public bool TargetsWearARing = true;
        /// <summary>
        /// Metres from a brazier (the entrance room's own light) that the statues are stood, near, far, and at the edge of its light (the
        /// last when the near places are under the plate or taken: the first pictures had the third statue in a dark corner).
        /// </summary>
        public float NearTheLight = 2.2f, FarFromTheLight = 3f, EdgeOfTheLight = 4f;
    }

    /// <summary>
    /// Where the teaching room's targets may stand (work order 133's follow-up): points on three rings round her start, the first
    /// ring <see cref="PracticeTasksValues.TargetDistance"/> out, straight ahead first (toward the room's middle) and then turning
    /// either way by 30 degrees; the runtime keeps the first that are on the floor, inside the room and clear.
    /// </summary>
    public static class PracticeTaskPlaces
    {
        /// <summary>Degrees between the turns tried, and metres between the rings.</summary>
        public const float TurnStep = 30f, RingStep = 1.5f;

        public static List<GroundPoint> Candidates(GroundPoint start, GroundPoint ahead, float distance)
        {
            var list = new List<GroundPoint>();
            float len = ahead.Length;
            GroundPoint f = len > 1e-4f ? ahead * (1f / len) : new GroundPoint(0f, 1f);
            float[] rings = { distance, distance + RingStep, Math.Max(1f, distance - RingStep) };
            foreach (float r in rings)
                for (int i = 0; i <= (int)(180f / TurnStep) * 2; i++)
                {
                    float turn = (i + 1) / 2 * TurnStep * (i % 2 == 1 ? 1f : -1f);
                    if (Math.Abs(turn) > 180f) continue;
                    double a = turn * Math.PI / 180.0, c = Math.Cos(a), s = Math.Sin(a);
                    var d = new GroundPoint((float)(f.X * c - f.Z * s), (float)(f.X * s + f.Z * c));
                    list.Add(start + d * r);
                }
            return list;
        }
    }

    /// <summary>What a task asks for (work order 133).</summary>
    public enum PracticeTaskKind
    {
        /// <summary>Walk <see cref="PracticeTasksValues.MoveMetres"/> by the move keys.</summary>
        Move,
        /// <summary>Basic shots that hit a dummy.</summary>
        Shoot,
        /// <summary>A dash that starts.</summary>
        Dash,
        /// <summary>One use of a skill of her kit: a hit on a dummy, or a recast skill's whole chain pressed.</summary>
        UseSkill,
        /// <summary>One clean press of a skill that has one (its chain's last press clean).</summary>
        CleanPress,
        /// <summary>A spender's hit that takes at least <see cref="PracticeTasksValues.MarksToSpend"/> marks off a dummy.</summary>
        SpendMarks,
        /// <summary>Down behind the practice room's low prop (the quick duck), or in cover against it (the cover key without it).</summary>
        Duck,
        /// <summary>Focus spent with its key.</summary>
        Focus,
        /// <summary>The last plate: the start menu opened from the room, or a run started.</summary>
        StartRun,
        /// <summary>The last plate in the first run's teaching room (the follow-up): its door is open, done when she walks out of it.</summary>
        GoOn,
        /// <summary>Work order 143: basic shots that hit a target from <see cref="PrecisionValues.StopShotMetres"/> or more, each fired accurate after a run (with precision in use only).</summary>
        StopAndShoot,
    }

    /// <summary>One task: what it asks, the skill it is about (-1 for none), its keys as shown, its line, how many it needs.</summary>
    public sealed class PracticeTask
    {
        public PracticeTaskKind Kind;
        /// <summary>The skill's slot in her skills (not its key), or -1.</summary>
        public int Slot = -1;
        /// <summary>The skill's presses in a chain (a recast skill), 1 for others.</summary>
        public int Presses = 1;
        /// <summary>The keys shown as keys on the plate, in order.</summary>
        public string[] Keys = Array.Empty<string>();
        /// <summary>The line of plain words.</summary>
        public string Words = "";
        /// <summary>How much makes it done: metres for <see cref="PracticeTaskKind.Move"/>, a count for the rest.</summary>
        public float Need = 1f;
        /// <summary>The progress is shown on the plate ("1 / 3"): shots or dashes counted, more than one needed.</summary>
        public bool ShowsCount => (Kind == PracticeTaskKind.Shoot || Kind == PracticeTaskKind.Dash) && Need > 1f;
    }

    /// <summary>One skill of her kit as the tasks need it: its slot, name and key, and how it plays.</summary>
    public struct PracticeSkill
    {
        public int Slot;
        public string Name;
        public string Key;
        /// <summary>Presses in its chain (<see cref="SkillDefinition.RecastCount"/>); 1: no chain.</summary>
        public int Presses;
        /// <summary>Its chain's last press can be clean (<see cref="RecastChain.HasCleanPress"/>).</summary>
        public bool CleanPress;
        /// <summary>It puts marks on what it hits.</summary>
        public bool BuildsMarks;
        /// <summary>A plain spender: it takes the marks off what it hits for more damage (<see cref="SkillDefinition.MarkDamageBonus"/>).</summary>
        public bool SpendsMarks;
        /// <summary>The first press only moves her (Backstep's hop).</summary>
        public bool OpeningMove;

        public static PracticeSkill Of(int slot, SkillDefinition s, string key) => new PracticeSkill
        {
            Slot = slot, Name = s != null ? s.Name : "", Key = key ?? "",
            Presses = s != null ? Math.Max(1, s.RecastCount) : 1,
            CleanPress = s != null && s.RecastCount > 1 && RecastChain.HasCleanPress(s),
            BuildsMarks = s != null && (s.MarksAdded > 0 || s.CleanTapMarks > 0),
            SpendsMarks = s != null && s.MarkDamageBonus > 0f,
            OpeningMove = s != null && s.OpeningMove,
        };
    }

    /// <summary>How the cover key works as the feel has it now: the quick duck tapped or held, taking cover, or nothing.</summary>
    public enum PracticeDuck { None, Tap, Hold, TakeCover }

    /// <summary>The keys and the ways the tasks name, read from the bindings and the rules at run time (work order 133: never assumed).</summary>
    public struct PracticeKeys
    {
        public string Up, Left, Down, Right;
        public string Shoot;
        /// <summary>The basic shot keeps firing while its button is held (no such rule in the build of work order 133: false).</summary>
        public bool ShootHeld;
        public string Dash;
        public bool DashToCursor;
        public string Cover;
        public PracticeDuck Duck;
        public string Focus;
        /// <summary>The key that opens the start menu from the room (Enter), or that starts a run without it (F5).</summary>
        public string Start;
        /// <summary>The start menu is installed: the key opens it, and START RUN begins a run.</summary>
        public bool StartMenu;
        /// <summary>The tasks run in the first run's teaching room (the follow-up): the last plate says the door leads on.</summary>
        public bool InRun;
        /// <summary>Work order 143: precision is in use, so the teaching ends with "Stop and shoot" before its last plate.</summary>
        public bool StopAndShoot;
    }

    /// <summary>
    /// The task list made from her kit as it is (work order 133: the producer's first list, corrected from the kit): move; shoot the
    /// dummy; dash; each starting skill in key order by its own name and key, used once on the dummy, and for one with a clean press
    /// a clean press after it; marks put on and spent by the spender, when the kit has a builder and a spender; the duck behind the
    /// practice room's low prop, when the cover key does something; focus filled and spent; the last plate, how to start a run.
    /// </summary>
    public static class PracticeTaskList
    {
        public static List<PracticeTask> Make(PracticeTasksValues v, PracticeKeys k, IReadOnlyList<PracticeSkill> kit)
        {
            v = v ?? new PracticeTasksValues();
            kit = kit ?? Array.Empty<PracticeSkill>();
            var list = new List<PracticeTask>();
            list.Add(new PracticeTask
            {
                Kind = PracticeTaskKind.Move, Keys = new[] { k.Up, k.Left, k.Down, k.Right },
                Words = PracticeWords.Move, Need = Math.Max(0.1f, v.MoveMetres),
            });
            list.Add(new PracticeTask
            {
                Kind = PracticeTaskKind.Shoot, Keys = new[] { k.Shoot }, Words = k.ShootHeld ? PracticeWords.ShootHeld : PracticeWords.Shoot,
                Need = Math.Max(1, v.ShotHits),
            });
            list.Add(new PracticeTask
            {
                Kind = PracticeTaskKind.Dash, Keys = new[] { k.Dash }, Words = k.DashToCursor ? PracticeWords.DashToCursor : PracticeWords.DashTheWayYouMove,
                Need = Math.Max(1, v.Dashes),
            });
            PracticeSkill spender = default;
            bool hasSpender = false, hasBuilder = false;
            var builders = new List<string>();
            foreach (PracticeSkill s in kit)
            {
                list.Add(new PracticeTask
                {
                    Kind = PracticeTaskKind.UseSkill, Slot = s.Slot, Presses = s.Presses, Keys = new[] { s.Key }, Words = PracticeWords.Use(s), Need = 1,
                });
                if (s.CleanPress)
                    list.Add(new PracticeTask
                    {
                        Kind = PracticeTaskKind.CleanPress, Slot = s.Slot, Presses = s.Presses, Keys = new[] { s.Key }, Words = PracticeWords.Clean(s), Need = 1,
                    });
                if (s.SpendsMarks && !hasSpender)
                {
                    spender = s;
                    hasSpender = true;
                }
                if (s.BuildsMarks)
                {
                    hasBuilder = true;
                    builders.Add(s.Key);
                }
            }
            if (hasSpender && hasBuilder)
                list.Add(new PracticeTask
                {
                    Kind = PracticeTaskKind.SpendMarks, Slot = spender.Slot, Keys = new[] { spender.Key },
                    Words = PracticeWords.Marks(builders, spender.Name), Need = Math.Max(1, v.MarksToSpend),
                });
            if (k.Duck != PracticeDuck.None && !string.IsNullOrEmpty(k.Cover))
                list.Add(new PracticeTask { Kind = PracticeTaskKind.Duck, Keys = new[] { k.Cover }, Words = PracticeWords.Duck(k.Duck), Need = 1 });
            if (!string.IsNullOrEmpty(k.Focus))
                list.Add(new PracticeTask { Kind = PracticeTaskKind.Focus, Keys = new[] { k.Focus }, Words = PracticeWords.Focus, Need = 1 });
            if (k.StopAndShoot) // work order 143: at the end of the teaching, before its last plate
                list.Add(new PracticeTask
                {
                    Kind = PracticeTaskKind.StopAndShoot, Keys = new[] { k.Shoot }, Words = PracticeWords.StopAndShoot, Need = Math.Max(1, PrecisionValues.Live.StopShotHits),
                });
            if (k.InRun)
            {
                foreach (PracticeTask t in list) t.Words = PracticeWords.InTheRun(t.Words); // the targets there are statues
                list.Add(new PracticeTask { Kind = PracticeTaskKind.GoOn, Words = PracticeWords.GoOn, Need = 1 }); // no key: she walks on
            }
            else
                list.Add(new PracticeTask
                {
                    Kind = PracticeTaskKind.StartRun, Keys = new[] { k.Start }, Words = k.StartMenu ? PracticeWords.StartWithMenu : PracticeWords.StartWithoutMenu, Need = 1,
                });
            return list;
        }
    }

    /// <summary>The words of the plates (work order 133: a first draft in the game's voice, short and plain, no exclamation marks; his to change).</summary>
    public static class PracticeWords
    {
        public const string Head = "PRACTICE";
        public const string Done = "DONE";
        public const string Skip = "skip";
        public const string End = "end";
        /// <summary>The small line in the room while the tasks are not running.</summary>
        public const string Again = "practice tasks";

        public const string Move = "Walk a few steps.";
        public const string Shoot = "Aim with the mouse and click to shoot the dummy.";
        public const string ShootHeld = "Aim with the mouse and hold to keep shooting the dummy.";
        public const string DashToCursor = "Dash toward the cursor.";
        public const string DashTheWayYouMove = "Dash the way you move.";
        public const string Focus = "Spent marks and clean presses fill focus. When it is full, press the key.";
        public const string StartWithMenu = "That is the kit. Press the key for the menu, then START RUN.";
        public const string StartWithoutMenu = "That is the kit. Press the key to start a run.";
        /// <summary>The last plate in the first run's teaching room (the follow-up).</summary>
        public const string GoOn = "That is the kit. The door is open: the run goes on.";
        /// <summary>Work order 143's task: "Stop and shoot".</summary>
        public const string StopAndShoot = "Run, stop, then shoot the dummy from 8 m or more. Only shots fired still count.";
        /// <summary>The targets' words in the grey room, and in the first run's teaching room (the crypt's statues).</summary>
        public const string Dummy = "the dummy", Statue = "a statue";

        /// <summary>A line as the teaching room says it: its targets are statues, not the practice room's dummies.</summary>
        public static string InTheRun(string words) => string.IsNullOrEmpty(words) ? words : words.Replace(Dummy, Statue);

        /// <summary>A skill's own line for its first use, by name; a plain one for a skill not here.</summary>
        public static string Use(PracticeSkill s)
        {
            switch (s.Name)
            {
                case "Quick Shot": return "Quick Shot: a fast shot that marks what it hits. Hit the dummy.";
                case "Scatter Blast": return "Scatter Blast: a shotgun blast at short range. Hit the dummy up close.";
                case "Backstep": return "Backstep: press to hop back, press again to shoot.";
                case "Triple Tap": return "Triple Tap: press three times, one shot a press.";
            }
            if (s.Presses > 1) return s.Name + ": press " + s.Presses + " times, one shot a press.";
            return s.Name + ": hit the dummy with it.";
        }

        /// <summary>The clean press's line: what to watch, the skill's box lighting up and the white bar on it.</summary>
        public static string Clean(PracticeSkill s) =>
            s.OpeningMove ? s.Name + " again: when its box lights up, shoot as the bar on it turns white."
                          : s.Name + " again: when its box lights up, make the last press as the bar turns white.";

        /// <summary>The marks' line: the builders' keys, and the spender by name.</summary>
        public static string Marks(IReadOnlyList<string> builders, string spender)
        {
            var sb = new StringBuilder("Mark the dummy with ");
            for (int i = 0; i < builders.Count; i++)
            {
                if (i > 0) sb.Append(i == builders.Count - 1 ? " or " : ", ");
                sb.Append(builders[i]);
            }
            sb.Append(", then spend the marks up close with ").Append(spender).Append('.');
            return sb.ToString();
        }

        /// <summary>The duck's line, as the cover key works now.</summary>
        public static string Duck(PracticeDuck way)
        {
            switch (way)
            {
                case PracticeDuck.Hold: return "The orange edge marks a low prop. Stand by it and hold the key to duck.";
                case PracticeDuck.Tap: return "The orange edge marks a low prop. Stand by it and tap the key to duck.";
                case PracticeDuck.TakeCover: return "Stand by the low prop and hold the key to take cover.";
                default: return "";
            }
        }

        /// <summary>A key as the bindings name it, made readable: "LeftCtrl" is "Left Ctrl"; "Digit1" is "1"; "LMB" stays.</summary>
        public static string KeyName(string label)
        {
            if (string.IsNullOrEmpty(label)) return "";
            if (label.StartsWith("Digit", StringComparison.Ordinal) && label.Length > 5) return label.Substring(5);
            if (label.StartsWith("Numpad", StringComparison.Ordinal) && label.Length > 6 && char.IsDigit(label[6])) return "Num " + label.Substring(6);
            var sb = new StringBuilder(label.Length + 2);
            for (int i = 0; i < label.Length; i++)
            {
                char c = label[i];
                bool lowerBefore = i > 0 && char.IsLower(label[i - 1]);
                if (i > 0 && char.IsUpper(c) && lowerBefore) sb.Append(' ');
                sb.Append(c);
            }
            return sb.ToString();
        }
    }

    /// <summary>What happened in the game that a task may count (work order 133): never a key going down, always what came of it.</summary>
    public enum PracticeEventKind
    {
        /// <summary>She walked by the move keys: <see cref="PracticeEvent.Metres"/>.</summary>
        Walked,
        /// <summary>A basic shot hit something: <see cref="PracticeEvent.OnDummy"/>.</summary>
        ShotHit,
        /// <summary>A dash started.</summary>
        Dashed,
        /// <summary>A skill's hit: <see cref="PracticeEvent.Slot"/>, <see cref="PracticeEvent.OnDummy"/>.</summary>
        SkillHit,
        /// <summary>A press of a chain fired its shot: Slot, <see cref="PracticeEvent.Shot"/> (which press), <see cref="PracticeEvent.Clean"/>.</summary>
        Recast,
        /// <summary>A spender's hit took marks: Slot, <see cref="PracticeEvent.Marks"/>, OnDummy.</summary>
        MarksSpent,
        /// <summary>She went down in a quick duck: <see cref="PracticeEvent.AtPracticeProp"/>.</summary>
        Ducked,
        /// <summary>She took cover (the cover key without the quick duck): AtPracticeProp.</summary>
        TookCover,
        /// <summary>Focus was spent: its press started.</summary>
        FocusSpent,
        /// <summary>The start menu opened from the room, or a run started.</summary>
        LeftForARun,
        /// <summary>She walked out of the first run's teaching room (the follow-up).</summary>
        LeftTheRoom,
    }

    public struct PracticeEvent
    {
        public PracticeEventKind Kind;
        public int Slot;
        public bool OnDummy;
        public int Shot;
        public bool Clean;
        public int Marks;
        public float Metres;
        public bool AtPracticeProp;
        /// <summary>Work order 143: a basic shot's hit fired accurate after a run (ShotHit; its Metres the distance to the target).</summary>
        public bool StoppedShot;

        public static PracticeEvent Of(PracticeEventKind kind) => new PracticeEvent { Kind = kind, Slot = -1 };
    }

    /// <summary>Each task's done-condition (work order 133): how much an event counts toward it, 0 when it does not.</summary>
    public static class PracticeTaskRules
    {
        /// <summary>
        /// The task is done only by a hit on a target (the follow-up's second round: the statues' rings show then): the basic shots,
        /// a skill used by a hit, the spender's marks. A chain's presses and its clean press count without a hit.
        /// </summary>
        public static bool NeedsATarget(PracticeTask t) =>
            t != null && (t.Kind == PracticeTaskKind.Shoot || t.Kind == PracticeTaskKind.SpendMarks || (t.Kind == PracticeTaskKind.UseSkill && t.Presses <= 1)
                          || t.Kind == PracticeTaskKind.StopAndShoot); // work order 143

        public static float Counts(PracticeTask t, PracticeEvent e)
        {
            if (t == null) return 0f;
            switch (t.Kind)
            {
                case PracticeTaskKind.Move: return e.Kind == PracticeEventKind.Walked && e.Metres > 0f ? e.Metres : 0f;
                case PracticeTaskKind.Shoot: return e.Kind == PracticeEventKind.ShotHit && e.OnDummy ? 1f : 0f;
                case PracticeTaskKind.Dash: return e.Kind == PracticeEventKind.Dashed ? 1f : 0f;
                case PracticeTaskKind.UseSkill:
                    if (e.Slot != t.Slot) return 0f;
                    // A chain is used when its last press has fired; any other skill when it hits a dummy.
                    if (t.Presses > 1) return e.Kind == PracticeEventKind.Recast && e.Shot >= t.Presses ? 1f : 0f;
                    return e.Kind == PracticeEventKind.SkillHit && e.OnDummy ? 1f : 0f;
                case PracticeTaskKind.CleanPress: return e.Kind == PracticeEventKind.Recast && e.Slot == t.Slot && e.Clean ? 1f : 0f;
                case PracticeTaskKind.SpendMarks:
                    // All at once: one hit that takes enough marks off a dummy (several small spends do not add up).
                    return e.Kind == PracticeEventKind.MarksSpent && e.Slot == t.Slot && e.OnDummy && e.Marks >= t.Need ? t.Need : 0f;
                case PracticeTaskKind.Duck: return (e.Kind == PracticeEventKind.Ducked || e.Kind == PracticeEventKind.TookCover) && e.AtPracticeProp ? 1f : 0f;
                case PracticeTaskKind.Focus: return e.Kind == PracticeEventKind.FocusSpent ? 1f : 0f;
                case PracticeTaskKind.StartRun: return e.Kind == PracticeEventKind.LeftForARun ? 1f : 0f;
                case PracticeTaskKind.GoOn: return e.Kind == PracticeEventKind.LeftTheRoom ? 1f : 0f;
                case PracticeTaskKind.StopAndShoot: // work order 143: from afar, fired accurate after a run
                    return e.Kind == PracticeEventKind.ShotHit && e.Slot < 0 && e.OnDummy && e.StoppedShot && e.Metres >= PrecisionValues.Live.StopShotMetres ? 1f : 0f;
                default: return 0f;
            }
        }
    }

    /// <summary>
    /// The tasks under way (work order 133): one at a time; an event that counts toward the task on the plate adds to it, and at its
    /// need it ticks; after the beat (<see cref="PracticeTasksValues.TickBeat"/>) the next one comes. Skip: the next at once. End: no
    /// more. The last plate (how to start a run) has no beat: done, the tasks are finished. Time is passed in, in seconds.
    /// </summary>
    public sealed class PracticeTaskRun
    {
        readonly List<PracticeTask> _tasks;

        public PracticeTaskRun(List<PracticeTask> tasks) => _tasks = tasks ?? new List<PracticeTask>();

        public IReadOnlyList<PracticeTask> Tasks => _tasks;
        /// <summary>The task on the plate, from 0.</summary>
        public int Index { get; private set; }
        /// <summary>What counts toward it so far.</summary>
        public float Progress { get; private set; }
        /// <summary>When it ticked, or NaN while it is not done.</summary>
        public float TickedAt { get; private set; } = float.NaN;
        public bool Ticked => !float.IsNaN(TickedAt);
        /// <summary>Every task done (or skipped) to the end.</summary>
        public bool Finished { get; private set; }
        /// <summary>Ended early with its key.</summary>
        public bool Ended { get; private set; }
        public bool Over => Finished || Ended;
        /// <summary>Tasks ticked by doing them (not skipped).</summary>
        public int Done { get; private set; }
        public int Skipped { get; private set; }

        public PracticeTask Current => !Over && Index >= 0 && Index < _tasks.Count ? _tasks[Index] : null;
        public bool OnLast => Index == _tasks.Count - 1;

        /// <summary>Something happened: counted toward the task on the plate (not during its beat). True when it ticked now.</summary>
        public bool Note(PracticeEvent e, float now)
        {
            PracticeTask t = Current;
            if (t == null || Ticked) return false;
            float c = PracticeTaskRules.Counts(t, e);
            if (c <= 0f) return false;
            Progress = Math.Min(t.Need, Progress + c);
            if (Progress + 1e-4f < t.Need) return false;
            Done++;
            if (OnLast)
            {
                Finished = true;
                return true;
            }
            TickedAt = now;
            return true;
        }

        /// <summary>Every frame: after a tick's beat, the next task.</summary>
        public void Step(float now, float beat)
        {
            if (Over || !Ticked || now - TickedAt < Math.Max(0f, beat)) return;
            Next();
        }

        /// <summary>The skip key: the next task at once (from the last plate, the tasks are finished).</summary>
        public void Skip()
        {
            if (Over) return;
            if (!Ticked) Skipped++;
            if (OnLast) Finished = true;
            else Next();
        }

        /// <summary>The end key: no more tasks.</summary>
        public void End()
        {
            if (!Over) Ended = true;
        }

        void Next()
        {
            Index++;
            Progress = 0f;
            TickedAt = float.NaN;
            if (Index >= _tasks.Count) Finished = true;
        }
    }

    /// <summary>When the tasks run by themselves (work order 133): the first time she is in the practice room, never again once they were finished or ended.</summary>
    public static class PracticeTaskFlag
    {
        /// <summary>
        /// Do the tasks begin by themselves now? The switch on, the saved flag not set (they were never finished or ended), none
        /// running, she in the practice room with nothing else up, for <see cref="PracticeTasksValues.FirstDelay"/>.
        /// </summary>
        public static bool BeginsByItself(PracticeTasksValues v, bool doneBefore, bool running, bool inPracticeRoom, float secondsInRoom) =>
            v != null && v.On && !v.InTheFirstRun && !doneBefore && !running && inPracticeRoom && secondsInRoom >= v.FirstDelay; // the follow-up: with the first run teaching, the grey room waits for H

        /// <summary>
        /// The follow-up: does the first run's teaching begin now? Both switches on, the flag not set, in a run, on floor 1, she in its
        /// entrance room, and this floor not taught already (a floor built again is a new one).
        /// </summary>
        public static bool BeginsInTheRun(PracticeTasksValues v, bool doneBefore, bool inRun, int floor, bool inEntrance, bool taughtThisFloor) =>
            v != null && v.On && v.InTheFirstRun && !doneBefore && inRun && floor == 1 && inEntrance && !taughtThisFloor;

        /// <summary>The teaching room's door opens once the tasks are over (finished or ended) or the last plate, which leads on, is up.</summary>
        public static bool DoorOpens(PracticeTaskRun run) =>
            run == null || run.Over || (run.Current != null && run.Current.Kind == PracticeTaskKind.GoOn);

        /// <summary>The run's clock is held while she is taught: until the door is open and she has walked out of the room.</summary>
        public static bool HoldsTheClock(bool teaching, bool doorOpen, bool inTheRoom) => teaching && (!doorOpen || inTheRoom);

        /// <summary>The producer's decision for the teaching room: the skill a clean-press task is about is ready when the task begins.</summary>
        public static bool ReadyAtStart(PracticeTask t, bool inRun) => inRun && t != null && t.Kind == PracticeTaskKind.CleanPress;

        /// <summary>Is the flag saved now? When a run of the tasks is over: finished or ended.</summary>
        public static bool Saves(PracticeTaskRun run) => run != null && run.Over;

        /// <summary>The flag's stored value read back: done when 1.</summary>
        public static bool Read(int stored) => stored == 1;

        public const int Stored = 1;
    }

    /// <summary>The session log's lines of the first run's teaching (the follow-up): one type, its name what happened.</summary>
    public static class PracticeTaskLog
    {
        public const string Type = "practice_tasks";
        public const string Began = "began", Finished = "finished", Ended = "ended", WalkedOn = "walked on", LeftTheRun = "left the run";
    }
}
