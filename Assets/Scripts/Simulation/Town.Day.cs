using System;
using System.Collections.Generic;
using System.Linq;

namespace FrontierLedger.Simulation
{
    public partial class Town
    {
        static readonly Good[] AllGoods = (Good[])Enum.GetValues(typeof(Good));

        /// <summary>One full day: the land, construction, production, trade, households, wages,
        /// crime, health, families, hiring, migration and elections.</summary>
        void SimulateDay()
        {
            int si = Season;
            foreach (var b in Buildings) b.Today.Clear();
            if (Errands == null) Errands = new List<Errand>();
            Errands.Clear();
            foreach (var p in People) { p.FedToday = 0; p.SaloonToday = -1; p.TaskId = -1; p.Chore = Chore.None; p.SeenToday = false; }

            GrowLand();
            Wear(si);
            Construct();
            UnloadWagon();
            Lessons(si);
            ChurchService();
            OddJobs(si);
            Produce(si);
            BankWithdrawals();
            Trade(si);
            Travellers(si);
            Households(si);
            if (DayOfSeason == 1 && si >= 2 && Homeless > 0)
                AddLog(si == 2 ? "Autumn, and " + Homeless + " settlers still sleep in tents. Without a roof they'll be freezing by winter."
                               : "Winter. " + Homeless + " settlers have no roof. By the fire with firewood in store they might last; without it they won't.", LogKind.Bad);
            Saloons();
            Barbers();
            Brothels();
            Payroll();
            int crimes = Crime();
            HealthAndMood(si, crimes);
            FamilyLife();
            Hiring();
            RehouseEveryone();
            Migration(si);
            Caravans();
            ElectionStep();
            if (Day % 7 == 0) Weekly();
            History.Add(new HistoryPoint { Day = Day, Treasury = Treasury, Population = People.Count, Food = TotalFood(), Firewood = TotalFirewood() });
            if (History.Count > 112) History.RemoveAt(0);
        }

        /// <summary>Everything people eat: food, bread and meat, wherever it is.</summary>
        public float TotalFood() { float s = 0; foreach (var b in Buildings) s += b.Stock[(int)Good.Food] + b.Stock[(int)Good.Bread] + b.Stock[(int)Good.Meat]; return s; }
        static float Pantry(Building h) { return h.Stock[(int)Good.Food] + h.Stock[(int)Good.Bread] + h.Stock[(int)Good.Meat]; }
        public float TotalFirewood() { return Buildings.Sum(b => b.Stock[(int)Good.Firewood]); }

        // =====================================================================
        // The land: saplings grow into trees, stumps rot away
        // =====================================================================
        void GrowLand()
        {
            for (int i = 0; i < Terrain.Length; i++)
            {
                byte t = Terrain[i];
                if (t == Tile.Sapling && Chance(Balance.SaplingGrowChance)) SetTerrain(i % Cols, i / Cols, Tile.Tree);
                else if (t == Tile.Stump && Chance(1.0 / 40)) SetTerrain(i % Cols, i / Cols, Tile.Grass);
            }
        }

        // =====================================================================
        // Construction: materials come from town stores (or are bought), builders do the work
        // =====================================================================
        /// <summary>Adults without a job who can take day work.</summary>
        bool DayLaborer(Citizen p) { return !p.IsChild && (p.Role == Role.Laborer || (p.Role == Role.Teacher && !SchoolInSession)) && p.Jail == 0 && p.Age < 66 && !LaidUp(p); }

        public static float LaborDays(BuildingDef d) { return d.Work + (d.Logs + d.Stone) / Balance.HaulPerDay; }
        public float ContractPriceFor(BuildingDef d) { return (float)Math.Round(LaborDays(d) * Policy.ContractRate, 2); }
        public IEnumerable<Building> Yards { get { return Businesses.Where(b => b.Type == BuildingType.BuilderYard && (b.OwnerId >= 0 || b.Employees.Count > 0)); } }

        /// <summary>The owner and hands at work in a business today.</summary>
        List<Citizen> CrewOf(Building y)
        {
            var crew = new List<Citizen>();
            var o = GetCitizen(y.OwnerId);
            if (o != null && o.WorkId == y.Id && o.Jail == 0 && !LaidUp(o)) crew.Add(o);
            foreach (var id in y.Employees) { var p = GetCitizen(id); if (p != null && p.Jail == 0 && !LaidUp(p)) crew.Add(p); }
            return crew;
        }

        /// <summary>One worker-day on a site: build if there are materials to build with, otherwise haul.
        /// Returns false when there's nothing they can do there.</summary>
        bool SiteWork(Building s, Citizen p)
        {
            var d = s.Def;
            float cap = d.Work * Math.Min(1f, s.MaterialsFraction) - s.Progress;
            if (cap > 0.001f) { s.Progress += Math.Min(cap, 1f); p.Chore = Chore.Build; p.TaskId = s.Id; return true; }
            Good g; float got = Haul(s, out g);
            if (got <= 0.001f) return false;   // nothing to build with until materials turn up
            p.Chore = g == Good.Logs ? Chore.HaulLogs : Chore.HaulStone; p.TaskId = s.Id;
            return true;
        }

        void Construct()
        {
            var sites = Sites.OrderBy(b => b.Id).ToList();
            var yards = Yards.ToList();
            if (yards.Count == 0) { DayLabor(sites); return; }
            // the treasury settles old bills first
            foreach (var y in yards)
                if (y.TownOwes > 0.01f && Treasury > 0)
                {
                    float pay = Math.Min(y.TownOwes, Treasury);
                    Treasury -= pay; LedgerThisWeek.Contracts += pay; y.TownOwes -= pay; y.Cash += pay; y.Week.Revenue += pay;
                }
            // new jobs go to whichever yard has the least on its books
            foreach (var s in sites)
                if (!yards.Any(y => y.Id == s.ContractorId)) Award(s, yards);
            foreach (var b in Buildings)
                if (b.Built && Wears(b) && b.Condition < Balance.RepairAt && !yards.Any(y => y.Id == b.RepairBy))
                {
                    var y = yards.OrderBy(x => Buildings.Count(q => q.RepairBy == x.Id || q.ContractorId == x.Id)).First();
                    b.RepairBy = y.Id; b.RepairPaid = false;
                }
            foreach (var y in yards)
            {
                var crew = CrewOf(y); int ci = 0, want = 0;
                var repairs = Buildings.Where(b => b.Built && b.RepairBy == y.Id).OrderBy(b => b.Condition).ToList();
                // badly worn buildings first, then new building, then the other repairs
                var jobs = new List<Building>();
                jobs.AddRange(repairs.Where(b => b.Condition < 40));
                jobs.AddRange(sites.Where(s => s.ContractorId == y.Id));
                jobs.AddRange(repairs.Where(b => b.Condition >= 40));
                foreach (var job in jobs)
                {
                    bool repair = job.Built;
                    if (repair ? !StartRepair(job, y) : !StartContract(job, y)) continue;   // waiting on payment
                    int max = repair ? 2 : Balance.SiteCrew(job.Def), n = 0;
                    want += max;
                    while (ci < crew.Count && n < max)
                    {
                        if (repair ? !RepairWork(job, crew[ci]) : !SiteWork(job, crew[ci])) break;
                        ci++; n++;
                        if (repair && job.RepairBy < 0) break;   // finished
                    }
                    if (!repair && job.Progress >= job.Def.Work - 0.001f && job.MaterialsFraction >= 0.999f) OnBuilt(job, false);
                }
                for (; ci < crew.Count; ci++) Craft(y, crew[ci]);   // the rest make furniture
                y.Backlog = want;
                // hire for the work in hand, as far as the till allows (the owner works too)
                int hire = Math.Min(y.Def.MaxEmployees, Math.Max(0, want - 1));
                hire = Math.Min(hire, (int)(y.Cash / Math.Max(0.5f, y.Wage * 3)));
                y.Desired = Math.Max(y.Desired, hire);
            }
        }

        void Award(Building s, List<Building> yards)
        {
            var y = yards.OrderBy(x => Buildings.Count(q => q.ContractorId == x.Id || q.RepairBy == x.Id)).ThenBy(x => x.Id).First();
            s.ContractorId = y.Id;
            if (s.ContractPaid <= 0) s.ContractPrice = ContractPriceFor(s.Def);
            var o = GetCitizen(y.OwnerId);
            AddLog((o != null ? o.Name : "The builder's yard") + " took the contract for the " + s.Def.Name + " at " + Money(s.ContractPrice) + ".", LogKind.Build);
        }

        /// <summary>The town pays half the contract when work begins.</summary>
        bool StartContract(Building s, Building y)
        {
            if (s.ContractPaid > 0 || s.ContractPrice <= 0) return true;
            float half = s.ContractPrice * 0.5f;
            if (Treasury < half) return false;
            Treasury -= half; LedgerThisWeek.Contracts += half;
            y.Cash += half; y.Week.Revenue += half; s.ContractPaid = half;
            return true;
        }

        /// <summary>Buildings that wear and get repaired.</summary>
        public static bool Wears(Building b)
        {
            var t = b.Type;
            return t != BuildingType.Road && t != BuildingType.Wagon && t != BuildingType.Farm && t != BuildingType.Cemetery;
        }

        float RepairDays(Building b) { return Math.Max(0.5f, (100f - b.Condition) / 100f * b.Def.Work * 0.5f); }
        public float RepairPrice(Building b) { return (float)Math.Round(RepairDays(b) * Policy.ContractRate, 2); }

        /// <summary>Repairs are paid in full before work starts: by the owner for a business,
        /// by the town for everything else.</summary>
        bool StartRepair(Building b, Building y)
        {
            if (b.RepairPaid) return true;
            float price = RepairPrice(b);
            if (b.Def.IsBusiness && b.OwnerId >= 0)
            {
                if (b.Cash < price) return false;
                b.Cash -= price; b.Week.Costs += price;
            }
            else
            {
                if (Treasury < price) return false;
                Treasury -= price; LedgerThisWeek.Contracts += price;
            }
            y.Cash += price; y.Week.Revenue += price; b.RepairPaid = true;
            return true;
        }

        bool RepairWork(Building b, Citizen p)
        {
            p.Chore = Chore.Repair; p.TaskId = b.Id;
            b.Condition = Math.Min(100f, b.Condition + 100f / Math.Max(1f, b.Def.Work * 0.5f));
            if (b.Condition >= 99.5f) { b.Condition = 100f; b.RepairBy = -1; b.RepairPaid = false; }
            return true;
        }

        /// <summary>A quiet day at the yard: turn logs into furniture.</summary>
        void Craft(Building y, Citizen p)
        {
            p.Chore = Chore.Craft; p.TaskId = y.Id;
            if (y.Stock[(int)Good.Furniture] >= Balance.FurnitureCap) return;
            float logs = Math.Min(y.Stock[(int)Good.Logs], Balance.FurnitureLogs * Balance.FurniturePerWorker);
            if (logs < 0.01f) return;
            y.Stock[(int)Good.Logs] -= logs;
            Make(y, Good.Furniture, logs / Balance.FurnitureLogs * ToolFactor(y, 1));
        }

        /// <summary>Once a storehouse stands, settlers without a job carry the supply wagon's load into it, paid by the day.
        /// With nobody free, folk do it a bit at a time after work. The empty wagon is sold to a passing freighter.</summary>
        void UnloadWagon()
        {
            var wagon = OfType(BuildingType.Wagon).FirstOrDefault();
            if (wagon == null) return;
            var stores = OfType(BuildingType.Storehouse).ToList();
            if (stores.Count == 0) return;
            if (wagon.Stock.Sum() > 0.01f)
            {
                var hands = People.Where(p => DayLaborer(p) && p.TaskId < 0).Take(4).ToList();
                float can = Balance.UnloadPerDay * Math.Max(1, hands.Count), moved = 0;
                Building into = null;
                foreach (var s in stores.OrderByDescending(x => x.Def.Capacity - x.Stock.Sum()))
                    for (int g = 0; g < Balance.GoodCount && can - moved > 0.01f; g++)
                    {
                        float q = Math.Min(Math.Min(wagon.Stock[g], s.Def.Capacity - s.Stock.Sum()), can - moved);
                        if (q <= 0.01f) continue;
                        wagon.Stock[g] -= q; s.Stock[g] += q; moved += q;
                        if (into == null) into = s;
                    }
                if (moved <= 0.01f) return;   // the storehouse is full: the rest waits in the wagon, which the town uses up first
                foreach (var p in hands.Take((int)Math.Ceiling(moved / Balance.UnloadPerDay - 0.01f))) { p.Chore = Chore.Unload; p.TaskId = into.Id; }
                if (wagon.Stock.Sum() > 0.01f) return;
            }
            Buildings.Remove(wagon);
            MarkDirty(wagon);
            Treasury += Balance.WagonSale; LedgerThisWeek.Exports += Balance.WagonSale;
            AddLog("The supply wagon is unloaded. A passing freighter bought the empty wagon for " + Money(Balance.WagonSale) + ".", LogKind.Good);
        }

