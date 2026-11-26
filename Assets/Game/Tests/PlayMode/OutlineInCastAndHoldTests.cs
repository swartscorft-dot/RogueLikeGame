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
    /// The aim outline during a cast and a hold (work order 140, part 3; his tick of 2026-11-26: "Vis under kast og hold"), in the
    /// ControlRoom scene through her intents: Heavy Volley's outline stands through its 1 s cast, toward the aim it was pressed with
    /// (the cast keeps that aim), and is gone the moment its shot resolves; Deadeye's is the circle of its 12 m reach round her through
    /// its 0.5 s cast; Fan Fire's cone stands while its key is held and it fires and goes as the hold ends. With
    /// <see cref="SkillLookValues.OutlineInCastAndHold"/> off, none of them shows one during the cast or the hold (work order 123's
    /// reading). The outline is drawn in LateUpdate, so what is seen in a frame is judged by the phase of the frame before.
    /// </summary>
    public class OutlineInCastAndHoldTests
    {
        const float Frame = 1f / 60f;
        const float Metres = 0.05f;

        PlayerCharacter _player;
        SkillIndicator _indicator;
        LineRenderer _line;
        bool _before;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _before = SkillLookValues.OutlineInCastAndHold;
            SkillLookValues.EffectsNotShapes = true;
            Time.timeScale = 1f;
            Time.captureDeltaTime = Frame;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            PlayerHealth health = _player.GetComponent<PlayerHealth>();
            health.MaxHealth = 100000;
            health.RestoreFull();
            _indicator = Object.FindAnyObjectByType<SkillIndicator>();
            Assert.That(_indicator, Is.Not.Null, "the aim outline in the scene");
            _line = _indicator.GetComponent<LineRenderer>();
            EnemyBrute brute = Object.FindAnyObjectByType<EnemyBrute>();
            if (brute != null) brute.enabled = false;
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            SkillLookValues.OutlineInCastAndHold = _before;
            SkillLookValues.EffectsNotShapes = true;
            Time.captureDeltaTime = 0f;
            if (_player != null) _player.GetComponent<PlayerInputDriver>().InputSource = null;
            PlayerInputDriver.ReadRealDevices = true;
        }

        int Slot(string name)
        {
            for (int i = 0; i < _player.Skills.Count; i++)
                if (_player.Skills[i].Name == name) return i;
            Assert.Fail("no skill " + name);
            return -1;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        /// <summary>One frame watched: whether the outline drawn for the frame before stood, and in what phase that frame was.</summary>
        sealed class Watch
        {
            public readonly List<(SkillPhase phase, bool shown)> Frames = new List<(SkillPhase, bool)>();
            public SkillPhase Last = SkillPhase.None;
            public bool ShownIn(SkillPhase p) => Frames.Exists(f => f.phase == p && f.shown);
            public bool HiddenThroughout(SkillPhase p) => Frames.TrueForAll(f => f.phase != p || !f.shown);
            public int CountIn(SkillPhase p) => Frames.FindAll(f => f.phase == p).Count;
        }

        IEnumerator Watching(Watch w, float seconds, System.Action each = null)
        {
            float end = Time.time + seconds;
            while (Time.time < end)
            {
                yield return null;
                w.Frames.Add((w.Last, _indicator.IsVisible));
                each?.Invoke();
                w.Last = _player.SkillPhase;
            }
        }

        IEnumerator Fresh(string name, System.Action<int, SkillDefinition> got)
        {
            int slot = Slot(name);
            _player.EquipSkill(slot);
            _player.StartFresh();
            yield return null;
            got(slot, _player.Skills[slot]);
        }

        [UnityTest]
        public IEnumerator HeavyVolley_TheOutlineStandsThroughItsCast_TowardThePressedAim_AndGoesAsItsShotResolves()
        {
            int slot = -1;
            SkillDefinition s = null;
            yield return Fresh("Heavy Volley", (i, d) => { slot = i; s = d; });
            Assert.That(s.Input, Is.EqualTo(SkillInput.Casting));
            Vector3 aim = _player.Position + Vector3.forward * 8f;
            var w = new Watch();
            Assert.That(_player.PressSkill(slot, aim), Is.EqualTo(IntentResult.Started));
            w.Last = _player.SkillPhase;
            float worstOff = 0f;
            yield return Watching(w, s.Startup + s.Recovery + 0.3f, () =>
            {
                if (w.Last != SkillPhase.Startup || !_indicator.IsVisible || _line.positionCount < 3) return;
                // The strip's far end lies along the aim it was pressed with: every corner is within its half width of that line.
                Vector3 from = Flat(_player.Position), dir = Flat(aim - _player.Position).normalized;
                for (int i = 0; i < _line.positionCount; i++)
                {
                    Vector3 p = Flat(_line.GetPosition(i)) - from;
                    float side = Mathf.Abs(Vector3.Cross(dir, p).y);
                    worstOff = Mathf.Max(worstOff, side - s.HalfWidth);
                }
            });
            Assert.That(w.CountIn(SkillPhase.Startup), Is.GreaterThan(30), "its 1 s cast was watched");
            Assert.That(w.ShownIn(SkillPhase.Startup), Is.True, "the outline stands during the cast");
            Assert.That(w.Frames.FindAll(f => f.phase == SkillPhase.Startup && !f.shown).Count, Is.LessThanOrEqualTo(1), "through the whole cast");
            Assert.That(worstOff, Is.LessThan(Metres), "toward the aim it was pressed with, its half width to each side");
            Assert.That(w.HiddenThroughout(SkillPhase.Recovery), Is.True, "gone the moment its shot resolves");
        }

        [UnityTest]
        public IEnumerator Deadeye_TheOutlineIsTheCircleOfItsReachRoundHer_ThroughItsCast()
        {
            int slot = -1;
            SkillDefinition s = null;
            yield return Fresh("Deadeye", (i, d) => { slot = i; s = d; });
            Assert.That(s.DeadeyeMost, Is.GreaterThan(0));
            GameObject dummy = GameObject.Find("Dummy_A");
            Assert.That(dummy, Is.Not.Null);
            dummy.transform.position = new Vector3(_player.Position.x + 4f, dummy.transform.position.y, _player.Position.z);
            Physics.SyncTransforms();
            dummy.GetComponent<Damageable>().AddMarks(2, _player.Kit.MaxMarks, 30f);
            var w = new Watch();
            Assert.That(_player.PressSkill(slot, _player.Position + Vector3.forward * 6f), Is.EqualTo(IntentResult.Started), "a marked enemy in sight");
            w.Last = _player.SkillPhase;
            float worst = 0f;
            yield return Watching(w, s.Startup + s.Recovery + 0.3f, () =>
            {
                if (w.Last != SkillPhase.Startup || !_indicator.IsVisible) return;
                Vector3 her = Flat(_player.Position);
                for (int i = 0; i < _line.positionCount; i++)
                    worst = Mathf.Max(worst, Mathf.Abs((Flat(_line.GetPosition(i)) - her).magnitude - s.Range));
            });
            Assert.That(w.ShownIn(SkillPhase.Startup), Is.True, "the outline stands during its cast");
            Assert.That(worst, Is.LessThan(Metres), "a circle of its reach (" + s.Range + " m) round her");
            Assert.That(w.HiddenThroughout(SkillPhase.Recovery), Is.True, "gone as it fires");
        }

        [UnityTest]
        public IEnumerator FanFire_TheConeStandsWhileItsKeyIsHeldAndItFires_AndGoesAsTheHoldEnds()
        {
            int slot = -1;
            SkillDefinition s = null;
            yield return Fresh("Fan Fire", (i, d) => { slot = i; s = d; });
            Assert.That(s.Input, Is.EqualTo(SkillInput.Holding));
            var w = new Watch();
            Assert.That(_player.PressSkill(slot, _player.Position + Vector3.forward * 6f), Is.EqualTo(IntentResult.Started));
            w.Last = _player.SkillPhase;
            yield return Watching(w, s.Startup + 0.5f);
            _player.ReleaseSkill(slot);
            yield return Watching(w, s.Recovery + 0.3f);
            Assert.That(w.ShownIn(SkillPhase.Startup), Is.True, "from the press");
            Assert.That(w.ShownIn(SkillPhase.Channel), Is.True, "while it is held and fires");
            Assert.That(w.Frames.FindAll(f => f.phase == SkillPhase.Channel && !f.shown), Is.Empty, "every frame of the hold");
            Assert.That(w.HiddenThroughout(SkillPhase.Recovery), Is.True, "gone as the hold ends");
        }

        [UnityTest]
        public IEnumerator SwitchOff_NoOutlineInTheCastsOrTheHold_AsWorkOrder123LeftIt()
        {
            SkillLookValues.OutlineInCastAndHold = false;
            foreach (string name in new[] { "Heavy Volley", "Fan Fire" })
            {
                int slot = -1;
                SkillDefinition s = null;
                yield return Fresh(name, (i, d) => { slot = i; s = d; });
                var w = new Watch();
                Assert.That(_player.PressSkill(slot, _player.Position + Vector3.forward * 6f), Is.EqualTo(IntentResult.Started), name);
                w.Last = _player.SkillPhase;
                yield return Watching(w, s.Startup + 0.4f);
                _player.ReleaseSkill(slot);
                yield return Watching(w, s.Recovery + 0.5f);
                Assert.That(w.Frames.TrueForAll(f => !f.shown), Is.True, name + ": no outline at any moment");
            }
        }
    }
}
