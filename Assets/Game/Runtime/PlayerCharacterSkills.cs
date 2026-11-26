using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;

namespace Roguelike
{
    /// <summary>
    /// The skills of <see cref="PlayerCharacter"/>: their state for the bar and the tests, the kit's slots and keys, a press, its
    /// start, its cooldown, the Slide and the Powder Keg (work order 081 moved them here from PlayerCharacter.cs, code unchanged).
    /// </summary>
    public sealed partial class PlayerCharacter
    {
        // ---------------- Skills, for the skill bar, the aim indicator and tests ----------------

        /// <summary>
        /// The skills as the game reads them, in their list order: <see cref="SettingsSkills"/>, or in a run with skill
        /// upgrades taken the run's own copies of them, patched by the upgrades (work order 017,
        /// <see cref="RunSkillUpgrades.View"/>). While Steady Hand's window is open, copies of those with the window's two
        /// changes (work order 025, <see cref="SteadyHandView"/>). Nothing is written to the settings or to the skills in memory.
        /// </summary>
        public IReadOnlyList<SkillDefinition> Skills
        {
            get
            {
                // Work order 067: her stats' layer (Blast, Wide window) laid on in the run's view, after the upgrades: the one
                // place a skill's area and perfect band are read as she has them. None outside a run or with the stats off.
                SkillStatLayer layer = SkillLayerNow;
                IReadOnlyList<SkillDefinition> skills = SkillUpgrades != null ? SkillUpgrades.View(SettingsSkills, layer)
                                                       : layer.Any ? _layerOnly.View(SettingsSkills, layer) : SettingsSkills;
                return IsSteadyHandWindow ? _steadyView.View(skills, FocusModeValues) : skills;
            }
        }
        /// <summary>The view for her stats' layer when the run hands her no skill upgrades (tests that put only the modifiers on): none taken.</summary>
        readonly RunSkillUpgrades _layerOnly = new RunSkillUpgrades();

        /// <summary>
        /// The skills of the settings, in their list order: the settings asset's, and after them, from the start values in
        /// memory, any skill the code knows that the asset lacks by name (work order 015: an asset from before the pool
        /// still has all twelve). Nothing is written to the asset. The practice room reads these as they are.
        /// </summary>
        public IReadOnlyList<SkillDefinition> SettingsSkills
        {
            get
            {
                SkillDefinition[] own = SkillSet != null ? SkillSet.Skills : null;
                if (own == null || own.Length == 0) return FallbackSkillsNow; // work order 109: the three new ones only with their switch on
                if (!ReferenceEquals(own, _completedFrom) || _completedWithNew != NewSkillsOn)
                {
                    _completedWithNew = NewSkillsOn; // work order 109
                    _completedFrom = own;
                    _completed = own;
                    var missing = new List<SkillDefinition>();
                    foreach (SkillDefinition s in _fallbackSkills)
                    {
                        bool have = false;
                        foreach (SkillDefinition o in own) have |= o != null && o.Name == s.Name;
                        if (!have) missing.Add(s);
                    }
                    if (missing.Count > 0)
                    {
                        var all = new List<SkillDefinition>(own);
                        all.AddRange(missing);
                        _completed = all.ToArray();
                    }
                    _completed = NewSkillRules.Switched(_completed, NewSkillsOn); // work order 109: off, without the three
                }
                return _completed;
            }
        }
        SkillDefinition[] _completedFrom, _completed;

