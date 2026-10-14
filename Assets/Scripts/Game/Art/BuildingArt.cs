using System.Collections.Generic;
using FrontierLedger.Simulation;
using UnityEngine;

namespace FrontierLedger.Game.Art
{
    /// <summary>Everything the renderer needs to show one building in one season.</summary>
    public sealed class BuildingSprites
    {
        public Sprite[] Base = new Sprite[2];  // ground layer (gardens, yards, lawns); two sway frames
        public Sprite Top;                     // the building itself, y-sorted with people and trees
        public Sprite Lights;                  // lit windows, faded in at night
        public PixelCanvas TopCanvas;          // kept for pixel-accurate click picking and UI icons
        public Texture2D TopTexture;
        public int Lift;                       // pixels the sprite rises above its footprint
        public int W, H;
        public readonly List<Vector2> Smoke = new List<Vector2>();       // chimney tops, sprite pixels
        public readonly List<Vector2Int> Chickens = new List<Vector2Int>();
        public RectInt Forge;
        public bool HasForge;
        public Vector2Int PileAt = new Vector2Int(-1, -1);              // where the goods pile sits (sprite pixels, bottom-left)
    }

    /// <summary>
    /// Procedural 3/4-view buildings in a warm, chunky farm-sim style: dark outlines, plank walls,
    /// scalloped shingles, window boxes. Generated once per (type, variant, season) and cached.
    /// </summary>
    public static class BuildingArt
    {
        public const string OL = "#3b2618";
        const string SNOW = "#f4f8fa", SNOW2 = "#d4e2ea";

        public static int LiftOf(BuildingType t)
        {
            switch (t)
            {
                case BuildingType.House: return 2;
                case BuildingType.Storehouse: return 14;
                case BuildingType.Gatherer: return 6;
                case BuildingType.Fishery: return 6;
                case BuildingType.LumberCamp: return 8;
                case BuildingType.Woodcutter: return 6;
                case BuildingType.Quarry: return 10;
                case BuildingType.Smithy: return 10;
                case BuildingType.Store: return 12;
                case BuildingType.Stall: return 6;
                case BuildingType.Saloon: return 16;
                case BuildingType.Sheriff: return 10;
                case BuildingType.Estate: return 18;
                case BuildingType.Wagon: return 8;
                default: return 0;
            }
        }

        public static int VariantOf(Building b) { return b.Type == BuildingType.House ? b.Id % 3 : 0; }

        static readonly Dictionary<int, BuildingSprites> cache = new Dictionary<int, BuildingSprites>();
        public static BuildingSprites Get(BuildingType type, int variant, int season)
        {
            int key = ((int)type * 16 + variant) * 4 + season;
            BuildingSprites s;
            if (!cache.TryGetValue(key, out s)) { s = Make(type, variant, season); cache[key] = s; }
            return s;
        }

        // ================================================================== kit
        sealed class Ctx
        {
            public PixelCanvas Bg, Tg;
            public BuildingSprites M;
            public int Si, V, L, W, H;
            public List<RectInt> Wins = new List<RectInt>();
            public List<RectInt> Glow = new List<RectInt>();
            public List<CropRow> Crops = new List<CropRow>();
            public bool Winter { get { return Si == 3; } }
            public int Y(int f) { return f + L; }
        }
        struct CropRow { public int X, Y, W; }

        static float Hs(int x, int y, int s = 99) { return Noise.Hash(x, y, s); }

        /// <summary>Horizontal plank wall.</summary>
        static void Planks(PixelCanvas g, int x, int y, int w, int h, string b, string dark, string light)
        {
            g.R(x, y, w, h, b);
            for (int r = y; r < y + h; r += 3)
            {
                g.R(x, r, w, 1, light);
                if (r + 2 < y + h) g.R(x, r + 2, w, 1, dark);
                int off = ((r - y) / 3) % 2 == 0 ? 3 : 8;
                for (int xx = x + off; xx < x + w; xx += 10) { g.D(xx, r, dark); g.D(xx, r + 1, dark); }
            }
        }

        /// <summary>Scalloped shingle roof seen from the front slope.</summary>
        static void Shingles(PixelCanvas g, int x, int y, int w, int h, string[] c, bool winter)
        {
            g.R(x, y, w, h, c[0]);
            for (int r = 0; r < h; r += 3)
            {
                int yy = y + r, off = (r / 3) % 2 == 0 ? 0 : 2;
                for (int xx = x + off; xx < x + w; xx += 4)
                {
                    g.D(xx, yy + 2, c[1]); g.D(xx + 3, yy + 2, c[1]);
                    g.D(xx + 1, yy, c[2]);
                }
                g.R(x, yy + 2, w, 1, c[1]);
                for (int xx = x + off + 1; xx < x + w; xx += 4) g.R(xx, yy + 2, 2, 1, c[0]);
            }
            g.R(x, y, w, 1, c[2]);
            g.R(x, y + h - 1, w, 1, c[1]);
            if (winter)
            {
                g.R(x, y, w, Mathf.Min(h - 2, 4), SNOW);
                for (int xx = x; xx < x + w; xx++) if (Hs(xx, y) < 0.5f) g.D(xx, y + Mathf.Min(h - 2, 4), SNOW2);
                for (int xx = x; xx < x + w; xx++) if (Hs(xx, y, 7) < 0.25f) g.D(xx, y + Mathf.Min(h - 2, 4) + 1, SNOW2);
            }
        }

        /// <summary>Triangular gable end (barns, warehouses).</summary>
        static void Gable(PixelCanvas g, int cx, int top, int halfW, int h, string wall, string trim, string[] roof, bool winter)
        {
            for (int r = 0; r < h; r++)
            {
                int hw = Mathf.RoundToInt((r + 1f) / h * halfW);
                g.R(cx - hw, top + r, hw * 2, 1, wall);
                g.D(cx - hw - 1, top + r, roof[1]); g.D(cx - hw - 2, top + r, roof[0]);
                g.D(cx + hw, top + r, roof[1]); g.D(cx + hw + 1, top + r, roof[0]);
                if (winter) { g.D(cx - hw - 2, top + r, SNOW); g.D(cx + hw + 1, top + r, SNOW); }
            }
            g.R(cx - 1, top - 1, 2, 2, roof[0]);
        }

