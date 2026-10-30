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
* Roads are 16 m wide (two of the old 8 m tiles) and the map is 1 km across.

### Zoning and bulldozing

* Zoning is painted with a round brush. Lots exist only beside roads: **4 rows deep, on both sides** (32 m), following the curve.
  While a zone or bulldoze tool is active every lot is shown faintly.
* Painting a different zone over a lot removes the building on it.
* Bulldoze: drag to clear buildings and zoning under the brush. **Click on a road** to remove it (the stretch between its two
  junctions) together with everything beside it.

## Rules

* Roads never cross each other. They meet at junctions: end one road on the other. (Crossing needs automatic junction
  creation, which is a later improvement.)
* Roads need a minimum length (40 m), a bend no tighter than 48 m radius, and at least 30 degrees between roads at a junction.
  Unconnected roads must stay about 15 m apart.
* A road cannot be built through buildings. Bulldoze them first.
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
| Residential | cottage 1x1, row houses 2x1 / 3x1, apartment block 2x1 / 2x2 / 3x3 / 4x3, L-shaped block 2x2, courtyard block 3x2 / 4x4, terrace tower 2x2 / 3x3 |
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
  CameraRig, HudToolbar     Camera, and the placeholder toolbar (immediate-mode GUI)

Assets/Tests/EditMode       Unit tests for the Simulation code
```

The Simulation assembly never references Unity, so it can be tested quickly and reused if the rendering is rebuilt
(for example with DOTS). The Game assembly only listens to events from the simulation and sends it commands.

## Where the roadworks twist plugs in

Every `RoadSegment` has a `BaseCapacity` and an `OpenFraction` (1 = open, 0 = closed) and knows its two end `RoadNode`s,
so the road graph is ready for routing. A closure will lower `OpenFraction` on some segments for a while, and traffic will route
using `EffectiveCapacity`. Splitting a road keeps the closure state on both halves. No traffic exists yet; that is the next milestone.

## Known limits (on purpose, for now)

* Roads cannot cross; you must build a junction. Curves are single quadratic Beziers per road (chain roads for S-bends).
* One lot is 8 m wide and one floor is 3.5 m. Buildings are stacks of gray boxes, cylinders and roofs until real models arrive.
* On the inside of a tight bend the inner rows of lots are dropped, because the cells would be squashed.
* Removing a road does not merge the two halves of a split road back together.
* Growth is a placeholder: random zoned lots fill up, with no demand, money, or population.
* Shadows are short-ranged because URP's default shadow distance is 50 m. Raise it in the URP asset when we polish visuals.
* Shaders are looked up by name, which is fine in the Editor. Before making a standalone build, make sure the URP
  Unlit/Lit shaders (and `Sprites/Default`) are included in the build.
