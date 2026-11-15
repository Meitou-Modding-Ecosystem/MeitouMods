# Rivalry Core (framework mod)

**Status: proposed, mod version 0.5.0, code-mod API 2.** A code mod in `RivalryCore` (id `rivalry.core`) that lets the world's factions
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
`KillGrudge` (25). At each assessment every grudge cools by `GrudgeCoolPerHour` (1) per game hour since the last; below 0.5 or once the
character leaves the table it is dropped. Directors and dialogue add to it too. A character's notoriety is the sum over factions.

**Held floor**: when a hit squad (`rivalry.pursue`) starts for a grudge at or above `HuntThreshold`, the threshold becomes that grudge's floor:
cooling and negative adds stop there. It is let go when the squad is beaten (every member down or dead) while a standing player character is
within `SeenRange` (1500, the outdoor sight range) of a member; a leader handed to the law clears the faction's grudges and floors outright
(see "Rival leaders"). So a grudge that sent a squad keeps sending one, paced by the sender's rules, until the player beats it in view.

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

`rivalry.pursue` (target: a character; args: text `template`, `home`, `leaderNames`, lines (see "Hunter barks"); numbers `hours` 24,
`minRadius`/`maxRadius` (`HuntSpawnMinRadius` 2200, `HuntSpawnMaxRadius` 3400), skill ranges). Phases:

1. **Travelling** (5): on the road for the distance from the faction's nearest base to the quarry over `HuntTravelUnitsPerHour` (2727: the
   original's off-screen travel speed 25 per second over its 1200/11 s game hour), clamped to `HuntTravelMinHours` (1) and the setting
   `HuntTravelMaxHours` (48; 0 skips the road). Base: the faction's nearest base ([factions](factions.md#bases): homes named in `Options.Homes`
   or `home`, else owned towns, else towns that spawn its squads, else regions that do), else `HuntTravelFallbackHours` (12). The squad is homed
   at that base's town (else the town nearest the quarry).
2. **Spawning** (0): `SpawnPlatoon` in the ring round the quarry, beyond sight (1500 × vision) and inside the loaded 3 × 3 zones (4608 each),
   tried for 24 h; then **loading** (1): once in the world the squad is held, its skills rolled, its leader named (see "Rival leaders"), its
   evidence planted, and the quarry marked TEMP_ENEMY.
3. **Closing / attacking** (2, 3): runs to the quarry's current position at every step; within 150 its idle members are ordered to attack.
4. Settled: quarry down or gone (won), every hunter down (beaten) or `hours` up (broke off). The squad is given back, the faction's last hit
   squad counts as finished now (`RivalRoster.Ended`), the leader is credited. Beaten in view releases the held floor; a beaten squad's leader
   is then watched (**aftermath**, 4) for its fate.

The run keeps `quarryName` (a loaded run finds the quarry by it, else the standing character with the largest grudge), `travelHours`,
`ended`, `leaderName`, `leaderSerial`. `PursueBehaviour.Underway(run)`: not yet in its aftermath; `Assessment.HitSquadUnderway(faction)` and
`HoursSinceHitSquad(faction)` let a rule send one squad at a time with a rest between. **Unknown** in play.

### Rival leaders

`RivalRoster` (`IRivalryState.Rivals`, `BehaviourContext.Rivals`, `Assessment.Rivals`; saved, hashed; factions by string id):

- **Naming**: when a pursue squad loads, its first living member takes a leader's name: the faction's free, idle leader that fought most
  recently and whose last body is not standing in the world, else a new name from `leaderNames` (picked by roll; the next unused; all used:
  numbered, e.g. "Old Sorrow II"). The leader's portrait serial ties the roster entry to the body.
- **Record** (`RivalLeader`): dispatches, wins, losses, withdrawals, left for dead, captures, escapes, comrades lost (squad members dead when a
  fight is settled; total and last), last outcome, quarry and hour, fate (free, captive, dead, jailed, recruited). Only a free leader is sent.
