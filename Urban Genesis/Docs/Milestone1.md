# Milestone 1: grid, roads, zoning, growing buildings

## Try it

1. In Unity Hub, open the `Urban Genesis` folder (Unity **6000.3.5f2**). The first import takes a few minutes.
2. Open `Assets/Scenes/SampleScene` and press **Play**. Nothing needs wiring: `GameBootstrap` creates the city when Play starts.
3. Build a road, zone next to it, and watch gray cubes grow.

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

Assets/Scripts/Game         Unity layer. Draws the model and turns input into commands.
  GameBootstrap, CityGame   Start-up; runs the clock and creates everything below
  CityRenderer              Ground texture, building cubes, tool preview overlay
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

- One cell is 8 m wide, and buildings are one cell each, with cube height set by floor count.
- Growth is a placeholder: random zoned cells fill up, with no demand, money, or population.
- Shadows are short-ranged because URP's default shadow distance is 50 m. Raise it in the URP asset when we polish visuals.
- Shaders are looked up by name, which is fine in the Editor. Before making a standalone build, make sure the URP
  Unlit/Lit shaders are included in the build.
