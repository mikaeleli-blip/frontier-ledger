using System;
using System.Collections.Generic;

namespace FrontierLedger.Simulation
{
    public enum BuildingType
    {
        Road, House, Storehouse, Farm, Gatherer, Fishery, LumberCamp, Woodcutter, Quarry, Smithy,
        Store, Stall, Saloon, Sheriff, Estate, Cemetery, Wagon, BuilderYard, BoardingHouse, Bank, School, DoctorOffice,
        Ranch, Bakery, Butcher, IronMine, Tannery, Brewery, Tailor, Hotel, Barber, Church, Brothel, LargeHouse, Manor   // saves store these as numbers: only append
    }
    public enum Category { Roads, Housing, Food, Industry, Trade, Civic, Hidden }
    public enum Role { Child, Laborer, Worker, Owner, Builder, Sheriff, Deputy, Mayor, Teacher, Doctor, Preacher }   // Builder is unused (old saves)
    /// <summary>What a laborer or a builder's crew is doing today.</summary>
    public enum Chore { None, HaulLogs, HaulStone, Build, Harvest, Repair, Craft, School, Tend, Unload, Preach }
    public enum Platform { None = -1, Law, Farm, Trade, People }
    public enum LogKind { Info, Good, Bad, Vote, Build, Death, Birth }

    /// <summary>The goods that move through the town. Saves keep one number per good in this order: only append.</summary>
    public enum Good { Food, Firewood, Logs, Stone, Tools, Furniture, Meat, Bread, Iron, Wool, Hides, Leather, Clothes, Beer, Cattle }

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
        public bool NeedsWater, NeedsStone, NeedsHill, NeedsPasture;
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
        public const int Cols = 160, Rows = 110;   // 16 px tiles
        public const int GoodCount = 15;
        /// <summary>Days in spring, summer, autumn and winter: a long summer to grow and a long autumn to bring it in.</summary>
        public static readonly int[] SeasonDays = { 14, 21, 21, 14 };
        public const int DaysPerYear = 70, StartYear = 1852;
        public const float DaysPerYearF = 70f;
        /// <summary>The day of the year (from 0) a season starts on.</summary>
        public static int SeasonStart(int s) { int d = 0; for (int i = 0; i < s; i++) d += SeasonDays[i]; return d; }

        // Food
        public static readonly float[] GardenBySeason = { 0.9f, 1.3f, 1.0f, 0f };
        public const float GardenBase = 1.4f;              // food per house per day before season factor
        public const float ChildAppetite = 0.6f;
        public const float FieldYield = 600f;              // food from a fully tended field: a year's food for about eight people
        public const float GrowthPerWorker = 0.022f;       // field growth per worker-day in spring/summer
        /// <summary>Logs or stone one laborer carries to a building site in a day.</summary>
        public const float HaulPerDay = 8f;
        /// <summary>Sacks and crates one settler carries from the supply wagon into the storehouse in a day, and what the empty wagon fetches.</summary>
        public const float UnloadPerDay = 40f, WagonSale = 40f;
        /// <summary>Most laborers that fit on one building site.</summary>
        public static int SiteCrew(BuildingDef d) { return Math.Max(2, Math.Min(5, d.W * d.H / 2 + 1)); }
        public const float HarvestPerWorker = 10f;         // food per worker-day in autumn
        public static readonly float[] GatherBySeason = { 1f, 1.3f, 1.1f, 0f };
        /// <summary>What an idle laborer brings home in a day of foraging (times the gathering season) and deadwood.</summary>
        public const float ForagePerDay = 0.8f, DeadwoodPerDay = 0.3f;
        /// <summary>Share of a field hand's harvest an idle laborer manages, and the share of a wage the farm pays them.</summary>
        public const float HelperHarvest = 0.8f, HelperPay = 0.6f;
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
        public const float ToolsPerWorker = 1.2f;          // smith: 1 log + 1 stone each, or half again as many from iron
        public const float IronToolsFactor = 1.5f;
        public const float ToolWearPerWorker = 0.02f;
        public const float NoToolsFactor = 0.6f;