        /// <summary>The slot of the skill in use, or -1.</summary>
        public int ActiveSkillSlot => _actionKind == ActionKind.Skill ? _castSlot : -1;
        public SkillDefinition ActiveSkill => _actionKind == ActionKind.Skill ? _cast.Skill : null;
        public SkillPhase SkillPhase => _actionKind == ActionKind.Skill ? _cast.Phase(Time.time) : SkillPhase.None;
        /// <summary>0 to 1, for a bar: the cast, the hold or the charge of the skill in use.</summary>
        public float SkillProgress => _actionKind == ActionKind.Skill ? _cast.Progress(Time.time) : 0f;
        public float SkillCharge => _actionKind == ActionKind.Skill ? _cast.Charge(Time.time) : 0f;
        /// <summary>Where the skill in use is aimed.</summary>
        public Vector3 AimPoint => _aimPoint;
        public float SkillCooldownRemaining(int slot) =>
            slot >= 0 && slot < Skills.Count ? CooldownFor(slot).Remaining(Time.time) : 0f;
        /// <summary>Charges of the slot's skill ready now (work order 015): 1 or 0 for a skill with one charge.</summary>
        public int SkillChargesReady(int slot) => slot >= 0 && slot < Skills.Count ? CooldownFor(slot).Charges(Time.time) : 0;
        /// <summary>Seconds until the slot's skill gets its next charge back; 0 when all are ready.</summary>
        public float SkillNextCharge(int slot) => slot >= 0 && slot < Skills.Count ? CooldownFor(slot).NextCharge(Time.time) : 0f;
        /// <summary>Seconds the Charge skill in use has charged (work order 015: the perfect band is judged on it); 0 otherwise.</summary>
        public float SkillChargedSeconds => _actionKind == ActionKind.Skill ? _cast.ChargedSeconds(Time.time) : 0f;
        /// <summary>True when the Charge skill in use, let go now, would be a perfect release (work order 015).</summary>
        public bool InPerfectBand => _actionKind == ActionKind.Skill && _cast.Phase(Time.time) == SkillPhase.Channel
                                     && (PoolRules.IsPerfect(_cast.Skill, _cast.ChargedSeconds(Time.time))
                                         || (_castPrime == PrimeUse.PerfectRelease && FocusModeRules.PrimedPerfect(_cast.Charge(Time.time), FocusModeValues)));
        /// <summary>Perfect releases so far (work order 015).</summary>
        public int PerfectReleases { get; private set; }
        /// <summary>True while Slide moves her (work order 015).</summary>
        public bool IsKeyMoving => _keyMoving;
        /// <summary>Kegs lying on their fuse now (work order 015).</summary>
        public int KegsLying => _kegs.Count;
        /// <summary>Where the latest keg lies, or lay; for tests.</summary>
        public Vector3 LastKegPoint { get; private set; }
        /// <summary>Kegs that burst, by their fuse or by a shot.</summary>
        public int KegsBurst { get; private set; }
        public int KegsSetOffByShots { get; private set; }
        /// <summary>Counters that dropped an attack (work order 015, Stopping Shot).</summary>
        public int Counters { get; private set; }
        /// <summary>
        /// The slot whose recovery is running free (decision 0004): the skill is over for the basic shot and the move
        /// keys, but the next skill waits for it. -1 when none.
        /// </summary>
        public int FreeRecoverySlot => Time.time < _skillGateUntil ? _gateSlot : -1;
        /// <summary>Seconds until a new skill may start after a free recovery; 0 when it may start now (or a lock holds it).</summary>
        public float SkillGateRemaining => Mathf.Max(0f, _skillGateUntil - Time.time);
        /// <summary>True while a skill slides her to a stand (decision 0004).</summary>
        public bool IsSkillSliding => _actionKind == ActionKind.Skill && _sliding;
        /// <summary>The share of run speed the latest skill slide started from: what she had at the press.</summary>
        public float SlideStartShare => _slideFrom;
        /// <summary>
        /// The live cursor as the character knows it: the latest cursor report, click or skill press. The arms
        /// follow it while they are held up after a shot (CharacterAnimation).
        /// </summary>
        public Vector3 AimCursor => _shotCursor;

        // ---------------- The kit (decision 0005) ----------------

        public KitValues Kit => SkillSet != null && SkillSet.Kit != null ? SkillSet.Kit : _fallbackKit;

        /// <summary>
        /// The skills she has and the keys they sit on (work order 015, 2026-11-20). A "slot" everywhere in this class is
        /// a skill's place in <see cref="Skills"/>, which says which skill it is; a key is a place on the bar
        /// (<see cref="SkillOnKey"/>), which the input, the skill bar and the reward screen go through. Made from the
        /// skill list the first time it is asked for, and again by <see cref="EquipStartSkills"/>.
        /// </summary>
        SkillLoadout Loadout
        {
            get
            {
                bool all = Kit.StartWithAllSkills;
                bool six = Kit.SixSlots; // work order 138: six slots, read as the bar is made
                if (!_loadoutReady || _loadoutAll != all || _loadout.SixSlots != six)
                {
                    _loadout.SixSlots = six;
                    _loadout.Start(Skills, all);
                    _loadoutReady = true;
                    _loadoutAll = all;
                }
                return _loadout;
            }
        }

        /// <summary>The keys of the bar: eight, in the order of the bindings' skill keys.</summary>
        public int KeyCount => SkillLoadout.KeyCount;
        /// <summary>The slot of the skill on a key, or -1 for an empty key.</summary>
        public int SkillOnKey(int key) => Loadout.SkillOn(key);
        /// <summary>The key a slot's skill sits on, or -1 when it is on none.</summary>
        public int KeyOfSkill(int slot) => Loadout.KeyOf(slot);
        /// <summary>The key a skill taken now would go on: the first free one in key order, or -1.</summary>
        public int FirstFreeKey => Loadout.FirstFreeKey;

