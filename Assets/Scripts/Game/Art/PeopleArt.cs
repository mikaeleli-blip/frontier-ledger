using System.Collections.Generic;
using FrontierLedger.Simulation;
using UnityEngine;

namespace FrontierLedger.Game.Art
{
    /// <summary>
    /// Settlers in a chunky, big-headed farm-sim style: 4 facing directions, a 3-frame walk,
    /// hair, skin, clothes and hats that show who they are and what they do.
    /// </summary>
    public static class PeopleArt
    {
        public const int Down = 0, Up = 1, Left = 2, Right = 3;
        const string OL = "#2b1d1a";

        static readonly string[][] Skins = { new[] { "#f6d2b0", "#e3b48e" }, new[] { "#e8b88a", "#cf9a6c" }, new[] { "#c68a5a", "#a8703f" }, new[] { "#8a5a3a", "#6e4428" } };
        static readonly string[] Hairs = { "#3b2618", "#6b4428", "#c8a050", "#a0401a", "#1e1a1a", "#8a5a2a" };
        static readonly string[][] WorkerShirts = { new[] { "#4a6fa5", "#3a5a88" }, new[] { "#b5432f", "#8f3322" }, new[] { "#5a8a4a", "#46703a" }, new[] { "#c49a3a", "#a07a2a" }, new[] { "#8a6a9a", "#6e5280" } };
        static readonly string[][] Dresses = { new[] { "#8a3a4a", "#6e2c3a" }, new[] { "#4a5a8a", "#3a486e" }, new[] { "#6a8a5a", "#526e46" }, new[] { "#a8734a", "#8a5a38" }, new[] { "#b56a8a", "#94526e" } };
        static readonly string[][] Pants = { new[] { "#5a3a2a", "#46301f" }, new[] { "#3e4f66", "#303e52" }, new[] { "#5a5a52", "#46463f" } };

        sealed class Look
        {
            public bool Female, Child, Jailed;
            public string Skin, SkinShade, Hair, HairShade, Shirt, ShirtShade, Pant, PantShade, Shoe = "#4a2f1e";
            public string Hat;           // null, straw, cowboy, cowboyTan, top, bowler, cap
            public string HatColor;
            public bool Vest, Coat, Badge, Suspenders, LongHair, Bun;
            public string Key;
        }

        static string Darker(string hex, float k = 0.78f)
        {
            var c = PixelCanvas.C(hex);
            return "#" + ((int)(c.r * k)).ToString("x2") + ((int)(c.g * k)).ToString("x2") + ((int)(c.b * k)).ToString("x2");
        }

