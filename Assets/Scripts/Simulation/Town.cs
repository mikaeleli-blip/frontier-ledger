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
        public const int CurrentVersion = 10;
        public int Version = CurrentVersion;
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
        public bool PeopleWeekValid;             // settlers' earned and spent figures cover a whole week
        public int CrimesWeek, CrimesLast, CaughtWeek, CaughtLast;
        public int Births, Deaths, Unburied;
        public int FounderBuilderId = -1;        // the builder who came with the wagon; owns the first yard
        public List<HistoryPoint> History = new List<HistoryPoint>();
        public List<FreightOrder> Orders = new List<FreightOrder>();
        public int NextOrderId = 1;
        public MarketReport Market = new MarketReport();

        // ---- runtime only ----
        [NonSerialized] Random rng;
        [NonSerialized] int[] occ;
        [NonSerialized] bool occDirty = true;
        [NonSerialized] public int MapVersion = 1;
        [NonSerialized] public List<int> DirtyTiles = new List<int>();
        /// <summary>Today's trips to the shops and deliveries, for the map to show.</summary>
        [NonSerialized] public List<Errand> Errands = new List<Errand>();
        /// <summary>Fired after each simulated day.</summary>
        [NonSerialized] public Action<int> DayEnded;

        public const int Cols = Balance.Cols, Rows = Balance.Rows;

        Random Rng { get { if (rng == null) rng = new Random(Seed ^ (Day * 7919)); return rng; } }

        /// <summary>Call after loading from JSON to rebuild runtime caches.</summary>
        public void AfterLoad()
        {
            rng = null; occDirty = true; MapVersion++;
            if (DirtyTiles == null) DirtyTiles = new List<int>();
            if (Errands == null) Errands = new List<Errand>();
            if (Orders == null) Orders = new List<FreightOrder>();
            foreach (var o in Orders) o.Goods = Fit(o.Goods, null);
            if (Campaign == null) Campaign = new Campaign();
            if (LastElection == null) LastElection = new ElectionResult();
            foreach (var p in People) if (p.Role == Role.Builder) { p.Role = Role.Laborer; p.WorkId = -1; }   // the old building crew
            foreach (var b in Buildings)
            {
                b.Stock = Fit(b.Stock, null); b.Price = Fit(b.Price, null); b.Delivered = Fit(b.Delivered, null);
            }
            if (Policy == null) Policy = new TownPolicy();
            Policy.StorehousePrice = Fit(Policy.StorehousePrice, new TownPolicy().StorehousePrice);
            Policy.Reserve = Fit(Policy.Reserve, new TownPolicy().Reserve);
            if (Version < 3)
            {
                // version 3 added upkeep, contracts, rent and deed debts
                foreach (var b in Buildings) { b.Condition = 100f; b.RepairBy = -1; b.RepairPaid = false; b.ContractorId = -1; b.ContractPaid = 0; b.Debt = 0; b.Arrears = 0; b.TownOwes = 0; }
                foreach (var p in People) p.EvictedDay = -999;
                var fresh = new TownPolicy();
                Policy.ContractRate = fresh.ContractRate; Policy.Rent = fresh.Rent;
                foreach (var b in Buildings) if (b.Def.IsBusiness && b.Built) b.Price[(int)Good.Furniture] = Balance.StartRetail[(int)Good.Furniture];
                Version = 3;
            }
            if (Version < 4)
            {
                // version 4 added boarding houses and birth parents
                Policy.BoardRent = new TownPolicy().BoardRent;
                foreach (var p in People) { p.MotherId = p.ParentId; p.FatherId = -1; p.RentOwed = 0; var m = GetCitizen(p.ParentId); if (m != null && m.PartnerId >= 0) p.FatherId = m.PartnerId; }
                Version = 4;
            }
            if (Version < 5)
            {
                // version 5: family hands, bank loans, the town running unsold businesses
                foreach (var p in People) p.FamilyHand = false;
                foreach (var b in Buildings) { b.LenderId = -1; b.MissedPayments = 0; }
                Version = 5;
            }
            if (Version < 6)
            {
                // version 6: schooling, illness, teachers and doctors. Grown settlers mostly had some school back east.
                foreach (var p in People)
                {
                    p.Sick = 0; p.Illness = 0; p.IllnessKind = 0; p.Physician = false;
                    p.Schooling = !p.IsChild ? (Chance(Balance.SchooledNewcomers) ? Rnd(0.5f, 1f) : Rnd(0f, 0.4f))
                                : p.Age > Balance.SchoolAge ? Math.Min(1f, (p.Age - Balance.SchoolAge) / 4f) * Rnd(0f, 0.6f) : 0f;
                }
                Version = 6;
            }
            if (Version < 7)
            {
                // version 7: summer and autumn last three weeks. Every date keeps its season and day of the season.
                Func<int, int> remap = d =>
                {
                    if (d <= 0) return d;
                    int y = (d - 1) / 56, r = (d - 1) % 56;
                    return y * Balance.DaysPerYear + Balance.SeasonStart(r / 14) + r % 14 + 1;
                };
                Day = remap(Day); TermEnd = remap(TermEnd);
                if (Campaign != null) Campaign.VoteDay = remap(Campaign.VoteDay);
                if (LastElection != null) LastElection.Day = remap(LastElection.Day);
                foreach (var b in Buildings) b.BuiltDay = remap(b.BuiltDay);
                foreach (var p in People) { p.ArrivedDay = remap(p.ArrivedDay); p.EvictedDay = remap(p.EvictedDay); p.RobbedDay = remap(p.RobbedDay); }
                foreach (var h in History) h.Day = remap(h.Day);
                foreach (var l in Log) l.Day = remap(l.Day);
                foreach (var o in Orders) { o.OrderedDay = remap(o.OrderedDay); o.ArriveDay = remap(o.ArriveDay); }
                Version = 7;
            }
            if (Version < 8)
            {
                // version 8: bank accounts
                foreach (var p in People) { p.Deposit = 0; p.BankId = -1; }
                foreach (var b in Buildings) { b.Deposit = 0; b.BankId = -1; }
                Version = 8;
            }
            if (Version < 9)
            {
                // version 9: new trades. Everyone has the clothes they came in, part worn.
                foreach (var p in People) { p.Clothes = Rnd(0.6f, 1f); p.BathedDay = -999; p.ChurchDay = -999; }
                foreach (var b in Buildings) { b.Attendance = 0; b.Collection = 0; }
                Version = 9;
            }
            if (Version < 10)
            {
                // version 10: each settler's earnings and spending, counted from now
                foreach (var p in People) { p.Earned = 0; p.EarnedLast = 0; p.SpentLast = 0; p.WorthStart = Savings(p); }
                PeopleWeekValid = false;
                Version = 10;
            }
        }

        /// <summary>Resizes a per-good array to the current number of goods, keeping what's there.</summary>
        static float[] Fit(float[] a, float[] defaults)
        {
            if (a != null && a.Length == Balance.GoodCount) return a;
            var r = new float[Balance.GoodCount];
            for (int i = 0; i < r.Length; i++) r[i] = a != null && i < a.Length ? a[i] : defaults != null && i < defaults.Length ? defaults[i] : 0f;
            return r;
        }

        // =====================================================================
        // Calendar
        // =====================================================================
        /// <summary>The season a day falls in (seasons aren't all the same length).</summary>
        public static int SeasonOf(int day)
        {
            int d = (day - 1) % Balance.DaysPerYear;
            for (int s = 0; s < 4; s++) { if (d < Balance.SeasonDays[s]) return s; d -= Balance.SeasonDays[s]; }
            return 3;
        }
        public int Season { get { return SeasonOf(Day); } }
        public int Year { get { return Balance.StartYear + (Day - 1) / Balance.DaysPerYear; } }
        public int DayOfSeason { get { return (Day - 1) % Balance.DaysPerYear - Balance.SeasonStart(Season) + 1; } }
        public int DaysInSeason { get { return Balance.SeasonDays[Season]; } }
        public string Weekday { get { return Defs.Weekdays[(Day - 1) % 7]; } }
        public string DateLabel { get { return Defs.Seasons[Season] + " " + DayOfSeason + ", " + Year; } }
        /// <summary>0..1 through the current season.</summary>
        public float SeasonProgress { get { return (DayOfSeason - 1 + TimeOfDay) / DaysInSeason; } }
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
        /// <summary>Enough school to read, write and reckon.</summary>
        public static bool Schooled(Citizen p) { return p.Schooling >= Balance.SchooledAt; }
        /// <summary>Too ill or hurt to work, go to school or leave the house.</summary>
        public static bool LaidUp(Citizen p) { return p.Sick > 0 && p.Illness >= 3f; }
        /// <summary>School runs in the summer and winter terms; children are needed at home for planting and harvest.</summary>
        public bool SchoolInSession { get { return Season == 1 || Season == 3; } }
        /// <summary>Jobs that pay a wage or salary (and are left when someone becomes an owner or the mayor).</summary>
        public static bool Employed(Role r) { return r == Role.Worker || r == Role.Sheriff || r == Role.Deputy || r == Role.Builder || r == Role.Teacher || r == Role.Doctor || r == Role.Preacher; }
        /// <summary>Work that needs someone who can read, write and keep accounts.</summary>
        /// <summary>Trades that pay their people a share of each sale instead of a day wage.</summary>
        public static bool PaysByShare(BuildingType t) { return t == BuildingType.Brothel; }
        /// <summary>Who may work at the brothel: unmarried women of 18 and over.</summary>
        public static bool BrothelWorker(Citizen p) { return p != null && p.Female && p.Age >= Balance.BrothelAge && p.PartnerId < 0; }
        public static bool SkilledWork(BuildingType t) { return t == BuildingType.Store || t == BuildingType.Bank || t == BuildingType.Hotel; }
        /// <summary>Work that puts food on the town's tables: settlers take it first when food is short.</summary>
        public static bool FoodJob(BuildingType t) { return t == BuildingType.Farm || t == BuildingType.Gatherer || t == BuildingType.Fishery || t == BuildingType.Ranch || t == BuildingType.Butcher; }
        /// <summary>Goods people eat.</summary>
        public static bool Edible(Good g) { return g == Good.Food || g == Good.Meat || g == Good.Bread; }
        /// <summary>Shops that make what they sell over the counter (and buy the makings, not the goods).</summary>
        public static bool MakesWhatItSells(BuildingType t) { return t == BuildingType.Butcher || t == BuildingType.Bakery || t == BuildingType.Tailor; }
        /// <summary>How much of a good a business keeps before the surplus goes east on the freight wagons.</summary>
        public static float StockCap(Building b, Good g)
        {
            if (g == Good.Food && b.Type == BuildingType.Farm) return Balance.BarnCapacity;
            if (g == Good.Cattle) return Balance.HerdCap;
            if (g == Good.Clothes) return Balance.ClothesCap;
            if (g == Good.Leather || g == Good.Hides) return 30f;   // hides and leather go east before they pile up
            if (g == Good.Iron || g == Good.Wool || g == Good.Beer) return 40f;
            return Balance.BusinessStockCap;
        }
        /// <summary>Trades that send their surplus east and keep working: the ranch's cattle drives and the mine's pig iron.</summary>
        public static bool ExportTrade(BuildingType t) { return t == BuildingType.Ranch || t == BuildingType.IronMine || t == BuildingType.Tannery; }
        public Building ChurchWithPreacher { get { return OfType(BuildingType.Church).FirstOrDefault(c => StaffOf(c, Role.Preacher) != null); } }
        public Citizen StaffOf(Building b, Role r) { foreach (var id in b.Employees) { var p = GetCitizen(id); if (p != null && p.Role == r) return p; } return null; }
        public Building OwnedBusiness(Citizen p) { return Buildings.FirstOrDefault(b => b.Def.IsBusiness && b.OwnerId == p.Id); }
        /// <summary>The business a settler's household owns, if any (theirs or their spouse's).</summary>
        public Building FamilyBusiness(Citizen p)
        {
            var own = OwnedBusiness(p); if (own != null) return own;
            var sp = GetCitizen(p.PartnerId); return sp != null ? OwnedBusiness(sp) : null;
        }
        public IEnumerable<Building> Banks { get { return Businesses.Where(b => b.Type == BuildingType.Bank && b.OwnerId >= 0); } }

        // =====================================================================
        // Banking
        // =====================================================================
        /// <summary>The bank new accounts are opened at.</summary>
        public Building HomeBank { get { return Banks.OrderBy(b => b.Id).FirstOrDefault(); } }
        public float Savings(Citizen p) { return p == null ? 0f : p.Money + p.Deposit; }
        public float DepositsAt(Building bank)
        {
            float s = 0;
            foreach (var p in People) if (p.BankId == bank.Id) s += p.Deposit;
            foreach (var b in Buildings) if (b.BankId == bank.Id) s += b.Deposit;
            return s;
        }
        /// <summary>What a bank keeps in the vault: a share of its deposits, and a little for itself.</summary>
        public float VaultReserve(Building bank) { return 20f + Balance.ReserveRatio * DepositsAt(bank); }
        public float Lendable(Building bank) { return Math.Max(0, bank.Cash - VaultReserve(bank)); }

        /// <summary>Takes money out of a settler's account into their pocket, as far as the bank's vault allows.</summary>
        float WithdrawFor(Citizen p, float amount)
        {
            var bank = GetBuilding(p.BankId);
            float t = bank == null ? 0 : Math.Min(Math.Min(amount, p.Deposit), Math.Max(0, bank.Cash));
            if (t <= 0) return 0;
            p.Deposit -= t; bank.Cash -= t; p.Money += t;
            if (p.Deposit < 0.005f) p.Deposit = 0;
            return t;
        }
        /// <summary>Takes money out of a business's account into its till.</summary>
        float WithdrawFor(Building b, float amount)
        {
            var bank = GetBuilding(b.BankId);
            float t = bank == null ? 0 : Math.Min(Math.Min(amount, b.Deposit), Math.Max(0, bank.Cash));
            if (t <= 0) return 0;
            b.Deposit -= t; bank.Cash -= t; b.Cash += t;
            if (b.Deposit < 0.005f) b.Deposit = 0;
            return t;
        }
        /// <summary>Hands a settler's account to someone else (an heir or a widow).</summary>
        void MergeAccount(Citizen from, Citizen to)
        {
            if (from.Deposit <= 0) return;
            if (to.BankId < 0 || to.BankId == from.BankId) { to.BankId = from.BankId; to.Deposit += from.Deposit; }
            else to.Money += WithdrawFor(from, from.Deposit);
            from.Deposit = 0;
        }
        /// <summary>A bank closing pays out every account from its vault; what it can't pay is lost.</summary>
        void CloseBank(Building bank)
        {
            float owed = DepositsAt(bank), paid = 0;
            foreach (var p in People.Where(x => x.BankId == bank.Id).ToList()) { paid += WithdrawFor(p, p.Deposit); p.Deposit = 0; p.BankId = -1; }
            foreach (var b in Buildings.Where(x => x.BankId == bank.Id).ToList()) { paid += WithdrawFor(b, b.Deposit); b.Deposit = 0; b.BankId = -1; }
            if (owed > paid + 0.5f) AddLog("The bank closed owing its depositors " + Money(owed - paid) + " it couldn't pay out.", LogKind.Bad);
            else if (owed > 0.5f) AddLog("The bank closed and paid out its depositors.", LogKind.Info);
        }
        public float DeedPrice(Building b) { var d = b.Def; return (float)Math.Round(b.BuildCost > 0 ? b.BuildCost : 8f + (d.Logs + d.Stone) * 0.6f, 2); }
        public Citizen Mayor { get { return GetCitizen(MayorId); } }
        bool MayorBonus(Platform p) { return MayorId >= 0 && MayorPlatform == p; }

        public float AverageHappiness { get { return People.Count == 0 ? 50f : People.Average(p => p.Happy); } }
        public float AverageHealth { get { return People.Count == 0 ? 100f : People.Average(p => p.Health); } }
        public int FreeBeds { get { return Buildings.Where(h => h.Built && (h.Type == BuildingType.House || h.Type == BuildingType.BoardingHouse)).Sum(h => h.Def.Beds - h.Residents.Count); } }
        /// <summary>Buildings people live in.</summary>
        public static bool IsHome(BuildingType t) { return t == BuildingType.House || t == BuildingType.BoardingHouse || t == BuildingType.Estate; }
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
            foreach (var id in b.Employees) { var p = GetCitizen(id); if (p != null && p.Jail == 0 && !LaidUp(p)) n++; }
            var o = GetCitizen(b.OwnerId);
            if (o != null && o.WorkId == b.Id && o.Jail == 0 && !LaidUp(o)) n++;
            return n;
        }
        public bool IsOpen(Building b) { return b.Built && WorkersAt(b) > 0; }

        // =====================================================================
        // Freight from back east
        // =====================================================================
        public int FreightLeadDays { get { return Season == 3 ? Balance.FreightDaysWinter : Balance.FreightDays; } }
        public float OrderCost(float[] goods, int laborers, int families)
        {
            float c = 0; bool any = laborers > 0 || families > 0;
            for (int g = 0; g < Balance.GoodCount; g++) { c += goods[g] * Balance.ImportPrice[g]; any |= goods[g] > 0; }
            return any ? c + laborers * Balance.PassageLaborer + families * Balance.PassageFamily + Balance.CaravanFee : 0f;
        }
        /// <summary>Goods already on the road that will need room in the stores.</summary>
        public float GoodsOnTheRoad { get { float s = 0; foreach (var o in Orders) if (!o.Delivered) s += o.Goods.Sum(); return s; } }

        /// <summary>Sends an order back east, paid now from the treasury. Returns why it can't be sent, or null.</summary>
        public string PlaceOrder(float[] goods, int laborers, int families)
        {
            float cost = OrderCost(goods, laborers, families), bulk = goods.Sum();
            if (cost <= 0) return "Nothing on the order";
            if (cost > Treasury) return "The treasury can't pay " + Money(cost);
            if (bulk > 0 && !Storages.Any()) return "There's nowhere to store goods";
            if (bulk > TownSpace - GoodsOnTheRoad + 0.5f) return "The stores can't take that much";
            var o = new FreightOrder { Id = NextOrderId++, OrderedDay = Day, ArriveDay = Day + FreightLeadDays, Laborers = laborers, Families = families, Cost = cost };
            for (int g = 0; g < Balance.GoodCount; g++) o.Goods[g] = goods[g];
            Orders.Add(o);
            Treasury -= cost; LedgerThisWeek.Imports += cost;
            AddLog("The town sent an order back east for " + FreightText(o) + " (" + Money(cost) + "). The caravan is due in " + (o.ArriveDay - Day) + " days.", LogKind.Info);
            return null;
        }

        public static string FreightText(FreightOrder o)
        {
            var parts = new List<string>();
            for (int g = 0; g < Balance.GoodCount; g++) if (o.Goods[g] > 0.5f) parts.Add(Math.Round(o.Goods[g]) + " " + Defs.GoodNames[g].ToLower());
            if (o.Laborers > 0) parts.Add(o.Laborers + (o.Laborers == 1 ? " laborer" : " laborers"));
            if (o.Families > 0) parts.Add(o.Families + (o.Families == 1 ? " family" : " families"));
            return parts.Count == 0 ? "nothing" : parts.Count == 1 ? parts[0] : string.Join(", ", parts.Take(parts.Count - 1).ToArray()) + " and " + parts[parts.Count - 1];
        }

        /// <summary>Orders due tomorrow are unloaded into the stores overnight; the newcomers step off the wagons when
        /// the caravan stops in town. What the stores can't hold is refunded.</summary>
        void Caravans()
        {
            Orders.RemoveAll(o => o.Delivered && o.ArriveDay <= Day);
            foreach (var o in Orders.Where(x => !x.Delivered && Day + 1 >= x.ArriveDay).ToList())
            {
                o.Delivered = true;
                float refund = 0;
                for (int g = 0; g < Balance.GoodCount; g++)
                {
                    if (o.Goods[g] <= 0) continue;
                    float put = PutInTown((Good)g, o.Goods[g]);
                    refund += (o.Goods[g] - put) * Balance.ImportPrice[g];
                }
                var names = new List<string>();
                for (int i = 0; i < o.Laborers; i++)
                {
                    var p = AddPerson(Chance(0.3), Pick(Defs.LastNames), Rnd(18, 34), (float)Math.Round(Rnd(5, 20)));
                    p.ArrivedDay = Day + 1; names.Add(p.Name);
                }
                for (int i = 0; i < o.Families; i++)
                {
                    string fam = Pick(Defs.LastNames);
                    var dad = AddPerson(false, fam, Rnd(22, 40), (float)Math.Round(Rnd(10, 35)));
                    var mom = AddPerson(true, fam, Rnd(20, 36), (float)Math.Round(Rnd(5, 15)));
                    dad.PartnerId = mom.Id; mom.PartnerId = dad.Id; dad.ArrivedDay = mom.ArrivedDay = Day + 1;
                    for (int k = Rng.Next(1, 4); k > 0; k--) { var c = AddPerson(Chance(0.5), fam, Rnd(1, 9), 0); c.ParentId = c.MotherId = mom.Id; c.FatherId = dad.Id; c.ArrivedDay = Day + 1; }
                    names.Add("the " + fam + " family");
                }
                if (refund > 0.01f) { Treasury += refund; LedgerThisWeek.Refunds += refund; }
                AddLog("The freight caravan came up the road with " + FreightText(o) + "." + (refund > 0.01f ? " The stores were full: " + Money(refund) + " of it went back for a refund." : ""), LogKind.Good);
                Log[0].Day = Day + 1;   // it rolls in tomorrow
                if (o.Laborers + o.Families > 0) RehouseEveryone();
            }
        }

        /// <summary>Notes a trip for the map: someone goes to a shop to buy.</summary>
        void Shop(Citizen p, Building to, Good g)
        {
            if (p == null || to == null || p.IsChild || Errands.Count > 400) return;
            if (Errands.Any(e => e.PersonId == p.Id && e.ToId == to.Id) || Errands.Count(e => e.PersonId == p.Id) >= 3) return;
            Errands.Add(new Errand { PersonId = p.Id, ToId = to.Id, Good = g });
        }

        /// <summary>Notes a trip for the map: someone goes in for a service (a bath and a shave).</summary>
        void Visit(Citizen p, Building to)
        {
            if (p == null || to == null || p.IsChild || Errands.Count > 400) return;
            if (Errands.Count(e => e.PersonId == p.Id) >= 3) return;
            Errands.Add(new Errand { PersonId = p.Id, ToId = to.Id, Service = true });
        }

        /// <summary>Notes a delivery for the map: one of the seller's hands carries the goods over.</summary>
        void Deliver(Building from, Building to, Good g)
        {
            if (from == null || to == null || from == to || Errands.Count > 400) return;
            if (Errands.Any(e => e.Delivery && e.FromId == from.Id && e.ToId == to.Id)) return;
            Citizen courier = null; int best = int.MaxValue;
            foreach (var id in from.Employees)
            {
                var c = GetCitizen(id); if (c == null || c.Jail > 0 || LaidUp(c) || Errands.Count(e => e.PersonId == c.Id) >= 2) continue;
                int k = (c.Id * 31 + Day * 7) % 13; if (k < best) { best = k; courier = c; }
            }
            if (courier == null) { var o = GetCitizen(from.OwnerId); if (o != null && o.WorkId == from.Id && o.Jail == 0 && !LaidUp(o) && Errands.Count(e => e.PersonId == o.Id) < 2) courier = o; }
            if (courier != null) Errands.Add(new Errand { PersonId = courier.Id, FromId = from.Id, ToId = to.Id, Good = g, Delivery = true });
        }

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
        public float TownStored { get { float s = 0; foreach (var b in Storages) for (int i = 0; i < Balance.GoodCount; i++) s += b.Stock[i]; return s; } }
        public float TownSpace { get { return Math.Max(0, TownCapacity - TownStored); } }

        /// <summary>The town takes from the supply wagon first and stores into it last, so it empties into the storehouse.</summary>
        float TakeFromTown(Good g, float q)
        {
            float taken = 0;
            foreach (var b in Storages.OrderBy(s => s.Type == BuildingType.Wagon ? 0 : 1))
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
            foreach (var b in Storages.OrderBy(s => s.Type == BuildingType.Wagon ? 1 : 0))
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
                case Good.Furniture: return t == BuildingType.BuilderYard;
                case Good.Meat: case Good.Hides: return t == BuildingType.Butcher;
                case Good.Bread: return t == BuildingType.Bakery;
                case Good.Iron: return t == BuildingType.IronMine;
                case Good.Wool: case Good.Cattle: return t == BuildingType.Ranch;
                case Good.Leather: return t == BuildingType.Tannery;
                case Good.Clothes: return t == BuildingType.Tailor;
                case Good.Beer: return t == BuildingType.Brewery;
            }
            return false;
        }

        /// <summary>Buys a good from the businesses that make it, at wholesale. Returns the amount bought
        /// and lowers <paramref name="budget"/> by what was paid.</summary>
        float BuyFromProducers(Good g, float qty, ref float budget, Building buyer, BuildingType? only = null)
        {
            float price = Balance.Wholesale[(int)g], bought = 0;
            if (qty <= 0 || budget < price * 0.1f) return 0;
            foreach (var s in Businesses.Where(b => b != buyer && Produces(b.Type, g) && (only == null || b.Type == only.Value) && b.Stock[(int)g] > 0.01f).OrderByDescending(b => b.Stock[(int)g]))
            {
                float q = Math.Min(Math.Min(qty - bought, s.Stock[(int)g]), budget / price);
                if (q <= 0.001f) break;
                float c = q * price;
                s.Stock[(int)g] -= q; s.Cash += c; s.Week.Revenue += c; s.Today.Sold += q; s.Week.Sold += q;
                if (q >= 1f) Deliver(s, buyer ?? OfType(BuildingType.Storehouse).FirstOrDefault(), g);
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

        public const float LogsPerClearedTree = 2f, StonePerClearedRock = 3f;

        /// <summary>Clears a tree, sapling, stump or boulder for a small fee. Felled trees give logs and broken boulders stone.
        /// Returns why it can't be done, or a note when the stores had no room for what it gave.</summary>
        public string ClearTile(int x, int y)
        {
            byte t = TerrainAt(x, y); float c = ClearCostOf(t);
            if (c <= 0) return null;
            if (Treasury < c) return "Clearing that costs " + Money(c);
            Treasury -= c; LedgerThisWeek.Construction += c;
            float lost = 0;
            if (t == Tile.Tree) lost = Salvage(Good.Logs, LogsPerClearedTree);
            if (t == Tile.Rock) lost = Salvage(Good.Stone, StonePerClearedRock);
            SetTerrain(x, y, Tile.Grass);
            if (lost > 0.01f) return "The stores are full: " + Math.Round(lost) + (t == Tile.Tree ? " logs" : " stone") + " left lying. Build another storehouse.";
            return null;
        }

        /// <summary>Timber and stone from clearing land go into the town stores. When those are full they go to a building site
        /// still waiting on them, then to a business that works them (woodcutter, smithy, builder's yard), which pays wholesale.
        /// Returns how much had nowhere to go.</summary>
        float Salvage(Good g, float q)
        {
            q -= PutInTown(g, q);
            foreach (var s in Sites)
            {
                if (q <= 0.01f) break;
                var d = s.Def;
                float need = (g == Good.Logs ? d.Logs : g == Good.Stone ? d.Stone : 0) - s.Delivered[(int)g];
                if (need <= 0.01f) continue;
                float t = Math.Min(need, q); s.Delivered[(int)g] += t; q -= t;
            }
            foreach (var b in Businesses)
            {
                if (q <= 0.01f) break;
                bool uses = g == Good.Logs ? b.Type == BuildingType.Woodcutter || b.Type == BuildingType.Smithy || b.Type == BuildingType.BuilderYard
                                           : g == Good.Stone && b.Type == BuildingType.Smithy;
                if (!uses) continue;
                float t = Math.Min(Math.Max(0, 30f - b.Stock[(int)g]), q);
                if (t <= 0.01f) continue;
                float pay = Math.Min(Math.Max(0, b.Cash), t * Balance.Wholesale[(int)g]);
                b.Stock[(int)g] += t; b.Cash -= pay; b.Week.Costs += pay; Treasury += pay; LedgerThisWeek.StoreSales += pay; q -= t;
            }
            return Math.Max(0, q);
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
        bool TouchesHill(int x, int y, int w, int h)
        {
            for (int dx = -1; dx <= w; dx++)
                for (int dy = -1; dy <= h; dy++)
                {
                    bool edge = dx == -1 || dy == -1 || dx == w || dy == h;
                    if (edge && TerrainAt(x + dx, y + dy) == Tile.Hill) return true;
                }
            return false;
        }
        /// <summary>Open grazing round a spot: grass, saplings and stumps nobody has built on.</summary>
        public int PastureAt(int x, int y, int w, int h)
        {
            int cx = x + w / 2, cy = y + h / 2, r = Balance.PastureRadius, n = 0;
            for (int yy = cy - r; yy <= cy + r; yy++)
                for (int xx = cx - r; xx <= cx + r; xx++)
                {
                    if ((xx - cx) * (xx - cx) + (yy - cy) * (yy - cy) > r * r) continue;
                    if (xx >= x && xx < x + w && yy >= y && yy < y + h) continue;   // the ranch itself
                    byte t = TerrainAt(xx, yy);
                    if (t != Tile.Grass && t != Tile.Sapling && t != Tile.Stump) continue;
                    var o = OccupantAt(xx, yy);
                    if (o == null) n++;
                }
            return n;
        }
        public int PastureNear(Building b) { var d = b.Def; return PastureAt(b.X, b.Y, d.W, d.H); }

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
            if (d.NeedsHill && !TouchesHill(x, y, d.W, d.H)) return "An iron mine has to be dug into a hillside";
            if (d.NeedsPasture && PastureAt(x, y, d.W, d.H) < Balance.PastureMin) return "A ranch needs open grassland around it for grazing";
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
                    if (!free && t == Tile.Tree) Salvage(Good.Logs, LogsPerClearedTree);       // the ground is cleared for the building:
                    if (!free && t == Tile.Rock) Salvage(Good.Stone, StonePerClearedRock);     // the timber and stone are kept
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
            b.Condition = 100f;
            // settle the contract and note what the building cost, so its deed can be priced
            float materials = d.Logs * Balance.Wholesale[(int)Good.Logs] + d.Stone * Balance.Wholesale[(int)Good.Stone];
            var yard = GetBuilding(b.ContractorId);
            if (yard != null && b.ContractPrice > 0)
            {
                float rest = Math.Max(0, b.ContractPrice - b.ContractPaid), pay = Math.Min(rest, Math.Max(0, Treasury));
                Treasury -= pay; LedgerThisWeek.Contracts += pay; b.ContractPaid += pay;
                yard.Cash += pay; yard.Week.Revenue += pay; yard.TownOwes += rest - pay;
                b.BuildCost = b.ContractPrice + materials;
            }
            else if (!quiet) b.BuildCost = LaborDays(d) * Policy.BuilderWage + materials;
            b.ContractorId = -1;
            if (d.IsBusiness)
            {
                for (int i = 0; i < Balance.GoodCount; i++) b.Price[i] = Balance.StartRetail[i];
                b.Wage = Math.Max(Policy.MinWage, d.BaseWage);
                b.Desired = d.MaxEmployees >= 1 ? 1 : 0;   // until it's sold, the town hires someone to run it
            }
            if (!quiet && b.Type != BuildingType.Road) AddLog("The " + d.Name + " is finished.", LogKind.Build);
            if (b.Type == BuildingType.Bank && b.OwnerId < 0 && !quiet) BankerArrives(b);
            if (b.Type == BuildingType.House || b.Type == BuildingType.BoardingHouse) RehouseEveryone();
        }

        /// <summary>A banker comes out from back east with money to lend, buys the new bank from the town and opens it.</summary>
        void BankerArrives(Building bank)
        {
            string fam = Pick(Defs.LastNames);
            var banker = AddPerson(Chance(0.8) ? false : true, fam, Rnd(34, 52), 0);
            var spouse = AddPerson(!banker.Female, fam, Rnd(30, 48), 10);
            banker.PartnerId = spouse.Id; spouse.PartnerId = banker.Id;
            float price = DeedPrice(bank);
            Treasury += price; LedgerThisWeek.Deeds += price;
            banker.Schooling = 1f; spouse.Schooling = Math.Max(spouse.Schooling, 0.6f);
            bank.OwnerId = banker.Id; banker.Role = Role.Owner; banker.WorkId = bank.Id;
            bank.Cash = Balance.BankCapital; bank.Desired = 1; bank.BuiltDay = Day;
            AddLog(banker.Name + " came out from back east with " + Money(Balance.BankCapital) + " to lend, paid the town " + Money(price) + " for the Bank and opened its doors.", LogKind.Good);
            RehouseEveryone();
        }

        public void Remove(Building b)
        {
            var d = b.Def;
            if (b.Type == BuildingType.Estate)
            {
                var m = Mayor; if (m != null) UnseatMayor(m, false);
                Campaign.Active = false;
            }
            if (b.Type == BuildingType.Bank) CloseBank(b);
            if (b.Deposit > 0) { WithdrawFor(b, b.Deposit); b.Deposit = 0; b.BankId = -1; }
            foreach (var id in b.Residents.ToList()) { var p = GetCitizen(id); if (p != null) p.HomeId = -1; }
            foreach (var id in b.Employees.ToList()) { var p = GetCitizen(id); if (p != null) { p.WorkId = -1; p.Role = Role.Laborer; } }
            foreach (var p in People) if (p.WorkId == b.Id && p.Role == Role.Builder) p.WorkId = -1;
            if (d.IsBusiness && b.OwnerId >= 0)
            {
                var o = GetCitizen(b.OwnerId);
                if (o != null) { o.Money += b.Cash * 0.5f; if (o.WorkId == b.Id) { o.WorkId = -1; o.Role = Role.Laborer; } }
            }
            else if (d.IsBusiness) Treasury += Math.Max(0, b.Cash);   // a town-run business: its till comes back
            foreach (var p in People) if (p.WorkId == b.Id) p.FamilyHand = false;
            Buildings.Remove(b);
            MarkDirty(b);
            // Salvage: half the materials go back to town stores, plus anything that was stored here
            PutInTown(Good.Logs, b.Delivered[(int)Good.Logs] * 0.5f);
            PutInTown(Good.Stone, b.Delivered[(int)Good.Stone] * 0.5f);
            if (d.IsStorage) for (int g = 0; g < Balance.GoodCount; g++) PutInTown((Good)g, b.Stock[g]);
            foreach (var x in Buildings) { if (x.ContractorId == b.Id) x.ContractorId = -1; if (x.RepairBy == b.Id) { x.RepairBy = -1; x.RepairPaid = false; } }
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
        public const int WagonX = Cols / 2 + 5, WagonY = Rows / 2 - 1;   // where the settlers stop: a little east of the map centre
        public const int TrailRow = WagonY + 2;   // the wagon trail across the map: the main road

        /// <summary>A handful of families arrive with a supply wagon in early spring. No houses yet.</summary>
        public static Town CreateNew(int seed)
        {
            var t = new Town { Seed = seed, Treasury = Balance.StartingTreasury };
            t.Terrain = TerrainGen.Generate(seed, Cols, Rows);
            // Open meadow around the camp
            for (int y = WagonY - 4; y <= WagonY + 7; y++)
                for (int x = WagonX - 8; x <= WagonX + 10; x++)
                {
                    double d = Math.Sqrt(Math.Pow((x - WagonX - 1) / 1.5, 2) + Math.Pow(y - WagonY - 1.5, 2));
                    if (d < 6.5 && t.TerrainAt(x, y) != Tile.Water) t.Terrain[y * Cols + x] = Tile.Grass;
                }
            // The wagon trail they came in on
            string err;
            for (int x = 0; x < Cols; x++)
            {
                if (t.TerrainAt(x, TrailRow) != Tile.Water) t.Terrain[TrailRow * Cols + x] = Tile.Grass;
                t.Place(BuildingType.Road, x, TrailRow, out err, true);
            }
            var wagon = t.Place(BuildingType.Wagon, WagonX, WagonY, out err, true);
            for (int g = 0; g < Balance.GoodCount; g++) wagon.Stock[g] = Balance.StartingStock[g];

            // Four families
            for (int f = 0; f < 4; f++)
            {
                string fam = t.Pick(Defs.LastNames);
                var dad = t.AddPerson(false, fam, t.Rnd(22, 40), (float)Math.Round(t.Rnd(25, 60)));
                var mom = t.AddPerson(true, fam, t.Rnd(20, 36), (float)Math.Round(t.Rnd(10, 30)));
                dad.PartnerId = mom.Id; mom.PartnerId = dad.Id;
                int kids = f == 0 ? 0 : t.Rng.Next(1, 3);
                for (int k = 0; k < kids; k++) { var c = t.AddPerson(t.Chance(0.5), fam, t.Rnd(1, 9), 0); c.ParentId = c.MotherId = mom.Id; c.FatherId = dad.Id; }
                if (f == 0) t.FounderBuilderId = dad.Id;   // a builder by trade
            }
            // and two single folk
            t.AddPerson(false, t.Pick(Defs.LastNames), t.Rnd(18, 30), (float)Math.Round(t.Rnd(15, 40)));
            t.AddPerson(true, t.Pick(Defs.LastNames), t.Rnd(18, 30), (float)Math.Round(t.Rnd(15, 40)));

            t.Hiring();
            t.DirtyTiles.Clear();
            t.Log.Clear();
            t.AddLog("Four families and two drifters stopped by the creek with what one wagon could carry.", LogKind.Good);
            var builder = t.GetCitizen(t.FounderBuilderId);
            if (builder != null) t.AddLog(builder.Name + " is a builder by trade and brought his tools. Pick a spot for his Builder's Yard (Industry). Until it stands, the town hires settlers by the day to build.", LogKind.Build);
            t.AddLog("Tents won't keep anyone warm through autumn and winter. Get everyone under a roof before the cold comes.", LogKind.Bad);
            t.AddLog("Get roofs over their heads before winter: a boarding house is quickest, cottages let families grow. A woodcutter keeps them warm. Food runs out faster than you think.", LogKind.Info);
            return t;
        }
    }
}
