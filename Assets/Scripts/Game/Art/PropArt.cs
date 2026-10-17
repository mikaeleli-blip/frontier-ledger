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
                    case Good.Iron:   // pig iron bars, stacked crosswise
                        for (int r = 0; r < level + 1 && r < 5; r++)
                            for (int i = 0; i < 4 - (r % 2); i++)
                            {
                                int x = 2 + i * 6 + (r % 2) * 3, y = 15 - r * 2;
                                g.R(x, y, 5, 2, r % 2 == 0 ? "#5a5a62" : "#4a4a52"); g.R(x, y, 5, 1, "#7a7a84"); g.D(x + 4, y + 1, "#8a5a3a");
                            }
                        break;
                    case Good.Leather: case Good.Hides:   // rolled leather, or hides folded in a stack
                        for (int i = 0; i < level + 1 && i < 5; i++)
                        {
                            int x = 1 + i * 5, y = 11 - (i % 2) * 2;
                            string c = good == Good.Leather ? "#7a4a2a" : "#b88a5a", d = good == Good.Leather ? "#5a341c" : "#9a6a42";
                            g.R(x, y, 5, 6, c); g.R(x, y, 5, 1, d); g.R(x + 1, y + 2, 3, 1, d); g.Disc(x + 2.5f, y + 4.5f, 1.2f, d);
                        }
                        break;
                    case Good.Beer:   // barrels on their sides, then stacked
                        for (int i = 0; i < level + 1 && i < 6; i++)
                        {
                            int x = 1 + (i % 4) * 6 + (i >= 4 ? 3 : 0), y = i >= 4 ? 6 : 12;
                            g.R(x, y, 5, 6, "#8a5a34"); g.R(x + 3, y, 2, 6, "#6e4428"); g.R(x, y + 1, 5, 1, "#5a5a5a"); g.R(x, y + 4, 5, 1, "#5a5a5a"); g.R(x + 1, y - 1, 3, 1, "#a8734a");
                        }
                        break;
                    case Good.Wool:   // bales of fleece
                        for (int i = 0; i < level + 1 && i < 5; i++)
                        {
                            int x = 1 + i * 5, y = 10 - (i % 2) * 2;
                            g.R(x, y, 5, 7, "#ece6d8"); g.R(x, y + 2, 5, 1, "#c8bca4"); g.R(x, y + 5, 5, 1, "#c8bca4"); g.D(x + 1, y + 1, "#ffffff");
                        }
                        break;
                    default:   // crates
                        for (int i = 0; i < level + 1 && i < 5; i++)
                        {
                            int x = 1 + i * 5, y = 11 - (i % 2) * 3;
                            g.R(x, y, 5, 5, "#c49a62"); g.R(x, y, 5, 1, "#d8b07a"); g.R(x + 4, y, 1, 5, "#a07a48"); g.D(x + 2, y + 2, "#a07a48");
                        }
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
        /// <summary>A covered freight wagon behind a yoke of oxen, side on, heading right. Pivot under the wheels.</summary>
        public static Sprite FreightWagon(int frame)
        {
            return Cached("freight_" + frame, () =>
            {
                var g = new PixelCanvas(42, 26);
                // oxen: the far one a shade darker, legs stepping by frame
                foreach (int far in new[] { 1, 0 })
                {
                    int ox = 28 + far * 2, oy = 13 - far;
                    string body = far == 1 ? "#5e3e24" : "#7a5232", shade = far == 1 ? "#4a301c" : "#5e3e24";
                    g.R(ox, oy, 9, 5, body); g.R(ox, oy + 4, 9, 1, shade); g.R(ox + 8, oy - 1, 3, 4, body); g.D(ox + 10, oy + 1, "#2a1c12");
                    g.D(ox + 8, oy - 2, "#e8dcc4"); g.D(ox + 10, oy - 2, "#e8dcc4");
                    int a = frame == 0 ? 0 : 1;
                    g.R(ox + 1 + a, oy + 5, 1, 3, shade); g.R(ox + 3 - a, oy + 5, 1, 3, shade); g.R(ox + 6 + a, oy + 5, 1, 3, shade); g.R(ox + 8 - a, oy + 5, 1, 3, shade);
                }
                g.R(22, 16, 7, 1, "#6e4428"); g.R(36, 11, 1, 3, "#8a5a34");   // tongue and yoke
                // wagon box and the canvas bonnet over its hoops
                g.R(3, 14, 20, 5, "#8a5a34"); g.R(3, 14, 20, 1, "#a8734a"); g.R(3, 18, 20, 1, "#6e4428");
                for (int x = 7; x < 22; x += 5) g.R(x, 15, 1, 3, "#6e4428");
                for (int r = 0; r < 10; r++)
                {
                    int inset = r < 3 ? 3 - r : 0;
                    g.R(4 + inset, 4 + r, 18 - inset * 2, 1, r % 3 == 2 ? "#ddd4c2" : "#f4f0e6");
                }
                foreach (int hx in new[] { 8, 13, 18 }) g.R(hx, 5, 1, 9, "#d8d0c0");
                g.R(21, 9, 2, 5, "#e8e0cc");
                // the teamster on the seat
                g.R(22, 10, 3, 4, "#6a8ab5"); g.R(22, 7, 3, 3, "#e8b890"); g.R(21, 6, 5, 1, "#7a5232"); g.R(22, 5, 3, 1, "#7a5232");
                // wheels, spokes turning
                foreach (int wx in new[] { 6, 18 })
                {
                    int cy = 20, r = wx == 6 ? 4 : 3;
                    for (int dy = -r; dy <= r; dy++) for (int dx = -r; dx <= r; dx++)
                    {
                        int d2 = dx * dx + dy * dy;
                        if (d2 <= r * r && d2 >= (r - 1) * (r - 1)) g.D(wx + dx, cy + dy, "#5a3a22");
                    }
                    if (frame == 0) { g.R(wx - r + 1, cy, r * 2 - 1, 1, "#8a5a34"); g.R(wx, cy - r + 1, 1, r * 2 - 1, "#8a5a34"); }
                    else for (int k = -r + 1; k < r; k++) { g.D(wx + k, cy + k, "#8a5a34"); g.D(wx + k, cy - k, "#8a5a34"); }
                    g.D(wx, cy, "#3a2418");
                }
                g.Outline(OL);
                return g;
            }, new Vector2(0.5f, 1f / 26f));
        }

        /// <summary>What a courier carries for each good (pivot top-left); steers are led on a rope instead (see Steer).</summary>
        public static Sprite Carried(Good good)
        {
            switch (good)
            {
                case Good.Logs: case Good.Firewood: return CarriedLog();
                case Good.Stone: return CarriedStone();
                case Good.Iron: return Cached("carry_iron", () => { var g = new PixelCanvas(9, 5); g.R(1, 1, 7, 3, "#5a5a62"); g.R(1, 1, 7, 1, "#7a7a84"); g.D(7, 3, "#8a5a3a"); g.Outline(OL); return g; }, new Vector2(0, 1));
                case Good.Meat: return Cached("carry_meat", () => { var g = new PixelCanvas(8, 8); g.R(2, 1, 4, 5, "#a0503a"); g.R(1, 3, 6, 3, "#a0503a"); g.R(2, 1, 4, 1, "#c8704a"); g.R(3, 6, 2, 1, "#e8d8c0"); g.Outline(OL); return g; }, new Vector2(0, 1));
                case Good.Bread: return Cached("carry_bread", () => { var g = new PixelCanvas(10, 7); g.R(1, 3, 8, 3, "#b8844a"); g.R(1, 3, 8, 1, "#d8a868"); g.R(2, 1, 3, 2, "#c8863a"); g.R(5, 1, 3, 2, "#e0a050"); g.R(4, 0, 2, 1, "#8a5a34"); g.Outline(OL); return g; }, new Vector2(0, 1));
                case Good.Wool: case Good.Clothes:
                    return Cached("carry_bundle" + (int)good, () => { var g = new PixelCanvas(9, 7); string c = good == Good.Wool ? "#ece6d8" : "#5a7aaa", d = good == Good.Wool ? "#c8bca4" : "#46628a"; g.R(1, 1, 7, 5, c); g.R(1, 3, 7, 1, d); g.R(4, 1, 1, 5, "#8a6a4a"); g.Outline(OL); return g; }, new Vector2(0, 1));
                case Good.Hides: case Good.Leather:
                    return Cached("carry_hide" + (int)good, () => { var g = new PixelCanvas(11, 5); string c = good == Good.Leather ? "#7a4a2a" : "#b88a5a"; g.R(1, 1, 9, 3, c); g.R(1, 1, 9, 1, good == Good.Leather ? "#9a6a42" : "#d0a878"); g.Disc(2, 2.5f, 1.2f, "#5a341c"); g.Outline(OL); return g; }, new Vector2(0, 1));
                case Good.Beer: return Cached("carry_keg", () => { var g = new PixelCanvas(8, 8); g.R(1, 1, 6, 6, "#8a5a34"); g.R(5, 1, 2, 6, "#6e4428"); g.R(1, 2, 6, 1, "#5a5a5a"); g.R(1, 5, 6, 1, "#5a5a5a"); g.Outline(OL); return g; }, new Vector2(0, 1));
                default: return CarriedSack();
            }
        }

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

        // ------------------------------------------------------------------ livestock
        /// <summary>A grazing cow, side on, facing left: frame 0 head down at the grass, 1 head up. Pivot at the feet, centred.</summary>
        public static Sprite Cow(int variant, int frame)
        {
            return Cached("cow" + variant + frame, () =>
            {
                var g = new PixelCanvas(16, 11);
                string[][] coats = { new[] { "#8a5a34", "#6e4428", "#f4ecd8" }, new[] { "#3a2e28", "#2a201c", "#f4ecd8" }, new[] { "#c8a070", "#a8804e", "#f4ecd8" }, new[] { "#a8442a", "#86341f", "#f4ecd8" } };
                var c = coats[variant % coats.Length];
                g.R(4, 2, 10, 5, c[0]); g.R(4, 6, 10, 1, c[1]); g.R(13, 2, 2, 3, c[0]); g.D(14, 1, c[1]);   // body and rump, tail
                if (variant % 2 == 1) { g.R(7, 3, 3, 2, c[2]); g.D(11, 2, c[2]); }                                // patches
                g.R(5, 7, 1, 3, c[1]); g.R(7, 7, 1, 3, c[1]); g.R(11, 7, 1, 3, c[1]); g.R(13, 7, 1, 3, c[1]);   // legs
                if (frame == 0) { g.R(1, 5, 4, 3, c[0]); g.D(1, 7, "#2a1c12"); g.D(3, 5, c[1]); }               // head down, grazing
                else { g.R(1, 1, 4, 3, c[0]); g.D(1, 3, "#2a1c12"); g.D(2, 2, "#1a1210"); g.D(4, 0, "#e8dcc4"); }  // head up, a horn
                g.Outline(OL);
                g.RBlend(4, 10, 10, 1, new Color32(0, 0, 0, 60));
                return g;
            }, new Vector2(0.5f, 1f / 11f));
        }

        /// <summary>A sheep: a white fleece on black legs. Pivot at the feet, centred.</summary>
        public static Sprite Sheep(int frame)
        {
            return Cached("sheep" + frame, () =>
            {
                var g = new PixelCanvas(11, 9);
                g.R(3, 2, 7, 4, "#f0ece0"); g.R(4, 1, 5, 1, "#f0ece0"); g.R(3, 5, 7, 1, "#d4ccbc"); g.D(5, 2, "#ffffff");
                g.R(4, 6, 1, 2, "#2a201c"); g.R(8, 6, 1, 2, "#2a201c");
                if (frame == 0) { g.R(1, 4, 2, 2, "#3a2e28"); } else { g.R(1, 2, 2, 2, "#3a2e28"); g.D(0, 3, "#3a2e28"); }
                g.Outline(OL);
                g.RBlend(3, 8, 7, 1, new Color32(0, 0, 0, 55));
                return g;
            }, new Vector2(0.5f, 1f / 9f));
        }

        /// <summary>A steer led on a rope behind a courier, walking left. Pivot at the feet, right edge (where the rope is held).</summary>
        public static Sprite Steer(int frame)
        {
            return Cached("steer" + frame, () =>
            {
                var g = new PixelCanvas(18, 12);
                g.R(6, 3, 10, 5, "#8a5a34"); g.R(6, 7, 10, 1, "#6e4428"); g.R(15, 3, 2, 3, "#8a5a34");
                g.R(3, 2, 4, 3, "#8a5a34"); g.D(3, 4, "#2a1c12"); g.D(5, 1, "#e8dcc4");
                int a = frame == 0 ? 0 : 1;
                g.R(7 + a, 8, 1, 3, "#6e4428"); g.R(9 - a, 8, 1, 3, "#6e4428"); g.R(13 + a, 8, 1, 3, "#6e4428"); g.R(15 - a, 8, 1, 3, "#6e4428");
                g.Line(3, 3, 0, 1, "#c8b88a");
                g.Outline(OL);
                return g;
            }, new Vector2(0f, 1f / 12f));
        }

        /// <summary>Horses tied at a hotel's hitching rail: one to three. Pivot bottom-left.</summary>
        public static Sprite Horses(int n)
        {
            n = Mathf.Clamp(n, 1, 3);
            return Cached("horses" + n, () =>
            {
                var g = new PixelCanvas(30, 13);
                string[][] coats = { new[] { "#6e4428", "#4e3020", "#2a1c12" }, new[] { "#c8a070", "#a8804e", "#f4ecd8" }, new[] { "#3a3434", "#2a2424", "#5a5050" } };
                for (int i = 0; i < n; i++)
                {
                    var c = coats[i]; int x = 1 + i * 9;
                    g.R(x + 2, 4, 5, 4, c[0]); g.R(x + 2, 7, 5, 1, c[1]);   // body, seen end-on-ish from behind the rail
                    g.R(x + 3, 0, 3, 4, c[0]); g.D(x + 3, 0, c[2]); g.D(x + 5, 0, c[2]); g.R(x + 4, 1, 1, 3, c[2]);   // head and mane
                    g.R(x + 2, 8, 1, 4, c[1]); g.R(x + 6, 8, 1, 4, c[1]);
                    g.R(x + 1, 5, 7, 2, "#8a3a2a"); g.D(x + 4, 4, "#c8a050");   // saddle and blanket
                }
                g.Outline(OL);
                return g;
            }, new Vector2(0, 0));
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
