using System;
using System.Collections.Generic;
using System.Linq;

namespace FrontierLedger.Simulation
{
    public partial class Town
    {
        /// <summary>One full day: gardens, farms, trade, shopping, wages, crime, mood, hiring, migration, elections.</summary>
        void SimulateDay()
        {
            var L = LedgerThisWeek;
            int si = Season;
            float gF = Balance.GardenBySeason[si];
            float fF = Balance.FarmBySeason[si] * (MayorBonus(Platform.Farm) ? 1.15f : 1f);
            float xF = MayorBonus(Platform.Trade) ? 1.25f : 1f;
            var biz = Businesses.ToList();
            foreach (var b in biz) b.Today.Clear();
            foreach (var p in People) { p.FedToday = 0; p.SaloonToday = -1; }

            // ---- Back gardens feed part of each household ----
            float gardenTotal = 0;
            foreach (var h in OfType(BuildingType.House))
            {
                var rs = h.Residents.Select(GetCitizen).Where(p => p != null).ToList();
                if (rs.Count == 0) { h.GardenToday = 0; continue; }
                float per = Math.Min(Balance.GardenPerPersonCap, Balance.GardenBase * gF / rs.Count);
                foreach (var p in rs) p.FedToday += per;
                h.GardenToday = per * rs.Count; gardenTotal += h.GardenToday;
            }
            var mayor = Mayor;
            if (mayor != null) { mayor.FedToday += 0.5f * gF; gardenTotal += 0.5f * gF; }

            // ---- Production ----
            var farms = OfType(BuildingType.Farm).ToList();
            foreach (var b in farms)
            {
                if (b.OwnerId < 0 && b.Employees.Count == 0) continue;
                int w = WorkersAt(b);
                float output = w * 3f * fF * (b.Tools > 0.05f ? 1.4f : 1f);
                b.Food += output; b.Today.Made = output; b.Week.Made += output;
                if (b.Tools > 0) b.Tools = Math.Max(0, b.Tools - w * 0.2f);
            }
            var smithies = OfType(BuildingType.Smithy).ToList();
            foreach (var b in smithies) { float output = WorkersAt(b) * 1.2f; b.Tools += output; b.Today.Made = output; b.Week.Made += output; }

            // ---- Farms buy tools from the blacksmith ----
            foreach (var f in farms)
            {
                if (f.OwnerId < 0 || f.Tools >= 2) continue;
                foreach (var s in smithies)
                {
                    int q = Math.Min(3, Math.Min((int)Math.Floor(s.Tools), (int)Math.Floor((f.Cash - 10) / Balance.ToolPrice)));
                    if (q <= 0) continue;
                    float c = q * Balance.ToolPrice;
                    s.Tools -= q; f.Tools += q; f.Cash -= c; f.Week.Costs += c; s.Cash += c; s.Week.Revenue += c; s.Today.Sold += q; s.Week.Sold += q;
                    break;
                }
            }

            // ---- Wholesale: farms sell food to stores and stalls ----
            foreach (var r in Shuffle(OpenRetailers()))
            {
                float room = r.Def.ShelfCapacity - r.Food;
                float budget = r.Cash - r.Wage * r.Employees.Count;
                foreach (var f in farms.OrderByDescending(x => x.Food))
                {
                    float q = Math.Min(room, Math.Min(f.Food, (float)Math.Floor(budget / Balance.Wholesale)));
                    if (q <= 0) continue;
                    float c = q * Balance.Wholesale;
                    f.Food -= q; r.Food += q; r.Cash -= c; r.Week.Costs += c; f.Cash += c; f.Week.Revenue += c;
                    f.Today.Sold += q; f.Week.Sold += q; r.Today.Bought += q; room -= q; budget -= c;
                }
            }

            // ---- Surplus leaves on the freight wagon ----
            foreach (var f in farms)
                if (f.Food > Balance.BarnCapacity)
                {
                    float ex = f.Food - Balance.BarnCapacity; f.Food = Balance.BarnCapacity;
                    float c = ex * Balance.ExportFood * xF; f.Cash += c; f.Week.Revenue += c; f.Today.Exported = ex;
                }
            foreach (var s in smithies)
                if (s.Tools > 12)
                {
                    float ex = s.Tools - 12; s.Tools = 12;
                    float c = ex * Balance.ExportTools * xF; s.Cash += c; s.Week.Revenue += c; s.Today.Exported = ex;
                }

            // ---- Settlers buy the food their gardens didn't cover ----
            var shops = OpenRetailers();
            float demand = 0, bought = 0;
            foreach (var p in Shuffle(People.ToList()))
            {
                float need = Math.Max(0, 1 - p.FedToday); demand += need;
                foreach (var r in shops.OrderBy(x => x.Price))
                {
                    if (need <= 0.001f) break;
                    float q = Math.Min(need, Math.Min(r.Food, p.Money / r.Price));
                    if (q <= 0.001f) continue;
                    float c = q * r.Price;
                    p.Money -= c; r.Food -= q; r.Cash += c; r.Week.Revenue += c; r.Today.Sold += q; r.Week.Sold += q;
                    need -= q; p.FedToday += q; bought += q;
                }
                p.Fed = Math.Min(1, p.FedToday);
                if (p.Fed < 0.7f) p.Hungry++; else p.Hungry = Math.Max(0, p.Hungry - 1);
            }
            Market = new MarketReport { Garden = gardenTotal, Demand = demand, Bought = bought, Shortfall = Math.Max(0, demand - bought) };

            // ---- Saloon ----
            foreach (var s in OfType(BuildingType.Saloon))
            {
                int cap = WorkersAt(s) * 6; if (cap == 0) continue;
                foreach (var p in Shuffle(People.Where(x => x.Money > 12 && x.Jail == 0).ToList()))
                {
                    if (cap <= 0) break;
                    if (Rng.NextDouble() < 0.45)
                    {
                        p.Money -= Balance.DrinkPrice; s.Cash += Balance.DrinkPrice; s.Week.Revenue += Balance.DrinkPrice;
                        s.Today.Sold++; s.Week.Sold++; p.Happy += 3; p.SaloonToday = s.Id; cap--;
                    }
                }
            }

            // ---- Business payroll ----
            foreach (var b in biz)
            {
                foreach (var id in b.Employees.ToList())
                {
                    var p = GetCitizen(id); if (p == null || p.Jail > 0) continue;
                    if (b.Cash >= b.Wage)
                    {
                        b.Cash -= b.Wage; b.Week.Costs += b.Wage;
                        float tax = b.Wage * Policy.IncomeTax;
                        p.Money += b.Wage - tax; Treasury += tax; L.IncomeTax += tax;
                    }
                    else
                    {
                        QuitJob(p); b.Desired = Math.Min(b.Desired, b.Employees.Count);
                        var o = GetCitizen(b.OwnerId);
                        AddLog((o != null ? o.Name : "The " + b.Def.Name) + " couldn't make payroll and let " + p.Name + " go.", LogKind.Bad);
                    }
                }
            }

            // ---- Public salaries ----
            foreach (var p in People.ToList())
            {
                float sal = p.Role == Role.Mayor ? Balance.SalaryMayor : p.Role == Role.Sheriff ? Balance.SalarySheriff : p.Role == Role.Deputy ? Balance.SalaryDeputy : 0;
                if (sal <= 0 || p.Jail > 0) continue;
                if (Treasury >= sal)
                {
                    Treasury -= sal; L.Salaries += sal;
                    float tax = sal * Policy.IncomeTax; p.Money += sal - tax; Treasury += tax; L.IncomeTax += tax; p.Unpaid = 0;
                }
                else
                {
                    p.Unpaid++; p.Happy -= 5;
                    if (p.Unpaid >= 3 && p.Role != Role.Mayor)
                    {
                        AddLog(p.Name + " quit. The treasury hasn't paid the " + Defs.RoleName(p.Role).ToLower() + " in days.", LogKind.Bad);
                        QuitJob(p);
                    }
                }
            }

            // ---- Upkeep ----
            float up = (Office != null ? 0.5f : 0) + (Estate != null ? 0.5f : 0);
            Treasury -= up; L.Upkeep += up;

            // ---- Crime: broke, hungry settlers steal ----
            var off = Office;
            int law = off != null ? off.Employees.Select(GetCitizen).Count(p => p != null && p.Jail == 0) : 0;
            float catchP = law > 0 ? Math.Min(0.9f, 0.25f + 0.18f * law + (MayorBonus(Platform.Law) ? 0.15f : 0)) : 0.05f;
            int crimesToday = 0;
            foreach (var p in People.ToList())
            {
                if (p.Jail > 0) { p.Jail--; continue; }
                if (p.Role == Role.Sheriff || p.Role == Role.Deputy) continue;
                if (p.Money < 3 && p.Hungry >= 2 && Rng.NextDouble() < 0.22 * (MayorBonus(Platform.Law) ? 0.5 : 1))
                {
                    var bizTargets = biz.Where(b => b.Cash > 5 && Buildings.Contains(b)).ToList();
                    var personTargets = People.Where(q => q != p && q.Money > 8).ToList();
                    int total = bizTargets.Count + personTargets.Count;
                    if (total == 0) continue;
                    int k = Rng.Next(total);
                    Building tb = k < bizTargets.Count ? bizTargets[k] : null;
                    Citizen tp = tb == null ? personTargets[k - bizTargets.Count] : null;
                    string tname = tb != null ? "the " + tb.Def.Name : tp.Name;
                    crimesToday++; CrimesWeek++;
                    if (Rng.NextDouble() < catchP)
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

            // ---- Mood ----
            foreach (var p in People)
            {
                float d = p.Fed >= 0.95f ? 1.2f : -(1 - p.Fed) * 10;
                d += p.Role == Role.Unemployed ? -1.2f : 0.4f;
                d += Policy.IncomeTax > 0.12f ? -(Policy.IncomeTax - 0.12f) * 25 : 0.6f;
                if (MayorBonus(Platform.People)) d += 0.6f;
                if (Policy.HeadTax > 1.5f) d -= (Policy.HeadTax - 1.5f) * 0.6f;
                if (p.HomeId < 0) { d -= 3; p.Homeless++; }
                d -= crimesToday * 0.4f;
                p.Happy = Clamp(p.Happy + d, 0, 100);
            }

            Hiring();

            // ---- People give up and leave ----
            foreach (var p in People.ToList())
            {
                if (p.Hungry >= 6) RemovePerson(p, "left town hungry, unable to afford food.");
                else if (p.Happy < 8 && p.Money < 6) RemovePerson(p, "packed a wagon and left, fed up with life here.");
                else if (p.Homeless >= 5) RemovePerson(p, "gave up waiting for a house and moved on.");
            }

            // ---- New settlers arrive ----
            int vacancy = FreeBeds;
            float ah = AverageHappiness;
            bool openJobs = Businesses.Any(b => b.OwnerId >= 0 && b.Employees.Count < Math.Min(b.Def.MaxEmployees, b.Desired))
                            || Businesses.Any(b => b.OwnerId < 0)
                            || (off != null && off.Employees.Count < 1 + Policy.Deputies);
            if (vacancy > 0 && ah > 35 && Rng.NextDouble() < 0.25 + (openJobs ? 0.3 : 0) + (ah - 50) / 200)
            {
                int n = vacancy >= 2 && Rng.NextDouble() < 0.3 ? 2 : 1;
                var names = new List<string>();
                for (int i = 0; i < n; i++)
                {
                    names.Add(AddPerson((float)Math.Round(Rnd(12, 45))).Name);
                    Treasury += Balance.ClaimFee; L.Claims += Balance.ClaimFee;
                }
                AddLog(string.Join(" and ", names.ToArray()) + " arrived by wagon and settled in.", LogKind.Good);
            }

            ElectionStep();
            if (Day % 7 == 0) Weekly();
            History.Add(new HistoryPoint { Day = Day, Treasury = Treasury, Population = People.Count });
            if (History.Count > 84) History.RemoveAt(0);
        }

        /// <summary>Sunday: head tax, business tax, owners take profit, staffing and price decisions.</summary>
        void Weekly()
        {
            foreach (var p in People)
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
                float reserve = b.Wage * Math.Max(1, b.Employees.Count) * 7 + 15;
                float draw = 0;
                if (o != null && b.Cash > reserve) { draw = (b.Cash - reserve) * 0.6f; b.Cash -= draw; o.Money += draw; }
                b.Last = new WeekReport { Valid = true, Revenue = b.Week.Revenue, Costs = b.Week.Costs, Profit = profit, Tax = tax, OwnerDraw = draw, Sold = b.Week.Sold, Made = b.Week.Made };
                b.Wage = Math.Max(Policy.MinWage, b.Wage);
                if (o != null && Day - b.BuiltDay >= 6)
                {
                    if (profit > b.Wage * 7 * 0.5f && b.Cash > b.Wage * 7) b.Desired = Math.Min(d.MaxEmployees, b.Desired + 1);
                    else if (profit < 0) { b.LossWeeks++; b.Desired = Math.Max(0, b.Employees.Count - 1); }
                    if (profit >= 0) b.LossWeeks = 0;
                    while (b.Employees.Count > b.Desired)
                    {
                        var p = GetCitizen(b.Employees[b.Employees.Count - 1]);
                        if (p == null) { b.Employees.RemoveAt(b.Employees.Count - 1); continue; }
                        QuitJob(p);
                        AddLog(o.Name + " laid off " + p.Name + " at the " + d.Name + ". Business is slow.", LogKind.Bad);
                    }
                }
                if (d.IsRetail)
                {
                    float fill = b.Food / d.ShelfCapacity;
                    if (fill < 0.2f) b.Price = Math.Min(3.4f, b.Price + 0.1f);
                    else if (fill > 0.6f) b.Price = Math.Max(1.6f, b.Price - 0.1f);
                }
                b.Week.Clear();
            }
            LedgerLastWeek = LedgerThisWeek; HasLastWeek = true; LedgerThisWeek = new TownLedger();
            CrimesLast = CrimesWeek; CaughtLast = CaughtWeek; CrimesWeek = 0; CaughtWeek = 0;
        }

        // =====================================================================
        // Elections
        // =====================================================================
        float PrefScore(Citizen v, Candidate c)
        {
            var cp = GetCitizen(c.CitizenId); if (cp == null) return -9;
            if (v.Id == c.CitizenId) return 99;
            float s = Noise.Hash((int)(v.Seed * 100000), c.CitizenId * 7, Campaign.VoteDay) * 0.8f;
            var own = OwnedBusiness(cp); if (own != null && v.WorkId == own.Id) s += 0.6f;
            if (v.HomeId >= 0 && v.HomeId == cp.HomeId) s += 0.4f;
            var vb = GetBuilding(v.WorkId);
            BuildingType? t = vb != null ? vb.Type : (BuildingType?)null;
            if (c.Platform == Platform.Farm && t == BuildingType.Farm) s += 0.5f;
            if (c.Platform == Platform.Trade && (t == BuildingType.Store || t == BuildingType.Stall || t == BuildingType.Smithy || t == BuildingType.Saloon)) s += 0.45f;
            if (c.Platform == Platform.Law) s += Math.Min(0.8f, CrimesLast * 0.12f + CrimesWeek * 0.1f) + (Day - v.RobbedDay < 20 ? 0.5f : 0);
            if (c.Platform == Platform.People && (v.Role == Role.Unemployed || v.Money < 10 || v.Happy < 40)) s += 0.55f;
            if (MayorId == c.CitizenId) s += (AverageHappiness - 50) / 80;
            return s;
        }

        /// <summary>Who this settler would vote for today, or null outside a campaign.</summary>
        public Candidate VoteOf(Citizen v)
        {
            if (!Campaign.Active) return null;
            Candidate best = null; float bs = float.MinValue;
            foreach (var c in Campaign.Candidates) { float s = PrefScore(v, c); if (s > bs) { bs = s; best = c; } }
            return best;
        }

        public Dictionary<int, int> Poll()
        {
            var t = new Dictionary<int, int>();
            foreach (var c in Campaign.Candidates) t[c.CitizenId] = 0;
            foreach (var v in People) { var c = VoteOf(v); if (c != null) t[c.CitizenId]++; }
            return t;
        }

        void StartCampaign(int days)
        {
            var el = Eligible().OrderByDescending(Standing).Take(3).ToList();
            if (el.Count == 0) return;
            var plats = Shuffle(new List<Platform> { Platform.Law, Platform.Farm, Platform.Trade, Platform.People });
            var inc = Mayor;
            var cands = el.Select(p => new Candidate { CitizenId = p.Id, Platform = p == inc && MayorPlatform != Platform.None ? MayorPlatform : Platform.None }).ToList();
            var used = new HashSet<Platform>(cands.Where(c => c.Platform != Platform.None).Select(c => c.Platform));
            foreach (var c in cands)
                if (c.Platform == Platform.None) { c.Platform = plats.First(k => !used.Contains(k)); used.Add(c.Platform); }
            if (cands.Count == 1)
            {
                var p = GetCitizen(cands[0].CitizenId);
                InstallMayor(p, cands[0].Platform);
                AddLog(p.Name + " runs unopposed and becomes mayor on a " + Defs.Get(cands[0].Platform).Name + " platform.", LogKind.Vote);
                return;
            }
            Campaign = new Campaign { Active = true, VoteDay = Day + days, Candidates = cands };
            AddLog("Campaign season! " + string.Join(", ", cands.Select(c => GetCitizen(c.CitizenId).Name).ToArray()) +
                   " are running for mayor. Votes are cast on day " + Campaign.VoteDay + ".", LogKind.Vote);
        }

        void HoldElection()
        {
            var t = Poll();
            var cands = Campaign.Candidates.OrderByDescending(c => t[c.CitizenId]).ThenByDescending(c => Standing(GetCitizen(c.CitizenId))).ToList();
            var w = cands[0]; var wp = GetCitizen(w.CitizenId);
            LastElection = new ElectionResult
            {
                Valid = true, Day = Day, TotalVoters = People.Count,
                Results = cands.Select(c => new CandidateResult { Name = GetCitizen(c.CitizenId).Name, Platform = c.Platform, Votes = t[c.CitizenId] }).ToList()
            };
            bool wasInc = MayorId == w.CitizenId;
            Campaign = new Campaign();
            InstallMayor(wp, w.Platform);
            AddLog("Election day! " + wp.Name + (wasInc ? " is re-elected" : " wins") + " with " + t[w.CitizenId] + " of " + People.Count +
                   " votes on a " + Defs.Get(w.Platform).Name + " platform.", LogKind.Vote);
            if (!wasInc) AddLog(wp.Name + " moves into the Mayor's Estate.", LogKind.Vote);
        }

        void ElectionStep()
        {
            var e = Estate;
            if (e == null) { Campaign.Active = false; return; }
            var m = Mayor;
            if (!Policy.ElectionsOn)
            {
                Campaign.Active = false;
                if (m == null)
                {
                    var c = Eligible().OrderByDescending(Standing).FirstOrDefault();
                    if (c != null)
                    {
                        var pl = (Platform)Rng.Next(4);
                        InstallMayor(c, pl);
                        AddLog(c.Name + " is appointed Mayor for life (" + Defs.Get(pl).Name + ").", LogKind.Vote);
                    }
                }
                return;
            }
            if (Campaign.Active) { if (Day >= Campaign.VoteDay) HoldElection(); return; }
            if (m == null) StartCampaign(4);
            else if (Day >= TermEnd - 7) StartCampaign(Math.Max(1, TermEnd - Day));
        }

        /// <summary>Turning elections back on schedules the next vote two weeks out.</summary>
        public void SetElections(bool on)
        {
            if (Policy.ElectionsOn == on) return;
            Policy.ElectionsOn = on;
            if (on && MayorId >= 0) TermEnd = Day + 14;
            AddLog(on ? "The council restores elections. The next vote is two weeks out." : "The council suspends elections. The mayor serves for life.", LogKind.Vote);
        }
    }
}
