using System;
using System.Collections.Generic;

namespace Roguelike.Rules
{
    /// <summary>
    /// One sorted table of what every prop DOES against a shot (work order 088, point 5; the draft room rule 13, "low is low and
    /// tall is tall"): low (she shoots over it, a shot of an enemy passes over it, and she can go down behind it: the duck), tall
    /// (it stops every shot, hers and theirs, standing; there is no duck at it), or neither (slender or walked over: it stops
    /// nothing and gives no duck). The greybox colours it by this (red tall, blue low, purple neither), the duck finds its low
    /// props by it and the shots stop at its tall ones: a floor puts the shot-stopping cover on a prop when this table says tall,
    /// and on nothing else (<see cref="PropValues.OneHeightTable"/>; off, the morning's rule: the cover height of
    /// <see cref="PropValues.CoverHeight"/> with the quick duck's sort on top).
    ///
    /// No model's size is changed here (that is look, for a look order). The table is the sort the quick duck has made since work
    /// order 043 (<see cref="PropSort.Of"/>, which a test holds equal to it, row by row): low up to <see cref="PropSort.LowMost"/>
    /// (1.1 m), tall from <see cref="PropSort.TallLeast"/> (1.8 m), and the four props between the two lines sorted one by one by
    /// what they look like from above. Those four are where the look and the rule disagree (<see cref="Disagrees"/>): their height
    /// says neither low nor tall. Not props, and not in the table: the kit's post (1.3 m) and its low near wall (0.95 m, 1 m in the
    /// grey boxes) stop every shot, as every wall does, at heights a prop would be low or between.
    /// </summary>
    public static class PropHeights
    {
        /// <summary>One row: the model, its height in metres (its table entry's, <see cref="PropSize.Crypt"/>), what it does, and why, in words.</summary>
        public readonly struct Row
        {
            public readonly PropModel Model;
            public readonly double Height;
            public readonly PropHeightClass Sort;
            public readonly string Why;

            public Row(PropModel model, PropHeightClass sort, string why)
            {
                Model = model;
                Height = PropSize.Of(model).Height;
                Sort = sort;
                Why = why;
            }
        }

        const string ByHeightLow = "low by its height (1.1 m at most)";
        const string ByHeightTall = "tall by its height (1.8 m at least)";

        /// <summary>
        /// Every prop model, lowest first (the order of the table is its height, then the model's number). The walked-over two are
        /// neither, whatever their height; the candelabra is neither (slender).
        /// </summary>
        public static readonly IReadOnlyList<Row> Table = new[]
        {
            new Row(PropModel.Dais, PropHeightClass.Slender, "walked over (0.15 m of it shows): stops nothing"),
            new Row(PropModel.BonePile, PropHeightClass.Low, ByHeightLow),
            new Row(PropModel.CoinHeap, PropHeightClass.Low, ByHeightLow),
            new Row(PropModel.Rubble, PropHeightClass.Low, ByHeightLow),
            new Row(PropModel.SkullPile, PropHeightClass.Low, ByHeightLow),
            new Row(PropModel.Chest, PropHeightClass.Low, ByHeightLow),
            new Row(PropModel.Sarcophagus, PropHeightClass.Low, ByHeightLow),
            new Row(PropModel.Coffin, PropHeightClass.Low, ByHeightLow),
            new Row(PropModel.CandleCluster, PropHeightClass.Low, ByHeightLow),
            new Row(PropModel.Basin, PropHeightClass.Low, ByHeightLow),
            new Row(PropModel.Brazier, PropHeightClass.Low, ByHeightLow),
            new Row(PropModel.Urns, PropHeightClass.Low, ByHeightLow),
            new Row(PropModel.PillarBroken, PropHeightClass.Low, "between the lines (1.2 m): low by its look from above, a wide plinth with a thin stub"),
            new Row(PropModel.Altar, PropHeightClass.Low, "between the lines (1.3 m): low by its look from above, a pedestal block"),
            new Row(PropModel.GraveMarker, PropHeightClass.Low, "between the lines (1.3 m): low by its look from above, a headstone"),
            new Row(PropModel.GreatBrazier, PropHeightClass.Tall, "between the lines (1.5 m): tall by its look from above, a bowl as wide as its footprint"),
            new Row(PropModel.Candelabra, PropHeightClass.Slender, "slender (1.8 m, a thin stem): stops nothing"),
            new Row(PropModel.GreatAltar, PropHeightClass.Tall, ByHeightTall),
            new Row(PropModel.Cage, PropHeightClass.Tall, ByHeightTall),
            new Row(PropModel.Bookshelf, PropHeightClass.Tall, ByHeightTall),
            new Row(PropModel.Banner, PropHeightClass.Slender, "hangs on its wall, walked under: stops nothing"),
            new Row(PropModel.BoneWall, PropHeightClass.Tall, ByHeightTall),
            new Row(PropModel.Statue, PropHeightClass.Tall, ByHeightTall),
            new Row(PropModel.BoneThrone, PropHeightClass.Tall, ByHeightTall),
            new Row(PropModel.Pillar, PropHeightClass.Tall, ByHeightTall),
            new Row(PropModel.KnightStatue, PropHeightClass.Tall, ByHeightTall),
        };

