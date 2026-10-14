using System;
using System.Collections.Generic;
using System.Linq;

namespace FrontierLedger.Simulation
{
    /// <summary>
    /// The whole settlement: map, buildings, settlers, goods and the town books. Engine-free so it can be
    /// unit tested and saved as plain JSON. The Unity layer reads it and calls Advance / Place / Remove.
    /// </summary>
    [Serializable]
    public partial class Town
    {
        public int Version = 2;
        public int Seed;
        public int Day = 1;
        public float TimeOfDay = 0.3f;           // 0 = midnight, 0.5 = noon
        public float Treasury;
        public TownPolicy Policy = new TownPolicy();
        public int NextId = 1;

        public byte[] Terrain;
        public List<Building> Buildings = new List<Building>();
        public List<Citizen> People = new List<Citizen>();
        public List<LogEntry> Log = new List<LogEntry>();

        public int MayorId = -1;
        public Platform MayorPlatform = Platform.None;
        public int TermEnd;
        public Campaign Campaign = new Campaign();
        public ElectionResult LastElection = new ElectionResult();

        public TownLedger LedgerThisWeek = new TownLedger();
        public TownLedger LedgerLastWeek = new TownLedger();
        public bool HasLastWeek;
        public int CrimesWeek, CrimesLast, CaughtWeek, CaughtLast;
        public int Births, Deaths, Unburied;
        public List<HistoryPoint> History = new List<HistoryPoint>();
        public MarketReport Market = new MarketReport();

        // ---- runtime only ----
        [NonSerialized] Random rng;
        [NonSerialized] int[] occ;
        [NonSerialized] bool occDirty = true;
        [NonSerialized] public int MapVersion = 1;
        [NonSerialized] public List<int> DirtyTiles = new List<int>();
        /// <summary>Fired after each simulated day.</summary>
        [NonSerialized] public Action<int> DayEnded;

        public const int Cols = Balance.Cols, Rows = Balance.Rows;

        Random Rng { get { if (rng == null) rng = new Random(Seed ^ (Day * 7919)); return rng; } }

        /// <summary>Call after loading from JSON to rebuild runtime caches.</summary>
        public void AfterLoad()
        {
            rng = null; occDirty = true; MapVersion++;
            if (DirtyTiles == null) DirtyTiles = new List<int>();
            if (Campaign == null) Campaign = new Campaign();
            if (LastElection == null) LastElection = new ElectionResult();
            foreach (var p in People) if (p.Role == Role.Builder) { p.Role = Role.Laborer; p.WorkId = -1; }   // the old building crew
            foreach (var b in Buildings)
            {
                if (b.Stock == null || b.Stock.Length != 5) b.Stock = new float[5];
                if (b.Price == null || b.Price.Length != 5) b.Price = new float[5];
                if (b.Delivered == null || b.Delivered.Length != 5) b.Delivered = new float[5];
            }
        }

        // =====================================================================
        // Calendar
        // =====================================================================
        public int Season { get { return ((Day - 1) % Balance.DaysPerYear) / Balance.DaysPerSeason; } }
        public int Year { get { return Balance.StartYear + (Day - 1) / Balance.DaysPerYear; } }
        public int DayOfSeason { get { return ((Day - 1) % Balance.DaysPerSeason) + 1; } }
        public string Weekday { get { return Defs.Weekdays[(Day - 1) % 7]; } }
        public string DateLabel { get { return Defs.Seasons[Season] + " " + DayOfSeason + ", " + Year; } }
        /// <summary>0..1 through the current season.</summary>
        public float SeasonProgress { get { return (DayOfSeason - 1 + TimeOfDay) / Balance.DaysPerSeason; } }
        /// <summary>0 in daylight, rising to 1 at the darkest point of night.</summary>
        public float Nightness
        {
            get
            {
                float t = TimeOfDay;
                if (t < 0.15f) return (0.15f - t) / 0.15f;
                if (t > 0.82f) return Math.Min(1f, (t - 0.82f) / 0.12f);
                return 0f;
            }
        }

