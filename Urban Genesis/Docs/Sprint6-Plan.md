# Sprint 6 plan: next features, in order

Built from `Gaps-for-Sprint6.md` (what the user stories still need) and the first UX evaluation (Mohamed Gamal, 23 Nov 2026). Items marked **(verify)** are things the tester reported that may already work and just are not discoverable; they get checked first.

## Status (updated 27 Nov 2026)

| Phase | Branch | State |
|---|---|---|
| A: first-impression fixes | `feature/sprint-6` | done, except the zoom-out road bug (waiting for tester details) |
| B: information | `feature/sprint-6` | done |
| C: protect the player | `feature/sprint-6` | done (plus the Undo/Redo arrow icons) |
| D: balance | `feature/sprint-6-d` | done |
| E: more game | `feature/sprint-6-d` | minimap, random events, menus done; pollution, traffic-to-economy, day/night, sound, real assets still open |
| F: infrastructure and services | `feature/sprint-6-d` | done |
| G: menus (save slots, settings) | `feature/sprint-6-d` | done, compile-checked only |

`feature/sprint-6-d` holds everything in `feature/sprint-6` plus D to G. Test checklist for D to G: `Sprint6-D-TestChecklist.md`.

Order rule: fix what confuses a first-time player, then show information, then protect the player from mistakes, then balance, then add new content.

## Phase A: first-impression fixes (small, do first)

- [x] Date shows "15 May 2026": the start date is hard-coded. Start from today's real date (or a clearly intended in-game date). *(UX: first impressions)*
- [x] Demolishing homes does not lower the population: make the population react straight away, not at the next economy update. *(UX 10)*
- [ ] Building roads when zoomed out is blocked or unclear: let roads be planned at any zoom, or say why not. **(verify)** *(UX 1)*
- [x] Camera: add left-drag panning when no tool is active, and edge scrolling. Middle-drag panning already exists but was not found. *(UX 2)*
- [x] Controls help panel (F1 or "?") (key `H` or a "?" button) listing camera and tool controls. *(UX 2, 4)*
- [x] Pause and speed controls were not noticed (pause button turns amber while paused): make them more visible, check space and the buttons. **(verify)** *(UX first impressions)*
- [x] Why can't I repeat building a house (tool stays selected; status line now shows a hint per tool): check that the zoning tool stays selected after painting, and that the status line says what to do next. **(verify)** *(UX 4, 5, 7)*

## Phase B: information (the biggest gap in the stories and the UX report)