        /// <summary>Without a builder's yard the town hires settlers without a job by the day.</summary>
        void DayLabor(List<Building> sites)
        {
            if (sites.Count == 0) return;
            var hands = People.Where(DayLaborer).OrderBy(p => p.Id).ToList();
            int hires = Math.Min(hands.Count, Policy.Builders);
            if (Policy.BuilderWage > 0) hires = Math.Min(hires, (int)(Math.Max(0, Treasury) / Policy.BuilderWage));
            int hi = 0;
            foreach (var s in sites)
            {
                var d = s.Def;
                int crew = 0, max = Balance.SiteCrew(d);
                while (hi < hires && crew < max)
                {
                    if (!SiteWork(s, hands[hi])) break;
                    hi++; crew++;
                }
                if (s.Progress >= d.Work - 0.001f && s.MaterialsFraction >= 0.999f) OnBuilt(s, false);
            }
        }

        /// <summary>Wear and tear: a little every day, more in winter. Empty cottages wear slower.</summary>
        void Wear(int si)
        {
            foreach (var b in Buildings)
            {
                if (!b.Built || !Wears(b)) continue;
                float w = Balance.WearPerDay * Balance.WearBySeason[si] * (b.Type == BuildingType.House && b.Residents.Count == 0 ? 0.5f : 1f);
                float before = b.Condition;
                b.Condition = Math.Max(0f, b.Condition - w);
                if (before >= 25f && b.Condition < 25f)
                    AddLog("The " + b.Def.Name + (b.Type == BuildingType.House ? " #" + b.Id : "") + " is falling apart" + (b.Type == BuildingType.House ? ". The roof leaks." : " and getting little done.") + " It needs a builder.", LogKind.Bad);
            }
        }

        /// <summary>Worn buildings get less done.</summary>
        static float ConditionFactor(Building b) { return b.Condition >= 50f ? 1f : b.Condition >= 25f ? 0.85f : 0.6f; }

        /// <summary>One laborer's day of carrying logs (then stone) from the town stores to a site.
        /// Materials the stores lack are bought from producers with town money.</summary>
        float Haul(Building s, out Good carried)
        {
            var d = s.Def; float load = Balance.HaulPerDay, total = 0; carried = Good.Logs;
            foreach (var g in new[] { Good.Logs, Good.Stone })
            {
                float need = Math.Min(load, (g == Good.Logs ? d.Logs : d.Stone) - s.Delivered[(int)g]);
                if (need <= 0.001f) continue;
                float got = TakeFromTown(g, need);
                if (need - got > 0.001f && Treasury > 0)
                {
                    float budget = Treasury;
                    got += BuyFromProducers(g, need - got, ref budget, null);
                    LedgerThisWeek.Materials += Treasury - budget; Treasury = budget;
                }
                if (got <= 0.001f) continue;
                if (total <= 0.001f) carried = g;
                s.Delivered[(int)g] += got; load -= got; total += got;
                if (load <= 0.001f) break;
            }
            return total;
        }

        // =====================================================================
        // Idle hands: laborers (and builders with nothing to build) help bring in the harvest
        // in autumn, and otherwise forage and pick up deadwood for their own household.
        // =====================================================================
        void OddJobs(int si)
        {
            var idle = People.Where(p => DayLaborer(p) && p.TaskId < 0).ToList();
            if (idle.Count == 0) return;
            int k = 0;
            if (si == 2)
                foreach (var f in Businesses.Where(b => b.Type == BuildingType.Farm && b.CropLeft > 1).OrderByDescending(b => b.CropLeft).ToList())
                    for (int hands = 0; hands < 4 && k < idle.Count && f.CropLeft > 1; hands++)
                    {
                        var p = idle[k++];
                        p.TaskId = f.Id; p.Chore = Chore.Harvest;
                        float h = Math.Min(f.CropLeft, Balance.HarvestPerWorker * Balance.HelperHarvest * FoodBonus);
                        f.CropLeft -= h; Make(f, Good.Food, h);
                        float pay = Math.Max(0, Math.Min(f.Cash, f.Wage * Balance.HelperPay));
                        f.Cash -= pay; f.Week.Costs += pay; p.Money += pay;
                    }
            for (; k < idle.Count; k++)
            {
                var home = GetBuilding(idle[k].HomeId);
                if (home == null) continue;
                home.Stock[(int)Good.Food] += Balance.ForagePerDay * Balance.GatherBySeason[si];
                home.Stock[(int)Good.Firewood] += Balance.DeadwoodPerDay;
            }
            ChildrenHelp(si);
        }

        /// <summary>Children of six and up who aren't at school pick berries and gather kindling for the household.</summary>
        void ChildrenHelp(int si)
        {
            foreach (var c in People)
            {
                if (!c.IsChild || c.Age < 6f || c.Chore == Chore.School || LaidUp(c)) continue;
                var home = GetBuilding(c.HomeId);
                if (home == null) continue;
                home.Stock[(int)Good.Food] += Balance.ChildForage * Balance.GatherBySeason[si];
                home.Stock[(int)Good.Firewood] += Balance.ChildKindling;
            }
        }

        // =====================================================================
        // School: in the summer and winter terms, the teacher takes the children
        // =====================================================================
        void Lessons(int si)
        {
            if (!SchoolInSession) return;
            foreach (var s in OfType(BuildingType.School).ToList())
            {
                var teacher = StaffOf(s, Role.Teacher);
                if (teacher == null || teacher.Jail > 0 || LaidUp(teacher)) continue;
                teacher.TaskId = s.Id; teacher.Chore = Chore.School;
                int seats = s.Def.Capacity, n = 0;
                foreach (var c in People.Where(c => c.IsChild && c.Age >= Balance.SchoolAge && c.Chore != Chore.School && !LaidUp(c)).OrderByDescending(c => c.Age))
                {
                    if (n >= seats) break;
                    c.TaskId = s.Id; c.Chore = Chore.School; n++;
                    c.Schooling = Math.Min(1f, c.Schooling + 1f / Balance.SchoolDaysFull);
                }
                s.Today.Sold = n;   // pupils today
            }
        }

        // =====================================================================
        // Production
        // =====================================================================
        float ToolFactor(Building b, int workers)
        {
            float f = b.Stock[(int)Good.Tools] > 0.01f ? 1f : Balance.NoToolsFactor;
            b.Stock[(int)Good.Tools] = Math.Max(0, b.Stock[(int)Good.Tools] - workers * Balance.ToolWearPerWorker);
            return f * ConditionFactor(b);
        }

        float FoodBonus { get { return MayorBonus(Platform.Farm) ? 1.15f : 1f; } }

        /// <summary>The work a business's people get done in a day: hands (and owners) without schooling a little less.</summary>
        float Effort(Building b)
        {
            float e = 0;
            foreach (var id in b.Employees) { var p = GetCitizen(id); if (p != null && p.Jail == 0 && !LaidUp(p)) e += Schooled(p) ? 1f : Balance.UnschooledFactor; }
            var o = GetCitizen(b.OwnerId);
            if (o != null && o.WorkId == b.Id && o.Jail == 0 && !LaidUp(o)) e += Schooled(o) ? 1f : Balance.UnschooledFactor;
            return e;
        }

        void Produce(int si)
        {
            foreach (var b in Businesses.ToList())
            {
                int w = WorkersAt(b); float e = Effort(b);
                switch (b.Type)
                {
                    case BuildingType.Farm: Farm(b, w, e, si); break;
                    case BuildingType.Gatherer:
                        if (w > 0) Make(b, Good.Food, e * Balance.GatherPerWorker * ToolFactor(b, w) * Balance.GatherBySeason[si] * Math.Min(1f, TreesNear(b) / 25f) * FoodBonus);
                        break;
                    case BuildingType.Fishery:
                        if (w > 0) Make(b, Good.Food, e * Balance.FishPerWorker * ToolFactor(b, w) * Balance.FishBySeason[si] * FoodBonus);
                        break;
                    case BuildingType.LumberCamp: Lumber(b, w, e); break;
                    case BuildingType.Woodcutter:
                        if (w > 0)
                        {
                            float tf = ToolFactor(b, w);
                            float logs = Math.Min(b.Stock[(int)Good.Logs], e * Balance.LogsSplitPerWorker * tf);
                            if (b.Stock[(int)Good.Firewood] < Balance.BusinessStockCap)
                            { b.Stock[(int)Good.Logs] -= logs; Make(b, Good.Firewood, logs * Balance.FirewoodPerLog); }
                        }
                        break;
                    case BuildingType.Quarry: Quarry(b, w, e); break;
                    case BuildingType.Smithy:
                        if (w > 0)
                        {
                            float tf = ToolFactor(b, w), cap = e * Balance.ToolsPerWorker * tf;
                            if (b.Stock[(int)Good.Tools] >= 30) break;
                            float n = Math.Min(Math.Min(b.Stock[(int)Good.Logs], b.Stock[(int)Good.Iron]), cap * Balance.IronToolsFactor);
                            if (n > 0.01f) { b.Stock[(int)Good.Logs] -= n; b.Stock[(int)Good.Iron] -= n; Make(b, Good.Tools, n); break; }
                            // no iron: bog iron dug locally, worked with logs and stone
                            n = Math.Min(Math.Min(b.Stock[(int)Good.Logs], b.Stock[(int)Good.Stone]), cap);
                            if (n > 0.01f) { b.Stock[(int)Good.Logs] -= n; b.Stock[(int)Good.Stone] -= n; Make(b, Good.Tools, n); }
                        }
                        break;
                    case BuildingType.Ranch:
                        if (w > 0)
                        {
                            float tf = ToolFactor(b, w), pasture = Math.Min(1f, PastureNear(b) / (float)Balance.PastureFull);
                            Make(b, Good.Cattle, e * Balance.CattleBySeason[si] * tf * pasture * FoodBonus);
                            Make(b, Good.Wool, e * Balance.WoolBySeason[si] * tf * pasture);
                        }
                        break;
                    case BuildingType.Butcher:
                        if (w > 0 && b.Stock[(int)Good.Meat] < b.Def.Capacity - Balance.MeatPerSteer)
                        {
                            float head = Math.Min(b.Stock[(int)Good.Cattle], e * Balance.SteersPerWorker * ConditionFactor(b));
                            if (head > 0.01f) { b.Stock[(int)Good.Cattle] -= head; Make(b, Good.Meat, head * Balance.MeatPerSteer); b.Stock[(int)Good.Hides] += head; }
                        }
                        break;
                    case BuildingType.Bakery:
                        if (w > 0 && b.Stock[(int)Good.Bread] < b.Def.Capacity - 4)
                        {
                            float flour = Math.Min(b.Stock[(int)Good.Food], Math.Min(e * Balance.BreadPerWorker * ConditionFactor(b), b.Def.Capacity - b.Stock[(int)Good.Bread]));
                            if (flour > 0.01f) { b.Stock[(int)Good.Food] -= flour; Make(b, Good.Bread, flour); }
                        }
                        break;
                    case BuildingType.Brewery:
                        if (w > 0)
                        {
                            float tf = ToolFactor(b, w);
                            if (b.Stock[(int)Good.Beer] >= StockCap(b, Good.Beer)) break;   // beer doesn't pay to send east
                            float grain = Math.Min(b.Stock[(int)Good.Food], e * Balance.GrainPerWorkerBrew * tf);
                            grain = Math.Min(grain, b.Stock[(int)Good.Firewood] / (Balance.FuelPerBeer * Balance.BeerPerGrain));
                            if (grain > 0.01f)
                            {
                                b.Stock[(int)Good.Food] -= grain; b.Stock[(int)Good.Firewood] -= grain * Balance.BeerPerGrain * Balance.FuelPerBeer;
                                Make(b, Good.Beer, grain * Balance.BeerPerGrain);
                            }
                        }
                        break;
                    case BuildingType.IronMine:
                        if (w > 0)
                        {
                            float tf = ToolFactor(b, w);
                            float n = Math.Min(e * Balance.IronPerWorker * tf, b.Stock[(int)Good.Firewood] / Balance.FuelPerIron);
                            if (n > 0.01f) { b.Stock[(int)Good.Firewood] -= n * Balance.FuelPerIron; Make(b, Good.Iron, n); }
                        }
                        break;
                    case BuildingType.Tannery:
                        if (w > 0)
                        {
                            float tf = ToolFactor(b, w);
                            float n = Math.Min(b.Stock[(int)Good.Hides], e * Balance.LeatherPerWorker * tf);
                            if (n > 0.01f) { b.Stock[(int)Good.Hides] -= n; Make(b, Good.Leather, n); }
                        }
                        break;
                    case BuildingType.Tailor:
                        if (w > 0 && b.Stock[(int)Good.Clothes] < Balance.ClothesCap)
                        {
                            // coats and dresses from wool, boots and jackets from leather: half and half when there's both
                            float cap = e * Balance.ClothesPerWorker * ConditionFactor(b);
                            float fromLeather = Math.Min(cap * 0.5f, b.Stock[(int)Good.Leather] / Balance.LeatherPerClothes);
                            float fromWool = Math.Min(cap - fromLeather, b.Stock[(int)Good.Wool] / Balance.WoolPerClothes);
                            fromLeather = Math.Min(cap - fromWool, b.Stock[(int)Good.Leather] / Balance.LeatherPerClothes);
                            b.Stock[(int)Good.Wool] -= fromWool * Balance.WoolPerClothes;
                            b.Stock[(int)Good.Leather] -= fromLeather * Balance.LeatherPerClothes;
                            Make(b, Good.Clothes, fromWool + fromLeather);
                        }
                        break;
                }
            }
        }

