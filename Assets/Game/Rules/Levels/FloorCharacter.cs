using System;

namespace Roguelike.Rules
{
    /// <summary>
    /// A template's first floor moved by the floors' character (work order 104): the template of this name is drawn from this floor
    /// on, in place of its own <see cref="RoomTemplate.FromFloor"/>. The template itself is not changed.
    /// </summary>
    [Serializable]
    public sealed class TemplateFloor
    {
        public string Name = "";
        public int Floor = 1;

        public TemplateFloor() { }

        public TemplateFloor(string name, int floor)
        {
            Name = name;
            Floor = floor;
        }
    }

    /// <summary>
    /// What makes each floor of the act a place of its own (work order 104; Kristian ticked "Etasjene får hver sin ting" on
    /// 2026-11-24: "én ny ting å lære per etasje, en dominerende fiendemiks og tema per etasje"; proposals 1 and 2 of the content
    /// plan). Data, all the producer's first guesses, Kristian's to change. Read only with <see cref="LevelValues.FloorCharacter"/>
    /// on; every list is by floor number from 1, past its end its last. The counts of enemies a room and what they deal are not
    /// here and are not changed (<see cref="LevelValues.Floors"/>, <see cref="LevelValues.FloorDamageScale"/>).
    /// </summary>
    [Serializable]
    public sealed class FloorCharacterValues
    {
        // The floors' names (the producer's start, his to change), shown under the floor's number in the run box and on the end
        // screen. Floor 4's leaves room for work order 108's lancer, which comes in there beside the bomber.
        public string[] Names = { "The Upper Tombs", "The Guard Halls", "The Shrine of Wards", "The Ossuary", "The Knight's Hall" };

        // The mix of each floor that shows its new thing (by its name in LevelValues.DefaultFloors), drawn DominantMixWeight times as
        // often as each of the floor's other two: floor 1 the swarm (all three first kinds), floor 2 the guarded swarm (the guard),
        // floor 3 the crossfire (its caster without a guard; the warder comes into any room of three or more), floor 4 the crossfire
        // (the most shooters, so the shooter's fan; its swarmers leave the bomber its places), floor 5 the guarded (no mix holds the
        // boss: the knight's guard). A name no mix of the floor has weights none.
        public string[] DominantMixes = { "swarm", "guarded swarm", "crossfire", "crossfire", "guarded" };
        public float DominantMixWeight = 2f;      // work order 104: "weight the one that shows the floor's new thing twice"

        // The room theme of each floor, drawn DominantThemeWeight times as likely for a combat room as by its recipe alone (the
        // producer's start: 1 tomb hall, 2 archive, 3 shrine, 4 ossuary, 5 tomb hall). The exit room's and the treasure rooms' are
        // drawn as before.
        public RoomTheme[] DominantThemes = { RoomTheme.TombHall, RoomTheme.Archive, RoomTheme.Shrine, RoomTheme.Ossuary, RoomTheme.TombHall };
        public float DominantThemeWeight = 3f;    // work order 104: "one room theme weighted three times"

        // A kind's elite form not on the kind's own floor (work order 104: "a kind's elite form does not arrive on the same floor as
        // the kind where that can be helped"): the elite guard this many floors after the guard's first floor (LevelValues.
        // GuardsFromFloor, 2: from floor 3), the elite caster as many after the caster's (CastersFromFloor, 3: from floor 4), so
        // moving a kind's floor moves its elite's with it. The elite brute stays on floor 1, where every kind is new and the elite
        // room must hold one of them; the elite shooter from LevelValues.EliteKindsFromFloor (2), a floor after the shooter, as before.
        public int EliteFloorsAfterItsKind = 1;

        // No more than three new combat templates a floor (work order 104: "move first floors, change no template"): of the first set,
        // five came on floor 1 and two on floor 2. The corner-long (it suits the archive) moves to floor 2 and the nave-apse (it suits
        // the shrine) to floor 3: floor 1 the nave, the corner and the colonnade; floor 2 the twin, the transept and the corner-long;
        // floor 3 the nave-apse beside the cross-hall elite room. The second set (LevelValues.MoreTemplates, off) is not moved.
        public TemplateFloor[] TemplateFirstFloors = { new TemplateFloor("corner-long", 2), new TemplateFloor("nave-apse", 3) };
    }

    /// <summary>
    /// The rules of the floors' character (work order 104), read with <see cref="LevelValues.FloorCharacter"/> on and nothing here
    /// with it off: a floor's name (<see cref="NameOf"/>), its mixes' draw (<see cref="PickMix"/>), its themes' weights
    /// (<see cref="ThemeWeight"/>), the elite kinds (<see cref="EliteKinds"/>) and the templates' first floors (<see cref="FirstFloorOf"/>).
    /// </summary>
    public static class FloorCharacterRules
    {
        static T Of<T>(T[] list, int floor, T none) =>
            list == null || list.Length == 0 ? none : list[Math.Max(0, Math.Min(list.Length - 1, floor - 1))];

