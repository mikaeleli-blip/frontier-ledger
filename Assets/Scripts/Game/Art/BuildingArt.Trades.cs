using FrontierLedger.Simulation;
using UnityEngine;

namespace FrontierLedger.Game.Art
{
    /// <summary>The trades: ranch, butcher, bakery, iron mine, tannery, brewery, tailor, hotel, barber and church.</summary>
    public static partial class BuildingArt
    {
        // ================================================================== food
        /// <summary>A red barn and a haystack on the left, a fenced paddock on the right where the herd grazes.</summary>
        static void Ranch(Ctx k)
        {
            var bg = k.Bg; var tg = k.Tg; bool W = k.Winter; int si = k.Si; int y0 = k.Y(0);
            // the paddock: grazed grass, a worn track to the barn
            string[] grazed = { "#86b452", "#78a84a", "#a8a052", SNOW };
            bg.R(27, y0 + 2, 36, 44, grazed[si]);
            for (int i = 0; i < 70; i++) bg.D(27 + (int)(Hs(i, 51, 3) * 36), y0 + 2 + (int)(Hs(51, i, 4) * 44), W ? "#dfe8ec" : si == 2 ? "#968e48" : "#6a9a40");
            bg.R(0, y0 + 32, 27, 16, W ? "#d8d2c6" : "#a08452");
            for (int i = 0; i < 24; i++) bg.D((int)(Hs(i, 52, 3) * 27), y0 + 32 + (int)(Hs(52, i, 4) * 16), W ? "#c4beb2" : "#8a6e44");
            // split-rail fence round the paddock, a gate by the barn
            for (int x = 26; x < 64; x += 6) { bg.R(x, y0 + 1, 2, 4, "#8a5a34"); bg.R(x, y0 + 43, 2, 4, "#8a5a34"); }
            for (int y = y0 + 1; y < y0 + 47; y += 6) { bg.R(62, y, 2, 4, "#8a5a34"); if (y > y0 + 30) bg.R(26, y, 2, 4, "#8a5a34"); }
            bg.R(26, y0 + 2, 38, 1, "#a8734a"); bg.R(26, y0 + 44, 38, 1, "#a8734a"); bg.R(26, y0 + 46, 38, 1, "#6e4428");
            bg.R(63, y0 + 2, 1, 44, "#a8734a"); bg.R(26, y0 + 31, 1, 15, "#a8734a");
            if (W) { bg.R(26, y0 + 1, 38, 1, SNOW); bg.R(26, y0 + 43, 38, 1, SNOW); }
            // the barn: gable end to the road, big doors and a hayloft
            Shadow(tg, 2, y0 + 30, 24);
            string[] roof = { "#5a4a3a", "#43372b", "#76644e" };
            Shingles(tg, 1, y0 - 10, 26, 14, roof, W);
            Gable(tg, 14, y0 - 8, 11, 12, "#b5432f", "#e8dcc4", roof, W);
            Planks(tg, 2, y0 + 4, 23, 26, "#b5432f", "#8a2e22", "#c8583e");
            tg.R(2, y0 + 4, 23, 1, "#e8dcc4");
            tg.R(7, y0 + 13, 13, 17, "#e8dcc4"); tg.R(8, y0 + 14, 11, 16, "#7a3a2a");
            tg.R(13, y0 + 14, 1, 16, "#e8dcc4");
            tg.Line(8, y0 + 14, 12, y0 + 29, "#e8dcc4"); tg.Line(12, y0 + 14, 8, y0 + 29, "#e8dcc4");
            tg.Line(14, y0 + 14, 18, y0 + 29, "#e8dcc4"); tg.Line(18, y0 + 14, 14, y0 + 29, "#e8dcc4");
            tg.R(10, y0 - 2, 6, 5, "#e8dcc4"); tg.R(11, y0 - 1, 4, 4, "#3a2618"); tg.D(12, y0 + 2, "#e0c070"); tg.D(13, y0 + 1, "#e0c070");
            Foundation(tg, 2, y0 + 29, 23);
            // haystack in front of the barn
            for (int r = 0; r < 9; r++)
            {
                int hw = 3 + Mathf.RoundToInt(Mathf.Sqrt(r) * 3.2f);
                tg.R(14 - hw, y0 + 34 + r, hw * 2, 1, r % 3 == 0 ? "#e0c070" : "#c8a050");
                for (int x = 14 - hw; x < 14 + hw; x += 3) tg.D(x + r % 2, y0 + 34 + r, "#a07a2a");
            }
            if (W) tg.R(9, y0 + 33, 10, 2, SNOW);
            // a water trough in the paddock
            tg.R(36, y0 + 38, 14, 3, "#8a5a34"); tg.R(37, y0 + 38, 12, 1, W ? "#dce8f0" : "#5a86a8"); tg.R(36, y0 + 40, 14, 1, "#6e4428");
            // a salt lick on a post
            tg.R(54, y0 + 8, 1, 5, "#8a5a34"); tg.R(53, y0 + 6, 3, 2, "#f0ece0");
            foreach (var sp in new[] { new Vector2Int(33, 10), new Vector2Int(46, 8), new Vector2Int(55, 18), new Vector2Int(37, 22), new Vector2Int(50, 27),
                                       new Vector2Int(42, 33), new Vector2Int(31, 29), new Vector2Int(57, 36), new Vector2Int(45, 16), new Vector2Int(53, 40) })
                k.M.Herd.Add(sp);
            k.M.PileAt = new Vector2Int(-1, -1);
        }

