using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using AllocExt = UnityEngine.TestTools.Constraints.ConstraintExtensions;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 141 in play on the practice room's dummies (Kristian, 2026-11-26, his tick of all four: Chain Kill, Pass It On, Sixth
    /// Chamber, Hunter's Count): each seen working, the pairs the order named (Sixth Chamber's pierce with Piercing Round, a Chain Kill
    /// bounce that kills a marked enemy with Pass It On, Hunter's Count with Chain Kill's bounces), Sixth Chamber's count on the skill
    /// bar, and the four held with their switches off doing nothing. She stands at (0, 0); Dummy_A where the scene puts it, at (4, 3);
    /// the other two dummies are placed by each test, every other target is put away. Her stats are off (no crit roll), so every
    /// number is exact.
    /// </summary>
    public class BasicShotUpgradesMorePlayTests
    {
        PlayerCharacter _player;
        Damageable _a, _b, _c;
        RunModifiers _m;
        Vector3 _along, _aside; // from her toward Dummy_A, and across that line, flat
        readonly List<Damageable> _putAway = new List<Damageable>();
        EnemyBrute _brute;

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
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _a = GameObject.Find("Dummy_A").GetComponent<Damageable>();
            _b = GameObject.Find("Dummy_B").GetComponent<Damageable>();
            _c = GameObject.Find("Dummy_C").GetComponent<Damageable>();
            _putAway.Clear();
            foreach (Damageable d in Damageable.All) if (d != _a && d != _b && d != _c) _putAway.Add(d);
            foreach (Damageable d in _putAway) d.gameObject.SetActive(false);
            _brute = Object.FindAnyObjectByType<EnemyBrute>();
            if (_brute != null) _brute.enabled = false;
            _m = new RunModifiers { StatsAndTiers = false };
            _player.Modifiers = _m;
            _player.Teleport(Vector3.zero);
            Vector3 a = _a.transform.position;
            _along = new Vector3(a.x, 0f, a.z).normalized;
            _aside = new Vector3(-_along.z, 0f, _along.x);
            Away(_b);
            Away(_c);
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            if (_player != null) _player.Modifiers = null;
            foreach (Damageable d in _putAway) if (d != null) d.gameObject.SetActive(true);
            _putAway.Clear();
            if (_brute != null) _brute.enabled = true;
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        int Base => Placeholders.BasicShotDamage;
        KitValues Kit => _player.Kit;

        /// <summary>Puts a dummy <paramref name="along"/> m past Dummy_A on her line and <paramref name="aside"/> m across it.</summary>
        void Place(Damageable d, float along, float aside)
        {
            Vector3 a = _a.transform.position;
            d.transform.position = new Vector3(a.x, a.y, a.z) + _along * along + _aside * aside;
        }

        void Away(Damageable d) => d.transform.position = new Vector3(40f, _a.transform.position.y, -40f);

        static IEnumerator Until(Func<bool> condition, float seconds)
        {
            float end = Time.time + seconds;
            while (!condition() && Time.time < end) yield return null;
        }

        /// <summary>One basic shot at a dummy, waited for until it is hit.</summary>
        IEnumerator ShootAt(Damageable d)
        {
            int hits = d.HitsTaken;
            yield return Until(() => _player.Action == ActionKind.None, 2f);
            _player.Shoot(d.transform.position);
            yield return Until(() => d.HitsTaken > hits, 2f);
            Assert.That(d.HitsTaken, Is.GreaterThan(hits), "the shot hit " + d.name);
            yield return null;
        }

        IEnumerator Miss()
        {
            int fired = _player.BasicShotsFired, hits = _player.BasicShotHits;
            yield return Until(() => _player.Action == ActionKind.None, 2f);
            _player.Shoot(_a.transform.position - _aside * 6f);
            yield return Until(() => _player.BasicShotsFired > fired, 2f);
            yield return null;
            Assert.That(_player.BasicShotHits, Is.EqualTo(hits), "a miss");
        }

        static int Lost(Damageable d, int before) => before - d.CurrentHealth;

        // ---------------- Chain Kill ----------------

        [UnityTest]
        public IEnumerator ChainKill_AKillBouncesToTheNearestBesideIt_ForTheShotsDamage_AndABounceThatKillsBouncesOn()
        {
            _m.BasicShot.Take(BasicShotUpgradeKind.ChainKill);
            _a.SetMaxHealth(Base);     // dies to one shot
            Place(_b, 0f, 3f);         // 3 m beside A, off her line
            _b.SetMaxHealth(Base);     // dies to one bounce
            Place(_c, 0f, 6f);         // 3 m past B, 6 m from A
            int c = _c.CurrentHealth;
            yield return ShootAt(_a);
            Assert.That(_a.IsAlive, Is.False, "her shot killed A");
            Assert.That(_b.IsAlive, Is.False, "the bounce went to B, the nearest beside A, for 100 % of the shot, and killed it");
            Assert.That(Lost(_c, c), Is.EqualTo(Base), "B's death bounced on to C");
            Assert.That(_c.IsAlive, Is.True, "a bounce that does not kill ends the chain");
            Assert.That(_player.ChainBounces, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator ChainKill_NoKillNoBounce_NeverPastItsReach_AndOffNothing()
        {
            _m.BasicShot.Take(BasicShotUpgradeKind.ChainKill);
            Place(_b, 0f, 3f);
            int b = _b.CurrentHealth;
            yield return ShootAt(_a);
            Assert.That(_a.IsAlive, Is.True);
            Assert.That(_b.CurrentHealth, Is.EqualTo(b), "no kill: no bounce");

            Place(_b, 0f, 5f); // past tier I's 4 m
            _a.SetMaxHealth(Base);
            yield return ShootAt(_a);
            Assert.That(_a.IsAlive, Is.False);
            Assert.That(_b.CurrentHealth, Is.EqualTo(b), "5 m away: past tier I's reach");

            _m.BasicShot.Take(BasicShotUpgradeKind.ChainKill); // tier II: 5.5 m
            _a.SetMaxHealth(Base);
            yield return ShootAt(_a);
            Assert.That(Lost(_b, b), Is.EqualTo(Base), "tier II reaches it");

            _m.BasicShot.Values = new BasicShotUpgradeValues { ChainKill = false };
            b = _b.CurrentHealth;
            _a.SetMaxHealth(Base);
            yield return ShootAt(_a);
            Assert.That(_b.CurrentHealth, Is.EqualTo(b), "switched off: a kill bounces nothing");
        }

        // ---------------- Chain Kill with Pass It On ----------------

        [UnityTest]
        public IEnumerator ABounceThatKillsAMarkedEnemy_WithPassItOn_PassesItsMarks()
        {
            _m.BasicShot.Take(BasicShotUpgradeKind.ChainKill);
            _m.BasicShot.Take(BasicShotUpgradeKind.PassItOn);
            _a.SetMaxHealth(Base);
            Place(_b, 0f, 3f);
            _b.SetMaxHealth(Base);
            _b.AddMarks(3, Kit.MaxMarks, Kit.MarkLifetime);
            Place(_c, 0f, 6f); // 3 m past B
            var lost = new List<MarkLoss>();
            void OnLost(Damageable d, int n, MarkLoss why) { if (d == _b) lost.Add(why); }
            Damageable.MarksLost += OnLost;
            try
            {
                yield return ShootAt(_a);
            }
            finally
            {
                Damageable.MarksLost -= OnLost;
            }
            Assert.That(_b.IsAlive, Is.False, "the bounce killed B");
            Assert.That(_player.MarksPassedOn, Is.EqualTo(3), "B's three marks passed on");
            Assert.That(_c.MarkCount, Is.EqualTo(3), "to C, the nearest living beside B");
            Assert.That(lost, Is.Empty, "the log does not count them as lost to the death");
            Assert.That(_player.ChainBounces, Is.EqualTo(2), "and the chain went on from B to C");
        }

        // ---------------- Pass It On ----------------

        [UnityTest]
        public IEnumerator PassItOn_ABasicKillOfAMarkedEnemy_PassesItsMarks_ToTheNearestWithinItsReach_UpToTheCap()
        {
            _m.BasicShot.Take(BasicShotUpgradeKind.PassItOn);
            _a.SetMaxHealth(Base);
            _a.AddMarks(3, Kit.MaxMarks, Kit.MarkLifetime);
            Place(_b, 0f, 4f);
            _b.AddMarks(3, Kit.MaxMarks, Kit.MarkLifetime);
            Place(_c, 0f, -6f); // farther, on the other side
            yield return ShootAt(_a);
            Assert.That(_a.IsAlive, Is.False);
            Assert.That(_b.MarkCount, Is.EqualTo(Kit.MaxMarks), "three on three: up to the cap");
            Assert.That(_b.MarksFallOffIn, Is.GreaterThan(Kit.MarkLifetime - 0.5f), "their lifetime renewed");
            Assert.That(_c.MarkCount, Is.Zero, "only the nearest");
            Assert.That(_player.MarksPassedOn, Is.EqualTo(2), "what landed");

            // Nobody within its 5 m: the marks die with it, as before.
            Place(_b, 0f, 6f);
            _b.SetMaxHealth(_b.MaxHealth); // no marks
            _a.SetMaxHealth(Base);
            _a.AddMarks(2, Kit.MaxMarks, Kit.MarkLifetime);
            int died = 0;
            void OnLost(Damageable d, int n, MarkLoss why) { if (d == _a && why == MarkLoss.Died) died += n; }
            Damageable.MarksLost += OnLost;
            try
            {
                yield return ShootAt(_a);
            }
            finally
            {
                Damageable.MarksLost -= OnLost;
            }
            Assert.That(_b.MarkCount, Is.Zero, "6 m: past its reach");
            Assert.That(died, Is.EqualTo(2), "lost to the death, as without it");

            _m.BasicShot.Values = new BasicShotUpgradeValues { PassItOn = false };
            Place(_b, 0f, 3f);
            _a.SetMaxHealth(Base);
            _a.AddMarks(2, Kit.MaxMarks, Kit.MarkLifetime);
            yield return ShootAt(_a);
            Assert.That(_b.MarkCount, Is.Zero, "switched off: nothing passes");
        }

        // ---------------- Sixth Chamber ----------------

        [UnityTest]
        public IEnumerator SixthChamber_TheSixthShotFired_HitOrMiss_IsTheHeavyRound_DoubleAndThroughOne()
        {
            _m.BasicShot.Take(BasicShotUpgradeKind.SixthChamber);
            Place(_b, 1.5f, 0f); // behind A on her line
            Assert.That(_player.ChamberRounds, Is.EqualTo(6));
            var lostA = new List<int>();
            var lostB = new List<int>();
            var fired = new List<int>();
            for (int shot = 1; shot <= 6; shot++)
            {
                if (shot == 2)
                {
                    yield return Miss(); // a miss counts as a shot fired
                    fired.Add(_player.ChamberFired);
                    continue;
                }
                int a = _a.CurrentHealth, b = _b.CurrentHealth;
                yield return ShootAt(_a);
                lostA.Add(Lost(_a, a));
                lostB.Add(Lost(_b, b));
                fired.Add(_player.ChamberFired);
                if (shot == 5) Assert.That(_player.ChamberNextHeavy, Is.True, "five fired: the next is the heavy round");
            }
            Assert.That(fired, Is.EqualTo(new[] { 1, 2, 3, 4, 5, 0 }), "the count, a miss among them");
            Assert.That(lostA, Is.EqualTo(new[] { Base, Base, Base, Base, 2 * Base }), "the sixth deals double");
            Assert.That(lostB, Is.EqualTo(new[] { 0, 0, 0, 0, Base }), "and goes through to the one behind at half its damage");
            Assert.That(_player.LastShotHeavy, Is.True);
            Assert.That(_player.HeavyRounds, Is.EqualTo(1));

            _m.BasicShot.Values = new BasicShotUpgradeValues { SixthChamber = false };
            Assert.That(_player.ChamberRounds, Is.Zero, "switched off: no count");
            for (int i = 0; i < 6; i++)
            {
                int a = _a.CurrentHealth;
                yield return ShootAt(_a);
                Assert.That(Lost(_a, a), Is.EqualTo(Base), "no heavy round");
            }
        }

        [UnityTest]
        public IEnumerator SixthChamber_SharesItsPierce_WithPiercingRound_NeverAddsIt()
        {
            _m.BasicShot.Take(BasicShotUpgradeKind.SixthChamber);
            _m.BasicShot.Take(BasicShotUpgradeKind.PiercingRound);
            Place(_b, 1.5f, 0f);
            Place(_c, 3f, 0f); // both behind A on her line, within reach
            var b = new List<int>();
            var c = new List<int>();
            for (int i = 0; i < 6; i++)
            {
                int hb = _b.CurrentHealth, hc = _c.CurrentHealth;
                yield return ShootAt(_a);
                b.Add(Lost(_b, hb));
                c.Add(Lost(_c, hc));
            }
            int half = BasicShotRules.PierceDamage(Base, 0.5f);
            Assert.That(b, Is.EqualTo(new[] { half, half, half, half, half, 2 * half }), "Piercing Round I: the next on the line at half; the heavy round's half of double");
            Assert.That(c, Is.EqualTo(new[] { 0, 0, 0, 0, 0, 0 }), "never two: the heavy round's one pierce is Piercing Round's, not one more");

            // Piercing Round III: two more at 80 %; the heavy round goes on to the same two at 80 % of its double.
            _m.BasicShot.Take(BasicShotUpgradeKind.PiercingRound);
            _m.BasicShot.Take(BasicShotUpgradeKind.PiercingRound);
            b.Clear();
            c.Clear();
            for (int i = 0; i < 6; i++)
            {
                int hb = _b.CurrentHealth, hc = _c.CurrentHealth;
                yield return ShootAt(_a);
                b.Add(Lost(_b, hb));
                c.Add(Lost(_c, hc));
            }
            int share = BasicShotRules.PierceDamage(Base, 0.8f), heavy = BasicShotRules.PierceDamage(2 * Base, 0.8f);
            Assert.That(b, Is.EqualTo(new[] { share, share, share, share, share, heavy }));
            Assert.That(c, Is.EqualTo(new[] { share, share, share, share, share, heavy }));
        }

        // ---------------- Hunter's Count ----------------

        [UnityTest]
        public IEnumerator HuntersCount_TheThirdHitOnTheSameEnemyMarksIt_SwitchingTargetsCostsTheCount()
        {
            _m.BasicShot.Take(BasicShotUpgradeKind.HuntersCount);
            _c.transform.position = new Vector3(-_a.transform.position.x, _a.transform.position.y, _a.transform.position.z); // to her left, a line of its own
            yield return ShootAt(_a);
            yield return ShootAt(_a);
            yield return ShootAt(_c);
            Assert.That(_a.MarkCount + _c.MarkCount, Is.Zero, "two on A and one on C: nothing yet");
            Assert.That(_a.HunterHits, Is.EqualTo(2));
            Assert.That(_c.HunterHits, Is.EqualTo(1), "each enemy counts its own");
            yield return ShootAt(_a);
            Assert.That(_a.MarkCount, Is.EqualTo(1), "A's third hit marks A");
            Assert.That(_a.HunterHits, Is.Zero);
            yield return ShootAt(_c);
            Assert.That(_c.MarkCount, Is.Zero, "the fifth basic hit on anyone no longer marks: C has two of its own");
            Assert.That(_player.HunterMarks, Is.EqualTo(1));

            _m.BasicShot.Take(BasicShotUpgradeKind.HuntersCount); // tier II: two marks on the third hit
            yield return ShootAt(_c);
            Assert.That(_c.MarkCount, Is.EqualTo(2));

            _m.BasicShot.Values = new BasicShotUpgradeValues { HuntersCount = false };
            int before = _a.MarkCount;
            for (int i = 0; i < 3; i++) yield return ShootAt(_a);
            Assert.That(_a.MarkCount, Is.EqualTo(before), "switched off: the kit's count of anyone's hits again (not yet at its fifth)");
        }

        [UnityTest]
        public IEnumerator HuntersCount_WithChainKill_ABounceAddsToTheCountOfTheEnemyItHits()
        {
            _m.BasicShot.Take(BasicShotUpgradeKind.HuntersCount);
            _m.BasicShot.Take(BasicShotUpgradeKind.ChainKill);
            Place(_b, 0f, 3f);
            yield return ShootAt(_b);
            yield return ShootAt(_b);
            Assert.That(_b.HunterHits, Is.EqualTo(2), "two of hers on B");
            Assert.That(_b.MarkCount, Is.Zero);
            _a.SetMaxHealth(Base);
            yield return ShootAt(_a);
            Assert.That(_a.IsAlive, Is.False);
            Assert.That(_player.ChainBounces, Is.EqualTo(1), "A's death bounced to B");
            Assert.That(_b.MarkCount, Is.EqualTo(1), "the bounce was B's third basic hit: B is marked");
            Assert.That(_b.HunterHits, Is.Zero);
        }

        // ---------------- The four held, every switch off ----------------

        [UnityTest]
        public IEnumerator TheFourHeld_TheirSwitchesOff_TheShotIsAsBefore()
        {
            foreach (BasicShotUpgradeKind k in new[] { BasicShotUpgradeKind.ChainKill, BasicShotUpgradeKind.SixthChamber, BasicShotUpgradeKind.HuntersCount, BasicShotUpgradeKind.PassItOn })
                for (int t = 0; t < 3; t++) _m.BasicShot.Take(k);
            _m.BasicShot.Values = new BasicShotUpgradeValues { ChainKill = false, SixthChamber = false, HuntersCount = false, PassItOn = false };
            Place(_b, 0f, 3f);
            int b = _b.CurrentHealth;
            var lost = new List<int>();
            for (int i = 0; i < Kit.BasicShotsPerMark; i++)
            {
                int a = _a.CurrentHealth;
                yield return ShootAt(_a);
                lost.Add(Lost(_a, a));
            }
            foreach (int l in lost) Assert.That(l, Is.EqualTo(Base), "no heavy round");
            Assert.That(_a.MarkCount, Is.EqualTo(1), "the kit's fifth hit marks, as before");
            _a.SetMaxHealth(Base);
            _a.AddMarks(2, Kit.MaxMarks, Kit.MarkLifetime);
            yield return ShootAt(_a);
            Assert.That(_b.CurrentHealth, Is.EqualTo(b), "no bounce");
            Assert.That(_b.MarkCount, Is.Zero, "nothing passed");
            Assert.That(_player.ChainBounces + _player.HeavyRounds + _player.HunterMarks + _player.MarksPassedOn, Is.Zero);
        }

        // ---------------- Sixth Chamber's count on the skill bar ----------------

        [UnityTest]
        public IEnumerator TheSkillBar_ShowsTheChambersCount_OverItsLeftEnd_GoldBeforeTheHeavyRound_AllocatingNothing()
        {
            SkillBar bar = Object.FindAnyObjectByType<SkillBar>();
            Assert.That(bar, Is.Not.Null, "the skill bar in the scene");
            bar.Tick();
            Assert.That(bar.ChamberShown, Is.False, "not held: no row");
            _m.BasicShot.Take(BasicShotUpgradeKind.SixthChamber);
            bar.Tick();
            Assert.That(bar.ChamberShown, Is.True);
            Assert.That(bar.ChamberRounds, Is.EqualTo(6));
            Assert.That(bar.ChamberFired, Is.Zero);
            Assert.That(bar.ChamberLit, Is.False);
            Rect row = bar.ChamberRow, plate = bar.StripPlateRect, left = bar.LeftEnd;
            TestContext.WriteLine("[141] the chamber row " + row + " over the strip plate " + plate + ", the left end " + left);
            Assert.That(row.yMax, Is.LessThanOrEqualTo(plate.yMin), "over the strip's plate, clear of it");
            Assert.That(row.center.x, Is.EqualTo(left.center.x).Within(0.5f), "centred over the left end");
            Assert.That(row.width, Is.LessThan(left.width * 2f), "small");
            Assert.That(row.yMin, Is.GreaterThan(0f));
            for (int i = 0; i < 5; i++) yield return Miss();
            bar.Tick();
            Assert.That(bar.ChamberFired, Is.EqualTo(5));
            Assert.That(bar.ChamberLit, Is.True, "the next shot is the heavy round: the row is gold");
            for (int i = 0; i < 5; i++) bar.Tick();
            Assert.That(() => { for (int i = 0; i < 100; i++) bar.Tick(); }, AllocExt.AllocatingGCMemory(Is.Not), "a frame of the bar with the row allocates nothing");
            yield return Miss();
            bar.Tick();
            Assert.That(bar.ChamberFired, Is.Zero, "the heavy round fired: the count starts again");
            Assert.That(bar.ChamberLit, Is.False);
            _m.BasicShot.Values = new BasicShotUpgradeValues { SixthChamber = false };
            bar.Tick();
            Assert.That(bar.ChamberShown, Is.False, "switched off: no row");
        }
    }
}
