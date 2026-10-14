# Frontier Ledger

An 1800s settlement builder where the town economy is the game. Settlers grow some of their food in
back gardens and buy the rest at stores and stalls, which buy wholesale from farms. Whoever owns a farm,
smithy, store or saloon hires hands and pays wages. You set the taxes, the town hires a sheriff and
deputies, and the settlers elect a mayor who moves into the Mayor's Estate.

Built in Unity 6. All art is generated in code as 3/4-view pixel art, so there are no image assets yet.

## Open it

1. Unity Hub → **Add** → **Add project from disk** → pick this folder.
2. Open it with Unity 6 (any 6000.x). If Hub asks to change the editor version, say yes.
3. The first time it opens, the project creates `Assets/Scenes/Main.unity` and opens it.
4. Press **Play**. The game builds itself from code, so the scene stays empty in the editor.

The town saves every Monday and when you quit, to `Application.persistentDataPath/frontier-ledger-save.json`.
Use **New town** in the Ledger tab to start over.

## Controls

| Action | Input |
|---|---|
| Pan | Left-drag (Inspect tool), right/middle-drag, WASD or arrows |
| Zoom | Mouse wheel, `+` / `-` |
| Recenter on town | `H` |
| Pause / resume | `Space` |
| Tools | `Q` inspect, `R` road, `1`–`8` buildings, `X` clear & demolish, `Esc` cancel |

## How the economy works

- **Food:** each settler eats 1 food a day. A house's back garden covers part of it (barely anything in winter);
  the rest is bought at stores and stalls. Shops buy from farms at $1.30; surplus leaves on the freight wagon.
- **Jobs:** you build a business, then the settler with the most savings buys the deed and hires hands.
  Owners take home weekly profit, hire when business is good and lay people off when it isn't.
- **Blacksmith** sells tools to farms (+40% harvest). **Saloon** sells drinks that cheer people up.
- **Taxes:** income tax on every wage, a weekly head tax on every settler, and a business tax on weekly profit.
  You also set the minimum wage and how many deputies the treasury pays for.
- **Law:** broke, hungry settlers steal. The sheriff and deputies catch them and jail them for two days.
- **Mayor:** every 4 weeks the 2–3 most prominent settlers campaign on a platform (Law & Order, Farmers' Champion,
  Railroad & Trade, Common Folk). Settlers vote for whoever employs them, lives with them or speaks to their troubles.
  The winner moves into the Mayor's Estate. Turn elections off and the mayor serves for life.

All the numbers live in `Assets/Scripts/Simulation/Defs.cs` (`Balance` and `Defs`).

## Code layout

```
Assets/Scripts/
  Simulation/          Pure C#, no Unity references (FrontierLedger.Simulation.asmdef)
    Defs.cs            Building types, platforms, names, every balance number
    Model.cs           Building, Citizen, campaign, ledger and report data
    TerrainGen.cs      River, lake, hills, woods and boulders from a seed
    Town.cs            Map, placing/removing, settlers, hiring
    Town.Day.cs        The daily and weekly simulation, crime, mood, migration, elections
  Game/                Unity layer (FrontierLedger.Game.asmdef)
    GameController.cs  Boots the game, runs the clock, input, save/load
    CameraRig.cs       Pixel-perfect orthographic camera, pan and zoom
    WorldView.cs       Sprites for ground, buildings, trees, settlers, smoke, night
    Hud.cs             Placeholder IMGUI interface: top bar, build palette, minimap, ledger tabs, inspector
    Art/               Procedural pixel art: buildings, nature and people, ground painter
Assets/Editor/
  ProjectSetup.cs      Creates and opens the Main scene on first load
```

The simulation runs one day per 10 seconds at 1× speed and does not depend on frame rate or Unity,
so it can be tested on its own.

## Roadmap

- Replace the IMGUI placeholder with a UI Toolkit interface
- Hand-made sprite sheets to replace the generated art
- Pathfinding along roads instead of straight-line walking
- More businesses (bakery, lumber mill, bank), a railroad station and freight schedule
- Events: droughts, gold strikes, outlaws, fires
