using FrontierLedger.Simulation;
using UnityEngine;

namespace FrontierLedger.Game.Art
{
    /// <summary>Rooms for the trades: butcher, bakery, tailor, hotel, barber, church and brewery.</summary>
    public static partial class InteriorArt
    {
        static void Counter(PixelCanvas g, int x, int y, int w, string top = "#a8734a", string face = "#7a4e2c")
        {
            g.R(x, y, w, 3, top); g.R(x, y, w, 1, Lighter(top)); g.R(x, y + 3, w, 3, face);
        }

        static void Butcher(PixelCanvas g, Interior it, int L)
        {
            int y0 = L;
            var r = new RectInt(1, y0 - 4, 30, 31);
            Room(g, it, r, 19, 6, 11, "#ece4d2", "#cfc4ad", "#8a2e22", "#d8c49a", "#c0ac80");   // sawdust on the floor
            // a rail of hooks: a side of beef, hams and a string of sausages
            g.R(3, y0 - 3, 26, 1, "#5a5a5a");
            g.R(5, y0 - 2, 4, 8, "#b04a3a"); g.R(6, y0 - 2, 2, 8, "#e8d8c0"); g.D(6, y0 + 2, "#c8604a");
            foreach (int hx in new[] { 12, 16 }) { g.D(hx + 1, y0 - 2, "#5a5a5a"); g.R(hx, y0 - 1, 3, 4, "#a0503a"); g.R(hx, y0 - 1, 3, 1, "#c8704a"); }
            for (int x = 21; x < 28; x += 2) { g.D(x, y0 - 2, "#8a3a2a"); g.D(x + 1, y0 - 1, "#8a3a2a"); }
            // chopping block, the counter with its scale
            g.R(24, y0 + 9, 5, 5, "#8a5a34"); g.R(24, y0 + 9, 5, 1, "#d8b07a"); g.D(26, y0 + 9, "#a0503a");
            Counter(g, 3, y0 + 12, 19);
            g.R(5, y0 + 10, 3, 2, "#c8a050"); g.D(6, y0 + 9, "#8a7a58");
            g.R(11, y0 + 11, 5, 1, "#e8dcc4"); g.R(12, y0 + 10, 3, 1, "#b04a3a");
            Barrel(g, 25, y0 + 17);
            it.Work.Add(new Spot(9, y0 + 11, PeopleArt.Down)); it.Work.Add(new Spot(17, y0 + 11, PeopleArt.Down));
            it.Visit.Add(new Spot(8, y0 + 22, PeopleArt.Up)); it.Visit.Add(new Spot(14, y0 + 22, PeopleArt.Up)); it.Visit.Add(new Spot(21, y0 + 23, PeopleArt.Up));
        }

        static void Bakery(PixelCanvas g, Interior it, int L)
        {
            int y0 = L;
            var r = new RectInt(2, y0 - 6, 28, 33);
            Room(g, it, r, 19, 6, 12, "#efe0c0", "#d4c4a0", "#a0503a");
            // the brick oven, glowing
            g.R(19, y0 - 6, 10, 13, "#a0503a");
            for (int y = y0 - 5; y < y0 + 7; y += 2) g.R(19 + (y % 4 == 1 ? 0 : 3), y, 3, 1, "#7a3a2a");
            g.R(21, y0 + 1, 6, 5, "#2a1410"); g.R(22, y0 + 3, 4, 3, "#c8501a"); g.D(23, y0 + 3, "#f2a03a"); g.R(22, y0, 4, 1, "#7a3a2a");
            it.Fire = new Vector2Int(24, y0 + 4);
            // shelves of loaves
            for (int s = 0; s < 3; s++)
            {
                g.R(4, y0 - 3 + s * 3, 13, 1, "#7a4e2c");
                for (int x = 4; x < 16; x += 3) { g.R(x, y0 - 5 + s * 3, 3, 2, "#c8863a"); g.D(x + 1, y0 - 5 + s * 3, "#e8b060"); }
            }
            // a floury kneading table, sacks of flour, and the counter
            g.R(6, y0 + 9, 10, 4, "#d8c49a"); g.R(6, y0 + 9, 10, 1, "#f4ecd8"); g.R(7, y0 + 13, 1, 2, "#6e4428"); g.R(14, y0 + 13, 1, 2, "#6e4428");
            g.R(9, y0 + 10, 4, 2, "#e8d8b0");
            Sack(g, 22, y0 + 10); Sack(g, 24, y0 + 13);
            Counter(g, 3, y0 + 16, 13, "#c49364", "#8a5a34");
            for (int x = 4; x < 14; x += 4) g.R(x, y0 + 15, 3, 1, "#c8863a");
            it.Work.Add(new Spot(23, y0 + 12, PeopleArt.Up)); it.Work.Add(new Spot(11, y0 + 9, PeopleArt.Down));
            it.Visit.Add(new Spot(7, y0 + 24, PeopleArt.Up)); it.Visit.Add(new Spot(12, y0 + 24, PeopleArt.Up)); it.Visit.Add(new Spot(19, y0 + 23, PeopleArt.Left));
        }

