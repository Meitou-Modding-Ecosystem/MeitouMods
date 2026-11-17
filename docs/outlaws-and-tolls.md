# Outlaws, hideouts and tolls

**Status: proposed for #59, not built.** How The Wasteland Remembers' named outlaws could be the bandit hideouts of the war and economy
layer (#59, #29, #46), and a design for bandit tolls on the roads. TWR keeps the outlaws; the war layer counts what they cost. Owner of the
TWR side: @aaallleeeccc.

## The pitch

The war simulation wants bandit hideouts as nodes that make life worse for the places around them. TWR already has 36 named outlaws with
camps, moods, bounties and gangs who answer when you hurt them. Rather than two sets of bandits that never meet, the war layer reads TWR's
outlaws as its hideouts: when Bad Pete is raiding, the roads near his ground cost the towns there; when the squad (or a garrison) breaks
his camp, the roads clear and the towns talk about it. Tolls are where the two meet the player: Bad Pete's people on the road asking for
cats.

## What TWR's outlaws are today

| Part | What it does | Setting (default) |
| --- | --- | --- |
| 36 named outlaws | Each with a character, a gang squad, home regions, a contract and a bounty. | always on |
| Mood | Each living outlaw is always **raiding**, **lying low**, **gathering men** or **licking wounds**, a new stretch every 2 to 3 game days, picked from the world seed and the date. Towns gossip about it within the "Outlaw news range" (8 km by default). | always on |
| Struck | When the squad bloodies an outlaw's people, the outlaw lies low (or licks wounds, if badly hurt) for a while. | always on |
| Reprisals | An outlaw whose people the squad hurt, and who lived, sends men after the squad 2 to 4 days later. | on |
| Growing bounties | A tenth more per spell of raiding, up to double; contracts pay the growth. | on |
| Successors | A few days after an outlaw dies or is jailed, one of the gang takes over the same ground under a new name. | on |
| Moving camps | A real camp squad that sits near towns while raiding and in a far corner while lying low; bigger after gathering men, smaller while licking wounds. | **off (test)**, not play-tested |

So every outlaw always has a mood and home ground; a live camp position exists only with moving camps on.

## Who owns what

| Thing | Owner | Why |
| --- | --- | --- |
| Who the outlaws are, their names, gangs, successors | TWR | Its records, dialogue and contracts depend on them. |
| Where a camp is, and its mood | TWR | Already simulated; one source of truth. |
| What an outlaw does to the squad (reprisals, tolls, bounties) | TWR | Player-facing encounters. |
| What a hideout costs a node or a road (stock lost, trade cut, unrest) | War / economy layer | Its numbers, its balance runner. |
| Bandit nests with no name (generic raiders, fauna lairs) | War layer | Nothing for TWR to remember. |
| Talking about all of it | TWR (or the news framework, `docs/news-and-rumours.md`) | One voice. |

## What TWR would publish (API version 4, proposed)

Reads, by reflection like the rest of TWR's API, plain types:

- `Outlaws()`: one row per named outlaw: `Name`, `Faction`, `Status` (`alive`, `dead`, `jailed`, `succeeded`), `Successor` (name or
  empty), `Mood` (`raiding`, `lying-low`, `gathering`, `wounded`), `MoodUntil` (game hour), `Bounty`, `Regions` (region names), `CampX` /
  `CampZ` (when moving camps are on, else empty), `Strength` (the camp's planned size).
- `OutlawAt(x, z)`: the outlaw whose ground a point is in, or empty.

Things another mod tells TWR:

- `HideoutStruck(name, by, bloodied, source)`: a garrison or band broke an outlaw's camp. TWR treats it as it treats the squad striking:
  the outlaw lies low or licks wounds, the towns talk about who did it, and no reprisal comes after the squad (it wasn't them).
- `RoadBusy(from, to, weight, source)`: trade on a road grew (the economy's caravans). Raiding outlaws drift towards busy roads in their
  regions when their next stretch starts.

What the war layer would do with it, as one example:

- Each living outlaw is a **hideout node** on its home ground, linked to the nearest places by the place graph (#55).
- While the outlaw is **raiding**, the edges near its ground carry a cost (stock lost per caravan, a chance a caravan is taken).
  **Lying low:** none. **Gathering:** none now, more next stretch. **Wounded:** none.
- A dead outlaw with no successor yet leaves an empty node; a successor picks it up.

## Tolls

A toll is an outlaw's people holding a road and asking passers-by to pay.

**When:** an outlaw is raiding, a road (an edge of the place graph, or simply a stretch near a town before the graph exists) runs through
their ground, and no hostile party is already after the squad (Rivalry Core's cap, #24: a toll squad is a hostile party and claims the slot).

**For the player (TWR):**
- A small squad of the outlaw's gang stands across the road near a town. Their leader speaks as the squad comes close (`World.Bark`):
  "Road's ours today. Fifty a head and you walk on."
- **Pay:** a share of the squad's cats (scaled to squad size, capped), the gang lets them by. Townsfolk nearby mutter that you paid off
  bandits. Paying the same outlaw twice in a week: they ask more next time.
- **Refuse and win:** counts as striking the outlaw (they lie low, the bounty may grow, a reprisal may follow), and the town is grateful:
  a little goodwill (less heat) with the town the road serves.
- **Refuse and lose:** the gang takes the toll and something besides; the squad is left on the road.
- **Walk round:** fine. The toll stands for half a game day, then moves on.

**For trade (the economy layer):**
- TWR reports `toll` events (outlaw, road, until) through `Report` and the news framework. The economy counts the cost to caravans on
  that edge while it stands.
- Whether NPC caravans are stopped is the economy's call. TWR doesn't simulate caravans.

**What it needs from the engine:** nothing new. Spawning and holding a small squad by a road is what TWR's harassment raids and bounty
crews already do (`SpawnPlatoon` / `HoldPlatoon` once #27 lands, TWR's own stand-ins before).

## Phases

1. **Reads (TWR only).** `Outlaws()` and `OutlawAt(x, z)` in API version 4. Small, testable now, no agreement needed.
2. **Tolls for the player (TWR only).** Behind a setting, off by default, with the Rivalry Core claim. Play-tested before anything reads it.
3. **Hideouts in the war layer.** Dustlife reads `Outlaws()` and places hideout nodes; TWR takes `HideoutStruck`. Agreed here first.
4. **Tolls and trade.** The economy reads toll events once it has caravans on edges (#46, #55).

## How we'd know it works

- **Unit tests (TWR):** an outlaw's row matches its mood and status across a stretch change, a death and a successor; `OutlawAt` finds the
  right ground; a toll is offered only while raiding and only with the slot free; paying twice raises the price.
- **World tests (TWR):** a toll squad spawns by the road and claims Rivalry Core's slot; refusing and winning marks the outlaw struck.
- **In play:** with moving camps on, find a raiding outlaw's road near a town; the toll appears, paying lets the squad by, fighting
  makes the outlaw lie low and the town talk about it.
- **Balance runner (Dustlife):** with hideouts on, the places near raiding outlaws lose stock on the days they raid and recover when the
  outlaw lies low. Wrong if hideouts change nothing, or starve a town that has no outlaw near it.

## Open questions

- Should every Dustlife bandit nest be one of TWR's outlaws, or can there be nameless nests too (proposed: both, named ones from TWR)?
- Do tolls stop NPC caravans, or only the player?
- Should an outlaw's mood respond to the war (raiding more where garrisons are thin, lying low near a strong one)? TWR could read
  Dustlife's garrisons for that, if they're published.
- Should a successor inherit the toll roads, or pick new ones?
- Kenshi Ultimate Overhaul and other big world mods add their own bandits: do they count as nameless nests?