        // Wholesale prices paid to producers, and what the freight wagons pay for surplus going east.
        //                                          food  wood  logs  stone tools furn  meat  bread iron  wool  hides leath cloth beer  cattle
        public static readonly float[] Wholesale = { 0.8f, 0.5f, 1.2f, 1.5f, 6f, 5f,   1.3f, 1.1f, 3.2f, 1.0f, 1.4f, 3.2f, 5.5f, 0.7f, 9.5f };
        public static readonly float[] Export = { 0.4f, 0.25f, 0.6f, 0.7f, 3f, 3f,   0.6f, 0.2f, 2.4f, 0.7f, 1.0f, 2.0f, 3f,   0.25f, 7.5f };
        public static readonly float[] StartRetail = { 1.1f, 0.7f, 0f, 0f, 0f, 8f,    1.8f, 1.4f, 0f,   0f,   0f,   0f,   7.5f, 0f,   0f };
        public const float BarnCapacity = 700f;            // a field's whole harvest stays in town
        public const float BusinessStockCap = 80f;

        // Freight from back east: prices include the haul, plus the teamsters' fee per caravan
        public static readonly float[] ImportPrice = { 1.6f, 1.0f, 2.4f, 3.0f, 10f, 0f, 0f, 0f, 5f, 2f, 0f, 0f, 10f, 0f, 0f };   // 0: not sold back east
        public const float CaravanFee = 10f;
        public const float PassageLaborer = 25f, PassageFamily = 60f;   // the town pays their way out
        public const int FreightDays = 4, FreightDaysWinter = 7;        // days until the caravan comes; the roads are slow in winter

        // Builder's yards
        /// <summary>A building in good repair starts here and wears down day by day, faster in winter.</summary>
        public const float WearPerDay = 0.45f;
        public static readonly float[] WearBySeason = { 1f, 0.8f, 1.1f, 1.8f };
        public const float RepairAt = 70f;                 // a yard is sent for below this condition

        // Owning a business
        public const float LoanInterest = 0.015f;          // a bank charges this much a week on what's owed
        public const float MinDown = 0.25f;                // a bank wants this share of the price paid down
        public const float LoanTerm = 26f;                 // weeks a loan is meant to be paid back over
        public const float BankCapital = 300f;             // what a new banker brings out from back east to lend
        // Banking: owned businesses bank everything but a small till; settlers keep pocket money at home and bank the rest
        public const float DepositInterest = 0.003f;       // at most this a week on savings (about 3% a year), paid out of what the bank earns on loans
        public const float DepositShare = 0.6f;            // share of its loan interest a bank pays its savers
        public const float ReserveRatio = 0.3f;            // share of deposits a bank keeps in its vault so people can draw on them
        public const float PocketMoney = 15f;              // what a settler keeps at home
        // Furniture: made by idle yard crews from logs, bought by households for comfort
        public const float FurnitureLogs = 2f, FurniturePerWorker = 0.5f, FurnitureCap = 20f, FurnitureWear = 0.003f;
        public const float FurnitureSavings = 25f;         // a household spends on furniture only above this much
        // Better houses: a household moves up when it has this much put by (or so many weeks' rent, if more), and back down below KeepUpWeeks of rent
        public const float LargeHouseSavings = 60f, ManorSavings = 250f;
        public const float LargeHouseWeeks = 12f, ManorWeeks = 20f, KeepUpWeeks = 4f;
        public const float LargeHouseMood = 0.3f, ManorMood = 0.6f;                      // pride in the house, a day
        public const float LargeHouseHeat = 1.3f, ManorHeat = 1.7f;                      // firewood burned against a cottage