        void Make(Building b, Good g, float q)
        {
            if (q <= 0) return;
            b.Stock[(int)g] += q; b.Today.Made += q; b.Week.Made += q;
        }

        void Farm(Building b, int w, float e, int si)
        {
            int dayOfSeason = DayOfSeason;
            if (si == 2 && dayOfSeason == 1)
            {
                b.CropLeft = b.CropGrowth * Balance.FieldYield * FoodBonus;
                if (b.CropLeft > 1) AddLog("Harvest time. The " + b.Def.Name + " has " + Math.Round(b.CropLeft) + " food standing in the field.", LogKind.Good);
            }
            if (si == 3 && dayOfSeason == 1)
            {
                if (b.CropLeft > 10) AddLog(Math.Round(b.CropLeft) + " food rotted in a field before it could be harvested.", LogKind.Bad);
                b.CropLeft = 0; b.CropGrowth = 0;
            }
            if (w == 0) return;
            float tf = ToolFactor(b, w);
            if (si <= 1) b.CropGrowth = Math.Min(1f, b.CropGrowth + e * Balance.GrowthPerWorker * tf);
            else if (si == 2 && b.CropLeft > 0)
            {
                float h = Math.Min(b.CropLeft, e * Balance.HarvestPerWorker * tf);
                b.CropLeft -= h; Make(b, Good.Food, h);
            }
        }

        int NearestTile(Building b, int r, byte kind)
        {
            var d = b.Def; int cx = b.X + d.W / 2, cy = b.Y + d.H / 2, best = -1, bd = int.MaxValue;
            for (int y = cy - r; y <= cy + r; y++)
                for (int x = cx - r; x <= cx + r; x++)
                {
                    int dd = (x - cx) * (x - cx) + (y - cy) * (y - cy);
                    if (dd > r * r || TerrainAt(x, y) != kind) continue;
                    if (dd < bd) { bd = dd; best = y * Cols + x; }
                }
            return best;
        }