        /// <summary>True when the slot holds a skill she can use: one she started the run with, or was given since.</summary>
        public bool IsEquipped(int slot) => slot >= 0 && slot < Skills.Count && (Kit.StartWithAllSkills || Loadout.Has(slot));

        /// <summary>
        /// She has the slot's skill from now on, on the first free key if it is on none (work order 015); with no key free
        /// she has it all the same, for the intents. For the floor reward, tests and tools. Returns its key, or -1.
        /// </summary>
        public int EquipSkill(int slot)
        {
            if (slot < 0 || slot >= Skills.Count) return -1;
            return Loadout.Take(slot);
        }

        /// <summary>Back to the skills a run starts with, on their own keys. For a new run, tests and tools.</summary>
        public void EquipStartSkills() => _loadoutReady = false;

        /// <summary>
        /// The run's upgrades (work order 012, <see cref="RunModifiers"/>), read where the values are used: damage dealt,
        /// a skill's cooldown, run speed, Quick Shot's first mark (and maximum health, by <see cref="PlayerHealth"/>).
        /// Null outside a run: the settings' values as they are. Nothing here writes to the settings.
        /// </summary>
        [System.NonSerialized] public RunModifiers Modifiers;

        /// <summary>
        /// The run's skill upgrades (work order 017, <see cref="RunSkillUpgrades"/>): <see cref="Skills"/> reads the skills
        /// through them. Null outside a run: the settings' skills as they are.
        /// </summary>
        [System.NonSerialized] public RunSkillUpgrades SkillUpgrades;

        /// <summary>How many skill upgrades the slot's skill has this run (work order 017): the pips on its box.</summary>
        public int UpgradesOn(int slot) =>
            SkillUpgrades != null && slot >= 0 && slot < Skills.Count && Skills[slot] != null ? SkillUpgrades.CountFor(Skills[slot].Name) : 0;

        /// <summary>The slot of the skill of this name, or -1.</summary>
        public int SlotOf(string name)
        {
            if (string.IsNullOrEmpty(name)) return -1;
            IReadOnlyList<SkillDefinition> skills = Skills;
            for (int i = 0; i < skills.Count; i++)
                if (skills[i] != null && skills[i].Name == name) return i;
            return -1;
        }

        /// <summary>Tags that jumped from a tagged target that died to the nearest enemy (work order 017, Spreading Tag).</summary>
        public int TagsSpread { get; private set; }

        /// <summary>The slot's cooldown as it runs now: the skill's, less the run's cuts to it.</summary>
        public float CooldownOf(int slot)
        {
            if (slot < 0 || slot >= Skills.Count || Skills[slot] == null) return 0f;
            SkillDefinition skill = Skills[slot];
            return Modifiers != null ? skill.Cooldown * Modifiers.CooldownShare(skill.Name) : skill.Cooldown;
        }

        /// <summary>
        /// The share of the feel's run speed she runs at: 1, more with the run's upgrades; work order 071: times the gains'
        /// (<see cref="FocusMoveScale"/>, 1.1) while spent focus's gains run.
        /// </summary>
        public float RunSpeedShare => (Modifiers != null ? Modifiers.RunSpeedShare : 1f) * FocusMoveScale;

        /// <summary>
        /// Press: the key of a skill went down, with the cursor over this point.
        /// Rejected while the skill is on cooldown. Buffered while the character is locked.
        /// The cooldown starts when the skill starts, so a skill cancelled by a dash is still spent.
        /// </summary>
        public IntentResult PressSkill(int slot, Vector3 point)
        {
            // Work order 142 (RecastGrace): a press, and only a press, is judged with one frame's grace at the end of its clean part.
            _graceNow = RecastGrace.Seconds(Time.deltaTime > 0f ? Time.deltaTime : Mathf.Min(Time.unscaledDeltaTime, RecastClock.MostPerFrame));
            try { return PressSkillJudged(slot, point); }
            finally { _graceNow = 0f; }
        }