- [x] **Inspect tool** (key I or 9; panel shows capacity, jobs, workers, income, road load) (select a building, road or junction): residential shows capacity, residents, free places; industrial/commercial shows jobs, workers, free positions; road shows type, capacity, current load, remaining capacity; building shows its upkeep and income per day. *(story 9, #38; UX 9, 14)*
- [x] Costs everywhere (prices on road chips, hints, refund note): show the price of each road type in the road options, price of rebuilding when upgrading, refund when demolishing, and say plainly whether demolishing costs money. *(stories 1.2, 8; UX 9, 11, 14)*
- [x] Housing and jobs overview (Jobs metric in the HUD) in the HUD: housing capacity next to population, free jobs, a "needs more housing" hint. *(story 5, UX 9)*
- [x] Happiness indicator removed from the HUD (happiness is now a wishlist feature). *(UX 12)*
- [x] Explain milestones (progress line under the bar): show the milestone criteria and progress. *(UX 12)*
- [x] Road capacity and load (Inspect shows capacity; overlay on key O or the route button): show a road's capacity before building it, and colour roads by load (congestion overlay). *(stories 2, 7, 36)*
- [x] Explain road types versus upgrades (description in the status line): short description of each road type in the road options and in the Upgrade tool. *(UX first impressions)*
- [x] Financial summary (panel at the end of every day) (day end): starting budget, income, expenses, final budget; per-building income and expenses. *(story 8, #37)*

## Phase C: protect the player

- [x] Warn before destructive actions (second click to confirm for upgrades that demolish and for removing a road; Undo covers the rest): zoning over zoned lots or buildings, demolishing, upgrading a road that removes buildings (show count, ask to confirm). *(UX 13)*
- [x] **Undo** (and redo, Ctrl+Z / Ctrl+Y and HUD buttons) for building, zoning, demolishing and upgrading. *(UX 13)*
- [x] "Clear zoning" and "Demolish" with fill mode (Clear zoning chip in the zoning options, key 8, Fill side toggle) so a whole side can be removed in one click; make Clear zoning easy to find. **(verify)** *(UX 6)*
- [x] Low-money and expenses-above-income alerts. *(#39, #40)*

## Phase D: balance

- [x] Starting money and prices (start $100,000; a road is refused if the upkeep it adds would drain the treasury within 4 weeks; unpaid upkeep stops at $0 instead of going negative): the treasury goes negative after a handful of buildings. Rebalance starting funds, road prices, taxes and upkeep; stop spending that would leave the budget negative. *(UX 8)*
- [x] Rent and building upkeep: DECIDED by the product owner (2026-11-26): keep the tax model, no building upkeep or rent (it would take away from the fun). Update stories 5 and 6 to match. *(stories 5, 6)*

## Phase E: more game

- [x] Minimap in a corner (bottom left, click to jump). *(UX 3)*
- [x] Capacity check for new buildings, first version: the Inspect panel shows how full the road in front of a building is and warns when it is jammed. *(story 7, #36)*
- [ ] Pollution / environment indicator: wishlist, along with happiness. *(UX first impressions)*
- [ ] Traffic feeds back into the economy (congestion changes travel cost and growth).
- [x] Random events with road closure (#11, #9): an accident closes a street for a day, shown in red (fires are in Phase F). [x] Milestone reward (#14): first a money bonus, now replaced by the unlocks of Phase F. [ ] Day/night cycle (#12): postponed, the ground and roads use unlit materials, so it needs the new assets and materials first.
- [ ] Sound effects and music (#16, #21, #41).
- [ ] Real models and textures (#4, #5, #13, #25).
- [x] Infrastructure (#17, epic): an **electricity** network and a **water and sewage** network, see Phase F.
- [x] Menus: five save slots (see Phase G), settings screen.

## Phase F: infrastructure and services (decided by the product owner, 2026-11-26)

- [x] River on every new map (roads cannot cross it or run along its banks; water tower and sewage outlet must stand within 85 m of it).
- [x] Power plant (on a free lot area beside a road); water tower and sewage outlet (always available) stand anywhere on the river bank, within 85 m of it, no road needed.
- [x] The highway to the outside world is never closed by pipes (they pass under it).
- [x] Cables: laid along a whole road; the road is closed for 3 days while crews work (orange on the map); drawn yellow on the road.
- [x] Water pipes: drawn freely across the land (click, click, carries on until Esc/right-click), blue lines. Only roads a pipe runs under are closed (3 days). Pipes that cross or touch are joined, ends snap onto other pipes. Bulldozer digs a pipe up. A cable laid on a road already closed for roadworks only extends the closure to 3 days from now.
- [x] Services: a building is served when its road has a power cable connected (through junctions) to a power plant, and a pipe network joined to a water tower and a sewage outlet runs under or within 8 m of its road.
- [x] Once the first milestone (100 residents) is passed, new buildings only grow on served roads and unserved buildings earn nothing (the Inspect panel and the advice line say what is missing).
- [x] Milestone rewards replace the money bonus: 100 hospital, 1,000 police station, 5,000 fire station and dense residential zone, 10,000 school.
- [x] Service vehicles in the traffic: ambulances, police cars, school buses; fire trucks race to fires.
- [x] Fire as a random event: with a fire station the fire is put out, without one the building burns down.
- [x] Dense residential zone (apartment blocks and high-rises).
- Not decided yet: whether services should affect anything besides traffic (for example hospitals and population growth), and whether demolishing a civic building should refund money (now it does not).
- Old saves: a save made before this phase that already has 100+ residents will switch to "services required" at once and its buildings will sit idle until cables and pipes are laid. Saves made while pipes were still tied to roads lose those pipes.

## Phase G: menus (save slots and settings)

- [x] Five save slots. Main menu: Continue (the most recently saved slot), Load game (slot list, minus button deletes after a second click), New city, Settings, Quit. Pause menu: Save (to the city's slot), Save to another slot, Settings, Main menu; the toolbar save button saves straight to the city's slot, or opens the slot list for a new city. An old single-slot save appears in slot 1.
- [x] Settings (kept per user, apply at once): edge scrolling on/off, camera speed 50-200%, interface size 80-150%, reset to defaults.
- Not tested in the engine yet: the menus are UI built in code and have only been compile-checked.

## Still open

- Roads cannot be built when zoomed out (Phase A, needs details from the tester).
- Traffic feeds back into the economy (Phase E).
- Pollution indicator, happiness (wishlist).
- Day/night cycle, sound and music, real models and textures: waiting for a way to hand over asset files (Git LFS setup postponed).
- Phase F design questions above (service effects, refund for civic buildings).
