using FrontierLedger.Simulation;
using UnityEngine;

namespace FrontierLedger.Game.Art
{
    public static partial class InteriorArt
    {
        /// <summary>A large house: papered walls, the parents' bed and two more for the children, a cot,
        /// a kitchen stove, a dining table and a parlour corner with an armchair and a bookcase.</summary>
        static void LargeHouse(PixelCanvas g, Interior it, int L)
        {
            int y0 = L;
            var r = new RectInt(2, y0 - 10, 31, 54);
            Room(g, it, r, 14, 6, 10, "#e4d8b8", "#c4b490", "#6e4428");
            Win(g, 6, y0 - 8, 5, 4); Win(g, 24, y0 - 8, 5, 4);
            Shelf(g, 14, y0 - 7, 8, new[] { "#e8dcc4", "#c87a3a", "#7fa6b8", "#e84a3a", "#3f6a3a" }, 5);
            // bedroom end
            Bed(g, it, 4, y0 + 2, 9, 8, "#7a3428", 2);                    // the parents' bed
            Bed(g, it, 23, y0 + 2, 8, 7, "#3f6a9a", 2);
            Stove(g, it, 16, y0 + 3, y0 - 9);
            // the dining table
            Rug(g, 9, y0 + 14, 17, 10, "#7a3428", "#c8a050");
            Table(g, 12, y0 + 17, 11, 4);
            Chair(g, 10, y0 + 18); Chair(g, 23, y0 + 18); Chair(g, 14, y0 + 23); Chair(g, 19, y0 + 23);
            // the children's corner and the parlour
            Bed(g, it, 4, y0 + 30, 8, 7, "#4a7a4a", 2);
            Bed(g, it, 25, y0 + 31, 5, 6, "#c8a050", 1);                  // a cot
            g.R(23, y0 + 24, 8, 1, "#6e4428");
            Shelf(g, 24, y0 + 21, 6, new[] { "#8a2e22", "#2f5d7a", "#c8a050" }, 7);
            g.R(15, y0 + 32, 6, 4, "#7a3a4a"); g.R(15, y0 + 32, 6, 1, "#9a4a5a"); g.R(15, y0 + 30, 6, 2, "#5a2a3a");   // armchair
            it.Work.Add(new Spot(18, y0 + 11, PeopleArt.Up));             // at the stove
            it.Seat.Add(new Spot(14, y0 + 24, PeopleArt.Up)); it.Seat.Add(new Spot(19, y0 + 24, PeopleArt.Up));
            it.Seat.Add(new Spot(16, y0 + 16, PeopleArt.Down)); it.Seat.Add(new Spot(11, y0 + 20, PeopleArt.Right));
            it.Seat.Add(new Spot(17, y0 + 36, PeopleArt.Up));
            it.Visit.Add(new Spot(15, y0 + 40, PeopleArt.Up)); it.Visit.Add(new Spot(20, y0 + 41, PeopleArt.Left));
        }

        /// <summary>A manor: a marble fireplace under a portrait, bookcases, a long dining table, a piano, a settee,
        /// and four good beds.</summary>
        static void Manor(PixelCanvas g, Interior it, int L)
        {
            int y0 = L;
            var r = new RectInt(5, y0 - 14, 54, 48);
            Room(g, it, r, 29, 6, 12, "#e8e0cc", "#cbbfa6", "#3f5a4a", "#a8784a", "#8a5e3a");
            Win(g, 10, y0 - 11, 4, 6); Win(g, 50, y0 - 11, 4, 6);
            // the fireplace and the portrait over it
            g.R(27, y0 - 9, 11, 10, "#ece6da"); g.R(27, y0 - 9, 11, 1, "#ffffff"); g.R(29, y0 - 5, 7, 6, "#2a1c12");
            g.R(30, y0 - 3, 5, 4, "#d8641e"); g.D(32, y0 - 3, "#f2c23a"); g.D(31, y0 - 2, "#f2a03a");
            it.Fire = new Vector2Int(32, y0 - 2);
            g.R(29, y0 - 13, 7, 4, "#c8a050"); g.R(30, y0 - 12, 5, 2, "#5a4a3a"); g.D(32, y0 - 12, "#e8c8a0");
            // bookcases either side
            foreach (int x in new[] { 16, 40 })
            {
                g.R(x, y0 - 12, 8, 11, "#5a3a22");
                for (int s = 0; s < 3; s++) Shelf(g, x + 1, y0 - 11 + s * 3, 6, new[] { "#8a2e22", "#2f5d7a", "#3f6a3a", "#c8a050", "#5a2a3a" }, 11 + s + x);
            }
            // a piano against the wall
            g.R(7, y0 + 1, 9, 5, "#2a1a14"); g.R(7, y0 + 1, 9, 1, "#4a3424"); g.R(8, y0 + 4, 7, 1, "#f4f0e6");
            for (int x = 9; x < 15; x += 2) g.D(x, y0 + 4, "#2a1a14");
            // the long dining table under a carpet
            Rug(g, 18, y0 + 3, 28, 13, "#5a2a3a", "#a8443a");
            Table(g, 22, y0 + 7, 20, 4);
            foreach (int x in new[] { 24, 29, 34, 39 }) { Chair(g, x, y0 + 6); Chair(g, x, y0 + 13); }
            Chair(g, 19, y0 + 8); Chair(g, 43, y0 + 8);
            // a settee
            g.R(47, y0 + 3, 9, 3, "#3f5470"); g.R(47, y0 + 6, 9, 2, "#2e3f57"); g.R(47, y0 + 2, 1, 6, "#2e3f57"); g.R(55, y0 + 2, 1, 6, "#2e3f57");
            // four good beds
            Bed(g, it, 7, y0 + 18, 10, 8, "#6a2a3a", 2);
            Bed(g, it, 18, y0 + 20, 8, 7, "#3f5470", 2);
            Bed(g, it, 39, y0 + 20, 8, 7, "#3f6a3a", 2);
            Bed(g, it, 48, y0 + 18, 9, 8, "#c8a050", 2);
            it.Work.Add(new Spot(42, y0 + 29, PeopleArt.Left));            // a maid sweeping
            it.Work.Add(new Spot(46, y0 + 12, PeopleArt.Left));            // serving at the table
            it.Seat.Add(new Spot(25, y0 + 9, PeopleArt.Down)); it.Seat.Add(new Spot(35, y0 + 9, PeopleArt.Down));
            it.Seat.Add(new Spot(30, y0 + 16, PeopleArt.Up)); it.Seat.Add(new Spot(40, y0 + 16, PeopleArt.Up));
            it.Seat.Add(new Spot(50, y0 + 9, PeopleArt.Down)); it.Seat.Add(new Spot(10, y0 + 9, PeopleArt.Up));
            it.Visit.Add(new Spot(32, y0 + 30, PeopleArt.Up)); it.Visit.Add(new Spot(28, y0 + 29, PeopleArt.Right));
        }
    }
}
