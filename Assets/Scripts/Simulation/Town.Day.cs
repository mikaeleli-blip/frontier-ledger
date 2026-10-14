using System;
using System.Collections.Generic;
using System.Linq;

namespace FrontierLedger.Simulation
{
    public partial class Town
    {
        static readonly Good[] AllGoods = { Good.Food, Good.Firewood, Good.Logs, Good.Stone, Good.Tools };

        /// <summary>One full day: the land, construction, production, trade, households, wages,
        /// crime, health, families, hiring, migration and elections.</summary>
        void SimulateDay()
        {
            int si = Season;
            foreach (var b in Buildings) b.Today.Clear();
            foreach (var p in People) { p.FedToday = 0; p.SaloonToday = -1; p.TaskId = -1; }

            GrowLand();
            Construct();
            OddJobs(si);
            Produce(si);
            Trade(si);
            Households(si);
            Saloons();
            Payroll();
            int crimes = Crime();
            HealthAndMood(si, crimes);
            FamilyLife();
            Hiring();
            RehouseEveryone();
            Migration(si);
            ElectionStep();
            if (Day % 7 == 0) Weekly();
            History.Add(new HistoryPoint { Day = Day, Treasury = Treasury, Population = People.Count, Food = TotalFood(), Firewood = TotalFirewood() });
            if (History.Count > 112) History.RemoveAt(0);
        }

        public float TotalFood() { return Buildings.Sum(b => b.Stock[(int)Good.Food]); }
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
        void Construct()
        {
            var sites = Sites.OrderBy(b => b.Id).ToList();
            if (sites.Count == 0) return;
            foreach (var s in sites)
            {
                var d = s.Def;
                foreach (var g in new[] { Good.Logs, Good.Stone })
                {
                    float need = (g == Good.Logs ? d.Logs : d.Stone) - s.Delivered[(int)g];
                    if (need <= 0.001f) continue;
                    float got = TakeFromTown(g, need);
                    if (need - got > 0.001f && Treasury > 0)
                    {
                        float budget = Treasury;
                        float bought = BuyFromProducers(g, need - got, ref budget, null);
                        LedgerThisWeek.Materials += Treasury - budget; Treasury = budget;
                        got += bought;
                    }
                    s.Delivered[(int)g] += got;
                }
            }
            var crew = People.Where(p => p.Role == Role.Builder && p.Jail == 0).ToList();
            var helpers = People.Where(p => p.Role == Role.Laborer && p.Jail == 0 && p.Age < 66).ToList();
            int bi = 0, hi = 0;
            foreach (var s in sites)
            {
                var d = s.Def;
                float cap = d.Work * Math.Min(1f, s.MaterialsFraction) - s.Progress;
                while (cap > 0.001f && (bi < crew.Count || hi < helpers.Count))
                {
                    float w;
                    if (bi < crew.Count) { crew[bi].TaskId = s.Id; bi++; w = 1f; }
                    else { helpers[hi].TaskId = s.Id; hi++; w = 0.25f; }
                    float use = Math.Min(cap, w);
                    s.Progress += use; cap -= use;
                }
                if (s.Progress >= d.Work - 0.001f && s.MaterialsFraction >= 0.999f) OnBuilt(s, false);
            }
        }

