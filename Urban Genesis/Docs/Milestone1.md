# Milestone 1: free-form roads, lots, zoning, growing buildings

(Second revision. The first version used a square grid; that version is still on the branch `feature/milestone-1-core-loop`.)

## Try it

1. In Unity Hub, open the `Urban Genesis` folder (Unity **6000.3.5f2**). The first import takes a few minutes.
2. Open `Assets/Scenes/SampleScene` and press **Play**. Nothing needs wiring: `GameBootstrap` creates the city when Play starts.
3. Draw a road, paint a zone beside it, and watch gray buildings grow.

| Input | Action |
| --- | --- |
| `1` Road, `2` Residential, `3` Commercial, `4` Industrial, `5` Bulldoze | Pick a tool (or use the toolbar) |
| `C` | Road tool: switch between **Straight** and **Curved** |
| `[` / `]` | Smaller / bigger brush (zone and bulldoze tools) |
| `F` | Zone tools: toggle **Fill** (click a lot to zone that whole side of the road) |
| `Shift` | While finishing a road: demolish the buildings in its way |
| `Esc` or right-click | Cancel the road you are drawing, or drop the tool |
| `WASD` / arrows (+ `Shift`) | Move the camera |
| `Q` / `E`, or right-drag | Rotate (right-drag up/down tilts) |
| Middle-drag | Pan |
| Mouse wheel | Zoom |
| `Space` | Pause / resume |

### Drawing roads

* **Straight:** press where the road starts, drag, release where it ends.
* **Curved:** click the start, click a point the road should bend *through*, click the end.
* The preview is blue when the road can be built and red when it cannot; the status line (bottom left) says why.
* A road can start or end in open ground, on a junction (a white ring shows it), or anywhere along another road. In the
  last case the other road is split there and a T-junction appears. Splits land on 8 m marks along the road.
* Roads are 16 m wide (two of the old 8 m tiles) and the map is 4 km across (4096 m).

### Zoning and bulldozing

* Zoning is painted with a round brush. Lots exist only beside roads: **4 rows deep, on both sides** (32 m), following the curve.
  While a zone or bulldoze tool is active every lot is shown faintly.
* **Fill (`F`):** with a zone tool, click a lot and the whole side of that road, all four rows, up to the next junction, is zoned in one go. A highlight shows what will be filled.
* Painting or filling a different zone over a lot removes the building on it.
* Where two roads' lots would cover the same ground (at junctions and inside bends) each patch of ground belongs to one road: a lot with a
  building keeps it, otherwise the lot nearer its road wins, otherwise the older road. The losing lot is hidden, not deleted, so its zoning
  returns if the other road is removed.
* Bulldoze: drag to clear buildings and zoning under the brush. **Click on a road** to remove it (the stretch between its two
  junctions) together with everything beside it.

## The interface

The HUD follows the Figma mock-up *Urban Genesis UI mock UP* and is built with UI Toolkit (`HudToolbar.cs` creates the elements, `Resources/UI/UrbanGenesis.uss` styles them). Icons are the same Lucide icons the mock-up uses.

| Area | What it does |
| --- | --- |
| Top left, city card | City name (picked at random from `CityNames.cs` each launch, until maps and saves exist), buildings and roads count |
| Top right, session bar | Weather, notifications, save, settings, menu (placeholders: they only show a notice) |
| Top right, advisory card | What the current tool would do or just did, otherwise a hint |
| Left, city menu | Layers, statistics, policies, map (placeholders) |
| Right, camera | North arrow (turns with the camera, click to face north), zoom in / out, recentre |
| Bottom centre, toolbar | **Roads**, **Zoning**, **Demolish** work. Power, Water, Services, Transit, Parks, Terrain are dimmed until they exist |
| Strip above the toolbar | Appears with a tool: Straight / Curved for roads, Residential / Commercial / Industrial and Fill for zoning, brush size for zoning and demolish |
| Bottom left, scale | A bar with a round length in metres that follows the zoom, plus the sizes that matter: 1 lot = 8 m, road = 16 m wide |
| Next to the cursor | While drawing a road, a tag shows its length in metres and in lots |
| Bottom bar | Game clock with pause, play, speed (click to cycle 1x to 3x) and fast-forward; Treasury, Population, Happiness, zoning demand (R/C/I), next population milestone |