        /// <summary>A butcher's shop: false front, striped awning, hams and sausages hanging in the window.</summary>
        static void Butcher(Ctx k)
        {
            var bg = k.Bg; var tg = k.Tg; bool W = k.Winter; int y0 = k.Y(0);
            bg.R(0, y0 + 27, 32, 5, W ? "#d8d2c6" : "#b98b5a");
            for (int x = 3; x < 32; x += 4) bg.R(x, y0 + 27, 1, 5, W ? "#bdb5a5" : "#8a5d3a");
            Shingles(tg, 2, y0 - 8, 28, 9, new[] { "#6e6a64", "#58544f", "#85807a" }, W);
            tg.R(0, y0 - 4, 32, 31, "#e8dcc4");
            Planks(tg, 1, y0 - 3, 30, 30, "#ece4d2", "#cfc4ad", "#f8f2e4");
            tg.R(0, y0 - 4, 32, 2, "#8a2e22"); tg.R(0, y0 - 2, 32, 1, "#6e241a");
            if (W) tg.R(0, y0 - 5, 32, 1, SNOW);
            tg.R(4, y0 + 0, 24, 7, "#8a2e22"); tg.R(5, y0 + 1, 22, 5, "#f4ecd8"); Letters(tg, 7, y0 + 2, 18, "#6e241a");
            // red and white awning
            for (int x = 0; x < 32; x++) tg.R(x, y0 + 9, 1, 4, ((x / 3) % 2 == 1) ? "#f4ecd8" : "#b5432f");
            for (int x = 1; x < 32; x += 3) tg.D(x, y0 + 13, ((x / 3) % 2 == 1) ? "#f4ecd8" : "#b5432f");
            if (W) tg.R(0, y0 + 8, 32, 1, SNOW);
            // the window: hams on hooks and a string of sausages
            Window(k, 3, y0 + 16, 11, 7, false, "#8a2e22");
            tg.R(4, y0 + 16, 9, 1, "#5a5a5a");
            foreach (int hx in new[] { 5, 9 }) { tg.D(hx + 1, y0 + 17, "#5a5a5a"); tg.R(hx, y0 + 18, 3, 4, "#a0503a"); tg.R(hx, y0 + 18, 3, 1, "#c8704a"); tg.D(hx + 1, y0 + 21, "#e8d8c0"); }
            for (int x = 4; x < 13; x += 2) tg.D(x, y0 + 22, "#8a3a2a");
            Door(k, 19, y0 + 16, 6, 11, "#6e3a2a");
            // a chopping block and a cleaver by the door
            tg.R(26, y0 + 22, 5, 5, "#8a5a34"); tg.R(26, y0 + 22, 5, 1, "#d8b07a"); tg.D(28, y0 + 22, "#a0503a");
            tg.R(28, y0 + 19, 1, 3, "#6e4428"); tg.R(27, y0 + 18, 3, 2, "#9a9aa2");
            k.M.PileAt = new Vector2Int(-1, -1);
        }

