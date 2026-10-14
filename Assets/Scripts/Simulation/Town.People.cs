using System;
using System.Collections.Generic;
using System.Linq;

namespace FrontierLedger.Simulation
{
    public partial class Town
    {
        // =====================================================================
        // Settlers
        // =====================================================================
        Citizen AddPerson(bool female, string family, float age, float money)
        {
            var p = new Citizen
            {
                Id = NextId++,
                First = Pick(female ? Defs.FemaleNames : Defs.MaleNames),
                Family = family,
                Female = female,
                Age = age,
                Money = money,
                Role = age < Balance.AdultAge ? Role.Child : Role.Laborer,
                Happy = Rnd(55, 70),
                Health = Rnd(85, 100),
                Seed = (float)Rng.NextDouble(),
                ArrivedDay = Day
            };
            People.Add(p);
            return p;
        }

        public IEnumerable<Citizen> ChildrenOf(Citizen p) { return People.Where(c => c.IsChild && (c.ParentId == p.Id || c.ParentId == p.PartnerId && p.PartnerId >= 0)); }
        public IEnumerable<Citizen> Household(Building h) { return h.Residents.Select(GetCitizen).Where(x => x != null); }

        void LeaveHome(Citizen p)
        {
            var h = GetBuilding(p.HomeId);
            if (h != null) h.Residents.Remove(p.Id);
            p.HomeId = -1;
        }

        void MoveInto(Citizen p, Building h)
        {
            if (p.HomeId == h.Id) return;
            LeaveHome(p);
            p.HomeId = h.Id; h.Residents.Add(p.Id); p.Homeless = 0;
        }

        /// <summary>Puts homeless households into houses: whole families into empty cottages first,
        /// then singles and orphans into any spare bed.</summary>
        void RehouseEveryone()
        {
            foreach (var h in Buildings) h.Residents.RemoveAll(id => GetCitizen(id) == null);
            var houses = OfType(BuildingType.House).ToList();
            // Families first (couples and parents with children), then people on their own
            var adults = People.Where(p => !p.IsChild && p.HomeId < 0 && p.Role != Role.Mayor)
                               .OrderBy(p => p.PartnerId >= 0 || ChildrenOf(p).Any() ? 0 : 1).ThenBy(p => p.ArrivedDay).ToList();
            foreach (var a in adults)
            {
                if (a.HomeId >= 0) continue;
                var partner = GetCitizen(a.PartnerId);
                var unit = new List<Citizen> { a };
                if (partner != null && partner.Role != Role.Mayor) unit.Add(partner);
                unit.AddRange(ChildrenOf(a).Where(c => c.HomeId < 0));
                if (partner != null && partner.HomeId >= 0 && partner.Role != Role.Mayor)
                {
                    // Partner already housed: join them if there is room
                    var ph = GetBuilding(partner.HomeId);
                    if (ph != null && ph.Type == BuildingType.House && ph.Def.Beds - ph.Residents.Count >= unit.Count - 1)
                    { foreach (var u in unit) if (u != partner) MoveInto(u, ph); continue; }
                }
                var empty = houses.FirstOrDefault(h => h.Residents.Count == 0);
                var target = empty ?? (unit.Count == 1 ? houses.Where(h => h.Def.Beds - h.Residents.Count >= 1).OrderBy(h => h.Residents.Count).FirstOrDefault() : null);
                if (target != null) foreach (var u in unit) MoveInto(u, target);
            }
            // Children go where their parents live; orphans take any spare bed
            foreach (var c in People.Where(p => p.IsChild && p.HomeId < 0))
            {
                var parent = GetCitizen(c.ParentId);
                var home = parent != null ? GetBuilding(parent.HomeId) : null;
                if (home != null && home.Type == BuildingType.House && home.Residents.Count < home.Def.Beds) { MoveInto(c, home); continue; }
                if (parent != null) continue;
                var spare = houses.Where(h => h.Residents.Count < h.Def.Beds && h.Residents.Count > 0).OrderBy(h => h.Residents.Count).FirstOrDefault();
                if (spare != null) MoveInto(c, spare);
            }
        }

        void QuitJob(Citizen p)
        {
            var b = GetBuilding(p.WorkId);
            if (b != null) b.Employees.Remove(p.Id);
            p.WorkId = -1;
            p.Role = p.IsChild ? Role.Child : Role.Laborer;
        }

        void ResumeOrIdle(Citizen p)
        {
            var own = OwnedBusiness(p);
            if (own != null) { p.Role = Role.Owner; p.WorkId = own.Id; }
            else { p.Role = Role.Laborer; p.WorkId = -1; }
        }

        void UnseatMayor(Citizen m, bool rehouse)
        {
            var e = Estate; if (e != null) e.Residents.Clear();
            MayorId = -1; MayorPlatform = Platform.None;
            ResumeOrIdle(m); m.HomeId = -1;
            if (rehouse) RehouseEveryone();
        }

