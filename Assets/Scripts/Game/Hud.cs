using System.Collections.Generic;
using System.Linq;
using FrontierLedger.Game.Art;
using FrontierLedger.Game.UI;
using FrontierLedger.Simulation;
using UnityEngine;

namespace FrontierLedger.Game
{
    /// <summary>
    /// The interface: a date box with speed controls, the stores bar, the treasury, a hotbar of buildings,
    /// a minimap, a message feed, a ledger book with tabs, and an inspector. Wooden frames, parchment and
    /// a pixel font, all drawn through the small Ui kit at a whole-number scale.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        enum Tab { None, Ledger, Citizens, Trade, Election, Gazette, Freight }
        readonly float[] cart = new float[Balance.GoodCount];
        int cartLaborers, cartFamilies;

        GameController gc;
        Town T { get { return gc.Town; } }
        int S = 2;
        float W, H;

        List<Rect> blockers = new List<Rect>(), building = new List<Rect>();
        Tab tab = Tab.None;
        Category cat = Category.Housing;
        string toast; float toastUntil;
        float confirmNewUntil; int confirmTearDown = -1;
        Vector2 panelScroll, inspScroll;
        float panelContentH, inspContentH;
        Texture2D minimap; int miniVersion = -1, miniSeason = -1; float miniTimer; Color32[] miniBase;
        string hoverTip;

        readonly List<System.Action> deferred = new List<System.Action>();
        void Later(System.Action a) { deferred.Add(a); }

        void Awake() { gc = GetComponent<GameController>(); }

        void Update()
        {
            if (deferred.Count == 0) return;
            var run = deferred.ToArray(); deferred.Clear();
            foreach (var a in run) a();
        }

        public void Toast(string msg) { toast = msg; toastUntil = Time.unscaledTime + 2.6f; }
        public void ResetConfirm() { confirmTearDown = -1; }
        public void CloseAll() { tab = Tab.None; }

        /// <summary>True when the mouse (Unity screen coords, y up) is over the interface.</summary>
        public bool Blocks(Vector2 mouse)
        {
            var p = new Vector2(mouse.x / S, (Screen.height - mouse.y) / S);
            foreach (var r in blockers) if (r.Contains(p)) return true;
            return false;
        }
        Rect Block(Rect r) { building.Add(r); return r; }

        // ================================================================== main
        void OnGUI()
        {
            if (gc == null || gc.Town == null) return;
            S = Mathf.Max(1, Mathf.RoundToInt(Screen.height / 560f));
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(S, S, 1));
            W = Screen.width / (float)S; H = Screen.height / (float)S;
            if (Event.current.type == EventType.Layout) building.Clear();
            hoverTip = null;

