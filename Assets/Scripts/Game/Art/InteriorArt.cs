using System.Collections.Generic;
using FrontierLedger.Simulation;
using UnityEngine;

namespace FrontierLedger.Game.Art
{
    /// <summary>Where a settler stands (feet) or lies (head on the pillow) inside a room, in building-sprite pixels.</summary>
    public struct Spot
    {
        public int X, Y, Dir;
        public Spot(int x, int y, int dir = PeopleArt.Down) { X = x; Y = y; Dir = dir; }
    }

    /// <summary>A building with its roof lifted: the room, its furniture, and the spots people use.</summary>
    public sealed class Interior
    {
        public Sprite Sprite;
        public PixelCanvas Canvas;
        public RectInt Room;
        public readonly List<Spot> Work = new List<Spot>();    // staff at their stations
        public readonly List<Spot> Visit = new List<Spot>();   // customers, guests, the doctor on a house call
        public readonly List<Spot> Seat = new List<Spot>();    // pupils at desks, family round the table
        public readonly List<Spot> Bed = new List<Spot>();     // pillows: the sleeping, the sick, prisoners in the cell
        public Vector2Int Sparks = new Vector2Int(-1, -1);     // where the smith's hammer strikes
        public Vector2Int Fire = new Vector2Int(-1, -1);       // a hearth or forge that flickers
    }

    /// <summary>
    /// Cutaway interiors drawn to sit exactly where the building sprite does, so lifting the roof shows the room
    /// in place: back wall, floorboards, side walls, a stub of front wall with the doorway, and furniture.
    /// </summary>
    public static class InteriorArt
    {
        const string OL = "#3b2618";
        static readonly Dictionary<int, Interior> cache = new Dictionary<int, Interior>();

        public static bool Has(BuildingType t)
        {
            switch (t)
            {
                case BuildingType.House: case BuildingType.BoardingHouse: case BuildingType.Estate: case BuildingType.Storehouse:
                case BuildingType.Store: case BuildingType.Saloon: case BuildingType.Bank: case BuildingType.Sheriff:
                case BuildingType.Smithy: case BuildingType.School: case BuildingType.DoctorOffice:
                    return true;
                default: return false;
            }
        }

        public static Interior Get(BuildingType t, int variant)
        {
            if (!Has(t)) return null;
            int key = (int)t * 16 + variant;
            Interior it;
            if (!cache.TryGetValue(key, out it)) { it = Make(t, variant); cache[key] = it; }
            return it;
        }

        static Interior Make(BuildingType t, int v)
        {
            var d = Defs.Get(t);
            int L = BuildingArt.LiftOf(t), W = d.W * 16, H = d.H * 16 + L;
            var it = new Interior { Canvas = new PixelCanvas(W, H) };
            var g = it.Canvas;
            switch (t)
            {
                case BuildingType.House: House(g, it, L, v); break;
                case BuildingType.BoardingHouse: Boarding(g, it, L); break;
                case BuildingType.Estate: Estate(g, it, L); break;
                case BuildingType.Storehouse: Storehouse(g, it, L); break;
                case BuildingType.Store: Store(g, it, L); break;
                case BuildingType.Saloon: Saloon(g, it, L); break;
                case BuildingType.Bank: Bank(g, it, L); break;
                case BuildingType.Sheriff: Sheriff(g, it, L); break;
                case BuildingType.Smithy: Smithy(g, it, L); break;
                case BuildingType.School: School(g, it, L); break;
                case BuildingType.DoctorOffice: Doctor(g, it, L); break;
            }
            g.Outline(OL);
            it.Sprite = g.ToSprite("interior_" + t + "_" + v);
            return it;
        }

        // ================================================================== kit
        static float Hs(int x, int y, int s = 99) { return Noise.Hash(x, y, s); }

