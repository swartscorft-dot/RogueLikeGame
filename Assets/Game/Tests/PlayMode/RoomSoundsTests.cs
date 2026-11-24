using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using AllocExt = UnityEngine.TestTools.Constraints.ConstraintExtensions;
using AllocIs = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 113, the sounds of the rooms, in the ControlRoom scene through the intents of <see cref="GeneratedLevels"/>, on
    /// floors with passages and templates (the defaults), with the rooms' switch on: a room's doors shutting play one shut sound a
    /// door, each where its block stands, and none of the old impact; their opening one open sound a door; the room's fight waking
    /// one swell, once a room (not again when it wakes again), and none of the boss's growl; a combat room cleared one release and
    /// none of the way opening; the stairs to the next floor one sound, not at the run's start; the room tone floor by floor, floor
    /// 1 today's own clip, each floor its row's; a recording under each new name played in its place; the switch off silent for the
    /// new sounds, with the old ones and today's tone on every floor; nothing allocated as a door sound plays, nor in a frame while
    /// nothing changes. All through what was asked to play (<see cref="PlaceholderSounds.Requested"/>,
    /// <see cref="PlaceholderSounds.Placed"/>): nobody but Kristian can hear whether they sound right.
    ///
    /// She cannot be hurt. Frames without a window are short (about 0.3 ms): the waits count frames or game seconds.
    /// </summary>
    public class RoomSoundsTests
    {
        const ulong RunSeed = 0x41414141UL; // the doors' tests' run: its first room is a combat room past a passage
        const ulong FloorSeed = 0x113113UL;

        PlayerCharacter _player;
        PlayerHealth _health;
        GeneratedLevels _levels;
        PlaceholderSounds _sounds;
        Func<string, AudioClip> _recordedBefore;
        readonly List<PlaceholderSoundRequest> _played = new List<PlaceholderSoundRequest>(4096);
        readonly List<PlacedSoundRequest> _placed = new List<PlacedSoundRequest>(4096);
        readonly List<Object> _made = new List<Object>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            _recordedBefore = PlaceholderSounds.RecordedClips;
            PlaceholderSounds.Requested += Record;
            PlaceholderSounds.Placed += RecordPlaced;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            _health.Untouchable = true;
            _levels = GeneratedLevels.Ensure();
            _levels.RunValues.SkillTiers = false;
            GameSettings.SetGreybox(false, keep: false);
            _sounds = PlaceholderSounds.Ensure();
            yield return null;
            _played.Clear();
            _placed.Clear();
        }

        [TearDown]
        public void CleanUp()
        {
            PlaceholderSounds.Requested -= Record;
            PlaceholderSounds.Placed -= RecordPlaced;
            PlaceholderSounds.RecordedClips = _recordedBefore;
            if (_sounds != null) Object.Destroy(_sounds.gameObject);
            _sounds = null;
            if (_player != null)
            {
                _player.Stop();
                _player.GetComponent<PlayerInputDriver>().InputSource = null;
            }
            if (_health != null) _health.Untouchable = false;
            if (_levels != null)
            {
                if (_levels.Paused) _levels.Resume();
                _levels.BackToPracticeRoom();
                Object.Destroy(_levels.gameObject);
            }
            _levels = null;
            foreach (Object o in _made) if (o != null) Object.Destroy(o);
            _made.Clear();
            GameSettings.Unload();
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
            _played.Clear();
            _placed.Clear();
        }

        void Record(PlaceholderSoundRequest r) => _played.Add(r);
        void RecordPlaced(PlacedSoundRequest r) => _placed.Add(r);

        // ---------------- Helpers ----------------

        int Count(PlaceholderSound sound) => _played.Count(r => r.Sound == sound);
        int Placed(PlaceholderSound sound) => _placed.Count(r => r.Sound == sound);
        string Heard => string.Join(", ", _played.Select(r => r.Sound + "@" + r.Frame));

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        static IEnumerator Until(Func<bool> condition, int most = 600)
        {
            for (int i = 0; i < most && !condition(); i++) yield return null;
        }

        static IEnumerator GameTime(float seconds)
        {
            float until = Time.time + seconds;
            float deadline = Time.realtimeSinceStartup + seconds + 10f;
            while (Time.time < until && Time.realtimeSinceStartup < deadline) yield return null;
        }

        GeneratedLevel StartRun()
        {
            _levels.Values = new LevelValues();
            GeneratedLevel level = _levels.StartRun(RunSeed);
            Assert.That(level, Is.Not.Null);
            Assert.That(level.Layout.Passages && level.Layout.Templates, "a floor with passages and templates");
            return level;
        }

        static (LevelRoom room, List<Enemy> own) FirstRoom(GeneratedLevel level)
        {
            LevelRoom room = level.Layout.Rooms[2];
            Assert.That(room.Kind, Is.EqualTo(RoomKind.Combat), "the run's first room past its passage");
            return (room, level.EnemiesByRoom[room.Order]);
        }

        static Vector3 InFrom(GeneratedLevel level, LevelRoom room, LevelDoor door, double depth) =>
            level.ToWorld(RoomDoorRules.PullInPlace(level.Layout, room, door, depth, 0d));

        /// <summary>She stands past the room's line: it wakes and its doors shut. Its doors.</summary>
        IEnumerator ShutIn(GeneratedLevel level, LevelRoom room, Action<GeneratedLevels.RoomDoors> done)
        {
            LevelDoor door = LevelGeometry.EntryDoor(level.Layout, room);
            _player.Teleport(InFrom(level, room, door, 5.5));
            yield return Until(() => _levels.CurrentRoom == room, 30);
            GeneratedLevels.RoomDoors d = _levels.DoorsOf(room);
            Assert.That(d, Is.Not.Null, "the room has its doors");
            yield return Until(() => d.Shut, 10);
            Assert.That(d.Shut, Is.True, "past the line: shut");
            yield return Frames(2);
            done(d);
        }

        static void Kill(IEnumerable<Enemy> enemies)
        {
            foreach (Enemy e in enemies.ToList())
                if (e != null && !e.IsDead) e.GetComponent<Damageable>().TakeHit(100000000);
        }

        IEnumerator ToTheNextFloor()
        {
            int floor = _levels.Floor;
            _levels.FinishFloorForTests();
            yield return Until(() => _levels.Choice == GeneratedLevels.ChoiceKind.FloorReward);
            Assert.That(_levels.PickReward(0), Is.True, "the floor's reward, floor " + floor);
            yield return Frames(2);
            if (_levels.Choice == GeneratedLevels.ChoiceKind.FocusChoice)
            {
                Assert.That(_levels.PickFocusChoice(0), Is.True, "the focus choice");
                yield return Frames(2);
            }
            Assert.That(_levels.Floor, Is.EqualTo(floor + 1), "down the stairs");
            yield return Frames(2);
        }

        static AudioClip ToneOf(GeneratedLevel level)
        {
            Assert.That(level.Mood, Is.Not.Null, "the floor has its mood");
            Assert.That(level.Mood.Tone, Is.Not.Null, "and its tone");
            return level.Mood.Tone.clip;
        }

        AudioClip MadeClip(string name, float seconds)
        {
            int n = Mathf.RoundToInt(seconds * 44100f);
            var clip = AudioClip.Create(name, n, 1, 44100, false);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = 0.3f * Mathf.Sin(i * 0.03f) * (1f - i / (float)n);
            clip.SetData(data, 0);
            _made.Add(clip);
            return clip;
        }

        // ---------------- The doors ----------------

        [UnityTest, Timeout(180000)]
        public IEnumerator TheDoorsShut_OneShutSoundADoor_WhereItStands_AndOpen_OneOpenSoundADoor_NotTheOldImpact()
        {
            GeneratedLevel level = StartRun();
            yield return Frames(2);
            (LevelRoom room, List<Enemy> own) = FirstRoom(level);
            _played.Clear();
            _placed.Clear();
            GeneratedLevels.RoomDoors d = null;
            yield return ShutIn(level, room, x => d = x);
            int doors = d.Blocks.Count;
            Assert.That(doors, Is.GreaterThanOrEqualTo(2), "the way in and the way on");
            Assert.That(Placed(PlaceholderSound.DoorShuts), Is.EqualTo(doors), "one shut sound a door: " + Heard);
            Assert.That(Count(PlaceholderSound.DoorShuts), Is.EqualTo(doors), "heard among the placeholders too");
            int shutFrame = _placed.First(p => p.Sound == PlaceholderSound.DoorShuts).Frame;
            foreach (RoomDoor block in d.Blocks)
            {
                PlacedSoundRequest p = _placed.Single(r => r.Sound == PlaceholderSound.DoorShuts && Vector3.Distance(r.At, block.transform.position) < 0.01f);
                Assert.That(p.Frame, Is.EqualTo(shutFrame), "all in the frame they shut");
                Assert.That(p.Voice.transform.position, Is.EqualTo(block.transform.position), "its voice stands at its door");
                Assert.That(p.Voice.spatialBlend, Is.EqualTo(_sounds.Rooms.DoorPlaced).Within(1e-6f), "placed in the world");
                Assert.That(p.Voice.minDistance, Is.EqualTo(_sounds.Rooms.DoorFullWithin).Within(1e-4f));
                Assert.That(p.Clip, Is.SameAs(_sounds.ClipOf(PlaceholderSound.DoorShuts)));
                Assert.That(p.Recorded, Is.False);
            }
            Assert.That(_placed.Select(p => p.Voice).Distinct().Count(), Is.EqualTo(doors), "a voice each");
            Assert.That(_played.Count(r => r.Sound == PlaceholderSound.Strike && r.Frame == shutFrame), Is.Zero, "not the old heavy impact: " + Heard);
            Assert.That(Count(PlaceholderSound.DoorOpens), Is.Zero, "not open yet");

            // The room's fight woke as she crossed its line: one swell, not the boss's growl.
            Assert.That(Count(PlaceholderSound.RoomWakes), Is.EqualTo(1), "the room woke: " + Heard);
            Assert.That(Count(PlaceholderSound.BossAnger), Is.Zero, "not the growl");

            yield return GameTime(1f);
            Assert.That(Placed(PlaceholderSound.DoorShuts), Is.EqualTo(doors), "once, while they stand");

            // Cleared: the doors sink open, one open sound a door where it stands; the room lets go, once, not the way opening.
            Kill(own);
            yield return Until(() => !d.Shut, 30);
            Assert.That(d.Shut, Is.False, "cleared: open");
            yield return Frames(3);
            Assert.That(Placed(PlaceholderSound.DoorOpens), Is.EqualTo(doors), "one open sound a door: " + Heard);
            foreach (RoomDoor block in d.Blocks)
                Assert.That(_placed.Count(r => r.Sound == PlaceholderSound.DoorOpens && Vector3.Distance(r.At, block.transform.position) < 0.01f), Is.EqualTo(1));
            Assert.That(Count(PlaceholderSound.RoomLetsGo), Is.EqualTo(1), "the room lets go: " + Heard);
            Assert.That(Count(PlaceholderSound.WayOpens), Is.Zero, "not the way opening");
            yield return GameTime(1.5f);
            Assert.That(Placed(PlaceholderSound.DoorOpens), Is.EqualTo(doors), "once");
            Assert.That(Count(PlaceholderSound.RoomLetsGo), Is.EqualTo(1), "once");
            Assert.That(Count(PlaceholderSound.RoomWakes), Is.EqualTo(1));
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator TheRoomWakes_OnceARoom_NotAgainWhenItWakesAgain_ThoughItsDoorsShutAgain()
        {
            GeneratedLevel level = StartRun();
            yield return Frames(2);
            (LevelRoom room, List<Enemy> own) = FirstRoom(level);
            RoomFight fight = _levels.FightOf(room);
            _played.Clear();
            _placed.Clear();
            GeneratedLevels.RoomDoors d = null;
            yield return ShutIn(level, room, x => d = x);
            int doors = d.Blocks.Count;
            Assert.That(Count(PlaceholderSound.RoomWakes), Is.EqualTo(1));

            // She gets away (to the exit room, its own enemies switched off): the fight ends, the doors open.
            LevelRoom exit = level.Layout.ExitRoom;
            foreach (Enemy e in level.EnemiesByRoom[exit.Order]) e.gameObject.SetActive(false);
            _player.Teleport(level.Middle(exit));
            yield return Until(() => !fight.On && !d.Shut, 120);
            Assume.That(fight.On, Is.False, "she got away");
            Time.timeScale = 4f;
            yield return GameTime(6f); // they walk home
            Time.timeScale = 1f;

            // Back past the line: the fight wakes again and the doors shut again; the swell is not heard again.
            yield return ShutIn(level, room, x => d = x);
            Assume.That(fight.Starts, Is.EqualTo(2), "it woke again");
            // The exit room she stood in woke too (its enemies switched off are not dead): one swell for it, its own room.
            int roomsWoken = _levels.RoomFights.Count(f => f.Starts > 0);
            Assert.That(roomsWoken, Is.InRange(1, 2));
            Assert.That(Count(PlaceholderSound.RoomWakes), Is.EqualTo(roomsWoken), "once a room, not again for this one: " + Heard);
            Assert.That(Placed(PlaceholderSound.DoorShuts), Is.EqualTo(2 * doors), "the doors are heard each time they shut");
            Assert.That(Count(PlaceholderSound.BossAnger), Is.Zero, "never the growl");
        }

        // ---------------- The way down ----------------

        [UnityTest, Timeout(180000)]
        public IEnumerator TheStairsDown_AreHeardOnceAFloor_NotAtTheRunsStart()
        {
            StartRun();
            yield return Frames(3);
            Assert.That(Count(PlaceholderSound.WayDown), Is.Zero, "the run's first floor is no way down");
            yield return ToTheNextFloor();
            Assert.That(Count(PlaceholderSound.WayDown), Is.EqualTo(1), "floor 1 to 2: " + Heard);
            yield return new WaitForSecondsRealtime(1.1f); // past its cap of one a second
            yield return ToTheNextFloor();
            Assert.That(Count(PlaceholderSound.WayDown), Is.EqualTo(2), "floor 2 to 3");
            yield return Frames(5);
            Assert.That(Count(PlaceholderSound.WayDown), Is.EqualTo(2), "once a floor");

            // A new run from floor 1 is no way down.
            _levels.StartRun(RunSeed + 1);
            yield return Frames(3);
            Assert.That(Count(PlaceholderSound.WayDown), Is.EqualTo(2), "a new run");
        }

        // ---------------- The room tone a floor ----------------

        [UnityTest, Timeout(240000)]
        public IEnumerator TheRoomTone_IsTodaysOnFloorOne_AndEachFloorsRowBelow_ChangedOnceAsTheFloorIsBuilt()
        {
            RoomToneRow[] rows = _sounds.Rooms.Tones;
            AudioClip today = null;
            for (int floor = 1; floor <= rows.Length; floor++)
            {
                _levels.Values = new LevelValues();
                GeneratedLevel level = _levels.Build(FloorSeed, floor);
                yield return Frames(3);
                AudioSource tone = level.Mood.Tone;
                AudioClip clip = ToneOf(level);
                RoomToneRow row = RoomSoundRules.RowOf(rows, floor).Value;
                Assert.That(_sounds.ToneFloorGiven, Is.EqualTo(floor), "floor " + floor + " given its row");
                Assert.That(tone.isPlaying && tone.loop, "it loops, floor " + floor);
                Assert.That(tone.pitch, Is.EqualTo(1f), "made at its pitch: played as it is");
                Assert.That(tone.volume, Is.EqualTo(level.Values.Mood.ToneVolume * _sounds.Values.Master).Within(1e-4f), "at the mood's level: under everything");
                Assert.That(clip.length, Is.EqualTo(row.Beat).Within(0.01f), "its beat's length, floor " + floor);
                if (floor == 1)
                {
                    today = clip;
                    Assert.That(clip.name, Is.EqualTo("Mood_RoomTone"), "floor 1: today's own clip");
                    continue;
                }
                Assert.That(clip, Is.Not.SameAs(today), "floor " + floor + ": its own");
                var data = new float[clip.samples];
                clip.GetData(data, 0);
                Assert.That(data, Is.EqualTo(Synth.Render(RoomSoundRules.ToneOf(row))).Within(1e-3f), "its row's tone, floor " + floor);
                yield return Frames(3);
                Assert.That(level.Mood.Tone.clip, Is.SameAs(clip), "changed once: it stays");
            }
            Assert.That(_sounds.TonesMade, Is.EqualTo(rows.Length - 1), "made once a row, floor 1's not at all");

            // The same floor again: the tone kept, not made again.
            _levels.Build(FloorSeed, 3);
            yield return Frames(3);
            Assert.That(_sounds.TonesMade, Is.EqualTo(rows.Length - 1), "kept");

            // Off while on floor 3: today's back at once.
            _sounds.Rooms.On = false;
            yield return Frames(2);
            Assert.That(_levels.Level.Mood.Tone.clip, Is.SameAs(today), "today's back");
            Assert.That(_levels.Level.Mood.Tone.isPlaying, Is.True);
        }

        // ---------------- Replaced by his recordings ----------------

        [UnityTest, Timeout(180000)]
        public IEnumerator ARecordingUnderEachNewName_IsPlayedInPlaceOfTheCodeMadeOne()
        {
            Object.Destroy(_sounds.gameObject);
            yield return null;
            Assert.That(PlaceholderSounds.Current, Is.Null);
            var recorded = new Dictionary<string, AudioClip>();
            foreach (PlaceholderSound s in RoomSoundRules.Sounds) recorded[PlaceholderSoundRules.ResourcePath(s)] = MadeClip("Recorded" + s, 0.3f);
            recorded[RoomSoundRules.ToneResourcePath] = MadeClip("RecordedRoomTone", 2f);
            Assert.That(RoomSoundRules.ToneResourcePath, Is.EqualTo("Sounds/Placeholders/RoomTone"));
            var looked = new List<string>();
            PlaceholderSounds.RecordedClips = path =>
            {
                looked.Add(path);
                return recorded.TryGetValue(path, out AudioClip c) ? c : null;
            };
            _sounds = PlaceholderSounds.Ensure();
            yield return null;
            foreach (PlaceholderSound s in RoomSoundRules.Sounds)
            {
                Assert.That(looked, Does.Contain("Sounds/Placeholders/" + s), s + " looked for under its name");
                Assert.That(_sounds.IsRecorded(s), Is.True, s.ToString());
                Assert.That(_sounds.ClipOf(s), Is.SameAs(recorded[PlaceholderSoundRules.ResourcePath(s)]), s.ToString());
            }
            Assert.That(_sounds.IsRecorded(PlaceholderSound.Strike), Is.False, "the others stay code-made");

            // Played: the shut doors with the recording, where they stand.
            GeneratedLevel level = StartRun();
            yield return Frames(2);
            _placed.Clear();
            GeneratedLevels.RoomDoors d = null;
            yield return ShutIn(level, FirstRoom(level).room, x => d = x);
            Assert.That(_placed.Count(p => p.Sound == PlaceholderSound.DoorShuts), Is.EqualTo(d.Blocks.Count));
            Assert.That(_placed.Where(p => p.Sound == PlaceholderSound.DoorShuts).All(p => p.Recorded && p.Clip == recorded[PlaceholderSoundRules.ResourcePath(PlaceholderSound.DoorShuts)]), "the recording played");

            // The tone: the recording on floor 1 as it is, on floor 3 at its row's pitch.
            Assert.That(ToneOf(level), Is.SameAs(recorded[RoomSoundRules.ToneResourcePath]), "floor 1: the recording");
            Assert.That(level.Mood.Tone.pitch, Is.EqualTo(1f));
            Assert.That(_sounds.ToneRecorded, Is.True);
            GeneratedLevel three = _levels.Build(FloorSeed, 3);
            yield return Frames(3);
            Assert.That(ToneOf(three), Is.SameAs(recorded[RoomSoundRules.ToneResourcePath]), "floor 3: the recording");
            Assert.That(three.Mood.Tone.pitch, Is.EqualTo(RoomSoundRules.RowOf(_sounds.Rooms.Tones, 3).Value.Pitch).Within(1e-5f), "at its row's pitch");
            Assert.That(_sounds.TonesMade, Is.Zero, "none made in code");
            Assert.That(looked.Count(p => p == RoomSoundRules.ToneResourcePath), Is.EqualTo(1), "looked for once");
            Object.Destroy(_sounds.gameObject);
            _sounds = null;
            yield return null;
            Assert.That(recorded.Values.All(c => c != null), Is.True, "a recording is not destroyed with the sounds");
        }

        // ---------------- The switch off ----------------

        [UnityTest, Timeout(240000)]
        public IEnumerator TheSwitchOff_IsSilentForTheNewSounds_TheOldOnesAsBefore_AndTodaysToneOnEveryFloor()
        {
            _sounds.Rooms.On = false;
            GeneratedLevel level = StartRun();
            yield return Frames(2);
            (LevelRoom room, List<Enemy> own) = FirstRoom(level);
            _played.Clear();
            GeneratedLevels.RoomDoors d = null;
            yield return ShutIn(level, room, x => d = x);
            Assert.That(Count(PlaceholderSound.Strike), Is.GreaterThanOrEqualTo(1), "the doors' heavy impact, as before: " + Heard);
            Assert.That(Count(PlaceholderSound.BossAnger), Is.EqualTo(1), "the room's growl, as before");
            Kill(own);
            yield return Until(() => !d.Shut, 30);
            yield return Frames(3);
            Assert.That(Count(PlaceholderSound.WayOpens), Is.EqualTo(1), "the way opening, as before: " + Heard);
            AudioClip today = ToneOf(level);
            Assert.That(today.name, Is.EqualTo("Mood_RoomTone"));
            Assert.That(_sounds.ToneFloorGiven, Is.Zero);
            yield return ToTheNextFloor();
            yield return new WaitForSecondsRealtime(1.1f);
            yield return ToTheNextFloor();
            for (int floor = 4; floor <= 5; floor++)
            {
                GeneratedLevel built = _levels.Build(FloorSeed, floor);
                yield return Frames(3);
                Assert.That(ToneOf(built), Is.SameAs(today), "today's tone on floor " + floor);
                Assert.That(built.Mood.Tone.pitch, Is.EqualTo(1f));
            }
            Assert.That(ToneOf(_levels.Level), Is.SameAs(today));
            Assert.That(_placed, Is.Empty, "nothing placed at a door");
            foreach (PlaceholderSound s in RoomSoundRules.Sounds) Assert.That(Count(s), Is.Zero, s + " not heard: " + Heard);
            Assert.That(_sounds.TonesMade, Is.Zero, "no tone made");
        }

        // ---------------- Nothing allocated ----------------

        [UnityTest, Timeout(180000)]
        public IEnumerator NothingIsAllocated_AsADoorSoundPlays_NorInAFrameWhileNothingChanges()
        {
            Assert.That(() => { s_junk = new byte[64]; }, AllocIs.AllocatingGCMemory(), "the measure sees an allocation");
            GeneratedLevel level = StartRun();
            yield return Frames(2);
            GeneratedLevels.RoomDoors d = null;
            yield return ShutIn(level, FirstRoom(level).room, x => d = x);
            _sounds.Tick();
            int shutBefore = Placed(PlaceholderSound.DoorShuts);
            _played.Clear();
            Assert.That(() => { for (int i = 0; i < 300; i++) _sounds.Tick(); }, AllocExt.AllocatingGCMemory(Is.Not), "while they stand: nothing");
            Assert.That(_played.Where(r => RoomSoundRules.Sounds.Contains(r.Sound)), Is.Empty, "and none of the five asked for");

            // The look sees the doors shut once more (a count put up by hand): the shut sounds play, nothing allocated.
            Assert.That(() =>
            {
                d.Closings++;
                _sounds.Tick();
            }, AllocExt.AllocatingGCMemory(Is.Not), "a door's sound playing allocates nothing");
            Assert.That(Placed(PlaceholderSound.DoorShuts), Is.EqualTo(shutBefore + d.Blocks.Count), "and it played");
        }

        static byte[] s_junk;
    }
}
