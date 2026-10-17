using System;
using System.Collections.Generic;

namespace FrontierLedger.Simulation
{
    [Serializable]
    public class PeriodStats
    {
        public float Revenue, Costs, Sold, Made;
        public float Drawn;   // what the owner took out during the week to keep the family fed and warm
        public void Clear() { Revenue = Costs = Sold = Made = Drawn = 0f; }
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

        // Construction
        public bool Built = true;
        public float Progress;                        // builder-days done
        public float[] Delivered = new float[Balance.GoodCount];   // materials on site
        public int ContractorId = -1;                 // the builder's yard doing the job
        public float ContractPrice, ContractPaid;     // what the town agreed to pay, and has paid so far
        public float BuildCost;                       // what it cost to put up, for pricing the deed

        // Goods held here: a business's stock, a house's pantry and woodpile, or town storage
        public float[] Stock = new float[Balance.GoodCount];       // a house keeps its furniture here too
        public float[] Price = new float[Balance.GoodCount];       // retail prices (stores, stalls, the yard's furniture)

        // Upkeep
        public float Condition = 100f;                // wears down; a builder's yard repairs it
        public int RepairBy = -1;                     // yard booked to repair it
        public bool RepairPaid;

        // Houses and the estate
        public List<int> Residents = new List<int>();
        public float GardenToday;
        public bool ColdToday;
        public float Arrears;                         // unpaid rent

        // Businesses and the sheriff's office
        public List<int> Employees = new List<int>();
        public int OwnerId = -1;
        public float Cash, Wage;
        public int Desired;
        public int LossWeeks;
        public float Debt;                            // what the owner still owes for the deed
        public int LenderId = -1;                     // the bank it's owed to (-1: the town, from older saves)
        public int MissedPayments;
        public float TownOwes;                        // contract money the treasury couldn't pay yet (yards)
        public int Backlog;                           // crew a yard could use on its jobs today
        public int VacantDays;                        // a school or doctor's office waiting for someone to take the post
        public float Deposit;                         // an owned business's money in the bank
        public int BankId = -1;                       // the bank holding it
        public int Attendance;                        // a church's congregation last Sunday
        public float Collection;                      // and what went in the plate
        public PeriodStats Week = new PeriodStats();
        public WeekReport Last = new WeekReport();
        public DayStats Today = new DayStats();

        // Fields
        public float CropGrowth;                      // 0..1 through spring and summer
        public float CropLeft;                        // food still standing in autumn
        // Lumber camp and quarry: progress on the current tree or boulder
        public float ChopWork;
        public int TargetTile = -1;
        // Cemetery
        public int Graves;

        public BuildingDef Def { get { return Defs.Get(Type); } }
        public bool Contains(int tx, int ty)
        {
            var d = Def;
            return tx >= X && tx < X + d.W && ty >= Y && ty < Y + d.H;
        }
        public float MaterialsFraction
        {
            get
            {
                var d = Def; float need = d.Logs + d.Stone;
                if (need <= 0) return 1f;
                return (Delivered[(int)Good.Logs] + Delivered[(int)Good.Stone]) / need;
            }
        }
        public float BuildFraction { get { var d = Def; return d.Work <= 0 ? 1f : Math.Min(1f, Progress / d.Work); } }
    }

