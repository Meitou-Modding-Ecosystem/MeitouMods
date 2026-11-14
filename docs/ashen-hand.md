# The Ashen Hand (content mod)

A shadow order built on [Rivalry Core](rivalry-core.md) (`ashen.hand`, needs `rivalry.core` 0.4). Code: `AshenHand/`; data file generator:
`AshenHand.Data/`; tests: `AshenHand.Tests/`. The name is a working title.

## Lore

- Publicly a cult of a spirit said to sleep under the Ashlands. Its inner circle knows what is buried there: a behemoth, a skeleton war
  machine of the Second Empire, which the order means to wake and turn on the continent.
- The dig needs money, hands and silence, hence its three arms: thieves fund it, takers supply labour (captives sold on, later kept), hunters
  answer anyone who hurts the order.
- It owns no town and holds no relation: neutral to all until crossed. Future steps: named officers, the dig as a world state, takers who keep
  their captives.

## Rules

| Rule | Fires when | Does |
| --- | --- | --- |
| `ashen.hand.thieves` | a player outpost is calm, the player's net worth (`PlayerReport.Wealth`: cats, gear, stored goods, buildings) ≥ 40000 and the outpost holds ≥ 10000 (the player's cats + goods in its containers + what its people carry, worn gear aside); chance 0.2 a day; once per outpost per 72 h | `rivalry.burgle` with the thief squad: goal 1000 cats, stealth 40 to 60, martial arts 30 to 50 |
| `ashen.hand.hunters` | the order's grudge against a standing player character ≥ Rivalry Core's `HuntThreshold` (60; a hit on one of its people adds 1, on a downed one 3, a kill 25); once per character per 48 h | `rivalry.pursue` with the hunter squad: melee attack, melee defence, strength and toughness 35 to 55 |

The player's cats are not in any container, so an outpost that holds only cats gives thieves nothing to take but pockets.

Both rules pass `evidence` = the ledger, so each squad's leader carries an Ashen Hand Ledger (Rivalry Core's [Evidence](rivalry-core.md#evidence)):
a log of days spent watching a mark. Right-clicked in a player character's inventory it puts a bounty on its owner with every lawful faction,
2 × the run's goal and at least 3000 (3000 for both today), so turning the owner in pays where executing them does not.

## Data file

`AshenHand.mod`, record ids `<n>-AshenHand.mod`: 1 the faction (default relation 0, no roaming population), 2 the thief and 3 the hunter
(copies of the game's CHARACTER `Dust Bandit`: every field and link but those whose list starts with `dialog`; `faction` the order; the
thief also without `weapons` and `crossbows`), 4 the thief squad (`squad`: thief, 1 to 4) and 5 the hunter squad (hunter, 3 to 5). The squads
have no AI package: Rivalry Core drives them. 6 the ledger: an ITEM copying every field and link of the game's ITEM "Books" (else the first
ITEM by id with "book" in its name; with none, no look, 0.2 kg), with its own name and text, `stackable` 1, `value` 20, no trade item.

The people are copied from the installed game, so the file is generated, not committed (`.mod` is git-ignored):

```
dotnet run --project AshenHand.Data -- "<game folder>" "<Kenshi>/mods/AshenHand/AshenHand.mod"
```

Without arguments: the game from `KENSHI_PATH` or `meitou.local.json`, the file in the working directory. It reads the unmodded base files.
It opens in FCS like any mod. **Unknown**: whether the copied Dust Bandit carries links besides talk that make it act as a bandit, and how
the game treats a FACTION record with only these two fields (Meitou reads absent fields as its defaults).

## Settings

Mod settings page: Thieves, Hunters (switches), Thieves from net worth (40000), Outpost holds at least (10000), Thief chance per day (0.2).
The rules read the settings object directly at each assessment.

## Installing and trying it

`Kenshi/mods/AshenHand/`: `AshenHand.dll`, `meitou.json`, `AshenHand.mod`; in `mods.cfg` below `RivalryCore`. In play, F10 `Rivalry: fire rule`
with `ashen.hand.thieves` (or `ashen.hand.hunters, <character name>`) starts either at once, whatever the conditions.

## Evidence

**Verified** by `AshenHand.Tests` (synthetic data and worlds, no population system): the generated records (copied fields, the thief
disarmed, no talk, squad counts), the file reading back as written, the thieves rule's two thresholds, the hunters rule's grudge.
**Unknown**: everything in play; the generator against the real game data.
