using System.Collections.Generic;
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
    /// Draws the town with SpriteRenderers: ground, buildings, trees, settlers, chickens,
    /// chimney smoke, water sparkle, cloud shadows, birds, sunset and night.
    /// Positions are worked out in the prototype's pixel space (y down) and converted to world units.
    /// </summary>
    public sealed class WorldView : MonoBehaviour
    {
        // Sorting orders
        const int OrderGround = -32000, OrderBase = -31000, OrderSmoke = 29000, OrderClouds = 29500,
                  OrderNight = 30000, OrderLights = 30010, OrderSelect = 31000, OrderGhost = 31010;

        Town town;
        CameraRig rig;
        GroundPainter painter;
        Texture2D groundTex;
        SpriteRenderer ground;
        int paintedSeason = -1, seenMapVersion = -1;
        float time;

        sealed class BuildingView
        {
            public Building B; public GameObject Go; public SpriteRenderer Base, Top, Lights, Forge;
            public List<SpriteRenderer> Chickens = new List<SpriteRenderer>(); public int Season = -1; public BuildingSprites Art;
        }
        sealed class PersonView { public Citizen P; public SpriteRenderer Sr, Shadow; public float X = float.NaN, Y; public bool Moving; public float Step; }
        sealed class Particle { public SpriteRenderer Sr; public float X, Y, Life, Max, Vx; public bool Dark; }
        sealed class Cloud { public SpriteRenderer Sr; public float X, Y; public int Rx; }

        readonly Dictionary<int, BuildingView> buildings = new Dictionary<int, BuildingView>();
        readonly Dictionary<int, SpriteRenderer> trees = new Dictionary<int, SpriteRenderer>();
        readonly Dictionary<int, PersonView> people = new Dictionary<int, PersonView>();
        readonly List<Particle> particles = new List<Particle>();
        readonly Stack<SpriteRenderer> particlePool = new Stack<SpriteRenderer>();
        readonly Dictionary<int, float> smokeTimers = new Dictionary<int, float>();
        readonly List<SpriteRenderer> sparkles = new List<SpriteRenderer>();
        readonly List<Cloud> clouds = new List<Cloud>();
        readonly List<SpriteRenderer> birds = new List<SpriteRenderer>();
        float birdX, birdY, birdVx; bool birdsActive;
        SpriteRenderer night, sunset;
        SpriteRenderer[] selEdges = new SpriteRenderer[4];
        SpriteRenderer selMarker;
        SpriteRenderer ghostBase, ghostTop, ghostFoot;
        Transform layerBuildings, layerTrees, layerPeople, layerFx;

        // ================================================================== setup
        public void Bind(Town t, CameraRig cameraRig)
        {
            town = t; rig = cameraRig;
            foreach (Transform c in transform) Destroy(c.gameObject);
            buildings.Clear(); trees.Clear(); people.Clear(); particles.Clear(); particlePool.Clear(); smokeTimers.Clear();
            sparkles.Clear(); clouds.Clear(); birds.Clear();
            paintedSeason = -1; seenMapVersion = -1;

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
            for (int i = 0; i < 140; i++) { var s = MakeSr("Sparkle", layerFx, NatureArt.White, OrderBase + 1); s.color = new Color32(212, 236, 245, 255); s.enabled = false; sparkles.Add(s); }
            for (int i = 0; i < 5; i++)
            {
                int rx = Random.Range(40, 90), ry = Random.Range(18, 34);
                var c = new Cloud { Sr = MakeSr("Cloud", layerFx, CloudSprite(rx, ry), OrderClouds), X = Random.Range(0f, Town.Cols * 16f), Y = Random.Range(0f, Town.Rows * 16f), Rx = rx };
                clouds.Add(c);
            }
            for (int i = 0; i < 5; i++) { var b = MakeSr("Bird", layerFx, NatureArt.White, OrderClouds + 1); b.enabled = false; birds.Add(b); }
            HideGhost(); SetSelection(Selection.None);
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
            SyncPeople();
            UpdateEffects(si);
            UpdateNight();
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
            switch (b.Type)
            {
                case BuildingType.House: return b.Residents.Count > 0;
                case BuildingType.Estate: return town.MayorId >= 0;
                case BuildingType.Sheriff: return b.Employees.Count > 0;
                default: return b.Def.IsBusiness && town.IsOpen(b) && (b.Type == BuildingType.Saloon || town.TimeOfDay > 0.75f);
            }
        }

        void SyncBuildings(int si)
        {
            var alive = new HashSet<int>();
            float n = town.Nightness;
            int cropFrame = Mathf.FloorToInt(time * 1.1f) % 2;
            foreach (var b in town.Buildings)
            {
                if (b.Type == BuildingType.Road) continue;
                alive.Add(b.Id);
                BuildingView v;
                if (!buildings.TryGetValue(b.Id, out v)) { v = CreateBuildingView(b); buildings[b.Id] = v; }
                if (v.Season != si)
                {
                    v.Art = ArtFor(b); v.Season = si;
                    v.Top.sprite = v.Art.Top; v.Lights.sprite = v.Art.Lights;
                }
                v.Base.sprite = v.Art.Base[cropFrame];
                bool lit = Lit(b) && n > 0;
                v.Lights.enabled = lit;
                if (lit) v.Lights.color = new Color(1, 1, 1, n);
                if (v.Forge != null)
                {
                    bool open = town.IsOpen(b);
                    v.Forge.enabled = open;
                    if (open)
                    {
                        string[] fl = { "#ff9a3a", "#ffc35a", "#e0601e", "#ff8a2a" };
                        v.Forge.color = PixelCanvas.C(fl[(Mathf.FloorToInt(time * 9) + b.Id) % 4]);
                    }
                }
                for (int i = 0; i < v.Chickens.Count; i++)
                {
                    var spot = v.Art.Chickens[i];
                    float t = time * 0.6f + i * 2.1f + b.Id;
                    float cx = b.X * 16 + spot.x + Mathf.Round(Mathf.Sin(t) * 3), cy = b.Y * 16 + spot.y + Mathf.Round(Mathf.Sin(t * 0.7f + 1) * 2);
                    var sr = v.Chickens[i];
                    sr.sprite = NatureArt.Chicken(Mathf.FloorToInt(time * 2 + i) % 3 == 0 ? 1 : 0);
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
            var v = new BuildingView { B = b, Art = ArtFor(b), Season = town.Season };
            v.Go = new GameObject(d.Name + " #" + b.Id);
            v.Go.transform.SetParent(layerBuildings, false);
            v.Go.transform.position = Pixels.ToWorld(b.X * 16, b.Y * 16 - v.Art.Lift);
            int foot = (b.Y + d.H) * 16;
            v.Base = MakeSr("Base", v.Go.transform, v.Art.Base[0], OrderBase);
            v.Top = MakeSr("Top", v.Go.transform, v.Art.Top, SortOrder(foot));
            v.Lights = MakeSr("Lights", v.Go.transform, v.Art.Lights, OrderLights);
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
        void SyncTrees(int si)
        {
            if (trees.Count > 0 && treeSeason == si && treeMapVersion == town.MapVersion) return;
            bool reseason = treeSeason != si;
            treeSeason = si; treeMapVersion = town.MapVersion;
            var want = new HashSet<int>();
            for (int ty = 0; ty < Town.Rows; ty++)
                for (int tx = 0; tx < Town.Cols; tx++)
                {
                    byte t = town.TerrainAt(tx, ty);
                    if (t != Tile.Tree && t != Tile.Rock) continue;
                    int k = ty * Town.Cols + tx; want.Add(k);
                    SpriteRenderer sr;
                    bool exists = trees.TryGetValue(k, out sr);
                    if (exists && !reseason) continue;
                    var pl = NatureArt.At(tx, ty, t, si);
                    if (!exists) { sr = MakeSr("Tree", layerTrees, pl.Sprite, SortOrder((ty + 1) * 16 - 1)); trees[k] = sr; }
                    sr.sprite = pl.Sprite;
                    sr.transform.position = Pixels.ToWorld(pl.Px, pl.Py);
                }
            if (want.Count != trees.Count)
            {
                var gone = new List<int>();
                foreach (var kv in trees) if (!want.Contains(kv.Key)) gone.Add(kv.Key);
                foreach (int k in gone) { Destroy(trees[k].gameObject); trees.Remove(k); }
            }
        }
        int treeSeason = -1, treeMapVersion = -1;

        // ---------------------------------------------------------------- settlers
        Vector2 SpotIn(Building b, Citizen p, bool garden)
        {
            var d = b.Def; float a = p.Seed, c = (p.Seed * 7.31f) % 1f;
            if (garden) return new Vector2(b.X * 16 + 5 + a * (d.W * 16 - 10), b.Y * 16 + 6 + c * 6);
            return new Vector2(b.X * 16 + 3 + a * (d.W * 16 - 6), (b.Y + d.H) * 16 + 1 + c * 3);
        }

        Vector2 TargetFor(Citizen p)
        {
            float ph = town.TimeOfDay; Building b; bool garden = false;
            if (p.Jail > 0) b = town.Office ?? town.GetBuilding(p.HomeId);
            else if (ph > 0.22f && ph < 0.7f && p.WorkId >= 0) b = town.GetBuilding(p.WorkId);
            else if (ph > 0.66f && ph < 0.82f && p.SaloonToday >= 0) b = town.GetBuilding(p.SaloonToday);
            else
            {
                b = town.GetBuilding(p.HomeId);
                if (p.Role == Role.Unemployed && ph > 0.25f && ph < 0.65f && b != null && b.Type == BuildingType.House) garden = true;
            }
            if (b == null) return new Vector2((28 + p.Seed * 14) * 16, 24 * 16 + 9);
            return SpotIn(b, p, garden);
        }

        void SyncPeople()
        {
            float speed = 16 * 5 * Mathf.Max(1, GameController.Instance != null ? GameController.Instance.Speed : 1) * Time.deltaTime;
            var alive = new HashSet<int>();
            foreach (var p in town.People)
            {
                alive.Add(p.Id);
                PersonView v;
                if (!people.TryGetValue(p.Id, out v))
                {
                    v = new PersonView { P = p };
                    v.Sr = MakeSr(p.Name, layerPeople, null, 0);
                    v.Shadow = MakeSr("Shadow", v.Sr.transform, NatureArt.White, 0);
                    v.Shadow.color = new Color(0, 0, 0, 0.3f);
                    people[p.Id] = v;
                }
                v.P = p;
                var t = TargetFor(p);
                if (float.IsNaN(v.X)) { v.X = t.x; v.Y = t.y; v.Moving = false; }
                else
                {
                    float dx = t.x - v.X, dy = t.y - v.Y;
                    if (Mathf.Abs(dx) <= speed && Mathf.Abs(dy) <= speed) { v.X = t.x; v.Y = t.y; v.Moving = false; }
                    else
                    {
                        v.Moving = true; v.Step += speed;
                        // walk the street grid: across first, then up or down
                        if (Mathf.Abs(dx) > 1) v.X += Mathf.Sign(dx) * Mathf.Min(speed, Mathf.Abs(dx));
                        else v.Y += Mathf.Sign(dy) * Mathf.Min(speed, Mathf.Abs(dy));
                    }
                }
                int frame = v.Moving && Mathf.FloorToInt(v.Step / 4) % 2 == 1 ? 1 : 0;
                float x = Mathf.Round(v.X), y = Mathf.Round(v.Y);
                v.Sr.sprite = NatureArt.Person(p, frame);
                v.Sr.transform.position = Pixels.ToWorld(x + 0.5f, y);
                v.Sr.sortingOrder = SortOrder(y, 2);
                v.Shadow.sortingOrder = v.Sr.sortingOrder - 1;
                v.Shadow.transform.localPosition = Pixels.ToWorld(-2.5f, 0);
                v.Shadow.transform.localScale = new Vector3(5, 1, 1);
            }
            if (alive.Count != people.Count)
            {
                var gone = new List<int>();
                foreach (var kv in people) if (!alive.Contains(kv.Key)) gone.Add(kv.Key);
                foreach (int id in gone) { Destroy(people[id].Sr.gameObject); people.Remove(id); }
            }
        }

        /// <summary>Where a settler is currently drawn, in prototype pixels.</summary>
        public bool TryGetPersonPos(int id, out Vector2 pos)
        {
            PersonView v;
            if (people.TryGetValue(id, out v) && !float.IsNaN(v.X)) { pos = new Vector2(v.X, v.Y); return true; }
            pos = Vector2.zero; return false;
        }

        // ---------------------------------------------------------------- effects
        Rect VisiblePx()
        {
            var o = rig.ViewOrigin; var s = rig.ViewSize;
            return new Rect(o.x - 48, o.y - 48, s.x + 96, s.y + 112);
        }

        void UpdateEffects(int si)
        {
            float dt = Time.deltaTime, nightness = town.Nightness;
            var vr = VisiblePx();
            // chimney and forge smoke
            foreach (var kv in buildings)
            {
                var b = kv.Value.B; var d = b.Def;
                float X = b.X * 16, Y = b.Y * 16;
                if (X > vr.xMax || X + d.W * 16 < vr.xMin || Y > vr.yMax || Y + d.H * 16 < vr.yMin) continue;
                float rate = 0; bool dark = false;
                if (b.Type == BuildingType.House && b.Residents.Count > 0) rate = (nightness > 0 || town.TimeOfDay < 0.3f || si == 3) ? 0.9f : 0.3f;
                else if (b.Type == BuildingType.Smithy && town.IsOpen(b)) { rate = 2.2f; dark = true; }
                else if (b.Type == BuildingType.Estate && town.MayorId >= 0) rate = 0.6f;
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
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                var p = particles[i];
                p.Life -= dt; p.X += (3 + p.Vx) * dt; p.Y -= 7 * dt;
                if (p.Life <= 0) { p.Sr.enabled = false; particlePool.Push(p.Sr); particles.RemoveAt(i); continue; }
                float k = 1 - p.Life / p.Max; int sz = 1 + Mathf.FloorToInt(k * 3);
                p.Sr.color = p.Dark ? new Color(110 / 255f, 104 / 255f, 98 / 255f, 0.6f * (1 - k)) : new Color(226 / 255f, 222 / 255f, 214 / 255f, 0.55f * (1 - k));
                Place(p.Sr, Mathf.Round(p.X - sz / 2f), Mathf.Round(p.Y - sz / 2f), sz, sz);
            }

            // water sparkle
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

            // cloud shadows drift east
            foreach (var c in clouds)
            {
                c.X += 4 * dt;
                if (c.X - c.Rx > Town.Cols * 16) { c.X = -c.Rx; c.Y = Random.Range(0f, Town.Rows * 16f); }
                c.Sr.transform.position = Pixels.ToWorld(Mathf.Round(c.X - c.Rx), Mathf.Round(c.Y - c.Rx / 2f));
            }

            // a small flock now and then
            var view = rig.ViewSize; var org = rig.ViewOrigin;
            if (birdsActive)
            {
                birdX += birdVx * dt;
                if (birdX > org.x + view.x + 60) birdsActive = false;
            }
            else if (nightness < 0.2f && Random.value < dt / 18f)
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
            float n = town.Nightness;
            night.enabled = n > 0;
            if (n > 0) { night.color = new Color(16 / 255f, 20 / 255f, 56 / 255f, 0.5f * n); Place(night, o.x - 4, o.y - 4, s.x + 8, s.y + 8); }
            float t = town.TimeOfDay;
            bool dusk = t > 0.7f && t < 0.86f;
            sunset.enabled = dusk;
            if (dusk)
            {
                float k = 1 - Mathf.Abs(t - 0.78f) / 0.08f;
                sunset.color = new Color(1f, 140 / 255f, 60 / 255f, 0.08f * k);
                Place(sunset, o.x - 4, o.y - 4, s.x + 8, s.y + 8);
            }
        }

        // ================================================================== selection, ghost & picking
        public void SetSelection(Selection sel)
        {
            foreach (var e in selEdges) e.enabled = false;
            selMarker.enabled = false;
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
        }

        void Update()
        {
            if (town == null || GameController.Instance == null) return;
            var sel = GameController.Instance.Selected;
            if (sel.Kind == SelectionKind.Building) SetSelection(sel);
            else
            {
                foreach (var e in selEdges) e.enabled = false;
                Vector2 pos;
                selMarker.enabled = sel.Kind == SelectionKind.Person && TryGetPersonPos(sel.Id, out pos);
                if (selMarker.enabled)
                {
                    TryGetPersonPos(sel.Id, out pos);
                    float bob = Mathf.FloorToInt(time * 3) % 2;
                    selMarker.color = new Color32(255, 215, 122, 255);
                    Place(selMarker, Mathf.Round(pos.x) - 1, Mathf.Round(pos.y) - 16 - bob, 3, 2);
                }
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

        public void HideGhost() { ghostBase.enabled = ghostTop.enabled = ghostFoot.enabled = false; }

        /// <summary>Finds the settler or building under a point (prototype pixels).</summary>
        public Selection PickAt(Vector2 w)
        {
            int best = -1; float bd = 6;
            foreach (var kv in people)
            {
                var v = kv.Value; if (float.IsNaN(v.X)) continue;
                float dd = Vector2.Distance(new Vector2(v.X, v.Y - 5), w);
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
                if (w.y < b.Y * 16 && !ArtFor(b).TopCanvas.IsOpaque((int)(w.x - x0), (int)(w.y - y0))) continue;
                if (y1 > hitFoot) { hitFoot = y1; hit = b; }
            }
            return hit != null ? Selection.Building(hit.Id) : Selection.None;
        }
    }
}
