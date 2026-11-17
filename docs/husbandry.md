# Husbandry (content mod)

Hitching posts and breeding for the player's animals (`husbandry`, no dependencies). Code: `Husbandry/`; data file generator: `Husbandry.Data/`.
Needs the engine's mod jobs and `PopulationSystem.SpawnAnimal` (MeitouClient fork PR #11; engine docs/modding.md "Mod jobs", "World hooks").

## Setting up a ranch

1. Research **Animal Husbandry** (Core, level 1, no blueprint). It unlocks the **Hitching Post** (build menu, Farming).
2. Build a post and an **Animal Feeder** (vanilla, research Item Storage) inside the pen, and stock the feeder with food.
3. Select animals, right-click the post and choose **Hitch here**. Each one lists "Hitched: Hitching Post" in its jobs list; the X unhitches it.
4. Keep the squad's **Feed Animals** option on: hitched animals walk to the feeder themselves when hungry (vanilla goal 243, food from storage in
   a town of the player's; the post creates a player town).

## Pen

A hitched animal standing idle more than **Pen radius** (setting, default 100 game units) from its post walks back to within half the radius;
one inside strolls to another spot now and then (setting **Grazing**). Anything else it is doing comes first, so it leaves the pen only for:
a food run to a feeder or food on the ground, a fight, a job of its jobs list (Follow or Bodyguard: remove it to keep the animal home), or a
player's order (riding it with MeitouTransport is one). It comes back once it stands idle again. Checked once a game second per animal.

Distances: 1 game unit is about 10 cm (engine docs/formats/terrain.md). Large farms' crop grids measure 81 x 72 (Wheat L) to 91 x 91 (Vegetable
L), about 90 x 90. Four of them by area are 180 x 180; a circle of that area has radius about 100.

## Breeding

- **Adults**: age 0.6 or more, the game's "Adult" label (below 0.4 "Pup", below 0.6 "Teen"); elders too.
- **Sex**: the game gives animals none (no ANIMAL_CHARACTER has `female chance`; Meitou draws every animal male, engine docs/game/animals.md
  "Breeding and eggs"), so the mod decides it half and half from the character's portrait serial, which is saved with the character. A character
  loaded from a save that has no serial gets a new one on load, so its sex is fixed from the first save made with Meitou.
- **Same kind**: same RACE. Pack Beast and Garru share Garru; Domesticated Bull and Pack / Wild Bull are different races. Robot races (Iron and
  Security Spiders, the Cleanser) never breed.
- **Tries**: each hitched adult female with an adult male of her race at the same post tries every **Days between tries** (default 3) game
  days, the first try that long after she is paired; she loses her clock when unpaired. The chance follows the hungrier of her and the best-fed
  male: hunger 2 or more (well fed) 50 %, 1 to 2 (hungry) 25 %, below 1 (starving) 10 % (settings).
- **Birth**: at once, no pregnancy. The pup is the mother's record, aged 0.05, born 12 units from her in her squad (a player character),
  hitched to her post, and "<mother> has given birth to a pup at the hitching post." is posted. None while the faction is at its head cap.

## Load scan

Any of the player's animals can be hitched, those of other mods included: the job takes every animal (the engine's `SquadRegistry.IsAnimal`), so
no list per kind is kept. At load the mod logs the animal kinds and races of the merged data, robots marked.

## Settings

Pen radius (30 to 600), Grazing, Breeding, Days between tries (1 to 30), and the three chances.

## Debug rows (F10)

- **Husbandry: hitched animals**: each hitched animal's race, sex, age, hunger and next try, then the latest tries (also in the log).
- **Husbandry: try breeding now**: every paired female tries at the next check.

## Save data

The sidecar holds the hitches (portrait serial, post id) and each paired female's next try (game hours), version 1. The engine keeps no mod jobs
in its save; the hitches go back on their characters at the first tick after a load. Both are in the world's state hash.

## Data file

`Husbandry.mod`, record ids `<n>-Husbandry.mod`: 1 the research, 2 the post (a copy of vanilla Prisoner Pole, `18913-small_changes_otto.mod`,
without its cage function, category FARMING). Generated from the installed game: `dotnet run --project Husbandry.Data -- "<Kenshi>" "<out>"`;
the Play project does it on F5. Untested in play.
