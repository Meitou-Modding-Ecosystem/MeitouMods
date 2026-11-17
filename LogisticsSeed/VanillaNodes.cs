using System.Numerics;
using Meitou.Data.Fcs;
using Meitou.Data.Gameplay;
using Meitou.Simulation.Population;

namespace Logistics.Seed;

/// <summary>
/// One version of a town the game can switch it to (a TOWN in an <c>override town</c> list): its record, the faction holding the town while
/// it shows (its own <c>faction</c>, or the version it replaced keeps holding it when it names none, as <c>TownSite.Replace</c> does), the
/// world states it needs (<c>world state</c> links: a WORLD_EVENT_STATE id and the value it must have) and the versions it can switch to in turn.
/// </summary>
public sealed record TownVersion(string Record, string Name, string? Faction, IReadOnlyList<StateCondition> When, IReadOnlyList<string> Next);

/// <summary>A world state a town version needs: true (value 1) or false (value 0), as the engine's <c>IWorldStates.Passes</c> reads it.</summary>
public readonly record struct StateCondition(string State, int Value);

/// <summary>
/// A vanilla node: a placed town. <see cref="Id"/> is <c>town:&lt;InstanceId&gt;</c>, stable across mod lists and town overrides.
/// <see cref="Owner"/> is the faction holding it now (the current version's, else the base record's); <see cref="AllowedOwners"/> every faction
/// that can hold it through the game's own versions (the base record's and every version reachable along <c>override town</c>), sorted and
/// without repeats. <see cref="Ownable"/> is false for nests, markers and points of interest: places that can be cleared, not held.
/// </summary>
public sealed record VanillaNode(string Id, string Name, string BaseRecord, TownType Kind, Vector3 Position, string? Owner,
    IReadOnlyList<string> AllowedOwners, IReadOnlyList<TownVersion> Versions, bool Ownable);

/// <summary>Builds the vanilla nodes from a world's population data (the loaded mod list's TOWN records and placements).</summary>
public static class VanillaNodes
{
    /// <summary>The node id of a placed town.</summary>
    public static string NodeId(TownSite site) => "town:" + site.InstanceId;

    /// <summary>Kinds that are places to clear rather than hold.</summary>
    public static bool IsOwnable(TownType kind) => kind is not (TownType.Nest or TownType.NestMarker or TownType.PointOfInterest or TownType.Null);

    /// <summary>
    /// Every placed town with an instance id, sorted by node id. Versions are found breadth-first along <c>override town</c> from the base
    /// record; a version reached twice is listed once, so chains that loop back end.
    /// </summary>
    public static IReadOnlyList<VanillaNode> Build(PopulationData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var nodes = new List<VanillaNode>();
        foreach (var site in data.Sites)
        {
            if (string.IsNullOrEmpty(site.InstanceId)) continue;
            var versions = Versions(data, site.Record);
            var owners = new SortedSet<string>(StringComparer.Ordinal);
            if (site.Record.Faction is { } baseFaction) owners.Add(baseFaction);
            foreach (var v in versions) if (v.Faction is { } f) owners.Add(f);
            nodes.Add(new VanillaNode(NodeId(site), site.Record.Name, site.Record.Id, site.Record.Type,
                site.Position, site.Town.Faction ?? site.Record.Faction, [.. owners], versions, IsOwnable(site.Record.Type)));
        }
        nodes.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
        return nodes;
    }

    static List<TownVersion> Versions(PopulationData data, TownData baseRecord)
    {
        var found = new List<TownVersion>();
        var seen = new HashSet<string>(StringComparer.Ordinal) { baseRecord.Id };
        var queue = new Queue<(TownData From, string? Holder)>();
        queue.Enqueue((baseRecord, baseRecord.Faction));
        while (queue.Count > 0)
        {
            var (from, holder) = queue.Dequeue();
            foreach (var link in from.OverrideTown)
            {
                if (!seen.Add(link.Id) || data.Db.Find(link.Id) is not { Type: FcsRecordType.Town } record) continue;
                var town = TownData.From(record);
                string? faction = town.Faction ?? holder;
                var when = town.WorldState
                    .Where(w => data.Db.Find(w.Id) is { Type: FcsRecordType.WorldEventState })
                    .DistinctBy(w => w.Id)
                    .Select(w => new StateCondition(w.Id, w.V0))
                    .ToList();
                found.Add(new TownVersion(town.Id, town.Name, faction, when, [.. town.OverrideTown.Select(l => l.Id)]));
                queue.Enqueue((town, faction));
            }
        }
        return found;
    }
}
