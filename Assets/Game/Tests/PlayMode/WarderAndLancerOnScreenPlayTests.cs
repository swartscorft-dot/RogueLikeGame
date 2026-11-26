using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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
    /// "Only when seen" for the warder's bolt and the lancer's charge (work order 140, part 2; told that the warder alone still throws its
    /// bolt from off his screen and the lancer begins its charge from off it, Kristian ticked "Ja, begge"), measured: on floors 4 and 5 of
    /// a few run seeds, every combat room that holds a lancer or a warder is fought by a simple stand-in for her (she cannot be hurt; she
    /// walks toward the nearest enemy of the room until she is within <see cref="Closes"/> of it and shoots it with the basic shot, the
    /// camera following her as in play), once with <see cref="RangedOpeningRules.WarderAndLancerToo"/> off and once on. Counted: each
    /// warder's bolt and each lancer's charge begun (its AttackStarted), whether the enemy stood on his screen at that moment (no
    /// margin), and each lancer's charges a minute of the time it was alive in its awake fight. With the switch on, none begins off
    /// screen. Writes what it found ("WARDLANCE"); nobody has played it: these are numbers of a stand-in, not of his play.
    ///
    /// Frames without a window are short (about 0.3 ms): the fights run at <see cref="Speed"/> times game speed and every wait counts
    /// game seconds.
    /// </summary>
    public class WarderAndLancerOnScreenPlayTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly string[] RunSeedTexts = { "BENCH032", "WARDLANC", "LANCEWRD" };
        const float Closes = 8f;          // m from the nearest enemy she walks to before she stands and shoots
        const float MostSeconds = 60f;    // game seconds a room is fought at most
        const float Speed = 3f;           // game time's speed in the fights

        PlayerCharacter _player;
        PlayerHealth _health;
        GeneratedLevels _levels;
        bool _before;
        Vector3 _aimAt;
        bool _shoot;
        Vector2 _move;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _before = RangedOpeningRules.WarderAndLancerToo;
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = Input;
            _health = _player.GetComponent<PlayerHealth>();
            _health.Untouchable = true;
            _levels = GeneratedLevels.Ensure();
            _levels.Values = new LevelValues();
        }

        [TearDown]
        public void CleanUp()
        {
            RangedOpeningRules.WarderAndLancerToo = _before;
            if (_player != null) _player.GetComponent<PlayerInputDriver>().InputSource = null;
            if (_health != null) _health.Untouchable = false;
            if (FloorMap.Current != null) Object.Destroy(FloorMap.Current.gameObject);
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        InputFrame? Input()
        {
            Camera cam = Camera.main;
            if (cam == null) return null;
            Vector3 screen = cam.WorldToScreenPoint(new Vector3(_aimAt.x, 0f, _aimAt.z));
            return new InputFrame { CursorScreen = new Vector2(screen.x, screen.y), ShootPressed = _shoot, ShootHeld = _shoot, Move = _move };
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        sealed class Tally
        {
            public int Bolts, BoltsOff, Charges, ChargesOff, Rooms, Lancers, Warders;
            public float LancerSeconds;
            public float ChargesAMinute => LancerSeconds > 0f ? Charges * 60f / LancerSeconds : 0f;

            public override string ToString() => string.Format(Inv,
                "{0} rooms ({1} lancers, {2} warders): warder bolts {3}, begun off screen {4}; lancer charges {5}, begun off screen {6}; {7:0.0} lancer-seconds alive, {8:0.00} charges a minute a lancer",
                Rooms, Lancers, Warders, Bolts, BoltsOff, Charges, ChargesOff, LancerSeconds, ChargesAMinute);
        }

        /// <summary>Every combat room of floors 4 and 5 of the run seeds with a lancer or a warder, fought by the stand-in; the tallies by floor.</summary>
        IEnumerator FightThem(bool on, Dictionary<int, Tally> tallies)
        {
            RangedOpeningRules.WarderAndLancerToo = on;
            foreach (string text in RunSeedTexts)
            {
                Assert.That(LevelSeed.TryParse(text, out ulong run), Is.True, text);
                foreach (int floor in new[] { 4, 5 })
                {
                    if (!tallies.ContainsKey(floor)) tallies[floor] = new Tally();
                    Tally tally = tallies[floor];
                    ulong seed = RunSeeds.FloorSeed(run, floor);
                    GeneratedLevel first = _levels.Build(seed, floor);
                    Assert.That(first, Is.Not.Null);
                    yield return Frames(2);
                    var orders = first.Layout.Rooms
                        .Where(r => r.Kind == RoomKind.Combat && first.EnemiesByRoom.TryGetValue(r.Order, out List<Enemy> l) && l.Any(e => e is EnemyLancer || e is EnemyWarder))
                        .Select(r => r.Order).ToList();
                    foreach (int order in orders)
                    {
                        // Each room on a floor built afresh, the other rooms' enemies gone: one fight at a time, the same each time.
                        GeneratedLevel level = _levels.Build(seed, floor);
                        yield return Frames(2);
                        yield return FightRoom(level, level.Layout.Rooms[order], tally);
                    }
                }
            }
            Time.timeScale = 1f;
        }

        IEnumerator FightRoom(GeneratedLevel level, LevelRoom room, Tally tally)
        {
            foreach (KeyValuePair<int, List<Enemy>> kv in level.EnemiesByRoom)
                if (kv.Key != room.Order)
                    foreach (Enemy e in kv.Value)
                        if (e != null) Object.Destroy(e.gameObject);
            yield return Frames(2);
            List<Enemy> own = level.EnemiesByRoom[room.Order].Where(e => e != null).ToList();
            RoomFight fight = _levels.FightOf(room);
            Assert.That(fight, Is.Not.Null, "room " + room.Letter + room.Order + " holds its fight");
            var lancers = own.OfType<EnemyLancer>().ToList();
            var warders = own.OfType<EnemyWarder>().ToList();
            tally.Rooms++;
            tally.Lancers += lancers.Count;
            tally.Warders += warders.Count;
            var handlers = new List<(Enemy, Action<EnemyReport>)>();
            foreach (Enemy e in lancers.Cast<Enemy>().Concat(warders))
            {
                Enemy me = e;
                Action<EnemyReport> h = r =>
                {
                    if (r.Kind != EnemyReportKind.AttackStarted) return;
                    bool off = !RangedOpening.OnScreen(me.transform.position, 0f);
                    if (me is EnemyLancer) { tally.Charges++; if (off) tally.ChargesOff++; }
                    else { tally.Bolts++; if (off) tally.BoltsOff++; }
                };
                e.Reported += h;
                handlers.Add((e, h));
            }
            LevelLayout layout = level.Layout;
            LevelDoor door = LevelGeometry.EntryDoor(layout, room);
            _player.Teleport(level.ToWorld(RoomDoorRules.PullInPlace(layout, room, door, _levels.Values.Fights.PointIn + 1.5, 0d)));
            for (int i = 0; i < 60 && !fight.On; i++) yield return null;
            Time.timeScale = Speed;
            float until = Time.time + MostSeconds;
            while (Time.time < until && fight.On && own.Any(e => e != null && !e.IsDead))
            {
                Enemy target = null;
                float best = float.PositiveInfinity;
                foreach (Enemy e in own)
                {
                    if (e == null || e.IsDead) continue;
                    float d = Flat(e.transform.position, _player.Position);
                    if (d < best) { best = d; target = e; }
                }
                if (target != null)
                {
                    _aimAt = target.transform.position;
                    _shoot = true;
                    Vector3 to = target.transform.position - _player.Position;
                    _move = best > Closes ? new Vector2(Mathf.Abs(to.x) > 0.4f * best ? Mathf.Sign(to.x) : 0f, Mathf.Abs(to.z) > 0.4f * best ? Mathf.Sign(to.z) : 0f) : Vector2.zero;
                }
                foreach (EnemyLancer l in lancers)
                    if (l != null && !l.IsDead && l.IsAwake) tally.LancerSeconds += Time.deltaTime;
                yield return null;
            }
            Time.timeScale = 1f;
            _shoot = false;
            _move = Vector2.zero;
            foreach ((Enemy e, Action<EnemyReport> h) in handlers)
                if (e != null) e.Reported -= h;
            foreach (Enemy e in own)
                if (e != null && !e.IsDead) e.GetComponent<Damageable>().TakeHit(100000000);
            yield return Frames(3);
        }

        [UnityTest, Timeout(3000000)]
        public IEnumerator OnFloors4And5_OffAndOn_HowOftenEachBeganOffScreen_AndTheLancersChargesAMinute()
        {
            Assert.That(_levels.Values.Lancers && _levels.Values.Warders && _levels.Values.RangedAroundTheRoom, Is.True, "the defaults: lancers and warders in the floors, the ranged rule on");
            var off = new Dictionary<int, Tally>();
            var onT = new Dictionary<int, Tally>();
            yield return FightThem(false, off);
            yield return FightThem(true, onT);
            foreach (int floor in new[] { 4, 5 })
            {
                string line = string.Format(Inv, "WARDLANCE floor {0} switch OFF (before): {1}\nWARDLANCE floor {0} switch ON (after): {2}", floor, off[floor], onT[floor]);
                Debug.Log(line);
                TestContext.WriteLine(line);
            }
            foreach (int floor in new[] { 4, 5 })
            {
                Assert.That(onT[floor].BoltsOff, Is.EqualTo(0), "floor " + floor + ": with the switch on no warder began its bolt off screen");
                Assert.That(onT[floor].ChargesOff, Is.EqualTo(0), "floor " + floor + ": with the switch on no lancer began its charge off screen");
            }
            Assert.That(off.Values.Sum(t => t.Rooms), Is.GreaterThan(0), "rooms with a lancer or a warder were fought");
        }
    }
}