Placeholders to replace once the simulation has them: weather (fixed 18 C). **Population** is the sum of the residents of all homes: each home holds two to four people (fixed per building), and the biggest residential building is a two-storey block with four apartments. Dense housing (high-rises) is left for a later milestone. A fully built 400 m road currently holds roughly 300 people, so 10,000 people need roughly 13 km of road (the map is 4 km across, so there is plenty of room). **Demand** bars show what the economy wants (see "Economy and demand"). **Happiness** comes from jobs and shops: half employment, half shopping. The clock advances 10 minutes per simulation tick. The number keys, `C`, `F`, `[` `]`, Space and Esc still work as before.

## Economy and demand

Three needs tie the zones into a loop (`Assets/Scripts/Simulation/Economy/`):

* **Work:** industry and commerce need workers; homes supply them (60 % of residents work) and need jobs.
* **Goods:** shops need goods; industry supplies them.
* **Shopping:** homes need shops; shops serve residents, but only as much as their staff and deliveries allow.

So a factory with no workers makes no goods, shops without goods serve nobody, and residents without shops are unhappy.

**How it works.** Every few ticks (`UpdateIntervalTicks`, default 4) `CityEconomy.Update` adds up what every building offers and needs, per road segment, and
matches supply to demand over the road network: only segments connected by **open roads** and within reach (work 2.5 km, shopping 1.5 km, goods 6 km, measured along the roads) match,
and nearer ones are preferred without starving the far ones. Closing a road (`RoadSegment.OpenFraction`) cuts or slows the trips through it automatically.
What stays unmatched becomes demand:

| Zone | Demand goes up when | and down when |
| --- | --- | --- |
| Residential | jobs go unfilled, shops have spare room | people are unemployed |
| Commercial | residents cannot shop (and goods exist), goods pile up unsold, people are unemployed | shops have spare capacity |
| Industrial | shops lack goods, people are unemployed | goods pile up unsold |

Every zone keeps a small base demand so an empty city can start. **Growth follows demand:** each growth attempt succeeds with probability `SpawnChance x demand`
(`CityEconomy.DemandDrivesGrowth`). A city of only homes soon stops growing and asks for industry; the advisory card says what is wrong
("Residents need jobs", "Shops are short of goods", ...).

All the numbers (jobs, service and goods per building type, distances, base demand, smoothing) are in `EconomyParameters.cs` and are meant to be tuned.

**Made for traffic.** The matches are kept as `CityEconomy.Flows`: trips of a kind (work, goods, shopping) from one road segment to another with a volume and a distance.
Traffic will route exactly these trips over the roads, and report congestion by changing `RoadGraph.Cost` (an `IRoadCost`), so jams and closures change who can reach what
without touching the economy code. Each building's own numbers (staffing, goods received, shoppers served) are in `CityEconomy.StatusOf(building)`.

## Money

Zoning and buildings are free. **Only roads cost money** (`CityTreasury.cs`, all numbers are public fields to tune):

* Building a road costs **$15 per metre**, taken when it is placed. A 300 m road is $4,500. You start with **$50,000** (set in the `CityGame` component).
  While drawing, the tag next to the cursor shows the price; a road you cannot afford is refused (red preview, the advisory card says what it costs and what you have).
* Every road costs **$0.50 per metre per game week** in upkeep.
* Bulldozing a road refunds **half** of its build cost.
* Income is tax: each **employed worker pays $12 a week** and each **shopper served $2**. So a city without jobs and shops earns nothing, and the weekly figure
  beside Treasury in the status bar (income minus upkeep) tells you whether the economy works. Money can go below zero; you just cannot build roads then.
* A game week is 1,008 ticks (a tick is 10 game minutes). Money moves every tick, purchases and refunds immediately.

Money is off in the pure simulation until `Treasury.Enable(funds)` is called, so tests and tools can build freely; the game enables it at start.

## Rules

* **Roads never cross each other. This is a deliberate rule:** roads only meet at T-junctions (or at the end of another road).
  End one road on the other.
* Roads need a minimum length (40 m), a bend no tighter than 48 m radius, and at least 30 degrees between roads at a junction.
  Unconnected roads must stay about 15 m apart.