        static void Window(Ctx k, int x, int y, int w, int h, bool box = true, string frame = "#efe2c4")
        {
            var g = k.Tg;
            g.R(x - 1, y - 1, w + 2, h + 2, frame);
            g.R(x, y, w, h, "#5a86a8");
            g.R(x, y + h / 2, w, (h + 1) / 2, "#4a7090");
            g.D(x, y, "#c8e4f0"); if (w > 2) g.D(x + 1, y, "#9ac4dc");
            if (w >= 4) g.R(x + w / 2, y, 1, h, frame);
            if (h >= 4) g.R(x, y + h / 2, w, 1, frame);
            k.Wins.Add(new RectInt(x, y, w, h));
            if (box)
            {
                g.R(x - 1, y + h + 1, w + 2, 2, "#8a5a34");
                if (!k.Winter)
                    for (int i = 0; i < w + 2; i += 2)
                    {
                        g.D(x - 1 + i, y + h, "#5a9a3a");
                        if (k.Si < 2) g.D(x - 1 + i + (i % 4 == 0 ? 0 : 1), y + h, i % 4 == 0 ? "#e85a6a" : "#f2cf4a");
                    }
                else g.R(x - 1, y + h, w + 2, 1, SNOW);
            }
        }

        static void Door(PixelCanvas g, int x, int y, int w, int h, string wood = "#8a5a34")
        {
            g.R(x - 1, y - 1, w + 2, h + 1, "#5a3a22");
            g.R(x, y, w, h, wood);
            g.R(x + 1, y + 1, w - 2, h / 2 - 1, "#6e4428");
            g.R(x + 1, y + h / 2 + 1, w - 2, h / 2 - 2, "#6e4428");
            g.D(x + w - 2, y + h / 2, "#f2cf4a");
            g.R(x - 1, y + h, w + 2, 1, "#a39c90");
        }

        static void Foundation(PixelCanvas g, int x, int y, int w)
        {
            g.R(x, y, w, 2, "#8d877d");
            for (int xx = x; xx < x + w; xx += 4) { g.D(xx, y, "#6e6860"); g.D(xx + 2, y + 1, "#6e6860"); }
            g.R(x, y, w, 1, "#a39c90");
        }

        static void Chimney(Ctx k, int x, int y, int h)
        {
            var g = k.Tg;
            g.R(x, y, 5, h, "#8a7e72");
            for (int r = y + 1; r < y + h; r += 2) { g.D(x + 1 + (r % 4 == 1 ? 0 : 2), r, "#6e645a"); }
            g.R(x + 3, y, 2, h, "#6e645a");
            g.R(x - 1, y, 7, 2, "#5e554c");
            if (k.Winter) g.R(x - 1, y - 1, 7, 1, SNOW);
            k.M.Smoke.Add(new Vector2(x + 2.5f, y - 1));
        }

        static void Letters(PixelCanvas g, int x, int y, int w, string c)
        {
            for (int i = 0; i + 3 <= w; i += 4)
                for (int yy = 0; yy < 3; yy++)
                    for (int xx = 0; xx < 3; xx++)
                        if (Noise.Hash(x + i + xx, y + yy, i) < 0.55f) g.D(x + i + xx, y + yy, c);
        }

