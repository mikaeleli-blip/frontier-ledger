using System.Collections.Generic;
using System.Linq;
using FrontierLedger.Game.Art;
using FrontierLedger.Simulation;
using UnityEngine;

namespace FrontierLedger.Game
{
    public enum SelectionKind { None, Building, Person }
    public struct Selection
    {
        public SelectionKind Kind; public int Id;
        public static readonly Selection None = new Selection { Kind = SelectionKind.None, Id = -1 };
        public static Selection Building(int id) { return new Selection { Kind = SelectionKind.Building, Id = id }; }
        public static Selection Person(int id) { return new Selection { Kind = SelectionKind.Person, Id = id }; }
    }

    /// <summary>
    /// Draws the town with SpriteRenderers: ground, buildings and construction sites, crops, piles of goods,
    /// trees, tents, settlers walking the roads, chickens, smoke, water sparkle, clouds, birds, dusk and night.
    /// Positions are worked out in map pixels (y down) and converted to world units.
    /// </summary>
    public sealed class WorldView : MonoBehaviour
    {
        const int OrderGround = -32000, OrderBase = -31000, OrderCrops = -30990, OrderSmoke = 29000, OrderClouds = 29500,
                  OrderNight = 30000, OrderLights = 30010, OrderSelect = 31000, OrderGhost = 31010;

        Town town;
        CameraRig rig;
        Nav nav;
        GroundPainter painter;
        Texture2D groundTex;
        SpriteRenderer ground;
        int paintedSeason = -1, seenMapVersion = -1;
        float time;

        sealed class BuildingView
        {
            public Building B; public GameObject Go; public SpriteRenderer Base, Top, Lights, Forge, Overlay, Pile;
            public List<SpriteRenderer> Chickens = new List<SpriteRenderer>(); public int Season = -1, Stage = -2; public BuildingSprites Art;
        }
        sealed class PersonView
        {
            public Citizen P; public SpriteRenderer Sr;
            public Vector2 Pos = new Vector2(float.NaN, 0), Goal = new Vector2(float.NaN, 0);
            public List<Vector2> Path = new List<Vector2>(); public int PathIdx;
            public int Dir; public float Step; public bool Moving; public bool Inside;
        }
        sealed class Particle { public SpriteRenderer Sr; public float X, Y, Life, Max, Vx; public bool Dark; }
        sealed class Cloud { public SpriteRenderer Sr; public float X, Y; public int Rx; }

        readonly Dictionary<int, BuildingView> buildings = new Dictionary<int, BuildingView>();
        readonly Dictionary<int, SpriteRenderer> trees = new Dictionary<int, SpriteRenderer>();
        readonly Dictionary<int, byte> treeKinds = new Dictionary<int, byte>();
        readonly Dictionary<int, PersonView> people = new Dictionary<int, PersonView>();
        readonly List<Particle> particles = new List<Particle>();
        readonly Stack<SpriteRenderer> particlePool = new Stack<SpriteRenderer>();
        readonly Dictionary<int, float> smokeTimers = new Dictionary<int, float>();
        readonly List<SpriteRenderer> sparkles = new List<SpriteRenderer>();
        readonly List<Cloud> clouds = new List<Cloud>();
        readonly List<SpriteRenderer> birds = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> tents = new List<SpriteRenderer>();
        readonly Dictionary<int, Vector2> tentSpot = new Dictionary<int, Vector2>();   // household head -> tent door
        SpriteRenderer campfire;
        float birdX, birdY, birdVx; bool birdsActive;
        SpriteRenderer night, sunset;
        readonly SpriteRenderer[] selEdges = new SpriteRenderer[4];
        SpriteRenderer selMarker;
        SpriteRenderer ghostBase, ghostTop, ghostFoot;
        Transform layerBuildings, layerTrees, layerPeople, layerFx;

        // ================================================================== setup
        public void Bind(Town t, CameraRig cameraRig)
        {
            town = t; rig = cameraRig; nav = new Nav(t);
            foreach (Transform c in transform) Destroy(c.gameObject);
            buildings.Clear(); trees.Clear(); treeKinds.Clear(); people.Clear(); particles.Clear(); particlePool.Clear(); smokeTimers.Clear();
            sparkles.Clear(); clouds.Clear(); birds.Clear(); tents.Clear(); tentSpot.Clear();
            paintedSeason = -1; seenMapVersion = -1; treeSeason = -1; treeMapVersion = -1;

            layerBuildings = Layer("Buildings"); layerTrees = Layer("Trees"); layerPeople = Layer("Settlers"); layerFx = Layer("Effects");
            painter = new GroundPainter(town);
            groundTex = new Texture2D(painter.Canvas.W, painter.Canvas.H, TextureFormat.RGBA32, false);
            groundTex.filterMode = FilterMode.Point; groundTex.wrapMode = TextureWrapMode.Clamp; groundTex.name = "Ground";
            var gs = Sprite.Create(groundTex, new Rect(0, 0, groundTex.width, groundTex.height), new Vector2(0, 1), Pixels.PerUnit, 0, SpriteMeshType.FullRect);
            ground = MakeSr("Ground", transform, gs, OrderGround);

            night = MakeSr("Night", layerFx, NatureArt.White, OrderNight);
            sunset = MakeSr("Sunset", layerFx, NatureArt.White, OrderNight - 1);
            for (int i = 0; i < 4; i++) selEdges[i] = MakeSr("Selection", layerFx, NatureArt.White, OrderSelect);
            selMarker = MakeSr("Marker", layerFx, NatureArt.White, OrderSelect);
            ghostBase = MakeSr("GhostBase", layerFx, null, OrderGhost);
            ghostTop = MakeSr("GhostTop", layerFx, null, OrderGhost + 1);
            ghostFoot = MakeSr("GhostFoot", layerFx, NatureArt.White, OrderGhost + 2);
            campfire = MakeSr("Campfire", layerBuildings, PropArt.Campfire(0), 0); campfire.enabled = false;
            for (int i = 0; i < 160; i++) { var s = MakeSr("Sparkle", layerFx, NatureArt.White, OrderBase + 1); s.color = new Color32(214, 238, 250, 255); s.enabled = false; sparkles.Add(s); }
            for (int i = 0; i < 5; i++)
            {
                int rx = Random.Range(40, 90), ry = Random.Range(18, 34);
                clouds.Add(new Cloud { Sr = MakeSr("Cloud", layerFx, CloudSprite(rx, ry), OrderClouds), X = Random.Range(0f, Town.Cols * 16f), Y = Random.Range(0f, Town.Rows * 16f), Rx = rx });
            }
            for (int i = 0; i < 5; i++) { var b = MakeSr("Bird", layerFx, NatureArt.White, OrderClouds + 1); b.enabled = false; birds.Add(b); }
            HideGhost();
        }

