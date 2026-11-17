using System.Numerics;
using Meitou.Data.Gameplay;
using Meitou.Simulation.Population;
using Meitou.Simulation.Towns;

namespace Logistics.Edges;

/// <summary>How two places are joined.</summary>
public enum EdgeKind
{
    /// <summary>The game's own road route between them (<c>RoadNetwork.Route</c> at full road preference).</summary>
    Road,
    /// <summary>Close enough that the game walks straight (<c>RoadNetwork.MinTrip</c> or less).</summary>
    Near,
    /// <summary>
    /// The fallback for an island, a place or group of places no road or near trip joins to the rest: straight from its nearest place to the
    /// nearest place outside it, so every place can reach every other.
    /// </summary>
    Overland,
}

/// <summary>
/// A place an edge can join: its node id (<c>town:&lt;InstanceId&gt;</c>, as vanilla node seeding gives it), its place on the ground (X/Z), its
/// town radius, and whether it can be held (nests, markers and points of interest cannot, and get no edges).
/// </summary>
public readonly record struct EdgePlace(string Id, Vector2 Position, float Radius, bool Ownable);

/// <summary>
/// An undirected edge between two nodes: <see cref="From"/> sorts before <see cref="To"/> (ordinal), <see cref="Cost"/> is the length walked
/// (world units, X/Z) from one place's centre to the other's along the route.
/// </summary>
public sealed record VanillaEdge(string From, string To, float Cost, EdgeKind Kind);

/// <summary>The knobs of <see cref="VanillaEdges.Build(IReadOnlyList{EdgePlace}, RoadNetwork?, EdgeOptions?)"/>; the defaults are the documented ones.</summary>
public sealed record EdgeOptions
{
    /// <summary>Each place tries an edge to this many of its nearest places (a pair is tried once, whichever side picked it).</summary>
    public int Neighbours { get; init; } = 6;
    /// <summary>Trips this long or shorter are <see cref="EdgeKind.Near"/>: the game's own <c>RoadNetwork.MinTrip</c>, below which it walks straight.</summary>
    public float NearTrip { get; init; } = RoadNetwork.MinTrip;
    /// <summary>Join each island to the rest with an <see cref="EdgeKind.Overland"/> edge, so the graph is one piece.</summary>
    public bool Overland { get; init; } = true;
}

/// <summary>
/// Builds the vanilla edges: which ownable places the game's roads join directly, and the cost of each trip. A route that passes through a
/// third place's radius on the way is not an edge (the trip is two edges, through that place). Run once per world and cache; nothing here goes
/// in the save.
/// </summary>
public static class VanillaEdges
{
    /// <summary>The node id of a placed town (the same as vanilla node seeding's).</summary>
    public static string NodeId(TownSite site) => "town:" + site.InstanceId;

    /// <summary>Kinds that are places to clear rather than hold (the same rule as vanilla node seeding's).</summary>
    public static bool IsOwnable(TownType kind) => kind is not (TownType.Nest or TownType.NestMarker or TownType.PointOfInterest or TownType.Null);

    /// <summary>Every placed town with an instance id as an edge place: its current version's radius and the base record's kind.</summary>
    public static IReadOnlyList<EdgePlace> Places(PopulationData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return [.. data.Sites
            .Where(s => !string.IsNullOrEmpty(s.InstanceId))
            .Select(s => new EdgePlace(NodeId(s), new Vector2(s.Position.X, s.Position.Z), s.Town.Radius, IsOwnable(s.Record.Type)))];
    }

    /// <summary>The vanilla edges of a world: its placed towns and its roads.</summary>
    public static IReadOnlyList<VanillaEdge> Build(PopulationData data, EdgeOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        return Build(Places(data), data.Roads, options);
    }

