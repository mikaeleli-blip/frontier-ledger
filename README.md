# Frontier Ledger

An 1800s settlement builder. It looks a bit like Stardew Valley and plays like Banished: food, firewood and
housing decide whether the town survives the winter.

The town economy is the game:

- Settlers grow some of their food in back gardens and buy the rest at stores and stalls.
- Stores and stalls buy wholesale from farms, fishers and gatherers.
- Whoever owns a business hires hands and pays them wages.
- You set the taxes and cottage rent, and pay a builder's yard by the job to build and repair.
- The town hires a sheriff and deputies, a schoolteacher and a doctor.
- Settlers elect a mayor, who moves into the Mayor's Estate.

Built in Unity 6. All art is generated in code as 3/4-view pixel art, so there are no image assets yet.

## Open it

1. Unity Hub → **Add** → **Add project from disk** → pick this folder.
2. Open it with Unity 6 (6000.x).
3. The first time it opens, the project creates `Assets/Scenes/Main.unity` and opens it.
4. Press **Play**. The game builds itself from code, so the scene stays empty in the editor.

The town saves once a week and when you quit, to `Application.persistentDataPath/frontier-ledger-save-v2.json`.
**Start a new town** is at the bottom of the Ledger's Rules page.

## The start

The map is 160 by 110 tiles: room for a big town with plenty of country left. A river runs a good walk west of the
town site and a creek to the east, with lakes, hills and woods around.

Six households arrive by wagon in spring 1852 with food, firewood, logs, stone and tools to last the first few months.
One of the men is a builder by trade: you pick where his Builder's Yard goes (Industry tab). Until it stands,
the town hires settlers by the day to build. There are no houses yet; families camp in tents by the wagon.

A town that builds nothing starves or freezes in its first winter. Tents can't keep anyone warm in autumn or winter;
people huddled by the fire last longer if there's firewood in store, but not for long. Warnings appear under the
stores bar when people are in tents as the cold comes, when food or firewood will run out within ten days, and when
a food business has nobody working it.

A sound opening:

1. Place the Builder's Yard, then a boarding house to get people out of the tents, then cottages for the families.
2. Build a gatherer's hut or fishing dock for food.
3. Build a lumber camp and a woodcutter so there is firewood for winter.
4. Build a storehouse. Settlers without a job carry the wagon's load into it (the town pays them by the day), and the empty wagon is sold. If the storehouse fills up, the rest waits in the wagon and is used first.
5. Then build crop fields, which are planted in spring and harvested in autumn.

## Controls

| Action | Input |
|---|---|
| Pan | Left-drag on the map, right/middle-drag, WASD or arrows (hold Shift to go faster), or click the minimap |
| Zoom | Mouse wheel, `+` / `-` |
| Recenter on town | `H` |
| Pause / speeds | `Space`, `1`–`4` (1×, 3×, 10×, 30×). A day lasts 3 minutes at 1×. |
| Tools | `Q` inspect, `R` road, `X` clear trees and rocks, `Esc` or right-click to cancel |
| Build | Hotbar at the bottom: Roads, Housing, Food, Industry, Trade, Civic |
| Inspect | Click a settler or building. Tear down is in the building panel. |

The six books at the top right are the Ledger, Citizens, Trade, Election, the Gazette and Freight.

