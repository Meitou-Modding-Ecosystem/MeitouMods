# The Bast Front

Author: PZA. A design brief for the Meitou Modding Ecosystem group, 2026-11-15. Everything in it is a proposal; the facts about the game
come from its own data and dialogue (base game only, so a fresh Meitou game has all of it).

## The pitch

Kenshi's world only changes when the player kills or jails a unique character. The Bast Front is the place where the world moves on
without you: a contested border where the United Cities and the Holy Nation fight over towns, a second war burns to the west between the
Holy Nation and the Shek, and each side's leaders, supplies and alliances decide who holds what. The player can stay out of it, profit from
it, or start in the middle of it.

## What the lore says

Every faction involved already talks about this war in the base game. In short:

- **The United Cities speak with two voices.** Koin, Tengu's diplomat, wants a limited war: take **Okran's Shield** as a border fortress and
  push the Holy Nation back, but keep the Holy Nation alive as a buffer against the Shek. Yamdu of the Traders Guild wants a long war in the
  west that others fight: provoke the Shek, let both sides wear each other down, harvest the Holy Nation's plains, iron and labour, and keep Tengu from
  growing too strong.
- **The Shek have one enemy and a set plan.** Esata's war is with the Phoenix and nobody else: take **Stack** first, then besiege the plains
  and **Okran's Shield**, and the Phoenix last at **Blister Hill**. She won't commit the Shek against the desert nobles and won't massacre civilians. Her
  hardliners, **Kral's Chosen**, raid on their own; the **Berserkers** are exiles.
- **The Holy Nation fights a holy war of defence.** The Phoenix sees the Shek as the war, and refuses to fight the UC as long as there are
  bigger threats. His counter-plan: strike at Esata's daughter **Seto**, let Esata's fury empty the Shek capital, and march on it. He knows
  the Shek line of succession: Esata, then the Mountain, then Flying Bull.
- **The UC fights through others.** Both Koin and Yamdu want the player to do the killing, covertly. The base game also has Traders Guild
  hired thugs, noble-backed Manhunters, slave raids and Mercenary Guild contracts.

## What the base game already has in Bast

Bast is a three-way border, and every side's presence there is tied to a leader being alive:

| Who is in Bast | While | Weight |
| --- | --- | --- |
| UC army force (Lord Inaba's) | Inaba is alive | 15 |
| UC army | Tengu is alive | 4 |
| Holy Army force (Valtena's) | Valtena is alive | 15 |
| Holy Army patrols | Valtena / the Phoenix alive | 2 + 2 |
| Holy Nation diaspora bandits | the Phoenix, Seta and Valtena are all gone | 2 |
| Rebel Farmers (heavy) | Tengu alive, and Inaba dead or Simion alive | 4 + 4 |
| Cannibal raids, slave escapees, drifters | always | 1-5 |

No Shek squads spawn in Bast. Okran's Shield, Valtena's seat and the UC's one war aim, sits on its border. So the shape is two fronts with
the Holy Nation squeezed between them:

- **West:** the Shek against the Holy Nation, starting at Stack.
- **Bast:** the UC against the Holy Nation, at Okran's Shield, with the Rebel Farmers inside.

The two fronts feed each other: when the Shek push at Stack, the Holy Nation pulls its army west, Okran's Shield thins out, and that is the
moment the UC turns border skirmishing into an invasion. If the Shek grow too strong, the UC eases off to keep its buffer.

## How it moves

Three layers, built bottom-up:

1. **World facts the map data can read.** Kenshi's world states can only test unique characters and the player's relations. An engine hook
   (issue #28) lets a code mod answer tagged world states: "the UC is at war with the Holy Nation", "the UC holds Okran's Shield", "day 50".
   Town versions, squads and campaigns then switch on those facts through ordinary FCS data.
2. **Bast content (FCS data).** Town versions for each holder (a UC-held Okran's Shield, a burned outpost, a Rebel Farmer camp), squads and
   raids gated on the front's states. Reactive World's Deadcat chain (a town that changes hands in stages) is the model.
3. **Dustlife moves the facts.** Its war simulation decides who attacks, who holds and who starves, and sets the facts of layer 1. A town
   redraws when its area loads.

Proposed rules per side (being tested in Dustlife's balance runner now):

| Side | Rule |
| --- | --- |
| Shek | Targets in order Stack, Okran's Shield, Blister Hill; never UC places; keeps a home guard; captured towns keep their people |
| Holy Nation | Its leaders anchor its towns (lose one, that town weakens); defends, striking Shek places only where weakly held; no offensive against the UC, but the low-intensity Bast border war; counter-attacks the Shek capital when the Shek army is away |
| United Cities | A standing border war in Bast; invades Okran's Shield only when the Holy Nation is weak there; never Blister Hill; eases off if the Shek get too strong; proxies elsewhere |
| Slave Traders, Traders Guild | The slave and trade side of the UC's economy: they hold, supply and profit, they don't invade |
| Kral's Chosen, Berserkers | Later phase: raiders and exiles who rise when the Shek leadership falters |

## A Bast Front game start

Hunt cadence (#24): hunters arriving early can feel too close for a fresh playthrough. A Bast Front start turns that around: the player who picks
it chooses to start inside a war. Normal starts keep the distance rules; this one opts in. Game starts are FCS data, so this works on any
runtime. The base game's "Son of a Captain" (a dead UC army captain's son, hated by the Holy Nation) is the template. Ideas:

- **Inaba's Levy:** a small UC squad of conscripts posted to a Bast outpost, allied with the UC, at war with the Holy Nation. The front is
  on your doorstep from day one.
- **The Shield's Crusaders:** Holy Nation zealots garrisoned near Okran's Shield, sworn to Valtena. Pick the other side.
- **Caught Between:** escaped slaves or Rebel Farmer stragglers inside Bast, hunted by both armies, with nothing but each other.

Each start sets the squad, the town, faction relations and starting money, and could switch on a "front is active" state so the war is
already burning when the game begins.

## Who builds what

| Piece | Who |
| --- | --- |
| The war simulation: who attacks, holds, starves; garrisons, supply, caravans | Dustlife (us) |
| The world-state hook (#28) | engine fork, maintainers |
| News, rumours, town talk, Atlas ("Okran's Shield fell to the UC") | The Wasteland Remembers (#23) |
| How factions react to the player at the front; hunts (one at a time, #24) | Rivalry Core |
| Town versions, war camps, standards, the new starts' gear and models | content mods and models |
| Character ownership: a named character belongs to the mod that named it (#25) | everyone |

## What we know and what is open

- **Tested:** Dustlife's balance runner plays the base map for 90 game days in under a second. With the three majors only, something
  happens every day, but bands are never raised or killed: without an economy the war doesn't grow. The economy (garrisons from surplus,
  hunger, caravans) comes first in the port.
- **Open:**
  - Which towns sit inside Bast by map position (outposts, ruins, the Bastion).
  - How fast spawns react after a state flips.
  - How the new starts set up a war "already burning" without breaking a fresh world.
  - Who owns what when a Bast leader is also another mod's rival (the ownership rule proposed under #25 covers it).

## Phases

1. **Foundation:** the Dustlife port (garrisons and economy), the world-state hook, the hunt and ownership rules.
2. **The front:** the Bast war in the simulation, its world states, the first town versions, news through The Wasteland Remembers.
3. **The starts:** the three Bast Front starts.
4. **The usurpers:** Kral's Chosen and the Berserkers as factions that rise and fall with the Shek leadership.
