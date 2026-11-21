using System;
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
using AllocIs = UnityEngine.TestTools.Constraints.Is;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 045 (decision 0011, Kristian's choices of 2026-11-20): the interface's one kinetic skin, in the ControlRoom
    /// scene. With the skin on every piece reports that it draws in the new manner and the pieces it replaces (her HUD
    /// health bar, the skill bar's boxes and rows, the cast bar under the middle) are not drawn; with it off every piece
    /// draws as before; the fonts and every icon load from Resources and a skill without an icon shows its key and a short
    /// name; nothing is allocated per frame with everything on screen (a fight with marks, a cast bar, the map, then a
    /// choice open). No frame is drawn: what each piece draws is worked out every frame without a window, and that is
    /// what these read.
    /// </summary>
    public class InterfaceStyleTests
    {
        const ulong RunSeed = 0x0E117E17UL;
        const int MostFrames = 600;

        static readonly string[] s_IconsThere =
        {
            "Quick Shot", "Powder Keg", "Fan Fire", "Scatter Blast", "Heavy Volley", "Backstep", "Triple Tap", "Tag Round",
            "Breaker Slug", "Slide", "Stopping Shot", "Dash", "Focus",
        };

        PlayerCharacter _player;
        PlayerHealth _health;
        SkillBar _skillBar;
        GeneratedLevels _levels;
        RunHud _hud;
        CastBar _cast;
        EnemyBars _bars;
        FloorMap _map;
        TimingFeedback _feedback;
        PlayerSigns _signs;
        Damageable _dummy;

        static NUnit.Framework.Constraints.IResolveConstraint NotAllocating => AllocExt.AllocatingGCMemory(Is.Not);

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
            _health = _player.GetComponent<PlayerHealth>();
            _health.Untouchable = true;
            _skillBar = Object.FindAnyObjectByType<SkillBar>();
            Assert.That(_skillBar, Is.Not.Null, "the skill bar in the scene");
            GameObject dummy = GameObject.Find("Dummy_A");
            _dummy = dummy != null ? dummy.GetComponent<Damageable>() : null;
            _levels = GeneratedLevels.Ensure();
            _hud = RunHud.Ensure();
            _cast = CastBar.Ensure();
            _bars = EnemyBars.Ensure();
            _map = FloorMap.Ensure();
            _feedback = TimingFeedback.Ensure();
            _signs = PlayerSigns.Ensure();
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            InterfaceStyle.Shared.Kinetic = true;
            if (_health != null) _health.Untouchable = false;
            if (_skillBar != null) _skillBar.enabled = true;
            foreach (MonoBehaviour m in new MonoBehaviour[] { _signs, _feedback, _map, _bars, _cast, _hud, _levels })
                if (m != null) Object.Destroy(m.gameObject);
            _signs = null;
            _feedback = null;
            _map = null;
            _bars = null;
            _cast = null;
            _hud = null;
            _levels = null;
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        // ---------------- Helpers ----------------

        int Slot(string name)
        {
            for (int i = 0; i < _player.Skills.Count; i++)
                if (_player.Skills[i].Name == name) return i;
            Assert.Fail("no skill " + name);
            return -1;
        }

        static IEnumerator Frames(Func<bool> condition)
        {
            for (int i = 0; i < MostFrames && !condition(); i++) yield return null;
        }

        static IEnumerator ForRealTime(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        void Near(Vector3 at, float back = 3f)
        {
            Vector3 p = at + Vector3.back * back;
            if (NavMesh.SamplePosition(p, out NavMeshHit hit, 2f, NavMesh.AllAreas)) p = hit.position;
            _player.Teleport(new Vector3(p.x, 0f, p.z));
        }

        /// <summary>Her feet on the screen, in the skin's layout pixels.</summary>
        Vector2 Feet()
        {
            Vector3 feet = Camera.main.WorldToScreenPoint(_player.Position);
            float s = InterfaceStyle.Scale;
            return new Vector2(feet.x / s, (Screen.height - feet.y) / s);
        }

        /// <summary>Long Shot equipped and charging toward a point: the cast bar is up.</summary>
        IEnumerator Charging(Vector3 toward)
        {
            int slot = Slot("Long Shot");
            _player.EquipSkill(slot);
            yield return Frames(() => _player.Action == ActionKind.None && !HitStop.IsActive);
            Assert.That(_player.PressSkill(slot, toward), Is.EqualTo(IntentResult.Started), "the charge");
            yield return null;
            _cast.Tick();
            Assume.That(_cast.Up, Is.True, "charging");
        }

        // ---------------- On ----------------

        [UnityTest]
        public IEnumerator WithTheSkinOn_EachPieceDrawsInTheNewManner_AndWhatItReplacesIsNotDrawn()
        {
            InterfaceValues v = InterfaceStyle.V;
            Assert.That(InterfaceStyle.Kinetic, Is.True, "on by default");

            // The strip and the strokes at her feet, not the skill bar's boxes and rows.
            _skillBar.Tick();
            Assert.That(_skillBar.Kinetic, Is.True);
            Assert.That(_skillBar.OldBarsDrawn, Is.False, "the boxes, the health and focus row and the cast row are not drawn");
            Assert.That(_skillBar.SlotCount, Is.EqualTo(SkillLoadout.KeyCount), "a slot per key");
            Assert.That(_skillBar.StrokesShown, Is.True, "she is on screen: the strokes show");
            Vector2 feet = Feet();
            Rect h = _skillBar.HealthStroke, f = _skillBar.FocusStroke;
            Assert.That(h.y - feet.y, Is.EqualTo(v.UnderFeet).Within(0.5f), "just under her feet");
            Assert.That((h.xMin + f.xMax) * 0.5f, Is.EqualTo(feet.x).Within(0.5f), "centred on her");
            Assert.That(f.xMin, Is.GreaterThan(h.xMax), "health left, focus right");
            Assert.That(_skillBar.HealthNumber, Is.EqualTo(_health.Current.ToString()), "one number: her health");
            KineticSlot first = _skillBar.SlotOf(0);
            Assert.That(first.Box.yMax, Is.EqualTo(v.LayoutHeight - v.StripBottom).Within(0.5f), "the strip at the bottom");

            // Her HUD health bar is not drawn, not even where no skill bar shows it.
            _skillBar.enabled = false;
            yield return null;
            Assert.That(_hud.HealthBarDrawn, Is.False, "never her old health bar while the skin is on");
            _skillBar.enabled = true;

            // The cast bar: a blade over her head, not the bar under the middle.
            Assume.That(_dummy, Is.Not.Null);
            Near(_dummy.transform.position);
            yield return null;
            yield return Charging(_dummy.transform.position);
            Assert.That(_cast.Showing, Is.True);
            Assert.That(_cast.Kinetic, Is.True);
            Assert.That(_cast.BladeDrawn, Is.True, "the blade over her head");
            Assert.That(_cast.CentreBarDrawn, Is.False, "the bar under the middle is not drawn");
            feet = Feet();
            Assert.That(_cast.Blade.yMax, Is.LessThan(feet.y - 20f), "over her, well above her feet");
            Assert.That(_cast.Blade.center.x, Is.EqualTo(feet.x).Within(60f), "over her");
            Assert.That(_cast.Blade.width, Is.EqualTo(v.BladeWidth));
            _player.ReleaseSkill(Slot("Long Shot"));

            // The small bars over enemies, and the boss's at the top.
            List<Enemy> made = _levels.SpawnPractice(EnemyKind.Shooter);
            Assume.That(made.Count, Is.GreaterThan(0));
            yield return null;
            Near(made[0].transform.position);
            yield return null;
            made[0].Body.TakeHit(10);
            _bars.Tick();
            Assert.That(_bars.Kinetic, Is.True);
            Assert.That(_bars.TryGet(made[0], out EnemyBarView bar), Is.True, "its bar, in the same place");
            Assert.That(bar.DrawCalls, Is.EqualTo(EnemyBarRules.DrawCalls(false, false, 0, bar.Pips, false)), "the same draw calls");
            List<Enemy> boss = _levels.SpawnPractice(EnemyKind.Boss);
            Assume.That(boss.Count, Is.GreaterThan(0));
            Assert.That(boss[0].GetComponent<BossHealthBar>().Kinetic, Is.True, "the boss's bar in the skin");

            // Words and numbers.
            Assert.That(_feedback.Kinetic, Is.True);
            Assume.That(TimingFeedback.NumbersOn, Is.True, "damage numbers on");
            _health.Untouchable = false;
            _health.TakeHit(2);
            _health.Untouchable = true;
            _feedback.Tick();
            Assert.That(_feedback.Numbers.Any(n => n.Kind == NumberKind.Hurt), Is.True, "her red number");
            Assert.That(InterfaceRules.NumberInk(NumberKind.Hurt), Is.EqualTo(UiInk.Red));
            _skillBar.Tick();
            Assert.That(_skillBar.LostAlpha, Is.GreaterThan(0f), "the white of what she just lost");
            Assert.That(_skillBar.LostFrom, Is.GreaterThan(_skillBar.HealthShare));
        }

        [UnityTest]
        public IEnumerator InARun_TheMapAndTheChoice_AreDrawnInTheSkin_AndAPickFlashes()
        {
            _levels.StartRun(RunSeed);
            yield return null;
            _map.Tick();
            Assert.That(_map.Shown, Is.True, "the small map in a floor");
            Assert.That(_map.Kinetic, Is.True, "in thin outlines, no box");

            _levels.FinishFloorForTests();
            yield return Frames(() => _levels.Choice == GeneratedLevels.ChoiceKind.FloorReward);
            Assume.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.FloorReward));
            Assert.That(_levels.ChoiceKinetic, Is.True, "the choice as the skin's cards");
            int count = _levels.KineticCards();
            Assert.That(count, Is.EqualTo(_levels.OfferedSkills.Count), "a card per option");
            float viewWidth = Screen.width / InterfaceStyle.Scale;
            for (int i = 0; i < count; i++)
            {
                Rect r = _levels.CardRect(i);
                Assert.That(r.xMax, Is.LessThan(viewWidth), "on screen");
                Assert.That(r.xMin, Is.GreaterThan(viewWidth * 0.6f), "at the right edge");
                string name = _player.Skills[_levels.OfferedSkills[i]].Name;
                Assert.That(_levels.CardTitle(i), Is.EqualTo(name), "the skill's name its title");
                StringAssert.Contains(SkillDefaults.Line(name), _levels.CardText(i), "and what it does in the text, as before");
                if (i > 0) Assert.That(r.yMin, Is.GreaterThan(_levels.CardRect(i - 1).yMax), "stacked down");
            }
            Assert.That(_map.Shown, Is.False, "the map is not drawn under a choice, as before");

            Assert.That(_levels.PickReward(0), Is.True);
            Assert.That(_levels.LastPickedCard, Is.EqualTo(0));
            Assert.That(_levels.PickFlashing, Is.True, "the pick flashes");
            yield return ForRealTime(InterfaceStyle.V.PickFlashSeconds + 0.05f);
            Assert.That(_levels.PickFlashing, Is.False, "and goes");
            _levels.BackToPracticeRoom();
        }

        // ---------------- Off ----------------

        [UnityTest]
        public IEnumerator WithTheSkinOff_EveryPieceDrawsAsBefore()
        {
            InterfaceStyle.Shared.Kinetic = false;
            _skillBar.Tick();
            Assert.That(_skillBar.Kinetic, Is.False);
            Assert.That(_skillBar.OldBarsDrawn, Is.True, "the boxes and rows as before");
            Assert.That(_skillBar.StrokesShown, Is.False, "no strokes at her feet");
            Assert.That(_skillBar.SlotCount, Is.Zero, "no strip");

            yield return null;
            Assert.That(_hud.HealthBarDrawn, Is.False, "the skill bar shows her health, as before");
            _skillBar.enabled = false;
            yield return null;
            Assert.That(_hud.HealthBarDrawn, Is.True, "and without one, the HUD's own bar, as before");
            _skillBar.enabled = true;

            Assume.That(_dummy, Is.Not.Null);
            Near(_dummy.transform.position);
            yield return null;
            yield return Charging(_dummy.transform.position);
            Assert.That(_cast.Kinetic, Is.False);
            Assert.That(_cast.CentreBarDrawn, Is.True, "the bar under the middle, as before");
            Assert.That(_cast.BladeDrawn, Is.False, "no blade");
            _player.ReleaseSkill(Slot("Long Shot"));

            _bars.Tick();
            Assert.That(_bars.Kinetic, Is.False);
            Assert.That(_feedback.Kinetic, Is.False);
            Assert.That(_map.Kinetic, Is.False);
            _levels.StartRun(RunSeed);
            yield return null;
            _levels.FinishFloorForTests();
            yield return Frames(() => _levels.Choice == GeneratedLevels.ChoiceKind.FloorReward);
            Assume.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.FloorReward));
            Assert.That(_levels.ChoiceKinetic, Is.False, "the choice in its box, as before");
            Assert.That(_levels.PickReward(0), Is.True);
            Assert.That(_levels.PickFlashing, Is.False, "no flash");
            _levels.BackToPracticeRoom();
        }

        // ---------------- Fonts and icons ----------------

        [Test]
        public void TheFontsAndEveryIcon_LoadFromResources()
        {
            Assert.That(InterfaceStyle.Heavy, Is.Not.Null, "the heavy condensed italic");
            Assert.That(InterfaceStyle.Light, Is.Not.Null, "the lighter italic");
            StringAssert.Contains("ExtraBoldItalic", InterfaceStyle.Heavy.name);
            StringAssert.Contains("SemiBoldItalic", InterfaceStyle.Light.name);
            Assert.That(InterfaceStyle.Text(true, 20, TextAnchor.MiddleCenter).font, Is.SameAs(InterfaceStyle.Heavy), "the styles use them");
            Assert.That(Resources.Load<TextAsset>("UI/Fonts/OFL"), Is.Not.Null, "the licence beside them");
            foreach (string name in s_IconsThere)
            {
                Texture2D icon = InterfaceStyle.Icon(name);
                Assert.That(icon, Is.Not.Null, name + "'s icon");
                Assert.That(icon.width, Is.EqualTo(InterfaceStyle.V.IconMaskSize), name + ": a small mask");
            }
            Assert.That(InterfaceStyle.Icon("Long Shot"), Is.Null, "left out: too thin to read at 32 pixels");
            Assert.That(InterfaceStyle.Icon("No Such Skill"), Is.Null, "a missing one is none, not an error");
        }

        [UnityTest]
        public IEnumerator ASkillWithoutAnIcon_ShowsItsKeyAndAShortName()
        {
            int slot = Slot("Long Shot");
            _player.EquipSkill(slot);
            yield return null;
            _skillBar.Tick();
            int key = _player.KeyOfSkill(slot);
            Assume.That(key, Is.GreaterThanOrEqualTo(0), "Long Shot on a key");
            KineticSlot s = _skillBar.SlotOf(key);
            Assert.That(s.Icon, Is.Null, "no icon");
            Assert.That(s.ShortName, Is.EqualTo("LONG"), "its short name instead");
            Assert.That(s.Key, Is.EqualTo(_player.GetComponent<PlayerInputDriver>().ActiveBindings.SkillLabel(key)), "and its key");
            KineticSlot quick = _skillBar.SlotOf(_player.KeyOfSkill(Slot("Quick Shot")));
            Assert.That(quick.Icon, Is.Not.Null, "a skill with one shows it");
            Assert.That(quick.Look, Is.EqualTo(SlotLook.Ready).Or.EqualTo(SlotLook.Cooling).Or.EqualTo(SlotLook.Live));
        }

        // ---------------- Nothing per frame ----------------

        [UnityTest]
        public IEnumerator WithEverythingOnScreen_NothingIsAllocatedPerFrame()
        {
            // The measure is the test framework's own (it counts the GC.Alloc samples on this thread); first, that it sees one.
            Assert.That(() => { s_junk = new byte[64]; }, AllocIs.AllocatingGCMemory(), "the measure sees an allocation");

            // A fight: an elite with marks and its bar, her hurt number, a cast bar, the map, the strip, the strokes.
            GeneratedLevel level = _levels.StartRun(RunSeed);
            yield return null;
            Enemy elite = level.Elite;
            Assume.That(elite, Is.Not.Null);
            Near(elite.transform.position, 4f);
            yield return null;
            yield return null;
            elite.Body.TakeHit(10);
            elite.Body.AddMarks(2, _player.Kit.MaxMarks, 60f);
            _health.Untouchable = false;
            _health.TakeHit(1);
            _health.Untouchable = true;
            yield return Charging(elite.transform.position);
            TickAll();
            Assume.That(_cast.BladeDrawn && _map.Shown && _bars.Count > 0 && _skillBar.StrokesShown, Is.True, "everything up");
            Assume.That(_feedback.Numbers.Count + _feedback.Words.Count, Is.GreaterThan(0), "a number up");
            EachPieceAllocatesNothing("a fight");
            Assert.That(() => { for (int i = 0; i < 300; i++) TickAll(); }, NotAllocating, "a fight's frame allocates nothing");
            _player.ReleaseSkill(Slot("Long Shot"));

            // A choice open: the cards.
            yield return Frames(() => _player.Action == ActionKind.None);
            _levels.FinishFloorForTests();
            yield return Frames(() => _levels.Choice == GeneratedLevels.ChoiceKind.FloorReward);
            Assume.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.FloorReward));
            TickAll();
            Assume.That(_levels.KineticCards(), Is.GreaterThan(0));
            EachPieceAllocatesNothing("a choice");
            Assert.That(() => { for (int i = 0; i < 300; i++) TickAll(); }, NotAllocating, "a choice's frame allocates nothing: its texts are made once");
            _levels.BackToPracticeRoom();
        }

        static byte[] s_junk;

        /// <summary>
        /// Each piece's frame measured apart (night of 2026-11-21: the whole frame allocated, and it was the words' and
        /// numbers' search for new stagger bars, TimingFeedback.Find), so a fault names its piece.
        /// </summary>
        void EachPieceAllocatesNothing(string when)
        {
            var allocating = new List<string>();
            void Measure(string piece, Action tick)
            {
                TestDelegate frames = () => { for (int i = 0; i < 300; i++) tick(); };
                if (!NotAllocating.Resolve().ApplyTo(frames).IsSuccess) allocating.Add(piece);
            }
            Measure("the strip and the strokes (SkillBar.Tick)", _skillBar.Tick);
            Measure("the cast blade (CastBar.Tick)", _cast.Tick);
            Measure("the bars over enemies (EnemyBars.Tick)", _bars.Tick);
            Measure("the map (FloorMap.Tick)", _map.Tick);
            Measure("the words and numbers (TimingFeedback.Tick)", _feedback.Tick);
            Measure("the signs (PlayerSigns.Tick)", _signs.Tick);
            Measure("the cards (GeneratedLevels.KineticCards)", () => _levels.KineticCards());
            Assert.That(allocating, Is.Empty, when + ": these pieces allocate in a frame");
        }

        // ---------------- A chest card's tier (work order 050) ----------------

        [UnityTest]
        public IEnumerator AChestCard_ShowsItsTier_AsItsNumeralsColour_AndItsWord()
        {
            _levels.StartRun(RunSeed);
            yield return null;
            List<GeneratedLevel.PlacedProp> chests = _levels.Chests();
            Assume.That(chests.Count, Is.GreaterThan(0), "a chest on floor 1");
            Assert.That(_levels.OpenChest(chests[0]), Is.True);
            yield return null;
            Assume.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.Chest));
            IReadOnlyList<Upgrade> offer = _levels.OfferedUpgrades;
            Assert.That(offer.Count, Is.GreaterThan(0));
            Assert.That(offer.Any(u => u.Tiered), Is.True, "the run's chest offers are tiered (stats and tiers on by default)");
            // Every tier once, read from the offer's own data before its cards are made.
            UpgradeTier[] tiers = { UpgradeTier.Common, UpgradeTier.Rare, UpgradeTier.Epic };
            for (int i = 0; i < offer.Count; i++)
            {
                offer[i].Tiered = true;
                offer[i].Tier = tiers[i % tiers.Length];
            }
            int count = _levels.KineticCards();
            Assert.That(count, Is.EqualTo(offer.Count), "a card per upgrade");
            UiInk[] inks = { UiInk.White, UiInk.Teal, UiInk.Gold };
            for (int i = 0; i < count; i++)
            {
                Assert.That(_levels.CardNumeralInk(i), Is.EqualTo(inks[i % inks.Length]), "card " + i + ": white common, teal rare, gold epic");
                Assert.That(_levels.CardTitle(i), Is.EqualTo(offer[i].Text), "what it does, its title");
                StringAssert.Contains(offer[i].TierWord.ToUpperInvariant(), _levels.CardText(i), "and its tier's word");
            }
            _levels.BackToPracticeRoom();
        }

        void TickAll()
        {
            _skillBar.Tick();
            _cast.Tick();
            _bars.Tick();
            _map.Tick();
            _feedback.Tick();
            _signs.Tick();
            _levels.KineticCards();
        }
    }
}