        Transform Layer(string n) { var go = new GameObject(n); go.transform.SetParent(transform, false); return go.transform; }

        static SpriteRenderer MakeSr(string name, Transform parent, Sprite s, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s; sr.sortingOrder = order;
            return sr;
        }

        static void Place(Component c, float px, float py, float w = 1f, float h = 1f)
        {
            c.transform.position = Pixels.ToWorld(px, py);
            c.transform.localScale = new Vector3(w, h, 1f);
        }

        static int SortOrder(float footPy, int bias = 0) { return Mathf.RoundToInt(footPy) * 4 + bias; }

        static Sprite CloudSprite(int rx, int ry)
        {
            var g = new PixelCanvas(rx * 2 + 2, ry * 2 + 2);
            var c = new Color32(18, 28, 48, 18);
            for (int dy = -ry; dy <= ry; dy += 2)
            {
                int hw = Mathf.RoundToInt(rx * Mathf.Sqrt(Mathf.Max(0, 1 - (dy / (float)ry) * (dy / (float)ry))) / 2) * 2;
                if (hw > 0) g.R(rx - hw + 1, dy + ry + 1, hw * 2, 2, c);
            }
            return g.ToSprite("cloud");
        }

        // ================================================================== frame
        void LateUpdate()
        {
            if (town == null) return;
            time += Time.deltaTime;
            int si = town.Season;
            SyncGround(si);
            SyncBuildings(si);
            SyncTrees(si);
            SyncTents(si);
            SyncPeople();
            UpdateEffects(si);
            UpdateNight();
            UpdateSelection();
        }

        void SyncGround(int si)
        {
            if (paintedSeason != si)
            {
                painter.PaintAll(si);
                groundTex.SetPixels32(painter.Canvas.Px); groundTex.Apply(false);
                paintedSeason = si; seenMapVersion = town.MapVersion; town.DirtyTiles.Clear();
                return;
            }
            if (seenMapVersion != town.MapVersion)
            {
                var done = new HashSet<int>();
                foreach (int k in town.DirtyTiles) if (done.Add(k)) painter.PaintAround(k % Town.Cols, k / Town.Cols, si);
                town.DirtyTiles.Clear();
                groundTex.SetPixels32(painter.Canvas.Px); groundTex.Apply(false);
                seenMapVersion = town.MapVersion;
            }
        }

        // ---------------------------------------------------------------- buildings
        public BuildingSprites ArtFor(Building b) { return BuildingArt.Get(b.Type, BuildingArt.VariantOf(b), town.Season); }

        bool Lit(Building b)
        {
            if (!b.Built) return false;
            switch (b.Type)
            {
                case BuildingType.House: return b.Residents.Count > 0;
                case BuildingType.Estate: return town.MayorId >= 0;
                case BuildingType.Sheriff: return b.Employees.Count > 0;
                case BuildingType.Storehouse: case BuildingType.Wagon: case BuildingType.Cemetery: return false;
                default: return b.Def.IsBusiness && town.IsOpen(b) && (b.Type == BuildingType.Saloon || town.TimeOfDay > 0.75f);
            }
        }

        static Good? PileGood(Building b)
        {
            if (b.Def.IsStorage)
            {
                int best = -1; float bv = 0.5f;
                for (int g = 0; g < 5; g++) if (b.Stock[g] > bv) { bv = b.Stock[g]; best = g; }
                return best < 0 ? (Good?)null : (Good)best;
            }
            foreach (Good g in new[] { Good.Food, Good.Firewood, Good.Logs, Good.Stone, Good.Tools }) if (Town.Produces(b.Type, g)) return g;
            return null;
        }

