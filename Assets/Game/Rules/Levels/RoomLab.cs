using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace Roguelike.Rules
{
    /// <summary>The kind of the one room the room lab builds (work order 084): an ordinary combat room, the elite room, a treasure room, the exit room.</summary>
    public enum LabKind { Ordinary, Elite, Treasure, Exit }

    /// <summary>
    /// An enemy mix of the room lab (work order 084): the floor whose numbers the room's enemies are drawn by, as a room of
    /// its kind on that floor draws them (<see cref="LevelGenerator.DrawGroups"/>, the elite room's elite of
    /// <see cref="LevelValues.EliteKinds"/>, the act boss in the exit room of the act's last floor), and whether bombers
    /// (work order 073) take the place of swarmers as the floors would with them on (<see cref="LevelBombers"/>).
    /// </summary>
    public sealed class LabMix
    {
        public readonly string Name;
        public readonly int Floor;
        public readonly bool Bombers;

        public LabMix(string name, int floor, bool bombers)
        {
            Name = name;
            Floor = floor;
            Bombers = bombers;
        }

        public override string ToString() => Name;
    }

    /// <summary>One room the lab can build: a template of the library for the room it is for, or the generator's drawn shapes for a kind (no template).</summary>
    public sealed class LabEntry
    {
        public readonly RoomTemplate Template;
        public readonly LabKind Kind;
        /// <summary>
        /// The floors of a run may hold this room with the values it was listed by: false for a template of work order 072's
        /// second set while <see cref="LevelValues.MoreTemplates"/> is off (since 2026-11-22: in the code, not in the floors,
        /// until he has played its rooms in the lab). Always true without a template.
        /// </summary>
        public readonly bool InFloors;

        public LabEntry(RoomTemplate template, LabKind kind, bool inFloors = true)
        {
            Template = template;
            Kind = kind;
            InFloors = template == null || inFloors;
        }

        /// <summary>The template's name, or what the generator draws without one.</summary>
        public string Name => Template != null ? Template.Name : Kind == LabKind.Treasure ? "treasure room" : "drawn shape";

        /// <summary>Its name for the picker and the developer's box: <see cref="RoomLab.NotInTheFloors"/> after a room the floors do not hold yet.</summary>
        public string Label => InFloors ? Name : Name + " " + RoomLab.NotInTheFloors;

        public override string ToString() => Label + " (" + RoomLab.Word(Kind) + ")";
    }

    /// <summary>
    /// The room lab's rules (work order 084; Kristian ticked "Gråboks-bryter og romlab" on 2026-11-22: "play your level a lot,
    /// from the first pass, after every change", the talk's iteration kept short): ONE room built alone, through the
    /// generator's own stages (<see cref="LevelGenerator.LabRoom"/>), with a short straight passage on its door in where she
    /// starts. What it may build (<see cref="Entries"/>), its enemy mixes (<see cref="Mixes"/>), what it draws for them
    /// (<see cref="Groups"/>), and its check (<see cref="Faults"/>): the floor's rules that one room keeps.
    ///
    /// Where a lab room differs from the same room met in a floor: it has no neighbours (no room before its passage, no door
    /// out: an ordinary or elite template's O marks are wall); its one passage is straight and a dead end behind her, and she
    /// starts in it rather than in an entrance; its door in is always in its south wall (the template turned to come in from
    /// the south, so the camera, which looks north, looks into the room); it is the floor's only room, so no template, theme
    /// or shape is kept from repeating the room before it, and the elite or exit room is not the row's middle or end; its seed
    /// draws its mirror, passage, door, carve, enemies, theme and props on the generator's streams, but no floor of the game
    /// has that seed's room. Everything inside the room is the floor's: the template's stamp and its ? cells, the shape drawn
    /// without one, the enemies by the floor's numbers placed by the generator's rules, the theme and the props by their
    /// rules, the kit, the mood, the cave, the door signs and the fight's point as the floor's level builder makes them.
    /// </summary>
    public static class RoomLab
    {
        /// <summary>
        /// The enemy mixes (the order's presets, the floors' own numbers): floor 1's, floor 3's, floor 5's (with the elite room:
        /// "floor 5 elite"; with the exit room the act boss and its room), and floor 3's with its bombers (work order 073 is in
        /// this branch: the most a room of floor 3 holds, 1, or 2 in the exit room).
        /// </summary>
        public static readonly LabMix[] Mixes =
        {
            new LabMix("floor 1", 1, false),
            new LabMix("floor 3", 3, false),
            new LabMix("floor 5", 5, false),
            new LabMix("floor 3 with bombers", 3, true),
        };

        public static string Word(LabKind kind) =>
            kind == LabKind.Elite ? "elite" : kind == LabKind.Treasure ? "treasure" : kind == LabKind.Exit ? "exit" : "ordinary";

        /// <summary>The room's kind in the layout: the elite room is a combat room marked elite.</summary>
        public static RoomKind KindOf(LabKind kind) =>
            kind == LabKind.Treasure ? RoomKind.Treasure : kind == LabKind.Exit ? RoomKind.Exit : RoomKind.Combat;

        public static bool EliteOf(LabKind kind) => kind == LabKind.Elite;

        /// <summary>The lab's kind of a template's room.</summary>
        public static LabKind KindFor(RoomTemplateFor f) => f == RoomTemplateFor.Elite ? LabKind.Elite : f == RoomTemplateFor.Exit ? LabKind.Exit : LabKind.Ordinary;

        /// <summary>The words after a room the floors of a run do not hold yet (work order 072's second set with <see cref="LevelValues.MoreTemplates"/> off).</summary>
        public const string NotInTheFloors = "(not in the floors yet)";

        /// <summary>
        /// The values the lab lists its templates by: a copy of the floors' (<paramref name="v"/>, field for field, its parts
        /// shared and never changed) with <see cref="LevelValues.MoreTemplates"/> on, so the lab offers EVERY template of the
        /// library whatever the floors say: playing the second set's rooms in the lab is how they come into the floors (his rule
        /// of 2026-11-22). The floors' values are never changed.
        /// </summary>
        public static LevelValues LabValues(LevelValues v)
        {
            v = v ?? new LevelValues();
            if (v.MoreTemplates) return v;
            var copy = new LevelValues();
            foreach (FieldInfo f in typeof(LevelValues).GetFields(BindingFlags.Public | BindingFlags.Instance))
                if (!f.IsInitOnly && !f.IsLiteral) f.SetValue(copy, f.GetValue(v));
            copy.MoreTemplates = true;
            return copy;
        }

        /// <summary>
        /// Every room the lab can build, in order: every template of the library the generator could draw (its check of
        /// <see cref="RoomTemplateLibrary.Usable"/> passed, with <see cref="LabValues"/>: the first eleven and work order 072's
        /// second set, whatever <see cref="LevelValues.MoreTemplates"/> says in the floors), in the library's order, each of the
        /// room it is for and marked when the floors with <paramref name="v"/> do not hold it (<see cref="LabEntry.InFloors"/>);
        /// then the generator's drawn shapes (no template: the carve of work order 051, its shape drawn by the seed) for an
        /// ordinary, the elite and the exit room; then the treasure room (no template is for one).
        /// </summary>
        public static List<LabEntry> Entries(LevelValues v)
        {
            v = v ?? new LevelValues();
            var list = new List<LabEntry>();
            foreach (RoomTemplate t in RoomTemplateLibrary.Usable(LabValues(v)))
                list.Add(new LabEntry(t, KindFor(t.For), InTheFloors(t, v)));
            list.Add(new LabEntry(null, LabKind.Ordinary));
            list.Add(new LabEntry(null, LabKind.Elite));
            list.Add(new LabEntry(null, LabKind.Exit));
            list.Add(new LabEntry(null, LabKind.Treasure));
            return list;
        }

        /// <summary>Do the floors made with these values hold this template? Not one of work order 072's second set while <see cref="LevelValues.MoreTemplates"/> is off.</summary>
        public static bool InTheFloors(RoomTemplate t, LevelValues v) =>
            t == null || t.Since < RoomTemplateLibrary.MoreSince || (v ?? new LevelValues()).MoreTemplates;

        /// <summary>The entries of one kind, in the order of <see cref="Entries"/>.</summary>
        public static List<LabEntry> EntriesOf(LabKind kind, LevelValues v) => Entries(v).Where(e => e.Kind == kind).ToList();

        /// <summary>
        /// The most bombers a room of this kind holds on the mix's floor with the floors' bombers on (<see cref="LevelBombers.Most"/>
        /// with the switch on, never the floor's first combat room): 0 for a mix without bombers.
        /// </summary>
        public static int BombersIn(LabKind kind, LabMix mix, LevelValues v)
        {
            if (mix == null || !mix.Bombers) return 0;
            RoomKind k = KindOf(kind);
            BomberFloorValues b = v?.BomberFloors ?? new BomberFloorValues();
            if (k != RoomKind.Combat && k != RoomKind.Exit) return 0;
            if (mix.Floor < b.FromFloor || b.MostInRoom == null || b.MostInRoom.Length == 0) return 0;
            int room = Math.Max(0, b.MostInRoom[Math.Max(0, Math.Min(b.MostInRoom.Length - 1, mix.Floor - 1))]);
            return k == RoomKind.Exit && room > 0 ? room + Math.Max(0, b.MoreInExit) : room;
        }

        /// <summary>
        /// The room's enemies as the floors draw them (see <see cref="LabMix"/>), from this stream: a room of its kind on the
        /// mix's floor (<see cref="LevelGenerator.DrawGroups"/>), the elite room's elite after them (as the floors' contents add
        /// it), and with bombers as many as <see cref="BombersIn"/> in place of swarmers (<see cref="LevelBombers.Convert"/>).
        /// None in a treasure room.
        /// </summary>
        public static List<EnemyGroup> Groups(LabKind kind, LabMix mix, LevelRandom rng, LevelValues v)
        {
            v = v ?? new LevelValues();
            RoomKind k = KindOf(kind);
            if (k == RoomKind.Treasure || mix == null) return new List<EnemyGroup>();
            List<EnemyGroup> groups = LevelGenerator.DrawGroups(k, mix.Floor, rng, v);
            if (EliteOf(kind))
            {
                EnemyKind[] kinds = v.EliteKinds(mix.Floor);
                groups.Add(new EnemyGroup(kinds[rng.Below(kinds.Length)], 1, elite: true));
            }
            int bombers = BombersIn(kind, mix, v);
            if (bombers > 0) LevelBombers.Convert(groups, bombers);
            return groups;
        }

        /// <summary>The lab room of an entry, a mix and a seed, built by the generator's stages (<see cref="LevelGenerator.LabRoom"/>).</summary>
        public static LevelLayout Build(LabEntry entry, LabMix mix, ulong seed, LevelValues v) =>
            LevelGenerator.LabRoom(entry.Template, entry.Kind, mix, seed, v ?? new LevelValues());

        /// <summary>
        /// Every rule a lab room breaks of those a floor's room keeps (the floor's validator's rules that one room alone can keep),
        /// added up in words; empty means it is a room of a floor. The room: its carved outline (<see cref="RoomShapes.Faults"/>),
        /// its template (<see cref="RoomTemplates.Check"/>: size, kind, door in beside an I, stamp) and its stamp not fallen back;
        /// its enemies (the validator's rules for a room's enemies, by the mix's floor) and its bombers (no more than
        /// <see cref="BombersIn"/>, each alone); its theme one its kind may have; its props (models, how many, every rule of
        /// <see cref="PropRules"/>, the share walkable, the clear lane). The passage: one cell wide, straight, as deep as a
        /// passage, holding nothing, its door on the room's wall (<see cref="Passages.DoorFault"/>); her start on its open floor.
        /// </summary>
        public static List<string> Faults(LevelLayout level, LabMix mix, LevelValues v)
        {
            v = v ?? new LevelValues();
            var faults = new List<string>();
            if (level == null || level.Rooms.Count != 2 || level.Doors.Count != 1)
            {
                faults.Add("a lab room is a passage and a room joined by one door");
                return faults;
            }
            LevelRoom passage = level.Rooms[0], room = level.Rooms[1];
            string Name(LevelRoom r) => r.Letter + r.Order.ToString(CultureInfo.InvariantCulture);
            if (passage.Kind != RoomKind.Passage) faults.Add("its first room is not a passage");

            // The room's outline and its template.
            var broken = new List<string>();
            RoomShapes.Faults(level, room, v, broken);
            foreach (string f in broken) faults.Add(Name(room) + " " + f);
            RoomTemplates.Check(level, faults);
            if (room.Template != null && room.Shape != RoomShape.Template) faults.Add(Name(room) + "'s template " + room.Template.Template.Name + " fell back to a drawn shape: its stamp broke a rule");

            // The passage, and her start in it.
            PassageValues pv = v.PassageRules ?? new PassageValues();
            int depth = Math.Max(passage.Rect.Width, passage.Rect.Height), across = Math.Min(passage.Rect.Width, passage.Rect.Height);
            if (across != v.DoorWidth) faults.Add($"{Name(passage)} is {across} cells across, and a passage as wide as a door ({v.DoorWidth})");
            if (depth < pv.Fewest || depth > Math.Min(pv.Most, pv.StraightMost)) faults.Add($"{Name(passage)} is {depth} cells deep, and the lab's passage {pv.Fewest} to {Math.Min(pv.Most, pv.StraightMost)}, straight");
            if (passage.SolidCount > 0) faults.Add($"{Name(passage)} has solid cells: a straight passage has none");
            if (passage.EnemyPoints.Count > 0 || passage.Props.Count > 0) faults.Add($"{Name(passage)} holds something, and a passage nothing");
            string door = Passages.DoorFault(level, level.Doors[0]);
            if (door != null) faults.Add("its door " + door);
            if (!passage.PlayerStart.HasValue || level.RoomAt(passage.PlayerStart.Value.X, passage.PlayerStart.Value.Y) != passage)
                faults.Add("her start is not on the passage's floor");

            // The enemies, by the mix's floor, and the bombers.
            LevelValidator.CheckEnemies(level, room, v, faults, Name(room));
            int bombers = room.EnemyGroups.Where(g => g != null && g.Kind == EnemyKind.Bomber).Sum(g => g.Count);
            int most = BombersIn(KindFor(room), mix, v);
            if (bombers > most) faults.Add($"{Name(room)} holds {bombers} bombers, and its mix at most {most}");
            if (room.EnemyGroups.Any(g => g != null && g.Kind == EnemyKind.Bomber && (g.Count != 1 || g.Elite || g.Boss))) faults.Add($"{Name(room)} has a bomber not alone");

            // The theme and the props, as the validator holds a floor's room to them.
            if (room.Theme != RoomTheme.None && (Array.IndexOf(RoomThemes.ThemesFor(room.Kind), room.Theme) < 0 || v.ThemeRules?.Of(room.Theme, room.Kind) == null))
                faults.Add($"{Name(room)} is a {RoomThemes.Word(room.Theme)}, which a {room.Kind} room is never");
            PropValues prv = v.PropRules;
            PropModel[] allowed = PropRules.ModelsFor(room, v);
            if (room.Props.Count > PropRules.MostIn(room, v)) faults.Add($"{Name(room)} has {room.Props.Count} props, at most {PropRules.MostIn(room, v)}");
            List<PropRules.WallSpan> niches = null;
            if (room.Props.Any(p => PropSize.Of(p.Model).WallPiece))
            {
                CryptKitPlan kit = PropRules.KitPlanForProps(level);
                niches = PropRules.NichesOf(level, room, kit);
                niches.AddRange(PropRules.CarvedNichesOf(level, room, kit));
            }
            for (int i = 0; i < room.Props.Count; i++)
            {
                LevelProp p = room.Props[i];
                string label = $"{Name(room)}'s prop {i} ({p.Model} at {p.Position})";
                if (Array.IndexOf(allowed, p.Model) < 0) faults.Add($"{label} does not belong in it");
                var pf = new List<string>();
                PropRules.Fault(level, room, p, room.Props, prv, pf, niches);
                foreach (string f in pf) faults.Add(label + " " + f);
            }
            if (room.Kind == RoomKind.Combat && room.Props.Count > 0 && PropRules.WalkableShare(level, room, prv) < prv.MinWalkable - 1e-9)
                faults.Add($"{Name(room)} keeps less than {prv.MinWalkable:P0} of its floor walkable");
            var lane = new PropRules.Lane(level, room, prv);
            foreach (LevelProp p in room.Props) lane.Block(p.Footprint);
            if (!lane.Clear()) faults.Add($"{Name(room)} has no clear lane {prv.LaneWidth} m wide from its door to its middle");
            return faults;
        }

        static LabKind KindFor(LevelRoom room) => room.Elite ? LabKind.Elite : room.Kind == RoomKind.Treasure ? LabKind.Treasure : room.Kind == RoomKind.Exit ? LabKind.Exit : LabKind.Ordinary;

        /// <summary>A lab room in one line, for the recorder's log and the developer's box: "nave (ordinary), floor 3, seed ABCD1234".</summary>
        public static string Describe(LabEntry entry, LabMix mix, ulong seed) =>
            entry + ", " + (mix != null ? mix.Name : "no mix") + ", seed " + LevelSeed.ToText(seed);
    }
}