The Ledger has five pages:
- **Town**: the treasury, what came in and went out this week and last (point at a line for the detail), who holds the money
  (treasury, settlers, businesses, the bank's own), and the richest, poorest and typical settler.
- **People**: every grown-up with their work, what they earned last week after tax, and what they have at home and in the bank.
  Sort by money, earnings, name or job; a gold mark means they own the business. Point at a name for spending and family money.
- **Business**: every business by last week's profit, with owner, take-home, cash and loans.
- **Bank**: the vault, savers' money, loans, the bank's own money, and lists of savers and loans.
- **Rules**: taxes, wages, rent, the stores, switches for births and newcomers, elections and Start a new town.
  With newcomers off, only settlers you order by Freight and the teacher, doctor, preacher or banker the town sends for arrive.

## How it works

- **Construction:** placing a building stakes out a site, and a builder's yard takes the contract.
  - The yard is a business like any other: a master builder who owns it and up to five hired hands.
  - The town pays the contract rate for each day of work the job takes: half when work starts, half when it's done. You set the rate in the Ledger.
  - The crew carries logs and stone from the storehouse or wagon to the site. If the stores run short, the town buys from producers.
  - Then they lay stone footings, raise a timber frame, and put up the walls and roof behind scaffolding.
  - More yards means more jobs at once. With no yard at all, the town falls back to hiring settlers without a job by the day.
- **Wear and repairs:** buildings wear down a little every day, faster in winter.
  - Below 70% they need a yard. Each yard books two repairs per worker plus two; the rest wait in line, worst first,
    for whichever yard has room. Repairs are paid for when work starts, and a yard finishes what it started first. The town pays for cottages and public buildings; owners pay for their businesses.
  - Drafty cottages burn more firewood, and leaking ones make the family miserable. Worn businesses get less done.
- **Firewood emergencies:** when the town has less than about a week's firewood for its homes, woodcutters take on every
  hand they can pay, and hired hands leave other work (never food work while food is short) to split wood, one a day.
- **Housing:** tents, then a boarding house, then a cottage, then a large house, then a manor.
  - A **large house** (7 beds, kitchen garden) goes to the best-off household below it once they have $60 or
    12 weeks' rent put by, whichever is more. Default rent $5 a week. A small mood boost; it burns 30% more firewood.
  - A **manor house** (8 beds, lawn, no garden) goes to the richest household in town once they have $250 or
    20 weeks' rent put by. Default rent $12 a week. A bigger mood boost; it burns 70% more firewood.
  - Under four weeks' rent put by, a household moves back down (a manor family to an empty large house if they can
    keep one, otherwise an empty cottage). With nowhere to go they stay, and are put out after four weeks behind.
  - Large houses and manors buy food and firewood first when the town is short. Both rents are on the Ledger's Rules page.
  - A boarding house sleeps eight cheaply, but has no garden, is cramped, and no babies are born there.
  - Single adults take a boarding-house bed, and grown children move out of their parents' cottage into one.
  - Families board only when no cottage is free, and move out as soon as one is. Two children share a bed,
    and a single boarder gives up their bed to a family with little ones.
- **Rent:** cottages, large houses and manors pay weekly rent per family, boarders pay per head. Four weeks behind and they're put out.
  Families living on charity pay nothing.
- **The well-off:** how rich a settler is runs from nothing at $100 put by (per grown-up in the family) to full at $500.
  The rich drink at the saloon and sometimes stand a round, go to the barber every few days, dine at the hotel ($1.60)
  once or twice a week, buy new clothes before theirs wear out, keep more furniture, and put more meat and bread on the
  table. They draw on the bank to pay for it.
  - **Servants:** a large house keeps one servant, a manor two, at the minimum wage paid by the family, one for every six
    weeks' wages they have put by. They're taken on from settlers looking for work, and let go when money gets tight.
    A family with help in the house is a little happier.
  - **Alms:** at Sunday service the well-off put 1% of what they have over $150 (up to $3) in the plate for the poor, and
    feel the better for it. Through the week the preacher hands it out, a dollar at a time, to grown-ups with under $15.
- **The bank:** the town pays whoever runs it a salary (Rules page, $3/day by default). If the bank stands empty for a week,
  the town sends back east for a banker, who comes once there are two free beds. Besides lending to families buying a business,
  the bank lends working money to owners whose till runs short, lends owners the price of a repair, and lends to the town
  when the treasury falls under $30 (up to $500, a switch on the Rules page). A business can owe up to what it's worth, and
  gets nothing more while behind on payments or after three losing weeks (repairs excepted). All loans pay 1.5% a week.
  The bank hires a clerk only once its loans earn his wage.