        static Look LookOf(Citizen p)
        {
            int si = (int)(p.Seed * 4) % 4, hi = (int)(p.Seed * 23) % Hairs.Length, ci = (int)(p.Seed * 51) % 5, pi = (int)(p.Seed * 77) % 3;
            var L = new Look { Female = p.Female, Child = p.IsChild, Jailed = p.Jail > 0 };
            L.Skin = Skins[si][0]; L.SkinShade = Skins[si][1];
            L.Hair = p.Age > 56 ? "#b8b0a8" : Hairs[hi]; L.HairShade = Darker(L.Hair, 0.75f);
            L.Pant = Pants[pi][0]; L.PantShade = Pants[pi][1];
            var shirt = L.Female ? Dresses[ci] : WorkerShirts[ci];
            L.Shirt = shirt[0]; L.ShirtShade = shirt[1];
            L.LongHair = L.Female && (int)(p.Seed * 97) % 3 != 0;
            L.Bun = L.Female && !L.LongHair;
            if (L.Jailed) { L.Shirt = "#d8d4c8"; L.ShirtShade = "#a8a49a"; L.Pant = "#3a3a3a"; L.PantShade = "#2a2a2a"; }
            else if (!L.Child)
                switch (p.Role)
                {
                    case Role.Owner: if (!L.Female) { L.Hat = "bowler"; L.HatColor = "#4a3226"; } L.Vest = true; L.Shirt = "#ece4d2"; L.ShirtShade = "#cfc4ad"; break;
                    case Role.Mayor: L.Hat = "top"; L.HatColor = "#2a2630"; L.Coat = true; L.Shirt = "#2e2a33"; L.ShirtShade = "#1e1a22"; break;
                    case Role.Sheriff: L.Hat = "cowboy"; L.HatColor = "#7a5232"; L.Badge = true; L.Shirt = "#c4a46a"; L.ShirtShade = "#a2864e"; break;
                    case Role.Deputy: L.Hat = "cowboy"; L.HatColor = "#b08a5a"; L.Badge = true; L.Shirt = "#6a8ab5"; L.ShirtShade = "#526e94"; break;
                    case Role.Builder: L.Hat = "cap"; L.HatColor = "#d0743a"; L.Suspenders = true; L.Shirt = "#d8c8a8"; L.ShirtShade = "#b8a888"; break;
                    case Role.Worker: if (!L.Female && (int)(p.Seed * 13) % 2 == 0) { L.Hat = "straw"; L.HatColor = "#e0c070"; } else if (!L.Female) { L.Hat = "cap"; L.HatColor = "#6a5a4a"; } break;
                    default: L.Shirt = L.Female ? L.Shirt : "#9a8e7a"; L.ShirtShade = L.Female ? L.ShirtShade : "#7e735f"; break;
                }
            L.Key = string.Join("|", new[] { L.Female ? "f" : "m", L.Child ? "c" : "a", L.Skin, L.Hair, L.Shirt, L.Pant, L.Hat ?? "-", L.HatColor ?? "-",
                L.Vest ? "v" : "", L.Coat ? "c" : "", L.Badge ? "b" : "", L.Suspenders ? "s" : "", L.LongHair ? "l" : "", L.Bun ? "u" : "" });
            return L;
        }

        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        /// <summary>Settler sprite, pivot at the feet. frame: 0 standing, 1 and 2 mid-stride.</summary>
        public static Sprite Get(Citizen p, int dir, int frame)
        {
            var L = LookOf(p);
            string key = L.Key + dir + frame;
            Sprite s;
            if (cache.TryGetValue(key, out s)) return s;
            var g = Draw(L, dir, frame);
            s = g.ToSprite(key, new Vector2(5.5f / 11f, 1.5f / g.H));
            cache[key] = s;
            return s;
        }