        static void Tailor(PixelCanvas g, Interior it, int L)
        {
            int y0 = L;
            var r = new RectInt(1, y0 - 4, 30, 31);
            Room(g, it, r, 19, 6, 11, "#e4dcc8", "#c8bca4", "#4a6a9a", "#a8784a", "#8a5e3a");
            // pigeonholes of cloth bolts, a long mirror
            g.R(3, y0 - 3, 15, 9, "#6e4428");
            string[] cloth = { "#8a3a4a", "#4a5a8a", "#6a8a5a", "#c8a050", "#e8dcc4", "#3a3a3a", "#a8734a" };
            for (int row = 0; row < 3; row++) for (int c = 0; c < 5; c++) g.R(4 + c * 3, y0 - 2 + row * 3, 2, 2, cloth[(row * 5 + c) % cloth.Length]);
            g.R(22, y0 - 3, 5, 9, "#c8a050"); g.R(23, y0 - 2, 3, 7, "#d8e8f0"); g.D(23, y0 - 2, "#ffffff");
            // the cutting table with cloth and shears, a dress form, the sewing chair
            Table(g, 5, y0 + 9, 12, 3); g.R(7, y0 + 9, 7, 2, "#5a7aaa"); g.D(15, y0 + 9, "#c8c8d0"); g.D(16, y0 + 10, "#c8c8d0");
            g.R(23, y0 + 9, 4, 5, "#8a3a4a"); g.R(22, y0 + 11, 6, 3, "#8a3a4a"); g.R(24, y0 + 8, 2, 1, "#5a4a3a"); g.R(24, y0 + 14, 2, 4, "#5a4a3a"); g.R(23, y0 + 18, 4, 1, "#3a2a1a");
            Chair(g, 10, y0 + 15);
            it.Work.Add(new Spot(11, y0 + 8, PeopleArt.Down)); it.Work.Add(new Spot(20, y0 + 15, PeopleArt.Right));
            it.Visit.Add(new Spot(8, y0 + 22, PeopleArt.Up)); it.Visit.Add(new Spot(15, y0 + 22, PeopleArt.Up)); it.Visit.Add(new Spot(25, y0 + 22, PeopleArt.Up));
        }

