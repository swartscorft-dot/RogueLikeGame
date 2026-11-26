using Roguelike.Rules;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Roguelike
{
    /// <summary>What moves the play camera for a moment (work order 130): it gets the rig's focus and distance as the rig worked them out, and may move them.</summary>
    public interface ICameraShot
    {
        void Frame(ref Vector3 focus, ref float distance);
    }

    /// <summary>
    /// The boss's entrance (work order 130; Kristian, 2026-11-25: "en litt cinematic inngang, forstørre kameraet litt og lage en kul
    /// intro animasjon. nå i første omgang, så syntes jeg intro videoen bare skal være en skikkelig tørr vits"; its rules
    /// <see cref="BossEntranceRules"/>, its numbers <see cref="Entrance"/>, its switch <see cref="BossEntranceValues.On"/>), as part of
    /// <see cref="GeneratedLevels"/>.
    ///
    /// When she crosses the point where the boss room's fight wakes (<see cref="RoomFightRules.PastThePoint"/>), once a boss room, the
    /// fight does not wake yet: everything holds, as under the pause (time scale 0, so her run's clock, the floor's time, the fight's
    /// timers and the recorder's clock do not count it; her input held; she cannot be hurt; the boss itself is stopped, so it can
    /// neither wake nor begin an attack). The play camera's own rig (<see cref="CameraRig.Shot"/>) travels from her to the boss and
    /// frames it closer, holds while the boss turns to the camera, gathers itself and springs (moved in code on its own model; its Idle clip
    /// goes on under it) and the card (<see cref="BossEntranceCard"/>) shows its name and one dry line drawn from the run's seed, and
    /// travels back while the boss turns to face her. Then, or at once when she presses one of her action keys, Enter, Space or Esc, everything is let go as it was and
    /// the fight wakes as it would have at the point (<see cref="RoomFightStart.Point"/>): its sound, the music, the doors and the
    /// recorder's line as before, now. A skip leaves exactly what the end leaves. A fight woken some other way first (she hurts the
    /// boss from the door) has no entrance. With the switch off nothing of this happens.
    /// </summary>
    public sealed partial class GeneratedLevels
    {
        /// <summary>The entrance's numbers (work order 130), first guesses, in the Inspector while playing.</summary>
        public BossEntranceValues Entrance = new BossEntranceValues();

        /// <summary>The longest real step the entrance takes in one frame: a hitch does not throw the card away (s).</summary>
        const float EntranceMostStep = 0.1f;

        sealed class EntranceShot : ICameraShot
        {
            public float Share;
            public Vector3 BossFocus;
            public BossEntranceValues Values;

            public void Frame(ref Vector3 focus, ref float distance)
            {
                focus = Vector3.Lerp(focus, BossFocus, Share);
                distance = BossEntranceRules.Distance(Share, distance, Values);
            }
        }

        readonly EntranceShot _shot = new EntranceShot();
        bool _entering;
        bool _enterFrozen;
        float _enterT;
        float _enterLastReal;
        int _enterStartFrame = -1;
        int _enterEndedFrame = -1;
        int _enterPlayedRoom = -1;
        float _enterScaleBefore = 1f;
        float _enterLookBase;
        bool _enterMadeUntouchable;
        bool _enterHeldInput;
        RoomFight _enterFight;
        EnemyBoss _enterBoss;
        CameraRig _enterRig;
        PlayerInputDriver _enterDriver;
        Quaternion _enterFacingFrom, _enterFacingViewer, _enterFacingTo;
        Transform _enterHead, _enterHeadFront;
        GameObject _enterBar;
        bool _enterBarWasShown;
        BossEntranceCard _card;

        /// <summary>True while the entrance plays: everything holds.</summary>
        public bool EntranceHolds => _entering;
        /// <summary>True on the frame the entrance ended (by its end or a skip): the key that ended it does nothing else.</summary>
        public bool EntranceEndedThisFrame => _enterEndedFrame == Time.frameCount;
        /// <summary>Seconds of real time into the entrance playing; 0 when none plays.</summary>
        public float EntranceTime => _entering ? _enterT : 0f;
        /// <summary>The part of the entrance playing; None when none plays.</summary>
        public EntrancePhase EntrancePhaseNow => _entering ? BossEntranceRules.PhaseAt(_enterT, Entrance) : EntrancePhase.None;
        /// <summary>How much the card shows now, 0 to 1 (the card draws from it).</summary>
        public float EntranceCardShows { get; private set; }
        /// <summary>The boss's name on the card, in capitals, and the line drawn for this run.</summary>
        public string EntranceName { get; private set; } = "";
        public string EntranceLine { get; private set; } = "";
        /// <summary>For tests and the log: entrances begun since the game started, how many ended by a skip, and whether the last one did.</summary>
        public int EntrancesBegun { get; private set; }
        public int EntrancesSkipped { get; private set; }
        public bool LastEntranceSkipped { get; private set; }

        /// <summary>
        /// From <see cref="TickRoomFights"/>, as she is past the point of a room whose fight is not on: true when the entrance takes
        /// the moment (it starts now, or plays), so the fight does not wake yet; false when the fight wakes at the point as before.
        /// </summary>
        bool EntranceTakes(LevelRoom room, RoomFight fight)
        {
            if (_entering) return fight == _enterFight;
            if (!BossEntranceValues.On || room == null || fight == null || _level == null || room.Order == _enterPlayedRoom) return false;
            if (!(_level.Boss is EnemyBoss boss) || boss.IsDead || !boss.isActiveAndEnabled || boss.Fight != fight) return false; // a boss switched off (a test's, a tool's) has none
            BeginEntrance(room, fight, boss);
            return true;
        }

        void BeginEntrance(LevelRoom room, RoomFight fight, EnemyBoss boss)
        {
            _entering = true;
            _enterFrozen = false;
            _enterPlayedRoom = room.Order;
            _enterFight = fight;
            _enterBoss = boss;
            _enterT = 0f;
            _enterLastReal = Time.unscaledTime;
            _enterStartFrame = Time.frameCount;
            EntrancesBegun++;
            // Everything holds, as under the pause: the time scale from before given back at the end, never a hit stop's 0.
            _enterScaleBefore = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            _enterHeldInput = !PlayerInputDriver.HeldByScreen;
            if (_enterHeldInput) PlayerInputDriver.HeldByScreen = true;
            _enterMadeUntouchable = _health != null && !_health.Untouchable;
            if (_enterMadeUntouchable) _health.Untouchable = true;
            _enterDriver = _player != null ? _player.GetComponent<PlayerInputDriver>() : null;
            // The boss stopped: it neither thinks nor wakes; its look is shown by the entrance.
            _enterLookBase = Time.time;
            boss.enabled = false;
            _enterFacingFrom = boss.transform.rotation;
            _enterFacingTo = FacingHer(boss, _enterFacingFrom);
            // Facing the camera: against its look laid flat (the game's yaw: toward the bottom of the screen).
            Transform look = Camera.main != null ? Camera.main.transform : null;
            Vector3 toCamera = look != null ? -Flat(look.forward) : Vector3.back;
            _enterFacingViewer = toCamera.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(toCamera.normalized, Vector3.up) : _enterFacingFrom;
            // Its Idle stands it bladed and sways it (the first pictures showed its front at one moment, its side at the next): its face
            // is held toward the camera by turning its model against where its head looks (EnemyLook.FacingOf's bones).
            EnemyLook bossLook = boss.Look;
            _enterHead = bossLook != null ? bossLook.Bone("Head") : null;
            _enterHeadFront = bossLook != null ? bossLook.Bone("headfront") : null;
            // Its stagger bar over its head (work order 008) is not shown while the card is up: in the close shot it floated at the
            // top of the screen. It is given back as it was at the end.
            StaggerBarView bar = boss.GetComponentInChildren<StaggerBarView>(true);
            _enterBar = bar != null ? bar.gameObject : null;
            _enterBarWasShown = _enterBar != null && _enterBar.activeSelf;
            if (_enterBarWasShown) _enterBar.SetActive(false);
            // The play camera's own rig, moved by the entrance's numbers.
            _enterRig = Camera.main != null ? Camera.main.GetComponent<CameraRig>() : null;
            if (_enterRig == null) _enterRig = FindAnyObjectByType<CameraRig>();
            _shot.Values = Entrance;
            _shot.Share = 0f;
            _shot.BossFocus = Flat(boss.transform.position) + (toCamera.sqrMagnitude > 1e-6f ? toCamera.normalized : Vector3.back) * Entrance.FocusNearer;
            if (_enterRig != null) _enterRig.Shot = _shot;
            // The card: the boss's own name as the game has it, and the run's line.
            EntranceName = (boss.Tuning != null && !string.IsNullOrEmpty(boss.Tuning.Title) ? boss.Tuning.Title : "").ToUpperInvariant();
            ulong seed = RunSeed ?? LastSeed ?? (_level.Layout != null ? _level.Layout.Seed : 0UL);
            EntranceLine = BossEntranceLines.For(seed, BossEntranceLines.Lines);
            if (_card == null) _card = GetComponent<BossEntranceCard>();
            if (_card == null) _card = gameObject.AddComponent<BossEntranceCard>();
            _card.Levels = this;
            PoseEntrance(0f);
            Debug.Log("Boss entrance (work order 130): floor " + Floor + ", " + EntranceName + ", \"" + EntranceLine + "\", " + Entrance.Total.ToString("0.0") + " s");
        }

        /// <summary>Every frame, after the room fights: the entrance goes on by real time, or ends (its end, or a key).</summary>
        void TickBossEntrance()
        {
            if (!_entering) return;
            float now = Time.unscaledTime;
            if (!_enterFrozen) _enterT += Mathf.Clamp(now - _enterLastReal, 0f, EntranceMostStep);
            _enterLastReal = now;
            Time.timeScale = 0f;
            if (_enterBoss == null || _level == null)
            {
                EndBossEntrance(stir: false);
                return;
            }
            if (SkipPressed())
            {
                EntrancesSkipped++;
                LastEntranceSkipped = true;
                EndBossEntrance(stir: true);
                return;
            }
            if (_enterT >= Entrance.Total && !_enterFrozen)
            {
                LastEntranceSkipped = false;
                EndBossEntrance(stir: true);
                return;
            }
            PoseEntrance(_enterT);
        }

        /// <summary>
        /// The intent of a skip (work order 130): as one of her action keys, Enter, Space or Esc. False when none plays. Not held to the
        /// keys' skip grace (work order 140, <see cref="BossEntranceValues.SkipGrace"/>): the tests and tools that call it end it at once.
        /// </summary>
        public bool SkipEntrance()
        {
            if (!_entering) return false;
            EntrancesSkipped++;
            LastEntranceSkipped = true;
            EndBossEntrance(stir: true);
            return true;
        }

        /// <summary>
        /// For the pictures (work order 130): the entrance shown at this moment and held there until <see cref="ReleaseEntranceTime"/>.
        /// </summary>
        public void HoldEntranceAt(float seconds)
        {
            if (!_entering) return;
            _enterFrozen = true;
            _enterT = Mathf.Clamp(seconds, 0f, Entrance.Total);
            PoseEntrance(_enterT);
        }

        /// <summary>The entrance goes on by real time again from where it was held.</summary>
        public void ReleaseEntranceTime()
        {
            _enterFrozen = false;
            _enterLastReal = Time.unscaledTime;
        }

        bool SkipPressed()
        {
            if (Time.frameCount == _enterStartFrame || _enterT < Entrance.SkipGrace) return false; // the press that walked her in is not one; work order 140: nor one in its first 0.3 s (a press read only as it comes, so not remembered)
            Keyboard k = ChoiceKeys;
            if (k != null && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame || k.escapeKey.wasPressedThisFrame))
                return true;
            Mouse m = PlayerInputDriver.ReadRealDevices ? Mouse.current : null;
            InputBindings b = _enterDriver != null ? _enterDriver.ActiveBindings : null;
            if (b == null) return false;
            if (Pressed(b.Shoot, m, k) || KeyPressed(k, b.Dash) || KeyPressed(k, b.Focus) || KeyPressed(k, b.Interact)) return true;
            if (b.Skills != null)
                for (int i = 0; i < b.Skills.Length; i++)
                    if ((_player == null || _player.KeyInUse(i)) && Pressed(b.Skills[i], m, k)) return true; // the merge of 2026-11-26: a key that holds no skill (3 and 4 with work order 138's six slots) is no action key
            return false;
        }

        static bool Pressed(InputBinding binding, Mouse m, Keyboard k)
        {
            var control = binding.Control(m, k);
            return control != null && control.wasPressedThisFrame;
        }

        static bool KeyPressed(Keyboard k, Key key) => k != null && key != Key.None && k[key].wasPressedThisFrame;

        /// <summary>The camera's share, the boss's body and the card at this moment.</summary>
        void PoseEntrance(float t)
        {
            BossEntranceValues v = Entrance;
            _shot.Share = BossEntranceRules.CameraShare(t, v);
            EntranceCardShows = BossEntranceRules.CardAlpha(t, v);
            EnemyBoss boss = _enterBoss;
            if (boss == null) return;
            EntrancePose pose = BossEntranceRules.PoseAt(t, v);
            boss.transform.rotation = Quaternion.Slerp(Quaternion.Slerp(_enterFacingFrom, _enterFacingViewer, pose.Turn), _enterFacingTo, pose.ToHer); // to the camera for the card, then to her
            EnemyLook look = boss.Look;
            if (look == null || look.Pivot == null) return;
            look.Tick(boss, Vector3.zero, _enterLookBase + t); // its Idle goes on under the entrance (it is stopped: nothing else shows it)
            // Its face to the camera as it turns there, whatever its Idle does to its body (the Tick above set the model's own turn).
            float face = pose.Turn * (1f - pose.ToHer);
            if (face > 0f && _enterHead != null && _enterHeadFront != null)
            {
                Vector3 looks = boss.transform.InverseTransformVector(_enterHeadFront.position - _enterHead.position);
                if (looks.x * looks.x + looks.z * looks.z > 1e-8f)
                    look.Pivot.localRotation = Quaternion.AngleAxis(-Mathf.Atan2(looks.x, looks.z) * Mathf.Rad2Deg * face, Vector3.up) * look.Pivot.localRotation;
            }
            float scale = look.Measured.Scale;
            look.Pivot.localPosition = Vector3.up * (look.Measured.Lift + pose.Lift);
            look.Pivot.localScale = new Vector3(scale, scale * pose.Height, scale);
        }

        /// <summary>
        /// The end of the entrance, by its end or a skip (<paramref name="stir"/>: the fight wakes as at the point), or without the
        /// fight when the floor goes: everything given back as it was, the boss in its fight's pose, the camera the play camera.
        /// </summary>
        void EndBossEntrance(bool stir)
        {
            if (!_entering) return;
            _entering = false;
            _enterFrozen = false;
            _enterEndedFrame = Time.frameCount;
            EntranceCardShows = 0f;
            if (_enterRig != null && ReferenceEquals(_enterRig.Shot, _shot)) _enterRig.Shot = null;
            _enterRig = null;
            _shot.Share = 0f;
            EnemyBoss boss = _enterBoss;
            if (boss != null)
            {
                boss.transform.rotation = FacingHer(boss, _enterFacingTo);
                EnemyLook look = boss.Look;
                if (look != null && look.Pivot != null)
                {
                    float scale = look.Measured.Scale;
                    look.Pivot.localPosition = Vector3.up * look.Measured.Lift;
                    look.Pivot.localScale = Vector3.one * scale;
                    look.Tick(boss, Vector3.zero, Time.time); // its clips go on from the game's time again (and its model's own turn)
                }
                boss.enabled = true;
            }
            if (_enterBarWasShown && _enterBar != null) _enterBar.SetActive(true);
            _enterBar = null;
            _enterBarWasShown = false;
            _enterHead = _enterHeadFront = null;
            if (_enterMadeUntouchable && _health != null) _health.Untouchable = false;
            _enterMadeUntouchable = false;
            if (_enterHeldInput) PlayerInputDriver.HeldByScreen = false; // the frame of the letting go: the key that ended it reaches her not
            _enterHeldInput = false;
            Time.timeScale = _enterScaleBefore;
            RoomFight fight = _enterFight;
            _enterFight = null;
            _enterBoss = null;
            _enterDriver = null;
            if (stir && fight != null && !fight.On && (_health == null || _health.IsAlive)) fight.Stir(RoomFightStart.Point, Time.time);
            Debug.Log("Boss entrance (work order 130): " + (stir ? LastEntranceSkipped ? "skipped" : "played to its end" : "ended with the floor"));
        }

        /// <summary>The boss turned flat toward her; as it was when she stands on it.</summary>
        Quaternion FacingHer(EnemyBoss boss, Quaternion otherwise)
        {
            if (_player == null) return otherwise;
            Vector3 d = _player.Position - boss.transform.position;
            d.y = 0f;
            return d.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(d.normalized, Vector3.up) : otherwise;
        }

        static Vector3 Flat(Vector3 p) => new Vector3(p.x, 0f, p.z);
    }
}
