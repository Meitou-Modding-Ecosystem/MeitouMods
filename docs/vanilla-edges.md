# Vanilla edges (seeding for Logistics Core)

**Status: built, for Logistics Core (#59) to take over.** A small library, `LogisticsEdges` (namespace `Logistics.Edges`), that turns the
game's own roads into edges between the vanilla nodes (docs/vanilla-nodes.md): which places a road joins directly, and what the trip costs.
It is #59's "derive edges from `RoadNetwork.Route` lengths once per world and cache them" step, so the graph Logistics Core moves goods,
caravans and squads along is the one the game's own travellers use. Not a mod: it moves into Logistics Core's repository when that exists.
Owner: @PZA-Mods-bot.

## Use

```csharp
IReadOnlyList<VanillaEdge> edges = VanillaEdges.Build(populationData);   // once per world, from the placed towns and PopulationData.Roads
```

| Field | Meaning |
| --- | --- |
| `From`, `To` | Node ids, `town:<InstanceId>` as vanilla node seeding gives them; `From` sorts before `To` (ordinal), each pair once |
| `Cost` | The length walked from one place's centre to the other's along the route (world units, X/Z) |
| `Kind` | `Road`: the game's road route. `Near`: close enough that the game walks straight (`RoadNetwork.MinTrip` or less). `Overland`: the fallback that joins an island to the rest |

The list is sorted by `From`, then `To`, and depends only on the places and roads, not on their order. Nothing goes in the save: the same
world gives the same edges, so build them at world start and cache them, as #59 says, until the engine exposes its own road graph.

`VanillaEdges.Build(places, roads, options)` takes the places directly (`EdgePlace`: id, X/Z position, town radius, ownable), for a mod that
adds its own nodes; `VanillaEdges.Places(populationData)` gives the vanilla ones.

## How edges are found

- Only ownable places get edges (the same rule as vanilla node seeding: nests, nest markers, points of interest and Null towns are places to
  clear, not hold). They do not block anything either.
- Each place tries its 6 nearest places (`EdgeOptions.Neighbours`); a pair is tried once, whichever side picked it.
- A pair `MinTrip` apart or closer is a `Near` edge: the game itself walks that straight.
- Otherwise the pair asks the engine's own router, `RoadNetwork.Route(a, b, 1)` (full road preference, a caravan's), both ways, since the
  joins and the search can differ by direction. The cheaper answer counts; no road route means no edge.
- A trip that passes through a third place's town radius on the way is not an edge: it is two edges, through that place. A third place
  whose circle overlaps one end's circle counts as part of that end (a town and its outskirts), so it never cuts a town off.
- Islands last: while some group of places is not joined to the rest, the smallest group joins its nearest place outside it with an
  `Overland` edge (straight). The result is one graph where every place can reach every other (`EdgeOptions.Overland = false` turns this off).

## On the base game's map

Built from the base game's level data (`WorldLevelData.LoadBaseGame`, `levels.Towns()`, `levels.Roads()`): 304 placed towns, 226 of them
ownable, 2,109 roads. **618 edges** in about 0.35 s: 542 `Road`, 73 `Near`, 3 `Overland`, all 226 places in one graph. Road trips run from
11,752 to 279,222 world units, median 61,728. The three overland joins are Island Lab to Fish Isle, Post-Ancient Workshop to Western Hive
and Abandoned Town to Library Ruins, the places no road reaches. For example:

| Place | Its edges |
| --- | --- |
| The Hub | Squin, Stack, Holy Mines, Waystation, Ruined Holy Outpost, Hive Village (all `Road`) |
| Heng | Trader's Edge (`Near`), Slave Farm, South Stone Camp, Tengu's Vault, Stoat, Eyesocket |
| Stoat | Stone Camp, Trader's Edge, Bast, Tengu's Vault, Drin, Heng |
| Shark | Mud Town, Swamp Village, Lost Library Outpost (`Near`), Swamped Lab, Grayflayer Village, Stone Rat Village, Infested Lab, a second Swamp Village |
| Bark | Port South, Old Village, Fort Mirage, Heft, Sho-Battai, Port North |

## Open points

- **Mods' towns:** a mod that places new towns gets edges for them from the same call, as long as its towns are in `PopulationData`. Roads a
  mod adds count once they are in the level data the engine reads.
- **The engine's own graph:** if the engine later exposes its road graph (junctions and roads) publicly, the edges can come from it
  directly; the ids and costs here would stay the same kind of thing.
- **Costs are lengths only.** Danger, tolls and terrain are Logistics Core's or a policy mod's to add on top (Outlaws and Tolls, Dustlife).

## Tests

`LogisticsEdges.Tests` (places and roads made in memory, no game install): a road between two towns is one `Road` edge costing its length;
places within `MinTrip` are `Near`; a route through a third town is two edges, not one; nests get no edges and never block; a place in a
town's outskirts does not cut it off; a place no road reaches, and a pair of islands, join the rest `Overland`; `Overland` can be turned
off; `Neighbours` limits the pairs tried; the input order never changes the result; two places with one id are refused.
