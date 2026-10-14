using System;
using System.Collections.Generic;

namespace FrontierLedger.Simulation
{
    public enum BuildingType
    {
        Road, House, Storehouse, Farm, Gatherer, Fishery, LumberCamp, Woodcutter, Quarry, Smithy,
        Store, Stall, Saloon, Sheriff, Estate, Cemetery, Wagon
    }
    public enum Category { Roads, Housing, Food, Industry, Trade, Civic, Hidden }
    public enum Role { Child, Laborer, Worker, Owner, Builder, Sheriff, Deputy, Mayor }
    public enum Platform { None = -1, Law, Farm, Trade, People }
    public enum LogKind { Info, Good, Bad, Vote, Build, Death, Birth }

    /// <summary>The five goods that move through the town.</summary>
    public enum Good { Food, Firewood, Logs, Stone, Tools }

    /// <summary>Terrain tile kinds. Values are stored in Town.Terrain.</summary>
    public static class Tile
    {
        public const byte Grass = 0, Water = 1, Tree = 2, Rock = 3, Hill = 4, Sapling = 5, Stump = 6, OutOfBounds = 255;
    }

    public sealed class BuildingDef
    {
        public BuildingType Type;
        public Category Category;
        public string Name, Blurb;
        public int W, H;
        public int Logs, Stone;          // building materials
        public float Work;               // builder-days to finish
        public float Money;              // paid up front by the treasury (roads only for now)
        public bool IsBusiness, IsRetail, IsPublic, Unique, IsStorage;
        public int MaxEmployees, Beds, Capacity, Graves;
        public float BaseWage;
        public int Radius;               // work area for lumber, gathering, quarrying
        public bool NeedsWater, NeedsStone;
        public Good[] Sells = new Good[0];   // what a retailer stocks
        public string Color;             // palette swatch and minimap
        public bool Instant { get { return Work <= 0; } }
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
        public const int DaysPerSeason = 14, DaysPerYear = 56, StartYear = 1852;
        public const float DaysPerYearF = 56f;

        // Food
        public static readonly float[] GardenBySeason = { 0.9f, 1.3f, 1.0f, 0f };
        public const float GardenBase = 1.4f;              // food per house per day before season factor
        public const float ChildAppetite = 0.6f;
        public const float FieldYield = 330f;              // food from a fully tended field
        public const float GrowthPerWorker = 0.022f;       // field growth per worker-day in spring/summer
        public const float HarvestPerWorker = 7f;          // food per worker-day in autumn
        public static readonly float[] GatherBySeason = { 1f, 1.3f, 1.1f, 0f };
        public const float GatherPerWorker = 3.6f;
        public static readonly float[] FishBySeason = { 1f, 1.1f, 1f, 0.5f };
        public const float FishPerWorker = 3.2f;

        // Heat: firewood per house per day by season
        public static readonly float[] FirewoodBySeason = { 0.4f, 0.1f, 0.6f, 1.4f };

        // Industry, per worker-day
        public const float TreeWorkPerWorker = 0.5f;       // a tree takes two worker-days to fell
        public const int LogsPerTree = 6;
        public const float SaplingGrowChance = 1f / 30f;   // per sapling per day
        public const float FirewoodPerLog = 5f;
        public const float LogsSplitPerWorker = 2f;
        public const float StonePerWorker = 2.2f;
        public const int StonePerRock = 6;
        public const float ToolsPerWorker = 1.2f;          // smith: 1 log + 1 stone each
        public const float ToolWearPerWorker = 0.02f;
        public const float NoToolsFactor = 0.6f;

        // Wholesale prices paid to producers
        public static readonly float[] Wholesale = { 0.8f, 0.5f, 1.2f, 1.5f, 6f };
        public static readonly float[] Export = { 0.4f, 0.25f, 0.6f, 0.7f, 3f };
        public static readonly float[] StartRetail = { 1.1f, 0.7f, 0f, 0f, 0f };
        public const float BarnCapacity = 300f;
        public const float BusinessStockCap = 80f;

        // People
        public const float AdultAge = 10f;                 // children join the workforce at this age (in years)
        public const float BirthChancePerDay = 0.012f;
        public const float MarriageChancePerDay = 0.15f;
        public const float SalaryMayor = 4f, SalarySheriff = 4f, SalaryDeputy = 3f;
        public const float ClaimFee = 5f;
        public const float ClearTree = 2f, ClearRock = 6f;
        public const int TermDays = 28;

        // Start
        public const float StartingTreasury = 600f;
        public static readonly float[] StartingStock = { 420f, 90f, 90f, 40f, 16f };
    }

