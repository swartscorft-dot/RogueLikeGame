using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using AllocExt = UnityEngine.TestTools.Constraints.ConstraintExtensions;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 068 (his words of the evening of 2026-11-21), in the ControlRoom scene through the intents: with an offer
    /// open, nothing of the fight's interface is drawn after the offer (its veil and cards), and the pause menu is drawn after
    /// the offer (every OnGUI in the scene run by hand in the engine's order, <see cref="ImguiByHand"/>); the plate at the top
    /// left says her health, and stands, as the strokes and the combo bar do, on screen at 1920 by 1080 and at 1280 by 720,
    /// clear of the strip, the map and the run box; the strokes two thirds of work order 063's width; a hit she takes flashes
    /// the plate; a full gauge shows its key; nothing is allocated in a frame with the plate up.
    /// </summary>
    public class InterfaceConceptPlayTests
    {
        const ulong RunSeed = 0x0E117E17UL;

        PlayerCharacter _player;
        PlayerHealth _health;
        SkillBar _bar;
        GeneratedLevels _levels;
        RunHud _hud;
        CastBar _cast;
        EnemyBars _bars;
        FloorMap _map;
        TimingFeedback _feedback;
        PlayerSigns _signs;

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
            _bar = Object.FindAnyObjectByType<SkillBar>();
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
            InterfaceStyle.ScreenForPictures = Vector2Int.zero;
            if (_health != null) _health.Untouchable = false;
            if (_levels != null)
            {
                if (_levels.Paused) _levels.Resume();
                _levels.BackToPracticeRoom();
            }
            foreach (MonoBehaviour m in new MonoBehaviour[] { _signs, _feedback, _map, _bars, _cast, _hud, _levels })
                if (m != null) Object.Destroy(m.gameObject);
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        static IEnumerator Until(System.Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        void Hurt(int damage)
        {
            _health.Untouchable = false;
            _health.TakeHit(damage);
            _health.Untouchable = true;
        }

        // ---------------- 1. The order they are drawn in ----------------

        [UnityTest]
        public IEnumerator WithAnOfferOpen_NothingOfTheFightsInterfaceIsDrawnAfterIt_AndThePauseMenuIsDrawnAfterTheOffer()
        {
            if (!ImguiByHand.Available) Assert.Inconclusive("the engine's IMGUI entry points moved: the order cannot be run by hand");
            _levels.StartRun(RunSeed);
            yield return null;
            Hurt(5); // her numbers up
            _levels.FinishFloorForTests();
            yield return Until(() => _levels.Choice == GeneratedLevels.ChoiceKind.FloorReward, 3f);
            Assume.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.FloorReward), "an offer open");
            _bar.Tick();
            _cast.Tick();
            List<ImguiByHand.Drawn> order = ImguiByHand.Draw(null);
            int offer = order.FindIndex(d => d.Piece == _levels);
            Assert.That(offer, Is.GreaterThanOrEqualTo(0), "the offer drawn: " + ImguiByHand.Describe(order));
            Assert.That(order[offer].Depth, Is.EqualTo(InterfaceDepth.Offer));
            for (int i = offer + 1; i < order.Count; i++)
                Assert.That(order[i].Piece is RunHud && _levels.Paused, Is.True,
                            order[i].Piece.GetType().Name + " is drawn after the offer, over its cards: " + ImguiByHand.Describe(order));
            Assert.That(order.FindIndex(d => d.Piece == _bar), Is.LessThan(offer), "the strip, the strokes and the plate under the offer");
            Assert.That(order.FindIndex(d => d.Piece == _hud), Is.LessThan(offer), "the run box under the offer");

            // The pause menu (a choice refuses the pause: the offer taken first) over everything, the offer's depth below its own.
            bool picked = false;
            yield return Until(() => picked = _levels.PickReward(0), 3f); // past the guard
            Assert.That(picked, Is.True, "the offer taken");
            yield return null;
            if (_levels.Choice == GeneratedLevels.ChoiceKind.FocusChoice) // the focus choice after the first floor
            {
                yield return Until(() => _levels.PickFocusChoice(0), 3f);
                yield return null;
            }
            Assert.That(_levels.Pause(), Is.True, "paused");
            yield return null;
            order = ImguiByHand.Draw(null);
            int hud = order.FindIndex(d => d.Piece == _hud);
            Assert.That(hud, Is.EqualTo(order.Count - 1), "the pause menu drawn last, over everything: " + ImguiByHand.Describe(order));
            Assert.That(order[hud].Depth, Is.EqualTo(InterfaceDepth.Screens));
            Assert.That(order[hud].Depth, Is.LessThan(InterfaceDepth.Offer), "a screen over an offer");
            _levels.Resume();
        }

        [Test]
        public void WorkOrder071sFocusEdge_IsUnderEveryPieceOfTheInterface()
        {
            Assert.That(FocusEdge.Depth, Is.EqualTo(InterfaceDepth.FocusEdge), "the one place of the depths knows the screen's teal edge");
            Assert.That(InterfaceDepth.FocusEdge, Is.GreaterThan(InterfaceDepth.EnemyBars), "drawn first: under the enemies' bars and everything after them");
        }

        // ---------------- 2. The plate and the strokes ----------------

        [UnityTest]
        public IEnumerator ThePlate_SaysHerHealth_FlashesWhenSheIsHit_AndShowsTheKeyOnAFullGauge()
        {
            _bar.Tick();
            Assert.That(_bar.PlateShown, Is.True, "the plate at the top left");
            Assert.That(_bar.PlateNumber, Is.EqualTo(_health.Current + " / " + _health.Max), "her health over its most");
            Hurt(17);
            yield return null;
            _bar.Tick();
            Assert.That(_bar.PlateNumber, Is.EqualTo(_health.Current + " / " + _health.Max), "after a hit too");
            Assert.That(_bar.HitFlash, Is.GreaterThan(0.5f), "the hit flashes the plate and the strokes");
            Assert.That(_bar.LostAlpha, Is.GreaterThan(0f), "what she just lost shows white");
            Assert.That(_bar.PlateFocusFull, Is.False);
            _player.FillFocus();
            yield return null;
            _bar.Tick();
            Assert.That(_bar.PlateFocusFull, Is.True, "a full gauge is white-hot with its key");
            Assert.That(() => { for (int i = 0; i < 300; i++) _bar.Tick(); }, NotAllocating, "nothing allocated in a frame with the plate up");
            InterfaceStyle.Shared.TopPlate = false;
            _bar.Tick();
            Assert.That(_bar.PlateShown, Is.False, "off: only the strokes, as before");
            InterfaceStyle.Shared.TopPlate = true;
        }

        [UnityTest]
        public IEnumerator ThePlate_TheStrokes_AndTheComboBar_StandOnScreen_ClearOfTheStripTheMapAndTheRunBox_AtBothSizes()
        {
            _levels.StartRun(RunSeed);
            yield return null;
            foreach (Vector2Int size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720) })
            {
                InterfaceStyle.ScreenForPictures = size;
                _bar.Tick();
                _cast.Tick();
                InterfaceValues v = InterfaceStyle.V;
                float viewWidth = size.x / InterfaceStyle.Scale;
                string at = size.x + " by " + size.y;
                Rect plate = _bar.PlateWhole;
                Assert.That(plate.xMin, Is.GreaterThanOrEqualTo(0f), at);
                Assert.That(plate.yMax, Is.LessThan(v.LayoutHeight * 0.25f), at + ": the plate in the top quarter");
                Rect strip = R(InterfaceRules.WholeStrip(_bar.SlotCount, viewWidth, v));
                Rect combo = R(InterfacePlate.ComboWhole(SkillLoadout.KeyCount, viewWidth, v));
                // The map's corner and the run box (the 720 layout's pixels made the 1080 layout's).
                float k = v.LayoutHeight / SkillBar.LayoutHeight;
                Rect map = new Rect(viewWidth - 278f * k, 0f, 278f * k, 200f * k);
                Rect run = RunHud.RunBoxKinetic(viewWidth / k, 6);
                run = new Rect(run.x * k, run.y * k, run.width * k, run.height * k);
                Assert.That(plate.Overlaps(map) || plate.Overlaps(run), Is.False, at + ": the plate clear of the map and the run box");
                Assert.That(plate.Overlaps(strip) || plate.Overlaps(combo), Is.False, at + ": and of the strip and the combo bar");
                Assert.That(combo.Overlaps(strip), Is.False, at + ": the combo bar clear of the strip");
                Assert.That(combo.xMin >= 0f && combo.xMax <= viewWidth && combo.yMax <= v.LayoutHeight, Is.True, at + ": the combo bar on screen");
                Assert.That(_bar.StrokesShown, Is.True);
                Assert.That(_bar.HealthStroke.width, Is.EqualTo(v.StrokeWidth * InterfaceRules.Fight(v) * 2f / 3f).Within(1e-3f), at + ": the strokes two thirds of 063's");
                Assert.That(_bar.HealthStroke.height, Is.EqualTo(v.StrokeHeight * InterfaceRules.Fight(v)).Within(1e-3f), at + ": no taller");
                Assert.That(_bar.HealthStroke.yMax, Is.LessThan(strip.yMin), at + ": the strokes over the strip");
                // The plate's number inside its plate, as the heavy italic measures it.
                GUIStyle number = InterfaceStyle.Text(true, v.PlateNumberSize, TextAnchor.MiddleLeft);
                Vector2 m = InterfaceStyle.Measure("100 / 100", number);
                Rect h = _bar.PlateHealthRect;
                Assert.That(InterfacePlate.HalfLean(h.height, v.PlateSlant) + 14f + m.x, Is.LessThan(h.width), at + ": her widest number inside the red plate");
                Assert.That(m.y, Is.LessThan(h.height * 1.25f), at + ": and its height");
            }
        }

        static Rect R(UiBox b) => new Rect(b.X, b.Y, b.W, b.H);
    }
}