    /// <summary>
    /// The edges between the ownable places, sorted by <see cref="VanillaEdge.From"/> then <see cref="VanillaEdge.To"/> (ordinal). The result
    /// depends only on the places and roads, not on their order. Throws on two places with one id.
    /// </summary>
    public static IReadOnlyList<VanillaEdge> Build(IReadOnlyList<EdgePlace> places, RoadNetwork? roads, EdgeOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(places);
        options ??= new EdgeOptions();

        var held = places.Where(p => p.Ownable && !string.IsNullOrEmpty(p.Id)).OrderBy(p => p.Id, StringComparer.Ordinal).ToArray();
        for (int i = 1; i < held.Length; i++)
        {
            if (held[i].Id == held[i - 1].Id) throw new ArgumentException($"Two places have the id {held[i].Id}.", nameof(places));
        }

        // The pairs to try: each place's nearest few (ties broken by id, so the order of the input never matters).
        var pairs = new SortedSet<(int A, int B)>();
        for (int i = 0; i < held.Length; i++)
        {
            foreach (int j in Nearest(held, i).Take(Math.Max(0, options.Neighbours)))
            {
                pairs.Add(i < j ? (i, j) : (j, i));
            }
        }

        var edges = new List<VanillaEdge>();
        var root = Enumerable.Range(0, held.Length).ToArray();
        int Find(int x)
        {
            while (root[x] != x) x = root[x] = root[root[x]];
            return x;
        }

        foreach (var (a, b) in pairs)
        {
            if (Join(held, a, b, roads, options) is not { } edge) continue;

            edges.Add(edge);
            root[Find(a)] = Find(b);
        }

        // Islands (places, or groups of places, no road or near trip joins to the rest): the smallest joins its nearest place outside it,
        // straight, until one graph is left. Ties go to the lower ids, so the result never depends on the input order.
        while (options.Overland)
        {
            var groups = Enumerable.Range(0, held.Length).GroupBy(Find).Select(g => g.ToArray()).ToList();
            if (groups.Count <= 1) break;

            var island = groups.OrderBy(g => g.Length).ThenBy(g => g.Min()).First();
            var inside = island.ToHashSet();
            var (i, j) = island
                .SelectMany(x => Enumerable.Range(0, held.Length).Where(y => !inside.Contains(y)).Select(y => (x, y)))
                .OrderBy(p => Vector2.DistanceSquared(held[p.x].Position, held[p.y].Position)).ThenBy(p => Math.Min(p.x, p.y)).ThenBy(p => Math.Max(p.x, p.y))
                .First();
            var (lo, hi) = i < j ? (i, j) : (j, i);
            edges.Add(new VanillaEdge(held[lo].Id, held[hi].Id, Vector2.Distance(held[i].Position, held[j].Position), EdgeKind.Overland));
            root[Find(i)] = Find(j);
        }

        edges.Sort((x, y) => string.CompareOrdinal(x.From, y.From) is var c and not 0 ? c : string.CompareOrdinal(x.To, y.To));
        return edges;
    }

    /// <summary>The other places by distance from place <paramref name="i"/>, nearest first (ties by id).</summary>
    static IEnumerable<int> Nearest(EdgePlace[] held, int i) =>
        Enumerable.Range(0, held.Length).Where(j => j != i).OrderBy(j => Vector2.DistanceSquared(held[i].Position, held[j].Position)).ThenBy(j => j);

    /// <summary>The edge between places a and b (a's id sorts first), or null when no trip joins them without passing through a third place.</summary>
    static VanillaEdge? Join(EdgePlace[] held, int a, int b, RoadNetwork? roads, EdgeOptions options)
    {
        var (pa, pb) = (held[a], held[b]);
        float straight = Vector2.Distance(pa.Position, pb.Position);

        if (straight <= options.NearTrip)
        {
            return Clear(held, a, b, [pa.Position, pb.Position]) ? new VanillaEdge(pa.Id, pb.Id, straight, EdgeKind.Near) : null;
        }

        if (roads is null) return null;

        // The game routes each way on its own (the joins and the A* can differ): the cheaper trip that passes through no third place counts.
        float best = float.MaxValue;
        foreach (var (from, to) in new[] { (pa, pb), (pb, pa) })
        {
            if (roads.Route(from.Position, to.Position, 1f) is not { } via) continue;

            var line = new List<Vector2>(via.Count + 1) { from.Position };
            line.AddRange(via);
            if (!Clear(held, a, b, line)) continue;

            float length = 0;
            for (int s = 1; s < line.Count; s++) length += Vector2.Distance(line[s - 1], line[s]);
            best = MathF.Min(best, length);
        }

        return best < float.MaxValue ? new VanillaEdge(pa.Id, pb.Id, best, EdgeKind.Road) : null;
    }

    /// <summary>
    /// True when the line from a to b passes through no third place's radius. A third place whose circle overlaps a's or b's counts as part of
    /// that end (a town and its outskirts), so it never blocks.
    /// </summary>
    static bool Clear(EdgePlace[] held, int a, int b, List<Vector2> line)
    {
        for (int c = 0; c < held.Length; c++)
        {
            if (c == a || c == b || held[c].Radius <= 0) continue;

            var pc = held[c];
            if (Vector2.Distance(pc.Position, held[a].Position) < pc.Radius + held[a].Radius
                || Vector2.Distance(pc.Position, held[b].Position) < pc.Radius + held[b].Radius) continue;

            for (int s = 1; s < line.Count; s++)
            {
                if (DistanceToSegment(pc.Position, line[s - 1], line[s]) < pc.Radius) return false;
            }
        }

        return true;
    }

    static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        float len2 = ab.LengthSquared();
        float t = len2 > 0 ? Math.Clamp(Vector2.Dot(p - a, ab) / len2, 0, 1) : 0;
        return Vector2.Distance(p, a + t * ab);
    }
}