        /// <summary>A bakery: plaster walls, a brick oven chimney, loaves in the window, a sign shaped like a loaf.</summary>
        static void Bakery(Ctx k)
        {
            var bg = k.Bg; var tg = k.Tg; bool W = k.Winter; int y0 = k.Y(0);
            bg.R(0, y0 + 27, 32, 5, W ? "#d8d2c6" : "#c8b088");
            bg.R(18, y0 + 27, 7, 5, W ? "#e4e0d8" : "#d8c49a");
            Shadow(tg, 2, y0 + 26, 28);
            Shingles(tg, 1, y0 - 10, 30, 15, new[] { "#8a5a34", "#6e4428", "#a8734a" }, W);
            // the oven's brick chimney
            tg.R(23, y0 - 14, 6, 12, "#a0503a");
            for (int r = y0 - 13; r < y0 - 2; r += 2) tg.R(23 + (r % 4 == 1 ? 0 : 3), r, 3, 1, "#7a3a2a");
            tg.R(22, y0 - 14, 8, 2, "#6e3a2a"); if (W) tg.R(22, y0 - 15, 8, 1, SNOW);
            k.M.Smoke.Add(new Vector2(26f, y0 - 15));
            Planks(tg, 2, y0 + 5, 28, 21, "#efe0c0", "#d4c4a0", "#faf0d8");
            tg.R(2, y0 + 5, 28, 1, "#d4c4a0");
            // window with loaves on shelves
            Window(k, 4, y0 + 11, 10, 8, false, "#a0503a");
            for (int s = 0; s < 2; s++)
            {
                tg.R(4, y0 + 14 + s * 4, 10, 1, "#7a4e2c");
                for (int x = 5; x < 13; x += 3) { tg.R(x, y0 + 12 + s * 4, 3, 2, "#c8863a"); tg.D(x + 1, y0 + 12 + s * 4, "#e8b060"); }
            }
            // a loaf-shaped sign on a bracket
            tg.R(16, y0 + 8, 1, 3, "#3a3a3a"); tg.R(16, y0 + 8, 6, 1, "#3a3a3a");
            tg.R(17, y0 + 10, 8, 4, "#c8863a"); tg.R(18, y0 + 9, 6, 1, "#c8863a"); tg.R(18, y0 + 10, 6, 1, "#e8b060");
            tg.D(19, y0 + 11, "#8a5a2a"); tg.D(21, y0 + 11, "#8a5a2a"); tg.D(23, y0 + 11, "#8a5a2a");
            Door(k, 19, y0 + 15, 6, 11, "#6e4428");
            tg.R(26, y0 + 22, 4, 5, "#d8c49a"); tg.R(27, y0 + 21, 2, 1, "#b8a478"); tg.D(27, y0 + 24, "#8a7a58");   // a sack of flour
            Foundation(tg, 2, y0 + 25, 28);
            k.M.PileAt = new Vector2Int(-1, -1);
        }