        /// <summary>The bare room: back wall (with wainscot), floorboards, side walls and the front wall cut down to a sill,
        /// open at the doorway. Returns the y where the floor starts.</summary>
        static int Room(PixelCanvas g, Interior it, RectInt r, int doorX, int doorW, int backH, string wall, string wallShade, string wains,
                        string floorA = "#c0925e", string floorB = "#a87a4a")
        {
            it.Room = r;
            // back wall
            g.R(r.x, r.y, r.width, backH, wall);
            for (int i = 0; i < r.width * backH / 9; i++) g.D(r.x + (int)(Hs(i, r.y, 5) * r.width), r.y + (int)(Hs(r.x, i, 6) * (backH - 4)), wallShade);
            g.R(r.x, r.y + backH - 4, r.width, 3, wains); g.R(r.x, r.y + backH - 4, r.width, 1, Lighter(wains));
            g.R(r.x, r.y + backH - 1, r.width, 1, "#5a3a22");
            // floorboards
            int fy = r.y + backH, fh = r.height - backH;
            g.R(r.x, fy, r.width, fh, floorA);
            for (int yy = fy + 2; yy < fy + fh; yy += 3)
            {
                g.R(r.x, yy, r.width, 1, floorB);
                int off = ((yy - fy) / 3) % 2 == 0 ? 4 : 11;
                for (int xx = r.x + off; xx < r.xMax; xx += 14) g.D(xx, yy - 1, floorB);
            }
            // side walls, seen edge-on
            g.R(r.x, r.y, 2, r.height, wallShade); g.R(r.xMax - 2, r.y, 2, r.height, wallShade);
            g.R(r.x + 1, r.y, 1, r.height, Lighter(wallShade));
            // the front wall cut down to a sill, open at the door
            g.R(r.x, r.yMax - 3, r.width, 3, wallShade); g.R(r.x, r.yMax - 3, r.width, 1, Lighter(wallShade));
            if (doorW > 0) { g.R(doorX, r.yMax - 3, doorW, 3, floorA); g.R(doorX, r.yMax - 1, doorW, 1, "#8d877d"); }
            return fy;
        }

        static string Lighter(string hex, float k = 1.18f)
        {
            var c = PixelCanvas.C(hex);
            return "#" + Mathf.Min(255, (int)(c.r * k)).ToString("x2") + Mathf.Min(255, (int)(c.g * k)).ToString("x2") + Mathf.Min(255, (int)(c.b * k)).ToString("x2");
        }

        static void Win(PixelCanvas g, int x, int y, int w, int h)
        {
            g.R(x - 1, y - 1, w + 2, h + 2, "#efe2c4");
            g.R(x, y, w, h, "#9ac4dc"); g.R(x, y + h / 2, w, (h + 1) / 2, "#7aa8c4");
            if (w >= 4) g.R(x + w / 2, y, 1, h, "#efe2c4");
            g.D(x, y, "#d8eef8");
        }

        /// <summary>A bed seen from above and in front: headboard, pillow, blanket.</summary>
        static void Bed(PixelCanvas g, Interior it, int x, int y, int w, int h, string blanket, int sleepers)
        {
            g.R(x, y, w, h, "#7a4e2c"); g.R(x, y, w, 2, "#5a3a22");
            g.R(x + 1, y + 2, w - 2, 2, "#f4f0e6");
            g.R(x + 1, y + 4, w - 2, h - 5, blanket); g.R(x + 1, y + 4, w - 2, 1, Lighter(blanket));
            for (int i = x + 2; i < x + w - 2; i += 3) g.D(i, y + 6, Lighter(blanket, 0.85f));
            g.R(x, y + h - 1, w, 1, "#5a3a22");
            int step = w / (sleepers + 1);
            for (int s = 0; s < sleepers; s++) it.Bed.Add(new Spot(x + step * (s + 1) - 2, y + 1));
        }

        static void Table(PixelCanvas g, int x, int y, int w, int h)
        {
            g.R(x, y, w, h, "#a8734a"); g.R(x, y, w, 1, "#c49364"); g.R(x, y + h - 1, w, 1, "#7a4e2c");
            g.R(x + 1, y + h, 1, 2, "#6e4428"); g.R(x + w - 2, y + h, 1, 2, "#6e4428");
        }

