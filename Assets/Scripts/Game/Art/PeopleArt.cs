using System.Collections.Generic;
using System.Text;
using FrontierLedger.Simulation;
using UnityEngine;

namespace FrontierLedger.Game.Art
{
    /// <summary>
    /// Settlers in a chunky, big-headed farm-sim style: 4 facing directions, a 3-frame walk.
    /// Every settler gets their own look from their id: complexion, hair colour and style, beards, build and height,
    /// shirts, dresses and patterns, hats, bonnets and kerchiefs. Children take after their mother or father.
    /// Roles and trades show through: lawmen's badges, the mayor's top hat, the preacher's collar, tradesmen's aprons.
    /// </summary>
    public static class PeopleArt
    {
        public const int Down = 0, Up = 1, Left = 2, Right = 3;
        const string OL = "#2b1d1a";

        // complexions: pale, fair, peach, ruddy, weathered
        static readonly string[][] Skins = { new[] { "#fbe3cf", "#ebc8ae" }, new[] { "#f6d2b0", "#e3b48e" }, new[] { "#f3c9a6", "#dfad88" }, new[] { "#f0c0a0", "#d9a080" }, new[] { "#ecbe98", "#d4a27c" } };
        // black, dark brown, brown, chestnut, light brown, auburn, red, ginger, strawberry, dark blond, blond, flaxen
        static readonly string[] Hairs = { "#2e2420", "#3b2618", "#5a3a22", "#7a4a26", "#8a6a42", "#8a3a1a", "#a8461e", "#c86a2a", "#d0905a", "#b08a4a", "#d8b060", "#e8d08a" };
        static readonly string[] ManShirts = { "#9a8e7a", "#e8e0cc", "#4a6fa5", "#6a8ab5", "#b5432f", "#8a3a2a", "#5a8a4a", "#c49a3a", "#8a6a9a", "#7a7a6a", "#a87a4a", "#3e4f66", "#d8c8a8", "#5a7a7a" };
        static readonly string[] Dresses = { "#8a3a4a", "#4a5a8a", "#6a8a5a", "#a8734a", "#b56a8a", "#5a4a6a", "#c8a060", "#7a8a9a", "#3a3a4a", "#a85a3a", "#e0d0b0", "#5a7a7a", "#9a5a6a", "#6a6a8a" };
        static readonly string[] Pants = { "#5a3a2a", "#3e4f66", "#5a5a52", "#a8885a", "#2e2a2a", "#5a5a3a", "#6e4a2e", "#4a4440" };
        static readonly string[] Jackets = { "#6e4a2e", "#5a5a5a", "#2e3a5a", "#3a4a3a", "#9a7a52", "#4a3a3a" };
        static readonly string[] Vests = { "#5a3a2a", "#2e2a33", "#6a5a3a", "#3a4a3a", "#7a2a2a", "#4a4a5a", "#8a7a5a" };
        static readonly string[] HatColors = { "#6a5a4a", "#4a3226", "#2e2a2a", "#7a6a5a", "#9a7a52", "#5a5a5a", "#3a3a4a" };
        static readonly string[] Bonnets = { "#f0ece0", "#a8c0d8", "#e8b8c0", "#e8d890", "#c8a070", "#b8c8a8", "#5a6a8a" };
        static readonly string[] Necks = { "#b5432f", "#3a5a88", "#f0ece0", "#c89a3a", "#5a7a4a" };
        static readonly string[] Shawls = { "#6a4a5a", "#4a5a6a", "#8a6a4a", "#5a6a4a", "#7a3a3a" };

        sealed class Look
        {
            public bool Female, Child, Jailed;
            public string Skin, SkinShade, Hair, HairShade, Shirt, ShirtShade, Pant, PantShade, Shoe = "#4a2f1e";
            public string Hat;           // null, straw, cowboy, top, bowler, cap, parson, slouch, bonnet, kerchief
            public string HatColor;
            public string HairStyle;     // men: short, parted, shaggy, curly; women and girls: long, bun, braids, updo
            public bool Bald, Freckles, Blush;
            public string Beard, BeardColor;   // full, mustache, chops, goatee, long
            public string Pattern;       // plain, check, stripe, print
            public string Vest, Jacket, Neck, Shawl;
            public bool Coat, Badge, Suspenders, Collar, Stout, Tall, Short;
            public string Apron;         // white for the butcher, baker, barber and many a housewife; leather for smith and tanner
            public string Key;
        }

        static string Darker(string hex, float k = 0.78f)
        {
            var c = PixelCanvas.C(hex);
            return "#" + ((int)(c.r * k)).ToString("x2") + ((int)(c.g * k)).ToString("x2") + ((int)(c.b * k)).ToString("x2");
        }