        // Ranch: steers ready for market and wool, per hand-day by season, at full pasture
        public static readonly float[] CattleBySeason = { 0.44f, 0.37f, 0.32f, 0.17f };   // calving in spring, winter feeding is slow
        public static readonly float[] WoolBySeason = { 1.2f, 0.6f, 0.2f, 0f };          // shearing in spring
        public const int PastureRadius = 6, PastureMin = 25, PastureFull = 50;          // open grass tiles round a ranch
        public const float HerdCap = 24f;                                                // steers penned before the rest are driven east
        // Butcher: dresses steers into meat and a hide
        public const float SteersPerWorker = 0.5f, MeatPerSteer = 8f;
        // Bakery and brewery: grain is food bought from the fields
        public const float BreadPerWorker = 12f;                                         // one loaf of bread per unit of flour
        public const float GrainPerWorkerBrew = 3f, BeerPerGrain = 3f, FuelPerBeer = 0.05f;
        public const float BrewingNeedsFood = 20f;                                       // food a head the town keeps before grain goes to beer
        // Iron mine: ore dug and smelted in a bloomery
        public const float IronPerWorker = 1.8f, FuelPerIron = 0.5f;
        // Tannery and tailor
        public const float LeatherPerWorker = 2f, ClothesPerWorker = 1f, WoolPerClothes = 2f, LeatherPerClothes = 1f;
        public const float ClothesCap = 20f;
        // Clothes on settlers' backs: 1 is a new set; they wear out, faster in winter and on growing children
        public const float ClothesWear = 0.0035f, ClothesNewSet = 0.8f, ClothesBuyBelow = 0.5f, ClothesSavings = 6f;
        public const float ClothesMended = 0.3f;   // families patch and mend: clothes never wear below this
        public const float ClothesGood = 0.5f;     // decently dressed: warmer in the cold, sick less in winter
        // A household's table: with money to spare they buy bread and meat for part of their food
        public const float BreadShare = 0.4f, MeatShare = 0.3f, TreatsFrom = 5f, TreatsFull = 30f;    // savings per adult
        // Saloon: whiskey goes only so far; with the brewery's beer on tap a barkeep serves a third more customers
        public const float DrinkPrice = 1.5f;
        public const int DrinksPerHand = 6, DrinksPerHandBeer = 8;
        // Hotel: travellers on the main road by season, rooms, and what a guest pays
        public static readonly float[] TravellersBySeason = { 2f, 3f, 2.5f, 0.8f };
        public const float RoomRate = 1.5f, MealPrice = 0.8f, GuestDrink = 0.4f, GuestsPerHand = 4f;
        // Barber and bathhouse
        public const float BathPrice = 0.75f, ShavePrice = 0.25f, BathFuel = 0.15f, CleanDays = 7f;
        public const int CustomersPerHand = 8;
        // Brothel: a visit's price is split, the house keeping the larger part; the women are paid their share, not a wage
        public const float VisitPrice = 3f, VisitShare = 0.4f;
        public const int VisitsPerWoman = 3;
        public const float BrothelAge = 18f;              // nobody younger works there or is let in
        // Church: the preacher lives on the Sunday collection; the town makes up a thin plate
        public const float PlateTopUp = 10f;
        public const float TanneryStink = 0.3f; public const int TanneryReach = 6;

        // People
        public const float AdultAge = 10f;                 // children join the workforce at this age (in years)
        public const float BirthChancePerDay = 0.009f;
        public const float MarriageChancePerDay = 0.15f;
        public const float SalaryMayor = 4f, SalarySheriff = 4f, SalaryDeputy = 3f, SalaryTeacher = 3f, SalaryDoctor = 4f;
        /// <summary>Children over 6 who aren't at school help at home: berries, kindling (times the gathering season).</summary>
        public const float ChildForage = 0.3f, ChildKindling = 0.1f;

        // School: children from 5 until they come of age, in the summer and winter terms
        public const float SchoolAge = 5f;
        public const float SchoolYearsFull = 4f;           // years of terms for a full schooling
        public static float SchoolDaysFull { get { return SchoolYearsFull * (SeasonDays[1] + SeasonDays[3]); } }   // summer and winter terms
        public const float SchooledAt = 0.5f;              // enough to read, write and reckon
        public const float UnschooledFactor = 0.9f;        // hands who can't read or reckon get this much done, and are paid this much
        public const float SchooledNewcomers = 0.8f;       // share of grown newcomers who had schooling back east
        public const int SendForStaffDays = 7;             // days the town waits for someone local before sending back east

        // Illness and the doctor
        public const float IllnessPerDay = 0.008f;         // chance a healthy settler falls ill on a given day
        public static readonly float[] IllnessBySeason = { 1f, 1.2f, 0.9f, 1.4f };
        public const float InjuryPerDay = 0.0015f;         // work accidents at the lumber camp, quarry, smithy and yard
        public const int DoctorPatients = 8;               // patients a doctor can see in a day
        public const float VisitFee = 1f;                  // paid to the doctor by patients who can afford it
        public const float ClaimFee = 5f;
        public const float ClearTree = 2f, ClearRock = 6f;
        public const int TermDays = 28;