        void SyncBuildings(int si)
        {
            var alive = new HashSet<int>();
            float n = NightAlpha();
            int frame = Mathf.FloorToInt(time * 1.1f) % 2;
            foreach (var b in town.Buildings)
            {
                if (b.Type == BuildingType.Road) continue;
                alive.Add(b.Id);
                BuildingView v;
                if (!buildings.TryGetValue(b.Id, out v)) { v = CreateBuildingView(b); buildings[b.Id] = v; }
                var d = b.Def;
                int stage = b.Built ? -1 : Mathf.Clamp(Mathf.FloorToInt(b.BuildFraction * 4), 0, 3);
                if (v.Season != si || v.Stage != stage)
                {
                    v.Art = ArtFor(b); v.Season = si; v.Stage = stage;
                    if (stage >= 0)
                    {
                        var site = BuildingArt.Site(b.Type, BuildingArt.VariantOf(b), si, stage);
                        v.Base.sprite = site[0]; v.Top.sprite = site[1];
                    }
                    else { v.Top.sprite = v.Art.Top; }
                    v.Lights.sprite = v.Art.Lights;
                }
                if (stage < 0) v.Base.sprite = v.Art.Base[frame];
                bool lit = Lit(b) && n > 0.02f;
                v.Lights.enabled = lit;
                if (lit) v.Lights.color = new Color(1, 1, 1, Mathf.Clamp01(n * 1.6f));
                if (v.Forge != null)
                {
                    bool open = b.Built && town.IsOpen(b);
                    v.Forge.enabled = open;
                    if (open)
                    {
                        string[] fl = { "#ff9a3a", "#ffc35a", "#e0601e", "#ff8a2a" };
                        v.Forge.color = PixelCanvas.C(fl[(Mathf.FloorToInt(time * 9) + b.Id) % 4]);
                    }
                }
                // Crops and graves sit on the ground layer
                if (v.Overlay != null)
                {
                    if (b.Type == BuildingType.Farm && b.Built)
                    {
                        int st = PropArt.FieldStage(b, si);
                        float tot = b.CropGrowth * Balance.FieldYield;
                        v.Overlay.sprite = PropArt.FieldCrops(st, frame, tot > 0 ? 1 - b.CropLeft / tot : 0);
                        v.Overlay.enabled = true;
                    }
                    else if (b.Type == BuildingType.Cemetery && b.Built) { v.Overlay.sprite = PropArt.Graves(b.Graves, si); v.Overlay.enabled = true; }
                    else v.Overlay.enabled = false;
                }
                // Pile of goods
                if (v.Pile != null)
                {
                    var g = b.Built ? PileGood(b) : null;
                    int lvl = 0;
                    if (g.HasValue) lvl = PropArt.PileLevel(b.Stock[(int)g.Value], b.Def.IsStorage ? b.Def.Capacity * 0.5f : (b.Type == BuildingType.Farm ? 160 : 60));
                    v.Pile.enabled = lvl > 0;
                    if (lvl > 0) v.Pile.sprite = PropArt.Pile(g.Value, lvl, si);
                }
                for (int i = 0; i < v.Chickens.Count; i++)
                {
                    var spot = v.Art.Chickens[i];
                    float t = time * 0.6f + i * 2.1f + b.Id;
                    float cx = b.X * 16 + spot.x + Mathf.Round(Mathf.Sin(t) * 4), cy = b.Y * 16 + spot.y + Mathf.Round(Mathf.Sin(t * 0.7f + 1) * 2);
                    var sr = v.Chickens[i];
                    sr.enabled = b.Built && n < 0.6f && si != 3;
                    sr.sprite = NatureArt.Chicken(Mathf.FloorToInt(time * 2 + i) % 3 == 0 ? 1 : 0);
                    sr.flipX = Mathf.Cos(t) < 0;
                    sr.transform.position = Pixels.ToWorld(cx, cy);
                    sr.sortingOrder = SortOrder(cy, 1);
                }
            }
            if (alive.Count != buildings.Count)
            {
                var gone = new List<int>();
                foreach (var kv in buildings) if (!alive.Contains(kv.Key)) gone.Add(kv.Key);
                foreach (int id in gone) { Destroy(buildings[id].Go); buildings.Remove(id); smokeTimers.Remove(id); }
            }
        }

        BuildingView CreateBuildingView(Building b)
        {
            var d = b.Def;
            var v = new BuildingView { B = b, Art = ArtFor(b) };
            v.Go = new GameObject(d.Name + " #" + b.Id);
            v.Go.transform.SetParent(layerBuildings, false);
            v.Go.transform.position = Pixels.ToWorld(b.X * 16, b.Y * 16 - v.Art.Lift);
            int foot = (b.Y + d.H) * 16;
            v.Base = MakeSr("Base", v.Go.transform, v.Art.Base[0], OrderBase);
            v.Top = MakeSr("Top", v.Go.transform, v.Art.Top, SortOrder(foot));
            v.Lights = MakeSr("Lights", v.Go.transform, v.Art.Lights, OrderLights);
            if (b.Type == BuildingType.Farm || b.Type == BuildingType.Cemetery)
            {
                v.Overlay = MakeSr("Overlay", v.Go.transform, null, OrderCrops);
                v.Overlay.transform.position = Pixels.ToWorld(b.X * 16, b.Y * 16);
            }
            if (v.Art.PileAt.x >= 0)
            {
                v.Pile = MakeSr("Pile", v.Go.transform, null, SortOrder(foot, 1));
                v.Pile.transform.position = Pixels.ToWorld(b.X * 16 + v.Art.PileAt.x, b.Y * 16 - v.Art.Lift + v.Art.PileAt.y);
            }
            if (v.Art.HasForge)
            {
                v.Forge = MakeSr("Forge", v.Go.transform, NatureArt.White, SortOrder(foot, 1));
                v.Forge.transform.localPosition = Pixels.ToWorld(v.Art.Forge.x, v.Art.Forge.y);
                v.Forge.transform.localScale = new Vector3(v.Art.Forge.width, v.Art.Forge.height, 1);
            }
            foreach (var c in v.Art.Chickens) v.Chickens.Add(MakeSr("Chicken", v.Go.transform, NatureArt.Chicken(0), 0));
            return v;
        }

