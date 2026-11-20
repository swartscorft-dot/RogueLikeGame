using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace Roguelike
{
    /// <summary>
    /// A reward on the ground, and the guard on every choice (work order 042; Kristian's words of 2026-11-20: "Several times
    /// I insta-pressed an upgrade because I killed the last enemy and the pop-up came at the same instant. This should
    /// happen in the form of a drop on the ground that one picks up with E"), as part of <see cref="GeneratedLevels"/>.
    ///
    /// The drop (<see cref="RewardDropValues.On"/>, on by default): nothing she earns by killing opens a choice by itself.
    /// The choices that opened the moment the last enemy of a room died were three: the elite room's skill upgrade, the
    /// floor's reward when its exit room is cleared, and the focus choice when floor 1 had no skill left to offer and it
    /// came in the reward's place. Each now leaves a <see cref="RewardDrop"/> where the last enemy of the room fell (the
    /// room's middle when that place cannot be stood on), landing on walkable ground within a couple of metres
    /// (<see cref="RewardDropRules.Landing"/>), in its colour: the elite's gold, the HUD's teal for a new skill, the focus
    /// gauge's blue. Standing within <see cref="RunValues.ChestReach"/> of one that lies still, a prompt shows over it and the
    /// interact key (E, as bound) picks it up (<see cref="Interact"/>, <see cref="PickUpDrop"/>): only then does its choice
    /// open, the same choice with the same offer as before. The focus choice still follows floor 1's reward without a
    /// drop of its own. What did not open a choice stays as it was: the elite room with no upgrade left heals her in full at
    /// once, a floor with no skill left to offer and no focus choice due goes on to the next at once, the last floor's end
    /// slows to the act-cleared screen, and a chest opens by her own act.
    ///
    /// A drop stays for the whole floor. Taking the floor's reward is the only way on to the next floor (as before), so
    /// the floor's end waits for its drop to be picked up, and the HUD says so (<see cref="WaitingLine"/>); nothing else is
    /// blocked. The elite room's drop would be lost with the floor: when she picks up the floor's drop while the elite's
    /// still lies, the elite's upgrade comes first and the floor's reward follows it, as the focus choice follows the
    /// reward, with no frame of play between.
    ///
    /// The guard (with the switch off too, for every choice, a chest's as well): for <see cref="RewardDropValues.GuardSeconds"/>
    /// of real time after a choice opens, its keys and clicks pick nothing and the cards are drawn dim; a pick is a press that
    /// went down after that (<see cref="ChoiceGuard"/>). The intents (<see cref="PickReward"/>, <see cref="PickSkillUpgrade"/>,
    /// <see cref="PickFocusChoice"/>, <see cref="PickUpgrade"/>) are not keys and are not guarded.
    ///
    /// The intents keep working in one call: a pick given while its drop lies picks the drop up and then picks (the one
    /// exception: the floor's reward while the elite's drop lies too, when the elite's upgrade comes first and must be
    /// picked first). <see cref="FinishFloorForTests"/> and <see cref="FinishEliteRoomForTests"/> bring the room's choice up
    /// as before, with no drop, so the tests that finish a room through them see what they saw; the tests of the drop kill
    /// the room's enemies themselves.
    /// </summary>
    public sealed partial class GeneratedLevels
    {
        /// <summary>The numbers of the drops and of the guard (work order 042), and the drops' switch.</summary>
        public RewardDropValues DropValues = new RewardDropValues();

        /// <summary>A new skill's drop: the HUD's teal. The elite's is its gold (<see cref="EnemyFactory.EliteTint"/>), the focus choice's the gauge's blue.</summary>
        public static readonly Color FloorRewardColor = new Color(0.25f, 0.85f, 0.85f);
        public static readonly Color FocusChoiceColor = new Color(0.25f, 0.6f, 1f);

        /// <summary>
        /// For tests: a keyboard the choice's keys and the interact key are read from while
        /// <see cref="PlayerInputDriver.ReadRealDevices"/> is false (a virtual device the test presses). Null: none.
        /// </summary>
        public Keyboard TestKeyboard { get; set; }

        /// <summary>The drops lying on this floor, not yet picked up.</summary>
        public IReadOnlyList<RewardDrop> Drops
        {
            get
            {
                Prune();
                return _drops;
            }
        }

        /// <summary>The floor's reward lying on the ground (a new skill, or the focus choice in its place), or null.</summary>
        public RewardDrop FloorDrop => Find(true);
        /// <summary>The elite room's upgrade lying on the ground, or null.</summary>
        public RewardDrop EliteDrop => Find(false);

        /// <summary>True when the choice on screen takes keys and clicks: its guard is over.</summary>
        public bool ChoiceLive => Choice != ChoiceKind.None && _guard.Live(Time.unscaledTime);
        /// <summary>The real time from which the choice on screen takes keys and clicks.</summary>
        public float ChoiceLiveFrom => _guard.LiveFrom;

        /// <summary>The HUD's line while the floor's reward waits on the ground; empty otherwise.</summary>
        public string WaitingLine
        {
            get
            {
                if (!InRun || FloorDrop == null || Choice != ChoiceKind.None || Ending != EndKind.None) return "";
                Key key = InteractKey;
                if (_waitingLine == null || key != _waitingLineKey)
                {
                    _waitingLineKey = key;
                    _waitingLine = "FLOOR CLEARED   its reward waits on the ground: pick it up (" + key + ") to go on";
                }
                return _waitingLine;
            }
        }

        string _waitingLine;
        Key _waitingLineKey;

        readonly List<RewardDrop> _drops = new List<RewardDrop>();
        readonly ChoiceGuard _guard = new ChoiceGuard();
        bool _openingADrop;
        Owed _floorFollows;
        GeneratedLevel _finishedForTestsOn;
        bool _floorFinishedForTests;
        bool _eliteFinishedForTests;
        int _dropPromptFrame = -1;
        RewardDrop _dropPrompt;

        /// <summary>What a drop held, kept when the drop is gone: the choice and its offer.</summary>
        sealed class Owed
        {
            public ChoiceKind Kind;
            public int[] Skills;
            public SkillUpgradeDefinition[] Upgrades;
            public GeneratedLevel Level;
        }

        /// <summary>The keyboard read for the choices and the interact key: the real one, or the test's.</summary>
        Keyboard ChoiceKeys => PlayerInputDriver.ReadRealDevices ? Keyboard.current : TestKeyboard;

        // ---------------- Intents ----------------

        /// <summary>The interact key: picks up the drop in reach, else opens the chest in reach. True when either happened.</summary>
        public bool Interact() => PickUpDrop() || OpenChest();

        /// <summary>Picks up the drop in reach that lies still, as the interact key does: its choice opens. False with none.</summary>
        public bool PickUpDrop()
        {
            RewardDrop drop = DropInReach();
            if (drop == null) return false;
            TakeDrop(drop);
            return true;
        }

        /// <summary>
        /// The drop she can pick up now: lying still, within <see cref="RunValues.ChestReach"/> of her, in a run, with no
        /// choice, pause or end screen up; the nearest of two. Null if none.
        /// </summary>
        public RewardDrop DropInReach()
        {
            if (!InRun || _level == null || _player == null || Choice != ChoiceKind.None || Paused || Ending != EndKind.None) return null;
            Prune();
            RewardDrop best = null;
            float bestDistance = RunValues.ChestReach;
            Vector3 p = _player.Position;
            for (int i = 0; i < _drops.Count; i++)
            {
                RewardDrop d = _drops[i];
                if (!d.Settled) continue;
                float distance = new Vector2(d.Landing.x - p.x, d.Landing.z - p.z).magnitude;
                if (distance > bestDistance) continue;
                best = d;
                bestDistance = distance;
            }
            return best;
        }

        /// <summary>The drop the prompt shows over now: <see cref="DropInReach"/>, found once a frame.</summary>
        public RewardDrop DropPrompt
        {
            get
            {
                if (_dropPromptFrame != Time.frameCount)
                {
                    _dropPromptFrame = Time.frameCount;
                    _dropPrompt = DropInReach();
                }
                return _dropPrompt;
            }
        }

        // ---------------- The drops ----------------

        /// <summary>
        /// Called where a choice opens: true when it is one she earned by killing and the switch is on, and a drop was put
        /// down in its place; the choice does not open. A choice that opens from a drop, or from another choice, opens.
        /// </summary>
        bool LeaveADrop(ChoiceKind kind)
        {
            if (_openingADrop || !DropValues.On || Choice != ChoiceKind.None || _level == null || !InRun) return false;
            bool elite = kind == ChoiceKind.SkillUpgrade;
            if (!elite && kind != ChoiceKind.FloorReward && kind != ChoiceKind.FocusChoice) return false;
            if (_finishedForTestsOn == _level && (elite ? _eliteFinishedForTests : _floorFinishedForTests))
            {
                // Finished by the tests' intent: as before work order 042.
                if (elite) _eliteFinishedForTests = false;
                else _floorFinishedForTests = false;
                return false;
            }
            LevelRoom room = elite ? _level.Layout.EliteRoom : _level.Layout.ExitRoom;
            Vector3 fell = LastFallen(room);
            Vector3 landing = LandingFor(fell, room);
            Color color = elite ? EnemyFactory.EliteTint : kind == ChoiceKind.FocusChoice ? FocusChoiceColor : FloorRewardColor;
            var owed = new Owed { Kind = kind, Skills = _offeredSkills.ToArray(), Upgrades = _offeredSkillUpgrades.ToArray(), Level = _level };
            RewardDrop drop = RewardDrop.Make(this, owed, kind, color, fell, landing, _level.transform,
                                              _player != null ? _player.ShotFxMaterial : null, DropValues);
            _drops.Add(drop);
            _offeredSkills.Clear(); // the offer waits in the drop; nothing is offered on screen
            _offeredSkillUpgrades.Clear();
            Debug.Log(string.Format(s_Inv, "Reward drop: {0} on floor {1}, fell at ({2:0.0}, {3:0.0}), lands at ({4:0.0}, {5:0.0})",
                                    kind, Floor, fell.x, fell.z, landing.x, landing.z));
            return true;
        }

        /// <summary>She picked up a drop (the interact key, or a pick's intent): its choice opens, the elite's first when the floor's is picked up with it lying.</summary>
        void TakeDrop(RewardDrop drop)
        {
            Owed owed = (Owed)drop.Owed;
            RemoveDrop(drop);
            if (owed.Kind != ChoiceKind.SkillUpgrade && EliteDrop is RewardDrop elite)
            {
                // The elite's would be lost with the floor: it comes first, and the floor's follows it.
                _floorFollows = owed;
                owed = (Owed)elite.Owed;
                RemoveDrop(elite);
            }
            Debug.Log("Reward drop picked up: " + owed.Kind + " on floor " + Floor);
            OpenOwed(owed);
        }

        void OpenOwed(Owed owed)
        {
            _offeredSkills.Clear();
            _offeredSkills.AddRange(owed.Skills);
            _offeredSkillUpgrades.Clear();
            _offeredSkillUpgrades.AddRange(owed.Upgrades);
            _openingADrop = true;
            OpenChoice(owed.Kind);
            _openingADrop = false;
        }

        /// <summary>
        /// Where a skill upgrade has been taken: when the floor's reward was picked up with the elite's, it comes now, the
        /// one choice closing and the other opening at once. False when none follows.
        /// </summary>
        bool FloorRewardFollows()
        {
            Owed owed = _floorFollows;
            _floorFollows = null;
            if (owed == null || owed.Level != _level || !InRun) return false;
            CloseChoice();
            OpenOwed(owed);
            return true;
        }

        /// <summary>A pick's intent given while its drop lies: the drop is picked up first, wherever she is (work order 042).</summary>
        void PickUpForAPick(ChoiceKind kind)
        {
            if (Choice != ChoiceKind.None || Paused || Ending != EndKind.None || !InRun) return;
            RewardDrop drop = kind == ChoiceKind.SkillUpgrade ? EliteDrop : FloorDrop;
            if (drop != null && drop.Kind == kind) TakeDrop(drop);
        }

        /// <summary>The tests' intents that finish a room: its choice comes up as before, without a drop.</summary>
        void FinishedForTests(bool elite)
        {
            if (_finishedForTestsOn != _level)
            {
                _finishedForTestsOn = _level;
                _floorFinishedForTests = false;
                _eliteFinishedForTests = false;
            }
            if (elite) _eliteFinishedForTests = true;
            else _floorFinishedForTests = true;
        }

        RewardDrop Find(bool floor)
        {
            Prune();
            for (int i = 0; i < _drops.Count; i++)
                if ((_drops[i].Kind != ChoiceKind.SkillUpgrade) == floor) return _drops[i];
            return null;
        }

        void RemoveDrop(RewardDrop drop)
        {
            _drops.Remove(drop);
            if (drop != null)
            {
                drop.gameObject.SetActive(false);
                Destroy(drop.gameObject);
            }
        }

        /// <summary>Forgets the drops of a floor that is gone (they go with its objects), and those destroyed.</summary>
        void Prune()
        {
            for (int i = _drops.Count - 1; i >= 0; i--)
                if (_drops[i] == null || ((Owed)_drops[i].Owed).Level != _level || _level == null) _drops.RemoveAt(i);
        }

        /// <summary>
        /// Where the last enemy of a room fell: of its dead, the one seen dead last (one that died after its own Update ran
        /// has not been seen yet, and is the last). The room's middle when none is known.
        /// </summary>
        Vector3 LastFallen(LevelRoom room)
        {
            if (room == null) return _player != null ? _player.Position : Vector3.zero;
            Vector3 middle = _level.Middle(room);
            if (!_level.EnemiesByRoom.TryGetValue(room.Order, out List<Enemy> list)) return middle;
            Enemy last = null;
            float lastAt = float.NegativeInfinity;
            foreach (Enemy e in list)
            {
                if (e == null || !e.IsDead) continue;
                float at = e.DeadSince < 0f ? float.PositiveInfinity : e.DeadSince;
                if (at < lastAt) continue;
                last = e;
                lastAt = at;
            }
            return last != null ? new Vector3(last.transform.position.x, 0f, last.transform.position.z) : middle;
        }

        /// <summary>Where the drop lands, on the floor's NavMesh (<see cref="RewardDropRules.Landing"/>).</summary>
        Vector3 LandingFor(Vector3 fell, LevelRoom room)
        {
            Vector3 middle = room != null ? _level.Middle(room) : fell;
            GroundPoint at = RewardDropRules.Landing(new GroundPoint(fell.x, fell.z), new GroundPoint(middle.x, middle.z), Standable, OpenWay, DropValues);
            var landing = new Vector3(at.X, 0f, at.Z);
            // Neither where it fell nor the middle could be stood on, nor anywhere round them: the nearest walkable ground.
            if (!Standable(at) && NavMesh.SamplePosition(landing, out NavMeshHit near, 4f, NavMesh.AllAreas))
                landing = new Vector3(near.position.x, 0f, near.position.z);
            return landing;
        }

        /// <summary>A point of this floor she can stand at: on its NavMesh, in a room, and clear of walls and props by the clearance.</summary>
        bool Standable(GroundPoint p)
        {
            var w = new Vector3(p.X, 0f, p.Z);
            LevelRoom room = _level != null ? _level.RoomAt(w) : null;
            if (room == null) return false;
            // Every prop's footprint, also one low enough to be in the NavMesh, or with no collider.
            LevelPoint at = _level.ToLevel(w);
            foreach (LevelProp prop in room.Props)
                if (prop.Footprint.DistanceTo(at) < DropValues.Clearance) return false;
            if (!NavMesh.SamplePosition(w, out NavMeshHit hit, 0.6f, NavMesh.AllAreas)) return false;
            if (new Vector2(hit.position.x - w.x, hit.position.z - w.z).magnitude > 0.05f) return false; // off the NavMesh: in a wall or a prop
            return !NavMesh.FindClosestEdge(hit.position, out NavMeshHit edge, NavMesh.AllAreas) || edge.distance >= DropValues.Clearance;
        }

        /// <summary>The straight way between two points is walkable: nothing in between.</summary>
        static bool OpenWay(GroundPoint a, GroundPoint b)
        {
            if (!NavMesh.SamplePosition(new Vector3(a.X, 0f, a.Z), out NavMeshHit from, 0.6f, NavMesh.AllAreas)) return false;
            return !NavMesh.Raycast(from.position, new Vector3(b.X, from.position.y, b.Z), out NavMeshHit _, NavMesh.AllAreas);
        }

        // ---------------- The guard ----------------

        /// <summary>A choice opened: its keys and clicks wait out the guard.</summary>
        void GuardTheChoice() => _guard.Open(Time.unscaledTime, DropValues.GuardSeconds);

        /// <summary>True when a choice's key pressed this frame may pick: it went down once the guard was over.</summary>
        bool ChoiceKeyMayPick => _guard.MayPick(Time.unscaledTime);

        /// <summary>
        /// The choice on screen, guarded: while the guard runs a click goes nowhere (its press is taken here, so neither it
        /// nor its release reaches a card, and a button held from before never picks) and the cards are dim, lighting up
        /// as the guard runs out.
        /// </summary>
        void DrawGuardedChoice(float viewWidth)
        {
            float now = Time.unscaledTime;
            bool live = _guard.Live(now);
            Event e = Event.current;
            if (!live && e != null && (e.type == EventType.MouseDown || e.type == EventType.MouseUp)) e.Use();
            bool enabledBefore = GUI.enabled;
            Color colorBefore = GUI.color;
            if (!live)
            {
                GUI.enabled = false;
                GUI.color = new Color(1f, 1f, 1f, 0.35f + 0.4f * _guard.Share(now));
            }
            DrawChoice(viewWidth);
            GUI.enabled = enabledBefore;
            GUI.color = colorBefore;
        }
    }
}