        /// <summary>Where a settler works, so their clothes can show their trade. Set by the map view.</summary>
        public static System.Func<Citizen, BuildingType?> WorkplaceOf;

        static int Stem(int id) { return id * 7919 + 13; }
        static float H(int stem, int k) { return Noise.Hash(stem, k, 4111); }
        static T Pick<T>(T[] a, float r) { return a[Mathf.Clamp((int)(r * a.Length), 0, a.Length - 1)]; }

        sealed class Cached { public Look L; public long Print; }
        static readonly Dictionary<int, Cached> looks = new Dictionary<int, Cached>();

        /// <summary>A settler's look changes only with their role, trade, jail, coming of age and greying; reuse it otherwise.</summary>
        static Look LookOf(Citizen p)
        {
            if (p.Id <= 0) return Build(p, null);
            var w = WorkplaceOf != null && (p.Role == Role.Worker || p.Role == Role.Owner) ? WorkplaceOf(p) : null;
            long print = ((((long)p.Role * 64 + (w.HasValue ? (int)w.Value + 1 : 0)) * 2 + (p.Jail > 0 ? 1 : 0)) * 64 + (int)(p.Age / 3f)) * 4 + (p.Female ? 1 : 0);
            Cached c;
            if (looks.TryGetValue(p.Id, out c) && c.Print == print) return c.L;
            c = new Cached { L = Build(p, w), Print = print };
            looks[p.Id] = c;
            return c.L;
        }