        // ---------------------------------------------------------------- trees
        int treeSeason = -1, treeMapVersion = -1;
        static bool IsGrowth(byte t) { return t == Tile.Tree || t == Tile.Rock || t == Tile.Sapling || t == Tile.Stump; }

        void SyncTrees(int si)
        {
            if (treeSeason == si && treeMapVersion == town.MapVersion) return;
            bool reseason = treeSeason != si;
            treeSeason = si; treeMapVersion = town.MapVersion;
            var want = new HashSet<int>();
            for (int ty = 0; ty < Town.Rows; ty++)
                for (int tx = 0; tx < Town.Cols; tx++)
                {
                    byte t = town.TerrainAt(tx, ty);
                    if (!IsGrowth(t)) continue;
                    int k = ty * Town.Cols + tx; want.Add(k);
                    SpriteRenderer sr; byte was;
                    bool exists = trees.TryGetValue(k, out sr);
                    if (exists && !reseason && treeKinds.TryGetValue(k, out was) && was == t) continue;
                    var pl = NatureArt.At(tx, ty, t, si);
                    if (!exists) { sr = MakeSr("Growth", layerTrees, pl.Sprite, 0); trees[k] = sr; }
                    sr.sprite = pl.Sprite;
                    sr.transform.position = Pixels.ToWorld(pl.Px, pl.Py);
                    sr.sortingOrder = SortOrder(pl.Py);
                    treeKinds[k] = t;
                }
            if (want.Count != trees.Count)
            {
                var gone = new List<int>();
                foreach (var kv in trees) if (!want.Contains(kv.Key)) gone.Add(kv.Key);
                foreach (int k in gone) { Destroy(trees[k].gameObject); trees.Remove(k); treeKinds.Remove(k); }
            }
        }

        // ---------------------------------------------------------------- tents for the homeless
        Vector2 CampCenter()
        {
            var s = town.Storages.FirstOrDefault();
            if (s == null) return new Vector2(Town.WagonX * 16 + 16, Town.WagonY * 16 + 40);
            return new Vector2(s.X * 16 + s.Def.W * 8, (s.Y + s.Def.H) * 16 + 10);
        }

        void SyncTents(int si)
        {
            var heads = town.People.Where(p => p.HomeId < 0 && !p.IsChild && (p.PartnerId < 0 || p.Female || town.GetCitizen(p.PartnerId) == null || town.GetCitizen(p.PartnerId).HomeId >= 0)).ToList();
            tentSpot.Clear();
            var center = CampCenter();
            int ctx = Mathf.FloorToInt(center.x / 16), cty = Mathf.FloorToInt(center.y / 16);
            var spots = new List<Vector2Int>();
            for (int r = 1; r < 8 && spots.Count < heads.Count; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                        int x = ctx + dx, y = cty + dy;
                        byte t = town.TerrainAt(x, y);
                        if (t != Tile.Grass || town.OccupantAt(x, y) != null || town.OccupantAt(x, y + 1) != null) continue;
                        if (spots.Count < heads.Count) spots.Add(new Vector2Int(x, y));
                    }
            for (int i = 0; i < heads.Count; i++)
            {
                if (i >= tents.Count) tents.Add(MakeSr("Tent", layerBuildings, null, 0));
                var sr = tents[i];
                if (i >= spots.Count) { sr.enabled = false; continue; }
                var s = spots[i];
                float px = s.x * 16 + 8, py = s.y * 16 + 14;
                sr.enabled = true; sr.sprite = PropArt.Tent(heads[i].Id, si);
                sr.transform.position = Pixels.ToWorld(px, py); sr.sortingOrder = SortOrder(py);
                tentSpot[heads[i].Id] = new Vector2(px, py + 3);
            }
            for (int i = heads.Count; i < tents.Count; i++) tents[i].enabled = false;
            campfire.enabled = heads.Count > 0;
            if (campfire.enabled)
            {
                campfire.sprite = PropArt.Campfire(Mathf.FloorToInt(time * 6) % 3);
                campfire.transform.position = Pixels.ToWorld(center.x, center.y + 4);
                campfire.sortingOrder = SortOrder(center.y + 4);
            }
        }

        // ---------------------------------------------------------------- settlers
        Vector2 Front(Building b, Citizen p, float spread = 1f)
        {
            var d = b.Def; float a = p.Seed, c = (p.Seed * 7.31f) % 1f;
            return new Vector2(b.X * 16 + 4 + a * (d.W * 16 - 8) * spread + (1 - spread) * (d.W * 8 - 4), (b.Y + d.H) * 16 + 3 + c * 4);
        }

