# Sprint 6 plan: next features, in order

Built from `Gaps-for-Sprint6.md` (what the user stories still need) and the first UX evaluation (Mohamed Gamal, 23 Nov 2026). Items marked **(verify)** are things the tester reported that may already work and just are not discoverable; they get checked first.

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

- [ ] **Inspect tool** (select a building, road or junction): residential shows capacity, residents, free places; industrial/commercial shows jobs, workers, free positions; road shows type, capacity, current load, remaining capacity; building shows its upkeep and income per day. *(story 9, #38; UX 9, 14)*
- [ ] Costs everywhere: show the price of each road type in the road options, price of rebuilding when upgrading, refund when demolishing, and say plainly whether demolishing costs money. *(stories 1.2, 8; UX 9, 11, 14)*
- [ ] Housing and jobs overview in the HUD: housing capacity next to population, free jobs, a "needs more housing" hint. *(story 5, UX 9)*
- [x] Happiness indicator removed from the HUD (happiness is now a wishlist feature). *(UX 12)*
- [ ] Explain milestones: show the milestone criteria and progress. *(UX 12)*
- [ ] Road capacity and load: show a road's capacity before building it, and colour roads by load (congestion overlay). *(stories 2, 7, 36)*
- [ ] Explain road types versus upgrades: short description of each road type in the road options and in the Upgrade tool. *(UX first impressions)*
- [ ] Financial summary (day end): starting budget, income, expenses, final budget; per-building income and expenses. *(story 8, #37)*

## Phase C: protect the player

- [ ] Warn before destructive actions: zoning over zoned lots or buildings, demolishing, upgrading a road that removes buildings (show count, ask to confirm). *(UX 13)*
- [ ] **Undo** (and redo) for building, zoning, demolishing and upgrading. *(UX 13)*
- [ ] "Clear zoning" and "Demolish" with fill mode so a whole side can be removed in one click; make Clear zoning easy to find. **(verify)** *(UX 6)*
- [ ] Low-money and expenses-above-income alerts. *(#39, #40)*

## Phase D: balance

- [ ] Starting money and prices: the treasury goes negative after a handful of buildings. Rebalance starting funds, road prices, taxes and upkeep; stop spending that would leave the budget negative. *(UX 8)*
- [ ] Rent and building upkeep per the stories (or update the stories to the tax model; decision pending). *(stories 5, 6)*

## Phase E: more game

- [ ] Minimap in a corner. *(UX 3)*
- [ ] Capacity check for new buildings: estimated traffic they add and a congestion warning. *(story 7, #36)*
- [ ] Pollution / environment indicator: wishlist, along with happiness. *(UX first impressions)*
- [ ] Traffic feeds back into the economy (congestion changes travel cost and growth).
- [ ] Day/night cycle (#12), random events with road closure (#11, #9), first milestone reward (#14).
- [ ] Sound effects and music (#16, #21, #41).
- [ ] Real models and textures (#4, #5, #13, #25).
- [ ] Infrastructure (#17, epic): for now this means an **electricity** network and a **water and sewage** network (details to settle before building).
- [ ] Menus: several save slots, settings screen.