- **Aftermath** (a beaten squad's leader, looked at every 30 ticks): dead: killed. In a cage of a faction neither the player's nor lawless, or
  a bounty on it claimed, alive: **handed to the law**: the faction forgets every grudge and floor (`NotorietyLedger.ClearFaction`) and a
  `Reckoning` (faction, leader, quarry, hour, grudge cleared) is kept. Carried by a player character or in the player's cage: captive, watched
  while held; free again: escaped. Gone from the world (its platoon unloaded) or still free after `AftermathHours` (12): left for dead.
  Joined the player's faction: recruited.
- Personality on redispatch and recruitment are planned on top of this record ([roadmap](roadmap.md)).

### Evidence

Burgle and Pursue plant evidence when their squad loads (`Evidence.Plant`): with a text arg `evidence` (an ITEM record), the squad's first
living member gets one in its main grid, its `ModTag` `rivalry.evidence|<portrait serial>|<bounty>|<crime>`. Bounty: `evidenceFactor` (2) ×
`goal`, at least `evidenceMin` (3000); crime Burglary for Burgle, Assault for Pursue. A dependent registers `c.AddItemUse(record, Evidence.Use)`
(engine "Item use"): a right click on a tagged note in a player character's own inventory adds the bounty to the named character's entry with
every booking faction (each faction's `BookingOf`, once), raised now and claimable again, posts a line and uses the note up. A character not
in the world (dead, or its platoon unloaded) leaves the note and posts that its owner is nowhere to be found; an untagged item of the record
moves as usual. The portrait serial is saved with the character and the tag with the item, so a note survives a load. A bounty under 4000
draws no bounty hunters and lasts 0.04 h per cat (3000: 120 h) from use. **Unknown** in play, including whether police arrest such a
character on sight.

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

`HunterBarks` watches the squads of pursue runs that are closing in or attacking, every 15 ticks, reading each member's combat target
(`CombatSystem.TargetOf`) while the member is in combat and the target is standing:

- A member that turns on someone not of the player's faction says a **distracted** line, at most one per squad per `BarkCooldownSeconds`
  (30): the squad is held up by another fight.
- The squad's first blow against one of the player's characters: its leader (if it can stand) opens with a line from its record: back after
  a fight, the last outcome's list; new while its faction has a reckoning, the reckoning list; else its first words. A member speaks the
  engage list when the leader cannot. Once per run.

Lines come from the run's text args (`HunterLines`: `say.distracted`, `say.engage`, `say.first`, `say.reckoning`, `say.won`, `say.withdrew`,
`say.beaten`, `say.leftForDead`, `say.escaped`; entries `|`-separated, one picked per bark), with generic defaults (`OffTargetLine` for
distracted). Placeholders: `{target}` the quarry, `{name}` the speaker, `{leader}` the faction's last leader handed to the law, `{fallen}` the
squad members dead after the leader's last fight. Switch: "Hunters speak". Display only: not hashed or saved. Squads the `rivalry.grudge`
director marks (no run) do not speak.

## Dialogue codes

| Code | Kind | Meaning |
| --- | --- | --- |
| 1600 | condition | grudge of the speaker's faction against the subject, compared with the value |
| 1601 | condition | the subject's notoriety, compared with the value |
| 1600 | effect | the speaker's faction's grudge against the target changes by the value |

## Options

The switches, assessment period, grudge cooling per hour, hunt threshold, hit squad travel at most, reinforce balance, raid chance and
`Announce` (a message per decision) are on the Mod settings page. Everything else in `RivalryOptions` is for dependents. The system copies the options in `Inputs` each tick.

## Save data

Sidecar system `rivalry`, version 3 (1 and 2 still load): next/last assessment hours, grudges by faction string id and character name +
record id (slots change on load; restored to the first living match in slot order at the first step after the load, unmatched ones dropped),
cooldowns; since version 2 the next run id and each run (faction by string id, args, and its platoon as template + position: platoon ids change
on load, so a loaded run takes the nearest platoon of that template and faction no other run holds); since version 3 each grudge's held floor
and, last, the rival roster (its own layout version 1: leaders, reckonings, each faction's last hit squad hour). Kill watches are not saved. A renamed
character loses its grudges across a save made before the rename.

## Debug rows (F10)

A row with a field takes its values separated by commas, in the order below; spaces round a value are dropped, so a name cannot hold a comma.
`[x]` may be left out. The row's own status line only says what was queued; the outcome is a `debug:` line in the game log one tick later (so the game must not be paused), and
`Rivalry: report` shows the latest line.

| Row | Field | Does |
| --- | --- | --- |
| `Rivalry: report` | none | characters, wealth (money, carried, stored, built), grudges, each town's buildings, stored value, defence and threat, the latest log line |
| `Rivalry: assess now` | none | runs the inspection, rules and directors at the next step instead of waiting for the period |
| `Rivalry: runs and rules` | none | the behaviours under way (id, behaviour, town, platoon, phase, end hour) and every rule's name |
| `Rivalry: rival leaders` | none | each named leader's record and fate, and the leaders handed to the law |
| `Rivalry: faction bases` | `faction` | how the faction's bases were found, their count and the five nearest the first player character |
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
Pursue have no test of their steps; nor do grudge cooling and floors, the rival roster, leader fates or the barks. **Unknown**: everything in a real game (campaign starts
from `RequestCampaign`, contract packages, the Visit behaviour end to end, a run's platoon found again after a real load), and the
`meitou-tools mod-check` result.
