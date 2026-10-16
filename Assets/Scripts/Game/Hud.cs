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
            int idleFood = T.Businesses.Count(b => Town.Produces(b.Type, Good.Food) && T.WorkersAt(b) == 0);
            if (idleFood > 0) lines.Add(new KeyValuePair<string, bool>(idleFood == 1 ? "A food business has nobody working it." : idleFood + " food businesses have nobody working them.", false));
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
            string[] names = { "Ledger: taxes, wages, the town books", "Citizens", "Trade: food, firewood and businesses", "Election and mayor", "Gazette: what's been happening",
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
            if (d.IsBusiness) { Ui.Text(r.x + 7, by, "Up to " + d.MaxEmployees + " workers · wage " + Town.Money(d.BaseWage), Ui.InkSoft); by += 11; }
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
            var view = new Rect(r.x + 8, r.y + 22, r.width - 16, r.height - 30);
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

        void LedgerTab()
        {
            Row("Treasury", Town.Money(T.Treasury), T.Treasury < 0 ? Ui.Bad : Ui.Title);
            Sparkline(new Rect(bx, by, bw, 26)); by += 30;
            var P = T.Policy;
            Heading("Taxes");
            P.IncomeTax = SliderRow("Income tax", Mathf.RoundToInt(P.IncomeTax * 100) + "%", P.IncomeTax * 100, 0, 40, 1, "Taken from every wage. Over 12% people grumble.") / 100f;
            P.HeadTax = SliderRow("Head tax (weekly)", Town.Money(P.HeadTax), P.HeadTax, 0, 3, 0.25f, "Every adult pays on Sunday. Over $1 hurts morale.");
            P.BusinessTax = SliderRow("Business tax", Mathf.RoundToInt(P.BusinessTax * 100) + "%", P.BusinessTax * 100, 0, 50, 1, "Share of each business's weekly profit.") / 100f;
            Heading("Wages & town payroll");
            float mw = SliderRow("Minimum wage", Town.Money(P.MinWage), P.MinWage, 1, 5, 0.25f, null);
            if (!Mathf.Approximately(mw, P.MinWage)) T.SetMinWage(mw);
            P.ContractRate = SliderRow("Contract rate", Town.Money(P.ContractRate) + "/day", P.ContractRate, 2, 6, 0.25f, "What the town pays a builder's yard for each day of work a job takes. Half when work starts, half when it's done. Too low and yards lay off their crews.");
            if (!T.Yards.Any())
            {
                P.Builders = Mathf.RoundToInt(SliderRow("Day laborers for building", P.Builders.ToString(), P.Builders, 0, 12, 1, "With no builder's yard, the town hires settlers without a job by the day to build."));
                P.BuilderWage = SliderRow("Day wage", Town.Money(P.BuilderWage), P.BuilderWage, 1, 5, 0.25f, null);
            }
            P.ReliefWage = SliderRow("Laborer relief", Town.Money(P.ReliefWage), P.ReliefWage, 0, 2, 0.1f, "Daily pay for adults without a job on days there's no work for them.");
            P.Deputies = Mathf.RoundToInt(SliderRow("Deputies", P.Deputies.ToString(), P.Deputies, 0, 3, 1, "Sheriff $4/day, deputies $3, from the treasury."));
            Heading("Housing");
            P.Rent = SliderRow("Cottage rent (weekly)", Town.Money(P.Rent), P.Rent, 0, 6, 0.25f, "Every family in a cottage pays on Sunday. Four weeks behind and they're put out. Over $3 hurts morale. Families living on charity pay nothing.");
            P.BoardRent = SliderRow("Boarding rent (weekly)", Town.Money(P.BoardRent), P.BoardRent, 0, 3, 0.25f, "Each adult in a boarding house pays this. Over $1.50 hurts morale.");
            Heading("Stores");
            P.Charity = Ui.Toggle(new Rect(bx, by, bw, 11), P.Charity, "Storehouse feeds those who can't pay"); by += 13;
            P.StorehouseSells = Ui.Toggle(new Rect(bx, by, bw, 11), P.StorehouseSells, "Storehouse sells when shops run out"); by += 13;
            P.Reserve[0] = SliderRow("Keep food in store", Mathf.RoundToInt(P.Reserve[0]).ToString(), P.Reserve[0], 0, 400, 10, "The town buys food for the winter up to this much.");
            P.Reserve[1] = SliderRow("Keep firewood", Mathf.RoundToInt(P.Reserve[1]).ToString(), P.Reserve[1], 0, 200, 10, null);
            P.Reserve[2] = SliderRow("Keep logs", Mathf.RoundToInt(P.Reserve[2]).ToString(), P.Reserve[2], 0, 150, 5, null);
            P.Reserve[3] = SliderRow("Keep stone", Mathf.RoundToInt(P.Reserve[3]).ToString(), P.Reserve[3], 0, 150, 5, null);
            Heading("Mayor");
            bool on = Ui.Toggle(new Rect(bx, by, bw, 11), P.ElectionsOn, "Elect a mayor every 4 weeks"); by += 13;
            if (on != P.ElectionsOn) T.SetElections(on);

            Heading("Town books");
            var C = T.LedgerThisWeek; var L = T.HasLastWeek ? T.LedgerLastWeek : null;
            Ui.Text(bx + bw - 54, by, "THIS WK", Ui.InkSoft, Ui.Align.Right); Ui.Text(bx + bw, by, "LAST WK", Ui.InkSoft, Ui.Align.Right); by += 11;
            Book("Income tax", C.IncomeTax, L != null ? L.IncomeTax : (float?)null, 1);
            Book("Head tax", C.HeadTax, L != null ? L.HeadTax : (float?)null, 1);
            Book("Business tax", C.BusinessTax, L != null ? L.BusinessTax : (float?)null, 1);
            Book("Storehouse sales", C.StoreSales, L != null ? L.StoreSales : (float?)null, 1);
            Book("Rent", C.Rent, L != null ? L.Rent : (float?)null, 1);
            Book("Deeds & payments", C.Deeds + C.Claims, L != null ? L.Deeds + L.Claims : (float?)null, 1);
            Book("Building contracts", C.Contracts, L != null ? L.Contracts : (float?)null, -1);
            Book("Town-run businesses", C.TownTakings - C.TownTills, L != null ? L.TownTakings - L.TownTills : (float?)null, 0);
            if (C.Builders > 0 || (L != null && L.Builders > 0)) Book("Day labor", C.Builders, L != null ? L.Builders : (float?)null, -1);
            Book("Relief", C.Relief, L != null ? L.Relief : (float?)null, -1);
            Book("Salaries", C.Salaries, L != null ? L.Salaries : (float?)null, -1);
            Book("Materials & food", C.Materials, L != null ? L.Materials : (float?)null, -1);
            Book("Clearing, roads", C.Construction, L != null ? L.Construction : (float?)null, -1);
            if (C.Imports > 0 || (L != null && L.Imports > 0)) Book("Freight orders", C.Imports - C.Refunds, L != null ? L.Imports - L.Refunds : (float?)null, -1);
            Book("Grants & upkeep", C.Grants + C.Upkeep, L != null ? L.Grants + L.Upkeep : (float?)null, -1);
            Ui.Line(bx, by, bw, Ui.Hex("#c8a070")); by += 2;
            Book("Net", C.Net, L != null ? L.Net : (float?)null, 0);
            if (C.CharityGoods > 0.5f) Para("Handed out free this week: goods worth " + Town.Money(C.CharityGoods) + ".");
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
            Good[] goods = { Good.Food, Good.Firewood, Good.Logs, Good.Stone, Good.Tools };
            float bulk = 0; foreach (var g in goods) bulk += cart[(int)g];
            foreach (var g in goods)
            {
                int step = g == Good.Tools ? 5 : 10;
                int max = Mathf.FloorToInt(cart[(int)g] + Mathf.Max(0, room - bulk));
                cart[(int)g] = OrderLine(GoodIcon(g), Defs.GoodNames[(int)g], Town.Money(Balance.ImportPrice[(int)g]) + " each", Mathf.RoundToInt(cart[(int)g]), step, max);
            }
            Row("Room in the stores", Mathf.FloorToInt(room) + (T.GoodsOnTheRoad > 0.5f ? " (after what's on the road)" : ""), room < 10 ? Ui.Warn : Ui.Ink);
            if (!T.Storages.Any()) Para("Build a storehouse to take goods.", Ui.Warn);
            Heading("Settlers");
            cartLaborers = OrderLine(Icon.People, "Laborer", Town.Money(Balance.PassageLaborer), cartLaborers, 1, 10);
            cartFamilies = OrderLine(Icon.Child, "Family", Town.Money(Balance.PassageFamily), cartFamilies, 1, 5);
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

        static readonly Role[] RoleOrder = { Role.Mayor, Role.Owner, Role.Worker, Role.Sheriff, Role.Deputy, Role.Teacher, Role.Doctor, Role.Laborer, Role.Child };

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
            for (int g = 0; g < 5; g++) Row(Defs.GoodNames[g], Mathf.FloorToInt(T.TownStock((Good)g)).ToString());
            Heading("Prices");
            Row("Producers get (food, wood)", Town.Money(Balance.Wholesale[0]) + ", " + Town.Money(Balance.Wholesale[1]));
            Row("Shops charge (food)", T.AverageFoodPrice > 0 ? Town.Money(T.AverageFoodPrice) : "-");
            Row("Storehouse charges", Town.Money(T.Policy.StorehousePrice[0]) + ", " + Town.Money(T.Policy.StorehousePrice[1]));
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
            if (p.Sick > 0)
                Row(Town.LaidUp(p) ? "Laid up with" : "Ill with", Defs.Illnesses[p.IllnessKind] + (p.SeenToday ? ", seen by the doctor" : ""),
                    p.Illness >= 10f ? Ui.Bad : p.Illness >= 3f ? Ui.Warn : Ui.InkSoft);
            var home = T.GetBuilding(p.HomeId); var work = T.GetBuilding(p.WorkId);
            Row("Home", home != null ? (home.Type == BuildingType.Estate ? "Mayor's Estate" : home.Type == BuildingType.BoardingHouse ? "Boarding House" : "Cottage #" + home.Id) : "tent by the wagon", home == null ? Ui.Bad : Ui.Ink);
            if (p.RentOwed > 0.01f) Row("Behind on rent", Town.Money(p.RentOwed), Ui.Warn);
            Row("Work", work != null ? work.Def.Name + (p.FamilyHand ? " (family)" : work.OwnerId < 0 && work.Def.IsBusiness ? " (town)" : "") : p.IsChild ? "too young" : p.Role == Role.Laborer ? "laborer" : "none");
            if (p.IsChild && p.Age >= Balance.SchoolAge)
            {
                float years = p.Schooling * Balance.SchoolYearsFull;
                Row("Schooling", years < 0.05f ? "none yet" : years.ToString("0.0") + " years" + (Town.Schooled(p) ? ", reads and reckons" : ""), Town.Schooled(p) ? Ui.Good : Ui.Ink);
                Row("Today", p.Chore == Chore.School ? "at school" : Town.LaidUp(p) ? "in bed" : p.Age >= 6 ? "helping at home" : "playing");
            }
            else if (!p.IsChild) Row("Schooling", Town.Schooled(p) ? "reads, writes, reckons" : "little or none", Town.Schooled(p) ? Ui.Ink : Ui.Warn);
            if (!p.IsChild && (p.Chore != Chore.None || p.Role == Role.Laborer))
            {
                Row("Today", ChoreText(p));
                var task = T.GetBuilding(p.TaskId);
                if (task != null && task != work) Row("At", task.Def.Name);
            }
            Row("Savings", Town.Money(p.Money));
            foreach (var e in T.Errands.Where(x => x.PersonId == p.Id))
            {
                var to = T.GetBuilding(e.ToId); if (to == null) continue;
                Para(e.Delivery ? "Today: taking " + Defs.GoodNames[(int)e.Good].ToLower() + " to sell at the " + to.Def.Name + "."
                                : "Today: buying " + Defs.GoodNames[(int)e.Good].ToLower() + " at the " + to.Def.Name + ".", Ui.Ink);
            }
            if (p.CharityDays > 0) Row("Living on", "town charity", Ui.Warn);
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

        static Icon GoodIcon(Good g) { return g == Good.Furniture ? Icon.Chair : (Icon)(int)g; }

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
                Para(working ? "Being repaired by " + who + "." : b.RepairPaid ? "Repair booked with " + who + "." : "Needs repair: " + Town.Money(T.RepairPrice(b)) + (b.Def.IsBusiness && b.OwnerId >= 0 ? ", paid by the owner." : ", paid by the town."), working ? Ui.Good : Ui.Warn);
            }
            else if (b.Condition < Balance.RepairAt) Para(T.Yards.Any() ? "Worn. A builder's yard will be sent." : "Worn, and there's no builder's yard to repair it.", Ui.Warn);
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
                else if (b.Type == BuildingType.Bank) Para("No banker. The town doesn't lend.", Ui.Warn);
                else
                {
                    float price = T.DeedPrice(b);
                    Para("Run by the town until a family buys it, for " + Town.Money(price) + (T.Banks.Any() ? " (or " + Town.Money(price * Balance.MinDown) + " down with a bank loan)." : " in full."), Ui.Warn);
                }
                Row("Cash", Town.Money(b.Cash));
                Row("Wage", Town.Money(b.Wage) + "/day");
                Row("Staff", T.WorkersAt(b) + " working · " + b.Employees.Count + "/" + d.MaxEmployees + " hired");
                if (b.Debt > 0.5f)
                {
                    var lender = T.GetBuilding(b.LenderId);
                    Row(lender != null ? "Owes the bank" : "Owes the town", Town.Money(b.Debt), b.MissedPayments > 0 ? Ui.Bad : Ui.Warn);
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
                    case BuildingType.Smithy: Stocks(b, Good.Logs, Good.Stone, Good.Tools); break;
                    case BuildingType.Bank:
                    {
                        var loans = T.Buildings.Where(x => x.LenderId == b.Id && x.Debt > 0.01f).ToList();
                        Row("To lend", Town.Money(Mathf.Max(0, b.Cash - 20)));
                        Row("Loans out", loans.Count + (loans.Count > 0 ? ", " + Town.Money(loans.Sum(x => x.Debt)) + " owed" : ""));
                        Row("Interest", Mathf.RoundToInt(Balance.LoanInterest * 1000) / 10f + "% a week");
                        Para("Lends a family the rest of a business's price once they have a quarter of it.", Ui.InkSoft);
                        break;
                    }
                    case BuildingType.BuilderYard:
                    {
                        int sites = T.Sites.Count(s => s.ContractorId == b.Id), fixes = T.Buildings.Count(x => x.Built && x.RepairBy == b.Id);
                        Row("Jobs", sites + (sites == 1 ? " building, " : " buildings, ") + fixes + (fixes == 1 ? " repair" : " repairs"));
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
                Row("Made today", b.Today.Made.ToString("0.0"));
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
                if (b.Type == BuildingType.House) Row("Garden today", b.GardenToday.ToString("0.0") + " food");
                Stocks(b, Good.Food, Good.Firewood, Good.Furniture);
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
                        if (b.Type == BuildingType.House || b.Type == BuildingType.BoardingHouse) s += "\n" + b.Residents.Count + " living here";
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
