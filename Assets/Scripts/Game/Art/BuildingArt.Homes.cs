using FrontierLedger.Simulation;
using UnityEngine;

namespace FrontierLedger.Game.Art
{
    public static partial class BuildingArt
    {
        /// <summary>Brick wall: courses three pixels high, joints staggered, a lighter top edge.</summary>
        static void Bricks(PixelCanvas g, int x, int y, int w, int h, string brick, string mortar, string light)
        {
            g.R(x, y, w, h, brick);
            for (int r = 0; r < h; r += 3)
            {
                g.R(x, y + r, w, 1, mortar);
                for (int xx = x + ((r / 3) % 2 == 1 ? 2 : 5); xx < x + w; xx += 6) g.R(xx, y + r, 1, Mathf.Min(3, h - r), mortar);
                for (int xx = x; xx < x + w; xx += 7) if (Hs(xx, y + r, 21) < 0.3f) g.D(xx + 1, y + r + 1, light);
            }
        }

        /// <summary>A window with louvered shutters either side.</summary>
        static void Shuttered(Ctx k, int x, int y, int w, int h, string shutter, string frame)
        {
            var g = k.Tg;
            g.R(x - 3, y - 1, 2, h + 2, shutter); g.R(x + w + 1, y - 1, 2, h + 2, shutter);
            for (int r = y; r < y + h; r += 2) { g.D(x - 3, r, Lighter(shutter)); g.D(x + w + 1, r, Lighter(shutter)); }
            Window(k, x, y, w, h, false, frame);
        }

        static string Lighter(string hex, float f = 1.2f)
        {
            var c = PixelCanvas.C(hex);
            return "#" + Mathf.Min(255, (int)(c.r * f)).ToString("x2") + Mathf.Min(255, (int)(c.g * f)).ToString("x2") + Mathf.Min(255, (int)(c.b * f)).ToString("x2");
        }

        // ================================================================== large house
        /// <summary>A two-storey clapboard family house: a deep roof with a dormer, shuttered windows, a front porch on posts,
        /// and a fenced kitchen garden down the side.</summary>
        static void LargeHouse(Ctx k)
        {
            var bg = k.Bg; var tg = k.Tg; bool W = k.Winter; int y0 = k.Y(0);
            string[] wall = { "#e8cf94", "#c8ae72", "#f6e2b0" };
            string[] roof = { "#7a3428", "#5a2418", "#9a4a38" };
            const string trim = "#f4ecd8", shutter = "#3f6a4a";
            // the kitchen garden down the side, picket-fenced
            bg.R(35, y0 + 13, 12, 31, W ? SNOW : "#7a5232");
            if (!W) for (int fy = y0 + 16; fy < y0 + 43; fy += 3) bg.R(36, fy, 10, 1, "#5e3e24");
            else for (int i = 0; i < 20; i++) bg.D(35 + (int)(Hs(i, 3, 1) * 12), y0 + 13 + (int)(Hs(3, i, 2) * 31), "#a89478");
            for (int fy = y0 + 18; fy < y0 + 43; fy += 3) k.Crops.Add(new CropRow { X = 37, Y = fy, W = 9 });
            for (int x = 34; x < 48; x += 3) { bg.R(x, y0 + 11, 2, 3, "#e8dcc4"); bg.D(x, y0 + 11, "#c8b89a"); }
            for (int y = y0 + 11; y < y0 + 45; y += 3) { bg.R(46, y, 2, 3, "#e8dcc4"); bg.D(46, y, "#c8b89a"); }
            bg.R(34, y0 + 12, 13, 1, "#c8b89a"); bg.R(47, y0 + 11, 1, 34, "#c8b89a");
            for (int x = 37; x < 48; x += 3) { bg.R(x, y0 + 44, 2, 3, "#e8dcc4"); bg.D(x, y0 + 44, "#c8b89a"); }
            if (W) { for (int x = 34; x < 48; x += 3) bg.D(x, y0 + 11, SNOW); }
            // a path from the porch steps
            bg.R(13, y0 + 44, 8, 4, W ? "#d8d2c6" : "#c4a878");
            Shadow(tg, 2, y0 + 43, 32);
            // the roof, two chimneys and a dormer
            Shingles(tg, 0, y0 - 14, 35, 21, roof, W);
            tg.Clear(35, y0 - 14, 13, 21);   // the scallops overhang the eave
            Chimney(k, 4, y0 - 19, 9); Chimney(k, 26, y0 - 19, 9);
            tg.R(13, y0 - 6, 9, 9, wall[0]); tg.R(13, y0 - 6, 9, 1, wall[1]);
            for (int r = 0; r < 5; r++) tg.R(17 - r, y0 - 11 + r, 1 + 2 * r, 1, roof[r % 2 == 0 ? 0 : 1]);
            tg.R(12, y0 - 7, 11, 1, roof[1]);
            if (W) tg.R(13, y0 - 12, 9, 1, SNOW);
            Window(k, 15, y0 - 4, 5, 5, false, trim);
            // upper storey
            Planks(tg, 2, y0 + 7, 31, 14, wall[0], wall[1], wall[2]);
            tg.R(2, y0 + 7, 31, 1, wall[1]);
            tg.R(2, y0 + 7, 1, 35, trim); tg.R(32, y0 + 7, 1, 35, trim);
            foreach (int x in new[] { 6, 15, 25 }) Shuttered(k, x, y0 + 10, 4, 6, shutter, trim);
            // the porch roof, posts and railing
            tg.R(0, y0 + 21, 35, 3, "#6e5a4a"); tg.R(0, y0 + 21, 35, 1, "#8e7a68"); tg.R(0, y0 + 23, 35, 1, "#4e3e32");
            if (W) tg.R(0, y0 + 20, 35, 1, SNOW);
            // ground floor
            Planks(tg, 2, y0 + 24, 31, 18, wall[0], wall[1], wall[2]);
            Shuttered(k, 6, y0 + 28, 4, 7, shutter, trim); Shuttered(k, 25, y0 + 28, 4, 7, shutter, trim);
            Window(k, 14, y0 + 27, 6, 2, false, trim);   // fanlight
            Door(k, 14, y0 + 30, 6, 12, "#5a3a22");
            foreach (int x in new[] { 1, 11, 23, 33 }) { tg.R(x, y0 + 24, 1, 18, trim); tg.D(x, y0 + 24, "#ffffff"); }
            foreach (var span in new[] { new[] { 2, 11 }, new[] { 24, 33 } })
            {
                tg.R(span[0], y0 + 36, span[1] - span[0], 1, trim);
                for (int x = span[0] + 1; x < span[1]; x += 2) tg.R(x, y0 + 37, 1, 4, trim);
            }
            // a rocking chair on the porch
            tg.R(4, y0 + 38, 4, 1, "#8a5a34"); tg.R(7, y0 + 34, 1, 4, "#6e4428"); tg.R(3, y0 + 41, 6, 1, "#5a3a22");
            Foundation(tg, 2, y0 + 42, 31);
        }

