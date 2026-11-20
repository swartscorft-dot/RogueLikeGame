using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using AllocExt = UnityEngine.TestTools.Constraints.ConstraintExtensions;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 042 (Kristian's answers of 2026-11-20): the small bars over the enemies, in the ControlRoom scene with
    /// enemies spawned in the practice room and an elite of a run's floor. A bar shows from the first hit and hides at full
    /// health with no marks; the marks show as pips beside it and the large marks go; a guard's stagger bar sits directly
    /// under its health bar at the same width, and the large stagger bar goes; an elite's is wider; the boss gets none and
    /// keeps its own; with the switch off, today's display; with eighty enemies on a floor nothing is allocated per frame.
    /// </summary>
    public class EnemyBarsTests
    {
        PlayerCharacter _player;
        PlayerHealth _health;
        GeneratedLevels _levels;
        EnemyBars _bars;

        static NUnit.Framework.Constraints.IResolveConstraint NotAllocating => AllocExt.AllocatingGCMemory(Is.Not);

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            _health.Untouchable = true;
            _levels = GeneratedLevels.Ensure();
            _bars = EnemyBars.Ensure();
        }

        [TearDown]
        public void CleanUp()
        {
            if (_health != null) _health.Untouchable = false;
            if (_bars != null) Object.Destroy(_bars.gameObject);
            _bars = null;
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        float Scale => Screen.height / _bars.Values.LayoutHeight;

        /// <summary>One enemy of a kind spawned in the practice room, and she put a few metres from it, so it is on screen.</summary>
        IEnumerator Spawn(EnemyKind kind, List<Enemy> into)
        {
            List<Enemy> made = _levels.SpawnPractice(kind);
            Assume.That(made.Count, Is.GreaterThan(0), "spawned");
            into.AddRange(made);
            yield return null;
            yield return null;
            Near(made[0]);
            yield return null;
            yield return null;
        }

        void Near(Enemy e)
        {
            Vector3 at = e.transform.position + Vector3.back * 3f;
            if (NavMesh.SamplePosition(at, out NavMeshHit hit, 2f, NavMesh.AllAreas)) at = hit.position;
            _player.Teleport(new Vector3(at.x, 0f, at.z));
        }

        EnemyBarView BarOf(Enemy e)
        {
            _bars.Tick();
            Assert.That(_bars.TryGet(e, out EnemyBarView view), Is.True, e.name + " shows a bar");
            return view;
        }

        bool Shows(Enemy e)
        {
            _bars.Tick();
            return _bars.TryGet(e, out _);
        }

        [UnityTest]
        public IEnumerator ABar_ShowsFromTheFirstHit_AndHidesAtFullHealthWithNoMarks()
        {
            var made = new List<Enemy>();
            yield return Spawn(EnemyKind.Shooter, made);
            Enemy e = made[0];
            Assert.That(Shows(e), Is.False, "full health, no marks: none");
            e.Body.TakeHit(10);
            EnemyBarView bar = BarOf(e);
            Assert.That(bar.HealthFill, Is.LessThan(1f).And.GreaterThan(0f), "its health");
            Assert.That(bar.Health.width, Is.EqualTo(EnemyBarRules.Width(EnemyKind.Shooter, false, _bars.Values) * Scale).Within(0.01f));
            Assert.That(bar.Health.height, Is.EqualTo(_bars.Values.Thickness * Scale).Within(0.01f), "thin");
            Assert.That(bar.HasStagger, Is.False, "a shooter has no stagger bar");
            Assert.That(bar.Pips, Is.EqualTo(_player.Kit.MaxMarks), "the cap as empty pips");
            Assert.That(bar.Lit, Is.Zero);

            // Over its head, on screen.
            Vector3 head = Camera.main.WorldToScreenPoint(e.transform.position + Vector3.up * e.ModelHeight);
            Assert.That(bar.Health.center.x, Is.EqualTo(head.x).Within(bar.Health.width * 0.25f), "centred over it");
            Assert.That(bar.Health.yMax, Is.LessThan(Screen.height - head.y), "above its head");

            e.Body.Refill();
            Assert.That(Shows(e), Is.False, "full again with no marks: hidden");
            e.Body.TakeHit(100000);
            yield return null;
            Assert.That(Shows(e), Is.False, "never over the dead");
        }

        [UnityTest]
        public IEnumerator TheMarks_ShowAsPipsBesideTheBar_AndTheLargeMarksGo()
        {
            var made = new List<Enemy>();
            yield return Spawn(EnemyKind.Brute, made);
            Enemy e = made[0];
            int cap = _player.Kit.MaxMarks;
            MarkPips big = MarkPips.For(e.Body, _player.ShotFxMaterial);
            e.Body.AddMarks(2, cap, 6f);
            yield return null;
            EnemyBarView bar = BarOf(e);
            Assert.That(e.Body.CurrentHealth, Is.EqualTo(e.Body.MaxHealth), "at full health");
            Assert.That(bar.Lit, Is.EqualTo(2), "a pip per mark");
            Assert.That(bar.Pips, Is.EqualTo(cap), "and the cap as empty pips");
            Assert.That(bar.AtCap, Is.False);
            Assert.That(bar.LitRow.x, Is.GreaterThan(bar.Health.xMax), "at the bar's end");
            Assert.That(bar.LitRow.height, Is.EqualTo(_bars.Values.Pip * Scale).Within(0.01f), "tiny");
            Assert.That(bar.EmptyRow.x, Is.GreaterThan(bar.LitRow.xMax), "the empty ones after the lit ones");
            Assert.That(big.Visible, Is.False, "the large marks are gone");

            e.Body.AddMarks(cap, cap, 6f);
            yield return null;
            bar = BarOf(e);
            Assert.That(bar.Lit, Is.EqualTo(cap));
            Assert.That(bar.AtCap, Is.True, "red at the cap");
            Assert.That(bar.DrawCalls, Is.EqualTo(3), "back, health, one row of pips");

            _bars.Values.BigMarks = true;
            yield return null;
            Assert.That(big.Visible, Is.True, "the large ones back beside it, behind their switch");
            Assert.That(Shows(e), Is.True);

            _bars.Values.BigMarks = false;
            _bars.Values.On = false;
            yield return null;
            Assert.That(Shows(e), Is.False, "the switch off: no small bar");
            Assert.That(big.Visible, Is.True, "and today's display");
        }

        [UnityTest]
        public IEnumerator AGuard_HasItsStaggerBar_DirectlyUnderTheHealthBar_AtTheSameWidth()
        {
            var made = new List<Enemy>();
            yield return Spawn(EnemyKind.Guard, made);
            Enemy e = made[0];
            StaggerBarView big = e.GetComponentInChildren<StaggerBarView>(true);
            Assert.That(big, Is.Not.Null, "a guard has a stagger bar");
            Assert.That(e.Body.Stagger, Is.Not.Null);
            e.Body.TakeHit(5, 10f);
            yield return null;
            EnemyBarView bar = BarOf(e);
            Assert.That(bar.HasStagger, Is.True);
            Assert.That(bar.StaggerFill, Is.LessThan(1f), "the stagger taken");
            Assert.That(bar.Stagger.width, Is.EqualTo(bar.Health.width).Within(0.01f), "the same width");
            Assert.That(bar.Stagger.x, Is.EqualTo(bar.Health.x).Within(0.01f));
            Assert.That(bar.Stagger.y, Is.EqualTo(bar.Health.yMax + _bars.Values.Edge * Scale).Within(0.01f), "directly under it");
            Assert.That(bar.Back.yMax, Is.GreaterThanOrEqualTo(bar.Stagger.yMax), "one stack, one back");
            Assert.That(bar.Health.width, Is.EqualTo(EnemyBarRules.Width(EnemyKind.Guard, false, _bars.Values) * Scale).Within(0.01f));
            Assert.That(big.Visible, Is.False, "the large stagger bar is gone");

            _bars.Values.On = false;
            yield return null;
            Assert.That(big.Visible, Is.True, "today's, with the switch off");
        }

        [UnityTest]
        public IEnumerator TheBoss_GetsNoSmallBar_AndKeepsItsOwn()
        {
            var made = new List<Enemy>();
            yield return Spawn(EnemyKind.Boss, made);
            Enemy boss = made[0];
            boss.Body.TakeHit(50);
            MarkPips big = MarkPips.For(boss.Body, _player.ShotFxMaterial);
            boss.Body.AddMarks(2, _player.Kit.MaxMarks, 6f);
            yield return null;
            Assert.That(Shows(boss), Is.False, "no small bar over the boss");
            Assert.That(big.Visible, Is.True, "its marks as before");
            Assert.That(boss.GetComponent<BossHealthBar>(), Is.Not.Null, "its bar at the top of the screen");
            StaggerBarView stagger = boss.GetComponentInChildren<StaggerBarView>(true);
            if (stagger != null) Assert.That(stagger.Visible, Is.True, "its own stagger bar");
        }

        [UnityTest]
        public IEnumerator AnElite_HasALittleWiderBar()
        {
            GeneratedLevel level = _levels.StartRun(0x0E117E17UL);
            yield return null;
            Enemy elite = level.Elite;
            Assume.That(elite, Is.Not.Null);
            Near(elite);
            yield return null;
            yield return null;
            elite.Body.TakeHit(10);
            EnemyBarView bar = BarOf(elite);
            Assert.That(bar.Elite, Is.True);
            Assert.That(bar.Health.width, Is.EqualTo(EnemyBarRules.Width(elite.Kind, true, _bars.Values) * Scale).Within(0.01f), "a little wider");
            Assert.That(bar.Health.width, Is.GreaterThan(EnemyBarRules.Width(elite.Kind, false, _bars.Values) * Scale));
            Assert.That(bar.HasStagger, Is.True, "an elite has a stagger bar, under its health");
            Assert.That(bar.DrawCalls, Is.EqualTo(EnemyBarRules.DrawCalls(true, true, 0, bar.Pips, false)), "the gold edge is one more call");
            _levels.BackToPracticeRoom();
        }

        [UnityTest]
        public IEnumerator WithEightyEnemies_NothingIsAllocatedPerFrame()
        {
            var made = new List<Enemy>();
            for (int i = 0; i < 40 && made.Count < 80; i++) made.AddRange(_levels.SpawnPractice(EnemyKind.Swarmer));
            Assume.That(made.Count, Is.GreaterThanOrEqualTo(80), "eighty enemies");
            yield return null;
            yield return null;
            Near(made[0]);
            yield return null;
            int cap = _player.Kit.MaxMarks;
            foreach (Enemy e in made)
            {
                e.Body.TakeHit(1);
                e.Body.AddMarks(2, cap, 60f);
            }
            yield return null;
            _bars.Tick(); // the list grows here, once, if it must
            Assert.That(_bars.Count, Is.GreaterThan(0), "bars on screen");
            Assert.That(() => { for (int i = 0; i < 300; i++) _bars.Tick(); }, NotAllocating, "the bars' frame allocates nothing");
            Debug.Log("Work order 042: " + made.Count + " enemies, " + _bars.Count + " bars on screen, " + _bars.DrawCalls + " draw calls");
            Assert.That(_bars.DrawCalls, Is.LessThanOrEqualTo(_bars.Count * 4), "a few rectangles each");
        }
    }
}