        IntentResult PressSkillJudged(int slot, Vector3 point)
        {
            if (slot < 0 || slot >= Skills.Count) return SkillPressed(slot, IntentResult.Rejected, 0f);
            if (!IsEquipped(slot))
            {
                Report(new PlayerReport { Kind = PlayerReportKind.SkillPressed, Slot = slot, Result = IntentResult.Rejected, NotEquipped = true });
                return IntentResult.Rejected;
            }
            if (RefuseWhileSprinting(slot)) return IntentResult.Rejected; // work order 092: no skill while she sprints, and the press is not kept
            // A recast (decision 0005): inside the window it fires the next shot at once, whatever the cooldown; before
            // the shot it follows has gone off it waits like any press during a skill.
            ExpireChain(Time.time);
            RecastPress recast = JudgeRecast(slot, Time.time);
            // Work order 086: down in a held duck, a skill that can go off now (ready, or a recast) brings her up and goes off;
            // one on its cooldown leaves her down.
            RiseToAct(recast != RecastPress.None || CooldownFor(slot).IsReady(Time.time));
            if ((_held.IsHeld || _duck.IsDown) && recast != RecastPress.None)
            {
                // Stunned or pulled (work order 035), or down in a quick duck (work order 043): even a recast waits, as in a lock.
                _shotCursor = point;
                _bufferedSkillReleased = false;
                return SkillPressed(slot, BufferPress(PressKind.Skill, point, slot), 0f);
            }
            if (recast == RecastPress.Shot || recast == RecastPress.Clean)
            {
                _shotCursor = point;
                DropCooldownPress(); // work order 066: a newer press replaces one waiting for its cooldown
                SkillPressed(slot, IntentResult.Started, 0f);
                Recast(slot, point, recast == RecastPress.Clean, buffered: false);
                return IntentResult.Started;
            }
            if (recast == RecastPress.Early)
            {
                _shotCursor = point;
                _bufferedSkillReleased = false;
                return SkillPressed(slot, BufferPress(PressKind.Skill, point, slot), 0f);
            }
            if (!CooldownFor(slot).IsReady(Time.time))
            {
                // Work order 064: Backstep's first press just before its cooldown ends waits for it and goes (he had 12 refused
                // on cooldown on 2026-11-21, 6 of them within 1.4 s of ready); earlier, refused as before. Work order 066: every
                // skill's (CooldownBufferRules, KitValues.CooldownBuffer). A skill with a charge left is ready and never here.
                float left = CooldownFor(slot).Remaining(Time.time);
                if (CooldownBufferRules.Waits(left, Kit.CooldownBuffer))
                {
                    _shotCursor = point;
                    _bufferedSkillReleased = false; // a hold skill holds only if its key is still down when it starts (ReleaseSkill)
                    _buffer.Store(new Press { Kind = PressKind.Skill, Point = point, Slot = slot, At = Time.time, ForCooldown = true, ReadyAt = Time.time + left }, Time.time);
                    return SkillPressed(slot, IntentResult.Buffered, left);
                }
                return SkillPressed(slot, IntentResult.Rejected, left);
            }
            if (RefusesWithNoTarget(slot, report: true)) return IntentResult.Rejected; // work order 109: Deadeye with no marked enemy in sight
            _shotCursor = point; // a press comes with the cursor: the arms held up after the skill follow it from here
            // In a lock, or in a free recovery: the next skill waits out the recovery, through the buffer. Slide (work
            // order 015) is its own move: it waits only for a dash, a committed skill or a Slide still moving her.
            bool waits = Skills[slot].Input == SkillInput.KeyMove
                ? _dashing || _keyMoving || _held.IsHeld || (_actionKind == ActionKind.Skill && !IsInLightSkill)
                : IsLocked || (Time.time < _skillGateUntil && !CutsRecovery(slot));
            waits |= _duck.IsDown; // down in a quick duck no skill starts (work order 043): it waits, as in a lock
            if (waits)
            {
                _bufferedSkillReleased = false;
                return SkillPressed(slot, BufferPress(PressKind.Skill, point, slot), 0f);
            }
            DropCooldownPress(); // work order 066: a newer press replaces one waiting for its cooldown
            SkillPressed(slot, IntentResult.Started, 0f);
            StartSkill(slot, point);
            return IntentResult.Started;
        }

        /// <summary>
        /// Work order 066: drops a press waiting for its skill's cooldown, if one waits (a newer press that starts at once
        /// replaces it; one that waits replaces it in the buffer itself). Any other buffered press stays.
        /// </summary>
        void DropCooldownPress()
        {
            if (!_buffer.TryPeek(out Press waiting) || !waiting.ForCooldown) return;
            _buffer.Clear();
            Report(new PlayerReport { Kind = PlayerReportKind.BufferDropped, Slot = waiting.Slot });
        }

