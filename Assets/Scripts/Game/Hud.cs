using System.Collections.Generic;
using System.Linq;
using FrontierLedger.Game.Art;
using FrontierLedger.Simulation;
using UnityEngine;

namespace FrontierLedger.Game
{
    /// <summary>
    /// First-pass interface drawn with IMGUI: top bar, build palette, minimap, ledger book with tabs,
    /// inspector, tooltip and toasts. Placeholder until we move to UI Toolkit; all numbers are live.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        // Lamplit walnut desk with brass fittings, same palette as the prototype
        static readonly Color Desk = Hex("#241a13"), Panel = Hex("#33261c"), Panel2 = Hex("#3f2f23"), Line = Hex("#5a4532"),
                              Ink = Hex("#f0e5d0"), Muted = Hex("#b49f82"), Brass = Hex("#d3a24a");
        const string GoodC = "#9cc46e", WarnC = "#e2b04a", BadC = "#de6f55", MutedC = "#b49f82", BrassC = "#d3a24a";

        static Color Hex(string h) { Color c; ColorUtility.TryParseHtmlString(h, out c); return c; }

        GameController gc;
        Town T { get { return gc.Town; } }

        float scale = 1f;
        readonly List<Rect> blockers = new List<Rect>();
        string toast; float toastUntil;
        float confirmNewUntil;
        int confirmTearDown = -1;
        int tab;
        Vector2 scrollTab, scrollInspector;
        static readonly string[] Tabs = { "Ledger", "Citizens", "Trade", "Election", "Gazette" };

        Texture2D minimap; int miniVersion = -1, miniSeason = -1; float miniTimer;
        Color32[] miniBase;

        GUIStyle sPanel, sPanel2, sBar, sTitle, sSub, sLabel, sLabelR, sSmall, sNum, sNumR, sBtn, sBtnOn, sTool, sToolOn, sTab, sTabOn,
                 sRow, sHead, sToast, sTip, sChip, sHeading, sMono, sToggle;
        bool stylesReady;

        public bool TextFieldFocused { get { return GUIUtility.keyboardControl != 0; } }

        void Awake() { gc = GetComponent<GameController>(); }

        // IMGUI runs several passes per frame; changing what is on screen mid-pass breaks the layout,
        // so clicks that change selection, tabs or buildings are queued and applied in Update.
        readonly List<System.Action> deferred = new List<System.Action>();
        void Later(System.Action a) { deferred.Add(a); }
        void Update()
        {
            if (deferred.Count == 0) return;
            var run = deferred.ToArray(); deferred.Clear();
            foreach (var a in run) a();
        }

        public void Toast(string msg) { toast = msg; toastUntil = Time.unscaledTime + 2.4f; }
        public void ResetConfirm() { confirmTearDown = -1; }

        /// <summary>True when the mouse (Unity screen coords, y up) is over a panel.</summary>
        public bool Blocks(Vector2 mouse)
        {
            var p = new Vector2(mouse.x / scale, (Screen.height - mouse.y) / scale);
            foreach (var r in blockers) if (r.Contains(p)) return true;
            return false;
        }

