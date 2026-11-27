# Test checklist: branch `feature/sprint-6-d` (phases D to G)

Everything in `feature/sprint-6` (phases A to C) is in this branch too, so test that checklist as well. This one covers what was added after it. Tick a box when the behaviour matches; write down anything that does not.

Everything you pay for gives half its price back when you demolish it (roads with their cable, pipes, civic buildings). Hospital covers 3,000 residents, school 4,000.

Handy facts: start money $100,000. Cable $10 per metre, pipe $14 per metre. Roadworks close a road for 3 days (orange). Power plant $15,000, water tower $6,000, sewage outlet $5,000, hospital $12,000, police station $8,000, fire station $8,000, school $10,000.

## D. Balance

- [ ] A new city starts with $100,000.
- [ ] Build a long road: when the upkeep it adds would drain the treasury within 4 weeks, the road is refused with a message that says so. A shorter road is accepted.
- [ ] Let the treasury run low with many roads and few buildings: money stops at $0 and never goes negative.
- [ ] Zoning and buildings are free; only roads, cables, pipes and civic buildings cost money.

## E. Minimap, events, milestones

- [ ] Minimap in the bottom-left corner shows roads, the river and the buildings; clicking it jumps the camera there.
- [ ] Road fullness: the Inspect panel on a building shows how full the road in front of it is and warns when it is jammed.
- [ ] Random accident: after a few days a street is closed for a day, shown red, with a message in the event notices. It reopens by itself.
- [ ] Reaching 100, 1,000, 5,000 and 10,000 residents shows a milestone message that names what it unlocks. No money is paid out any more.
- [ ] The milestone detail in the HUD shows the reward as well as the target.

Traffic feeds the economy
- [ ] Build homes at one end of a long road and factories at the other, and let the city run. Open the traffic overlay (O): busy roads turn red.
- [ ] Inspect a very busy road: it has a line "Jams: trips through here take N% longer, which counts against the homes, jobs and shops behind it". An empty road has no such line.
- [ ] When the average trip is 25% slower or worse the advice line says "Traffic jams make the average trip N% slower..." and suggests upgrading the busiest roads or adding routes.
- [ ] Upgrading the jammed road (or adding a second route) and waiting a little makes the advice disappear and employment/income recover.
- [ ] A small, quiet city is not affected (no jam text, income as before).

## F. River, infrastructure, services

River
- [ ] Every new city has a river across the map. Roads cannot cross it or run along its banks (message mentions the river).
- [ ] Undo/redo keep the river.

Power
- [ ] Power tool (Power button, zap icon): click a road to lay a cable along it. It costs money, the road turns orange for 3 days, then reopens and shows a yellow line.
- [ ] Place a power plant on a free area beside a road (Power group). It is only useful once a cable on its road connects through junctions to the roads you want to serve.
- [ ] Laying a cable on a road that already has one is refused.

Water, sewage (changed after your last feedback)
- [ ] Water tower and sewage outlet can be placed on the river bank, within 85 m of the river, with no road nearby. Too far, in the water, or on top of something gives a clear message.
- [ ] Water pipe tool (Water button, droplets icon): click to start, move to see a preview with length and cost, click to lay the pipe and carry on from its end. Right-click or Esc stops.
- [ ] A pipe that crosses no road closes nothing. A pipe that crosses a road closes only that road for 3 days; roads it will close are shown orange in the preview.
- [ ] A pipe passes under the highway to the outside world without closing it.
- [ ] Pipe ends snap onto other pipes. Two pipes that cross are joined (water carries through the crossing).
- [ ] Pipes are shown as blue lines on the ground, also where there is no road.
- [ ] A pipe network that is not touching the tower (or outlet) carries nothing: the Inspect panel of the tower says "Not connected".
- [ ] Demolish tool: clicking on a pipe digs it up (preview shows it highlighted, half its price comes back). The tower and outlet can be demolished too.
- [ ] Laying a cable on a road that is already closed for roadworks (for example from a pipe laid two days ago) is allowed and extends the closure to 3 days from now; the road reopens only after that. A road closed by an accident cannot get a cable or pipe until it reopens.

