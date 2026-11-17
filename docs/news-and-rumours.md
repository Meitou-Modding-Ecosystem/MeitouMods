# News and rumours (shared framework)

**Status: proposed for #57, not built.** A small shared mod that carries news across the map: what happened, where, and who has heard
of it yet. Mods report events; the framework decides how far word has travelled and how sure each telling is; a presentation mod (The
Wasteland Remembers today) turns it into talk. Owner if agreed: @aaallleeeccc.

## The pitch

Today news is everywhere at once or nowhere. A town falls at Okran's Shield and, in TWR, every speaker within range of it knows at
that instant, while a bar in Squin never hears of it at all. With this framework word **travels**: the Shield's own people talk about it
that evening, travellers bring it to Stack the next day, it reaches the Hub as a rumour a few days later, and somewhere on the far side of
the map it arrives garbled or not at all. The squad hears it where it is, from whoever has heard.

- One record per event, whoever reports it (no town announcing the same fall three times).
- Word moves outward at a set pace, along roads once PZA's place graph lands (#55).
- Each town knows what has reached it, and how sure it is.
- Mods can ask "has Stack heard yet?" and react when word arrives, not when the thing happened.

## What exists today

| Piece | Where | What it does |
| --- | --- | --- |
| `WastelandApi.Report(kind, place, faction, text, weight, source)` | TWR, API version 2 (#23) | Any mod reports an event. The same kind at the same place within 12 game hours is taken once. |
| TWR's news board | TWR (`NewsBoard.cs`, `News.cs`) | Keeps reports 10 game days. Speakers within the "Outlaw news range" of the place (8 km by default, the map is about 30 km across) talk about the three weightiest from the last 7 days. The squad hears a piece when it first comes within range: a History line, the Atlas's News section, a Recap moment, a message at weight 60+. **No travel time.** |
| Dustlife's event kinds | `docs/dustlife.md` | `town-fell`, `siege-began`, `siege-broken`, `raid-sent`, `garrison-grew`, `town-short`, `caravan-lost`, `warlord-fell`, `warlord-rose`, each with a line of its own in TWR. |
| The place graph | #55 | Places as nodes, the game's roads as edges, with lengths. Lists "the road from X to Y is cut" and "Z is cut off" as events for TWR to tell. |
| Off-screen town overrides | #56 | Towns switch to their new version when it happens, not when the player arrives: the moment news should start from. |

## Model

**Event:** what happened. Kind, place (a town name, or a map point with the nearest town), faction (optional), weight 0 to 100, game
hour, reporting mod, optional text. Same kind and place within 12 game hours is one event (today's rule, kept). The strongest weight
reported wins; the first report's hour stands.

**Word:** an event's spread. For each event, the framework knows how far word has got: a radius from the place without the road graph,
or a distance along each road with it. A town has heard once word reaches it.

**Telling:** how a town knows it, decided when word arrives and kept:

| Telling | When | How it sounds |
| --- | --- | --- |
| Witnessed | the town is the place, or within the "seen from here" range (default 1.5 km) | "Okran's Shield has fallen." |
| Told | word arrived by road or traveller | "They say the Shield's fallen to the United Cities." |
| Rumour | further than the "told" range (default 12 km), or older than a few days when it arrives | "Rumour has it the Holy Nation lost a town up north." |

The telling is data for the presentation mod: which line to pick is its business. A rumour can drop details (the faction, the exact
place) but never invents one, unless the optional distortion below is on.

## Spread

- **Pace:** word travels at the off-screen travel speed Rivalry Core already uses for hit squads (2,727 world units, about 270 m, per game
  hour): across the whole map (about 300,000 units) in roughly four and a half game days. Weight speeds it up: weight 80+ moves at
  double pace (everyone talks about a fallen capital), weight under 30 at half pace and stops after the "told" range.
- **Without the place graph:** a circle growing from the place. Mountains don't slow it; acceptable for a first version.
- **With the place graph (#55):** word follows edges, weighted by road length, as caravans and travellers do. A **cut** edge stops it;
  a **cut-off** place hears nothing new until the edge reopens, and its own news can't get out. That is a sieged town's silence, for free.
- **Carriers (later):** a caravan arriving on an edge brings everything its starting place had heard (the economy's caravans, #46, #59).
  A town crier relays word one extra edge per day.
- **Fades:** an event stops spreading after its "live" time (7 game days today, longer for weight 80+) and is forgotten after 10, as now.

## Who has heard what

- Each town keeps the events that have reached it, with their telling and arrival hour: at most a fixed number per town (24),
  weightiest kept.
- **The squad** hears an event when one of its people is in a town that has heard it, or within the "seen from here" range of the place
  itself. That replaces today's "within range of the place" rule, so the squad can learn of a far event in a town that has heard, and
  can walk past a place without knowing what happened there days ago if nobody nearby has heard yet.
- Hearing is recorded once per event, so History, the Atlas and Recap get it once.

## Surfaces

For mods that report (unchanged for those already calling TWR):

- `Report(kind, place, faction, text, weight, source)`: as today. With the framework installed, TWR forwards to it, so no sender changes.

For mods that read or react (new):

- `Heard(town)`: the events that town has heard, with telling and arrival hour.
- `HasHeard(town, kind, place)`: a quick yes/no.
- `SquadHeard()`: what the squad knows.
- An arrival hook: "word of event N reached town T". Rivalry Core could let a faction react only once its towns have heard of a deed;
  Dustlife could let a garrison panic when it hears a neighbour fell.

Plain types throughout, nothing throws, as TWR's API does.

## Presentation

| Who | Shows |
| --- | --- |
| The framework alone | One plain message when the squad hears an event, so it is usable without TWR. |
| TWR, when loaded | Criers, bar talk, rumours and lookouts near the towns that have heard; History, the Atlas's News section, Recap. Its own lines per kind and telling. |
| Other mods | Anything they like from the reads above. |

## The player's own deeds (later, and the biggest change)

TWR's titles and town heat could travel the same way: kill a Shek warlord and the nearby towns know at once, Admag hears by road two days
later, and the far south has never heard of you. Today a title is known everywhere at once, and a town's sister towns feel half its
heat at once. Moving those onto the framework turns fame into something that spreads, which is much of what TWR is about. It touches a
lot of TWR, so it comes after the framework has been played.

## Optional: distortion

A setting, off by default. A rumour (only a rumour) has a small chance of one wrong detail when it arrives: the wrong faction, or a
neighbouring town. The event itself stays true for every mod that asks; only the telling is wrong. Never on events that drive gameplay in
another mod.

## Saving and determinism

- Events, each event's spread, and each town's heard list are saved with the game and in the world's hash.
- Randomness (distortion, carrier timing) from the engine's hashed RNG with fixed keys (event id, town), as the engine's determinism
  contract asks. No wall-clock time.
- Bounded: at most 40 live events (today's cap), 24 per town, so saves and hashes stay small.

## Phases

1. **Inside TWR first.** Travel delay and tellings in TWR's own news board, circle spread, behind a setting. Unit-tested engine-free
   rules, world tests for the squad hearing in a town that has heard. Proves the idea in play before anyone depends on it.
2. **The shared mod.** The same rules moved into a small mod here, with `Report` forwarding from TWR and the reads and arrival hook above.
   TWR becomes its presentation.
3. **Roads.** Spread along the place graph once #55 is built: cut edges and cut-off places.
4. **Carriers and fame.** Caravans carry word (with the economy work); titles and heat travel (TWR).

## How we'd know it works

- **Unit tests:** an event reaches a town at distance d after d / pace hours; weight changes the pace as specified; a cut edge stops it;
  the same kind and place twice is one event; a town keeps the weightiest 24; distortion never changes the event itself.
- **World tests:** the squad in a town that has heard gets one History line; the squad at the place of an old event nobody nearby has
  heard does not.
- **In play:** report a siege at Okran's Shield (TWR's F10 "test news", in its play-test builds), stand in Stack: nothing that day, a "told" line the next;
  travel to the Hub a few days later: a rumour. Wrong if Stack hears at once, or the Hub before Stack.

## Open questions

- Pace: is 270 m per game hour right, or should word outrun hit squads (riders, birds)?
- Should a faction's own towns hear its news faster (couriers) than strangers do?
- Does each mod want to set its own event's pace, or is weight enough?
- Where should "the squad heard it" live: here, or in each presentation mod?
- How does it look on a map without roads between some places (islands, the far south)?
