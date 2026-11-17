# Factions and their bases

Where each faction's squads can come from, for Rivalry Core's hit squads (`FactionBases`) and for content mods choosing a faction.

## Bases

`FactionBases.Of(data, faction, names)` returns every base of the first kind the faction has, in this order:

1. **Named**: placed towns or regions (BIOME_GROUP names) listed for the faction in `RivalryOptions.Homes` (by FACTION string id or name;
   `|`-separated; set by a dependent in its `Initialize`) or in a run's `home` argument. Set on purpose, so it wins.
2. **Owned**: placed towns, camps and outposts the faction owns now (`PopulationData.SiteFactions`, which follows town overrides).
3. **Spawns**: placed towns whose `residents`, `roaming squads` or `bar squads` list one of its SQUAD_TEMPLATEs (spawn faction as the engine
   resolves it: the template's own `faction`, else the town's).
4. **Region**: the map cells (64 × 64 of 4608) of regions whose `homeless spawns` list one of its templates.

A pursue squad travels from the base nearest its quarry (and is homed at that base's town when it is one); with none, the fallback travel time.
Everything is read from the loaded data, so a **modded faction** is covered by whatever towns, squad lists or region spawns its mod adds; a
faction with none of those (like The Ashen Hand) names its homes. F10 `Rivalry: faction bases` with a faction name shows the kind and the
nearest five. **Unknown**: none of this checked against the real game data yet.

## Vanilla factions

**Observed** from third-party listings ([pindrop.gg factions](https://pindrop.gg/kenshi/factions), [towns](https://pindrop.gg/kenshi/towns),
the Kenshi wiki's Faction page), not from the game files. Regions are the in-game region names.

| Faction | Kind | Holdings (region) |
| --- | --- | --- |
| United Cities | major | Bark, Heft, Sho-Battai, Stoat (The Great Desert); Brink (Stormgap Coast); Catun (Bonefields); Clownsteady, Drifter's Last (The Hook); Heng (Heng); Farming Village (The Outlands); Tengu's Vault (Skimsands); 29 places in all |
| The Holy Nation | major | Bad Teeth, Blister Hill (Okran's Pride); Stack (Okran's Gulf); Okran's Fist (Arm of Okran); Okran's Shield (Iron Valleys); Rebirth; 26 in all |
| Shek Kingdom | major | Admag (Stenn Desert); Squin (Border Zone); Last Stand (Spider Plains) |
| Tech Hunters | major | Black Scratch (The Outlands); Flats Lagoon; Mourn (Bonefields); World's End (Arm of Okran) |
| Western Hive | major | Western Hive and hive villages (Vain) |
| Southern Hive | regional | Southern Hive (Royal Valley), Southern Hive villages |
| Slave Traders | regional | Slave Markets (South Wetlands); Eyesocket (Stormgap Coast); Port North, Port South, Stone Camp (The Great Desert); Slave Farm, South Stone Camp (Heng); Slave Farm South (The Hook) |
| Traders Guild | regional | Trader's Edge (Heng) |
| Holy Nation Outlaws | regional | The Hub (Border Zone), Rebel Base |
| Anti-Slavers | regional | Spring (Stobe's Gamble) |
| Swampers | regional | Shark, Grayflayer Village, Mud Town (The Swamp) |
| Hounds, Stone Rats, Grayflayers, Blackshifters, Red Sabres, Swamp Ninjas | swamp gangs | Rot, Stone Rat Village (The Swamp), Smugglers Bar, Secret Drug Farm, Red Sabre Hideout, Swamp Ninja Base |
| Flotsam Ninjas | regional | Flotsam Village (Hidden Forest), safehouses, scout posts |
| Mongrel | regional | Mongrel (Fog Islands) |
| Second Empire Exile | regional | Cat-Lon's Exile (Ashlands) |
| Skeletons | regional | Black Desert City (Deadlands), Scraphouse, Burn's Tower |
| Skeleton Legion | hostile | Barrier Tower, Sniper Tower |
| Cannibals | hostile | Cannibal Capital, Cannibal Village, First Village (Cannibal Plains) |
| Cannibal Hunters | regional | The Bastion (Sinkuun) |
| Rebel Farmers | regional | Fort Simion (Sinkuun) |
| Nomads, Empire Peasants | regional | Settled Nomad Village (Dreg); Old Village |
| Reavers | bandit | Ark (Stobe's Garden), Reaver Camps |
| Crab Raiders | bandit | Crab Town (The Pits East), Crab Village |
| Skin Bandits | bandit | Skinhouse HQ (Sonorous Dark), Skinhouses |
| Skeleton Bandits | bandit | Iron HQ (The Crags) |
| Dust Bandits | bandit | Dust King Tower (Border Zone) |
| Berserkers | hostile | Berserker Village (Spider Plains) |
| Band of Bones, Kral's Chosen | rogue Shek | Exile Camp; New Kralia (Stenn Desert) |
| Thrall Masters | hostile | Tower of Abuse |
| United Heroes League | hostile | Fort Mirage |
| Manhunters, Sand Ninjas, Preacher Cult, Scavengers, The Gorrillo Bandits | minor | Manhunter Base, Cactus Den, Cult Village (Stormgap Coast), Old Control Tower, The Old Prison |
| Fishmen, Fogmen | hostile | Fish Isle, Island Lab; Tower of Fog, Deadhive Overrun (Fog Islands) |
| Bloodraiders, Free Traders, Grass Pirates, Machinists, Shinobi Thieves, Shrieking Bandits, Slave Hunters, Starving Bandits, Trade Ninjas | no placed town | roam (regions), or sit in other factions' towns (e.g. Shinobi Thieves in bars, Trade Ninjas at The Hub): found by kinds 3 and 4 |