        // =====================================================================
        // Small helpers
        // =====================================================================
        float Rnd(float a, float b) { return a + (float)Rng.NextDouble() * (b - a); }
        bool Chance(double p) { return Rng.NextDouble() < p; }
        T Pick<T>(IList<T> list) { return list[Rng.Next(list.Count)]; }
        List<T> Shuffle<T>(List<T> a)
        {
            for (int i = a.Count - 1; i > 0; i--) { int j = Rng.Next(i + 1); var t = a[i]; a[i] = a[j]; a[j] = t; }
            return a;
        }
        static float Clamp(float v, float a, float b) { return v < a ? a : v > b ? b : v; }

        public Building GetBuilding(int id) { if (id < 0) return null; for (int i = 0; i < Buildings.Count; i++) if (Buildings[i].Id == id) return Buildings[i]; return null; }
        public Citizen GetCitizen(int id) { if (id < 0) return null; for (int i = 0; i < People.Count; i++) if (People[i].Id == id) return People[i]; return null; }
        public IEnumerable<Building> OfType(BuildingType t) { return Buildings.Where(b => b.Type == t && b.Built); }
        public IEnumerable<Building> Businesses { get { return Buildings.Where(b => b.Built && b.Def.IsBusiness); } }
        public IEnumerable<Building> Sites { get { return Buildings.Where(b => !b.Built); } }
        public IEnumerable<Building> Storages { get { return Buildings.Where(b => b.Built && b.Def.IsStorage); } }
        public IEnumerable<Citizen> Adults { get { return People.Where(p => !p.IsChild); } }
        public Building Office { get { return OfType(BuildingType.Sheriff).FirstOrDefault(); } }
        public Building Estate { get { return OfType(BuildingType.Estate).FirstOrDefault(); } }
        public Building OwnedBusiness(Citizen p) { return Buildings.FirstOrDefault(b => b.Def.IsBusiness && b.OwnerId == p.Id); }
        public Citizen Mayor { get { return GetCitizen(MayorId); } }
        bool MayorBonus(Platform p) { return MayorId >= 0 && MayorPlatform == p; }

        public float AverageHappiness { get { return People.Count == 0 ? 50f : People.Average(p => p.Happy); } }
        public float AverageHealth { get { return People.Count == 0 ? 100f : People.Average(p => p.Health); } }
        public int FreeBeds { get { return OfType(BuildingType.House).Sum(h => h.Def.Beds - h.Residents.Count); } }
        public int Homeless { get { return People.Count(p => p.HomeId < 0); } }
        public float AverageFoodPrice
        {
            get
            {
                var r = Buildings.Where(b => b.Built && b.Def.IsRetail && b.OwnerId >= 0).ToList();
                return r.Count == 0 ? (Policy.StorehouseSells ? Policy.StorehousePrice[0] : 0f) : r.Average(b => b.Price[0]);
            }
        }

        public int WorkersAt(Building b)
        {
            int n = 0;
            foreach (var id in b.Employees) { var p = GetCitizen(id); if (p != null && p.Jail == 0) n++; }
            var o = GetCitizen(b.OwnerId);
            if (o != null && o.WorkId == b.Id && o.Jail == 0) n++;
            return n;
        }
        public bool IsOpen(Building b) { return b.Built && WorkersAt(b) > 0; }

        void AddLog(string msg, LogKind kind = LogKind.Info)
        {
            Log.Insert(0, new LogEntry { Day = Day, Message = msg, Kind = kind });
            if (Log.Count > 250) Log.RemoveRange(250, Log.Count - 250);
        }

        public static string Money(float v)
        {
            float a = Math.Abs(v);
            string s = a >= 1000 ? Math.Round(a).ToString("#,0") : a.ToString("0.00");
            return (v < 0 ? "-$" : "$") + s;
        }

