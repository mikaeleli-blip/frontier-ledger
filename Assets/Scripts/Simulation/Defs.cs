using System;
using System.Collections.Generic;

namespace FrontierLedger.Simulation
{
    public enum BuildingType { Road, House, Farm, Store, Stall, Smithy, Saloon, Sheriff, Estate }
    public enum Role { Unemployed, Worker, Owner, Sheriff, Deputy, Mayor }
    public enum Platform { None = -1, Law, Farm, Trade, People }
    public enum LogKind { Info, Good, Bad, Vote, Build }

    /// <summary>Terrain tile kinds. Values are stored in Town.Terrain.</summary>
    public static class Tile
    {
        public const byte Grass = 0, Water = 1, Tree = 2, Rock = 3, Hill = 4, OutOfBounds = 255;
    }

    public sealed class BuildingDef
    {
        public BuildingType Type;
        public string Name;
        public int W, H, Cost;
        public bool IsBusiness, IsRetail, IsPublic, Unique;
        public int MaxEmployees, Beds, ShelfCapacity, DesiredStart;
        public float BaseWage, StartPrice;
        /// <summary>Hex color used for the build palette and minimap.</summary>
        public string Color;
    }

    public sealed class PlatformDef
    {
        public Platform Id;
        public string Name, Description;
    }

    /// <summary>All tunable game numbers live here so balancing happens in one place.</summary>
    public static class Balance
    {
        public const int Cols = 64, Rows = 44;
        public const int DaysPerSeason = 28, DaysPerYear = 112, StartYear = 1852;

        public static readonly float[] GardenBySeason = { 1f, 1.2f, 0.85f, 0.25f };
        public static readonly float[] FarmBySeason = { 1f, 1.25f, 1.05f, 0.6f };
        public const float GardenBase = 1.6f;      // food per house per day before season factor
        public const float GardenPerPersonCap = 0.6f;
        public const float BarnCapacity = 90f;
        public const float Wholesale = 1.3f;       // farm -> shop price per food
        public const float ExportFood = 0.7f;      // freight wagon pays per food
        public const float ExportTools = 3.0f;
        public const float ToolPrice = 4f;
        public const float DrinkPrice = 1.5f;
        public const float SalaryMayor = 4f, SalarySheriff = 4f, SalaryDeputy = 3f;
        public const float ClaimFee = 5f;
        public const float ClearTree = 3f, ClearRock = 10f;
        public const float StartingTreasury = 600f;
        public const int TermDays = 28;
    }

    public static class Defs
    {
        static readonly Dictionary<BuildingType, BuildingDef> map = new Dictionary<BuildingType, BuildingDef>();
        public static readonly BuildingType[] Buildable =
        {
            BuildingType.Road, BuildingType.House, BuildingType.Farm, BuildingType.Store, BuildingType.Stall,
            BuildingType.Smithy, BuildingType.Saloon, BuildingType.Sheriff, BuildingType.Estate
        };

        public static readonly PlatformDef[] Platforms =
        {
            new PlatformDef { Id = Platform.Law, Name = "Law & Order", Description = "Half as much theft, and the sheriff catches more thieves." },
            new PlatformDef { Id = Platform.Farm, Name = "Farmers' Champion", Description = "Every farm grows 15% more food." },
            new PlatformDef { Id = Platform.Trade, Name = "Railroad & Trade", Description = "Freight wagons pay 25% more for exported food and tools." },
            new PlatformDef { Id = Platform.People, Name = "Common Folk", Description = "Every settler grows a little happier each day." },
        };

        static Defs()
        {
            Add(new BuildingDef { Type = BuildingType.Road, Name = "Road", W = 1, H = 1, Cost = 2, Color = "#a88a5c" });
            Add(new BuildingDef { Type = BuildingType.House, Name = "House & Garden", W = 2, H = 2, Cost = 40, Beds = 4, Color = "#c79a62" });
            Add(new BuildingDef { Type = BuildingType.Farm, Name = "Farm", W = 3, H = 3, Cost = 120, IsBusiness = true, MaxEmployees = 4, BaseWage = 3f, DesiredStart = 2, Color = "#b59a4a" });
            Add(new BuildingDef { Type = BuildingType.Store, Name = "General Store", W = 2, H = 2, Cost = 100, IsBusiness = true, IsRetail = true, MaxEmployees = 2, BaseWage = 3f, ShelfCapacity = 90, StartPrice = 2.2f, DesiredStart = 1, Color = "#2f5d7a" });
            Add(new BuildingDef { Type = BuildingType.Stall, Name = "Market Stall", W = 1, H = 1, Cost = 30, IsBusiness = true, IsRetail = true, MaxEmployees = 1, BaseWage = 2.5f, ShelfCapacity = 25, StartPrice = 2.0f, DesiredStart = 0, Color = "#b5432f" });
            Add(new BuildingDef { Type = BuildingType.Smithy, Name = "Blacksmith", W = 2, H = 2, Cost = 110, IsBusiness = true, MaxEmployees = 2, BaseWage = 3.5f, DesiredStart = 1, Color = "#57504a" });
            Add(new BuildingDef { Type = BuildingType.Saloon, Name = "Saloon", W = 2, H = 2, Cost = 90, IsBusiness = true, MaxEmployees = 2, BaseWage = 3f, DesiredStart = 1, Color = "#8a5a32" });
            Add(new BuildingDef { Type = BuildingType.Sheriff, Name = "Sheriff's Office", W = 2, H = 2, Cost = 100, IsPublic = true, Unique = true, Color = "#9c8b74" });
            Add(new BuildingDef { Type = BuildingType.Estate, Name = "Mayor's Estate", W = 3, H = 2, Cost = 200, IsPublic = true, Unique = true, Color = "#e9e1cf" });
        }

        static void Add(BuildingDef d) { map[d.Type] = d; }
        public static BuildingDef Get(BuildingType t) { return map[t]; }
        public static PlatformDef Get(Platform p) { return Platforms[(int)p]; }

        public static string RoleName(Role r)
        {
            switch (r)
            {
                case Role.Owner: return "Owner";
                case Role.Worker: return "Worker";
                case Role.Sheriff: return "Sheriff";
                case Role.Deputy: return "Deputy";
                case Role.Mayor: return "Mayor";
                default: return "Unemployed";
            }
        }

        public static readonly string[] Seasons = { "Spring", "Summer", "Autumn", "Winter" };
        public static readonly string[] Weekdays = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };

        public static readonly string[] FirstNames =
        {
            "John","William","James","George","Charles","Thomas","Henry","Mary","Anna","Eliza","Sarah","Margaret","Martha","Emma",
            "Clara","Samuel","Joseph","Hannah","Josiah","Ezra","Abigail","Silas","Cora","Amos","Hattie","Wyatt","Ada","Levi","Rose",
            "Jebediah","Nell","Orrin","Lottie","Caleb","Ida","Hiram","Della","Virgil","Mercy","Augustus"
        };
        public static readonly string[] LastNames =
        {
            "Whitaker","Barlow","Hollis","Crane","Pruitt","McCready","Haskins","Delaney","Fenwick","Ostrander","Tolliver","Brannigan",
            "Sutter","Keogh","Lindqvist","Garrity","Moberly","Ashcroft","Pike","Calloway","Thorne","Bixby","Quarles","Redding",
            "Swensen","Mulligan","Abernathy","Cobb"
        };
    }
}
