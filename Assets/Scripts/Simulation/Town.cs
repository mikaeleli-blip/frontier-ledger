using System;
using System.Collections.Generic;
using System.Linq;

namespace FrontierLedger.Simulation
{
    /// <summary>
    /// The whole settlement: map, buildings, settlers and the town books. Engine-free so it can be
    /// unit tested and saved as plain JSON. The Unity layer reads it and calls Advance / Place / Remove.
    /// </summary>
    [Serializable]
    public partial class Town
    {
        public int Version = 1;
        public int Seed;
        public int Day = 1;
        public float TimeOfDay = 0.18f;          // 0 = midnight, 0.5 = noon
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
        }

        // =====================================================================
        // Calendar
        // =====================================================================
        public int Season { get { return ((Day - 1) % Balance.DaysPerYear) / Balance.DaysPerSeason; } }
        public int Year { get { return Balance.StartYear + (Day - 1) / Balance.DaysPerYear; } }
        public int DayOfSeason { get { return ((Day - 1) % Balance.DaysPerSeason) + 1; } }
        public string Weekday { get { return Defs.Weekdays[(Day - 1) % 7]; } }
        public string DateLabel { get { return Weekday + " · " + Defs.Seasons[Season] + " " + DayOfSeason + ", " + Year; } }
        /// <summary>0 in daylight, rising to 1 at the darkest point of night.</summary>
        public float Nightness
        {
            get
            {
                float t = TimeOfDay;
                if (t < 0.12f) return (0.12f - t) / 0.12f;
                if (t > 0.8f) return (t - 0.8f) / 0.2f;
                return 0f;
            }
        }

        // =====================================================================
        // Small helpers
        // =====================================================================
        float Rnd(float a, float b) { return a + (float)Rng.NextDouble() * (b - a); }
        T Pick<T>(IList<T> list) { return list[Rng.Next(list.Count)]; }
        List<T> Shuffle<T>(List<T> a)
        {
            for (int i = a.Count - 1; i > 0; i--) { int j = Rng.Next(i + 1); var t = a[i]; a[i] = a[j]; a[j] = t; }
            return a;
        }
        static float Clamp(float v, float a, float b) { return v < a ? a : v > b ? b : v; }

        public Building GetBuilding(int id) { if (id < 0) return null; for (int i = 0; i < Buildings.Count; i++) if (Buildings[i].Id == id) return Buildings[i]; return null; }
        public Citizen GetCitizen(int id) { if (id < 0) return null; for (int i = 0; i < People.Count; i++) if (People[i].Id == id) return People[i]; return null; }
        public IEnumerable<Building> OfType(BuildingType t) { return Buildings.Where(b => b.Type == t); }
        public IEnumerable<Building> Businesses { get { return Buildings.Where(b => b.Def.IsBusiness); } }
        public Building Office { get { return Buildings.FirstOrDefault(b => b.Type == BuildingType.Sheriff); } }
        public Building Estate { get { return Buildings.FirstOrDefault(b => b.Type == BuildingType.Estate); } }
        public Building OwnedBusiness(Citizen p) { return Buildings.FirstOrDefault(b => b.Def.IsBusiness && b.OwnerId == p.Id); }
        public Citizen Mayor { get { return GetCitizen(MayorId); } }
        bool MayorBonus(Platform p) { return MayorId >= 0 && MayorPlatform == p; }

