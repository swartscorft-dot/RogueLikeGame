using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
    /// Work order 110 (Kristian, 2026-11-24: yes to "Tre valg per plass, hver med en buff og en kostnad. Det som synes av dem er
    /// lite og stille"; the switch <see cref="MetaValues.MoreParts"/>, on), in the ControlRoom scene through the intents: the
    /// store's nine cards and rows measure inside their shapes at 1920 by 1080 and 1280 by 720, in the concept's look and this
    /// morning's; a run started with each new part has its numbers and her sheet shows them; the Quick-Draw Grip's first shot
    /// after a dash or a skill starts sooner and the next at the feel's; the Reflex Sight's basic shots crit as the crits'
    /// stream says at its chance; and with the switch off the store and a run are the four's.
    ///
    /// The save is written only under a temporary folder of each test (<see cref="GeneratedLevels.BetweenRunsSavePath"/>, set
    /// before any switch is on), and the game's own save file is checked untouched at the end. Nobody has seen the screen or
    /// played the parts: these are numbers.
    /// </summary>
    public class MoreWeaponPartsPlayTests
    {
        const ulong RunSeed = 0x0E117E17UL; // WeaponAttachmentsTests'
        const ulong CritSeed = 0x110C417UL; // any: the crits' stream is replayed below from the same seed
        static readonly Vector2Int[] Sizes = { new Vector2Int(1920, 1080), new Vector2Int(1280, 720) };
        static readonly Vector3 NearA = new Vector3(2f, 0f, 1f); // Dummy_A (4, 3) is 2.8 m away here (WeaponAttachmentsTests')
        static readonly string[] Keys = { BetweenRunsSwitch.Key, AttachmentsSwitch.Key };

        static readonly string[] Five =
        {
            WeaponAttachments.ReflexSight, WeaponAttachments.LongBarrel, WeaponAttachments.Compensator,
            WeaponAttachments.WeightedGrip, WeaponAttachments.QuickDrawGrip,
        };

        static string s_realPath;
        static bool s_realExisted;
        static long s_realLength;
        static DateTime s_realWritten;

        PlayerCharacter _player;
        GeneratedLevels _levels;
        RunHud _hud;
        Damageable _a;
        string _folder;
        readonly Dictionary<string, int?> _prefsBefore = new Dictionary<string, int?>();

        string SavePath => Path.Combine(_folder, GeneratedLevels.BetweenRunsFileName);

        [OneTimeSetUp]
        public void RememberTheRealSave()
        {
            s_realPath = GeneratedLevels.DefaultBetweenRunsSavePath;
            s_realExisted = File.Exists(s_realPath);
            s_realLength = s_realExisted ? new FileInfo(s_realPath).Length : -1;
            s_realWritten = s_realExisted ? File.GetLastWriteTimeUtc(s_realPath) : DateTime.MinValue;
        }

        [OneTimeTearDown]
        public void TheRealSave_IsUntouched()
        {
            Assert.That(File.Exists(s_realPath), Is.EqualTo(s_realExisted), "the game's own save neither made nor removed");
            if (s_realExisted)
            {
                Assert.That(new FileInfo(s_realPath).Length, Is.EqualTo(s_realLength));
                Assert.That(File.GetLastWriteTimeUtc(s_realPath), Is.EqualTo(s_realWritten), "nor written");
            }
        }

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            _prefsBefore.Clear();
            foreach (string k in Keys) _prefsBefore[k] = PlayerPrefs.HasKey(k) ? PlayerPrefs.GetInt(k) : (int?)null;
            ClearKeys();
            BetweenRunsSwitch.Unload();
            AttachmentsSwitch.Unload();
            _folder = Path.Combine(Path.GetTempPath(), "MoreWeaponPartsPlayTests-" + Guid.NewGuid().ToString("N"));
            GeneratedLevels.BetweenRunsSavePath = SavePath; // before any switch is on: never the game's own save
            InterfaceStyle.Shared.Kinetic = true;
            InterfaceStyle.Shared.Menus.Concept = true;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _player.GetComponent<PlayerHealth>().Untouchable = true;
            _a = GameObject.Find("Dummy_A").GetComponent<Damageable>();
            _a.SetMaxHealth(1000000);
            _levels = GeneratedLevels.Ensure();
            _levels.MetaValues.Enabled = false; // work order 117: the trials are on in code now; this class's tests were written with them off and turn them on themselves
            _levels.MetaValues.Attachments = false;
            _levels.DropValues.On = false;
            _hud = RunHud.Ensure();
            yield return null;
            Assert.That(GeneratedLevels.BetweenRunsSavePathInUse, Is.EqualTo(SavePath), "never the game's own save");
        }

        [TearDown]
        public void CleanUp()
        {
            InterfaceStyle.ScreenForPictures = Vector2Int.zero;
            InterfaceStyle.Shared.Menus.Concept = true;
            if (_levels != null)
            {
                if (_hud != null) _hud.CloseSheet();
                if (_levels.Paused) _levels.Resume();
                if (_levels.BetweenRunsUp) _levels.LeaveBetweenRuns();
                _levels.BackToPracticeRoom();
            }
            if (CastBar.Current != null) Object.Destroy(CastBar.Current.gameObject);
            foreach (MonoBehaviour m in new MonoBehaviour[] { _hud, _levels })
                if (m != null) Object.Destroy(m.gameObject);
            _hud = null;
            _levels = null;
            if (_player != null) _player.Modifiers = null;
            BetweenRunsSwitch.Unload();
            AttachmentsSwitch.Unload();
            ClearKeys();
            foreach (KeyValuePair<string, int?> p in _prefsBefore)
                if (p.Value.HasValue) PlayerPrefs.SetInt(p.Key, p.Value.Value);
            PlayerPrefs.Save();
            GeneratedLevels.BetweenRunsSavePath = null;
            if (_folder != null && Directory.Exists(_folder)) Directory.Delete(_folder, true);
            _folder = null;
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        static void ClearKeys()
        {
            foreach (string k in Keys) PlayerPrefs.DeleteKey(k);
            PlayerPrefs.Save();
        }

        // ---------------- Helpers ----------------

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        IEnumerator UntilFree() =>
            Until(() => !_player.IsLocked && _player.Action == ActionKind.None && _player.SkillGateRemaining <= 0f && _player.RecastSlot < 0 && !_player.IsDashing, 5f);

        /// <summary>The trial and the attachments on through the values, a save written first where the game reads it.</summary>
        void OnWithSave(MetaState s)
        {
            Directory.CreateDirectory(_folder);
            Assert.That(MetaSaveFile.Save(SavePath, s, out string error), Is.True, error);
            _levels.MetaValues.Enabled = true;
            _levels.MetaValues.Attachments = true;
        }

        /// <summary>A run's modifiers in the practice room with these parts on (as WeaponAttachmentsTests makes "a run"); her stats on or off.</summary>
        RunModifiers Fitted(bool stats, params string[] ids)
        {
            RunValues values = _levels.RunValues;
            var m = new RunModifiers { StatsAndTiers = stats, StatValues = values.Stats, CooldownFloor = values.CooldownFloor };
            foreach (string id in ids) m.Weapon.Add(WeaponRack.Find(new MetaValues(), id));
            _player.Modifiers = m;
            return m;
        }

        static void Put(Transform t, Vector3 at) => t.position = new Vector3(at.x, t.position.y, at.z);

        // ---------------- The store ----------------

        [UnityTest]
        public IEnumerator TheStoresNineCards_MeasureInsideTheirShapes_AtBothSizes_InBothLooks_AndOffTheFoursAsBefore()
        {
            var save = new MetaState { Currency = 40 };
            save.AttachmentsOwned.AddRange(new[] { WeaponAttachments.ReflexSight, WeaponAttachments.Compensator, WeaponAttachments.LaserSight });
            save.AttachmentsFitted.AddRange(new[] { WeaponAttachments.ReflexSight, WeaponAttachments.Compensator });
            OnWithSave(save);
            Assert.That(_levels.MetaValues.MoreParts, Is.True, "on by default");
            Assert.That(_levels.OpenBetweenRuns(), Is.True);
            yield return null;
            yield return null;

            // The lines: a head a slot, three parts under each, in slot order.
            Assert.That(_hud.WeaponLines.Where(l => !l.Contains("\n")).Skip(2), Is.EqualTo(new[] { "Pistols:  the basic shot", "SIGHT", "BARREL", "GRIP" }));
            Assert.That(_hud.WeaponLines.Count(l => l.Contains("\n")), Is.EqualTo(9), "a row a part");
            StringAssert.Contains("Reflex Sight   FITTED\n+10 % crit chance on basic shots.\nCost:  Reach 8.5 m.", _hud.WeaponText);
            StringAssert.Contains("Long Barrel   price 40 Echoes\nReach 13 m.\nCost:  20 % slower between shots.", _hud.WeaponText);
            StringAssert.Contains("Quick-Draw Grip   55 Echoes, 15 more\n", _hud.WeaponText);

            // The concept's look: the cards.
            List<string> names = _hud.BetweenCardNames.ToList();
            int pieces = _levels.MetaValues.Pieces.Length, ranks = _levels.MetaValues.Ranks.Length;
            Assert.That(names.Count, Is.EqualTo(pieces + 9 + ranks), "a card for every piece, every part and every rank");
            Assert.That(names.Skip(pieces).Take(9), Is.EqualTo(new[]
            {
                "Laser Sight", "Ghost Ring", "Reflex Sight", "Bull Barrel", "Long Barrel", "Compensator", "Runner's Grip", "Weighted Grip", "Quick-Draw Grip",
            }));
            int laser = names.IndexOf("Laser Sight"), ghost = names.IndexOf("Ghost Ring"), reflex = names.IndexOf("Reflex Sight"), bull = names.IndexOf("Bull Barrel");
            Assert.That(_hud.BetweenCardStates[reflex], Is.EqualTo("FITTED"));
            Assert.That(_hud.BetweenCardStates[laser], Is.EqualTo("owned"));
            foreach (Vector2Int size in Sizes)
            {
                InterfaceStyle.ScreenForPictures = size;
                string at = size.x + " by " + size.y;
                float viewWidth = size.x / InterfaceStyle.Scale;
                Assert.That(_hud.BetweenTextOutside(), Is.Empty, at + ": every word inside its shape");
                for (int i = 0; i < names.Count; i++)
                {
                    Rect r = _hud.BetweenCardRect(i);
                    Assert.That(r.xMin >= 0f && r.yMin >= 0f && r.xMax <= viewWidth && r.yMax <= InterfaceStyle.V.LayoutHeight, Is.True, at + ": card " + names[i] + " on screen, " + r);
                    for (int j = 0; j < i; j++) Assert.That(r.Overlaps(_hud.BetweenCardRect(j)), Is.False, at + ": cards " + names[j] + " and " + names[i] + " apart");
                    Assert.That(_hud.BetweenCardAt(r.center), Is.EqualTo(i), at + ": a click on " + names[i] + " is on it");
                }
                Rect l = _hud.BetweenCardRect(laser), g = _hud.BetweenCardRect(ghost), x = _hud.BetweenCardRect(reflex), b = _hud.BetweenCardRect(bull);
                Assert.That(g.y, Is.EqualTo(l.y).Within(0.01f), at + ": a slot's first two side by side");
                Assert.That(g.x, Is.GreaterThan(l.xMax), at + ": the second to the right");
                Assert.That(x.x, Is.EqualTo(l.x).Within(0.01f), at + ": the third under the first");
                Assert.That(x.y, Is.GreaterThan(l.yMax), at);
                Assert.That(b.y, Is.GreaterThan(x.yMax), at + ": the barrels from a new row, under the sights");
                Assert.That(_hud.BetweenFit, Is.GreaterThan(0.7f), at + ": the screen not drawn much smaller to fit");
                Debug.Log("Work order 110: the store's nine at " + at + ": fit " + _hud.BetweenFit.ToString("0.000") + ", Laser " + l + ", Ghost " + g + ", Reflex " + x + ", Bull " + b);
            }

            // This morning's look, drawn by hand (a batch run sends no OnGUI).
            if (ImguiByHand.Available)
            {
                InterfaceStyle.Shared.Menus.Concept = false;
                foreach (Vector2Int size in Sizes)
                {
                    InterfaceStyle.ScreenForPictures = size;
                    string at = size.x + " by " + size.y + ", this morning's look";
                    ImguiByHand.Drawn drawn = ImguiByHand.Draw(null).First(d => d.Piece == _hud);
                    Assert.That(drawn.Error, Is.Null, at + ": drawn without a fault");
                    float viewWidth = size.x / (size.y / SkillBar.LayoutHeight);
                    Rect rack = _hud.BetweenFlatRack, gun = _hud.BetweenFlatWeapon, rk = _hud.BetweenFlatRanks;
                    foreach (Rect p in new[] { rack, gun, rk })
                        Assert.That(p.width > 0f && p.xMin >= 0f && p.yMin >= 0f && p.xMax <= viewWidth && p.yMax <= SkillBar.LayoutHeight, Is.True, at + ": a panel on screen, " + p);
                    Assert.That(gun.Overlaps(rack) || gun.Overlaps(rk), Is.False, at + ": the weapon's panel apart");
                    Assert.That(_hud.FlatPartsSmall, Is.True, at + ": nine rows in the smaller type");
                    Debug.Log("Work order 110: this morning's look at " + at + ": the parts' type " + _hud.FlatPartsFontSize + ", the weapon's panel " + gun + ", the rack " + rack + ", the ranks " + rk);
                }
                InterfaceStyle.Shared.Menus.Concept = true;
            }
            InterfaceStyle.ScreenForPictures = Vector2Int.zero;

            // Off: the four, as before; one column, 060's rows in 060's type.
            Assert.That(_levels.LeaveBetweenRuns(), Is.True);
            yield return null;
            _levels.MetaValues.MoreParts = false;
            Assert.That(_levels.OpenBetweenRuns(), Is.True);
            yield return null;
            yield return null;
            Assert.That(_hud.WeaponLines.Count(l => l.Contains("\n")), Is.EqualTo(4), "a row each of the four");
            Assert.That(_hud.WeaponText, Does.Not.Contain("Reflex").And.Not.Contain("Long Barrel").And.Not.Contain("Compensator").And.Not.Contain("Weighted").And.Not.Contain("Quick-Draw"));
            StringAssert.Contains("Laser Sight   owned\n", _hud.WeaponText, "the Laser Sight owned, the hidden Reflex Sight not shown as fitted");
            names = _hud.BetweenCardNames.ToList();
            Assert.That(names.Skip(pieces).Take(4), Is.EqualTo(new[] { "Laser Sight", "Ghost Ring", "Bull Barrel", "Runner's Grip" }));
            Assert.That(names.Count, Is.EqualTo(pieces + 4 + ranks));
            Rect l4 = _hud.BetweenCardRect(pieces), g4 = _hud.BetweenCardRect(pieces + 1);
            Assert.That(g4.x, Is.EqualTo(l4.x).Within(0.01f), "one column, as 060 and 078 had it");
            Assert.That(g4.y, Is.GreaterThan(l4.yMax));
            if (ImguiByHand.Available)
            {
                InterfaceStyle.Shared.Menus.Concept = false;
                InterfaceStyle.ScreenForPictures = Sizes[0];
                Assert.That(ImguiByHand.Draw(null).First(d => d.Piece == _hud).Error, Is.Null);
                Assert.That(_hud.FlatPartsSmall, Is.False, "four rows in 060's type");
                InterfaceStyle.Shared.Menus.Concept = true;
                InterfaceStyle.ScreenForPictures = Vector2Int.zero;
            }
            Assert.That(_levels.LeaveBetweenRuns(), Is.True);
            if (!ImguiByHand.Available) Assert.Inconclusive("the engine's IMGUI entry points moved: this morning's look was not drawn by hand");
        }

        // ---------------- A run with each new part ----------------

        [UnityTest]
        public IEnumerator ARunStartedWithEachNewPart_HasItsNumbers_AndHerSheetShowsThem()
        {
            OnWithSave(new MetaState { Currency = 1000 });
            FeelValues feel = _player.Feel;
            foreach (string id in Five)
            {
                Assert.That(_levels.OpenBetweenRuns(RunSeed), Is.True, id + ": the screen");
                yield return null;
                Assert.That(_levels.UseAttachment(id), Is.True, id + ": bought (or fitted) on the screen");
                Assert.That(WeaponRack.IsFitted(_levels.BetweenRunsState, id), Is.True);
                WeaponAttachment part = WeaponRack.Find(_levels.MetaValues, id);
                // Only this part fitted: the others of its slot come off by themselves; take off the other slots'.
                foreach (string other in _levels.BetweenRunsState.AttachmentsFitted.ToList())
                    if (other != id) Assert.That(_levels.TakeOffAttachment(other), Is.True);
                Assert.That(_levels.StartFromBetweenRuns(), Is.Not.Null, id + ": the run");
                yield return null;
                yield return null;

                RunModifiers m = _levels.Modifiers;
                Assert.That(m.Weapon.Parts, Is.EqualTo(new[] { part.Name }), id);
                Assert.That(_player.BasicShotRange, Is.EqualTo(feel.BasicShotRange + part.ReachAdd).Within(1e-5f), id + ": reach");
                Assert.That(_player.BasicShotHalfWidth, Is.EqualTo(feel.BasicShotHalfWidth).Within(1e-6f), id + ": width");
                float interval = (feel.BasicShotStartup + feel.BasicShotActive + feel.BasicShotRecovery) * part.IntervalScale;
                Assert.That(_player.BasicShotBaseInterval, Is.EqualTo(interval).Within(1e-5f), id + ": time from shot to shot");
                Assert.That(_player.Weapon.ShotStagger, Is.EqualTo(part.Stagger), id + ": stagger");
                Assert.That(_player.Weapon.ShotDamage(Placeholders.BasicShotDamage, false), Is.EqualTo((int)Math.Round(Placeholders.BasicShotDamage * part.DamageScale, MidpointRounding.AwayFromZero)), id + ": damage");
                Assert.That(_player.Weapon.MovingShare(feel.MovingShotSpeedShare), Is.EqualTo(part.MovingSpeedShare > 0f ? part.MovingSpeedShare : feel.MovingShotSpeedShare), id + ": on the move");
                Assert.That(_player.Weapon.ShotCritChance(m.Stats.CritChance), Is.EqualTo(m.Stats.CritChance + part.CritChanceAdd).Within(1e-6f), id + ": crit");
                Assert.That(_player.Weapon.ShotStartup(feel.BasicShotStartup, true), Is.EqualTo(feel.BasicShotStartup * part.FirstShotStartupScale).Within(1e-6f), id + ": first shot after a dash");
                Assert.That(_player.Laser == null || !_player.Laser.Shown, Is.True, id + ": nothing drawn");

                // Her sheet (Tab): the basic shot's panel, and the loadout among her stats.
                Assert.That(_hud.OpenSheet(), Is.True, id + ": the sheet");
                yield return null;
                Assert.That(_hud.SheetTileNames[0], Is.EqualTo(HerSheet.BasicShotName));
                Assert.That(_hud.HoverSheetSkill(0), Is.True);
                SheetSkill p = _hud.SheetPanel;
                Debug.Log("Work order 110: her sheet's basic shot with " + part.Name + ": " + p.ToString().Replace("\n", " | "));
                Assert.That(p.Taken, Does.Contain(part.Name), id + ": named on the basic shot's panel");
                Assert.That(_hud.SheetStats.Any(s => s.Value.Contains(part.Name)), Is.True, id + ": the loadout line names it: " + string.Join("; ", _hud.SheetStats.Select(s => s.Name + " " + s.Value)));
                Assert.That(p.ValueOf("reach"), Is.EqualTo(OfferTexts.Metres(feel.BasicShotRange + part.ReachAdd)), id);
                if (id == WeaponAttachments.ReflexSight) Assert.That(p.ValueOf("crit"), Does.StartWith(OfferTexts.Pct(m.Stats.CritChance + 0.10f) + " chance"));
                if (id == WeaponAttachments.Compensator) Assert.That(p.ValueOf("damage"), Does.StartWith("32"));
                if (id == WeaponAttachments.QuickDrawGrip)
                {
                    Assert.That(p.ValueOf("damage"), Does.StartWith("36"));
                    Assert.That(p.ValueOf("first shot"), Is.EqualTo("after a dash or a skill it goes off in 0.02 s, not 0.08 s"));
                }
                if (id == WeaponAttachments.WeightedGrip) Assert.That(p.ValueOf("stagger"), Is.EqualTo("3 a hit"));
                foreach (Vector2Int size in Sizes)
                {
                    InterfaceStyle.ScreenForPictures = size;
                    Assert.That(_hud.SheetTextOutside(), Is.Empty, id + " at " + size.x + " by " + size.y + ": her sheet's words inside their shapes");
                }
                InterfaceStyle.ScreenForPictures = Vector2Int.zero;
                Assert.That(_hud.CloseSheet(), Is.True);
                _levels.BackToPracticeRoom();
                yield return null;
            }
        }

        // ---------------- The two new knobs, felt ----------------

        [UnityTest]
        public IEnumerator QuickDrawGrip_TheFirstShotAfterADashOrASkill_StartsSooner_TheNextAtTheFeels()
        {
            FeelValues feel = _player.Feel;
            Vector3 away = _player.Position + Vector3.left * 8f;
            foreach (bool grip in new[] { false, true })
            {
                if (grip) Fitted(false, WeaponAttachments.QuickDrawGrip);
                else Fitted(false);
                float quick = grip ? feel.BasicShotStartup * 0.25f : feel.BasicShotStartup;
                string with = grip ? "with the grip" : "the stock gun";
                int draws = _player.QuickDraws;

                yield return UntilFree();
                _player.Shoot(away);
                Assert.That(_player.ActionLeadTime, Is.EqualTo(feel.BasicShotStartup).Within(1e-6f), with + ": a shot after a shot, the feel's startup");
                yield return UntilFree();

                Assert.That(_player.DashToward(_player.Position + Vector3.back * 3f), Is.EqualTo(IntentResult.Started));
                yield return UntilFree();
                int fired = _player.BasicShotsFired;
                float clicked = Time.time;
                _player.Shoot(away);
                Assert.That(_player.LastShotWasFirstAfterDashOrSkill, Is.True, with + ": the first after the dash");
                Assert.That(_player.ActionLeadTime, Is.EqualTo(quick).Within(1e-6f), with + ": the first shot after a dash");
                yield return Until(() => _player.BasicShotsFired > fired, 2f);
                Debug.Log("Work order 110, " + with + ": the first shot after a dash went off " + ((Time.time - clicked) * 1000f).ToString("0.0") + " ms after the click");
                yield return UntilFree();
                _player.Shoot(away);
                Assert.That(_player.LastShotWasFirstAfterDashOrSkill, Is.False);
                Assert.That(_player.ActionLeadTime, Is.EqualTo(feel.BasicShotStartup).Within(1e-6f), with + ": the next, the feel's");
                yield return UntilFree();

                Assert.That(_player.PressSkill(0, away), Is.Not.EqualTo(IntentResult.Rejected), "a skill");
                yield return UntilFree();
                yield return new WaitForSeconds(0.3f);
                _player.Shoot(away);
                Assert.That(_player.LastShotWasFirstAfterDashOrSkill, Is.True, with + ": the first after a skill");
                Assert.That(_player.ActionLeadTime, Is.EqualTo(quick).Within(1e-6f), with + ": the first shot after a skill");
                yield return UntilFree();
                Assert.That(_player.QuickDraws, Is.EqualTo(draws + (grip ? 2 : 0)), with + ": quick draws counted");
                _player.StartFresh();
                yield return null;
            }
            _player.Modifiers = null;
        }

        [UnityTest]
        public IEnumerator ReflexSight_BasicShotsCrit_AsTheCritsStreamSays_At15PercentNot5()
        {
            Assert.That(_levels.RunValues.Stats.StartCritChance, Is.EqualTo(0.05f), "her start's crit chance");
            const int shots = 16;
            // A seed whose stream, replayed, crits more often at 15 % than at 5 % over these shots, so the two runs tell apart
            // (the first run of this test, with a fixed seed, crit once in each).
            ulong seed = CritSeed;
            while (Replayed(seed, shots, 0.15f) <= Replayed(seed, shots, 0.05f)) seed++;
            var crits = new Dictionary<bool, int>();
            foreach (bool sight in new[] { false, true })
            {
                RunModifiers m = sight ? Fitted(true, WeaponAttachments.ReflexSight) : Fitted(true);
                float chance = m.Weapon.ShotCritChance(m.Stats.CritChance);
                Assert.That(chance, Is.EqualTo(sight ? 0.15f : 0.05f).Within(1e-6f));
                _player.SeedCrits(seed);
                int before = _player.Crits, hits = _a.HitsTaken;
                for (int i = 0; i < shots; i++)
                {
                    yield return UntilFree();
                    _player.Teleport(NearA);
                    Physics.SyncTransforms();
                    yield return null;
                    int fired = _player.BasicShotsFired;
                    _player.Shoot(_a.transform.position);
                    yield return Until(() => _player.BasicShotsFired > fired, 2f);
                    yield return new WaitForSeconds(0.3f);
                }
                int took = _a.HitsTaken - hits;
                Assert.That(took, Is.EqualTo(shots), "every shot hit the dummy");
                // The same stream, replayed: one roll a hit at this chance.
                int expected = Replayed(seed, took, chance);
                crits[sight] = _player.Crits - before;
                Debug.Log("Work order 110: " + (sight ? "the Reflex Sight" : "the stock gun") + ": " + crits[sight] + " crits of " + took + " (the stream says " + expected + ", seed " + seed + ")");
                Assert.That(crits[sight], Is.EqualTo(expected), "the crits as the stream says at " + chance);
                _player.StartFresh();
                yield return null;
            }
            Assert.That(crits[true], Is.GreaterThan(crits[false]), "the sight's basic shots crit more often, on the same stream");
            _player.Modifiers = null;
        }

        /// <summary>The crits a stream from this seed gives over this many hits at this chance (one roll a hit, as Deal rolls).</summary>
        static int Replayed(ulong seed, int hits, float chance)
        {
            LevelRandom replay = LevelRandom.Stream(seed, PlayerCharacter.CritStage);
            int n = 0;
            for (int i = 0; i < hits; i++) if (CritRules.Rolls(replay, chance)) n++;
            return n;
        }

        // ---------------- Off ----------------

        [UnityTest]
        public IEnumerator Off_ASaveWithTheFiveFitted_ARunIsTheFoursOnly_TheirNumbersAsBefore()
        {
            var save = new MetaState { Currency = 0 };
            save.AttachmentsOwned.AddRange(Five);
            save.AttachmentsOwned.Add(WeaponAttachments.BullBarrel);
            save.AttachmentsFitted.AddRange(new[] { WeaponAttachments.ReflexSight, WeaponAttachments.BullBarrel, WeaponAttachments.QuickDrawGrip });
            OnWithSave(save);
            _levels.MetaValues.MoreParts = false;
            Assert.That(_levels.OpenBetweenRuns(RunSeed), Is.True);
            yield return null;
            Assert.That(_hud.WeaponLines.Count(l => l.Contains("\n")), Is.EqualTo(4));
            Assert.That(_levels.BetweenRunsState.AttachmentsOwned, Is.EqualTo(save.AttachmentsOwned), "the save's parts kept");
            Assert.That(_levels.StartFromBetweenRuns(), Is.Not.Null);
            yield return null;
            yield return null;
            AttachmentEffects w = _levels.Modifiers.Weapon;
            Assert.That(w.Parts, Is.EqualTo(new[] { "Bull Barrel" }), "only the four's applied");
            var bull = new AttachmentEffects();
            bull.Add(WeaponAttachments.Create()[2]);
            Assert.That(w.ToString(), Is.EqualTo(bull.ToString()), "the Bull Barrel's numbers alone, as before");
            Assert.That(_player.BasicShotRange, Is.EqualTo(_player.Feel.BasicShotRange).Within(1e-6f), "no sight");
            Assert.That(w.ShotStartup(_player.Feel.BasicShotStartup, true), Is.EqualTo(_player.Feel.BasicShotStartup), "no quick draw");
            _levels.BackToPracticeRoom();
            yield return null;
            Assert.That(File.ReadAllText(SavePath), Does.Contain("reflex-sight").And.Contain("quick-draw-grip"), "still in the save");
        }
    }
}