Services requirement
- [ ] Before 100 residents buildings grow without any services.
- [ ] After the first milestone the message says new buildings need power, water and sewage. New buildings then only grow on roads that have power, water (pipe under or within 8 m of the road) and sewage.
- [ ] A building without services says "NOT SERVED" in the Inspect panel and lists what is missing; it earns nothing. The advice line mentions missing services.
- [ ] After you connect the missing service, the building starts working again.

Milestone buildings (Services group; locked entries say "(locked)")
- [ ] Hospital unlocks at 100, police station at 1,000, fire station and dense residential zone at 5,000, school at 10,000. Clicking a locked one explains this.
- [ ] They are placed on a free area beside a road and cost money; demolishing gives half the price back (the Inspect panel says how much).
- [ ] Hospital: the Inspect panel says how many of the city's residents it looks after. With a hospital, more people want to move in (residential demand rises a bit, more homes grow). One hospital covers 3,000 residents; a bigger city needs more.
- [ ] School: same coverage line (4,000 residents). With a school the number of working residents, and so filled jobs and tax income, goes up by up to 20%.
- [ ] Their vehicles appear in the traffic: ambulances, police cars, school buses.
- [ ] Fire: a random fire starts after the first days. With a fire station a fire truck drives to it and the fire goes out; without one the building burns down. The fire is shown with a flickering overlay and a message.

Dense residential
- [ ] After 5,000 residents the "Dense residential" chip in the zoning options works (before it is greyed and explains the unlock). It grows apartment blocks and high-rises.
- [ ] Dense buildings need services like other homes and count as residents.

## G. Menus

Save slots
- [ ] Main menu with no saves: Continue and Load game are greyed out.
- [ ] Pause menu (Esc or city name) without a save: "Save game" opens a list of five slots. Saving into slot 2 works and shows "Game saved to slot 2".
- [ ] Afterwards the pause menu shows "Save (slot 2)" and "Save to another slot". The toolbar save button saves straight into slot 2.
- [ ] Saving over a used slot overwrites it. The list shows city name, residents and time.
- [ ] Main menu: Continue loads the most recently saved slot; Load game shows the list and loads the slot you click. Cities in different slots stay separate.
- [ ] The minus button beside a used slot deletes it only after a second click (note says so).
- [ ] "Save and exit" on a never-saved city asks for a slot first, then returns to the main menu.
- [ ] A save from before the slots existed shows up in slot 1.
- [ ] A save keeps the river, pipes, cables, roadworks in progress, tower, outlet and civic buildings.

Settings (main menu and pause menu)
- [ ] Edge scrolling off: pushing the mouse to the window edge no longer moves the camera; on: it does.
- [ ] Camera speed buttons change keyboard and edge scrolling speed (50% to 200%).
- [ ] Interface size buttons resize the toolbar and menus at once (80% to 150%); at 150% nothing falls off the screen.
- [ ] Reset to defaults sets 100% / 100% / edge scrolling on.
- [ ] The settings stay after closing and restarting the game.
- [ ] Slot rows and the − / + buttons look right (not cut off or cramped).

## Refunds

- [ ] Demolishing a road that has a power cable gives back half of the road and half of the cable (the road's Inspect panel shows the sum). The day summary lists the demolition refunds.
- [ ] Demolishing a pipe, a hospital/police/fire/school/power plant/tower/outlet gives back half of its price.
- [ ] Upgrading a road or building a new road over such a building also refunds it. A building that burns down gives nothing.

## UI clean-up

- [ ] Top right has no weather/temperature any more; the session buttons (undo, redo, traffic overlay, save, ?, settings, menu) fit without being squeezed, and the settings button opens the settings screen.
- [ ] No notifications button, no placeholder panel on the left (layers, statistics, policies, map) and no greyed-out Transit, Parks or Terrain tools in the bottom toolbar.

## Other

- [ ] Undo and redo are arrow icons in the toolbar and still work (Ctrl+Z, Ctrl+Y), also after laying pipes and cables.
- [ ] Zoomed-out roads: try building a road far zoomed out and note what happens (open bug, details needed for the tester report).

## Known limitations (not bugs)

- Jams are measured from the vehicles you see (a sample of the real trips), so a very big city does not get proportionally worse jams.
- Police and fire stations only add vehicles and fire fighting; hospitals and schools have the effects above.
- Old saves with 100+ residents will have idle buildings until cables and pipes are laid; saves made when pipes were tied to roads lose their pipes.
- No sound, day/night cycle or real models yet.