        /// <summary>
        /// Work order 066: every frame, a press waiting for its cooldown is dropped once she is dead, stunned or pulled, or a
        /// choice, the pause menu or an end screen is up (<see cref="CooldownBufferRules.Kept"/>).
        /// </summary>
        void DropCooldownPressWhenHeldBack()
        {
            if (!_buffer.TryPeek(out Press waiting) || !waiting.ForCooldown) return;
            if (_herHealth == null) _herHealth = GetComponent<PlayerHealth>();
            if (CooldownBufferRules.Kept(_herHealth == null || _herHealth.IsAlive, _held.IsHeld,
                                         PlayerInputDriver.HeldByChoice, PlayerInputDriver.HeldByScreen)) return;
            DropCooldownPress();
        }

        /// <summary>
        /// For tests (work order 066): the slot's skill on cooldown with <paramref name="left"/> seconds to go and every charge
        /// spent; the charges after the first come back a whole cooldown apart, as after real uses.
        /// </summary>
        public void SpendCooldownForTests(int slot, float left)
        {
            if (slot < 0 || slot >= Skills.Count) return;
            while (_cooldowns.Count <= slot) _cooldowns.Add(new Cooldown());
            _cooldowns[slot] = new Cooldown();
            Cooldown cooldown = CooldownFor(slot);
            float now = Time.time;
            cooldown.Start(now, left);
            for (int i = 1; i < cooldown.MaxCharges; i++) cooldown.Start(now, CooldownOf(slot));
        }

        /// <summary>
        /// A skill press that waited in the buffer may start now: a recast whose window has opened, or an ordinary start.
        /// A press that is neither any more (its chain ran out and the skill is cooling down) is dropped.
        /// </summary>
        bool StartWaitingSkill(Press press, float now)
        {
            ExpireChain(now);
            RecastPress recast = JudgeRecast(press.Slot, now, buffered: true); // work order 040: a press that waited is never clean
            if (recast == RecastPress.Shot || recast == RecastPress.Clean)
            {
                Recast(press.Slot, press.Point, recast == RecastPress.Clean, buffered: true);
                return true;
            }
            if (recast == RecastPress.None && CooldownFor(press.Slot).IsReady(now) && !RefusesWithNoTarget(press.Slot, report: false)) // work order 109: Deadeye's targets gone while it waited
            {
                // Work order 066: a press that waited for its cooldown is aimed where the cursor is now, when it goes; a press
                // that waited out a lock, at its own point, as before.
                StartSkill(press.Slot, press.ForCooldown && _cursorKnown ? (PlacedOnTheGround(press.Slot) ? _groundCursor : _shotCursor) : press.Point); // work order 094: the floor's point for one placed there
                return true;
            }
            Report(new PlayerReport { Kind = PlayerReportKind.BufferDropped, Slot = press.Slot });
            return false;
        }

        /// <summary>
        /// The key of a skill went up. Ends a Holding skill and fires a Charge skill.
        /// If that skill's press is still waiting in the buffer, the skill starts as already released.
        /// </summary>
        public void ReleaseSkill(int slot)
        {
            Report(new PlayerReport { Kind = PlayerReportKind.SkillReleased, Slot = slot });
            if (_actionKind == ActionKind.Skill && _castSlot == slot) _cast.Release(Time.time);
            // Only the waiting skill's own key: the key of the skill before it going up says nothing about this one.
            // A press can wait out a whole free recovery now, and keys are rolled: the next goes down before the last
            // comes up. Before 2026-11-19 any skill key going up did this, and a Charge skill that waited fired at
            // once with no charge.
            else if (_buffer.TryPeek(out Press waiting) && waiting.Kind == PressKind.Skill && waiting.Slot == slot)
                _bufferedSkillReleased = true;
        }

        /// <summary>
        /// Where the cursor is. A skill that is being held or charged follows it, from the first report
        /// after the skill started. Without a report it stays aimed at the point it was pressed with.
        /// A basic shot follows it until it goes off, and she looks toward it.
        /// </summary>
        public void AimAt(Vector3 point)
        {
            SetCursor(point, point);
            _cursorOnCamera = false; // a point in the world (tests, tools): it stays where it is
        }

        /// <summary>
        /// Work order 094: the cursor as the input driver reads it, as two points on its ray, both laid on the ground.
        /// <paramref name="point"/> is under where the ray meets the level at <paramref name="height"/>, the gun's
        /// (<see cref="ShotHeight"/>): the shot, her look, a held line or cone follow it, as <see cref="AimAt(Vector3)"/> says.
        /// <paramref name="ground"/> is where it meets the floor: a skill placed on the ground (<see cref="PlacedOnTheGround"/>)
        /// that waited for its cooldown lands there.
        /// </summary>
        public void AimAt(Vector3 point, Vector3 ground, float height)
        {
            SetCursor(point, ground);
            _cursorHeight = height;
            _cursorReadFrom = transform.position;
            _cursorOnCamera = true;
        }

