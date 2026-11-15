using Meitou.Data.Fcs;
using Meitou.Data.Gameplay;
using Meitou.Simulation;

namespace Rivalry;

/// <summary>
/// Any faction's hit squads (docs/rivalry-core.md "Hit squads of any faction"): which faction sends one after a character, and which of its own
/// squads it sends. A faction whose mod gives it a rule of its own (<see cref="RivalryOptions.OwnHitSquads"/>) is left to that rule.
/// </summary>
public static class HitSquads
{
    /// <summary>
    /// The faction that sends a hit squad after the character now: not the player's, not an ally, not one with a rule of its own; its grudge at
    /// the hunt threshold; a squad of its own to send; none of its hit squads out and its last one finished the rest ago. The largest grudge
    /// first, ties by faction index; -1: none.
    /// </summary>
    public static int FactionFor(Assessment a, CharacterId who) => a.Notoriety.Entries
        .Where(g => g.Key.Who == who && g.Value >= a.Options.HuntThreshold && MaySend(a, g.Key.Faction))
        .OrderByDescending(g => g.Value)
        .ThenBy(g => g.Key.Faction)
        .Select(g => g.Key.Faction)
        .DefaultIfEmpty(-1)
        .First();

    /// <summary>
    /// Whether the faction may send a hit squad now, its grudge aside.
    /// </summary>
    static bool MaySend(Assessment a, int faction)
    {
        if (faction == a.PlayerFaction || a.IsAlly(faction) || a.Faction(faction) is not { } data) return false;
        if (a.Options.OwnHitSquads.Contains(data.Id) || TemplateFor(a.Data, faction) is null) return false;

        return !a.HitSquadUnderway(faction) && a.HoursSinceHitSquad(faction) >= a.Options.HitSquadRestHours;
    }

    /// <summary>
    /// The squad a faction sends: of the SQUAD_TEMPLATEs in its <c>squads</c> list (its roaming squads), then its <c>squad default</c>, that are
    /// not traders, the one with the largest weight (ties by record id). Null: the faction has none, and sends no hit squad.
    /// </summary>
    public static string? TemplateFor(PopulationData data, int faction)
    {
        if (faction < 0 || faction >= data.Factions.Count) return null;

        var own = data.Factions[faction];
        return Best(data, own.Squads) ?? Best(data, own.SquadDefault);
    }

    /// <summary>
    /// The heaviest usable template of a list; null when it has none.
    /// </summary>
    static string? Best(PopulationData data, IReadOnlyList<RecordLink> list) => list
        .Where(l => data.Db.Find(l.Id) is { Type: FcsRecordType.SquadTemplate } record && !SquadTemplate.From(record).IsTrader)
        .OrderByDescending(l => l.V0)
        .ThenBy(l => l.Id, StringComparer.Ordinal)
        .Select(l => l.Id)
        .FirstOrDefault();
}
