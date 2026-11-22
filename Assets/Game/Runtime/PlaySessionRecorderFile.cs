using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Roguelike
{
    /// <summary>
    /// The log file of <see cref="PlaySessionRecorder"/>: opened, its first line with the setup and every value, and the commit
    /// read from git (work order 081 moved them here from PlaySessionRecorder.cs, code unchanged).
    /// </summary>
    public sealed partial class PlaySessionRecorder
    {
        // ---------------- The file ----------------

        void OpenLog(PlayerCharacter player)
        {
            Directory.CreateDirectory(Folder);
            string stem = _startedAt.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
            string path = Path.Combine(Folder, stem + ".jsonl");
            for (int n = 2; File.Exists(path) || File.Exists(SessionSummary.SummaryPathFor(path)); n++)
                path = Path.Combine(Folder, stem + "_" + n + ".jsonl");
            var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
            _writer = new StreamWriter(stream, new UTF8Encoding(false), 16 * 1024);
            LogPath = path;
            WriteStart(player);
            Flush(); // the start is on disk at once
        }

        void WriteStart(PlayerCharacter player)
        {
            var start = new SessionEvent(Now(), SessionEventTypes.SessionStart);
            start.AddExtra("date", _startedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            string commit = "", branch = "";
            if (Application.isEditor) ReadGitHead(Path.GetDirectoryName(Application.dataPath), out commit, out branch);
            start.AddExtra("commit", commit);
            start.AddExtra("branch", branch);
            start.AddExtra("unity", Application.unityVersion);
            start.AddExtra("scene", player.gameObject.scene.name);
            start.AddExtra("editor", Application.isEditor ? "true" : "false");
            AddFields(start, "feel.", player.Feel);
            AddFields(start, "identity.", player.Identity);
            AddFields(start, "kit.", player.Kit);
            EnemyBrute enemy = null;
            foreach (Enemy e in _enemies)
                if (e is EnemyBrute brute) { enemy = brute; break; }
            if (enemy != null) AddEnemyFields(start, "enemy.", enemy.Values);
            // The other kinds (work order 008), as their values start in code.
            var swarmer = new SwarmerValues();
            AddFields(start, "swarmer.", swarmer);
            AddEnemyFields(start, "swarmer.", swarmer.Enemy);
            var shooter = new ShooterValues();
            AddFields(start, "shooter.", shooter);
            AddEnemyFields(start, "shooter.", shooter.Enemy);
            var guard = new GuardValues();
            AddFields(start, "guard.", guard);
            AddEnemyFields(start, "guard.", guard.Enemy);
            AddFields(start, "guard.stagger.", guard.Stagger);
            start.AddExtra("things_that_fly", ThingsThatFly.On ? "true" : "false"); // work order 036: the switch as it stood
            Add(start);

            IReadOnlyList<SkillDefinition> skills = player.Skills;
            for (int slot = 0; slot < skills.Count; slot++)
            {
                SkillDefinition s = skills[slot];
                if (s == null) continue;
                var e = new SessionEvent(Now(), SessionEventTypes.Skill, s.Name)
                {
                    Slot = slot, Input = s.Input.ToString(), Cooldown = s.Cooldown, Damage = s.Damage,
                };
                e.AddExtra("startup", Text(s.Startup));
                e.AddExtra("recovery", Text(s.Recovery));
                e.AddExtra("hit_count", s.HitCount.ToString(CultureInfo.InvariantCulture));
                if (s.Input == SkillInput.Holding || s.Input == SkillInput.Charge) e.AddExtra("hold_max", Text(s.HoldMax));
                if (s.Input == SkillInput.Holding) e.AddExtra("tick_interval", Text(s.TickInterval));
                if (s.Input == SkillInput.Charge) e.AddExtra("charge_time", Text(s.ChargeTime));
                // The kit (decision 0005).
                e.AddExtra("equipped", player.IsEquipped(slot) ? "true" : "false");
                // The key it sits on (work order 015: the place in the list is which skill it is, not its key).
                int key = player.KeyOfSkill(slot);
                InputBindings bindings = player.GetComponent<PlayerInputDriver>() is PlayerInputDriver driver ? driver.ActiveBindings : null;
                e.AddExtra("key", key < 0 ? "none" : bindings != null ? bindings.SkillLabel(key) : key.ToString(CultureInfo.InvariantCulture));
                e.AddExtra("lock", s.Lock.ToString());
                if (s.MarksAdded > 0) e.AddExtra("marks_added", s.MarksAdded.ToString(CultureInfo.InvariantCulture));
                if (s.MarkDamageBonus > 0f) e.AddExtra("mark_bonus", Text(s.MarkDamageBonus));
                if (s.RecastCount > 1) e.AddExtra("recast_count", s.RecastCount.ToString(CultureInfo.InvariantCulture));
                // Amended 2026-11-19: a chain may open with a move that fires nothing (Backstep's hop: its first press
                // is a skill start and end with no shot), and its clean press may spend the marks instead of a clean tap.
                if (s.RecastCount > 1 && s.OpeningMove) e.AddExtra("opening_move", "true");
                if (s.RecastCount > 1 && s.CleanTapMarks > 0) e.AddExtra("clean_tap_marks", s.CleanTapMarks.ToString(CultureInfo.InvariantCulture));
                if (s.RecastCount > 1 && s.CleanMarkDamageBonus > 0f) e.AddExtra("clean_mark_bonus", Text(s.CleanMarkDamageBonus));
                Add(e);
            }
        }

        static void AddEnemyFields(SessionEvent e, string prefix, EnemyValues values)
        {
            AddFields(e, prefix, values);
            if (values?.Attacks == null) return;
            foreach (EnemyAttackDefinition attack in values.Attacks)
                if (attack != null) AddFields(e, prefix + attack.Name + ".", attack);
        }

        /// <summary>Every public number, switch and word of a settings object, under a prefix: the tuning in use.</summary>
        static void AddFields(SessionEvent e, string prefix, object values)
        {
            if (values == null) return;
            foreach (FieldInfo field in values.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object v = field.GetValue(values);
                switch (v)
                {
                    case float f: e.AddExtra(prefix + field.Name, Text(f)); break;
                    case int i: e.AddExtra(prefix + field.Name, i.ToString(CultureInfo.InvariantCulture)); break;
                    case bool b: e.AddExtra(prefix + field.Name, b ? "true" : "false"); break;
                    case string s when field.Name != "Name": e.AddExtra(prefix + field.Name, s); break;
                    case Enum en: e.AddExtra(prefix + field.Name, en.ToString()); break;
                }
            }
        }

        static string Text(float f) => f.ToString("0.####", CultureInfo.InvariantCulture);

        /// <summary>
        /// The commit and branch checked out, read from .git/HEAD in the project folder or a folder above it, without
        /// starting a process. A worktree's .git file and packed refs are followed. Empty when it cannot be read.
        /// </summary>
        public static void ReadGitHead(string projectFolder, out string commit, out string branch)
        {
            commit = "";
            branch = "";
            try
            {
                string gitDir = null;
                for (string dir = projectFolder; dir != null && gitDir == null; dir = Path.GetDirectoryName(dir))
                {
                    string git = Path.Combine(dir, ".git");
                    if (Directory.Exists(git)) gitDir = git;
                    else if (File.Exists(git))
                    {
                        string line = File.ReadAllText(git).Trim();
                        if (!line.StartsWith("gitdir:", StringComparison.Ordinal)) return;
                        gitDir = line.Substring(7).Trim();
                        if (!Path.IsPathRooted(gitDir)) gitDir = Path.GetFullPath(Path.Combine(dir, gitDir));
                    }
                }
                if (gitDir == null) return;
                string head = File.ReadAllText(Path.Combine(gitDir, "HEAD")).Trim();
                if (!head.StartsWith("ref:", StringComparison.Ordinal))
                {
                    commit = head;
                    return;
                }
                string refName = head.Substring(4).Trim();
                branch = refName.StartsWith("refs/heads/", StringComparison.Ordinal) ? refName.Substring(11) : refName;
                string common = gitDir;
                string commonFile = Path.Combine(gitDir, "commondir");
                if (File.Exists(commonFile))
                {
                    common = File.ReadAllText(commonFile).Trim();
                    if (!Path.IsPathRooted(common)) common = Path.GetFullPath(Path.Combine(gitDir, common));
                }
                foreach (string dir in new[] { gitDir, common })
                {
                    string loose = Path.Combine(dir, refName.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(loose))
                    {
                        commit = File.ReadAllText(loose).Trim();
                        return;
                    }
                }
                string packed = Path.Combine(common, "packed-refs");
                if (!File.Exists(packed)) return;
                foreach (string line in File.ReadAllLines(packed))
                {
                    if (line.EndsWith(" " + refName, StringComparison.Ordinal))
                    {
                        commit = line.Substring(0, line.IndexOf(' '));
                        return;
                    }
                }
            }
            catch (Exception)
            {
                // Not knowing the commit is no reason to record nothing.
            }
        }
    }
}