        // =====================================================================
        // Town stores
        // =====================================================================
        public float TownStock(Good g) { float s = 0; foreach (var b in Storages) s += b.Stock[(int)g]; return s; }
        public float TownCapacity { get { float c = 0; foreach (var b in Storages) c += b.Def.Capacity; return c; } }
        public float TownStored { get { float s = 0; foreach (var b in Storages) for (int i = 0; i < 5; i++) s += b.Stock[i]; return s; } }
        public float TownSpace { get { return Math.Max(0, TownCapacity - TownStored); } }

        float TakeFromTown(Good g, float q)
        {
            float taken = 0;
            foreach (var b in Storages)
            {
                if (q - taken <= 0) break;
                float t = Math.Min(b.Stock[(int)g], q - taken);
                b.Stock[(int)g] -= t; taken += t;
            }
            return taken;
        }

        float PutInTown(Good g, float q)
        {
            float put = 0;
            foreach (var b in Storages)
            {
                float free = b.Def.Capacity - b.Stock.Sum();
                float p = Math.Min(free, q - put);
                if (p <= 0) continue;
                b.Stock[(int)g] += p; put += p;
                if (put >= q) break;
            }
            return put;
        }

        /// <summary>Which business type makes a good.</summary>
        public static bool Produces(BuildingType t, Good g)
        {
            switch (g)
            {
                case Good.Food: return t == BuildingType.Farm || t == BuildingType.Gatherer || t == BuildingType.Fishery;
                case Good.Firewood: return t == BuildingType.Woodcutter;
                case Good.Logs: return t == BuildingType.LumberCamp;
                case Good.Stone: return t == BuildingType.Quarry;
                case Good.Tools: return t == BuildingType.Smithy;
            }
            return false;
        }

        /// <summary>Buys a good from the businesses that make it, at wholesale. Returns the amount bought
        /// and lowers <paramref name="budget"/> by what was paid.</summary>
        float BuyFromProducers(Good g, float qty, ref float budget, Building buyer)
        {
            float price = Balance.Wholesale[(int)g], bought = 0;
            if (qty <= 0 || budget < price * 0.1f) return 0;
            foreach (var s in Businesses.Where(b => b != buyer && Produces(b.Type, g) && b.Stock[(int)g] > 0.01f).OrderByDescending(b => b.Stock[(int)g]))
            {
                float q = Math.Min(Math.Min(qty - bought, s.Stock[(int)g]), budget / price);
                if (q <= 0.001f) break;
                float c = q * price;
                s.Stock[(int)g] -= q; s.Cash += c; s.Week.Revenue += c; s.Today.Sold += q; s.Week.Sold += q;
                budget -= c; bought += q;
                if (bought >= qty - 0.001f) break;
            }
            return bought;
        }

        // =====================================================================
        // Map
        // =====================================================================
        public byte TerrainAt(int x, int y) { return x < 0 || y < 0 || x >= Cols || y >= Rows ? Tile.OutOfBounds : Terrain[y * Cols + x]; }
        void SetTerrain(int x, int y, byte v) { if (TerrainAt(x, y) == Tile.OutOfBounds) return; Terrain[y * Cols + x] = v; MarkDirty(x, y); }
        void MarkDirty(int x, int y) { DirtyTiles.Add(y * Cols + x); MapVersion++; }
        void MarkDirty(Building b) { var d = b.Def; for (int dx = 0; dx < d.W; dx++) for (int dy = 0; dy < d.H; dy++) MarkDirty(b.X + dx, b.Y + dy); occDirty = true; }
        public static bool IsTreeish(byte t) { return t == Tile.Tree || t == Tile.Sapling; }