        // ================================================================== industry
        /// <summary>An iron mine: a timber headframe over the shaft, and a stone bloomery furnace glowing beside it.</summary>
        static void IronMine(Ctx k)
        {
            var bg = k.Bg; var tg = k.Tg; bool W = k.Winter; int y0 = k.Y(0);
            bg.R(0, y0 + 2, 32, 30, W ? "#d4d0c8" : "#7a6a58");
            for (int i = 0; i < 46; i++) bg.D((int)(Hs(i, 61, 3) * 32), y0 + 2 + (int)(Hs(61, i, 4) * 30), i % 3 == 0 ? "#a0583a" : W ? "#bcb6aa" : "#5e5246");
            // the shaft collar
            tg.R(4, y0 + 17, 13, 7, "#6e4428"); tg.R(5, y0 + 18, 11, 5, "#1a1210"); tg.R(4, y0 + 17, 13, 1, "#a8734a");
            // headframe: two legs, a crossbar, the sheave wheel and the rope with an ore bucket
            tg.Line(3, y0 + 23, 10, y0 - 12, "#8a5a34"); tg.Line(17, y0 + 23, 10, y0 - 12, "#8a5a34");
            tg.Line(4, y0 + 23, 11, y0 - 12, "#6e4428");
            tg.R(5, y0 + 6, 11, 1, "#8a5a34"); tg.R(7, y0 - 3, 7, 1, "#8a5a34");
            tg.Disc(10.5f, y0 - 11f, 3.5f, "#5a3a22"); tg.Disc(10.5f, y0 - 11f, 2.2f, "#8a8a92"); tg.D(10, y0 - 11, "#3a3a3a");
            tg.R(13, y0 - 10, 1, 22, "#c8b88a");
            tg.R(11, y0 + 10, 5, 4, "#5a4a3a"); tg.R(11, y0 + 10, 5, 1, "#7a6a58"); tg.D(12, y0 + 11, "#a0583a"); tg.D(14, y0 + 11, "#a0583a");
            if (W) { tg.D(10, y0 - 14, SNOW); tg.R(9, y0 - 15, 3, 1, SNOW); }
            // the bloomery: a squat stone stack, glowing at its mouth
            for (int r = 0; r < 20; r++)
            {
                int hw = 4 + r / 5;
                tg.R(25 - hw, y0 + 6 + r, hw * 2, 1, r % 4 == 0 ? "#6e6860" : "#8d877d");
                if (r % 4 == 2) for (int x = 25 - hw; x < 25 + hw; x += 4) tg.D(x + (r / 4) % 2 * 2, y0 + 6 + r, "#6e6860");
            }
            tg.R(21, y0 + 2, 8, 4, "#6e6860"); tg.R(20, y0 + 2, 10, 1, "#5e554c");
            if (W) tg.R(20, y0 + 1, 10, 1, SNOW);
            tg.R(22, y0 + 20, 6, 5, "#2a1410"); tg.R(23, y0 + 22, 4, 3, "#a8401a"); tg.D(24, y0 + 22, "#f2a03a");
            k.M.Forge = new RectInt(23, y0 + 21, 4, 4); k.M.HasForge = true;
            k.M.Smoke.Add(new Vector2(25f, y0 + 1));
            // bellows and a pick by the furnace
            tg.R(17, y0 + 24, 4, 3, "#7a4e2c"); tg.R(17, y0 + 24, 4, 1, "#a8734a");
            tg.Line(28, y0 + 27, 31, y0 + 21, "#a8734a"); tg.Line(29, y0 + 21, 31, y0 + 23, "#9a9aa2");
            k.M.PileAt = new Vector2Int(2, y0 + 31);
        }

        /// <summary>A tannery: an open shed of hides stretched on frames, tanning pits in a muddy yard, a pile of bark.</summary>
        static void Tannery(Ctx k)
        {
            var bg = k.Bg; var tg = k.Tg; bool W = k.Winter; int y0 = k.Y(0);
            bg.R(0, y0 + 18, 48, 14, W ? "#d8d2c6" : "#7a6a4a");
            for (int i = 0; i < 40; i++) bg.D((int)(Hs(i, 71, 3) * 48), y0 + 18 + (int)(Hs(71, i, 4) * 14), W ? "#c4beb2" : "#5e5034");
            // tanning pits
            foreach (int vx in new[] { 22, 33 })
            {
                bg.Ellipse(vx, y0 + 25, 5, 3, PixelCanvas.C("#8a6a4a"));
                bg.Ellipse(vx, y0 + 25, 4, 2.2f, PixelCanvas.C(W ? "#9ab0bc" : "#5a3a1a"));
                bg.D(vx - 2, y0 + 24, W ? "#c8dce6" : "#7a5a2a");
            }
            // the shed: roof on posts, hides stretched on frames underneath
            Shadow(tg, 2, y0 + 18, 44);
            Shingles(tg, 1, y0 - 8, 46, 11, new[] { "#6e5a42", "#54442f", "#8a765a" }, W);
            tg.R(2, y0 + 3, 44, 2, "#4e3e2a");
            foreach (int x in new[] { 2, 23, 45 }) { tg.R(x, y0 + 3, 2, 16, "#7a5a34"); tg.D(x, y0 + 3, "#a8804a"); }
            string[] hide = { "#b88a5a", "#a87a4a", "#c49a6a", "#9a6a42" };
            int[] hx = { 5, 14, 27, 36 };
            for (int i = 0; i < 4; i++)
            {
                int x = hx[i];
                tg.R(x, y0 + 5, 8, 1, "#6e4428"); tg.R(x, y0 + 16, 8, 1, "#6e4428"); tg.R(x, y0 + 5, 1, 12, "#6e4428"); tg.R(x + 7, y0 + 5, 1, 12, "#6e4428");
                tg.R(x + 1, y0 + 7, 6, 8, hide[i]); tg.R(x + 2, y0 + 6, 4, 1, hide[i]); tg.R(x + 2, y0 + 15, 4, 1, hide[i]);
                tg.D(x + 2, y0 + 9, "#8a5a34"); tg.D(x + 5, y0 + 12, "#8a5a34");
            }
            // bark for the pits, and a scraping beam
            for (int r = 0; r < 6; r++) tg.R(39 + r / 2, y0 + 24 + r, 9 - r, 1, r % 2 == 0 ? "#6e4a2a" : "#5a3a20");
            tg.Line(12, y0 + 27, 17, y0 + 21, "#8a5a34"); tg.R(16, y0 + 21, 2, 6, "#6e4428");
            k.M.PileAt = new Vector2Int(1, y0 + 31);
        }

