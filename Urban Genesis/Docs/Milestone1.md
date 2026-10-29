# Milestone 1: grid, roads, zoning, growing buildings

## Try it

1. In Unity Hub, open the `Urban Genesis` folder (Unity **6000.3.5f2**). The first import takes a few minutes.
2. Open `Assets/Scenes/SampleScene` and press **Play**. Nothing needs wiring: `GameBootstrap` creates the city when Play starts.
3. Build a road, zone next to it, and watch gray buildings grow.

| Input | Action |
| --- | --- |
| `1` Road, `2` Residential, `3` Commercial, `4` Industrial, `5` Bulldoze | Pick a tool (or use the toolbar). Press and drag in the city, release to apply |
| `Esc` | Cancel the drag, or drop the tool |
| `WASD` / arrows (+ `Shift`) | Move the camera |
| `Q` / `E`, or right-drag | Rotate (right-drag up/down tilts) |
| Middle-drag | Pan |
| Mouse wheel | Zoom |
| `Space` | Pause / resume |

Rules: roads go in straight lines along the grid. Zones can only be painted within 4 cells of a road, and they are
cleared again if that road is removed. Buildings cannot be built over; bulldoze them first.

## Buildings

Buildings are not all one cell. Each zone has a list of building types (`Assets/Scripts/Simulation/BuildingCatalog.cs`),
and growth picks one at random, weighted by how common it should be:

| Zone | Types (cells) |
| --- | --- |
| Residential | cottage (1x1), row houses (2x1), apartment block (2x1 or 2x2), L-shaped block (2x2), courtyard block (3x2), terrace tower (2x2) |
| Commercial | shop (1x1), office slab (2x1), tower (1x1), podium tower (2x2), mall (3x2) |
| Industrial | shed (1x1), warehouse (2x1 or 3x1), factory (2x2), silo yard (2x1) |

A building is only placed where its whole footprint fits inside the zoned area, so a one-cell-wide strip of zone only gets
small buildings. Growth tries the footprint in all four rotations and prefers the one whose front faces the nearest road.
Gaps that nothing bigger fits into are filled with 1x1 buildings. Bulldozing any one cell of a big building removes the whole building.

Each type is drawn from a few boxes, cylinders and roofs (`BuildingDesigner.cs`), and the building's `Variant` number changes the
small details (roof direction, chimney, which end is lower), so two of the same type are rarely identical.

To add a building type: add a line to the catalog, add a `BuildingShape` value, and add a design for it in `BuildingDesigner`.
The tests then check it stays inside its footprint, reaches its floor count and has more than one look.

## Run the tests

`Window > General > Test Runner > EditMode > Run All`. They cover the road graph, zoning rules, growth, bulldozing,
the simulation clock, and a randomised "invariants" test that checks the rules above hold after hundreds of random edits.

## How the code is organised

```
Assets/Scripts/Simulation   Pure C#, no Unity types. The city model.
  CitySimulation            Commands (PlaceRoad, PaintZone, Bulldoze), growth tick, events
  RoadNetwork / RoadSegment Road graph: each road cell is a node, neighbours are joined by segments
  CityGrid, GridCoord, ...  Cell data and helpers
  TickClock                 Real time -> simulation ticks, pause and speed
  BuildingCatalog/Template  Which building types exist per zone, their size, height range and rarity
  BuildingDesigner          Turns a building into boxes, cylinders and roofs (pure data, no Unity)

Assets/Scripts/Game         Unity layer. Draws the model and turns input into commands.
  GameBootstrap, CityGame   Start-up; runs the clock and creates everything below
  CityRenderer              Ground texture, building models, tool preview overlay
  BuildingModelFactory      Builds a building's meshes from its design (the place to plug in Blender models later)
  ProceduralMeshes          Pyramid and pitched-roof meshes
  ToolController            Mouse tools and previews
  CameraRig                 Camera
  HudToolbar                Toolbar (immediate-mode GUI, placeholder)

Assets/Tests/EditMode       Unit tests for the Simulation code
```

The Simulation assembly never references Unity, so it can be tested quickly and reused if the rendering is rebuilt
(for example with DOTS). The Game assembly only listens to events from the simulation and sends it commands.

## Where the roadworks twist plugs in

Every `RoadSegment` already has a `BaseCapacity` and an `OpenFraction` (1 = open, 0 = closed), and
`RoadNetwork.Neighbours(cell, onlyOpen)` can skip closed segments. A closure will lower `OpenFraction` on some segments
for a while, and traffic will route using `EffectiveCapacity`. No traffic exists yet; that is the next milestone.

## Known limits (on purpose, for now)

- One cell is 8 m wide and one floor is 3.5 m. Buildings are stacks of gray boxes, cylinders and roofs until real models arrive.
- Growth is a placeholder: random zoned cells fill up, with no demand, money, or population.
- Shadows are short-ranged because URP's default shadow distance is 50 m. Raise it in the URP asset when we polish visuals.
- Shaders are looked up by name, which is fine in the Editor. Before making a standalone build, make sure the URP
  Unlit/Lit shaders are included in the build.