        void SetCursor(Vector3 point, Vector3 ground)
        {
            _cursorPoint = point;
            _hasCursor = true;
            _cursorKnown = true;
            _shotCursor = point;
            _groundCursor = ground;
        }

        Vector3 _groundCursor;   // work order 094: the cursor's point on the floor, the latest report
        float _cursorHeight;     // the height its ray was met at for the aim
        Vector3 _cursorReadFrom; // where she stood when the driver read it
        bool _cursorOnCamera;    // it came from the screen (the driver), not a point in the world

        /// <summary>
        /// Work order 094, her aim while she runs: the camera follows her, so the ground under a cursor held still on the screen
        /// moves with her. A shot that goes off, or a line drawn, after she has moved since the driver's read takes the cursor's
        /// point moved on by that move (one frame's at most, 0.1 m at a run at 60 frames a second). Zero for a point in the world
        /// (<see cref="AimAt(Vector3)"/>) and with <see cref="FeelValues.AimAtShotHeight"/> off.
        /// </summary>
        Vector3 CursorDrift =>
            _cursorOnCamera && Feel.AimAtShotHeight ? new Vector3(transform.position.x - _cursorReadFrom.x, 0f, transform.position.z - _cursorReadFrom.z) : Vector3.zero;

        /// <summary>
        /// Work order 094: the cursor's point slid along its ray to another height. The shot's aim line (<see cref="BasicShotAimLine"/>;
        /// until work order 102 the Laser Sight's line) is read after the animation has moved her pistols (at a run by up to a
        /// tenth of a metre a frame), at a height a little off the one the driver met the ray at; the point under the ray at the
        /// line's own height keeps the line through the cursor. Both
        /// points the driver gave lie under the one ray, which runs straight, so the point at any height lies on the line
        /// through them, as far along as the height is. Any other point, and with the switch off, as it is.
        /// </summary>
        Vector3 AtHeight(Vector3 cursor, float height)
        {
            if (!_cursorOnCamera || !Feel.AimAtShotHeight || _cursorHeight <= 1e-3f || cursor != _cursorPoint) return cursor;
            return _groundCursor + (cursor - _groundCursor) * (height / _cursorHeight);
        }

        /// <summary>
        /// Work order 094: the slot's skill lands on the floor under the cursor (a circle, a press on a point: Powder Keg), so it
        /// is aimed at the cursor's ground point; every other skill is a line or a cone from her, aimed at the gun's height.
        /// </summary>
        public bool PlacedOnTheGround(int slot) =>
            slot >= 0 && slot < Skills.Count && Skills[slot] != null && (Skills[slot].Shape == SkillShape.Circle || Skills[slot].Input == SkillInput.Point);

        /// <summary>Press and let go of the first skill at once. Shorthand for tests and tools.</summary>
        public IntentResult CastAt(Vector3 point)
        {
            IntentResult result = PressSkill(0, point);
            if (result != IntentResult.Rejected) ReleaseSkill(0);
            return result;
        }

        // ---------------- Slide: a skill that moves her along the move keys (work order 015, 2026-11-20) ----------------
        // Not the slowdown of decision 0004 (SkillSlide, _sliding): this is a skill of its own. She moves MoveDistance
        // along the held move keys (the way she faces with none held) over its Startup, on the NavMesh as the dash is, with
        // no invulnerability. It is no action: a basic shot starts and goes off during it as anywhere, and does not slow
        // it, for nothing else moves her while it runs. A dash ends it. Its charges are the cooldown's (Cooldown.MaxCharges).

        void StartKeyMove(int slot)
        {
            SkillDefinition skill = Skills[slot];
            float now = Time.time;
            Vector3 dir = _moveInput != Vector3.zero ? _moveInput : Flat(transform.forward);
            if (dir.sqrMagnitude < 1e-6f) dir = Vector3.forward;
            dir.Normalize();
            LeaveCover(CoverLeaving.MovingSkill); // a skill that moves her takes her out of cover, as the dash does
            CooldownFor(slot).Start(now, CooldownOf(slot));
            _keyMoving = true;
            _keyMoveSlot = slot;
            _keyMoveFrom = transform.position;
            _keyMoveTo = _keyMoveFrom + dir * skill.MoveDistance;
            _keyMoveStartedAt = now;
            _keyMoveSeconds = Mathf.Max(0f, skill.Startup);
            Halt();
            Report(new PlayerReport { Kind = PlayerReportKind.SkillStarted, Slot = slot });
            KeyMove(now);
        }