* **A road built through buildings demolishes them, but only if you say so.** The preview turns orange, the buildings that would go
  are highlighted, and the status line counts them. Hold **Shift** when you finish the road to demolish them and build it.
  Without Shift the road is refused and nothing is touched.
* **Buildings only spawn with a direct connection to a road.** A building stands on a block of lots beside one road with its
  front edge on that road, and growth only ever starts from a lot touching the road. Zoning only the far rows of a strip
  grows nothing.
* **Big buildings take priority.** For each spot, growth tries the biggest building templates first and builds the first one
  that fits; small buildings fill what is left. `CitySimulation.BigBuildingPriority` (0 to 1, default 0.85) controls how
  strictly: 1 always takes the biggest that fits, lower values sometimes skip a big one for variety.

## Buildings

Each zone has a list of building types (`Assets/Scripts/Simulation/BuildingCatalog.cs`). Width is how many lots along the
road, depth is how many rows away from it:

| Zone | Types (width x depth) |
| --- | --- |
| Residential | cottage 1x1, bungalow 1x1, chalet 1x1, villa 1x1, farmhouse 1x2, semi-detached houses 2x1, terrace of three 3x1, **four-apartment block 2x1 (two storeys)** |
| Commercial | shop 1x1, office slab 2x1 / 3x2, tower 1x1, podium tower 2x2 / 3x3, mall 3x2 / 4x3 |
| Industrial | shed 1x1, warehouse 2x1 / 3x1 / 4x2, factory 2x2 / 3x3 / 4x4, silo yard 2x1 |

Deep templates exist on purpose: because every building must touch the road, they are what use the back rows of a zoned strip.
A building is only placed where its whole footprint is zoned for it, free, clear of roads and other buildings, and (on a
bend) close enough to the road edge. Tight bends therefore get smaller buildings than straight roads.

Each type is drawn from a few boxes, cylinders and roofs (`BuildingDesigner.cs`); the building's `Variant` number changes
small details, so two of the same type are rarely identical. To add a type: add a line to the catalog, add a `BuildingShape`
value, and add a design for it in `BuildingDesigner`. The tests then check it stays inside its footprint, reaches its floor
count and has more than one look.

## Run the tests

`Window > General > Test Runner > EditMode > Run All`. They cover the geometry and curve maths, the road rules (snapping,
junction splits, crossings, bends, clearance), lot generation on straight and curved roads, zoning, growth (road connection,
big-first order, curves, repeatability), bulldozing, and a randomised stress test that checks all the rules above hold after
hundreds of random edits.

## How the code is organised

```
Assets/Scripts/Simulation   Pure C#, no Unity types. The city model. Units are metres on a flat plane (X east, Y north).
  Vec2, Geometry            Maths helpers: vectors, polygon overlap, distances
  RoadCurve                 A road's centre line: a quadratic Bezier with arc-length lookups, offsets and splitting
  RoadNetwork / Node / Segment   The road graph: planning (snapping, junction splits, rules), building, removing
  Lot, LotLayout            The lots beside every road; zoning is stored on lots
  CitySimulation            Commands (PlanRoad/PlaceRoad, PaintZone, Bulldoze), growth tick, events
  TickClock                 Real time -> simulation ticks, pause and speed
  BuildingCatalog/Template  Which building types exist per zone, their size, height range and rarity
  Building, BuildingDesigner  A placed building (oriented footprint), and its design as boxes/cylinders/roofs

Assets/Scripts/Game         Unity layer. Draws the model and turns input into commands.
  GameBootstrap, CityGame   Start-up; runs the clock and creates everything below
  CityRenderer              Ground, roads, lot tints, building models, tool preview overlay
  BuildingModelFactory      Builds a building's meshes from its design (the place to plug in Blender models later)
  ToolController            Mouse tools and previews
  CameraRig                 Camera
  HudToolbar                The HUD, built from code with UI Toolkit (see "The interface")

Assets/Resources/UI         HUD style sheet (UrbanGenesis.uss), icons (white PNGs, tinted by the style sheet) and Inter fonts
Assets/Editor               Import settings for the HUD icons
Assets/Tests/EditMode       Unit tests for the Simulation code
```

The Simulation assembly never references Unity, so it can be tested quickly and reused if the rendering is rebuilt
(for example with DOTS). The Game assembly only listens to events from the simulation and sends it commands.