        static void Chair(PixelCanvas g, int x, int y)
        {
            g.R(x, y, 3, 2, "#8a5a34"); g.R(x, y - 2, 3, 1, "#6e4428"); g.D(x, y + 2, "#6e4428"); g.D(x + 2, y + 2, "#6e4428");
        }

        static void Stove(PixelCanvas g, Interior it, int x, int y, int pipeTop)
        {
            g.R(x + 1, pipeTop, 2, y - pipeTop, "#3a3a3e");
            g.R(x, y, 5, 5, "#2e2e32"); g.R(x, y, 5, 1, "#5a5a62"); g.R(x + 1, y + 2, 3, 2, "#a8401a"); g.D(x + 2, y + 2, "#f2a03a");
            g.D(x, y + 5, "#2e2e32"); g.D(x + 4, y + 5, "#2e2e32");
            it.Fire = new Vector2Int(x + 2, y + 3);
        }

        static void Shelf(PixelCanvas g, int x, int y, int w, string[] goods, int seed)
        {
            g.R(x, y + 3, w, 1, "#7a4e2c");
            for (int i = 0; i < w; i += 2)
            {
                float h = Hs(x + i, y, seed);
                if (h < 0.15f) continue;
                string c = goods[(int)(h * 97) % goods.Length];
                g.R(x + i, y + (h < 0.5f ? 0 : 1), 1, h < 0.5f ? 3 : 2, c);
            }
        }

        static void Barrel(PixelCanvas g, int x, int y)
        {
            g.R(x, y, 5, 6, "#8a5a34"); g.R(x + 3, y, 2, 6, "#6e4428");
            g.R(x, y + 1, 5, 1, "#5a5a5a"); g.R(x, y + 4, 5, 1, "#5a5a5a");
            g.R(x + 1, y - 1, 3, 1, "#a8734a");
        }

        static void Crate(PixelCanvas g, int x, int y, int s = 6)
        {
            g.R(x, y, s, s, "#c49a62"); g.R(x, y, s, 1, "#d8b07a"); g.R(x + s - 1, y, 1, s, "#a07a48");
            g.Line(x, y, x + s - 1, y + s - 1, "#a07a48");
        }

        static void Sack(PixelCanvas g, int x, int y)
        {
            g.R(x + 1, y, 3, 1, "#b8a478"); g.R(x, y + 1, 5, 4, "#d8c49a"); g.R(x + 3, y + 1, 2, 4, "#b8a478"); g.D(x + 2, y, "#8a7a58");
        }

        static void Rug(PixelCanvas g, int x, int y, int w, int h, string a, string b)
        {
            g.R(x, y, w, h, a); g.R(x + 1, y + 1, w - 2, h - 2, b); g.R(x + 2, y + 2, w - 4, h - 4, a);
        }

        // ================================================================== rooms
        static void House(PixelCanvas g, Interior it, int L, int v)
        {
            string[] papers = { "#e8d4b0", "#dce0c8", "#e4d0c8" };
            string[] blankets = { "#b5432f", "#3f6a9a", "#4a7a4a" };
            int y0 = L;
            var r = new RectInt(3, y0 + 16, 26, 28);
            Room(g, it, r, 13, 6, 10, papers[v], "#b8a888", "#8a5a34");
            Win(g, 8, y0 + 18, 5, 4);
            Shelf(g, 16, y0 + 19, 7, new[] { "#e8dcc4", "#c87a3a", "#7fa6b8", "#e84a3a" }, 3);
            Bed(g, it, 5, y0 + 26, 8, 8, blankets[v], 2);                 // the parents' bed
            Bed(g, it, 5, y0 + 35, 7, 6, "#c8a050", 2);                   // the children's
            Bed(g, it, 23, y0 + 35, 5, 6, "#8a6a9a", 1);                  // a cot
            Stove(g, it, 23, y0 + 26, y0 + 17);
            Table(g, 14, y0 + 29, 7, 3); Chair(g, 12, y0 + 30); Chair(g, 21, y0 + 30);
            Rug(g, 13, y0 + 37, 8, 4, "#a8443a", "#c8a050");
            it.Work.Add(new Spot(21, y0 + 33, PeopleArt.Up));            // at the stove
            it.Seat.Add(new Spot(15, y0 + 35, PeopleArt.Up)); it.Seat.Add(new Spot(19, y0 + 35, PeopleArt.Up)); it.Seat.Add(new Spot(17, y0 + 29, PeopleArt.Down));
            it.Visit.Add(new Spot(14, y0 + 40, PeopleArt.Left)); it.Visit.Add(new Spot(18, y0 + 41, PeopleArt.Up));
        }