        void RebuildOcc()
        {
            if (occ == null) occ = new int[Cols * Rows];
            for (int i = 0; i < occ.Length; i++) occ[i] = -1;
            for (int i = 0; i < Buildings.Count; i++)
            {
                var b = Buildings[i]; var d = b.Def;
                for (int dx = 0; dx < d.W; dx++)
                    for (int dy = 0; dy < d.H; dy++)
                    {
                        int x = b.X + dx, y = b.Y + dy;
                        if (x >= 0 && y >= 0 && x < Cols && y < Rows) occ[y * Cols + x] = i;
                    }
            }
            occDirty = false;
        }

        public Building OccupantAt(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Cols || y >= Rows) return null;
            if (occDirty || occ == null) RebuildOcc();
            int i = occ[y * Cols + x];
            return i >= 0 && i < Buildings.Count ? Buildings[i] : null;
        }
        public bool IsRoad(int x, int y) { var b = OccupantAt(x, y); return b != null && b.Type == BuildingType.Road; }

        static float ClearCostOf(byte t) { return t == Tile.Tree || t == Tile.Sapling ? Balance.ClearTree : t == Tile.Rock ? Balance.ClearRock : t == Tile.Stump ? 0.5f : 0f; }
        public float ClearCost(BuildingType type, int x, int y)
        {
            var d = Defs.Get(type); float c = 0;
            for (int dx = 0; dx < d.W; dx++) for (int dy = 0; dy < d.H; dy++) c += ClearCostOf(TerrainAt(x + dx, y + dy));
            return c;
        }

        /// <summary>Clears a tree, sapling, stump or boulder for a small fee. Felled trees go to town stores as logs.</summary>
        public string ClearTile(int x, int y)
        {
            byte t = TerrainAt(x, y); float c = ClearCostOf(t);
            if (c <= 0) return null;
            if (Treasury < c) return "Clearing that costs " + Money(c);
            Treasury -= c; LedgerThisWeek.Construction += c;
            if (t == Tile.Tree) PutInTown(Good.Logs, 2);
            if (t == Tile.Rock) PutInTown(Good.Stone, 3);
            SetTerrain(x, y, Tile.Grass);
            return null;
        }