        static readonly PropHeightClass[] s_sort = Build();

        static PropHeightClass[] Build()
        {
            var sort = new PropHeightClass[PropSize.Crypt.Length];
            var seen = new bool[sort.Length];
            foreach (Row r in Table)
            {
                sort[(int)r.Model] = r.Sort;
                seen[(int)r.Model] = true;
            }
            for (int i = 0; i < sort.Length; i++)
                if (!seen[i]) sort[i] = PropSort.Of((PropModel)i); // a model added later and not in the table: the duck's sort
            return sort;
        }

        /// <summary>What the model does (see the class).</summary>
        public static PropHeightClass Of(PropModel model) => (int)model >= 0 && (int)model < s_sort.Length ? s_sort[(int)model] : PropSort.Of(model);

        /// <summary>It stops every shot, standing: tall.</summary>
        public static bool Tall(PropModel model) => Of(model) == PropHeightClass.Tall;

        /// <summary>She shoots over it and can go down behind it: low.</summary>
        public static bool Low(PropModel model) => Of(model) == PropHeightClass.Low;

        /// <summary>
        /// Its look and its rule disagree: its height is between the low line and the tall line (1.1 to 1.8 m), so from 23 m up
        /// nobody can tell by its height what it does (the walked-over and the slender ones are not counted).
        /// </summary>
        public static bool Disagrees(PropModel model)
        {
            PropSize s = PropSize.Of(model);
            if (s.Slender || s.Walkable) return false;
            return s.Height > PropSort.LowMost + 1e-3 && s.Height < PropSort.TallLeast - 1e-3;
        }

        /// <summary>The rows whose look and rule disagree, for the producer's look order (work order 088).</summary>
        public static List<Row> Disagreeing()
        {
            var list = new List<Row>();
            foreach (Row r in Table)
                if (Disagrees(r.Model)) list.Add(r);
            return list;
        }

        /// <summary>
        /// Does this prop of a floor stop shots, by the values: with <see cref="PropValues.OneHeightTable"/> on, the table's tall
        /// (and never a slender one); off, the morning's rule, the cover height (<see cref="LevelProp.IsCover"/>), on which the quick
        /// duck's sort then lays its own (<see cref="PropSort.LowerUnderTheDuck"/>).
        /// </summary>
        public static bool StopsShots(LevelProp prop, PropValues v)
        {
            if (prop == null) return false;
            v = v ?? new PropValues();
            return v.OneHeightTable ? !prop.Slender && Tall(prop.Model) : prop.IsCover(v);
        }
    }
}