        public float AverageHappiness { get { return People.Count == 0 ? 50f : People.Average(p => p.Happy); } }
        public int FreeBeds { get { return OfType(BuildingType.House).Sum(h => h.Def.Beds - h.Residents.Count); } }
        public float AverageFoodPrice
        {
            get
            {
                var r = Buildings.Where(b => b.Def.IsRetail && b.OwnerId >= 0).ToList();
                return r.Count == 0 ? 0f : r.Average(b => b.Price);
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
        public bool IsOpen(Building b) { return WorkersAt(b) > 0; }
        List<Building> OpenRetailers() { return Buildings.Where(b => b.Def.IsRetail && IsOpen(b)).ToList(); }

        void AddLog(string msg, LogKind kind = LogKind.Info)
        {
            Log.Insert(0, new LogEntry { Day = Day, Message = msg, Kind = kind });
            if (Log.Count > 200) Log.RemoveRange(200, Log.Count - 200);
        }

        public static string Money(float v)
        {
            float a = Math.Abs(v);
            string s = a >= 1000 ? Math.Round(a).ToString("#,0") : a.ToString("0.00");
            return (v < 0 ? "-$" : "$") + s;
        }

        // =====================================================================
        // Map
        // =====================================================================
        public byte TerrainAt(int x, int y) { return x < 0 || y < 0 || x >= Cols || y >= Rows ? Tile.OutOfBounds : Terrain[y * Cols + x]; }
        void SetTerrain(int x, int y, byte v) { if (TerrainAt(x, y) == Tile.OutOfBounds) return; Terrain[y * Cols + x] = v; MarkDirty(x, y); }
        void MarkDirty(int x, int y) { DirtyTiles.Add(y * Cols + x); MapVersion++; }
        void MarkDirty(Building b) { var d = b.Def; for (int dx = 0; dx < d.W; dx++) for (int dy = 0; dy < d.H; dy++) MarkDirty(b.X + dx, b.Y + dy); occDirty = true; }

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

        static float ClearCostOf(byte t) { return t == Tile.Tree ? Balance.ClearTree : t == Tile.Rock ? Balance.ClearRock : 0f; }
        public float ClearCost(BuildingType type, int x, int y)
        {
            var d = Defs.Get(type); float c = 0;
            for (int dx = 0; dx < d.W; dx++) for (int dy = 0; dy < d.H; dy++) c += ClearCostOf(TerrainAt(x + dx, y + dy));
            return c;
        }

        /// <summary>Clears a tree or boulder. Returns an error message or null.</summary>
        public string ClearTile(int x, int y)
        {
            byte t = TerrainAt(x, y); float c = ClearCostOf(t);
            if (c <= 0) return null;
            if (Treasury < c) return "Clearing that " + (t == Tile.Tree ? "tree" : "rock") + " costs " + Money(c);
            Treasury -= c; LedgerThisWeek.Construction += c;
            SetTerrain(x, y, Tile.Grass);
            return null;
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
            if (d.Unique && OfType(type).Any()) return "The town already has a " + d.Name;
            float cc = ClearCost(type, x, y);
            if (!free && Treasury < d.Cost + cc)
                return "The treasury needs " + Money(d.Cost + cc) + (cc > 0 ? " (incl. " + Money(cc) + " to clear the land)" : "");
            return null;
        }

        /// <summary>Builds something. Returns the new building, or null with an error message.</summary>
        public Building Place(BuildingType type, int x, int y, out string error, bool free = false)
        {
            error = CanPlace(type, x, y, free);
            if (error != null) return null;
            var d = Defs.Get(type);
            float cc = ClearCost(type, x, y);
            if (!free) { Treasury -= d.Cost + cc; LedgerThisWeek.Construction += d.Cost + cc; }
            for (int dx = 0; dx < d.W; dx++)
                for (int dy = 0; dy < d.H; dy++)
                {
                    byte t = TerrainAt(x + dx, y + dy);
                    if (t == Tile.Tree || t == Tile.Rock) Terrain[(y + dy) * Cols + x + dx] = Tile.Grass;
                }
            var b = new Building { Id = NextId++, Type = type, X = x, Y = y, BuiltDay = Day };
            if (d.IsBusiness)
            {
                b.Price = d.StartPrice;
                b.Wage = Math.Max(Policy.MinWage, d.BaseWage);
                b.Desired = d.DesiredStart;
            }
            Buildings.Add(b);
            MarkDirty(b);
            if (type != BuildingType.Road && !free) AddLog("The town built a " + d.Name + ".", LogKind.Build);
            if (type == BuildingType.House)
                foreach (var p in People) if (p.HomeId < 0 && p.Role != Role.Mayor) FindHome(p);
            return b;
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
            foreach (var id in b.Employees.ToList()) { var p = GetCitizen(id); if (p != null) { p.WorkId = -1; p.Role = Role.Unemployed; } }
            if (d.IsBusiness && b.OwnerId >= 0)
            {
                var o = GetCitizen(b.OwnerId);
                if (o != null) { o.Money += b.Cash * 0.5f; if (o.WorkId == b.Id) { o.WorkId = -1; o.Role = Role.Unemployed; } }
            }
            Buildings.Remove(b);
            MarkDirty(b);
            float refund = d.Cost * 0.25f;
            Treasury += refund; LedgerThisWeek.Refunds += refund;
            if (b.Type != BuildingType.Road) AddLog("The " + d.Name + " was torn down.", LogKind.Bad);
            if (b.Type == BuildingType.Estate && MayorId < 0) AddLog("With the estate gone, the town has no mayor.", LogKind.Bad);
            foreach (var p in People) if (p.HomeId < 0 && p.Role != Role.Mayor) FindHome(p);
        }

        // =====================================================================
        // Settlers
        // =====================================================================
        bool FindHome(Citizen p)
        {
            var h = OfType(BuildingType.House).Where(x => x.Residents.Count < x.Def.Beds).OrderBy(x => x.Residents.Count).FirstOrDefault();
            if (h != null) { p.HomeId = h.Id; h.Residents.Add(p.Id); p.Homeless = 0; return true; }
            p.HomeId = -1; return false;
        }

        Citizen AddPerson(float money)
        {
            var p = new Citizen
            {
                Id = NextId++,
                Name = Pick(Defs.FirstNames) + " " + Pick(Defs.LastNames),
                Money = money,
                Role = Role.Unemployed,
                Happy = Rnd(55, 70),
                Seed = (float)Rng.NextDouble(),
                ArrivedDay = Day
            };
            FindHome(p);
            People.Add(p);
            return p;
        }

        void QuitJob(Citizen p)
        {
            var b = GetBuilding(p.WorkId);
            if (b != null) b.Employees.Remove(p.Id);
            p.WorkId = -1; p.Role = Role.Unemployed;
        }

        void ResumeOrIdle(Citizen p)
        {
            var own = OwnedBusiness(p);
            if (own != null) { p.Role = Role.Owner; p.WorkId = own.Id; }
            else { p.Role = Role.Unemployed; p.WorkId = -1; }
        }

        void UnseatMayor(Citizen m, bool rehouse)
        {
            var e = Estate; if (e != null) e.Residents.Clear();
            MayorId = -1; MayorPlatform = Platform.None;
            ResumeOrIdle(m); m.HomeId = -1;
            if (rehouse && !FindHome(m)) AddLog(m.Name + " left the estate but found no house to move into.", LogKind.Bad);
        }

        void InstallMayor(Citizen p, Platform platform)
        {
            var e = Estate; if (e == null) return;
            var old = Mayor;
            if (old != null && old != p) { UnseatMayor(old, true); AddLog(old.Name + " packs up and moves out of the Mayor's Estate."); }
            if (p.Role == Role.Worker || p.Role == Role.Sheriff || p.Role == Role.Deputy) QuitJob(p);
            var h = GetBuilding(p.HomeId);
            if (h != null && h != e) h.Residents.Remove(p.Id);
            p.Role = Role.Mayor; p.WorkId = e.Id; p.HomeId = e.Id;
            e.Residents.Clear(); e.Residents.Add(p.Id);
            MayorId = p.Id; MayorPlatform = platform; TermEnd = Day + Balance.TermDays;
        }

        void RemovePerson(Citizen p, string why)
        {
            if (p.Role == Role.Mayor) { MayorId = -1; MayorPlatform = Platform.None; var e = Estate; if (e != null) e.Residents.Clear(); }
            else if (p.Role != Role.Owner) QuitJob(p);
            var own = OwnedBusiness(p);
            if (own != null) { own.OwnerId = -1; AddLog("The " + own.Def.Name + " is up for sale."); }
            var h = GetBuilding(p.HomeId); if (h != null) h.Residents.Remove(p.Id);
            if (Campaign.Active)
            {
                Campaign.Candidates.RemoveAll(c => c.CitizenId == p.Id);
                if (Campaign.Candidates.Count == 0) Campaign.Active = false;
            }
            People.Remove(p);
            AddLog(p.Name + " " + why, LogKind.Bad);
        }

        float Standing(Citizen p) { return p.Money + (OwnedBusiness(p) != null ? 30 : 0) + p.Happy * 0.3f + (MayorId == p.Id ? 15 : 0); }
        List<Citizen> Eligible() { return People.Where(p => p.Role != Role.Sheriff && p.Role != Role.Deputy && p.Jail == 0 && Day - p.ArrivedDay >= 3).ToList(); }

        // =====================================================================
        // Jobs
        // =====================================================================
        void AssignOwner(Building b)
        {
            var d = b.Def;
            var c = People.Where(p => (p.Role == Role.Unemployed || p.Role == Role.Worker) && p.Jail == 0 && p.Money >= 12 && OwnedBusiness(p) == null)
                          .OrderByDescending(p => p.Money).FirstOrDefault();
            if (c == null) return;
            if (c.Role == Role.Worker) QuitJob(c);
            float invest = c.Money * 0.6f, deed = Math.Min(invest, d.Cost * 0.5f);
            c.Money -= invest; Treasury += deed; LedgerThisWeek.Deeds += deed;
            float grant = Math.Min(25f, Math.Max(0f, Treasury)); Treasury -= grant; LedgerThisWeek.Grants += grant;
            b.Cash += grant + invest - deed; b.OwnerId = c.Id; c.Role = Role.Owner; c.WorkId = b.Id; b.LossWeeks = 0; b.BuiltDay = Day;
            AddLog(c.Name + " bought the deed to the " + d.Name + " for " + Money(deed) + ".", LogKind.Good);
        }

        struct Vacancy { public Building B; public float Wage; public Role Role; }

        void Hire(Citizen p, Vacancy v)
        {
            p.WorkId = v.B.Id; p.Role = v.Role; v.B.Employees.Add(p.Id);
            string label = v.Role == Role.Worker ? "a hand at the " + v.B.Def.Name : v.Role == Role.Sheriff ? "the new Sheriff" : "a deputy";
            var boss = GetCitizen(v.B.OwnerId);
            AddLog(p.Name + " was hired as " + label + (boss != null && v.Role == Role.Worker ? " by " + boss.Name : "") + " at " + Money(v.Wage) + "/day.");
        }

        void Hiring()
        {
            foreach (var b in Businesses.ToList()) if (b.OwnerId < 0) AssignOwner(b);
            var off = Office;
            if (off != null)
            {
                var deps = off.Employees.Select(GetCitizen).Where(p => p != null && p.Role == Role.Deputy).ToList();
                while (deps.Count > Policy.Deputies)
                {
                    var dpt = deps[deps.Count - 1]; deps.RemoveAt(deps.Count - 1);
                    QuitJob(dpt); AddLog(dpt.Name + " turned in the deputy's badge.");
                }
            }
            var vac = new List<Vacancy>();
            foreach (var b in Businesses)
            {
                if (b.OwnerId < 0) continue;
                int want = Math.Min(b.Def.MaxEmployees, b.Desired);
                for (int i = b.Employees.Count; i < want; i++) vac.Add(new Vacancy { B = b, Wage = b.Wage, Role = Role.Worker });
            }
            if (off != null && Treasury > 15)
            {
                var staff = off.Employees.Select(GetCitizen).Where(p => p != null).ToList();
                if (!staff.Any(p => p.Role == Role.Sheriff)) vac.Add(new Vacancy { B = off, Wage = Balance.SalarySheriff, Role = Role.Sheriff });
                for (int i = staff.Count(p => p.Role == Role.Deputy); i < Policy.Deputies; i++) vac.Add(new Vacancy { B = off, Wage = Balance.SalaryDeputy, Role = Role.Deputy });
            }
            vac = vac.OrderByDescending(v => v.Wage).ToList();
            var idle = Shuffle(People.Where(p => p.Role == Role.Unemployed && p.Jail == 0).ToList());
            foreach (var v in vac) { if (idle.Count == 0) break; var p = idle[0]; idle.RemoveAt(0); Hire(p, v); }
        }

        public void SetMinWage(float v)
        {
            Policy.MinWage = v;
            foreach (var b in Businesses) b.Wage = Math.Max(v, b.Def.BaseWage);
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
        public static Town CreateNew(int seed)
        {
            var t = new Town { Seed = seed, Treasury = Balance.StartingTreasury };
            t.Terrain = TerrainGen.Generate(seed, Cols, Rows);
            // Open the town site
            for (int y = 19; y <= 29; y++)
                for (int x = 27; x <= 49; x++)
                    if (t.TerrainAt(x, y) != Tile.Water) t.Terrain[y * Cols + x] = Tile.Grass;
            string err;
            for (int x = 1; x < Cols - 1; x++)
            {
                if (t.TerrainAt(x, 24) != Tile.Water) t.Terrain[24 * Cols + x] = Tile.Grass;
                t.Place(BuildingType.Road, x, 24, out err, true);
            }
            for (int y = 6; y < Rows - 8; y++)
            {
                if (t.OccupantAt(38, y) != null) continue;
                if (t.TerrainAt(38, y) != Tile.Water) t.Terrain[y * Cols + 38] = Tile.Grass;
                t.Place(BuildingType.Road, 38, y, out err, true);
            }
            t.Place(BuildingType.House, 29, 22, out err, true);
            t.Place(BuildingType.House, 32, 22, out err, true);
            t.Place(BuildingType.House, 35, 22, out err, true);
            t.Place(BuildingType.Store, 39, 22, out err, true);
            t.Place(BuildingType.Farm, 42, 25, out err, true);
            for (int i = 0; i < 9; i++) t.AddPerson((float)Math.Round(t.Rnd(15, 55)));
            t.Hiring();
            foreach (var b in t.Businesses) b.BuiltDay = -10;
            t.DirtyTiles.Clear();
            t.AddLog("Nine settlers have staked their claims at Cedar Fork. The town council hands you the treasury.", LogKind.Good);
            t.AddLog("Tip: build a Sheriff's Office and Mayor's Estate. Idle hands need jobs. Clearing trees costs $3, rocks $10.");
            return t;
        }
    }
}