        // =====================================================================
        // Idle hands: laborers (and builders with nothing to build) help bring in the harvest
        // in autumn, and otherwise forage and pick up deadwood for their own household.
        // =====================================================================
        void OddJobs(int si)
        {
            var idle = People.Where(p => !p.IsChild && p.Jail == 0 && p.Age < 66 && p.TaskId < 0 && (p.Role == Role.Laborer || p.Role == Role.Builder)).ToList();
            if (idle.Count == 0) return;
            int k = 0;
            if (si == 2)
                foreach (var f in Businesses.Where(b => b.Type == BuildingType.Farm && b.CropLeft > 1).OrderByDescending(b => b.CropLeft).ToList())
                    for (int hands = 0; hands < 4 && k < idle.Count && f.CropLeft > 1; hands++)
                    {
                        var p = idle[k++];
                        p.TaskId = f.Id;
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
        }

        // =====================================================================
        // Production
        // =====================================================================
        float ToolFactor(Building b, int workers)
        {
            float f = b.Stock[(int)Good.Tools] > 0.01f ? 1f : Balance.NoToolsFactor;
            b.Stock[(int)Good.Tools] = Math.Max(0, b.Stock[(int)Good.Tools] - workers * Balance.ToolWearPerWorker);
            return f;
        }

        float FoodBonus { get { return MayorBonus(Platform.Farm) ? 1.15f : 1f; } }

        void Produce(int si)
        {
            foreach (var b in Businesses.ToList())
            {
                int w = WorkersAt(b);
                switch (b.Type)
                {
                    case BuildingType.Farm: Farm(b, w, si); break;
                    case BuildingType.Gatherer:
                        if (w > 0) Make(b, Good.Food, w * Balance.GatherPerWorker * ToolFactor(b, w) * Balance.GatherBySeason[si] * Math.Min(1f, TreesNear(b) / 25f) * FoodBonus);
                        break;
                    case BuildingType.Fishery:
                        if (w > 0) Make(b, Good.Food, w * Balance.FishPerWorker * ToolFactor(b, w) * Balance.FishBySeason[si] * FoodBonus);
                        break;
                    case BuildingType.LumberCamp: Lumber(b, w); break;
                    case BuildingType.Woodcutter:
                        if (w > 0)
                        {
                            float tf = ToolFactor(b, w);
                            float logs = Math.Min(b.Stock[(int)Good.Logs], w * Balance.LogsSplitPerWorker * tf);
                            if (b.Stock[(int)Good.Firewood] < Balance.BusinessStockCap)
                            { b.Stock[(int)Good.Logs] -= logs; Make(b, Good.Firewood, logs * Balance.FirewoodPerLog); }
                        }
                        break;
                    case BuildingType.Quarry: Quarry(b, w); break;
                    case BuildingType.Smithy:
                        if (w > 0)
                        {
                            float tf = ToolFactor(b, w);
                            float n = Math.Min(Math.Min(b.Stock[(int)Good.Logs], b.Stock[(int)Good.Stone]), w * Balance.ToolsPerWorker * tf);
                            if (b.Stock[(int)Good.Tools] < 30)
                            { b.Stock[(int)Good.Logs] -= n; b.Stock[(int)Good.Stone] -= n; Make(b, Good.Tools, n); }
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

        void Farm(Building b, int w, int si)
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
            if (si <= 1) b.CropGrowth = Math.Min(1f, b.CropGrowth + w * Balance.GrowthPerWorker * tf);
            else if (si == 2 && b.CropLeft > 0)
            {
                float h = Math.Min(b.CropLeft, w * Balance.HarvestPerWorker * tf);
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

        void Lumber(Building b, int w)
        {
            if (w == 0) return;
            float tf = ToolFactor(b, w);
            var d = b.Def;
            if (b.Stock[(int)Good.Logs] < Balance.BusinessStockCap)
            {
                b.ChopWork += w * Balance.TreeWorkPerWorker * tf;
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

        void Quarry(Building b, int w)
        {
            if (w == 0 || b.Stock[(int)Good.Stone] >= Balance.BusinessStockCap) return;
            float tf = ToolFactor(b, w);
            var d = b.Def;
            if (b.TargetTile < 0 || Terrain[b.TargetTile] != Tile.Rock) b.TargetTile = NearestTile(b, d.Radius, Tile.Rock);
            if (b.TargetTile >= 0)
            {
                b.ChopWork += w * 0.5f * tf;
                if (b.ChopWork >= 1f)
                {
                    SetTerrain(b.TargetTile % Cols, b.TargetTile / Cols, Tile.Grass);
                    b.TargetTile = -1; b.ChopWork = 0;
                    Make(b, Good.Stone, Balance.StonePerRock);
                }
            }
            else Make(b, Good.Stone, w * Balance.StonePerWorker * tf);
        }

        // =====================================================================
        // Trade between businesses, the town and the freight wagon
        // =====================================================================
        float Spend(Building b) { return Math.Max(0, b.Cash - b.Wage * Math.Max(1, b.Employees.Count)); }

        void BuyFor(Building b, Good g, float want)
        {
            if (want <= 0.01f) return;
            float budget = Spend(b), before = budget;
            float got = BuyFromProducers(g, want, ref budget, b);
            float spent = before - budget;
            b.Cash -= spent; b.Week.Costs += spent; b.Stock[(int)g] += got; b.Today.Bought += got;
            // The town will sell from its stores what no business can
            if (got < want - 0.01f && (g == Good.Tools || g == Good.Logs || g == Good.Stone))
            {
                float reserve = g == Good.Tools ? 4 : Policy.Reserve[(int)g];
                float spare = Math.Max(0, TownStock(g) - reserve);
                float price = Balance.Wholesale[(int)g];
                float q = Math.Min(Math.Min(want - got, spare), Spend(b) / price);
                if (q > 0.01f)
                {
                    TakeFromTown(g, q); b.Stock[(int)g] += q;
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
                float need = b.Wage * (b.Employees.Count + 1) * 2 + (b.Type == BuildingType.Woodcutter || b.Type == BuildingType.Smithy || b.Def.IsRetail ? 25 : 8);
                if (b.Cash >= need || o.Money <= 8) continue;
                float put = Math.Min(need - b.Cash, o.Money - 8);
                o.Money -= put; b.Cash += put;
            }
        }

        void Trade(int si)
        {
            float xF = MayorBonus(Platform.Trade) ? 1.25f : 1f;
            var biz = Businesses.ToList();
            OwnersTopUp(biz);
            // Inputs and tools
            foreach (var b in biz)
            {
                int w = WorkersAt(b);
                if (b.OwnerId < 0) continue;
                if (b.Type == BuildingType.Woodcutter) BuyFor(b, Good.Logs, 15 - b.Stock[(int)Good.Logs]);
                if (b.Type == BuildingType.Smithy) { BuyFor(b, Good.Logs, 8 - b.Stock[(int)Good.Logs]); BuyFor(b, Good.Stone, 8 - b.Stock[(int)Good.Stone]); }
                bool usesTools = !b.Def.IsRetail && b.Type != BuildingType.Saloon && b.Type != BuildingType.Smithy;
                if (usesTools && w > 0 && b.Stock[(int)Good.Tools] < 0.5f * w) BuyFor(b, Good.Tools, w - b.Stock[(int)Good.Tools]);
            }
            // Shops restock
            foreach (var r in biz.Where(b => b.Def.IsRetail && IsOpen(b)).OrderBy(x => Rng.Next()))
            {
                var d = r.Def; float per = d.Capacity / (float)d.Sells.Length;
                foreach (var g in d.Sells) BuyFor(r, g, per - r.Stock[(int)g]);
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
                    float cap = g == Good.Food && b.Type == BuildingType.Farm ? Balance.BarnCapacity : Balance.BusinessStockCap;
                    float extra = b.Stock[(int)g] - cap;
                    if (extra <= 0) continue;
                    if (g == Good.Food && Treasury > 0)
                    {
                        float price = Balance.Export[0];
                        float q = Math.Min(Math.Min(extra, TownSpace), Treasury / price);
                        if (q > 0)
                        {
                            PutInTown(g, q); b.Stock[(int)g] -= q; extra -= q;
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
                float paid = q * src.Price;
                if (src.Town) { Treasury += paid; LedgerThisWeek.StoreSales += paid; }
                else { src.B.Cash += paid; src.B.Week.Revenue += paid; src.B.Today.Sold += q; src.B.Week.Sold += q; }
            }
            return got;
        }

        void Households(int si)
        {
            var report = new MarketReport();
            float heatNeed = Balance.FirewoodBySeason[si];
            foreach (var h in Buildings.Where(b => b.Built && (b.Type == BuildingType.House || b.Type == BuildingType.Estate)))
            {
                var rs = Household(h).ToList();
                h.ColdToday = false;
                if (rs.Count == 0) { h.GardenToday = 0; continue; }
                if (h.Type == BuildingType.House)
                {
                    h.GardenToday = Balance.GardenBase * Balance.GardenBySeason[si] * FoodBonus;
                    h.Stock[(int)Good.Food] += h.GardenToday; report.Garden += h.GardenToday;
                }
                float appetite = rs.Sum(p => p.IsChild ? Balance.ChildAppetite : 1f);
                var payers = rs.Where(p => !p.IsChild).ToList();
                float wantFood = Math.Max(0, appetite * 2f - h.Stock[(int)Good.Food]);
                float wantWood = Math.Max(0, heatNeed * 2.5f - h.Stock[(int)Good.Firewood]);
                float needNow = Math.Max(0, appetite - h.Stock[(int)Good.Food]);
                report.Demand += needNow;
                float gotFood = Purchase(Good.Food, wantFood, payers);
                report.Bought += Math.Min(gotFood, needNow);
                h.Stock[(int)Good.Food] += gotFood;
                h.Stock[(int)Good.Firewood] += Purchase(Good.Firewood, wantWood, payers);
                bool helped = false;
                if (Policy.Charity)
                {
                    float shortFood = appetite - h.Stock[(int)Good.Food];
                    if (shortFood > 0.01f) { float g = TakeFromTown(Good.Food, shortFood); h.Stock[(int)Good.Food] += g; report.Bought += g; LedgerThisWeek.CharityGoods += g * Balance.Wholesale[0]; helped |= g > 0.01f; }
                    float shortWood = heatNeed - h.Stock[(int)Good.Firewood];
                    if (shortWood > 0.01f) { float g = TakeFromTown(Good.Firewood, shortWood); h.Stock[(int)Good.Firewood] += g; LedgerThisWeek.CharityGoods += g * Balance.Wholesale[1]; helped |= g > 0.01f; }
                }
                foreach (var p in rs) p.CharityDays = helped ? p.CharityDays + 1 : 0;

                // Eat
                float eat = Math.Min(h.Stock[(int)Good.Food], appetite);
                h.Stock[(int)Good.Food] -= eat;
                float frac = appetite > 0 ? eat / appetite : 1f;
                foreach (var p in rs) { p.FedToday = frac; }
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
                float got = Purchase(Good.Food, appetite, payers);
                if (Policy.Charity && got < appetite - 0.01f)
                {
                    float g = TakeFromTown(Good.Food, appetite - got); got += g; LedgerThisWeek.CharityGoods += g * Balance.Wholesale[0];
                    p.CharityDays = g > 0.01f ? p.CharityDays + 1 : 0;
                }
                report.Bought += got;
                p.FedToday = got / appetite;
                p.Warm = si <= 1;   // a campfire does in spring and summer
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
            foreach (var s in OfType(BuildingType.Saloon))
            {
                int cap = WorkersAt(s) * 6; if (cap == 0) continue;
                foreach (var p in Shuffle(People.Where(x => !x.IsChild && x.Money > 12 && x.Jail == 0).ToList()))
                {
                    if (cap <= 0) break;
                    if (Chance(0.45))
                    {
                        const float price = 1.5f;
                        p.Money -= price; s.Cash += price; s.Week.Revenue += price;
                        s.Today.Sold++; s.Week.Sold++; p.Happy += 3; p.SaloonToday = s.Id; cap--;
                    }
                }
            }
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
                foreach (var id in b.Employees.ToList())
                {
                    var p = GetCitizen(id); if (p == null || p.Jail > 0) continue;
                    if (b.Cash >= b.Wage)
                    {
                        b.Cash -= b.Wage; b.Week.Costs += b.Wage;
                        float tax = b.Wage * Policy.IncomeTax;
                        p.Money += b.Wage - tax; Treasury += tax; LedgerThisWeek.IncomeTax += tax;
                    }
                    else
                    {
                        QuitJob(p); b.Desired = Math.Min(b.Desired, b.Employees.Count);
                        var o = GetCitizen(b.OwnerId);
                        AddLog((o != null ? o.Name : "The " + b.Def.Name) + " couldn't make payroll and let " + p.Name + " go.", LogKind.Bad);
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
                    case Role.Builder: PayFromTreasury(p, Policy.BuilderWage, ref L.Builders); break;
                    case Role.Laborer:
                        if (p.Age < 66 && Policy.ReliefWage > 0) PayFromTreasury(p, Policy.ReliefWage, ref L.Relief);
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

        void HealthAndMood(int si, int crimes)
        {
            foreach (var p in People.ToList())
            {
                float dh = p.Fed >= 0.95f ? 2f : -(1 - p.Fed) * 14f;
                if (!p.Warm) dh -= ColdDamage[si] * (p.HomeId < 0 ? 1.2f : 1f) * (p.IsChild || p.Age > 60 ? 1.3f : 1f) * Rnd(0.6f, 1.4f);
                else if (p.Fed >= 0.95f) dh += 1.5f;
                if (p.Age > 60) dh -= (p.Age - 60) * 0.06f;
                p.Health = Clamp(p.Health + dh, 0, 100);
                if (p.Health <= 0)
                {
                    string cause = p.Hungry >= 3 && p.Fed < 0.7f ? "starved" : !p.Warm ? "froze to death" : "fell sick and died";
                    Die(p, cause);
                    continue;
                }

                float d = p.Fed >= 0.95f ? 1f : -(1 - p.Fed) * 8;
                if (!p.Warm) d -= ColdMood[si];
                if (p.HomeId < 0) d -= HomelessMood[si];
                if (!p.IsChild && p.Role == Role.Laborer) d -= 0.4f; else if (!p.IsChild) d += 0.4f;
                d += Policy.IncomeTax > 0.12f ? -(Policy.IncomeTax - 0.12f) * 12 : 0.4f;
                if (Policy.HeadTax > 1f) d -= (Policy.HeadTax - 1f) * 0.5f;
                if (MayorBonus(Platform.People)) d += 0.6f;
                if (p.Health < 50) d -= 1;
                if (p.CharityDays > 0) d -= 0.8f;
                d -= p.Grief * 0.04f;
                d -= Math.Min(3f, Unburied * 0.3f);
                d -= crimes * 0.4f;
                p.Happy = Clamp(p.Happy + d, 0, 100);
            }
            // Bodies with no grave weigh on the town less as time passes
            if (Unburied > 0 && Day % 14 == 0) Unburied--;
            // A drifter with nothing to stay for may move on
            foreach (var p in People.ToList())
                if (!p.IsChild && p.PartnerId < 0 && !ChildrenOf(p).Any() && p.Happy < 8 && p.Money < 5 && p.Role != Role.Mayor)
                {
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
            if (!Chance(0.035 + (AverageHappiness - 40) / 1500.0)) return;
            var names = new List<Citizen>();
            if (Chance(0.55))
            {
                string fam = Pick(Defs.LastNames);
                var dad = AddPerson(false, fam, Rnd(20, 42), (float)Math.Round(Rnd(20, 55)));
                var mom = AddPerson(true, fam, Rnd(19, 38), (float)Math.Round(Rnd(5, 25)));
                dad.PartnerId = mom.Id; mom.PartnerId = dad.Id;
                names.Add(dad); names.Add(mom);
                for (int k = Rng.Next(0, 3); k > 0; k--) { var c = AddPerson(Chance(0.5), fam, Rnd(1, 9), 0); c.ParentId = mom.Id; names.Add(c); }
                AddLog("The " + fam + " family (" + names.Count + ") rolled in on a wagon and asked to stay.", LogKind.Good);
            }
            else
            {
                var p = AddPerson(Chance(0.5), Pick(Defs.LastNames), Rnd(17, 35), (float)Math.Round(Rnd(10, 45)));
                names.Add(p);
                AddLog(p.Name + " drifted into town looking for work.", LogKind.Good);
            }
            foreach (var p in names) if (!p.IsChild) { Treasury += Balance.ClaimFee; LedgerThisWeek.Claims += Balance.ClaimFee; }
            RehouseEveryone();
        }

        /// <summary>How many hands an owner wants next week: hire while goods sell and the till can pay,
        /// cut back when stock piles up or money runs short. Fields and gatherers lay off for winter.</summary>
        int StaffingDecision(Building b, float profit)
        {
            var d = b.Def;
            int n = b.Employees.Count;
            int nextSeason = (((Day) % Balance.DaysPerYear) / Balance.DaysPerSeason);
            if ((b.Type == BuildingType.Farm || b.Type == BuildingType.Gatherer) && nextSeason == 3) return 0;
            if (b.Type == BuildingType.Farm && nextSeason == 0 && Season == 3) return Math.Min(d.MaxEmployees, 2);
            if (b.Cash < b.Wage * (n + 1) * 2) return Math.Max(0, n - 1);
            float fill = 0;
            if (d.IsRetail) fill = d.Sells.Average(g => b.Stock[(int)g]) / (d.Capacity / (float)d.Sells.Length);
            else
            {
                Good? made = null;
                foreach (var g in AllGoods) if (Produces(b.Type, g)) made = g;
                if (made.HasValue) fill = b.Stock[(int)made.Value] / (b.Type == BuildingType.Farm ? Balance.BarnCapacity : Balance.BusinessStockCap);
                else fill = profit > 0 ? 0.3f : 0.7f;
            }
            if (d.IsRetail) return fill < 0.3f || profit < 0 ? Math.Max(0, n - 1) : (b.Week.Sold > 30 * (n + 1) ? Math.Min(d.MaxEmployees, n + 1) : n);
            if (fill < 0.5f && b.Cash > b.Wage * (n + 1) * 4) return Math.Min(d.MaxEmployees, n + 1);
            if (fill > 0.9f) return Math.Max(0, n - 1);
            return Math.Max(n, Math.Min(b.Desired, d.MaxEmployees));
        }

        // =====================================================================
        // Sunday: taxes, profits, staffing and prices
        // =====================================================================
        void Weekly()
        {
            foreach (var p in Adults)
            {
                float a = Policy.HeadTax; if (a <= 0) continue;
                if (p.Money >= a) { p.Money -= a; Treasury += a; LedgerThisWeek.HeadTax += a; }
                else p.Happy -= 3;
            }
            foreach (var b in Businesses.ToList())
            {
                var d = b.Def;
                float profit = b.Week.Revenue - b.Week.Costs;
                float tax = Math.Min(Math.Max(0, b.Cash), Math.Max(0, profit) * Policy.BusinessTax);
                b.Cash -= tax; Treasury += tax; LedgerThisWeek.BusinessTax += tax;
                var o = GetCitizen(b.OwnerId);
                float reserve = b.Wage * (b.Employees.Count + 1) * 4 + 25;
                float draw = 0;
                if (o != null && b.Cash > reserve) { draw = (b.Cash - reserve) * 0.6f; b.Cash -= draw; o.Money += draw; }
                b.Last = new WeekReport { Valid = true, Revenue = b.Week.Revenue, Costs = b.Week.Costs, Profit = profit, Tax = tax, OwnerDraw = draw, Sold = b.Week.Sold, Made = b.Week.Made };
                b.Wage = Math.Max(Policy.MinWage, b.Wage);
                if (o != null && Day - b.BuiltDay >= 6)
                {
                    b.Desired = StaffingDecision(b, profit);
                    if (profit >= 0) b.LossWeeks = 0; else b.LossWeeks++;
                    while (b.Employees.Count > b.Desired)
                    {
                        var p = GetCitizen(b.Employees[b.Employees.Count - 1]);
                        if (p == null) { b.Employees.RemoveAt(b.Employees.Count - 1); continue; }
                        QuitJob(p);
                        AddLog(o.Name + " laid off " + p.Name + " at the " + d.Name + ". Business is slow.", LogKind.Bad);
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
            LedgerLastWeek = LedgerThisWeek; HasLastWeek = true; LedgerThisWeek = new TownLedger();
            CrimesLast = CrimesWeek; CaughtLast = CaughtWeek; CrimesWeek = 0; CaughtWeek = 0;
        }
    }
}