        void InstallMayor(Citizen p, Platform platform)
        {
            var e = Estate; if (e == null) return;
            var old = Mayor;
            if (old != null && old != p) { UnseatMayor(old, false); AddLog(old.Name + " packs up and moves out of the Mayor's Estate."); }
            if (p.Role == Role.Worker || p.Role == Role.Sheriff || p.Role == Role.Deputy || p.Role == Role.Builder) QuitJob(p);
            LeaveHome(p);
            p.Role = Role.Mayor; p.WorkId = e.Id; p.HomeId = e.Id;
            e.Residents.Clear(); e.Residents.Add(p.Id);
            MayorId = p.Id; MayorPlatform = platform; TermEnd = Day + Balance.TermDays;
            RehouseEveryone();
        }

        /// <summary>Takes a settler out of the town, for death or leaving.</summary>
        void RemovePerson(Citizen p)
        {
            if (p.Role == Role.Mayor) { MayorId = -1; MayorPlatform = Platform.None; var e = Estate; if (e != null) e.Residents.Clear(); }
            else if (p.Role != Role.Owner) QuitJob(p);
            var own = OwnedBusiness(p);
            if (own != null)
            {
                // A grown child or partner inherits the business; otherwise it goes up for sale
                var heir = GetCitizen(p.PartnerId);
                if (heir != null && !heir.IsChild && OwnedBusiness(heir) == null && heir.Role != Role.Mayor)
                {
                    if (heir.Role == Role.Worker || heir.Role == Role.Builder || heir.Role == Role.Sheriff || heir.Role == Role.Deputy) QuitJob(heir);
                    own.OwnerId = heir.Id; heir.Role = Role.Owner; heir.WorkId = own.Id; heir.Money += p.Money; p.Money = 0;
                    AddLog(heir.Name + " takes over the " + own.Def.Name + ".");
                }
                else { own.OwnerId = -1; AddLog("The " + own.Def.Name + " is up for sale."); }
            }
            var partner = GetCitizen(p.PartnerId);
            if (partner != null) { partner.PartnerId = -1; partner.Money += p.Money; }
            foreach (var c in People.Where(c => c.ParentId == p.Id)) c.ParentId = partner != null ? partner.Id : -1;
            LeaveHome(p);
            if (Campaign.Active)
            {
                Campaign.Candidates.RemoveAll(c => c.CitizenId == p.Id);
                if (Campaign.Candidates.Count == 0) Campaign.Active = false;
            }
            People.Remove(p);
        }

        void Die(Citizen p, string cause)
        {
            var kin = People.Where(x => x != p && (x.Id == p.PartnerId || x.ParentId == p.Id || x.Id == p.ParentId || (x.Family == p.Family && x.HomeId == p.HomeId && x.HomeId >= 0))).ToList();
            RemovePerson(p);
            foreach (var k in kin) k.Grief = Math.Max(k.Grief, 60);
            foreach (var o in People) o.Happy = Clamp(o.Happy - 1.5f, 0, 100);
            Deaths++;
            var cem = OfType(BuildingType.Cemetery).FirstOrDefault(c => c.Graves < c.Def.Graves);
            string where;
            if (cem != null) { cem.Graves++; where = " Buried in the cemetery."; }
            else { Unburied++; where = " There is no cemetery to bury " + (p.Female ? "her" : "him") + " in."; }
            AddLog(p.Name + (p.IsChild ? ", a child of " + Math.Floor(p.Age) + "," : ", aged " + Math.Floor(p.Age) + ",") + " " + cause + "." + where, LogKind.Death);
            RehouseEveryone();
        }

        float Standing(Citizen p) { return p.Money + (OwnedBusiness(p) != null ? 30 : 0) + p.Happy * 0.3f + (MayorId == p.Id ? 15 : 0) + Math.Min(20, p.Age - 18) * 0.5f; }
        List<Citizen> Eligible() { return People.Where(p => !p.IsChild && p.Age >= 21 && p.Role != Role.Sheriff && p.Role != Role.Deputy && p.Jail == 0 && Day - p.ArrivedDay >= 3).ToList(); }
        bool Employable(Citizen p) { return !p.IsChild && p.Role == Role.Laborer && p.Jail == 0 && p.Age < 66; }