        void Lumber(Building b, int w, float e)
        {
            if (w == 0) return;
            float tf = ToolFactor(b, w);
            var d = b.Def;
            if (b.Stock[(int)Good.Logs] < Balance.BusinessStockCap)
            {
                b.ChopWork += e * Balance.TreeWorkPerWorker * tf;
                while (b.ChopWork >= 1f)
                {
                    if (b.TargetTile < 0 || Terrain[b.TargetTile] != Tile.Tree) b.TargetTile = NearestTile(b, d.Radius, Tile.Tree);
                    if (b.TargetTile < 0) { b.ChopWork = 0; break; }
                    SetTerrain(b.TargetTile % Cols, b.TargetTile / Cols, Tile.Stump);
                    b.TargetTile = -1; b.ChopWork -= 1f;
                    Make(b, Good.Logs, Balance.LogsPerTree);
                }
            }
            // Foresting: replant stumps and open ground so the woods come back
            int cx = b.X + d.W / 2, cy = b.Y + d.H / 2;
            int growing = CountInRadius(cx, cy, d.Radius, t => t == Tile.Tree || t == Tile.Sapling);
            for (int i = 0; i < w && growing < 46; i++)
            {
                int spot = NearestTile(b, d.Radius, Tile.Stump);
                if (spot < 0)
                {
                    int x = cx + Rng.Next(-d.Radius, d.Radius + 1), y = cy + Rng.Next(-d.Radius, d.Radius + 1);
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) > d.Radius * d.Radius || TerrainAt(x, y) != Tile.Grass || OccupantAt(x, y) != null) continue;
                    if (OccupantAt(x - 1, y) != null || OccupantAt(x + 1, y) != null || OccupantAt(x, y - 1) != null || OccupantAt(x, y + 1) != null) continue;
                    spot = y * Cols + x;
                }
                if (Chance(0.5)) { SetTerrain(spot % Cols, spot / Cols, Tile.Sapling); growing++; }
            }
        }

        void Quarry(Building b, int w, float e)
        {
            if (w == 0 || b.Stock[(int)Good.Stone] >= Balance.BusinessStockCap) return;
            float tf = ToolFactor(b, w);
            var d = b.Def;
            if (b.TargetTile < 0 || Terrain[b.TargetTile] != Tile.Rock) b.TargetTile = NearestTile(b, d.Radius, Tile.Rock);
            if (b.TargetTile >= 0)
            {
                b.ChopWork += e * 0.5f * tf;
                if (b.ChopWork >= 1f)
                {
                    SetTerrain(b.TargetTile % Cols, b.TargetTile / Cols, Tile.Grass);
                    b.TargetTile = -1; b.ChopWork = 0;
                    Make(b, Good.Stone, Balance.StonePerRock);
                }
            }
            else Make(b, Good.Stone, e * Balance.StonePerWorker * tf);
        }

        // =====================================================================
        // Trade between businesses, the town and the freight wagon
        // =====================================================================
        float Spend(Building b) { return Math.Max(0, b.Cash - b.Wage * Math.Max(1, b.Employees.Count)); }

        void BuyFor(Building b, Good g, float want, BuildingType? only = null)
        {
            if (want <= 0.01f) return;
            float budget = Spend(b), before = budget;
            float got = BuyFromProducers(g, want, ref budget, b, only);
            float spent = before - budget;
            b.Cash -= spent; b.Week.Costs += spent; b.Stock[(int)g] += got; b.Today.Bought += got;
            // The town will sell from its stores what no business can: materials, tools, iron and wool ordered from
            // back east, and grain from the granary above its winter reserve
            bool fromStores = g == Good.Tools || g == Good.Logs || g == Good.Stone || g == Good.Iron || g == Good.Wool || g == Good.Leather || (g == Good.Food && only == BuildingType.Farm);
            if (got < want - 0.01f && fromStores)
            {
                float reserve = g == Good.Tools ? 4 : g == Good.Food ? Math.Max(Policy.Reserve[0], People.Count * 10f) : Policy.Reserve[(int)g];
                float spare = Math.Max(0, TownStock(g) - reserve);
                float price = Balance.Wholesale[(int)g];
                float q = Math.Min(Math.Min(want - got, spare), Spend(b) / price);
                if (q > 0.01f)
                {
                    TakeFromTown(g, q); b.Stock[(int)g] += q;
                    var hand = CrewOf(b).FirstOrDefault(); var store = OfType(BuildingType.Storehouse).FirstOrDefault();
                    if (hand != null && store != null) Shop(hand, store, g);
                    b.Cash -= q * price; b.Week.Costs += q * price; Treasury += q * price; LedgerThisWeek.StoreSales += q * price;
                }
            }
        }

        /// <summary>Owners keep their till stocked from their own savings so the business can pay
        /// wages and buy what it needs.</summary>
        void OwnersTopUp(List<Building> biz)
        {
            foreach (var b in biz)
            {
                var o = GetCitizen(b.OwnerId); if (o == null) continue;
                float need = TillFor(b);
                if (b.Cash >= need || Savings(o) <= 8) continue;
                float put = Math.Min(need - b.Cash, Savings(o) - 8);
                if (o.Money - 8 < put) WithdrawFor(o, put - Math.Max(0, o.Money - 8));
                put = Math.Min(put, o.Money - 8); if (put <= 0) continue;
                o.Money -= put; b.Cash += put;
            }
        }

        /// <summary>What a business keeps in its till for a couple of days' wages and supplies.</summary>
        float TillFor(Building b)
        {
            return b.Wage * (b.Employees.Count + 1) * 2 + (b.Def.IsRetail ? 60 : BuysSupplies(b.Type) ? 25 : 8);   // shops need money to restock
        }

        /// <summary>Businesses that buy what they work: logs, iron, grain, hides, beer, firewood.</summary>
        static bool BuysSupplies(BuildingType t)
        {
            return t == BuildingType.Woodcutter || t == BuildingType.Smithy || t == BuildingType.Brewery || t == BuildingType.IronMine || t == BuildingType.Tannery
                || t == BuildingType.Saloon || t == BuildingType.Hotel || t == BuildingType.Barber;
        }

        /// <summary>Each morning businesses top up their tills from the bank and settlers their pockets; a town-run business
        /// takes its whole account back.</summary>
        void BankWithdrawals()
        {
            foreach (var b in Buildings)
            {
                if (b.Deposit <= 0) continue;
                if (b.OwnerId < 0 || !b.Def.IsBusiness) WithdrawFor(b, b.Deposit);
                else if (b.Cash < TillFor(b)) WithdrawFor(b, TillFor(b) - b.Cash);
            }
            foreach (var p in People)
                if (p.Deposit > 0 && p.Money < Balance.PocketMoney * 0.5f) WithdrawFor(p, Balance.PocketMoney - p.Money);
        }

        /// <summary>Sunday: businesses bank all but their till, settlers all but their pocket money, and savings earn interest.</summary>
        void BankDeposits()
        {
            var home = HomeBank;
            foreach (var b in Businesses)
            {
                if (b.OwnerId < 0 || b.Type == BuildingType.Bank) continue;
                var bank = GetBuilding(b.BankId) ?? home; if (bank == null || bank.OwnerId < 0) continue;
                float extra = b.Cash - TillFor(b) * 1.5f;
                if (extra < 1f) continue;
                b.Cash -= extra; b.Deposit += extra; b.BankId = bank.Id; bank.Cash += extra;
            }
            foreach (var p in People)
            {
                if (p.IsChild) continue;
                var bank = GetBuilding(p.BankId) ?? home; if (bank == null || bank.OwnerId < 0) continue;
                float extra = p.Money - Balance.PocketMoney;
                if (extra < 2f) continue;
                p.Money -= extra; p.Deposit += extra; p.BankId = bank.Id; bank.Cash += extra;
            }
            // savers get a share of what the bank earned on its loans this week, up to the usual rate; it's added to their accounts
            foreach (var bank in Banks.ToList())
            {
                float deposits = DepositsAt(bank);
                if (deposits < 1f) continue;
                float rate = Math.Min(Balance.DepositInterest, Balance.DepositShare * Math.Max(0, bank.Last.Revenue) / deposits);
                if (rate <= 0) continue;
                foreach (var p in People) if (p.BankId == bank.Id && p.Deposit > 0) { float i = p.Deposit * rate; p.Deposit += i; bank.Week.Costs += i; }
                foreach (var b in Buildings) if (b.BankId == bank.Id && b.Deposit > 0) { float i = b.Deposit * rate; b.Deposit += i; bank.Week.Costs += i; }
            }
        }

        public float LoansOut(Building bank) { float s = 0; foreach (var b in Buildings) if (b.LenderId == bank.Id) s += b.Debt; return s; }

        float SpareLogs() { return OfType(BuildingType.LumberCamp).Sum(b => b.Stock[(int)Good.Logs]); }

        /// <summary>The treasury keeps the tills of town-run businesses stocked for wages and supplies.</summary>
        void TownTopUp(List<Building> biz)
        {
            foreach (var b in biz)
            {
                if (b.OwnerId >= 0 || Treasury <= 0) continue;
                float need = b.Wage * Math.Max(1, Math.Max(b.Employees.Count, b.Desired)) * 2 + (BuysSupplies(b.Type) || b.Def.IsRetail || b.Type == BuildingType.BuilderYard ? 25 : 8);
                if (b.Cash >= need) continue;
                float put = Math.Min(need - b.Cash, Treasury);
                Treasury -= put; LedgerThisWeek.TownTills += put; b.Cash += put;
            }
        }

        void Trade(int si)
        {
            float xF = MayorBonus(Platform.Trade) ? 1.25f : 1f;
            var biz = Businesses.ToList();
            OwnersTopUp(biz);
            TownTopUp(biz);
            // Inputs and tools
            foreach (var b in biz)
            {
                int w = WorkersAt(b);
                if (b.Type == BuildingType.Woodcutter) BuyFor(b, Good.Logs, 15 - b.Stock[(int)Good.Logs]);
                Supplies(b, si);
                // the yard only buys logs for furniture when the lumber camps have plenty to spare and homes are warm
                if (b.Type == BuildingType.BuilderYard && b.Stock[(int)Good.Furniture] < Balance.FurnitureCap && SpareLogs() > 20 && TotalFirewood() > 80)
                    BuyFor(b, Good.Logs, 6 - b.Stock[(int)Good.Logs]);
                bool usesTools = !b.Def.IsRetail && b.Type != BuildingType.Saloon && b.Type != BuildingType.Smithy && b.Type != BuildingType.Hotel && b.Type != BuildingType.Barber && b.Type != BuildingType.Brothel;
                if (usesTools && w > 0 && b.Stock[(int)Good.Tools] < 0.5f * w) BuyFor(b, Good.Tools, w - b.Stock[(int)Good.Tools]);
            }
            // Shops restock
            foreach (var r in biz.Where(b => b.Def.IsRetail && IsOpen(b) && !MakesWhatItSells(b.Type)).OrderBy(x => Rng.Next()))
            {
                var d = r.Def; float per = d.Capacity / (float)d.Sells.Length;
                foreach (var g in d.Sells) BuyFor(r, g, per - r.Stock[(int)g]);
            }
            // The town's own fields, docks, camps and quarries fill its stores first: it doesn't buy from itself
            foreach (var b in biz.Where(x => x.OwnerId < 0 && x.Built))
                foreach (var g in AllGoods)
                {
                    if (!Produces(b.Type, g) || b.Stock[(int)g] < 0.5f) continue;
                    float reserve = g == Good.Food ? Math.Max(Policy.Reserve[0], People.Count * 10f) : Policy.Reserve[(int)g];
                    float q = Math.Min(Math.Min(b.Stock[(int)g], reserve - TownStock(g)), TownSpace);
                    if (q < 0.5f) continue;
                    b.Stock[(int)g] -= q; PutInTown(g, q);
                    if (q >= 1f) Deliver(b, OfType(BuildingType.Storehouse).FirstOrDefault(), g);
                }
            // The town keeps its reserves of firewood and building materials
            foreach (var g in AllGoods)
            {
                float want = Math.Min(Policy.Reserve[(int)g] - TownStock(g), TownSpace);
                if (want <= 0.5f || Treasury <= 0) continue;
                float budget = Math.Min(Treasury, 60f), before = budget;
                float got = BuyFromProducers(g, want, ref budget, null);
                Treasury -= before - budget; LedgerThisWeek.Materials += before - budget;
                PutInTown(g, got);
            }
            // Surplus: food goes to the town granary when there is room, everything else leaves on the freight wagon
            foreach (var b in biz)
            {
                foreach (var g in AllGoods)
                {
                    if (!Produces(b.Type, g)) continue;
                    float extra = b.Stock[(int)g] - StockCap(b, g);
                    if (extra <= 0) continue;
                    if (g == Good.Food && Treasury > 0)
                    {
                        float price = Balance.Export[0];
                        float q = Math.Min(Math.Min(extra, TownSpace), Treasury / price);
                        if (q > 0)
                        {
                            PutInTown(g, q); b.Stock[(int)g] -= q; extra -= q;
                            if (q >= 1f) Deliver(b, OfType(BuildingType.Storehouse).FirstOrDefault(), g);
                            Treasury -= q * price; LedgerThisWeek.Materials += q * price;
                            b.Cash += q * price; b.Week.Revenue += q * price;
                        }
                    }
                    if (extra > 0)
                    {
                        float c = extra * Balance.Export[(int)g] * xF;
                        b.Stock[(int)g] -= extra; b.Cash += c; b.Week.Revenue += c; b.Today.Exported += extra;
                    }
                }
            }
        }

        /// <summary>What each trade buys to work with each morning.</summary>
        void Supplies(Building b, int si)
        {
            switch (b.Type)
            {
                case BuildingType.Smithy:
                    BuyFor(b, Good.Logs, 8 - b.Stock[(int)Good.Logs]);
                    BuyFor(b, Good.Iron, 8 - b.Stock[(int)Good.Iron]);
                    if (b.Stock[(int)Good.Iron] < 1f) BuyFor(b, Good.Stone, 8 - b.Stock[(int)Good.Stone]);   // bog iron needs stone for the hearth
                    break;
                case BuildingType.Butcher: if (IsOpen(b)) BuyFor(b, Good.Cattle, 4 - b.Stock[(int)Good.Cattle]); break;
                case BuildingType.Bakery: if (IsOpen(b)) BuyFor(b, Good.Food, 24 - b.Stock[(int)Good.Food], BuildingType.Farm); break;
                case BuildingType.Brewery:
                    if (!IsOpen(b)) break;
                    if (People.Count == 0 || TotalFood() / People.Count >= Balance.BrewingNeedsFood) BuyFor(b, Good.Food, 12 - b.Stock[(int)Good.Food], BuildingType.Farm);
                    BuyFor(b, Good.Firewood, 6 - b.Stock[(int)Good.Firewood]);
                    break;
                case BuildingType.IronMine: if (IsOpen(b)) BuyFor(b, Good.Firewood, 12 - b.Stock[(int)Good.Firewood]); break;
                case BuildingType.Tannery: if (IsOpen(b)) BuyFor(b, Good.Hides, 10 - b.Stock[(int)Good.Hides]); break;
                case BuildingType.Tailor:
                    if (!IsOpen(b)) break;
                    BuyFor(b, Good.Wool, 8 - b.Stock[(int)Good.Wool]);
                    BuyFor(b, Good.Leather, 4 - b.Stock[(int)Good.Leather]);
                    break;
                case BuildingType.Saloon: BuyFor(b, Good.Beer, 30 - b.Stock[(int)Good.Beer]); break;
                case BuildingType.Hotel:
                    if (!IsOpen(b)) break;
                    BuyFor(b, Good.Meat, 6 - b.Stock[(int)Good.Meat]);
                    BuyFor(b, Good.Bread, 6 - b.Stock[(int)Good.Bread]);
                    BuyFor(b, Good.Food, 10 - b.Stock[(int)Good.Food]);
                    BuyFor(b, Good.Beer, 8 - b.Stock[(int)Good.Beer]);
                    BuyFor(b, Good.Firewood, (si == 1 ? 3 : 8) - b.Stock[(int)Good.Firewood]);
                    break;
                case BuildingType.Barber: if (IsOpen(b)) BuyFor(b, Good.Firewood, 8 - b.Stock[(int)Good.Firewood]); break;
            }
        }

        /// <summary>Takes a meal's worth from a larder: meat and bread first, then plain food. Returns how much was eaten.</summary>
        static float EatFrom(Building h, float amount)
        {
            float got = 0;
            foreach (var g in new[] { Good.Meat, Good.Bread, Good.Food })
            {
                float t = Math.Min(h.Stock[(int)g], amount - got);
                if (t <= 0) continue;
                h.Stock[(int)g] -= t; got += t;
                if (got >= amount - 0.001f) break;
            }
            return got;
        }

        /// <summary>Travellers come up the main road and put up at the hotels: a room, supper and a drink, paid with money
        /// from outside. More in summer, and more as the town grows.</summary>
        void Travellers(int si)
        {
            var hotels = Businesses.Where(b => b.Type == BuildingType.Hotel && IsOpen(b)).OrderBy(x => Rng.Next()).ToList();
            if (hotels.Count == 0) return;
            float draw = 1f + Math.Min(People.Count, 80) / 40f + (OfType(BuildingType.Saloon).Any(IsOpen) ? 0.2f : 0f);
            int coming = (int)Math.Round(Balance.TravellersBySeason[si] * draw * Rnd(0.5f, 1.5f));
            foreach (var h in hotels)
            {
                int asked = Math.Min(coming, h.Def.Capacity);
                h.Week.Made += asked;   // travellers asking for a room, served or not: the owner hires for them
                int n = Math.Min(asked, (int)(WorkersAt(h) * Balance.GuestsPerHand));
                if (n <= 0) continue;
                coming -= n;
                float take = n * Balance.RoomRate;
                for (int i = 0; i < n; i++) if (EatFrom(h, 1f) >= 0.99f) take += Balance.MealPrice;
                int drinks = Math.Min(n, (int)(h.Stock[(int)Good.Beer] / 0.5f));
                h.Stock[(int)Good.Beer] -= drinks * 0.5f; take += drinks * Balance.GuestDrink;
                h.Stock[(int)Good.Firewood] = Math.Max(0, h.Stock[(int)Good.Firewood] - n * (si == 1 ? 0.1f : 0.25f));
                h.Cash += take; h.Week.Revenue += take; h.Today.Sold += n; h.Week.Sold += n;
            }
        }

        /// <summary>Settlers with a little money go for a shave and a hot bath about once a week.</summary>
        void Barbers()
        {
            foreach (var b in OfType(BuildingType.Barber).Where(IsOpen).ToList())
            {
                int cap = WorkersAt(b) * Balance.CustomersPerHand;
                foreach (var p in Shuffle(People.Where(x => !x.IsChild && x.Jail == 0 && !LaidUp(x) && x.Money > 6f && Day - x.BathedDay >= 6).ToList()))
                {
                    if (!Chance(0.35)) continue;
                    b.Week.Made++;   // wanted a bath today, served or not: the owner hires for them
                    if (cap <= 0) continue;
                    bool bath = b.Stock[(int)Good.Firewood] >= Balance.BathFuel;   // no firewood, no hot water: just a shave
                    float price = bath ? Balance.BathPrice : Balance.ShavePrice;
                    p.Money -= price; b.Cash += price; b.Week.Revenue += price; b.Today.Sold++; b.Week.Sold++;
                    if (bath) { b.Stock[(int)Good.Firewood] -= Balance.BathFuel; p.BathedDay = Day; } else p.BathedDay = Day - 4;
                    p.Happy = Math.Min(100, p.Happy + (bath ? 2.5f : 1f));
                    Visit(p, b); cap--;
                }
            }
        }

        /// <summary>Men with money in their pocket visit the brothel now and then, single men more than married ones.
        /// Each visit's price is split: the house keeps the larger part, the woman is paid her share (taxed like a wage).</summary>
        void Brothels()
        {
            foreach (var b in OfType(BuildingType.Brothel).Where(IsOpen).ToList())
            {
                var women = b.Employees.Select(GetCitizen).Where(w => w != null && w.Jail == 0 && !LaidUp(w) && BrothelWorker(w)).ToList();
                var left = women.ToDictionary(w => w.Id, w => Balance.VisitsPerWoman);
                foreach (var p in Shuffle(People.Where(x => !x.Female && x.Age >= Balance.BrothelAge && x.Jail == 0 && !LaidUp(x) && x.Money > Balance.VisitPrice + 1f).ToList()))
                {
                    if (!Chance(p.PartnerId < 0 ? 0.3 : 0.06)) continue;
                    b.Week.Made++;   // came by, served or not: the house takes on women for the trade there is
                    var w = women.Where(x => left[x.Id] > 0).OrderByDescending(x => left[x.Id]).FirstOrDefault();
                    if (w == null) continue;
                    left[w.Id]--;
                    float price = Balance.VisitPrice, share = price * Balance.VisitShare, house = price - share;
                    p.Money -= price;
                    float tax = share * Policy.IncomeTax;
                    w.Money += share - tax; Treasury += tax; LedgerThisWeek.IncomeTax += tax;
                    b.Cash += house; b.Week.Revenue += house; b.Today.Sold++; b.Week.Sold++;
                    p.Happy = Math.Min(100, p.Happy + 3f);
                    Visit(p, b);
                }
                // men passing through: hotel guests, teamsters and drovers, paying with money from outside
                int travellers = Businesses.Where(h => h.Type == BuildingType.Hotel).Sum(h => (int)h.Today.Sold);
                for (int i = 0; i < travellers; i++)
                {
                    if (!Chance(0.4)) continue;
                    b.Week.Made++;
                    var w = women.Where(x => left[x.Id] > 0).OrderByDescending(x => left[x.Id]).FirstOrDefault();
                    if (w == null) continue;
                    left[w.Id]--;
                    float price = Balance.VisitPrice, share = price * Balance.VisitShare, house = price - share, tax = share * Policy.IncomeTax;
                    w.Money += share - tax; Treasury += tax; LedgerThisWeek.IncomeTax += tax;
                    b.Cash += house; b.Week.Revenue += house; b.Today.Sold++; b.Week.Sold++;
                }
            }
        }

        /// <summary>Sunday morning service: most of the town goes, and the plate goes round. On weekdays the preacher
        /// calls on the sick. (Worked out the day before, like everyone's plans, so the map shows it on the Sunday.)</summary>
        void ChurchService()
        {
            int sunday = Day + 1;
            foreach (var c in OfType(BuildingType.Church).ToList())
            {
                var rev = StaffOf(c, Role.Preacher);
                if (rev == null || rev.Jail > 0 || LaidUp(rev)) continue;
                rev.TaskId = c.Id; rev.Chore = Chore.Preach;
                if (sunday % 7 != 0)
                {
                    var sick = People.Where(p => p != rev && LaidUp(p) && p.HomeId >= 0).OrderByDescending(p => p.Illness).FirstOrDefault();
                    if (sick != null) { rev.TaskId = sick.HomeId; sick.Happy = Math.Min(100, sick.Happy + 2f); }
                    continue;
                }
                int n = 0; float plate = 0;
                foreach (var p in People.Where(x => x.Jail == 0 && !LaidUp(x) && x.ChurchDay != sunday && (x == rev || (x.Seed * 53f) % 1f < 0.75f)).OrderBy(x => x.HomeId).ToList())
                {
                    if (n >= c.Def.Capacity) break;
                    p.ChurchDay = sunday; n++;
                    p.Happy = Math.Min(100, p.Happy + 4f); p.Grief = Math.Max(0, p.Grief - 15f);
                    if (!p.IsChild && p != rev) { float g = Math.Min(0.5f, Math.Max(0, p.Money) * 0.04f); p.Money -= g; plate += g; }
                }
                // the town makes up a thin plate so the preacher can live
                float topUp = Math.Max(0, Math.Min(Balance.PlateTopUp - plate, Treasury));
                Treasury -= topUp; LedgerThisWeek.Salaries += topUp;
                rev.Money += plate + topUp;
                c.Attendance = n; c.Collection = plate; c.Today.Sold = n;
            }
        }

        /// <summary>Households buy a set of clothes for whoever's are worn through, when they can afford it.</summary>
        void BuyClothes()
        {
            if (Sources(Good.Clothes).Count == 0) return;   // no tailor and none in the stores: they make do and mend
            foreach (var h in Buildings.Where(b => b.Built && IsHome(b.Type)).ToList())
            {
                var rs = Household(h).ToList();
                var payers = rs.Where(p => !p.IsChild).ToList();
                if (payers.Count == 0) continue;
                int bought = 0;
                foreach (var p in rs.Where(x => x.Clothes < Balance.ClothesBuyBelow).OrderBy(x => x.Clothes).ToList())
                {
                    if (bought >= 2) break;
                    float spare = payers.Sum(x => Savings(x)) / payers.Count;
                    if (spare < Balance.ClothesSavings) break;
                    float price = Sources(Good.Clothes).Select(x => x.Price).DefaultIfEmpty(0f).Min();
                    if (price <= 0) break;
                    foreach (var x in payers) if (x.Money < price && x.Deposit > 0) WithdrawFor(x, price - x.Money);
                    float got = Purchase(Good.Clothes, 1f, payers);
                    if (got <= 0.01f) break;
                    p.Clothes = Math.Min(1.2f, p.Clothes + Balance.ClothesNewSet * got); bought++;
                }
            }
        }

        // =====================================================================
        // Households: gardens, buying food and firewood, eating, keeping warm
        // =====================================================================
        struct Source { public Building B; public float Price; public bool Town; }

        List<Source> Sources(Good g)
        {
            var list = new List<Source>();
            foreach (var r in Buildings)
                if (r.Built && r.Def.IsRetail && IsOpen(r) && Array.IndexOf(r.Def.Sells, g) >= 0 && r.Stock[(int)g] > 0.01f)
                    list.Add(new Source { B = r, Price = r.Price[(int)g] });
            // With no shop open, farmers, fishers and woodcutters sell at their own gate
            if (list.Count == 0)
                foreach (var b in Businesses)
                    if (Produces(b.Type, g) && b.Stock[(int)g] > 0.5f && (b.OwnerId >= 0 || WorkersAt(b) > 0))
                        list.Add(new Source { B = b, Price = Balance.Wholesale[(int)g] * 1.4f });
            // Furniture is sold straight from the builder's yards
            if (g == Good.Furniture)
                foreach (var y in Yards) if (y.Stock[(int)g] > 0.99f) list.Add(new Source { B = y, Price = y.Price[(int)g] });
            // The storehouse only sells when no shop has any left
            if (list.Count == 0 && Policy.StorehouseSells && Policy.StorehousePrice[(int)g] > 0)
                foreach (var s in Storages) if (s.Stock[(int)g] > 0.01f) list.Add(new Source { B = s, Price = Policy.StorehousePrice[(int)g], Town = true });
            return list.OrderBy(x => x.Price).ToList();
        }

        /// <summary>Buys up to qty of a good for a household, paid by its adults. Returns the amount bought.</summary>
        float Purchase(Good g, float qty, List<Citizen> payers)
        {
            float got = 0;
            foreach (var src in Sources(g))
            {
                if (qty - got <= 0.001f) break;
                float purse = payers.Sum(p => Math.Max(0, p.Money));
                float q = Math.Min(Math.Min(qty - got, src.B.Stock[(int)g]), purse / src.Price);
                if (q <= 0.001f) continue;
                float cost = q * src.Price;
                foreach (var p in payers.OrderByDescending(x => x.Money))
                {
                    float pay = Math.Min(Math.Max(0, p.Money), cost); p.Money -= pay; cost -= pay;
                    if (cost <= 0.0001f) break;
                }
                src.B.Stock[(int)g] -= q; got += q;
                // whoever isn't out at work does the shopping
                Shop(payers.OrderBy(x => Employed(x.Role) || x.Role == Role.Owner || x.Role == Role.Mayor ? 1 : 0).ThenBy(x => x.Id).FirstOrDefault(), src.B, g);
                float paid = q * src.Price;
                if (src.Town) { Treasury += paid; LedgerThisWeek.StoreSales += paid; }
                else { src.B.Cash += paid; src.B.Week.Revenue += paid; src.B.Today.Sold += q; src.B.Week.Sold += q; }
            }
            return got;
        }

        /// <summary>What it would cost a household to buy qty of a good today, as far as there's any for sale.</summary>
        float CostOf(Good g, float qty)
        {
            float cost = 0;
            foreach (var src in Sources(g))
            {
                if (qty <= 0.001f) break;
                float q = Math.Min(qty, src.B.Stock[(int)g]);
                cost += q * src.Price; qty -= q;
            }
            return cost;
        }

        /// <summary>An owner takes what the family needs out of the business, from whatever it holds beyond the float it
        /// works on (wages and supplies), till first, then its bank account. A banker only takes the bank's own money,
        /// never the savers'. Returns what was taken.</summary>
        float OwnerKeep(Building b, Citizen o, float amount)
        {
            float avail;
            if (b.Type == BuildingType.Bank) avail = Math.Max(0, Math.Min(b.Cash + LoansOut(b) - DepositsAt(b), b.Cash - VaultReserve(b)));
            else
            {
                float spare = b.Cash + b.Deposit - TillFor(b);
                if (spare <= 0.01f) return 0;
                if (b.Cash < Math.Min(amount, spare) && b.Deposit > 0) WithdrawFor(b, Math.Min(amount, spare) - b.Cash);
                avail = Math.Min(spare, b.Cash);
            }
            float t = Math.Min(amount, avail);
            if (t <= 0.01f) return 0;
            b.Cash -= t; o.Money += t; b.Week.Drawn += t;
            return t;
        }

        void Households(int si)
        {
            var report = new MarketReport();
            float heatSeason = Balance.FirewoodBySeason[si];
            foreach (var h in Buildings.Where(b => b.Built && IsHome(b.Type)))
            {
                var rs = Household(h).ToList();
                h.ColdToday = false;
                // a drafty cottage burns more; furniture wears out slowly
                float heatNeed = heatSeason * (h.Type == BuildingType.BoardingHouse ? 1.5f : 1f) * (h.Type != BuildingType.Estate ? (h.Condition < 25 ? 1.6f : h.Condition < 50 ? 1.25f : 1f) : 1f);
                h.Stock[(int)Good.Furniture] = Math.Max(0, h.Stock[(int)Good.Furniture] * (1 - Balance.FurnitureWear));
                if (rs.Count == 0) { h.GardenToday = 0; continue; }
                if (h.Type == BuildingType.House)
                {
                    h.GardenToday = Balance.GardenBase * Balance.GardenBySeason[si] * FoodBonus;
                    h.Stock[(int)Good.Food] += h.GardenToday; report.Garden += h.GardenToday;
                }
                float appetite = rs.Sum(p => p.IsChild ? Balance.ChildAppetite : 1f);
                var payers = rs.Where(p => !p.IsChild).ToList();
                // a family that farms, fishes, gathers, butchers, bakes or cuts firewood lives off its own work first
                var fam = payers.Select(FamilyBusiness).FirstOrDefault(x => x != null);
                if (fam != null && fam.Built)
                {
                    foreach (var g in new[] { Good.Meat, Good.Bread, Good.Food })
                        if (Produces(fam.Type, g)) { float t = Math.Min(fam.Stock[(int)g], Math.Max(0, appetite * 2f - Pantry(h))); fam.Stock[(int)g] -= t; h.Stock[(int)g] += t; }
                    if (Produces(fam.Type, Good.Firewood)) { float t = Math.Min(fam.Stock[(int)Good.Firewood], Math.Max(0, heatNeed * 2.5f - h.Stock[(int)Good.Firewood])); fam.Stock[(int)Good.Firewood] -= t; h.Stock[(int)Good.Firewood] += t; }
                }
                // with money to spare, part of the table is bread from the baker and meat from the butcher
                float spare = payers.Count == 0 ? 0 : payers.Sum(p => Savings(p)) / payers.Count;
                float treat = Clamp((spare - Balance.TreatsFrom) / (Balance.TreatsFull - Balance.TreatsFrom), 0, 1);
                if (treat > 0.01f)
                {
                    h.Stock[(int)Good.Bread] += Purchase(Good.Bread, Math.Max(0, appetite * Balance.BreadShare * treat - h.Stock[(int)Good.Bread]), payers);
                    h.Stock[(int)Good.Meat] += Purchase(Good.Meat, Math.Max(0, appetite * Balance.MeatShare * treat - h.Stock[(int)Good.Meat]), payers);
                }
                float wantFood = Math.Max(0, appetite * 2f - Pantry(h));
                float wantWood = Math.Max(0, heatNeed * 2.5f - h.Stock[(int)Good.Firewood]);
                float needNow = Math.Max(0, appetite - Pantry(h));
                report.Demand += needNow;
                float gotFood = Purchase(Good.Food, wantFood, payers);
                h.Stock[(int)Good.Food] += gotFood;
                h.Stock[(int)Good.Firewood] += Purchase(Good.Firewood, wantWood, payers);
                // there's food and firewood for sale but not the money for today's: savings first, then the owner
                // takes it out of the family business
                float lackFood = Math.Max(0, appetite - Pantry(h)), lackWood = Math.Max(0, heatNeed - h.Stock[(int)Good.Firewood]);
                if (lackFood > 0.01f || lackWood > 0.01f)
                {
                    float need = CostOf(Good.Food, lackFood) + CostOf(Good.Firewood, lackWood) - payers.Sum(p => Math.Max(0, p.Money));
                    foreach (var p in payers) if (need > 0.01f && p.Deposit > 0) need -= WithdrawFor(p, need);
                    var o = fam != null ? GetCitizen(fam.OwnerId) : null;
                    if (need > 0.01f && o != null) OwnerKeep(fam, o, need * 1.02f);
                    if (payers.Sum(p => Math.Max(0, p.Money)) > 0.01f)
                    {
                        float more = Purchase(Good.Food, lackFood, payers); h.Stock[(int)Good.Food] += more; gotFood += more;
                        h.Stock[(int)Good.Firewood] += Purchase(Good.Firewood, lackWood, payers);
                    }
                }
                report.Bought += Math.Min(gotFood, needNow);
                bool helped = false;
                if (Policy.Charity)
                {
                    float shortFood = appetite - Pantry(h);
                    if (shortFood > 0.01f) { float g = TakeFromTown(Good.Food, shortFood); h.Stock[(int)Good.Food] += g; report.Bought += g; LedgerThisWeek.CharityGoods += g * Balance.Wholesale[0]; helped |= g > 0.01f; }
                    float shortWood = heatNeed - h.Stock[(int)Good.Firewood];
                    if (shortWood > 0.01f) { float g = TakeFromTown(Good.Firewood, shortWood); h.Stock[(int)Good.Firewood] += g; LedgerThisWeek.CharityGoods += g * Balance.Wholesale[1]; helped |= g > 0.01f; }
                }
                foreach (var p in rs) p.CharityDays = helped ? p.CharityDays + 1 : 0;

                // Eat: the bread and meat on the table first, then plain food
                float meat = h.Stock[(int)Good.Meat], bread = h.Stock[(int)Good.Bread];
                float eat = EatFrom(h, appetite);
                meat -= h.Stock[(int)Good.Meat]; bread -= h.Stock[(int)Good.Bread];
                float frac = appetite > 0 ? eat / appetite : 1f;
                foreach (var p in rs) { p.FedToday = frac; p.AteMeat = meat >= appetite * 0.12f; p.AteBread = bread >= appetite * 0.15f; }
                // Keep warm
                report.HeatDemand += heatNeed;
                float burn = Math.Min(h.Stock[(int)Good.Firewood], heatNeed);
                h.Stock[(int)Good.Firewood] -= burn;
                h.ColdToday = heatNeed > 0.15f && burn < heatNeed * 0.95f;
                if (h.ColdToday) { report.ColdHomes++; report.HeatShortfall += heatNeed - burn; }
                foreach (var p in rs) p.Warm = !h.ColdToday;
            }
            // Folk without a roof buy a meal where they can and sleep by the wagon fire
            foreach (var p in People.Where(x => x.HomeId < 0))
            {
                float appetite = p.IsChild ? Balance.ChildAppetite : 1f;
                report.Demand += appetite;
                var payers = p.IsChild ? People.Where(x => x.Id == p.ParentId || x.PartnerId == p.ParentId && p.ParentId >= 0).ToList() : new List<Citizen> { p };
                if (payers.Count == 0) payers = new List<Citizen> { p };
                p.AteMeat = p.AteBread = false;
                float got = Purchase(Good.Food, appetite, payers);
                if (Policy.Charity && got < appetite - 0.01f)
                {
                    float g = TakeFromTown(Good.Food, appetite - got); got += g; LedgerThisWeek.CharityGoods += g * Balance.Wholesale[0];
                    p.CharityDays = g > 0.01f ? p.CharityDays + 1 : 0;
                }
                report.Bought += got;
                p.FedToday = got / appetite;
                p.Warm = si <= 1;   // a campfire does in spring and summer
                // in the cold they huddle by the fire, burning firewood from the town stores
                p.ByFire = !p.Warm && TakeFromTown(Good.Firewood, 0.3f) > 0.2f;
            }
            foreach (var p in People)
            {
                p.Fed = Math.Min(1, p.FedToday);
                if (p.Fed < 0.7f) p.Hungry++; else p.Hungry = Math.Max(0, p.Hungry - 1);
                if (!p.Warm && si != 1) p.ColdDays++; else p.ColdDays = 0;
            }
            report.Shortfall = Math.Max(0, report.Demand - report.Bought);
            Market = report;
        }

        void Saloons()
        {
            int travellers = Businesses.Where(b => b.Type == BuildingType.Hotel).Sum(b => (int)b.Today.Sold);
            foreach (var s in OfType(BuildingType.Saloon))
            {
                int cap = WorkersAt(s) * (s.Stock[(int)Good.Beer] >= 1f ? Balance.DrinksPerHandBeer : Balance.DrinksPerHand); if (cap == 0) continue;
                foreach (var p in Shuffle(People.Where(x => !x.IsChild && x.Money > 12 && x.Jail == 0).ToList()))
                {
                    if (cap <= 0) break;
                    if (Chance(0.45))
                    {
                        Pour(s);
                        p.Money -= Balance.DrinkPrice; s.Cash += Balance.DrinkPrice; s.Week.Revenue += Balance.DrinkPrice;
                        s.Today.Sold++; s.Week.Sold++; p.Happy += 3; p.SaloonToday = s.Id; cap--;
                    }
                }
                // travellers from the hotel come in for a drink
                for (; travellers > 0 && cap > 0; travellers--)
                {
                    if (!Chance(0.4)) continue;
                    Pour(s); s.Cash += Balance.DrinkPrice; s.Week.Revenue += Balance.DrinkPrice; s.Today.Sold++; s.Week.Sold++; cap--;
                }
            }
        }

        /// <summary>A drink: a measure of the brewery's beer while there is some, otherwise whiskey.</summary>
        static void Pour(Building s)
        {
            if (s.Stock[(int)Good.Beer] >= 1f) s.Stock[(int)Good.Beer] -= 1f;
        }

        // =====================================================================
        // Wages
        // =====================================================================
        void PayFromTreasury(Citizen p, float amount, ref float ledgerLine)
        {
            if (Treasury >= amount)
            {
                Treasury -= amount; ledgerLine += amount;
                float tax = amount * Policy.IncomeTax; p.Money += amount - tax; Treasury += tax; LedgerThisWeek.IncomeTax += tax; p.Unpaid = 0;
            }
            else
            {
                if (p.Role == Role.Laborer) return;   // no relief this week, but no grudge either
                p.Unpaid++; p.Happy -= 3;
                if (p.Unpaid >= 3 && p.Role != Role.Mayor)
                {
                    AddLog(p.Name + " quit. The treasury hasn't paid the " + Defs.RoleName(p.Role).ToLower() + " in days.", LogKind.Bad);
                    QuitJob(p);
                }
            }
        }

        void Payroll()
        {
            foreach (var b in Businesses.ToList())
            {
                if (PaysByShare(b.Type)) continue;   // paid their share of each visit as it happens
                foreach (var id in b.Employees.ToList())
                {
                    var p = GetCitizen(id); if (p == null || p.Jail > 0 || p.FamilyHand) continue;
                    float wage = b.Wage * (Schooled(p) ? 1f : Balance.UnschooledFactor);   // a hand who can't read or reckon is paid a little less
                    if (b.Cash >= wage)
                    {
                        b.Cash -= wage; b.Week.Costs += wage;
                        float tax = wage * Policy.IncomeTax;
                        p.Money += wage - tax; Treasury += tax; LedgerThisWeek.IncomeTax += tax;
                    }
                    else
                    {
                        QuitJob(p); b.Desired = Math.Min(b.Desired, b.Employees.Count);
                        var o = GetCitizen(b.OwnerId);
                        AddLog((o != null ? o.Name : "The town") + " couldn't make payroll at the " + b.Def.Name + " and let " + p.Name + " go.", LogKind.Bad);
                    }
                }
            }
            var L = LedgerThisWeek;
            foreach (var p in People.ToList())
            {
                if (p.Jail > 0) continue;
                switch (p.Role)
                {
                    case Role.Mayor: PayFromTreasury(p, Balance.SalaryMayor, ref L.Salaries); break;
                    case Role.Sheriff: PayFromTreasury(p, Balance.SalarySheriff, ref L.Salaries); break;
                    case Role.Deputy: PayFromTreasury(p, Balance.SalaryDeputy, ref L.Salaries); break;
                    case Role.Teacher:
                        // paid by the term; between terms the teacher works like anyone else
                        if (SchoolInSession) PayFromTreasury(p, Balance.SalaryTeacher, ref L.Salaries);
                        else if (p.Chore != Chore.None && p.Chore != Chore.Harvest) PayFromTreasury(p, Policy.BuilderWage, ref L.Builders);
                        break;
                    case Role.Doctor: PayFromTreasury(p, Balance.SalaryDoctor, ref L.Salaries); break;
                    case Role.Laborer:
                        if (p.Chore != Chore.None && p.Chore != Chore.Harvest) PayFromTreasury(p, Policy.BuilderWage, ref L.Builders);
                        else if (p.Age < 66 && Policy.ReliefWage > 0) PayFromTreasury(p, Policy.ReliefWage, ref L.Relief);
                        break;
                }
            }
            float up = Buildings.Count(b => b.Built && b.Def.IsPublic && b.Type != BuildingType.Wagon) * 0.12f;
            Treasury -= up; L.Upkeep += up;
        }

        // =====================================================================
        // Crime: broke, hungry adults steal
        // =====================================================================
        int Crime()
        {
            var off = Office;
            int law = off != null ? off.Employees.Select(GetCitizen).Count(p => p != null && p.Jail == 0) : 0;
            float catchP = law > 0 ? Math.Min(0.9f, 0.25f + 0.18f * law + (MayorBonus(Platform.Law) ? 0.15f : 0)) : 0.05f;
            var biz = Businesses.ToList();
            int crimesToday = 0;
            foreach (var p in People.ToList())
            {
                if (p.Jail > 0) { p.Jail--; continue; }
                if (p.IsChild || p.Role == Role.Sheriff || p.Role == Role.Deputy) continue;
                if (p.Money < 3 && p.Hungry >= 2 && Chance(0.2 * (MayorBonus(Platform.Law) ? 0.5 : 1)))
                {
                    var bizTargets = biz.Where(b => b.Cash > 5).ToList();
                    var personTargets = People.Where(q => q != p && !q.IsChild && q.Money > 8).ToList();
                    int total = bizTargets.Count + personTargets.Count;
                    if (total == 0) continue;
                    int k = Rng.Next(total);
                    Building tb = k < bizTargets.Count ? bizTargets[k] : null;
                    Citizen tp = tb == null ? personTargets[k - bizTargets.Count] : null;
                    string tname = tb != null ? "the " + tb.Def.Name : tp.Name;
                    crimesToday++; CrimesWeek++;
                    if (Chance(catchP))
                    {
                        p.Jail = 2; CaughtWeek++;
                        var sh = off != null ? off.Employees.Select(GetCitizen).FirstOrDefault(x => x != null) : null;
                        AddLog((sh != null ? sh.Name : "A posse") + " caught " + p.Name + " trying to rob " + tname + ". Two days in the jailhouse.", LogKind.Good);
                    }
                    else
                    {
                        float amt = Math.Min(6, (tb != null ? tb.Cash : tp.Money) * 0.3f);
                        if (tb != null) tb.Cash -= amt; else { tp.Money -= amt; tp.Happy -= 8; tp.RobbedDay = Day; }
                        p.Money += amt;
                        AddLog(p.Name + " robbed " + tname + " of " + Money(amt) + " and got away.", LogKind.Bad);
                    }
                }
            }
            return crimesToday;
        }

        // =====================================================================
        // Health and mood
        // =====================================================================
        static readonly float[] ColdDamage = { 0.5f, 0f, 2.5f, 6.5f };
        static readonly float[] ColdMood = { 1f, 0f, 2.5f, 4f };
        static readonly float[] HomelessMood = { 0.6f, 0.4f, 1.5f, 2.5f };

        // =====================================================================
        // Illness: colds and fevers, worse in winter, for the cold and hungry, and in crowded homes
        // =====================================================================
        void FallIll(int si)
        {
            var sickHomes = new HashSet<int>(People.Where(x => x.Sick > 0 && x.HomeId >= 0).Select(x => x.HomeId));
            foreach (var p in People)
            {
                if (p.Sick > 0) continue;
                // work accidents
                var w = GetBuilding(p.WorkId);
                if (w != null && p.Jail == 0 && !p.IsChild && (p.Role == Role.Worker || p.Role == Role.Owner) && Chance(Balance.InjuryPerDay))
                {
                    int kind = w.Type == BuildingType.LumberCamp || w.Type == BuildingType.Woodcutter || w.Type == BuildingType.Butcher ? 10
                             : w.Type == BuildingType.Quarry || w.Type == BuildingType.IronMine ? 11
                             : w.Type == BuildingType.Smithy || w.Type == BuildingType.Bakery || w.Type == BuildingType.Brewery ? 12
                             : w.Type == BuildingType.BuilderYard ? 13 : w.Type == BuildingType.Ranch ? 14 : -1;
                    if (kind >= 0) { Afflict(p, kind, 4.5f, Rng.Next(4, 9)); continue; }
                }
                double chance = Balance.IllnessPerDay * Balance.IllnessBySeason[si];
                if (p.Fed < 0.7f || !p.Warm) chance *= 2;
                if (p.HomeId < 0) chance *= 1.5;
                else if (sickHomes.Contains(p.HomeId)) chance *= 2.5;   // it goes round the household
                var home = GetBuilding(p.HomeId);
                if (home != null && home.Type == BuildingType.BoardingHouse) chance *= 1.3;
                if (p.IsChild && p.Age < 2) chance *= 2; else if (p.IsChild || p.Age > 60) chance *= 1.3;
                if (Day - p.BathedDay <= Balance.CleanDays) chance *= 0.8;          // clean folk fall sick less
                if (si >= 2 && p.Clothes >= Balance.ClothesGood) chance *= 0.9;     // a good coat in the cold
                if (p.AteMeat) chance *= 0.9;
                if (!Chance(chance)) continue;
                double r = Rng.NextDouble();
                if (r < 0.75) Afflict(p, Rng.Next(0, 3), 1.5f, Rng.Next(2, 5));
                else if (r < 0.94) Afflict(p, si == 1 ? 4 : si == 3 ? (Chance(0.5) ? 5 : 6) : 3, 4.5f, Rng.Next(4, 8));
                else Afflict(p, p.IsChild && Chance(0.5) ? 9 : si == 3 || si == 0 ? 8 : 7, 14f, Rng.Next(5, 9));
            }
        }

        void Afflict(Citizen p, int kind, float severity, int days)
        {
            p.IllnessKind = kind; p.Illness = severity; p.Sick = days;
            if (severity >= 3f) AddLog(p.Name + (p.IsChild ? ", aged " + Math.Floor(p.Age) + "," : "") + (kind >= 10 ? " suffered " : " came down with ") + Defs.Illnesses[kind] + "." +
                                       (severity >= 10f && !OfType(BuildingType.DoctorOffice).Any(o => StaffOf(o, Role.Doctor) != null) ? " There's no doctor in town." : ""), LogKind.Bad);
        }

        /// <summary>The doctor sees the worst of the sick and injured each day, and anyone in a bad way.</summary>
        void Doctoring()
        {
            foreach (var o in OfType(BuildingType.DoctorOffice).ToList())
            {
                var doc = StaffOf(o, Role.Doctor);
                if (doc == null || doc.Jail > 0 || LaidUp(doc)) continue;
                var patients = People.Where(p => p != doc && !p.SeenToday && (LaidUp(p) || p.Health < 50))
                                     .OrderByDescending(p => p.Illness).ThenBy(p => p.Health).Take(Balance.DoctorPatients).ToList();
                foreach (var p in patients)
                {
                    p.SeenToday = true;
                    var payer = p.IsChild ? GetCitizen(p.ParentId) : p;
                    if (payer != null && payer.Money > 5f + Balance.VisitFee) { payer.Money -= Balance.VisitFee; doc.Money += Balance.VisitFee; }
                }
                // a house call on the worst case, otherwise office hours
                var worst = patients.FirstOrDefault(p => p.HomeId >= 0);
                doc.TaskId = worst != null ? worst.HomeId : o.Id; doc.Chore = Chore.Tend;
                o.Today.Sold = patients.Count;
            }
        }

        void HealthAndMood(int si, int crimes)
        {
            FallIll(si);
            Doctoring();
            bool schoolOpen = OfType(BuildingType.School).Any(s => StaffOf(s, Role.Teacher) != null);
            var stink = Businesses.Where(b => b.Type == BuildingType.Tannery && IsOpen(b)).ToList();
            foreach (var p in People.ToList())
            {
                bool sick = p.Sick > 0;
                float dh = p.Fed >= 0.95f ? (sick ? 0f : 2f) : -(1 - p.Fed) * 14f;
                if (!p.Warm) dh -= ColdDamage[si] * (p.HomeId < 0 ? (p.ByFire ? 0.9f : 1.2f) : 1f) * (p.IsChild || p.Age > 60 ? 1.3f : 1f) * Rnd(0.6f, 1.4f) * (p.SeenToday ? 0.75f : 1f)
                                * (p.Clothes >= Balance.ClothesGood ? 0.85f : 1f);   // a good coat helps
                else if (p.Fed >= 0.95f && !sick) dh += 1.5f;
                if (p.Age > 60) dh -= (p.Age - 60) * 0.06f;
                if (sick) dh -= p.Illness * (p.SeenToday ? 0.65f : 1f) * (p.Age < 2 ? 1.6f : p.Age > 60 ? 1.4f : 1f) * Rnd(0.7f, 1.3f);   // babies and the old are frailer
                if (p.SeenToday) dh += 1f;
                p.Health = Clamp(p.Health + dh, 0, 100);
                if (p.Health <= 0)
                {
                    string cause = sick && p.Illness >= 3f ? "died of " + Defs.Illnesses[p.IllnessKind] : p.Fed < 0.95f || p.Hungry > 0 ? "starved" : !p.Warm ? "froze to death"
                                 : p.Age > 60 ? "died of old age" : "fell sick and died";
                    Die(p, cause);
                    continue;
                }
                if (sick)
                {
                    p.Sick -= p.SeenToday && Chance(0.5) ? 2 : 1;   // the doctor's care shortens it
                    if (p.Sick <= 0)
                    {
                        p.Sick = 0;
                        if (p.Illness >= 10f) AddLog(p.Name + " pulled through " + Defs.Illnesses[p.IllnessKind] + ".", LogKind.Good);
                        p.Illness = 0;
                    }
                }

                float d = p.Fed >= 0.95f ? 1f : -(1 - p.Fed) * 8;
                if (!p.Warm) d -= ColdMood[si];
                if (p.HomeId < 0) d -= HomelessMood[si];
                if (!p.IsChild && p.Role == Role.Laborer) d -= 0.4f; else if (!p.IsChild) d += 0.4f;
                d += Policy.IncomeTax > 0.12f ? -(Policy.IncomeTax - 0.12f) * 12 : 0.4f;
                if (Policy.HeadTax > 1f) d -= (Policy.HeadTax - 1f) * 0.5f;
                if (MayorBonus(Platform.People)) d += 0.6f;
                if (p.Health < 50) d -= 1;
                if (sick) d -= p.Illness >= 3f ? 1.5f : 0.5f;
                if (schoolOpen && !p.IsChild && ChildrenOf(p).Any(c => c.Age >= Balance.SchoolAge)) d += 0.3f;   // their children are getting an education
                if (p.CharityDays > 0) d -= 0.8f;
                if (p.AteMeat) d += 0.3f;
                if (p.AteBread) d += 0.2f;
                if (p.Clothes > 0.8f) d += 0.15f; else if (p.Clothes >= Balance.ClothesGood) d += 0.05f;   // new clothes, or decent ones
                var home = GetBuilding(p.HomeId);
                if (home != null)
                {
                    d += (Math.Min(home.Stock[(int)Good.Furniture], 3f) - 1f) * 0.2f;   // a bare house is dreary, a furnished one homely
                    if (home.Type != BuildingType.Estate && home.Condition < 25) d -= 0.6f;
                    if (!p.IsChild && home.Type == BuildingType.House && Policy.Rent > 3f) d -= (Policy.Rent - 3f) * 0.2f;
                    if (home.Type == BuildingType.BoardingHouse) d -= 0.3f + (Policy.BoardRent > 1.5f ? (Policy.BoardRent - 1.5f) * 0.3f : 0f);   // cramped
                    foreach (var t in stink)
                        if (Math.Abs(t.X + 1 - home.X) <= Balance.TanneryReach && Math.Abs(t.Y + 1 - home.Y) <= Balance.TanneryReach) { d -= Balance.TanneryStink; break; }   // the tannery downwind
                }
                d -= p.Grief * 0.04f;
                d -= Math.Min(3f, Unburied * 0.3f);
                d -= crimes * 0.4f;
                p.Happy = Clamp(p.Happy + d, 0, 100);
            }
            // Bodies with no grave weigh on the town less as time passes
            if (Unburied > 0 && Day % 14 == 0) Unburied--;
            // A drifter with nothing to stay for may move on
            foreach (var p in People.ToList())
                if (!p.IsChild && p.PartnerId < 0 && !ChildrenOf(p).Any() && p.Happy < 8 && Savings(p) < 5 && p.Role != Role.Mayor)
                {
                    WithdrawFor(p, p.Deposit);
                    RemovePerson(p);
                    AddLog(p.Name + " packed up and left town, fed up with life here.", LogKind.Bad);
                }
        }

        // =====================================================================
        // Newcomers
        // =====================================================================
        void Migration(int si)
        {
            if (si == 3 || AverageHappiness < 40 || FreeBeds < 2) return;
            // newcomers ask around first: they won't settle where the larders look thin, least of all before winter
            float stock = TotalFood(), need = (People.Count + 3) * (si == 2 ? 14f : 5f);
            bool rising = History.Count >= 8 && History[History.Count - 1].Food >= History[History.Count - 8].Food;
            if (stock < need || (!rising && stock < (People.Count + 3) * 14f)) return;   // not while the stores are only running down
            // families ask whether there's a school and a doctor
            bool school = OfType(BuildingType.School).Any(s => StaffOf(s, Role.Teacher) != null);
            bool doctor = OfType(BuildingType.DoctorOffice).Any(o => StaffOf(o, Role.Doctor) != null);
            bool church = ChurchWithPreacher != null;
            if (!Chance((0.035 + (AverageHappiness - 40) / 1500.0) * (1 + (school ? 0.25 : 0) + (doctor ? 0.25 : 0) + (church ? 0.15 : 0)))) return;
            var names = new List<Citizen>();
            if (Chance(0.55 + (school ? 0.15 : 0)))
            {
                string fam = Pick(Defs.LastNames);
                var dad = AddPerson(false, fam, Rnd(20, 42), (float)Math.Round(Rnd(20, 55)));
                var mom = AddPerson(true, fam, Rnd(19, 38), (float)Math.Round(Rnd(5, 25)));
                dad.PartnerId = mom.Id; mom.PartnerId = dad.Id;
                names.Add(dad); names.Add(mom);
                for (int k = Rng.Next(0, 3); k > 0; k--) { var c = AddPerson(Chance(0.5), fam, Rnd(1, 9), 0); c.ParentId = c.MotherId = mom.Id; c.FatherId = dad.Id; names.Add(c); }
                AddLog("The " + fam + " family (" + names.Count + ") rolled in on a wagon and asked to stay.", LogKind.Good);
            }
            else
            {
                var p = AddPerson(Chance(0.5), Pick(Defs.LastNames), Rnd(17, 35), (float)Math.Round(Rnd(10, 45)));
                names.Add(p);
                AddLog(p.Name + " drifted into town looking for work.", LogKind.Good);
            }
            foreach (var p in names) if (!p.IsChild) { Treasury += Balance.ClaimFee; LedgerThisWeek.Claims += Balance.ClaimFee; }
            PutInTown(Good.Food, names.Count * 12f);   // they bring some provisions
            RehouseEveryone();
        }

        /// <summary>How many hands an owner wants next week: hire while goods sell and the till can pay,
        /// cut back when stock piles up or money runs short. Fields and gatherers lay off for winter.</summary>
        int StaffingDecision(Building b, float profit)
        {
            var d = b.Def;
            int n = b.Employees.Count;
            bool townRun = b.OwnerId < 0;   // the treasury covers the wages, so cash doesn't hold it back
            int afford = townRun ? d.MaxEmployees : (int)(b.Cash / Math.Max(0.5f, b.Wage * 2));
            if (b.Type == BuildingType.Bank) return LoansOut(b) > 150f ? 1 : 0;   // a clerk once there's lending business to keep
            var owner = GetCitizen(b.OwnerId); int self = owner != null && owner.WorkId == b.Id ? 1 : 0;
            if (b.Type == BuildingType.Brothel)
            {
                // no wages to find: at least one woman, and as many as the trade keeps busy
                int want = (int)Math.Ceiling(b.Week.Made / 7f / Balance.VisitsPerWoman - 0.15f);
                return Math.Max(1, Math.Min(d.MaxEmployees, want));
            }
            if (b.Type == BuildingType.Hotel || b.Type == BuildingType.Barber)
            {
                // hands for the trade there's been: four guests a hand at a hotel, eight customers a day at the barber's
                float per = b.Type == BuildingType.Hotel ? Balance.GuestsPerHand : Balance.CustomersPerHand;
                int want = (int)Math.Ceiling(b.Week.Made / 7f / per - 0.15f) - self;
                if (b.Type == BuildingType.Hotel && Season == 3) want = Math.Min(want, 1 - self);   // a quiet winter
                return Math.Max(0, Math.Min(Math.Min(d.MaxEmployees, want), Math.Max(afford, townRun ? 1 : 0)));
            }
            if (b.Type == BuildingType.BuilderYard)
            {
                // keep the crew the jobs need, but no builder takes more than one in four of the town's adults;
                // in quiet weeks one hand stays on to make furniture
                int want = Math.Min(Math.Min(d.MaxEmployees, Math.Max(1, Adults.Count() / 4)), Math.Max(0, b.Backlog - 1));
                if (want == 0 && b.Stock[(int)Good.Furniture] < Balance.FurnitureCap * 0.5f && b.Cash > b.Wage * 10) want = 1;
                return Math.Min(want, afford);
            }
            int nextSeason = SeasonOf(Day + 1);
            if (b.Type == BuildingType.Farm)
            {
                // two hands tend the field; harvest needs every hand there is; nothing to do in winter
                int want;
                if (Season == 3) want = nextSeason == 0 ? 2 : 0;
                else if (Season == 2) want = b.CropLeft > 1 ? d.MaxEmployees : 0;
                else want = Season == 1 && DayOfSeason > DaysInSeason - 7 ? d.MaxEmployees : 2;   // every hand for the last week of summer
                return Math.Min(want, Math.Max(afford, 1));
            }
            if (b.Type == BuildingType.Gatherer && nextSeason == 3) return 0;
            if (!townRun && b.Cash < b.Wage * (n + 1) * 2) return Math.Max(0, n - 1);
            float fill = 0;
            if (d.IsRetail) fill = d.Sells.Average(g => b.Stock[(int)g]) / (d.Capacity / (float)d.Sells.Length);
            else
            {
                Good? made = null;
                foreach (var g in AllGoods) if (Produces(b.Type, g)) made = g;
                if (made.HasValue) fill = b.Stock[(int)made.Value] / StockCap(b, made.Value);
                else fill = profit > 0 ? 0.3f : 0.7f;
            }
            if (MakesWhatItSells(b.Type))
            {
                // a baker, butcher or tailor: more hands when the counter sells out and there's something to work with
                bool makings = b.Type == BuildingType.Butcher ? b.Stock[(int)Good.Cattle] > 0.5f : b.Type == BuildingType.Bakery ? b.Stock[(int)Good.Food] > 3f
                             : b.Stock[(int)Good.Wool] >= Balance.WoolPerClothes || b.Stock[(int)Good.Leather] >= 1f;
                if (profit < 0 || fill > 0.85f) return Math.Max(0, n - 1);
                if (fill < 0.4f && makings && b.Week.Sold > 25 * (n + 1) && (townRun || b.Cash > b.Wage * (n + 1) * 4)) return Math.Min(d.MaxEmployees, n + 1);
                return n;
            }
            if (ExportTrade(b.Type))
            {
                // what doesn't sell in town goes east: keep hands on while it pays
                if (b.Type == BuildingType.Tannery && b.Stock[(int)Good.Hides] < 2f) return Math.Max(0, n - 1);
                if (profit < 0) return Math.Max(0, n - 1);
                if (profit > b.Wage * 3 && (townRun || b.Cash > b.Wage * (n + 1) * 4)) return Math.Min(d.MaxEmployees, n + 1);
                return n;
            }
            if (d.IsRetail) return fill < 0.3f || profit < 0 ? Math.Max(0, n - 1) : (b.Week.Sold > 30 * (n + 1) ? Math.Min(d.MaxEmployees, n + 1) : n);
            if (fill < 0.5f && (townRun || b.Cash > b.Wage * (n + 1) * 4)) return Math.Min(d.MaxEmployees, n + 1);
            if (fill > 0.9f) return Math.Max(0, n - 1);
            return Math.Max(n, Math.Min(b.Desired, d.MaxEmployees));
        }

        // =====================================================================
        // Paying for a business: bank loans (and deeds owed to the town in older saves)
        // =====================================================================
        void PayLoan(Building b, Citizen o, float profit, float reserve)
        {
            if (b.Debt <= 0.01f) { b.Debt = 0; return; }
            var bank = GetBuilding(b.LenderId);
            if (bank == null)
            {
                // owed to the town (older saves): paid down out of profits, no interest
                if (b.Cash <= reserve * 0.5f) return;
                float p = Math.Min(Math.Min(b.Debt, b.Cash - reserve * 0.5f), Math.Max(1f, Math.Max(0, profit) * 0.35f));
                b.Cash -= p; b.Debt -= p; Treasury += p; LedgerThisWeek.Deeds += p;
                if (b.Debt <= 0.01f) { b.Debt = 0; AddLog(o.Name + " paid off the " + b.Def.Name + ".", LogKind.Good); }
                return;
            }
            float interest = b.Debt * Balance.LoanInterest;
            float due = Math.Min(b.Debt + interest, interest + b.Debt / Balance.LoanTerm + Math.Max(0, profit) * 0.2f);
            if (b.Cash - b.Wage * 2 < due && b.Deposit > 0) WithdrawFor(b, due - (b.Cash - b.Wage * 2));
            float paid = Math.Min(due, Math.Max(0, b.Cash - b.Wage * 2));
            b.Cash -= paid; b.Week.Costs += Math.Min(paid, interest);
            bank.Cash += paid; bank.Week.Revenue += Math.Min(paid, interest);
            b.Debt = Math.Max(0, b.Debt + interest - paid);
            var banker = GetCitizen(bank.OwnerId);
            if (b.Debt <= 0.01f) { b.Debt = 0; b.LenderId = -1; b.MissedPayments = 0; AddLog(o.Name + " paid off the bank loan on the " + b.Def.Name + ".", LogKind.Good); return; }
            if (paid >= due * 0.75f) { b.MissedPayments = 0; return; }
            // the bank gives seasonal work its season: farms settle up at harvest, gatherers outside winter
            if ((b.Type == BuildingType.Farm && Season != 2) || (b.Type == BuildingType.Gatherer && Season == 3)) return;
            b.MissedPayments++;
            o.Happy -= 4;
            if (b.MissedPayments < 4)
            {
                AddLog(o.Name + " fell behind on the bank loan for the " + b.Def.Name + ".", LogKind.Bad);
                return;
            }
            // foreclosure: the bank takes the business and what's in it, and gets the rest back when it sells
            Repossess(b);
            AddLog((banker != null ? banker.Name : "The bank") + " took the " + b.Def.Name + " from " + o.Name + " after four missed payments." +
                   (b.Debt > 0.01f ? " It's for sale, and the bank gets the " + Money(b.Debt) + " still owed out of the price. The town runs it meanwhile." : ""), LogKind.Bad);
            foreach (var id in b.Employees) { var e = GetCitizen(id); if (e != null && e.FamilyHand) { e.FamilyHand = false; } }
            foreach (var id in b.Employees.ToList()) { var e = GetCitizen(id); if (e != null && (e.PartnerId == o.Id || e.MotherId == o.Id || e.FatherId == o.Id)) QuitJob(e); }
            o.Role = Role.Laborer; o.WorkId = -1;
        }

        /// <summary>The bank takes back a business whose owner defaulted or left: the till and the business's account go
        /// against the loan, and the business stays pledged to the bank until it sells, when the bank is paid first.</summary>
        void Repossess(Building b)
        {
            var bank = GetBuilding(b.LenderId);
            b.OwnerId = -1; b.MissedPayments = 0;
            if (bank == null || bank.OwnerId < 0) { if (b.Deposit > 0) WithdrawFor(b, b.Deposit); b.Debt = 0; b.LenderId = -1; return; }
            if (b.Deposit > 0) WithdrawFor(b, b.Deposit);
            float t = Math.Min(Math.Max(0, b.Cash), b.Debt);
            b.Cash -= t; bank.Cash += t; b.Debt -= t;
            if (b.Debt <= 0.01f) { b.Debt = 0; b.LenderId = -1; }
        }

        // =====================================================================
        // Rent: families pay the town for their cottage every week
        // =====================================================================
        /// <summary>Takes up to amount from a household's adults, richest first. Returns what was paid.</summary>
        float Collect(List<Citizen> payers, float amount)
        {
            float paid = 0;
            foreach (var p in payers.OrderByDescending(x => x.Money))
            {
                float t = Math.Min(Math.Max(0, p.Money), amount - paid);
                p.Money -= t; paid += t;
                if (paid >= amount - 0.001f) return paid;
            }
            // the rest from their bank accounts
            foreach (var p in payers.OrderByDescending(x => x.Deposit))
            {
                if (paid >= amount - 0.001f) break;
                float t = WithdrawFor(p, amount - paid);
                p.Money -= t; paid += t;
            }
            return paid;
        }

        void CollectRent()
        {
            foreach (var h in OfType(BuildingType.House).ToList())
            {
                if (Policy.Rent <= 0) { h.Arrears = 0; continue; }
                var rs = Household(h).ToList();
                if (rs.Count == 0) { h.Arrears = 0; continue; }
                if (Policy.Charity && rs.Any(p => p.CharityDays > 0)) continue;   // the town doesn't charge families it is feeding
                var payers = rs.Where(p => !p.IsChild).ToList();
                float due = Policy.Rent + Math.Min(h.Arrears, Policy.Rent);       // catching up a week at a time
                float paid = Collect(payers, due);
                Treasury += paid; LedgerThisWeek.Rent += paid;
                h.Arrears = Math.Max(0, h.Arrears + Policy.Rent - paid);
                if (h.Arrears <= 0.01f) continue;
                foreach (var p in payers) p.Happy -= 2;
                if (h.Arrears >= Policy.Rent * 4 - 0.01f)
                {
                    var head = payers.FirstOrDefault();
                    AddLog("The " + (head != null ? head.Family : "") + " family was put out of Cottage #" + h.Id + ", four weeks behind on rent.", LogKind.Bad);
                    foreach (var p in rs) { LeaveHome(p); p.EvictedDay = Day; }
                    h.Arrears = 0;
                }
            }
            // boarders pay for their own bed, and are put out on their own
            if (Policy.BoardRent > 0)
                foreach (var h in OfType(BuildingType.BoardingHouse).ToList())
                    foreach (var p in Household(h).Where(x => !x.IsChild).ToList())
                    {
                        if (Policy.Charity && p.CharityDays > 0) continue;
                        float due = Policy.BoardRent + Math.Min(p.RentOwed, Policy.BoardRent);
                        if (p.Money < due) WithdrawFor(p, due - p.Money);
                        float paid = Math.Min(Math.Max(0, p.Money), due);
                        p.Money -= paid; Treasury += paid; LedgerThisWeek.Rent += paid;
                        p.RentOwed = Math.Max(0, p.RentOwed + Policy.BoardRent - paid);
                        if (p.RentOwed <= 0.01f) continue;
                        p.Happy -= 2;
                        if (p.RentOwed >= Policy.BoardRent * 4 - 0.01f)
                        {
                            AddLog(p.Name + " was put out of the boarding house, four weeks behind on rent.", LogKind.Bad);
                            var unit = new List<Citizen> { p }; unit.AddRange(ChildrenOf(p).Where(c => c.HomeId == h.Id));
                            foreach (var u in unit) { LeaveHome(u); u.EvictedDay = Day; }
                            p.RentOwed = 0;
                        }
                    }
            RehouseEveryone();
        }

        /// <summary>Households with savings to spare buy a piece of furniture now and then.</summary>
        void BuyFurniture()
        {
            foreach (var h in Buildings.Where(b => b.Built && (b.Type == BuildingType.House || b.Type == BuildingType.Estate)).ToList())
            {
                var payers = Household(h).Where(p => !p.IsChild).ToList();
                if (payers.Count == 0) continue;
                float target = h.Type == BuildingType.Estate ? 5f : 3f;
                if (h.Stock[(int)Good.Furniture] > target - 0.5f) continue;
                if (payers.Sum(p => Savings(p)) < Balance.FurnitureSavings) continue;
                h.Stock[(int)Good.Furniture] += Purchase(Good.Furniture, 1f, payers);
            }
        }

        // =====================================================================
        // Sunday: taxes, profits, staffing and prices
        // =====================================================================
        void Weekly()
        {
            CollectRent();
            BuyFurniture();
            BuyClothes();
            foreach (var p in Adults)
            {
                float a = Policy.HeadTax; if (a <= 0) continue;
                if (p.Money < a) WithdrawFor(p, a - p.Money);
                if (p.Money >= a) { p.Money -= a; Treasury += a; LedgerThisWeek.HeadTax += a; }
                else p.Happy -= 3;
            }
            foreach (var b in Businesses.ToList())
            {
                var d = b.Def;
                float profit = b.Week.Revenue - b.Week.Costs;
                var o = GetCitizen(b.OwnerId);
                float tax = 0, draw = 0;
                float reserve = b.Wage * (b.Employees.Count + 1) * 4 + 25;
                if (o != null)
                {
                    tax = Math.Min(Math.Max(0, b.Cash), Math.Max(0, profit) * Policy.BusinessTax);
                    b.Cash -= tax; Treasury += tax; LedgerThisWeek.BusinessTax += tax;
                    PayLoan(b, o, profit, reserve);
                    if (b.OwnerId >= 0 && b.Type == BuildingType.Bank)
                    {
                        // the banker takes home part of the bank's profit, never the depositors' money
                        draw = Math.Min(Math.Max(0, profit) * 0.6f, Math.Max(0, b.Cash - VaultReserve(b) - b.Wage * 4));
                        b.Cash -= draw; o.Money += draw;
                    }
                    else if (b.OwnerId >= 0 && b.Cash + b.Deposit > reserve)
                    {
                        draw = (b.Cash + b.Deposit - reserve) * 0.6f;
                        float fromAccount = Math.Min(draw, b.Deposit);
                        if (fromAccount > 0 && (o.BankId < 0 || o.BankId == b.BankId)) { b.Deposit -= fromAccount; o.Deposit += fromAccount; o.BankId = b.BankId; }   // a transfer in the bank's books
                        else fromAccount = 0;
                        float fromTill = Math.Min(draw - fromAccount, Math.Max(0, b.Cash));
                        b.Cash -= fromTill; o.Money += fromTill;
                        draw = fromAccount + fromTill;
                    }
                }
                else if (b.Cash > reserve)
                {
                    // a town-run business: the takings go to the treasury
                    float sweep = b.Cash - reserve; b.Cash -= sweep; Treasury += sweep; LedgerThisWeek.TownTakings += sweep;
                }
                b.Last = new WeekReport { Valid = true, Revenue = b.Week.Revenue, Costs = b.Week.Costs, Profit = profit, Tax = tax, OwnerDraw = draw + b.Week.Drawn, Sold = b.Week.Sold, Made = b.Week.Made };
                if (PaysByShare(b.Type))
                {
                    // what a woman took home a day last week (kept as it was while nobody worked there)
                    if (b.Employees.Count > 0) b.Wage = Math.Max(1f, b.Week.Sold * Balance.VisitPrice * Balance.VisitShare / (7f * b.Employees.Count));
                }
                else b.Wage = Math.Max(Policy.MinWage, b.Wage);
                if (Day - b.BuiltDay >= 6)
                {
                    b.Desired = StaffingDecision(b, profit);
                    if (o == null && b.Def.MaxEmployees >= 1 && b.Type != BuildingType.Bank) b.Desired = Math.Max(1, b.Desired);   // a town-run business always has someone minding it
                    if (profit >= 0) b.LossWeeks = 0; else b.LossWeeks++;
                    // hired hands go first when business is slow; then family go and look for paid work
                    b.Employees.RemoveAll(id => GetCitizen(id) == null);
                    while (b.Employees.Count > b.Desired)
                    {
                        var p = b.Employees.Select(GetCitizen).LastOrDefault(x => !x.FamilyHand) ?? GetCitizen(b.Employees[b.Employees.Count - 1]);
                        bool family = p.FamilyHand;
                        QuitJob(p);
                        AddLog(family ? p.Name + " left the family " + d.Name.ToLower() + " to look for paid work. There isn't enough to do."
                                      : (o != null ? o.Name : "The town") + " laid off " + p.Name + " at the " + d.Name + ". Business is slow.", LogKind.Bad);
                    }

                }
                if (d.IsRetail)
                    foreach (var g in d.Sells)
                    {
                        float per = d.Capacity / (float)d.Sells.Length, fill = b.Stock[(int)g] / per;
                        float lo = Balance.Wholesale[(int)g] * 1.15f, hi = Balance.Wholesale[(int)g] * 2.6f;
                        if (fill < 0.2f) b.Price[(int)g] = Math.Min(hi, b.Price[(int)g] + 0.1f);
                        else if (fill > 0.6f) b.Price[(int)g] = Math.Max(lo, b.Price[(int)g] - 0.1f);
                    }
                b.Week.Clear();
            }
            BankDeposits();
            LedgerLastWeek = LedgerThisWeek; HasLastWeek = true; LedgerThisWeek = new TownLedger();
            CrimesLast = CrimesWeek; CaughtLast = CaughtWeek; CrimesWeek = 0; CaughtWeek = 0;
        }
    }
}
