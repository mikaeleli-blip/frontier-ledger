using System.Collections.Generic;
using FrontierLedger.Simulation;
using UnityEngine;

namespace FrontierLedger.Game.Art
{
    /// <summary>Trees, boulders, settlers and chickens.</summary>
    public static class NatureArt
    {
        const string SNOW = "#f2f6f8";
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        static Sprite Cached(string key, System.Func<PixelCanvas> make)
        {
            Sprite s;
            if (!cache.TryGetValue(key, out s)) { s = make().ToSprite(key); cache[key] = s; }
            return s;
        }

        static void Shadow(PixelCanvas g, int x, int y, int w, byte a) { g.RBlend(x, y, w, 1, new Color32(0, 0, 0, a)); }

        // ------------------------------------------------------------------ trees & rocks
        public struct Placement { public Sprite Sprite; public float Px, Py; }

        /// <summary>Picks the sprite and pixel offset for the tree or boulder on a tile.</summary>
        public static Placement At(int tx, int ty, byte tile, int season)
        {
            int ox = Mathf.RoundToInt((Noise.Hash(tx, ty, 21) - 0.5f) * 4);
            int oy = -Mathf.RoundToInt(Noise.Hash(tx, ty, 22) * 2);
            if (tile == Tile.Rock)
            {
                int v = Noise.Hash(tx, ty, 23) < 0.5f ? 1 : 0;
                return new Placement { Sprite = Rock(season, v), Px = tx * 16 + 1 + ox, Py = ty * 16 + 5 + oy };
            }
            bool pine = Noise.Hash(tx, ty, 5) < (ty < Balance.Rows / 2 ? 0.55f : 0.3f);
            if (pine) return new Placement { Sprite = Pine(season), Px = tx * 16 + 1 + ox, Py = ty * 16 - 8 + oy };
            return new Placement { Sprite = Oak(season, Noise.Hash(tx, ty, 6) < 0.5f ? 0 : 1), Px = tx * 16 - 1 + ox, Py = ty * 16 - 6 + oy };
        }

        static readonly string[][][] OakPal =
        {
            new[] { new[] { "#2f5a25", "#4e8a35", "#6aa842", "#93c75a" }, new[] { "#2f5a25", "#4e8a35", "#6aa842", "#93c75a" } },
            new[] { new[] { "#1f3a1c", "#2f5e2a", "#3f7a32", "#5f9a42" }, new[] { "#1f3a1c", "#2f5e2a", "#3f7a32", "#5f9a42" } },
            new[] { new[] { "#5a2a12", "#a8501f", "#d0742a", "#eba04a" }, new[] { "#5a4012", "#b0821f", "#d8ac36", "#f0d070" } },
        };

        public static Sprite Oak(int si, int v)
        {
            return Cached("oak" + si + v, () =>
            {
                var g = new PixelCanvas(18, 22);
                Shadow(g, 4, 20, 10, 70); Shadow(g, 3, 21, 12, 38);
                g.R(8, 12, 2, 9, "#6b4a2e"); g.R(9, 14, 1, 6, "#4e3420"); g.D(7, 20, "#6b4a2e"); g.D(10, 20, "#6b4a2e");
                if (si == 3)
                {
                    const string br = "#5a4030";
                    g.R(8, 6, 2, 8, "#6b4a2e");
                    g.Line(8, 10, 4, 5, br); g.Line(9, 9, 14, 4, br); g.Line(8, 7, 6, 2, br); g.Line(9, 6, 12, 1, br); g.Line(5, 6, 3, 4, br); g.Line(13, 5, 15, 4, br);
                    foreach (var p in new[] { new[] { 4, 4 }, new[] { 14, 3 }, new[] { 6, 1 }, new[] { 12, 0 }, new[] { 3, 3 }, new[] { 15, 3 }, new[] { 8, 5 } }) g.D(p[0], p[1], SNOW);
                    return g;
                }
                var pal = OakPal[si][v];
                float cx = 9, cy = v == 1 ? 8f : 7.5f, r = v == 1 ? 6.2f : 7f;
                System.Func<int, int, bool> inside = (x, y) =>
                {
                    if (x < 0 || y < 0 || x >= 18 || y >= 16) return false;
                    float dx = x + 0.5f - cx, dy = (y + 0.5f - cy) * 1.12f;
                    return Mathf.Sqrt(dx * dx + dy * dy) + (Noise.Hash(x, y, v + 50) - 0.5f) * 1.6f < r;
                };
                for (int y = 0; y < 16; y++)
                    for (int x = 0; x < 18; x++)
                    {
                        if (!inside(x, y)) continue;
                        string col;
                        if (!inside(x - 1, y) || !inside(x + 1, y) || !inside(x, y - 1) || !inside(x, y + 1)) col = pal[0];
                        else
                        {
                            float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                            float s = (-dx * 0.55f - dy * 0.85f) / r + (Noise.Hash(x, y, v + 70) - 0.5f) * 0.5f;
                            col = s > 0.42f ? pal[3] : s < -0.18f ? pal[1] : pal[2];
                        }
                        g.D(x, y, col);
                        if (si == 0 && v == 1 && col != pal[0] && Noise.Hash(x, y, 77) < 0.13f) g.D(x, y, "#f4c6d2");
                    }
                return g;
            });
        }

