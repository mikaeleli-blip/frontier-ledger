using System.Collections.Generic;
using FrontierLedger.Simulation;
using UnityEngine;

namespace FrontierLedger.Game.Art
{
    /// <summary>Everything the renderer needs to show one building in one season.</summary>
    public sealed class BuildingSprites
    {
        public Sprite[] Base = new Sprite[2];  // ground layer (gardens, fields, lawns), two crop-sway frames
        public Sprite Top;                     // the building itself, y-sorted with people and trees
        public Sprite Lights;                  // lit windows, faded in at night
        public PixelCanvas TopCanvas;          // kept for pixel-accurate click picking
        public int Lift;                       // pixels the sprite rises above its footprint
        public int W, H;
        public readonly List<Vector2> Smoke = new List<Vector2>();       // chimney tops, sprite pixels
        public readonly List<Vector2Int> Chickens = new List<Vector2Int>();
        public RectInt Forge;
        public bool HasForge;
    }

    /// <summary>
    /// Procedural 3/4-view pixel art for every building, ported from the HTML prototype.
    /// Sprites are generated once per (type, variant, season) and cached.
    /// </summary>
    public static class BuildingArt
    {
        const string OL = "#2a1b14", SNOW = "#f2f6f8", SNOW2 = "#cfdde6";

