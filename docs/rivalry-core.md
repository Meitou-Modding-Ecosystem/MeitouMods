# Rivalry Core (framework mod)

**Status: proposed, mod version 0.2.0, code-mod API 2.** A code mod in `RivalryCore` (id `rivalry.core`) that lets the world's factions
inspect the player's setup and act on it: hunt a character, raid a weak rich town, reinforce a town that is losing a fight. Not rival-only: the
same pipeline sends allies to help and friendly visitors. Built on [dependencies](https://github.com/TheIvoryDisaster/MeitouClient/blob/master/docs/modding-dependencies.md); other mods add probes, conditions,
target kinds, behaviours, rules and directors (see "Extension API"). Needs the engine hooks of [modding](https://github.com/TheIvoryDisaster/MeitouClient/blob/master/docs/modding.md) "World hooks".

## Pipeline

At each assessment (every `AssessEveryHours` game hours, default 2, in the system's `SlowWorld` at `SystemAnchor.End`), all serial:

1. **Inspect** (`Inspector`) the player faction into an immutable `PlayerReport`; probes add their numbers.
2. **Decide**: the rules are evaluated in the order added (`RuleEngine`), then each `IDirector` reads the `Assessment`; both record `Decision`s.
3. **Act**: decisions are carried out in order. A `StartDecision` starts a behaviour; the rest go through `Actions`. Each yields a log line
   (`History`, the game log).

Between assessments, every slow step: behaviours under way are stepped (`BehaviourRuns`), then the debug rows' asks run.

A throwing rule or director is logged and off for the rest of that world; a failing decision or run is logged and dropped, the rest go on.

## Inspection

| Report | Contents |
| --- | --- |
| `CharacterReport` | each living player-faction character: id, name, squad, position, town (nearest player town whose radius holds it), down, in combat, strength, gear value (equipment sections), carried value (the rest, bags included); `Value(probe)` |
| `TownReport` | each player town: finished buildings, turrets (function 12), gates (21), highest research `tech level` (3), value in player-owned containers, building value (finished buildings and furniture at their construction materials' base price), defenders (player characters in it), defenders down, defence, hostiles standing in it, threat; `UnderAttack`, `Balance` = defence / threat; `Public`, `Beds` (the player's bed fixtures); `Value(probe)` |
| `CampaignReport` | running campaigns aimed at a player town: faction, FACTION_CAMPAIGN id, town, phase, hostile |
| `PlayerReport` | the above, money, `Carried` (worn and carried), `Stored` (town containers), `Built` (building value), `Wealth` (all four), `Strongest` |

Engine choices (framework's own, not the original's): strength = mean of melee attack, melee defence, dexterity, toughness (10 without stats, 0
while down); defence = defenders' strength + 15 per turret; hostile = faction relation ≤ -30 with the player, or a squad whose campaign war
targets include the player. Item value = the game's base price (`Market.BaseValue`: quality, material, manufacturer) × quantity; **Observed** (2026-11-13, a new game
without a base): the record's `value` field alone gave wealth 0. `rivalry.tempting` counts only what can be stolen (stored, carried), not buildings. Function numbers: [campaigns](https://github.com/TheIvoryDisaster/MeitouClient/blob/master/docs/game/factions-squads-towns.md#campaigns).

## Notoriety

`NotorietyLedger`: grudge per (faction, character). Fed by `ICombatHitObserver`: a player-faction attacker landing a hit on another faction's
member adds `HitGrudge` (1), plus `DownedHitGrudge` (3) if the victim was already down; the victim dying within `KillWindowHours` (1) adds
`KillGrudge` (25). Halves every `GrudgeHalfLifeDays` (4); below 0.5 or once the character leaves the table it is dropped. Directors and dialogue
add to it too. A character's notoriety is the sum over factions.

## Built-ins

Registered under the prefix `rivalry.`; constants in `BuiltIn`. Each rule and the director has a settings switch; cooldowns are per rule or
director and key.

| Kind | Name | Meaning |
| --- | --- | --- |
| target | `rivalry.town` | each player town; key `town{number}` |
| target | `rivalry.character` | each standing player character; key: its name |
| condition | `rivalry.under-attack`, `rivalry.calm` | hostiles are standing in the town, or not |
| condition | `rivalry.losing` | under attack and `Balance` < `ReinforceBelowBalance` (0.75) or half its defenders down |
| condition | `rivalry.tempting` | not under attack, holds ≥ `RaidWealthMin` (20000) and > 400 per point of defence |
| condition | `rivalry.town-public` | the town is open to the public |
| behaviour | `rivalry.raid`, `rivalry.reinforce`, `rivalry.hunt` | the `Raid`, `Reinforce`, `Hunt` actions below; args `campaign`, `delay`, `nearby`, `package`, `squads`, `hours` |
| behaviour | `rivalry.visit` | see "Visit" |
| behaviour | `rivalry.burgle`, `rivalry.pursue` | see "Burgle" and "Pursue": squads the mod walks itself |
| rule | `rivalry.reinforce` | town `rivalry.losing`: `rivalry.reinforce` by the closest ally (relation ≥ 50; allies with a DEFEND_TOWN campaign first); once per town per 12 h |
| rule | `rivalry.opportunist` | town `rivalry.tempting`, chance `RaidChancePerDay`: `rivalry.raid` by the most hostile faction that has an assault campaign; once per town per 96 h |
| director | `rivalry.grudge` | a non-allied faction's grudge on a standing character ≥ `HuntThreshold` (60): `Hunt` with `HuntSquads` squads; largest first, at most 2 per assessment, once per faction and character name per 48 h |

## Rules

A `Rule`: `Name`, `Target` kind (default `rivalry.town`), named `Conditions` and an optional `When` test, `Behaviour`, `ChancePerDay` (null:
always; spread over the assessments), `CooldownHours`, `Faction` picker (-1 skips the target), `Args`, `Notice`, `Enabled`. Per target, in the
kind's order: not cooling, conditions hold, roll (`Assessment.Roll("{rule}/{key}")`) passes, a faction is found; then the cooldown
`{rule}/{key}` is claimed and the start recorded. Rules are code only; no data file.

## Behaviours

`IBehaviour.Start(context, request)` returns a log line; one that lasts calls `context.Begin(this, request, until)` for a `BehaviourRun`
(id, behaviour, source, key, faction, town, platoon, phase, started, until, args) and is stepped by `Step(context, run)` every slow step until
it returns false. The run is the behaviour's only state. `BehaviourContext`: world, data, report, options, population, grudges (`Notoriety`),
`Roll`, `Apply(decision)`, `ValueOf(item)` (the report's prices), `System<T>()`.

### Visit

`rivalry.visit` (args: text `template`, a SQUAD_TEMPLATE whose package is a wandering trader's; numbers `hours` 48, `minRadius` 600,
`maxRadius` 1200, `money`): phase 0 `SpawnPlatoon` in the ring round the town, homed at the nearest town site, tried each step for 24 h when no
spot is walkable; phase 1 waits up to 24 h for the platoon to load, then sends `VisitTown` at the next tick; phase 2 ends when the stay + 24 h
has passed or the platoon is gone. **Unknown**: everything in play (see "Evidence"); a town that is not public sends the trader away at once.

### Driving a squad

`SquadDriver`, for a behaviour that walks its squad itself instead of giving it a package: `Hold` (`PopulationSystem.HoldPlatoon` and
`CharacterCold.Engaged` on each member, optionally the Sneak order), `Keep` (holds again; a hold is not saved, so driven runs call it every
step), `GiveBack`, `Able` (members that can act), `WalkTo` (true within 20; a walk under way is left alone unless `redirect`) and `RollSkills`:
a number argument named `<skill>Min` (and `<skill>Max`), the skill a `StatsEnumerated` name in any case (`stealthMin`, `martialArtsMax`),
gives every member that skill uniformly in the range when the squad loads. A run that ends, or whose squad unloads, gives the platoon back.

### Burgle

`rivalry.burgle` (args: text `template`; numbers `goal` 1000 cats, `hours` 12, `minRadius` 600, `maxRadius` 1200, `salePrice` 1000, skill
ranges). The run keeps `loot`, its spawn point and the buyer's site in its args.

| Phase | What happens | Next |
| --- | --- | --- |
| 0 spawning, 1 loading | as Visit; the platoon is held, and once loaded its members sneak and get their skills | 2 |
| 2 stealing | each thief walks to one of the player's containers in the town, the one with the most valuable stack first; a locked one is picked with `LockSystem.Picking.Step` (the game's time and its Burglary for witnesses); it takes the most valuable stack that fits (a Theft). No container with goods left: it pickpockets the player character in the town carrying the most (`CrimeSystem.Pickpocket`, the game's roll) | seen, or caught pickpocketing: 3; `loot` ≥ `goal` or `hours` up: 5 |
| 3 fighting | sneaking ends; the thieves are ordered onto the nearest standing player character and left to the fight. After it, one lifts the nearest downed player character a buyer exists for | carrying: 4; nobody to take or `hours` up: 5 |
| 4 abducting | the squad walks to the buyer's town; the captive's hunger is held at 1.5 of 3 (`CharacterCold.FedTo`). Within 400 of the town centre the nearest able member of the buyer's faction takes the captive as a slave (`ArrestSystem.TakeOver`) and the squad's purse gets `salePrice` | sold, body lost or 72 h up: 5 |
| 5 leaving | the squad runs back to its spawn point; there, or after 6 h, it is given back to the game | ends |

Seen: a thief is in combat, has a pending crime that was witnessed (`Pending.Timer` ≥ `CrimeSystem.CaughtTimer`), or
`AggroSystem.Spotted` says a standing player character has it. Buyer (`SlaveMarket`): the nearest town site whose faction is not hostile to the
thieves and keeps slaves of the captive's race, that is one of its squad templates lists a `slaves` character of that race; so the game's own
data decides who enslaves whom. **Unknown**: everything in play, above all NPC sneaking against player characters, a walk through zones that
unload (the carrier and captive then travel as a stand-in or not at all), and what a released squad without a package does.

### Pursue

`rivalry.pursue` (target: a character; args: text `template`; numbers `hours` 24, `minRadius` 800, `maxRadius` 1500, skill ranges): spawns
in the ring round the quarry, holds the platoon, marks the quarry TEMP_ENEMY for the squad (so [hunter barks](#hunter-barks) apply), runs to
where the quarry is now at every step, and within 150 orders its idle members to attack. Ends, giving the squad back, when the quarry is down or
gone, no hunter can act or `hours` are up. The quarry is not saved: after a load the run takes the standing player character its faction
holds the largest grudge against. Needs no AI_PACKAGE. **Unknown** in play.

## Actions

| Decision | Carried out by |
| --- | --- |
| `Raid(faction, town, campaign?)` | `PopulationSystem.RequestCampaign` with an ASSAULT_TOWN campaign: the given one, else the faction's first hostile one (its `campaigns` list, then records whose `faction override` names it). The game's request rules then apply: world states and leader only, no chance, range or cooldown ([campaigns](https://github.com/TheIvoryDisaster/MeitouClient/blob/master/docs/game/factions-squads-towns.md#campaigns)) |
| `Reinforce(faction, town, campaign?, nearby)` | the same with a DEFEND_TOWN campaign (the "Ally ... (trigger)" records carry a `faction override`), and with `nearby` each of the faction's candidate squads within `ReinforceRadius` (8000) marks up to 16 of the town's hostiles TEMP_ENEMY and runs the defend package (`DefendPackage`, else the first AI_PACKAGE by id with signal 15, `Contract_DefendPlayerBase`) for `ReinforceHours` |
| `Hunt(faction, target, squads, package?)` | the nearest candidate squads within `HuntRadius` (20000) mark the target TEMP_ENEMY (squad memory, so the aggro fights it on sight) and, given a package (`HuntPackage`), run it as a contract aimed at the target |
| `Direct(squad, package, hours, target?)` | `StartContract` (dialogue's `AI contract`), deferred to the next tick |
| `ShiftRelation`, `Grudge`, `Notice`, `Custom` | `PlayerRelations.Add`; the ledger; `World.Post`; the director's own serial code |

Candidate squad: of the faction, not the player's, not dead, leader alive, in no campaign, not a town's own squad unless `UseTownSquads`; nearest
leader first, ties by id.

**Unknown**: which vanilla AI_PACKAGE makes a squad travel to a contract's mission target and attack it; so `HuntPackage` has no default and a
hunt without one only marks the target. A dependent (e.g. a rival faction mod) ships that package as data. Whether `Contract_DefendPlayerBase`
walks a squad into the town from 8000 away is **Unknown** until tried in play.

## Extension API

```csharp
public void Initialize(ModContext c)
{
    var rivalry = c.Dependency<IRivalryApi>("rivalry.core")!;
    var mine = rivalry.For(c.Id);                                   // names must start with "<my id>."
    mine.AddTownProbe("inn.rating", (world, town) => Rating(world, town));
    mine.AddCondition("inn.open", (a, t) => a.Report.TownOf(t.Town)?.Value("inn.rating") > 0);
    mine.AddRule(new Rule
    {
        Name = "inn.travellers",
        Conditions = [BuiltIn.TownPublic, BuiltIn.Calm, "inn.open"],
        Behaviour = BuiltIn.Visit,
        ChancePerDay = _ => 0.5f,
        CooldownHours = _ => 24,
        Faction = (a, _) => a.Data.FactionIndex("1234-inn.mod"),
        Args = (_, _) => new BehaviourArgs().Set("template", "5678-inn.mod").Set("hours", 48),
    });
}
```

| Call | Adds |
| --- | --- |
| `AddTownProbe`, `AddCharacterProbe` | a number per town or character in every report (`Value(name)`; 0 when it throws) |
| `AddCondition` | a named test over the assessment and a target |
| `AddTarget` | a target kind: lists `RuleTarget`s (stable `Key`, town, character, site) in a fixed order |
| `AddBehaviour` | an `IBehaviour` |
| `AddRule` | a `Rule`, evaluated after those added before |
| `AddDirector` | an `IDirector` (`Assess(assessment, decisions)`), asked after the rules; `decisions.Start(behaviour, target, args)` starts a behaviour |

- Registrations only before the first world (in `Initialize`); a duplicate or unprefixed name throws. Rivalry Core implements
  `IModDependents`: a dependent that fails to load has its pieces dropped.
- Read only in probes, conditions, targets and directors; randomness from `Assessment.Roll(key)` / `BehaviourContext.Roll(key)`; no state
  outside cooldowns (`TryClaim`/`Cooling`) and runs, which are hashed and saved.
- `IRivalryApi.In(world)` gives the report, ledger, history and runs to a dependent's own systems (serial steps only).

## Hunter barks

A hunting squad is one of a faction with a grudge at `HuntThreshold` against a player character whose squad memory holds TEMP_ENEMY on that
character (what `Hunt` sets; the tag fades 6 game hours after its last write, so the hunt and its barks end with it). Every 15 ticks the system
reads each member's combat target (`CombatSystem.TargetOf`); a member that has just turned on anyone but its quarry says `OffTargetLine`
(`{target}` = the quarry's name) through `World.Bark`, at most one member per squad per `BarkCooldownSeconds` (30). Switch: "Hunters speak".
Display only: the bark state is not hashed or saved.

## Dialogue codes

| Code | Kind | Meaning |
| --- | --- | --- |
| 1600 | condition | grudge of the speaker's faction against the subject, compared with the value |
| 1601 | condition | the subject's notoriety, compared with the value |
| 1600 | effect | the speaker's faction's grudge against the target changes by the value |

## Options

The switches, assessment period, half-life, hunt threshold, reinforce balance, raid chance and `Announce` (a message per decision) are on the Mod
settings page. Everything else in `RivalryOptions` is for dependents. The system copies the options in `Inputs` each tick.

## Save data

Sidecar system `rivalry`, version 2 (version 1 still loads): next/last assessment hours, grudges by faction string id and character name +
record id (slots change on load; restored to the first living match in slot order at the first step after the load, unmatched ones dropped),
cooldowns; since version 2 the next run id and each run (faction by string id, args, and its platoon as template + position: platoon ids change
on load, so a loaded run takes the nearest platoon of that template and faction no other run holds). Kill watches are not saved. A renamed
character loses its grudges across a save made before the rename.

## Debug rows (F10)

A row with a field takes its values separated by commas, in the order below; spaces round a value are dropped, so a name cannot hold a comma.
`[x]` may be left out. The row's own status line only says what was queued; the outcome is a `debug:` line in the game log one tick later, and
`Rivalry: report` shows the latest line.

| Row | Field | Does |
| --- | --- | --- |
| `Rivalry: report` | none | characters, wealth (money, carried, stored, built), grudges, each town's buildings, stored value, defence and threat, the latest log line |
| `Rivalry: assess now` | none | runs the inspection, rules and directors at the next step instead of waiting for the period |
| `Rivalry: runs and rules` | none | the behaviours under way (id, behaviour, town, platoon, phase, end hour) and every rule's name |
| `Rivalry: grudge on selected` | `faction[, amount]` | adds `amount` to the faction's grudge against each selected character |
| `Rivalry: fire rule` | `rule[, target]` | starts the rule's behaviour on one target, skipping its switch, conditions, chance and cooldown |
| `Rivalry: visit town` | `template, faction[, hours[, town]]` | starts `rivalry.visit` (see "Visit") |

| Value | Accepts | Left out |
| --- | --- | --- |
| `rule` | a rule's full name, exact case, as `Rivalry: runs and rules` lists it: `rivalry.reinforce`, `rivalry.opportunist`, and dependents' (`ashen.hand.thieves`, `ashen.hand.hunters`) | required |
| `target` | for a town rule: the player town's name (any case), its number, or its key `town{number}`. For a character rule: the character's name, exact case; it must be standing | the rule's first target: the first player town, or the first standing character |
| `faction` | a faction's name (any case) or record id | required; unknown: nothing happens (grudge) or the behaviour refuses (visit) |
| `amount` | a number, `.` as decimal point; negative eases the grudge | 100 |
| `template` | a SQUAD_TEMPLATE's record id (not its name) whose package is a wandering trader's | required |
| `hours` | the stay in game hours | 48 |
| `town` | as a town `target` | the first player town |

Examples (a player town named `Home`, a character named `Beep`):

| Row | Field | Expect in the log |
| --- | --- | --- |
| fire rule | `ashen.hand.thieves` | `debug: rule ashen.hand.thieves fired on town1`, then `ashen.hand.thieves: burgle: run 1 against town 1, ...` |
| fire rule | `ashen.hand.thieves, Home` | the same, on that town |
| fire rule | `ashen.hand.hunters, Beep` | `debug: rule ashen.hand.hunters fired on Beep`, then `ashen.hand.hunters: pursue: run 2 after Beep, ...` |
| fire rule | `rivalry.opportunist, town1` | fired, then the raid's line; `no faction` unless a hostile faction has an assault campaign |
| fire rule | `rivalry.reinforce` | fired, then the reinforce line; `no faction` unless the player has an ally (relation ≥ 50) |
| grudge on selected | `The Ashen Hand, 100` | no log line; the next assessment sends its hunters (threshold 60); `assess now` brings it at once |
| grudge on selected | `Dust Bandits` | +100; the grudge director then marks the character for that faction's nearby squads |
| visit town | `<trader squad id>, Tech Hunters, 24, Home` | `debug: visit: run 3, <id> to town 1, ...` |

What a `fire rule` line means: `no rule X` (misspelt, or its mod is not loaded); `rule X: no target K` (no such town or standing character;
a town rule given a character, or the reverse); `rule X: no faction for K` (the rule's own faction pick found none: for The Ashen Hand,
`AshenHand.mod` is missing or not enabled); `rule X threw: ...`. A rule fired this way does not start its cooldown. The behaviour's own line follows;
`burgle: no population`, `no template` or `no town` are its refusals, and `waiting for a spot` means no walkable point was found yet in the
ring round the town (tried again for 24 h). The record ids in a visit are the game's own, read in FCS; none is listed here.

## Building

`dotnet build RivalryCore` writes `RivalryCore.dll` and `meitou.json`; the game assemblies are referenced with `Private="false"`. Copy both
into `Kenshi/mods/RivalryCore/` beside an empty `RivalryCore.mod` made in FCS and enable it in `mods.cfg` above its dependents.
Tests: `dotnet test RivalryCore.Tests` (synthetic worlds, no game needed).

## Evidence

**Verified** by `RivalryCore.Tests` (synthetic worlds without a population system): the reinforce rule and its nearby squad, a grudge's
hunt, grudges and cooldowns across a save, a dependent's probe/condition/rule firing once per cooldown, a run across a save, a version 1 save
loading, name prefixes and dropping a mod's pieces, the world hash on 1 and 4 threads, which races a faction buys (`SlaveMarket`). Burgle and
Pursue have no test of their steps. **Unknown**: everything in a real game (campaign starts
from `RequestCampaign`, contract packages, the Visit behaviour end to end, a run's platoon found again after a real load), and the
`meitou-tools mod-check` result.
