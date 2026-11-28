using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using AllocExt = UnityEngine.TestTools.Constraints.ConstraintExtensions;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 151 (<see cref="EnemyTintValues"/>): on a floor (floor 5, its rooms' mood on, as he plays it), each tinted kind's
    /// added picture (the mood's slot after its own on each mesh of its model) carries its mesh's own colour at the floor's level, by a
    /// property block on that slot alone; an untinted kind carries nothing. Through a hit's flash (today's block over the whole renderer,
    /// then its tint again), a daze (the lancer's pale blue, then its red), the Grave Ram's own daze (its block, today's, then ash white),
    /// a ward put on and taken off (the warded elite's gold all through, its break's daze, gold again), and a death. With the switch off,
    /// no block of this order's is ever laid. And a frame of it allocates nothing.
    /// </summary>
    public class EnemyTintPlayTests
    {
        const ulong RunSeed = 0x41414141UL; // RoomFightsPlayTests' run, as work order 137's pictures
        const int Floor = 5;
        const float Apart = 2.4f;           // m between enemies in a row
        static readonly int s_BaseColor = Shader.PropertyToID("_BaseColor");

        PlayerCharacter _player;
        GeneratedLevels _levels;
        GeneratedLevel _level;
        LevelRoom _room;
        EnemyBrute _template;
        Vector3 _mid;
        int _made;
        bool _chipsWere;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false;
            EnemyTintValues.Show = true;
            _chipsWere = HitChipsValues.On;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _player.GetComponent<PlayerHealth>().Untouchable = true;
            CombatEffects.Ensure();
            ThingsThatFly.Ensure();
            _levels = GeneratedLevels.Ensure();
            _levels.Values = new LevelValues();
            _levels.RunValues.SkillTiers = false;
            GameSettings.SetGreybox(false, keep: false);
        }

        [TearDown]
        public void CleanUp()
        {
            EnemyTintValues.Show = true;
            HitChipsValues.On = _chipsWere;
            GameSettings.Unload();
            if (_player != null) _player.GetComponent<PlayerHealth>().Untouchable = false;
            if (_levels != null) Object.Destroy(_levels.gameObject);
            if (CombatEffects.Current != null) Object.Destroy(CombatEffects.Current.gameObject);
            if (ThingsThatFly.Current != null) Object.Destroy(ThingsThatFly.Current.gameObject);
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        // ---------------- Helpers ----------------

        static Vector3 OnNavMesh(Vector3 p) => NavMesh.SamplePosition(p, out NavMeshHit hit, 3f, NavMesh.AllAreas) ? hit.position : p;

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        /// <summary>Floor 5 built alone, its first combat room emptied of its own enemies, she in the floor's first room (far off: nothing wakes).</summary>
        IEnumerator BuildFloor()
        {
            _level = _levels.Build(RunSeeds.FloorSeed(RunSeed, Floor), Floor);
            Assert.That(_level, Is.Not.Null);
            yield return null;
            Assert.That(_level.Mood, Is.Not.Null, "the rooms' mood on, as he plays");
            _room = RoomCallerRules.CombatRooms(_level.Layout)[0];
            foreach (Enemy e in _level.EnemiesByRoom[_room.Order].ToList()) _level.Leave(e);
            _template = Object.FindObjectsByType<EnemyBrute>(FindObjectsSortMode.InstanceID).First(b => b.ComesBack && b.GetComponentInParent<GeneratedLevel>() == null);
            _mid = OnNavMesh(_level.Middle(_room));
            _player.Teleport(OnNavMesh(_level.Middle(_level.Layout.Rooms[0])));
            yield return null;
        }

        /// <summary>One enemy of a kind in the room, <paramref name="dx"/> m east of its middle, asleep, joined to the floor (the mood's added picture on it).</summary>
        Enemy Make(EnemyKind kind, float dx, bool elite = false)
        {
            Enemy e = EnemyFactory.Make(kind, OnNavMesh(_mid + new Vector3(dx, 0f, 0f)), _level.transform, _template, "Tint151_" + kind + "_" + (++_made));
            if (elite)
            {
                LevelValues v = _levels.Values;
                EnemyFactory.MakeElite(e, v.EliteHealthOf(kind), v.EliteDamageScale, v.EliteSizeScale, v.EliteStaggerShare);
            }
            if (e is EnemyCaller caller) caller.UseValues(_levels.Values.CallerRules);
            e.HealthOverride = 1000000;
            _level.Join(e, _room, roomsOwn: true);
            // Its own copy of its values with no range to wake in (a brute's are the practice brute's asset otherwise: given a floor brute's copy first).
            if (e is EnemyBrute brute) brute.FloorValues = _levels.Values.FloorBruteValues(_template.Values);
            e.Values.AggroRange = 0f;
            return e;
        }

        EnemyGraveRam MakeRam(float dx)
        {
            EnemyGraveRam ram = EnemyGraveRam.Make(OnNavMesh(_mid + new Vector3(dx, 0f, 0f)), _level.transform, _template, "Tint151_GraveRam", 151UL);
            _level.Join(ram, _room, roomsOwn: true);
            return ram;
        }

        /// <summary>The mood's slot on a mesh: the last whose material is not the mesh's own; -1 for none.</summary>
        static int AddedSlot(Renderer r)
        {
            Material[] m = r.sharedMaterials;
            for (int k = m.Length - 1; k >= 1; k--)
                if (m[k] != null && m[k] != m[0]) return k;
            return -1;
        }

        static Color OwnOf(Renderer r) => r.sharedMaterial.GetColor(s_BaseColor);

        /// <summary>What this order lays on the mesh's added picture: the mood's colour for it times the mesh's own, in linear light.</summary>
        static Vector4 Expected(Renderer r)
        {
            int slot = AddedSlot(r);
            Color level = r.sharedMaterials[slot].GetColor(s_BaseColor), own = OwnOf(r);
            bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            Color l = linear ? level.linear : level, t = linear ? own.linear : own;
            return new Vector4(l.r * t.r, l.g * t.g, l.b * t.b, level.a);
        }

        static MaterialPropertyBlock BlockOn(Renderer r, int slot)
        {
            var block = new MaterialPropertyBlock();
            if (slot < 0) r.GetPropertyBlock(block);
            else r.GetPropertyBlock(block, slot);
            return block;
        }

        static void Near(Vector4 a, Vector4 b, string what)
        {
            Assert.That(Vector4.Distance(a, b), Is.LessThan(1e-3f), what + ": " + a.ToString("F4") + " against " + b.ToString("F4"));
        }

        /// <summary>Its tint carried on the added picture of each tinted mesh, nothing over the whole renderer, nothing on an untinted mesh.</summary>
        static void CarriesItsTint(Enemy e, string what)
        {
            Assert.That(e.Look, Is.Not.Null, what + ": its model");
            int tinted = 0;
            foreach (Renderer r in e.Look.Renderers)
            {
                int slot = AddedSlot(r);
                if (slot < 0)
                {
                    // A mesh the mood gave no added picture (one hidden as it was made, as the lancer's sword): nothing on it.
                    Assert.That(r.HasPropertyBlock(), Is.False, what + " " + r.name + ": no added picture, nothing on it");
                    continue;
                }
                Assert.That(BlockOn(r, -1).isEmpty, Is.True, what + " " + r.name + ": nothing over the whole renderer");
                Color own = OwnOf(r);
                if (EnemyTintRules.Tinted(new Rgb(own.r, own.g, own.b)))
                {
                    tinted++;
                    MaterialPropertyBlock b = BlockOn(r, slot);
                    Assert.That(b.isEmpty, Is.False, what + " " + r.name + ": its tint on its added picture");
                    Near(b.GetVector(s_BaseColor), Expected(r), what + " " + r.name);
                    for (int k = 0; k < slot; k++) Assert.That(BlockOn(r, k).isEmpty, Is.True, what + " " + r.name + ": its own slot " + k + " untouched");
                }
                else Assert.That(r.HasPropertyBlock(), Is.False, what + " " + r.name + ": an untinted mesh, nothing on it");
            }
            Assert.That(tinted, Is.GreaterThan(0), what + ": a tinted mesh");
        }

        static void CarriesNothing(Enemy e, string what)
        {
            foreach (Renderer r in e.Look.Renderers)
                Assert.That(r.HasPropertyBlock(), Is.False, what + " " + r.name + ": no block");
        }

        // ---------------- The tints ----------------

        [UnityTest]
        public IEnumerator OnAFloor_EachTintedKind_ItsAddedPictureCarriesItsTint_AndTheUntintedKindsNothing()
        {
            yield return BuildFloor();
            Enemy brute = Make(EnemyKind.Brute, -3f * Apart);
            Enemy elite = Make(EnemyKind.Brute, -2f * Apart, elite: true);
            Enemy caster = Make(EnemyKind.Caster, -Apart);
            Enemy warder = Make(EnemyKind.Warder, 0f);
            Enemy caller = Make(EnemyKind.Caller, Apart);
            Enemy lancer = Make(EnemyKind.Lancer, 2f * Apart);
            Enemy bomber = Make(EnemyKind.Bomber, 3f * Apart);
            EnemyGraveRam ram = MakeRam(4.5f * Apart);
            yield return Frames(4);

            CarriesNothing(brute, "the brute");
            CarriesNothing(caster, "the caster");
            CarriesItsTint(elite, "the elite brute");
            CarriesItsTint(warder, "the warder");
            CarriesItsTint(caller, "the caller");
            CarriesItsTint(lancer, "the lancer");
            CarriesItsTint(bomber, "the bomber");
            CarriesItsTint(ram, "the Grave Ram");

            // The elite's gold reads as gold on its added picture: blue far under red, and never brighter than the untinted picture.
            Renderer skin = elite.Look.Skin;
            Vector4 gold = BlockOn(skin, AddedSlot(skin)).GetVector(s_BaseColor);
            Color level = skin.sharedMaterials[AddedSlot(skin)].GetColor(s_BaseColor);
            float untinted = QualitySettings.activeColorSpace == ColorSpace.Linear ? level.linear.r : level.r;
            Assert.That(gold.z, Is.LessThan(0.2f * gold.x), "gold: " + gold);
            Assert.That(Mathf.Max(gold.x, gold.y, gold.z), Is.LessThanOrEqualTo(untinted + 1e-4f), "no stronger than the floor's own level");
            Assert.That(elite.GetComponent<HitFlash>().Tint.LaidCount, Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator AHitsFlash_OnTheGoldElite_IsTodaysFlash_AndThenItsGoldAgain()
        {
            HitChipsValues.On = false; // work order 127's chips off: the flash as it was
            yield return BuildFloor();
            Enemy elite = Make(EnemyKind.Brute, 0f, elite: true);
            yield return Frames(4);
            CarriesItsTint(elite, "before");
            HitFlash flash = elite.GetComponent<HitFlash>();
            for (int i = 0; i < 10 && !flash.IsFlashing; i++)
            {
                elite.Body.TakeHit(1);
                yield return null;
            }
            Assert.That(flash.IsFlashing, Is.True, "it flashes");
            foreach (Renderer r in elite.Look.Renderers)
            {
                Assert.That(Near01(BlockOn(r, -1).GetColor(s_BaseColor), EnemyLook.FlashColor), Is.True, r.name + ": today's flash over the whole renderer");
                if (AddedSlot(r) >= 0) Assert.That(BlockOn(r, AddedSlot(r)).isEmpty, Is.True, r.name + ": nothing of this order's under it, so the flash reaches the added picture as today");
            }
            yield return Until(() => !flash.IsFlashing, 3f);
            yield return Frames(2);
            CarriesItsTint(elite, "after the flash");
            Assert.That(Near01(OwnOf(elite.Look.Skin), EnemyFactory.EliteTint), Is.True, "gold again");
        }

        static bool Near01(Color a, Color b) => Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f;

        [UnityTest]
        public IEnumerator ADaze_OnALancer_ItsAddedPictureTakesTheDazesBlue_AndThenItsRedAgain()
        {
            yield return BuildFloor();
            Enemy lancer = Make(EnemyKind.Lancer, 0f);
            yield return Frames(4);
            CarriesItsTint(lancer, "the lancer");
            Vector4 red = BlockOn(lancer.Look.Skin, AddedSlot(lancer.Look.Skin)).GetVector(s_BaseColor);
            lancer.Body.DazeFor(0.6f);
            yield return Frames(3);
            Assert.That(lancer.IsDazed, Is.True);
            CarriesItsTint(lancer, "the lancer dazed");
            Vector4 dazed = BlockOn(lancer.Look.Skin, AddedSlot(lancer.Look.Skin)).GetVector(s_BaseColor);
            Assert.That(dazed.z, Is.GreaterThan(dazed.x), "the daze's pale blue: " + dazed);
            yield return Until(() => !lancer.IsDazed, 3f);
            yield return Frames(3);
            CarriesItsTint(lancer, "the daze over");
            Near(BlockOn(lancer.Look.Skin, AddedSlot(lancer.Look.Skin)).GetVector(s_BaseColor), red, "its red again");
        }

        [UnityTest]
        public IEnumerator TheGraveRamsDaze_IsItsOwnBlockAsToday_AndThenAshWhiteAgain()
        {
            yield return BuildFloor();
            EnemyGraveRam ram = MakeRam(0f);
            yield return Frames(4);
            CarriesItsTint(ram, "the Ram");
            ram.Body.DazeFor(0.8f);
            yield return Until(() => ram.ShowsDazedTint, 2f);
            yield return null;
            Assert.That(ram.ShowsDazedTint, Is.True);
            Color deep = InterfaceStyle.C(ram.Tuning.DazedTint);
            foreach (Renderer r in ram.Look.Renderers)
            {
                Assert.That(Near01(BlockOn(r, -1).GetColor(s_BaseColor), deep), Is.True, r.name + ": its daze's own block over the whole renderer, as today");
                if (AddedSlot(r) >= 0) Assert.That(BlockOn(r, AddedSlot(r)).isEmpty, Is.True, r.name + ": nothing of this order's under it");
            }
            Assert.That(ram.GetComponent<HitFlash>().Tint.LaidCount, Is.EqualTo(0));
            yield return Until(() => !ram.ShowsDazedTint, 3f);
            yield return Frames(3);
            CarriesItsTint(ram, "the daze over");
            Assert.That(Near01(OwnOf(ram.Look.Skin), InterfaceStyle.C(ram.Tuning.Tint)), Is.True, "ash white again");
        }

        /// <summary>
        /// The merge of 2026-11-28, W4 (the producer's ruling, against W3's fix that made the Grave Ram's telegraph and its stones' lines
        /// its own children): a telegraph is never a mesh of the enemy's model, so no block of this order's is laid on it and its colour is
        /// the telegraph's; the same for an ordinary enemy's telegraph child (the gold elite's), and through the Ram's daze.
        /// </summary>
        [UnityTest]
        public IEnumerator TheTelegraphs_OfTheRamAndOfATintedEnemy_AreTheirChildren_AndTakeNoTint_NorTheRamsDaze()
        {
            yield return BuildFloor();
            Enemy elite = Make(EnemyKind.Brute, -Apart, elite: true);
            EnemyGraveRam ram = MakeRam(Apart);
            yield return Frames(4);
            CarriesItsTint(elite, "the elite brute");
            CarriesItsTint(ram, "the Grave Ram");
            Color telegraph = _template.Telegraph.sharedMaterial.color;
            var lines = new System.Collections.Generic.List<LineRenderer> { elite.Telegraph, ram.Telegraph };
            lines.AddRange(ram.StoneLines);
            void NoTint(string when)
            {
                foreach (LineRenderer l in lines)
                {
                    Assert.That(l, Is.Not.Null, when);
                    Assert.That(l.transform.IsChildOf(l == elite.Telegraph ? elite.transform : ram.transform), Is.True, when + ": " + l.name + " is its enemy's own");
                    Assert.That(l.HasPropertyBlock(), Is.False, when + ": " + l.name + " carries no block");
                    Assert.That(l.sharedMaterial.color, Is.EqualTo(telegraph), when + ": " + l.name + " in the telegraph's colour");
                }
            }
            NoTint("tints laid");
            ram.Body.DazeFor(0.8f);
            yield return Until(() => ram.ShowsDazedTint, 2f);
            yield return null;
            Assert.That(ram.GetComponent<HitFlash>().Tint.LaidCount, Is.EqualTo(0), "the daze's own block over its body: none of this order's");
            NoTint("through the Ram's daze");
        }

        /// <summary>The merge of 2026-11-28, W4: the ROOMS page's "Enemy tints" on "As before" takes every block of the order's off, as its switch does; "Show" lays them again.</summary>
        [UnityTest]
        public IEnumerator TheRoomsPagesLine_AsBefore_TakesTheTintsOff_AndShow_LaysThemAgain()
        {
            yield return BuildFloor();
            Enemy elite = Make(EnemyKind.Brute, 0f, elite: true);
            yield return Frames(4);
            CarriesItsTint(elite, "shown");
            try
            {
                EnemyTintSettings.Set(false, keep: false);
                Assert.That(EnemyTintSettings.InUse, Is.False);
                yield return Frames(2);
                CarriesNothing(elite, "as before");
                Assert.That(elite.GetComponent<HitFlash>().Tint.LaidCount, Is.EqualTo(0));
                EnemyTintSettings.Set(true, keep: false);
                yield return Frames(2);
                CarriesItsTint(elite, "shown again");
            }
            finally
            {
                EnemyTintSettings.Set(true, keep: false);
            }
        }

        [UnityTest]
        public IEnumerator AWard_PutOnTheEliteAndTakenOff_ItKeepsItsGold_ThroughTheBreaksDaze()
        {
            yield return BuildFloor();
            Enemy elite = Make(EnemyKind.Brute, 0f, elite: true);
            var warder = (EnemyWarder)Make(EnemyKind.Warder, Apart);
            yield return Frames(4);
            CarriesItsTint(elite, "before the ward");
            Vector4 gold = BlockOn(elite.Look.Skin, AddedSlot(elite.Look.Skin)).GetVector(s_BaseColor);
            warder.Provoke();
            yield return Until(() => EnemyWarder.IsWarded(elite), 12f);
            Assert.That(EnemyWarder.IsWarded(elite), Is.True, "the ward on the elite");
            yield return null;
            CarriesItsTint(elite, "warded");
            Near(BlockOn(elite.Look.Skin, AddedSlot(elite.Look.Skin)).GetVector(s_BaseColor), gold, "warded: its gold as before");
            warder.Body.TakeHit(warder.Body.CurrentHealth); // the warder dies: the ward breaks, and the elite is dazed
            yield return Until(() => !EnemyWarder.IsWarded(elite), 2f);
            Assert.That(EnemyWarder.IsWarded(elite), Is.False, "the ward off");
            yield return Frames(2);
            if (elite.IsDazed)
            {
                CarriesItsTint(elite, "the break's daze");
                Assert.That(BlockOn(elite.Look.Skin, AddedSlot(elite.Look.Skin)).GetVector(s_BaseColor).z, Is.GreaterThan(gold.z), "the daze's blue in place of the gold");
                yield return Until(() => !elite.IsDazed, 4f);
                yield return Frames(3);
            }
            CarriesItsTint(elite, "the ward off");
            Near(BlockOn(elite.Look.Skin, AddedSlot(elite.Look.Skin)).GetVector(s_BaseColor), gold, "gold again");
        }

        [UnityTest]
        public IEnumerator ItsDeath_TheTintStaysOnTheBodyAsItLies_AsTheBodysOwnColourDoes_ThenItIsHidden()
        {
            yield return BuildFloor();
            Enemy elite = Make(EnemyKind.Brute, 0f, elite: true);
            yield return Frames(4);
            CarriesItsTint(elite, "alive");
            elite.Body.TakeHit(elite.Body.CurrentHealth);
            yield return Frames(3);
            Assert.That(elite.IsDead, Is.True);
            CarriesItsTint(elite, "lying dead");
            float until = Time.time + elite.ShownDeadFor(elite.Values) + 0.5f;
            while (Time.time < until) yield return null;
            Assert.That(elite.Look.Renderers.All(r => !r.enabled), Is.True, "hidden, as today");
        }

        [UnityTest]
        public IEnumerator TheSwitchOff_NoBlockOfThisOrdersIsEverLaid_AndTheFlashIsTodays_AndOffWhileOnTakesThemOff()
        {
            HitChipsValues.On = false;
            EnemyTintValues.Show = false;
            yield return BuildFloor();
            Enemy elite = Make(EnemyKind.Brute, -Apart, elite: true);
            Enemy warder = Make(EnemyKind.Warder, 0f);
            Enemy lancer = Make(EnemyKind.Lancer, Apart);
            yield return Frames(4);
            foreach (Enemy e in new[] { elite, warder, lancer })
            {
                CarriesNothing(e, "off: " + e.LogWord);
                Assert.That(e.GetComponent<HitFlash>().Tint.LaidCount, Is.EqualTo(0));
            }
            HitFlash flash = elite.GetComponent<HitFlash>();
            for (int i = 0; i < 10 && !flash.IsFlashing; i++)
            {
                elite.Body.TakeHit(1);
                yield return null;
            }
            Assert.That(flash.IsFlashing, Is.True);
            foreach (Renderer r in elite.Look.Renderers)
            {
                Assert.That(Near01(BlockOn(r, -1).GetColor(s_BaseColor), EnemyLook.FlashColor), Is.True, "off: today's flash");
                if (AddedSlot(r) >= 0) Assert.That(BlockOn(r, AddedSlot(r)).isEmpty, Is.True, "off: nothing else");
            }
            yield return Until(() => !flash.IsFlashing, 3f);
            yield return Frames(2);
            CarriesNothing(elite, "off, the flash over");

            // On, then off while it plays: every block of this order's taken off again.
            EnemyTintValues.Show = true;
            yield return Frames(2);
            CarriesItsTint(lancer, "on");
            EnemyTintValues.Show = false;
            yield return Frames(2);
            foreach (Enemy e in new[] { elite, warder, lancer }) CarriesNothing(e, "off again: " + e.LogWord);
        }

        [UnityTest]
        public IEnumerator AFrameOfIt_InAFloorFight_TintsLaid_AllocatesNothing()
        {
            yield return BuildFloor();
            var tinted = new List<Enemy>
            {
                Make(EnemyKind.Brute, -2f * Apart, elite: true), Make(EnemyKind.Warder, -Apart), Make(EnemyKind.Caller, 0f),
                Make(EnemyKind.Lancer, Apart), Make(EnemyKind.Brute, 2f * Apart),
            };
            tinted.Add(MakeRam(3.5f * Apart));
            // The fight: she walks into the room, and it wakes.
            _player.Teleport(OnNavMesh(_mid + new Vector3(0f, 0f, -6f)));
            foreach (Enemy e in tinted) e.Provoke();
            yield return Frames(30);
            var calls = new List<Action>();
            foreach (Enemy e in tinted)
            {
                HitFlash f = e.GetComponent<HitFlash>();
                MethodInfo m = typeof(HitFlash).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                Assert.That(m, Is.Not.Null, "HitFlash.LateUpdate");
                calls.Add((Action)Delegate.CreateDelegate(typeof(Action), f, m));
            }
            Assert.That(tinted.Sum(e => e.GetComponent<HitFlash>().Tint.LaidCount), Is.GreaterThan(0), "tints laid");
            Action frame = () => { for (int i = 0; i < calls.Count; i++) calls[i](); };
            frame();
            Assert.That(() => { for (int i = 0; i < 100; i++) frame(); }, AllocExt.AllocatingGCMemory(Is.Not), "a hundred frames of every tint in the fight allocate nothing");
            // And laying again (a tint changed: a daze), the same.
            foreach (Enemy e in tinted) e.Body.DazeFor(0.5f);
            yield return Frames(2);
            Assert.That(() => { for (int i = 0; i < 100; i++) frame(); }, AllocExt.AllocatingGCMemory(Is.Not), "and dazed");
        }
    }
}