        Vector2 TileSpot(int tile, Citizen p, float ox = 0)
        {
            int x = tile % Town.Cols, y = tile / Town.Cols;
            return new Vector2(x * 16 + 8 + ox + (p.Seed - 0.5f) * 6, y * 16 + 15);
        }

        Vector2 WorkSpot(Building b, Citizen p)
        {
            var d = b.Def;
            float wob = Mathf.Floor(time / 6f + p.Seed * 10);
            switch (b.Type)
            {
                case BuildingType.Farm:
                {
                    float fx = Noise.Hash(p.Id, (int)wob, 1), fy = Noise.Hash((int)wob, p.Id, 2);
                    return new Vector2(b.X * 16 + 6 + fx * 36, b.Y * 16 + 8 + Mathf.Floor(fy * 9) * 4 + 3);
                }
                case BuildingType.LumberCamp:
                case BuildingType.Quarry:
                    if (b.TargetTile >= 0) return TileSpot(b.TargetTile, p, p.Seed < 0.5f ? -8 : 8);
                    break;
                case BuildingType.Gatherer:
                {
                    int r = d.Radius, cx = b.X + 1, cy = b.Y + 1;
                    for (int k = 0; k < 12; k++)
                    {
                        int x = cx + Mathf.RoundToInt((Noise.Hash(p.Id, k + (int)wob * 13, 3) - 0.5f) * 2 * r), y = cy + Mathf.RoundToInt((Noise.Hash(k + (int)wob * 13, p.Id, 4) - 0.5f) * 2 * r);
                        if (town.TerrainAt(x, y) == Tile.Tree && town.TerrainAt(x, y + 1) != Tile.Water) return new Vector2(x * 16 + 8, y * 16 + 18);
                    }
                    break;
                }
                case BuildingType.Fishery:
                {
                    int[] dx = { 0, 1, -1, 2, 0, 1 }, dy = { 2, 2, 0, 0, -1, -1 };
                    for (int k = 0; k < 6; k++)
                    {
                        int x = b.X + dx[k], y = b.Y + dy[k];
                        if (town.TerrainAt(x, y) == Tile.Water) return new Vector2(x * 16 + 8, y * 16 + 10);
                    }
                    break;
                }
            }
            return Front(b, p);
        }

        /// <summary>Where a settler wants to be right now, and whether they're indoors there.</summary>
        Vector2 TargetFor(Citizen p, out bool inside, out Building visit)
        {
            inside = false; visit = null;
            float ph = town.TimeOfDay;
            bool workHours = ph > 0.24f && ph < 0.7f, evening = ph >= 0.7f && ph < 0.86f, nightTime = ph >= 0.86f || ph < 0.2f;
            if (p.Jail > 0) { var off = town.Office; if (off != null) { visit = off; inside = true; return Front(off, p, 0.3f); } }
            if (workHours && !p.IsChild)
            {
                if ((p.Role == Role.Builder || p.Role == Role.Laborer) && p.TaskId >= 0)
                {
                    var s = town.GetBuilding(p.TaskId);
                    if (s != null) { visit = s; float a = p.Seed; return new Vector2(s.X * 16 + 2 + a * (s.Def.W * 16 - 4), (s.Y + s.Def.H) * 16 + 2 - (p.Id % 3) * 6); }
                }
                var w = town.GetBuilding(p.WorkId);
                if (w != null && w.Built) { visit = w; return w.Type == BuildingType.Estate ? Front(w, p, 0.3f) : WorkSpot(w, p); }
            }
            if (evening && p.SaloonToday >= 0) { var s = town.GetBuilding(p.SaloonToday); if (s != null) { visit = s; return Front(s, p); } }
            var home = town.GetBuilding(p.HomeId);
            if (home != null)
            {
                visit = home;
                if (nightTime) inside = true;
                var f = Front(home, p, 0.5f);
                if (p.IsChild && !nightTime)
                {
                    float wob = Mathf.Floor(time / 4f + p.Seed * 7);
                    f += new Vector2((Noise.Hash(p.Id, (int)wob, 7) - 0.5f) * 40, (Noise.Hash((int)wob, p.Id, 8)) * 14);
                }
                else if (!nightTime && !workHours && p.Role == Role.Laborer) f.y += 4;
                return f;
            }
            // homeless: by the tent of their household
            Vector2 tent;
            int headId = p.IsChild ? p.ParentId : (p.PartnerId >= 0 && !p.Female ? p.PartnerId : p.Id);
            if (tentSpot.TryGetValue(headId, out tent) || tentSpot.TryGetValue(p.Id, out tent))
            {
                if (nightTime) inside = true;
                return tent + new Vector2((p.Seed - 0.5f) * 10, 2);
            }
            var c = CampCenter();
            return c + new Vector2((p.Seed - 0.5f) * 30, 6 + (p.Seed * 13) % 6);
        }

        void Repath(PersonView v, Vector2 goal, Building visit)
        {
            v.Goal = goal; v.Path.Clear(); v.PathIdx = 0;
            var s = new Vector2Int(Mathf.FloorToInt(v.Pos.x / 16), Mathf.FloorToInt((v.Pos.y - 1) / 16));
            var t = new Vector2Int(Mathf.FloorToInt(goal.x / 16), Mathf.FloorToInt((goal.y - 1) / 16));
            if (s != t)
            {
                var tiles = nav.Find(s, t, visit);
                if (tiles != null)
                    for (int i = 1; i < tiles.Count - 1; i++) v.Path.Add(new Vector2(tiles[i].x * 16 + 8, tiles[i].y * 16 + 12));
            }
            v.Path.Add(goal);
        }

