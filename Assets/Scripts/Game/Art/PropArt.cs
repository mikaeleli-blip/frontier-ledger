using System.Collections.Generic;
using FrontierLedger.Simulation;
using UnityEngine;

namespace FrontierLedger.Game.Art
{
    /// <summary>Crops by growth stage, piles of goods, gravestones, tents and the fishing pier.</summary>
    public static class PropArt
    {
        const string OL = "#3b2618", SNOW = "#f4f8fa";
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        static Sprite Cached(string key, System.Func<PixelCanvas> make, Vector2 pivot)
        {
            Sprite s;
            if (!cache.TryGetValue(key, out s)) { s = make().ToSprite(key, pivot); cache[key] = s; }
            return s;
        }

        // ------------------------------------------------------------------ field crops
        /// <summary>Crop stage for a field: 0 bare, 1 seedlings, 2 young, 3 tall green, 4 ripe, 5 stubble.</summary>
        public static int FieldStage(Building b, int season)
        {
            if (season == 3) return 0;
            if (season <= 1)
            {
                float g = b.CropGrowth;
                if (g <= 0.02f) return 0;
                if (g < 0.25f) return 1;
                if (g < 0.6f) return 2;
                return 3;
            }
            float total = b.CropGrowth * Balance.FieldYield;
            if (total < 1f) return 0;
            float left = b.CropLeft / total;
            return left > 0.05f ? 4 : 5;
        }

        /// <summary>Crops overlay for a 48x48 field. Two frames sway in the wind. Taller crops further down the field
        /// are drawn by the renderer in rows so people can walk between them.</summary>
        public static Sprite FieldCrops(int stage, int frame, float harvestedFrac)
        {
            int hk = Mathf.Clamp(Mathf.RoundToInt(harvestedFrac * 10), 0, 10);
            return Cached("crops" + stage + frame + "_" + (stage == 4 ? hk : 0), () =>
            {
                var g = new PixelCanvas(48, 48);
                if (stage == 0) return g;
                for (int row = 0; row < 11; row++)
                {
                    int y = 4 + row * 4;
                    for (int x = 3; x < 45; x += 3)
                    {
                        float h = Noise.Hash(x, y, 4);
                        int sw = ((x / 3 + frame + row) % 2);
                        switch (stage)
                        {
                            case 1: if (h < 0.75f) { g.D(x, y - 1, "#7cc04a"); g.D(x + 1, y - 2, "#9ad860"); } break;
                            case 2:
                                g.R(x, y - 2, 2, 2, "#4a9a34"); g.D(x + sw, y - 3, "#7cc04a"); g.D(x + 1 - sw, y - 3, "#5aae3e"); break;
                            case 3:
                                g.R(x, y - 4, 2, 4, "#3f8a2e"); g.D(x + sw, y - 5, "#6ab43e"); g.D(x - 1 + 2 * sw, y - 3, "#58a03a"); g.D(x + 1, y - 2, "#2e6a24"); break;
                            case 4:
                                bool cut = (row * 15 + x / 3) < hk * 16.5f;
                                if (cut) { g.D(x, y - 1, "#c8a050"); g.D(x + 1, y - 1, "#a8843a"); break; }
                                g.R(x, y - 4, 2, 4, "#c89a32"); g.R(x, y - 6 + sw, 2, 2, "#f0c850"); g.D(x + sw, y - 7 + sw, "#f8dc78"); g.D(x + 1, y - 2, "#a07a28"); break;
                            case 5: if (h < 0.8f) { g.D(x, y - 1, "#c8a050"); g.D(x + 1, y - 1, "#a8843a"); } break;
                        }
                    }
                }
                return g;
            }, new Vector2(0, 1));
        }