        static void Boarding(PixelCanvas g, Interior it, int L)
        {
            int y0 = L;
            var r = new RectInt(2, y0 - 16, 44, 40);
            Room(g, it, r, 21, 6, 12, "#d8c8a0", "#b8a478", "#7a5a3a");
            Win(g, 8, y0 - 12, 4, 5); Win(g, 36, y0 - 12, 4, 5);
            string[] bl = { "#8a6a4a", "#6a7a5a", "#7a5a5a", "#5a6a7a" };
            // four beds along the back, four by the walls; a stove and a long table in the aisle
            int[] xs = { 4, 12, 30, 38 };
            for (int i = 0; i < 4; i++) Bed(g, it, xs[i], y0 - 3, 7, 8, bl[i], 1);
            for (int i = 0; i < 4; i++) Bed(g, it, xs[i], y0 + 8, 7, 8, bl[3 - i], 1);
            Stove(g, it, 22, y0 - 5, y0 - 15);
            Table(g, 21, y0 + 4, 7, 4); Chair(g, 19, y0 + 5); Chair(g, 28, y0 + 5);
            it.Seat.Add(new Spot(22, y0 + 12, PeopleArt.Up)); it.Seat.Add(new Spot(26, y0 + 12, PeopleArt.Up)); it.Seat.Add(new Spot(24, y0 + 4, PeopleArt.Down));
            it.Work.Add(new Spot(24, y0 + 2, PeopleArt.Up));
            it.Visit.Add(new Spot(24, y0 + 19, PeopleArt.Up)); it.Visit.Add(new Spot(20, y0 + 18, PeopleArt.Right));
        }

        static void Estate(PixelCanvas g, Interior it, int L)
        {
            int y0 = L;
            var r = new RectInt(5, y0 - 12, 38, 37);
            Room(g, it, r, 22, 4, 12, "#e4dcc8", "#c8bca4", "#5a6a5a", "#a8784a", "#8a5e3a");
            // fireplace, bookcase, portrait
            g.R(20, y0 - 11, 9, 11, "#8d877d"); g.R(20, y0 - 11, 9, 1, "#a39c90"); g.R(22, y0 - 6, 5, 5, "#2a1c12"); g.R(23, y0 - 4, 3, 3, "#d8641e"); g.D(24, y0 - 4, "#f2c23a");
            it.Fire = new Vector2Int(24, y0 - 3);
            g.R(8, y0 - 11, 8, 10, "#6e4428");
            for (int s = 0; s < 3; s++) Shelf(g, 9, y0 - 10 + s * 3, 6, new[] { "#8a2e22", "#2f5d7a", "#3f6a3a", "#c8a050" }, 9 + s);
            g.R(33, y0 - 10, 6, 6, "#c8a050"); g.R(34, y0 - 9, 4, 4, "#6a5a4a"); g.D(36, y0 - 8, "#e8c8a0");
            Rug(g, 14, y0 + 3, 20, 9, "#5a2a3a", "#a8443a");
            // the mayor's desk, a settee, the family's beds
            Table(g, 33, y0 + 2, 8, 4); Chair(g, 36, y0 + 7);
            g.R(15, y0 + 1, 12, 3, "#3f5470"); g.R(15, y0 + 4, 12, 2, "#2e3f57"); g.R(15, y0, 1, 6, "#2e3f57"); g.R(26, y0, 1, 6, "#2e3f57");
            Bed(g, it, 7, y0 + 13, 9, 8, "#6a2a3a", 2);
            Bed(g, it, 34, y0 + 13, 7, 7, "#3f5470", 2);
            Bed(g, it, 26, y0 + 15, 6, 6, "#c8a050", 2);
            it.Work.Add(new Spot(37, y0 + 1, PeopleArt.Down));
            it.Seat.Add(new Spot(18, y0 + 1, PeopleArt.Down)); it.Seat.Add(new Spot(23, y0 + 1, PeopleArt.Down)); it.Seat.Add(new Spot(20, y0 + 10, PeopleArt.Up));
            it.Visit.Add(new Spot(24, y0 + 20, PeopleArt.Up)); it.Visit.Add(new Spot(20, y0 + 18, PeopleArt.Up));
        }

