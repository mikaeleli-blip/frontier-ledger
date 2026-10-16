using System;
using System.Collections.Generic;
using System.Linq;

namespace FrontierLedger.Simulation
{
    public partial class Town
    {
        float PrefScore(Citizen v, Candidate c)
        {
            var cp = GetCitizen(c.CitizenId); if (cp == null) return -9;
            if (v.Id == c.CitizenId) return 99;
            if (v.PartnerId == c.CitizenId) return 50;
            float s = Noise.Hash((int)(v.Seed * 100000), c.CitizenId * 7, Campaign.VoteDay) * 0.8f;
            var own = OwnedBusiness(cp); if (own != null && v.WorkId == own.Id) s += 0.6f;
            if (v.Family == cp.Family) s += 0.5f;
            var vb = GetBuilding(v.WorkId);
            BuildingType? t = vb != null ? vb.Type : (BuildingType?)null;
            bool foodJob = t == BuildingType.Farm || t == BuildingType.Gatherer || t == BuildingType.Fishery;
            bool tradeJob = t == BuildingType.Store || t == BuildingType.Stall || t == BuildingType.Smithy || t == BuildingType.Saloon
                            || t == BuildingType.LumberCamp || t == BuildingType.Woodcutter || t == BuildingType.Quarry;
            if (c.Platform == Platform.Farm && (foodJob || Market.Shortfall > 1)) s += 0.5f;
            if (c.Platform == Platform.Trade && tradeJob) s += 0.45f;
            if (c.Platform == Platform.Law) s += Math.Min(0.8f, CrimesLast * 0.12f + CrimesWeek * 0.1f) + (Day - v.RobbedDay < 20 ? 0.5f : 0);
            if (c.Platform == Platform.People && (v.Role == Role.Laborer || Savings(v) < 10 || v.Happy < 40 || v.HomeId < 0)) s += 0.55f;
            if (MayorId == c.CitizenId) s += (AverageHappiness - 50) / 80;
            return s;
        }

        /// <summary>Who this settler would vote for today, or null outside a campaign or for children.</summary>
        public Candidate VoteOf(Citizen v)
        {
            if (!Campaign.Active || v.IsChild) return null;
            Candidate best = null; float bs = float.MinValue;
            foreach (var c in Campaign.Candidates) { float s = PrefScore(v, c); if (s > bs) { bs = s; best = c; } }
            return best;
        }

        public Dictionary<int, int> Poll()
        {
            var t = new Dictionary<int, int>();
            foreach (var c in Campaign.Candidates) t[c.CitizenId] = 0;
            foreach (var v in Adults) { var c = VoteOf(v); if (c != null && t.ContainsKey(c.CitizenId)) t[c.CitizenId]++; }
            return t;
        }

        void StartCampaign(int days)
        {
            var el = Eligible().OrderByDescending(Standing).ToList();
            // At most one candidate per household
            var picked = new List<Citizen>();
            foreach (var p in el) { if (picked.Count >= 3) break; if (picked.Any(q => q.PartnerId == p.Id)) continue; picked.Add(p); }
            if (picked.Count == 0) return;
            var plats = Shuffle(new List<Platform> { Platform.Law, Platform.Farm, Platform.Trade, Platform.People });
            var inc = Mayor;
            var cands = picked.Select(p => new Candidate { CitizenId = p.Id, Platform = p == inc && MayorPlatform != Platform.None ? MayorPlatform : Platform.None }).ToList();
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
            var cands = Campaign.Candidates.Where(c => GetCitizen(c.CitizenId) != null)
                .OrderByDescending(c => t[c.CitizenId]).ThenByDescending(c => Standing(GetCitizen(c.CitizenId))).ToList();
            if (cands.Count == 0) { Campaign = new Campaign(); return; }
            var w = cands[0]; var wp = GetCitizen(w.CitizenId);
            int voters = Adults.Count();
            LastElection = new ElectionResult
            {
                Valid = true, Day = Day, TotalVoters = voters,
                Results = cands.Select(c => new CandidateResult { Name = GetCitizen(c.CitizenId).Name, Platform = c.Platform, Votes = t[c.CitizenId] }).ToList()
            };
            bool wasInc = MayorId == w.CitizenId;
            Campaign = new Campaign();
            InstallMayor(wp, w.Platform);
            AddLog("Election day! " + wp.Name + (wasInc ? " is re-elected" : " wins") + " with " + t[w.CitizenId] + " of " + voters +
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