        /// <summary>A brick brewhouse with a tall stack, arched doors, and barrels racked out front.</summary>
        static void Brewery(Ctx k)
        {
            var bg = k.Bg; var tg = k.Tg; bool W = k.Winter; int y0 = k.Y(0);
            bg.R(0, y0 + 27, 48, 5, W ? "#d8d2c6" : "#b8996a");
            Shadow(tg, 3, y0 + 27, 42);
            Shingles(tg, 2, y0 - 10, 44, 13, new[] { "#4a4f57", "#34383f", "#646a74" }, W);
            // smokestack
            tg.R(36, y0 - 16, 6, 14, "#9a5a3a"); for (int r = y0 - 15; r < y0 - 2; r += 2) tg.R(36 + (r % 4 == 1 ? 0 : 3), r, 3, 1, "#7a4028");
            tg.R(35, y0 - 16, 8, 2, "#6e3a2a"); if (W) tg.R(35, y0 - 17, 8, 1, SNOW);
            k.M.Smoke.Add(new Vector2(39f, y0 - 17));
            // brick walls
            tg.R(2, y0 + 3, 44, 25, "#9a5a3a");
            for (int r = 0; r < 25; r += 3)
            {
                tg.R(2, y0 + 3 + r, 44, 1, "#7a4028");
                for (int x = ((r / 3) % 2 == 1 ? 3 : 6); x < 46; x += 6) tg.R(x, y0 + 3 + r, 1, 3, "#7a4028");
            }
            tg.R(2, y0 + 3, 44, 1, "#b87a5a");
            tg.R(11, y0 + 5, 26, 6, "#3a2a1a"); tg.R(12, y0 + 6, 24, 4, "#e0b04a"); Letters(tg, 14, y0 + 7, 20, "#3a2a1a");
            Window(k, 5, y0 + 14, 5, 6, false, "#c8b88a"); Window(k, 38, y0 + 14, 5, 6, false, "#c8b88a");
            // the arch over the doors
            tg.R(18, y0 + 13, 12, 2, "#c8b88a"); tg.R(17, y0 + 15, 1, 13, "#c8b88a"); tg.R(30, y0 + 15, 1, 13, "#c8b88a");
            Door(k, 19, y0 + 16, 10, 11, "#6e4428");
            tg.R(24, y0 + 17, 1, 10, "#4e3020");
            // barrels on a rack
            tg.R(2, y0 + 26, 13, 1, "#6e4428");
            Barrel(tg, 3, y0 + 20); Barrel(tg, 9, y0 + 20); Barrel(tg, 6, y0 + 14);
            Foundation(tg, 2, y0 + 27, 44);
            k.M.PileAt = new Vector2Int(33, y0 + 31);
        }