- **Owning a business:** a finished business is run by the town until a family can buy it at what it cost to build.
  - The town hires and pays its hands and keeps its takings meanwhile.
  - A family buys with its own savings, in full. Once a Bank is open, the banker lends the rest to a family with a
    quarter of the price, at 1.5% a week, paid back over about 26 weeks. Four missed payments and the bank takes the business
    back (farms settle at harvest). It takes what's in the till and the business's account against the loan, then the town
    runs it until it sells, and the bank is paid what it's still owed out of the price. The same happens when an owner with
    a loan leaves town or dies with no one to take over.
  - When a Bank is finished, a banker comes out from back east with money to lend and buys it from the town.
  - Banking: owned businesses bank everything but a small till each Sunday, and settlers bank all but $15 of pocket money.
    They draw on their accounts whenever they need to (shopping, rent, taxes, buying a business), and banked money is safe
    from thieves. The bank keeps 30% of deposits in the vault and lends out the rest. Savers share in what the loans earn,
    up to 0.3% a week. The banker only takes home the bank's profit.
  - One business to a family. The owner's wife or husband and grown children at home work it alongside them for no wage,
    and only leave it for paid work when it isn't paying its way or the town needs food.
- **Jobs:** settlers take the best-paying work going, and food work first while the town has less than a month's food
  put by. A hired hand will leave for clearly better pay, or for a food job when food is short.
  Keeping a store or a bank takes someone who can read, write and reckon.
- **School:** a schoolhouse takes up to 25 children from age 5 until they come of age.
  - The teacher is a settler who can read and write, or someone sent for from back east if nobody in town can (they need a free bed).
  - Classes run in the summer and winter terms. It closes for spring planting and the harvest, and the teacher works like
    anyone else between terms. The town pays the teacher only in term.
  - Two years of school is enough to read, write and reckon. Most settlers who come from back east already can;
    children raised here only learn if there's a school.
  - Grown-ups who can't read or reckon get 10% less done and are paid 10% less, and can't keep a store or a bank.
  - Children who aren't at school help at home, picking berries and gathering kindling.
  - Parents are happier with their children in school, and more families settle in a town with a school.
- **Illness:** settlers catch colds, the ague, dysentery, fevers and pneumonia. Winter, cold, hunger, crowded boarding
  houses and a sick family member make it likelier. Babies and the old are frailer. Lumbermen, quarrymen, smiths and
  builders sometimes get hurt at work. The seriously ill stay in bed and can't work or go to school.
- **Doctor:** the town pays a doctor from back east to settle in a Doctor's Office.
  - Every day the doctor sees up to 8 of the worst cases, on house calls. Patients get over it faster and far fewer die.
  - Patients who can afford it pay a dollar a visit. More families settle in a town with a doctor.
- **Furniture:** in quiet weeks the yard's crew makes furniture from logs and sells it from the yard.
  Households with savings buy a piece now and then; a furnished home is a happier one.
- **Food:** each adult eats 1 food a day, each child 0.6.
  - Gardens cover part of it in spring through autumn and nothing in winter.
  - The rest is bought at shops, or from the town storehouse when no shop has stock.
  - With charity on, the town feeds households that can't pay. Town-run fields, docks, camps and quarries fill the
    storehouse up to its reserves before the town buys anything, so charity has food even when the treasury is empty.
  - Families that farm, fish, gather, butcher, bake or cut firewood eat and burn their own first.
- **Fields:** a fully tended field yields 600 food, a year's food for about eight people. Crops grow while hands work them in spring and the three weeks of summer. They are harvested through the three weeks of autumn, and whatever is still standing at the first frost rots.
- **Idle hands:**
  - Laborers without a job help bring in harvests for a share of a wage.
  - The rest of the time they forage and gather deadwood for their own household.
- **Firewood:**
  - Homes burn firewood every day, most of all in winter.
  - Lumber camps fell trees, leaving stumps and replanting saplings. Woodcutters split logs into firewood.
  - Cold homes make people sick. The homeless camp by the fire, which only keeps them warm in spring and summer.
- **Tools:**
  - The blacksmith turns logs and stone (bog iron) into tools, or logs and iron into half again as many.
  - Workers without tools produce 40% less.