        void SyncPeople()
        {
            int gameSpeed = GameController.Instance != null ? Mathf.Max(1, GameController.Instance.Speed) : 1;
            float baseSpeed = 34f * gameSpeed * Time.deltaTime;
            var alive = new HashSet<int>();
            foreach (var p in town.People)
            {
                alive.Add(p.Id);
                PersonView v;
                if (!people.TryGetValue(p.Id, out v))
                {
                    v = new PersonView { P = p };
                    v.Sr = MakeSr(p.Name, layerPeople, null, 0);
                    people[p.Id] = v;
                }
                v.P = p;
                bool inside; Building visit;
                var goal = TargetFor(p, out inside, out visit);
                if (float.IsNaN(v.Pos.x)) { v.Pos = goal; v.Goal = goal; v.Path.Clear(); }
                if (float.IsNaN(v.Goal.x) || (goal - v.Goal).sqrMagnitude > 9f) Repath(v, goal, visit);

                v.Moving = false;
                if (v.PathIdx < v.Path.Count)
                {
                    int tx = Mathf.FloorToInt(v.Pos.x / 16), ty = Mathf.FloorToInt((v.Pos.y - 1) / 16);
                    float speed = baseSpeed * (nav.IsRoad(tx, ty) ? 1.5f : 1f) * (p.IsChild ? 1.1f : 1f) * (p.Age > 60 ? 0.8f : 1f);
                    var target = v.Path[v.PathIdx];
                    var delta = target - v.Pos;
                    float dist = delta.magnitude;
                    if (dist <= speed) { v.Pos = target; v.PathIdx++; }
                    else
                    {
                        v.Pos += delta / dist * speed;
                        v.Moving = true; v.Step += speed;
                        v.Dir = Mathf.Abs(delta.x) > Mathf.Abs(delta.y) ? (delta.x < 0 ? PeopleArt.Left : PeopleArt.Right) : (delta.y < 0 ? PeopleArt.Up : PeopleArt.Down);
                    }
                }
                bool arrived = v.PathIdx >= v.Path.Count;
                v.Inside = inside && arrived;
                if (arrived && !v.Moving) v.Dir = p.Role == Role.Worker || p.Role == Role.Builder ? v.Dir : PeopleArt.Down;
                v.Sr.enabled = !v.Inside;
                if (!v.Sr.enabled) continue;
                int frame = v.Moving ? ((Mathf.FloorToInt(v.Step / 5) % 4) == 1 ? 1 : (Mathf.FloorToInt(v.Step / 5) % 4) == 3 ? 2 : 0) : 0;
                // workers on the job make a little working motion
                if (!v.Moving && arrived && (p.Role == Role.Worker || p.Role == Role.Builder) && town.TimeOfDay > 0.24f && town.TimeOfDay < 0.7f)
                    frame = (Mathf.FloorToInt(time * 3 + p.Seed * 5) % 2) * 2;
                float x = Mathf.Round(v.Pos.x), y = Mathf.Round(v.Pos.y);
                v.Sr.sprite = PeopleArt.Get(p, v.Dir, frame);
                v.Sr.transform.position = Pixels.ToWorld(x, y);
                v.Sr.sortingOrder = SortOrder(y, 2);
            }
            if (alive.Count != people.Count)
            {
                var gone = new List<int>();
                foreach (var kv in people) if (!alive.Contains(kv.Key)) gone.Add(kv.Key);
                foreach (int id in gone) { Destroy(people[id].Sr.gameObject); people.Remove(id); }
            }
        }

        /// <summary>Where a settler is currently drawn, in map pixels.</summary>
        public bool TryGetPersonPos(int id, out Vector2 pos)
        {
            PersonView v;
            if (people.TryGetValue(id, out v) && !float.IsNaN(v.Pos.x)) { pos = v.Pos; return true; }
            pos = Vector2.zero; return false;
        }
        public bool IsInside(int id) { PersonView v; return people.TryGetValue(id, out v) && v.Inside; }

        // ---------------------------------------------------------------- effects
        Rect VisiblePx()
        {
            var o = rig.ViewOrigin; var s = rig.ViewSize;
            return new Rect(o.x - 48, o.y - 48, s.x + 96, s.y + 112);
        }

        float NightAlpha()
        {
            int sp = GameController.Instance != null ? GameController.Instance.Speed : 1;
            float k = sp >= 4 ? 0.35f : sp >= 2 ? 0.65f : 1f;
            return town.Nightness * k;
        }

