using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 138, part 1 (Kristian, 2026-11-25: "Jeg tror også vi har litt for mange spell slots. Kanskje fjerne 2 spell
    /// slots"; his tick "To av de tomme"), in play: the skill bar draws six boxes, Q 1 2 R F and the right button, centred with
    /// no hole; keys 3 and 4 start nothing; the pause menu's keys list the six; the two found skills of a run go on 1 and 2;
    /// the practice room's every-skill switch puts the first two found on 1 and 2. Off, the eight of before.
    /// </summary>
    public class SixSlotsPlayTests
    {
        const ulong RunSeed = 0x13800001UL;
        const int MostFrames = 600;
        static readonly int[] SixKeys = { 0, 1, 2, 4, 6, 7 };

        PlayerCharacter _player;
        PlayerInputDriver _driver;
        SkillBar _bar;
        GeneratedLevels _levels;
        readonly List<Object> _copies = new List<Object>();
        readonly Queue<InputFrame> _frames = new Queue<InputFrame>();
        InputFrame _idle;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            InterfaceStyle.Shared.Kinetic = true;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            _driver = Object.FindAnyObjectByType<PlayerInputDriver>();
            _bar = Object.FindAnyObjectByType<SkillBar>();
            Assert.That(_player, Is.Not.Null);
            Assert.That(_bar, Is.Not.Null);
            _frames.Clear();
            _idle = default;
            _driver.InputSource = () => null;
        }

        [TearDown]
        public void CleanUp()
        {
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            if (RunHud.Current != null) Object.Destroy(RunHud.Current.gameObject); // made by a test here; it outlives the scene
            foreach (Object o in _copies) if (o != null) Object.Destroy(o);
            _copies.Clear();
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        static IEnumerator Frames(Func<bool> condition)
        {
            for (int i = 0; i < MostFrames && !condition(); i++) yield return null;
        }

        void OwnKit(Action<KitValues> change)
        {
            SkillSettings copy = Object.Instantiate(_player.SkillSet);
            _copies.Add(copy);
            change(copy.Kit);
            _player.SkillSet = copy;
        }

        float ViewWidth => Screen.width / InterfaceStyle.Scale;

        [UnityTest]
        public IEnumerator TheBar_DrawsSixBoxes_OnQ12RFAndTheRightButton_CentredWithNoHole()
        {
            Assert.That(_player.Kit.SixSlots, Is.True, "on by default");
            yield return null;
            _bar.Tick();
            Assert.That(_bar.SlotCount, Is.EqualTo(6), "six boxes");
            Assert.That(Enumerable.Range(0, 6).Select(_bar.KeyOfBox), Is.EqualTo(SixKeys), "keys Q 1 2 R F and the right button, in key order");
            InputBindings b = _driver.ActiveBindings;
            Assert.That(Enumerable.Range(0, 6).Select(i => _bar.SlotAt(i).Key), Is.EqualTo(SixKeys.Select(k => SkillBarLabel(b, k))), "each box its key's label");
            Assert.That(Enumerable.Range(0, 6).Select(i => b.SkillLabel(_bar.KeyOfBox(i))), Is.EqualTo(new[] { "Q", "1", "2", "R", "F", "RMB" }));
            float gap = _bar.SlotAt(1).Home.xMin - _bar.SlotAt(0).Home.xMax;
            for (int i = 1; i < 6; i++)
            {
                Assert.That(_bar.SlotAt(i).Home.xMin - _bar.SlotAt(i - 1).Home.xMax, Is.EqualTo(gap).Within(0.01f), "the same gap before box " + i + ": no hole");
                Assert.That(_bar.SlotAt(i).Home.y, Is.EqualTo(_bar.SlotAt(0).Home.y).Within(0.01f), "on one line");
            }
            float middle = (_bar.SlotAt(0).Home.xMin + _bar.SlotAt(5).Home.xMax) * 0.5f;
            Assert.That(middle, Is.EqualTo(ViewWidth * 0.5f).Within(0.5f), "centred");
            Assert.That(_bar.LeftEnd.xMax, Is.LessThanOrEqualTo(_bar.SlotAt(0).Home.xMin + 0.5f), "the dash's end left of the first box");
            Assert.That(_bar.RightEnd.xMin, Is.GreaterThanOrEqualTo(_bar.SlotAt(5).Home.xMax - 0.5f), "focus's end right of the last");
            Assert.That(_bar.SlotOf(3).Box.width, Is.Zero, "key 3 has no box");
            Assert.That(_bar.SlotOf(5).Box.width, Is.Zero, "key 4 has no box");
            Debug.Log($"[138] six boxes from x {_bar.SlotAt(0).Home.xMin:0.0} to {_bar.SlotAt(5).Home.xMax:0.0} of a view {ViewWidth:0.0} wide, a gap of {gap:0.0}; dash end {_bar.LeftEnd}, focus end {_bar.RightEnd}");
        }

        static string SkillBarLabel(InputBindings b, int key) => b.SkillLabel(key);

        [UnityTest]
        public IEnumerator Off_TheBarDrawsEight_AsBefore()
        {
            OwnKit(k => k.SixSlots = false);
            yield return null;
            _bar.Tick();
            Assert.That(_bar.SlotCount, Is.EqualTo(8));
            Assert.That(Enumerable.Range(0, 8).Select(_bar.KeyOfBox), Is.EqualTo(Enumerable.Range(0, 8)));
            for (int key = 0; key < 8; key++) Assert.That(_bar.SlotOf(key).Home, Is.EqualTo(_bar.SlotAt(key).Home), "the box of key " + key + " is its place");
            Assert.That(_player.BarCount, Is.EqualTo(8));
            Assert.That(_player.KeyInUse(3) && _player.KeyInUse(5), Is.True);
        }

        [UnityTest]
        public IEnumerator TheSixKeys_StartTheSkillsOnThem_AndKeys3And4StartNothing()
        {
            _player.EquipStartSkills();
            int keg = _player.SlotOf("Powder Keg"), fan = _player.SlotOf("Fan Fire"), longShot = _player.SlotOf("Long Shot");
            Assert.That(_player.EquipSkill(keg), Is.EqualTo(1), "the first found on 1");
            Assert.That(_player.EquipSkill(fan), Is.EqualTo(2), "the second on 2");
            Assert.That(_player.EquipSkill(longShot), Is.EqualTo(-1), "a third has no key");
            Assert.That(_player.SkillOnKey(3), Is.EqualTo(-1));
            Assert.That(_player.SkillOnKey(5), Is.EqualTo(-1));
            Damageable a = GameObject.Find("Dummy_A").GetComponent<Damageable>();
            Vector2 onA = Camera.main.WorldToScreenPoint(new Vector3(a.transform.position.x, 0f, a.transform.position.z));
            _idle = new InputFrame { CursorScreen = onA };
            _driver.InputSource = () => _frames.Count > 0 ? _frames.Dequeue() : _idle;
            for (int key = 0; key < SkillLoadout.KeyCount; key++)
            {
                int slot = _player.SkillOnKey(key);
                _frames.Enqueue(new InputFrame { CursorScreen = onA, SkillsPressed = 1 << key });
                yield return null;
                yield return null;
                if (_player.KeyInUse(key))
                {
                    Assert.That(slot, Is.GreaterThanOrEqualTo(0), "key " + key + " holds a skill");
                    Assert.That(_player.ActiveSkillSlot, Is.EqualTo(slot), "key " + key + " starts " + _player.Skills[slot].Name);
                }
                else
                {
                    Assert.That(_player.Action, Is.EqualTo(ActionKind.None), "key " + key + " starts nothing");
                    Assert.That(_player.ActiveSkillSlot, Is.EqualTo(-1));
                }
                _player.Teleport(_player.Position); // clear it, so the next key is not buffered
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator ThePauseMenusKeys_ListTheSix_AndNot3Or4()
        {
            RunHud hud = RunHud.Ensure();
            Assert.That(hud, Is.Not.Null);
            _player.EquipStartSkills(); // the start four: the practice room hands her every skill
            yield return null;
            List<string> lines = hud.KeyLines();
            InputBindings b = _driver.ActiveBindings;
            foreach (int key in SixKeys) Assert.That(lines.Any(l => l.StartsWith(b.SkillLabel(key) + "  ", StringComparison.Ordinal)), Is.True, "a line for " + b.SkillLabel(key));
            Assert.That(lines.Any(l => l.StartsWith("3  ", StringComparison.Ordinal) || l.StartsWith("4  ", StringComparison.Ordinal)), Is.False, "none for 3 or 4");
            StringAssert.Contains("R  Scatter Blast", string.Join("\n", lines));
            StringAssert.Contains("1  (empty)", string.Join("\n", lines), "a free slot says so");
        }

        [UnityTest]
        public IEnumerator EverySkill_InThePracticeRoom_TheFirstTwoFoundOn1And2_AndTheBarStillSix()
        {
            OwnKit(k => k.StartWithAllSkills = true);
            yield return null;
            _bar.Tick();
            Assert.That(_player.Skills[_player.SkillOnKey(1)].Name, Is.EqualTo("Powder Keg"));
            Assert.That(_player.Skills[_player.SkillOnKey(2)].Name, Is.EqualTo("Fan Fire"));
            Assert.That(Enumerable.Range(0, _player.Skills.Count).All(_player.IsEquipped), Is.True, "every skill had, for the intents");
            Assert.That(_bar.SlotCount, Is.EqualTo(6));
            Assert.That(Enumerable.Range(0, 6).All(i => _bar.SlotAt(i).Skill >= 0), Is.True, "every box filled");
        }

        [UnityTest]
        public IEnumerator WhatThePracticeRoomGivesHer_IsLogged()
        {
            // The order asks what the practice room shows now: logged for the report, and the four start skills on their keys.
            yield return null;
            string keys = string.Join(", ", Enumerable.Range(0, SkillLoadout.KeyCount).Select(k => k + ":" + (_player.SkillOnKey(k) >= 0 ? _player.Skills[_player.SkillOnKey(k)].Name : "-")));
            Debug.Log("[138] the practice room's keys: " + keys + "; equipped " + Enumerable.Range(0, _player.Skills.Count).Count(_player.IsEquipped) + " of " + _player.Skills.Count);
            Assert.That(_player.Skills[_player.SkillOnKey(0)].Name, Is.EqualTo("Quick Shot"));
            Assert.That(_player.Skills[_player.SkillOnKey(4)].Name, Is.EqualTo("Scatter Blast"));
            Assert.That(_player.Skills[_player.SkillOnKey(6)].Name, Is.EqualTo("Backstep"));
            Assert.That(_player.Skills[_player.SkillOnKey(7)].Name, Is.EqualTo("Triple Tap"));
        }

        [UnityTest]
        public IEnumerator ARun_WithTheRewardOfBefore_TheTwoFoundGoOn1And2_AndWithSixFullNothingIsOffered()
        {
            // Part 1 on and part 2 off (the order: "say what happens then and make it sane"): the old reward gives the two found
            // skills on 1 and 2; from then on no skill could go on a key, so no reward screen comes, as when no skill is left.
            _levels = GeneratedLevels.Ensure();
            _levels.MetaValues.Enabled = false;
            _levels.MetaValues.Attachments = false;
            KitSwitchesForTests.SkillsOnlyReward(_levels);
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _levels.StartRun(RunSeed);
            yield return null;
            var taken = new List<int>();
            for (int floor = 1; floor <= 2; floor++)
            {
                _levels.FinishFloorForTests();
                yield return Frames(() => _levels.Choice == GeneratedLevels.ChoiceKind.FloorReward);
                Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.FloorReward), "floor " + floor + "'s reward");
                taken.Add(_levels.OfferedSkills[0]);
                Assert.That(_levels.PickReward(0), Is.True);
                if (floor == 1) Assert.That(_levels.PickFocusChoice(0), Is.True, "the focus screen of before");
                yield return null;
            }
            Assert.That(_player.KeyOfSkill(taken[0]), Is.EqualTo(1));
            Assert.That(_player.KeyOfSkill(taken[1]), Is.EqualTo(2));
            Assert.That(_player.FirstFreeKey, Is.EqualTo(-1), "six full");
            Assert.That(_levels.Floor, Is.EqualTo(3));
            _levels.FinishFloorForTests();
            yield return Frames(() => _levels.Floor == 4 || _levels.Choice != GeneratedLevels.ChoiceKind.None);
            Assert.That(_levels.Choice, Is.Not.EqualTo(GeneratedLevels.ChoiceKind.FloorReward), "no skill screen with six full");
            Assert.That(_levels.Floor, Is.EqualTo(4), "straight to the next floor");
        }
    }
}
