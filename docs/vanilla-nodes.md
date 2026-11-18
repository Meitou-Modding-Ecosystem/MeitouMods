# Vanilla nodes (seeding for Logistics Core)

**Status: built, for Logistics Core (#59) to take over.** A small library, `LogisticsSeed` (namespace `Logistics.Seed`), that turns the
loaded game's placed towns into nodes: each with its stable id, its owner, and the owners the game's own town versions allow. It is the
"every node needs a list of possible owners" gap from #59, filled from vanilla data, so mods extend real lists rather than guess. Not a mod:
it moves into Logistics Core's repository when that exists. Owner: @aaallleeeccc.

## Use

```csharp
IReadOnlyList<VanillaNode> nodes = VanillaNodes.Build(populationData);   // at world build, from the loaded mod list
```

| Field | Meaning |
| --- | --- |
| `Id` | `town:<InstanceId>` (`TownSite.InstanceId`, e.g. `town:9-rebirth-INGAME`): stable across mod lists and town overrides |
| `Name`, `BaseRecord`, `Kind`, `Position` | The placed TOWN record's name and id, its `TownType`, where it stands |
| `Owner` | The FACTION string id holding it now: the current version's faction, else the base record's |
| `AllowedOwners` | Every FACTION string id that can hold it through the game's own versions, ordinal-sorted, no repeats |
| `Versions` | Each reachable version: its TOWN record, the faction holding the town while it shows, the world states it needs (`StateCondition`: a WORLD_EVENT_STATE id and value 1 or 0) and the versions it can switch to next |
| `Ownable` | False for nests, nest markers, points of interest and Null towns: places to clear, not hold |

## How owners are found

As the engine switches towns (`PopulationSystem`'s override choice, `TownSite.Replace`):
- versions are the TOWN records in `override town`, followed along each version's own list (the chain moves forward a step at a time), so
  a town's owners include every faction its versions can reach, not only the first step;
- a version naming no faction keeps the faction of the version it replaced (a "rebuilt" Stack stays with whoever took it);
- a version's conditions are its `world state` links that are real WORLD_EVENT_STATEs, as the engine counts them;
- a version reached twice is listed once, so chains that loop back to the base town end.

Owners added by other mods stay the framework's job (#59: a mod adds a faction with the town version it shows under; removing one is a
deliberate call). This list is the vanilla default they extend.

## On the base game's data

The base game (`gamedata.base`, `Newwworld.mod`, `Dialogue.mod`, `rebirth.mod`) has 346 TOWN records, 225 of them base towns. 154 are
ownable; 55 have versions. Per ownable town: 106 have one possible owner, 29 two, 12 three, 5 four, 2 none (no faction). For example:

| Town | Owner now | Allowed owners |
| --- | --- | --- |
| Stack | The Holy Nation | The Holy Nation, Shek Kingdom, Kral's Chosen |
| Okran's Shield | The Holy Nation | The Holy Nation, Shek Kingdom, Reavers, United Cities |
| Bad Teeth | The Holy Nation | The Holy Nation, Shek Kingdom, Kral's Chosen, United Cities |
| Squin | Shek Kingdom | Shek Kingdom, Berserkers, Kral's Chosen |
| Stoat | United Cities | United Cities, Tech Hunters, Yabuta Outlaws |

(Checked by running `VanillaNodes.Build` over the base game's TOWN records, each given a placement; a real game supplies the placements and
their instance ids from the level data.)

## Tests

`LogisticsSeed.Tests` (towns made in memory, no game install): every placed town with an instance id is a node keyed by it; owners follow
the override chain, a faction-less version keeps the holder, only real world states count, loops end; nests and points of interest are not
ownable; the same data gives the same nodes.