        int CountInRadius(int cx, int cy, int r, Func<byte, bool> pred)
        {
            int n = 0;
            for (int y = cy - r; y <= cy + r; y++)
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) > r * r) continue;
                    if (pred(TerrainAt(x, y))) n++;
                }
            return n;
        }
        public int TreesNear(Building b) { var d = b.Def; return CountInRadius(b.X + d.W / 2, b.Y + d.H / 2, d.Radius, t => t == Tile.Tree); }

        bool TouchesWater(int x, int y, int w, int h)
        {
            for (int dx = -1; dx <= w; dx++)
                for (int dy = -1; dy <= h; dy++)
                {
                    bool edge = dx == -1 || dy == -1 || dx == w || dy == h;
                    if (edge && TerrainAt(x + dx, y + dy) == Tile.Water) return true;
                }
            return false;
        }
        bool NearStone(int x, int y, int w, int h, int r)
        {
            for (int dx = -1; dx <= w; dx++)
                for (int dy = -1; dy <= h; dy++)
                    if (TerrainAt(x + dx, y + dy) == Tile.Hill) return true;
            return CountInRadius(x + w / 2, y + h / 2, r, t => t == Tile.Rock) > 0;
        }

        /// <summary>Returns why a building can't go here, or null if it can.</summary>
        public string CanPlace(BuildingType type, int x, int y, bool free = false)
        {
            var d = Defs.Get(type);
            if (x < 0 || y < 0 || x + d.W > Cols || y + d.H > Rows) return "Too close to the edge of the map";
            for (int dx = 0; dx < d.W; dx++)
                for (int dy = 0; dy < d.H; dy++)
                {
                    if (OccupantAt(x + dx, y + dy) != null) return "That ground is already taken";
                    byte t = TerrainAt(x + dx, y + dy);
                    if (t == Tile.Hill) return "Too steep. Nothing can be built on the hills";
                    if (t == Tile.Water && type != BuildingType.Road) return "Only a road can bridge the water";
                }
            if (d.Unique && Buildings.Any(b => b.Type == type)) return "The town already has a " + d.Name;
            if (d.NeedsWater && !TouchesWater(x, y, d.W, d.H)) return "A fishing dock has to touch the river or lake";
            if (d.NeedsStone && !NearStone(x, y, d.W, d.H, d.Radius)) return "A quarry needs a hillside next to it or boulders nearby";
            float money = d.Money + ClearCost(type, x, y);
            if (!free && Treasury < money) return "The treasury needs " + Money(money) + " to clear the ground";
            return null;
        }

        /// <summary>Lays out a construction site (or a road, which is instant).
        /// Returns the new building, or null with an error message.</summary>
        public Building Place(BuildingType type, int x, int y, out string error, bool free = false)
        {
            error = CanPlace(type, x, y, free);
            if (error != null) return null;
            var d = Defs.Get(type);
            float money = d.Money + ClearCost(type, x, y);
            if (!free) { Treasury -= money; LedgerThisWeek.Construction += money; }
            for (int dx = 0; dx < d.W; dx++)
                for (int dy = 0; dy < d.H; dy++)
                {
                    byte t = TerrainAt(x + dx, y + dy);
                    if (t == Tile.Tree || t == Tile.Rock || t == Tile.Sapling || t == Tile.Stump) Terrain[(y + dy) * Cols + x + dx] = Tile.Grass;
                }
            var b = new Building { Id = NextId++, Type = type, X = x, Y = y, BuiltDay = Day, Built = free || d.Instant };
            if (b.Built) { b.Progress = d.Work; b.Delivered[(int)Good.Logs] = d.Logs; b.Delivered[(int)Good.Stone] = d.Stone; }
            Buildings.Add(b);
            MarkDirty(b);
            if (b.Built) OnBuilt(b, free);
            else AddLog("Builders staked out a " + d.Name + ". It needs " + MaterialsText(d) + ".", LogKind.Build);
            return b;
        }

        public static string MaterialsText(BuildingDef d)
        {
            var parts = new List<string>();
            if (d.Logs > 0) parts.Add(d.Logs + " logs");
            if (d.Stone > 0) parts.Add(d.Stone + " stone");
            return parts.Count == 0 ? "no materials" : string.Join(" and ", parts.ToArray());
        }

        void OnBuilt(Building b, bool quiet)
        {
            var d = b.Def;
            b.Built = true; b.BuiltDay = Day; MarkDirty(b);
            if (d.IsBusiness)
            {
                for (int i = 0; i < 5; i++) b.Price[i] = Balance.StartRetail[i];
                b.Wage = Math.Max(Policy.MinWage, d.BaseWage);
                b.Desired = d.MaxEmployees > 1 ? 1 : 0;
            }
            if (!quiet && b.Type != BuildingType.Road) AddLog("The " + d.Name + " is finished.", LogKind.Build);
            if (b.Type == BuildingType.House) RehouseEveryone();
        }

        public void Remove(Building b)
        {
            var d = b.Def;
            if (b.Type == BuildingType.Estate)
            {
                var m = Mayor; if (m != null) UnseatMayor(m, false);
                Campaign.Active = false;
            }
            foreach (var id in b.Residents.ToList()) { var p = GetCitizen(id); if (p != null) p.HomeId = -1; }
            foreach (var id in b.Employees.ToList()) { var p = GetCitizen(id); if (p != null) { p.WorkId = -1; p.Role = Role.Laborer; } }
            foreach (var p in People) if (p.WorkId == b.Id && p.Role == Role.Builder) p.WorkId = -1;
            if (d.IsBusiness && b.OwnerId >= 0)
            {
                var o = GetCitizen(b.OwnerId);
                if (o != null) { o.Money += b.Cash * 0.5f; if (o.WorkId == b.Id) { o.WorkId = -1; o.Role = Role.Laborer; } }
            }
            Buildings.Remove(b);
            MarkDirty(b);
            // Salvage: half the materials go back to town stores, plus anything that was stored here
            PutInTown(Good.Logs, b.Delivered[(int)Good.Logs] * 0.5f);
            PutInTown(Good.Stone, b.Delivered[(int)Good.Stone] * 0.5f);
            if (d.IsStorage) for (int g = 0; g < 5; g++) PutInTown((Good)g, b.Stock[g]);
            if (b.Type != BuildingType.Road) AddLog("The " + d.Name + (b.Built ? " was torn down." : " site was cleared."), LogKind.Bad);
            RehouseEveryone();
        }

        // =====================================================================
        // Time
        // =====================================================================
        /// <summary>Advance the clock by a fraction of a day; simulates each day that completes.</summary>
        public void Advance(float days)
        {
            TimeOfDay += days;
            while (TimeOfDay >= 1f)
            {
                TimeOfDay -= 1f;
                SimulateDay();
                Day++;
                if (DayEnded != null) DayEnded(Day);
            }
        }

        // =====================================================================
        // New town
        // =====================================================================
        public const int WagonX = 37, WagonY = 22;

        /// <summary>A handful of families arrive with a supply wagon in early spring. No houses yet.</summary>
        public static Town CreateNew(int seed)
        {
            var t = new Town { Seed = seed, Treasury = Balance.StartingTreasury };
            t.Terrain = TerrainGen.Generate(seed, Cols, Rows);
            // Open meadow around the camp
            for (int y = 18; y <= 29; y++)
                for (int x = 29; x <= 47; x++)
                {
                    double d = Math.Sqrt(Math.Pow((x - 38) / 1.5, 2) + Math.Pow(y - 23.5, 2));
                    if (d < 6.5 && t.TerrainAt(x, y) != Tile.Water) t.Terrain[y * Cols + x] = Tile.Grass;
                }
            // The wagon trail they came in on
            string err;
            for (int x = 0; x < Cols; x++)
            {
                if (t.TerrainAt(x, 24) != Tile.Water) t.Terrain[24 * Cols + x] = Tile.Grass;
                t.Place(BuildingType.Road, x, 24, out err, true);
            }
            var wagon = t.Place(BuildingType.Wagon, WagonX, WagonY, out err, true);
            for (int g = 0; g < 5; g++) wagon.Stock[g] = Balance.StartingStock[g];

            // Four families
            for (int f = 0; f < 4; f++)
            {
                string fam = t.Pick(Defs.LastNames);
                var dad = t.AddPerson(false, fam, t.Rnd(22, 40), (float)Math.Round(t.Rnd(25, 60)));
                var mom = t.AddPerson(true, fam, t.Rnd(20, 36), (float)Math.Round(t.Rnd(10, 30)));
                dad.PartnerId = mom.Id; mom.PartnerId = dad.Id;
                int kids = f == 0 ? 0 : t.Rng.Next(1, 3);
                for (int k = 0; k < kids; k++) { var c = t.AddPerson(t.Chance(0.5), fam, t.Rnd(1, 9), 0); c.ParentId = mom.Id; }
            }
            // and two single folk
            t.AddPerson(false, t.Pick(Defs.LastNames), t.Rnd(18, 30), (float)Math.Round(t.Rnd(15, 40)));
            t.AddPerson(true, t.Pick(Defs.LastNames), t.Rnd(18, 30), (float)Math.Round(t.Rnd(15, 40)));

            t.Hiring();
            t.DirtyTiles.Clear();
            t.Log.Clear();
            t.AddLog("Four families and two drifters stopped by the creek with what one wagon could carry.", LogKind.Good);
            t.AddLog("Get cottages up before winter, and a woodcutter to keep them warm. Food runs out faster than you think.", LogKind.Info);
            return t;
        }
    }
}