        void UpdateEffects(int si)
        {
            float dt = Time.deltaTime, nightness = town.Nightness;
            var vr = VisiblePx();
            foreach (var kv in buildings)
            {
                var b = kv.Value.B; var d = b.Def;
                if (!b.Built) continue;
                float X = b.X * 16, Y = b.Y * 16;
                if (X > vr.xMax || X + d.W * 16 < vr.xMin || Y > vr.yMax || Y + d.H * 16 < vr.yMin) continue;
                float rate = 0; bool dark = false;
                if (b.Type == BuildingType.House && b.Residents.Count > 0) rate = (nightness > 0 || town.TimeOfDay < 0.3f || si == 3 || si == 2) ? 0.9f : 0.3f;
                else if (b.Type == BuildingType.Smithy && town.IsOpen(b)) { rate = 2.2f; dark = true; }
                else if ((b.Type == BuildingType.Estate && town.MayorId >= 0) || (b.Type == BuildingType.LumberCamp && town.IsOpen(b))) rate = 0.6f;
                if (rate <= 0) continue;
                float tmr; if (!smokeTimers.TryGetValue(b.Id, out tmr)) tmr = Random.value;
                tmr -= dt * rate;
                if (tmr <= 0)
                {
                    tmr = 1 + Random.value * 0.4f;
                    var art = kv.Value.Art;
                    foreach (var s in art.Smoke) if (particles.Count < 500) Spawn(X + s.x, Y - art.Lift + s.y, dark);
                }
                smokeTimers[b.Id] = tmr;
            }
            if (campfire.enabled && Random.value < dt * 1.5f) Spawn(Pixels.ToPixel(campfire.transform.position).x, Pixels.ToPixel(campfire.transform.position).y - 6, false);
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                var p = particles[i];
                p.Life -= dt; p.X += (3 + p.Vx) * dt; p.Y -= 7 * dt;
                if (p.Life <= 0) { p.Sr.enabled = false; particlePool.Push(p.Sr); particles.RemoveAt(i); continue; }
                float k = 1 - p.Life / p.Max; int sz = 1 + Mathf.FloorToInt(k * 3);
                p.Sr.color = p.Dark ? new Color(110 / 255f, 104 / 255f, 98 / 255f, 0.6f * (1 - k)) : new Color(236 / 255f, 232 / 255f, 224 / 255f, 0.6f * (1 - k));
                Place(p.Sr, Mathf.Round(p.X - sz / 2f), Mathf.Round(p.Y - sz / 2f), sz, sz);
            }

            int used = 0;
            if (si != 3)
            {
                int tick = Mathf.FloorToInt(time * 1.6f);
                int tx0 = Mathf.Max(0, Mathf.FloorToInt(vr.xMin / 16)), tx1 = Mathf.Min(Town.Cols - 1, Mathf.CeilToInt(vr.xMax / 16));
                int ty0 = Mathf.Max(0, Mathf.FloorToInt(vr.yMin / 16)), ty1 = Mathf.Min(Town.Rows - 1, Mathf.CeilToInt(vr.yMax / 16));
                for (int ty = ty0; ty <= ty1 && used < sparkles.Count; ty++)
                    for (int tx = tx0; tx <= tx1 && used < sparkles.Count; tx++)
                    {
                        if (town.TerrainAt(tx, ty) != Tile.Water || town.IsRoad(tx, ty)) continue;
                        for (int k = 0; k < 2 && used < sparkles.Count; k++)
                        {
                            int q = tick + ((tx * 3 + ty * 5 + k) % 7);
                            if (Noise.Hash(tx * 7 + k, ty * 13, q) >= 0.16f) continue;
                            var s = sparkles[used++]; s.enabled = true;
                            Place(s, tx * 16 + 2 + (int)(Noise.Hash(tx, ty, k + q) * 11), ty * 16 + 3 + (int)(Noise.Hash(ty, tx, k + q) * 10), 2, 1);
                        }
                    }
            }
            for (int i = used; i < sparkles.Count; i++) sparkles[i].enabled = false;

            foreach (var c in clouds)
            {
                c.X += 4 * dt;
                if (c.X - c.Rx > Town.Cols * 16) { c.X = -c.Rx; c.Y = Random.Range(0f, Town.Rows * 16f); }
                c.Sr.transform.position = Pixels.ToWorld(Mathf.Round(c.X - c.Rx), Mathf.Round(c.Y - c.Rx / 2f));
            }

            var view = rig.ViewSize; var org = rig.ViewOrigin;
            if (birdsActive)
            {
                birdX += birdVx * dt;
                if (birdX > org.x + view.x + 60) birdsActive = false;
            }
            else if (nightness < 0.2f && si != 3 && Random.value < dt / 18f)
            {
                birdsActive = true; birdX = org.x - 30; birdY = org.y + Random.value * view.y * 0.6f + 10; birdVx = 26 + Random.value * 14;
            }
            for (int i = 0; i < birds.Count; i++)
            {
                var b = birds[i];
                b.enabled = birdsActive && i < 3 + (int)(birdVx % 3);
                if (!b.enabled) continue;
                float bx = Mathf.Round(birdX - i * 7), by = Mathf.Round(birdY + (i % 2 == 1 ? 1 : -1) * Mathf.Ceil(i / 2f) * 4);
                bool flap = Mathf.FloorToInt(time * 6 + i) % 2 == 1;
                b.color = new Color32(42, 36, 32, 255);
                if (flap) Place(b, bx, by, 3, 1); else Place(b, bx, by + 1, 3, 1);
            }
        }

        void Spawn(float x, float y, bool dark)
        {
            var sr = particlePool.Count > 0 ? particlePool.Pop() : MakeSr("Smoke", layerFx, NatureArt.White, OrderSmoke);
            sr.enabled = true;
            particles.Add(new Particle { Sr = sr, X = x, Y = y, Life = 3.2f, Max = 3.2f, Vx = (Random.value - 0.3f) * 1.5f, Dark = dark });
        }