        public static Sprite Pine(int si)
        {
            return Cached("pine" + si, () =>
            {
                var g = new PixelCanvas(14, 24);
                Shadow(g, 3, 22, 8, 70);
                g.R(6, 18, 2, 4, "#5a3a22");
                string[] P = { "#16301f", "#234a2f", "#2f5d3a", "#457a4f" };
                for (int i = 0; i < 3; i++)
                {
                    int y0 = 1 + i * 5; float mh = 3 + i * 1.6f;
                    for (int j = 0; j < 8; j++)
                    {
                        int hw = Mathf.Max(1, Mathf.RoundToInt((j + 1) / 8f * mh)), y = y0 + j;
                        for (int x = 7 - hw; x < 7 + hw; x++)
                        {
                            string col = x < 7 ? P[2] : P[1];
                            if (x == 7 - hw || x == 6 + hw || j == 7) col = P[0];
                            else if (x < 7 && Noise.Hash(x, y, 31) < 0.25f) col = P[3];
                            if (si == 3 && (j < 2 || (j < 6 && Noise.Hash(x, y, 33) < 0.18f)) && col != P[0]) col = SNOW;
                            g.D(x, y, col);
                        }
                    }
                }
                return g;
            });
        }

        public static Sprite Rock(int si, int v)
        {
            return Cached("rock" + si + v, () =>
            {
                var g = new PixelCanvas(14, 11);
                Shadow(g, 2, 9, 10, 64);
                float rx = v == 1 ? 4.6f : 6f, ry = v == 1 ? 3f : 3.8f, cx = 7, cy = v == 1 ? 6f : 5.6f;
                string[] P = { "#3e3a36", "#6c665e", "#8d877d", "#b2aca1" };
                System.Func<int, int, bool> ins = (x, y) =>
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    return dx * dx + dy * dy + (Noise.Hash(x, y, v + 90) - 0.5f) * 0.25f < 1;
                };
                for (int y = 0; y < 11; y++)
                    for (int x = 0; x < 14; x++)
                    {
                        if (!ins(x, y)) continue;
                        bool edge = !ins(x - 1, y) || !ins(x + 1, y) || !ins(x, y - 1) || !ins(x, y + 1);
                        string col;
                        if (edge) col = P[0];
                        else
                        {
                            float s = -(x + 0.5f - cx) / rx * 0.6f - (y + 0.5f - cy) / ry * 0.9f + (Noise.Hash(x, y, 91) - 0.5f) * 0.4f;
                            col = s > 0.5f ? P[3] : s < -0.3f ? P[1] : P[2];
                            if (si == 3 && y + 0.5f < cy - 0.4f) col = SNOW;
                            else if (si < 2 && y < cy - 1 && Noise.Hash(x, y, 93) < 0.2f) col = "#6a8a3a";
                        }
                        g.D(x, y, col);
                    }
                g.D(8, Mathf.RoundToInt(cy), P[1]); g.D(9, Mathf.RoundToInt(cy) + 1, P[1]);
                return g;
            });
        }

        // ------------------------------------------------------------------ settlers
        public static readonly Dictionary<Role, string> RoleColor = new Dictionary<Role, string>
        {
            { Role.Owner, "#d4a542" }, { Role.Worker, "#e8dcc0" }, { Role.Unemployed, "#8f8f8f" },
            { Role.Sheriff, "#3f8fcb" }, { Role.Deputy, "#7cc0e8" }, { Role.Mayor, "#9a3a62" }
        };
        static readonly string[] Skin = { "#f1c9a0", "#d9a477", "#a8714a", "#7a4e30" };
        static readonly string[] Hats = { "#b08a5a", "#6b4428", "#d8b55a", "#4a3a2e" };
        static readonly string[] Pants = { "#4a3a2e", "#3e4f66", "#5a4a3a" };