        static void Storehouse(PixelCanvas g, Interior it, int L)
        {
            int y0 = L;
            var r = new RectInt(3, y0 - 12, 42, 41);
            Room(g, it, r, 17, 14, 13, "#9a6a4a", "#7a4a2a", "#5a3420", "#a08452", "#8a6a3a");
            // racks of sacks along the back, crates and barrels on the floor
            for (int s = 0; s < 2; s++) { g.R(5, y0 - 10 + s * 5, 38, 1, "#5a3420"); for (int x = 6; x < 42; x += 5) Sack(g, x, y0 - 14 + s * 5 + 4); }
            Barrel(g, 6, y0 + 4); Barrel(g, 12, y0 + 5); Barrel(g, 6, y0 + 12);
            Crate(g, 33, y0 + 3); Crate(g, 39, y0 + 3, 5); Crate(g, 35, y0 + 10, 7);
            for (int x = 14; x < 30; x += 5) Sack(g, x, y0 + 16);
            g.R(18, y0 + 6, 10, 4, "#a8734a"); g.R(18, y0 + 6, 10, 1, "#c49364"); g.D(19, y0 + 10, "#3a3a3a"); g.D(26, y0 + 10, "#3a3a3a");   // a hand cart
            it.Work.Add(new Spot(23, y0 + 14, PeopleArt.Up));
            it.Visit.Add(new Spot(20, y0 + 23, PeopleArt.Up)); it.Visit.Add(new Spot(27, y0 + 23, PeopleArt.Up)); it.Visit.Add(new Spot(24, y0 + 20, PeopleArt.Up));
        }

        static void Store(PixelCanvas g, Interior it, int L)
        {
            int y0 = L;
            var r = new RectInt(1, y0 - 8, 30, 35);
            Room(g, it, r, 13, 6, 12, "#e8d8b0", "#c8b48a", "#2f5d7a");
            string[] goods = { "#e84a3a", "#f2cf4a", "#7fb05a", "#e8dcc4", "#c87a3a", "#3f7aa8", "#8a5a34" };
            for (int s = 0; s < 3; s++) Shelf(g, 3, y0 - 7 + s * 3, 26, goods, 20 + s);
            // the counter, with scales and a jar of candy
            g.R(3, y0 + 9, 19, 3, "#a8734a"); g.R(3, y0 + 9, 19, 1, "#c49364"); g.R(3, y0 + 12, 19, 3, "#7a4e2c");
            g.R(6, y0 + 7, 3, 2, "#c8a050"); g.D(7, y0 + 6, "#8a7a58");
            g.R(16, y0 + 6, 3, 3, "#bcd8e4"); g.D(17, y0 + 7, "#e84a3a"); g.D(16, y0 + 8, "#f2cf4a");
            Barrel(g, 24, y0 + 6); Barrel(g, 25, y0 + 14); Sack(g, 22, y0 + 18);
            Stove(g, it, 4, y0 + 17, y0 + 15);
            it.Work.Add(new Spot(10, y0 + 8, PeopleArt.Down)); it.Work.Add(new Spot(17, y0 + 8, PeopleArt.Down));
            it.Visit.Add(new Spot(8, y0 + 19, PeopleArt.Up)); it.Visit.Add(new Spot(14, y0 + 19, PeopleArt.Up)); it.Visit.Add(new Spot(19, y0 + 21, PeopleArt.Up));
            it.Visit.Add(new Spot(12, y0 + 24, PeopleArt.Up)); it.Visit.Add(new Spot(25, y0 + 24, PeopleArt.Left));
        }