        static double Unit(LevelRandom rng) => (rng.NextBits() >> 11) * (1.0 / (1UL << 53));

        static bool On(LevelValues v) => v != null && v.FloorCharacter && v.Character != null;

        /// <summary>The floor's name; "" with the switch off.</summary>
        public static string NameOf(int floor, LevelValues v) => On(v) ? Of(v.Character.Names, floor, "") ?? "" : "";

        /// <summary>The floor's dominant mix's name; "" with the switch off.</summary>
        public static string DominantMix(int floor, LevelValues v) => On(v) ? Of(v.Character.DominantMixes, floor, "") ?? "" : "";

        /// <summary>The floor's dominant theme; <see cref="RoomTheme.None"/> with the switch off.</summary>
        public static RoomTheme DominantTheme(int floor, LevelValues v) => On(v) ? Of(v.Character.DominantThemes, floor, RoomTheme.None) : RoomTheme.None;

        /// <summary>A mix's weight in its floor's draw: <see cref="FloorCharacterValues.DominantMixWeight"/> for the floor's dominant one, 1 for the others.</summary>
        public static double MixWeight(EnemyMix mix, int floor, LevelValues v) =>
            mix != null && On(v) && mix.Name == DominantMix(floor, v) ? Math.Max(0f, v.Character.DominantMixWeight) : 1d;

        /// <summary>
        /// The mix a room of this floor holds, drawn from <paramref name="mixes"/>: with the switch off one of them evenly
        /// (<see cref="LevelRandom.Below"/>, as the generator always drew it); on, by <see cref="MixWeight"/>, from one number.
        /// </summary>
        public static EnemyMix PickMix(EnemyMix[] mixes, int floor, LevelRandom rng, LevelValues v)
        {
            if (!On(v)) return mixes[rng.Below(mixes.Length)];
            double total = 0;
            foreach (EnemyMix m in mixes) total += MixWeight(m, floor, v);
            double pick = Unit(rng) * total;
            foreach (EnemyMix m in mixes)
            {
                double w = MixWeight(m, floor, v);
                if (pick < w) return m;
                pick -= w;
            }
            return mixes[mixes.Length - 1];
        }

        /// <summary>A combat theme's weight factor on this floor: <see cref="FloorCharacterValues.DominantThemeWeight"/> for the floor's theme, 1 for the others and with the switch off.</summary>
        public static double ThemeWeight(RoomTheme theme, int floor, LevelValues v) =>
            On(v) && theme != RoomTheme.None && theme == DominantTheme(floor, v) ? Math.Max(0f, v.Character.DominantThemeWeight) : 1d;

        /// <summary>
        /// The kinds an elite may be on this floor with the switch on: a brute on floor 1; from <see cref="LevelValues.EliteKindsFromFloor"/>
        /// a shooter too; a guard <see cref="FloorCharacterValues.EliteFloorsAfterItsKind"/> floors after <see cref="LevelValues.GuardsFromFloor"/>;
        /// a caster as many after the casters' own floor (<see cref="LevelValues.CastersFromFloor"/> with the act's plan on), while they are on.
        /// </summary>
        public static EnemyKind[] EliteKinds(int floor, LevelValues v)
        {
            int after = Math.Max(0, v.Character.EliteFloorsAfterItsKind);
            bool shooter = floor >= v.EliteKindsFromFloor, guard = floor >= Math.Max(v.EliteKindsFromFloor, v.GuardsFromFloor + after);
            bool caster = v.Casters && floor >= Math.Max(v.EliteKindsFromFloor, (v.ActPlan ? v.CastersFromFloor + after : v.EliteCasterFromFloor));
            var kinds = new System.Collections.Generic.List<EnemyKind> { EnemyKind.Brute };
            if (shooter) kinds.Add(EnemyKind.Shooter);
            if (guard) kinds.Add(EnemyKind.Guard);
            if (caster) kinds.Add(EnemyKind.Caster);
            return kinds.ToArray();
        }

        /// <summary>The first floor a template is drawn on: its own, or with the switch on the one <see cref="FloorCharacterValues.TemplateFirstFloors"/> gives it.</summary>
        public static int FirstFloorOf(RoomTemplate t, LevelValues v)
        {
            if (t == null) return int.MaxValue;
            if (On(v) && v.Character.TemplateFirstFloors != null)
                foreach (TemplateFloor f in v.Character.TemplateFirstFloors)
                    if (f != null && f.Name == t.Name) return f.Floor;
            return t.FromFloor;
        }
    }
}
