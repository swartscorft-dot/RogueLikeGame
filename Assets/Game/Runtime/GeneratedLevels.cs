using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Roguelike
{
    /// <summary>
    /// Walking through generated levels from inside the practice scene (work order 006). No scene, prefab or asset is
    /// made: the level is built at run time, far to one side of the practice room (<see cref="GeneratedLevel"/>), and
    /// the character is moved there and back with <see cref="PlayerCharacter.Teleport"/>.
    ///
    /// Keys, read only while <see cref="PlayerInputDriver.ReadRealDevices"/> is true: F5 builds a level from a new seed
    /// and puts her at its start, F6 builds the same seed again, F7 takes her back to the practice room and removes the
    /// level. Building a level removes the one before. Tests use the same three as intents: <see cref="Build"/>,
    /// <see cref="Rebuild"/> and <see cref="BackToPracticeRoom"/>.
    ///
    /// In the practice room (work order 008), under the same rule: F9 spawns a pack of swarmers, F10 a shooter, F11 a
    /// guard, each about <see cref="SpawnDistance"/> from her on the practice floor, and F12 removes everything spawned
    /// this way. They stay dead when killed. The intents are <see cref="SpawnPractice"/> and <see cref="ClearSpawned"/>.
    /// Nothing is spawned while she is in a generated level.
    ///
    /// It installs itself when Play starts, under the same rule as the play session recorder, so never in a test run
    /// (<see cref="SessionRecordingRule.MayStartByItself"/>); tests make one with <see cref="Ensure"/>. It does
    /// nothing while there is no <see cref="PlayerCharacter"/> in the scene.
    ///
    /// While she is in a level it shows its seed, the generator's version and the room she stands in, and says
    /// "Level cleared" once the boss room's enemy is dead. It logs the seed and the text map of every level it builds,
    /// and tells the play session recorder when a level is built and when she enters a room.
    /// </summary>
    public sealed class GeneratedLevels : MonoBehaviour
    {
        public const Key NewLevelKey = Key.F5;
        public const Key SameLevelKey = Key.F6;
        public const Key PracticeRoomKey = Key.F7;
        public const Key SwarmersKey = Key.F9;
        public const Key ShooterKey = Key.F10;
        public const Key GuardKey = Key.F11;
        public const Key ClearSpawnsKey = Key.F12;
        /// <summary>Metres from her that a spawned enemy (or a pack's middle) stands.</summary>
        public const float SpawnDistance = 12f;

        static readonly CultureInfo s_Inv = CultureInfo.InvariantCulture;
        static ulong s_freshCounter;

        /// <summary>The one running now, or null.</summary>
        public static GeneratedLevels Current { get; private set; }

        /// <summary>The level values in use; tests and tools may replace them before building.</summary>
        public LevelValues Values = new LevelValues();

        /// <summary>The level she can walk now, or null.</summary>
        public GeneratedLevel Level => _level != null ? _level : null;
        public bool IsInLevel => Level != null;
        /// <summary>The seed of the last level built, also after leaving it; null before the first.</summary>
        public ulong? LastSeed { get; private set; }
        /// <summary>The room she stands in now, or null (outside every room, or not in a level).</summary>
        public LevelRoom CurrentRoom { get; private set; }
        /// <summary>The rooms of this level she has been in, by their place in the run.</summary>
        public IReadOnlyCollection<int> RoomsEntered => _entered;
        /// <summary>True once the boss room's enemy of this level is dead.</summary>
        public bool Cleared { get; private set; }
        /// <summary>True when it installed itself at the start of Play.</summary>
        public bool StartedByItself { get; private set; }

        GeneratedLevel _level;
        PlayerCharacter _player;
        Vector3 _practiceSpot;
        bool _practiceSpotKnown;
        readonly HashSet<int> _entered = new HashSet<int>();
        readonly StringBuilder _text = new StringBuilder();
        GUIStyle _style;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Current = null;
            s_freshCounter = 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void InstallByItself()
        {
            if (!SessionRecordingRule.MayStartByItself(SceneManager.GetActiveScene().name, Application.isBatchMode)) return;
            Ensure().StartedByItself = true;
        }

        /// <summary>The one running, made if there is none. It lives across scene loads.</summary>
        public static GeneratedLevels Ensure()
        {
            if (Current != null) return Current;
            var go = new GameObject("GeneratedLevels");
            DontDestroyOnLoad(go);
            Current = go.AddComponent<GeneratedLevels>();
            return Current;
        }

        void OnDestroy()
        {
            if (_level != null) Destroy(_level.gameObject);
            if (_spawnRoot != null) Destroy(_spawnRoot.gameObject);
            if (Current == this) Current = null;
        }

        // ---------------- Practice spawns (work order 008) ----------------

        Transform _spawnRoot;
        readonly List<Enemy> _spawned = new List<Enemy>();

        /// <summary>The enemies spawned in the practice room and not yet cleared, dead ones too.</summary>
        public IReadOnlyList<Enemy> Spawned => _spawned;

        /// <summary>
        /// Spawns enemies of a kind about <see cref="SpawnDistance"/> from her on the practice floor: for swarmers a pack
        /// (<see cref="SwarmerValues.PracticePack"/>, round a circle of the level values' pack radius), else one. Returns
        /// what it made; nothing while she is in a generated level, without a character or without the practice enemy
        /// to copy.
        /// </summary>
        public List<Enemy> SpawnPractice(EnemyKind kind)
        {
            var made = new List<Enemy>();
            PlayerCharacter player = FindPlayer();
            EnemyBrute template = PracticeEnemy();
            if (player == null || template == null || _level != null) return made;
            if (_spawnRoot == null)
            {
                // In the scene, not under this object that lives across scene loads: a new scene takes them away.
                _spawnRoot = new GameObject("PracticeSpawns").transform;
                SceneManager.MoveGameObjectToScene(_spawnRoot.gameObject, player.gameObject.scene);
            }
            Vector3 middle = SpawnPoint(player.Position);
            int count = kind == EnemyKind.Swarmer ? new SwarmerValues().PracticePack : 1;
            List<Vector3> places = EnemyFactory.PackPlaces(middle, count, Values.PackRadius);
            for (int i = 0; i < places.Count; i++)
            {
                Enemy e = EnemyFactory.Make(kind, places[i], _spawnRoot, template, "Spawned_" + kind + "_" + (_spawned.Count + 1));
                if (e == null) continue;
                made.Add(e);
                _spawned.Add(e);
            }
            return made;
        }

        /// <summary>Removes everything spawned in the practice room.</summary>
        public void ClearSpawned()
        {
            _spawned.Clear();
            if (_spawnRoot == null) return;
            _spawnRoot.gameObject.SetActive(false);
            Destroy(_spawnRoot.gameObject);
            _spawnRoot = null;
        }

        /// <summary>
        /// A point on the NavMesh about <see cref="SpawnDistance"/> from her: the first of eight ways round (north first)
        /// whose straight line from her stays on the NavMesh all the way, else the farthest one gets. Ground height.
        /// </summary>
        static Vector3 SpawnPoint(Vector3 from)
        {
            Vector3 feet = new Vector3(from.x, 0f, from.z);
            if (NavMesh.SamplePosition(feet, out NavMeshHit start, 2f, NavMesh.AllAreas)) feet = start.position;
            Vector3 best = feet;
            float bestDistance = -1f;
            for (int i = 0; i < 8; i++)
            {
                Vector3 dir = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward;
                Vector3 to = feet + dir * SpawnDistance;
                if (!NavMesh.Raycast(feet, to, out NavMeshHit hit, NavMesh.AllAreas)) return new Vector3(to.x, 0f, to.z);
                float d = Vector3.Distance(feet, hit.position);
                if (d > bestDistance)
                {
                    bestDistance = d;
                    best = hit.position - dir * 1.5f; // off the edge a little, so a pack fits
                }
            }
            return new Vector3(best.x, 0f, best.z);
        }

        // ---------------- Intents ----------------

        /// <summary>Builds the level of this seed, removing the one before, and puts her at its start. Null without a character.</summary>
        public GeneratedLevel Build(ulong seed)
        {
            PlayerCharacter player = FindPlayer();
            if (player == null) return null;
            LevelLayout layout = LevelGenerator.Generate(seed, Values);

            if (_level != null) Remove();
            else
            {
                _practiceSpot = player.Position;
                _practiceSpotKnown = true;
            }

            EnemyBrute template = PracticeEnemy();
            GameObject practiceFloor = GameObject.Find("Floor");
            Material baseMaterial = practiceFloor != null && practiceFloor.TryGetComponent(out Renderer floorRenderer) ? floorRenderer.sharedMaterial : null;
            _level = GeneratedLevel.Build(layout, Values, template, baseMaterial);
            LastSeed = seed;
            Cleared = false;
            _entered.Clear();
            CurrentRoom = null;

            player.Teleport(_level.PlayerStart);
            Debug.Log(string.Format(s_Inv, "Generated level {0} (seed {1}, generator version {2}, attempt {3}): {4} rooms, {5} enemies, {6} props{7}\n{8}",
                                    layout.SeedText, layout.Seed, layout.Version, layout.Attempt, layout.Rooms.Count, _level.Enemies.Count,
                                    _level.Props.Count, template == null ? " (no enemy in the scene to copy)" : "", LevelMap.Draw(layout)));
            PlaySessionRecorder.Current?.NoteLevelBuilt(layout.SeedText, layout.Version, layout.Rooms.Count);
            TrackRoom();
            return _level;
        }

        /// <summary>A level from a new seed.</summary>
        public GeneratedLevel BuildNew()
        {
            ulong entropy = (ulong)DateTime.UtcNow.Ticks ^ (++s_freshCounter * 0x9E3779B97F4A7C15UL);
            return Build(LevelSeed.Fresh(entropy));
        }

        /// <summary>The last seed's level again, from nothing; a new seed's if there was none.</summary>
        public GeneratedLevel Rebuild() => LastSeed.HasValue ? Build(LastSeed.Value) : BuildNew();

        /// <summary>Takes her back to where she was in the practice room and removes the level. Nothing without a level.</summary>
        public void BackToPracticeRoom()
        {
            if (_level == null) return;
            PlayerCharacter player = FindPlayer();
            if (player != null) player.Teleport(_practiceSpotKnown ? _practiceSpot : Vector3.zero);
            Remove();
        }

        void Remove()
        {
            // Off at once, so its NavMesh and colliders are gone before anything else is built or warped; destroyed at the end of the frame.
            _level.gameObject.SetActive(false);
            Destroy(_level.gameObject);
            _level = null;
            CurrentRoom = null;
            Cleared = false;
            _entered.Clear();
        }

        PlayerCharacter FindPlayer()
        {
            if (_player == null) _player = FindAnyObjectByType<PlayerCharacter>();
            return _player;
        }

        /// <summary>The scene's own enemy: one that comes back and is not part of a generated level.</summary>
        EnemyBrute PracticeEnemy()
        {
            foreach (EnemyBrute e in FindObjectsByType<EnemyBrute>(FindObjectsSortMode.InstanceID))
                if (e.ComesBack && e.GetComponentInParent<GeneratedLevel>() == null) return e;
            return null;
        }

        // ---------------- Every frame ----------------

        void Update()
        {
            if (FindPlayer() == null) return;
            if (PlayerInputDriver.ReadRealDevices && Keyboard.current is Keyboard keyboard)
            {
                if (keyboard[NewLevelKey].wasPressedThisFrame) BuildNew();
                else if (keyboard[SameLevelKey].wasPressedThisFrame) Rebuild();
                else if (keyboard[PracticeRoomKey].wasPressedThisFrame) BackToPracticeRoom();
                else if (keyboard[SwarmersKey].wasPressedThisFrame) SpawnPractice(EnemyKind.Swarmer);
                else if (keyboard[ShooterKey].wasPressedThisFrame) SpawnPractice(EnemyKind.Shooter);
                else if (keyboard[GuardKey].wasPressedThisFrame) SpawnPractice(EnemyKind.Guard);
                else if (keyboard[ClearSpawnsKey].wasPressedThisFrame) ClearSpawned();
            }
            if (_level == null)
            {
                CurrentRoom = null;
                return;
            }
            TrackRoom();
            if (!Cleared && _level.Boss != null && _level.Boss.IsDead)
            {
                Cleared = true;
                Debug.Log("Level cleared: " + _level.Layout.SeedText);
            }
        }

        void TrackRoom()
        {
            // Every point of the level's ground is in a room, doorways included; none means she has left it (a death
            // brings her back to the practice room's spawn, and the level stays until F5, F6 or F7).
            LevelRoom room = _level.RoomAt(_player.Position);
            if (room == null) CurrentRoom = null;
            if (room == null || room == CurrentRoom) return;
            CurrentRoom = room;
            _entered.Add(room.Order);
            PlaySessionRecorder.Current?.NoteRoomEntered(room.Order, room.Kind.ToString().ToLowerInvariant());
        }

        /// <summary>What the box on screen says, line by line; empty when she is not in a level.</summary>
        public string StatusText()
        {
            if (_level == null) return "";
            LevelLayout layout = _level.Layout;
            _text.Clear();
            _text.Append(string.Format(s_Inv, "level {0}   generator v{1}", layout.SeedText, layout.Version)).Append('\n');
            _text.Append(CurrentRoom != null
                ? string.Format(s_Inv, "room {0} of {1} ({2})", CurrentRoom.Order + 1, layout.Rooms.Count, CurrentRoom.Kind.ToString().ToLowerInvariant())
                : string.Format(s_Inv, "outside the level ({0} rooms)", layout.Rooms.Count)).Append('\n');
            _text.Append(string.Format(s_Inv, "rooms entered {0} of {1}", _entered.Count, layout.Rooms.Count)).Append('\n');
            if (Cleared) _text.Append("LEVEL CLEARED   seed ").Append(layout.SeedText).Append('\n');
            _text.Append("F5 new level   F6 same level   F7 practice room");
            return _text.ToString();
        }

        void OnGUI()
        {
            if (_player == null) return;
            bool inLevel = _level != null;
            if (!inLevel && !StartedByItself) return;
            if (_style == null) _style = new GUIStyle(GUI.skin.box) { fontSize = 12, alignment = TextAnchor.UpperLeft };

            // Laid out for a view 720 pixels high and scaled to the real height, like the readout in the other corner.
            float scale = Screen.height / SkillBar.LayoutHeight;
            Matrix4x4 matrixBefore = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            const float width = 300f;
            float x = Screen.width / scale - width - 10f;
            if (inLevel)
            {
                string text = StatusText();
                int lines = text.Split('\n').Length;
                GUI.Box(new Rect(x, 10, width, 10 + lines * 15), text, _style);
            }
            else
            {
                GUI.Box(new Rect(x, 10, width, 40), "F5 generated level" + (LastSeed.HasValue ? "   F6 " + LevelSeed.ToText(LastSeed.Value) + " again" : "")
                                                    + "\nF9 swarmers   F10 shooter   F11 guard   F12 clear", _style);
            }
            GUI.matrix = matrixBefore;
        }
    }
}