        // ================================================================== trade
        /// <summary>A tailor's: a blue false front, a dress form and bolts of cloth in the window, scissors on the sign.</summary>
        static void Tailor(Ctx k)
        {
            var bg = k.Bg; var tg = k.Tg; bool W = k.Winter; int y0 = k.Y(0);
            bg.R(0, y0 + 27, 32, 5, W ? "#d8d2c6" : "#b98b5a");
            for (int x = 3; x < 32; x += 4) bg.R(x, y0 + 27, 1, 5, W ? "#bdb5a5" : "#8a5d3a");
            Shingles(tg, 2, y0 - 8, 28, 9, new[] { "#6e6a64", "#58544f", "#85807a" }, W);
            tg.R(0, y0 - 4, 32, 31, "#4a6a9a");
            Planks(tg, 1, y0 - 3, 30, 30, "#5a7aaa", "#46628a", "#7090be");
            tg.R(0, y0 - 4, 32, 2, "#f4ecd8"); tg.R(0, y0 - 2, 32, 1, "#c8bca4");
            if (W) tg.R(0, y0 - 5, 32, 1, SNOW);
            // sign with a pair of scissors
            tg.R(4, y0 + 0, 24, 7, "#f4ecd8"); tg.R(5, y0 + 1, 22, 5, "#2e3a4e"); Letters(tg, 13, y0 + 2, 13, "#f4ecd8");
            tg.D(7, y0 + 2, "#c8c8d0"); tg.D(8, y0 + 3, "#c8c8d0"); tg.D(9, y0 + 4, "#c8c8d0"); tg.D(9, y0 + 2, "#c8c8d0"); tg.D(7, y0 + 4, "#c8c8d0");
            tg.D(6, y0 + 1, "#e8b860"); tg.D(6, y0 + 5, "#e8b860");
            // display window: a dress form and bolts of cloth
            Window(k, 3, y0 + 11, 12, 12, false, "#f4ecd8");
            tg.R(6, y0 + 13, 4, 5, "#8a3a4a"); tg.R(5, y0 + 15, 6, 3, "#8a3a4a"); tg.R(7, y0 + 12, 2, 1, "#5a4a3a"); tg.R(7, y0 + 18, 2, 4, "#5a4a3a"); tg.R(6, y0 + 22, 4, 1, "#3a2a1a");
            string[] bolts = { "#c8a050", "#5a8a4a", "#e8dcc4" };
            for (int i = 0; i < 3; i++) { tg.R(11 + i, y0 + 16 + i, 1, 6 - i, bolts[i]); }
            Door(k, 19, y0 + 16, 6, 11, "#3a4a5a");
            tg.R(26, y0 + 20, 4, 7, "#c8a050"); tg.R(26, y0 + 20, 4, 1, "#e0c070"); tg.R(27, y0 + 22, 2, 1, "#a07a2a");   // a bolt of cloth by the door
            k.M.PileAt = new Vector2Int(-1, -1);
        }

        /// <summary>A two-storey hotel: false front with a big sign, a balcony on posts, a boardwalk and a hitching rail.</summary>
        static void Hotel(Ctx k)
        {
            var bg = k.Bg; var tg = k.Tg; bool W = k.Winter; int y0 = k.Y(0);
            bg.R(0, y0 + 27, 48, 5, W ? "#d8d2c6" : "#b98b5a");
            for (int x = 3; x < 48; x += 4) bg.R(x, y0 + 27, 1, 5, W ? "#bdb5a5" : "#8a5d3a");
            string[] wall = { "#c8a070", "#a8804e", "#dcb888" };
            Planks(tg, 0, y0 - 22, 48, 49, wall[0], wall[1], wall[2]);
            tg.R(0, y0 - 24, 48, 3, "#7a4a2a"); tg.R(0, y0 - 24, 48, 1, "#a8734a");
            tg.R(12, y0 - 26, 24, 3, "#7a4a2a"); tg.R(12, y0 - 26, 24, 1, "#a8734a");
            if (W) { tg.R(0, y0 - 25, 48, 1, SNOW); tg.R(12, y0 - 27, 24, 1, SNOW); }
            tg.R(6, y0 - 20, 36, 7, "#3a2414"); tg.R(7, y0 - 19, 34, 5, "#f4ecd8"); Letters(tg, 12, y0 - 18, 24, "#7a2a1a");
            // upper floor: four sash windows with shutters
            foreach (int x in new[] { 4, 15, 29, 40 })
            {
                tg.R(x - 1, y0 - 10, 1, 7, "#5a7a4a"); tg.R(x + 4, y0 - 10, 1, 7, "#5a7a4a");
                Window(k, x, y0 - 10, 4, 7, false, "#f4ecd8");
            }
            // balcony and its railing
            tg.R(0, y0 - 1, 48, 2, "#5a3a22");
            for (int x = 1; x < 48; x += 2) tg.R(x, y0 + 1, 1, 3, "#e8dcc4");
            tg.R(0, y0 + 1, 48, 1, "#e8dcc4"); tg.R(0, y0 + 4, 48, 2, "#4e3420");
            if (W) tg.R(0, y0 - 2, 48, 1, SNOW);
            foreach (int x in new[] { 1, 16, 31, 46 }) { tg.R(x, y0 + 6, 1, 21, "#8a6a4a"); tg.D(x, y0 + 6, "#a8886a"); }
            // ground floor: parlour windows and the front door
            Window(k, 4, y0 + 12, 8, 7, false, "#f4ecd8"); Window(k, 36, y0 + 12, 8, 7, false, "#f4ecd8");
            Window(k, 18, y0 + 10, 12, 2, false, "#f4ecd8");   // fanlight
            Door(k, 20, y0 + 15, 8, 12, "#5a3a22");
            tg.R(23, y0 + 16, 2, 10, "#3e2818");
            tg.R(13, y0 + 23, 4, 4, "#8a5a34"); tg.R(13, y0 + 23, 4, 1, "#a8734a");   // a bench
            // hitching rail out front
            tg.R(1, y0 + 25, 14, 1, "#8a5a34"); tg.R(1, y0 + 25, 1, 6, "#6e4428"); tg.R(14, y0 + 25, 1, 6, "#6e4428");
            k.M.HitchAt = new Vector2Int(1, y0 + 31);
            k.M.PileAt = new Vector2Int(-1, -1);
        }

