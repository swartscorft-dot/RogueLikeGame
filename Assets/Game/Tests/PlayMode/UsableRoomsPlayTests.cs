using System;
using System.Collections;
using System.Collections.Generic;
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
    /// Rooms you can use (work order 088), built in the ControlRoom scene through the intents of <see cref="GeneratedLevels"/>: in a
    /// floor of a fixed seed, an arrow loosed by the room's own shooter from where it stands at her, down behind a low prop the
    /// generator placed for the duck, is stopped while she is down and she takes nothing; behind a tall cover the generator placed,
    /// the line from a shooter's place to her is broken by the built cover; the greybox colours every built prop as the one sorted
    /// table says (<see cref="PropHeights"/>: red tall, blue low, purple neither); and the room lab builds its room with the cover
    /// the generator gave it, as a floor's.
    ///
    /// Frames without a window are short (about 0.3 ms): the waits here count frames or real time, never a frame's length.
    /// </summary>
    public class UsableRoomsPlayTests
    {
        PlayerCharacter _player;
        PlayerHealth _health;
        FeelValues _feel;
        bool _takeCoverWas, _coverStopsWas, _quickDuckWas;
        GeneratedLevels _levels;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            _health.MaxHealth = 100000;
            _health.RestoreFull();
            _feel = _player.Feel;
            _takeCoverWas = _feel.TakeCover;
            _coverStopsWas = _feel.CoverStopsShots;
            _quickDuckWas = _feel.QuickDuck;
            _feel.TakeCover = true;
            _feel.CoverStopsShots = true;
            _feel.QuickDuck = true;
        }

        [TearDown]
        public void CleanUp()
        {
            if (_feel != null)
            {
                _feel.TakeCover = _takeCoverWas; // the feel values live in an asset
                _feel.CoverStopsShots = _coverStopsWas;
                _feel.QuickDuck = _quickDuckWas;
            }
            if (_player != null)
            {
                _player.HoldCover(false);
                _player.GetComponent<PlayerInputDriver>().InputSource = null;
            }
            if (FloorMap.Current != null) Object.Destroy(FloorMap.Current.gameObject);
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            if (TimingFeedback.Current != null) Object.Destroy(TimingFeedback.Current.gameObject);
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        /// <summary>A built room's own shooter at a place, the others of the floor gone; it stands where it is.</summary>
        EnemyShooter OnlyShooterAt(GeneratedLevel level, LevelRoom room, Vector3 at)
        {
            EnemyShooter keep = null;
            if (level.EnemiesByRoom.TryGetValue(room.Order, out List<Enemy> list))
                keep = list.OfType<EnemyShooter>().Where(e => e != null).OrderBy(e => Flat(e.transform.position - at).sqrMagnitude).FirstOrDefault();
            foreach (List<Enemy> enemies in level.EnemiesByRoom.Values)
                foreach (Enemy e in enemies)
                    if (e != null && e != keep) Object.Destroy(e.gameObject);
            if (keep != null) keep.Values.MoveSpeed = 0f;
            return keep;
        }

        /// <summary>
        /// The first seed (floors 2 and 3: the shooters shoot single arrows there) whose floor has a combat room with a low cover the
        /// generator added that serves the duck against one of its shooters at most 10.5 m from her spot (in its reach of 11 m).
        /// </summary>
        static bool FindLow(LevelValues v, out ulong seed, out int floor, out int order, out LevelPoint spot, out LevelPoint shooter)
        {
            for (ulong s = 1; s < 200; s++)
                foreach (int f in new[] { 2, 3 })
                {
                    LevelLayout l = LevelGenerator.Generate(s, f, v);
                    foreach (LevelRoom r in l.Rooms)
                    {
                        if (r.Kind != RoomKind.Combat) continue;
                        FightFrame frame = FightCover.FrameOf(l, r, v);
                        if (frame == null || !frame.HasShooters) continue;
                        foreach (LevelProp p in r.Props.Where(x => x.Fight && FightCover.IsLow(x)))
                            foreach (LevelPoint q in frame.Shooters)
                            {
                                if (!FightCover.LowServes(l, frame, p, q, v)) continue;
                                double dx = p.Position.X - q.X, dy = p.Position.Y - q.Y, len = Math.Sqrt(dx * dx + dy * dy);
                                PropFootprint fp = p.Footprint;
                                double reach = 0;
                                foreach (LevelPoint c in fp.Corners()) reach = Math.Max(reach, ((c.X - p.Position.X) * dx + (c.Y - p.Position.Y) * dy) / len);
                                if (p.Shape == PropShape.Circle) reach = p.Radius;
                                double out1 = reach + v.CoverRules.DuckSpot;
                                var at = new LevelPoint(p.Position.X + dx / len * out1, p.Position.Y + dy / len * out1);
                                if (at.DistanceTo(q) > 10.5) continue;
                                seed = s;
                                floor = f;
                                order = r.Order;
                                spot = at;
                                shooter = q;
                                return true;
                            }
                    }
                }
            seed = 0;
            floor = order = 0;
            spot = shooter = default;
            return false;
        }

        [UnityTest]
        public IEnumerator AnArrow_FromAShootersPlace_AtHerDownBehindAPlacedLowProp_IsStopped_AndSheTakesNothing()
        {
            _levels = GeneratedLevels.Ensure();
            _levels.Values = new LevelValues { RoomFights = false }; // one shooter, provoked alone; the generator never reads the switch
            Assert.That(FindLow(_levels.Values, out ulong seed, out int floor, out int order, out LevelPoint spot, out LevelPoint q), Is.True, "a placed low cover on a shooter's line");
            GeneratedLevel level = _levels.Build(seed, floor);
            Assert.That(level, Is.Not.Null);
            yield return Frames(2);
            LevelRoom room = level.Layout.Rooms[order];
            EnemyShooter shooter = OnlyShooterAt(level, room, level.ToWorld(q));
            Assert.That(shooter, Is.Not.Null, "the room's shooter");
            yield return Frames(2);
            Assert.That(shooter.GetComponent<NavMeshAgent>().Warp(level.ToWorld(q)), Is.True);
            _player.Teleport(level.ToWorld(spot));
            yield return Frames(2);
            var strikes = new List<EnemyReport>();
            shooter.Reported += r => { if (r.Kind == EnemyReportKind.Struck) strikes.Add(r); };
            int health = _health.Current;
            shooter.Provoke();
            ThingsThatFly fly = ThingsThatFly.Ensure();
            var shown = new List<ThingsThatFly.Shown>();
            float Eta()
            {
                fly.ListFlying(shown);
                float best = float.PositiveInfinity;
                foreach (ThingsThatFly.Shown s in shown)
                {
                    if (s.Owner != shooter || s.Ending || s.Passed) continue;
                    float d = Flat(s.Position - _player.Position).magnitude - shooter.Tuning.ArrowRadius - Placeholders.PlayerRadius;
                    best = Mathf.Min(best, Mathf.Max(0f, d) / Mathf.Max(0.1f, shooter.Tuning.ArrowSpeed));
                }
                return best;
            }
            yield return Until(() => Eta() < 0.25f, 8f);
            Assert.That(Eta(), Is.LessThan(0.25f), "an arrow on its way, about to arrive (seed " + LevelSeed.ToText(seed) + " floor " + floor + " room " + order + ")");
            _player.HoldCover(true); // the key going down
            Assert.That(_player.IsQuickDucking, Is.True, "down behind the placed low prop");
            yield return Until(() => strikes.Count > 0, 2f);
            Assert.That(strikes.Count, Is.GreaterThan(0), "the arrow arrived");
            Assert.That(strikes[0].Outcome, Is.EqualTo(StrikeOutcome.StoppedByCover), "the low prop she is down behind stopped it");
            Assert.That(_health.Current, Is.EqualTo(health), "she took nothing");
            _player.HoldCover(false);
        }

        [UnityTest]
        public IEnumerator BehindAPlacedTallCover_TheLineFromAShootersPlaceToHer_IsBroken()
        {
            _levels = GeneratedLevels.Ensure();
            LevelValues v = _levels.Values;
            int found = 0, tried = 0;
            for (ulong s = 1; s < 60 && found < 6; s++)
            {
                LevelLayout l = LevelGenerator.Generate(s, 2, v);
                var hides = new List<(LevelPoint q, LevelPoint spot)>();
                foreach (LevelRoom r in l.Rooms)
                {
                    FightFrame f = FightCover.FrameOf(l, r, v);
                    if (f == null || !f.HasRanged) continue;
                    foreach (LevelProp p in r.Props.Where(x => x.Fight && FightCover.IsTall(x)))
                        foreach (LevelPoint q in f.Ranged)
                        {
                            if (!FightCover.Hides(l, r, FightCover.PropThing(p), q, v)) continue;
                            double dx = p.Position.X - q.X, dy = p.Position.Y - q.Y, len = Math.Sqrt(dx * dx + dy * dy);
                            double out1 = (p.Shape == PropShape.Circle ? p.Radius : Math.Max(p.Width, p.Depth) * 0.5) + v.CoverRules.HideBehind * 0.5;
                            hides.Add((q, new LevelPoint(p.Position.X + dx / len * out1, p.Position.Y + dy / len * out1)));
                            break;
                        }
                }
                if (hides.Count == 0) continue;
                GeneratedLevel level = _levels.Build(s, 2);
                yield return Frames(2);
                Physics.SyncTransforms();
                foreach ((LevelPoint q, LevelPoint spot) in hides)
                {
                    Vector3 a = level.ToWorld(q), b = level.ToWorld(spot);
                    float distance = Flat(b - a).magnitude;
                    float clear = Cover.Line(new GroundPoint(a.x, a.z), new GroundPoint(b.x, b.z));
                    tried++;
                    Assert.That(clear, Is.LessThan(distance - 0.1f), "the built tall cover breaks the line, seed " + LevelSeed.ToText(s) + " floor 2");
                    found++;
                }
            }
            TestContext.WriteLine("USABLETALL " + tried + " lines from a shooter's or a caster's place to her behind a placed tall cover, every one broken");
            Assert.That(found, Is.GreaterThan(0), "placed tall covers found");
        }

        /// <summary>The greybox's part of every built prop is what the one sorted table says it does.</summary>
        [UnityTest]
        public IEnumerator TheGreybox_ColoursEveryBuiltProp_AsTheSortedTableSays()
        {
            _levels = GeneratedLevels.Ensure();
            int props = 0;
            foreach ((ulong seed, int floor) in new[] { (7UL, 1), (11UL, 3), (13UL, 5) })
            {
                GeneratedLevel level = _levels.Build(seed, floor);
                yield return Frames(2);
                foreach (GeneratedLevel.PlacedProp p in level.Props.Concat(level.LargeProps))
                {
                    if (p?.Object == null) continue;
                    Cover cover = p.Object.GetComponent<Cover>();
                    GreyboxPart part = GreyboxRules.PropPart(p.Object.GetComponent<Collider>() != null, cover != null && cover.Stops, p.Object.GetComponent<Shelter>() != null, p.Prop.Model == PropModel.Chest);
                    PropSize size = PropSize.Of(p.Prop.Model);
                    GreyboxPart wanted = p.Prop.Model == PropModel.Chest ? GreyboxPart.AsItIs
                        : size.Walkable ? GreyboxPart.Hidden
                        : PropHeights.Of(p.Prop.Model) == PropHeightClass.Tall ? GreyboxPart.HighCover
                        : PropHeights.Of(p.Prop.Model) == PropHeightClass.Low ? GreyboxPart.LowCover
                        : GreyboxPart.BlocksWalking;
                    Assert.That(part, Is.EqualTo(wanted), p.Prop.Model + " (" + PropHeights.Of(p.Prop.Model) + ") seed " + seed + " floor " + floor);
                    props++;
                }
            }
            TestContext.WriteLine("USABLEGREYBOX " + props + " built props, every one in the colour of the sorted table");
        }

        /// <summary>The room lab builds its room with the cover the generator gave it (the props stage of a floor): the same props, the tall ones stopping shots.</summary>
        [UnityTest]
        public IEnumerator TheRoomLab_BuildsItsRoom_WithTheCoverAFloorsRoomGets()
        {
            _levels = GeneratedLevels.Ensure();
            List<LabEntry> entries = RoomLab.Entries(_levels.Values).Where(e => e.Template != null && e.InFloors).ToList();
            int built = 0, cover = 0;
            foreach (LabEntry entry in entries.Take(6))
            {
                GeneratedLevel level = _levels.StartLab(entry, 1, 4242UL);
                Assert.That(level, Is.Not.Null, "lab room " + entry.Template.Name);
                yield return Frames(2);
                LevelLayout wanted = RoomLab.Build(entry, RoomLab.Mixes[1], 4242UL, _levels.Values);
                LevelRoom room = level.Layout.Rooms[1];
                Assert.That(room.Props.Select(p => p.ToString()), Is.EqualTo(wanted.Rooms[1].Props.Select(p => p.ToString())), "the lab's room is the generator's: " + entry.Template.Name);
                Assert.That(room.Props.Count(p => p.Fight), Is.EqualTo(room.CoverTallAdded + room.CoverLowAdded));
                foreach (GeneratedLevel.PlacedProp p in level.Props.Where(x => x.Room == room && x.Prop.Fight))
                {
                    Assert.That(p.Object.GetComponent<Cover>() != null, Is.EqualTo(FightCover.IsTall(p.Prop)), p.Prop.Model + ": tall stops shots, low does not");
                    cover++;
                }
                built++;
            }
            TestContext.WriteLine("USABLELAB " + built + " lab rooms built, " + cover + " covers placed for their fights");
            Assert.That(cover, Is.GreaterThan(0), "the lab's rooms hold cover for their fights");
        }
    }
}