- **The trades:** each one buys from the last and sells to the next; what doesn't sell in town goes east on the freight wagons.
  The freight wagons pay only about 40% of what goods fetch in town (iron $1.28 against $3.20, steers $3.80 against $9.50),
  so selling at home pays far better and the trades that live on shipping east earn much less. Surplus grain still goes into
  the town's stores at $0.40.
  - *Cattle ranch:* needs open grassland round it. Raises steers all year and shears wool in spring. Steers go to the butcher;
    past 24 head the rest are driven east.
  - *Butcher shop:* dresses steers into meat for the counter and hides for the tannery.
  - *Bakery:* buys grain from the fields (or the granary's surplus) and bakes bread.
  - *Tannery:* turns hides into leather. Families living within 6 lots grumble about the stink.
  - *Tailor:* makes clothes from wool and leather.
  - *Iron mine:* dug into a hillside; smelts iron with firewood for the blacksmith, and sends the rest east.
  - *Brewery:* brews beer from grain for the saloons and the hotel, unless food is short. A saloon with beer on tap
    serves a third more customers.
  - *Hotel:* travellers on the main road pay for a room, supper and a drink: money from outside. More come in summer
    and as the town grows; a hand looks after four guests.
  - *Barber & bathhouse:* a shave and a hot bath about once a week. It cheers people up, and clean folk fall sick less.
  - *Brothel:* a parlor house with a red lamp by the door. Men of 18 and over with a few dollars in their pocket visit now
    and then (single men far more than married ones), and so do travellers staying at the hotel. A visit is $3: the house
    keeps 60% and the woman 40%, which is her pay instead of a wage (taxed like one). Up to four women, three visits each a
    day. Only unmarried women of 18 and over work there, and a woman who marries leaves. When no woman in town will take the
    work, women come up the road for it once there's a bed. Anyone can own it.
  - *Church:* a preacher lives on the Sunday collection, which the town makes up to $10 a week. Sunday service cheers
    the congregation and eases grief; church weddings and funerals mean more; families like a town with a church.
- **The table:** households with money to spare buy bread and meat for part of their food, and are happier and
  healthier for it.
- **Clothes:** clothes wear thin over three or four years and get patched and mended. New, decent clothes cheer people,
  keep out the cold, and mean less illness in winter. Households buy from the tailor, or clothes ordered from back east.
- **Running a business:**
  - Owners hire when trade is good and lay hands off when it isn't. Fields take on hands for the harvest.
    Nobody is taken on unless there's a week's wages in hand for every hired hand (the treasury backs the town's own).
  - Owners take home the weekly profit above a reserve. If the family can't afford the day's food or firewood, they spend
    their savings first, then the owner takes what's needed from whatever the business holds beyond its working money.
    A banker can only take the bank's own money, never the savers'.
  - When no shop is open, farmers, fishers and woodcutters sell at their own gate.
- **Freight from back east:** in the Freight book, order food, firewood, logs, stone, tools, iron, wool and clothes, or pay passage for laborers
  ($25) and families ($60).
  - The treasury pays when you order: goods at back-east prices with the haul included, plus a $10 teamsters' fee.
  - A caravan of covered wagons comes up the main road 4 days later (7 in winter), stops by the storehouse and drives on.
  - Goods go into the storehouse; whatever won't fit is refunded. Settlers climb down at the stop and look for work and a bed.
- **Taxes:**
  - Income tax on wages.
  - A weekly head tax on every adult.
  - A business tax on weekly profit.
  - High taxes make people unhappy.
- **Families:**
  - Single men and women marry (never close relatives) when there is an empty cottage for them, have children, and grow old.
  - Births slow down when the town is short of food.
  - The dead are buried in the cemetery if there is room.
  - Newcomers arrive when there are spare beds and the larders aren't thin or running down, and bring some food with them.
- **Law:** broke, hungry settlers steal. The sheriff and deputies catch them and jail them for two days.
- **Daily life:** settlers open their front doors to go in at night and come out in the morning. Most saloon-goers head inside, and jailed settlers are taken into the sheriff's office.
  - Shopkeepers, barkeeps, bankers, smiths, lawmen, the teacher, the doctor and the preacher work indoors, and so do
    the butcher, baker, tailor, barber, brewer and hotel staff. Butchers, bakers and barbers wear aprons.
  - On Sunday morning most of the town walks to church. Ranch hands ride herd among the cattle, steers are led to the
    butcher on a rope, and travellers' horses stand at the hotel's rail in the evening.
  - Everyone is seen at their work. Farm hands sow and hoe in spring and summer, scythe the ripe grain in autumn and fork
    hay and muck. Lumberjacks chop at the nearest trees, which shake and drop chips until they come down. Quarrymen
    swing picks at boulders, woodcutters split logs with a maul and saw at the sawhorse, fishers cast from the bank,
    and gatherers pick into baskets. Ranch hands fork hay and swing a rope among the herd. Tanners scrape hides and work
    the pits, and miners haul the ore rope and shovel charcoal into the furnace.
  - Indoors the butcher cleaves, the baker kneads and works the oven peel, the tailor cuts and sews, the barber shaves
    whoever's in the chair, the brewer stirs the kettle, the barkeep wipes the bar, clerks serve and keep the books,
    the smith's striker works the bellows, the teacher points at the blackboard and the preacher preaches.
  - Every purchase is a trip: someone from the household walks to the shop, goes in, and comes home with a sack.
    Those at work shop after hours. Farmers, fishers, gatherers and woodcutters carry what they sell to the store or storehouse.
- **Seeing inside:** click a cottage, a large house, a manor, the boarding house, the estate, the storehouse, store, saloon, bank, sheriff's office,
  smithy, schoolhouse, doctor's office, butcher, bakery, tailor, hotel, barber, church or brewery and the roof lifts off. Staff work at their stations, customers wait at the counter,
  pupils sit at their desks, and the sleeping and the sick lie in bed. Clicking a settler who's indoors opens their building too.
- **Mayor:**
  - Every 4 weeks, 2–3 settlers campaign on a platform: Law & Order, Farmers' Champion, Railroad & Trade or Common Folk.
  - Settlers vote for whoever employs them, lives with them or speaks to their troubles.
  - The winner moves into the Mayor's Estate with their family.
  - Turn elections off and the mayor serves for life.

All the numbers live in `Assets/Scripts/Simulation/Defs.cs` (`Balance` and `Defs`).

## Code layout

```
Assets/Scripts/
  Simulation/          Pure C#, no Unity references (FrontierLedger.Simulation.asmdef)
    Defs.cs            Building types, roles, goods, names, every balance number
    Model.cs           Building, Citizen, policy, ledger and report data
    TerrainGen.cs      River, lake, hills, woods and boulders from a seed
    Town.cs            Map, calendar, placing/removing, town stores, the starting wagon
    Town.People.cs     Births, deaths, marriage, housing, owners and hiring
    Town.Day.cs        The daily and weekly simulation: building, production, trade, households, wages, health
    Town.Election.cs   Campaigns, polls and elections
  Game/                Unity layer (FrontierLedger.Game.asmdef)
    GameController.cs  Boots the game, runs the clock, input, save/load
    CameraRig.cs       Pixel-perfect orthographic camera, pan and integer zoom
    Nav.cs             A* pathfinding for settlers (roads are fastest)
    WorldView.cs       Sprites for ground, buildings, sites, trees, settlers, smoke, night
    Hud.cs             Wooden pixel-art interface: date, stores, hotbar, minimap, books, inspector
    UI/                Pixel font and UI kit (9-slice frames, buttons, icons)
    Art/               Procedural pixel art: buildings, nature, people, props, ground
Assets/Editor/
  ProjectSetup.cs      Creates and opens the Main scene on first load
```

A day lasts 3 minutes at 1× speed. Spring and winter last 14 days, summer and autumn 21, so a year is 70 days. The simulation doesn't depend on frame rate or Unity, so it can be tested on its own.

## Roadmap

- Replace the IMGUI interface with UI Toolkit, and the legacy Input Manager with the Input System
- Hand-made sprite sheets to replace the generated art
- A railroad station and freight schedule
- Events: droughts, gold strikes, outlaws, fires