    [Serializable]
    public class Citizen
    {
        public int Id;
        public string First, Family;
        public bool Female;
        public float Age;                // years
        public int PartnerId = -1;
        public int ParentId = -1;        // who looks after a child (the mother, or the father if she dies)
        public int MotherId = -1, FatherId = -1;   // birth parents, never change
        public float RentOwed;           // a boarder's unpaid rent
        public float Money;
        public int HomeId = -1;
        public int WorkId = -1;
        public Role Role;
        public float Fed = 1f, FedToday;
        public bool Warm = true;
        public float Health = 100f;
        public int Hungry;
        public float Happy;
        public float Grief;              // fades after a death in the family
        public int Jail;
        public float Seed;               // 0..1, drives looks and voting quirks
        public int Homeless;
        public int Unpaid;
        public int RobbedDay = -999;
        public int ArrivedDay;
        public int SaloonToday = -1;
        public int TaskId = -1;          // building a laborer worked at today (site or field)
        public Chore Chore;              // and what they did there
        public int CharityDays;
        public int ColdDays;
        public int EvictedDay = -999;
        public bool ByFire;              // sleeping rough but huddled by the town fire with firewood to burn
        public bool FamilyHand;          // works the family business for no wage
        public float Schooling;          // 0..1: how much school they've had (half is enough to read, write and reckon)
        public int Sick;                 // days left of an illness or injury
        public float Illness;            // health it costs each day (1.5 mild, 4.5 serious, 14 grave)
        public int IllnessKind;          // index into Defs.Illnesses
        public bool SeenToday;           // the doctor saw them today
        public bool Physician;           // trained as a doctor
        public float Deposit;            // savings in the bank (Money is what they keep at home)
        public int BankId = -1;          // the bank holding them
        public float Clothes = 1f;       // the clothes on their back: 1 a new set, wearing down to rags
        public int BathedDay = -999;     // last bath and shave at the barber's
        public int ChurchDay = -999;     // last Sunday at church
        public bool AteMeat, AteBread;   // what was on the table today
        public float Earned;             // money coming in this week (wages after tax, shares, an owner's take, interest)
        public float EarnedLast, SpentLast;   // and last week: what came in, and what went out
        public float WorthStart;         // home money plus savings when the week began
        public string Name { get { return First + " " + Family; } }
        public bool IsChild { get { return Age < Balance.AdultAge; } }
    }

    /// <summary>Goods and settlers the town has sent for from back east, coming by freight caravan.</summary>
    [Serializable]
    public class FreightOrder
    {
        public int Id, OrderedDay, ArriveDay;
        public float[] Goods = new float[Balance.GoodCount];
        public int Laborers, Families;
        public float Cost;
        public bool Delivered;
    }

    /// <summary>A trip someone makes today: to a shop to buy, or carrying goods to a buyer. Not saved; for the map.</summary>
    public sealed class Errand
    {
        public int PersonId, ToId, FromId = -1;
        public Good Good;
        public bool Delivery;        // carrying goods there (selling) rather than coming back with them (buying)
        public bool Service;         // going for a service (a bath and a shave), not goods
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
        public float IncomeTax, HeadTax, BusinessTax, Deeds, Claims, Refunds, StoreSales, Exports, Rent, TownTakings;   // income
        public float Salaries, Builders, Relief, Upkeep, Grants, Materials, Construction, Contracts, TownTills, Imports; // spending
        public float CharityGoods;                                                                         // value handed out, not money
        public float Income { get { return IncomeTax + HeadTax + BusinessTax + Deeds + Claims + Refunds + StoreSales + Exports + Rent + TownTakings; } }
        public float Spending { get { return Salaries + Builders + Relief + Upkeep + Grants + Materials + Construction + Contracts + TownTills + Imports; } }
        public float Net { get { return Income - Spending; } }
    }

    [Serializable]
    public class TownPolicy
    {
        public float IncomeTax = 0.12f;    // share of every wage and salary
        public float BusinessTax = 0.15f;  // share of weekly profit
        public float HeadTax = 1f;         // dollars per adult per week
        public float MinWage = 2f;
        public int Deputies = 1;
        public int Builders = 5;           // without a builder's yard: most laborers the town hires each day
        public float BuilderWage = 2f;     // and their day wage
        public float ContractRate = 3.5f;  // paid to a builder's yard per day of work a job takes
        public float Rent = 2.5f;          // per cottage per week
        public float BoardRent = 0.75f;    // per boarder per week
        public float LargeRent = 5f;       // per large house per week
        public float ManorRent = 12f;      // per manor house per week
        public float ReliefWage = 0.6f;    // paid to laborers on days without work
        public bool Charity = true;        // the storehouse feeds and warms those who can't pay
        public bool ElectionsOn = true;
        public bool StorehouseSells = true;
        public bool Births = true;         // couples have babies
        public bool Newcomers = true;      // drifters and families come up the road on their own
        public float[] StorehousePrice = { 1.1f, 0.7f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 9f, 0f, 0f };   // clothes ordered from back east sell when no tailor has any
        public float[] Reserve = { 150f, 60f, 40f, 20f, 10f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f };   // the town buys up to these levels
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
        public float HeatDemand, HeatShortfall;
        public int ColdHomes;
    }

    [Serializable]
    public class HistoryPoint
    {
        public int Day;
        public float Treasury, Food, Firewood;
        public int Population;
    }
}