        // ------------------------------------------------------------------ piles of goods
        /// <summary>A pile showing roughly how much of a good is stocked: level 1-4.</summary>
        public static Sprite Pile(Good good, int level, int season)
        {
            return Cached("pile" + (int)good + level + season, () =>
            {
                var g = new PixelCanvas(28, 18);
                bool w = season == 3;
                switch (good)
                {
                    case Good.Logs:
                        for (int i = 0; i < level * 2 && i < 8; i++)
                        {
                            int row = i < 4 ? 0 : 1, col = i % 4, x = 1 + col * 6 + row * 3, y = 13 - row * 5;
                            g.R(x, y, 6, 4, "#8a5a34"); g.R(x, y + 3, 6, 1, "#6e4428");
                            g.Disc(x + 1.5f, y + 1.8f, 2.2f, "#d8b07a"); g.D(x + 1, y + 1, "#a8804a");
                        }
                        break;
                    case Good.Firewood:
                        for (int r = 0; r < level + 1 && r < 5; r++)
                            for (int x = 1; x < 26; x += 3)
                            {
                                int y = 15 - r * 3;
                                g.R(x, y, 3, 3, "#a8734a"); g.D(x + 1, y + 1, "#d8b07a"); g.D(x, y, "#c49364");
                            }
                        if (w) g.R(1, 15 - level * 3, 25, 1, SNOW);
                        break;
                    case Good.Stone:
                        for (int i = 0; i < level * 2 + 1 && i < 9; i++)
                        {
                            int row = i < 4 ? 0 : i < 7 ? 1 : 2, col = row == 0 ? i : row == 1 ? i - 4 : i - 7;
                            int x = 1 + col * 6 + row * 3, y = 12 - row * 4;
                            g.R(x, y, 6, 5, "#a39c90"); g.R(x, y, 6, 1, "#c8c1b4"); g.R(x + 5, y, 1, 5, "#7e776e");
                        }
                        break;
                    case Good.Food:
                        for (int i = 0; i < level + 1 && i < 5; i++)
                        {
                            int x = 1 + i * 5, y = 10 - (i % 2) * 2;
                            g.R(x, y, 5, 7, "#d8c08a"); g.R(x + 1, y - 1, 3, 1, "#b8a06a"); g.R(x, y + 2, 5, 1, "#b8a06a");
                            g.D(x + 2, y + 4, "#a07a48");
                        }
                        if (level >= 3) { g.R(22, 10, 5, 7, "#e0a03a"); g.D(23, 9, "#5a9a3a"); }
                        break;
                    case Good.Furniture:
                        for (int i = 0; i < level && i < 4; i++)
                        {
                            int x = 1 + i * 6, y = 16;
                            if (i % 3 == 0)
                            {   // chair
                                g.R(x, y - 10, 2, 10, "#8a5a34"); g.R(x, y - 5, 5, 2, "#b8844a"); g.R(x + 4, y - 3, 1, 3, "#8a5a34"); g.R(x + 1, y - 3, 1, 3, "#6e4428");
                            }
                            else if (i % 3 == 1)
                            {   // table
                                g.R(x - 1, y - 6, 7, 2, "#c49364"); g.R(x - 1, y - 6, 7, 1, "#dcae7c"); g.R(x, y - 4, 1, 4, "#8a5a34"); g.R(x + 4, y - 4, 1, 4, "#8a5a34");
                            }
                            else
                            {   // chest
                                g.R(x, y - 5, 6, 5, "#a0683a"); g.R(x, y - 6, 6, 2, "#b87a4a"); g.R(x + 2, y - 4, 2, 1, "#f2cf4a");
                            }
                        }
                        break;
                    case Good.Tools:
                        g.R(2, 10, 20, 6, "#8a5a34"); g.R(2, 10, 20, 1, "#b8844a");
                        for (int i = 0; i < level + 1; i++) { g.R(4 + i * 4, 4, 1, 7, "#a8734a"); g.R(3 + i * 4, 3, 3, 2, "#9a9aa2"); }
                        break;
                }
                g.Outline(OL);
                return g;
            }, new Vector2(0, 0));
        }

        public static int PileLevel(float amount, float full)
        {
            if (amount < 0.5f) return 0;
            return Mathf.Clamp(1 + Mathf.FloorToInt(amount / full * 4), 1, 4);
        }

        // ------------------------------------------------------------------ things settlers carry
        /// <summary>A log carried on the shoulder (pivot top-left).</summary>
        public static Sprite CarriedLog()
        {
            return Cached("carry_log", () =>
            {
                var g = new PixelCanvas(13, 6);
                g.R(1, 1, 11, 3, "#8a5a34"); g.R(1, 1, 11, 1, "#a8734a"); g.R(1, 3, 11, 1, "#6e4428");
                g.R(1, 1, 2, 3, "#d8b07a"); g.D(1, 2, "#a8804a"); g.D(7, 2, "#6e4428");
                g.Outline(OL);
                return g;
            }, new Vector2(0, 1));
        }

        /// <summary>A block of stone held against the chest (pivot top-left).</summary>
        public static Sprite CarriedSack()
        {
            return Cached("carry_sack", () =>
            {
                var g = new PixelCanvas(9, 8);
                g.R(2, 1, 5, 6, "#d8c49a"); g.R(1, 3, 7, 4, "#d8c49a"); g.R(6, 2, 2, 5, "#b8a478");
                g.R(3, 0, 3, 1, "#b8a478"); g.D(4, 1, "#8a7a58"); g.D(3, 4, "#f0e0bc");
                g.Outline(OL);
                return g;
            }, new Vector2(0, 1));
        }

