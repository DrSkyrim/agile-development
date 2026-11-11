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

Placeholders to replace once the simulation has them: weather (fixed 18 C), Treasury and Happiness (shown as a dash). **Population** is the sum of the residents of all homes: each home holds two to four people (fixed per building), and the biggest residential building is a two-storey block with four apartments. Dense housing (high-rises) is left for a later milestone. A fully built 400 m road currently holds roughly 300 people, so 10,000 people need roughly 13 km of road (the map is 4 km across, so there is plenty of room). **Demand** bars show how much of the zoned land of that type is still empty. The clock advances 10 minutes per simulation tick. The number keys, `C`, `F`, `[` `]`, Space and Esc still work as before.

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
* Growth is a placeholder: random zoned lots fill up, with no demand, money, or population.
* Shadows are short-ranged because URP's default shadow distance is 50 m. Raise it in the URP asset when we polish visuals.
* Shaders are looked up by name, which is fine in the Editor. Before making a standalone build, make sure the URP
  Unlit/Lit shaders (and `Sprites/Default`) are included in the build.
