using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 138, part 3, in play on the practice room's dummies (layout: she at (0, 0), Dummy_A at (4, 3)): each of the
    /// five basic-shot upgrades seen working (a pierced dummy hit along her line, a cooldown shorter after hits, the build-up
    /// rising and dropping on a miss, the fourth hit marking, a shot at the cap spending the marks), and each held with its
    /// switch off doing nothing. Her stats are off here (no crit roll), so every number is exact.
    /// </summary>
    public class BasicShotUpgradesPlayTests
    {
        static readonly Vector3 Her = Vector3.zero;
        static readonly Vector3 Behind = new Vector3(6.4f, 1f, 4.8f);   // on her line through Dummy_A, 3 m past it (8 m from her)
        static readonly Vector3 OffTheLine = new Vector3(6.4f, 1f, 1.0f); // as far, 3 m off that line
        static readonly Vector3 Nothing = new Vector3(4f, 0f, -3f);    // open ground: a shot there hits nothing

        PlayerCharacter _player;
        Damageable _a, _b, _c;
        RunModifiers _m;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _a = GameObject.Find("Dummy_A").GetComponent<Damageable>();
            _b = GameObject.Find("Dummy_B").GetComponent<Damageable>();
            _c = GameObject.Find("Dummy_C").GetComponent<Damageable>();
            _b.transform.position = Behind;
            _c.transform.position = OffTheLine;
            _m = new RunModifiers { StatsAndTiers = false };
            _player.Modifiers = _m;
            _player.Teleport(Her);
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            if (_player != null) _player.Modifiers = null;
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        static IEnumerator Until(Func<bool> condition, float seconds)
        {
            float end = Time.time + seconds;
            while (!condition() && Time.time < end) yield return null;
        }

        static int Lost(Damageable d, int before) => before - d.CurrentHealth;

        /// <summary>One basic shot at Dummy_A, waited for: what A and B lost from it.</summary>
        IEnumerator ShootA(List<int> lostA, List<int> lostB = null)
        {
            int hits = _a.HitsTaken, a = _a.CurrentHealth, b = _b.CurrentHealth;
            yield return Until(() => _player.Action == ActionKind.None, 2f);
            _player.Shoot(_a.transform.position);
            yield return Until(() => _a.HitsTaken > hits, 2f);
            Assert.That(_a.HitsTaken, Is.GreaterThan(hits), "the shot hit Dummy_A");
            yield return null;
            lostA?.Add(Lost(_a, a));
            lostB?.Add(Lost(_b, b));
        }

        IEnumerator Miss()
        {
            int shots = _player.BasicShotHits;
            yield return Until(() => _player.Action == ActionKind.None, 2f);
            _player.Shoot(Nothing);
            yield return new WaitForSeconds(0.3f);
            Assert.That(_player.BasicShotHits, Is.EqualTo(shots), "a miss");
        }

        int Base => Placeholders.BasicShotDamage;

        // ---------------- Piercing Round ----------------

        [UnityTest]
        public IEnumerator PiercingRound_TheDummyBehindOnHerLine_TakesItsShare_TheOneOffTheLineNothing()
        {
            _m.BasicShot.Take(BasicShotUpgradeKind.PiercingRound);
            int c = _c.CurrentHealth;
            var a = new List<int>();
            var b = new List<int>();
            yield return ShootA(a, b);
            Assert.That(a[0], Is.EqualTo(Base), "the first takes the shot");
            Assert.That(b[0], Is.EqualTo(BasicShotRules.PierceDamage(Base, 0.5f)), "the one behind, on her line: half (tier I)");
            Assert.That(_c.CurrentHealth, Is.EqualTo(c), "nothing off her line: nothing picks a target");
            Assert.That(_player.PiercedHits, Is.EqualTo(1));
            _m.BasicShot.Take(BasicShotUpgradeKind.PiercingRound);
            yield return ShootA(a, b);
            Assert.That(b[1], Is.EqualTo(BasicShotRules.PierceDamage(Base, 0.8f)), "tier II: 80 %");
            _m.BasicShot.Values = new BasicShotUpgradeValues { PiercingRound = false };
            yield return ShootA(a, b);
            Assert.That(b[2], Is.Zero, "switched off: it stops at the first, as before");
        }

        // ---------------- Quickening ----------------

        [UnityTest]
        public IEnumerator Quickening_EachBasicHit_TakesItsSecondsOffARunningCooldown()
        {
            int quick = _player.SlotOf("Quick Shot");
            Assert.That(_player.PressSkill(quick, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _player.Action == ActionKind.None && _player.SkillGateRemaining <= 0f, 3f);
            Assert.That(_player.SkillCooldownRemaining(quick), Is.GreaterThan(1f), "Quick Shot's cooldown runs");

            // Without it: a hit takes nothing off (what Quick Draw might is not held).
            float r0 = _player.SkillCooldownRemaining(quick), t0 = Time.time;
            yield return ShootA(null);
            float without = r0 - _player.SkillCooldownRemaining(quick) - (Time.time - t0);
            Assert.That(without, Is.EqualTo(0f).Within(0.02f), "no cut without it");

            _m.BasicShot.Take(BasicShotUpgradeKind.Quickening);
            r0 = _player.SkillCooldownRemaining(quick);
            t0 = Time.time;
            yield return ShootA(null);
            float with = r0 - _player.SkillCooldownRemaining(quick) - (Time.time - t0);
            Assert.That(with, Is.EqualTo(0.1f).Within(0.02f), "tier I: 0.1 s off a hit");
            Assert.That(_player.QuickenedSeconds, Is.GreaterThan(0.09f));

            _m.BasicShot.Values = new BasicShotUpgradeValues { Quickening = false };
            r0 = _player.SkillCooldownRemaining(quick);
            t0 = Time.time;
            yield return ShootA(null);
            Assert.That(r0 - _player.SkillCooldownRemaining(quick) - (Time.time - t0), Is.EqualTo(0f).Within(0.02f), "switched off: nothing");
        }

        // ---------------- Hot Barrel ----------------

        [UnityTest]
        public IEnumerator HotBarrel_RisesAHitAtATime_OnTheMuzzleFlashToo_AndAMissDropsIt()
        {
            _m.BasicShot.Take(BasicShotUpgradeKind.HotBarrel);
            var a = new List<int>();
            var warmth = new List<float>();
            for (int i = 0; i < 4; i++)
            {
                yield return ShootA(a);
                warmth.Add(_player.LastFlashWarmth);
            }
            Assert.That(a, Is.EqualTo(new[] { Base, Mathf.RoundToInt(Base * 1.03f), Mathf.RoundToInt(Base * 1.06f), Mathf.RoundToInt(Base * 1.09f) }), "+3 % a hit in a row");
            Assert.That(warmth, Is.EqualTo(new[] { 0f, 0.1f, 0.2f, 0.3f }).Within(1e-4f), "the flash warms step by step, a tenth of its most a hit");
            Assert.That(_player.HeatHits, Is.EqualTo(4));
            yield return Miss();
            Assert.That(_player.HeatShare, Is.Zero, "a miss cools the barrel");
            yield return ShootA(a);
            Assert.That(a[4], Is.EqualTo(Base), "back to +0 %");

            _m.BasicShot.Values = new BasicShotUpgradeValues { HotBarrel = false };
            for (int i = 0; i < 3; i++) yield return ShootA(a);
            Assert.That(a.GetRange(5, 3), Is.EqualTo(new[] { Base, Base, Base }), "switched off: no build-up");
            Assert.That(_player.LastFlashWarmth, Is.Zero);
        }

        // ---------------- Marking Rounds ----------------

        [UnityTest]
        public IEnumerator MarkingRounds_TheFourthHitMarks_NotTheFifth()
        {
            _m.BasicShot.Take(BasicShotUpgradeKind.MarkingRounds);
            Assert.That(_player.BasicHitsPerMark, Is.EqualTo(4));
            var marks = new List<int>();
            for (int i = 0; i < 4; i++)
            {
                yield return ShootA(null);
                marks.Add(_a.MarkCount);
            }
            Assert.That(marks, Is.EqualTo(new[] { 0, 0, 0, 1 }), "a mark on the fourth hit");
            _m.BasicShot.Values = new BasicShotUpgradeValues { MarkingRounds = false };
            Assert.That(_player.BasicHitsPerMark, Is.EqualTo(_player.Kit.BasicShotsPerMark), "switched off: the kit's fifth");
        }

        // ---------------- Tap Out ----------------

        [UnityTest]
        public IEnumerator TapOut_AShotAtTheCap_SpendsTheMarks_ForTwentyPercentEach()
        {
            KitValues kit = _player.Kit;
            var spent = new List<PlayerReport>();
            _player.Reported += r => { if (r.Kind == PlayerReportKind.MarksSpent) spent.Add(r); };
            _m.BasicShot.Take(BasicShotUpgradeKind.TapOut);
            _a.AddMarks(kit.MaxMarks, kit.MaxMarks, kit.MarkLifetime);
            var a = new List<int>();
            yield return ShootA(a);
            Assert.That(_a.MarkCount, Is.Zero, "all five spent");
            Assert.That(a[0], Is.EqualTo(MarkRules.SpenderDamage(Base, kit.MaxMarks, 0.2f)), "double damage at five");
            Assert.That(_player.TapOuts, Is.EqualTo(1));
            Assert.That(spent.Count, Is.EqualTo(1));
            Assert.That(spent[0].Slot, Is.EqualTo(-1), "spent by the basic shot");
            Assert.That(spent[0].Marks, Is.EqualTo(kit.MaxMarks));

            _a.AddMarks(kit.MaxMarks - 1, kit.MaxMarks, kit.MarkLifetime);
            yield return ShootA(a);
            Assert.That(a[1], Is.EqualTo(Base), "under the cap at tier I: kept");
            Assert.That(_a.MarkCount, Is.EqualTo(kit.MaxMarks - 1));

            _m.BasicShot.Values = new BasicShotUpgradeValues { TapOut = false };
            _a.AddMarks(1, kit.MaxMarks, kit.MarkLifetime);
            yield return ShootA(a);
            Assert.That(a[2], Is.EqualTo(Base), "switched off: at the cap, nothing spent");
            Assert.That(_a.MarkCount, Is.EqualTo(kit.MaxMarks));
        }

        // ---------------- All five held, every switch off ----------------

        [UnityTest]
        public IEnumerator AllFiveHeld_EverySwitchOff_TheShotIsAsBefore()
        {
            foreach (BasicShotUpgradeKind k in RunBasicShotUpgrades.All)
                for (int t = 0; t < 3; t++) _m.BasicShot.Take(k);
            _m.BasicShot.Values = new BasicShotUpgradeValues { PiercingRound = false, Quickening = false, HotBarrel = false, MarkingRounds = false, TapOut = false,
                                                               ChainKill = false, SixthChamber = false, HuntersCount = false, PassItOn = false }; // work order 141: its four held too
            KitValues kit = _player.Kit;
            _a.AddMarks(kit.MaxMarks, kit.MaxMarks, kit.MarkLifetime);
            var a = new List<int>();
            var b = new List<int>();
            for (int i = 0; i < kit.BasicShotsPerMark; i++) yield return ShootA(a, b);
            foreach (int lost in a) Assert.That(lost, Is.EqualTo(Base), "every shot its own damage");
            foreach (int lost in b) Assert.That(lost, Is.Zero, "nothing pierced");
            Assert.That(_a.MarkCount, Is.EqualTo(kit.MaxMarks), "nothing spent (and the fifth hit's mark is past the cap)");
            Assert.That(_player.PiercedHits + _player.TapOuts, Is.Zero);
            Assert.That(_player.QuickenedSeconds, Is.Zero);
        }
    }
}