        // =====================================================================
        // Families: aging, marriage, births, old age
        // =====================================================================
        void FamilyLife()
        {
            float yearPerDay = 1f / Balance.DaysPerYearF;
            foreach (var p in People.ToList())
            {
                bool wasChild = p.IsChild;
                p.Age += yearPerDay;
                if (wasChild && !p.IsChild)
                {
                    p.Role = Role.Laborer;
                    AddLog(p.Name + " has come of age and is looking for work.", LogKind.Good);
                }
                p.Grief = Math.Max(0, p.Grief - 1.5f);
            }

            // Marriage: two single adults wed when there is a cottage for them
            if (Chance(Balance.MarriageChancePerDay))
            {
                var men = People.Where(p => !p.IsChild && !p.Female && p.PartnerId < 0 && p.Age < 55 && p.Role != Role.Mayor).ToList();
                var women = People.Where(p => !p.IsChild && p.Female && p.PartnerId < 0 && p.Age < 50 && p.Role != Role.Mayor).ToList();
                var empty = OfType(BuildingType.House).FirstOrDefault(h => h.Residents.Count == 0);
                if (men.Count > 0 && women.Count > 0 && empty != null)
                {
                    var m = Pick(men);
                    var w = women.OrderBy(x => Math.Abs(x.Age - m.Age)).First();
                    m.PartnerId = w.Id; w.PartnerId = m.Id;
                    var kids = ChildrenOf(w).Concat(ChildrenOf(m)).Distinct().ToList();
                    string old = w.Family; w.Family = m.Family;
                    MoveInto(m, empty); MoveInto(w, empty);
                    foreach (var k in kids) if (empty.Residents.Count < empty.Def.Beds) MoveInto(k, empty);
                    m.Happy = Math.Min(100, m.Happy + 15); w.Happy = Math.Min(100, w.Happy + 15);
                    AddLog(m.First + " " + m.Family + " and " + w.First + " " + old + " were married and moved into a cottage of their own.", LogKind.Birth);
                }
            }

            // Births: families hold off when the town is short of food
            float foodPerHead = People.Count == 0 ? 0 : TotalFood() / People.Count;
            if (foodPerHead >= 8f)
            foreach (var w in People.Where(p => p.Female && !p.IsChild && p.Age >= 18 && p.Age <= 44 && p.PartnerId >= 0).ToList())
            {
                var h = GetBuilding(w.HomeId);
                var m = GetCitizen(w.PartnerId);
                if (h == null || m == null || m.HomeId != w.HomeId || h.Residents.Count >= h.Def.Beds) continue;
                if (w.Fed < 0.9f || w.Health < 60) continue;
                if (!Chance(Balance.BirthChancePerDay)) continue;
                var baby = AddPerson(Chance(0.5), m.Family, 0, 0);
                baby.ParentId = w.Id; baby.Health = 100;
                MoveInto(baby, h);
                Births++;
                w.Happy = Math.Min(100, w.Happy + 20); m.Happy = Math.Min(100, m.Happy + 20);
                AddLog(w.First + " and " + m.First + " " + m.Family + " had a " + (baby.Female ? "daughter" : "son") + ", " + baby.First + ".", LogKind.Birth);
            }

            // Old age
            foreach (var p in People.ToList())
                if (p.Age > 55 && Chance((p.Age - 55) * 0.0007)) Die(p, "died of old age");
        }

        // =====================================================================
        // Jobs
        // =====================================================================
        void AssignOwner(Building b)
        {
            var d = b.Def;
            var c = People.Where(p => !p.IsChild && (p.Role == Role.Laborer || p.Role == Role.Worker) && p.Jail == 0 && p.Money >= 10 && p.Age < 60 && OwnedBusiness(p) == null)
                          .OrderByDescending(p => p.Money).FirstOrDefault();
            if (c == null) return;
            if (c.Role != Role.Laborer) QuitJob(c);
            float invest = c.Money * 0.6f, deed = Math.Min(invest * 0.5f, 8f + (d.Logs + d.Stone) * 0.3f);
            c.Money -= invest; Treasury += deed; LedgerThisWeek.Deeds += deed;
            float grant = Math.Min(30f, Math.Max(0f, Treasury)); Treasury -= grant; LedgerThisWeek.Grants += grant;
            b.Cash += grant + invest - deed; b.OwnerId = c.Id; c.Role = Role.Owner; c.WorkId = b.Id; b.LossWeeks = 0; b.BuiltDay = Day;
            AddLog(c.Name + " bought the deed to the " + d.Name + " for " + Money(deed) + ".", LogKind.Good);
        }

        struct Vacancy { public Building B; public float Wage; public Role Role; }

        void Hire(Citizen p, Vacancy v)
        {
            p.Role = v.Role;
            if (v.Role == Role.Builder) { p.WorkId = -1; AddLog(p.Name + " joined the town's building crew at " + Money(v.Wage) + "/day."); return; }
            p.WorkId = v.B.Id; v.B.Employees.Add(p.Id);
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
            var builders = People.Where(p => p.Role == Role.Builder).ToList();
            while (builders.Count > Policy.Builders) { var b = builders[builders.Count - 1]; builders.RemoveAt(builders.Count - 1); QuitJob(b); }

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
            if (Treasury > 5) for (int i = builders.Count; i < Policy.Builders; i++) vac.Add(new Vacancy { Wage = Policy.BuilderWage + 0.01f, Role = Role.Builder });
            vac = vac.OrderByDescending(v => v.Wage).ToList();
            var idle = Shuffle(People.Where(Employable).ToList());
            foreach (var v in vac) { if (idle.Count == 0) break; var p = idle[0]; idle.RemoveAt(0); Hire(p, v); }
        }

        public void SetMinWage(float v)
        {
            Policy.MinWage = v;
            foreach (var b in Businesses) b.Wage = Math.Max(v, b.Def.BaseWage);
        }
    }
}