        void UpdateNight()
        {
            var o = rig.ViewOrigin; var s = rig.ViewSize;
            float n = NightAlpha();
            night.enabled = n > 0;
            if (n > 0) { night.color = new Color(16 / 255f, 20 / 255f, 56 / 255f, 0.5f * n); Place(night, o.x - 4, o.y - 4, s.x + 8, s.y + 8); }
            float t = town.TimeOfDay;
            bool dusk = t > 0.72f && t < 0.88f;
            sunset.enabled = dusk;
            if (dusk)
            {
                float k = 1 - Mathf.Abs(t - 0.8f) / 0.08f;
                sunset.color = new Color(1f, 140 / 255f, 60 / 255f, 0.09f * k);
                Place(sunset, o.x - 4, o.y - 4, s.x + 8, s.y + 8);
            }
        }

        // ================================================================== selection, ghost & picking
        void UpdateSelection()
        {
            foreach (var e in selEdges) e.enabled = false;
            selMarker.enabled = false;
            if (GameController.Instance == null) return;
            var sel = GameController.Instance.Selected;
            if (sel.Kind == SelectionKind.Building)
            {
                var b = town.GetBuilding(sel.Id); if (b == null) return;
                var d = b.Def; int lift = BuildingArt.LiftOf(b.Type);
                float x = b.X * 16, y = b.Y * 16 - lift, w = d.W * 16, h = d.H * 16 + lift;
                var gold = new Color32(255, 215, 122, 255);
                Place(selEdges[0], x, y, w, 1); Place(selEdges[1], x, y + h - 1, w, 1);
                Place(selEdges[2], x, y, 1, h); Place(selEdges[3], x + w - 1, y, 1, h);
                foreach (var e in selEdges) { e.enabled = true; e.color = gold; }
            }
            else if (sel.Kind == SelectionKind.Person)
            {
                Vector2 pos;
                if (!TryGetPersonPos(sel.Id, out pos) || IsInside(sel.Id)) return;
                float bob = Mathf.FloorToInt(time * 3) % 2;
                selMarker.enabled = true;
                selMarker.color = new Color32(255, 215, 122, 255);
                Place(selMarker, Mathf.Round(pos.x) - 1, Mathf.Round(pos.y) - 22 - bob, 3, 2);
            }
        }

        public void ShowGhost(BuildingType type, int tx, int ty, bool ok)
        {
            var d = Defs.Get(type);
            ghostFoot.enabled = true;
            ghostFoot.color = ok ? new Color(156 / 255f, 214 / 255f, 110 / 255f, 0.38f) : new Color(232 / 255f, 96 / 255f, 70 / 255f, 0.45f);
            Place(ghostFoot, tx * 16, ty * 16, d.W * 16, d.H * 16);
            if (type == BuildingType.Road) { ghostBase.enabled = ghostTop.enabled = false; return; }
            var art = BuildingArt.Get(type, 0, town.Season);
            ghostBase.enabled = ghostTop.enabled = true;
            ghostBase.sprite = art.Base[0]; ghostTop.sprite = art.Top;
            ghostBase.color = ghostTop.color = new Color(1, 1, 1, 0.7f);
            Place(ghostBase, tx * 16, ty * 16 - art.Lift); Place(ghostTop, tx * 16, ty * 16 - art.Lift);
        }

        public void ShowClearMarker(int tx, int ty, int w, int h)
        {
            ghostBase.enabled = ghostTop.enabled = false;
            ghostFoot.enabled = true;
            ghostFoot.color = new Color(232 / 255f, 96 / 255f, 70 / 255f, 0.45f);
            Place(ghostFoot, tx * 16, ty * 16, w * 16, h * 16);
        }

        public void HideGhost() { if (ghostBase == null) return; ghostBase.enabled = ghostTop.enabled = ghostFoot.enabled = false; }

        /// <summary>Finds the settler or building under a point (map pixels).</summary>
        public Selection PickAt(Vector2 w)
        {
            int best = -1; float bd = 7;
            foreach (var kv in people)
            {
                var v = kv.Value; if (float.IsNaN(v.Pos.x) || v.Inside) continue;
                float dd = Vector2.Distance(new Vector2(v.Pos.x, v.Pos.y - 8), w);
                if (dd < bd) { bd = dd; best = kv.Key; }
            }
            if (best >= 0) return Selection.Person(best);
            Building hit = null; float hitFoot = -1;
            foreach (var b in town.Buildings)
            {
                if (b.Type == BuildingType.Road) continue;
                var d = b.Def; int lift = BuildingArt.LiftOf(b.Type);
                float x0 = b.X * 16, y0 = b.Y * 16 - lift, x1 = x0 + d.W * 16, y1 = (b.Y + d.H) * 16;
                if (w.x < x0 || w.x >= x1 || w.y < y0 || w.y >= y1) continue;
                if (w.y < b.Y * 16 && b.Built && !ArtFor(b).TopCanvas.IsOpaque((int)(w.x - x0), (int)(w.y - y0))) continue;
                if (y1 > hitFoot) { hitFoot = y1; hit = b; }
            }
            return hit != null ? Selection.Building(hit.Id) : Selection.None;
        }
    }
}
