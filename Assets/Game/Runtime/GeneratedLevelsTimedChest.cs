using System.Collections.Generic;
using System.Globalization;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace Roguelike
{
    /// <summary>
    /// The timed chest (work order 153; Kristian's tick of 2026-11-25, "Tidskisten": "The timed chest: open it to fight, and the reward
    /// shrinks while you fight"; the rules in <see cref="TimedChestRules"/>, the numbers in <see cref="LevelValues.TimedChestRules"/>, the
    /// switch <see cref="LevelValues.TimedChests"/> with the ROOMS page's "Timed chest", <see cref="TimedChestSettings"/>).
    ///
    /// As a floor's fights are set up (<see cref="SetUpRoomFights"/>, after its callers, waves and doors), the room the rules give it
    /// (<see cref="TimedChestRules.Room"/>: from floor 2, one a floor) gets a chest, the game's own treasure chest's model with a collider and
    /// a carved place in the NavMesh of its own, at the first of its corner places the floor's NavMesh holds with nothing of the floor in its
    /// way. Nothing else of the floor changes: no enemy, no prop, no door, no sign (the door signs hold one shape a door and a combat room has
    /// none: the order's "if not, leave the sign").
    ///
    /// Shut and dim (<see cref="TimedChestValues.DimShade"/>) until the room's own fight is cleared and its doors are open again; then it glows
    /// faintly (<see cref="TimedChestValues.GlowTint"/>) and the interact key opens it (<see cref="UseTimedChest"/>, the drops' and the chests'
    /// key and prompt, "Open: a fight, the reward shrinks"). It is never opened by walking past it, and she may leave it shut.
    ///
    /// Opened: one wave of the room's own two kinds (<see cref="TimedChestRules.WaveCounts"/>), made then, the floor's health and damage for
    /// each, one of the room's own (<see cref="GeneratedLevel.Join"/>), waiting out of the game; the room's fight wakes again (no second cue,
    /// no second room_fight line), so the doors shut behind her as at any waking, and the room is not cleared until the wave is dead. Its
    /// enemies arrive by work order 136's rule (<see cref="RoomWaveRules.Pick"/>: on his screen inside the ranged rule's margin, 7 to 11 m
    /// from her, never nearer than 3, never below her on his screen, apart from the others, on the NavMesh with a path to her), each after
    /// its 1 s disc (<see cref="PlaceArrival"/>), at most <see cref="RoomWaveValues.MostAtOnce"/> at one moment and the rest a group at a
    /// time; a shooter's or a caster's second's grace runs from its arrival. No caller, no elite; no enemy drops anything (none does).
    ///
    /// Its reward: <see cref="TimedChestRules.Whole"/> Echoes, shown over the chest and on the HUD, whole until 9 s after the opening, then
    /// 1 % of the whole less a second, never under 10 % (<see cref="TimedChestRules.Share"/>), frozen at the wave's last kill; the doors open
    /// as the room is cleared again, and the same key takes what is left (<see cref="TimedChestRules.Left"/>) into the run's Echoes
    /// (<see cref="RunEarnings.FromTimedChests"/>, in a run with what lasts between runs on). Her death in its fight: nothing. The recorder
    /// writes one line a floor (<see cref="PlaySessionRecorder.NoteTimedChest"/>).
    ///
    /// None in a rehearsal, in the room lab, with what lasts between runs off (its reward is Echoes), with the switch or the page's line off;
    /// the benchmark builds its floors with the switch off. Off: every floor as on fb15f9c, field for field.
    /// </summary>
    public sealed partial class GeneratedLevels
    {
        /// <summary>Where a floor's timed chest stands in its life (work order 153).</summary>
        public enum TimedChestState { Shut, Openable, Fighting, Won, Taken, Lost }

        /// <summary>A floor's timed chest (work order 153).</summary>
        public sealed class TimedChest
        {
            public LevelRoom Room;
            public RoomFight Fight;
            public GameObject Object;
            /// <summary>Where it stands (on the NavMesh), and its corner place of the rules.</summary>
            public Vector3 Place;
            public LevelPoint At;
            public TimedChestState State;
            /// <summary>The Echoes it holds whole; the Echoes taken (0 until then).</summary>
            public int Whole, Taken;
            /// <summary>Game times: the room cleared (it could be opened), opened, its wave's last kill; minus one before.</summary>
            public float OpenableAt = -1f, OpenedAt = -1f, WonAt = -1f;
            /// <summary>Its wave's enemies, made at the opening; those still to arrive; those standing in their discs; every arrival so far.</summary>
            public readonly List<Enemy> Wave = new List<Enemy>();
            public readonly List<Enemy> Waiting = new List<Enemy>();
            public readonly List<WaveArrival> Arriving = new List<WaveArrival>();
            public readonly List<WaveArrival> Arrived = new List<WaveArrival>();
            /// <summary>Its wave's kinds as the recorder writes them ("swarmer 4, brute 2").</summary>
            public string Kinds = "";
            /// <summary>Her damage taken from the opening to its end.</summary>
            public int Damage;
            /// <summary>True once its line is written.</summary>
            public bool Told;
            /// <summary>Its floor, and its room as the run box counts, kept as it was set up (the floor may be gone when its line is written).</summary>
            public int FloorNumber, RoomNumber;
            internal float NextTryAt;
            internal Material Material;
            internal List<LevelPoint> Points;
            internal Vector3[] OnMesh;
            internal sbyte[] OnMeshKnown; // 0 not asked, 1 on it, -1 not
            internal LevelRandom Rng;
            internal GUIContent[] Over, Takes, Hud; // by the percent shown, 0 to 100 (the producer's round 2)
            internal GUIContent Open;
            /// <summary>Its pool of warm light on the floor while it is lit (the producer's round 2); null while unlit.</summary>
            public MeshRenderer Pool;
            internal Mesh PoolMesh;
            internal Material PoolMaterial;

            /// <summary>The seconds its reward has fallen for: from the opening to now, or to its wave's last kill.</summary>
            public float Seconds(float now) => OpenedAt < 0f ? 0f : (WonAt >= 0f ? WonAt : now) - OpenedAt;
        }

        TimedChest _timedChest;
        readonly List<LevelPoint> _chestTaken = new List<LevelPoint>();
        NavMeshPath _chestPath;

        /// <summary>This floor's timed chest, or null (work order 153).</summary>
        public TimedChest TimedChestOfFloor => _timedChest;

        /// <summary>The share of its whole the chest's reward holds now (1 before it is opened), and its Echoes now.</summary>
        public double TimedChestShare => _timedChest == null ? 0d : TimedChestRules.Share(_timedChest.Seconds(Time.time), Values.TimedChestRules);
        public int TimedChestEchoes => _timedChest == null ? 0 : TimedChestRules.LeftAt(_timedChest.Whole, TimedChestShare, Values.TimedChestRules); // as shown and as paid

        /// <summary>From <see cref="SetUpRoomFights"/>, after the callers, the waves and the doors: the floor's chest, if the rules give it one.</summary>
        void SetUpTimedChest()
        {
            ForgetTimedChest();
            if (_level == null || _fightList.Count == 0 || !TimedChestSettings.InUse(Values) || InRehearsal || InLab || !MetaOn) return;
            LevelLayout layout = _level.Layout;
            TimedChestValues c = Values.TimedChestRules;
            int whole = TimedChestRules.Whole(layout.Floor, c);
            if (whole <= 0) return;
            var callers = new List<LevelRoom>();
            for (int i = 0; i < _roomCallers.Count; i++) callers.Add(_roomCallers[i].Room);
            LevelRoom room = TimedChestRules.Room(layout, Values, callers);
            RoomFight fight = FightOf(room);
            if (room == null || fight == null) return;
            Physics.SyncTransforms(); // the floor's colliders, made this frame, for the check of its place
            PropSize size = PropSize.Of(PropModel.Chest);
            var half = new Vector3((float)size.Width * 0.5f + ChestClear, ChestCheckHalfHeight, (float)size.Depth * 0.5f + ChestClear);
            int walls = 1 << _level.WallLayer;
            foreach (LevelPoint p in TimedChestRules.PlacesInOrder(layout, room, Values))
            {
                if (!NavMesh.SamplePosition(_level.ToWorld(p), out NavMeshHit hit, c.OnNavMeshWithin, NavMesh.AllAreas) || _level.RoomAt(hit.position) != room) continue;
                int rotation = FacingTheMiddle(layout, room, p);
                Quaternion turn = Quaternion.Euler(0f, rotation, 0f);
                if (Physics.CheckBox(hit.position + Vector3.up * (ChestCheckHalfHeight + ChestCheckFrom), half, turn, walls, QueryTriggerInteraction.Ignore)) continue; // a large prop, a wall piece
                var t = new TimedChest
                {
                    Room = room, Fight = fight, Place = hit.position, At = p, Whole = whole,
                    FloorNumber = Floor > 0 ? Floor : layout.Floor, RoomNumber = LevelGeometry.RoomNumber(layout, room) + 1,
                };
                MakeChest(t, rotation, c);
                MakeChestWords(t, c);
                _timedChest = t;
                ShowChest(t);
                Debug.Log(string.Format(CultureInfo.InvariantCulture, "Timed chest: floor {0}, room {1}{2} at ({3:0.0}, {4:0.0}), {5} Echoes", layout.Floor, room.Letter, room.Order,
                                        p.X, p.Y, whole));
                return;
            }
        }

        /// <summary>m round the chest's footprint that must be clear of everything on the walls' layer as it is put down.</summary>
        const float ChestClear = 0.2f;
        /// <summary>The check's box: from this far over the floor (never the floor itself), this half height.</summary>
        const float ChestCheckFrom = 0.1f, ChestCheckHalfHeight = 0.5f;

        /// <summary>The quarter turn whose front faces the room's middle most (the chest's front: its own +Z).</summary>
        static int FacingTheMiddle(LevelLayout layout, LevelRoom room, LevelPoint p)
        {
            LevelPoint m = room.Middle(layout.CellSize);
            double dx = m.X - p.X, dy = m.Y - p.Y;
            int best = 0;
            double bestDot = double.NegativeInfinity;
            for (int rot = 0; rot < 360; rot += 90)
            {
                LevelPoint f = LevelProp.Make(PropModel.Chest, p, rot, PropPlace.Floor).Front;
                double dot = f.X * dx + f.Y * dy;
                if (dot > bestDot)
                {
                    bestDot = dot;
                    best = rot;
                }
            }
            return best;
        }

        /// <summary>The chest: the treasure chest's model and collider (<see cref="PropFactory"/>), a material of its own, and its place carved out of the NavMesh.</summary>
        void MakeChest(TimedChest t, int rotation, TimedChestValues c)
        {
            LevelProp prop = LevelProp.Make(PropModel.Chest, t.At, rotation, PropPlace.Floor);
            CryptKitValues kit = Values.Crypt ?? new CryptKitValues();
            t.Material = PropFactory.MakeMaterial(PropModel.Chest, kit.Cyan, Values.PropRules != null ? Values.PropRules.Glow : 0f);
            t.Material.name = "TimedChest";
            t.Object = PropFactory.Make(prop, t.Place, _level.transform, _level.WallLayer, t.Material, "TimedChest_" + t.Room.Letter + t.Room.Order, _level.LowestPropCollider);
            var obstacle = t.Object.AddComponent<NavMeshObstacle>(); // enemies walk round it
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.size = new Vector3((float)prop.Width, Mathf.Max(0.5f, (float)prop.Height), (float)prop.Depth);
            obstacle.center = new Vector3(0f, obstacle.size.y * 0.5f, 0f);
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;
            // The cave rule: drawn with its room, as every piece of the floor is (a renderer made after the floor was built: the doors' way).
            if (_level.Cave != null)
                foreach (Renderer r in t.Object.GetComponentsInChildren<Renderer>(true)) _level.Cave.TrackDoor(r, t.Room.Order, t.Room.Order);
        }

        /// <summary>
        /// Its pool of warm light (the producer's round 2): lit while it can be opened, through its fight and while its reward waits; gone
        /// once taken or lost. The room mood's own pool (<see cref="MoodShapes.Pool"/>: added light on the floor, full at its middle and
        /// nothing at its rim) in the vault chest's gold, under the room's own mood root when the mood is on (so it is the room's, shown and
        /// rebuilt with it; made again by the chest's step if a rebuild took it), else under the floor's root; the cave rule draws it with its
        /// room. No light component: the rooms' budget of real lights is as it was. Made once a lighting, never in a frame of nothing changing.
        /// </summary>
        void LightTheChest(TimedChest t, bool on)
        {
            TimedChestValues c = Values.TimedChestRules;
            if (!on || c.PoolStrength <= 0f || c.PoolReach <= 0f)
            {
                if (t.Pool != null) Destroy(t.Pool.gameObject);
                t.Pool = null;
                return;
            }
            if (t.Pool != null) return;
            if (t.PoolMesh == null)
            {
                var b = new MoodShapes.Build();
                Rgb gold = Values.Mood != null ? Values.Mood.GoldColour : c.GlowTint;
                MoodShapes.Pool(b, new Vector3(t.Place.x, PoolHeight, t.Place.z), c.PoolReach, MoodShapes.Linear(gold, c.PoolStrength), PoolSegments, out _, out _);
                t.PoolMesh = b.ToMesh("TimedChest_Pool", false);
            }
            if (t.PoolMaterial == null) t.PoolMaterial = MoodShapes.MakeUnlit("TimedChest_Pool", MoodShapes.Blend.Add, null, PoolQueue);
            RoomMood.RoomParts parts = _level.Mood != null ? _level.Mood.Of(t.Room.Order) : null;
            Transform parent = parts != null && parts.Root != null ? parts.Root.transform : _level.transform;
            var go = new GameObject("TimedChest_Pool_" + t.Room.Order) { layer = CryptKit.Layer };
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            go.AddComponent<MeshFilter>().sharedMesh = t.PoolMesh;
            t.Pool = go.AddComponent<MeshRenderer>();
            t.Pool.sharedMaterial = t.PoolMaterial;
            t.Pool.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            t.Pool.receiveShadows = false;
            if (_level.Cave != null) _level.Cave.TrackDoor(t.Pool, t.Room.Order, t.Room.Order);
        }

        /// <summary>m over the floor the pool is drawn at (the mood's pools stand 0.03 m up); its segments (the mood's sources' 16); its draw order (the mood's glow's).</summary>
        const float PoolHeight = 0.03f;
        const int PoolSegments = 16, PoolQueue = 3001;

        /// <summary>Lit: it can be opened, its fight is on, or its reward waits.</summary>
        static bool Lit(TimedChest t) => t.State == TimedChestState.Openable || t.State == TimedChestState.Fighting || t.State == TimedChestState.Won;

        /// <summary>Every word the chest and the HUD show, made once for its whole: nothing is made while its number counts.</summary>
        void MakeChestWords(TimedChest t, TimedChestValues c)
        {
            // By the percent shown (the producer's round 2: the share beside the Echoes she would take now, rounded as paid).
            t.Over = new GUIContent[101];
            t.Takes = new GUIContent[101];
            t.Hud = new GUIContent[101];
            string currency = MetaValues != null ? MetaValues.CurrencyName : "Echoes";
            for (int p = 0; p <= 100; p++)
            {
                string echoes = TimedChestRules.Left(t.Whole, p / 100d, c).ToString(CultureInfo.InvariantCulture) + " " + currency;
                string share = p.ToString(CultureInfo.InvariantCulture) + " %";
                t.Over[p] = new GUIContent(share + c.Between + echoes);
                t.Takes[p] = new GUIContent(c.TakeWords + " " + echoes);
                t.Hud[p] = new GUIContent(c.HudWords + "   " + share + c.Between + echoes);
            }
            t.Open = new GUIContent(c.OpenWords);
        }

        /// <summary>Its look for its state, set once as the state changes: dim while shut, opened or done; a faint warm glow while it can be used.</summary>
        void ShowChest(TimedChest t)
        {
            Material m = t.Material;
            if (m == null) return;
            TimedChestValues c = Values.TimedChestRules;
            bool glow = Lit(t); // the producer's round 2: aglow with its pool through its fight too, so he sees where the reward is
            bool dim = !glow;
            LightTheChest(t, glow);
            Color shade = dim ? new Color(c.DimShade, c.DimShade, c.DimShade, 1f) : Color.white;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", shade);
            m.color = shade;
            if (!m.HasProperty("_EmissionColor")) return;
            if (glow || c.ShutLight > 0f)
            {
                if (m.HasProperty("_EmissionMap") && m.GetTexture("_EmissionMap") == null) m.SetTexture("_EmissionMap", PropFactory.Texture(PropModel.Chest)); // its own picture glows, not a flat colour
                Color lit = glow ? CryptKit.Glowing(c.GlowTint, c.Glow) : CryptKit.Glowing(new Rgb(1f, 1f, 1f), c.ShutLight); // shut: its shape read, no colour
                m.SetColor("_EmissionColor", QualitySettings.activeColorSpace == ColorSpace.Linear ? lit.gamma : lit);
                m.EnableKeyword("_EMISSION");
            }
            else
            {
                m.SetColor("_EmissionColor", Color.black);
                m.DisableKeyword("_EMISSION");
            }
        }

        /// <summary>The floor goes: its chest's line written if it was never written (not opened, or opened and left), its material destroyed (its object goes with the floor's root).</summary>
        void ForgetTimedChest()
        {
            TimedChest t = _timedChest;
            _timedChest = null;
            _chestTaken.Clear();
            if (t == null) return;
            StopCountingDamage(t);
            if (!t.Told) NoteTimedChest(t, t.State == TimedChestState.Shut || t.State == TimedChestState.Openable ? TimedChestEvents.NotOpened : TimedChestEvents.Left);
            for (int i = 0; i < t.Arriving.Count; i++)
                if (t.Arriving[i].Disc != null) Destroy(t.Arriving[i].Disc.gameObject);
            t.Arriving.Clear();
            if (t.Pool != null) Destroy(t.Pool.gameObject);
            t.Pool = null;
            if (t.PoolMesh != null) Destroy(t.PoolMesh);
            if (t.PoolMaterial != null) Destroy(t.PoolMaterial);
            if (t.Material != null) Destroy(t.Material);
        }

        /// <summary>The enemies of the chest's wave still to arrive in this room: the doors count them in play (0 for any other room). Allocates nothing.</summary>
        int TimedChestWaitingFor(int room)
        {
            TimedChest t = _timedChest;
            if (t == null || t.State != TimedChestState.Fighting || t.Room.Order != room) return 0;
            int n = 0;
            for (int i = 0; i < t.Wave.Count; i++)
                if (t.Wave[i] != null && !t.Wave[i].IsDead && t.Wave[i].WaitsForItsWave) n++;
            return n;
        }

        /// <summary>The chest she can use now: within <see cref="RunValues.ChestReach"/> of her, openable or holding its reward, with no choice, pause or end screen up. Null if none.</summary>
        public TimedChest TimedChestInReach()
        {
            TimedChest t = _timedChest;
            if (t == null || _player == null || t.Object == null || Choice != ChoiceKind.None || Paused || Ending != EndKind.None) return null;
            if (t.State != TimedChestState.Openable && t.State != TimedChestState.Won) return null;
            if (_health != null && !_health.IsAlive) return null;
            Vector3 p = _player.Position;
            return new Vector2(t.Place.x - p.x, t.Place.z - p.z).magnitude <= RunValues.ChestReach ? t : null;
        }

        /// <summary>The interact key on the chest (work order 153): opens it, or takes what is left of its reward. False when none is in reach.</summary>
        public bool UseTimedChest()
        {
            TimedChest t = TimedChestInReach();
            if (t == null) return false;
            if (t.State == TimedChestState.Openable) OpenTimedChest(t);
            else TakeTimedChest(t);
            return true;
        }

        /// <summary>It is opened: its wave is made and waits, the room's fight wakes again (the doors shut behind her), the clock begins.</summary>
        void OpenTimedChest(TimedChest t)
        {
            float now = Time.time;
            TimedChestValues c = Values.TimedChestRules;
            EnemyBrute template = PracticeEnemy();
            LevelLayout layout = _level.Layout;
            TimedChestRules.WaveCounts(layout, t.Room, c, out int leads, out int partners);
            TimedChestRules.WaveKinds(t.Room, out EnemyKind lead, out EnemyKind partner);
            int floor = Floor > 0 ? Floor : layout.Floor;
            float scale = Values.DamageScaleOf(floor);
            var kinds = new List<EnemyKind>(leads + partners);
            for (int i = 0; i < leads + partners; i++)
            {
                EnemyKind kind = i < leads ? lead : partner;
                Enemy e = template != null ? EnemyFactory.Make(kind, t.Place, _level.transform, template, "Chest_" + t.Room.Letter + t.Room.Order + "_" + i) : null;
                if (e == null) continue;
                e.HealthOverride = Values.HealthOf(kind);
                e.TakesTurns = true; // a floor's enemy: it takes turns to attack (work order 023)
                if (e is EnemyBrute brute) brute.FloorValues = Values.FloorBruteValues(template.Values);
                e.DamageScale *= scale; // work order 030, lever B: as the floor's own
                _level.Join(e, t.Room, roomsOwn: true); // one of the room's own: its doors, its clearing and "enemies left" count it
                e.Fight = t.Fight;
                e.WaitForItsWave();
                t.Wave.Add(e);
                t.Waiting.Add(e);
                kinds.Add(e.Kind);
            }
            t.Kinds = RoomWaveRules.KindsWords(kinds);
            t.State = TimedChestState.Fighting;
            t.OpenedAt = now;
            t.NextTryAt = now + Mathf.Max(0f, c.FirstDiscsAfter);
            t.Damage = 0;
            if (_health != null) _health.Damaged += CountChestDamage;
            ShowChest(t);
            // The room's fight wakes again for its wave, told as a waking already heard: no second cue and no second room_fight line.
            if (t.Fight.Stir(RoomFightStart.Provoked, now)) t.Fight.TakeStarted();
            Debug.Log("Timed chest opened: " + t.Kinds + " (" + t.Wave.Count + "), " + t.Whole + " Echoes");
        }

        void CountChestDamage(int amount)
        {
            if (_timedChest != null && _timedChest.State == TimedChestState.Fighting) _timedChest.Damage += amount;
        }

        void StopCountingDamage(TimedChest t)
        {
            if (_health != null) _health.Damaged -= CountChestDamage;
        }

        /// <summary>She takes what is left: into the run's Echoes (in a meta run), told to the recorder; the chest is done and dim.</summary>
        void TakeTimedChest(TimedChest t)
        {
            int left = TimedChestRules.LeftAt(t.Whole, TimedChestRules.Share(t.Seconds(Time.time), Values.TimedChestRules), Values.TimedChestRules); // what the chest showed
            t.Taken = left;
            t.State = TimedChestState.Taken;
            ShowChest(t);
            if (_metaRun && Earnings != null)
            {
                Earnings.FromTimedChests += left;
                PlaySessionRecorder.Current?.NoteBetweenRunsEarned(left, "timed chest", Floor);
            }
            PlaceholderSounds.Current?.AskFor(SoundMoment.DropTaken); // the drops' own sound as a reward is taken (work order 048)
            NoteTimedChest(t, TimedChestEvents.Taken);
            Debug.Log("Timed chest: " + left + " of " + t.Whole + " Echoes taken");
        }

        /// <summary>Every frame, after the waves and before the doors (work order 153). Allocates nothing while nothing changes.</summary>
        void TickTimedChest()
        {
            TimedChest t = _timedChest;
            if (t == null || _level == null) return;
            float now = Time.time;
            if (t.Pool == null && Lit(t) && t.Object != null) LightTheChest(t, true); // a rebuild of the room's mood took its pool: made again
            switch (t.State)
            {
                case TimedChestState.Shut:
                    // Openable once the room's own fight is cleared and its doors are open again.
                    RoomDoors d = DoorsOf(t.Room);
                    if (t.Fight.Starts > 0 && !t.Fight.On && AliveOf(t.Room.Order) == 0 && WaitingFor(t.Room.Order) == 0 && (d == null || !d.Shut))
                    {
                        t.State = TimedChestState.Openable;
                        t.OpenableAt = now;
                        ShowChest(t);
                    }
                    break;
                case TimedChestState.Fighting:
                    RoomWaveValues rules = Values.WaveRules ?? s_chestWaveRules;
                    TickChestDiscs(t, now, rules);
                    if (_health != null && !_health.IsAlive)
                    {
                        // Her death in its fight: nothing.
                        t.State = TimedChestState.Lost;
                        StopCountingDamage(t);
                        ShowChest(t);
                        NoteTimedChest(t, TimedChestEvents.Died);
                        break;
                    }
                    if (t.Waiting.Count > 0 && now >= t.NextTryAt && (rules.MostAtOnce <= 0 || t.Arriving.Count == 0)) ChestArrivals(t, now, rules);
                    if (t.Waiting.Count == 0 && t.Arriving.Count == 0 && AllDead(t.Wave))
                    {
                        // Its wave's last kill: the reward stops falling.
                        t.State = TimedChestState.Won;
                        t.WonAt = now;
                        StopCountingDamage(t);
                        ShowChest(t);
                    }
                    break;
            }
        }

        static readonly RoomWaveValues s_chestWaveRules = new RoomWaveValues();

        static bool AllDead(List<Enemy> wave)
        {
            for (int i = 0; i < wave.Count; i++) if (wave[i] != null && !wave[i].IsDead) return false;
            return true;
        }

        /// <summary>
        /// As many of the wave's waiting enemies as may come now (at most <see cref="RoomWaveValues.MostAtOnce"/>) are given their points by
        /// work order 136's rule (<see cref="RoomWaveRules.Pick"/>) and their discs; the rest wait <see cref="RoomWaveValues.WaitSeconds"/>,
        /// as a wave's do (<see cref="TryArrivals"/>; its own lists, so no room_wave line is written for it).
        /// </summary>
        void ChestArrivals(TimedChest t, float now, RoomWaveValues rules)
        {
            if (_player == null || t.Waiting.Count == 0) return;
            LevelLayout layout = _level.Layout;
            Vector3 herWorld = _player.Position;
            LevelPoint her = _level.ToLevel(herWorld);
            Camera cam = Camera.main;
            GroundQuad view = InLevel(ViewOfTheGround(cam, 0f));
            double upX = 0, upZ = 1; // with no camera, the follow camera's: looking north
            if (cam != null)
            {
                Vector3 f = cam.transform.forward;
                if (f.x * f.x + f.z * f.z > 1e-6f)
                {
                    upX = f.x;
                    upZ = f.z;
                }
            }
            float margin = Values.RangedAround != null ? Values.RangedAround.ScreenMargin : 0f;
            if (t.Points == null)
            {
                t.Points = RoomWaveRules.RoomPoints(layout, t.Room, Values, rules.PointStep);
                t.OnMesh = new Vector3[t.Points.Count];
                t.OnMeshKnown = new sbyte[t.Points.Count];
                t.Rng = LevelRandom.Stream(layout.Seed, TimedChestRules.Stage, 2000 + t.Room.Order);
            }
            _chestTaken.Clear();
            for (int i = 0; i < t.Arriving.Count; i++) _chestTaken.Add(_level.ToLevel(t.Arriving[i].At));
            AddChestTaken(_level.EnemiesByRoom, t.Room.Order);
            AddChestTaken(_level.CalledByRoom, t.Room.Order);
            _chestTaken.Add(t.At); // never on the chest itself
            bool herOnMesh = NavMesh.SamplePosition(herWorld, out NavMeshHit herHit, HerOnNavMeshWithin, NavMesh.AllAreas);
            if (_chestPath == null) _chestPath = new NavMeshPath();
            bool Reachable(int i)
            {
                if (t.OnMeshKnown[i] == 0)
                {
                    bool on = NavMesh.SamplePosition(_level.ToWorld(t.Points[i]), out NavMeshHit hit, rules.OnNavMeshWithin, NavMesh.AllAreas)
                              && _level.RoomAt(hit.position) == t.Room;
                    t.OnMeshKnown[i] = (sbyte)(on ? 1 : -1);
                    if (on) t.OnMesh[i] = hit.position;
                }
                if (t.OnMeshKnown[i] < 0) return false;
                return !herOnMesh || (NavMesh.CalculatePath(herHit.position, t.OnMesh[i], NavMesh.AllAreas, _chestPath) && _chestPath.status == NavMeshPathStatus.PathComplete);
            }
            int wanted = rules.MostAtOnce > 0 ? Mathf.Min(t.Waiting.Count, rules.MostAtOnce) : t.Waiting.Count;
            List<int> kept = RoomWaveRules.Pick(t.Points, wanted, her, view, margin, upX, upZ, _chestTaken, rules, t.Rng, Reachable);
            for (int k = 0; k < kept.Count; k++)
            {
                Enemy e = t.Waiting[k];
                var a = new WaveArrival { Enemy = e, Wave = 1, At = t.OnMesh[kept[k]], Since = now };
                a.Disc = PlaceArrival(e, a.At, rules);
                t.Arriving.Add(a);
                t.Arrived.Add(a);
            }
            t.Waiting.RemoveRange(0, kept.Count);
            if (t.Waiting.Count > 0) t.NextTryAt = now + Mathf.Max(0f, rules.WaitSeconds);
        }

        void AddChestTaken(IReadOnlyDictionary<int, List<Enemy>> byRoom, int room)
        {
            if (!byRoom.TryGetValue(room, out List<Enemy> list)) return;
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && !list[i].IsDead && list[i].isActiveAndEnabled) _chestTaken.Add(_level.ToLevel(list[i].transform.position));
        }

        /// <summary>The discs of the wave's arrivals: thickening, and over at their second's end, when each enemy stands there awake (as a wave's, <see cref="TickDiscs"/>).</summary>
        void TickChestDiscs(TimedChest t, float now, RoomWaveValues rules)
        {
            for (int i = t.Arriving.Count - 1; i >= 0; i--)
            {
                WaveArrival a = t.Arriving[i];
                float share = rules.DiscSeconds > 0f ? (now - a.Since) / rules.DiscSeconds : 1f;
                if (share < 1f)
                {
                    if (a.Disc != null) a.Disc.widthMultiplier = Mathf.Lerp(DiscThin, DiscThick, share);
                    continue;
                }
                if (a.Disc != null) Destroy(a.Disc.gameObject);
                a.Disc = null;
                t.Arriving.RemoveAt(i);
                if (t.Arriving.Count == 0 && t.Waiting.Count > 0 && rules.MostAtOnce > 0)
                    t.NextTryAt = Mathf.Max(t.NextTryAt, now + Mathf.Max(0f, rules.WaitSeconds)); // a group has stood: the next one interval later
                Enemy e = a.Enemy;
                if (e == null || e.IsDead) continue;
                e.ArriveAt(a.At);
                RangedOpening opening = e is EnemyShooter s ? s.Opening : e is EnemyCaster c ? c.Opening : null;
                opening?.Arrived(t.Fight, now);
            }
        }

        /// <summary>The chest's line, once (work order 153).</summary>
        void NoteTimedChest(TimedChest t, string name)
        {
            if (t.Told) return;
            t.Told = true;
            PlaySessionRecorder recorder = PlaySessionRecorder.Current;
            if (recorder == null) return;
            bool opened = t.OpenedAt >= 0f;
            double share = t.WonAt >= 0f ? TimedChestRules.Share(t.WonAt - t.OpenedAt, Values.TimedChestRules) : -1d;
            recorder.NoteTimedChest(name, t.FloorNumber, t.RoomNumber, t.Room.Order, opened,
                                    opened && t.OpenableAt >= 0f ? t.OpenedAt - t.OpenableAt : -1f, opened ? t.Kinds : "", opened ? t.Wave.Count : 0,
                                    t.WonAt >= 0f ? t.WonAt - t.OpenedAt : -1f, share, t.Whole, t.Taken, t.Damage);
        }

        // ---------------- What it shows (work order 153) ----------------

        /// <summary>
        /// From the floors' OnGUI: over the chest its share and Echoes (or the prompt, in reach), and on the HUD its line while its fight is
        /// on and its reward waits. Every word was made with the chest (<see cref="MakeChestWords"/>): nothing is made here.
        ///
        /// The producer's round 2: the line in the size of the HUD's own names (the boss's name, <c>InterfaceValues.BossNameSize</c>), under
        /// the top plate at its left (<see cref="TimedChestValues.HudUnderPlate"/>), where it covers the screen's top left corner: farther from
        /// her than an arrival's 11 m, so never over a disc, and no bar of a combat room stands there (the top centre was chosen against: the
        /// waves' discs come up the screen from her, and round 1's picture had one under the line there). The number over the chest twice the
        /// prompts' size (<see cref="TimedChestValues.OverSize"/>); the prompt above the chest, reaching away from her on screen so it never
        /// lies across her body.
        /// </summary>
        void DrawTimedChest(bool kinetic, float scale)
        {
            TimedChest t = _timedChest;
            Camera cam = Camera.main;
            if (t == null || t.Object == null || cam == null || Choice != ChoiceKind.None || Ending != EndKind.None) return;
            if (t.State == TimedChestState.Shut || t.State == TimedChestState.Taken || t.State == TimedChestState.Lost) return;
            TimedChestValues c = Values.TimedChestRules;
            int p = Mathf.Clamp(TimedChestRules.Percent(TimedChestShare), 0, 100);
            Vector3 screen = cam.WorldToScreenPoint(t.Place + Vector3.up * c.LabelHeight);
            bool inReach = TimedChestInReach() == t;
            Color colorBefore = GUI.color;
            Matrix4x4 before = GUI.matrix;
            float layoutScale = kinetic ? Mathf.Max(1e-4f, InterfaceStyle.Scale) : scale;
            if (kinetic) InterfaceStyle.BeginLayout(out Matrix4x4 _);
            if (screen.z > 0f)
            {
                float x = screen.x / layoutScale, y = (InterfaceStyle.ScreenHeight - screen.y) / layoutScale;
                if (inReach)
                {
                    Vector3 her = cam.WorldToScreenPoint(_player.Position);
                    int away = her.x / layoutScale > x ? -1 : 1; // she stands right of it on screen: the prompt reaches left of it, and the other way
                    DrawChestPromptLine(x, y, t.State == TimedChestState.Openable ? t.Open : t.Takes[p], kinetic, away);
                }
                else if (t.State != TimedChestState.Openable) DrawChestLine(x, y - (c.OverSize * 0.8f + OverClear), t.Over[p], kinetic, c.OverSize, 0); // its foot at that point: over the chest, never on it
            }
            if (t.State == TimedChestState.Fighting || t.State == TimedChestState.Won)
            {
                InterfaceValues v = InterfaceStyle.V;
                if (kinetic)
                {
                    float top = (v.TopPlate ? InterfacePlate.Whole(v).Bottom : 0f) + c.HudUnderPlate;
                    DrawChestLine(v.HealthPlateX, top + v.BossNameSize * 0.8f, t.Hud[p], true, v.BossNameSize, 1);
                }
                else DrawChestLine(PlainHudLeft, PlainHudTop, t.Hud[p], false, 0, 1);
            }
            GUI.color = colorBefore;
            GUI.matrix = before;
        }

        /// <summary>Layout pixels between the number over the chest and the point 1.4 m over it, where its foot stands (round 2's first pictures had the larger number lying across the chest's lid).</summary>
        const float OverClear = 6f;

        /// <summary>Without the kinetic skin: the HUD's line's left and middle in the plain layout (720 high), under the plain health readout.</summary>
        const float PlainHudLeft = 10f, PlainHudTop = 96f;

        GUIStyle _chestStyle;
        GUIContent _chestKey;
        Key _chestKeyOf = Key.None;

        /// <summary>
        /// The prompt: the interact key on its plate and the words after it (as the treasure chest's, <see cref="InterfaceStyle.Prompt"/>), from
        /// contents made once, its middle line at <paramref name="y"/>; it starts at <paramref name="x"/> and reaches right
        /// (<paramref name="away"/> 1) or ends there and reaches left (-1).
        /// </summary>
        void DrawChestPromptLine(float x, float y, GUIContent words, bool kinetic, int away)
        {
            Key key = InteractKey;
            if (_chestKey == null || key != _chestKeyOf)
            {
                _chestKeyOf = key;
                _chestKey = new GUIContent(key.ToString());
            }
            if (!kinetic)
            {
                if (_promptStyle == null) _promptStyle = new GUIStyle(GUI.skin.box) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
                float w = _promptStyle.CalcSize(words).x + 50f, left0 = away > 0 ? x : x - w;
                GUI.Box(new Rect(left0, y - 14f, 40f, 26f), _chestKey, _promptStyle);
                GUI.Box(new Rect(left0 + 42f, y - 14f, w - 42f, 26f), words, _promptStyle);
                return;
            }
            InterfaceValues v = InterfaceStyle.V;
            GUIStyle style = InterfaceStyle.Text(true, v.PromptSize, TextAnchor.MiddleLeft);
            float wordsWidth = style.CalcSize(words).x;
            float h = v.PromptSize * 1.45f, keyW = Mathf.Max(v.PromptKeyWidth, style.CalcSize(_chestKey).x + 10f);
            float width = keyW + 8f + wordsWidth + 12f;
            var back = new Rect(away > 0 ? x : x - width, y - h * 0.5f, width, h);
            InterfaceStyle.Slanted(back, InterfaceStyle.C(v.Back));
            var plate = new Rect(back.x + 3f, back.y + 3f, keyW, h - 6f);
            InterfaceStyle.Slanted(plate, InterfaceStyle.C(v.White));
            InterfaceStyle.Label(plate, _chestKey, InterfaceStyle.Text(true, v.PromptSize - 3, TextAnchor.MiddleCenter), InterfaceStyle.C(v.Black));
            InterfaceStyle.Label(new Rect(back.x + keyW + 9f, back.y, wordsWidth + 4f, h), words, style, InterfaceStyle.C(v.White), true);
        }

        /// <summary>
        /// A line of words over a faint slanted back with a teal rule under it (as <see cref="InterfaceStyle.Line"/>), from a content made once,
        /// in type <paramref name="size"/> (layout pixels), its middle line at <paramref name="middle"/>; centred on <paramref name="x"/>
        /// (<paramref name="anchor"/> 0) or starting there (1).
        /// </summary>
        void DrawChestLine(float x, float middle, GUIContent words, bool kinetic, int size, int anchor)
        {
            if (!kinetic)
            {
                if (_chestStyle == null) _chestStyle = new GUIStyle(GUI.skin.box) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
                float w = _chestStyle.CalcSize(words).x + 24f;
                GUI.Box(new Rect(anchor == 0 ? x - w * 0.5f : x, middle - 13f, w, 26f), words, _chestStyle);
                return;
            }
            InterfaceValues v = InterfaceStyle.V;
            GUIStyle style = InterfaceStyle.Text(true, size, TextAnchor.MiddleCenter);
            float width = style.CalcSize(words).x + 30f, h = size * 1.6f;
            var r = new Rect(anchor == 0 ? x - width * 0.5f : x, middle - h * 0.5f, width, h);
            InterfaceStyle.Slanted(r, InterfaceStyle.C(v.Back));
            InterfaceStyle.Rule(r.x + 6f, r.yMax - v.RuleThickness, r.width - 12f);
            InterfaceStyle.Label(r, words, style, InterfaceStyle.C(v.White), true);
        }

        /// <summary>For the tests: where the HUD's line of the chest stands now, in layout pixels of a view 1080 high (the kinetic skin), and its size.</summary>
        public Rect TimedChestHudLineForTests()
        {
            InterfaceValues v = InterfaceStyle.V;
            TimedChestValues c = Values.TimedChestRules;
            float top = (v.TopPlate ? InterfacePlate.Whole(v).Bottom : 0f) + c.HudUnderPlate;
            return new Rect(v.HealthPlateX, top, 0f, v.BossNameSize * 1.6f);
        }

        /// <summary>For the tests: one step of the chest, as a frame takes it.</summary>
        public void TickTimedChestForTests() => TickTimedChest();
    }
}