        /// <summary>Canvas 11 wide; adults stand 17 px tall, children 14 (big-headed). Feet sit two rows above the bottom.</summary>
        static PixelCanvas Draw(Look L, int dir, int frame)
        {
            if (dir == Right) return Draw(L, Left, frame).Mirrored();
            int torso = L.Child ? 2 : 4, legs = L.Child ? 2 : 3;
            const int hy = 3;                         // top of the head
            int ty = hy + 7, ly = ty + torso, fy = ly + legs;
            int H = fy + 3;
            var g = new PixelCanvas(11, H);
            const int ox = 1;
            System.Action<int, int, string> P = (x, y, c) => g.D(x + ox, y, c);
            System.Action<int, int, int, int, string> RR = (x, y, w, h, c) => g.R(x + ox, y, w, h, c);
            bool side = dir == Left;
            bool skirt = L.Female && !L.Child;
            int l1 = frame == 1 ? 1 : 0, l2 = frame == 2 ? 1 : 0;

            // ---------- legs / skirt ----------
            if (!side)
            {
                if (skirt)
                {
                    int sway = frame == 1 ? -1 : frame == 2 ? 1 : 0;
                    RR(1, ly - 1, 7, 1, L.Shirt);
                    for (int r = 0; r < legs; r++) RR(1 + (r == legs - 1 ? sway : 0), ly + r, 7, 1, r == legs - 1 ? L.ShirtShade : L.Shirt);
                    RR(6, ly - 1, 1, legs + 1, L.ShirtShade);
                    RR(2, fy - l1, 2, 1, L.Shoe); RR(5, fy - l2, 2, 1, L.Shoe);
                }
                else
                {
                    RR(2, ly, 2, legs - l1, L.Pant); RR(2, fy - l1, 2, 1, L.Shoe);
                    RR(5, ly, 2, legs - l2, L.PantShade); RR(5, fy - l2, 2, 1, L.Shoe);
                    RR(4, ly, 1, 1, L.Pant);
                }
            }
            else
            {
                if (skirt)
                {
                    RR(2, ly - 1, 5, 1, L.Shirt);
                    for (int r = 0; r < legs; r++) RR(1 + (frame == 0 ? 1 : 0), ly + r, frame == 0 ? 5 : 6, 1, r == legs - 1 ? L.ShirtShade : L.Shirt);
                    if (frame == 0) RR(2, fy, 3, 1, L.Shoe); else { RR(1, fy, 2, 1, L.Shoe); RR(5, fy, 2, 1, L.Shoe); }
                }
                else if (frame == 0) { RR(3, ly, 2, legs, L.Pant); RR(4, ly, 1, legs, L.PantShade); RR(2, fy, 3, 1, L.Shoe); }
                else
                {
                    RR(2, ly, 2, 1, L.Pant); RR(1, ly + 1, 2, legs - 1, L.Pant); RR(0, fy, 3, 1, L.Shoe);
                    RR(4, ly, 2, 1, L.PantShade); RR(5, ly + 1, 2, legs - 1, L.PantShade); RR(5, fy, 2, 1, L.Shoe);
                }
            }

            // ---------- torso and arms ----------
            if (!side)
            {
                RR(2, ty, 5, torso, L.Shirt); RR(1, ty, 7, 1, L.Shirt);
                RR(6, ty + 1, 1, torso - 1, L.ShirtShade);
                if (!skirt) RR(2, ty + torso - 1, 5, 1, Darker(L.Pant, 0.85f));
                if (L.Vest && dir == Down) { RR(2, ty, 1, torso - 1, "#5a3a2a"); RR(6, ty, 1, torso - 1, "#5a3a2a"); }
                if (L.Coat && dir == Down) { RR(4, ty, 1, 2, "#ece4d2"); P(4, ty + 1, "#b5432f"); }
                if (L.Suspenders) { P(3, ty, "#5a3a2a"); P(5, ty, "#5a3a2a"); RR(3, ty + 1, 1, torso - 2, "#5a3a2a"); RR(5, ty + 1, 1, torso - 2, "#5a3a2a"); }
                if (L.Badge && dir == Down) P(5, ty + 1, "#f2d04a");
                int arm = L.Child ? 1 : 2, la = frame == 1 ? -1 : frame == 2 ? 1 : 0;
                RR(1, ty + 1, 1, arm + la, L.Shirt); P(1, ty + 1 + arm + la, L.Skin);
                RR(7, ty + 1, 1, arm - la, L.ShirtShade); P(7, ty + 1 + arm - la, L.SkinShade);
            }
            else
            {
                RR(2, ty, 4, torso, L.Shirt); RR(5, ty, 1, torso, L.ShirtShade);
                if (!skirt) RR(2, ty + torso - 1, 4, 1, Darker(L.Pant, 0.85f));
                if (L.Badge) P(2, ty + 1, "#f2d04a");
                if (L.Coat) P(2, ty, "#ece4d2");
                int arm = L.Child ? 1 : 2, sw = frame == 1 ? -1 : frame == 2 ? 1 : 0;
                RR(3 + sw, ty + 1, 2, arm, L.ShirtShade); RR(3 + sw, ty + 1 + arm, 2, 1, L.Skin);
            }

            // ---------- head ----------
            if (!side)
            {
                RR(2, hy, 5, 1, L.Skin); RR(1, hy + 1, 7, 5, L.Skin); RR(2, hy + 6, 5, 1, L.Skin);
                RR(6, hy + 2, 2, 4, L.SkinShade);
                if (dir == Down)
                {
                    RR(2, hy, 5, 1, L.Hair); RR(1, hy + 1, 7, 2, L.Hair); P(1, hy + 3, L.Hair); P(7, hy + 3, L.HairShade);
                    RR(2, hy + 1, 3, 1, Lighter(L.Hair)); P(2, hy + 3, L.Hair);
                    if (L.LongHair) { RR(0, hy + 2, 1, 6, L.Hair); RR(8, hy + 2, 1, 6, L.HairShade); RR(1, hy + 3, 1, 4, L.Hair); RR(7, hy + 3, 1, 4, L.HairShade); }
                    if (L.Bun) RR(3, hy - 1, 3, 1, L.Hair);
                    RR(3, hy + 3, 1, 2, OL); RR(5, hy + 3, 1, 2, OL);
                    P(2, hy + 5, "#e89a8a"); P(6, hy + 5, "#d88a7a");
                }
                else
                {
                    RR(2, hy, 5, 1, L.Hair); RR(1, hy + 1, 7, 5, L.Hair); RR(2, hy + 6, 5, 1, L.HairShade); RR(6, hy + 2, 2, 4, L.HairShade);
                    if (!L.LongHair) { P(1, hy + 3, L.Skin); P(7, hy + 3, L.SkinShade); }
                    else RR(1, hy + 6, 7, 3, L.Hair);
                    if (L.Bun) RR(3, hy - 1, 3, 2, L.HairShade);
                }
            }
            else
            {
                RR(2, hy, 4, 1, L.Skin); RR(1, hy + 1, 6, 5, L.Skin); RR(2, hy + 6, 4, 1, L.Skin);
                P(0, hy + 3, L.Skin);
                RR(2, hy, 4, 1, L.Hair); RR(1, hy + 1, 6, 2, L.Hair); RR(4, hy + 3, 3, 3, L.Hair); RR(6, hy + 3, 1, 3, L.HairShade);
                if (L.LongHair) RR(4, hy + 3, 3, 6, L.Hair);
                if (L.Bun) RR(5, hy - 1, 2, 2, L.HairShade);
                P(4, hy + 3, L.SkinShade);
                RR(2, hy + 3, 1, 2, OL);
                P(1, hy + 5, "#e89a8a");
            }

            // ---------- hat ----------
            if (L.Hat != null)
            {
                string hc = L.HatColor, hs = Darker(hc, 0.72f);
                int by = hy + 1;
                switch (L.Hat)
                {
                    case "straw": RR(2, by - 2, 5, 2, hc); RR(2, by - 1, 5, 1, "#8a5a2a"); RR(-1, by, 11, 1, hc); break;
                    case "cowboy": RR(2, by - 2, 5, 2, hc); P(4, by - 2, hs); RR(0, by, 9, 1, hc); P(-1, by - 1, hc); P(9, by - 1, hc); break;
                    case "top": RR(2, by - 4, 5, 4, hc); RR(2, by - 1, 5, 1, "#b5432f"); RR(1, by, 7, 1, hc); RR(6, by - 4, 1, 4, hs); break;
                    case "bowler": RR(3, by - 2, 3, 1, hc); RR(2, by - 1, 5, 1, hc); RR(1, by, 7, 1, hs); break;
                    case "cap":
                        RR(1, by - 1, 7, 2, hc); RR(2, by - 2, 5, 1, hc); RR(6, by - 1, 2, 2, hs);
                        if (dir == Down) RR(1, by + 1, 7, 1, hs); else if (dir == Left) RR(-1, by + 1, 3, 1, hs);
                        break;
                }
            }

            g.Outline(OL);
            g.RBlend(ox + 1, H - 1, 7, 1, new Color32(0, 0, 0, 70));
            return g;
        }

        static string Lighter(string hex)
        {
            var c = PixelCanvas.C(hex);
            return "#" + Mathf.Min(255, c.r + 30).ToString("x2") + Mathf.Min(255, c.g + 26).ToString("x2") + Mathf.Min(255, c.b + 20).ToString("x2");
        }
    }
}
