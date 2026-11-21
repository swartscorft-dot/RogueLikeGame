using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// The faults the fourth review of 2026-11-21 found (work order 055), in the ControlRoom scene through the intents: a
    /// choice card holds the whole of its text; a card's seen corner takes the click; the strokes and the blade are placed
    /// from this frame's camera; a clean Backstep deals with the stats on what it dealt with them off; a reward drop lands
    /// where she can reach it, never on a large prop, or its choice opens at once; the fog of a carved room is born over its
    /// open floor; the opposite move key ends a quick duck; the interface's values and textures do not outlive a Play session;
    /// the icons are decoded when a run starts; the pause screen's lists, the prompt's text, the act-cleared sound, an epic's
    /// second bell, the mood's plan after a toggle; and the three things to check (Second Look's footer, the flat damage's
    /// weights in the settings asset, a Powder Keg aimed at a carved block).
    ///
    /// A frame without a window is about 0.3 ms: the waits count frames or real time, or game time only where the game runs.
    /// </summary>
    public class ReviewFix4Tests
    {
        const ulong RunSeed = 0x0550A055UL;
        const int MostFrames = 600;
        static readonly Vector3 Practice = new Vector3(-8f, 0f, -8f); // open ground in the practice room, away from her start
        static readonly Vector3 NearA = new Vector3(2f, 0f, 1f);      // Dummy_A is 2.3 m away here

        PlayerCharacter _player;
        PlayerHealth _health;
        PlayerInputDriver _driver;
        GeneratedLevels _levels;
        RunHud _hud;
        PlaceholderSounds _sounds;
        Damageable _a;
        FeelValues _feel;
        bool _takeCoverWas, _quickDuckWas;
        Keyboard _keyboard;
        Mouse _mouse;
        readonly List<Object> _made = new List<Object>();
        readonly List<(PlayerReport Report, int Frame, float Time)> _reports = new List<(PlayerReport, int, float)>();
        readonly List<PlaceholderSoundRequest> _played = new List<PlaceholderSoundRequest>();
        readonly List<ShotSoundRequest> _voiced = new List<ShotSoundRequest>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            Time.captureDeltaTime = 0f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            InterfaceStyle.Shared.Kinetic = true;
            PlaceholderSounds.Requested += Record;
            ShotSounds.Requested += Voice;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _driver = _player.GetComponent<PlayerInputDriver>();
            _driver.InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            _health.MaxHealth = 100000; // nothing here is about her health
            _health.RestoreFull();
            _feel = _player.Feel;
            _takeCoverWas = _feel.TakeCover;
            _quickDuckWas = _feel.QuickDuck;
            _a = GameObject.Find("Dummy_A").GetComponent<Damageable>();
            _a.SetMaxHealth(1000000);
            _levels = GeneratedLevels.Ensure();
            _reports.Clear();
            _player.Reported += r => _reports.Add((r, Time.frameCount, Time.time));
            _player.Teleport(Practice);
            yield return null;
            _played.Clear();
            _voiced.Clear();
        }

        [TearDown]
        public void CleanUp()
        {
            PlaceholderSounds.Requested -= Record;
            ShotSounds.Requested -= Voice;
            Time.captureDeltaTime = 0f;
            InterfaceStyle.Shared.Kinetic = true;
            if (_feel != null)
            {
                _feel.TakeCover = _takeCoverWas; // the feel values live in an asset
                _feel.QuickDuck = _quickDuckWas;
            }
            if (_keyboard != null) InputSystem.RemoveDevice(_keyboard);
            if (_mouse != null) InputSystem.RemoveDevice(_mouse);
            _keyboard = null;
            _mouse = null;
            foreach (Object o in _made) if (o != null) Object.Destroy(o);
            _made.Clear();
            if (_player != null && (_levels == null || _player.Modifiers != _levels.Modifiers)) _player.Modifiers = null; // those put on by hand
            if (_sounds != null) Object.Destroy(_sounds.gameObject);
            _sounds = null;
            if (_hud != null) Object.Destroy(_hud.gameObject);
            _hud = null;
            if (_health != null) _health.Untouchable = false;
            if (_levels != null)
            {
                if (_levels.Paused) _levels.Resume();
                Object.Destroy(_levels.gameObject);
            }
            _levels = null;
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        void Record(PlaceholderSoundRequest r) => _played.Add(r);
        void Voice(ShotSoundRequest r) => _voiced.Add(r);

        // ---------------- Helpers ----------------

        static IEnumerator Frames(Func<bool> condition)
        {
            for (int i = 0; i < MostFrames && !condition(); i++) yield return null;
        }

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
        Vector2 ScreenMiddle => new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        int Slot(string name)
        {
            for (int i = 0; i < _player.Skills.Count; i++)
                if (_player.Skills[i].Name == name) return i;
            Assert.Fail("no skill " + name);
            return -1;
        }

        IEnumerator UntilFree() =>
            Until(() => !_player.IsLocked && _player.Action == ActionKind.None && _player.SkillGateRemaining <= 0f && _player.RecastSlot < 0, 5f);

        /// <summary>Her feet on the screen, in the skin's layout pixels, by the camera as it is now.</summary>
        Vector2 Feet()
        {
            Vector3 feet = Camera.main.WorldToScreenPoint(_player.Position);
            float s = InterfaceStyle.Scale;
            return new Vector2(feet.x / s, (Screen.height - feet.y) / s);
        }

        /// <summary>Floor 1's reward and the focus choice taken through the intents: she is on floor 2.</summary>
        IEnumerator OnToTheNextFloor()
        {
            int floor = _levels.Floor;
            _levels.FinishFloorForTests();
            yield return Frames(() => _levels.Choice == GeneratedLevels.ChoiceKind.FloorReward);
            Assert.That(_levels.PickReward(0), Is.True, "floor " + floor + "'s reward");
            if (_levels.Choice == GeneratedLevels.ChoiceKind.FocusChoice) Assert.That(_levels.PickFocusChoice(0), Is.True);
            yield return null;
            Assert.That(_levels.Floor, Is.EqualTo(floor + 1));
        }

        /// <summary>A low prop (a sarcophagus) as a level builds it, its near face <paramref name="face"/> m from <paramref name="her"/> along <paramref name="way"/>.</summary>
        GameObject LowProp(Vector3 her, Vector3 way, float face = 0.5f)
        {
            const PropModel model = PropModel.Sarcophagus;
            PropSize size = PropSize.Of(model);
            float half = size.Shape == PropShape.Circle ? (float)size.Radius : (float)size.Depth * 0.5f;
            Vector3 at = Flat(her) + way * (face + half);
            int rotation = (Mathf.RoundToInt(Quaternion.LookRotation(-way).eulerAngles.y / 90f) * 90) % 360;
            var values = new PropValues();
            LevelProp prop = LevelProp.Make(model, new LevelPoint(at.x, at.z), rotation, PropPlace.Floor);
            GameObject go = PropFactory.Make(prop, at, null, LayerMask.NameToLayer("Default"), null, "TestProp_" + model, 1f);
            if (prop.IsCover(values)) go.AddComponent<Cover>().LowUnderQuickDuck = PropSort.LowerUnderTheDuck(prop, values);
            if (!prop.Slender) go.AddComponent<Shelter>();
            _made.Add(go);
            Physics.SyncTransforms();
            return go;
        }

        // ---------------- 1. A choice card holds the whole of its text ----------------

        /// <summary>Every card on screen: its title and its text each fit the rectangle they are drawn in, inside the card.</summary>
        void EveryCardHoldsItsText(string what)
        {
            InterfaceValues v = InterfaceStyle.V;
            GUIStyle titleStyle = InterfaceStyle.Text(true, v.CardTitleSize, TextAnchor.UpperLeft, true);
            GUIStyle textStyle = InterfaceStyle.Text(false, v.CardTextSize, TextAnchor.UpperLeft, true);
            int count = _levels.KineticCards();
            Assert.That(count, Is.GreaterThan(0), what + ": cards");
            float height = _levels.CardRect(0).height;
            for (int i = 0; i < count; i++)
            {
                Rect card = _levels.CardRect(i), title = _levels.CardTitleRect(i), text = _levels.CardTextRect(i);
                string n = what + ", card " + i + " (" + _levels.CardTitle(i) + "): ";
                Assert.That(card.height, Is.EqualTo(height), n + "the cards of one offer are of one height");
                Assert.That(InterfaceStyle.MeasureHeight(_levels.CardTitle(i), titleStyle, title.width), Is.LessThanOrEqualTo(title.height + 0.5f), n + "the title fits");
                Assert.That(title.yMax, Is.LessThanOrEqualTo(card.yMax + 0.01f), n + "the title on the card");
                if (_levels.CardText(i).Length == 0) continue;
                Assert.That(InterfaceStyle.MeasureHeight(_levels.CardText(i), textStyle, text.width), Is.LessThanOrEqualTo(text.height + 0.5f), n + "the text fits");
                Assert.That(text.yMin, Is.GreaterThanOrEqualTo(title.yMax - 0.01f), n + "the text under the title");
                Assert.That(text.yMax, Is.LessThanOrEqualTo(card.yMax + 0.01f), n + "the text on the card");
            }
        }

        [UnityTest]
        public IEnumerator TheCards_OfAChestWithStatsOn_AFloorReward_AndASkillUpgrade_HoldTheWholeOfTheirText()
        {
            _levels.Offers.PopUp = false; // work order 065: this morning's cards at the right edge, with this morning's words, pinned here
            _levels.StartRun(RunSeed);
            yield return null;
            List<GeneratedLevel.PlacedProp> chests = _levels.Chests();
            Assume.That(chests.Count, Is.GreaterThan(0), "a chest on floor 1");
            Assert.That(_levels.OpenChest(chests[0]), Is.True);
            Assume.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.Chest));
            IReadOnlyList<Upgrade> offer = _levels.OfferedUpgrades;
            Assume.That(offer.Count, Is.GreaterThanOrEqualTo(2));
            // The two longest of the stats' cards, in this frame, before any card is made of the offer.
            offer[0].Kind = UpgradeKind.LongerBarrel;
            offer[0].Amount = 1f;
            offer[0].Tiered = true;
            offer[0].Tier = UpgradeTier.Common;
            offer[1].Kind = UpgradeKind.FlatDamage;
            offer[1].Amount = 5f;
            offer[1].Tiered = true;
            offer[1].Tier = UpgradeTier.Rare;
            EveryCardHoldsItsText("a chest");
            for (int i = 0; i < offer.Count; i++)
            {
                string title = _levels.CardTitle(i), text = _levels.CardText(i);
                bool whole = text.Contains(offer[i].Text) || (title + ": " + text.Split('\n')[0]) == offer[i].Text;
                Assert.That(whole, Is.True, "the whole of \"" + offer[i].Text + "\" on the card: \"" + title + "\" / \"" + text + "\"");
                StringAssert.Contains(offer[i].TierWord.ToUpperInvariant(), text, "and its tier's word");
            }
            Assert.That(_levels.CardTitle(0), Is.EqualTo("Longer barrel"), "its short name the title");
            Assert.That(_levels.PickUpgrade(0), Is.True);
            yield return null;

            _levels.FinishFloorForTests();
            yield return Frames(() => _levels.Choice == GeneratedLevels.ChoiceKind.FloorReward);
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.FloorReward));
            EveryCardHoldsItsText("a floor reward");
            for (int i = 0; i < _levels.CardCount; i++)
                StringAssert.Contains(SkillDefaults.Line(_player.Skills[_levels.OfferedSkills[i]].Name), _levels.CardText(i));
            Assert.That(_levels.PickReward(0), Is.True);
            if (_levels.Choice == GeneratedLevels.ChoiceKind.FocusChoice)
            {
                EveryCardHoldsItsText("the focus choice");
                Assert.That(_levels.PickFocusChoice(0), Is.True);
            }
            yield return null;

            _levels.FinishEliteRoomForTests();
            yield return Frames(() => _levels.Choice == GeneratedLevels.ChoiceKind.SkillUpgrade);
            Assume.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.SkillUpgrade), "the elite's upgrade on floor 2");
            EveryCardHoldsItsText("a skill upgrade");
            for (int i = 0; i < _levels.CardCount; i++)
                StringAssert.Contains(_levels.OfferedSkillUpgrades[i].Line, _levels.CardText(i));
            _levels.BackToPracticeRoom();
        }

        // ---------------- 2. A card's seen corner takes the click ----------------

        [UnityTest]
        public IEnumerator ACardsSeenCorner_IsOnTheCard_AndThePatchBesideTheOtherCorner_IsNot()
        {
            _levels.Offers.PopUp = false; // work order 065: this morning's cards at the right edge, with this morning's words, pinned here
            _levels.StartRun(RunSeed);
            yield return null;
            _levels.FinishFloorForTests();
            yield return Frames(() => _levels.Choice == GeneratedLevels.ChoiceKind.FloorReward);
            Assert.That(_levels.KineticCards(), Is.GreaterThan(0));
            Rect r = _levels.CardRect(0);
            float slant = InterfaceStyle.V.Slant, half = r.height * 0.5f;
            Assume.That(slant * (half - 2f), Is.GreaterThan(4f), "the card leans enough to tell");
            var upperTip = new Vector2(r.xMax + slant * half - 3f, r.y + 2f);       // seen: the top leans right
            var lowerTip = new Vector2(r.x - slant * half + 3f, r.yMax - 2f);       // seen: the bottom leans left
            var besideLower = new Vector2(r.xMax - 3f, r.yMax - 2f);                // the upright rectangle's corner: not seen
            var besideUpper = new Vector2(r.x + 3f, r.y + 2f);
            Assert.That(r.Contains(upperTip), Is.False, "outside the upright rectangle");
            Assert.That(_levels.CardHit(0, upperTip), Is.True, "the seen upper right tip is on the card");
            Assert.That(_levels.CardHit(0, lowerTip), Is.True, "and the lower left one");
            Assert.That(r.Contains(besideLower), Is.True);
            Assert.That(_levels.CardHit(0, besideLower), Is.False, "the patch where the upright rectangle's corner was is not");
            Assert.That(_levels.CardHit(0, besideUpper), Is.False);
            Assert.That(_levels.CardHit(0, r.center), Is.True, "its middle is");
            _levels.BackToPracticeRoom();
        }

        // ---------------- 3. The strokes and the blade after the camera ----------------

        [Test]
        public void TheStrokesAndTheBlade_WorkOutTheirPlaces_AfterTheCameraHasMoved()
        {
            int Order(Type t) => t.GetCustomAttribute<DefaultExecutionOrder>()?.order ?? 0;
            Assert.That(Order(typeof(SkillBar)), Is.GreaterThan(Order(typeof(CameraRig))), "the skill bar's LateUpdate runs after the camera's");
            Assert.That(Order(typeof(CastBar)), Is.GreaterThan(Order(typeof(CameraRig))), "and the cast bar's");
        }

        [UnityTest]
        public IEnumerator TheStrokesAndTheBlade_AreWhereThisFramesCameraShowsHer_AfterAMoveTheCameraFollows()
        {
            SkillBar bar = Object.FindAnyObjectByType<SkillBar>();
            Assert.That(bar, Is.Not.Null);
            CastBar cast = CastBar.Ensure();
            _made.Add(cast.gameObject);
            int shot = Slot("Long Shot");
            _player.EquipSkill(shot);
            _player.Teleport(NearA);
            yield return null;
            yield return UntilFree();
            // Far, and a charge begun there: in this frame's LateUpdates the camera follows her, then the strokes and the blade.
            // What they are and where the camera shows her are read at the frame's very end, as the frame is drawn.
            FieldInfo herTop = typeof(CastBar).GetField("_herTop", BindingFlags.NonPublic | BindingFlags.Instance);
            Vector2 feet = default;
            float headX = 0f, under = 0f;
            Rect h = default, f = default, blade = default;
            bool shown = false, bladeDrawn = false;
            int looked = 0;
            var end = new GameObject("LooksAtTheFramesEnd").AddComponent<LooksAtTheFramesEnd>();
            _made.Add(end.gameObject);
            end.Look = () =>
            {
                looked++;
                feet = Feet();
                // Work order 063: the strokes' gap, 0.30 m down the camera's picture under the lowest her boots show, as this frame's camera shows it.
                Vector3 down = Camera.main.WorldToScreenPoint(bar.BootsPoint - Camera.main.transform.up * InterfaceStyle.V.UnderFeetMetres);
                under = (Screen.height - down.y) / InterfaceStyle.Scale - feet.y;
                headX = Camera.main.WorldToScreenPoint(_player.Position + Vector3.up * (float)herTop.GetValue(cast)).x / InterfaceStyle.Scale;
                shown = bar.StrokesShown;
                h = bar.HealthStroke;
                f = bar.FocusStroke;
                bladeDrawn = cast.BladeDrawn;
                blade = cast.Blade;
                end.Look = null;
            };
            _player.Teleport(Practice);
            Assert.That(_player.PressSkill(shot, Practice + Vector3.forward * 6f), Is.EqualTo(IntentResult.Started));
            yield return null; // the frame's LateUpdates are done
            Assert.That(looked, Is.EqualTo(1), "looked at the frame's end");
            InterfaceValues v = InterfaceStyle.V;
            Assert.That(shown, Is.True);
            Assert.That(h.y - feet.y, Is.EqualTo(under).Within(0.5f), "under her feet as this frame's camera shows them (work order 063: by the gap in metres)");
            Assert.That((h.xMin + f.xMax) * 0.5f, Is.EqualTo(feet.x).Within(0.5f), "centred on her as this frame's camera shows her");
            Assert.That(bladeDrawn, Is.True, "the blade over her head");
            Assert.That(blade.center.x, Is.EqualTo(headX).Within(0.5f), "over her head as this frame's camera shows it");
            _player.ReleaseSkill(shot);
        }

        // ---------------- 4. A clean Backstep with the stats on ----------------

        List<PlayerReport> Hits(int slot) => _reports.Where(r => r.Report.Kind == PlayerReportKind.Hit && r.Report.Target == _a && r.Report.Slot == slot)
                                                     .Select(r => r.Report).ToList();

        /// <summary>One Backstep, its second press in its clean part, at the dummy carrying <paramref name="marks"/>: its shot's hit.</summary>
        IEnumerator CleanBackstep(RunModifiers modifiers, int marks, Action<PlayerReport> hit)
        {
            int slot = Slot("Backstep");
            _player.Modifiers = modifiers;
            _player.StartFresh();
            _player.Teleport(NearA);
            yield return null;
            yield return UntilFree();
            _a.TakeMarks();
            if (marks > 0) _a.AddMarks(marks, _player.Kit.MaxMarks, _player.Kit.MarkLifetime);
            Assume.That(_a.MarkCount, Is.EqualTo(marks));
            int had = Hits(slot).Count;
            Assert.That(_player.PressSkill(slot, _a.transform.position), Is.EqualTo(IntentResult.Started), "the hop");
            yield return Until(() => _player.IsRecastWindowOpen, 2f);
            yield return Until(() => _player.RecastPressWouldBeClean, 2f);
            Assume.That(_player.RecastPressWouldBeClean, Is.True, "in the clean part");
            Assert.That(_player.PressSkill(slot, _a.transform.position), Is.EqualTo(IntentResult.Started), "the clean press");
            yield return Until(() => Hits(slot).Count > had, 2f);
            Assert.That(Hits(slot).Count, Is.EqualTo(had + 1), "its shot hit the dummy");
            hit(Hits(slot).Last());
            yield return UntilFree();
        }

        [UnityTest]
        public IEnumerator ACleanBackstep_WithTheStatsOn_DealsWhatItDealtWithThemOff_AndMoreWithACritDamageStep()
        {
            _player.EquipSkill(Slot("Backstep"));
            var marks = new[] { 0, 1, 3 };
            var off = new int[marks.Length];
            var on = new int[marks.Length];
            for (int i = 0; i < marks.Length; i++)
            {
                yield return CleanBackstep(new RunModifiers { StatsAndTiers = false }, marks[i], h => off[i] = h.Damage);
                PlayerReport onHit = default;
                // On, no crit by a roll: the start's chance at 0 (the clean press is a crit without one).
                yield return CleanBackstep(new RunModifiers { StatsAndTiers = true, StatValues = new StatValues { StartCritChance = 0f } }, marks[i], h => onHit = h);
                on[i] = onHit.Damage;
                Assert.That(onHit.Crit, Is.True, "a clean press is a crit, " + marks[i] + " marks");
            }
            Assert.That(on, Is.EqualTo(off), "at 0, 1 and 3 marks the same as with the stats off (" + string.Join(", ", off) + "), not 1.5 times it");
            var step = new RunModifiers { StatsAndTiers = true, StatValues = new StatValues { StartCritChance = 0f } };
            step.Take(new Upgrade { Kind = UpgradeKind.CritDamage, Amount = 0.15f, Tiered = true });
            int more = 0;
            yield return CleanBackstep(step, 0, h => more = h.Damage);
            Assert.That(more, Is.GreaterThan(off[0]), "a crit damage step raises it");
        }

        // ---------------- 5. A reward drop she can reach ----------------

        List<GeneratedLevel.PlacedProp> BuiltProps(GeneratedLevel level) =>
            (List<GeneratedLevel.PlacedProp>)typeof(GeneratedLevel).GetField("_props", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(level);

        /// <summary>Props of the floor that block standing round a point (the test's own, put in the built props' list).</summary>
        static void Block(List<GeneratedLevel.PlacedProp> into, GeneratedLevel level, LevelRoom room, Vector3 round, float radius)
        {
            LevelPoint c = level.ToLevel(round);
            for (float x = -radius; x <= radius + 1e-3f; x += 1f)
            for (float y = -radius; y <= radius + 1e-3f; y += 1f)
                into.Add(new GeneratedLevel.PlacedProp { Room = room, Prop = LevelProp.Make(PropModel.Sarcophagus, new LevelPoint(c.X + x, c.Y + y), 0, PropPlace.Floor) });
        }

        bool PathFromHer(Vector3 to)
        {
            if (!NavMesh.SamplePosition(Flat(_player.Position), out NavMeshHit her, 2f, NavMesh.AllAreas)) return false;
            if (!NavMesh.SamplePosition(Flat(to), out NavMeshHit at, 0.6f, NavMesh.AllAreas)) return false;
            var path = new NavMeshPath();
            return NavMesh.CalculatePath(her.position, at.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
        }

        [UnityTest]
        public IEnumerator AnEliteRoomWhereNothingCanBeStoodAt_OpensItsChoiceAtOnce()
        {
            _levels.StartRun(RunSeed);
            yield return null;
            _health.Untouchable = true;
            GeneratedLevel level = _levels.Level;
            LevelRoom room = level.Layout.EliteRoom;
            Assume.That(room, Is.Not.Null, "an elite room on floor 1");
            List<GeneratedLevel.PlacedProp> props = BuiltProps(level);
            int before = props.Count;
            float cs = level.Layout.CellSize;
            Block(props, level, room, level.Middle(room), Mathf.Max(room.Rect.Width, room.Rect.Height) * cs * 0.5f + 1f);
            try
            {
                foreach (Enemy e in level.EliteRoomEnemies.Where(e => e != null && !e.IsDead)) e.Body.TakeHit(100000000);
                yield return Frames(() => _levels.EliteDrop != null || _levels.Choice != GeneratedLevels.ChoiceKind.None);
                Assert.That(_levels.EliteDrop, Is.Null, "no drop where she could not stand at it");
                Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.SkillUpgrade), "its choice opens at once, as with the drops off");
            }
            finally
            {
                props.RemoveRange(before, props.Count - before);
            }
            _levels.BackToPracticeRoom();
        }

        [UnityTest]
        public IEnumerator AnEliteRoomWhoseDeathPlacesAndMiddleAreBlocked_LeavesADropSheCanPathTo_OrItsChoice()
        {
            _levels.StartRun(RunSeed);
            yield return null;
            _health.Untouchable = true;
            GeneratedLevel level = _levels.Level;
            LevelRoom room = level.Layout.EliteRoom;
            Assume.That(room, Is.Not.Null, "an elite room on floor 1");
            List<GeneratedLevel.PlacedProp> props = BuiltProps(level);
            int before = props.Count;
            List<Enemy> alive = level.EliteRoomEnemies.Where(e => e != null && !e.IsDead).ToList();
            Assume.That(alive.Count, Is.GreaterThan(0));
            Block(props, level, room, level.Middle(room), 2f);
            foreach (Enemy e in alive) Block(props, level, room, e.transform.position, 1f);
            try
            {
                foreach (Enemy e in alive) e.Body.TakeHit(100000000);
                yield return Frames(() => _levels.EliteDrop != null || _levels.Choice != GeneratedLevels.ChoiceKind.None);
                RewardDrop drop = _levels.EliteDrop;
                if (drop == null) Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.SkillUpgrade), "no drop: its choice");
                else
                {
                    Assert.That(level.RoomAt(drop.Landing), Is.SameAs(room), "in the room");
                    LevelPoint at = level.ToLevel(drop.Landing);
                    Assert.That(props.All(p => p.Room != room || p.Prop.Footprint.DistanceTo(at) >= _levels.DropValues.Clearance - 1e-3), Is.True, "clear of every prop built");
                    Assert.That(PathFromHer(drop.Landing), Is.True, "a whole path from her to it");
                }
            }
            finally
            {
                props.RemoveRange(before, props.Count - before);
            }
            _levels.BackToPracticeRoom();
        }

        [UnityTest]
        public IEnumerator ADrop_NeverLandsOnALargePropsFootprint_AndAlwaysWhereSheCanPath()
        {
            GeneratedLevel level = null;
            for (ulong seed = 1; seed <= 24 && (level == null || !level.LargeProps.Any(p => p.Prop.Model == PropModel.Dais)); seed++)
            {
                _levels.Values = new LevelValues();
                level = _levels.Build(seed * 7919UL + 55UL, 1 + (int)(seed % 5));
                yield return null;
            }
            Assume.That(level != null && level.LargeProps.Any(p => p.Prop.Model == PropModel.Dais), Is.True, "a floor with a dais");
            float clearance = _levels.DropValues.Clearance, cs = level.Layout.CellSize;
            int landed = 0;
            foreach (LevelRoom room in level.LargeProps.Select(p => p.Room).Distinct())
            {
                List<GeneratedLevel.PlacedProp> large = level.LargeProps.Where(p => p.Room == room).ToList();
                CellRect r = room.Rect;
                for (float x = r.X * cs + 1f; x < (r.X + r.Width) * cs; x += 2.5f)
                for (float y = r.Y * cs + 1f; y < (r.Y + r.Height) * cs; y += 2.5f)
                {
                    Vector3 fell = level.ToWorld(new LevelPoint(x, y));
                    if (!_levels.TryLandingFor(fell, room, out Vector3 landing)) continue;
                    landed++;
                    LevelPoint at = level.ToLevel(landing);
                    Assert.That(level.RoomAt(landing), Is.SameAs(room), "in its room, from " + fell);
                    foreach (GeneratedLevel.PlacedProp p in large)
                        Assert.That(p.Prop.Footprint.DistanceTo(at), Is.GreaterThanOrEqualTo(clearance - 1e-3), "off " + p.Prop.Model + "'s footprint, from " + fell);
                    Assert.That(PathFromHer(landing), Is.True, "a whole path from her, from " + fell);
                }
            }
            Assert.That(landed, Is.GreaterThan(0), "drops landed");
            _levels.BackToPracticeRoom();
        }

        // ---------------- 6. The fog of a carved room ----------------

        [UnityTest]
        public IEnumerator TheFogOfACrossOrAnOctagon_IsBornOverItsOpenFloor()
        {
            ulong seed = 0;
            int floor = 0;
            LevelRoom carved = null;
            for (ulong s = 1; s <= 400 && carved == null; s++)
            {
                int f = 1 + (int)(s % 5);
                LevelLayout layout = LevelGenerator.Generate(s, f, new LevelValues());
                foreach (LevelRoom room in new[] { layout.EliteRoom, layout.ExitRoom })
                    if (room != null && room.SolidCount > 0 && (room.Shape == RoomShape.Cross || room.Shape == RoomShape.Octagon
                                                                     // Changed deliberately (work order 058): with the templates on the elite and the
                                                                     // exit room are stamped from a template (the cross hall, the throne arcade's cut
                                                                     // corners), whose outline is no rectangle either; a cross or an octagon they are
                                                                     // only when a stamp falls back, which no floor of 10,000 did.
                                                                     || room.Shape == RoomShape.Template))
                    {
                        carved = room;
                        seed = s;
                        floor = f;
                        break;
                    }
            }
            Assume.That(carved, Is.Not.Null, "an elite or exit room that is a cross, an octagon or a template");
            _levels.Values = new LevelValues();
            GeneratedLevel level = _levels.Build(seed, floor);
            LevelRoom here = level.Layout.Rooms.First(r => r.Order == carved.Order);
            Assert.That(here.Shape, Is.EqualTo(carved.Shape), "the same floor built");
            _player.Teleport(level.Middle(here));
            yield return null;
            yield return null;
            RoomMood.RoomParts parts = level.Mood.Rooms.First(p => p.Order == here.Order);
            ParticleSystem fog = parts.Fog;
            Assert.That(fog, Is.Not.Null);
            Assert.That(fog.main.maxParticles, Is.EqualTo(level.Values.Mood.FogMostPerRoom), "the room's budget kept");
            // Where they are born: the drift (up to 0.18 m/s) taken away, then simulated.
            ParticleSystem.MainModule main = fog.main;
            main.startSpeed = 0f;
            main.prewarm = false;
            fog.Simulate(6f, true, true);
            var all = new ParticleSystem.Particle[fog.main.maxParticles];
            int n = fog.GetParticles(all);
            Assume.That(n, Is.GreaterThan(10), "fog was made");
            int outside = 0;
            for (int i = 0; i < n; i++)
                if (level.RoomAt(Flat(all[i].position)) != here) outside++;
            Assert.That(outside, Is.Zero, outside + " of " + n + " fog particles born off the open floor of the " + here.Shape + " " + here.Letter + here.Order);
            _levels.BackToPracticeRoom();
        }

        // ---------------- 7. The opposite move key in a quick duck ----------------

        void UseKeyboardAndMouse()
        {
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _mouse = InputSystem.AddDevice<Mouse>();
            InputBindings b = _driver.ActiveBindings;
            _driver.InputSource = () =>
            {
                InputFrame? f = PlayerInputDriver.FrameFrom(_mouse, _keyboard, b);
                if (!f.HasValue) return null;
                InputFrame x = f.Value;
                x.CursorScreen = ScreenMiddle;
                return x;
            };
        }

        void Keys(params Key[] down) => InputSystem.QueueStateEvent(_keyboard, new KeyboardState(down));

        IEnumerator Press(params Key[] down)
        {
            Keys(down);
            yield return Frames(() => down.All(k => _keyboard[k].isPressed));
        }

        [UnityTest]
        public IEnumerator InAQuickDuck_TheOppositeMoveKey_PressedWithOneHeld_EndsIt_LettingGoOfAKey_DoesNot()
        {
            _feel.TakeCover = true;
            _feel.QuickDuck = true;
            LowProp(Practice, Vector3.right);
            yield return null;
            UseKeyboardAndMouse();
            InputBindings b = _driver.ActiveBindings;
            Assume.That(b.MoveLeft != Key.None && b.MoveRight != Key.None && b.MoveUp != Key.None && b.TakeCover != Key.None);
            yield return Press(b.MoveLeft, b.MoveUp);
            yield return null;
            yield return Press(b.MoveLeft, b.MoveUp, b.TakeCover); // the tap, left and up held
            yield return Frames(() => _player.IsQuickDucking);
            Assert.That(_player.IsQuickDucking, Is.True, "ducked by the key");
            Keys(b.MoveLeft, b.TakeCover); // up let go, left still down
            yield return Frames(() => !_keyboard[b.MoveUp].isPressed);
            for (int i = 0; i < 4; i++) yield return null;
            Assume.That(Time.time, Is.LessThan(_player.DuckUpAt), "before she would rise by herself");
            Assert.That(_player.IsQuickDucking, Is.True, "a key let go is no new press: still down");
            int ends = _reports.Count(r => r.Report.Kind == PlayerReportKind.DuckEnded);
            Keys(b.MoveLeft, b.TakeCover, b.MoveRight); // right pressed with left held: no direction at all
            yield return Frames(() => _keyboard[b.MoveRight].isPressed);
            yield return null; // whichever of the driver and her Updates ran first
            Assume.That(Time.time, Is.LessThan(_player.DuckUpAt), "before she would rise by herself");
            Assert.That(_player.IsQuickDucking, Is.False, "the opposite key going down ends it");
            Assert.That(_reports.Count(r => r.Report.Kind == PlayerReportKind.DuckEnded), Is.EqualTo(ends + 1));
            Assert.That(_reports.Last(r => r.Report.Kind == PlayerReportKind.DuckEnded).Report.DuckEnd, Is.EqualTo(DuckEnd.Moved));
        }

        // ---------------- 9, 10. The interface's values and textures between Play sessions ----------------

        [Test]
        public void TheInterfacesValues_AreMadeAnew_AndItsTextures_Destroyed_AsANewPlaySessionBegins()
        {
            MethodInfo reset = typeof(InterfaceStyle).GetMethod("ResetStatics", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(reset, Is.Not.Null);
            try
            {
                InterfaceStyle.Shared.Kinetic = false; // as unticked in the Inspector in Play
                Texture2D icon = InterfaceStyle.Icon("Quick Shot"), pips = InterfaceStyle.Pips;
                GUIStyle button = InterfaceStyle.Button(20);
                Texture2D normal = button.normal.background, hover = button.hover.background, active = button.active.background;
                Assume.That(icon != null && pips != null && normal != null && hover != null && active != null, Is.True, "the textures made");
                reset.Invoke(null, null); // as a new Play session begins, with domain reload off
                Assert.That(InterfaceStyle.Shared.Kinetic, Is.True, "the values as they start");
                Assert.That(icon == null, Is.True, "the icon destroyed");
                Assert.That(pips == null, Is.True, "the pips' texture destroyed");
                Assert.That(normal == null && hover == null && active == null, Is.True, "the buttons' textures destroyed");
                Assert.That(InterfaceStyle.Icon("Quick Shot"), Is.Not.Null, "and made again when asked");
            }
            finally
            {
                InterfaceStyle.Shared.Kinetic = true;
            }
        }

        // ---------------- 11. The icons decoded when a run starts ----------------

        [UnityTest]
        public IEnumerator AfterARunStarts_EveryIconIsDecoded_AndEquippingASkillDecodesNone()
        {
            SkillBar bar = Object.FindAnyObjectByType<SkillBar>();
            int onDisk = Resources.LoadAll<TextAsset>(InterfaceStyle.V.IconFolder.TrimEnd('/')).Length;
            Assume.That(onDisk, Is.GreaterThan(10), "the icons on disk");
            InterfaceStyle.ForgetIcons();
            _levels.StartRun(RunSeed); // in the same frame: no tick of the skill bar between
            Assert.That(InterfaceStyle.IconsLoaded, Is.EqualTo(onDisk), "every icon on disk, before any frame of play");
            int decoded = InterfaceStyle.IconsDecoded;
            yield return null;
            int slot = Enumerable.Range(0, _player.Skills.Count).First(s => !_player.IsEquipped(s) && InterfaceRules.IconName(_player.Skills[s].Name).Length > 0
                                                                           && _player.Skills[s].Name != "Long Shot");
            _player.EquipSkill(slot);
            bar.Tick();
            yield return null;
            Assert.That(InterfaceStyle.IconsDecoded, Is.EqualTo(decoded), "equipping " + _player.Skills[slot].Name + " decodes nothing");
            Assert.That(InterfaceStyle.Icon(_player.Skills[slot].Name), Is.Not.Null, "and its icon shows");
            _levels.BackToPracticeRoom();
        }

        // ---------------- 12. Small ones ----------------

        [UnityTest]
        public IEnumerator ThePauseScreensLists_AreMadeWhenItOpens_NotEveryFrame()
        {
            _hud = RunHud.Ensure();
            _levels.StartRun(RunSeed);
            yield return null;
            int made = _hud.PauseListsMade;
            Assert.That(_hud.Escape(), Is.True);
            Assert.That(_levels.Paused, Is.True);
            for (int i = 0; i < 10; i++) yield return null;
            Assert.That(_hud.PauseListsMade, Is.EqualTo(made + 1), "made once as it opened");
            Assert.That(_hud.Escape(), Is.True);
            yield return null;
            Assert.That(_hud.Escape(), Is.True);
            for (int i = 0; i < 3; i++) yield return null;
            Assert.That(_hud.PauseListsMade, Is.EqualTo(made + 2), "and once more when it opens again");
            Assert.That(_hud.Escape(), Is.True);
        }

        [UnityTest]
        public IEnumerator ThePromptsText_IsTheSameStringFrameAfterFrame()
        {
            _hud = RunHud.Ensure();
            yield return null;
            yield return null;
            string a = _hud.PromptText;
            Assert.That(a, Is.EqualTo("F5  start a run"));
            yield return null;
            Assert.That(_hud.PromptText, Is.SameAs(a), "kept while its parts are unchanged: the skin's cache keys on it");
        }

        [UnityTest]
        public IEnumerator TheActClearedSound_Plays_WhenTheElitesUpgradeLeftLyingComesFirst()
        {
            _sounds = PlaceholderSounds.Ensure();
            _levels.StartRun(RunSeed);
            yield return null;
            _health.Untouchable = true;
            for (int floor = 1; floor < _levels.Values.FloorsPerAct; floor++) yield return OnToTheNextFloor();
            List<Enemy> alive = _levels.Level.EliteRoomEnemies.Where(e => e != null && !e.IsDead).ToList();
            Assume.That(alive.Count, Is.GreaterThan(0), "an elite room on the last floor");
            foreach (Enemy e in alive) e.Body.TakeHit(100000000);
            yield return Frames(() => _levels.EliteDrop != null || _levels.Choice != GeneratedLevels.ChoiceKind.None);
            Assume.That(_levels.EliteDrop, Is.Not.Null, "the elite's drop lies");
            _played.Clear();
            _levels.FinishFloorForTests();
            yield return Frames(() => _levels.Choice != GeneratedLevels.ChoiceKind.None || _levels.ActCleared);
            Assume.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.SkillUpgrade), "the elite's upgrade first");
            for (int i = 0; i < 3; i++) yield return null;
            Assert.That(_played.Count(r => r.Sound == PlaceholderSound.ActCleared), Is.Zero, "not yet: the act has not ended");
            Assert.That(_levels.PickSkillUpgrade(0), Is.True);
            Assert.That(_levels.ActCleared, Is.True);
            for (int i = 0; i < 3; i++) yield return null;
            Assert.That(_played.Count(r => r.Sound == PlaceholderSound.ActCleared), Is.EqualTo(1),
                        "heard where the act ends: " + string.Join(", ", _played.Select(r => r.Sound)));
        }

        [UnityTest]
        public IEnumerator AnEpicOffersSecondBell_DoesNotRing_AfterItsChoiceIsGone()
        {
            _sounds = PlaceholderSounds.Ensure();
            AudioClip bell = _sounds.ClipOf(PlaceholderSound.RareOffer);
            _levels.RunValues.StatsAndTiers = true;
            _levels.StartRun(RunSeed);
            yield return null;
            _levels.RunValues.Stats.ChestOdds = new[] { 0f, 0f, 100f };
            List<GeneratedLevel.PlacedProp> chests = _levels.Chests();
            Assume.That(chests.Count, Is.GreaterThan(0));
            _voiced.Clear();
            Assert.That(_levels.OpenChest(chests[0]), Is.True);
            Assume.That(_levels.OfferedUpgrades.Any(u => u.Tiered && u.Tier == UpgradeTier.Epic), Is.True, "an epic card on offer");
            yield return Until(() => _voiced.Count(v => v.Clip == bell) >= 1, 2f);
            Assert.That(_voiced.Count(v => v.Clip == bell), Is.EqualTo(1), "the first bell");
            _levels.BackToPracticeRoom(); // F7, inside the guard
            yield return new WaitForSecondsRealtime(0.4f);
            yield return null;
            Assert.That(_voiced.Count(v => v.Clip == bell), Is.EqualTo(1), "no second bell in the practice room");
        }

        [UnityTest]
        public IEnumerator TheMood_RebuiltAfterTheLargePropsSwitchIsToggled_PlansTheLargePropsTheLevelHas()
        {
            GeneratedLevel level = null;
            for (ulong seed = 1; seed <= 12 && (level == null || level.LargeProps.Count == 0); seed++)
            {
                _levels.Values = new LevelValues();
                level = _levels.Build(seed * 104729UL + 5UL, 3);
                yield return null;
            }
            Assume.That(level != null && level.LargeProps.Count > 0, Is.True, "a floor with large props");
            RoomMood mood = level.Mood;
            int builds = mood.Builds;
            level.Values.LargeProps = false; // unticked in the Inspector
            level.Values.Mood.FogSize += 0.05f; // and a number of the mood changed: it is built again
            yield return null;
            yield return null;
            Assert.That(mood.Builds, Is.EqualTo(builds + 1), "built again");
            Assert.That(mood.Plan.Rooms.Sum(r => r.LargeProps.Count), Is.EqualTo(level.LargeProps.Count), "the plan has the large props the level built");
            Assert.That(level.Values.LargeProps, Is.False, "the switch as it was set");
            _levels.BackToPracticeRoom();
        }

        // ---------------- 13. To check ----------------

        [UnityTest]
        public IEnumerator AfterASecondLookThatGaveTheSameSkill_TheFooterNoLongerOffersADraw()
        {
            _levels.StartRun(RunSeed);
            yield return null;
            _levels.Modifiers.Meta.RedrawsLeft = 1; // Second Look, as the trial's piece gives it
            int left = Enumerable.Range(0, _player.Skills.Count).First(s => !_player.IsEquipped(s));
            for (int slot = 0; slot < _player.Skills.Count; slot++)
                if (slot != left) _player.EquipSkill(slot); // one skill left to offer
            _levels.FinishFloorForTests();
            yield return Frames(() => _levels.Choice == GeneratedLevels.ChoiceKind.FloorReward);
            Assert.That(_levels.OfferedSkills, Is.EqualTo(new[] { left }));
            _levels.KineticCards();
            StringAssert.Contains("draws again", _levels.CardsFooter, "the draw is there");
            Assert.That(_levels.RedrawOffer(), Is.True);
            Assert.That(_levels.OfferedSkills, Is.EqualTo(new[] { left }), "the same skill back");
            _levels.KineticCards();
            Assert.That(_levels.CardsFooter, Does.Not.Contain("draws again"), "spent: the footer says so");
            _levels.BackToPracticeRoom();
        }

        [UnityTest]
        public IEnumerator TheSkillsAsset_KeepsTheFlatDamageWeightsInitialiser_SoAHitAddsHerFlatDamageByName()
        {
            Assert.That(_player.SkillSet, Is.Not.Null, "the settings asset");
            Assert.That(_player.SkillSet.Skills.All(s => s.FlatDamageWeight < 0f), Is.True, "no weight set in the asset: the field's -1 kept");
            var m = new RunModifiers { StatsAndTiers = true, StatValues = new StatValues { StartCritChance = 0f } };
            m.Take(new Upgrade { Kind = UpgradeKind.FlatDamage, Amount = 4f, Tiered = true });
            _player.Modifiers = m;
            int quick = Slot("Quick Shot");
            _player.EquipSkill(quick);
            _player.Teleport(NearA);
            yield return null;
            yield return UntilFree();
            Assert.That(_player.PressSkill(quick, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => Hits(quick).Count > 0, 2f);
            Assert.That(Hits(quick).Count, Is.GreaterThan(0));
            Assert.That(Hits(quick)[0].Damage, Is.EqualTo(_player.Skills[quick].Damage + 4), "Quick Shot's weight by name, 1: all of it");
        }

        [UnityTest]
        public IEnumerator APowderKeg_AimedAtACarvedBlock_LandsOnTheOpenFloorShortOfIt()
        {
            GeneratedLevel level = null;
            LevelRoom room = null;
            Vector3 stand = default, aim = default;
            for (ulong seed = 1; seed <= 12 && room == null; seed++)
            {
                _levels.Values = new LevelValues();
                level = _levels.Build(seed * 31UL + 7UL, 2);
                yield return null;
                float cs = level.Layout.CellSize;
                foreach (LevelRoom r in level.Layout.Rooms)
                {
                    if (r.SolidCount == 0 || room != null) continue;
                    foreach ((int x, int y) in r.SolidList)
                    {
                        // An open cell of the room beside the block, two cells deep, on the floor's NavMesh.
                        foreach ((int dx, int dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                        {
                            int ox = x + dx * 2, oy = y + dy * 2;
                            if (level.Layout.RoomAt((ox + 0.5) * cs, (oy + 0.5) * cs) != r || level.Layout.RoomAt((x + dx + 0.5) * cs, (y + dy + 0.5) * cs) != r) continue;
                            Vector3 p = level.ToWorld(new LevelPoint((ox + 0.5) * cs, (oy + 0.5) * cs));
                            if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 0.3f, NavMesh.AllAreas)) continue;
                            room = r;
                            stand = Flat(hit.position);
                            aim = level.ToWorld(new LevelPoint((x + 0.5) * cs, (y + 0.5) * cs));
                            break;
                        }
                        if (room != null) break;
                    }
                }
            }
            Assume.That(room, Is.Not.Null, "a carved block with open floor two cells deep beside it");
            int keg = Slot("Powder Keg");
            _player.EquipSkill(keg);
            _player.Teleport(stand);
            yield return null;
            yield return UntilFree();
            Vector3 before = _player.LastKegPoint;
            Assert.That(_player.PressSkill(keg, aim), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _player.LastKegPoint != before, 3f);
            Vector3 landed = _player.LastKegPoint;
            Assert.That(landed, Is.Not.EqualTo(before), "it landed");
            Debug.Log(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                                    "[055] Powder Keg aimed at a carved block of {0}{1} ({2}): from ({3:0.0}, {4:0.0}) at ({5:0.0}, {6:0.0}), landed at ({7:0.0}, {8:0.0}), {9:0.00} m from her",
                                    room.Letter, room.Order, room.Shape, stand.x, stand.z, aim.x, aim.z, landed.x, landed.z, Vector3.Distance(Flat(landed), stand)));
            Assert.That(level.RoomAt(landed), Is.SameAs(room), "on an open cell of the room, not on the block");
            Assert.That(Vector3.Distance(Flat(landed), stand), Is.LessThan(Vector3.Distance(Flat(aim), stand) - 1f), "short of the block");
            _levels.BackToPracticeRoom();
        }
    }
}
