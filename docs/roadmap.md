# Roadmap

Planned work on Rivalry Core and The Ashen Hand that the code already records data for. Not built yet.

## Leader personalities (next milestone)

A returning squad leader says something unique about the last fight when it is dispatched again: whom it fought, how it ended (won, beaten,
left for dead, escaped, comrades lost) and how often. Builds on `RivalLeader` (see [Rival leaders](rivalry-core.md#rival-leaders)); today a
leader only picks a starter line by its last outcome ([hunter barks](rivalry-core.md#hunter-barks)). Open: traits per leader (saved), lines
written per trait, a bark at dispatch time (when the squad loads) as well as at the first blow.

## Recruiting a rival

With much difficulty: a leader beaten many times (`Losses`, `LeftForDead`) or held captive (`Captures`, fate captive) may be talked into the
player's squad. Needs a dialogue condition on the roster (e.g. losses ≥ N, captive) and the recruit effect; fate `Recruited` is already set when
a leader joins the player's faction.

## Reckonings reheating

`Reckoning` records each leader handed to the law and the grudge it cleared. Planned: when that faction's grudge heats up again, its next leader
references the jailed one (a reckoning line is said today), and the faction may try to break the jailed leader out.
