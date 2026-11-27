using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using AllocExt = UnityEngine.TestTools.Constraints.ConstraintExtensions;
using AllocIs = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// The lancer's warning mark in play (work order 145; Kristian, 2026-11-27: "Utenomfra, men et lite varsel ikon kan være nice.
    /// Et utropstegn"), in the ControlRoom scene's practice room with the game's own follow camera: a lancer 13 m below her (off
    /// his screen) begins its charge and the mark is up at the bottom edge straight under her, through the wind-up and into the run;
    /// it goes when the lancer comes on screen and does not come back in that charge; one 15 m to her side has its mark at that
    /// side's edge, level with her, and it goes when the lancer dies; one that begins 10.5 m above her (on screen) gets none, also
    /// when she walks away from it; a shooter drawing off screen gets none; with the switch off nothing is followed or drawn, and
    /// the mark's frame changes nothing of the lancer (the game as before, field for field); its frame allocates nothing while a
    /// mark is up. The lancers are made by the factory as the practice room's key makes one. Nobody has seen or played it: these
    /// are numbers.
    /// </summary>
    public class LancerWarningMarkPlayTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        const float Below = 13f;   // m south of her: off screen (the camera shows about 7.7 m toward its bottom), in the lancer's band
        const float Side = 15f;    // m east of her: off screen (about 13.3 m at 16:9 and 10 m at 4:3), in its band
        const float Above = 10.5f; // m north of her: on screen (about 11.3 m toward its top at any shape), in its band
        const float Nearer = 6f;   // m she steps toward the lancer below: it is then 7 m from her, on screen
        const float Away = 8f;     // m she steps south, away from the lancer above: it is then 18.5 m from her, off screen

        PlayerCharacter _player;
        PlayerHealth _health;
        EnemyBrute _practiceEnemy;
        GeneratedLevels _levels;
        LancerWarningMarks _marks;
        Transform _root;
        Vector3 _her;
        bool _onBefore;
        int _made;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _onBefore = LancerWarningMark.On;
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            _health.Untouchable = true;
            _practiceEnemy = Object.FindAnyObjectByType<EnemyBrute>();
            Assert.That(_practiceEnemy, Is.Not.Null, "the practice enemy, to copy");
            _practiceEnemy.enabled = false;
            _levels = GeneratedLevels.Ensure();
            _marks = LancerWarningMarks.Ensure();
            Assume.That(FindStand(out _her), Is.True, "a place on the practice floor with room below, beside and above her");
            _player.Teleport(_her);
            yield return null;
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            LancerWarningMark.On = _onBefore;
            if (_root != null) Object.Destroy(_root.gameObject);
            _root = null;
            if (_marks != null) Object.Destroy(_marks.gameObject);
            _marks = null;
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            if (_health != null) _health.Untouchable = false;
            if (_player != null) _player.GetComponent<PlayerInputDriver>().InputSource = null;
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        // ---------------- Helpers ----------------

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        static float Room(Vector3 from, Vector3 dir, float max)
        {
            if (!NavMesh.SamplePosition(Flat(from), out NavMeshHit start, 1f, NavMesh.AllAreas)) return 0f;
            if (NavMesh.Raycast(start.position, start.position + dir * max, out NavMeshHit hit, NavMesh.AllAreas))
                return Vector2.Distance(new Vector2(hit.position.x, hit.position.z), new Vector2(start.position.x, start.position.z));
            return Mathf.Min(max, Cover.ClearDistance(Flat(start.position), dir, max, new LancerValues().SightHeight));
        }

        /// <summary>A place on the practice floor with clear, walkable room for the three lancers' lines and her steps.</summary>
        static bool FindStand(out Vector3 stand)
        {
            for (int z = 0; z <= 12; z += 2)
                for (int x = -10; x <= 4; x += 2)
                {
                    stand = new Vector3(x, 0f, z);
                    if (Room(stand, Vector3.back, Below + 2f) >= Below + 2f && Room(stand, Vector3.right, Side + 1.5f) >= Side + 1.5f
                        && Room(stand, Vector3.forward, Above + 1f) >= Above + 1f)
                        return true;
                }
            stand = Vector3.zero;
            return false;
        }

        EnemyLancer Lancer(Vector3 at)
        {
            if (_root == null) _root = new GameObject("LancerWarningMarkSpawns").transform;
            Enemy e = EnemyFactory.Make(EnemyKind.Lancer, Flat(at), _root, _practiceEnemy, "LancerMark_" + (++_made));
            Assert.That(e, Is.InstanceOf<EnemyLancer>());
            return (EnemyLancer)e;
        }

        static bool InWindup(EnemyLancer l) => l != null && l.State == EnemyState.Windup && l.Attack != null && l.Attack.Name == LancerValues.Charge;

        static bool OnScreen(Enemy e) => RangedOpening.OnScreen(e.transform.position, LancerWarningMark.ScreenMargin);

        /// <summary>Her ground point on the screen, layout px of a view 1080 high (y down), and the view's width in them.</summary>
        Vector2 HerOnScreen(out float viewWidth)
        {
            Camera cam = Camera.main;
            float scale = InterfaceStyle.ScreenHeight / 1080f;
            viewWidth = InterfaceStyle.ScreenWidth / scale;
            Vector3 s = cam.WorldToScreenPoint(_player.Position);
            return new Vector2(s.x / scale, (InterfaceStyle.ScreenHeight - s.y) / scale);
        }

        string Where(EnemyLancer l) => string.Format(Inv, "lancer at ({0:0.0}, {1:0.0}), she at ({2:0.0}, {3:0.0}), on screen {4}, shown {5}, followed {6}, mark ({7:0.0}, {8:0.0}); screen {9}x{10}",
            l.transform.position.x, l.transform.position.z, _player.Position.x, _player.Position.z, OnScreen(l), _marks.Shown, _marks.Followed,
            _marks.At(Math.Max(0, _marks.MarkOf(l))).x, _marks.At(Math.Max(0, _marks.MarkOf(l))).y, InterfaceStyle.ScreenWidth, InterfaceStyle.ScreenHeight);

        // ---------------- Below her: up at the bottom edge, gone once seen ----------------

        [UnityTest]
        public IEnumerator ALancerBelowHer_OffScreen_BeginsItsCharge_TheMarkIsUpAtTheBottomEdge_UnderHer_AndGoesWhenItIsOnScreen()
        {
            Assert.That(LancerWarningMark.On, Is.True, "on by default");
            EnemyLancer l = Lancer(_her + Vector3.back * Below);
            yield return Until(() => InWindup(l), 5f);
            Assert.That(InWindup(l), Is.True, "it winds up its charge 13 m below her");
            Assert.That(OnScreen(l), Is.False, "off his screen as it begins");
            _marks.Tick(); // a test's frame resumes before the LateUpdates: the marks' frame now, as the engine would run it next
            Assert.That(_marks.Shown, Is.EqualTo(1), "one mark: " + Where(l));
            int i = _marks.MarkOf(l);
            Assert.That(i, Is.GreaterThanOrEqualTo(0), "its own");
            Vector2 her = HerOnScreen(out float w);
            LancerWarningMark.Bounds(w, 1080f, out float left, out _, out float right, out float bottom);
            Vector2 at = _marks.At(i);
            Debug.Log("LANCERMARK below: " + Where(l));
            Assert.That(at.y, Is.EqualTo(bottom).Within(0.5f), "at the bottom edge, over the skill strip");
            Assert.That(at.x, Is.EqualTo(Mathf.Clamp(her.x, left, right)).Within(2f), "straight under her: the lancer is straight below");

            // Through the wind-up, still off screen: still up.
            yield return null;
            Assert.That(InWindup(l) && !OnScreen(l), Is.True);
            _marks.Tick();
            Assert.That(_marks.Shown, Is.EqualTo(1), "it stays through the wind-up");

            // She steps toward it: it is on her screen, the mark goes.
            _player.Teleport(_her + Vector3.back * Nearer);
            yield return Until(() => OnScreen(l), 1f);
            Assert.That(OnScreen(l), Is.True, "7 m from her: on screen");
            _marks.Tick();
            Assert.That(_marks.Shown, Is.EqualTo(0), "seen: gone (" + Where(l) + ")");

            // Back where she was: off screen again in the same charge, and no mark again.
            _player.Teleport(_her);
            yield return null;
            yield return null;
            bool stillInIt = InWindup(l) || l.IsCharging;
            _marks.Tick();
            Debug.Log("LANCERMARK below, back off screen: still in its charge " + stillInIt + "; " + Where(l));
            Assert.That(_marks.Shown, Is.EqualTo(0), "not again in the same charge");
        }

        [UnityTest]
        public IEnumerator ALancerBelowHer_ItsMarkStays_IntoTheRun_WhileItIsStillOffScreen()
        {
            EnemyLancer l = Lancer(_her + Vector3.back * Below);
            yield return Until(() => InWindup(l), 5f);
            Assert.That(InWindup(l) && !OnScreen(l), Is.True, "its wind-up begun off screen");
            yield return Until(() => l.IsCharging, 3f);
            Assert.That(l.IsCharging, Is.True, "the charge sets off");
            _marks.Tick();
            if (!OnScreen(l)) Assert.That(_marks.Shown, Is.EqualTo(1), "running, still off screen: up (" + Where(l) + ")");
            yield return Until(() => !l.IsCharging, 3f);
            _marks.Tick();
            Assert.That(_marks.Shown, Is.EqualTo(0), "the charge over: gone");
            Assert.That(_marks.Followed, Is.EqualTo(0), "let go");
        }

        // ---------------- Beside her: at that side's edge; gone when it dies ----------------

        [UnityTest]
        public IEnumerator ALancerToHerSide_OffScreen_HasItsMarkAtTheRightEdge_LevelWithHer_AndItGoesWhenTheLancerDies()
        {
            EnemyLancer l = Lancer(_her + Vector3.right * Side);
            yield return Until(() => InWindup(l), 5f);
            Assert.That(InWindup(l), Is.True, "it winds up 15 m to her side");
            Assert.That(OnScreen(l), Is.False, "off his screen");
            _marks.Tick();
            int i = _marks.MarkOf(l);
            Assert.That(i, Is.GreaterThanOrEqualTo(0), "its mark is up: " + Where(l));
            Vector2 her = HerOnScreen(out float w);
            LancerWarningMark.Bounds(w, 1080f, out _, out float top, out float right, out float bottom);
            Vector2 at = _marks.At(i);
            Debug.Log("LANCERMARK side: " + Where(l));
            Assert.That(at.x, Is.EqualTo(right).Within(0.5f), "at the right edge");
            Assert.That(at.y, Is.EqualTo(Mathf.Clamp(her.y, top, bottom)).Within(2f), "level with her: the lancer is straight to her right");
            l.GetComponent<Damageable>().TakeHit(100000000);
            yield return null;
            _marks.Tick();
            Assert.That(l.IsDead, Is.True);
            Assert.That(_marks.Shown, Is.EqualTo(0), "dead: gone");
        }

        // ---------------- Begun on screen: none ----------------

        [UnityTest]
        public IEnumerator ALancerThatBeginsOnScreen_GetsNoMark_AlsoWhenSheWalksAwayFromIt()
        {
            EnemyLancer l = Lancer(_her + Vector3.forward * Above);
            yield return Until(() => InWindup(l), 5f);
            Assert.That(InWindup(l), Is.True, "it winds up 10.5 m above her");
            Assume.That(OnScreen(l), Is.True, "on his screen as it begins (the camera shows about 11.3 m toward its top)");
            _marks.Tick();
            Assert.That(_marks.Shown, Is.EqualTo(0), "begun on screen: none");
            Assert.That(_marks.Followed, Is.EqualTo(1), "followed through the charge all the same, so it is not taken for a new one");
            _player.Teleport(_her + Vector3.back * Away);
            yield return Until(() => !OnScreen(l), 1f);
            Assert.That(OnScreen(l), Is.False, "18.5 m away: off screen");
            _marks.Tick();
            Assert.That(_marks.Shown, Is.EqualTo(0), "off screen in a charge begun on it: still none (" + Where(l) + ")");
        }

        // ---------------- Other kinds: none ----------------

        [UnityTest]
        public IEnumerator AShooterDrawingOffScreen_GetsNoMark()
        {
            if (_root == null) _root = new GameObject("LancerWarningMarkSpawns").transform;
            Enemy s = EnemyFactory.Make(EnemyKind.Shooter, Flat(_her + Vector3.back * 10.5f), _root, _practiceEnemy, "ShooterMark");
            yield return Until(() => s.State == EnemyState.Windup, 5f);
            Assume.That(s.State, Is.EqualTo(EnemyState.Windup), "it draws at her");
            Assume.That(OnScreen(s), Is.False, "from off his screen");
            _marks.Tick();
            Assert.That(_marks.Shown, Is.EqualTo(0), "no other enemy gets one");
            Assert.That(_marks.Followed, Is.EqualTo(0));
        }

        // ---------------- The switch off: the game as before ----------------

        struct LancerFields
        {
            public EnemyState State;
            public float WindupRemaining, Lowered;
            public Vector3 Position;
            public Quaternion Turn;
            public int Charges, Health;
            public bool Charging;

            public static LancerFields Of(EnemyLancer l) => new LancerFields
            {
                State = l.State, WindupRemaining = l.WindupRemaining, Lowered = l.Lowered, Position = l.transform.position, Turn = l.transform.rotation,
                Charges = l.Charges, Health = l.Body.CurrentHealth, Charging = l.IsCharging,
            };

            public override string ToString() => string.Format(Inv, "{0} {1:0.000} {2:0.000} {3} {4} {5} {6} {7}", State, WindupRemaining, Lowered, Position, Turn.eulerAngles, Charges, Health, Charging);
        }

        [UnityTest]
        public IEnumerator TheSwitchOff_NothingIsFollowedOrDrawn_AndTheMarksFrame_ChangesNothingOfTheLancer()
        {
            LancerWarningMark.On = false;
            EnemyLancer l = Lancer(_her + Vector3.back * Below);
            int frames = 0, shown = 0, drawn = 0;
            float deadline = Time.realtimeSinceStartup + 6f;
            bool began = false, ran = false;
            while (Time.realtimeSinceStartup < deadline && !(ran && !l.IsCharging))
            {
                began |= InWindup(l);
                ran |= l.IsCharging;
                if (InWindup(l) || l.IsCharging)
                {
                    // The mark's frame, many times over in the same frame of the game: the lancer as it was, field for field.
                    LancerFields before = LancerFields.Of(l);
                    for (int k = 0; k < 10; k++) _marks.Tick();
                    Assert.That(LancerFields.Of(l).ToString(), Is.EqualTo(before.ToString()), "the mark's frame changes nothing of it");
                    if (ImguiByHand.Available) { ImguiByHand.Draw(null); drawn += _marks.LastDrawn; }
                }
                shown += _marks.Shown + _marks.Followed;
                frames++;
                yield return null;
            }
            Assert.That(began && ran, Is.True, "it wound up off screen and charged");
            Assert.That(shown, Is.EqualTo(0), "off: nothing followed, nothing up, in " + frames + " frames");
            Assert.That(drawn, Is.EqualTo(0), "off: nothing drawn");

            // The same with it on: the mark is up and drawn, and the lancer is still as it was.
            LancerWarningMark.On = true;
            EnemyLancer m = Lancer(_her + Vector3.back * Below + Vector3.right * 2f);
            yield return Until(() => InWindup(m) || m.IsCharging, 6f);
            Assume.That(InWindup(m) && !OnScreen(m), Is.True, "a second lancer winding up off screen");
            LancerFields was = LancerFields.Of(m);
            for (int k = 0; k < 10; k++) _marks.Tick();
            Assert.That(LancerFields.Of(m).ToString(), Is.EqualTo(was.ToString()), "on, the mark's frame changes nothing of it either");
            Assert.That(_marks.MarkOf(m), Is.GreaterThanOrEqualTo(0), "on: up");
            if (ImguiByHand.Available)
            {
                ImguiByHand.Draw(null);
                Assert.That(_marks.LastDrawn, Is.GreaterThanOrEqualTo(1), "on: drawn");
            }
        }

        // ---------------- What a frame allocates ----------------

        static byte[] s_junk;

        [UnityTest]
        public IEnumerator WhileAMarkIsUp_ItsFrameAllocatesNothing()
        {
            EnemyLancer l = Lancer(_her + Vector3.back * Below);
            yield return Until(() => InWindup(l), 5f);
            Assert.That(InWindup(l) && !OnScreen(l), Is.True);
            _marks.Tick();
            Assert.That(_marks.Shown, Is.EqualTo(1), "a mark is up");
            Assert.That(() => { s_junk = new byte[64]; }, AllocIs.AllocatingGCMemory(), "the measure sees an allocation");
            Assert.That(() => { for (int i = 0; i < 300; i++) _marks.Tick(); }, AllocExt.AllocatingGCMemory(NUnit.Framework.Is.Not), "a frame of the marks allocates nothing");
            Assert.That(_marks.Shown, Is.EqualTo(1), "still up");
        }
    }
}