        void KeyMove(float now)
        {
            float k = _keyMoveSeconds > 0f ? Mathf.Clamp01((now - _keyMoveStartedAt) / _keyMoveSeconds) : 1f;
            Vector3 target = Vector3.Lerp(_keyMoveFrom, _keyMoveTo, k);
            if (_agent.isOnNavMesh)
            {
                _agent.velocity = Vector3.zero;
                // NavMeshAgent.Move keeps her on the NavMesh, so it stops at walls and edges, as the dash does.
                _agent.Move(Flat(target - transform.position));
            }
            if (k >= 1f) EndKeyMove();
        }

        void EndKeyMove()
        {
            if (!_keyMoving) return;
            _keyMoving = false;
            Report(new PlayerReport { Kind = PlayerReportKind.SkillEnded, Slot = _keyMoveSlot, Ending = SkillEnding.RanOut });
            _keyMoveSlot = -1;
        }

        // ---------------- Powder Keg: thrown, then set off (work order 015, 2026-11-20) ----------------
        // A skill with a Fuse does not burst where it lands: the keg lies there with its circle showing, and bursts when the
        // fuse runs out, or at once, for ShotBurstScale times the damage, when a shot of hers passes through it (a basic
        // shot, or a line skill; the shot goes on). Each keg is its own: two would lie and burst apart.

        void PlaceKeg(SkillDefinition skill, SkillArea area, Look look)
        {
            GroundPoint c = area.Centre;
            var centre = new Vector3(c.X, 0f, c.Z);
            LastKegPoint = centre;
            ShotFx circle = SkillLookValues.EffectsNotShapes ? SkillLookKegLanded() : ShotFx.Outline(centre, skill.Radius, skill.Fuse, look.Paint, ShotFxMaterial); // work order 123: the keg, not a circle
            _kegs.Add(new LyingKeg { Skill = skill, Slot = _castSlot, Centre = c, BurstAt = Time.time + skill.Fuse, Circle = circle, Prime = _castPrime });
        }

        void BurstKegsOnFuse(float now)
        {
            for (int i = _kegs.Count - 1; i >= 0; i--)
                if (i < _kegs.Count && now >= _kegs[i].BurstAt) BurstKeg(i, 1f);
        }

        /// <summary>Sets off every lying keg a line from <paramref name="origin"/> toward <paramref name="aim"/>, this long and wide, passes through.</summary>
        void SetOffKegs(GroundPoint origin, GroundPoint aim, float range, float halfWidth, float length)
        {
            for (int i = _kegs.Count - 1; i >= 0; i--)
            {
                if (i >= _kegs.Count) continue;
                LyingKeg keg = _kegs[i];
                if (!LineShot.Hits(origin, aim, range, halfWidth, keg.Centre, KegRadius, length)) continue;
                KegsSetOffByShots++;
                BurstKeg(i, Mathf.Max(0f, keg.Skill.ShotBurstScale));
            }
        }

        void BurstKeg(int index, float damageScale)
        {
            LyingKeg keg = _kegs[index];
            _kegs.RemoveAt(index);
            if (keg.Circle != null) Destroy(keg.Circle.gameObject);
            KegsBurst++;
            var area = new SkillArea(keg.Skill, keg.Centre, keg.Centre, CoverQuery);
            Look look = LookFor(keg.Skill, 1f);
            Vector3 shotPoint = SpawnSkillFx(keg.Skill, look, area);
            PlaySound(look);
            int damage = Mathf.RoundToInt((SkillRules.Damage(keg.Skill, 1f) + FlatFor(keg.Skill)) * damageScale); // work order 050: her flat damage, by its weight
            bool landed = HitArea(keg.Skill, keg.Slot, area, look, shotPoint, damage, keg.Skill.Stagger, clean: false, out _, keg.Prime);
            Report(new PlayerReport { Kind = PlayerReportKind.SkillFired, Slot = keg.Slot, Landed = landed, Charge = 1f, Clean = damageScale > 1f, At = keg.Centre });
            if (landed) HitStop.Trigger(Feel.HitStop);
        }

        void ClearKegs()
        {
            foreach (LyingKeg keg in _kegs)
                if (keg.Circle != null) Destroy(keg.Circle.gameObject);
            _kegs.Clear();
        }