    public static class Defs
    {
        static readonly Dictionary<BuildingType, BuildingDef> map = new Dictionary<BuildingType, BuildingDef>();
        public static readonly string[] GoodNames = { "Food", "Firewood", "Logs", "Stone", "Tools" };
        public static readonly string[] GoodColors = { "#e0a03a", "#c8642a", "#a8734a", "#a6a29a", "#7fa6b8" };
        public static readonly Category[] PaletteOrder = { Category.Roads, Category.Housing, Category.Food, Category.Industry, Category.Trade, Category.Civic };
        public static readonly string[] CategoryNames = { "Roads", "Housing", "Food", "Industry", "Trade", "Civic", "" };

        public static IEnumerable<BuildingDef> InCategory(Category c)
        {
            foreach (var d in map.Values) if (d.Category == c) yield return d;
        }

        public static readonly PlatformDef[] Platforms =
        {
            new PlatformDef { Id = Platform.Law, Name = "Law & Order", Description = "Half as much theft, and the sheriff catches more thieves." },
            new PlatformDef { Id = Platform.Farm, Name = "Farmers' Champion", Description = "Fields, gatherers and fishers bring in 15% more food." },
            new PlatformDef { Id = Platform.Trade, Name = "Railroad & Trade", Description = "Freight wagons pay 25% more for exported goods." },
            new PlatformDef { Id = Platform.People, Name = "Common Folk", Description = "Every settler grows a little happier each day." },
        };

        static Defs()
        {
            Add(new BuildingDef { Type = BuildingType.Road, Category = Category.Roads, Name = "Dirt Road", W = 1, H = 1, Money = 1, Color = "#b08a5a",
                Blurb = "Settlers walk faster on roads. Roads can bridge water." });
            Add(new BuildingDef { Type = BuildingType.House, Category = Category.Housing, Name = "Cottage", W = 2, H = 3, Logs = 16, Stone = 6, Work = 5, Beds = 5, Color = "#c79a62",
                Blurb = "Home for one family. The garden out back grows some of their food. Burns firewood to stay warm." });
            Add(new BuildingDef { Type = BuildingType.Storehouse, Category = Category.Housing, Name = "Storehouse", W = 3, H = 2, Logs = 20, Stone = 10, Work = 6, IsPublic = true, IsStorage = true, Capacity = 400, Color = "#9a7a4a",
                Blurb = "Town stores for building materials and the winter food reserve. Sells food and firewood to settlers." });
            Add(new BuildingDef { Type = BuildingType.Farm, Category = Category.Food, Name = "Crop Field", W = 3, H = 3, Logs = 12, Stone = 4, Work = 4, IsBusiness = true, MaxEmployees = 4, BaseWage = 2.4f, Color = "#b59a4a",
                Blurb = "Planted in spring, tended through summer, harvested in autumn. Nothing grows in winter." });
            Add(new BuildingDef { Type = BuildingType.Gatherer, Category = Category.Food, Name = "Gatherer's Hut", W = 2, H = 2, Logs = 10, Work = 3, IsBusiness = true, MaxEmployees = 3, BaseWage = 2.2f, Radius = 7, Color = "#6f8a3a",
                Blurb = "Gathers berries, roots and mushrooms from nearby woods. Needs trees around it. Idle in winter." });
            Add(new BuildingDef { Type = BuildingType.Fishery, Category = Category.Food, Name = "Fishing Dock", W = 2, H = 2, Logs = 12, Stone = 4, Work = 4, IsBusiness = true, MaxEmployees = 2, BaseWage = 2.3f, NeedsWater = true, Color = "#4f7f91",
                Blurb = "Must touch the river or lake. Catches fish all year, less through the ice." });
            Add(new BuildingDef { Type = BuildingType.LumberCamp, Category = Category.Industry, Name = "Lumber Camp", W = 2, H = 2, Logs = 10, Stone = 6, Work = 4, IsBusiness = true, MaxEmployees = 3, BaseWage = 2.5f, Radius = 7, Color = "#7a5a32",
                Blurb = "Fells trees nearby for logs and replants saplings so the forest comes back." });
            Add(new BuildingDef { Type = BuildingType.Woodcutter, Category = Category.Industry, Name = "Woodcutter", W = 2, H = 2, Logs = 8, Stone = 6, Work = 3, IsBusiness = true, MaxEmployees = 2, BaseWage = 2.3f, Color = "#9a5a32",
                Blurb = "Splits logs into firewood. Every home burns firewood, most of all in winter." });
            Add(new BuildingDef { Type = BuildingType.Quarry, Category = Category.Industry, Name = "Quarry", W = 2, H = 2, Logs = 12, Work = 4, IsBusiness = true, MaxEmployees = 3, BaseWage = 2.6f, Radius = 5, NeedsStone = true, Color = "#8d877d",
                Blurb = "Breaks boulders nearby, then cuts stone from the hillside. Needs a hill or boulders close by." });
            Add(new BuildingDef { Type = BuildingType.Smithy, Category = Category.Industry, Name = "Blacksmith", W = 2, H = 2, Logs = 14, Stone = 16, Work = 6, IsBusiness = true, MaxEmployees = 2, BaseWage = 2.8f, Color = "#57504a",
                Blurb = "Turns logs and stone into tools. Workers without tools get 40% less done." });
            Add(new BuildingDef { Type = BuildingType.Store, Category = Category.Trade, Name = "General Store", W = 2, H = 2, Logs = 18, Stone = 8, Work = 5, IsBusiness = true, IsRetail = true, MaxEmployees = 2, BaseWage = 2.4f, Capacity = 140, Color = "#2f5d7a",
                Sells = new[] { Good.Food, Good.Firewood }, Blurb = "Buys food and firewood wholesale and sells them to households." });
            Add(new BuildingDef { Type = BuildingType.Stall, Category = Category.Trade, Name = "Market Stall", W = 1, H = 1, Logs = 4, Work = 1, IsBusiness = true, IsRetail = true, MaxEmployees = 1, BaseWage = 2.0f, Capacity = 40, Color = "#b5432f",
                Sells = new[] { Good.Food }, Blurb = "A small food stand. Cheap to build." });
            Add(new BuildingDef { Type = BuildingType.Saloon, Category = Category.Trade, Name = "Saloon", W = 2, H = 2, Logs = 20, Stone = 8, Work = 6, IsBusiness = true, MaxEmployees = 2, BaseWage = 2.4f, Color = "#8a5a32",
                Blurb = "Drinks and company. Cheers settlers up." });
            Add(new BuildingDef { Type = BuildingType.Sheriff, Category = Category.Civic, Name = "Sheriff's Office", W = 2, H = 2, Logs = 16, Stone = 14, Work = 6, IsPublic = true, Unique = true, Color = "#9c8b74",
                Blurb = "Hires a sheriff and deputies, paid by the treasury, who catch thieves." });
            Add(new BuildingDef { Type = BuildingType.Estate, Category = Category.Civic, Name = "Mayor's Estate", W = 3, H = 2, Logs = 30, Stone = 30, Work = 12, IsPublic = true, Unique = true, Color = "#e9e1cf",
                Blurb = "The town elects a mayor, who moves in. The mayor's platform helps the whole town." });
            Add(new BuildingDef { Type = BuildingType.Cemetery, Category = Category.Civic, Name = "Cemetery", W = 3, H = 3, Stone = 8, Work = 2, IsPublic = true, Graves = 18, Color = "#6f7a5a",
                Blurb = "Somewhere to bury the dead. Without one, every death weighs on the whole town." });
            Add(new BuildingDef { Type = BuildingType.Wagon, Category = Category.Hidden, Name = "Supply Wagon", W = 2, H = 2, IsPublic = true, IsStorage = true, Capacity = 300, Color = "#c49c69",
                Blurb = "Everything the settlers brought with them. Holds town stores until a storehouse is built." });
        }