        static void Star5(PixelCanvas g, int x, int y, string c)
        {
            string[] rows = { "..x..", "xxxxx", ".xxx.", ".x.x.", "x...x" };
            for (int j = 0; j < rows.Length; j++)
                for (int i = 0; i < rows[j].Length; i++) if (rows[j][i] == 'x') g.D(x + i, y + j, c);
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
        static void Shadow(PixelCanvas g, int x, int y, int w) { g.RBlend(x, y, w, 2, new Color32(0, 0, 0, 55)); }

        // ================================================================== build
        static BuildingSprites Make(BuildingType type, int v, int si)
        {
            var d = Defs.Get(type);
            int L = LiftOf(type), W = d.W * 16, H = d.H * 16 + L;
            var m = new BuildingSprites { Lift = L, W = W, H = H };
            var k = new Ctx { Bg = new PixelCanvas(W, H), Tg = new PixelCanvas(W, H), M = m, Si = si, V = v, L = L, W = W, H = H };
            switch (type)
            {
                case BuildingType.House: House(k); break;
                case BuildingType.Storehouse: Storehouse(k); break;
                case BuildingType.Farm: Field(k); break;
                case BuildingType.Gatherer: Gatherer(k); break;
                case BuildingType.Fishery: Fishery(k); break;
                case BuildingType.LumberCamp: LumberCamp(k); break;
                case BuildingType.Woodcutter: Woodcutter(k); break;
                case BuildingType.Quarry: Quarry(k); break;
                case BuildingType.Smithy: Smithy(k); break;
                case BuildingType.Store: Store(k); break;
                case BuildingType.Stall: Stall(k); break;
                case BuildingType.Saloon: Saloon(k); break;
                case BuildingType.Sheriff: Sheriff(k); break;
                case BuildingType.Estate: Estate(k); break;
                case BuildingType.Cemetery: Cemetery(k); break;
                case BuildingType.Wagon: Wagon(k); break;
            }
            k.Tg.Outline(OL);
            string n = type + "_" + v + "_" + si;
            for (int f = 0; f < 2; f++)
            {
                var b = new PixelCanvas(W, H);
                b.Draw(k.Bg, 0, 0);
                foreach (var row in k.Crops) GardenRow(b, row, si, f);
                m.Base[f] = b.ToSprite(n + "_base" + f);
            }
            m.Top = k.Tg.ToSprite(n + "_top");
            m.TopTexture = m.Top.texture;
            m.TopCanvas = k.Tg;
            var lights = new PixelCanvas(W, H);
            var halo = PixelCanvas.C("#ffbe5a", 60);
            var rects = new List<RectInt>(k.Wins); rects.AddRange(k.Glow);
            foreach (var r in rects) lights.RBlend(r.x - 2, r.y - 2, r.width + 4, r.height + 4, halo);
            foreach (var r in rects) { lights.R(r.x, r.y, r.width, r.height, "#ffd27a"); lights.D(r.x, r.y, "#fff0c0"); }
            if (m.HasForge) lights.RBlend(m.Forge.x - 4, m.Forge.y - 4, m.Forge.width + 8, m.Forge.height + 8, PixelCanvas.C("#ff8c32", 80));
            m.Lights = lights.ToSprite(n + "_lights");
            return m;
        }

        /// <summary>Vegetable rows in a house garden.</summary>
        static void GardenRow(PixelCanvas g, CropRow r, int si, int frame)
        {
            for (int i = 0; i < r.W; i += 3)
            {
                int x = r.X + i, y = r.Y;
                float h = Noise.Hash(x, y, 4);
                int sw = (x / 3 + frame) % 2;
                if (si == 3) { if (h < 0.3f) g.D(x, y, "#9a8466"); continue; }
                if (si == 0) { g.D(x, y, "#4a8a2e"); if (h < 0.6f) g.D(x + sw, y - 1, "#7cc04a"); continue; }
                if (si == 1)
                {
                    g.R(x, y - 1, 2, 2, "#3f7a32"); g.D(x + sw, y - 2, "#5aa83e");
                    if (h < 0.3f) g.D(x + 1, y, "#e84a3a"); else if (h < 0.5f) g.D(x, y, "#f2cf4a");
                }
                else
                {
                    g.R(x, y - 1, 2, 2, "#7a7a32");
                    if (h < 0.45f) { g.R(x, y, 2, 1, "#e07a2a"); g.D(x, y - 1, "#f09a3a"); }
                }
            }
        }

        // ================================================================== housing
        static void House(Ctx k)
        {
            var bg = k.Bg; var tg = k.Tg; bool W = k.Winter; int v = k.V;
            string[][] walls = { new[] { "#c88a52", "#a86a3a", "#e0a868" }, new[] { "#e8dcc4", "#c8b89a", "#f6eedc" }, new[] { "#8aa4b0", "#6e8894", "#a8c0ca" } };
            string[][] roofs = { new[] { "#b5432f", "#8a2e22", "#d8664a" }, new[] { "#3f6a9a", "#2e4e74", "#5a8abc" }, new[] { "#4a7a4a", "#365a36", "#6a9a5a" } };
            int y0 = k.Y(0);
            // garden plot behind the cottage
            bg.R(3, y0 + 2, 26, 13, W ? SNOW : "#7a5232");
            if (!W) for (int fy = y0 + 4; fy < y0 + 15; fy += 3) bg.R(4, fy, 24, 1, "#5e3e24");
            else for (int i = 0; i < 26; i++) bg.D(3 + (int)(Hs(i, v, 1) * 26), y0 + 2 + (int)(Hs(v, i, 2) * 13), "#a89478");
            for (int x = 1; x < 31; x += 3) { bg.R(x, y0, 2, 3, "#e8dcc4"); bg.D(x, y0, "#c8b89a"); }
            bg.R(1, y0 + 1, 30, 1, "#c8b89a");
            bg.R(1, y0, 1, 17, "#c8b89a"); bg.R(30, y0, 1, 17, "#c8b89a");
            if (W) for (int x = 1; x < 31; x += 3) bg.D(x, y0, SNOW);
            k.Crops.Add(new CropRow { X = 5, Y = y0 + 6, W = 22 });
            k.Crops.Add(new CropRow { X = 5, Y = y0 + 9, W = 22 });
            k.Crops.Add(new CropRow { X = 5, Y = y0 + 12, W = 22 });
            bg.R(12, y0 + 46, 8, 2, W ? "#d8d2c6" : "#c4a878");
            // cottage
            Shadow(tg, 3, y0 + 45, 26);
            Shingles(tg, 1, y0 + 16, 30, 14, roofs[v], W);
            Chimney(k, 23, y0 + 9, 9);
            Planks(tg, 3, y0 + 30, 26, 14, walls[v][0], walls[v][1], walls[v][2]);
            tg.R(3, y0 + 30, 26, 1, walls[v][1]);
            Foundation(tg, 3, y0 + 43, 26);
            Window(k, 6, y0 + 33, 5, 4); Window(k, 21, y0 + 33, 5, 4);
            Door(tg, 13, y0 + 33, 6, 10);
        }

        static void Storehouse(Ctx k)
        {
            var bg = k.Bg; var tg = k.Tg; bool W = k.Winter;
            int y0 = k.Y(0);
            bg.R(0, y0 + 26, 48, 6, W ? "#d8d2c6" : "#b8996a");
            for (int i = 0; i < 20; i++) bg.D((int)(Hs(i, 2, 3) * 48), y0 + 26 + (int)(Hs(2, i, 4) * 6), W ? "#c4beb2" : "#a08452");
            Shadow(tg, 3, y0 + 29, 42);
            string[] roof = { "#7a4a2a", "#5a3420", "#9a6a3a" };
            // long roof running back
            Shingles(tg, 2, y0 - 12, 44, 16, roof, W);
            // gable front
            Gable(tg, 24, y0 - 10, 21, 14, "#b5432f", "#e8dcc4", roof, W);
            Planks(tg, 3, y0 + 4, 42, 25, "#b5432f", "#8a2e22", "#c8583e");
            tg.R(3, y0 + 4, 42, 1, "#e8dcc4");
            // big double doors with an X brace
            tg.R(16, y0 + 12, 16, 17, "#e8dcc4");
            tg.R(17, y0 + 13, 14, 16, "#7a4a2a");
            tg.R(23, y0 + 13, 2, 16, "#e8dcc4");
            tg.Line(17, y0 + 13, 22, y0 + 28, "#e8dcc4"); tg.Line(22, y0 + 13, 17, y0 + 28, "#e8dcc4");
            tg.Line(25, y0 + 13, 30, y0 + 28, "#e8dcc4"); tg.Line(30, y0 + 13, 25, y0 + 28, "#e8dcc4");
            // hayloft
            tg.R(21, y0 - 3, 6, 5, "#e8dcc4"); tg.R(22, y0 - 2, 4, 4, "#3a2618");
            Window(k, 7, y0 + 10, 4, 4, false); Window(k, 37, y0 + 10, 4, 4, false);
            Foundation(tg, 3, y0 + 29, 42);
            Barrel(tg, 4, y0 + 22); Crate(tg, 10, y0 + 23); Crate(tg, 38, y0 + 23); Barrel(tg, 33, y0 + 23);
            k.M.PileAt = new Vector2Int(1, y0 + 31);
        }

        // ================================================================== food
        static void Field(Ctx k)
        {
            var bg = k.Bg; var tg = k.Tg; bool W = k.Winter;
            // tilled soil with furrows; crops are drawn on top by the renderer by growth stage
            bg.R(1, 1, 46, 46, W ? SNOW : "#7a5232");
            for (int y = 3; y < 46; y += 4) { bg.R(2, y, 44, 1, W ? "#dfe8ec" : "#5e3e24"); bg.R(2, y + 1, 44, 1, W ? SNOW : "#8a6240"); }
            // split-rail fence
            for (int x = 0; x < 48; x += 8) { bg.R(x, 0, 2, 4, "#8a5a34"); bg.R(x, 44, 2, 4, "#8a5a34"); }
            for (int y = 0; y < 48; y += 8) { bg.R(0, y, 2, 4, "#8a5a34"); bg.R(46, y, 2, 4, "#8a5a34"); }
            bg.R(0, 1, 48, 1, "#a8734a"); bg.R(0, 46, 20, 1, "#a8734a"); bg.R(28, 46, 20, 1, "#a8734a");
            bg.R(0, 0, 1, 48, "#a8734a"); bg.R(47, 0, 1, 48, "#a8734a");
            if (W) { bg.R(0, 0, 48, 1, SNOW); }
            // scarecrow
            int sx = 22, sy = 14;
            tg.R(sx + 1, sy, 1, 14, "#7a4e2c");
            tg.R(sx - 4, sy + 4, 11, 1, "#7a4e2c");
            tg.R(sx - 2, sy + 3, 7, 6, "#4a6fa5"); tg.R(sx - 2, sy + 6, 7, 1, "#3a5a88");
            tg.R(sx - 1, sy - 3, 5, 4, "#e0c070"); tg.D(sx, sy - 2, "#2b1d1a"); tg.D(sx + 2, sy - 2, "#2b1d1a");
            tg.R(sx - 3, sy - 4, 9, 1, "#c8a050"); tg.R(sx - 1, sy - 6, 5, 2, "#c8a050");
            tg.D(sx - 4, sy + 5, "#e0c070"); tg.D(sx + 6, sy + 5, "#e0c070");
            if (W) tg.R(sx - 3, sy - 5, 9, 1, SNOW);
            k.M.PileAt = new Vector2Int(36, 46);
            k.M.Chickens.Add(new Vector2Int(10, 45)); k.M.Chickens.Add(new Vector2Int(16, 46));
        }

        static void Gatherer(Ctx k)
        {
            var bg = k.Bg; var tg = k.Tg; bool W = k.Winter; int y0 = k.Y(0);
            bg.Ellipse(16, y0 + 27, 14, 4, PixelCanvas.C(W ? "#d8d2c6" : "#a08452"));
            Shadow(tg, 5, y0 + 27, 22);
            // thatched roof hut
            string thatch = W ? "#d8c8a0" : "#c8a050", thatchD = "#a07a2a", thatchL = "#e0c070";
            for (int r = 0; r < 14; r++)
            {
                int hw = 4 + r;
                tg.R(16 - hw, y0 + 2 + r, hw * 2, 1, thatch);
                for (int x = 16 - hw; x < 16 + hw; x += 3) tg.D(x + (r % 2), y0 + 2 + r, thatchD);
                if (r % 3 == 0) tg.R(16 - hw, y0 + 2 + r, hw * 2, 1, r % 6 == 0 ? thatchL : thatch);
            }
            if (W) for (int r = 0; r < 4; r++) tg.R(12 - r, y0 + 2 + r, 8 + 2 * r, 1, SNOW);
            Planks(tg, 6, y0 + 16, 20, 11, "#9a6a3a", "#7a4e2c", "#b8844a");
            Door(tg, 14, y0 + 19, 4, 7, "#6e4428");
            Window(k, 8, y0 + 19, 3, 3, false);
            // baskets of berries and mushrooms
            if (!W)
            {
                tg.R(20, y0 + 23, 6, 4, "#b8844a"); tg.R(20, y0 + 22, 6, 1, "#8a5a34");
                tg.R(21, y0 + 21, 4, 1, "#7a3a8a"); tg.D(22, y0 + 20, "#5a2a6a"); tg.D(24, y0 + 21, "#d84a3a");
                tg.R(1, y0 + 25, 5, 3, "#b8844a"); tg.D(2, y0 + 24, "#e8dcc4"); tg.D(4, y0 + 24, "#d84a3a"); tg.D(3, y0 + 23, "#e8dcc4");
            }
            k.M.PileAt = new Vector2Int(26, y0 + 30);
        }

        static void Fishery(Ctx k)
        {
            var bg = k.Bg; var tg = k.Tg; bool W = k.Winter; int y0 = k.Y(0);
            bg.R(1, y0 + 22, 30, 10, "#8a5a34");
            for (int x = 1; x < 31; x += 3) bg.R(x, y0 + 22, 1, 10, "#6e4428");
            if (W) bg.R(1, y0 + 22, 30, 1, SNOW);
            Shadow(tg, 3, y0 + 20, 20);
            Shingles(tg, 2, y0 + 3, 20, 9, new[] { "#6e7a82", "#4e5a62", "#8e9aa2" }, W);
            Planks(tg, 3, y0 + 12, 18, 9, "#7a9aa8", "#5e7e8c", "#98b8c4");
            Door(tg, 10, y0 + 14, 4, 6, "#5a3a22");
            Window(k, 5, y0 + 14, 3, 3, false);
            Window(k, 16, y0 + 14, 3, 3, false);
            Foundation(tg, 3, y0 + 19, 18);
            // fish drying rack
            tg.R(23, y0 + 10, 1, 12, "#6e4428"); tg.R(30, y0 + 10, 1, 12, "#6e4428"); tg.R(23, y0 + 11, 8, 1, "#8a5a34");
            if (!W) for (int x = 24; x < 30; x += 2) { tg.R(x, y0 + 12, 1, 4, "#a8b8c0"); tg.D(x, y0 + 16, "#7a8a92"); }
            // net and oars
            tg.R(24, y0 + 22, 6, 3, "#c8b88a"); for (int x = 24; x < 30; x += 2) tg.D(x, y0 + 23, "#8a7a5a");
            tg.Line(2, y0 + 27, 8, y0 + 21, "#a8734a");
            k.M.PileAt = new Vector2Int(14, y0 + 31);
        }

        // ================================================================== industry
        static void LumberCamp(Ctx k)
        {
            var bg = k.Bg; var tg = k.Tg; bool W = k.Winter; int y0 = k.Y(0);
            bg.R(0, y0 + 22, 32, 10, W ? "#d8d2c6" : "#9e7a4d");
            for (int i = 0; i < 26; i++) bg.D((int)(Hs(i, 6, 3) * 32), y0 + 22 + (int)(Hs(6, i, 4) * 10), W ? "#c4beb2" : "#d8b07a");
            Shadow(tg, 3, y0 + 21, 20);
            Shingles(tg, 1, y0 + 2, 22, 10, new[] { "#5a6a3a", "#40502a", "#7a8a4a" }, W);
            // log cabin walls: stacked round logs
            for (int r = 0; r < 4; r++)
            {
                int y = y0 + 12 + r * 2;
                tg.R(3, y, 18, 2, "#8a5a34"); tg.R(3, y + 1, 18, 1, "#6e4428");
                tg.R(2, y, 2, 2, "#d8b07a"); tg.D(2, y, "#a8804a"); tg.R(20, y, 2, 2, "#d8b07a"); tg.D(21, y + 1, "#a8804a");
            }
            Door(tg, 10, y0 + 14, 4, 6, "#5a3a22");
            Window(k, 5, y0 + 14, 3, 3, false);
            Chimney(k, 16, y0 - 2, 6);
            // axe in a stump
            tg.R(24, y0 + 22, 6, 4, "#7a4e2c"); tg.R(24, y0 + 22, 6, 1, "#d8b07a");
            tg.Line(27, y0 + 22, 29, y0 + 16, "#a8734a"); tg.R(28, y0 + 15, 3, 2, "#9a9aa2");
            k.M.PileAt = new Vector2Int(2, y0 + 31);
        }

        static void Woodcutter(Ctx k)
        {
            var bg = k.Bg; var tg = k.Tg; bool W = k.Winter; int y0 = k.Y(0);
            bg.R(0, y0 + 6, 32, 26, W ? "#d8d2c6" : "#a8845a");
            for (int i = 0; i < 40; i++) bg.D((int)(Hs(i, 8, 3) * 32), y0 + 6 + (int)(Hs(8, i, 4) * 26), W ? "#c4beb2" : "#d8b07a");
            // lean-to shed on the right
            Shadow(tg, 16, y0 + 19, 15);
            Shingles(tg, 15, y0 - 2, 17, 10, new[] { "#7a4a2a", "#5a3420", "#9a6a3a" }, W);
            Planks(tg, 17, y0 + 8, 13, 12, "#9a6a3a", "#7a4e2c", "#b8844a");
            tg.R(20, y0 + 11, 7, 9, "#2a1f1a");
            tg.Line(21, y0 + 12, 21, y0 + 18, "#9a9aa2"); tg.R(20, y0 + 12, 3, 2, "#9a9aa2");   // saw hanging inside
            // sawhorse with a log
            tg.Line(3, y0 + 20, 6, y0 + 14, "#8a5a34"); tg.Line(9, y0 + 20, 6, y0 + 14, "#8a5a34");
            tg.R(2, y0 + 13, 11, 3, "#8a5a34"); tg.Disc(3, y0 + 14.5f, 1.8f, "#d8b07a");
            // chopping block, axe and split pieces
            tg.R(13, y0 + 22, 6, 5, "#8a5a34"); tg.R(13, y0 + 22, 6, 1, "#d8b07a"); tg.D(15, y0 + 22, "#a8804a");
            tg.Line(17, y0 + 22, 21, y0 + 18, "#a8734a"); tg.R(20, y0 + 17, 3, 2, "#9a9aa2");
            tg.R(11, y0 + 27, 3, 2, "#c49364"); tg.R(19, y0 + 26, 3, 2, "#c49364");
            k.M.PileAt = new Vector2Int(1, y0 + 31);
        }

        static void Quarry(Ctx k)
        {
            var bg = k.Bg; var tg = k.Tg; bool W = k.Winter; int y0 = k.Y(0);
            bg.R(0, y0 + 4, 32, 28, W ? "#e4e8ea" : "#a39c90");
            for (int i = 0; i < 40; i++) bg.D((int)(Hs(i, 9, 3) * 32), y0 + 4 + (int)(Hs(9, i, 4) * 28), W ? "#c8d0d4" : "#8d877d");
            for (int i = 0; i < 12; i++) bg.D((int)(Hs(i, 9, 5) * 32), y0 + 4 + (int)(Hs(9, i, 6) * 28), "#c8c1b4");
            // wooden derrick (A-frame with a boom and a block on a rope)
            tg.Line(5, y0 + 26, 11, y0 - 6, "#8a5a34"); tg.Line(17, y0 + 26, 11, y0 - 6, "#8a5a34");
            tg.Line(6, y0 + 26, 12, y0 - 6, "#6e4428");
            tg.R(7, y0 + 12, 9, 1, "#8a5a34");
            tg.Line(11, y0 - 5, 28, y0 + 2, "#8a5a34");
            tg.R(27, y0 + 2, 1, 12, "#c8b88a");
            tg.R(24, y0 + 14, 7, 5, "#b2aca1"); tg.R(24, y0 + 14, 7, 1, "#c8c1b4"); tg.R(29, y0 + 15, 2, 4, "#8d877d");
            tg.R(9, y0 + 22, 5, 4, "#6e4428"); tg.D(11, y0 + 23, "#9a9aa2");
            // pickaxe leaning on a block
            tg.R(18, y0 + 22, 6, 5, "#a39c90"); tg.R(18, y0 + 22, 6, 1, "#c8c1b4");
            tg.Line(19, y0 + 21, 23, y0 + 16, "#a8734a"); tg.Line(21, y0 + 16, 25, y0 + 17, "#9a9aa2");
            if (W) { tg.D(11, y0 - 7, SNOW); tg.R(24, y0 + 13, 7, 1, SNOW); }
            k.M.PileAt = new Vector2Int(1, y0 + 31);
        }

        static void Smithy(Ctx k)
        {
            var bg = k.Bg; var tg = k.Tg; bool W = k.Winter; int y0 = k.Y(0);
            bg.R(0, y0 + 26, 32, 6, W ? "#d8d2c6" : "#8a7458");
            Shadow(tg, 3, y0 + 26, 26);
            Shingles(tg, 1, y0 + 2, 30, 12, new[] { "#4a4f57", "#34383f", "#646a74" }, W);
            Chimney(k, 22, y0 - 6, 9);
            // stone walls
            tg.R(2, y0 + 14, 28, 13, "#8d877d");
            for (int r = 0; r < 13; r += 3)
            {
                tg.R(2, y0 + 14 + r, 28, 1, "#6e6860");
                for (int x = 2 + ((r / 3) % 2 == 1 ? 3 : 0); x < 30; x += 6) tg.R(x, y0 + 14 + r, 1, 3, "#6e6860");
            }
            tg.R(2, y0 + 14, 28, 1, "#a39c90");
            // open forge bay
            tg.R(4, y0 + 16, 15, 11, "#2a1f1a");
            tg.R(5, y0 + 21, 6, 5, "#6c665e"); tg.R(5, y0 + 21, 6, 1, "#8d877d"); tg.R(6, y0 + 22, 4, 3, "#3a1a10");
            k.M.Forge = new RectInt(6, y0 + 22, 4, 3); k.M.HasForge = true;
            // anvil
            tg.R(12, y0 + 23, 6, 1, "#4a4a50"); tg.D(12, y0 + 23, "#9a9aa2"); tg.R(13, y0 + 24, 4, 1, "#3a3a3e"); tg.R(14, y0 + 25, 2, 2, "#3a3a3e");
            // tools on the wall
            tg.D(14, y0 + 18, "#9a9aa2"); tg.R(14, y0 + 19, 1, 2, "#a8734a"); tg.D(16, y0 + 18, "#9a9aa2"); tg.R(16, y0 + 19, 1, 2, "#a8734a");
            Window(k, 23, y0 + 17, 4, 4, false);
            Door(tg, 22, y0 + 22, 5, 5, "#6e4428");
            k.M.PileAt = new Vector2Int(-1, -1);
        }

        // ================================================================== trade
        static void Store(Ctx k)
        {
            var bg = k.Bg; var tg = k.Tg; bool W = k.Winter; int y0 = k.Y(0);
            // boardwalk
            bg.R(0, y0 + 27, 32, 5, W ? "#d8d2c6" : "#b98b5a");
            for (int x = 3; x < 32; x += 4) bg.R(x, y0 + 27, 1, 5, W ? "#bdb5a5" : "#8a5d3a");
            Shingles(tg, 2, y0 - 8, 28, 10, new[] { "#6e6a64", "#58544f", "#85807a" }, W);
            // false front
            tg.R(0, y0 - 4, 32, 31, "#d8c8a0");
            Planks(tg, 1, y0 - 3, 30, 30, "#e8d8b0", "#c8b48a", "#f4e8c8");
            tg.R(0, y0 - 4, 32, 2, "#2f5d7a"); tg.R(0, y0 - 2, 32, 1, "#23465c");
            if (W) tg.R(0, y0 - 5, 32, 1, SNOW);
            tg.R(4, y0 + 0, 24, 7, "#2f5d7a"); tg.R(5, y0 + 1, 22, 5, "#efe1c4"); Letters(tg, 7, y0 + 2, 18, "#2f3a46");
            // striped awning
            for (int x = 0; x < 32; x++) tg.R(x, y0 + 10, 1, 4, ((x / 3) % 2 == 1) ? "#f4ecd8" : "#3f7aa8");
            for (int x = 1; x < 32; x += 3) tg.D(x, y0 + 14, ((x / 3) % 2 == 1) ? "#f4ecd8" : "#3f7aa8");
            if (W) tg.R(0, y0 + 9, 32, 1, SNOW);
            Window(k, 3, y0 + 17, 7, 6, false, "#2f5d7a"); Window(k, 22, y0 + 17, 7, 6, false, "#2f5d7a");
            string[] goods = { "#e84a3a", "#f2cf4a", "#7fb05a", "#e8dcc4", "#c87a3a" };
            for (int i = 0; i < 6; i++) { tg.D(3 + i + (i > 2 ? 1 : 0), y0 + 22, goods[i % 5]); tg.D(22 + i + (i > 2 ? 1 : 0), y0 + 22, goods[(i + 2) % 5]); }
            Door(tg, 13, y0 + 17, 6, 10, "#6e4428");
            tg.R(14, y0 + 18, 4, 3, "#5a86a8");
            Barrel(tg, 25, y0 + 25); Crate(tg, 1, y0 + 25, 5);
        }

        static void Stall(Ctx k)
        {
            var bg = k.Bg; var tg = k.Tg; bool W = k.Winter; int y0 = k.Y(0);
            bg.R(0, y0 + 9, 16, 7, W ? "#d8d2c6" : "#b8996a");
            tg.R(2, y0 - 2, 1, 14, "#6e4428"); tg.R(13, y0 - 2, 1, 14, "#6e4428");
            for (int x = 0; x < 16; x++) tg.R(x, y0 - 5, 1, 5, ((x / 2) % 2 == 1) ? "#f4ecd8" : "#c8443a");
            for (int x = 0; x < 16; x += 2) tg.D(x, y0, ((x / 2) % 2 == 1) ? "#f4ecd8" : "#c8443a");
            if (W) tg.R(0, y0 - 5, 16, 2, SNOW);
            tg.R(1, y0 + 6, 14, 5, "#8a5a34"); tg.R(1, y0 + 6, 14, 1, "#b8844a");
            string[] pc = { "#e84a3a", "#f2cf4a", "#7fb05a", "#e0802a" };
            if (!W) for (int x = 2; x < 14; x++) { tg.D(x, y0 + 5, pc[x % 4]); if (x % 2 == 1) tg.D(x, y0 + 4, pc[(x + 1) % 4]); }
            tg.R(3, y0 + 11, 4, 3, "#b8844a"); tg.R(9, y0 + 11, 4, 3, "#b8844a");
        }

        static void Saloon(Ctx k)
        {
            var bg = k.Bg; var tg = k.Tg; bool W = k.Winter; int y0 = k.Y(0);
            bg.R(0, y0 + 27, 32, 5, W ? "#d8d2c6" : "#b98b5a");
            for (int x = 3; x < 32; x += 4) bg.R(x, y0 + 27, 1, 5, W ? "#bdb5a5" : "#8a5d3a");
            Planks(tg, 0, y0 - 14, 32, 41, "#8a5a32", "#6e4426", "#a87444");
            tg.R(10, y0 - 16, 12, 3, "#8a5a32"); tg.R(10, y0 - 16, 12, 1, "#c9a46a");
            tg.R(0, y0 - 14, 32, 1, "#c9a46a");
            if (W) { tg.R(0, y0 - 15, 32, 1, SNOW); tg.R(10, y0 - 17, 12, 1, SNOW); }
            tg.R(3, y0 - 11, 26, 6, "#3a2414"); tg.R(4, y0 - 10, 24, 4, "#e0b04a"); Letters(tg, 6, y0 - 9, 20, "#3a2414");
            Window(k, 5, y0 - 2, 5, 5, false, "#c9a46a"); Window(k, 22, y0 - 2, 5, 5, false, "#c9a46a");
            // balcony
            tg.R(0, y0 + 5, 32, 2, "#5a3a22");
            for (int x = 1; x < 32; x += 2) tg.R(x, y0 + 7, 1, 3, "#c9a46a");
            tg.R(0, y0 + 7, 32, 1, "#c9a46a"); tg.R(0, y0 + 10, 32, 2, "#3a2414");
            if (W) tg.R(0, y0 + 4, 32, 1, SNOW);
            tg.R(0, y0 + 12, 2, 15, "#4b2e1a"); tg.R(30, y0 + 12, 2, 15, "#4b2e1a");
            Window(k, 4, y0 + 16, 5, 5, false, "#c9a46a"); Window(k, 23, y0 + 16, 5, 5, false, "#c9a46a");
            tg.R(11, y0 + 14, 10, 13, "#1e1410");
            k.Glow.Add(new RectInt(12, y0 + 15, 8, 4));
            tg.R(12, y0 + 18, 3, 5, "#a8734a"); tg.R(17, y0 + 18, 3, 5, "#a8734a");
            tg.R(12, y0 + 18, 3, 1, "#c49364"); tg.R(17, y0 + 18, 3, 1, "#c49364");
            Barrel(tg, 25, y0 + 25);
        }

        // ================================================================== civic
        static void Sheriff(Ctx k)
        {
            var bg = k.Bg; var tg = k.Tg; bool W = k.Winter; int y0 = k.Y(0);
            bg.R(0, y0 + 27, 32, 5, W ? "#d8d2c6" : "#b98b5a");
            for (int x = 3; x < 32; x += 4) bg.R(x, y0 + 27, 1, 5, W ? "#bdb5a5" : "#8a5d3a");
            tg.R(2, y0 - 6, 28, 8, W ? SNOW : "#5d574f"); tg.R(2, y0 - 6, 28, 1, W ? SNOW2 : "#8a8276");
            tg.R(26, y0 - 12, 1, 9, "#c8c8c8");
            tg.R(21, y0 - 12, 5, 4, "#efe1c4"); tg.R(21, y0 - 12, 5, 1, "#b5432f"); tg.R(21, y0 - 10, 5, 1, "#b5432f"); tg.R(21, y0 - 12, 2, 2, "#2f5d7a");
            // brick walls
            tg.R(0, y0 + 2, 32, 25, "#b06a4a");
            for (int r = 0; r < 25; r += 3)
            {
                tg.R(0, y0 + 2 + r, 32, 1, "#8a4e36");
                for (int x = ((r / 3) % 2 == 1 ? 2 : 5); x < 32; x += 6) tg.R(x, y0 + 2 + r, 1, 3, "#8a4e36");
            }
            tg.R(0, y0 + 2, 32, 1, "#c88a6a");
            tg.R(5, y0 + 4, 22, 7, "#3a2c22"); Star5(tg, 14, y0 + 5, "#f2cf4a");
            Letters(tg, 6, y0 + 6, 6, "#f2cf4a"); Letters(tg, 21, y0 + 6, 5, "#f2cf4a");
            tg.R(0, y0 + 12, 32, 2, "#6b4428"); if (W) tg.R(0, y0 + 11, 32, 1, SNOW);
            foreach (int x in new[] { 4, 22 })
            {
                tg.R(x - 1, y0 + 16, 8, 7, "#6f6252"); tg.R(x, y0 + 17, 6, 5, "#2c3a48");
                k.Wins.Add(new RectInt(x, y0 + 17, 6, 5));
                for (int i = x + 1; i < x + 6; i += 2) tg.R(i, y0 + 17, 1, 5, "#9a9a9a");
            }
            Door(tg, 13, y0 + 16, 6, 11, "#5a3a22");
        }

        static void Estate(Ctx k)
        {
            var bg = k.Bg; var tg = k.Tg; bool W = k.Winter; int si = k.Si; int y0 = k.Y(0);
            string[][] lawns = { new[] { "#7fbe55", "#72ae4c" }, new[] { "#6fae48", "#64a03f" }, new[] { "#a6a052", "#979348" }, new[] { SNOW, "#e4ecf0" } };
            var lawn = lawns[si];
            bg.R(0, y0, 48, 32, lawn[0]);
            for (int x = 0; x < 48; x += 4) bg.R(x, y0, 2, 32, lawn[1]);
            bg.R(20, y0 + 25, 8, 7, W ? "#d8d2c6" : "#d8c8a0");
            foreach (var span in new[] { new[] { 0, 19 }, new[] { 29, 48 } })
                for (int x = span[0]; x < span[1] - 1; x += 3)
                {
                    bg.R(x, y0 + 27, 4, 4, W ? "#e4ecf0" : "#3f7a3a");
                    bg.D(x + 1, y0 + 27, W ? SNOW : "#5a9a4a");
                    if (!W && si < 2 && Noise.Hash(x, 1, 8) < 0.5f) bg.D(x + 2, y0 + 28, new[] { "#e85a6a", "#f2cf4a", "#f4f1e6" }[x % 3]);
                }
            string[] rc = { "#3f5470", "#2e3f57", "#566f8f" };
            Shadow(tg, 6, y0 + 24, 36);
            Shingles(tg, 4, y0 - 12, 40, 19, rc, W);
            for (int i = 0; i < 6; i++) { tg.Clear(4, y0 - 12 + i, 6 - i, 1); tg.Clear(38 + i, y0 - 12 + i, 6 - i, 1); }
            foreach (int dx in new[] { 13, 30 })
            {
                tg.R(dx, y0 - 7, 6, 7, "#ece4d2"); tg.R(dx - 1, y0 - 8, 8, 2, rc[1]);
                Window(k, dx + 2, y0 - 5, 2, 3, false, "#ece4d2");
                if (W) tg.R(dx - 1, y0 - 9, 8, 1, SNOW);
            }
            foreach (int cx in new[] { 7, 37 }) { Chimney(k, cx, y0 - 17, 8); }
            Planks(tg, 5, y0 + 7, 38, 18, "#f4ecdc", "#d6ccb6", "#fffaf0");
            foreach (int x in new[] { 8, 13, 33, 38 })
                foreach (int fy in new[] { 9, 16 })
                {
                    tg.R(x - 2, y0 + fy, 1, 4, "#3f5a3a"); tg.R(x + 3, y0 + fy, 1, 4, "#3f5a3a");
                    Window(k, x, y0 + fy, 2, 4, false, "#ffffff");
                }
            // portico with columns
            for (int r = 0; r < 6; r++) tg.R(23 - r, y0 + 2 + r, 2 + 2 * r, 1, "#fffaf0");
            tg.R(17, y0 + 8, 14, 2, "#e8dfcc");
            foreach (int x in new[] { 18, 21, 25, 28 }) { tg.R(x, y0 + 10, 2, 14, "#fffaf0"); tg.R(x + 1, y0 + 10, 1, 14, "#d6ccb6"); }
            Door(tg, 22, y0 + 15, 4, 9, "#3a4a3a");
            tg.R(18, y0 + 24, 12, 1, "#c8c1b4"); tg.R(17, y0 + 25, 14, 1, "#a8a090");
        }

        static void Cemetery(Ctx k)
        {
            var bg = k.Bg; var tg = k.Tg; bool W = k.Winter;
            bg.R(1, 1, 46, 46, W ? "#e8eef0" : "#6a9a48");
            for (int i = 0; i < 60; i++) bg.D(1 + (int)(Hs(i, 11, 3) * 46), 1 + (int)(Hs(11, i, 4) * 46), W ? "#d4e2ea" : "#5a8a3e");
            // low stone wall with a gate at the bottom
            for (int x = 0; x < 48; x++)
            {
                if (x >= 20 && x < 28) continue;
                bg.R(x, 44, 1, 4, (x % 4 == 0) ? "#6e6860" : "#8d877d");
            }
            for (int y = 0; y < 48; y++) { bg.R(0, y, 2, 1, (y % 4 == 0) ? "#6e6860" : "#8d877d"); bg.R(46, y, 2, 1, (y % 4 == 0) ? "#6e6860" : "#8d877d"); }
            bg.R(0, 0, 48, 2, "#8d877d");
            if (W) { bg.R(0, 0, 48, 1, SNOW); }
            // iron gate posts
            tg.R(19, 40, 2, 8, "#5a5a5a"); tg.R(27, 40, 2, 8, "#5a5a5a"); tg.R(21, 41, 6, 1, "#3a3a3a");
            for (int x = 21; x < 27; x += 2) tg.R(x, 42, 1, 5, "#3a3a3a");
            // a weeping willow in the corner
            k.M.PileAt = new Vector2Int(-1, -1);
        }

        static void Wagon(Ctx k)
        {
            var bg = k.Bg; var tg = k.Tg; bool W = k.Winter; int y0 = k.Y(0);
            bg.Ellipse(16, y0 + 26, 15, 5, PixelCanvas.C(W ? "#d8d2c6" : "#a08452"));
            Shadow(tg, 3, y0 + 24, 26);
            // canvas cover: three hoops
            for (int r = 0; r < 12; r++)
            {
                int hw = Mathf.RoundToInt(Mathf.Sqrt(Mathf.Max(0, 1 - Mathf.Pow((11 - r) / 12f, 2))) * 12);
                tg.R(16 - hw, y0 + 2 + r, hw * 2, 1, r < 2 ? "#fffaf0" : "#efe6d2");
            }
            tg.R(4, y0 + 13, 24, 1, "#d6ccb6");
            for (int x = 8; x < 26; x += 6) tg.R(x, y0 + 3, 1, 11, "#d6ccb6");
            if (W) tg.R(8, y0 + 2, 16, 2, SNOW);
            // wooden bed
            tg.R(3, y0 + 14, 26, 6, "#8a5a34"); tg.R(3, y0 + 14, 26, 1, "#b8844a"); tg.R(3, y0 + 17, 26, 1, "#6e4428");
            // wheels
            foreach (int wx in new[] { 8, 24 })
            {
                tg.Disc(wx, y0 + 22, 4.5f, "#5a3a22"); tg.Disc(wx, y0 + 22, 3.2f, "#b8844a"); tg.Disc(wx, y0 + 22, 1.2f, "#5a3a22");
                tg.Line(wx - 3, y0 + 22, wx + 3, y0 + 22, "#5a3a22"); tg.Line(wx, y0 + 19, wx, y0 + 25, "#5a3a22");
            }
            // tongue
            tg.Line(28, y0 + 18, 31, y0 + 21, "#6e4428");
            Crate(tg, 0, y0 + 24, 5); Barrel(tg, 27, y0 + 24);
            k.M.PileAt = new Vector2Int(1, y0 + 31);
        }

        // ================================================================== construction sites
        static readonly Dictionary<int, Sprite[]> siteCache = new Dictionary<int, Sprite[]>();

        /// <summary>A construction site: stone footings, timber scaffolding and the building rising
        /// from the ground with progress. Returns {base, top}.</summary>
        public static Sprite[] Site(BuildingType type, int variant, int season, int stage)
        {
            int key = (((int)type * 16 + variant) * 4 + season) * 8 + stage;
            Sprite[] s;
            if (siteCache.TryGetValue(key, out s)) return s;
            var full = Get(type, variant, season);
            var d = Defs.Get(type);
            int W = full.W, H = full.H, L = full.Lift;
            var bg = new PixelCanvas(W, H);
            bool winter = season == 3;
            // trampled ground and footings
            bg.R(1, L + 1, W - 2, d.H * 16 - 2, winter ? "#d8d2c6" : "#9e7a4d");
            for (int i = 0; i < W * d.H / 2; i++) bg.D(1 + (int)(Hs(i, (int)type, 3) * (W - 2)), L + 1 + (int)(Hs((int)type, i, 4) * (d.H * 16 - 2)), winter ? "#c4beb2" : "#8a6a42");
            for (int x = 1; x < W - 1; x += 3) { bg.D(x, L + 1, "#8d877d"); bg.D(x, L + d.H * 16 - 2, "#8d877d"); }
            for (int y = L + 1; y < L + d.H * 16 - 1; y += 3) { bg.D(1, y, "#8d877d"); bg.D(W - 2, y, "#8d877d"); }
            // the building rising from the bottom
            var tg = new PixelCanvas(W, H);
            float frac = stage / 4f;
            int topVisible = Mathf.RoundToInt(H - (H * frac));
            if (stage > 0)
            {
                for (int y = topVisible; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        var c = full.TopCanvas.Get(x, y);
                        if (c.a == 0) continue;
                        // unfinished walls show as bare timber
                        if (stage < 4 && y < topVisible + 3) c = PixelCanvas.C("#c49a62");
                        tg.Set(x, y, c);
                    }
            }
            // scaffolding poles and planks around the frame
            if (stage < 4)
            {
                int top = Mathf.Max(0, topVisible - 6);
                for (int x = 2; x < W - 2; x += 10) tg.R(x, top, 1, H - top - 1, "#a8734a");
                tg.R(2, top, W - 4, 1, "#a8734a");
                for (int y = top + 8; y < H - 2; y += 8) tg.R(2, y, W - 4, 1, "#8a5a34");
                tg.Line(2, H - 2, Mathf.Min(W - 3, 12), top, "#8a5a34");
            }
            tg.Outline(OL);
            s = new[] { bg.ToSprite("site_base" + key), tg.ToSprite("site_top" + key) };
            siteCache[key] = s;
            return s;
        }
    }
}