        static void Hotel(PixelCanvas g, Interior it, int L)
        {
            int y0 = L;
            var r = new RectInt(1, y0 - 20, 46, 47);
            Room(g, it, r, 20, 8, 14, "#d8c8a0", "#b8a478", "#7a4a2a", "#a87444", "#8a5a32");
            // pigeonholes for the room keys over the front desk, a clock, wall lamps
            g.R(4, y0 - 18, 11, 7, "#6e4428");
            for (int row = 0; row < 2; row++) for (int c = 0; c < 4; c++) { g.R(5 + c * 2 + c / 2, y0 - 17 + row * 3, 2, 2, "#3a2414"); if ((row + c) % 2 == 0) g.D(5 + c * 2 + c / 2, y0 - 16 + row * 3, "#c8a050"); }
            g.R(19, y0 - 18, 4, 4, "#c8a050"); g.R(20, y0 - 17, 2, 2, "#f4f0e6");
            foreach (int lx in new[] { 17, 30 }) { g.D(lx, y0 - 13, "#3a3a3a"); g.R(lx, y0 - 12, 1, 2, "#f2c860"); }
            Counter(g, 3, y0 - 4, 13);
            g.R(5, y0 - 5, 3, 1, "#f4ecd8"); g.D(12, y0 - 5, "#c8a050");   // the register and a bell
            // the stairs up to the rooms
            for (int st = 0; st < 8; st++) g.R(34 + st, y0 - 6 - st * 2, 11 - st, 2, st % 2 == 0 ? "#8a5a34" : "#6e4428");
            g.R(33, y0 - 20, 1, 16, "#5a3a22");
            // the dining room: tables laid with cloths, and a stove
            foreach (var t in new[] { new Vector2Int(8, y0 + 6), new Vector2Int(24, y0 + 6), new Vector2Int(8, y0 + 16), new Vector2Int(24, y0 + 16) })
            {
                g.R(t.x, t.y, 8, 4, "#f4ecd8"); g.R(t.x, t.y + 3, 8, 1, "#c8bca4"); g.R(t.x + 1, t.y + 4, 1, 2, "#6e4428"); g.R(t.x + 6, t.y + 4, 1, 2, "#6e4428");
                g.D(t.x + 2, t.y + 1, "#e8e8f0"); g.D(t.x + 5, t.y + 1, "#e8e8f0");
            }
            Stove(g, it, 39, y0 + 12, y0 - 3);
            Rug(g, 17, y0 + 22, 14, 3, "#6a2a3a", "#a8443a");
            it.Work.Add(new Spot(9, y0 - 5, PeopleArt.Down)); it.Work.Add(new Spot(20, y0 + 12, PeopleArt.Down)); it.Work.Add(new Spot(38, y0 + 19, PeopleArt.Up));
            it.Visit.Add(new Spot(7, y0 + 10, PeopleArt.Right)); it.Visit.Add(new Spot(17, y0 + 10, PeopleArt.Left)); it.Visit.Add(new Spot(23, y0 + 10, PeopleArt.Right));
            it.Visit.Add(new Spot(33, y0 + 10, PeopleArt.Left)); it.Visit.Add(new Spot(7, y0 + 20, PeopleArt.Right)); it.Visit.Add(new Spot(17, y0 + 20, PeopleArt.Left));
            it.Visit.Add(new Spot(23, y0 + 20, PeopleArt.Right)); it.Visit.Add(new Spot(33, y0 + 20, PeopleArt.Left)); it.Visit.Add(new Spot(10, y0 + 1, PeopleArt.Up));
        }

        static void Barber(PixelCanvas g, Interior it, int L)
        {
            int y0 = L;
            var r = new RectInt(1, y0 - 4, 30, 31);
            Room(g, it, r, 19, 6, 11, "#e8eef0", "#c8d0d4", "#b5432f", "#c8b8a0", "#a8987e");
            // a long mirror over a shelf of bottles and razors
            g.R(3, y0 - 3, 15, 5, "#c8a050"); g.R(4, y0 - 2, 13, 3, "#d8e8f0"); g.D(4, y0 - 2, "#ffffff");
            Shelf(g, 3, y0 + 2, 15, new[] { "#4a9a6a", "#8a3a22", "#c8862a", "#e8dcc4", "#9a9aa2" }, 41);
            // two barber's chairs
            foreach (int cx in new[] { 5, 12 })
            {
                g.R(cx, y0 + 9, 4, 3, "#a8302a"); g.R(cx, y0 + 7, 1, 4, "#a8302a"); g.R(cx + 3, y0 + 7, 1, 4, "#8a2a22");
                g.R(cx + 1, y0 + 12, 2, 2, "#5a5a5a"); g.R(cx, y0 + 14, 4, 1, "#3a3a3a");
            }
            // the bath behind a curtain, the boiler that heats the water
            g.R(20, y0 - 3, 1, 15, "#8a6a9a"); for (int y = y0 - 3; y < y0 + 12; y += 2) g.D(20, y, "#6e5280");
            g.R(22, y0 + 7, 8, 4, "#8a8a92"); g.R(23, y0 + 7, 6, 1, "#b8d8e8"); g.R(22, y0 + 11, 1, 1, "#5a5a5a"); g.R(29, y0 + 11, 1, 1, "#5a5a5a");
            g.R(24, y0 - 3, 5, 6, "#5a5a62"); g.R(25, y0 - 2, 3, 4, "#7a7a82"); g.R(25, y0 + 1, 3, 1, "#a8401a");
            it.Fire = new Vector2Int(26, y0 + 1);
            // a bench for those waiting
            g.R(3, y0 + 18, 10, 2, "#8a5a34"); g.R(3, y0 + 18, 10, 1, "#a8734a"); g.D(4, y0 + 20, "#6e4428"); g.D(11, y0 + 20, "#6e4428");
            it.Work.Add(new Spot(10, y0 + 12, PeopleArt.Left)); it.Work.Add(new Spot(17, y0 + 12, PeopleArt.Left));
            it.Visit.Add(new Spot(7, y0 + 12, PeopleArt.Down)); it.Visit.Add(new Spot(14, y0 + 12, PeopleArt.Down)); it.Visit.Add(new Spot(26, y0 + 10, PeopleArt.Down));
            it.Visit.Add(new Spot(5, y0 + 19, PeopleArt.Down)); it.Visit.Add(new Spot(10, y0 + 19, PeopleArt.Down));
        }