        public static int LiftOf(BuildingType t)
        {
            switch (t)
            {
                case BuildingType.Store: return 8;
                case BuildingType.Stall: return 4;
                case BuildingType.Smithy: return 10;
                case BuildingType.Saloon: return 12;
                case BuildingType.Sheriff: return 10;
                case BuildingType.Estate: return 16;
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

        // ------------------------------------------------------------------ kit
        sealed class Ctx
        {
            public PixelCanvas Bg, Tg;
            public BuildingSprites M;
            public int Si, V, L;
            public List<RectInt> Wins = new List<RectInt>();
            public List<RectInt> Glow = new List<RectInt>();
            public List<CropRow> Crops = new List<CropRow>();
            public bool Winter { get { return Si == 3; } }
            public int Y(int f) { return f + L; }
        }
        struct CropRow { public int X, Y, W; public bool Field; }

        static float Hs(int x, int y) { return Noise.Hash(x, y, 99); }

        static void Roof(PixelCanvas g, int x, int y, int w, int h, string[] c, bool winter)
        {
            g.R(x, y, w, h, OL); g.R(x + 1, y + 1, w - 2, h - 2, c[0]);
            for (int r = 0; r < h - 2; r += 2)
            {
                int yy = y + 1 + r;
                g.R(x + 1, yy + 1, w - 2, 1, c[1]);
                int off = (r / 2) % 2 == 1 ? 1 : 3;
                for (int xx = x + 1 + off; xx < x + w - 1; xx += 4) g.D(xx, yy, c[1]);
            }
            g.R(x + 1, y + 1, w - 2, 1, c[2]);
            if (winter)
            {
                g.R(x + 1, y + 1, w - 2, 3, SNOW);
                for (int xx = x + 1; xx < x + w - 1; xx++) if (Hs(xx, y) < 0.45f) g.D(xx, y + 4, SNOW2);
            }
        }
        static void Wall(PixelCanvas g, int x, int y, int w, int h, string[] c)
        {
            g.R(x, y, w, h, OL); g.R(x + 1, y, w - 2, h - 1, c[0]);
            for (int yy = y + 2; yy < y + h - 1; yy += 3) g.R(x + 1, yy, w - 2, 1, c[1]);
            g.R(x + 1, y, w - 2, 1, c[1]);
        }
        static void Win(Ctx k, int x, int y, int w, int h, string frame)
        {
            var g = k.Tg;
            g.R(x - 1, y - 1, w + 2, h + 2, frame); g.R(x, y, w, h, "#2c3a48"); g.D(x, y, "#8fb4c6");
            if (w >= 4) g.R(x + (w >> 1), y, 1, h, frame);
            if (h >= 5) g.R(x, y + (h >> 1), w, 1, frame);
            k.Wins.Add(new RectInt(x, y, w, h));
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
        static void Boardwalk(PixelCanvas g, int y, bool w)
        {
            g.R(0, y, 32, 4, w ? "#d8d2c6" : "#b98b5a");
            for (int x = 3; x < 32; x += 4) g.R(x, y, 1, 4, w ? "#bdb5a5" : "#8a5d3a");
            g.R(0, y + 3, 32, 1, w ? "#a8a090" : "#6b4428");
        }

        // ------------------------------------------------------------------ build
        static BuildingSprites Make(BuildingType type, int v, int si)
        {
            var d = Defs.Get(type);
            int L = LiftOf(type), W = d.W * 16, H = d.H * 16 + L;
            var m = new BuildingSprites { Lift = L, W = W, H = H };
            var k = new Ctx { Bg = new PixelCanvas(W, H), Tg = new PixelCanvas(W, H), M = m, Si = si, V = v, L = L };
            switch (type)
            {
                case BuildingType.House: House(k); break;
                case BuildingType.Farm: Farm(k); break;
                case BuildingType.Store: Store(k); break;
                case BuildingType.Stall: Stall(k); break;
                case BuildingType.Smithy: Smithy(k); break;
                case BuildingType.Saloon: Saloon(k); break;
                case BuildingType.Sheriff: Sheriff(k); break;
                case BuildingType.Estate: Estate(k); break;
            }
            string n = type + "_" + v + "_" + si;
            for (int f = 0; f < 2; f++)
            {
                var b = new PixelCanvas(W, H);
                b.Draw(k.Bg, 0, 0);
                foreach (var row in k.Crops) DrawCrops(b, row, si, f);
                m.Base[f] = b.ToSprite(n + "_base" + f);
            }
            m.Top = k.Tg.ToSprite(n + "_top");
            m.TopCanvas = k.Tg;
            var lights = new PixelCanvas(W, H);
            var halo = PixelCanvas.C("#ffbe5a", 60);
            var core = PixelCanvas.C("#ffcf6a");
            var rects = new List<RectInt>(k.Wins); rects.AddRange(k.Glow);
            foreach (var r in rects) lights.RBlend(r.x - 2, r.y - 2, r.width + 4, r.height + 4, halo);
            foreach (var r in rects) lights.R(r.x, r.y, r.width, r.height, core);
            if (m.HasForge) lights.RBlend(m.Forge.x - 4, m.Forge.y - 4, m.Forge.width + 8, m.Forge.height + 8, PixelCanvas.C("#ff8c32", 80));
            m.Lights = lights.ToSprite(n + "_lights");
            return m;
        }

        static void DrawCrops(PixelCanvas g, CropRow r, int si, int frame)
        {
            int step = r.Field ? 2 : 3;
            for (int i = 0; i < r.W; i += step)
            {
                int x = r.X + i, y = r.Y;
                float h = Noise.Hash(x, y, 4);
                int sw = Mathf.RoundToInt(Mathf.Sin(frame * Mathf.PI - x * 0.25f + y * 0.1f + 0.8f) * 0.7f);
                if (si == 3) { if (h < 0.3f) g.D(x, y, "#8a6a4a"); continue; }
                if (si == 0) { g.D(x, y, "#5f9a3a"); if (h < 0.55f) g.D(x + sw, y - 1, "#8ccc5a"); continue; }
                if (!r.Field)
                {
                    if (si == 1) { g.R(x, y - 1, 2, 2, "#3f7a32"); g.D(x + sw, y - 2, "#5f9a42"); if (h < 0.2f) g.D(x + 1, y, "#d9534a"); }
                    else { g.R(x, y - 1, 2, 2, "#7a7a32"); if (h < 0.4f) g.R(x, y, 2, 1, "#d8742a"); }
                }
                else
                {
                    if (si == 1) { g.R(x, y - 2, 1, 3, "#3f7a32"); g.D(x + sw, y - 3, "#7fb04a"); }
                    else { g.R(x, y - 2, 1, 3, "#c9a03a"); g.D(x + sw, y - 3, "#ecc95a"); }
                }
            }
        }

        // ------------------------------------------------------------------ buildings
        static void House(Ctx k)
        {
            var bg = k.Bg; var tg = k.Tg; bool W = k.Winter; int v = k.V;
            string[][] walls = { new[] { "#a7744a", "#8a5d3a" }, new[] { "#ddd2bb", "#bfb39a" }, new[] { "#8d9fa8", "#71838c" } };
            string[][] roofs = { new[] { "#9c3b2e", "#7a2a22", "#bf5a45" }, new[] { "#6b5240", "#523d30", "#86694f" }, new[] { "#56684a", "#44543a", "#6f8460" } };
            // garden behind the house
            bg.R(3, 2, 26, 12, W ? SNOW : "#6b4a2e");
            if (!W) foreach (int fy in new[] { 4, 7, 10 }) bg.R(4, fy + 1, 24, 1, "#553a24");
            else for (int i = 0; i < 26; i++) bg.D(3 + (int)(Noise.Hash(i, v, 1) * 26), 2 + (int)(Noise.Hash(v, i, 2) * 12), "#9a8466");
            const string fc = "#8a5d3a", fp = "#b48858";
            bg.R(1, 1, 30, 1, fc); bg.R(1, 3, 30, 1, fc); bg.R(1, 1, 1, 14, fc); bg.R(30, 1, 1, 14, fc);
            for (int x = 1; x < 31; x += 3) { bg.R(x, 0, 1, 4, fp); bg.D(x, 0, "#5a3a22"); }
            if (W) for (int x = 1; x < 31; x += 3) bg.D(x, 0, SNOW);
            bg.R(14, 31, 4, 1, "#b8a07a");
            k.Crops.Add(new CropRow { X = 4, Y = 5, W = 24 });
            k.Crops.Add(new CropRow { X = 4, Y = 8, W = 24 });
            k.Crops.Add(new CropRow { X = 4, Y = 11, W = 24 });
            Roof(tg, 3, 13, 26, 11, roofs[v], W);
            tg.R(21, 8, 4, 7, OL); tg.R(22, 9, 2, 6, "#9a4a36"); tg.D(22, 11, "#7a3426"); tg.D(23, 13, "#7a3426"); if (W) tg.R(21, 8, 4, 1, SNOW);
            k.M.Smoke.Add(new Vector2(22.5f, 7));
            Wall(tg, 5, 24, 22, 8, walls[v]);
            Win(k, 8, 26, 3, 3, "#efe6d2"); Win(k, 21, 26, 3, 3, "#efe6d2");
            tg.R(14, 25, 4, 6, OL); tg.R(15, 26, 2, 5, "#6b4428"); tg.D(16, 28, "#e2b44a");
        }

        static void Farm(Ctx k)
        {
            var bg = k.Bg; var tg = k.Tg; bool W = k.Winter;
            bg.R(1, 1, 46, 46, W ? SNOW : "#6b4a2e");
            if (!W) for (int fy = 5; fy < 46; fy += 4) bg.R(2, fy, 44, 1, "#553a24");
            else for (int i = 0; i < 70; i++) bg.D(2 + (int)(Noise.Hash(i, 3, 1) * 44), 2 + (int)(Noise.Hash(3, i, 2) * 44), "#9a8466");
            bg.R(21, 31, 26, 16, W ? "#dfe5e6" : "#9e7a4d");
            for (int i = 0; i < 30; i++) bg.D(21 + (int)(Noise.Hash(i, 5, 3) * 26), 31 + (int)(Noise.Hash(5, i, 4) * 16), W ? "#c9d4da" : "#8a6a42");
            const string fc = "#7a5232", fp = "#a8784c";
            bg.R(0, 0, 48, 1, fc); bg.R(0, 2, 48, 1, fc); bg.R(0, 0, 1, 48, fc); bg.R(47, 0, 1, 48, fc); bg.R(0, 46, 21, 1, fc);
            for (int x = 0; x < 48; x += 6) bg.R(x, 0, 1, 4, fp);
            for (int y = 0; y < 48; y += 6) { bg.R(0, y, 1, 2, fp); bg.R(47, y, 1, 2, fp); }
            for (int fy = 4; fy <= 44; fy += 4) k.Crops.Add(new CropRow { X = 3, Y = fy, W = fy >= 30 ? 16 : 43, Field = true });
            k.M.Chickens.Add(new Vector2Int(23, 40)); k.M.Chickens.Add(new Vector2Int(26, 43)); k.M.Chickens.Add(new Vector2Int(24, 46));

            string[] red = { "#a8402e", "#8a3224" }, rf = { "#4a3a32", "#3a2c26", "#6a5646" };
            for (int x = 28; x <= 47; x++)
            {
                int e = 25 + (x <= 37 ? 37 - x : x - 38), top = e - 10;
                if (x == 28 || x == 47) { tg.R(x, top, 1, 11, OL); continue; }
                tg.R(x, top, 1, e - top, x <= 37 ? rf[2] : rf[1]);
                for (int y = top + 2; y < e; y += 2) tg.D(x, y, rf[0]);
                tg.D(x, top, OL);
                if (W) tg.R(x, top + 1, 1, 3, SNOW);
            }
            if (!W) tg.R(37, 15, 1, 10, "#7d6858");
            for (int r = 0; r <= 8; r++)
            {
                int y = 25 + r, x0 = 37 - r, x1 = 38 + r;
                tg.R(x0, y, x1 - x0 + 1, 1, red[0]);
                for (int x = x0 + 1; x < x1; x++) if ((x - 30) % 3 == 0) tg.D(x, y, red[1]);
                tg.D(x0, y, rf[1]); tg.D(x1, y, rf[1]); tg.D(x0 - 1, y, OL); tg.D(x1 + 1, y, OL);
            }
            tg.R(29, 33, 18, 1, "#efe6d2");
            tg.R(29, 34, 18, 14, OL); tg.R(30, 34, 16, 13, red[0]);
            for (int x = 32; x < 46; x += 3) tg.R(x, 34, 1, 13, red[1]);
            tg.R(33, 38, 10, 9, "#efe6d2"); tg.R(34, 39, 8, 8, "#7a2a1e");
            for (int i = 0; i < 8; i++) { tg.D(34 + i, 39 + i, "#efe6d2"); tg.D(41 - i, 39 + i, "#efe6d2"); }
            tg.R(36, 27, 4, 4, "#efe6d2"); tg.R(37, 28, 2, 2, "#2a1b14"); tg.D(37, 29, "#d9b85a");
            if (W) tg.R(30, 46, 16, 1, SNOW);
            tg.R(22, 32, 6, 4, OL); tg.R(23, 32, 4, 3, "#d9b85a"); tg.R(23, 33, 4, 1, "#b8943a"); if (W) tg.R(23, 32, 4, 1, SNOW);
        }

        static void Store(Ctx k)
        {
            var bg = k.Bg; var tg = k.Tg; bool W = k.Winter;
            string[] fc = { "#c9b896", "#b3a280" };
            Boardwalk(bg, k.Y(28), W);
            Roof(tg, 2, k.Y(-6), 28, 10, new[] { "#6e6a64", "#58544f", "#85807a" }, W);
            tg.R(0, k.Y(-1), 32, 30, OL); tg.R(1, k.Y(0), 30, 28, fc[0]);
            for (int x = 4; x < 31; x += 4) tg.R(x, k.Y(0), 1, 14, fc[1]);
            tg.R(0, k.Y(-1), 32, 2, "#2f5d7a"); tg.R(0, k.Y(1), 32, 1, "#23465c");
            for (int x = 2; x < 31; x += 5) tg.D(x, k.Y(2), "#23465c");
            if (W) tg.R(0, k.Y(-2), 32, 1, SNOW);
            tg.R(4, k.Y(4), 24, 7, OL); tg.R(5, k.Y(5), 22, 5, "#efe1c4"); Letters(tg, 7, k.Y(6), 18, "#2f3a46");
            tg.R(0, k.Y(14), 32, 1, OL);
            for (int x = 0; x < 32; x++) tg.R(x, k.Y(15), 1, 3, ((x / 3) % 2 == 1) ? "#efe1c4" : "#2f5d7a");
            for (int x = 0; x < 32; x += 2) tg.D(x, k.Y(18), "#23465c");
            if (W) tg.R(0, k.Y(14), 32, 1, SNOW);
            Win(k, 4, k.Y(21), 7, 5, "#2f5d7a"); Win(k, 21, k.Y(21), 7, 5, "#2f5d7a");
            string[] gc = { "#d9534a", "#f2cf4a", "#7fb05a", "#e8dcc0" };
            for (int i = 0; i < 5; i++) { tg.D(5 + i + (i > 2 ? 1 : 0), k.Y(25), gc[i % 4]); tg.D(22 + i + (i > 2 ? 1 : 0), k.Y(25), gc[(i + 2) % 4]); }
            tg.R(13, k.Y(19), 6, 9, OL); tg.R(14, k.Y(20), 4, 8, "#5a3a22"); tg.R(14, k.Y(20), 4, 3, "#2c3a48"); tg.D(17, k.Y(24), "#e2b44a");
            tg.R(0, k.Y(18), 2, 10, "#8a5d3a"); tg.R(30, k.Y(18), 2, 10, "#8a5d3a");
            tg.R(25, k.Y(24), 4, 5, OL); tg.R(26, k.Y(24), 2, 5, "#8a5a34"); tg.R(26, k.Y(25), 2, 1, "#4a3a2e"); tg.R(26, k.Y(27), 2, 1, "#4a3a2e");
            tg.R(2, k.Y(25), 4, 3, "#b98b5a"); tg.D(3, k.Y(26), "#8a5d3a"); tg.D(4, k.Y(26), "#8a5d3a");
        }

        static void Stall(Ctx k)
        {
            var bg = k.Bg; var tg = k.Tg; bool W = k.Winter;
            bg.R(0, k.Y(9), 16, 7, W ? "#dfe5e6" : "#9e7a4d");
            tg.R(2, k.Y(0), 1, 12, "#6b4428"); tg.R(13, k.Y(0), 1, 12, "#6b4428");
            tg.R(0, k.Y(-4), 16, 7, OL);
            for (int x = 1; x < 15; x++)
            {
                string c = ((x - 1) / 2) % 2 == 1 ? "#efe1c4" : "#b5432f";
                tg.R(x, k.Y(-3), 1, 5, c);
                if (x % 2 == 1) tg.D(x, k.Y(2), c);
            }
            if (W) tg.R(1, k.Y(-3), 14, 2, SNOW);
            tg.R(1, k.Y(8), 14, 4, OL); tg.R(2, k.Y(8), 12, 1, "#b98b5a"); tg.R(2, k.Y(9), 12, 2, "#8a5a34");
            string[] pc = { "#d9534a", "#f2cf4a", "#7fb05a", "#d8742a" };
            for (int x = 2; x < 14; x++)
            {
                if (!W || x % 3 != 0) tg.D(x, k.Y(7), pc[x % 4]);
                if (x % 2 == 1 && !W) tg.D(x, k.Y(6), pc[(x + 1) % 4]);
            }
            tg.R(3, k.Y(12), 4, 3, "#b98b5a"); tg.R(9, k.Y(12), 4, 3, "#b98b5a"); tg.D(4, k.Y(13), "#8a5d3a"); tg.D(10, k.Y(13), "#8a5d3a");
        }

        static void Smithy(Ctx k)
        {
            var bg = k.Bg; var tg = k.Tg; bool W = k.Winter;
            bg.R(0, k.Y(28), 32, 4, W ? "#dfe5e6" : "#8a7458");
            Roof(tg, 1, k.Y(2), 30, 14, new[] { "#4a4f57", "#3a3e45", "#5d636c" }, W);
            tg.R(23, k.Y(-9), 6, 13, OL); tg.R(24, k.Y(-8), 4, 12, "#7d776f");
            for (int r = 0; r < 12; r += 3) tg.R(24, k.Y(-8) + r, 4, 1, "#5f5a54");
            if (W) tg.R(23, k.Y(-9), 6, 1, SNOW);
            k.M.Smoke.Add(new Vector2(26, k.Y(-10)));
            tg.R(1, k.Y(15), 30, 15, OL); tg.R(2, k.Y(16), 28, 13, "#8d877d");
            for (int r = 0; r < 13; r += 3)
            {
                tg.R(2, k.Y(16) + r, 28, 1, "#6c665e");
                for (int x = 2 + ((r / 3) % 2 == 1 ? 3 : 0); x < 30; x += 6) tg.R(x, k.Y(16) + r, 1, 3, "#6c665e");
            }
            tg.R(1, k.Y(15), 30, 2, "#5a3a22"); tg.R(11, k.Y(15), 3, 1, "#c8c8c8");
            tg.R(4, k.Y(18), 16, 12, OL); tg.R(5, k.Y(19), 14, 11, "#1e1814");
            tg.R(6, k.Y(23), 6, 6, "#6c665e"); tg.R(6, k.Y(23), 6, 1, "#8d877d"); tg.R(7, k.Y(24), 4, 3, "#3a1a10");
            k.M.Forge = new RectInt(7, k.Y(24), 4, 3); k.M.HasForge = true;
            tg.D(14, k.Y(20), "#9a9a9a"); tg.D(16, k.Y(20), "#9a9a9a"); tg.R(14, k.Y(21), 1, 2, "#6b4428"); tg.R(16, k.Y(21), 1, 2, "#6b4428");
            tg.R(13, k.Y(26), 6, 1, "#4a4a50"); tg.D(13, k.Y(26), "#9a9aa2"); tg.R(14, k.Y(27), 4, 1, "#3a3a3e"); tg.R(15, k.Y(28), 2, 1, "#3a3a3e"); tg.R(14, k.Y(29), 4, 1, "#3a3a3e");
            Win(k, 23, k.Y(19), 4, 4, "#5a3a22");
            tg.R(23, k.Y(25), 5, 5, OL); tg.R(24, k.Y(25), 3, 5, "#8a5a34"); tg.R(24, k.Y(26), 3, 1, "#4a3a2e"); tg.R(24, k.Y(28), 3, 1, "#4a3a2e");
            tg.R(24, k.Y(25), 3, 1, W ? "#d6e6ee" : "#4f8aa8");
        }

        static void Saloon(Ctx k)
        {
            var bg = k.Bg; var tg = k.Tg; bool W = k.Winter;
            string[] wc = { "#7a4b2a", "#663d22" };
            Boardwalk(bg, k.Y(28), W);
            bg.R(22, k.Y(30), 8, 1, "#6b4428"); bg.D(22, k.Y(29), "#6b4428"); bg.D(29, k.Y(29), "#6b4428");
            Roof(tg, 2, k.Y(-8), 28, 10, new[] { "#5b4636", "#45352a", "#74604d" }, W);
            tg.R(0, k.Y(-6), 32, 35, OL); tg.R(10, k.Y(-11), 12, 6, OL);
            tg.R(1, k.Y(-5), 30, 33, wc[0]); tg.R(11, k.Y(-10), 10, 6, wc[0]);
            for (int x = 3; x < 31; x += 3) tg.R(x, k.Y(-5), 1, 33, wc[1]);
            tg.R(1, k.Y(-4), 30, 1, "#c9a46a"); tg.R(11, k.Y(-9), 10, 1, "#c9a46a");
            if (W) { tg.R(0, k.Y(-7), 32, 1, SNOW); tg.R(10, k.Y(-12), 12, 1, SNOW); }
            tg.R(3, k.Y(-2), 26, 6, OL); tg.R(4, k.Y(-1), 24, 4, "#d8a84a"); Letters(tg, 6, k.Y(0), 20, "#3a2414");
            Win(k, 6, k.Y(5), 4, 5, "#c9a46a"); Win(k, 22, k.Y(5), 4, 5, "#c9a46a");
            tg.R(0, k.Y(11), 32, 1, "#c9a46a");
            for (int x = 1; x < 32; x += 2) tg.R(x, k.Y(12), 1, 2, "#c9a46a");
            tg.R(0, k.Y(14), 32, 2, "#3a2414");
            if (W) tg.R(0, k.Y(10), 32, 1, SNOW);
            tg.R(0, k.Y(16), 2, 12, "#4b2e1a"); tg.R(30, k.Y(16), 2, 12, "#4b2e1a");
            Win(k, 4, k.Y(19), 5, 5, "#c9a46a"); Win(k, 23, k.Y(19), 5, 5, "#c9a46a");
            tg.R(11, k.Y(17), 10, 11, OL); tg.R(12, k.Y(18), 8, 10, "#1e1410");
            k.Glow.Add(new RectInt(12, k.Y(18), 8, 3));
            tg.R(12, k.Y(21), 3, 5, "#a8734a"); tg.R(17, k.Y(21), 3, 5, "#a8734a"); tg.R(12, k.Y(21), 3, 1, "#c49364"); tg.R(17, k.Y(21), 3, 1, "#c49364");
        }

        static void Sheriff(Ctx k)
        {
            var bg = k.Bg; var tg = k.Tg; bool W = k.Winter;
            Boardwalk(bg, k.Y(28), W);
            tg.R(2, k.Y(-6), 28, 12, OL); tg.R(3, k.Y(-5), 26, 10, W ? SNOW : "#5d574f"); tg.R(3, k.Y(-5), 26, 1, W ? SNOW2 : "#8a8276");
            if (!W) for (int i = 0; i < 14; i++) tg.D(3 + (int)(Noise.Hash(i, 1, 5) * 26), k.Y(-4) + (int)(Noise.Hash(1, i, 6) * 9), "#4f4a43");
            tg.R(27, k.Y(-10), 1, 9, "#c8c8c8"); tg.R(22, k.Y(-10), 5, 3, "#efe1c4"); tg.R(22, k.Y(-10), 5, 1, "#b5432f"); tg.R(22, k.Y(-8), 5, 1, "#b5432f"); tg.R(22, k.Y(-10), 2, 2, "#2f5d7a");
            tg.R(0, k.Y(3), 32, 26, OL); tg.R(1, k.Y(4), 30, 24, "#b6a68a");
            for (int r = 0; r < 24; r += 3)
            {
                tg.R(1, k.Y(4) + r, 30, 1, "#9c8b74");
                for (int x = 1 + ((r / 3) % 2 == 1 ? 2 : 5); x < 31; x += 6) tg.R(x, k.Y(4) + r, 1, 3, "#9c8b74");
            }
            tg.R(0, k.Y(3), 32, 1, "#6f6252"); if (W) tg.R(0, k.Y(2), 32, 1, SNOW);
            tg.R(5, k.Y(5), 22, 7, OL); tg.R(6, k.Y(6), 20, 5, "#3a2c22"); Star5(tg, 14, k.Y(6), "#e6c15a");
            Letters(tg, 7, k.Y(7), 6, "#e6c15a"); Letters(tg, 20, k.Y(7), 5, "#e6c15a");
            tg.R(0, k.Y(13), 32, 2, "#6b4428"); tg.R(0, k.Y(15), 32, 1, OL); if (W) tg.R(0, k.Y(13), 32, 1, SNOW);
            foreach (int x in new[] { 4, 22 })
            {
                tg.R(x - 1, k.Y(17), 8, 7, "#6f6252"); tg.R(x, k.Y(18), 6, 5, "#2c3a48");
                k.Wins.Add(new RectInt(x, k.Y(18), 6, 5));
                for (int i = x + 1; i < x + 6; i += 2) tg.R(i, k.Y(18), 1, 5, "#9a9a9a");
            }
            tg.R(12, k.Y(17), 8, 11, OL); tg.R(13, k.Y(18), 6, 10, "#5a3a22"); tg.R(15, k.Y(18), 2, 10, "#4a2f1e"); tg.D(18, k.Y(23), "#e2b44a");
            tg.R(9, k.Y(18), 2, 3, "#efe1c4"); tg.D(9, k.Y(19), "#6b4428");
        }

        static void Estate(Ctx k)
        {
            var bg = k.Bg; var tg = k.Tg; bool W = k.Winter; int si = k.Si;
            string[][] lawns = { new[] { "#7fae55", "#72a04c" }, new[] { "#6f9e48", "#64903f" }, new[] { "#a6a052", "#979348" }, new[] { SNOW, "#e4ecf0" } };
            var lawn = lawns[si];
            bg.R(0, k.Y(0), 48, 32, lawn[0]);
            for (int x = 0; x < 48; x += 4) bg.R(x, k.Y(0), 2, 32, lawn[1]);
            bg.R(20, k.Y(26), 8, 6, W ? "#d8d2c6" : "#c8b89a");
            for (int y = k.Y(27); y < k.Y(32); y += 2) bg.D(21 + (y % 3), y, "#a89a7a");
            foreach (var span in new[] { new[] { 0, 19 }, new[] { 29, 48 } })
                for (int x = span[0]; x < span[1] - 1; x += 3)
                {
                    bg.R(x, k.Y(28), 4, 3, W ? "#dfe7ea" : "#2f5d3a");
                    bg.D(x + 1, k.Y(28), W ? SNOW : "#457a4f");
                    if (!W && si < 2 && Noise.Hash(x, 1, 8) < 0.5f) bg.D(x + 2, k.Y(29), new[] { "#e8566a", "#f2cf4a", "#f4f1e6" }[x % 3]);
                }
            string[] rc = { "#3f5470", "#2e3f57", "#566f8f" };
            Roof(tg, 4, k.Y(-10), 40, 19, rc, W);
            for (int i = 0; i < 6; i++)
            {
                tg.Clear(4, k.Y(-10) + i, 6 - i, 1); tg.Clear(38 + i, k.Y(-10) + i, 6 - i, 1);
                tg.D(10 - i, k.Y(-10) + i, OL); tg.D(37 + i, k.Y(-10) + i, OL);
            }
            foreach (int dx in new[] { 13, 30 })
            {
                tg.R(dx, k.Y(-5), 6, 7, OL); tg.R(dx + 1, k.Y(-4), 4, 5, "#ece4d2");
                Win(k, dx + 2, k.Y(-2), 2, 3, "#ece4d2");
                tg.R(dx - 1, k.Y(-6), 8, 2, rc[1]); if (W) tg.R(dx - 1, k.Y(-7), 8, 1, SNOW);
            }
            foreach (int cx in new[] { 7, 37 })
            {
                tg.R(cx, k.Y(-15), 4, 8, OL); tg.R(cx + 1, k.Y(-14), 2, 7, "#9a4a36"); if (W) tg.R(cx, k.Y(-15), 4, 1, SNOW);
                k.M.Smoke.Add(new Vector2(cx + 1.5f, k.Y(-16)));
            }
            tg.R(5, k.Y(8), 38, 18, OL); tg.R(6, k.Y(8), 36, 17, "#ece4d2");
            for (int y = k.Y(10); y < k.Y(25); y += 3) tg.R(6, y, 36, 1, "#d6ccb6");
            tg.R(6, k.Y(8), 36, 1, "#cfc4ad");
            foreach (int x in new[] { 9, 14, 33, 38 })
                foreach (int fy in new[] { 10, 17 })
                {
                    tg.R(x - 2, k.Y(fy), 1, 4, "#3f5a3a"); tg.R(x + 3, k.Y(fy), 1, 4, "#3f5a3a");
                    Win(k, x, k.Y(fy), 2, 4, "#f6f1e6");
                }
            for (int r = 0; r < 6; r++)
            {
                int y = k.Y(4) + r, x0 = 23 - r, x1 = 24 + r;
                tg.R(x0, y, x1 - x0 + 1, 1, "#f6f1e6"); tg.D(x0, y, OL); tg.D(x1, y, OL);
            }
            tg.R(17, k.Y(10), 14, 2, "#e8dfcc"); tg.R(17, k.Y(12), 14, 1, "#cfc4ad");
            foreach (int x in new[] { 18, 21, 25, 28 }) { tg.R(x, k.Y(12), 2, 13, "#f6f1e6"); tg.R(x + 1, k.Y(12), 1, 13, "#cfc4ad"); }
            tg.R(22, k.Y(16), 4, 9, OL); tg.R(23, k.Y(17), 2, 8, "#3a4a3a"); tg.R(22, k.Y(16), 4, 1, "#7fa6b8");
            tg.R(19, k.Y(25), 10, 1, "#bdb5a5"); tg.R(18, k.Y(26), 12, 1, "#a8a090");
        }
    }
}