        /// <summary>A barbershop and bathhouse: a striped pole by the door, a chair in the window, the boiler's stovepipe.</summary>
        static void Barber(Ctx k)
        {
            var bg = k.Bg; var tg = k.Tg; bool W = k.Winter; int y0 = k.Y(0);
            bg.R(0, y0 + 27, 32, 5, W ? "#d8d2c6" : "#b98b5a");
            for (int x = 3; x < 32; x += 4) bg.R(x, y0 + 27, 1, 5, W ? "#bdb5a5" : "#8a5d3a");
            Shingles(tg, 1, y0 - 8, 30, 13, new[] { "#5a6a5a", "#43503f", "#76887a" }, W);
            // the boiler's stovepipe with a rain cap
            tg.R(26, y0 - 13, 2, 9, "#3a3a3e"); tg.R(25, y0 - 14, 4, 1, "#5a5a62");
            k.M.Smoke.Add(new Vector2(27f, y0 - 15));
            Planks(tg, 2, y0 + 4, 28, 23, "#ece4d2", "#cfc4ad", "#f8f2e4");
            tg.R(2, y0 + 4, 28, 1, "#b5432f");
            tg.R(4, y0 + 6, 24, 5, "#b5432f"); tg.R(5, y0 + 7, 22, 3, "#f4ecd8"); Letters(tg, 6, y0 + 7, 20, "#2e3a4e");
            // the window: a barber's chair and a mirror
            Window(k, 4, y0 + 14, 9, 8, false, "#b5432f");
            tg.R(6, y0 + 17, 4, 3, "#a8302a"); tg.R(6, y0 + 15, 1, 3, "#a8302a"); tg.R(7, y0 + 20, 2, 2, "#5a5a5a");
            tg.R(10, y0 + 15, 2, 3, "#d8e8f0");
            // striped pole
            for (int y = y0 + 13; y < y0 + 26; y++) { tg.D(15, y, (y + 1) % 3 == 0 ? "#2f5d9a" : y % 3 == 0 ? "#c8302a" : "#f8f4ec"); tg.D(16, y, y % 3 == 0 ? "#2f5d9a" : (y + 2) % 3 == 0 ? "#c8302a" : "#e8e4dc"); }
            tg.R(15, y0 + 12, 2, 1, "#c8a050"); tg.R(15, y0 + 26, 2, 1, "#c8a050");
            Door(k, 19, y0 + 15, 6, 11, "#5a3a22");
            tg.R(26, y0 + 21, 4, 5, "#8a8a92"); tg.R(26, y0 + 21, 4, 1, "#b8b8c0");   // a tin bathtub out to dry
            Foundation(tg, 2, y0 + 26, 28);
            k.M.PileAt = new Vector2Int(-1, -1);
        }