        static void Saloon(PixelCanvas g, Interior it, int L)
        {
            int y0 = L;
            var r = new RectInt(0, y0 - 14, 32, 41);
            Room(g, it, r, 11, 10, 12, "#8a5a32", "#6e4426", "#3a2414", "#a87444", "#8a5a32");
            // mirror and bottles behind the bar
            g.R(6, y0 - 12, 14, 5, "#bcd8e4"); g.R(6, y0 - 12, 14, 1, "#c9a46a"); g.R(6, y0 - 8, 14, 1, "#c9a46a");
            Shelf(g, 4, y0 - 7, 18, new[] { "#4a9a6a", "#8a3a22", "#c8862a", "#e8dcc4" }, 31);
            g.R(2, y0 + 1, 21, 3, "#6e4426"); g.R(2, y0 + 1, 21, 1, "#c9a46a"); g.R(2, y0 + 4, 21, 3, "#3a2414");
            for (int x = 4; x < 22; x += 4) { g.R(x, y0 + 9, 2, 2, "#c9a46a"); g.D(x, y0 + 11, "#3a2414"); }   // bar stools
            // tables and a piano
            foreach (int tx in new[] { 5, 19 }) { g.R(tx, y0 + 14, 7, 3, "#8a5a34"); g.R(tx, y0 + 14, 7, 1, "#a87444"); g.R(tx + 3, y0 + 17, 1, 3, "#3a2414"); }
            g.R(24, y0 - 1, 7, 7, "#2a1c12"); g.R(24, y0 + 3, 7, 1, "#f4f0e6"); for (int x = 25; x < 31; x += 2) g.D(x, y0 + 3, "#1a1010");
            it.Work.Add(new Spot(9, y0 + 0, PeopleArt.Down)); it.Work.Add(new Spot(16, y0 + 0, PeopleArt.Down));
            it.Visit.Add(new Spot(5, y0 + 11, PeopleArt.Up)); it.Visit.Add(new Spot(9, y0 + 11, PeopleArt.Up)); it.Visit.Add(new Spot(13, y0 + 11, PeopleArt.Up)); it.Visit.Add(new Spot(17, y0 + 11, PeopleArt.Up));
            it.Visit.Add(new Spot(4, y0 + 19, PeopleArt.Right)); it.Visit.Add(new Spot(13, y0 + 19, PeopleArt.Left)); it.Visit.Add(new Spot(18, y0 + 19, PeopleArt.Right)); it.Visit.Add(new Spot(27, y0 + 19, PeopleArt.Left));
            it.Visit.Add(new Spot(27, y0 + 9, PeopleArt.Up));
        }

        static void Bank(PixelCanvas g, Interior it, int L)
        {
            int y0 = L;
            var r = new RectInt(1, y0 - 10, 30, 36);
            Room(g, it, r, 13, 6, 12, "#e8e2d4", "#c8c1b4", "#6e3a2c", "#8a6a4a", "#6e5038");
            // the safe, a clock, the teller's cage
            g.R(21, y0 - 7, 8, 11, "#2e3a32"); g.R(21, y0 - 7, 8, 1, "#4e5a52"); g.R(23, y0 - 4, 4, 4, "#1e2622"); g.D(25, y0 - 2, "#c8a050"); g.R(22, y0 + 4, 6, 1, "#1a1a1a");
            g.R(9, y0 - 8, 4, 4, "#c8a050"); g.R(10, y0 - 7, 2, 2, "#f4f0e6");
            g.R(3, y0 + 4, 17, 3, "#5a3a22"); g.R(3, y0 + 4, 17, 1, "#8a5a34"); g.R(3, y0 + 7, 17, 3, "#3a2414");
            for (int x = 4; x < 20; x += 2) g.R(x, y0 - 1, 1, 5, "#c8a050");
            g.R(3, y0 - 2, 17, 1, "#c8a050");
            Rug(g, 8, y0 + 16, 14, 5, "#6a2a3a", "#a8443a");
            it.Work.Add(new Spot(11, y0 + 3, PeopleArt.Down));
            it.Visit.Add(new Spot(8, y0 + 15, PeopleArt.Up)); it.Visit.Add(new Spot(14, y0 + 15, PeopleArt.Up)); it.Visit.Add(new Spot(23, y0 + 18, PeopleArt.Up));
        }

