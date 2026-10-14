# Frontier Ledger

An 1800s settlement builder. It looks a bit like Stardew Valley and plays like Banished: food, firewood and
housing decide whether the town survives the winter.

The town economy is the game:

- Settlers grow some of their food in back gardens and buy the rest at stores and stalls.
- Stores and stalls buy wholesale from farms, fishers and gatherers.
- Whoever owns a business hires hands and pays them wages.
- You set the taxes and pay a building crew.
- The town hires a sheriff and deputies.
- Settlers elect a mayor, who moves into the Mayor's Estate.

Built in Unity 6. All art is generated in code as 3/4-view pixel art, so there are no image assets yet.

## Open it

1. Unity Hub → **Add** → **Add project from disk** → pick this folder.
2. Open it with Unity 6 (6000.x).
3. The first time it opens, the project creates `Assets/Scenes/Main.unity` and opens it.
4. Press **Play**. The game builds itself from code, so the scene stays empty in the editor.

The town saves once a week and when you quit, to `Application.persistentDataPath/frontier-ledger-save-v2.json`.
**Start a new town** is at the bottom of the Ledger book.

## The start

Six households arrive by wagon in spring 1852 with food, firewood, logs, stone and tools to last the first few months.
There are no houses. Families camp in tents by the wagon until cottages go up.

A town that builds nothing starves or freezes in its first winter.

A sound opening:

1. Build cottages.
2. Build a gatherer's hut or fishing dock for food.
3. Build a lumber camp and a woodcutter so there is firewood for winter.
4. Build a storehouse so the wagon can be unloaded.
5. Then build crop fields, which are planted in spring and harvested in autumn.

## Controls

| Action | Input |
|---|---|
| Pan | Left-drag on the map, right/middle-drag, WASD or arrows |
| Zoom | Mouse wheel, `+` / `-` |
| Recenter on town | `H` |
| Pause / speeds | `Space`, `1` / `2` / `3` (1×, 2×, 4×) |
| Tools | `Q` inspect, `R` road, `X` clear trees and rocks, `Esc` or right-click to cancel |
| Build | Hotbar at the bottom: Roads, Housing, Food, Industry, Trade, Civic |
| Inspect | Click a settler or building. Tear down is in the building panel. |

The five books at the top right are the Ledger (taxes, wages, crew, reserves, the town's books), Citizens,
Trade, Election and the Gazette.

## How it works

- **Construction:** placing a building marks a site.
  - The town's builders carry logs and stone from stores (bought from producers if the stores run short) and raise it over several days.
  - Idle laborers help at a quarter of a builder's pace.
  - You set how many builders the treasury pays.
- **Food:** each adult eats 1 food a day, each child 0.6.
  - Gardens cover part of it in spring through autumn and nothing in winter.
  - The rest is bought at shops, or from the town storehouse when no shop has stock.
  - With charity on, the town feeds households that can't pay.
- **Fields:** crops grow while hands work them in spring and summer. They are harvested in autumn, and whatever is still standing at the first frost rots.
- **Idle hands:**
  - Laborers without a job, and builders with nothing to build, help bring in harvests for a share of a wage.
  - The rest of the time they forage and gather deadwood for their own household.
- **Firewood:**
  - Homes burn firewood every day, most of all in winter.
  - Lumber camps fell trees, leaving stumps and replanting saplings. Woodcutters split logs into firewood.
  - Cold homes make people sick. The homeless camp by the fire, which only keeps them warm in spring and summer.
- **Tools:**
  - The blacksmith turns logs and stone into tools.
  - Workers without tools produce 40% less.
- **Jobs:**
  - When a business is finished, the settler with the most savings buys the deed and runs it.
  - Owners hire when trade is good and lay hands off when it isn't.
  - Owners take home the weekly profit above a reserve.
- **Taxes:**
  - Income tax on wages.
  - A weekly head tax on every adult.
  - A business tax on weekly profit.
  - High taxes make people unhappy.
- **Families:**
  - Couples marry when there is an empty cottage for them, have children, and grow old.
  - Births slow down when the town is short of food.
  - The dead are buried in the cemetery if there is room.
  - Newcomers arrive when there are spare beds.
- **Law:** broke, hungry settlers steal. The sheriff and deputies catch them and jail them for two days.
- **Mayor:**
  - Every 4 weeks, 2–3 settlers campaign on a platform: Law & Order, Farmers' Champion, Railroad & Trade or Common Folk.
  - Settlers vote for whoever employs them, lives with them or speaks to their troubles.
  - The winner moves into the Mayor's Estate.
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

A day lasts 16 seconds at 1× speed. The simulation doesn't depend on frame rate or Unity, so it can be tested on its own.

## Roadmap

- Replace the IMGUI interface with UI Toolkit, and the legacy Input Manager with the Input System
- Hand-made sprite sheets to replace the generated art
- More businesses (bakery, sawmill, bank), a railroad station and freight schedule
- Events: droughts, gold strikes, outlaws, fires
