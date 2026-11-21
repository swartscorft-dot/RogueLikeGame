using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 067 (his choice of 2026-11-21 for the evening, "Wide window og Blast"), in the ControlRoom scene through the
    /// intents: with Blast taken, a dummy standing just outside a keg's old burst and inside the new one is hit (and with no
    /// stat it is not), the same at a cone's edge (Scatter Blast) and across a line's width (Breaker Slug); the aim shape she
    /// sees, the keg's fuse circle and the cone's flash are the size the hit is; and the two cards' numbers are true: take the
    /// card, read the skill (or the clean part of a real hop) as she has it, and it is the card's "after". The Wide window's
    /// judging against the box and the blade is in <see cref="InterfaceInAFightTests"/> (work order 063's test, extended).
    ///
    /// Where the dummy stands is found with the game's own hit rule (<see cref="SkillArea"/>, with the room's cover): the middle
    /// of the band the old area misses and the grown one hits. Nothing is seen; waits are real or game time with a limit.
    /// </summary>
    public class BlastInAFightTests
    {
        static readonly Vector3 NearA = new Vector3(2f, 0f, 1f);
        /// <summary>Blast at its cap (+40 %), two epic steps: the widest margins for where the dummy stands.</summary>
        const float AtCap = 0.4f;

        PlayerCharacter _player;
        Damageable _a;
        SkillIndicator _indicator;
        RunModifiers _m;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.MeleeYouRead = false;
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _player.GetComponent<PlayerHealth>().Untouchable = true;
            _a = GameObject.Find("Dummy_A").GetComponent<Damageable>();
            _indicator = Object.FindAnyObjectByType<SkillIndicator>();
            Assert.That(_indicator, Is.Not.Null, "the aim shape in the scene");
            // The other dummies and the practice brute out of the way: only the dummy placed here is counted.
            int k = 0;
            foreach (string name in new[] { "Dummy_B", "Dummy_C", "Dummy_D", "Dummy_E", "Dummy_F" })
            {
                GameObject d = GameObject.Find(name);
                if (d != null) d.transform.position = new Vector3(-17f + 3f * k++, d.transform.position.y, 18f);
            }
            var brute = Object.FindAnyObjectByType<EnemyBrute>();
            if (brute != null)
            {
                brute.enabled = false;
                if (brute.TryGetComponent(out NavMeshAgent agent)) agent.Warp(new Vector3(-17f, 0f, -17f));
            }
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            if (_player != null && _m != null && _player.Modifiers == _m) _player.Modifiers = null; // put on by hand
            _m = null;
            Time.timeScale = 1f;
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

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        IEnumerator Fresh()
        {
            yield return Until(() => _player.Action == ActionKind.None && !HitStop.IsActive && _player.KegsLying == 0, 4f);
            _player.StartFresh();
            _player.Teleport(NearA);
            yield return null;
        }

        /// <summary>Her stats on, put on her by hand as a run puts its modifiers on, with these steps taken.</summary>
        void Take(UpgradeKind kind, params float[] steps)
        {
            if (_m == null)
            {
                _m = new RunModifiers { StatsAndTiers = true };
                _player.Modifiers = _m;
            }
            foreach (float s in steps) _m.Take(new Upgrade { Kind = kind, Amount = s, Tier = UpgradeTier.Epic, Tiered = true });
        }

        static SkillDefinition Grown(SkillDefinition s, float share) => new SkillStatLayer(share, 0f, new StatValues().BlastSkills).Apply(s);

        void Place(GroundPoint p)
        {
            Vector3 was = _a.transform.position;
            _a.transform.position = new Vector3(p.X, was.y, p.Z);
        }

        /// <summary>
        /// The middle of the band of places <paramref name="at"/>(t), t from <paramref name="lo"/> to <paramref name="hi"/>, that
        /// the old area misses and the grown one hits, by the game's own rule with the room's cover.
        /// </summary>
        float Between(SkillDefinition old, SkillDefinition grown, GroundPoint origin, GroundPoint aim, Func<float, GroundPoint> at, float lo, float hi, string what)
        {
            CoverLine cover = _player.CoverQuery;
            var before = new SkillArea(old, origin, aim, cover);
            var after = new SkillArea(grown, origin, aim, cover);
            float first = float.NaN, last = float.NaN;
            for (int i = 0; i <= 400; i++)
            {
                float t = lo + (hi - lo) * i / 400f;
                if (before.Hits(at(t), _a.Radius) || !after.Hits(at(t), _a.Radius)) continue;
                if (float.IsNaN(first)) first = t;
                last = t;
            }
            Assert.That(float.IsNaN(first), Is.False, what + ": a place the old area misses and the grown one hits");
            return (first + last) * 0.5f;
        }

        Vector3[] AimShape()
        {
            LineRenderer line = _indicator.GetComponent<LineRenderer>();
            var points = new Vector3[line.positionCount];
            line.GetPositions(points);
            return points;
        }

        static float FlatDistance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        static ShotFx Newest(ShotFxKind kind) =>
            Object.FindObjectsByType<ShotFx>(FindObjectsSortMode.None).Where(f => f.Kind == kind).OrderBy(f => f.Age).FirstOrDefault();

        // ---------------- A keg's burst ----------------

        /// <summary>One keg thrown 4 m ahead of her, the dummy put beside where it lies: whether its burst hit the dummy, and the radius the dummy was put at.</summary>
        IEnumerator Keg(Action<bool, float, float, float> done)
        {
            int keg = Slot("Powder Keg");
            _player.EquipSkill(keg);
            yield return Fresh();
            SkillDefinition skill = _player.Skills[keg];
            SkillDefinition old = skill.BeforeStats, grown = Grown(old, AtCap);
            int hits = _a.HitsTaken;
            Vector3 aim = NearA + new Vector3(0f, 0f, 4f);
            Place(new GroundPoint(-14f, 14f)); // away while it flies
            Assert.That(_player.PressSkill(keg, aim), Is.EqualTo(IntentResult.Started), "the keg");
            yield return null;
            Vector3[] shape = AimShape();
            Assert.That(_indicator.IsVisible, Is.True, "its aim shape shows while it is thrown");
            Vector3 middle = shape.Aggregate(Vector3.zero, (s, p) => s + p) / shape.Length;
            float shown = shape.Average(p => FlatDistance(p, middle));
            yield return Until(() => _player.KegsLying > 0, 2f);
            Assert.That(_player.KegsLying, Is.EqualTo(1), "it landed");
            var centre = new GroundPoint(_player.LastKegPoint.x, _player.LastKegPoint.z);
            ShotFx fuse = Newest(ShotFxKind.Outline);
            Assert.That(fuse, Is.Not.Null, "the fuse circle on the ground");
            float d = Between(old, grown, centre, centre, t => new GroundPoint(centre.X + t, centre.Z), 2f, 5f, "the keg");
            Place(new GroundPoint(centre.X + d, centre.Z));
            yield return Until(() => _player.KegsLying == 0, 3f);
            yield return null;
            Assert.That(shown, Is.EqualTo(skill.Radius).Within(1e-3f), "the aim shape is the burst's radius");
            Assert.That(fuse.Size, Is.EqualTo(skill.Radius).Within(1e-4f), "the fuse circle is the burst's radius");
            done(_a.HitsTaken > hits, d, skill.Radius, shown);
        }

        [UnityTest]
        public IEnumerator WithBlastTaken_ADummyJustOutsideAKegsOldBurst_IsHit_AndTheAimShapeAndFuseCircleAreTheHitsSize()
        {
            bool hit = true;
            float at = 0f, radius = 0f, shown = 0f;
            yield return Keg((h, d, r, s) => { hit = h; at = d; radius = r; shown = s; });
            Assert.That(radius, Is.EqualTo(2.5f).Within(1e-5f), "no stat: today's 2.5 m");
            Assert.That(hit, Is.False, "no stat: a dummy " + at + " m from the keg is not hit");
            Take(UpgradeKind.Blast, 0.2f, 0.2f);
            Assert.That(_player.BlastNow, Is.EqualTo(AtCap).Within(1e-6f));
            yield return Keg((h, d, r, s) => { hit = h; at = d; radius = r; shown = s; });
            Assert.That(radius, Is.EqualTo(2.5f * (1f + AtCap)).Within(1e-4f), "3.5 m at the cap");
            Assert.That(hit, Is.True, "with Blast: the dummy " + at + " m from the keg is hit");
            Assert.That(shown, Is.EqualTo(3.5f).Within(1e-3f), "the aim shape grew with it");
        }

        // ---------------- A cone's edge ----------------

        /// <summary>Scatter Blast fired ahead of her with the dummy at its cone's edge: whether it was hit, and the angles of the aim shape and the flash.</summary>
        IEnumerator Cone(Action<bool, float, float, float> done)
        {
            int slot = Slot("Scatter Blast");
            _player.EquipSkill(slot);
            yield return Fresh();
            SkillDefinition skill = _player.Skills[slot];
            SkillDefinition old = skill.BeforeStats, grown = Grown(old, AtCap);
            GroundPoint origin = _player.GroundPosition;
            var aim = new GroundPoint(origin.X, origin.Z + 3f);
            const float distance = 3f;
            float degrees = Between(old, grown, origin, aim,
                                    t => new GroundPoint(origin.X + distance * Mathf.Sin(t * Mathf.Deg2Rad), origin.Z + distance * Mathf.Cos(t * Mathf.Deg2Rad)), 20f, 80f, "the cone");
            Place(new GroundPoint(origin.X + distance * Mathf.Sin(degrees * Mathf.Deg2Rad), origin.Z + distance * Mathf.Cos(degrees * Mathf.Deg2Rad)));
            int hits = _a.HitsTaken, fired = _player.SkillShotsFired;
            Assert.That(_player.PressSkill(slot, new Vector3(aim.X, 0f, aim.Z)), Is.EqualTo(IntentResult.Started), "the blast");
            yield return null;
            Vector3[] shape = AimShape();
            Vector3 apex = shape[0];
            float shown = Vector3.Angle(Flat(shape[1] - apex), Flat(shape[shape.Length - 1] - apex));
            yield return Until(() => _player.SkillShotsFired > fired, 2f);
            ShotFx wedge = Newest(ShotFxKind.Wedge);
            Assert.That(wedge, Is.Not.Null, "the cone's flash");
            float flash = wedge.Angle;
            yield return null;
            done(_a.HitsTaken > hits, degrees, shown, flash);
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        [UnityTest]
        public IEnumerator WithBlastTaken_ADummyJustOutsideACone_IsHit_AndTheAimShapeAndTheFlashAreTheHitsAngle()
        {
            bool hit = true;
            float at = 0f, shown = 0f, flash = 0f;
            yield return Cone((h, d, s, f) => { hit = h; at = d; shown = s; flash = f; });
            Assert.That(hit, Is.False, "no stat: a dummy " + at + " degrees off her aim is not hit");
            Assert.That(shown, Is.EqualTo(70f).Within(0.05f), "today's 70 degrees shown");
            Assert.That(flash, Is.EqualTo(70f).Within(1e-3f));
            Take(UpgradeKind.Blast, 0.2f, 0.2f);
            yield return Cone((h, d, s, f) => { hit = h; at = d; shown = s; flash = f; });
            Assert.That(hit, Is.True, "with Blast: the dummy " + at + " degrees off is hit");
            Assert.That(_player.Skills[Slot("Scatter Blast")].Angle, Is.EqualTo(98f).Within(1e-3f), "70 degrees +40 %");
            Assert.That(shown, Is.EqualTo(98f).Within(0.05f), "the aim shape is the hit's angle");
            Assert.That(flash, Is.EqualTo(98f).Within(1e-3f), "and so is the flash");
        }

        // ---------------- A line's width ----------------

        IEnumerator Line(Action<bool, float, float> done)
        {
            int slot = Slot("Breaker Slug");
            _player.EquipSkill(slot);
            yield return Fresh();
            SkillDefinition skill = _player.Skills[slot];
            SkillDefinition old = skill.BeforeStats, grown = Grown(old, AtCap);
            GroundPoint origin = _player.GroundPosition;
            var aim = new GroundPoint(origin.X, origin.Z + 3f);
            float across = Between(old, grown, origin, aim, t => new GroundPoint(origin.X + t, origin.Z + 3f), 0.5f, 2f, "the line");
            Place(new GroundPoint(origin.X + across, origin.Z + 3f));
            int hits = _a.HitsTaken, fired = _player.SkillShotsFired;
            Assert.That(_player.PressSkill(slot, new Vector3(aim.X, 0f, aim.Z)), Is.EqualTo(IntentResult.Started), "the slug");
            yield return null;
            Vector3[] shape = AimShape();
            float shown = FlatDistance(shape[0], shape[1]);
            yield return Until(() => _player.SkillShotsFired > fired, 2f);
            yield return null;
            done(_a.HitsTaken > hits, across, shown);
        }

        [UnityTest]
        public IEnumerator WithBlastTaken_ADummyJustOutsideALinesWidth_IsHit_AndTheAimShapeIsTheHitsWidth()
        {
            bool hit = true;
            float at = 0f, shown = 0f;
            yield return Line((h, d, s) => { hit = h; at = d; shown = s; });
            Assert.That(hit, Is.False, "no stat: a dummy " + at + " m off the line is not hit");
            Assert.That(shown, Is.EqualTo(1.2f).Within(1e-3f), "today's 1.2 m shown");
            Take(UpgradeKind.Blast, 0.2f, 0.2f);
            yield return Line((h, d, s) => { hit = h; at = d; shown = s; });
            Assert.That(hit, Is.True, "with Blast: the dummy " + at + " m off the line is hit");
            Assert.That(shown, Is.EqualTo(1.68f).Within(1e-3f), "1.2 m +40 %, the hit's width");
        }

        // ---------------- The cards are true ----------------

        /// <summary>Her values now as the game hands them to the cards (GeneratedLevelsOffers.OfferContextNow): her skills as she has them.</summary>
        OfferContext Context()
        {
            var equipped = new List<SkillDefinition>();
            for (int slot = 0; slot < _player.Skills.Count; slot++)
                if (_player.IsEquipped(slot) && _player.Skills[slot] != null) equipped.Add(_player.Skills[slot]);
            return new OfferContext { Modifiers = _player.Modifiers, Feel = _player.Feel, Kit = _player.Kit, Identity = _player.Identity, Equipped = equipped };
        }

        static string After(string line) => line.Substring(line.LastIndexOf(OfferTexts.Arrow, StringComparison.Ordinal) + OfferTexts.Arrow.Length);

        [UnityTest]
        public IEnumerator TheCards_ReadAsOrdered_AndTheirAfter_IsTheSkillAsSheHasItOnceTaken()
        {
            foreach (string name in new[] { "Powder Keg", "Long Shot", "Backstep" }) _player.EquipSkill(Slot(name));
            Take(UpgradeKind.Blast, 0.08f);
            Take(UpgradeKind.WideWindow, 0.10f);
            yield return Fresh();
            // Blast: the keg's burst.
            var blast = new Upgrade { Kind = UpgradeKind.Blast, Amount = 0.14f, Tier = UpgradeTier.Rare, Tiered = true };
            OfferCard bc = OfferTexts.ForUpgrade(blast, Context());
            Debug.Log("[067] " + bc.ToString().Replace("\n", "  |  "));
            Assert.That(bc.Name, Is.EqualTo("Blast"));
            Assert.That(bc.Sentence, Is.EqualTo("Your area skills hit a wider area."));
            Assert.That(bc.Numbers.Count, Is.InRange(1, 3));
            string kegLine = bc.Numbers.Single(n => n.StartsWith("Powder Keg burst", StringComparison.Ordinal));
            StringAssert.StartsWith("Powder Keg burst  " + OfferTexts.Metres(_player.Skills[Slot("Powder Keg")].Radius) + OfferTexts.Arrow, kegLine, "before: as she has it now");
            // Wide window: the band and the clean part.
            var wide = new Upgrade { Kind = UpgradeKind.WideWindow, Amount = 0.25f, Tier = UpgradeTier.Epic, Tiered = true };
            OfferCard wc = OfferTexts.ForUpgrade(wide, Context());
            Debug.Log("[067] " + wc.ToString().Replace("\n", "  |  "));
            Assert.That(wc.Name, Is.EqualTo("Wide window"));
            string bandLine = wc.Numbers.Single(n => n.StartsWith("Long Shot perfect band", StringComparison.Ordinal));
            string stepLine = wc.Numbers.Single(n => n.StartsWith("Backstep clean part", StringComparison.Ordinal));

            _m.Take(blast);
            _m.Take(wide);
            yield return null;
            Assert.That(After(kegLine), Is.EqualTo(OfferTexts.Metres(_player.Skills[Slot("Powder Keg")].Radius)), "the card's after is the keg as she has it");
            Assert.That(_player.Skills[Slot("Powder Keg")].Radius, Is.EqualTo(2.5f * 1.22f).Within(1e-4f), "+8 % and +14 %");
            SkillDefinition shot = _player.Skills[Slot("Long Shot")];
            Assert.That(After(bandLine), Is.EqualTo((shot.PerfectTo - shot.PerfectFrom).ToString("0.00", CultureInfo.InvariantCulture) + " s"), "the band she will be judged on");
            // A real hop: the clean part the judge reads.
            int step = Slot("Backstep");
            Assert.That(_player.PressSkill(step, NearA + Vector3.forward * 3f), Is.EqualTo(IntentResult.Started), "the hop");
            yield return Until(() => _player.IsRecastWindowOpen, 2f);
            Assert.That(_player.IsRecastWindowOpen, Is.True);
            Assert.That(After(stepLine), Is.EqualTo((_player.RecastCleanTo - _player.RecastCleanFrom).ToString("0.00", CultureInfo.InvariantCulture) + " s"),
                        "the card's after is the clean part of a real hop");
            Assert.That(_player.RecastCleanTo - _player.RecastCleanFrom, Is.EqualTo(0.2f * 1.35f).Within(1e-4f), "+10 % and +25 %");
        }
    }
}