        static Look Build(Citizen p, BuildingType? work)
        {
            int s = p.Id > 0 ? Stem(p.Id) : (int)(p.Seed * 1000003f);
            System.Func<int, float> R = k => H(s, k);
            var L = new Look { Female = p.Female, Child = p.IsChild, Jailed = p.Jail > 0 };

            // complexion and hair run in families: a child takes after its mother or its father
            int skinFrom = s, hairFrom = s;
            if (p.IsChild)
            {
                int a = R(1) < 0.5f ? p.MotherId : p.FatherId, b = R(2) < 0.5f ? p.MotherId : p.FatherId;
                if (a <= 0) a = p.MotherId > 0 ? p.MotherId : p.FatherId;
                if (b <= 0) b = p.MotherId > 0 ? p.MotherId : p.FatherId;
                if (a > 0) hairFrom = Stem(a);
                if (b > 0) skinFrom = Stem(b);
            }
            int si = Mathf.Clamp((int)(H(skinFrom, 4) * Skins.Length), 0, Skins.Length - 1);
            L.Skin = Skins[si][0]; L.SkinShade = Skins[si][1];
            L.Hair = Pick(Hairs, H(hairFrom, 3));
            if (p.IsChild && p.Age < 8 && R(5) < 0.35f) L.Hair = Lighter(L.Hair);   // towheaded little ones
            if (p.Age > 62) L.Hair = "#e0dcd6";
            else if (p.Age > 55) L.Hair = "#b8b0a8";
            else if (p.Age > 47 && R(6) < 0.5f) L.Hair = "#8a8078";                // salt and pepper
            L.HairShade = Darker(L.Hair, 0.75f);
            L.Freckles = si <= 2 && R(11) < 0.25f;
            L.Blush = L.Female || L.Child || si == 3 || R(10) < 0.3f;

            if (!L.Child)
            {
                L.Stout = R(12) < 0.2f;
                float h = R(13); L.Tall = h < 0.25f; L.Short = h > 0.85f;
            }

            if (L.Female)
            {
                float hs = R(7);
                L.HairStyle = L.Child ? (hs < 0.45f ? "long" : hs < 0.9f ? "braids" : "bun") : (hs < 0.3f ? "long" : hs < 0.65f ? "bun" : hs < 0.8f ? "braids" : "updo");
            }
            else
            {
                L.HairStyle = Pick(new[] { "short", "parted", "shaggy", "curly" }, R(7));
                L.Bald = !L.Child && p.Age > 38 && R(8) < (p.Age - 38) / 40f;
                if (!L.Child && p.Age >= 19)
                {
                    float r = R(9), old = p.Age > 45 ? 0.15f : 0f;
                    L.Beard = r < 0.4f - old ? null : r < 0.6f ? "full" : r < 0.76f ? "mustache" : r < 0.86f ? "chops" : r < 0.93f ? "goatee" : "long";
                    if (L.Beard == "long" && p.Age < 35) L.Beard = "full";
                    L.BeardColor = R(14) < 0.15f && p.Age < 48 ? Pick(new[] { "#a8461e", "#c86a2a", "#8a3a1a" }, R(15)) : L.Hair;
                    if (L.BeardColor != L.Hair && L.Hair.StartsWith("#e")) L.BeardColor = L.Hair;
                    if ((L.Beard == "full" || L.Beard == "long") && L.HairStyle == "shaggy") L.HairStyle = "short";   // keep the face clear
                }
            }

            // everyday clothes
            float pat = R(16);
            if (L.Female)
            {
                L.Shirt = Pick(Dresses, R(17));
                L.Pattern = pat < 0.3f ? "check" : pat < 0.45f ? "print" : "plain";
                if (L.Child) { if (R(18) < 0.45f) L.Apron = "#f4f0e6"; if (R(19) < 0.15f) { L.Hat = "bonnet"; L.HatColor = Pick(Bonnets, R(20)); } }
                else
                {
                    if (R(18) < 0.35f) L.Apron = "#f4f0e6";
                    if (p.Age > 40 && R(21) < 0.35f) L.Shawl = Pick(Shawls, R(22));
                    float hr = R(19);
                    if (hr < 0.25f) { L.Hat = "bonnet"; L.HatColor = Pick(Bonnets, R(20)); }
                    else if (hr < 0.35f) { L.Hat = "kerchief"; L.HatColor = Pick(Bonnets, R(20)); }
                }
            }
            else
            {
                L.Shirt = L.Child ? Pick(new[] { "#e8e0cc", "#6a8ab5", "#b5432f", "#5a8a4a", "#c49a3a", "#9a8e7a", "#a87a4a" }, R(17)) : Pick(ManShirts, R(17));
                L.Pattern = pat < 0.22f ? "check" : pat < 0.35f ? "stripe" : "plain";
                if (L.Child)
                {
                    if (R(18) < 0.35f) L.Suspenders = true;
                    float hr = R(19);
                    if (hr < 0.3f) { L.Hat = "cap"; L.HatColor = Pick(HatColors, R(20)); }
                    else if (hr < 0.42f) { L.Hat = "straw"; L.HatColor = "#e0c070"; }
                }
                else
                {
                    if (R(18) < 0.25f) L.Jacket = Pick(Jackets, R(23));
                    else if (R(24) < 0.25f) L.Suspenders = true;
                    if (R(25) < 0.2f) L.Vest = Pick(Vests, R(26));
                    if (R(27) < 0.2f) L.Neck = Pick(Necks, R(28));
                    float hr = R(19);
                    L.Hat = hr < 0.3f ? "straw" : hr < 0.5f ? "slouch" : hr < 0.65f ? "cap" : hr < 0.72f ? "bowler" : hr < 0.8f ? "cowboy" : null;
                    L.HatColor = L.Hat == "straw" ? Pick(new[] { "#e0c070", "#d8b860", "#e8d088" }, R(20)) : Pick(HatColors, R(20));
                }
            }
            L.Pant = Pick(Pants, R(29));
            if (L.Child && L.Suspenders && R(30) < 0.6f) L.Pant = Pick(new[] { "#3e4f66", "#6e4a2e", "#5a5a52" }, R(31));   // overalls
            L.ShirtShade = Darker(L.Shirt); L.PantShade = Darker(L.Pant);
            L.Shoe = R(32) < 0.6f ? "#4a2f1e" : "#2a2220";

            if (L.Jailed) { L.Shirt = "#d8d4c8"; L.ShirtShade = "#a8a49a"; L.Pant = "#3a3a3a"; L.PantShade = "#2a2a2a"; L.Pattern = "stripe"; L.Hat = null; L.Apron = null; L.Jacket = null; L.Vest = null; L.Shawl = null; }
            else if (!L.Child)
                switch (p.Role)
                {
                    case Role.Owner:
                        if (!L.Female) { L.Hat = R(33) < 0.15f ? "top" : "bowler"; L.HatColor = Pick(new[] { "#2a2630", "#4a3226", "#5a5a5a" }, R(34)); L.Shirt = R(35) < 0.7f ? "#ece4d2" : "#d8e0e8"; L.Pattern = "plain"; L.Jacket = null; }
                        else { L.Shirt = Pick(new[] { "#3a3a4a", "#5a4a6a", "#4a5a8a", "#6a3a4a" }, R(35)); L.Apron = null; }
                        L.Vest = L.Female ? null : Pick(Vests, R(26)); L.Neck = null;
                        break;
                    case Role.Mayor: L.Hat = "top"; L.HatColor = "#2a2630"; L.Coat = true; L.Shirt = "#2e2a33"; L.Pattern = "plain"; L.Jacket = null; L.Apron = null; L.Shawl = null; break;
                    case Role.Sheriff: L.Hat = "cowboy"; L.HatColor = "#7a5232"; L.Badge = true; L.Shirt = "#c4a46a"; L.Pattern = "plain"; L.Jacket = null; L.Apron = null; break;
                    case Role.Deputy: L.Hat = "cowboy"; L.HatColor = Pick(new[] { "#b08a5a", "#8a6a4a", "#6a5a4a" }, R(34)); L.Badge = true; L.Shirt = Pick(new[] { "#6a8ab5", "#9a8e7a", "#b5432f" }, R(35)); L.Pattern = "plain"; L.Apron = null; break;
                    case Role.Builder: L.Hat = "cap"; L.HatColor = "#d0743a"; L.Suspenders = true; L.Shirt = "#d8c8a8"; break;
                    case Role.Doctor:
                        if (!L.Female) { L.Hat = "bowler"; L.HatColor = "#2a2630"; }
                        L.Coat = true; L.Shirt = "#3a3440"; L.Pattern = "plain"; L.Jacket = null; L.Apron = null; L.Vest = null;
                        break;
                    case Role.Teacher:
                        if (L.Female) { L.Shirt = Pick(new[] { "#5a6a8a", "#5a4a6a", "#6a6a6a", "#3a4a5a" }, R(35)); L.Pattern = "plain"; if (L.HairStyle == "long") L.HairStyle = "bun"; L.Apron = null; L.Hat = null; }
                        else { L.Vest = Pick(Vests, R(26)); L.Shirt = "#ece4d2"; L.Pattern = "plain"; L.Jacket = null; L.Hat = R(33) < 0.5f ? "bowler" : null; L.HatColor = "#4a3226"; }
                        break;
                    case Role.Preacher: L.Hat = "parson"; L.HatColor = "#1e1a22"; L.Coat = true; L.Collar = true; L.Shirt = "#24202a"; L.Pattern = "plain"; L.Pant = "#24202a"; L.Jacket = null; L.Vest = null; L.Neck = null; L.Beard = L.Beard == "long" ? "full" : L.Beard; break;
                    case Role.Worker: break;
                    default:
                        if (!L.Female && R(36) < 0.4f) { L.Shirt = Pick(new[] { "#9a8e7a", "#8a8070", "#a89a80", "#7a7a6a" }, R(37)); L.Pattern = "plain"; }   // homespun
                        break;
                }
            L.ShirtShade = Darker(L.Shirt); L.PantShade = Darker(L.Pant);

            // the trade shows: aprons, the baker's cap, the cattleman's hat
            if (!L.Child && !L.Jailed && work.HasValue)
            {
                var wt = work.Value;
                if (wt == BuildingType.Butcher || wt == BuildingType.Bakery || wt == BuildingType.Barber) L.Apron = "#f4f0e6";
                else if (wt == BuildingType.Smithy || wt == BuildingType.Tannery || wt == BuildingType.IronMine) L.Apron = "#6e4428";
                if (wt == BuildingType.Bakery && !L.Female) { L.Hat = "cap"; L.HatColor = "#f4f0e6"; }
                if (wt == BuildingType.Ranch && !L.Female) { L.Hat = "cowboy"; L.HatColor = Pick(new[] { "#9a7a52", "#7a5a3a", "#b8986a" }, R(34)); L.Neck = L.Neck ?? "#b5432f"; }
                if (wt == BuildingType.Hotel || wt == BuildingType.Bank || wt == BuildingType.Store) { L.Neck = null; if (L.Hat == "straw" || L.Hat == "slouch") L.Hat = null; }
            }
            if (L.Jacket != null) L.Suspenders = false;

            var sb = new StringBuilder(96);
            sb.Append(L.Female ? 'f' : 'm').Append(L.Child ? 'c' : 'a').Append(L.Skin).Append(L.Hair).Append(L.HairStyle).Append(L.Bald ? 'B' : '-')
             .Append(L.Freckles ? 'F' : '-').Append(L.Blush ? 'R' : '-').Append(L.Beard ?? "-").Append(L.BeardColor ?? "-")
             .Append(L.Shirt).Append(L.Pattern).Append(L.Pant).Append(L.Shoe).Append(L.Hat ?? "-").Append(L.HatColor ?? "-")
             .Append(L.Vest ?? "-").Append(L.Jacket ?? "-").Append(L.Neck ?? "-").Append(L.Shawl ?? "-").Append(L.Apron ?? "-")
             .Append(L.Coat ? 'c' : '-').Append(L.Badge ? 'b' : '-').Append(L.Suspenders ? 's' : '-').Append(L.Collar ? 'k' : '-')
             .Append(L.Stout ? 'S' : '-').Append(L.Tall ? 'T' : L.Short ? 't' : '-').Append(L.Jailed ? 'J' : '-');
            L.Key = sb.ToString();
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

        /// <summary>A settler asleep (or ill) in bed: just the head on the pillow. Pivot at the top left.</summary>
        public static Sprite Sleeper(Citizen p)
        {
            var L = LookOf(p);
            string top = L.Bald ? L.Skin : L.Hair;
            string key = "sleep|" + L.Skin + "|" + L.Hair + (L.Bald ? "B" : "") + (L.Beard != null ? L.BeardColor : "") + (L.Child ? "c" : "a");
            Sprite s;
            if (cache.TryGetValue(key, out s)) return s;
            var g = new PixelCanvas(7, 6);
            g.R(2, 1, 3, 1, top); g.R(1, 2, 5, 1, top);
            g.R(2, 3, 3, 2, L.Skin); g.D(1, 3, L.Hair); g.D(5, 3, L.Hair);
            g.D(2, 3, L.SkinShade); g.D(4, 3, L.SkinShade);
            if (L.Beard != null) g.R(2, 4, 3, 1, L.BeardColor);
            g.Outline(OL);
            s = g.ToSprite(key, new Vector2(0, 1));
            cache[key] = s;
            return s;
        }

        /// <summary>Canvas 11 wide; adults stand about 17 px tall (taller or shorter by a pixel), children 14 (big-headed).
        /// Feet sit two rows above the bottom.</summary>
        static PixelCanvas Draw(Look L, int dir, int frame)
        {
            if (dir == Right) return Draw(L, Left, frame).Mirrored();
            int torso = L.Child ? 2 : 4, legs = L.Child ? 2 : L.Tall ? 4 : L.Short ? 2 : 3;
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
            int bx = L.Stout ? 1 : 2, bw = L.Stout ? 7 : 5;   // the body, front on
            string arms = L.Jacket ?? L.Shirt, armShade = L.Jacket != null ? Darker(L.Jacket) : L.ShirtShade;

            // ---------- legs / skirt ----------
            if (!side)
            {
                if (skirt)
                {
                    int sway = frame == 1 ? -1 : frame == 2 ? 1 : 0, sx = L.Stout ? 0 : 1, sw = L.Stout ? 9 : 7;
                    RR(sx, ly - 1, sw, 1, L.Shirt);
                    for (int r = 0; r < legs; r++) RR(sx + (r == legs - 1 ? sway : 0), ly + r, sw, 1, r == legs - 1 ? L.ShirtShade : L.Shirt);
                    RR(sx + sw - 2, ly - 1, 1, legs + 1, L.ShirtShade);
                    Pattern(L, RR, sx, ly - 1, sw - 2, legs);
                    RR(2, fy - l1, 2, 1, L.Shoe); RR(5, fy - l2, 2, 1, L.Shoe);
                }
                else
                {
                    RR(2, ly, 2, legs - l1, L.Pant); RR(2, fy - l1, 2, 1, L.Shoe);
                    RR(5, ly, 2, legs - l2, L.PantShade); RR(5, fy - l2, 2, 1, L.Shoe);
                    RR(4, ly, 1, 1, L.Pant);
                    if (L.Stout) { P(1, ly, L.Pant); P(7, ly, L.PantShade); }
                }
            }
            else
            {
                if (skirt)
                {
                    RR(2, ly - 1, 5, 1, L.Shirt);
                    for (int r = 0; r < legs; r++) RR(1 + (frame == 0 ? 1 : 0), ly + r, frame == 0 ? 5 : 6, 1, r == legs - 1 ? L.ShirtShade : L.Shirt);
                    Pattern(L, RR, 2, ly - 1, 4, legs);
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
                RR(bx, ty, bw, torso, L.Shirt); RR(bx - 1, ty, bw + 2, 1, L.Shirt);
                RR(bx + bw - 1, ty + 1, 1, torso - 1, L.ShirtShade);
                Pattern(L, RR, bx, ty + 1, bw - 1, torso - 1);
                if (!skirt) RR(bx, ty + torso - 1, bw, 1, Darker(L.Pant, 0.85f));
                if (L.Jacket != null)
                {
                    if (dir == Down) { RR(bx - 1, ty, 2, torso, L.Jacket); RR(bx + bw - 1, ty, 2, torso, armShade); }
                    else { RR(bx - 1, ty, bw + 2, torso, L.Jacket); RR(bx + bw - 1, ty, 1, torso, armShade); }
                }
                if (L.Vest != null && dir == Down) { RR(bx, ty, 1, torso - 1, L.Vest); RR(bx + bw - 1, ty, 1, torso - 1, Darker(L.Vest)); }
                if (L.Vest != null && dir == Up) RR(bx, ty, bw, torso - 1, L.Vest);
                if (L.Shawl != null) { RR(bx - 1, ty, bw + 2, 1, L.Shawl); RR(bx, ty + 1, bw, 1, L.Shawl); if (dir == Down) P(4, ty + 2, L.Shawl); else RR(bx + 1, ty + 2, bw - 2, 1, L.Shawl); }
                if (L.Coat && dir == Down) { RR(4, ty, 1, 2, "#ece4d2"); if (!L.Collar) P(4, ty + 1, "#b5432f"); }
                if (L.Suspenders) { P(3, ty, "#5a3a2a"); P(5, ty, "#5a3a2a"); RR(3, ty + 1, 1, torso - 2, "#5a3a2a"); RR(5, ty + 1, 1, torso - 2, "#5a3a2a"); if (L.Child && dir == Down) RR(3, ty + 1, 3, torso - 1, L.Pant); }
                if (L.Neck != null) { RR(3, ty, 3, 1, L.Neck); if (dir == Down) P(4, ty + 1, L.Neck); }
                if (L.Badge && dir == Down) P(5, ty + 1, "#f2d04a");
                if (L.Collar && dir == Down) { P(4, ty, "#f8f8f4"); }
                if (L.Apron != null && dir == Down) { RR(3, ty + 1, 3, torso - 1, L.Apron); RR(3, ly, 3, Mathf.Min(legs, skirt ? 2 : 1), L.Apron); }
                int arm = L.Child ? 1 : 2, la = frame == 1 ? -1 : frame == 2 ? 1 : 0;
                int axl = bx - 1, axr = bx + bw;
                RR(axl, ty + 1, 1, arm + la, arms); P(axl, ty + 1 + arm + la, L.Skin);
                RR(axr, ty + 1, 1, arm - la, armShade); P(axr, ty + 1 + arm - la, L.SkinShade);
            }
            else
            {
                int sx = L.Stout ? 1 : 2, sw = L.Stout ? 5 : 4;
                RR(sx, ty, sw, torso, L.Shirt); RR(sx + sw - 1, ty, 1, torso, L.ShirtShade);
                Pattern(L, RR, sx, ty + 1, sw - 1, torso - 1);
                if (!skirt) RR(sx, ty + torso - 1, sw, 1, Darker(L.Pant, 0.85f));
                if (L.Jacket != null) RR(sx + 1, ty, sw - 1, torso, L.Jacket);
                if (L.Vest != null) RR(sx + 2, ty, sw - 2, torso - 1, L.Vest);
                if (L.Shawl != null) RR(sx, ty, sw, 2, L.Shawl);
                if (L.Apron != null) { RR(sx, ty + 1, 1, torso - 1, L.Apron); }
                if (L.Neck != null) P(sx, ty, L.Neck);
                if (L.Badge) P(2, ty + 1, "#f2d04a");
                if (L.Coat || L.Collar) P(2, ty, L.Collar ? "#f8f8f4" : "#ece4d2");
                int arm = L.Child ? 1 : 2, swg = frame == 1 ? -1 : frame == 2 ? 1 : 0;
                RR(3 + swg, ty + 1, 2, arm, armShade); RR(3 + swg, ty + 1 + arm, 2, 1, L.Skin);
            }

            // ---------- head ----------
            string top = L.Bald ? L.Skin : L.Hair, topShade = L.Bald ? L.SkinShade : L.HairShade;
            if (!side)
            {
                RR(2, hy, 5, 1, L.Skin); RR(1, hy + 1, 7, 5, L.Skin); RR(2, hy + 6, 5, 1, L.Skin);
                RR(6, hy + 2, 2, 4, L.SkinShade);
                if (dir == Down)
                {
                    // hair
                    RR(2, hy, 5, 1, top); RR(1, hy + 1, 7, L.Bald ? 1 : 2, top);
                    if (L.Bald) { P(3, hy, Lighter(L.Skin)); P(1, hy + 2, L.Hair); P(7, hy + 2, L.HairShade); }
                    else RR(2, hy + 1, 3, 1, Lighter(L.Hair));
                    P(1, hy + 3, L.Hair); P(7, hy + 3, L.HairShade);
                    switch (L.HairStyle)
                    {
                        case "parted": P(5, hy + 1, L.HairShade); RR(2, hy + 1, 3, 1, Lighter(L.Hair)); break;
                        case "shaggy": if (!L.Bald) { P(1, hy + 4, L.Hair); P(7, hy + 4, L.HairShade); P(4, hy + 2, L.HairShade); } break;
                        case "curly": if (!L.Bald) { RR(1, hy, 7, 1, L.Hair); P(1, hy - 1 + 1, L.HairShade); P(3, hy + 2, Lighter(L.Hair)); P(6, hy + 1, L.HairShade); } break;
                        case "long": RR(0, hy + 2, 1, 6, L.Hair); RR(8, hy + 2, 1, 6, L.HairShade); RR(1, hy + 3, 1, 4, L.Hair); RR(7, hy + 3, 1, 4, L.HairShade); break;
                        case "bun": RR(3, hy - 1, 3, 1, L.Hair); break;
                        case "updo": RR(2, hy - 1, 5, 1, L.Hair); RR(3, hy - 2, 3, 1, L.HairShade); break;
                        case "braids":
                            for (int y = hy + 3; y < hy + 8; y++) { P(0, y, (y % 2 == 0) ? L.Hair : L.HairShade); P(8, y, (y % 2 == 1) ? L.Hair : L.HairShade); }
                            P(0, hy + 8, "#b5432f"); P(8, hy + 8, "#b5432f");
                            break;
                    }
                    // face
                    RR(3, hy + 3, 1, 2, OL); RR(5, hy + 3, 1, 2, OL);
                    if (L.Freckles) { P(2, hy + 4, L.SkinShade); P(6, hy + 4, L.SkinShade); }
                    if (L.Blush) { P(2, hy + 5, "#e89a8a"); P(6, hy + 5, "#d88a7a"); }
                    if (L.Beard != null)
                    {
                        string b = L.BeardColor, bs = Darker(b, 0.8f);
                        switch (L.Beard)
                        {
                            // beards keep to the jaw and chin so the face stays clear
                            case "full": RR(3, hy + 5, 3, 1, b); RR(2, hy + 6, 5, 1, b); RR(3, ty, 3, 1, bs); break;
                            case "long": RR(3, hy + 5, 3, 1, b); RR(2, hy + 6, 5, 1, b); RR(3, ty, 3, 2, b); P(4, ty + 2, bs); break;
                            case "mustache": RR(3, hy + 5, 3, 1, b); P(2, hy + 5, bs); P(6, hy + 5, bs); break;
                            case "chops": RR(1, hy + 3, 1, 3, b); RR(7, hy + 3, 1, 3, bs); P(2, hy + 5, b); P(6, hy + 5, bs); break;
                            case "goatee": RR(3, hy + 6, 3, 1, b); P(4, hy + 5, b); break;
                        }
                    }
                }
                else
                {
                    RR(2, hy, 5, 1, top); RR(1, hy + 1, 7, 5, L.Hair); RR(2, hy + 6, 5, 1, L.HairShade); RR(6, hy + 2, 2, 4, L.HairShade);
                    if (L.Bald) { RR(2, hy, 5, 1, L.Skin); RR(1, hy + 1, 7, 2, L.Skin); RR(6, hy + 1, 2, 2, L.SkinShade); }
                    bool longish = L.HairStyle == "long";
                    if (!longish) { P(1, hy + 3, L.Skin); P(7, hy + 3, L.SkinShade); }
                    else RR(1, hy + 6, 7, 3, L.Hair);
                    if (L.HairStyle == "bun") RR(3, hy - 1, 3, 2, L.HairShade);
                    if (L.HairStyle == "updo") { RR(2, hy - 1, 5, 2, L.HairShade); }
                    if (L.HairStyle == "braids") { RR(2, hy + 6, 1, 3, L.Hair); RR(6, hy + 6, 1, 3, L.HairShade); P(2, hy + 9, "#b5432f"); P(6, hy + 9, "#b5432f"); }
                    if (L.Beard != null) { P(1, hy + 5, L.BeardColor); P(7, hy + 5, Darker(L.BeardColor, 0.8f)); }
                }
            }
            else
            {
                RR(2, hy, 4, 1, L.Skin); RR(1, hy + 1, 6, 5, L.Skin); RR(2, hy + 6, 4, 1, L.Skin);
                P(0, hy + 3, L.Skin);
                RR(2, hy, 4, 1, top); RR(1, hy + 1, 6, L.Bald ? 1 : 2, top); RR(4, hy + 3, 3, 3, L.Hair); RR(6, hy + 3, 1, 3, L.HairShade);
                if (L.Bald) { RR(4, hy + 2, 3, 1, L.Hair); P(3, hy, Lighter(L.Skin)); }
                switch (L.HairStyle)
                {
                    case "long": RR(4, hy + 3, 3, 6, L.Hair); break;
                    case "bun": RR(5, hy - 1, 2, 2, L.HairShade); break;
                    case "updo": RR(3, hy - 1, 4, 1, L.Hair); RR(4, hy - 2, 2, 1, L.HairShade); break;
                    case "braids": for (int y = hy + 6; y < hy + 9; y++) P(6, y, y % 2 == 0 ? L.Hair : L.HairShade); P(6, hy + 9, "#b5432f"); break;
                    case "shaggy": if (!L.Bald) P(3, hy + 3, L.Hair); break;
                    case "curly": if (!L.Bald) { P(1, hy, L.Hair); P(5, hy + 2, Lighter(L.Hair)); } break;
                }
                P(4, hy + 3, L.SkinShade);
                RR(2, hy + 3, 1, 2, OL);
                if (L.Freckles) P(2, hy + 5, L.SkinShade);
                if (L.Blush) P(1, hy + 5, "#e89a8a");
                if (L.Beard != null)
                {
                    string b = L.BeardColor;
                    switch (L.Beard)
                    {
                        case "full": P(1, hy + 5, b); RR(3, hy + 5, 2, 1, b); RR(2, hy + 6, 3, 1, b); P(2, ty, b); break;
                        case "long": P(1, hy + 5, b); RR(3, hy + 5, 2, 1, b); RR(2, hy + 6, 3, 1, b); RR(2, ty, 2, 2, b); break;
                        case "mustache": RR(1, hy + 5, 2, 1, b); break;
                        case "chops": RR(3, hy + 3, 1, 3, b); P(2, hy + 5, b); break;
                        case "goatee": RR(1, hy + 6, 2, 1, b); break;
                    }
                }
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
                    case "parson": RR(2, by - 2, 5, 2, hc); RR(-1, by, 11, 1, hc); P(4, by - 2, hs); break;   // a low crown and a wide flat brim
                    case "slouch": RR(2, by - 2, 5, 2, hc); P(3, by - 2, Lighter(hc)); RR(0, by, 9, 1, hc); P(-1, by + 1, hs); P(9, by + 1, hs); RR(2, by - 1, 5, 1, hs); break;   // a soft brim that droops
                    case "cap":
                        RR(1, by - 1, 7, 2, hc); RR(2, by - 2, 5, 1, hc); RR(6, by - 1, 2, 2, hs);
                        if (dir == Down) RR(1, by + 1, 7, 1, hs); else if (dir == Left) RR(-1, by + 1, 3, 1, hs);
                        break;
                    case "bonnet":   // a sunbonnet: deep brim round the face, a curtain over the neck
                        if (dir == Down) { RR(1, by - 2, 7, 2, hc); RR(0, by, 9, 1, hs); RR(0, by + 1, 1, 3, hc); RR(8, by + 1, 1, 3, hs); P(2, hy + 6, hs); P(6, hy + 6, hs); }
                        else if (dir == Up) { RR(1, by - 2, 7, 4, hc); RR(1, by + 2, 7, 3, hc); RR(6, by - 1, 2, 6, hs); RR(1, by + 5, 7, 1, hs); }
                        else { RR(1, by - 2, 6, 3, hc); RR(-1, by - 1, 2, 3, hs); RR(4, by + 1, 3, 4, hc); RR(6, by + 1, 1, 4, hs); }
                        break;
                    case "kerchief":   // a headscarf knotted at the back
                        if (dir == Down) { RR(2, by - 2, 5, 1, hc); RR(1, by - 1, 7, 2, hc); P(2, by - 1, Lighter(hc)); }
                        else if (dir == Up) { RR(1, by - 2, 7, 3, hc); P(4, by + 1, hs); P(3, by + 2, hs); P(5, by + 2, hs); }
                        else { RR(1, by - 2, 6, 3, hc); P(6, by + 1, hs); P(7, by + 1, hs); }
                        break;
                }
            }

            g.Outline(OL);
            g.RBlend(ox + 1, H - 1, 7, 1, new Color32(0, 0, 0, 70));
            return g;
        }

        /// <summary>Gingham checks, pinstripes or a calico print over a patch of clothing.</summary>
        static void Pattern(Look L, System.Action<int, int, int, int, string> RR, int x, int y, int w, int h)
        {
            if (L.Pattern == null || L.Pattern == "plain" || w <= 0 || h <= 0) return;
            string dark = Darker(L.Shirt, 0.72f), light = Lighter(L.Shirt);
            for (int yy = y; yy < y + h; yy++)
                for (int xx = x; xx < x + w; xx++)
                {
                    if (L.Pattern == "check" && (xx % 2 == 0) && ((yy - y) % 2 == 0)) RR(xx, yy, 1, 1, dark);
                    else if (L.Pattern == "stripe" && xx % 2 == 0) RR(xx, yy, 1, 1, dark);
                    else if (L.Pattern == "print" && (xx + yy * 2) % 4 == 0) RR(xx, yy, 1, 1, light);
                }
        }

        static string Lighter(string hex)
        {
            var c = PixelCanvas.C(hex);
            return "#" + Mathf.Min(255, c.r + 30).ToString("x2") + Mathf.Min(255, c.g + 26).ToString("x2") + Mathf.Min(255, c.b + 20).ToString("x2");
        }
    }
}