        static void Sheriff(PixelCanvas g, Interior it, int L)
        {
            int y0 = L;
            var r = new RectInt(0, y0 - 6, 32, 33);
            Room(g, it, r, 13, 6, 11, "#c8b48a", "#a8946a", "#6b4428");
            // wanted posters, gun rack, desk, and the cell
            g.R(3, y0 - 5, 4, 5, "#f4ecd8"); g.R(4, y0 - 3, 2, 2, "#5a4a3a"); g.R(9, y0 - 5, 4, 5, "#f4ecd8"); g.R(10, y0 - 3, 2, 2, "#5a4a3a");
            g.R(14, y0 - 4, 6, 1, "#6e4428"); for (int x = 15; x < 20; x += 2) g.R(x, y0 - 5, 1, 4, "#3a3a3e");
            Table(g, 3, y0 + 8, 10, 4); Chair(g, 7, y0 + 6);
            g.R(5, y0 + 7, 3, 1, "#f4ecd8"); g.D(10, y0 + 7, "#f2cf4a");
            Bed(g, it, 23, y0 + 5, 7, 6, "#7a7a6a", 1);
            Bed(g, it, 23, y0 + 13, 7, 6, "#7a7a6a", 1);
            g.R(20, y0 + 4, 1, 17, "#3a3a3e");
            for (int x = 20; x < 30; x += 2) g.R(x, y0 + 19, 1, 3, "#3a3a3e");
            g.R(20, y0 + 19, 10, 1, "#5a5a62");
            for (int y = y0 + 6; y < y0 + 20; y += 2) g.D(20, y, "#5a5a62");
            it.Work.Add(new Spot(8, y0 + 7, PeopleArt.Down)); it.Work.Add(new Spot(16, y0 + 14, PeopleArt.Right));
            it.Visit.Add(new Spot(9, y0 + 17, PeopleArt.Up)); it.Visit.Add(new Spot(14, y0 + 20, PeopleArt.Up));
        }

        static void Smithy(PixelCanvas g, Interior it, int L)
        {
            int y0 = L;
            var r = new RectInt(2, y0 + 2, 28, 25);
            Room(g, it, r, 22, 5, 9, "#8d877d", "#6e6860", "#5a544c", "#6e6050", "#5a4e40");
            // tool rack on the wall
            for (int x = 14; x < 27; x += 3) { g.D(x, y0 + 3, "#9a9aa2"); g.R(x, y0 + 4, 1, 3, "#a8734a"); }
            // stone forge with its hood, bellows, anvil, quench tub
            g.R(4, y0 + 2, 9, 4, "#6e6860"); g.R(5, y0 + 6, 7, 6, "#8d877d"); g.R(5, y0 + 6, 7, 1, "#a39c90");
            g.R(6, y0 + 8, 5, 3, "#3a1a10"); g.R(7, y0 + 9, 3, 2, "#e0601e"); g.D(8, y0 + 9, "#ffc35a");
            it.Fire = new Vector2Int(8, y0 + 9);
            g.R(4, y0 + 13, 4, 3, "#7a4e2c"); g.R(4, y0 + 13, 4, 1, "#a8734a");
            g.R(14, y0 + 14, 6, 2, "#4a4a50"); g.D(14, y0 + 14, "#9a9aa2"); g.R(15, y0 + 16, 4, 1, "#3a3a3e"); g.R(16, y0 + 17, 2, 2, "#3a3a3e");
            g.R(23, y0 + 11, 5, 5, "#7a4e2c"); g.R(24, y0 + 12, 3, 2, "#4a6a7a");
            it.Sparks = new Vector2Int(17, y0 + 13);
            it.Work.Add(new Spot(17, y0 + 22, PeopleArt.Up)); it.Work.Add(new Spot(9, y0 + 18, PeopleArt.Up));
            it.Visit.Add(new Spot(25, y0 + 22, PeopleArt.Left));
        }