        static void Add(BuildingDef d) { map[d.Type] = d; }
        public static BuildingDef Get(BuildingType t) { return map[t]; }
        public static PlatformDef Get(Platform p) { return Platforms[(int)p]; }

        public static string RoleName(Role r)
        {
            switch (r)
            {
                case Role.Child: return "Child";
                case Role.Owner: return "Owner";
                case Role.Worker: return "Worker";
                case Role.Builder: return "Builder";
                case Role.Sheriff: return "Sheriff";
                case Role.Deputy: return "Deputy";
                case Role.Mayor: return "Mayor";
                default: return "Laborer";
            }
        }

        public static readonly string[] Seasons = { "Spring", "Summer", "Autumn", "Winter" };
        public static readonly string[] Weekdays = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };

        public static readonly string[] MaleNames =
        {
            "John","William","James","George","Charles","Thomas","Henry","Samuel","Joseph","Josiah","Ezra","Silas","Amos","Wyatt",
            "Levi","Jebediah","Orrin","Caleb","Hiram","Virgil","Augustus","Elias","Jasper","Abel","Gideon","Rufus","Otis","Asa"
        };
        public static readonly string[] FemaleNames =
        {
            "Mary","Anna","Eliza","Sarah","Margaret","Martha","Emma","Clara","Hannah","Abigail","Cora","Hattie","Ada","Rose",
            "Nell","Lottie","Ida","Della","Mercy","Ruth","Lydia","Esther","Minnie","Pearl","Opal","Viola","Tabitha","Sadie"
        };
        public static readonly string[] LastNames =
        {
            "Whitaker","Barlow","Hollis","Crane","Pruitt","McCready","Haskins","Delaney","Fenwick","Ostrander","Tolliver","Brannigan",
            "Sutter","Keogh","Lindqvist","Garrity","Moberly","Ashcroft","Pike","Calloway","Thorne","Bixby","Quarles","Redding",
            "Swensen","Mulligan","Abernathy","Cobb","Harlan","Vickers"
        };
    }
}