## Where the roadworks twist plugs in

Every `RoadSegment` has a `BaseCapacity` and an `OpenFraction` (1 = open, 0 = closed) and knows its two end `RoadNode`s,
so the road graph is ready for routing. A closure will lower `OpenFraction` on some segments for a while, and traffic will route
using `EffectiveCapacity`. Splitting a road keeps the closure state on both halves. No traffic exists yet; that is the next milestone.

## Known limits (on purpose, for now)

* Roads cannot cross (by design); you must build a junction. Curves are single quadratic Beziers per road (chain roads for S-bends).
* One lot is 8 m wide and one floor is 3.5 m. Buildings are stacks of gray boxes, cylinders and roofs until real models arrive.
* On the inside of a tight bend the inner rows of lots are dropped, because the cells would be squashed.
* Removing a road does not merge the two halves of a split road back together.
* There is no budget screen, loans or taxes you can set yet. Residents move in the moment a home is built, and nothing is ever abandoned.
* Shadows are short-ranged because URP's default shadow distance is 50 m. Raise it in the URP asset when we polish visuals.
* Shaders are looked up by name, which is fine in the Editor. Before making a standalone build, make sure the URP
  Unlit/Lit shaders (and `Sprites/Default`) are included in the build.

## Highways and the outside connection

- **Highway** is a road type (2+2 lanes, 28 m wide with a median). Press **H** to cycle road types or use the chips under the Road tool. It has no lots, so nothing is built beside it, costs $45/m with $1.5/m/week upkeep, and counts as twice as fast when the economy works out routes. It needs at least 100 m of length and gentle bends (radius 150 m).
- A highway only joins other roads at its **ends**, and a street cannot join a highway in the middle: connect streets to the end of the highway.
- **Outside connection:** every launch, a highway appears at a random point on the map's edge and runs to a random spot 250–450 m from the middle. It is free, has no upkeep and cannot be bulldozed. The camera starts looking at its inner end. Later it will carry trips to and from outside the city (import, export, commuters).

## Main menu and saving

- Pressing Play shows the **main menu** (title, Continue, New city, Quit), styled like the HUD. *Continue* is greyed out until a save exists and shows the saved city's name, residents and save time.
- In a city, press **Esc** (when no tool is active) or **click the city name** to open the pause menu: *Resume*, *Save game*, *Main menu*. The simulation stops while it is open. *Main menu* asks whether to save first.
- There is **one save slot**, a JSON file called `urban-genesis-save.json` in Unity's per-user data folder (`Application.persistentDataPath`; the Editor's console prints nothing, but on Windows it is under `AppData/LocalLow/<company>/<product>`).
- A save holds the roads (with closures and the outside connection), zones, buildings, money, game time, the camera and the game speed. The economy is recalculated on load, so it is not stored.
- Code: `CitySimulation.Capture()` / `CitySimulation.Restore()` (plain data in `CitySnapshot`), `SaveGame` / `SaveStore` for the file, `GameFlow` for menu ↔ city, `PauseMenu`, `MainMenu`, `MenuUi`.
- Lots are saved by where their centre is, not by their internal keys, so changing how lots are numbered will not break saves. Anything that no longer fits on load is dropped, and the game says how many items.

## Road types, junctions and traffic lights

| Type | Lanes | Width | Buildings | Joins | Cost / upkeep | Speed |
|---|---|---|---|---|---|---|
| Street | 1+1 | 16 m | yes | anywhere | $15/m, $0.5/m/wk | 1.0 |
| Avenue | 2+2 | 22 m | yes | anywhere | $30/m, $0.9/m/wk | 1.4 |
| Highway 1+1 | 1+1 | 20 m | no | ends only | $35/m, $1.2/m/wk | 1.8 |
| Highway 2+2 | 2+2 | 28 m | no | ends only | $45/m, $1.5/m/wk | 2.0 |
| One-way highway | 1, one way | 12 m | no | ends only | $30/m, $1.0/m/wk | 2.0 |