        static void School(PixelCanvas g, Interior it, int L)
        {
            int y0 = L;
            var r = new RectInt(3, y0 - 8, 26, 35);
            Room(g, it, r, 13, 6, 12, "#efe7d4", "#cfc4ae", "#7a5a3a");
            // blackboard with chalk, the teacher's desk, benches for the pupils
            g.R(8, y0 - 7, 16, 6, "#2e4a3a"); g.R(8, y0 - 7, 16, 1, "#7a5a3a"); g.R(8, y0 - 2, 16, 1, "#7a5a3a");
            for (int i = 0; i < 9; i++) g.D(10 + (int)(Hs(i, 4, 7) * 12), y0 - 6 + (int)(Hs(4, i, 8) * 4), "#e8e8e0");
            g.R(10, y0 - 5, 5, 1, "#e8e8e0"); g.R(17, y0 - 4, 4, 1, "#e8e8e0");
            Table(g, 11, y0 + 4, 9, 3); g.R(13, y0 + 3, 2, 1, "#e84a3a");
            Stove(g, it, 23, y0 + 2, y0 - 7);
            for (int row = 0; row < 3; row++)
            {
                int ry = y0 + 12 + row * 5;
                g.R(5, ry, 22, 2, "#a8734a"); g.R(5, ry, 22, 1, "#c49364"); g.D(6, ry + 2, "#6e4428"); g.D(25, ry + 2, "#6e4428");
                for (int s = 0; s < 4; s++) it.Seat.Add(new Spot(7 + s * 6, ry + 4, PeopleArt.Up));
            }
            it.Work.Add(new Spot(22, y0 + 8, PeopleArt.Down));
            it.Visit.Add(new Spot(9, y0 + 8, PeopleArt.Down));
        }

        static void Doctor(PixelCanvas g, Interior it, int L)
        {
            int y0 = L;
            var r = new RectInt(2, y0 - 12, 28, 37);
            Room(g, it, r, 20, 6, 12, "#e4ecf0", "#c4d0d8", "#5a6a7a");
            // a cabinet of bottles, a framed diploma, the examining cot, a desk
            g.R(4, y0 - 11, 9, 9, "#6e4428"); g.R(5, y0 - 10, 7, 7, "#bcd8e4");
            string[] glass = { "#4a9a6a", "#4a6ab0", "#c8862a", "#8a3a5a" };
            for (int s = 0; s < 2; s++) for (int i = 0; i < 3; i++) { g.R(6 + i * 2, y0 - 9 + s * 3, 1, 2, glass[(i + s) % 4]); }
            g.R(17, y0 - 10, 6, 5, "#c8a050"); g.R(18, y0 - 9, 4, 3, "#f4ecd8");
            Bed(g, it, 15, y0 + 2, 12, 6, "#e4ecf0", 1);
            Table(g, 4, y0 + 6, 8, 3); Chair(g, 7, y0 + 4); g.R(5, y0 + 5, 3, 1, "#f4ecd8"); g.R(9, y0 + 5, 2, 2, "#2a2a2a");
            it.Work.Add(new Spot(14, y0 + 9, PeopleArt.Right)); it.Work.Add(new Spot(8, y0 + 5, PeopleArt.Down));
            it.Visit.Add(new Spot(8, y0 + 17, PeopleArt.Up)); it.Visit.Add(new Spot(22, y0 + 15, PeopleArt.Up));
        }
    }
}