        // ================================================================== civic
        /// <summary>A white frame church: the steeple and its spire over the door, tall arched windows down the nave.</summary>
        static void Church(Ctx k)
        {
            var bg = k.Bg; var tg = k.Tg; bool W = k.Winter; int si = k.Si; int y0 = k.Y(0);
            string[] lawns = { "#86c05a", "#74b04c", "#a8a052", SNOW };
            bg.R(0, y0 + 37, 32, 11, lawns[si]);
            bg.R(13, y0 + 37, 6, 11, W ? "#e4e0d8" : "#d8c8a0");
            if (!W && si < 2) for (int x = 2; x < 31; x += 4) if (x < 11 || x > 20) bg.D(x, y0 + 43 + (x % 3), x % 8 == 2 ? "#f2cf4a" : "#f4f1e6");
            string[] wall = { "#f4efe2", "#d6ccb6", "#fffaf0" };
            string[] roof = { "#5a5e66", "#43464d", "#767a82" };
            Shadow(tg, 3, y0 + 36, 26);
            // the nave: roof running back, gable end to the road
            Shingles(tg, 2, y0 - 2, 28, 14, roof, W);
            Gable(tg, 16, y0 + 0, 13, 10, wall[0], wall[1], roof, W);
            Planks(tg, 3, y0 + 10, 26, 27, wall[0], wall[1], wall[2]);
            tg.R(3, y0 + 10, 26, 1, wall[1]);
            // tall arched windows either side of the tower
            foreach (int x in new[] { 5, 23 })
            {
                tg.R(x - 1, y0 + 15, 6, 15, "#e8e2d4");
                tg.R(x, y0 + 17, 4, 12, "#5a86a8"); tg.R(x + 1, y0 + 16, 2, 1, "#5a86a8");
                tg.R(x, y0 + 23, 4, 6, "#4a7090"); tg.R(x + 2, y0 + 16, 1, 13, "#e8e2d4");
                tg.D(x, y0 + 17, "#c8e4f0");
                k.Wins.Add(new RectInt(x, y0 + 17, 4, 12));
            }
            // the tower: belfry, then a white spire with a cross on top
            Planks(tg, 11, y0 - 8, 10, 45, wall[0], wall[1], wall[2]);
            tg.R(20, y0 - 8, 1, 45, wall[1]);
            tg.R(10, y0 - 9, 12, 2, roof[1]); if (W) tg.R(10, y0 - 10, 12, 1, SNOW);
            tg.R(12, y0 - 6, 8, 6, "#2a1c12"); tg.R(15, y0 - 5, 2, 4, "#c89a3a"); tg.D(14, y0 - 2, "#a87a2a"); tg.D(17, y0 - 2, "#a87a2a");
            for (int i = 12; i < 20; i += 2) tg.D(i, y0 - 6, wall[0]);
            for (int r = 0; r < 18; r++)
            {
                int hw = Mathf.Max(1, Mathf.RoundToInt((r + 1) / 18f * 6));
                tg.R(16 - hw, y0 - 27 + r, hw * 2, 1, r % 3 == 0 ? roof[2] : roof[0]);
                tg.D(16 + hw - 1, y0 - 27 + r, roof[1]);
            }
            if (W) tg.R(15, y0 - 25, 2, 6, SNOW);
            tg.R(15, y0 - 30, 2, 4, "#c89a3a"); tg.R(14, y0 - 29, 4, 1, "#c89a3a");
            // a round window over the doors, and the doors
            tg.Disc(16, y0 + 9, 2.5f, "#5a86a8"); tg.D(16, y0 + 8, "#c8e4f0");
            k.Wins.Add(new RectInt(15, y0 + 8, 2, 2));
            Door(k, 13, y0 + 24, 6, 12, "#7a3a2a");
            tg.R(16, y0 + 25, 1, 11, "#5a2a1e");
            tg.R(12, y0 + 22, 8, 1, wall[1]);
            Foundation(tg, 3, y0 + 36, 26);
            k.M.PileAt = new Vector2Int(-1, -1);
        }
    }
}