        // ================================================================== styles
        static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1); t.SetPixel(0, 0, c); t.Apply(); t.hideFlags = HideFlags.HideAndDontSave; return t;
        }
        static Texture2D Framed(Color fill, Color border, int bw = 1)
        {
            var t = new Texture2D(8, 8); t.filterMode = FilterMode.Point; t.hideFlags = HideFlags.HideAndDontSave;
            for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++) t.SetPixel(x, y, (x < bw || y < bw || x >= 8 - bw || y >= 8 - bw) ? border : fill);
            t.Apply(); return t;
        }
        GUIStyle Style(GUIStyle baseStyle, int size, Color color, FontStyle fs = FontStyle.Normal, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            var s = new GUIStyle(baseStyle) { fontSize = size, fontStyle = fs, alignment = anchor, richText = true, wordWrap = false };
            s.normal.textColor = s.hover.textColor = s.active.textColor = s.focused.textColor = color;
            return s;
        }

        void BuildStyles()
        {
            var skin = GUI.skin;
            sPanel = new GUIStyle { normal = { background = Framed(Panel, Line) }, border = new RectOffset(2, 2, 2, 2), padding = new RectOffset(10, 10, 8, 8) };
            sPanel2 = new GUIStyle(sPanel) { normal = { background = Framed(Panel2, Line) } };
            sBar = new GUIStyle { normal = { background = Framed(Desk, Line) }, border = new RectOffset(2, 2, 2, 2) };
            sTitle = Style(skin.label, 22, Brass, FontStyle.Bold);
            sHeading = Style(skin.label, 16, Brass, FontStyle.Bold); sHeading.wordWrap = true;
            sSub = Style(skin.label, 10, Muted, FontStyle.Bold);
            sLabel = Style(skin.label, 13, Ink); sLabel.wordWrap = true;
            sLabelR = Style(skin.label, 13, Ink, FontStyle.Normal, TextAnchor.MiddleRight);
            sSmall = Style(skin.label, 11, Muted); sSmall.wordWrap = true;
            sNum = Style(skin.label, 14, Ink, FontStyle.Bold);
            sNumR = Style(skin.label, 13, Ink, FontStyle.Normal, TextAnchor.MiddleRight);
            sMono = Style(skin.label, 12, Ink);
            sBtn = Style(skin.button, 13, Ink, FontStyle.Normal, TextAnchor.MiddleCenter);
            sBtn.normal.background = Framed(Panel, Line); sBtn.hover.background = Framed(Panel2, Line); sBtn.active.background = Framed(Panel2, Brass);
            sBtn.border = new RectOffset(2, 2, 2, 2); sBtn.padding = new RectOffset(8, 8, 4, 4);
            sBtnOn = new GUIStyle(sBtn); sBtnOn.normal.background = sBtnOn.hover.background = Solid(Brass);
            sBtnOn.normal.textColor = sBtnOn.hover.textColor = Desk;
            sTool = new GUIStyle(sBtn) { alignment = TextAnchor.MiddleLeft, padding = new RectOffset(26, 8, 5, 5) };
            sToolOn = new GUIStyle(sTool); sToolOn.normal.background = sToolOn.hover.background = Framed(Panel2, Brass, 2);
            sTab = Style(skin.button, 12, Muted, FontStyle.Normal, TextAnchor.MiddleCenter);
            sTab.normal.background = sTab.hover.background = sTab.active.background = Solid(Panel);
            sTabOn = Style(skin.button, 12, Ink, FontStyle.Bold, TextAnchor.MiddleCenter);
            sTabOn.normal.background = sTabOn.hover.background = sTabOn.active.background = Framed(Panel2, Brass, 1);
            sRow = Style(skin.label, 12, Ink); sRow.hover.background = Solid(new Color(Brass.r, Brass.g, Brass.b, 0.12f)); sRow.padding = new RectOffset(2, 2, 2, 2);
            sHead = Style(skin.label, 10, Muted, FontStyle.Bold);
            sToast = Style(skin.box, 13, Desk, FontStyle.Bold, TextAnchor.MiddleCenter); sToast.normal.background = Solid(Hex(BadC)); sToast.padding = new RectOffset(12, 12, 6, 6);
            sTip = Style(skin.box, 12, Ink, FontStyle.Normal, TextAnchor.UpperLeft); sTip.normal.background = Framed(new Color(0.11f, 0.08f, 0.055f, 0.95f), Hex("#8d6a2e"));
            sTip.border = new RectOffset(2, 2, 2, 2); sTip.padding = new RectOffset(8, 8, 5, 5); sTip.wordWrap = true;
            sChip = Style(skin.box, 11, Ink, FontStyle.Normal, TextAnchor.MiddleCenter); sChip.normal.background = Framed(Panel, Line); sChip.padding = new RectOffset(7, 7, 2, 2);
            sToggle = new GUIStyle(skin.toggle) { fontSize = 13 };
            sToggle.normal.textColor = sToggle.onNormal.textColor = sToggle.hover.textColor = sToggle.onHover.textColor = Ink;
            stylesReady = true;
        }

        static string Col(string text, string hex) { return "<color=" + hex + ">" + text + "</color>"; }
        static string Money(float v) { return Town.Money(v); }
        static string MoodColor(float h) { return h < 35 ? BadC : h < 55 ? WarnC : GoodC; }
        static string FedLabel(Citizen p) { return p.Fed >= 0.95f ? Col("Fed", GoodC) : p.Fed >= 0.7f ? Col("Peckish", WarnC) : Col("Hungry", BadC); }

        // ================================================================== main
        void OnGUI()
        {
            if (gc == null || gc.Town == null) return;
            if (!stylesReady) BuildStyles();
            scale = Mathf.Clamp(Screen.height / 900f, 1f, 3f);
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));
            float W = Screen.width / scale, H = Screen.height / scale;
            if (Event.current.type == EventType.Layout) blockers.Clear();

            float rightW = Mathf.Min(372, W * 0.34f);
            var top = new Rect(0, 0, W, 54);
            var palette = new Rect(8, 62, 182, 12 + Defs.Buildable.Length * 30 + 66);
            var mini = new Rect(8, H - 8 - 150, 196, 150);
            var right = new Rect(W - rightW - 8, 62, rightW, H - 70);
            AddBlocker(top); AddBlocker(palette); AddBlocker(mini); AddBlocker(right);

            DrawTopBar(top);
            DrawPalette(palette);
            DrawMinimap(mini);
            DrawRight(right);
            DrawHint(new Rect(palette.xMax + 12, H - 28, right.xMin - palette.xMax - 24, 22));
            DrawTooltip();
            if (toast != null && Time.unscaledTime < toastUntil)
            {
                var size = sToast.CalcSize(new GUIContent(toast));
                GUI.Label(new Rect((W - size.x) / 2, 64, size.x, size.y + 4), toast, sToast);
            }
        }

        void AddBlocker(Rect r) { if (Event.current.type == EventType.Layout) blockers.Add(r); }

        // ---------------------------------------------------------------- top bar
        void DrawTopBar(Rect r)
        {
            GUI.Box(r, GUIContent.none, sBar);
            GUI.Label(new Rect(14, 6, 220, 26), "Frontier Ledger", sTitle);
            GUI.Label(new Rect(15, 32, 220, 14), "CEDAR FORK SETTLEMENT", sSub);
            float x = 236;
            var mayor = T.Mayor;
            float mood = T.AverageHappiness, price = T.AverageFoodPrice;
            bool shortFood = T.Market.Shortfall > 0.5f;
            int employed = T.People.Count(p => p.Role != Role.Unemployed);
            Stat(ref x, "DATE", T.DateLabel, 180);
            Stat(ref x, "TREASURY", T.Treasury < 0 ? Col(Money(T.Treasury), BadC) : Money(T.Treasury), 96);
            Stat(ref x, "SETTLERS", T.People.Count + " · " + T.FreeBeds + " beds free", 118);
            Stat(ref x, "EMPLOYED", employed + " / " + T.People.Count, 74);
            Stat(ref x, "FOOD PRICE", price > 0 ? (shortFood ? Col(Money(price) + " · short", WarnC) : Money(price)) : Col("No shop", WarnC), 96);
            Stat(ref x, "MOOD", Col(Mathf.RoundToInt(mood) + "%", MoodColor(mood)), 54);
            Stat(ref x, "MAYOR", mayor != null ? mayor.Name : T.Estate != null ? (T.Campaign.Active ? "Campaigning" : "Vacant") : "No estate", 150);

            float bx = r.width - 8 - 4 * 44;
            int[] speeds = { 0, 1, 2, 4 };
            string[] labels = { "II", "1×", "2×", "4×" };
            for (int i = 0; i < 4; i++)
            {
                if (GUI.Button(new Rect(bx + i * 44, 13, 40, 28), labels[i], gc.Speed == speeds[i] ? sBtnOn : sBtn)) gc.SetSpeed(speeds[i]);
            }
        }

        void Stat(ref float x, string label, string value, float w)
        {
            GUI.Label(new Rect(x, 8, w, 14), label, sSub);
            GUI.Label(new Rect(x, 22, w + 20, 22), value, sNum);
            x += w + 14;
        }

        // ---------------------------------------------------------------- palette
        void DrawPalette(Rect r)
        {
            GUI.Box(r, GUIContent.none, sPanel);
            float y = r.y + 8;
            GUI.Label(new Rect(r.x + 10, y, 120, 16), "BUILD", sSub); y += 18;
            if (ToolButton(new Rect(r.x + 8, y, r.width - 16, 26), "Inspect", "Q", "#d3a24a", gc.Tool == ToolKind.Inspect, true)) gc.SetTool(ToolKind.Inspect);
            y += 30;
            for (int i = 0; i < Defs.Buildable.Length; i++)
            {
                var t = Defs.Buildable[i]; var d = Defs.Get(t);
                bool on = gc.Tool == ToolKind.Build && gc.BuildType == t;
                bool can = T.Treasury >= d.Cost && !(d.Unique && T.OfType(t).Any());
                string name = t == BuildingType.House ? "House" : d.Name;
                if (ToolButton(new Rect(r.x + 8, y, r.width - 16, 26), name, "$" + d.Cost, d.Color, on, can)) gc.SetTool(ToolKind.Build, t);
                y += 30;
            }
            if (ToolButton(new Rect(r.x + 8, y, r.width - 16, 26), "Clear & demolish", "X", "#de6f55", gc.Tool == ToolKind.Clear, true)) gc.SetTool(ToolKind.Clear);
        }

        bool ToolButton(Rect r, string name, string right, string swatch, bool on, bool enabled)
        {
            bool prev = GUI.enabled; GUI.enabled = enabled;
            bool clicked = GUI.Button(r, name, on ? sToolOn : sTool);
            GUI.enabled = prev;
            var old = GUI.color;
            GUI.color = enabled ? Hex(swatch) : new Color(0.5f, 0.45f, 0.4f, 0.6f);
            GUI.DrawTexture(new Rect(r.x + 9, r.y + 8, 10, 10), Texture2D.whiteTexture);
            GUI.color = old;
            GUI.Label(new Rect(r.xMax - 60, r.y, 54, r.height), Col(right, MutedC), sLabelR);
            return clicked;
        }

        // ---------------------------------------------------------------- minimap
        void RebuildMinimap()
        {
            int w = Town.Cols * 2, h = Town.Rows * 2, si = T.Season;
            if (minimap == null) { minimap = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave }; }
            if (miniBase == null) miniBase = new Color32[w * h];
            var tileCol = new Color32[5];
            tileCol[0] = PixelCanvas.C(GroundPainter.GrassBase(si));
            tileCol[1] = PixelCanvas.C(si == 3 ? "#bcd8e6" : "#3d7896");
            tileCol[2] = PixelCanvas.C(si == 3 ? "#6f8a78" : "#2f5d3a");
            tileCol[3] = PixelCanvas.C("#8d877d");
            tileCol[4] = PixelCanvas.C(si == 3 ? "#d8e2e8" : "#a0915f");
            for (int ty = 0; ty < Town.Rows; ty++)
                for (int tx = 0; tx < Town.Cols; tx++)
                {
                    var b = T.OccupantAt(tx, ty);
                    var c = b != null ? PixelCanvas.C(b.Type == BuildingType.Road ? "#c49c69" : b.Def.Color) : tileCol[Mathf.Min(4, (int)T.TerrainAt(tx, ty))];
                    for (int dy = 0; dy < 2; dy++) for (int dx = 0; dx < 2; dx++) miniBase[(h - 1 - (ty * 2 + dy)) * w + tx * 2 + dx] = c;
                }
            miniVersion = T.MapVersion; miniSeason = si;
        }

        void DrawMinimap(Rect r)
        {
            GUI.Box(r, GUIContent.none, sPanel);
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
                    int x = Mathf.Clamp((int)(pos.x / 8), 0, w - 1), y = Mathf.Clamp((int)(pos.y / 8), 0, h - 1);
                    px[(h - 1 - y) * w + x] = new Color32(255, 255, 255, 255);
                }
                minimap.SetPixels32(px); minimap.Apply(false);
            }
            var inner = new Rect(r.x + 6, r.y + 6, r.width - 12, r.height - 12);
            float sx = inner.width / (Town.Cols * 16f), sy = inner.height / (Town.Rows * 16f);
            GUI.DrawTexture(inner, minimap, ScaleMode.StretchToFill);
            var o = gc.Rig.ViewOrigin; var v = gc.Rig.ViewSize;
            var vr = Rect.MinMaxRect(Mathf.Max(inner.x, inner.x + o.x * sx), Mathf.Max(inner.y, inner.y + o.y * sy),
                                     Mathf.Min(inner.xMax, inner.x + (o.x + v.x) * sx), Mathf.Min(inner.yMax, inner.y + (o.y + v.y) * sy));
            Outline(vr, Hex("#ffd77a"));
            var e = Event.current;
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0 && inner.Contains(e.mousePosition))
            {
                gc.Rig.CenterOn(new Vector2((e.mousePosition.x - inner.x) / sx, (e.mousePosition.y - inner.y) / sy));
                e.Use();
            }
        }

        static void Outline(Rect r, Color c)
        {
            var old = GUI.color; GUI.color = c;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 1), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.yMax - 1, r.width, 1), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, 1, r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - 1, r.y, 1, r.height), Texture2D.whiteTexture);
            GUI.color = old;
        }

        // ---------------------------------------------------------------- hint & tooltip
        void DrawHint(Rect r)
        {
            string hint;
            if (gc.Tool == ToolKind.Inspect) hint = "Drag to pan · scroll to zoom · click a building or settler · Space pauses · H recenters";
            else if (gc.Tool == ToolKind.Clear) hint = "Click or drag over trees, rocks and buildings. Right-drag pans. Esc to stop.";
            else if (gc.BuildType == BuildingType.Road) hint = "Click and drag to lay road. Roads can bridge water. Right-drag pans.";
            else hint = "Placing " + Defs.Get(gc.BuildType).Name + ". Trees and rocks are cleared for a fee. Esc to stop.";
            var old = GUI.color; GUI.color = new Color(0, 0, 0, 0.45f);
            GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = old;
            GUI.Label(new Rect(r.x + 8, r.y, r.width - 16, r.height), hint, sSmall);
        }

        void DrawTooltip()
        {
            if (!gc.PointerOnMap || Input.GetMouseButton(1) || Input.GetMouseButton(2)) return;
            string s = null; var h = gc.HoverTile;
            if (gc.Tool == ToolKind.Inspect)
            {
                var pick = gc.View.PickAt(gc.HoverPixel);
                if (pick.Kind == SelectionKind.Person)
                {
                    var p = T.GetCitizen(pick.Id);
                    var w = T.GetBuilding(p.WorkId);
                    s = "<b>" + p.Name + "</b>\n" + Defs.RoleName(p.Role) + (w != null && p.Role != Role.Mayor ? " · " + w.Def.Name : "");
                }
                else if (pick.Kind == SelectionKind.Building)
                {
                    var b = T.GetBuilding(pick.Id); var d = b.Def;
                    s = "<b>" + d.Name + "</b>";
                    if (d.IsBusiness) { var o = T.GetCitizen(b.OwnerId); s += "\n" + (o != null ? "Owner: " + o.Name : "For sale") + " · " + T.WorkersAt(b) + " at work"; }
                    if (b.Type == BuildingType.House || b.Type == BuildingType.Estate)
                    {
                        var names = b.Residents.Select(T.GetCitizen).Where(x => x != null).Select(x => x.Name).ToArray();
                        s += "\n" + (names.Length > 0 ? string.Join(", ", names) : "Empty");
                    }
                    if (b.Type == BuildingType.Sheriff)
                    {
                        var names = b.Employees.Select(T.GetCitizen).Where(x => x != null).Select(x => x.Name).ToArray();
                        s += "\n" + (names.Length > 0 ? string.Join(", ", names) : "No lawmen hired");
                    }
                }
                else
                {
                    byte t = T.TerrainAt(h.x, h.y);
                    if (t == Tile.Hill) s = "Hills · too steep to build";
                    else if (t == Tile.Tree) s = "Woodland · $3 to clear";
                    else if (t == Tile.Rock) s = "Boulder · $10 to clear";
                    else if (t == Tile.Water && T.OccupantAt(h.x, h.y) == null) s = "Water · roads can bridge it";
                }
            }
            else if (gc.Tool == ToolKind.Clear)
            {
                var b = T.OccupantAt(h.x, h.y); byte t = T.TerrainAt(h.x, h.y);
                if (b != null) s = "Tear down " + b.Def.Name + " · " + Money(b.Def.Cost * 0.25f) + " back";
                else if (t == Tile.Tree) s = "Clear tree · $3";
                else if (t == Tile.Rock) s = "Break up boulder · $10";
            }
            else
            {
                var d = Defs.Get(gc.BuildType);
                int ax = gc.BuildType == BuildingType.Road ? h.x : h.x - (d.W - 1) / 2, ay = gc.BuildType == BuildingType.Road ? h.y : h.y - (d.H - 1) / 2;
                string err = T.CanPlace(gc.BuildType, ax, ay); float cc = T.ClearCost(gc.BuildType, ax, ay);
                s = err != null ? Col(err, BadC) : "<b>" + d.Name + "</b> · " + Money(d.Cost) + (cc > 0 ? " + " + Money(cc) + " clearing" : "");
            }
            if (s == null) return;
            var m = Event.current.mousePosition;
            var content = new GUIContent(s);
            float width = Mathf.Min(240, sTip.CalcSize(content).x + 4);
            float height = sTip.CalcHeight(content, width);
            float W = Screen.width / scale, H = Screen.height / scale;
            GUI.Label(new Rect(Mathf.Min(m.x + 16, W - width - 8), Mathf.Min(m.y + 18, H - height - 8), width, height), s, sTip);
        }

        // ---------------------------------------------------------------- right panel
        void DrawRight(Rect r)
        {
            GUI.Box(r, GUIContent.none, sPanel);
            GUILayout.BeginArea(new Rect(r.x + 1, r.y + 1, r.width - 2, r.height - 2));
            if (gc.Selected.Kind != SelectionKind.None)
            {
                GUILayout.BeginVertical(sPanel2, GUILayout.MaxHeight(r.height * 0.5f));
                scrollInspector = GUILayout.BeginScrollView(scrollInspector, GUIStyle.none, GUI.skin.verticalScrollbar);
                DrawInspector();
                GUILayout.EndScrollView();
                GUILayout.EndVertical();
            }
            GUILayout.BeginHorizontal();
            for (int i = 0; i < Tabs.Length; i++)
                if (GUILayout.Button(Tabs[i], tab == i ? sTabOn : sTab, GUILayout.Height(30))) { int t = i; Later(() => { tab = t; scrollTab = Vector2.zero; }); }
            GUILayout.EndHorizontal();
            scrollTab = GUILayout.BeginScrollView(scrollTab, GUIStyle.none, GUI.skin.verticalScrollbar);
            GUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(12, 12, 8, 12) });
            switch (tab)
            {
                case 0: DrawLedger(); break;
                case 1: DrawCitizens(); break;
                case 2: DrawTrade(); break;
                case 3: DrawElection(); break;
                default: DrawGazette(); break;
            }
            GUILayout.EndVertical();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void Heading(string s) { GUILayout.Space(8); GUILayout.Label(s.ToUpper(), sSub); }
        void KV(string k, string v)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(Col(k, MutedC), sLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label(v, sNumR);
            GUILayout.EndHorizontal();
        }

        // ---------------------------------------------------------------- ledger tab
        void DrawLedger()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("TOWN TREASURY", sSub);
            GUILayout.FlexibleSpace();
            GUILayout.Label(T.Treasury < 0 ? Col(Money(T.Treasury), BadC) : Money(T.Treasury), sNum);
            GUILayout.EndHorizontal();
            Sparkline(GUILayoutUtility.GetRect(10, 46, GUILayout.ExpandWidth(true)));

            Heading("Taxes & law");
            var P = T.Policy;
            P.IncomeTax = Slider("Income tax (wages & salaries)", Mathf.RoundToInt(P.IncomeTax * 100) + "%", P.IncomeTax * 100, 0, 40, 1,
                "Above 12% settlers grumble. Taken from every paycheck.") / 100f;
            P.HeadTax = Slider("Head tax (every settler, weekly)", Money(P.HeadTax), P.HeadTax, 0, 3, 0.25f,
                "Everyone pays on Sunday, jobs or not. Above $1.50 it hurts morale.");
            P.BusinessTax = Slider("Business tax (weekly profit)", Mathf.RoundToInt(P.BusinessTax * 100) + "%", P.BusinessTax * 100, 0, 50, 1,
                "Charged on each business's profit every Sunday.") / 100f;
            float mw = Slider("Minimum daily wage", Money(P.MinWage), P.MinWage, 1, 6, 0.5f, "Owners must pay at least this. Too high and they hire fewer hands.");
            if (!Mathf.Approximately(mw, P.MinWage)) T.SetMinWage(mw);
            P.Deputies = Mathf.RoundToInt(Slider("Deputies on the payroll", P.Deputies.ToString(), P.Deputies, 0, 3, 1,
                "Sheriff earns $4/day, deputies $3, paid from the treasury."));
            GUILayout.Space(4);
            bool on = GUILayout.Toggle(P.ElectionsOn, " Hold mayoral elections every 4 weeks", sToggle);
            if (on != P.ElectionsOn) T.SetElections(on);

            Heading("Town books");
            var C = T.LedgerThisWeek; var L = T.HasLastWeek ? T.LedgerLastWeek : null;
            BookRow("", "This week", "Last week", true);
            BookRow("Income tax", Money(C.IncomeTax), L != null ? Money(L.IncomeTax) : "—");
            BookRow("Head tax", Money(C.HeadTax), L != null ? Money(L.HeadTax) : "—");
            BookRow("Business tax", Money(C.BusinessTax), L != null ? Money(L.BusinessTax) : "—");
            BookRow("Deed sales", Money(C.Deeds), L != null ? Money(L.Deeds) : "—");
            BookRow("Land claim fees", Money(C.Claims), L != null ? Money(L.Claims) : "—");
            BookRow("Demolition refunds", Money(C.Refunds), L != null ? Money(L.Refunds) : "—");
            BookRow("Sheriff & mayor pay", Col("-" + Money(C.Salaries), MutedC), L != null ? Col("-" + Money(L.Salaries), MutedC) : "—");
            BookRow("Upkeep", Col("-" + Money(C.Upkeep), MutedC), L != null ? Col("-" + Money(L.Upkeep), MutedC) : "—");
            BookRow("Startup grants", Col("-" + Money(C.Grants), MutedC), L != null ? Col("-" + Money(L.Grants), MutedC) : "—");
            BookRow("Construction & clearing", Col("-" + Money(C.Construction), MutedC), L != null ? Col("-" + Money(L.Construction), MutedC) : "—");
            BookRow("<b>Net</b>", Col(Money(C.Net), C.Net < 0 ? BadC : GoodC), L != null ? Col(Money(L.Net), L.Net < 0 ? BadC : GoodC) : "—");

            GUILayout.Space(14);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Autosaves every Monday.", sSmall);
            GUILayout.FlexibleSpace();
            bool armed = Time.unscaledTime < confirmNewUntil;
            if (GUILayout.Button(armed ? "Click again to start over" : "New town", sBtn))
            {
                if (armed) { Later(gc.NewTown); confirmNewUntil = 0; } else confirmNewUntil = Time.unscaledTime + 3f;
            }
            GUILayout.EndHorizontal();
        }

        float Slider(string label, string value, float v, float min, float max, float step, string help)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, sLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label(Col(value, BrassC), sNumR, GUILayout.Width(60));
            GUILayout.EndHorizontal();
            float nv = GUILayout.HorizontalSlider(v, min, max);
            nv = Mathf.Round(nv / step) * step;
            GUILayout.Label(help, sSmall);
            GUILayout.Space(4);
            return nv;
        }

        void BookRow(string a, string b, string c, bool header = false)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(a, header ? sHead : sLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label(b, header ? sHead : sNumR, GUILayout.Width(82));
            GUILayout.Label(c, header ? sHead : sNumR, GUILayout.Width(82));
            GUILayout.EndHorizontal();
        }

        void Sparkline(Rect r)
        {
            if (Event.current.type != EventType.Repaint) return;
            var hist = T.History;
            var old = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.25f); GUI.DrawTexture(r, Texture2D.whiteTexture);
            if (hist.Count < 2) { GUI.color = old; return; }
            float mn = Mathf.Min(0, hist.Min(p => p.Treasury)), mx = Mathf.Max(1, hist.Max(p => p.Treasury) * 1.05f);
            float bw = r.width / hist.Count;
            for (int i = 0; i < hist.Count; i++)
            {
                float v = hist[i].Treasury, y = r.yMax - (v - mn) / (mx - mn) * (r.height - 4);
                GUI.color = new Color(Brass.r, Brass.g, Brass.b, 0.18f);
                GUI.DrawTexture(new Rect(r.x + i * bw, y, Mathf.Max(1, bw), r.yMax - y), Texture2D.whiteTexture);
                GUI.color = Brass;
                GUI.DrawTexture(new Rect(r.x + i * bw, y - 1, Mathf.Max(1, bw), 2), Texture2D.whiteTexture);
            }
            GUI.color = old;
        }

        // ---------------------------------------------------------------- citizens tab
        static readonly Role[] RoleOrder = { Role.Owner, Role.Worker, Role.Unemployed, Role.Sheriff, Role.Deputy, Role.Mayor };

        void DrawCitizens()
        {
            GUILayout.BeginHorizontal();
            foreach (var role in RoleOrder)
            {
                int n = T.People.Count(p => p.Role == role); if (n == 0) continue;
                GUILayout.Label(Col(n + " " + Defs.RoleName(role) + (n > 1 && role != Role.Unemployed ? "s" : ""), NatureArt.RoleColor[role]), sChip);
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUILayout.Label("NAME", sHead, GUILayout.Width(118)); GUILayout.Label("JOB", sHead, GUILayout.Width(92));
            GUILayout.Label("SAVINGS", sHead, GUILayout.Width(56)); GUILayout.Label("EATS", sHead, GUILayout.Width(52)); GUILayout.Label("MOOD", sHead);
            GUILayout.EndHorizontal();
            foreach (var p in T.People.OrderBy(p => System.Array.IndexOf(RoleOrder, p.Role)).ThenByDescending(p => p.Money))
            {
                var w = T.GetBuilding(p.WorkId);
                string job = (p.Role == Role.Worker || p.Role == Role.Owner) && w != null ? w.Def.Name : Defs.RoleName(p.Role);
                if (p.Role == Role.Owner) job += " ★";
                bool sel = gc.Selected.Kind == SelectionKind.Person && gc.Selected.Id == p.Id;
                var rect = GUILayoutUtility.GetRect(10, 22, GUILayout.ExpandWidth(true));
                if (sel && Event.current.type == EventType.Repaint) { var o = GUI.color; GUI.color = new Color(Brass.r, Brass.g, Brass.b, 0.18f); GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = o; }
                if (GUI.Button(rect, GUIContent.none, sRow)) { var id = p.Id; Later(() => { gc.Select(Selection.Person(id)); ResetConfirm(); }); }
                GUI.Label(new Rect(rect.x, rect.y, 118, 22), p.Name + (p.Jail > 0 ? Col(" (jailed)", BadC) : ""), sMono);
                GUI.Label(new Rect(rect.x + 122, rect.y, 92, 22), Col(job, NatureArt.RoleColor[p.Role]), sMono);
                GUI.Label(new Rect(rect.x + 214, rect.y, 56, 22), Money(p.Money), sMono);
                GUI.Label(new Rect(rect.x + 270, rect.y, 52, 22), FedLabel(p), sMono);
                GUI.Label(new Rect(rect.x + 322, rect.y, 40, 22), Col(Mathf.RoundToInt(p.Happy).ToString(), MoodColor(p.Happy)), sMono);
            }
            GUILayout.Space(6);
            GUILayout.Label("★ owns the business. Click a settler to inspect them.", sSmall);
        }

        // ---------------------------------------------------------------- trade tab
        void DrawTrade()
        {
            var m = T.Market;
            float xF = T.MayorId >= 0 && T.MayorPlatform == Platform.Trade ? 1.25f : 1f;
            Heading("Food today · " + Defs.Seasons[T.Season]);
            KV("Grown in back gardens", m.Garden.ToString("0.0"));
            KV("Still needed from shops", m.Demand.ToString("0.0"));
            KV("Bought at shops", m.Bought.ToString("0.0"));
            KV("Went without", m.Shortfall > 0.5f ? Col(m.Shortfall.ToString("0.0"), BadC) : m.Shortfall.ToString("0.0"));
            KV("Harvested on farms", T.OfType(BuildingType.Farm).Sum(b => b.Today.Made).ToString("0.0"));
            KV("In farm barns", T.OfType(BuildingType.Farm).Sum(b => b.Food).ToString("0.0"));
            KV("On shop shelves", T.Buildings.Where(b => b.Def.IsRetail).Sum(b => b.Food).ToString("0.0"));
            KV("Barns hold up to", Balance.BarnCapacity + " each");
            KV("Farm → shop price", Money(Balance.Wholesale));
            KV("Freight wagon pays", Money(Balance.ExportFood * xF) + " food · " + Money(Balance.ExportTools * xF) + " tools");
            GUILayout.Space(4);
            GUILayout.Label("Each settler eats 1 food a day. Gardens cover part of it (very little in winter); the rest is bought from stores and stalls, which buy from farms. Surplus leaves on the freight wagon cheaply.", sSmall);

            Heading("Businesses");
            GUILayout.BeginHorizontal();
            GUILayout.Label("BUSINESS", sHead, GUILayout.Width(110)); GUILayout.Label("OWNER", sHead, GUILayout.Width(110));
            GUILayout.Label("STAFF", sHead, GUILayout.Width(40)); GUILayout.Label("LAST WK", sHead);
            GUILayout.EndHorizontal();
            foreach (var b in T.Businesses)
            {
                var o = T.GetCitizen(b.OwnerId);
                var rect = GUILayoutUtility.GetRect(10, 22, GUILayout.ExpandWidth(true));
                if (GUI.Button(rect, GUIContent.none, sRow)) { var id = b.Id; Later(() => { gc.Select(Selection.Building(id)); ResetConfirm(); }); }
                GUI.Label(new Rect(rect.x, rect.y, 110, 22), b.Def.Name, sMono);
                GUI.Label(new Rect(rect.x + 110, rect.y, 110, 22), o != null ? o.Name : Col("For sale", WarnC), sMono);
                GUI.Label(new Rect(rect.x + 220, rect.y, 40, 22), T.WorkersAt(b).ToString(), sMono);
                GUI.Label(new Rect(rect.x + 260, rect.y, 80, 22), b.Last.Valid ? Col(Money(b.Last.Profit), b.Last.Profit < 0 ? BadC : GoodC) : "—", sMono);
            }
        }

        // ---------------------------------------------------------------- election tab
        void DrawElection()
        {
            if (T.Estate == null)
            {
                GUILayout.Label("<b>No Mayor's Estate yet.</b>", sLabel);
                GUILayout.Label("Build the Mayor's Estate (" + Money(Defs.Get(BuildingType.Estate).Cost) + "). Then the town picks a mayor, who moves out of their house and into the estate. The mayor's platform gives the whole town a bonus.", sSmall);
                return;
            }
            var m = T.Mayor;
            GUILayout.BeginVertical(sPanel2);
            GUILayout.BeginHorizontal();
            GUILayout.Label("MAYOR", sSub); GUILayout.FlexibleSpace();
            if (m != null && !T.Policy.ElectionsOn) GUILayout.Label(Col("For life", WarnC), sSmall);
            GUILayout.EndHorizontal();
            if (m != null)
            {
                GUILayout.Label(m.Name, sHeading);
                var pd = Defs.Get(T.MayorPlatform);
                GUILayout.Label("<b>" + pd.Name + ":</b> " + pd.Description + (T.Policy.ElectionsOn ? "\nTerm ends day " + T.TermEnd + " (" + Mathf.Max(0, T.TermEnd - T.Day) + " days)." : ""), sSmall);
            }
            else GUILayout.Label("Vacant.", sSmall);
            GUILayout.EndVertical();

            if (T.Campaign.Active)
            {
                var poll = T.Poll(); int total = Mathf.Max(1, T.People.Count);
                int days = T.Campaign.VoteDay - T.Day;
                Heading("Campaign · votes cast in " + days + " day" + (days == 1 ? "" : "s"));
                foreach (var c in T.Campaign.Candidates.OrderByDescending(c => poll[c.CitizenId]))
                {
                    var p = T.GetCitizen(c.CitizenId); if (p == null) continue;
                    var pd = Defs.Get(c.Platform);
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("<b>" + p.Name + "</b>" + (T.MayorId == p.Id ? Col(" (incumbent)", MutedC) : ""), sLabel);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(poll[c.CitizenId] + " votes", sNumR);
                    GUILayout.EndHorizontal();
                    GUILayout.Label(pd.Name + ": " + pd.Description, sSmall);
                    var bar = GUILayoutUtility.GetRect(10, 8, GUILayout.ExpandWidth(true));
                    var old = GUI.color;
                    GUI.color = new Color(0, 0, 0, 0.35f); GUI.DrawTexture(bar, Texture2D.whiteTexture);
                    GUI.color = Brass; GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * poll[c.CitizenId] / total, bar.height), Texture2D.whiteTexture);
                    GUI.color = old;
                    GUILayout.Space(8);
                }
                GUILayout.Label("Settlers favor whoever employs them, lives with them, or speaks to their troubles: farmhands like the farm platform, shop workers like trade, the poor like Common Folk, and a crime wave helps Law & Order.", sSmall);
            }
            else if (!T.Policy.ElectionsOn) GUILayout.Label("Elections are off. Turn them on in the Ledger tab to put the mayor's seat to a vote.", sSmall);

            if (T.LastElection.Valid)
            {
                Heading("Last election · day " + T.LastElection.Day);
                foreach (var r in T.LastElection.Results)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(r.Name, sLabel, GUILayout.Width(130));
                    GUILayout.Label(Col(Defs.Get(r.Platform).Name, MutedC), sLabel);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(r.Votes.ToString(), sNumR);
                    GUILayout.EndHorizontal();
                }
            }
        }

        // ---------------------------------------------------------------- gazette tab
        void DrawGazette()
        {
            foreach (var l in T.Log)
            {
                string c = l.Kind == LogKind.Bad ? BadC : l.Kind == LogKind.Good ? GoodC : l.Kind == LogKind.Vote ? BrassC : "#f0e5d0";
                GUILayout.BeginHorizontal();
                GUILayout.Label(Col("Day " + l.Day, MutedC), sSmall, GUILayout.Width(54));
                GUILayout.Label(Col(l.Message, c), sLabel);
                GUILayout.EndHorizontal();
                GUILayout.Space(2);
            }
        }

        // ---------------------------------------------------------------- inspector
        void DrawInspector()
        {
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            if (gc.Selected.Kind == SelectionKind.Person)
            {
                var p = T.GetCitizen(gc.Selected.Id);
                if (p == null) { Later(() => gc.Select(Selection.None)); GUILayout.EndVertical(); GUILayout.EndHorizontal(); return; }
                var own = T.OwnedBusiness(p); var home = T.GetBuilding(p.HomeId); var work = T.GetBuilding(p.WorkId);
                GUILayout.Label(p.Name, sHeading);
                GUILayout.Label(Defs.RoleName(p.Role) + (p.Jail > 0 ? " · " + Col("in jail", BadC) : "") + (own != null && p.Role == Role.Mayor ? " · still owns the " + own.Def.Name : ""), sSmall);
                KV("Works at", p.Role == Role.Mayor ? "Mayor's Estate" : work != null ? work.Def.Name : "—");
                KV("Lives at", home != null ? (home.Type == BuildingType.Estate ? "Mayor's Estate" : "House #" + home.Id) : Col("Homeless", BadC));
                KV("Savings", Money(p.Money));
                KV("Eating", FedLabel(p));
                KV("Mood", Col(Mathf.RoundToInt(p.Happy) + "%", MoodColor(p.Happy)));
                if (T.Campaign.Active) { var v = T.VoteOf(p); var vc = v != null ? T.GetCitizen(v.CitizenId) : null; KV("Will vote for", vc != null ? vc.Name : "—"); }
            }
            else
            {
                var b = T.GetBuilding(gc.Selected.Id);
                if (b == null) { Later(() => gc.Select(Selection.None)); GUILayout.EndVertical(); GUILayout.EndHorizontal(); return; }
                var d = b.Def;
                GUILayout.Label(d.Name, sHeading);
                if (d.IsBusiness)
                {
                    var o = T.GetCitizen(b.OwnerId);
                    GUILayout.Label(o != null ? "Owned by <b>" + o.Name + "</b>" + (o.WorkId != b.Id ? " (absent)" : "") : Col("For sale, waiting for a settler with savings", WarnC), sSmall);
                    KV("Cash on hand", Money(b.Cash));
                    KV("Daily wage", Money(b.Wage));
                    KV("Staff", T.WorkersAt(b) + " working · " + b.Employees.Count + "/" + d.MaxEmployees + " hired");
                    if (b.Type == BuildingType.Farm)
                    {
                        KV("Food in barn", b.Food.ToString("0.0"));
                        KV("Tools", b.Tools.ToString("0.0") + (b.Tools > 0.05f ? Col(" +40%", GoodC) : ""));
                        KV("Grew today", b.Today.Made.ToString("0.0"));
                    }
                    if (d.IsRetail)
                    {
                        KV("Food on shelves", b.Food.ToString("0.0") + " / " + d.ShelfCapacity);
                        KV("Retail price", Money(b.Price));
                        KV("Sold today", b.Today.Sold.ToString("0.0"));
                    }
                    if (b.Type == BuildingType.Smithy) { KV("Tools in stock", b.Tools.ToString("0.0")); KV("Forged today", b.Today.Made.ToString("0.0")); }
                    if (b.Type == BuildingType.Saloon) KV("Drinks poured today", b.Today.Sold.ToString("0"));
                    if (b.Last.Valid)
                    {
                        Heading("Last week");
                        KV("Revenue", Money(b.Last.Revenue)); KV("Costs", Money(b.Last.Costs));
                        KV("Profit", Col(Money(b.Last.Profit), b.Last.Profit < 0 ? BadC : GoodC));
                        KV("Business tax", Money(b.Last.Tax)); KV("Owner took home", Money(b.Last.OwnerDraw));
                    }
                    Heading("Payroll");
                    var staff = b.Employees.Select(T.GetCitizen).Where(x => x != null).Select(x => x.Name + (x.Jail > 0 ? " (jailed)" : "")).ToArray();
                    GUILayout.Label(staff.Length > 0 ? string.Join(", ", staff) : "No hired hands", sSmall);
                }
                else if (b.Type == BuildingType.House || b.Type == BuildingType.Estate)
                {
                    var rs = b.Residents.Select(T.GetCitizen).Where(x => x != null).ToList();
                    GUILayout.Label(b.Type == BuildingType.Estate ? (rs.Count > 0 ? "Home of the mayor" : "Empty, awaiting a mayor")
                        : rs.Count + " of " + d.Beds + " beds taken · garden gave " + b.GardenToday.ToString("0.0") + " food today", sSmall);
                    foreach (var p in rs)
                    {
                        if (GUILayout.Button(p.Name + "  ·  " + Defs.RoleName(p.Role) + "  ·  " + Money(p.Money) + "  ·  " + FedLabel(p), sRow)) { var id = p.Id; Later(() => gc.Select(Selection.Person(id))); }
                    }
                    if (b.Type == BuildingType.Estate && T.MayorPlatform != Platform.None)
                        GUILayout.Label("Platform: <b>" + Defs.Get(T.MayorPlatform).Name + "</b>. " + Defs.Get(T.MayorPlatform).Description, sSmall);
                }
                else if (b.Type == BuildingType.Sheriff)
                {
                    var st = b.Employees.Select(T.GetCitizen).Where(x => x != null).Select(x => Defs.RoleName(x.Role) + " " + x.Name).ToArray();
                    GUILayout.Label(st.Length > 0 ? string.Join(" · ", st) : "No lawmen hired yet", sSmall);
                    KV("Crimes this week", T.CrimesWeek.ToString());
                    KV("Caught this week", T.CaughtWeek.ToString());
                    KV("Last week", T.CaughtLast + " of " + T.CrimesLast + " caught");
                    var jailed = T.People.Where(x => x.Jail > 0).Select(x => x.Name).ToArray();
                    KV("In the jailhouse", jailed.Length > 0 ? string.Join(", ", jailed) : "Nobody");
                }
                GUILayout.Space(8);
                bool armed = confirmTearDown == b.Id;
                if (GUILayout.Button(armed ? "Click again to tear it down" : "Tear down (" + Money(d.Cost * 0.25f) + " back)", sBtn, GUILayout.ExpandWidth(false)))
                {
                    if (armed) { var bb = b; Later(() => { if (T.Buildings.Contains(bb)) T.Remove(bb); gc.Select(Selection.None); }); confirmTearDown = -1; }
                    else confirmTearDown = b.Id;
                }
            }
            GUILayout.EndVertical();
            if (GUILayout.Button("×", sBtn, GUILayout.Width(26), GUILayout.Height(24))) { Later(() => gc.Select(Selection.None)); confirmTearDown = -1; }
            GUILayout.EndHorizontal();
        }
    }
}