        /// <summary>A 5px-wide settler, pivot at the feet. Frame 1 is the mid-stride pose.</summary>
        public static Sprite Person(Citizen p, int frame)
        {
            bool jailed = p.Jail > 0;
            int si = (int)(p.Seed * 4) % 4, hi = (int)(p.Seed * 16) % 4, li = (int)(p.Seed * 64) % 3;
            string key = "p" + (jailed ? "J" : p.Role.ToString()) + si + hi + li + frame;
            Sprite s;
            if (cache.TryGetValue(key, out s)) return s;
            var g = new PixelCanvas(5, 12);
            int y = 0;
            bool top = !jailed && (p.Role == Role.Owner || p.Role == Role.Mayor);
            if (top) { g.R(1, 0, 3, 2, "#2a2420"); g.R(0, 2, 5, 1, "#2a2420"); g.R(1, 1, 3, 1, p.Role == Role.Mayor ? "#b5432f" : "#6b4428"); y = 3; }
            else if (jailed) { g.R(1, 0, 3, 1, "#5a4030"); y = 1; }
            else
            {
                string hat = p.Role == Role.Sheriff ? "#c49364" : p.Role == Role.Deputy ? "#8a5d3a" : Hats[hi];
                g.R(1, 0, 3, 1, hat); g.R(0, 1, 5, 1, hat); y = 2;
            }
            g.R(1, y, 3, 2, Skin[si]); y += 2;
            string bc = jailed ? "#d8d8d8" : RoleColor[p.Role];
            g.R(0, y, 5, 3, bc); g.D(0, y + 2, Skin[si]); g.D(4, y + 2, Skin[si]);
            if (!jailed)
            {
                if (p.Role == Role.Worker) { g.D(1, y, "#4a3a2e"); g.D(3, y, "#4a3a2e"); g.D(1, y + 1, "#4a3a2e"); g.D(3, y + 1, "#4a3a2e"); }
                if (p.Role == Role.Owner) { g.D(2, y, "#f4efe4"); g.D(2, y + 1, "#8a6a2a"); }
                if (p.Role == Role.Sheriff || p.Role == Role.Deputy) g.D(1, y + 1, "#f2d04a");
                if (p.Role == Role.Mayor) { g.D(1, y, "#e6c15a"); g.D(2, y + 1, "#e6c15a"); g.D(3, y + 2, "#e6c15a"); }
            }
            else g.R(0, y + 1, 5, 1, "#3a3a3a");
            y += 3;
            string pc = jailed ? "#3a3a3a" : Pants[li];
            if (frame == 0) { g.R(1, y, 1, 2, pc); g.R(3, y, 1, 2, pc); }
            else { g.D(1, y, pc); g.D(3, y, pc); g.D(0, y + 1, pc); g.D(4, y + 1, pc); }
            y += 2;
            // crop to the drawn height so the pivot sits at the feet
            var c = new PixelCanvas(5, y);
            for (int yy = 0; yy < y; yy++) for (int xx = 0; xx < 5; xx++) c.Set(xx, yy, g.Get(xx, yy));
            var t = c.ToTexture(key);
            s = Sprite.Create(t, new Rect(0, 0, 5, y), new Vector2(2.5f / 5f, 0f), Pixels.PerUnit, 0, SpriteMeshType.FullRect);
            s.name = key;
            cache[key] = s;
            return s;
        }

        /// <summary>A chicken, pivot at the feet. Frame 1 is pecking.</summary>
        public static Sprite Chicken(int frame)
        {
            string key = "chick" + frame;
            Sprite s;
            if (cache.TryGetValue(key, out s)) return s;
            var g = new PixelCanvas(5, 5);
            g.RBlend(0, 4, 3, 1, new Color32(0, 0, 0, 64));
            g.R(0, 2, 3, 2, "#f4f1e6"); g.D(1, 4, "#d8742a");
            if (frame == 1) { g.D(3, 2, "#f4f1e6"); g.D(3, 1, "#d9534a"); g.D(4, 3, "#e8a23a"); }
            else { g.D(2, 1, "#f4f1e6"); g.D(2, 0, "#d9534a"); g.D(3, 1, "#e8a23a"); }
            var t = g.ToTexture(key);
            s = Sprite.Create(t, new Rect(0, 0, 5, 5), new Vector2(0.2f, 0f), Pixels.PerUnit, 0, SpriteMeshType.FullRect);
            s.name = key;
            cache[key] = s;
            return s;
        }

        static Sprite white;
        /// <summary>A single white pixel, used for overlays, particles and outlines.</summary>
        public static Sprite White
        {
            get
            {
                if (white == null)
                {
                    var g = new PixelCanvas(1, 1); g.Set(0, 0, new Color32(255, 255, 255, 255));
                    var t = g.ToTexture("white");
                    white = Sprite.Create(t, new Rect(0, 0, 1, 1), new Vector2(0f, 1f), Pixels.PerUnit, 0, SpriteMeshType.FullRect);
                }
                return white;
            }
        }
    }
}