            DateBox();
            StoresBar();
            Alerts();
            MoneyAndTabs();
            Hotbar();
            Minimap();
            Messages();
            if (tab != Tab.None) BookPanel();
            if (gc.Selected.Kind != SelectionKind.None) Inspector();
            MapTooltip();
            if (hoverTip != null) UiTip(hoverTip);
            if (toast != null && Time.unscaledTime < toastUntil)
            {
                float w = PixelFont.Width(toast) + 16;
                var r = new Rect(Mathf.Round((W - w) / 2), 44, w, 17);
                Ui.Tip(r); Ui.Text(r.x + 8, r.y + 5, toast, Ui.Bad);
            }
            if (Event.current.type == EventType.Repaint) { blockers = new List<Rect>(building); }
        }

        void UiTip(string s)
        {
            var m = Event.current.mousePosition;
            float w = Mathf.Min(180, PixelFont.Width(s) + 12);
            float h = Ui.WrappedHeight(s, w - 12) + 8;
            var r = new Rect(Mathf.Min(m.x + 10, W - w - 4), Mathf.Min(m.y + 12, H - h - 4), w, h);
            Ui.Tip(r); Ui.Wrapped(new Rect(r.x + 6, r.y + 5, w - 12, h), s, Ui.Ink);
        }

        // ---------------------------------------------------------------- date and speed
        void DateBox()
        {
            var r = Block(new Rect(4, 4, 150, 52));
            Ui.Panel(r);
            int si = T.Season;
            Ui.IconAt(r.x + 9, r.y + 8, si == 3 ? Icon.Snow : si == 2 ? Icon.Leaf : Icon.Sun);
            Ui.Text(r.x + 22, r.y + 9, T.DateLabel, Ui.Title);
            // day bar: dawn to night
            var bar = new Rect(r.x + 9, r.y + 21, 132, 5);
            Ui.Fill(bar, Ui.Hex("#3b2618"));
            int segs = 24;
            for (int i = 0; i < segs; i++)
            {
                float t = (i + 0.5f) / segs;
                float n = t < 0.15f ? 1 - t / 0.15f : t > 0.82f ? Mathf.Min(1, (t - 0.82f) / 0.12f) : 0;
                Ui.Fill(new Rect(bar.x + 1 + i * (bar.width - 2) / segs, bar.y + 1, (bar.width - 2) / segs + 0.5f, 3), Color.Lerp(Ui.Hex("#9ad0f0"), Ui.Hex("#2a3a6a"), n));
            }
            Ui.Fill(new Rect(bar.x + 1 + T.TimeOfDay * (bar.width - 3), bar.y - 1, 2, 7), Ui.Gold);
            var speeds = GameController.Speeds;
            string[] labels = { "", "1x", "3x", "10x", "30x" };
            for (int i = 0; i < speeds.Length; i++)
            {
                var b = new Rect(r.x + 8 + i * 27, r.y + 31, 26, 15);
                bool on = gc.Speed == speeds[i];
                if (i == 0 ? Ui.Button(b, "", on, true, Icon.Pause) : Ui.Button(b, labels[i], on)) gc.SetSpeed(speeds[i]);
                if (Ui.Hover(b)) hoverTip = i == 0 ? "Pause (Space)" : "Speed " + labels[i] + " (" + i + ")";
            }
        }

        // ---------------------------------------------------------------- stores bar
        void StoresBar()
        {
            float food = T.TotalFood(), wood = T.TotalFirewood();
            int people = T.People.Count, kids = T.People.Count(p => p.IsChild), homeless = T.Homeless;
            var items = new List<KeyValuePair<Icon, string>>
            {
                new KeyValuePair<Icon, string>(Icon.Food, Mathf.FloorToInt(food).ToString()),
                new KeyValuePair<Icon, string>(Icon.Firewood, Mathf.FloorToInt(wood).ToString()),
                new KeyValuePair<Icon, string>(Icon.Logs, Mathf.FloorToInt(T.TownStock(Good.Logs)).ToString()),
                new KeyValuePair<Icon, string>(Icon.Stone, Mathf.FloorToInt(T.TownStock(Good.Stone)).ToString()),
                new KeyValuePair<Icon, string>(Icon.Tools, Mathf.FloorToInt(T.TownStock(Good.Tools)).ToString()),
                new KeyValuePair<Icon, string>(Icon.People, (people - kids) + "+" + kids),
                new KeyValuePair<Icon, string>(Icon.Heart, Mathf.RoundToInt(T.AverageHappiness) + "%"),
                new KeyValuePair<Icon, string>(Icon.Health, Mathf.RoundToInt(T.AverageHealth) + "%"),
            };
            if (homeless > 0) items.Add(new KeyValuePair<Icon, string>(Icon.Warning, homeless + " no roof"));
            float w = 12; foreach (var kv in items) w += 14 + PixelFont.Width(kv.Value) + 10;
            var r = Block(new Rect(Mathf.Round((W - w) / 2), 4, w, 24));
            Ui.Panel(r);
            float x = r.x + 8;
            string[] tips =
            {
                "Food in town: shops, barns, pantries and stores. Each settler eats about 1 a day.",
                "Firewood in town. Homes burn more as it gets colder; winter takes the most.",
                "Logs in town stores, used for building.",
                "Stone in town stores, used for building.",
                "Tools in town stores. Workers without tools get 40% less done.",
                "Adults + children",
                "Average happiness",
                "Average health. Hunger and cold wear it down.",
                "Settlers without a roof. A boarding house is quickest; tents can't keep anyone warm in autumn or winter.",
            };
            for (int i = 0; i < items.Count; i++)
            {
                var kv = items[i];
                var cell = new Rect(x - 2, r.y + 4, 14 + PixelFont.Width(kv.Value) + 6, 16);
                Ui.IconAt(x, r.y + 7, kv.Key);
                Color c = Ui.Ink;
                if (i == 0 && food < people * 6) c = Ui.Bad;
                if (i == 1 && T.Season >= 2 && wood < T.Buildings.Count(h => h.Built && (h.Type == BuildingType.House || h.Type == BuildingType.BoardingHouse) && h.Residents.Count > 0) * 6) c = Ui.Bad;
                if (i == 6) c = Ui.Mood(T.AverageHappiness);
                if (i == 7) c = Ui.Mood(T.AverageHealth);
                if (i == 8) c = Ui.Bad;
                Ui.Text(x + 12, r.y + 9, kv.Value, c);
                if (Ui.Hover(cell)) hoverTip = tips[Mathf.Min(i, tips.Length - 1)];
                x += 14 + PixelFont.Width(kv.Value) + 10;
            }
        }

        // ---------------------------------------------------------------- warnings under the stores bar
        /// <summary>Plain warnings before disaster: people in tents as the cold comes, food or firewood running out,
        /// food businesses with nobody working them.</summary>
        void Alerts()
        {
            var lines = new List<KeyValuePair<string, bool>>();   // text, severe
            int si = T.Season, homeless = T.Homeless;
            if (homeless > 0 && si >= 1)
                lines.Add(new KeyValuePair<string, bool>(si == 1 ? homeless + " in tents. Get them under a roof before autumn." : si == 2 ? homeless + " in tents. They will freeze this winter without a roof." : homeless + " in tents are freezing. Keep firewood in store for their fire.", si >= 2));
            float eat = T.People.Sum(p => p.IsChild ? Balance.ChildAppetite : 1f);
            float grow = T.Market.Garden;
            float daily = Mathf.Max(0.5f, eat - grow);
            float foodDays = T.TotalFood() / daily;
            if (T.People.Count > 0 && foodDays < 10) lines.Add(new KeyValuePair<string, bool>("Food for about " + Mathf.FloorToInt(foodDays) + " days.", foodDays < 5));
            int homes = T.Buildings.Count(h => h.Built && Town.IsHome(h.Type) && h.Residents.Count > 0);
            float burn = homes * Balance.FirewoodBySeason[si] + (si >= 2 ? homeless * 0.3f : 0f);
            if (burn > 0.5f)
            {
                float woodDays = T.TotalFirewood() / burn;
                if (woodDays < 10) lines.Add(new KeyValuePair<string, bool>("Firewood for about " + Mathf.FloorToInt(woodDays) + " days.", woodDays < 5 || si == 3));
            }
            int idleFood = T.Businesses.Count(b => Town.FoodJob(b.Type) && b.Type != BuildingType.Butcher && T.WorkersAt(b) == 0);
            if (idleFood > 0) lines.Add(new KeyValuePair<string, bool>(idleFood == 1 ? "A food business has nobody working it." : idleFood + " food businesses have nobody working them.", false));
            int noTools = T.Businesses.Count(b => Town.FoodJob(b.Type) && b.Type != BuildingType.Butcher && T.WorkersAt(b) > 0 && b.Stock[(int)Good.Tools] < 0.01f);
            if (noTools > 0 && (si <= 2)) lines.Add(new KeyValuePair<string, bool>((noTools == 1 ? "A food business has" : noTools + " food businesses have") + " no tools: 40% less food. Build a blacksmith or order tools.", false));
            if (lines.Count == 0) return;
            float w = 0; foreach (var l in lines) w = Mathf.Max(w, PixelFont.Width(l.Key));
            w += 26;
            var r = Block(new Rect(Mathf.Round((W - w) / 2), 31, w, lines.Count * 11 + 8));
            Ui.Fill(r, Ui.Hex("#3b2618"));
            var bg = Ui.Parch; bg.a = 0.92f; Ui.Fill(new Rect(r.x + 1, r.y + 1, r.width - 2, r.height - 2), bg);
            float y = r.y + 4;
            foreach (var l in lines)
            {
                Ui.IconAt(r.x + 5, y - 1, Icon.Warning);
                Ui.Text(r.x + 17, y, l.Key, l.Value ? Ui.Bad : Ui.Warn);
                y += 11;
            }
        }

        // ---------------------------------------------------------------- treasury and book tabs
        void MoneyAndTabs()
        {
            var r = Block(new Rect(W - 160, 4, 156, 24));
            Ui.Panel(r);
            Ui.IconAt(r.x + 8, r.y + 7, Icon.Coin);
            Ui.Text(r.x + 21, r.y + 9, "Treasury", Ui.InkSoft);
            Ui.Text(r.xMax - 8, r.y + 9, Town.Money(T.Treasury), T.Treasury < 0 ? Ui.Bad : Ui.Title, Ui.Align.Right);
            Icon[] icons = { Icon.Book, Icon.People, Icon.Scales, Icon.Ballot, Icon.News, Icon.Wagon };
            string[] names = { "Ledger: the town's money, who has what, taxes and wages", "Citizens", "Trade: food, firewood and businesses", "Election and mayor", "Gazette: what's been happening",
                               "Freight: order goods and settlers from back east" };
            for (int i = 0; i < 6; i++)
            {
                var b = Block(new Rect(W - 160 + i * 26, 30, 24, 18));
                var t = (Tab)(i + 1);
                if (Ui.Button(b, "", tab == t, true, icons[i])) { Later(() => { tab = tab == t ? Tab.None : t; panelScroll = Vector2.zero; }); }
                if (Ui.Hover(b)) hoverTip = names[i];
            }
        }

        // ---------------------------------------------------------------- hotbar
        void Hotbar()
        {
            var cats = Defs.PaletteOrder;
            float slotW = 30;
            var defs = Defs.InCategory(cat).ToList();
            int nSlots = 2 + 8;
            float barW = nSlots * (slotW + 2) + 14;
            var bar = Block(new Rect(Mathf.Round((W - barW) / 2), H - 44, barW, 40));
            // category tabs above
            float tabW = 48, tabsW = cats.Length * (tabW + 2);
            var tabsR = Block(new Rect(Mathf.Round((W - tabsW) / 2), bar.y - 16, tabsW, 16));
            for (int i = 0; i < cats.Length; i++)
            {
                var c = cats[i];
                var tr = new Rect(tabsR.x + i * (tabW + 2), tabsR.y, tabW, 16);
                if (Ui.Button(tr, Defs.CategoryNames[(int)c], cat == c)) { Later(() => cat = c); }
            }
            Ui.Panel(bar);
            float x = bar.x + 7, y = bar.y + 5;
            // tools
            if (Slot(new Rect(x, y, slotW, slotW), null, Icon.Pointer, gc.Tool == ToolKind.Inspect, "Inspect (Q)\nClick a building or settler to see how they're doing.")) gc.SetTool(ToolKind.Inspect);
            x += slotW + 2;
            if (Slot(new Rect(x, y, slotW, slotW), null, Icon.Axe, gc.Tool == ToolKind.Clear, "Clear land (X)\nFell trees (+2 logs), break boulders (+3 stone), pull stumps. Small fee.")) gc.SetTool(ToolKind.Clear);
            x += slotW + 6;
            foreach (var d in defs.Take(8))
            {
                var r = new Rect(x, y, slotW, slotW);
                bool on = gc.Tool == ToolKind.Build && gc.BuildType == d.Type;
                bool can = !(d.Unique && T.Buildings.Any(b => b.Type == d.Type));
                Texture tex = d.Type == BuildingType.Road ? null : BuildingArt.Get(d.Type, 0, T.Season).TopTexture;
                if (Slot(r, tex, d.Type == BuildingType.Road ? Icon.Hammer : (Icon?)null, on, null, can) && can) gc.SetTool(ToolKind.Build, d.Type);
                if (Ui.Hover(r)) BuildTip(d, new Vector2(r.x, bar.y - 20));
                x += slotW + 2;
            }
        }

        bool Slot(Rect r, Texture tex, Icon? icon, bool on, string tip, bool enabled = true)
        {
            Ui.SlotBox(r, on);
            if (tex != null)
            {
                float aspect = tex.width / (float)tex.height;
                float h = r.height - 6, w = h * aspect;
                if (w > r.width - 6) { w = r.width - 6; h = w / aspect; }
                var c = GUI.color; if (!enabled) GUI.color = new Color(1, 1, 1, 0.4f);
                Ui.Texture(new Rect(r.x + (r.width - w) / 2, r.y + (r.height - h) / 2, w, h), tex);
                GUI.color = c;
            }
            else if (icon.HasValue) Ui.IconAt(Mathf.Round(r.x + (r.width - 9) / 2), Mathf.Round(r.y + (r.height - 9) / 2), icon.Value, enabled ? 1 : 0.4f);
            if (tip != null && Ui.Hover(r)) hoverTip = tip;
            return Ui.Clickable(r);
        }

        void BuildTip(BuildingDef d, Vector2 anchor)
        {
            if (Event.current.type != EventType.Repaint) return;
            float w = 190;
            string blurb = d.Blurb ?? "";
            float h = 30 + Ui.WrappedHeight(blurb, w - 14) + (d.IsBusiness ? 11 : 0);
            var r = new Rect(Mathf.Clamp(anchor.x - 60, 4, W - w - 4), anchor.y - h, w, h);
            Ui.Tip(r);
            Ui.Text(r.x + 7, r.y + 6, d.Name, Ui.Title);
            float x = r.x + 7, y = r.y + 17;
            if (d.Type == BuildingType.Road) { Ui.Text(x, y, "$" + d.Money + " a tile, built at once", Ui.InkSoft); }
            else
            {
                if (d.Logs > 0) { Ui.IconAt(x, y - 1, Icon.Logs); x += 11; x += Ui.Text(x, y, d.Logs.ToString(), T.TownStock(Good.Logs) >= d.Logs ? Ui.Ink : Ui.Bad) + 6; }
                if (d.Stone > 0) { Ui.IconAt(x, y - 1, Icon.Stone); x += 11; x += Ui.Text(x, y, d.Stone.ToString(), T.TownStock(Good.Stone) >= d.Stone ? Ui.Ink : Ui.Bad) + 6; }
                Ui.IconAt(x, y - 1, Icon.Hammer); x += 11; Ui.Text(x, y, d.Work + " work-days", Ui.InkSoft);
            }
            float by = r.y + 28;
            if (d.IsBusiness) { Ui.Text(r.x + 7, by, Town.PaysByShare(d.Type) ? "Up to " + d.MaxEmployees + " women · paid " + Mathf.RoundToInt(Balance.VisitShare * 100) + "% of each visit" : "Up to " + d.MaxEmployees + " workers · wage " + Town.Money(d.BaseWage), Ui.InkSoft); by += 11; }
            Ui.Wrapped(new Rect(r.x + 7, by, w - 14, 200), blurb, Ui.Ink);
        }

        // ---------------------------------------------------------------- minimap
        void RebuildMinimap()
        {
            int w = Town.Cols, h = Town.Rows, si = T.Season;   // one pixel a tile
            if (minimap == null) minimap = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
            if (miniBase == null) miniBase = new Color32[w * h];
            var tileCol = new Color32[7];
            tileCol[0] = PixelCanvas.C(GroundPainter.GrassBase(si));
            tileCol[1] = PixelCanvas.C(si == 3 ? "#bcd8e6" : "#3b7cc0");
            tileCol[2] = PixelCanvas.C(si == 3 ? "#6f8a78" : si == 2 ? "#c47a24" : "#3d7a2a");
            tileCol[3] = PixelCanvas.C("#a39c90");
            tileCol[4] = PixelCanvas.C(si == 3 ? "#d8e2e8" : GroundPainter.HillColors[si][0]);
            tileCol[5] = tileCol[0]; tileCol[6] = tileCol[0];
            for (int ty = 0; ty < Town.Rows; ty++)
                for (int tx = 0; tx < Town.Cols; tx++)
                {
                    var b = T.OccupantAt(tx, ty);
                    byte t = T.TerrainAt(tx, ty);
                    var c = b != null ? PixelCanvas.C(b.Type == BuildingType.Road ? "#c49c69" : b.Def.Color) : tileCol[Mathf.Min(6, (int)t)];
                    miniBase[(h - 1 - ty) * w + tx] = c;
                }
            miniVersion = T.MapVersion; miniSeason = si;
        }

        void Minimap()
        {
            var r = Block(new Rect(4, H - 4 - (MiniH + 16), MiniW + 16, MiniH + 16));
            Ui.Panel(r);
            if (minimap == null || miniVersion != T.MapVersion || miniSeason != T.Season) RebuildMinimap();
            miniTimer -= Time.unscaledDeltaTime;
            if (miniTimer <= 0 && Event.current.type == EventType.Repaint)
            {
                miniTimer = 0.25f;
                var px = (Color32[])miniBase.Clone();
                int w = minimap.width, h = minimap.height;
                foreach (var p in T.People)
                {
                    Vector2 pos;
                    if (!gc.View.TryGetPersonPos(p.Id, out pos)) continue;
                    int x = Mathf.Clamp((int)(pos.x / 16), 0, w - 1), y = Mathf.Clamp((int)(pos.y / 16), 0, h - 1);
                    px[(h - 1 - y) * w + x] = new Color32(255, 255, 255, 255);
                }
                minimap.SetPixels32(px); minimap.Apply(false);
            }
            var inner = new Rect(r.x + 8, r.y + 8, MiniW, MiniH);
            if (Ui.Repaint) GUI.DrawTexture(inner, minimap, ScaleMode.StretchToFill);
            float sx = inner.width / (Town.Cols * 16f), sy = inner.height / (Town.Rows * 16f);
            var o = gc.Rig.ViewOrigin; var v = gc.Rig.ViewSize;
            var vr = Rect.MinMaxRect(Mathf.Max(inner.x, inner.x + o.x * sx), Mathf.Max(inner.y, inner.y + o.y * sy),
                                     Mathf.Min(inner.xMax, inner.x + (o.x + v.x) * sx), Mathf.Min(inner.yMax, inner.y + (o.y + v.y) * sy));
            Ui.Fill(new Rect(vr.x, vr.y, vr.width, 1), Ui.Gold); Ui.Fill(new Rect(vr.x, vr.yMax - 1, vr.width, 1), Ui.Gold);
            Ui.Fill(new Rect(vr.x, vr.y, 1, vr.height), Ui.Gold); Ui.Fill(new Rect(vr.xMax - 1, vr.y, 1, vr.height), Ui.Gold);
            var e = Event.current;
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0 && inner.Contains(e.mousePosition))
            {
                gc.CenterOn(new Vector2((e.mousePosition.x - inner.x) / sx, (e.mousePosition.y - inner.y) / sy));
                e.Use();
            }
        }

        /// <summary>Minimap size in UI pixels: about a pixel per tile, in the map's proportions.</summary>
        const float MiniW = 160f, MiniH = 160f * Town.Rows / Town.Cols;

        // ---------------------------------------------------------------- message feed
        void Messages()
        {
            float y = H - 4 - (MiniH + 16) - 8;
            int shown = 0;
            foreach (var l in T.Log)
            {
                if (T.Day - l.Day > 2 || shown >= 4) break;
                if (l.Kind == LogKind.Info) continue;
                Color c = l.Kind == LogKind.Death || l.Kind == LogKind.Bad ? Ui.Bad : l.Kind == LogKind.Birth || l.Kind == LogKind.Good ? Ui.Good : l.Kind == LogKind.Vote ? Ui.Hex("#8a5a10") : Ui.Ink;
                var lines = PixelFont.Wrap(l.Message, 230);
                float h = lines.Count * PixelFont.LineH + 6;
                y -= h + 2;
                var r = new Rect(4, y, 240, h);
                var bg = Ui.Parch; bg.a = 0.88f;
                Ui.Fill(r, Ui.Hex("#3b2618")); Ui.Fill(new Rect(r.x + 1, r.y + 1, r.width - 2, r.height - 2), bg);
                float ly = r.y + 4;
                foreach (var line in lines) { Ui.Text(r.x + 5, ly, line, c); ly += PixelFont.LineH; }
                shown++;
            }
        }

        // ================================================================== the ledger book
        float bx, by, bw;   // content cursor
        void Row(string k, string v, Color? vc = null)
        {
            Ui.Text(bx, by, k, Ui.InkSoft);
            if (PixelFont.Width(k) + PixelFont.Width(v) + 6 > bw) by += 10;   // too long to share the line: the value goes underneath
            Ui.Text(bx + bw, by, v, vc ?? Ui.Ink, Ui.Align.Right);
            by += 11;
        }
        void Heading(string s) { by += 4; Ui.Text(bx, by, s.ToUpper(), Ui.Title); by += 10; Ui.Line(bx, by - 2, bw, Ui.Hex("#c8a070")); by += 2; }
        void Para(string s, Color? c = null) { by += Ui.Wrapped(new Rect(bx, by, bw, 400), s, c ?? Ui.InkSoft) + 2; }

        void BookPanel()
        {
            float pw = 262;
            var r = Block(new Rect(W - pw - 4, 52, pw, H - 52 - 64));
            Ui.Panel(r);
            string[] titles = { "", "Town Ledger", "Citizens", "Trade & Stores", "Election", "The Gazette", "Freight from Back East" };
            Ui.Text(r.x + 10, r.y + 9, titles[(int)tab], Ui.Title);
            if (Ui.Button(new Rect(r.xMax - 22, r.y + 6, 15, 14), "", false, true, Icon.Close)) Later(() => tab = Tab.None);
            float top = r.y + 22;
            if (tab == Tab.Ledger) { LedgerPages(new Rect(r.x + 8, r.y + 24, r.width - 16, 14)); top += 18; }
            var view = new Rect(r.x + 8, top, r.width - 16, r.yMax - 8 - top);
            Ui.Fill(new Rect(view.x, view.y - 1, view.width, 1), Ui.Hex("#c8a070"));
            panelScroll = GUI.BeginScrollView(view, panelScroll, new Rect(0, 0, view.width - 6, Mathf.Max(view.height, panelContentH)), false, false, GUIStyle.none, GUIStyle.none);
            bx = 2; by = 4; bw = view.width - 12;
            switch (tab)
            {
                case Tab.Ledger: LedgerTab(); break;
                case Tab.Citizens: CitizensTab(); break;
                case Tab.Trade: TradeTab(); break;
                case Tab.Election: ElectionTab(); break;
                case Tab.Gazette: GazetteTab(); break;
                case Tab.Freight: FreightTab(); break;
            }
            panelContentH = by + 6;
            GUI.EndScrollView();
            ScrollIndicator(view, panelScroll.y, panelContentH);
        }

        void ScrollIndicator(Rect view, float scroll, float contentH)
        {
            if (contentH <= view.height) return;
            float frac = view.height / contentH, pos = scroll / (contentH - view.height);
            float h = Mathf.Max(10, view.height * frac);
            Ui.Fill(new Rect(view.xMax - 3, view.y + (view.height - h) * pos, 3, h), Ui.Hex("#b8844a"));
        }

        float SliderRow(string label, string value, float v, float min, float max, float step, string help)
        {
            Ui.Text(bx, by, label, Ui.Ink);
            Ui.Text(bx + bw, by, value, Ui.Hex("#8a5a10"), Ui.Align.Right);
            by += 10;
            float nv = Ui.Slider(new Rect(bx, by, bw, 9), v, min, max, step);
            by += 11;
            if (help != null) Para(help);
            by += 2;
            return nv;
        }

        // ================================================================== the ledger: who has what
        enum Page { Town, People, Business, Bank, Rules }
        enum Sort { Money, Earned, Name, Job }
        Page page = Page.Town;
        Sort sortBy = Sort.Money;
        static readonly string[] PageNames = { "Town", "People", "Business", "Bank", "Rules" };
        static readonly string[] PageTips =
        {
            "The town's money: what came in, what went out, and who holds the money in town",
            "Every settler: what they have and what they earned",
            "Every business: profit, cash, what the owner took home, and loans",
            "The bank: the vault, savers and loans",
            "Taxes, wages, rent, the stores and elections"
        };

        static string M(float v) { return Town.Money(v); }
        bool IsSel(SelectionKind k, int id) { return gc.Selected.Kind == k && gc.Selected.Id == id; }

        /// <summary>The page buttons along the top of the ledger, outside the scrolling part.</summary>
        void LedgerPages(Rect r)
        {
            float w = Mathf.Floor((r.width - 8) / 5f);
            for (int i = 0; i < 5; i++)
            {
                var b = new Rect(r.x + i * (w + 2), r.y, w, 14);
                var pg = (Page)i;
                if (Ui.Button(b, PageNames[i], page == pg)) Later(() => { page = pg; panelScroll = Vector2.zero; });
                if (Ui.Hover(b)) hoverTip = PageTips[i];
            }
        }

        void LedgerTab()
        {
            switch (page)
            {
                case Page.Town: TownPage(); break;
                case Page.People: PeoplePage(); break;
                case Page.Business: BusinessPage(); break;
                case Page.Bank: BankPage(); break;
                default: RulesPage(); break;
            }
        }

        /// <summary>A row that selects a settler or building when clicked. Returns true while the mouse is over it.</summary>
        bool PickRow(string left, string right, Color lc, Color rc, Selection pick)
        {
            var rr = new Rect(bx - 2, by - 2, bw + 4, 11);
            bool sel = IsSel(pick.Kind, pick.Id), hov = Ui.Hover(rr);
            if (sel || hov) Ui.Fill(rr, sel ? Ui.Gold : Ui.ParchDark);
            float rw = Ui.Text(bx + bw, by, right, rc, Ui.Align.Right);
            Ui.Text(bx, by, Trim(left, bw - rw - 6), lc);
            if (Ui.Clickable(rr)) Later(() => { gc.Select(pick); ResetConfirm(); });
            by += 11;
            return hov;
        }

        // ---------------------------------------------------------------- town page
        void TownPage()
        {
            Row("Treasury", M(T.Treasury), T.Treasury < 0 ? Ui.Bad : Ui.Title);
            Sparkline(new Rect(bx, by, bw, 26)); by += 30;

            var C = T.LedgerThisWeek; var L = T.HasLastWeek ? T.LedgerLastWeek : null;
            Heading("The town's money");
            Ui.Text(bx + bw - 54, by, "THIS WK", Ui.InkSoft, Ui.Align.Right); Ui.Text(bx + bw, by, "LAST WK", Ui.InkSoft, Ui.Align.Right); by += 11;
            Ui.Text(bx, by, "Came in", Ui.InkSoft); by += 10;
            Book("Taxes", C, L, x => x.IncomeTax + x.HeadTax + x.BusinessTax, 1, x => "income tax " + M(x.IncomeTax) + ", head tax " + M(x.HeadTax) + ", business tax " + M(x.BusinessTax) + ".");
            Book("Rent", C, L, x => x.Rent, 1, x => "rent from cottages, large houses, manors and boarders " + M(x.Rent) + ".");
            Book("Sales", C, L, x => x.StoreSales + x.Exports + x.TownTakings, 1, x => "storehouse sales " + M(x.StoreSales) + ", takings from town-run businesses " + M(x.TownTakings) + (x.Exports > 0 ? ", the wagon sold " + M(x.Exports) : "") + ".");
            Book("Deeds & claims", C, L, x => x.Deeds + x.Claims, 1, x => "businesses sold to families " + M(x.Deeds) + ", land claims from newcomers " + M(x.Claims) + ".");
            if (C.Borrowed > 0.01f || (L != null && L.Borrowed > 0.01f)) Book("Borrowed", C, L, x => x.Borrowed, 1, x => "borrowed from the bank when the treasury ran dry " + M(x.Borrowed) + ".");
            Ui.Text(bx, by, "Went out", Ui.InkSoft); by += 10;
            Book("Wages & relief", C, L, x => x.Salaries + x.Builders + x.Relief, -1, x => "salaries for the sheriff, teacher, doctor, banker and the rest " + M(x.Salaries) + (x.Builders > 0 ? ", day labor " + M(x.Builders) : "") + ", relief for laborers with no work " + M(x.Relief) + ".");
            Book("Building", C, L, x => x.Contracts + x.Construction, -1, x => "builder's yard contracts " + M(x.Contracts) + ", clearing land and roads " + M(x.Construction) + ".");
            Book("Supplies", C, L, x => x.Materials + x.Imports - x.Refunds + x.TownTills, -1, x => "food, firewood and materials for the stores " + M(x.Materials) + (x.Imports > 0 ? ", freight from back east " + M(x.Imports - x.Refunds) : "") + ", cash put in town-run tills " + M(x.TownTills) + ".");
            Book("Upkeep & grants", C, L, x => x.Upkeep + x.Grants, -1, x => "upkeep " + M(x.Upkeep) + ", grants to new owners " + M(x.Grants) + ".");
            if (C.LoanPayments > 0.01f || (L != null && L.LoanPayments > 0.01f) || T.TownDebt > 0.01f) Book("Bank loan", C, L, x => x.LoanPayments, -1, x => "interest and repayment on the town's loan " + M(x.LoanPayments) + ".");
            Ui.Line(bx, by, bw, Ui.Hex("#c8a070")); by += 2;
            Book("Net", C, L, x => x.Net, 0, null);
            if (C.CharityGoods > 0.5f) Para("Handed out free this week: goods worth " + M(C.CharityGoods) + ".");

            // who holds the money
            float home = 0, banked = 0;
            foreach (var p in T.People) { home += p.Money; banked += p.Deposit; }
            float till = 0, acct = 0, vault = 0, deposits = 0, lent = 0, owedTown = 0;
            foreach (var b in T.Businesses)
            {
                if (b.Type == BuildingType.Bank) { vault += b.Cash; deposits += T.DepositsAt(b); }
                else { till += b.Cash; acct += b.Deposit; }
            }
            foreach (var b in T.Buildings)
                if (b.Debt > 0.01f) { if (T.GetBuilding(b.LenderId) != null) lent += b.Debt; else owedTown += b.Debt; }
            lent += T.TownDebt;
            float bankOwn = vault + lent - deposits;
            bool hasBank = vault > 0.01f || lent > 0.01f || deposits > 0.01f;

            Heading("Who has the money");
            var cols = new[] { Ui.Hex("#d8a040"), Ui.Hex("#6a9a3a"), Ui.Hex("#5a86a8"), Ui.Hex("#9a5a8a") };
            float[] part = { Mathf.Max(0, T.Treasury), home + banked, till + acct, hasBank ? Mathf.Max(0, bankOwn) : 0 };
            float sum = part.Sum();
            var bar = new Rect(bx, by, bw, 9);
            Ui.Fill(bar, Ui.Ink);
            if (sum > 0)
            {
                float x = bar.x + 1;
                for (int i = 0; i < part.Length; i++) { float w = (bar.width - 2) * part[i] / sum; Ui.Fill(new Rect(x, bar.y + 1, w, bar.height - 2), cols[i]); x += w; }
            }
            by += 13;
            Legend(cols[0], "Town treasury", T.Treasury, null);
            Legend(cols[1], "Settlers", home + banked, "at home " + M(home) + (banked > 0.01f ? ", in the bank " + M(banked) : ""));
            Legend(cols[2], "Businesses", till + acct, "in the tills " + M(till) + (acct > 0.01f ? ", in the bank " + M(acct) : ""));
            if (hasBank) Legend(cols[3], "The bank's own", bankOwn, "vault " + M(vault) + " + lent out " + M(lent) + " - savers' money " + M(deposits));
            if (lent - T.TownDebt > 0.5f) Row("Businesses owe the bank", M(lent - T.TownDebt), Ui.Warn);
            if (T.TownDebt > 0.5f) Row("The town owes the bank", M(T.TownDebt), Ui.Warn);
            if (owedTown > 0.5f) Row("Businesses owe the town", M(owedTown), Ui.Warn);

            // settlers at a glance
            var grown = T.Adults.OrderBy(p => T.Savings(p)).ToList();
            if (grown.Count > 0)
            {
                Heading("Settlers");
                var rich = grown[grown.Count - 1];
                PickRow("Richest: " + rich.Name, M(T.Savings(rich)), Ui.Ink, Ui.Title, Selection.Person(rich.Id));
                var poorest = grown[0];
                PickRow("Poorest: " + poorest.Name, M(T.Savings(poorest)), Ui.Ink, T.Savings(poorest) < 5f ? Ui.Bad : Ui.Title, Selection.Person(poorest.Id));
                Row("Typical grown-up has", M(T.Savings(grown[grown.Count / 2])));
                int poor = grown.Count(p => T.Savings(p) < 5f);
                Row("Down to their last $5", poor + " of " + grown.Count, poor > 0 ? Ui.Warn : Ui.Ink);
                int charity = T.People.Count(p => p.CharityDays > 0);
                if (charity > 0) Row("Living on town charity", charity.ToString(), Ui.Warn);
                Row(T.PeopleWeekValid ? "All earned last week" : "All earned this week", M(grown.Sum(p => T.PeopleWeekValid ? p.EarnedLast : p.Earned)));
                Para("The People page lists everyone and what they have.");
            }
        }

        void Book(string name, TownLedger C, TownLedger L, System.Func<TownLedger, float> f, int sign, System.Func<TownLedger, string> tip)
        {
            var rr = new Rect(bx - 2, by - 2, bw + 4, 11);
            if (tip != null && Ui.Hover(rr))
            {
                Ui.Fill(rr, Ui.ParchDark);
                hoverTip = name + "\nThis week: " + tip(C) + (L != null ? "\nLast week: " + tip(L) : "");
            }
            Book(name, f(C), L != null ? f(L) : (float?)null, sign);
        }

        void Legend(Color c, string name, float v, string sub)
        {
            Ui.Fill(new Rect(bx, by, 7, 7), Ui.Ink); Ui.Fill(new Rect(bx + 1, by + 1, 5, 5), c);
            Ui.Text(bx + 11, by, name, Ui.Ink);
            Ui.Text(bx + bw, by, M(v), v < 0 ? Ui.Bad : Ui.Title, Ui.Align.Right);
            by += 10;
            if (sub != null) by += Ui.Wrapped(new Rect(bx + 11, by, bw - 11, 60), sub, Ui.InkSoft);
            by += 2;
        }

        // ---------------------------------------------------------------- people page
        string JobOf(Citizen p)
        {
            var own = p.Role == Role.Owner ? T.OwnedBusiness(p) : null;
            if (own != null) return own.Def.Name;
            var w = T.GetBuilding(p.WorkId);
            if (p.Role == Role.Worker && w != null) return w.Def.Name;
            return Defs.RoleName(p.Role);
        }

        string MoneyTip(Citizen p)
        {
            var job = T.GetBuilding(p.WorkId);
            string s = p.Name + (p.FamilyHand && job != null ? "\nWorks the family " + job.Def.Name.ToLower() + " for no wage." : "") + "\nAt home " + M(p.Money) + (p.Deposit > 0.01f ? ", in the bank " + M(p.Deposit) : "") + ".";
            if (T.PeopleWeekValid) s += "\nLast week: earned " + M(p.EarnedLast) + ", spent " + M(p.SpentLast) + ".";
            s += "\nThis week so far: earned " + M(p.Earned) + ".";
            var sp = T.GetCitizen(p.PartnerId);
            if (sp != null) s += "\nWith " + sp.First + ", the family has " + M(T.Savings(p) + T.Savings(sp)) + ".";
            var own = T.OwnedBusiness(p);
            if (own != null) s += "\nThe " + own.Def.Name + " has " + M(own.Cash + own.Deposit) + (own.Debt > 0.5f ? " and owes " + M(own.Debt) : "") + ".";
            if (p.CharityDays > 0) s += "\nLiving on town charity.";
            return s;
        }

        void PeoplePage()
        {
            bool full = T.PeopleWeekValid;
            var folk = T.People.Where(p => !p.IsChild || T.Savings(p) > 0.5f).ToList();
            float total = folk.Sum(p => T.Savings(p));
            Row("Settlers' money, all told", M(total));
            Row("On average", M(total / Mathf.Max(1, folk.Count)) + " each");
            int poor = folk.Count(p => T.Savings(p) < 5f);
            Row("Down to their last $5", poor.ToString(), poor > 0 ? Ui.Warn : Ui.Ink);
            Para("HAS is money at home and in the bank. EARNED is what came in " + (full ? "last week" : "this week so far") +
                 ", after tax: wages, shares, an owner's take, interest. A gold mark: they own the business. Point at a name for more; click to find them.");
            string[] sorts = { "Most money", "Earned", "Name", "Job" };
            float sw = Mathf.Floor((bw - 6) / 4f);
            for (int i = 0; i < 4; i++)
            {
                var s = (Sort)i;
                if (Ui.Button(new Rect(bx + i * (sw + 2), by, sw, 13), sorts[i], sortBy == s)) Later(() => sortBy = s);
            }
            by += 18;
            Ui.Text(bx, by, "NAME", Ui.InkSoft); Ui.Text(bx + 91, by, "WORK", Ui.InkSoft);
            Ui.Text(bx + bw - 44, by, full ? "EARNED" : "SO FAR", Ui.InkSoft, Ui.Align.Right); Ui.Text(bx + bw, by, "HAS", Ui.InkSoft, Ui.Align.Right);
            by += 11;
            System.Func<Citizen, float> earned = p => full ? p.EarnedLast : p.Earned;
            IEnumerable<Citizen> list;
            switch (sortBy)
            {
                case Sort.Earned: list = folk.OrderByDescending(earned).ThenByDescending(p => T.Savings(p)); break;
                case Sort.Name: list = folk.OrderBy(p => p.Family).ThenBy(p => p.First); break;
                case Sort.Job: list = folk.OrderBy(p => System.Array.IndexOf(RoleOrder, p.Role)).ThenBy(JobOf).ThenByDescending(p => T.Savings(p)); break;
                default: list = folk.OrderByDescending(p => T.Savings(p)); break;
            }
            foreach (var p in list)
            {
                var rr = new Rect(bx - 2, by - 2, bw + 4, 11);
                bool sel = IsSel(SelectionKind.Person, p.Id), hov = Ui.Hover(rr);
                if (sel || hov) Ui.Fill(rr, sel ? Ui.Gold : Ui.ParchDark);
                float has = T.Savings(p), e = earned(p);
                Ui.Text(bx, by, Trim(p.Name, 80), p.Jail > 0 ? Ui.Bad : Ui.Ink);
                bool owner = p.Role == Role.Owner && T.OwnedBusiness(p) != null;
                if (owner) { Ui.Fill(new Rect(bx + 84, by + 1, 5, 5), Ui.Ink); Ui.Fill(new Rect(bx + 85, by + 2, 3, 3), Ui.Gold); }
                Ui.Text(bx + 91, by, Trim(JobOf(p), bw - 91 - 80), owner ? Ui.Title : Ui.InkSoft);
                Ui.Text(bx + bw - 44, by, e > 0.005f ? M(e) : "-", e > 0.005f ? Ui.Ink : Ui.InkSoft, Ui.Align.Right);
                Ui.Text(bx + bw, by, M(has), has < 5f ? Ui.Bad : Ui.Title, Ui.Align.Right);
                if (hov) hoverTip = MoneyTip(p);
                if (Ui.Clickable(rr)) { var id = p.Id; Later(() => { gc.Select(Selection.Person(id)); ResetConfirm(); }); }
                by += 11;
            }
            if (folk.Count == 0) Para("Nobody here yet.");
        }

        // ---------------------------------------------------------------- business page
        string BizTip(Building b, Citizen o)
        {
            string s = b.Def.Name;
            if (b.Last.Valid)
            {
                s += "\nLast week: took in " + M(b.Last.Revenue) + ", costs " + M(b.Last.Costs) + ", profit " + M(b.Last.Profit) + (b.Last.Tax > 0.005f ? ", tax " + M(b.Last.Tax) : "") + ".";
                if (o != null) s += "\n" + o.First + " took home " + M(b.Last.OwnerDraw) + ".";
            }
            else s += "\nNot open a full week yet.";
            s += "\nThis week so far: took in " + M(b.Week.Revenue) + ", costs " + M(b.Week.Costs) + ".";
            if (b.Type != BuildingType.Bank) s += "\nTill " + M(b.Cash) + (b.Deposit > 0.01f ? ", in the bank " + M(b.Deposit) : "") + ".";
            s += "\n" + b.Employees.Count + (b.Employees.Count == 1 ? " hand" : " hands") + (Town.PaysByShare(b.Type) ? ", paid a share of each visit." : ", wage " + M(b.Wage) + " a day.");
            return s;
        }

        void BusinessPage()
        {
            var all = T.Businesses.ToList();
            if (all.Count == 0) { Para("No businesses yet. Build one from the Food or Industry tabs."); return; }
            var week = all.Where(b => b.Last.Valid).ToList();
            int owned = all.Count(b => b.OwnerId >= 0);
            Row("Businesses", owned + " owned · " + (all.Count - owned) + " town-run");
            float profit = week.Sum(b => b.Last.Profit);
            Row("Profit last week, all told", M(profit), profit < 0 ? Ui.Bad : Ui.Good);
            Row("Owners took home", M(week.Where(b => b.OwnerId >= 0).Sum(b => b.Last.OwnerDraw)));
            Row("Cash in tills and accounts", M(all.Where(b => b.Type != BuildingType.Bank).Sum(b => b.Cash + b.Deposit)));
            float debt = all.Sum(b => b.Debt);
            if (debt > 0.5f) Row("Owed on loans", M(debt), Ui.Warn);
            int losing = week.Count(b => b.Last.Profit < 0);
            if (losing > 0) Row("Lost money last week", losing.ToString(), Ui.Warn);
            int behind = all.Count(b => b.MissedPayments > 0);
            if (behind > 0) Row("Behind on loan payments", behind.ToString(), Ui.Bad);
            Para("Best week first. Point at one for its books; click to find it.");
            by += 2;
            foreach (var b in all.OrderByDescending(b => b.Last.Valid ? b.Last.Profit : -99999f))
            {
                var o = T.GetCitizen(b.OwnerId);
                bool owes = b.Debt > 0.5f;
                var rr = new Rect(bx - 2, by - 2, bw + 4, owes ? 32 : 22);
                bool sel = IsSel(SelectionKind.Building, b.Id), hov = Ui.Hover(rr);
                if (sel || hov) Ui.Fill(rr, sel ? Ui.Gold : Ui.ParchDark);
                Ui.Text(bx, by, b.Def.Name, Ui.Ink);
                Ui.Text(bx + bw, by, b.Last.Valid ? (b.Last.Profit >= 0 ? "+" : "") + M(b.Last.Profit) + "/wk" : "new", b.Last.Valid ? (b.Last.Profit < 0 ? Ui.Bad : Ui.Good) : Ui.InkSoft, Ui.Align.Right);
                by += 10;
                float cash = b.Type == BuildingType.Bank ? b.Cash : b.Cash + b.Deposit;
                float rw = Ui.Text(bx + bw, by, (b.Type == BuildingType.Bank ? "vault " : "has ") + M(cash), cash < b.Wage * 2 ? Ui.Warn : Ui.InkSoft, Ui.Align.Right);
                string who = o != null ? o.Name + (b.Last.Valid ? " · took " + M(b.Last.OwnerDraw) : "") : b.Type == BuildingType.Bank ? "no banker" : "town-run, for sale";
                Ui.Text(bx + 6, by, Trim(who, bw - 6 - rw - 6), o != null ? Ui.InkSoft : Ui.Warn);
                by += 10;
                if (owes)
                {
                    var lender = T.GetBuilding(b.LenderId);
                    string l3 = (o == null && lender != null ? "pledged to the bank " : "owes " + (lender != null ? "the bank " : "the town ")) + M(b.Debt) +
                                (b.MissedPayments > 0 ? " · " + b.MissedPayments + " missed" : "");
                    Ui.Text(bx + 6, by, l3, b.MissedPayments > 0 ? Ui.Bad : Ui.Warn);
                    by += 10;
                }
                if (hov) hoverTip = BizTip(b, o);
                if (Ui.Clickable(rr)) { var id = b.Id; Later(() => { gc.Select(Selection.Building(id)); ResetConfirm(); }); }
                by += 3;
            }
        }

        // ---------------------------------------------------------------- bank page
        void BankPage()
        {
            var banks = T.OfType(BuildingType.Bank).ToList();
            if (banks.Count == 0)
            {
                Para("No bank yet.", Ui.Ink);
                Para("Build one from the Trade tab. Without a bank, settlers keep their money at home, and a family has to pay for a business in full.");
                return;
            }
            foreach (var bank in banks)
            {
                var o = T.GetCitizen(bank.OwnerId);
                Heading(banks.Count > 1 ? "Bank #" + bank.Id : "The bank");
                if (o != null) PickRow("Banker", o.Name, Ui.InkSoft, Ui.Ink, Selection.Person(o.Id));
                else Para("No banker: no deposits or loans until a family buys it.", Ui.Warn);
                var loans = T.Buildings.Where(x => x.LenderId == bank.Id && x.Debt > 0.01f).OrderByDescending(x => x.Debt).ToList();
                var savers = T.People.Where(x => x.BankId == bank.Id && x.Deposit > 0.01f).OrderByDescending(x => x.Deposit).ToList();
                var firms = T.Buildings.Where(x => x.BankId == bank.Id && x.Deposit > 0.01f).OrderByDescending(x => x.Deposit).ToList();
                float townLoan = T.TownLenderId == bank.Id ? T.TownDebt : 0;
                float dep = T.DepositsAt(bank), lent = loans.Sum(x => x.Debt) + townLoan, own = bank.Cash + lent - dep;
                Row("In the vault", M(bank.Cash));
                Row("Savers' money", M(dep) + " (" + (savers.Count + firms.Count) + (savers.Count + firms.Count == 1 ? " account)" : " accounts)"));
                int nLoans = loans.Count + (townLoan > 0.01f ? 1 : 0);
                Row("Lent out", M(lent) + " (" + nLoans + (nLoans == 1 ? " loan)" : " loans)"));
                Row("Free to lend", M(T.Lendable(bank)), T.Lendable(bank) < 20 ? Ui.Warn : Ui.Good);
                Row("The bank's own money", M(own), own < 0 ? Ui.Bad : Ui.Title);
                if (bank.Last.Valid)
                {
                    Row("Profit last week", M(bank.Last.Profit), bank.Last.Profit < 0 ? Ui.Bad : Ui.Good);
                    if (o != null) Row("Banker took home", M(bank.Last.OwnerDraw) + " + " + M(T.Policy.BankerSalary * 7) + " salary");
                }
                Para("The town pays the banker " + M(T.Policy.BankerSalary) + " a day. The bank lends to families buying a business, to owners short of working money or repairs, and to the town when the treasury runs dry. The vault plus what's lent out, less what it owes savers, is the bank's own. Loans pay " + Mathf.RoundToInt(Balance.LoanInterest * 1000) / 10f +
                     "% a week; savers get up to " + Mathf.RoundToInt(Balance.DepositInterest * 1000) / 10f + "%. It keeps " + Mathf.RoundToInt(Balance.ReserveRatio * 100) + "% of savers' money in the vault.");
                Heading("Savers");
                foreach (var p in savers) { if (PickRow(p.Name, M(p.Deposit), Ui.Ink, Ui.Title, Selection.Person(p.Id))) hoverTip = MoneyTip(p); }
                foreach (var f in firms) PickRow(f.Def.Name + " (business)", M(f.Deposit), Ui.Ink, Ui.Title, Selection.Building(f.Id));
                if (savers.Count + firms.Count == 0) Para("Nobody banks here yet.");
                Heading("Loans");
                foreach (var l in loans)
                {
                    var lo = T.GetCitizen(l.OwnerId);
                    bool hov = PickRow(l.Def.Name + (lo != null ? " · " + lo.Name : " · taken back"), M(l.Debt), Ui.Ink, l.MissedPayments > 0 ? Ui.Bad : Ui.Title, Selection.Building(l.Id));
                    if (l.MissedPayments > 0) { Ui.Text(bx + 6, by - 1, l.MissedPayments + (l.MissedPayments == 1 ? " missed payment" : " missed payments") + ". Four and the bank takes it.", Ui.Bad); by += 10; }
                    else if (lo == null) { Ui.Text(bx + 6, by - 1, "Repossessed: paid first when it sells.", Ui.InkSoft); by += 10; }
                    if (hov) hoverTip = BizTip(l, lo);
                }
                if (townLoan > 0.01f) Row("The town", M(townLoan), Ui.Title);
                if (nLoans == 0) Para("No loans out.");
            }
        }

        // ---------------------------------------------------------------- rules page
        void RulesPage()
        {
            var P = T.Policy;
            Heading("Taxes");
            P.IncomeTax = SliderRow("Income tax", Mathf.RoundToInt(P.IncomeTax * 100) + "%", P.IncomeTax * 100, 0, 40, 1, "Taken from every wage. Over 12% people grumble.") / 100f;
            P.HeadTax = SliderRow("Head tax (weekly)", M(P.HeadTax), P.HeadTax, 0, 3, 0.25f, "Every adult pays on Sunday. Over $1 hurts morale.");
            P.BusinessTax = SliderRow("Business tax", Mathf.RoundToInt(P.BusinessTax * 100) + "%", P.BusinessTax * 100, 0, 50, 1, "Share of each business's weekly profit.") / 100f;
            Heading("Wages & town payroll");
            float mw = SliderRow("Minimum wage", M(P.MinWage), P.MinWage, 1, 5, 0.25f, null);
            if (!Mathf.Approximately(mw, P.MinWage)) T.SetMinWage(mw);
            P.ContractRate = SliderRow("Contract rate", M(P.ContractRate) + "/day", P.ContractRate, 2, 6, 0.25f, "What the town pays a builder's yard for each day of work a job takes. Half when work starts, half when it's done. Too low and yards lay off their crews.");
            if (!T.Yards.Any())
            {
                P.Builders = Mathf.RoundToInt(SliderRow("Day laborers for building", P.Builders.ToString(), P.Builders, 0, 12, 1, "With no builder's yard, the town hires settlers without a job by the day to build."));
                P.BuilderWage = SliderRow("Day wage", M(P.BuilderWage), P.BuilderWage, 1, 5, 0.25f, null);
            }
            P.ReliefWage = SliderRow("Laborer relief", M(P.ReliefWage), P.ReliefWage, 0, 2, 0.1f, "Daily pay for adults without a job on days there's no work for them.");
            P.Deputies = Mathf.RoundToInt(SliderRow("Deputies", P.Deputies.ToString(), P.Deputies, 0, 3, 1, "Sheriff $4/day, deputies $3, from the treasury."));
            Heading("Housing");
            P.Rent = SliderRow("Cottage rent (weekly)", M(P.Rent), P.Rent, 0, 6, 0.25f, "Every family in a cottage pays on Sunday. Four weeks behind and they're put out. Over $3 hurts morale. Families living on charity pay nothing.");
            P.BoardRent = SliderRow("Boarding rent (weekly)", M(P.BoardRent), P.BoardRent, 0, 3, 0.25f, "Each adult in a boarding house pays this. Over $1.50 hurts morale.");
            P.LargeRent = SliderRow("Large house rent (weekly)", M(P.LargeRent), P.LargeRent, 0, 15, 0.5f, "A family moves up from a cottage once they have " + M(T.MoveInAt(BuildingType.LargeHouse)) + " put by, and back down under " + M(T.KeepUpAt(BuildingType.LargeHouse)) + ". Over $8 they grumble.");
            P.ManorRent = SliderRow("Manor rent (weekly)", M(P.ManorRent), P.ManorRent, 0, 40, 1f, "The richest household moves in once they have " + M(T.MoveInAt(BuildingType.Manor)) + " put by, and moves out under " + M(T.KeepUpAt(BuildingType.Manor)) + ". Over $20 they grumble.");
            Heading("Stores");
            P.Charity = Ui.Toggle(new Rect(bx, by, bw, 11), P.Charity, "Storehouse feeds those who can't pay"); by += 13;
            P.StorehouseSells = Ui.Toggle(new Rect(bx, by, bw, 11), P.StorehouseSells, "Storehouse sells when shops run out"); by += 13;
            P.Reserve[0] = SliderRow("Keep food in store", Mathf.RoundToInt(P.Reserve[0]).ToString(), P.Reserve[0], 0, 400, 10, "The town buys food for the winter up to this much.");
            P.Reserve[1] = SliderRow("Keep firewood", Mathf.RoundToInt(P.Reserve[1]).ToString(), P.Reserve[1], 0, 200, 10, null);
            P.Reserve[2] = SliderRow("Keep logs", Mathf.RoundToInt(P.Reserve[2]).ToString(), P.Reserve[2], 0, 150, 5, null);
            P.Reserve[3] = SliderRow("Keep stone", Mathf.RoundToInt(P.Reserve[3]).ToString(), P.Reserve[3], 0, 150, 5, null);
            Heading("Bank");
            P.BankerSalary = SliderRow("Banker's salary", M(P.BankerSalary) + "/day", P.BankerSalary, 0, 6, 0.25f, "The town pays whoever runs the bank, so a banker can make a living. When the bank stands empty a week, the town sends back east for one.");
            P.TownBorrows = Ui.Toggle(new Rect(bx, by, bw, 11), P.TownBorrows, "Borrow from the bank when the treasury runs dry"); by += 13;
            Para("Up to " + M(Balance.TownCreditLimit) + ", paid back each Sunday with interest." + (T.TownDebt > 0.01f ? " The town owes " + M(T.TownDebt) + " now." : ""));
            Heading("Population");
            P.Births = Ui.Toggle(new Rect(bx, by, bw, 11), P.Births, "Couples have babies"); by += 13;
            P.Newcomers = Ui.Toggle(new Rect(bx, by, bw, 11), P.Newcomers, "Newcomers settle here"); by += 13;
            Para("Off, no babies are born / nobody comes up the road on their own (families, drifters, women for the brothel). Settlers you order by Freight, and a teacher, doctor, preacher or banker the town sends for, still come.");
            Heading("Mayor");
            bool on = Ui.Toggle(new Rect(bx, by, bw, 11), P.ElectionsOn, "Elect a mayor every 4 weeks"); by += 13;
            if (on != P.ElectionsOn) T.SetElections(on);
            by += 8;
            bool armed = Time.unscaledTime < confirmNewUntil;
            if (Ui.Button(new Rect(bx, by, 120, 15), armed ? "Click to confirm" : "Start a new town")) { if (armed) { Later(gc.NewTown); confirmNewUntil = 0; } else confirmNewUntil = Time.unscaledTime + 3; }
            Ui.Text(bx + bw, by + 4, "Saves every Monday", Ui.InkSoft, Ui.Align.Right);
            by += 18;
        }

        // ================================================================== freight orders
        /// <summary>One line of the order form: what it is, its price, and minus / count / plus.</summary>
        int OrderLine(Icon icon, string name, string price, int count, int step, int max)
        {
            Ui.IconAt(bx, by - 1, icon);
            Ui.Text(bx + 12, by, name, Ui.Ink);
            Ui.Text(bx + 118, by, price, Ui.InkSoft, Ui.Align.Right);
            float cx = bx + bw - 54;
            if (Ui.Button(new Rect(cx, by - 3, 15, 13), "", false, count > 0, Icon.Minus)) count = Mathf.Max(0, count - step);
            Ui.Text(cx + 27, by, count.ToString(), count > 0 ? Ui.Title : Ui.InkSoft, Ui.Align.Center);
            if (Ui.Button(new Rect(cx + 39, by - 3, 15, 13), "", false, count + step <= max, Icon.Plus)) count = Mathf.Min(max, count + step);
            by += 15;
            return count;
        }

        void FreightTab()
        {
            int days = T.FreightLeadDays;
            Para("Order what the town needs from back east. The treasury pays now, and a freight caravan brings it up the main road in about " + days +
                 " days" + (T.Season == 3 ? " (the roads are slow in winter)" : "") + ". Goods go into the storehouse; settlers step off the wagons and look for work.");
            float room = Mathf.Max(0, T.TownSpace - T.GoodsOnTheRoad);
            Heading("Goods");
            Good[] goods = { Good.Food, Good.Firewood, Good.Logs, Good.Stone, Good.Tools, Good.Iron, Good.Wool, Good.Clothes };
            float bulk = 0; foreach (var g in goods) bulk += cart[(int)g];
            foreach (var g in goods)
            {
                int step = g == Good.Tools || g == Good.Clothes ? 5 : 10;
                int max = Mathf.FloorToInt(cart[(int)g] + Mathf.Max(0, room - bulk));
                cart[(int)g] = OrderLine(GoodIcon(g), Defs.GoodNames[(int)g], Town.Money(Balance.ImportPrice[(int)g]) + " each", Mathf.RoundToInt(cart[(int)g]), step, max);
            }
            Row("Room in the stores", Mathf.FloorToInt(room) + (T.GoodsOnTheRoad > 0.5f ? " (after what's on the road)" : ""), room < 10 ? Ui.Warn : Ui.Ink);
            if (!T.Storages.Any()) Para("Build a storehouse to take goods.", Ui.Warn);
            cartLaborers = OrderLine(Icon.People, "Laborer", Town.Money(Balance.PassageLaborer), cartLaborers, 1, 10);
            cartFamilies = OrderLine(Icon.Child, "Family", Town.Money(Balance.PassageFamily), cartFamilies, 1, 5);
            Para("Iron goes to the blacksmith, wool to the tailor; clothes sell from the storehouse when no tailor has any.");
            Heading("Settlers");
            Para("The town pays their passage. They come with a little money of their own but no home: make sure there are beds (" + T.FreeBeds + " free now).", T.FreeBeds < cartLaborers + cartFamilies * 4 ? Ui.Warn : Ui.InkSoft);
            Heading("Order");
            float cost = T.OrderCost(cart, cartLaborers, cartFamilies);
            if (cost > 0) Row("Teamsters' fee", Town.Money(Balance.CaravanFee));
            Row("Total", Town.Money(cost), cost > T.Treasury ? Ui.Bad : Ui.Title);
            by += 2;
            bool can = cost > 0 && cost <= T.Treasury;
            if (Ui.Button(new Rect(bx, by, 120, 15), "Send the order", false, can))
            {
                var c = (float[])cart.Clone(); int lab = cartLaborers, fam = cartFamilies;
                Later(() =>
                {
                    var err = T.PlaceOrder(c, lab, fam);
                    if (err != null) Toast(err);
                    else { System.Array.Clear(cart, 0, cart.Length); cartLaborers = cartFamilies = 0; Toast("Order sent. The caravan is due in " + T.FreightLeadDays + " days."); }
                });
            }
            if (cost > T.Treasury) Ui.Text(bx + bw, by + 4, "Not enough in the treasury", Ui.Bad, Ui.Align.Right);
            by += 20;
            Heading("On the road");
            var open = T.Orders.Where(o => o.ArriveDay >= T.Day).OrderBy(o => o.ArriveDay).ToList();
            if (open.Count == 0) Para("Nothing ordered.");
            foreach (var o in open)
            {
                int left = o.ArriveDay - T.Day;
                Ui.Text(bx, by, left <= 0 ? "Arriving today" : "Due in " + left + (left == 1 ? " day" : " days"), left <= 0 ? Ui.Good : Ui.Title);
                Ui.Text(bx + bw, by, Town.Money(o.Cost), Ui.InkSoft, Ui.Align.Right); by += 10;
                Para(Town.FreightText(o) + ".", Ui.Ink);
            }
        }

        void Book(string name, float cur, float? last, int sign)
        {
            Color c = sign == 0 ? (cur < 0 ? Ui.Bad : Ui.Good) : sign < 0 ? Ui.InkSoft : Ui.Ink;
            Ui.Text(bx, by, name, sign == 0 ? Ui.Title : Ui.Ink);
            Ui.Text(bx + bw - 54, by, (sign < 0 && cur > 0 ? "-" : "") + Town.Money(cur), c, Ui.Align.Right);
            Color lc = sign == 0 && last.HasValue ? (last.Value < 0 ? Ui.Bad : Ui.Good) : c;
            Ui.Text(bx + bw, by, last.HasValue ? (sign < 0 && last.Value > 0 ? "-" : "") + Town.Money(last.Value) : "-", lc, Ui.Align.Right);
            by += 11;
        }

        void Sparkline(Rect r)
        {
            Ui.Inset(r);
            var hist = T.History;
            if (hist.Count < 2) return;
            float mn = Mathf.Min(0, hist.Min(p => p.Treasury)), mx = Mathf.Max(1, hist.Max(p => p.Treasury) * 1.05f);
            float step = (r.width - 4) / (hist.Count - 1);
            for (int i = 0; i < hist.Count; i++)
            {
                float y = r.yMax - 2 - (hist[i].Treasury - mn) / (mx - mn) * (r.height - 4);
                var col = Ui.Hex("#d8a040"); col.a = 0.35f;
                Ui.Fill(new Rect(r.x + 2 + i * step, y, Mathf.Max(1, step), r.yMax - 2 - y), col);
                Ui.Fill(new Rect(r.x + 2 + i * step, y, Mathf.Max(1, step), 1), Ui.Hex("#8a5a10"));
            }
        }

        static readonly Role[] RoleOrder = { Role.Mayor, Role.Owner, Role.Worker, Role.Sheriff, Role.Deputy, Role.Teacher, Role.Doctor, Role.Preacher, Role.Laborer, Role.Child };

        void CitizensTab()
        {
            int adults = T.Adults.Count(), kids = T.People.Count - adults;
            Row("Adults · children", adults + " · " + kids);
            Row("Births · deaths", T.Births + " · " + T.Deaths);
            Row("Without a roof", T.Homeless.ToString(), T.Homeless > 0 ? Ui.Bad : Ui.Ink);
            Row("Free beds", T.FreeBeds.ToString());
            by += 2;
            float x = bx;
            foreach (var role in RoleOrder)
            {
                int n = T.People.Count(p => p.Role == role); if (n == 0) continue;
                string s = n + " " + Defs.RoleName(role).ToLower() + (n > 1 && role != Role.Child ? "s" : (n > 1 ? "ren" : ""));
                float w = PixelFont.Width(s) + 8;
                if (x + w > bx + bw) { x = bx; by += 14; }
                var cr = new Rect(x, by, w, 12); Ui.Inset(cr); Ui.Text(cr.x + 4, cr.y + 3, s, Ui.Ink);
                x += w + 3;
            }
            by += 18;
            Ui.Text(bx, by, "NAME", Ui.InkSoft); Ui.Text(bx + 104, by, "JOB", Ui.InkSoft); Ui.Text(bx + bw, by, "HP  MOOD", Ui.InkSoft, Ui.Align.Right); by += 11;
            foreach (var p in T.People.OrderBy(p => System.Array.IndexOf(RoleOrder, p.Role)).ThenBy(p => p.Family))
            {
                var rr = new Rect(bx - 2, by - 2, bw + 4, 11);
                bool sel = gc.Selected.Kind == SelectionKind.Person && gc.Selected.Id == p.Id;
                if (sel || Ui.Hover(rr)) Ui.Fill(rr, sel ? Ui.Hex("#f2cf4a") : Ui.ParchDark);
                var w = T.GetBuilding(p.WorkId);
                string job = (p.Role == Role.Worker || p.Role == Role.Owner) && w != null ? w.Def.Name : Defs.RoleName(p.Role);
                string name = p.First + " " + p.Family + (p.IsChild ? " (" + Mathf.FloorToInt(p.Age) + ")" : "");
                Ui.Text(bx, by, Trim(name, 100), p.Jail > 0 ? Ui.Bad : Ui.Ink);
                Ui.Text(bx + 104, by, Trim(job, 80), Ui.InkSoft);
                Ui.Bar(new Rect(bx + bw - 44, by + 1, 18, 5), p.Health / 100f, Ui.Mood(p.Health));
                Ui.Bar(new Rect(bx + bw - 20, by + 1, 20, 5), p.Happy / 100f, Ui.Mood(p.Happy));
                if (Ui.Clickable(rr)) { var id = p.Id; Later(() => { gc.Select(Selection.Person(id)); ResetConfirm(); }); }
                by += 11;
            }
        }

        static string Trim(string s, float w)
        {
            if (PixelFont.Width(s) <= w) return s;
            while (s.Length > 1 && PixelFont.Width(s + ".") > w) s = s.Substring(0, s.Length - 1);
            return s + ".";
        }

        void TradeTab()
        {
            var m = T.Market;
            Heading("Food today · " + Defs.Seasons[T.Season]);
            Row("Grown in gardens", m.Garden.ToString("0.0"));
            Row("Still needed", m.Demand.ToString("0.0"));
            Row("Bought or handed out", m.Bought.ToString("0.0"));
            Row("Went without", m.Shortfall.ToString("0.0"), m.Shortfall > 0.5f ? Ui.Bad : Ui.Ink);
            Row("Food in town", Mathf.FloorToInt(T.TotalFood()).ToString());
            int eaters = T.People.Count;
            float days = eaters > 0 ? T.TotalFood() / Mathf.Max(1, eaters - T.Market.Garden) : 0;
            Row("Lasts about", Mathf.FloorToInt(days) + " days", days < 14 ? Ui.Bad : Ui.Ink);
            Heading("Heat");
            Row("Firewood burned per day", m.HeatDemand.ToString("0.0"));
            Row("Cold homes", m.ColdHomes.ToString(), m.ColdHomes > 0 ? Ui.Bad : Ui.Ink);
            Row("Firewood in town", Mathf.FloorToInt(T.TotalFirewood()).ToString());
            Heading("Town stores");
            Row("Capacity", Mathf.FloorToInt(T.TownStored) + " / " + Mathf.FloorToInt(T.TownCapacity));
            for (int g = 0; g < Balance.GoodCount; g++) if (g < 5 || T.TownStock((Good)g) >= 0.5f) Row(Defs.GoodNames[g], Mathf.FloorToInt(T.TownStock((Good)g)).ToString());
            Heading("Prices");
            Row("Producers get (food, wood)", Town.Money(Balance.Wholesale[0]) + ", " + Town.Money(Balance.Wholesale[1]));
            Row("Shops charge (food)", T.AverageFoodPrice > 0 ? Town.Money(T.AverageFoodPrice) : "-");
            Row("Storehouse charges", Town.Money(T.Policy.StorehousePrice[0]) + ", " + Town.Money(T.Policy.StorehousePrice[1]));
            foreach (var g in new[] { Good.Bread, Good.Meat, Good.Clothes })
            {
                var shops = T.Businesses.Where(b => Town.Produces(b.Type, g) && b.Def.IsRetail).ToList();
                if (shops.Count > 0) Row(Defs.GoodNames[(int)g] + " sells for", Town.Money(shops.Average(b => b.Price[(int)g])));
            }
            Heading("How folk live");
            int n = Mathf.Max(1, T.People.Count);
            Row("Bread on the table", Mathf.RoundToInt(100f * T.People.Count(p => p.AteBread) / n) + "%");
            Row("Meat on the table", Mathf.RoundToInt(100f * T.People.Count(p => p.AteMeat) / n) + "%");
            Row("In decent clothes", Mathf.RoundToInt(100f * T.People.Count(p => p.Clothes >= Balance.ClothesGood) / n) + "%");
            Row("Bathed this week", Mathf.RoundToInt(100f * T.Adults.Count(p => T.Day - p.BathedDay <= 7) / Mathf.Max(1, T.Adults.Count())) + "% of grown-ups");
            Heading("Businesses");
            foreach (var b in T.Businesses)
            {
                var o = T.GetCitizen(b.OwnerId);
                var rr = new Rect(bx - 2, by - 2, bw + 4, 21);
                if (Ui.Hover(rr)) Ui.Fill(rr, Ui.ParchDark);
                Ui.Text(bx, by, b.Def.Name, Ui.Ink);
                Ui.Text(bx + bw, by, b.Last.Valid ? Town.Money(b.Last.Profit) + "/wk" : "new", b.Last.Valid ? (b.Last.Profit < 0 ? Ui.Bad : Ui.Good) : Ui.InkSoft, Ui.Align.Right);
                by += 10;
                Ui.Text(bx + 6, by, (o != null ? o.Name : "For sale") + " · " + T.WorkersAt(b) + " working", o != null ? Ui.InkSoft : Ui.Warn);
                if (Ui.Clickable(rr)) { var id = b.Id; Later(() => gc.Select(Selection.Building(id))); }
                by += 11;
            }
            if (!T.Businesses.Any()) Para("No businesses yet. Build one from the Food or Industry tabs.");
        }

        void ElectionTab()
        {
            if (T.Estate == null)
            {
                Para("No Mayor's Estate yet.", Ui.Ink);
                Para("Build one from the Civic tab. The town then picks a mayor, who moves out of their cottage and into the estate. The mayor's platform gives the whole town a bonus.");
                return;
            }
            var m = T.Mayor;
            Heading("Mayor");
            if (m != null)
            {
                Ui.Text(bx, by, m.Name, Ui.Title); by += 11;
                var pd = Defs.Get(T.MayorPlatform);
                Para(pd.Name + ": " + pd.Description);
                if (T.Policy.ElectionsOn) Row("Term ends", "day " + T.TermEnd + " (" + Mathf.Max(0, T.TermEnd - T.Day) + " days)");
                else Para("Serving for life.", Ui.Warn);
            }
            else Para("The seat is empty.");
            if (T.Campaign.Active)
            {
                var poll = T.Poll(); int total = Mathf.Max(1, T.Adults.Count());
                int days = T.Campaign.VoteDay - T.Day;
                Heading("Campaign · vote in " + days + " day" + (days == 1 ? "" : "s"));
                foreach (var c in T.Campaign.Candidates.OrderByDescending(c => poll.ContainsKey(c.CitizenId) ? poll[c.CitizenId] : 0))
                {
                    var p = T.GetCitizen(c.CitizenId); if (p == null) continue;
                    int votes = poll.ContainsKey(c.CitizenId) ? poll[c.CitizenId] : 0;
                    Ui.Text(bx, by, p.Name + (T.MayorId == p.Id ? " (mayor)" : ""), Ui.Ink);
                    Ui.Text(bx + bw, by, votes + " votes", Ui.Title, Ui.Align.Right); by += 10;
                    Ui.Bar(new Rect(bx, by, bw, 6), votes / (float)total, Ui.Hex("#d8a040")); by += 8;
                    Para(Defs.Get(c.Platform).Name + ": " + Defs.Get(c.Platform).Description);
                }
                Para("Settlers favour whoever employs them, family, and whoever speaks to their troubles: food workers like the farm platform, tradesfolk like trade, the poor like Common Folk, and a crime wave helps Law & Order.");
            }
            if (T.LastElection.Valid)
            {
                Heading("Last election · day " + T.LastElection.Day);
                foreach (var r in T.LastElection.Results) Row(r.Name, r.Votes + " votes");
            }
        }

        void GazetteTab()
        {
            foreach (var l in T.Log.Take(80))
            {
                Color c = l.Kind == LogKind.Death || l.Kind == LogKind.Bad ? Ui.Bad : l.Kind == LogKind.Birth || l.Kind == LogKind.Good ? Ui.Good : l.Kind == LogKind.Vote ? Ui.Hex("#8a5a10") : Ui.Ink;
                Ui.Text(bx, by, "Day " + l.Day, Ui.InkSoft);
                by += Ui.Wrapped(new Rect(bx + 34, by, bw - 34, 400), l.Message, c) + 3;
            }
        }

        // ================================================================== inspector
        void Inspector()
        {
            float iw = 196;
            var r = Block(new Rect(4, 62, iw, Mathf.Min(H - 62 - 120, Mathf.Max(90, inspContentH + 34))));
            Ui.Panel(r);
            if (Ui.Button(new Rect(r.xMax - 22, r.y + 6, 15, 14), "", false, true, Icon.Close)) Later(() => gc.Select(Selection.None));
            var view = new Rect(r.x + 8, r.y + 8, r.width - 16, r.height - 14);
            inspScroll = GUI.BeginScrollView(view, inspScroll, new Rect(0, 0, view.width - 6, Mathf.Max(view.height, inspContentH)), false, false, GUIStyle.none, GUIStyle.none);
            bx = 2; by = 1; bw = view.width - 12;
            if (gc.Selected.Kind == SelectionKind.Person) PersonPanel(); else BuildingPanel();
            inspContentH = by + 4;
            GUI.EndScrollView();
            ScrollIndicator(view, inspScroll.y, inspContentH);
        }

        static string ChoreText(Citizen p)
        {
            switch (p.Chore)
            {
                case Chore.HaulLogs: return "hauling logs";
                case Chore.HaulStone: return "hauling stone";
                case Chore.Build: return "building";
                case Chore.Harvest: return "harvest hand";
                case Chore.Repair: return "repairing";
                case Chore.Craft: return "making furniture";
                case Chore.School: return p.IsChild ? "at school" : "teaching";
                case Chore.Tend: return "seeing patients";
                case Chore.Unload: return "unloading the wagon";
                case Chore.Preach: return "preaching and visiting";
                default: return "idle";
            }
        }

        void PersonPanel()
        {
            var p = T.GetCitizen(gc.Selected.Id);
            if (p == null) { Later(() => gc.Select(Selection.None)); return; }
            Ui.Text(bx, by, p.Name, Ui.Title); by += 10;
            Ui.Text(bx, by, Defs.RoleName(p.Role) + " · " + Mathf.FloorToInt(p.Age) + " years" + (p.Jail > 0 ? " · in jail" : ""), p.Jail > 0 ? Ui.Bad : Ui.InkSoft); by += 13;
            Ui.Text(bx, by, "Health", Ui.InkSoft); Ui.Bar(new Rect(bx + 46, by + 1, bw - 46, 6), p.Health / 100f, Ui.Mood(p.Health)); by += 10;
            Ui.Text(bx, by, "Mood", Ui.InkSoft); Ui.Bar(new Rect(bx + 46, by + 1, bw - 46, 6), p.Happy / 100f, Ui.Mood(p.Happy)); by += 12;
            Row("Eating", p.Fed >= 0.95f ? "well" : p.Fed >= 0.7f ? "short" : "hungry", p.Fed >= 0.95f ? Ui.Good : p.Fed >= 0.7f ? Ui.Warn : Ui.Bad);
            Row("Warmth", p.Warm ? "warm" : "cold", p.Warm ? Ui.Good : Ui.Bad);
            if (p.AteMeat || p.AteBread) Row("On the table", p.AteMeat && p.AteBread ? "bread and meat" : p.AteMeat ? "meat" : "bread", Ui.Good);
            Row("Clothes", p.Clothes > 0.8f ? "new" : p.Clothes >= Balance.ClothesGood ? "decent" : "patched and mended", p.Clothes >= Balance.ClothesGood ? Ui.Ink : Ui.Warn);
            if (!p.IsChild && T.Day - p.BathedDay <= 7) Row("Last bath", T.Day - p.BathedDay <= 0 ? "today" : (T.Day - p.BathedDay) + (T.Day - p.BathedDay == 1 ? " day ago" : " days ago"));
            if (p.ChurchDay > T.Day - 7 && p.ChurchDay >= 0) Row("Church", p.ChurchDay == T.Day ? "this morning" : "last Sunday");
            if (p.Sick > 0)
            {
                Row(Town.LaidUp(p) ? "Laid up with" : "Ill with", Defs.Illnesses[p.IllnessKind], p.Illness >= 10f ? Ui.Bad : p.Illness >= 3f ? Ui.Warn : Ui.InkSoft);
                if (p.SeenToday) Row("Doctor", "seen today", Ui.Good);
            }
            var home = T.GetBuilding(p.HomeId); var work = T.GetBuilding(p.WorkId);
            Row("Home", home != null ? (home.Type == BuildingType.House ? "Cottage #" + home.Id : home.Def.Name) : "tent by the wagon", home == null ? Ui.Bad : Ui.Ink);
            if (p.RentOwed > 0.01f) Row("Behind on rent", Town.Money(p.RentOwed), Ui.Warn);
            Row("Work", work != null ? work.Def.Name + (p.FamilyHand ? " (family)" : work.OwnerId < 0 && work.Def.IsBusiness ? " (town)" : "") : p.IsChild ? "too young" : p.Role == Role.Laborer ? "laborer" : "none");
            if (p.IsChild && p.Age >= Balance.SchoolAge)
            {
                float years = p.Schooling * Balance.SchoolYearsFull;
                Row("Schooling", years < 0.05f ? "none yet" : years.ToString("0.0") + " years" + (Town.Schooled(p) ? ", can read" : ""), Town.Schooled(p) ? Ui.Good : Ui.Ink);
                Row("Today", p.Chore == Chore.School ? "at school" : Town.LaidUp(p) ? "in bed" : p.Age >= 6 ? "helping at home" : "playing");
            }
            else if (!p.IsChild) Row("Schooling", Town.Schooled(p) ? "reads, writes, reckons" : "little or none", Town.Schooled(p) ? Ui.Ink : Ui.Warn);
            if (!p.IsChild && (p.Chore != Chore.None || p.Role == Role.Laborer))
            {
                Row("Today", ChoreText(p));
                var task = T.GetBuilding(p.TaskId);
                if (task != null && task != work) Row("At", task.Def.Name);
            }
            foreach (var e in T.Errands.Where(x => x.PersonId == p.Id))
            {
                var to = T.GetBuilding(e.ToId); if (to == null) continue;
                Para(e.Service ? "Today: a shave and a hot bath at the " + to.Def.Name + "."
                     : e.Delivery ? "Today: taking " + Defs.GoodNames[(int)e.Good].ToLower() + " to sell at the " + to.Def.Name + "."
                                : "Today: buying " + Defs.GoodNames[(int)e.Good].ToLower() + " at the " + to.Def.Name + ".", Ui.Ink);
            }
            if (p.CharityDays > 0) Row("Living on", "town charity", Ui.Warn);
            if (!p.IsChild || T.Savings(p) > 0.01f)
            {
                Heading("Money");
                Row("At home", M(p.Money), p.Money + p.Deposit < 5f ? Ui.Bad : Ui.Ink);
                if (p.Deposit > 0.01f) Row("In the bank", M(p.Deposit));
                if (T.PeopleWeekValid) { Row("Earned last week", M(p.EarnedLast)); Row("Spent last week", M(p.SpentLast)); }
                Row("Earned this week", M(p.Earned));
                var own = T.OwnedBusiness(p);
                if (own != null) Row("The " + own.Def.Name.ToLower() + " has", M(own.Cash + own.Deposit) + (own.Debt > 0.5f ? ", owes " + M(own.Debt) : ""), own.MissedPayments > 0 ? Ui.Bad : Ui.Ink);
            }
            var partner = T.GetCitizen(p.PartnerId);
            var kids = T.ChildrenOf(p).ToList();
            var parent = T.GetCitizen(p.ParentId);
            Heading("Family");
            if (partner != null) Row(p.Female ? "Husband" : "Wife", partner.First);
            if (parent != null) Row(p.Female ? "Daughter of" : "Son of", parent.First);
            if (kids.Count > 0) Para("Children: " + string.Join(", ", kids.Select(k => k.First + " (" + Mathf.FloorToInt(k.Age) + ")").ToArray()), Ui.Ink);
            if (partner == null && parent == null && kids.Count == 0) Para("On their own.");
            if (p.Grief > 5) Para("Grieving a loss in the family.", Ui.Bad);
            if (T.Campaign.Active && !p.IsChild)
            {
                var v = T.VoteOf(p); var vc = v != null ? T.GetCitizen(v.CitizenId) : null;
                if (vc != null) Row("Will vote for", vc.First + " " + vc.Family);
            }
        }

        /// <summary>Who comes by today: customers buying, and producers bringing goods to sell.</summary>
        void TripRows(Building b)
        {
            int buyers = T.Errands.Count(e => e.ToId == b.Id && !e.Delivery), drops = T.Errands.Count(e => e.ToId == b.Id && e.Delivery);
            if (buyers > 0) Row("Customers today", buyers.ToString());
            if (drops > 0) Row("Deliveries today", drops.ToString());
        }

        static Icon GoodIcon(Good g)
        {
            switch (g)
            {
                case Good.Furniture: return Icon.Chair;
                case Good.Meat: return Icon.Meat;
                case Good.Bread: return Icon.Bread;
                case Good.Iron: return Icon.Iron;
                case Good.Wool: return Icon.Wool;
                case Good.Hides: return Icon.Hide;
                case Good.Leather: return Icon.Leather;
                case Good.Clothes: return Icon.Clothes;
                case Good.Beer: return Icon.Beer;
                case Good.Cattle: return Icon.Steer;
                default: return (Icon)(int)g;
            }
        }

        /// <summary>Wear and repair state for buildings that wear.</summary>
        void ConditionRows(Building b)
        {
            if (!Town.Wears(b)) return;
            Ui.Text(bx, by, "Condition", Ui.InkSoft);
            Ui.Bar(new Rect(bx + 54, by + 1, bw - 54, 6), b.Condition / 100f, b.Condition < 25 ? Ui.Bad : b.Condition < 50 ? Ui.Warn : Ui.Good); by += 10;
            var y = T.GetBuilding(b.RepairBy);
            if (y != null)
            {
                bool working = T.People.Any(p => p.TaskId == b.Id && p.Chore == Chore.Repair);
                var o = T.GetCitizen(y.OwnerId);
                string who = o != null ? o.First + " " + o.Family + "'s yard" : "the builder's yard";
                Para(working ? "Being repaired by " + who + "." : b.RepairPaid ? "Repair under way with " + who + "." :
                     "Next in line at " + who + ": " + Town.Money(T.RepairPrice(b)) + " when work starts, paid by " + (b.Def.IsBusiness && b.OwnerId >= 0 ? "the owner." : "the town."), working ? Ui.Good : Ui.Warn);
            }
            else if (b.Condition < Balance.RepairAt) Para(T.Yards.Any() ? "Worn. Waiting for a builder's yard to have room; the worst go first." : "Worn, and there's no builder's yard to repair it.", Ui.Warn);
            bool home = b.Type == BuildingType.House || b.Type == BuildingType.BoardingHouse;
            if (b.Condition < 25) Para(home ? "The roof leaks: the family is miserable and burns more firewood." : b.Def.IsBusiness ? "Falling apart: work goes at half pace." : "Falling apart.", Ui.Bad);
            else if (b.Condition < 50) Para(home ? "Drafty: burns more firewood." : b.Def.IsBusiness ? "Worn: work goes a little slower." : "Worn.", Ui.Warn);
        }

        void Stocks(Building b, params Good[] goods)
        {
            float x = bx;
            foreach (var g in goods)
            {
                string s = Mathf.FloorToInt(b.Stock[(int)g]).ToString();
                Ui.IconAt(x, by - 1, GoodIcon(g)); Ui.Text(x + 11, by, s, Ui.Ink);
                x += 16 + PixelFont.Width(s) + 4;
            }
            by += 12;
        }

        void BuildingPanel()
        {
            var b = T.GetBuilding(gc.Selected.Id);
            if (b == null) { Later(() => gc.Select(Selection.None)); return; }
            var d = b.Def;
            Ui.Text(bx, by, d.Name, Ui.Title); by += 12;
            if (!b.Built)
            {
                Ui.Text(bx, by, "Under construction", Ui.Warn); by += 11;
                Ui.Text(bx, by, "Materials", Ui.InkSoft); Ui.Bar(new Rect(bx + 54, by + 1, bw - 54, 6), b.MaterialsFraction, Ui.Hex("#a8734a")); by += 10;
                Ui.Text(bx, by, "Work", Ui.InkSoft); Ui.Bar(new Rect(bx + 54, by + 1, bw - 54, 6), b.BuildFraction, Ui.Good); by += 12;
                if (d.Logs > 0) Row("Logs", Mathf.FloorToInt(b.Delivered[(int)Good.Logs]) + " / " + d.Logs);
                if (d.Stone > 0) Row("Stone", Mathf.FloorToInt(b.Delivered[(int)Good.Stone]) + " / " + d.Stone);
                int crew = T.People.Count(p => p.TaskId == b.Id);
                Row("Working on it", crew + (crew == 1 ? " person" : " people"), crew == 0 ? Ui.Warn : Ui.Ink);
                var yard = T.GetBuilding(b.ContractorId);
                if (yard != null)
                {
                    var yo = T.GetCitizen(yard.OwnerId);
                    Row("Contractor", yo != null ? yo.First + " " + yo.Family : "Builder's Yard");
                    Row("Contract", Town.Money(b.ContractPrice) + (b.ContractPaid > 0 ? " (" + Town.Money(b.ContractPaid) + " paid)" : ""));
                    if (b.ContractPaid <= 0 && T.Treasury < b.ContractPrice * 0.5f) Para("Waiting: the treasury can't pay the " + Town.Money(b.ContractPrice * 0.5f) + " deposit.", Ui.Bad);
                    else if (crew == 0) Para("Queued behind the yard's other jobs.", Ui.InkSoft);
                }
                if (b.MaterialsFraction < 0.999f && T.TownStock(Good.Logs) < 1 && d.Logs > b.Delivered[(int)Good.Logs]) Para("Waiting on logs. Build a lumber camp, or clear trees.", Ui.Bad);
                if (b.MaterialsFraction < 0.999f && T.TownStock(Good.Stone) < 1 && d.Stone > b.Delivered[(int)Good.Stone]) Para("Waiting on stone. Build a quarry near a hill or boulders.", Ui.Bad);
                if (!T.Yards.Any())
                {
                    if (T.Policy.Builders == 0) Para("No builder's yard, and the town hires no day laborers. Raise it in the Ledger.", Ui.Bad);
                    else if (crew == 0 && !T.People.Any(p => p.Role == Role.Laborer && !p.IsChild)) Para("No builder's yard, and everyone has a job. Nobody is free to build.", Ui.Warn);
                }
            }
            else if (d.IsBusiness)
            {
                var o = T.GetCitizen(b.OwnerId);
                if (o != null) Para("Owned by " + o.Name + (o.WorkId != b.Id ? " (away)" : ""), Ui.Ink);
                else if (b.Type == BuildingType.Bank) Para("No banker. The town doesn't lend. After a week empty the town sends back east for one" + (T.FreeBeds < 2 ? ", once there are two free beds." : ".") , Ui.Warn);
                else
                {
                    float price = T.DeedPrice(b);
                    Para("Run by the town until a family buys it, for " + Town.Money(price) + (T.Banks.Any() ? " (or " + Town.Money(price * Balance.MinDown) + " down with a bank loan)." : " in full."), Ui.Warn);
                }
                if (b.Type == BuildingType.Bank) Row("In the vault", Town.Money(b.Cash));
                else if (b.Deposit > 0.01f) { Row("In the till", Town.Money(b.Cash)); Row("In the bank", Town.Money(b.Deposit)); }
                else Row("Cash", Town.Money(b.Cash));
                if (Town.PaysByShare(b.Type)) Row("Pay", Mathf.RoundToInt(Balance.VisitShare * 100) + "% of each visit · about " + Town.Money(b.Wage) + "/day");
                else Row("Wage", Town.Money(b.Wage) + "/day");
                Row("Staff", T.WorkersAt(b) + " working · " + b.Employees.Count + "/" + d.MaxEmployees + " hired");
                if (b.Debt > 0.5f)
                {
                    var lender = T.GetBuilding(b.LenderId);
                    Row(lender != null ? (b.OwnerId < 0 ? "Pledged to the bank" : "Owes the bank") : "Owes the town", Town.Money(b.Debt), b.MissedPayments > 0 ? Ui.Bad : Ui.Warn);
                    if (b.OwnerId < 0 && lender != null) Para("The bank took it back. It's paid first out of the price when it sells.", Ui.InkSoft);
                    if (b.MissedPayments > 0) Para(b.MissedPayments + (b.MissedPayments == 1 ? " missed payment." : " missed payments.") + " Four and the bank takes it.", Ui.Bad);
                }
                ConditionRows(b);
                switch (b.Type)
                {
                    case BuildingType.Farm:
                    {
                        int si = T.Season;
                        string stage = si == 3 ? "Fallow for winter" : si <= 1 ? "Growing: " + Mathf.RoundToInt(b.CropGrowth * 100) + "%" : b.CropLeft > 1 ? "Harvesting: " + Mathf.FloorToInt(b.CropLeft) + " left" : "Harvested";
                        Row("Field", stage);
                        Stocks(b, Good.Food, Good.Tools);
                        break;
                    }
                    case BuildingType.Gatherer: Row("Trees nearby", T.TreesNear(b).ToString(), T.TreesNear(b) < 15 ? Ui.Warn : Ui.Ink); Stocks(b, Good.Food, Good.Tools); break;
                    case BuildingType.LumberCamp: Row("Trees nearby", T.TreesNear(b).ToString(), T.TreesNear(b) < 6 ? Ui.Warn : Ui.Ink); Stocks(b, Good.Logs, Good.Tools); break;
                    case BuildingType.Woodcutter: Stocks(b, Good.Logs, Good.Firewood, Good.Tools); break;
                    case BuildingType.Quarry: case BuildingType.Fishery: Stocks(b, b.Type == BuildingType.Quarry ? Good.Stone : Good.Food, Good.Tools); break;
                    case BuildingType.Smithy:
                        Stocks(b, Good.Logs, Good.Iron, Good.Stone, Good.Tools);
                        Para(b.Stock[(int)Good.Iron] >= 1f ? "Working iron: half again as many tools." : "No iron, so it's bog iron and stone. An iron mine, or iron from back east, gets half again as many tools.", b.Stock[(int)Good.Iron] >= 1f ? Ui.Good : Ui.InkSoft);
                        break;
                    case BuildingType.Ranch:
                    {
                        int past = T.PastureNear(b);
                        Row("Open grazing", past + " lots" + (past < Balance.PastureFull ? " (thin)" : ""), past < Balance.PastureFull ? Ui.Warn : Ui.Ink);
                        Stocks(b, Good.Cattle, Good.Wool, Good.Tools);
                        Para("Steers sell to the butcher for " + Town.Money(Balance.Wholesale[(int)Good.Cattle]) + "; past " + Balance.HerdCap + " head they're driven east for " + Town.Money(Balance.Export[(int)Good.Cattle]) + ". Wool is shorn in spring for the tailor." +
                             (past < Balance.PastureFull ? " Buildings and woods round it eat into the grazing." : ""), Ui.InkSoft);
                        break;
                    }
                    case BuildingType.Butcher:
                        Stocks(b, Good.Cattle, Good.Meat, Good.Hides);
                        Row("Meat price", Town.Money(b.Price[(int)Good.Meat]));
                        if (b.Stock[(int)Good.Cattle] < 0.5f && b.Stock[(int)Good.Meat] < 4) Para(T.OfType(BuildingType.Ranch).Any() ? "Waiting on steers from the ranch." : "No steers to dress. It needs a cattle ranch.", Ui.Warn);
                        break;
                    case BuildingType.Bakery:
                        Stocks(b, Good.Food, Good.Bread);
                        Row("Bread price", Town.Money(b.Price[(int)Good.Bread]));
                        if (b.Stock[(int)Good.Food] < 1f) Para("No grain. It buys from the crop fields, or from the granary when it holds more than the town needs.", Ui.Warn);
                        break;
                    case BuildingType.Brewery:
                        Stocks(b, Good.Food, Good.Firewood, Good.Beer);
                        Row("Sells beer for", Town.Money(Balance.Wholesale[(int)Good.Beer]) + " a measure");
                        if (T.People.Count > 0 && T.TotalFood() / T.People.Count < Balance.BrewingNeedsFood) Para("Food is short: no grain goes to beer.", Ui.Warn);
                        else if (!T.OfType(BuildingType.Saloon).Any() && !T.OfType(BuildingType.Hotel).Any()) Para("No saloon or hotel to sell to.", Ui.Warn);
                        break;
                    case BuildingType.IronMine:
                        Stocks(b, Good.Firewood, Good.Iron, Good.Tools);
                        Para("Burns a load of firewood for every two of iron. The blacksmith buys what he needs; the rest goes east at " + Town.Money(Balance.Export[(int)Good.Iron]) + ".", b.Stock[(int)Good.Firewood] < 1f ? Ui.Warn : Ui.InkSoft);
                        break;
                    case BuildingType.Tannery:
                        Stocks(b, Good.Hides, Good.Leather, Good.Tools);
                        if (b.Stock[(int)Good.Hides] < 1f) Para(T.OfType(BuildingType.Butcher).Any() ? "Waiting on hides from the butcher." : "No hides. They come from a butcher shop.", Ui.Warn);
                        if (T.IsOpen(b)) Para("Families living within " + Balance.TanneryReach + " lots grumble about the stink.", Ui.InkSoft);
                        break;
                    case BuildingType.Tailor:
                        Stocks(b, Good.Wool, Good.Leather, Good.Clothes);
                        Row("Clothes price", Town.Money(b.Price[(int)Good.Clothes]));
                        Row("Settlers needing clothes", T.People.Count(x => x.Clothes < Balance.ClothesBuyBelow).ToString());
                        if (b.Stock[(int)Good.Wool] < Balance.WoolPerClothes && b.Stock[(int)Good.Leather] < 1f) Para("No wool or leather. Wool comes from a ranch (or back east), leather from a tannery.", Ui.Warn);
                        break;
                    case BuildingType.Hotel:
                        Row("Rooms", d.Capacity.ToString());
                        Row("Guests last night", ((int)b.Today.Sold).ToString());
                        if (b.Last.Valid) { Row("Guests last week", ((int)b.Last.Sold).ToString()); if (b.Last.Made > b.Last.Sold + 0.5f) Row("Turned away", ((int)(b.Last.Made - b.Last.Sold)).ToString(), Ui.Warn); }
                        Stocks(b, Good.Food, Good.Meat, Good.Bread, Good.Beer, Good.Firewood);
                        Para("Travellers on the main road pay " + Town.Money(Balance.RoomRate) + " a room, " + Town.Money(Balance.MealPrice) + " for supper and " + Town.Money(Balance.GuestDrink) + " a drink: money from outside. A hand looks after " + Balance.GuestsPerHand + " guests.", Ui.InkSoft);
                        break;
                    case BuildingType.Brothel:
                        Row("Visits today", ((int)b.Today.Sold).ToString());
                        if (b.Last.Valid) { Row("Visits last week", ((int)b.Last.Sold).ToString()); if (b.Last.Made > b.Last.Sold + 0.5f) Row("Turned away", ((int)(b.Last.Made - b.Last.Sold)).ToString(), Ui.Warn); }
                        Para(Town.Money(Balance.VisitPrice) + " a visit: the house keeps " + Mathf.RoundToInt((1 - Balance.VisitShare) * 100) + "%, the woman " + Mathf.RoundToInt(Balance.VisitShare * 100) +
                             "% as her pay (taxed like a wage). Up to " + Balance.VisitsPerWoman + " visits a woman a day. Only unmarried women of 18 and over work here, and a woman who marries leaves.", Ui.InkSoft);
                        break;
                    case BuildingType.Barber:
                        Row("Customers today", ((int)b.Today.Sold).ToString());
                        Row("Bathed this week", T.Adults.Count(x => T.Day - x.BathedDay <= 7) + " of " + T.Adults.Count());
                        Stocks(b, Good.Firewood);
                        Para(Town.Money(Balance.BathPrice) + " for a shave and a hot bath, " + Town.Money(Balance.ShavePrice) + " for a shave when there's no firewood to heat the water. Clean folk fall sick less for a week.", b.Stock[(int)Good.Firewood] < Balance.BathFuel ? Ui.Warn : Ui.InkSoft);
                        break;
                    case BuildingType.Saloon:
                        Stocks(b, Good.Beer);
                        Row("On tap", b.Stock[(int)Good.Beer] >= 1f ? "beer from the brewery" : "whiskey only", b.Stock[(int)Good.Beer] >= 1f ? Ui.Good : Ui.InkSoft);
                        break;
                    case BuildingType.Bank:
                    {
                        var loans = T.Buildings.Where(x => x.LenderId == b.Id && x.Debt > 0.01f).ToList();
                        int savers = T.People.Count(x => x.BankId == b.Id && x.Deposit > 0.01f), firms = T.Buildings.Count(x => x.BankId == b.Id && x.Deposit > 0.01f);
                        Heading("The bank's books");
                        Row("Deposits", Town.Money(T.DepositsAt(b)));
                        Row("Accounts", savers + (savers == 1 ? " settler, " : " settlers, ") + firms + (firms == 1 ? " business" : " businesses"));
                        Row("Vault reserve", Town.Money(T.VaultReserve(b)));
                        Row("Free to lend", Town.Money(T.Lendable(b)), T.Lendable(b) < 20 ? Ui.Warn : Ui.Good);
                        Row("Loans out", loans.Count + (loans.Count > 0 ? ", " + Town.Money(loans.Sum(x => x.Debt)) + " owed" : ""));
                        if (T.TownLenderId == b.Id && T.TownDebt > 0.01f) Row("The town owes", Town.Money(T.TownDebt), Ui.Warn);
                        Row("Banker's salary", Town.Money(T.Policy.BankerSalary) + "/day from the town");
                        Row("Interest on loans", Mathf.RoundToInt(Balance.LoanInterest * 1000) / 10f + "% a week");
                        Row("Savings earn", "up to " + Mathf.RoundToInt(Balance.DepositInterest * 1000) / 10f + "% a week");
                        Para("Businesses bank their takings and settlers their savings, keeping " + Town.Money(Balance.PocketMoney) + " at home. They can draw on it any day. The bank keeps " + Mathf.RoundToInt(Balance.ReserveRatio * 100) + "% of deposits in the vault and lends out the rest: to families buying a business (a quarter down, the rest paid weekly), to owners short of working money or a repair, and to the town when the treasury runs dry. A business can owe up to what it's worth, and gets no more while it's behind on payments or has lost money three weeks running. Savers share in what the loans earn.", Ui.InkSoft);
                        break;
                    }
                    case BuildingType.BuilderYard:
                    {
                        int sites = T.Sites.Count(s => s.ContractorId == b.Id), fixes = T.Buildings.Count(x => x.Built && x.RepairBy == b.Id);
                        Row("Jobs", sites + (sites == 1 ? " building, " : " buildings, ") + fixes + (fixes == 1 ? " repair" : " repairs"));
                        int queue = T.Buildings.Count(x => x.Built && Town.Wears(x) && x.Condition < Balance.RepairAt && T.GetBuilding(x.RepairBy) == null);
                        if (queue > 0) Row("Repairs waiting in town", queue.ToString(), Ui.Warn);
                        Para("A yard takes on two repairs for each pair of hands it has, and two more. The rest wait for whichever yard has room.", Ui.InkSoft);
                        if (b.TownOwes > 0.5f) Row("Town owes them", Town.Money(b.TownOwes), Ui.Warn);
                        Stocks(b, Good.Logs, Good.Furniture, Good.Tools);
                        Row("Furniture price", Town.Money(b.Price[(int)Good.Furniture]));
                        if (sites + fixes == 0) Para("No jobs on the books: the crew makes furniture.", Ui.InkSoft);
                        break;
                    }
                    case BuildingType.Store: case BuildingType.Stall:
                        Stocks(b, d.Sells);
                        Row("Food price", Town.Money(b.Price[0]));
                        if (b.Type == BuildingType.Store) Row("Firewood price", Town.Money(b.Price[1]));
                        break;
                }
                if (b.Type != BuildingType.Hotel && b.Type != BuildingType.Barber && b.Type != BuildingType.Saloon && b.Type != BuildingType.Bank && b.Type != BuildingType.Brothel) Row("Made today", b.Today.Made.ToString("0.0"));
                TripRows(b);
                if (b.Last.Valid)
                {
                    Heading("Last week");
                    Row("Revenue", Town.Money(b.Last.Revenue)); Row("Costs", Town.Money(b.Last.Costs));
                    Row("Profit", Town.Money(b.Last.Profit), b.Last.Profit < 0 ? Ui.Bad : Ui.Good);
                    Row("Owner took home", Town.Money(b.Last.OwnerDraw));
                }
                var staff = b.Employees.Select(T.GetCitizen).Where(x => x != null).Select(x => x.First + " " + x.Family + (x.FamilyHand ? " (family)" : "")).ToArray();
                if (staff.Length > 0) { Heading("Hands"); Para(string.Join(", ", staff), Ui.Ink); }
            }
            else if (Town.IsHome(b.Type))
            {
                var rs = T.Household(b).ToList();
                Row("Beds", rs.Count + " / " + Mathf.Max(1, d.Beds));
                if (b.Type == BuildingType.House || b.Type == BuildingType.LargeHouse) Row("Garden today", b.GardenToday.ToString("0.0") + " food");
                Stocks(b, Good.Food, Good.Firewood, Good.Furniture);
                if (b.Stock[(int)Good.Bread] + b.Stock[(int)Good.Meat] > 0.1f) Stocks(b, Good.Bread, Good.Meat);
                if (b.Type == BuildingType.BoardingHouse && rs.Count > 0)
                {
                    Row("Rent", T.Policy.BoardRent <= 0 ? "free" : Town.Money(T.Policy.BoardRent) + "/week a boarder");
                    Para("No garden, no babies, and families leave when a cottage frees up.", Ui.InkSoft);
                }
                if (b.Type == BuildingType.House && rs.Count > 0)
                {
                    bool charity = T.Policy.Charity && rs.Any(p => p.CharityDays > 0);
                    Row("Rent", charity ? "waived (on charity)" : T.Policy.Rent <= 0 ? "free" : Town.Money(T.Policy.Rent) + "/week", Ui.Ink);
                    if (b.Arrears > 0.01f) Row("Behind on rent", Town.Money(b.Arrears), b.Arrears >= T.Policy.Rent * 3 ? Ui.Bad : Ui.Warn);
                }
                if (Town.HomeTier(b.Type) >= 1)
                {
                    float rent = T.RentOf(b.Type);
                    bool charity = T.Policy.Charity && rs.Any(p => p.CharityDays > 0);
                    Row("Rent", charity ? "waived (on charity)" : rent <= 0 ? "free" : M(rent) + "/week", Ui.Ink);
                    if (b.Arrears > 0.01f) Row("Behind on rent", M(b.Arrears), b.Arrears >= rent * 3 ? Ui.Bad : Ui.Warn);
                    if (rs.Count > 0) Row("Family has", M(rs.Where(p => !p.IsChild).Sum(p => T.Savings(p))));
                    Para((b.Type == BuildingType.Manor ? "When it's empty, the richest household in town with " : "When it's empty, the best-off household in a cottage or boarding house with ") + M(T.MoveInAt(b.Type)) +
                         " or more put by moves in. Under " + M(T.KeepUpAt(b.Type)) + " they move somewhere cheaper. Rent is set on the Ledger's Rules page.", Ui.InkSoft);
                }
                ConditionRows(b);
                if (b.ColdToday) Para("Cold: no firewood to burn.", Ui.Bad);
                Heading("Living here");
                foreach (var p in rs)
                {
                    var rr = new Rect(bx - 2, by - 2, bw + 4, 11);
                    if (Ui.Hover(rr)) Ui.Fill(rr, Ui.ParchDark);
                    Ui.Text(bx, by, p.First + " " + p.Family + (p.IsChild ? " (" + Mathf.FloorToInt(p.Age) + ")" : ""), Ui.Ink);
                    Ui.Text(bx + bw, by, Defs.RoleName(p.Role), Ui.InkSoft, Ui.Align.Right);
                    if (Ui.Clickable(rr)) { var id = p.Id; Later(() => gc.Select(Selection.Person(id))); }
                    by += 11;
                }
                if (rs.Count == 0) Para("Empty.");
                if (b.Type == BuildingType.Estate && T.MayorPlatform != Platform.None) Para("Platform: " + Defs.Get(T.MayorPlatform).Name + ". " + Defs.Get(T.MayorPlatform).Description);
            }
            else if (d.IsStorage)
            {
                Row("Holds", Mathf.FloorToInt(b.Stock.Sum()) + " / " + d.Capacity);
                ConditionRows(b);
                Stocks(b, Good.Food, Good.Firewood, Good.Logs, Good.Stone, Good.Tools);
                var more = Enumerable.Range(5, Balance.GoodCount - 5).Where(g => b.Stock[g] >= 0.5f).Select(g => (Good)g).ToArray();
                if (more.Length > 0) Stocks(b, more);
                TripRows(b);
                if (b.Type == BuildingType.Wagon)
                {
                    int carrying = T.People.Count(x => x.Chore == Chore.Unload);
                    if (!T.OfType(BuildingType.Storehouse).Any()) Para("Build a storehouse and the settlers will unload the wagon into it.", Ui.Warn);
                    else if (T.OfType(BuildingType.Storehouse).All(s => s.Stock.Sum() >= s.Def.Capacity - 1f) && carrying == 0)
                        Para("The storehouse is full. The rest waits here and gets used first; build another storehouse to empty it sooner.", Ui.Warn);
                    else Para(carrying > 0 ? carrying + (carrying == 1 ? " settler is" : " settlers are") + " carrying it into the storehouse." : "Being unloaded into the storehouse, a little each day after work.", Ui.Good);
                }
                Para(d.Blurb);
            }
            else if (b.Type == BuildingType.Sheriff)
            {
                ConditionRows(b);
                var st = b.Employees.Select(T.GetCitizen).Where(x => x != null).Select(x => Defs.RoleName(x.Role) + " " + x.First + " " + x.Family).ToArray();
                Para(st.Length > 0 ? string.Join(", ", st) : "No lawmen hired yet.", Ui.Ink);
                Row("Crimes this week", T.CrimesWeek.ToString()); Row("Caught", T.CaughtWeek.ToString());
                Row("Last week", T.CaughtLast + " of " + T.CrimesLast + " caught");
                var jailed = T.People.Where(x => x.Jail > 0).Select(x => x.First + " " + x.Family).ToArray();
                Para("Jailhouse: " + (jailed.Length > 0 ? string.Join(", ", jailed) : "empty"));
            }
            else if (b.Type == BuildingType.School)
            {
                ConditionRows(b);
                var tch = T.StaffOf(b, Role.Teacher);
                Row("Teacher", tch != null ? tch.First + " " + tch.Family : "none yet", tch != null ? Ui.Ink : Ui.Warn);
                if (tch == null) Para("Takes a settler who can read and write. If nobody in town can, the town sends back east: someone comes once there's a free bed and " + Town.Money(40) + " in the treasury.", Ui.Warn);
                Row("Term", T.SchoolInSession ? "in session" : "out for " + (T.Season == 0 ? "spring planting" : "the harvest"), T.SchoolInSession ? Ui.Good : Ui.InkSoft);
                int age = T.People.Count(x => x.IsChild && x.Age >= Balance.SchoolAge);
                Row("Pupils today", (T.SchoolInSession ? b.Today.Sold : 0) + " of " + age + " children");
                Row("Seats", d.Capacity.ToString());
                Row("Salary", Town.Money(Balance.SalaryTeacher) + "/day in term");
                Row("Grown-ups schooled", T.Adults.Count(Town.Schooled) + " of " + T.Adults.Count());
                Para("Children here 2 years or more can read, write and reckon: they can keep a store or a bank, and get 10% more done than those who can't.", Ui.InkSoft);
            }
            else if (b.Type == BuildingType.DoctorOffice)
            {
                ConditionRows(b);
                var dr = T.StaffOf(b, Role.Doctor);
                Row("Doctor", dr != null ? "Dr. " + dr.First + " " + dr.Family : "none yet", dr != null ? Ui.Ink : Ui.Warn);
                if (dr == null) Para("The town has sent back east for a doctor. One comes once there's a free bed and " + Town.Money(40) + " in the treasury.", Ui.Warn);
                Row("Patients today", b.Today.Sold.ToString());
                Row("Ill or hurt", T.People.Count(x => x.Sick > 0) + " (" + T.People.Count(Town.LaidUp) + " in bed)");
                Row("Salary", Town.Money(Balance.SalaryDoctor) + "/day");
                Para("Sees up to " + Balance.DoctorPatients + " of the worst cases a day. Patients who can afford it pay " + Town.Money(Balance.VisitFee) + " a visit.", Ui.InkSoft);
                var sick = T.People.Where(x => x.Sick > 0 && x.Illness >= 3f).OrderByDescending(x => x.Illness).Take(6).ToList();
                if (sick.Count > 0)
                {
                    Heading("Sick and hurt");
                    foreach (var x in sick) Para(x.First + " " + x.Family + ": " + Defs.Illnesses[x.IllnessKind] + (x.SeenToday ? " (seen)" : ""), x.Illness >= 10f ? Ui.Bad : Ui.Ink);
                }
            }
            else if (b.Type == BuildingType.Church)
            {
                ConditionRows(b);
                var rev = T.StaffOf(b, Role.Preacher);
                Row("Preacher", rev != null ? "Rev. " + rev.First + " " + rev.Family : "none yet", rev != null ? Ui.Ink : Ui.Warn);
                if (rev == null) Para("Takes a settler who can read and write. If nobody can, the town sends back east: someone comes once there's a free bed and " + Town.Money(40) + " in the treasury.", Ui.Warn);
                Row("Last Sunday", b.Attendance + " in the pews");
                Row("Collection", Town.Money(b.Collection));
                Row("Town makes it up to", Town.Money(Balance.PlateTopUp) + " a week");
                Row("Pews", d.Capacity.ToString());
                Para("Sunday service cheers the congregation and eases grief. A church wedding cheers the whole town, a funeral is a comfort, and grief passes sooner with a preacher to talk to. Families moving west like a town with a church.", Ui.InkSoft);
            }
            else if (b.Type == BuildingType.Cemetery)
            {
                Row("Graves", b.Graves + " / " + d.Graves);
                if (T.Unburied > 0) Para(T.Unburied + " recent dead had nowhere to be buried.", Ui.Bad);
            }
            if (b.Type != BuildingType.Wagon)
            {
                by += 6;
                bool armed = confirmTearDown == b.Id;
                string label = !b.Built ? (armed ? "Click to cancel it" : "Cancel construction") : (armed ? "Click to tear it down" : "Tear down");
                if (Ui.Button(new Rect(bx, by, 128, 15), label))
                {
                    if (armed) { var bb = b; Later(() => { if (T.Buildings.Contains(bb)) T.Remove(bb); gc.Select(Selection.None); }); confirmTearDown = -1; }
                    else confirmTearDown = b.Id;
                }
                by += 18;
                Para("You get back half the materials.");
            }
        }

        // ================================================================== map tooltip
        void MapTooltip()
        {
            if (!gc.PointerOnMap || Input.GetMouseButton(1) || Input.GetMouseButton(2)) return;
            string s = null; Color c = Ui.Ink; var h = gc.HoverTile;
            if (gc.Tool == ToolKind.Inspect)
            {
                var pick = gc.View.PickAt(gc.HoverPixel);
                if (pick.Kind == SelectionKind.Person)
                {
                    var p = T.GetCitizen(pick.Id);
                    if (p != null) { var w = T.GetBuilding(p.WorkId); s = p.Name + "\n" + Defs.RoleName(p.Role) + (w != null && p.Role != Role.Mayor ? " · " + w.Def.Name : ""); }
                }
                else if (pick.Kind == SelectionKind.Building)
                {
                    var b = T.GetBuilding(pick.Id);
                    if (b != null)
                    {
                        s = b.Def.Name + (b.Built ? "" : " (being built, " + Mathf.RoundToInt(b.BuildFraction * 100) + "%)");
                        if (b.Built && b.Def.IsBusiness) { var o = T.GetCitizen(b.OwnerId); s += "\n" + (o != null ? o.Name : "For sale") + " · " + T.WorkersAt(b) + " working"; }
                        if (Town.IsHome(b.Type) && b.Type != BuildingType.Estate) s += "\n" + (b.Residents.Count == 0 && Town.HomeTier(b.Type) >= 1 ? "Empty: needs a family with " + M(T.MoveInAt(b.Type)) : b.Residents.Count + " living here");
                    }
                }
                else
                {
                    byte t = T.TerrainAt(h.x, h.y);
                    if (t == Tile.Hill) s = "Hills: too steep to build";
                    else if (t == Tile.Tree) s = "Woods";
                    else if (t == Tile.Sapling) s = "Sapling";
                    else if (t == Tile.Rock) s = "Boulder";
                    else if (t == Tile.Water && T.OccupantAt(h.x, h.y) == null) s = "Water: roads can bridge it";
                }
            }
            else if (gc.Tool == ToolKind.Clear)
            {
                byte t = T.TerrainAt(h.x, h.y); var b = T.OccupantAt(h.x, h.y);
                if (b != null && b.Type != BuildingType.Road) s = b.Def.Name + ": click to select, then Tear down";
                else if (t == Tile.Tree) s = "Fell tree · $2 · +2 logs" + (T.TownSpace < 2 ? " (stores full)" : "");
                else if (t == Tile.Sapling) s = "Pull sapling · $2";
                else if (t == Tile.Rock) s = "Break boulder · $6 · +3 stone" + (T.TownSpace < 3 ? " (stores full)" : "");
                else if (t == Tile.Stump) s = "Pull stump · $0.50";
            }
            else
            {
                var d = Defs.Get(gc.BuildType);
                var a = gc.PlacementAnchor;
                string err = T.CanPlace(gc.BuildType, a.x, a.y);
                float cc = T.ClearCost(gc.BuildType, a.x, a.y);
                if (err != null) { s = err; c = Ui.Bad; }
                else s = d.Name + (d.Type == BuildingType.Road ? " · $1" : " · " + Town.MaterialsText(d)) + (cc > 0 ? "\nClearing the ground: " + Town.Money(cc) : "");
            }
            if (s == null) return;
            var m = Event.current.mousePosition;
            var lines = s.Split('\n');
            float tw = lines.Max(l => PixelFont.Width(l)) + 12, ht = lines.Length * PixelFont.LineH + 7;
            var r = new Rect(Mathf.Min(m.x + 12, W - tw - 4), Mathf.Min(m.y + 14, H - ht - 4), tw, ht);
            Ui.Tip(r);
            float y = r.y + 5;
            foreach (var l in lines) { Ui.Text(r.x + 6, y, l, c); y += PixelFont.LineH; c = Ui.InkSoft; }
        }
    }
}