- A one-way highway carries traffic from where you started drawing to where you finished (chevrons show the direction). The route calculation does **not** use the direction yet; that comes with the traffic feature.
- **Traffic lights** stand at every intersection (3 or more roads meet, so T-junctions and four-way crossings). They cycle on their own, but the simulation does not use them yet: they exist so the traffic feature can decide how junctions behave.
- **Junctions tool** (toolbar, or **J** / **6**): click an intersection to select it, then press **T** or the "Traffic lights" button to switch its lights off or on. The choice is saved.

### Lane connections

In the Junctions tool, select an intersection and small white dots appear on each lane that arrives there (lane 1 is the one beside the kerb, on the driver's right). Click a dot to pick that lane; a green or red marker then shows on every road it could continue into. Click a marker to allow (green) or block (red) that turn. "Reset lanes" in the options strip allows everything again.

- Every lane must keep at least one exit, and U-turns and turns the road directions do not allow (into a one-way road that leads in) are not offered.
- Rules follow the road when it is split by a new junction, are dropped when a road is removed, and are saved.
- Like traffic lights, the rules are not used by the route calculation yet: they are stored for the traffic feature (`RoadNode.IsTurnAllowed`).

### Highway turn-offs (ramps)

A **one-way highway** can start or end anywhere along a Highway 1+1 or Highway 2+2: the highway splits there and the one-way road becomes an exit or an entrance, depending on which way you draw it. Streets and avenues still connect to a highway only at its ends (or via a ramp). Because a wide road takes a while to get clear of, a ramp needs to leave at roughly 30° or more. Junctions made only of highways and ramps flow freely and have no traffic lights, but the lane-connection tool works there.

### Highway junctions and crossings

- Any highway-class road (Highway 1+1, Highway 2+2, one-way highway) may **end on the middle of another highway, a street or an avenue**, which splits it there.
- A highway may **cross** another highway: both are split and a flat four-way junction is made, with no traffic lights. A crossing must be at least about 35° (not a shallow graze), at least 30 m from either road's end or from another junction, and a new road may cross each existing road once. Streets and avenues cannot cross highways.
- One-way highways need only **50 m** (other highways keep 100 m, streets 40 m).
- There is no bridge or underpass yet, so crossings are always at street level.

## Traffic

Cars, short trucks and long trucks now drive the roads (`Simulation/Traffic/`, drawn by `Game/TrafficRenderer.cs`).

- **Where trips come from:** the economy's `TripFlow`s. Work and shopping trips become cars, goods trips become short or long trucks. Outside connections also bring in and take out cars and trucks. `CityTraffic.TripsPerVolume`, `OutsideTripsPerTick` and `MaxVehicles` (300) tune the amount.
- **Routing:** `RoutePlanner` finds the quickest route over directed roads, so one-way roads, banned lane turns and closed roads are respected. Vehicles may turn around at dead ends.
- **Driving:** each vehicle follows the one ahead per lane (IDM car-following), picks a lane that is allowed to make its next turn, and slows for bends and its destination.
- **Junctions:** traffic lights use the real signal timing (`TrafficSignals`), the same one the light poles show. Unlit junctions let one vehicle through at a time, longest waiter first. Vehicles never enter a junction unless the road beyond has room.
- **Clean-up:** vehicles on removed or closed roads vanish, and a vehicle stuck for 90 s is removed (counted in `GridlockRemovals`). Traffic is not saved.
- **Models:** placeholders built from cubes. To use your own, put prefabs at `Assets/Resources/Vehicles/Car`, `ShortTruck` and `LongTruck` (origin at the middle on the ground, front along +Z, metre scale).

## Road names and the Upgrade tool

Roads are named by lanes per direction: **single-lane road** (1+1), **two-lane road** (2+2), **single-lane highway** (1+1), **two-lane highway** (2+2) and **one-way highway**. The saved numbers did not change, so old saves still load.

The **Upgrade** tool (toolbar, key `7` or `U`) rebuilds a road as another type in place. Pick the target type in the options row, point at a road: green means it fits, orange means buildings beside it will be demolished (the lots move when the road gets wider), red shows why it cannot be done (too short or sharp for that type, would be too close to another road, or would break a junction's join rules). It costs the new road's price minus half of the old road's price. Lane rules at the road's junctions are reset when the lane count changes, and the road to the outside world cannot be rebuilt. Rules live in `RoadNetwork.CheckUpgrade` and `CitySimulation.PlanUpgrade/UpgradeRoad`.