        /// <summary>
        /// Starts a skill. A recast (decision 0005) starts the next shot of the chain: no cooldown, and the chain goes on;
        /// any other start ends a chain that runs and begins this skill's own.
        /// </summary>
        void StartSkill(int slot, Vector3 point, bool recast = false)
        {
            SkillDefinition skill = Skills[slot];
            if (skill.Input == SkillInput.KeyMove)
            {
                StartKeyMove(slot);
                return;
            }
            bool light = skill.Lock == SkillLock.Light;
            // A skill moves her over its startup, but the shot after an opening move does not: Backstep hops on its
            // first press only (amended 2026-11-19).
            bool moves = skill.MoveDistance != 0f && !(recast && skill.OpeningMove);
            FeelValues feel = Feel;
            if (moves) LeaveCover(CoverLeaving.MovingSkill); // a skill that moves her takes her out of cover, as the dash does
            DropWaitingClick(); // a skill drops a waiting click
            _action.Cancel(); // a skill may start during a basic shot, and ends it
            _movingShot = false;
            _shotTurnsBody = false;
            _easing = false; // a skill ends the slow of a shot on the move at once
            _skillGateUntil = float.NegativeInfinity; // the recovery it waited for is over
            _aimPoint = point;
            if (light)
            {
                // A light skill (decision 0005) turns the body as a basic shot does: only for an aim outside the front
                // angle, held turned while she backpedals; within it the upper body aims. No stop, no slide.
                _sliding = false;
                _slideFrom = 0f;
                _shotFacing.ShotStarted(BodyMustTurnToward(Flat(point - transform.position), feel), Time.time, OffTheMark.ShotTurnHold(feel)); // work order 040
            }
            else
            {
                _shotFacing.End(turnBack: false); // a committed skill ends the backpedal at once: it faces the cursor
                // The slide (decision 0004): from the speed she actually has, along the way she actually moves. During a
                // hit stop the agent reads standing, so the last frame in which time ran is used.
                Vector3 velocity = Flat(Time.timeScale > 0f ? _agent.velocity : _lastVelocity);
                float share = feel.RunSpeed > 0f ? Mathf.Clamp01(velocity.magnitude / feel.RunSpeed) : 0f;
                _rising = false;
                // In cover she does not leave her place (an experiment, 2026-11-19): no slide.
                _sliding = !moves && !_inCover && feel.SkillSlowdown > 0f && share > 0.01f;
                if (_sliding)
                {
                    _slideFrom = share;
                    _slideStartedAt = Time.time;
                    _slideDir = velocity / velocity.magnitude;
                }
                else
                {
                    _slideFrom = 0f;
                    Halt();
                }
            }
            _castSlot = slot;
            _hasCursor = false; // follow only a cursor that is reported after this skill started, never a stale one
            _castMoved = 0f;
            Vector3 toAim = Flat(point - transform.position);
            _castMoveDir = toAim.sqrMagnitude > 1e-6f ? toAim.normalized : Flat(transform.forward).normalized;
            if (feel.FaceCursorOnCast && !light) Face(Flat(point - transform.position));

            _actionKind = ActionKind.Skill;
            ActionsStarted++;
            float startup = StartupOf(skill, recast); // work order 064: Backstep's shot after its hop goes off quicker
            ActionLeadTime = skill.Input == SkillInput.Charge ? 0f : startup;
            _cast.Start(skill, Time.time, startup);
            _chainPressAt = Time.time; // work order 040: the cast bar's chain runs from this press (read only while a chain runs)
            // Snap Shot (work order 017): a Charge skill with no charging is let go at its press, and fires as a perfect release.
            if (skill.Input == SkillInput.Charge && skill.NoCharge) _cast.Release(Time.time);
            _markedAt.Clear();
            _castClean = false;
            _castFullClean = false;
            _castMoves = moves;
            if (!recast)
            {
                EndChain(); // another skill starting ends a chain (decision 0005)
                _chain.Start(slot, skill.RecastCount, skill.RecastWindow, skill.OpeningMove);
                _chain.NextWindow(ChainBeat.Window); // work order 069: the second press's own window (RecastRhythm)
                CooldownFor(slot).Start(Time.time, CooldownOf(slot));
                TakePrime(slot, skill);
                _useKillCut = _useDazeCut = false; // a new use: its kill and daze cuts may come once (work order 033)
            }
            _castOpening = !recast && _chain.IsOpeningMove;
            NewSkillStarted(skill); // work order 109: Deadeye's aim tone
            Report(new PlayerReport { Kind = PlayerReportKind.SkillStarted, Slot = slot });
        }

        Cooldown CooldownFor(int slot)
        {
            while (_cooldowns.Count <= slot) _cooldowns.Add(new Cooldown());
            Cooldown cooldown = _cooldowns[slot];
            // Charges (work order 015): read live from the skill, so a change in the asset applies at once.
            if (slot < Skills.Count && Skills[slot] != null) cooldown.MaxCharges = Skills[slot].Charges;
            return cooldown;
        }
    }
}
