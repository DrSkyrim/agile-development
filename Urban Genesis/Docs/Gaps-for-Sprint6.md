# Gaps checklist: what is done and what is left after Sprint 5

Compared against the user stories document (stories 1 to 9 plus the epics) and the GitHub issues (#1 to #41, read by title only). Written 2026-11-16 at the end of Sprint 5. Sprint 6 plan: close these gaps, add real assets, add infrastructure.

## Where the game differs from the stories (decide first)

- [ ] Buildings grow from zoning; the player does not place single buildings. Stories 3, 4 and 5 (choose a building, building construction cost, "cannot afford this building") do not match this. Decide: change the stories, or add a "place a building" mode.
- [ ] Income is a weekly tax per employed worker and per shopper served, minus road upkeep. The stories ask for rent per occupied unit, annual building maintenance and revenue per building.
- [x] The budget settles once per game day (story 8 asks for this).

## Done or mostly done

- [x] 1.1 View the budget (#28): budget and change in the HUD, updated each day.
- [x] 1.2 Check construction costs (#29): road and upgrade previews show the cost. Roads only, not buildings.
- [x] 1.3 Prevent unaffordable construction (#30): roads and upgrades are refused without money, with a message. Roads only.
- [x] 2 Build roads (#31): pick start and end, cost deducted, longer roads cost less per metre.
- [x] 5 (part) Population shown in the HUD.
- [x] 6 (part) Workers and shoppers generate income that goes into the budget.
- [x] Vehicles (#10, #13): cars, short and long trucks, routing, lights, lane rules. Issues look ready to close or update.
- [x] Zoning and roads (#7, #23): zone brushes, fill mode, clear zoning, road tools. Issues look ready to update.
- [x] Road upgrades (the Upgrade tool). Building upgrades do not exist.

## Partial

- [ ] Road capacity (stories 2, 7, 9; #36, #38): roads have a capacity in the simulation and the economy uses it, but nothing shows it: no capacity before building, no load, no remaining capacity.
- [ ] Road closure (#9): the simulation can close roads and vehicles avoid them, but neither the player nor events can close one.
- [ ] Money and population (#8): only totals are shown in the HUD.
- [ ] Game progress: the HUD has a "milestone target" label; check what is behind it.

## Not done

- [ ] Select a building or road and see capacity, residents, workers and load (story 9, #38). Suggested first task: a selection and info panel; it also covers parts of stories 5 and 7.
- [ ] Financial summary: starting budget, income, expenses, final budget (story 8, #37).
- [ ] Expected daily income or expenses per building (story 8, story 6).
- [ ] Low-money and expenses-above-income alerts (#39, #40).
- [ ] Capacity check for new buildings: estimate the traffic a new building adds and warn about congestion (story 7, #36).
- [ ] Tell the player when more housing is needed and show housing capacity next to population (story 5).
- [ ] Day/night cycle (#12).
- [ ] Random events (#11), with road closure folded into them.
- [ ] First milestone reward (#14).
- [ ] Sound effects and music (#16, #21, #41).
- [ ] Real models and textures: buildings (#4, #25), road, vehicles (#13), textures (#5). Vehicles have a prefab hook at `Assets/Resources/Vehicles/{Car,ShortTruck,LongTruck}`.
- [ ] Infrastructure and services (#17, epic "Infrastructure feature").

## Traffic and economy follow-ups

- [ ] Traffic feeds back into the economy: congestion changes travel cost, trip time and growth; routing uses that cost.
- [ ] Trips start and end at real buildings, not a random spot on the building's road.
- [ ] Trip counts follow real demand; today they are capped at 0.5 vehicles per building (`CityTraffic.VehiclesPerBuilding`) and spread over the economy's flows.
- [ ] Heavy roads can jam badly at 0.5 per building (a single-avenue test city dropped to about 2 m/s). Idea: let trips taper off while roads are jammed.
- [ ] Left turns do not yield to oncoming traffic at lit junctions.
- [ ] Vehicles are not saved; public transport and parking do not exist.
- [ ] Balance demand, growth, taxes and costs after playtesting.

## Menus and UI

- [ ] More than one save slot.
- [ ] Settings screen (audio, graphics, controls).
- [ ] Traffic UI: vehicle count, congestion overlay.
- [ ] Other polish found while testing.