        // Start
        public const float StartingTreasury = 600f;
        public static readonly float[] StartingStock = { 420f, 90f, 90f, 40f, 16f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f };
    }

    public static class Defs
    {
        static readonly Dictionary<BuildingType, BuildingDef> map = new Dictionary<BuildingType, BuildingDef>();
        public static readonly string[] GoodNames = { "Food", "Firewood", "Logs", "Stone", "Tools", "Furniture", "Meat", "Bread", "Iron", "Wool", "Hides", "Leather", "Clothes", "Beer", "Steers" };
        public static readonly string[] GoodColors = { "#e0a03a", "#c8642a", "#a8734a", "#a6a29a", "#7fa6b8", "#b07a4a", "#b84a3a", "#d8a860", "#5a5a62", "#ece6d8", "#9a6a42", "#7a4a2a", "#4a6a9a", "#c89a3a", "#6e4428" };
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
            Add(new BuildingDef { Type = BuildingType.House, Category = Category.Housing, Name = "Cottage", W = 2, H = 3, Logs = 16, Stone = 6, Work = 10, Beds = 5, Color = "#c79a62",
                Blurb = "Home for one family. The garden out back grows some of their food. Burns firewood to stay warm." });
            Add(new BuildingDef { Type = BuildingType.BoardingHouse, Category = Category.Housing, Name = "Boarding House", W = 3, H = 2, Logs = 18, Stone = 4, Work = 8, Beds = 8, Color = "#b89a6a",
                Blurb = "Cheap rooms for eight. No garden and little privacy: boarders buy all their food, no babies are born here, and families move out as soon as a cottage is free." });
            Add(new BuildingDef { Type = BuildingType.LargeHouse, Category = Category.Housing, Name = "Large House", W = 3, H = 3, Logs = 28, Stone = 14, Work = 18, Beds = 7, Color = "#b8865a",
                Blurb = "A roomy two-storey house with a garden, for a family doing well. When it's empty the family with the most put by (and enough saved) moves up from their cottage. Higher rent, set in the Ledger. They're glad of the room. Burns more firewood." });
            Add(new BuildingDef { Type = BuildingType.Manor, Category = Category.Housing, Name = "Manor House", W = 4, H = 3, Logs = 40, Stone = 36, Work = 30, Beds = 8, Color = "#e6dcc6",
                Blurb = "A grand house on a lawn, for the richest family in town. Only real money moves in, and the rent is steep, set in the Ledger. Living here is a point of pride. Burns plenty of firewood." });
            Add(new BuildingDef { Type = BuildingType.Storehouse, Category = Category.Housing, Name = "Storehouse", W = 3, H = 2, Logs = 20, Stone = 10, Work = 12, IsPublic = true, IsStorage = true, Capacity = 400, Color = "#9a7a4a",
                Blurb = "Town stores for building materials and the winter food reserve. Sells food and firewood to settlers." });
            Add(new BuildingDef { Type = BuildingType.Farm, Category = Category.Food, Name = "Crop Field", W = 3, H = 3, Logs = 12, Stone = 4, Work = 8, IsBusiness = true, MaxEmployees = 4, BaseWage = 2.4f, Color = "#b59a4a",
                Blurb = "Planted in spring, tended through summer, harvested in autumn. Nothing grows in winter." });
            Add(new BuildingDef { Type = BuildingType.Gatherer, Category = Category.Food, Name = "Gatherer's Hut", W = 2, H = 2, Logs = 10, Work = 6, IsBusiness = true, MaxEmployees = 3, BaseWage = 2.2f, Radius = 7, Color = "#6f8a3a",
                Blurb = "Gathers berries, roots and mushrooms from nearby woods. Needs trees around it. Idle in winter." });
            Add(new BuildingDef { Type = BuildingType.Fishery, Category = Category.Food, Name = "Fishing Dock", W = 2, H = 2, Logs = 12, Stone = 4, Work = 8, IsBusiness = true, MaxEmployees = 2, BaseWage = 2.3f, NeedsWater = true, Color = "#4f7f91",
                Blurb = "Must touch the river or lake. Catches fish all year, less through the ice." });
            Add(new BuildingDef { Type = BuildingType.Ranch, Category = Category.Food, Name = "Cattle Ranch", W = 4, H = 3, Logs = 20, Stone = 4, Work = 10, IsBusiness = true, MaxEmployees = 3, BaseWage = 2.4f, Radius = Balance.PastureRadius, NeedsPasture = true, Color = "#9a7a3a",
                Blurb = "Cattle and sheep on open grassland: it needs plenty of open ground around it. Raises steers for the butcher, or drives them east to sell, and shears wool in spring. Works all year." });
            Add(new BuildingDef { Type = BuildingType.Butcher, Category = Category.Food, Name = "Butcher Shop", W = 2, H = 2, Logs = 12, Stone = 6, Work = 8, IsBusiness = true, IsRetail = true, MaxEmployees = 2, BaseWage = 2.4f, Capacity = 60, Color = "#a8443a",
                Sells = new[] { Good.Meat }, Blurb = "Buys steers from the ranches, sells meat over the counter and the hides to the tannery. Meat on the table cheers a family and keeps them healthier." });
            Add(new BuildingDef { Type = BuildingType.Bakery, Category = Category.Food, Name = "Bakery", W = 2, H = 2, Logs = 14, Stone = 10, Work = 10, IsBusiness = true, IsRetail = true, MaxEmployees = 2, BaseWage = 2.3f, Capacity = 50, Color = "#d8a860",
                Sells = new[] { Good.Bread }, Blurb = "Buys grain from the fields and bakes bread. Families with money to spare buy bread: a fuller table and a happier household." });
            Add(new BuildingDef { Type = BuildingType.LumberCamp, Category = Category.Industry, Name = "Lumber Camp", W = 2, H = 2, Logs = 10, Stone = 6, Work = 8, IsBusiness = true, MaxEmployees = 3, BaseWage = 2.5f, Radius = 7, Color = "#7a5a32",
                Blurb = "Fells trees nearby for logs and replants saplings so the forest comes back." });
            Add(new BuildingDef { Type = BuildingType.Woodcutter, Category = Category.Industry, Name = "Woodcutter", W = 2, H = 2, Logs = 8, Stone = 6, Work = 6, IsBusiness = true, MaxEmployees = 2, BaseWage = 2.3f, Color = "#9a5a32",
                Blurb = "Splits logs into firewood. Every home burns firewood, most of all in winter." });
            Add(new BuildingDef { Type = BuildingType.Quarry, Category = Category.Industry, Name = "Quarry", W = 2, H = 2, Logs = 12, Work = 8, IsBusiness = true, MaxEmployees = 3, BaseWage = 2.6f, Radius = 5, NeedsStone = true, Color = "#8d877d",
                Blurb = "Breaks boulders nearby, then cuts stone from the hillside. Needs a hill or boulders close by." });
            Add(new BuildingDef { Type = BuildingType.Smithy, Category = Category.Industry, Name = "Blacksmith", W = 2, H = 2, Logs = 14, Stone = 16, Work = 12, IsBusiness = true, MaxEmployees = 2, BaseWage = 2.8f, Color = "#57504a",
                Blurb = "Turns logs and stone (bog iron) into tools, or logs and iron from a mine or back east, which makes half again as many. Workers without tools get 40% less done." });
            Add(new BuildingDef { Type = BuildingType.BuilderYard, Category = Category.Industry, Name = "Builder's Yard", W = 2, H = 2, Logs = 12, Stone = 4, Work = 8, IsBusiness = true, MaxEmployees = 5, BaseWage = 2.6f, Color = "#b8844a",
                Blurb = "A master builder and crew. The town pays them by the job to raise what you place and to repair worn buildings. In quiet weeks they make furniture." });
            Add(new BuildingDef { Type = BuildingType.IronMine, Category = Category.Industry, Name = "Iron Mine", W = 2, H = 2, Logs = 16, Stone = 8, Work = 12, IsBusiness = true, MaxEmployees = 4, BaseWage = 2.8f, NeedsHill = true, Color = "#5a5048",
                Blurb = "Dug into a hillside: digs ore and smelts it into iron, burning firewood. With iron the blacksmith makes half again as many tools. Surplus iron goes east on the freight wagons." });
            Add(new BuildingDef { Type = BuildingType.Tannery, Category = Category.Industry, Name = "Tannery", W = 3, H = 2, Logs = 14, Stone = 6, Work = 10, IsBusiness = true, MaxEmployees = 2, BaseWage = 2.4f, Color = "#7a5a3a",
                Blurb = "Turns the butcher's hides into leather for the tailor. It stinks: families living within a few lots grumble." });
            Add(new BuildingDef { Type = BuildingType.Brewery, Category = Category.Industry, Name = "Brewery", W = 3, H = 2, Logs = 16, Stone = 10, Work = 10, IsBusiness = true, MaxEmployees = 2, BaseWage = 2.5f, Color = "#b8843a",
                Blurb = "Brews beer from grain and sells it to the saloons and the hotel: a saloon with beer on tap serves a third more customers. Stops buying grain when food runs short." });
            Add(new BuildingDef { Type = BuildingType.Store, Category = Category.Trade, Name = "General Store", W = 2, H = 2, Logs = 18, Stone = 8, Work = 10, IsBusiness = true, IsRetail = true, MaxEmployees = 2, BaseWage = 2.4f, Capacity = 140, Color = "#2f5d7a",
                Sells = new[] { Good.Food, Good.Firewood }, Blurb = "Buys food and firewood wholesale and sells them to households." });
            Add(new BuildingDef { Type = BuildingType.Stall, Category = Category.Trade, Name = "Market Stall", W = 1, H = 1, Logs = 4, Work = 2, IsBusiness = true, IsRetail = true, MaxEmployees = 1, BaseWage = 2.0f, Capacity = 40, Color = "#b5432f",
                Sells = new[] { Good.Food }, Blurb = "A small food stand. Cheap to build." });
            Add(new BuildingDef { Type = BuildingType.Bank, Category = Category.Trade, Name = "Bank", W = 2, H = 2, Logs = 14, Stone = 18, Work = 12, IsBusiness = true, MaxEmployees = 1, BaseWage = 3f, Color = "#8a5a4a",
                Blurb = "Settlers bank their savings and businesses their takings, for a little interest, and can draw on them any time. The banker lends out part of it, always keeping some in the vault: families can buy a business with a quarter down and pay the rest weekly with interest. Miss four payments and the bank takes it back." });
            Add(new BuildingDef { Type = BuildingType.Saloon, Category = Category.Trade, Name = "Saloon", W = 2, H = 2, Logs = 20, Stone = 8, Work = 12, IsBusiness = true, MaxEmployees = 2, BaseWage = 2.4f, Color = "#8a5a32",
                Blurb = "Drinks and company. Cheers settlers up." });
            Add(new BuildingDef { Type = BuildingType.Tailor, Category = Category.Trade, Name = "Tailor", W = 2, H = 2, Logs = 14, Stone = 4, Work = 8, IsBusiness = true, IsRetail = true, MaxEmployees = 2, BaseWage = 2.4f, Capacity = 20, Color = "#4a6a9a",
                Sells = new[] { Good.Clothes }, Blurb = "Makes clothes and boots from wool and leather and sells them. Clothes wear thin: settlers in good clothes are happier, feel the cold less and fall sick less in winter." });
            Add(new BuildingDef { Type = BuildingType.Hotel, Category = Category.Trade, Name = "Hotel", W = 3, H = 2, Logs = 26, Stone = 12, Work = 16, IsBusiness = true, MaxEmployees = 3, BaseWage = 2.3f, Capacity = 8, Color = "#a86a4a",
                Blurb = "Rooms and meals for travellers on the main road, who pay with money from outside. Buys food, beer and firewood. Busier in summer, and as the town grows." });
            Add(new BuildingDef { Type = BuildingType.Barber, Category = Category.Trade, Name = "Barber & Bathhouse", W = 2, H = 2, Logs = 14, Stone = 8, Work = 8, IsBusiness = true, MaxEmployees = 2, BaseWage = 2.2f, Color = "#c84a4a",
                Blurb = "A shave and a hot bath for 75 cents. Settlers come about once a week: it cheers them up, and clean folk fall sick less. Heats the water with firewood." });
            Add(new BuildingDef { Type = BuildingType.Brothel, Category = Category.Trade, Name = "Brothel", W = 2, H = 2, Logs = 18, Stone = 6, Work = 10, IsBusiness = true, MaxEmployees = 4, BaseWage = 2f, Color = "#8a2a3a",
                Blurb = "A parlor house with a red lamp by the door. Men pay $3 a visit, mostly in the evening, travellers from the hotel too; the house keeps 60% and the woman 40%, which is her pay instead of a wage. Only unmarried women of 18 and over work here; anyone can own it." });
            Add(new BuildingDef { Type = BuildingType.Sheriff, Category = Category.Civic, Name = "Sheriff's Office", W = 2, H = 2, Logs = 16, Stone = 14, Work = 12, IsPublic = true, Unique = true, Color = "#9c8b74",
                Blurb = "Hires a sheriff and deputies, paid by the treasury, who catch thieves." });
            Add(new BuildingDef { Type = BuildingType.Estate, Category = Category.Civic, Name = "Mayor's Estate", W = 3, H = 2, Logs = 30, Stone = 30, Work = 24, Beds = 6, IsPublic = true, Unique = true, Color = "#e9e1cf",
                Blurb = "The town elects a mayor, who moves in with their family. The mayor's platform helps the whole town." });
            Add(new BuildingDef { Type = BuildingType.School, Category = Category.Civic, Name = "Schoolhouse", W = 2, H = 2, Logs = 16, Stone = 4, Work = 10, IsPublic = true, Capacity = 25, Color = "#e8dcc4",
                Blurb = "One room, one teacher paid by the town each term, up to 25 children from 5 until they come of age. Classes run in summer and winter; it closes for planting and harvest, when the teacher lends a hand like everyone else. Children who stay home help their family instead. Schooled children grow up able to keep a store or a bank, and get more done." });
            Add(new BuildingDef { Type = BuildingType.DoctorOffice, Category = Category.Civic, Name = "Doctor's Office", W = 2, H = 2, Logs = 14, Stone = 6, Work = 10, IsPublic = true, Color = "#7a9ab0",
                Blurb = "The town pays a doctor from back east to settle here. Every day he sees the worst of the sick and injured: they get over it faster and far fewer die. Patients who can afford it pay him a dollar a visit." });
            Add(new BuildingDef { Type = BuildingType.Church, Category = Category.Civic, Name = "Church", W = 2, H = 3, Logs = 24, Stone = 12, Work = 16, IsPublic = true, Capacity = 40, Color = "#f0ead8",
                Blurb = "A preacher comes and lives on the Sunday collection; the town makes up a thin plate. Sunday service cheers the congregation and eases grief, weddings and funerals mean more, and families like a town with a church." });
            Add(new BuildingDef { Type = BuildingType.Cemetery, Category = Category.Civic, Name = "Cemetery", W = 3, H = 3, Stone = 8, Work = 4, IsPublic = true, Graves = 18, Color = "#6f7a5a",
                Blurb = "Somewhere to bury the dead. Without one, every death weighs on the whole town." });
            Add(new BuildingDef { Type = BuildingType.Wagon, Category = Category.Hidden, Name = "Supply Wagon", W = 2, H = 2, IsPublic = true, IsStorage = true, Capacity = 300, Color = "#c49c69",
                Blurb = "Everything the settlers brought with them. Once a storehouse is built, settlers unload it there and the empty wagon is sold." });
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
                case Role.Teacher: return "Teacher";
                case Role.Doctor: return "Doctor";
                case Role.Preacher: return "Preacher";
                default: return "Laborer";
            }
        }

        /// <summary>What a settler can come down with. Indexes are stored in saves: only append.</summary>
        public static readonly string[] Illnesses =
        {
            "a cold", "a sore throat", "an upset stomach",                         // mild
            "the ague", "dysentery", "a fever", "pleurisy",                         // serious
            "typhoid fever", "pneumonia", "scarlet fever",                          // grave
            "an axe cut", "a crushed hand", "a bad burn", "a fall",                 // work accidents
            "a kick from a horse"
        };

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
