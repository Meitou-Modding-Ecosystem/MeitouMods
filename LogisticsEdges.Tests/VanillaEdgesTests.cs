using System.Numerics;
using Logistics.Edges;
using Meitou.Data.World;
using Meitou.Simulation.Towns;

namespace Logistics.Edges.Tests;

public class VanillaEdgesTests
{
    const float TownRadius = 2500;

    static EdgePlace Town(string id, float x, float z, float radius = TownRadius, bool ownable = true) =>
        new("town:" + id, new Vector2(x, z), radius, ownable);

    /// <summary>A straight road through the points (X/Z), between junction numbers <paramref name="start"/> and <paramref name="end"/>.</summary>
    static RoadPath Road(string id, int start, int end, params (float X, float Z)[] points)
    {
        var list = points.Select(p => new Vector3(p.X, 0, p.Z)).ToList();
        float length = 0;
        for (int i = 1; i < list.Count; i++) length += Vector3.Distance(list[i - 1], list[i]);
        return new RoadPath(id, 400, start, end, length, list);
    }

    static RoadNetwork Roads(params RoadPath[] paths) => new(paths);

    [Fact]
    public void A_road_between_two_towns_is_a_road_edge_costing_its_length()
    {
        var edges = VanillaEdges.Build([Town("a", 0, 0), Town("b", 40000, 0)], Roads(Road("r", 1, 2, (0, 0), (20000, 10000), (40000, 0))));

        var edge = Assert.Single(edges);
        Assert.Equal(("town:a", "town:b", EdgeKind.Road), (edge.From, edge.To, edge.Kind));
        Assert.Equal(2 * MathF.Sqrt(20000f * 20000 + 10000f * 10000), edge.Cost, 1f);
    }

    [Fact]
    public void Places_within_the_games_straight_walk_are_near_edges()
    {
        var edges = VanillaEdges.Build([Town("a", 0, 0), Town("b", 6000, 8000)], roads: null);

        var edge = Assert.Single(edges);
        Assert.Equal(EdgeKind.Near, edge.Kind);
        Assert.Equal(10000, edge.Cost, 0.01f);
    }

    [Fact]
    public void A_route_through_a_third_town_is_two_edges_not_one()
    {
        var roads = Roads(Road("ac", 1, 2, (0, 0), (30000, 0)), Road("cb", 2, 3, (30000, 0), (60000, 0)));

        var edges = VanillaEdges.Build([Town("a", 0, 0), Town("b", 60000, 0), Town("c", 30000, 0)], roads);

        Assert.Equal([("town:a", "town:c"), ("town:b", "town:c")], edges.Select(e => (e.From, e.To)));
        Assert.All(edges, e => Assert.Equal(EdgeKind.Road, e.Kind));
        Assert.All(edges, e => Assert.Equal(30000, e.Cost, 1f));
    }

    [Fact]
    public void Nests_get_no_edges_and_never_block_a_route()
    {
        var roads = Roads(Road("ab", 1, 2, (0, 0), (60000, 0)));

        var edges = VanillaEdges.Build([Town("a", 0, 0), Town("b", 60000, 0), Town("nest", 30000, 0, ownable: false)], roads);

        var edge = Assert.Single(edges);
        Assert.Equal(("town:a", "town:b", EdgeKind.Road), (edge.From, edge.To, edge.Kind));
    }

    [Fact]
    public void A_place_overlapping_an_end_counts_as_that_end()
    {
        // "o" sits in a's outskirts, on the road out: it must not cut a off from b.
        var roads = Roads(Road("ab", 1, 2, (0, 0), (60000, 0)));

        var edges = VanillaEdges.Build([Town("a", 0, 0), Town("b", 60000, 0), Town("o", 3000, 0, radius: 1250)], roads);

        Assert.Contains(edges, e => (e.From, e.To, e.Kind) == ("town:a", "town:b", EdgeKind.Road));
    }

    [Fact]
    public void A_place_no_road_reaches_gets_an_overland_edge_to_its_nearest_place()
    {
        var roads = Roads(Road("ab", 1, 2, (0, 0), (40000, 0)));

        var edges = VanillaEdges.Build([Town("a", 0, 0), Town("b", 40000, 0), Town("far", 40000, 300000)], roads);

        Assert.Equal(2, edges.Count);
        var overland = Assert.Single(edges, e => e.Kind == EdgeKind.Overland);
        Assert.Equal(("town:b", "town:far"), (overland.From, overland.To));
        Assert.Equal(300000, overland.Cost, 1f);
    }

    [Fact]
    public void Two_islands_near_each_other_still_join_the_mainland()
    {
        // "x" and "y" walk to each other, but nothing joins them to a and b: the island joins at its nearest place.
        var roads = Roads(Road("ab", 1, 2, (0, 0), (40000, 0)));

        var edges = VanillaEdges.Build([Town("a", 0, 0), Town("b", 40000, 0), Town("x", 40000, 200000), Town("y", 40000, 208000)], roads);

        Assert.Equal(
            [("town:a", "town:b", EdgeKind.Road), ("town:b", "town:x", EdgeKind.Overland), ("town:x", "town:y", EdgeKind.Near)],
            edges.Select(e => (e.From, e.To, e.Kind)));
    }

    [Fact]
    public void Overland_can_be_switched_off()
    {
        var edges = VanillaEdges.Build([Town("a", 0, 0), Town("far", 0, 300000)], roads: null, new EdgeOptions { Overland = false });

        Assert.Empty(edges);
    }

    [Fact]
    public void Neighbours_limits_which_pairs_are_tried()
    {
        // Five towns 5000 apart in a row: with one neighbour each, only next-door pairs are tried.
        EdgePlace[] row = [.. Enumerable.Range(0, 5).Select(i => Town(i.ToString(), i * 5000, 0, radius: 500))];

        var edges = VanillaEdges.Build(row, roads: null, new EdgeOptions { Neighbours = 1 });

        Assert.All(edges, e => Assert.Equal(5000, e.Cost, 0.01f));
        Assert.Equal(4, edges.Count);
    }

    [Fact]
    public void The_result_does_not_depend_on_the_input_order()
    {
        var roads = Roads(Road("ac", 1, 2, (0, 0), (30000, 0)), Road("cb", 2, 3, (30000, 0), (60000, 0)), Road("cd", 2, 4, (30000, 0), (30000, 45000)));
        EdgePlace[] places = [Town("a", 0, 0), Town("b", 60000, 0), Town("c", 30000, 0), Town("d", 30000, 45000), Town("e", 33000, 52000, radius: 900)];

        var first = VanillaEdges.Build(places, roads);
        var second = VanillaEdges.Build([.. places.Reverse()], roads);

        Assert.Equal(first, second);
        Assert.All(first, e => Assert.True(string.CompareOrdinal(e.From, e.To) < 0));
        Assert.Equal(first.OrderBy(e => e.From, StringComparer.Ordinal).ThenBy(e => e.To, StringComparer.Ordinal), first);
    }

    [Fact]
    public void Two_places_with_one_id_are_refused()
    {
        Assert.Throws<ArgumentException>(() => VanillaEdges.Build([Town("a", 0, 0), Town("a", 5000, 0)], roads: null));
    }
}
