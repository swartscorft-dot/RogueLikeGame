using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    /// Work order 112, the look list of the light check, in the ControlRoom scene through <see cref="GeneratedLevels"/>, on work
    /// order 091's run (226K2223): with the switch off a floor at lamplit C has no lid, its lamps and the mood's sources are those
    /// of each of the three off, value for value; on, the throne room's carved blocks and the piers have lids (above the black
    /// tops, without a collider, coloured by the lamps and never black), drawn only with a lamplit level and their rooms shown,
    /// coloured again only when the level changes, gone with the greybox and with the floor; a passage's sconce hangs beside its
    /// lantern; nothing allocates in a frame while nothing changes. Frames without a window are short: waits count frames.
    /// </summary>
    public class LookListPlayTests
    {
        const ulong RunSeed = 0x09100001UL;

        PlayerCharacter _player;
        GeneratedLevels _levels;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            LookListValues.Reset();
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null);
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _player.GetComponent<PlayerHealth>().Untouchable = true;
            _levels = GeneratedLevels.Ensure();
            _levels.RunValues.SkillTiers = false;
            GameSettings.SetGreybox(false, keep: false);
            GameSettings.SetLightStep(3, keep: false);
        }

        [TearDown]
        public void CleanUp()
        {
            LookListValues.Reset();
            if (_player != null)
            {
                _player.GetComponent<PlayerInputDriver>().InputSource = null;
                _player.GetComponent<PlayerHealth>().Untouchable = false;
            }
            if (_levels != null) Object.Destroy(_levels.gameObject);
            GameSettings.Unload();
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        GeneratedLevel Floor(int floor)
        {
            _levels.Values = new LevelValues();
            GeneratedLevel level = _levels.Build(RunSeeds.FloorSeed(RunSeed, floor), floor);
            Assert.That(level, Is.Not.Null);
            foreach (Enemy e in level.Enemies) if (e != null) e.gameObject.SetActive(false); // the rooms still: nothing comes at her
            return level;
        }

        void Follow()
        {
            _levels.FollowLamplit();
            if (_levels.Level != null && _levels.Level.Mood != null) _levels.Level.Mood.Tick();
            if (_levels.Lamplit != null) _levels.Lamplit.Tick();
        }

        static string LightsText(LamplitView view) =>
            string.Join("\n", view.Root.GetComponentsInChildren<Light>(true).Where(l => l.name.StartsWith("Lamp_")).Select(l => l.name + " " + l.color + " " + l.range + " " + l.transform.position.ToString("F4")));

        static string SourcesText(GeneratedLevel level) =>
            string.Join("\n", level.Mood.Plan.Rooms.SelectMany(r => r.Sources.Select(s => r.Room.Order + " " + s.Kind + " " + s.At)));

        [UnityTest]
        public IEnumerator SwitchOff_NoLids_TheLampsAndTheMoodAsWithEachOfTheThreeOff()
        {
            foreach (int f in new[] { 3, 5 })
            {
                LookListValues.On = false;
                GeneratedLevel level = Floor(f);
                yield return Frames(2);
                Follow();
                LamplitView view = _levels.Lamplit;
                Assert.That(view != null && view.On != null, "lamplit C on");
                Assert.That(view.Tops, Is.Null, "off: no lids");
                Assert.That(view.Root.GetComponentsInChildren<Renderer>(true).Any(r => r.name.StartsWith(LookListTops.Name)), Is.False);
                Assert.That(view.Plan.Lamps.All(l => l.Share == 1f && l.Along == 0d && !l.ByLookList), "off: no lamp of the order");
                string lights = LightsText(view), sources = SourcesText(level);
                int sconces = view.SconceRenderers.Count;

                LookListValues.On = true;
                LookListValues.LitTops = LookListValues.OssuaryCandles = LookListValues.WarmPassages = false;
                level = Floor(f);
                yield return Frames(2);
                Follow();
                view = _levels.Lamplit;
                Assert.That(view.Tops, Is.Null);
                Assert.That(LightsText(view), Is.EqualTo(lights), "the lamps' lights, field for field, floor " + f);
                Assert.That(SourcesText(level), Is.EqualTo(sources), "the mood's sources, floor " + f);
                Assert.That(view.SconceRenderers.Count, Is.EqualTo(sconces));
                LookListValues.Reset();
            }
        }

        [UnityTest]
        public IEnumerator On_TheThroneRoomsBlocksAndThePiersHaveLids_LitNeverBlack_OnlyWithALevel_AndGoneWithTheFloor()
        {
            GeneratedLevel level = Floor(5);
            yield return Frames(2);
            Follow();
            LamplitView view = _levels.Lamplit;
            LookListTops tops = view.Tops;
            Assert.That(tops, Is.Not.Null, "lids on floor 5");
            Assert.That(tops.Blocks, Is.EqualTo(level.SolidCaps.Count), "a lid on every carved block's cell");
            Assert.That(tops.Blocks, Is.GreaterThan(0), "the throne room's two pillars");
            Assert.That(tops.Piers, Is.GreaterThan(0));
            Assert.That(tops.LitFor, Is.SameAs(LightSteps.Of(3)));
            Assert.That(view.Root.GetComponentsInChildren<Collider>(true), Is.Empty, "nothing has a collider");
            float capTop = level.SolidCaps.Max(c => c.GetComponent<Renderer>().bounds.max.y);
            foreach (Renderer r in tops.Renderers)
            {
                Assert.That(r.sharedMaterial.shader.name, Is.EqualTo(MoodShapes.UnlitShaderName));
                Assert.That(r.shadowCastingMode, Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off));
            }
            for (int i = 0; i < tops.LidCount; i++)
                for (int k = 0; k < tops.PointsOf(i); k++)
                {
                    Color c = tops.ColourAt(i, k);
                    Assert.That(c.r + c.g + c.b, Is.GreaterThan(0.01f), "no lid point black");
                    Assert.That(Mathf.Max(c.r, c.g, c.b), Is.LessThanOrEqualTo(LookListValues.TopMost * LookListValues.RimShare + 1e-4f));
                }

            // She in the throne room: its lids drawn, a lid's top over the carved top.
            LevelRoom throne = level.Layout.ExitRoom;
            _player.Teleport(level.Middle(throne));
            yield return Frames(3);
            int throneLid = Enumerable.Range(0, tops.LidCount).First(i => tops.OwnersOf(i).Contains(throne.Order) && tops.OwnersOf(i).Length == 1);
            Assert.That(tops.Renderers[throneLid].enabled, Is.True, "the throne room's lids drawn while she is in it");
            Assert.That(tops.Renderers[throneLid].bounds.min.y, Is.GreaterThan(capTop), "over the carved top");

            // Another level: coloured again (the lamps stronger at B? weaker: B's lamps 1.15 against C's 1.3), once.
            int colourings = tops.Colourings;
            Color before = tops.ColourAt(throneLid, 0);
            GameSettings.SetLightStep(2, keep: false);
            yield return Frames(3);
            Assert.That(tops.Colourings, Is.EqualTo(colourings + 1), "coloured again for B, once");
            Assert.That(tops.ColourAt(throneLid, 0), Is.Not.EqualTo(before));
            yield return Frames(5);
            Assert.That(tops.Colourings, Is.EqualTo(colourings + 1), "and not again while nothing changes");

            // As before: none drawn; the greybox: none drawn; back to C: drawn.
            GameSettings.SetLightStep(0, keep: false);
            yield return Frames(3);
            Assert.That(tops.Renderers.Any(r => r.enabled), Is.False, "as before: no lid drawn");
            GameSettings.SetLightStep(3, keep: false);
            yield return Frames(3);
            Assert.That(tops.Renderers[throneLid].enabled, Is.True, "lamplit C again: drawn");
            GameSettings.SetGreybox(true, keep: false);
            yield return Frames(3);
            Assert.That(tops.Renderers.Any(r => r != null && r.enabled), Is.False, "the greybox: no lid drawn");
            GameSettings.SetGreybox(false, keep: false);
            yield return Frames(3);

            // Gone with the floor: its meshes and its material too.
            var meshes = tops.Renderers.Select(r => r.GetComponent<MeshFilter>().sharedMesh).ToList();
            Material material = tops.Renderers[0].sharedMaterial;
            Floor(1);
            yield return Frames(3);
            Assert.That(meshes.All(m => m == null), "every lid's mesh destroyed with the floor");
            Assert.That(material == null, "the lids' material destroyed with the floor");
        }

        [UnityTest]
        public IEnumerator AFrame_AllocatesNothing_WithTheLids_WhileNothingChanges()
        {
            GeneratedLevel level = Floor(5);
            yield return Frames(2);
            Follow();
            _player.Teleport(level.Middle(level.Layout.ExitRoom));
            yield return Frames(3);
            LamplitView view = _levels.Lamplit;
            Assert.That(view.Tops, Is.Not.Null);
            Follow();
            Assert.That(() => { for (int i = 0; i < 300; i++) { _levels.FollowLamplit(); view.Tick(); level.Mood.Tick(); } }, AllocExt.AllocatingGCMemory(Is.Not), "a frame allocates nothing");
        }

        [UnityTest]
        public IEnumerator APassagesSconce_HangsBesideItsLantern_AndTheOssuarysCandlesBurn()
        {
            GeneratedLevel level = Floor(3);
            yield return Frames(2);
            Follow();
            LamplitView view = _levels.Lamplit;
            LevelRoom passage = level.Layout.Rooms[1];
            Assert.That(passage.Kind, Is.EqualTo(RoomKind.Passage));
            Lamp sconce = view.Plan.Of(passage.Order).FirstOrDefault(l => l.Kind == LampKind.Sconce);
            Assert.That(sconce, Is.Not.Null, "floor 3's first passage has a warm sconce");
            Assert.That(sconce.Along, Is.Not.EqualTo(0d), "beside a lantern (both its far faces hold one)");
            Assert.That(view.Plan.Of(passage.Order).Where(l => l.Kind == LampKind.Lantern).All(l => l.Share == LookListValues.LanternShare), "its lanterns turned down");
            Assert.That(view.Root.GetComponentsInChildren<Renderer>(true).Any(r => r.name == "Lamplit_SconceFlames_" + passage.Order), "its flame drawn");
            LevelRoom ossuary = level.Layout.Rooms.First(r => r.Theme == RoomTheme.Ossuary);
            Assert.That(level.Mood.Plan.Of(ossuary.Order).Sources.Count(s => s.ByLookList), Is.GreaterThan(0), "the ossuary's candles in the mood");
            Assert.That(view.Plan.Of(ossuary.Order).Count(l => l.ByLookList && l.Kind == LampKind.Candle), Is.GreaterThan(0), "and lamps at them");
        }
    }
}
