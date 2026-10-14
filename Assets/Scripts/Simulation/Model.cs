using System;
using System.Collections.Generic;

namespace FrontierLedger.Simulation
{
    [Serializable]
    public class PeriodStats
    {
        public float Revenue, Costs, Sold, Made;
        public void Clear() { Revenue = Costs = Sold = Made = 0f; }
    }

    [Serializable]
    public class WeekReport
    {
        public bool Valid;
        public float Revenue, Costs, Profit, Tax, OwnerDraw, Sold, Made;
    }

    [Serializable]
    public class DayStats
    {
        public float Sold, Made, Bought, Exported;
        public void Clear() { Sold = Made = Bought = Exported = 0f; }
    }

    [Serializable]
    public class Building
    {
        public int Id;
        public BuildingType Type;
        public int X, Y;
        public int BuiltDay;

        // Houses and the estate
        public List<int> Residents = new List<int>();
        public float GardenToday;

        // Businesses and the sheriff's office
        public List<int> Employees = new List<int>();
        public int OwnerId = -1;
        public float Cash, Food, Tools, Price, Wage;
        public int Desired;
        public int LossWeeks;
        public PeriodStats Week = new PeriodStats();
        public WeekReport Last = new WeekReport();
        public DayStats Today = new DayStats();

        public BuildingDef Def { get { return Defs.Get(Type); } }
        public bool Contains(int tx, int ty)
        {
            var d = Def;
            return tx >= X && tx < X + d.W && ty >= Y && ty < Y + d.H;
        }
    }

    [Serializable]
    public class Citizen
    {
        public int Id;
        public string Name;
        public float Money;
        public int HomeId = -1;
        public int WorkId = -1;
        public Role Role;
        public float Fed = 1f, FedToday;
        public int Hungry;
        public float Happy;
        public int Jail;
        public float Seed;          // 0..1, drives looks and voting quirks
        public int Homeless;
        public int Unpaid;
        public int RobbedDay = -999;
        public int ArrivedDay;
        public int SaloonToday = -1;
    }

    [Serializable]
    public class Candidate
    {
        public int CitizenId;
        public Platform Platform;
    }

    [Serializable]
    public class Campaign
    {
        public bool Active;
        public int VoteDay;
        public List<Candidate> Candidates = new List<Candidate>();
    }

    [Serializable]
    public class CandidateResult
    {
        public string Name;
        public Platform Platform;
        public int Votes;
    }

    [Serializable]
    public class ElectionResult
    {
        public bool Valid;
        public int Day, TotalVoters;
        public List<CandidateResult> Results = new List<CandidateResult>();
    }

    [Serializable]
    public class TownLedger
    {
        public float IncomeTax, HeadTax, BusinessTax, Deeds, Claims, Refunds;   // income
        public float Salaries, Upkeep, Grants, Construction;                    // spending
        public float Income { get { return IncomeTax + HeadTax + BusinessTax + Deeds + Claims + Refunds; } }
        public float Spending { get { return Salaries + Upkeep + Grants + Construction; } }
        public float Net { get { return Income - Spending; } }
    }

    [Serializable]
    public class TownPolicy
    {
        public float IncomeTax = 0.10f;    // share of every wage and salary
        public float BusinessTax = 0.15f;  // share of weekly profit
        public float HeadTax = 1f;         // dollars per settler per week
        public float MinWage = 2f;
        public int Deputies = 1;
        public bool ElectionsOn = true;
    }

    [Serializable]
    public class LogEntry
    {
        public int Day;
        public string Message;
        public LogKind Kind;
    }

    [Serializable]
    public class MarketReport
    {
        public float Garden, Demand, Bought, Shortfall;
    }

    [Serializable]
    public class HistoryPoint
    {
        public int Day;
        public float Treasury;
        public int Population;
    }
}