        public static Sprite CarriedStone()
        {
            return Cached("carry_stone", () =>
            {
                var g = new PixelCanvas(9, 7);
                g.R(1, 1, 7, 5, "#a39c90"); g.R(1, 1, 7, 1, "#c8c1b4"); g.R(6, 1, 2, 5, "#7e776e"); g.D(3, 3, "#8d877d");
                g.Outline(OL);
                return g;
            }, new Vector2(0, 1));
        }

        /// <summary>A hammer: raised (0) or striking (1). Pivot top-left.</summary>
        public static Sprite Hammer(int frame)
        {
            return Cached("hammer" + frame, () =>
            {
                var g = new PixelCanvas(9, 9);
                if (frame == 0) { g.Line(2, 7, 5, 3, "#a8734a"); g.R(4, 1, 4, 2, "#9a9aa2"); g.D(4, 1, "#c8c8d0"); }
                else { g.Line(1, 4, 5, 4, "#a8734a"); g.R(5, 2, 2, 4, "#9a9aa2"); g.D(5, 2, "#c8c8d0"); }
                g.Outline(OL);
                return g;
            }, new Vector2(0, 1));
        }

        // ------------------------------------------------------------------ graves
        public static Sprite Graves(int n, int season)
        {
            n = Mathf.Clamp(n, 0, 18);
            return Cached("graves" + n + season, () =>
            {
                var g = new PixelCanvas(48, 48);
                for (int i = 0; i < n; i++)
                {
                    int col = i % 6, row = i / 6;
                    int x = 5 + col * 7, y = 6 + row * 12;
                    bool cross = Noise.Hash(i, 3, 40) < 0.35f;
                    if (cross) { g.R(x + 2, y, 2, 8, "#8a5a34"); g.R(x, y + 2, 6, 2, "#8a5a34"); }
                    else { g.R(x, y + 1, 6, 7, "#a39c90"); g.R(x + 1, y, 4, 1, "#a39c90"); g.R(x + 5, y + 1, 1, 7, "#7e776e"); g.R(x + 2, y + 3, 2, 1, "#6e6860"); }
                    g.R(x, y + 8, 6, 2, season == 3 ? "#dfe8ec" : "#5a7a3a");
                    if (season < 2 && Noise.Hash(i, 4, 41) < 0.5f) g.D(x + 1, y + 8, "#f2cf4a");
                }
                g.Outline(OL);
                return g;
            }, new Vector2(0, 1));
        }

        // ------------------------------------------------------------------ tents for families without a roof
        public static Sprite Tent(int variant, int season)
        {
            return Cached("tent" + variant + season, () =>
            {
                var g = new PixelCanvas(20, 16);
                g.Ellipse(10, 14.5f, 9, 1.6f, new Color32(0, 0, 0, 55), true);
                string[] cloth = { "#e8dcc4", "#c8b48a", "#b8a890" };
                string c = cloth[variant % 3], cd = variant % 3 == 0 ? "#c8b89a" : "#a8946a";
                for (int r = 0; r < 12; r++)
                {
                    int hw = 1 + r * 8 / 11;
                    g.R(10 - hw, 2 + r, hw * 2, 1, c);
                    g.R(10, 2 + r, hw, 1, cd);
                }
                g.R(9, 7, 2, 7, "#3a2618");
                g.R(9, 0, 1, 3, "#8a5a34");
                if (season == 3) g.R(8, 2, 4, 2, SNOW);
                g.Outline(OL);
                return g;
            }, new Vector2(0.5f, 1.5f / 16f));
        }

        public static Sprite Campfire(int frame)
        {
            return Cached("fire" + frame, () =>
            {
                var g = new PixelCanvas(12, 12);
                g.Ellipse(6, 10, 5, 1.6f, PixelCanvas.C("#5a5a52"));
                g.R(2, 8, 8, 2, "#6e4428"); g.Line(2, 9, 9, 7, "#8a5a34");
                string[] fl = { "#ffd04a", "#ff8a2a", "#e05a1e" };
                int h = 5 + (frame % 2);
                for (int y = 0; y < h; y++)
                {
                    int w = Mathf.Max(1, (h - y) * 4 / h);
                    g.R(6 - w / 2 - (frame == 2 ? 1 : 0), 8 - y, w, 1, fl[y < 2 ? 2 : y < 4 ? 1 : 0]);
                }
                return g;
            }, new Vector2(0.5f, 2f / 12f));
        }
    }
}
