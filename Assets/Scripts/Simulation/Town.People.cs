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
                WorthStart = money,
                Role = age < Balance.AdultAge ? Role.Child : Role.Laborer,
                Happy = Rnd(55, 70),
                Health = Rnd(85, 100),
                Seed = (float)Rng.NextDouble(),
                ArrivedDay = Day,
                Clothes = Rnd(0.7f, 1f)
            };
            // grown settlers mostly had some school back east; children as much as their age allowed
            if (age >= Balance.AdultAge) p.Schooling = Chance(Balance.SchooledNewcomers) ? Rnd(0.5f, 1f) : Rnd(0f, 0.4f);
            else if (age > Balance.SchoolAge) p.Schooling = Math.Min(1f, (age - Balance.SchoolAge) / 4f) * Rnd(0.3f, 1f);
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

        /// <summary>A settler with the partner and children who live with them.</summary>
        List<Citizen> FamilyUnit(Citizen a)
        {
            var unit = new List<Citizen> { a };
            var partner = GetCitizen(a.PartnerId);
            if (partner != null && partner.Role != Role.Mayor && partner.HomeId == a.HomeId) unit.Add(partner);
            unit.AddRange(ChildrenOf(a).Where(c => c.HomeId == a.HomeId && !unit.Contains(c)));
            return unit;
        }

        /// <summary>Spare beds. In a boarding house two children share a bed.</summary>
        float FreeIn(Building h)
        {
            if (h.Type != BuildingType.BoardingHouse) return h.Def.Beds - h.Residents.Count;
            float used = 0; foreach (var id in h.Residents) { var r = GetCitizen(id); used += r != null && r.IsChild ? 0.5f : 1f; }
            return h.Def.Beds - used;
        }
        static float BedsFor(List<Citizen> unit, bool boarding) { return boarding ? unit.Sum(u => u.IsChild ? 0.5f : 1f) : unit.Count; }

        /// <summary>
        /// Finds everyone a bed. Families want a cottage of their own and only board when none is free;
        /// single adults take a boarding-house bed, and move out of other people's cottages when one opens up.
        /// </summary>
        void RehouseEveryone()
        {
            foreach (var h in Buildings) h.Residents.RemoveAll(id => GetCitizen(id) == null);
            var houses = OfType(BuildingType.House).ToList();
            var boards = OfType(BuildingType.BoardingHouse).ToList();

            // Boarding families move into an empty cottage as soon as there is one
            foreach (var bh in boards)
                foreach (var id in bh.Residents.ToList())
                {
                    var a = GetCitizen(id);
                    if (a == null || a.IsChild || a.HomeId != bh.Id) continue;
                    var unit = FamilyUnit(a);
                    if (unit.Count < 2) continue;
                    var empty = houses.FirstOrDefault(h => h.Residents.Count == 0);
                    if (empty == null) break;
                    foreach (var u in unit) MoveInto(u, empty);
                    AddLog("The " + a.Family + " family left the boarding house for a cottage of their own.", LogKind.Good);
                }
            // Grown children and lodgers move out of a family's cottage into the boarding house
            foreach (var h in houses)
                foreach (var id in h.Residents.ToList())
                {
                    var a = GetCitizen(id);
                    if (a == null || a.IsChild || a.PartnerId >= 0 || ChildrenOf(a).Any() || a.HomeId != h.Id) continue;
                    bool family = h.Residents.Select(GetCitizen).Any(o => o != null && o != a && !o.IsChild && (o.PartnerId >= 0 || ChildrenOf(o).Any()));
                    if (!family) continue;
                    var bed = boards.FirstOrDefault(b => FreeIn(b) >= 1);
                    if (bed == null) break;
                    MoveInto(a, bed);
                }

            // Then everyone without a roof, families first
            var adults = People.Where(p => !p.IsChild && p.HomeId < 0 && p.Role != Role.Mayor && MayRehouse(p))
                               .OrderBy(p => p.PartnerId >= 0 || ChildrenOf(p).Any() ? 0 : 1).ThenBy(p => p.ArrivedDay).ToList();
            foreach (var a in adults)
            {
                if (a.HomeId >= 0) continue;
                var partner = GetCitizen(a.PartnerId);
                var unit = new List<Citizen> { a };
                if (partner != null && partner.Role != Role.Mayor && partner.HomeId < 0) unit.Add(partner);
                unit.AddRange(ChildrenOf(a).Where(c => c.HomeId < 0));
                if (partner != null && partner.HomeId >= 0)
                {
                    // Partner already housed: join them if there is room
                    var ph = GetBuilding(partner.HomeId);
                    if (ph != null && IsHome(ph.Type) && FreeIn(ph) >= BedsFor(unit, ph.Type == BuildingType.BoardingHouse)) { foreach (var u in unit) MoveInto(u, ph); continue; }
                }
                var empty = houses.FirstOrDefault(h => h.Residents.Count == 0);
                Building target;
                float need = BedsFor(unit, true);
                if (unit.Count > 1)
                {
                    target = empty ?? boards.Where(b => FreeIn(b) >= need).OrderBy(b => b.Residents.Count).FirstOrDefault();
                    // children come first: a single boarder gives up their bed to a family with little ones
                    if (target == null && unit.Any(u => u.IsChild))
                        foreach (var bh in boards)
                        {
                            var singles = Household(bh).Where(x => !x.IsChild && x.PartnerId < 0 && !ChildrenOf(x).Any()).OrderByDescending(x => x.ArrivedDay).ToList();
                            if (FreeIn(bh) + singles.Count < need) continue;
                            foreach (var sgl in singles)
                            {
                                if (FreeIn(bh) >= need) break;
                                LeaveHome(sgl);
                                AddLog(sgl.Name + " gave up a bed in the boarding house so the " + a.Family + " children could sleep indoors.", LogKind.Info);
                            }
                            target = bh; break;
                        }
                }
                else target = boards.Where(b => FreeIn(b) >= 1).OrderBy(b => b.Residents.Count).FirstOrDefault()
                              ?? empty ?? houses.Where(h => FreeIn(h) >= 1).OrderBy(h => h.Residents.Count).FirstOrDefault();
                if (target != null) foreach (var u in unit) MoveInto(u, target);
            }
            // Children go where their parents live; orphans take any spare bed
            foreach (var c in People.Where(p => p.IsChild && p.HomeId < 0))
            {
                var parent = GetCitizen(c.ParentId);
                var home = parent != null ? GetBuilding(parent.HomeId) : null;
                if (home != null && IsHome(home.Type) && FreeIn(home) >= (home.Type == BuildingType.BoardingHouse ? 0.5f : 1f)) { MoveInto(c, home); continue; }
                if (parent != null) continue;
                var spare = houses.Where(h => FreeIn(h) >= 1 && h.Residents.Count > 0).OrderBy(h => h.Residents.Count).FirstOrDefault()
                            ?? boards.FirstOrDefault(b => FreeIn(b) >= 0.5f);
                if (spare != null) MoveInto(c, spare);
            }
        }

        /// <summary>
        /// Households with money put by move up into an empty large house or manor: the manor goes to the richest,
        /// a large house to the best-off of the rest. Their old home frees up for someone else.
        /// </summary>
        void MoveUp()
        {
            foreach (var t in new[] { BuildingType.Manor, BuildingType.LargeHouse })
                foreach (var h in OfType(t).Where(x => x.Residents.Count == 0).ToList())
                {
                    int tier = HomeTier(t);
                    float need = MoveInAt(t), best = 0;
                    Citizen head = null; List<Citizen> unit = null;
                    foreach (var a in People)
                    {
                        if (a.IsChild || a.Role == Role.Mayor || a.Jail > 0) continue;
                        var cur = GetBuilding(a.HomeId);
                        if (cur != null && (cur.Type == BuildingType.Estate || HomeTier(cur.Type) >= tier)) continue;
                        var sp = GetCitizen(a.PartnerId);
                        if (sp != null && (sp.Role == Role.Mayor || (sp.Id < a.Id && sp.HomeId == a.HomeId && sp.Jail == 0))) continue;   // each couple once
                        var u = FamilyUnit(a);
                        if (u.Count > h.Def.Beds) continue;
                        float worth = u.Where(x => !x.IsChild).Sum(x => Savings(x));
                        if (worth < need || worth <= best) continue;
                        best = worth; head = a; unit = u;
                    }
                    if (head == null) continue;
                    foreach (var u in unit) { MoveInto(u, h); u.Happy = Math.Min(100, u.Happy + 5); }
                    h.Arrears = 0;
                    AddLog((unit.Count > 1 ? "The " + head.Family + " family" : head.Name) + " moved into the " + h.Def.Name + ".", LogKind.Good);
                }
        }

        /// <summary>Someone put out for unpaid rent is turned away for two weeks, unless the rent is now free
        /// or they have two weeks' rent in hand.</summary>
        bool MayRehouse(Citizen p)
        {
            if (Day - p.EvictedDay >= 14) return true;
            var partner = GetCitizen(p.PartnerId);
            bool family = partner != null || ChildrenOf(p).Any();
            float rent = family ? Policy.Rent : Policy.BoardRent;
            float savings = Savings(p) + Savings(partner);
            return rent <= 0.01f || savings >= rent * 2;
        }

        /// <summary>Parents, children and brothers and sisters don't marry.</summary>
        bool Related(Citizen a, Citizen b)
        {
            if (a.MotherId >= 0 && a.MotherId == b.MotherId) return true;
            if (a.FatherId >= 0 && a.FatherId == b.FatherId) return true;
            if (a.ParentId >= 0 && a.ParentId == b.ParentId) return true;
            if (a.MotherId == b.Id || a.FatherId == b.Id || a.ParentId == b.Id) return true;
            if (b.MotherId == a.Id || b.FatherId == a.Id || b.ParentId == a.Id) return true;
            var pa = GetCitizen(a.ParentId); if (pa != null && pa.PartnerId == b.Id) return true;
            var pb = GetCitizen(b.ParentId); if (pb != null && pb.PartnerId == a.Id) return true;
            return false;
        }

        /// <summary>Clears the Mayor's Estate; the family that lived there needs a new home.</summary>
        void EmptyEstate()
        {
            var e = Estate; if (e == null) return;
            foreach (var id in e.Residents.ToList()) { var r = GetCitizen(id); if (r != null && r.HomeId == e.Id) r.HomeId = -1; }
            e.Residents.Clear();
        }

        void QuitJob(Citizen p)
        {
            var b = GetBuilding(p.WorkId);
            if (b != null) b.Employees.Remove(p.Id);
            p.WorkId = -1; p.FamilyHand = false;
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
            EmptyEstate();
            MayorId = -1; MayorPlatform = Platform.None;
            ResumeOrIdle(m); m.HomeId = -1;
            if (rehouse) RehouseEveryone();
        }

        void InstallMayor(Citizen p, Platform platform)
        {
            var e = Estate; if (e == null) return;
            var old = Mayor;
            if (old != null && old != p) { UnseatMayor(old, false); AddLog(old.Name + " packs up and moves out of the Mayor's Estate."); }
            if (Employed(p.Role)) QuitJob(p);
            EmptyEstate();
            p.Role = Role.Mayor; p.WorkId = e.Id;
            // the mayor's household moves in too
            var family = new List<Citizen> { p };
            var spouse = GetCitizen(p.PartnerId); if (spouse != null) family.Add(spouse);
            family.AddRange(ChildrenOf(p).Where(c => !family.Contains(c)));
            foreach (var f in family) { LeaveHome(f); f.HomeId = e.Id; e.Residents.Add(f.Id); }
            MayorId = p.Id; MayorPlatform = platform; TermEnd = Day + Balance.TermDays;
            RehouseEveryone();
        }

        /// <summary>Takes a settler out of the town, for death or leaving.</summary>
        void RemovePerson(Citizen p)
        {
            if (p.Role == Role.Mayor) { MayorId = -1; MayorPlatform = Platform.None; LeaveHome(p); EmptyEstate(); }
            else if (p.Role != Role.Owner) QuitJob(p);
            var own = OwnedBusiness(p);
            if (own != null)
            {
                // A grown child or partner inherits the business; otherwise it goes up for sale
                var heir = GetCitizen(p.PartnerId);
                if (heir != null && !heir.IsChild && OwnedBusiness(heir) == null && heir.Role != Role.Mayor)
                {
                    if (Employed(heir.Role)) QuitJob(heir);
                    own.OwnerId = heir.Id; heir.Role = Role.Owner; heir.WorkId = own.Id; heir.Money += p.Money; p.Money = 0; MergeAccount(p, heir);
                    AddLog(heir.Name + " takes over the " + own.Def.Name + ".");
                }
                else
                {
                    if (own.Type == BuildingType.Bank) { own.OwnerId = -1; CloseBank(own); }   // no banker, no bank
                    if (own.Deposit > 0) WithdrawFor(own, own.Deposit);   // a town-run business keeps its money in the till
                    if (own.Debt > 0.01f) Repossess(own);   // the bank holds it until it sells, and is paid first
                    else { own.OwnerId = -1; own.Debt = 0; own.LenderId = -1; own.MissedPayments = 0; }
                    foreach (var id in own.Employees) { var e = GetCitizen(id); if (e != null) e.FamilyHand = false; }
                    AddLog("The " + own.Def.Name + " is up for sale. The town runs it until someone buys it." +
                           (own.Debt > 0.01f ? " The bank gets the " + Money(own.Debt) + " still owed out of the price." : ""));
                }
            }
            var partner = GetCitizen(p.PartnerId);
            if (partner != null) { partner.PartnerId = -1; partner.Money += p.Money; MergeAccount(p, partner); }
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
            bool funeral = ChurchWithPreacher != null;   // a proper funeral is a comfort
            foreach (var k in kin) k.Grief = Math.Max(k.Grief, funeral ? 45 : 60);
            foreach (var o in People) o.Happy = Clamp(o.Happy - (funeral ? 1f : 1.5f), 0, 100);
            Deaths++;
            var cem = OfType(BuildingType.Cemetery).FirstOrDefault(c => c.Graves < c.Def.Graves);
            string where;
            if (cem != null) { cem.Graves++; where = " Buried in the cemetery."; }
            else { Unburied++; where = " There is no cemetery to bury " + (p.Female ? "her" : "him") + " in."; }
            AddLog(p.Name + (p.IsChild ? ", a child of " + Math.Floor(p.Age) + "," : ", aged " + Math.Floor(p.Age) + ",") + " " + cause + "." + where, LogKind.Death);
            RehouseEveryone();
        }

        float Standing(Citizen p) { return Savings(p) + (OwnedBusiness(p) != null ? 30 : 0) + p.Happy * 0.3f + (MayorId == p.Id ? 15 : 0) + Math.Min(20, p.Age - 18) * 0.5f; }
        List<Citizen> Eligible() { return People.Where(p => !p.IsChild && p.Age >= 21 && p.Role != Role.Sheriff && p.Role != Role.Deputy && p.Role != Role.Teacher && p.Role != Role.Doctor && p.Role != Role.Preacher && p.Jail == 0 && Day - p.ArrivedDay >= 3).ToList(); }
        bool Employable(Citizen p) { return !p.IsChild && p.Role == Role.Laborer && p.Jail == 0 && p.Age < 66; }

        // =====================================================================
        // Families: aging, marriage, births, old age
        // =====================================================================
        void Marry(Citizen m, Citizen w, Building empty)
        {
            m.PartnerId = w.Id; w.PartnerId = m.Id;
            foreach (var x in new[] { m, w }) { var j = GetBuilding(x.WorkId); if (j != null && j.Type == BuildingType.Brothel && x.Role == Role.Worker) QuitJob(x); }
            var kids = ChildrenOf(w).Concat(ChildrenOf(m)).Distinct().ToList();
            string old = w.Family; w.Family = m.Family;
            // one of them already keeps a large house or a manor with room for the other: they make their home there
            int coming = 2 + kids.Count;
            var grand = new[] { m, w }.Select(x => GetBuilding(x.HomeId))
                                      .FirstOrDefault(h => h != null && HomeTier(h.Type) >= 1 && h.Residents.Count + coming - h.Residents.Count(id => id == m.Id || id == w.Id || kids.Any(k => k.Id == id)) <= h.Def.Beds);
            if (grand != null) empty = grand;
            MoveInto(m, empty); MoveInto(w, empty);
            foreach (var k in kids) if (empty.Residents.Count < empty.Def.Beds) MoveInto(k, empty);
            m.Happy = Math.Min(100, m.Happy + 15); w.Happy = Math.Min(100, w.Happy + 15);
            var church = ChurchWithPreacher;
            if (church != null)
            {
                // a church wedding: the whole town turns out
                var rev = StaffOf(church, Role.Preacher);
                m.Happy = Math.Min(100, m.Happy + 5); w.Happy = Math.Min(100, w.Happy + 5);
                foreach (var o in People) if (o != m && o != w) o.Happy = Math.Min(100, o.Happy + 1f);
                AddLog(m.First + " " + m.Family + " and " + w.First + " " + old + " were married at the church by " + rev.Name + (grand != null ? ", and made their home in the " + grand.Def.Name + "." : ", and moved into a cottage of their own."), LogKind.Birth);
                return;
            }
            AddLog(m.First + " " + m.Family + " and " + w.First + " " + old + " were married" + (grand != null ? " and made their home in the " + grand.Def.Name + "." : " and moved into a cottage of their own."), LogKind.Birth);
        }

        void FamilyLife()
        {
            float yearPerDay = 1f / Balance.DaysPerYearF;
            float griefFades = ChurchWithPreacher != null ? 2f : 1.5f;   // a minister to talk to
            float wear = Balance.ClothesWear * (Season == 3 ? 1.5f : Season == 2 ? 1.2f : 1f);
            foreach (var p in People.ToList())
            {
                bool wasChild = p.IsChild;
                p.Age += yearPerDay;
                if (wasChild && !p.IsChild)
                {
                    p.Role = Role.Laborer;
                    AddLog(p.Name + " has come of age and is looking for work.", LogKind.Good);
                }
                p.Grief = Math.Max(0, p.Grief - griefFades);
                if (p.Clothes > Balance.ClothesMended) p.Clothes = Math.Max(Balance.ClothesMended, p.Clothes - wear * (p.IsChild ? 1.3f : 1f));   // children grow out of theirs
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
                    var w = women.Where(x => !Related(m, x)).OrderBy(x => Math.Abs(x.Age - m.Age)).FirstOrDefault();
                    if (w != null) Marry(m, w, empty);
                }
            }

            // Births: families hold off when the town is short of food
            float foodPerHead = People.Count == 0 ? 0 : TotalFood() / People.Count;
            if (foodPerHead >= 8f)
            foreach (var w in People.Where(p => p.Female && !p.IsChild && p.Age >= 18 && p.Age <= 44 && p.PartnerId >= 0).ToList())
            {
                var h = GetBuilding(w.HomeId);
                var m = GetCitizen(w.PartnerId);
                if (h == null || m == null || m.HomeId != w.HomeId || h.Residents.Count >= h.Def.Beds || h.Type == BuildingType.BoardingHouse) continue;
                if (w.Fed < 0.9f || w.Health < 60) continue;
                if (!Chance(Balance.BirthChancePerDay)) continue;
                var baby = AddPerson(Chance(0.5), m.Family, 0, 0);
                baby.ParentId = baby.MotherId = w.Id; baby.FatherId = m.Id; baby.Health = 100;
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
            var founder = GetCitizen(FounderBuilderId);
            if (b.Type == BuildingType.BuilderYard && founder != null && !founder.IsChild && founder.Jail == 0 && founder.Role != Role.Mayor && OwnedBusiness(founder) == null)
            {
                // the builder who came with the wagon brought his own tools: the first yard is his
                if (Employed(founder.Role)) QuitJob(founder);
                b.OwnerId = founder.Id; founder.Role = Role.Owner; founder.WorkId = b.Id; b.Debt = 0;
                b.Cash += 40f + founder.Money * 0.3f; founder.Money *= 0.7f; b.LossWeeks = 0; b.BuiltDay = Day;
                FounderBuilderId = -1;
                AddLog(founder.Name + " opened his Builder's Yard. From now on the town pays him by the job.", LogKind.Good);
                return;
            }
            // Families buy businesses with their own savings, in full, or with a bank loan for the rest.
            // Until someone can, the town runs it.
            float price = DeedPrice(b);
            var bank = b.Type == BuildingType.Bank ? null : Banks.Where(k => IsOpen(k)).OrderByDescending(k => Lendable(k)).FirstOrDefault();
            Citizen buyer = null; float best = 0, loan = 0;
            foreach (var p in People)
            {
                if (p.IsChild || p.Jail > 0 || p.Age >= 60 || (p.Role != Role.Laborer && p.Role != Role.Worker) || p.FamilyHand) continue;
                if (FamilyBusiness(p) != null) continue;   // one business to a family
                if (SkilledWork(b.Type) && !Schooled(p)) continue;   // a store or bank needs someone who can keep the books
                var sp = GetCitizen(p.PartnerId);
                float savings = Savings(p) + Savings(sp);
                float need = 0;
                if (savings - 5f >= price) need = 0;
                else if (bank != null && savings - 5f >= price * Balance.MinDown && Lendable(bank) >= price - (savings - 5f)) need = price - (savings - 5f);
                else continue;
                if (savings > best) { best = savings; buyer = p; loan = need; }
            }
            if (buyer == null) return;
            if (buyer.Role != Role.Laborer) QuitJob(buyer);
            var partner = GetCitizen(buyer.PartnerId);
            var payers = new List<Citizen> { buyer }; if (partner != null) payers.Add(partner);
            Collect(payers, price - loan);
            // a business the bank took back: the bank is paid what it's still owed out of the price, the town gets the rest
            float toBank = 0;
            var holder = GetBuilding(b.LenderId);
            if (b.Debt > 0.01f && holder != null && holder.OwnerId >= 0)
            {
                toBank = Math.Min(price, b.Debt);
                holder.Cash += toBank;
                if (b.Debt - toBank > 0.01f) AddLog("The bank got back " + Money(toBank) + " of the " + Money(b.Debt) + " owed on the " + d.Name + " and wrote off the rest.", LogKind.Bad);
            }
            if (loan > 0) { bank.Cash -= loan; b.Debt = loan; b.LenderId = bank.Id; b.MissedPayments = 0; }
            else { b.Debt = 0; b.LenderId = -1; }
            Treasury += price - toBank; LedgerThisWeek.Deeds += price - toBank;
            // the town takes back what's in its till; the family puts some savings in, and the town a little start-up money
            Treasury += Math.Max(0, b.Cash); LedgerThisWeek.TownTakings += Math.Max(0, b.Cash); b.Cash = 0;
            float capital = Collect(payers, payers.Sum(x => Math.Max(0, Savings(x))) * (b.Type == BuildingType.Bank ? 0.8f : 0.3f));
            float grant = Math.Min(20f, Math.Max(0f, Treasury)); Treasury -= grant; LedgerThisWeek.Grants += grant;
            b.Cash = capital + grant; b.OwnerId = buyer.Id; buyer.Role = Role.Owner; buyer.WorkId = b.Id; b.LossWeeks = 0; b.BuiltDay = Day;
            foreach (var id in b.Employees) { var e = GetCitizen(id); if (e != null) e.FamilyHand = false; }
            var banker = bank != null ? GetCitizen(bank.OwnerId) : null;
            AddLog(buyer.Name + " bought the " + d.Name + " from the town for " + Money(price) +
                   (loan > 0 ? ", borrowing " + Money(loan) + " from " + (banker != null ? banker.Name + "'s bank." : "the bank.") : ", paid in full."), LogKind.Good);
        }

                struct Vacancy { public Building B; public float Wage; public Role Role; }

        void Hire(Citizen p, Vacancy v)
        {
            p.Role = v.Role;
            if (v.Role == Role.Builder) { p.WorkId = -1; AddLog(p.Name + " joined the town's building crew at " + Money(v.Wage) + "/day."); return; }
            p.WorkId = v.B.Id; v.B.Employees.Add(p.Id);
            string label = v.Role == Role.Worker ? "a hand at the " + v.B.Def.Name : v.Role == Role.Sheriff ? "the new Sheriff" : v.Role == Role.Teacher ? "the schoolteacher"
                         : v.Role == Role.Doctor ? "the town doctor" : v.Role == Role.Preacher ? "the preacher" : "a deputy";
            if (v.Role == Role.Preacher)
            {
                AddLog(p.Name + " took up the pulpit at the church. The preacher lives on the Sunday collection.", LogKind.Good);
                v.B.VacantDays = 0; return;
            }
            var boss = GetCitizen(v.B.OwnerId);
            AddLog(p.Name + " was hired as " + label + (v.Role == Role.Worker ? (boss != null ? " by " + boss.Name : " by the town") : "") + " at " + Money(v.Wage) + "/day.");
            v.B.VacantDays = 0;
        }

        /// <summary>Whether a settler can take a job: teaching and storekeeping need schooling, doctoring a doctor.</summary>
        static bool CanDo(Citizen p, Vacancy v)
        {
            if (v.Role == Role.Doctor) return p.Physician;
            if (v.Role == Role.Teacher || v.Role == Role.Preacher) return Schooled(p);
            if (v.Role == Role.Worker && v.B != null && v.B.Type == BuildingType.Brothel) return BrothelWorker(p);
            if (v.Role == Role.Worker && v.B != null && SkilledWork(v.B.Type)) return Schooled(p);
            return true;
        }

        /// <summary>When nobody in town can take the post, the town sends back east, and someone comes once there's a bed for them.</summary>
        void SendForStaff(Building b, Role role)
        {
            b.VacantDays++;
            if (b.VacantDays < Balance.SendForStaffDays || Treasury < 40f) return;
            bool married = role == Role.Doctor && Chance(0.6);
            if (FreeBeds < (married ? 2 : 1))
            {
                if (b.VacantDays == Balance.SendForStaffDays)
                    AddLog((role == Role.Doctor ? "A doctor" : role == Role.Preacher ? "A preacher" : "A schoolteacher") + " back east would come out, but there's no bed in town for them.", LogKind.Bad);
                return;
            }
            string fam = Pick(Defs.LastNames);
            Citizen p;
            if (role == Role.Doctor)
            {
                p = AddPerson(Chance(0.08), fam, Rnd(27, 50), (float)Math.Round(Rnd(25, 60)));
                p.Physician = true; p.Schooling = 1f;
                Citizen sp = null;
                if (married) { sp = AddPerson(!p.Female, fam, Rnd(24, 46), (float)Math.Round(Rnd(5, 20))); sp.PartnerId = p.Id; p.PartnerId = sp.Id; sp.Schooling = Math.Max(sp.Schooling, 0.5f); }
                AddLog("Dr. " + p.Name + " came out from back east" + (sp != null ? " with " + (p.Female ? "her husband" : "his wife") + " " + sp.First : "") +
                       " to practise here. The town pays " + (p.Female ? "her " : "him ") + Money(Balance.SalaryDoctor) + " a day.", LogKind.Good);
            }
            else if (role == Role.Preacher)
            {
                p = AddPerson(false, fam, Rnd(26, 50), (float)Math.Round(Rnd(5, 20)));
                p.Schooling = 1f;
                Citizen sp = null;
                if (FreeBeds >= 1 && Chance(0.5)) { sp = AddPerson(true, fam, Rnd(22, 46), (float)Math.Round(Rnd(2, 10))); sp.PartnerId = p.Id; p.PartnerId = sp.Id; sp.Schooling = Math.Max(sp.Schooling, 0.5f); }
                AddLog("The Reverend " + p.Name + " came out from back east" + (sp != null ? " with his wife " + sp.First : "") + " to preach. He lives on the Sunday collection.", LogKind.Good);
            }
            else
            {
                p = AddPerson(Chance(0.7), fam, Rnd(18, 30), (float)Math.Round(Rnd(8, 25)));
                p.Schooling = 1f;
                AddLog(p.Name + " came out from back east to teach school, for " + Money(Balance.SalaryTeacher) + " a day.", LogKind.Good);
            }
            p.Role = role; p.WorkId = b.Id; b.Employees.Add(p.Id); b.VacantDays = 0;
            RehouseEveryone();
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

            // The owner's family works the business with them: spouse and grown children at home, no wages
            foreach (var b in Businesses.ToList())
            {
                var o = GetCitizen(b.OwnerId);
                if (o == null || o.WorkId != b.Id || b.Employees.Count >= b.Def.MaxEmployees || PaysByShare(b.Type)) continue;
                var sp = GetCitizen(o.PartnerId);
                var kin = new List<Citizen>(); if (sp != null) kin.Add(sp);
                kin.AddRange(People.Where(c => !c.IsChild && c.PartnerId < 0 && (c.MotherId == o.Id || c.FatherId == o.Id || (sp != null && (c.MotherId == sp.Id || c.FatherId == sp.Id)))));
                foreach (var k in kin)
                {
                    if (b.Employees.Count >= b.Def.MaxEmployees) break;
                    if (k.Role != Role.Laborer || k.Jail > 0 || k.Age >= 66 || k.HomeId != o.HomeId) continue;
                    k.Role = Role.Worker; k.WorkId = b.Id; k.FamilyHand = true; b.Employees.Add(k.Id);
                    AddLog(k.Name + " works the family " + b.Def.Name.ToLower() + " alongside " + o.First + ".", LogKind.Good);
                }
            }

            var vac = new List<Vacancy>();
            foreach (var b in Businesses)
            {
                if (b.Type == BuildingType.Bank && b.OwnerId < 0) continue;   // a bank needs a banker; the town doesn't lend
                int want = Math.Min(b.Def.MaxEmployees, PaysByShare(b.Type) ? Math.Max(1, b.Desired) : b.Desired);
                // nobody is taken on without a week's wages in hand for every hired hand (the town's money backs its own)
                float purse = b.Cash + b.Deposit + (b.OwnerId < 0 ? Math.Max(0, Treasury) : 0);
                int hired = b.Employees.Count(id => { var e = GetCitizen(id); return e != null && !e.FamilyHand; });
                int affordable = PaysByShare(b.Type) ? b.Def.MaxEmployees : b.Wage > 0 ? (int)(purse / (b.Wage * 7f)) : 0;
                for (int i = b.Employees.Count; i < want && hired < affordable; i++, hired++) vac.Add(new Vacancy { B = b, Wage = b.Wage, Role = Role.Worker });
            }
            if (off != null && Treasury > 15)
            {
                var staff = off.Employees.Select(GetCitizen).Where(p => p != null).ToList();
                if (!staff.Any(p => p.Role == Role.Sheriff)) vac.Add(new Vacancy { B = off, Wage = Balance.SalarySheriff, Role = Role.Sheriff });
                for (int i = staff.Count(p => p.Role == Role.Deputy); i < Policy.Deputies; i++) vac.Add(new Vacancy { B = off, Wage = Balance.SalaryDeputy, Role = Role.Deputy });
            }
            // the schoolteacher and the doctor are on the town payroll
            if (Treasury > 15)
            {
                foreach (var s in OfType(BuildingType.School)) if (StaffOf(s, Role.Teacher) == null) vac.Add(new Vacancy { B = s, Wage = Balance.SalaryTeacher, Role = Role.Teacher });
                foreach (var o in OfType(BuildingType.DoctorOffice)) if (StaffOf(o, Role.Doctor) == null) vac.Add(new Vacancy { B = o, Wage = Balance.SalaryDoctor, Role = Role.Doctor });
                foreach (var c in OfType(BuildingType.Church)) if (StaffOf(c, Role.Preacher) == null) vac.Add(new Vacancy { B = c, Wage = Balance.PlateTopUp / 7f, Role = Role.Preacher });
            }
            // when food is short, settlers take food jobs first
            bool hungry = People.Count > 0 && TotalFood() / People.Count < 30f;   // less than a month's food put by
            int homes = Buildings.Count(h => h.Built && IsHome(h.Type) && h.Residents.Count > 0);
            bool cold = TotalFirewood() < homes * 8f;
            vac = vac.OrderByDescending(v => v.Wage + (v.B == null ? 0f :
                        (hungry && FoodJob(v.B.Type) ? 1f : 0f) +
                        (cold && (v.B.Type == BuildingType.Woodcutter || v.B.Type == BuildingType.LumberCamp) ? 1f : 0f))).ToList();
            var idle = Shuffle(People.Where(Employable).ToList());
            int filled = 0;
            var open = new List<Vacancy>();
            foreach (var v in vac)
            {
                var p = idle.FirstOrDefault(x => CanDo(x, v));
                if (p == null) { open.Add(v); continue; }
                idle.Remove(p); Hire(p, v); filled++;
            }
            foreach (var v in open)
                if ((v.Role == Role.Teacher || v.Role == Role.Doctor || v.Role == Role.Preacher) && v.B.Employees.Count == 0) SendForStaff(v.B, v.Role);
            // a brothel that no woman in town will work at draws women up the road from the camps and river towns, once there's a bed
            foreach (var b in OfType(BuildingType.Brothel).Where(x => x.Built).ToList())
            {
                int need = Math.Min(b.Def.MaxEmployees, Math.Max(1, b.Desired));
                if (b.Employees.Count >= need) { b.VacantDays = 0; continue; }
                if (++b.VacantDays < Balance.SendForStaffDays || FreeBeds < 1) continue;
                var w = AddPerson(true, Pick(Defs.LastNames), Rnd(19, 32), (float)Math.Round(Rnd(3, 12)));
                w.Role = Role.Worker; w.WorkId = b.Id; b.Employees.Add(w.Id); b.VacantDays = 0;
                AddLog(w.Name + " came up the road to work at the Brothel.", LogKind.Info);
                RehouseEveryone();
            }
            // a hired hand leaves for a job that pays clearly better (or for food work when food is short) - one a day
            foreach (var v in open)
            {
                if (v.Role != Role.Worker || v.B == null) continue;
                bool urgent = hungry && FoodJob(v.B.Type);
                // family hands only leave the family business for urgent food work, or when it isn't paying its way
                var mover = People.Where(p => p.Role == Role.Worker && p.Jail == 0 && p.WorkId != v.B.Id && CanDo(p, v) && !AtBrothel(p) &&
                                              (!p.FamilyHand || urgent || Losing(GetBuilding(p.WorkId))))
                                  .Select(p => new { p, w = GetBuilding(p.WorkId) })
                                  .Where(x => x.w != null && !(urgent && FoodJob(x.w.Type)) && (urgent || x.p.FamilyHand || x.w.Wage + 0.4f < v.Wage))
                                  .OrderBy(x => x.w.Wage).FirstOrDefault();
                if (mover == null) continue;
                var from = mover.w; bool fam = mover.p.FamilyHand; QuitJob(mover.p); Hire(mover.p, v);
                AddLog(mover.p.Name + " left the " + from.Def.Name + " for " + (urgent ? "the " + v.B.Def.Name + ": the town needs food." : "better pay at the " + v.B.Def.Name + "."));
                break;
            }
        }

        /// <summary>Women at the brothel aren't lured away to other work; they leave it by marrying or when the house lets them go.</summary>
        bool AtBrothel(Citizen p) { var w = GetBuilding(p.WorkId); return w != null && w.Type == BuildingType.Brothel; }

        /// <summary>A business that isn't paying its way. A farm earns at harvest, and a gatherer's hut is idle in winter,
        /// so losses out of season don't count.</summary>
        bool Losing(Building b)
        {
            if (b == null || !b.Last.Valid || b.Last.Profit > 0) return false;
            if (b.Type == BuildingType.Farm && Season != 2) return false;
            if (b.Type == BuildingType.Gatherer && Season == 3) return false;
            return true;
        }

        public void SetMinWage(float v)
        {
            Policy.MinWage = v;
            foreach (var b in Businesses) if (!PaysByShare(b.Type)) b.Wage = Math.Max(v, b.Def.BaseWage);
        }
    }
}
