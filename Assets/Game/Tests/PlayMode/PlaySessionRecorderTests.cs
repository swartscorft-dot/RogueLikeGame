using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// The play session recorder in the built ControlRoom scene: started on purpose into a temporary folder, it logs
    /// what the character is told to do through the intent API and what became of it, and writes a summary whose
    /// numbers match. It never starts by itself in a test run. The temporary folder is removed after each test.
    /// Layout: player at (0, 0), Dummy_A at (4, 3), the enemy at home at (15, -10).
    /// </summary>
    public class PlaySessionRecorderTests
    {
        static readonly Vector3 NearA = new Vector3(2f, 0f, 1f);

        PlayerCharacter _player;
        PlayerInputDriver _driver;
        PlayerHealth _health;
        Damageable _a;
        EnemyBrute _enemy;
        string _folder;
        PlaySessionRecorder _recorder;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;

            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            _driver = Object.FindAnyObjectByType<PlayerInputDriver>();
            _enemy = Object.FindAnyObjectByType<EnemyBrute>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _health = _player.GetComponent<PlayerHealth>();
            _a = GameObject.Find("Dummy_A").GetComponent<Damageable>();
            _driver.InputSource = () => null; // the real mouse and keyboard must not steer a test
            _folder = Path.Combine(Path.GetTempPath(), "PlaySessionRecorderTests-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void CleanUp()
        {
            if (_recorder != null) _recorder.StopRecording();
            _recorder = null;
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        [UnityTest]
        public IEnumerator DoesNotStartByItself_InATestRun()
        {
            yield return null;
            Assert.That(PlaySessionRecorder.Current, Is.Null, "no recorder runs unless a test starts one");
            Assert.That(Object.FindAnyObjectByType<PlaySessionRecorder>(), Is.Null);
            Assert.That(SessionRecordingRule.MayRecordCharacter(PlayerInputDriver.ReadRealDevices), Is.False,
                        "and a recorder that started by itself would not take this character");

            // Nothing has been written to the real folder since this run's Unity started.
            string folder = PlaySessionRecorder.DefaultFolder();
            if (Directory.Exists(folder))
            {
                DateTime since = System.Diagnostics.Process.GetCurrentProcess().StartTime;
                string[] fresh = Directory.GetFiles(folder).Where(f => File.GetLastWriteTime(f) >= since).ToArray();
                Assert.That(fresh, Is.Empty, "files written into " + folder + " during this run");
            }
        }

        [UnityTest]
        public IEnumerator Records_ASkill_ARejectedPress_AShot_ADash_AndTheHits_AndSummarisesThem()
        {
            const int Quick = 0;
            SkillDefinition skill = _player.Skills[Quick];
            _player.Teleport(NearA);
            yield return null;

            _recorder = PlaySessionRecorder.StartRecording(_folder);
            Assert.That(_recorder.Player, Is.SameAs(_player), "it found the character");
            Assert.That(_recorder.LogPath, Is.Not.Null);
            Assert.That(File.Exists(_recorder.LogPath), Is.True, "the log is opened, and its start written, at once");

            Assert.That(_player.PressSkill(Quick, _a.transform.position), Is.EqualTo(IntentResult.Started));
            _player.ReleaseSkill(Quick);
            yield return Until(() => _player.Action == ActionKind.None && _player.SkillShotHits >= 1, 3f);
            Assert.That(_player.PressSkill(Quick, _a.transform.position), Is.EqualTo(IntentResult.Rejected), "on cooldown");

            _player.Shoot(_a.transform.position);
            yield return Until(() => _player.BasicShotsFired >= 1 && _player.Action == ActionKind.None, 3f);
            Assume.That(_player.BasicShotHits, Is.EqualTo(1), "the shot hit A");

            Vector3 away = _player.Position + Vector3.left * 5f;
            Assert.That(_player.DashToward(away), Is.EqualTo(IntentResult.Started));
            yield return Until(() => !_player.IsDashing, 3f);
            Assert.That(_player.DashToward(away), Is.EqualTo(IntentResult.Rejected), "the dash is on cooldown");

            _player.Steer(Vector3.back);
            yield return new WaitForSeconds(0.6f);
            _player.Stop();
            yield return new WaitForSeconds(0.3f);

            string log = _recorder.LogPath;
            _recorder.StopRecording();
            Assert.That(_recorder == null || !_recorder.IsRecording, Is.True);
            Assert.That(PlaySessionRecorder.Current, Is.Null);
            _recorder = null;

            // ---- The log ----
            string[] lines = File.ReadAllLines(log);
            List<SessionEvent> events = lines.Select(SessionEvent.Parse).ToList();
            Assert.That(events, Has.None.Null, "every line is an event with a time and a type");
            Assert.That(events.Select(e => e.T), Is.Ordered, "in order of time");
            Assert.That(events[0].Type, Is.EqualTo(SessionEventTypes.SessionStart));
            Assert.That(events[events.Count - 1].Type, Is.EqualTo(SessionEventTypes.SessionEnd));

            SessionEvent start = events[0];
            Assert.That(start.ExtraValue("date"), Is.Not.Empty);
            Assert.That(start.ExtraValue("unity"), Is.EqualTo(Application.unityVersion));
            Assert.That(start.ExtraValue("scene"), Is.EqualTo("ControlRoom"));
            Assert.That(start.ExtraValue("commit"), Does.Match("^[0-9a-f]{40}$"), "the commit, read from .git/HEAD");
            Assert.That(start.ExtraValue("feel.RunSpeed"), Is.Not.Null, "the feel values in use");
            Assert.That(start.ExtraValue("feel.DashCooldown"), Is.Not.Null);
            List<SessionEvent> kit = events.Where(e => e.Type == SessionEventTypes.Skill).ToList();
            Assert.That(kit.Select(e => e.Name), Is.EqualTo(_player.Skills.Select(s => s.Name)), "the skills, in slot order");
            Assert.That(kit[Quick].Cooldown, Is.EqualTo(skill.Cooldown));

            List<SessionEvent> presses = Of(events, SessionEventTypes.SkillPress);
            Assert.That(presses.Count, Is.EqualTo(2));
            Assert.That(presses[0].Name, Is.EqualTo(skill.Name));
            Assert.That(presses[0].Result, Is.EqualTo(SessionWords.Started));
            Assert.That(presses[1].Result, Is.EqualTo(SessionWords.Rejected));
            Assert.That(presses[1].Reason, Is.EqualTo(SessionWords.Cooldown));
            Assert.That(presses[1].CooldownLeft, Is.GreaterThan(0f).And.LessThanOrEqualTo(skill.Cooldown));
            Assert.That(Of(events, SessionEventTypes.SkillRelease).Count, Is.EqualTo(1));
            Assert.That(Of(events, SessionEventTypes.SkillStart).Select(e => e.Name), Is.EqualTo(new[] { skill.Name }));
            Assert.That(Of(events, SessionEventTypes.SkillFired).Single().Hit, Is.True);
            SessionEvent ended = Of(events, SessionEventTypes.SkillEnd).Single();
            Assert.That(ended.Result, Is.EqualTo(SessionWords.RanOut));

            List<SessionEvent> hits = Of(events, SessionEventTypes.Hit);
            Assert.That(hits.Count, Is.EqualTo(2));
            Assert.That(hits[0].Name, Is.EqualTo(skill.Name));
            Assert.That(hits[0].Damage, Is.EqualTo(skill.Damage));
            Assert.That(hits[0].Target, Is.EqualTo(SessionWords.Dummy));
            Assert.That(hits[0].Empowered, Is.False);
            Assert.That(hits[1].Name, Is.EqualTo(SessionWords.BasicShot));
            Assert.That(hits[1].Damage, Is.EqualTo(Placeholders.BasicShotDamage));

            Assert.That(Of(events, SessionEventTypes.Click).Single().Result, Is.EqualTo(SessionWords.Started));
            Assert.That(Of(events, SessionEventTypes.ShotStart).Count, Is.EqualTo(1));
            Assert.That(Of(events, SessionEventTypes.ShotFired).Single().Hit, Is.True);

            List<SessionEvent> dashes = Of(events, SessionEventTypes.DashPress);
            Assert.That(dashes.Select(e => e.Result), Is.EqualTo(new[] { SessionWords.Started, SessionWords.Rejected }));
            Assert.That(dashes[1].Reason, Is.EqualTo(SessionWords.Cooldown));
            Assert.That(dashes[1].CooldownLeft, Is.GreaterThan(0f));
            Assert.That(Of(events, SessionEventTypes.DashStart).Count, Is.EqualTo(1));

            List<SessionEvent> samples = Of(events, SessionEventTypes.Move);
            Assert.That(samples.Count, Is.GreaterThanOrEqualTo(3), "a few samples a second");
            Assert.That(samples.Any(e => e.Moving == true), Is.True);
            Assert.That(samples.Any(e => e.Moving == false), Is.True);

            // ---- The summary ----
            string summaryPath = SessionSummary.SummaryPathFor(log);
            Assert.That(File.Exists(summaryPath), Is.True, "the summary was written next to the log");
            string summary = File.ReadAllText(summaryPath);
            Debug.Log("Example play session summary:\n" + summary);
            TestContext.Out.WriteLine(summary);

            ScopeStats s = SessionSummary.Compute(events).Session;
            ActionRow q = s.Row(skill.Name);
            Assert.That(q.Pressed, Is.EqualTo(2));
            Assert.That(q.Started, Is.EqualTo(1));
            Assert.That(q.RejectedOnCooldown, Is.EqualTo(1));
            Assert.That(q.Damage, Is.EqualTo(skill.Damage));
            Assert.That(q.Hits, Is.EqualTo(1));
            Assert.That(q.Misses, Is.EqualTo(0));
            ActionRow shot = s.Row(SessionWords.BasicShot);
            Assert.That(shot.Pressed, Is.EqualTo(1));
            Assert.That(shot.Started, Is.EqualTo(1));
            Assert.That(shot.Hits, Is.EqualTo(1));
            ActionRow dash = s.Row(SessionWords.Dash);
            Assert.That(dash.Pressed, Is.EqualTo(2));
            Assert.That(dash.Started, Is.EqualTo(1));
            Assert.That(dash.RejectedOnCooldown, Is.EqualTo(1));
            Assert.That(s.StartedActions, Is.EqualTo(3));
            Assert.That(s.Presses, Is.EqualTo(5));
            Assert.That(s.NeverUsed.Count, Is.EqualTo(_player.Skills.Count - 1));
            Assert.That(s.Pairs.Select(p => p.Key), Is.EqualTo(new[] { skill.Name + " then Basic shot", "Basic shot then Dash" }));
            Assert.That(summary, Does.Contain(skill.Name));

            // Recomputed from the log file alone, the summary is the same.
            Assert.That(SessionSummary.WriteFromLog(log), Is.EqualTo(summaryPath));
            Assert.That(File.ReadAllText(summaryPath), Is.EqualTo(summary));
        }

        [UnityTest]
        public IEnumerator Records_TheEnemysFight_AndAChargedSkill()
        {
            Assume.That(_enemy, Is.Not.Null, "EnemyBrute in scene");
            Damageable body = _enemy.GetComponent<Damageable>();
            int charge = Enumerable.Range(0, _player.Skills.Count).FirstOrDefault(i => _player.Skills[i].Input == SkillInput.Charge);
            Assume.That(_player.Skills[charge].Input, Is.EqualTo(SkillInput.Charge), "the kit has a Charge skill");

            _recorder = PlaySessionRecorder.StartRecording(_folder);
            _player.Teleport(_enemy.Home + Vector3.left * 2f); // inside the Slam's band
            yield return Until(() => _enemy.State == EnemyState.Windup, 3f);
            Assume.That(_enemy.State, Is.EqualTo(EnemyState.Windup), "the enemy winds up an attack");
            string attack = _enemy.Attack.Name;

            Assert.That(_player.PressSkill(charge, _enemy.transform.position), Is.EqualTo(IntentResult.Started));
            yield return new WaitForSeconds(0.3f);
            _player.ReleaseSkill(charge);
            yield return Until(() => _enemy.Strikes >= 1 && _player.Action == ActionKind.None, 4f);
            Assume.That(_enemy.Strikes, Is.GreaterThanOrEqualTo(1), "the enemy struck");

            body.TakeHit(body.CurrentHealth);
            yield return null;
            yield return null;
            Assume.That(_enemy.IsDead, Is.True);

            string log = _recorder.LogPath;
            _recorder.StopRecording();
            _recorder = null;

            List<SessionEvent> events = SessionSummary.ReadLog(log);
            Assert.That(Of(events, SessionEventTypes.EnemyWake).Count, Is.EqualTo(1));
            Assert.That(Of(events, SessionEventTypes.EnemyAttack).First().Name, Is.EqualTo(attack));
            SessionEvent strike = Of(events, SessionEventTypes.EnemyStrike).First();
            Assert.That(strike.Result, Is.EqualTo(SessionWords.Landed), "she stood in the circle");
            Assert.That(Of(events, SessionEventTypes.PlayerDamaged).Sum(e => e.Damage ?? 0), Is.EqualTo(_health.MaxHealth - _health.Current));
            Assert.That(Of(events, SessionEventTypes.EnemyDeath).Count, Is.EqualTo(1));
            SessionEvent ended = Of(events, SessionEventTypes.SkillEnd).Single();
            Assert.That(ended.Held, Is.EqualTo(0.3f).Within(0.05f), "held for as long as the key was down");
            Assert.That(ended.Charge, Is.EqualTo(0.3f / _player.Skills[charge].ChargeTime).Within(0.06f));

            SessionStats stats = SessionSummary.Compute(events);
            Assert.That(stats.Fights, Is.EqualTo(1));
            Assert.That(stats.Kills, Is.EqualTo(1));
            Assert.That(stats.InFights.EnemyAttacks, Is.GreaterThanOrEqualTo(1));
            Assert.That(stats.InFights.StrikesLanded, Is.GreaterThanOrEqualTo(1));
            Assert.That(stats.InFights.DamageTaken, Is.GreaterThan(0));
            Assert.That(stats.Session.Holds.Single(h => h.Name == _player.Skills[charge].Name).Uses, Is.EqualTo(1));
        }

        /// <summary>Decision 0005: marks added and spent, a recast chain, a press on an empty slot and the cooldown cut.</summary>
        [UnityTest]
        public IEnumerator Records_TheKit_MarksAChainAnEmptySlotAndTheCut()
        {
            Damageable a = GameObject.Find("Dummy_A").GetComponent<Damageable>();
            const int Quick = 0, Keg = 1, Scatter = 4, Triple = 7;
            _player.EquipStartSkills();
            _recorder = PlaySessionRecorder.StartRecording(_folder);
            _player.Teleport(new Vector3(2f, 0f, 1f));
            yield return null;

            Assert.That(_player.PressSkill(Keg, a.transform.position), Is.EqualTo(IntentResult.Rejected));
            Assert.That(_player.PressSkill(Quick, a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _player.Action == ActionKind.None && _player.SkillGateRemaining <= 0f, 2f);
            Assert.That(_player.PressSkill(Triple, a.transform.position), Is.EqualTo(IntentResult.Started));
            for (int press = 2; press <= 3; press++)
            {
                yield return Until(() => _player.IsRecastWindowOpen, 2f);
                Assert.That(_player.PressSkill(Triple, a.transform.position), Is.EqualTo(IntentResult.Started));
            }
            yield return Until(() => _player.Action == ActionKind.None && _player.SkillGateRemaining <= 0f, 2f);
            Assume.That(a.MarkCount, Is.EqualTo(4), "one from Quick Shot, three from Triple Tap");
            Assert.That(_player.PressSkill(Scatter, a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _player.Action == ActionKind.None, 2f);
            _player.FillFocus();
            Assert.That(_player.ActivateIdentity(), Is.EqualTo(IntentResult.Started));
            yield return null;

            string log = _recorder.LogPath;
            _recorder.StopRecording();
            _recorder = null;

            List<SessionEvent> events = SessionSummary.ReadLog(log);
            Assert.That(Of(events, SessionEventTypes.SkillPress).Count(e => e.Reason == SessionWords.NotEquipped), Is.EqualTo(1), "the press on the empty slot");
            Assert.That(Of(events, SessionEventTypes.MarkAdd).Last().Marks, Is.EqualTo(4), "the count after each mark");
            SessionEvent spent = Of(events, SessionEventTypes.MarksSpent).Single();
            Assert.That(spent.Marks, Is.EqualTo(4));
            Assert.That(spent.Damage, Is.EqualTo(MarkRules.SpenderDamage(_player.Skills[Scatter].Damage, 4, _player.Skills[Scatter].MarkDamageBonus)));
            Assert.That(Of(events, SessionEventTypes.RecastPress).Select(e => e.Shot), Is.EqualTo(new int?[] { 2, 3 }));
            Assert.That(Of(events, SessionEventTypes.ChainEnd).Single().Result, Is.EqualTo(SessionWords.Complete));
            SessionEvent cut = Of(events, SessionEventTypes.CooldownCut).Single();
            Assert.That(cut.ExtraValue("cut." + _player.Skills[Scatter].Name), Is.Not.Null, "Scatter Blast's cooldown was cut");

            SessionStats stats = SessionSummary.Compute(events);
            Assert.That(stats.Kit.SpenderHits, Is.EqualTo(1));
            Assert.That(stats.Kit.MarksPerSpenderHit, Is.EqualTo(4f));
            Assert.That(stats.Kit.ChainsByLength[3], Is.EqualTo(1));
            Assert.That(stats.Kit.LastPresses, Is.EqualTo(1));
            Assert.That(stats.Kit.FocusFromMarks, Is.EqualTo(4 * _player.Identity.FocusPerMarkSpent).Within(1e-3f));
            Assert.That(stats.Kit.NotEquippedPresses, Is.EqualTo(1));
            Assert.That(stats.Session.NeverUsed, Does.Not.Contain(_player.Skills[Keg].Name), "a skill that was not on the bar is not 'never used'");
            StringAssert.Contains("Marks and recasts", SessionSummary.Format(stats));
        }

        /// <summary>
        /// Amended 2026-11-19 after Kristian played: Backstep's two presses (the hop fires nothing, the second press is the
        /// shot) and its clean spend, in the log and in the summary's lines for each recast skill and each spender.
        /// </summary>
        [UnityTest]
        public IEnumerator Records_BackstepsTwoPresses_AndItsCleanSpend()
        {
            const int Backstep = 6;
            SkillDefinition step = _player.Skills[Backstep];
            KitValues kitValues = _player.Kit;
            Assume.That(step.RecastCount, Is.EqualTo(2));
            _player.EquipStartSkills();
            _recorder = PlaySessionRecorder.StartRecording(_folder);
            _player.Teleport(NearA);
            yield return null;
            _a.AddMarks(5, kitValues.MaxMarks, kitValues.MarkLifetime);
            Assume.That(_a.MarkCount, Is.EqualTo(5));

            Assert.That(_player.PressSkill(Backstep, _a.transform.position), Is.EqualTo(IntentResult.Started), "the hop");
            yield return Until(() => _player.IsRecastWindowOpen, 2f);
            yield return Until(() => _player.RecastSinceOpened >= (_player.RecastCleanFrom + _player.RecastCleanTo) * 0.5f, 2f);
            Assume.That(_player.IsRecastWindowOpen, Is.True);
            Assert.That(_player.PressSkill(Backstep, _a.transform.position), Is.EqualTo(IntentResult.Started), "the clean shot");
            yield return Until(() => _player.Action == ActionKind.None && _player.RecastSlot == -1, 2f);
            Assume.That(_a.MarkCount, Is.EqualTo(0), "the marks were spent");

            string log = _recorder.LogPath;
            _recorder.StopRecording();
            _recorder = null;

            List<SessionEvent> events = SessionSummary.ReadLog(log);
            SessionEvent kit = events.Single(e => e.Type == SessionEventTypes.Skill && e.Name == step.Name);
            Assert.That(kit.ExtraValue("recast_count"), Is.EqualTo("2"));
            Assert.That(kit.ExtraValue("opening_move"), Is.EqualTo("true"));
            Assert.That(kit.ExtraValue("clean_mark_bonus"), Is.EqualTo("0.5"));
            List<SessionEvent> mine(string type) => Of(events, type).Where(e => e.Name == step.Name).ToList();
            Assert.That(mine(SessionEventTypes.SkillPress).Select(e => e.Result), Is.EqualTo(new[] { SessionWords.Started, SessionWords.Started }), "two presses");
            Assert.That(mine(SessionEventTypes.SkillStart).Count, Is.EqualTo(2), "the hop and the shot");
            Assert.That(mine(SessionEventTypes.SkillEnd).Select(e => e.Hit), Is.EqualTo(new bool?[] { false, true }), "the hop fired nothing; the shot did");
            Assert.That(mine(SessionEventTypes.SkillFired).Single().Hit, Is.True, "one shot");
            SessionEvent recast = mine(SessionEventTypes.RecastPress).Single();
            Assert.That(recast.Shot, Is.EqualTo(2));
            Assert.That(recast.Clean, Is.True);
            Assert.That(recast.Focus, Is.EqualTo(_player.Identity.FocusPerCleanTap).Within(1e-4f));
            int want = MarkRules.SpenderDamage(step.Damage, 5, step.CleanMarkDamageBonus);
            SessionEvent spent = mine(SessionEventTypes.MarksSpent).Single();
            Assert.That(spent.Marks, Is.EqualTo(5));
            Assert.That(spent.Damage, Is.EqualTo(want));
            Assert.That(spent.Focus, Is.EqualTo(5 * _player.Identity.FocusPerMarkSpent).Within(1e-4f));
            Assert.That(mine(SessionEventTypes.MarkAdd), Is.Empty, "a clean shot adds no mark");
            SessionEvent chain = mine(SessionEventTypes.ChainEnd).Single();
            Assert.That(chain.Shot, Is.EqualTo(2));
            Assert.That(chain.Result, Is.EqualTo(SessionWords.Complete));

            SessionStats stats = SessionSummary.Compute(events);
            KitSkillRow row = stats.Kit.Skill(step.Name);
            Assert.That(row, Is.Not.Null);
            Assert.That(row.ChainsByLength[2], Is.EqualTo(1));
            Assert.That(row.LastPresses, Is.EqualTo(1));
            Assert.That(row.CleanPresses, Is.EqualTo(1));
            Assert.That(row.SpenderHits, Is.EqualTo(1));
            Assert.That(row.MarksPerSpenderHit, Is.EqualTo(5f));
            Assert.That(row.CleanSpend, Is.True);
            ActionRow actions = stats.Session.Row(step.Name);
            Assert.That(actions.Started, Is.EqualTo(2));
            Assert.That(actions.Hits, Is.EqualTo(1));
            Assert.That(actions.Damage, Is.EqualTo(want));
            string summary = SessionSummary.Format(stats);
            TestContext.Out.WriteLine(summary);
            StringAssert.Contains("  Backstep (clean presses)", summary);
        }

        [UnityTest]
        public IEnumerator AFolderItCannotWrite_StopsTheRecording_SaysSoOnce_AndTheGameGoesOn()
        {
            // A file where the folder should be: the folder cannot be made.
            Directory.CreateDirectory(_folder);
            string blocked = Path.Combine(_folder, "not-a-folder");
            File.WriteAllText(blocked, "");
            LogAssert.Expect(LogType.Warning, new Regex("Play session recorder stopped"));

            _recorder = PlaySessionRecorder.StartRecording(blocked);
            Assert.That(_recorder.HasFailed, Is.True);
            Assert.That(_recorder.IsRecording, Is.False);

            Assert.That(_player.PressSkill(0, _a.transform.position), Is.EqualTo(IntentResult.Started), "the game goes on");
            yield return Until(() => _player.Action == ActionKind.None, 3f);
            _recorder.StopRecording();
            _recorder = null;
        }

        static List<SessionEvent> Of(List<SessionEvent> events, string type) => events.Where(e => e.Type == type).ToList();

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }
    }
}