        // ================================================================== manor
        /// <summary>A brick manor on a lawn: a hipped slate roof with dormers and tall chimneys, two storeys of tall windows,
        /// a white portico on columns, clipped hedges and an iron fence with a gate.</summary>
        static void Manor(Ctx k)
        {
            var bg = k.Bg; var tg = k.Tg; bool W = k.Winter; int si = k.Si; int y0 = k.Y(0);
            string[][] lawns = { new[] { "#7fbe55", "#72ae4c" }, new[] { "#6fae48", "#64a03f" }, new[] { "#a6a052", "#979348" }, new[] { SNOW, "#e4ecf0" } };
            var lawn = lawns[si];
            bg.R(0, y0, 64, 48, lawn[0]);
            for (int x = 0; x < 64; x += 4) bg.R(x, y0, 2, 48, lawn[1]);
            // the gravel drive up to the steps
            string gravel = W ? "#d8d2c6" : "#d8c8a0";
            bg.R(27, y0 + 36, 10, 12, gravel);
            for (int i = 0; i < 18; i++) bg.D(27 + (int)(Hs(i, 5, 3) * 10), y0 + 36 + (int)(Hs(5, i, 4) * 12), W ? "#c4beb2" : "#b8a880");
            // clipped hedges and flower beds either side of the drive
            foreach (var span in new[] { new[] { 2, 25 }, new[] { 39, 62 } })
                for (int x = span[0]; x < span[1] - 1; x += 3)
                {
                    bg.R(x, y0 + 39, 4, 4, W ? "#e4ecf0" : "#2f6a32");
                    bg.D(x + 1, y0 + 39, W ? SNOW : "#4a8a42");
                    if (!W && si < 2 && Noise.Hash(x, 2, 8) < 0.6f) bg.D(x + 2, y0 + 40, new[] { "#e85a6a", "#f2cf4a", "#f4f1e6", "#b05a82" }[x % 4]);
                }
            // an iron fence along the road, with stone gateposts
            for (int x = 0; x < 64; x++)
            {
                if (x >= 26 && x <= 37) continue;
                if (x % 2 == 0) { bg.R(x, y0 + 44, 1, 4, "#2a2a2e"); bg.D(x, y0 + 43, "#4a4a52"); }
            }
            bg.R(0, y0 + 45, 26, 1, "#2a2a2e"); bg.R(38, y0 + 45, 26, 1, "#2a2a2e");
            foreach (int x in new[] { 24, 37 }) { bg.R(x, y0 + 41, 3, 7, "#b8b0a0"); bg.R(x, y0 + 41, 3, 1, "#d8d0c0"); bg.R(x - 1, y0 + 40, 5, 1, "#8d877d"); if (W) bg.R(x - 1, y0 + 39, 5, 1, SNOW); }
            Shadow(tg, 4, y0 + 35, 56);
            // the hipped slate roof
            string[] slate = { "#4a5260", "#363c48", "#5e6878" };
            Shingles(tg, 2, y0 - 20, 60, 23, slate, W);
            tg.Clear(62, y0 - 20, 2, 23);
            for (int i = 0; i < 8; i++) { tg.Clear(2, y0 - 20 + i, 8 - i, 1); tg.Clear(54 + i, y0 - 20 + i, 8 - i, 1); }
            Chimney(k, 8, y0 - 25, 10); Chimney(k, 51, y0 - 25, 10);
            foreach (int dx in new[] { 15, 43 })
            {
                tg.R(dx, y0 - 11, 7, 8, "#f4ecdc"); tg.R(dx - 1, y0 - 12, 9, 2, slate[1]);
                Window(k, dx + 2, y0 - 9, 3, 4, false, "#f4ecdc");
                if (W) tg.R(dx - 1, y0 - 13, 9, 1, SNOW);
            }
            // two storeys of brick
            Bricks(tg, 4, y0 + 3, 56, 31, "#a24a32", "#7a3424", "#c06a4a");
            tg.R(3, y0 + 2, 58, 2, "#f4ecdc"); tg.R(3, y0 + 3, 58, 1, "#d6ccb6");
            tg.R(4, y0 + 18, 56, 1, "#e8dcc4");
            tg.R(4, y0 + 3, 2, 31, "#e8dcc4"); tg.R(58, y0 + 3, 2, 31, "#e8dcc4");   // quoins
            for (int y = y0 + 4; y < y0 + 34; y += 4) { tg.R(6, y, 1, 2, "#e8dcc4"); tg.R(57, y, 1, 2, "#e8dcc4"); }
            foreach (int x in new[] { 9, 16, 44, 51 })
            {
                tg.R(x - 1, y0 + 6, 6, 1, "#e8dcc4");                         // stone lintels
                Window(k, x, y0 + 8, 4, 7, false, "#ffffff");
                tg.R(x - 1, y0 + 21, 6, 1, "#e8dcc4");
                Window(k, x, y0 + 23, 4, 8, false, "#ffffff");
            }
            // the portico: pediment, columns, fanlit door
            for (int r = 0; r < 8; r++) tg.R(31 - r * 2, y0 + 2 + r, 2 + 4 * r, 1, "#fffaf0");
            tg.R(17, y0 + 10, 30, 2, "#e8dfcc"); tg.R(17, y0 + 12, 30, 1, "#c8bca4");
            tg.D(31, y0 + 6, "#c8a050"); tg.D(32, y0 + 6, "#c8a050");
            Window(k, 24, y0 + 14, 3, 5, false, "#ffffff"); Window(k, 37, y0 + 14, 3, 5, false, "#ffffff");
            Window(k, 29, y0 + 19, 6, 2, false, "#ffffff");
            Door(k, 29, y0 + 22, 6, 12, "#2e3a2e");
            tg.R(31, y0 + 23, 2, 10, "#1e281e");
            foreach (int x in new[] { 19, 23, 39, 43 }) { tg.R(x, y0 + 13, 2, 21, "#fffaf0"); tg.R(x + 1, y0 + 13, 1, 21, "#d6ccb6"); tg.R(x - 1, y0 + 13, 4, 1, "#e8dfcc"); }
            Foundation(tg, 4, y0 + 34, 56);
            tg.R(25, y0 + 34, 14, 1, "#d8d0c0"); tg.R(24, y0 + 35, 16, 1, "#c8c1b4"); tg.R(23, y0 + 36, 18, 1, "#a8a090");   // steps
            // urns with flowers by the steps
            foreach (int x in new[] { 20, 41 })
            {
                tg.R(x, y0 + 33, 3, 3, "#b8b0a0"); tg.R(x - 1, y0 + 32, 5, 1, "#a39c90");
                if (!W) { tg.R(x - 1, y0 + 30, 5, 2, "#3f7a3a"); tg.D(x, y0 + 30, si < 2 ? "#e85a6a" : "#e0a040"); tg.D(x + 2, y0 + 30, si < 2 ? "#f2cf4a" : "#c8642a"); }
                else tg.R(x - 1, y0 + 31, 5, 1, SNOW);
            }
        }
    }
}