        static void Church(PixelCanvas g, Interior it, int L)
        {
            int y0 = L;
            var r = new RectInt(3, y0 + 2, 26, 35);
            Room(g, it, r, 13, 6, 10, "#f4efe2", "#d6ccb6", "#8a6a4a", "#a8784a", "#8a5e3a");
            // a plain cross and two tall windows on the back wall
            g.R(15, y0 + 3, 2, 6, "#8a5a34"); g.R(13, y0 + 5, 6, 1, "#8a5a34");
            foreach (int wx in new[] { 6, 23 }) Win(g, wx, y0 + 3, 3, 5);
            // the pulpit on a little platform
            g.R(11, y0 + 12, 10, 2, "#8a5a34"); g.R(11, y0 + 12, 10, 1, "#a8734a");
            g.R(14, y0 + 10, 4, 4, "#6e4428"); g.R(14, y0 + 10, 4, 1, "#a8734a"); g.D(15, y0 + 10, "#f4ecd8");
            // pews either side of the aisle
            for (int row = 0; row < 3; row++)
            {
                int py = y0 + 19 + row * 5;
                foreach (int px in new[] { 5, 19 })
                {
                    g.R(px, py, 9, 2, "#a8734a"); g.R(px, py, 9, 1, "#c49364"); g.R(px, py - 2, 9, 1, "#7a4e2c");
                    g.D(px, py + 2, "#6e4428"); g.D(px + 8, py + 2, "#6e4428");
                    for (int s = 0; s < 3; s++) it.Visit.Add(new Spot(px + 1 + s * 3, py + 1, PeopleArt.Up));
                }
            }
            it.Work.Add(new Spot(16, y0 + 12, PeopleArt.Down));
        }

        static void Brewery(PixelCanvas g, Interior it, int L)
        {
            int y0 = L;
            var r = new RectInt(2, y0 - 6, 44, 34);
            Room(g, it, r, 19, 10, 12, "#c8a888", "#a8886a", "#7a4028", "#8a7a6a", "#6e5e50");
            // the copper kettle over its firebox
            g.R(6, y0 + 9, 12, 6, "#8a4a3a"); g.R(9, y0 + 11, 6, 3, "#2a1410"); g.R(10, y0 + 12, 4, 2, "#c8501a"); g.D(11, y0 + 12, "#f2a03a");
            it.Fire = new Vector2Int(12, y0 + 13);
            g.Disc(12, y0 + 5, 5.5f, "#b8743a"); g.Disc(12, y0 + 4, 4f, "#d8944a"); g.R(9, y0 + 2, 6, 1, "#f0b870");
            g.R(11, y0 - 6, 2, 6, "#8a5a3a");
            // the mash tun, a wooden vat with a paddle
            g.Disc(27, y0 + 6, 5.5f, "#7a4e2c"); g.Disc(27, y0 + 6, 4.2f, "#c8a060"); g.Line(27, y0 + 4, 31, y0 - 1, "#8a5a34");
            for (int y = y0 + 2; y < y0 + 11; y += 3) g.R(22, y, 11, 1, "#5a3a22");
            // barrels racked along the side
            g.R(35, y0 + 14, 10, 1, "#5a3a22");
            Barrel(g, 35, y0 + 8); Barrel(g, 40, y0 + 8); Barrel(g, 37, y0 + 2); Barrel(g, 35, y0 + 18); Barrel(g, 40, y0 + 18);
            Sack(g, 5, y0 + 19); Sack(g, 9, y0 + 20);
            it.Work.Add(new Spot(19, y0 + 13, PeopleArt.Left)); it.Work.Add(new Spot(27, y0 + 15, PeopleArt.Up));
            it.Visit.Add(new Spot(24, y0 + 25, PeopleArt.Up)); it.Visit.Add(new Spot(30, y0 + 24, PeopleArt.Left));
        }
    }
}
