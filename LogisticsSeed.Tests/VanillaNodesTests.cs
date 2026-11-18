using System.Numerics;
using Meitou.Data;
using Meitou.Data.Fcs;
using Meitou.Data.Gameplay;
using Meitou.Data.World;
using Meitou.Simulation.Population;

namespace Logistics.Seed.Tests;

/// <summary>
/// A small Bast in memory: Stack held by the Holy Nation, which the Shek take when the Phoenix dies; from there Kral's Chosen take it when
/// Esata dies too, and a later version (no faction of its own) only rebuilds it under whoever holds it. Plus a nest and a point of interest.
/// </summary>
public class VanillaNodesTests
{
    const string Holy = "hn-f", Shek = "shek-f", Kral = "kral-f";

    static FcsRecord Rec(string id, FcsRecordType type, string name) => new() { StringId = id, Flags = 0x10, Name = name, RecordType = type };
    static FcsReference Ref(string id, int v0 = 0) => new(id, v0, 0, 0);

    static FcsRecord Town(string id, string name, TownType type, string? faction, params string[] overrides)
    {
        var t = Rec(id, FcsRecordType.Town, name);
        t.Ints["type"] = (int)type;
        if (faction is not null) t.References["faction"] = [Ref(faction)];
        if (overrides.Length > 0) t.References["override town"] = [.. overrides.Select(o => Ref(o))];
        return t;
    }

    static PopulationData Data()
    {
        var phoenixDead = Rec("phoenix-dead", FcsRecordType.WorldEventState, "Phoenix dead");
        var esataDead = Rec("esata-dead", FcsRecordType.WorldEventState, "Esata dead");
        var stack = Town("stack", "Stack", TownType.Town, Holy, "stack-shek");
        var stackShek = Town("stack-shek", "Stack (Shek)", TownType.Town, Shek, "stack-kral", "stack-rebuilt");
        stackShek.References["world state"] = [Ref("phoenix-dead", 1)];
        var stackKral = Town("stack-kral", "Stack (Kral's Chosen)", TownType.Town, Kral, "stack");   // loops back to the base
        stackKral.References["world state"] = [Ref("esata-dead", 1), Ref("phoenix-dead", 1), Ref("not-a-state", 1)];
        var stackRebuilt = Town("stack-rebuilt", "Stack (rebuilt)", TownType.Town, null);
        var den = Town("den", "Beak Den", TownType.Nest, "beasts-f");
        var poi = Town("poi", "Old Statue", TownType.PointOfInterest, null);
        var file = new FcsFile();
        file.Records.AddRange([Rec("204-gamedata.base", FcsRecordType.Faction, "Nameless"), Rec(Holy, FcsRecordType.Faction, "The Holy Nation"),
            Rec(Shek, FcsRecordType.Faction, "Shek Kingdom"), Rec(Kral, FcsRecordType.Faction, "Kral's Chosen"),
            Rec("beasts-f", FcsRecordType.Faction, "Beasts"), phoenixDead, esataDead, stack, stackShek, stackKral, stackRebuilt, den, poi]);
        var db = new GameDatabase();
        db.Apply(file, "test.mod");
        return PopulationData.Create(db, [
            new TownPlacement("9-test-INGAME", "stack", new Vector3(1000, 0, 2000), null),
            new TownPlacement("12-test-INGAME", "den", new Vector3(5000, 0, 5000), null),
            new TownPlacement("13-test-INGAME", "poi", new Vector3(7000, 0, 7000), null),
            new TownPlacement("", "stack", new Vector3(0, 0, 0), null)]);              // no instance id: not a node
    }

    [Fact]
    public void Every_placed_town_is_a_node_keyed_by_its_instance_id()
    {
        var nodes = VanillaNodes.Build(Data());
        Assert.Equal(["town:12-test-INGAME", "town:13-test-INGAME", "town:9-test-INGAME"], nodes.Select(n => n.Id));
        var stack = nodes.Single(n => n.Name == "Stack");
        Assert.Equal(("stack", TownType.Town, Holy, new Vector3(1000, 0, 2000)), (stack.BaseRecord, stack.Kind, stack.Owner, stack.Position));
    }

    [Fact]
    public void Allowed_owners_follow_the_override_chain_and_a_version_without_a_faction_keeps_the_holder()
    {
        var stack = VanillaNodes.Build(Data()).Single(n => n.Name == "Stack");
        Assert.Equal([Holy, Kral, Shek], stack.AllowedOwners);                         // ordinal, no repeats
        Assert.Equal(["stack-shek", "stack-kral", "stack-rebuilt"], stack.Versions.Select(v => v.Record));
        Assert.Equal(Shek, stack.Versions.Single(v => v.Record == "stack-rebuilt").Faction);   // rebuilt under the Shek who held it
        Assert.Equal([new StateCondition("phoenix-dead", 1)], stack.Versions.Single(v => v.Record == "stack-shek").When);
        Assert.Equal([new StateCondition("esata-dead", 1), new StateCondition("phoenix-dead", 1)],
            stack.Versions.Single(v => v.Record == "stack-kral").When);                // only real world states
    }

    [Fact]
    public void Nests_and_points_of_interest_are_nodes_but_not_ownable()
    {
        var nodes = VanillaNodes.Build(Data());
        Assert.True(nodes.Single(n => n.Name == "Stack").Ownable);
        var den = nodes.Single(n => n.Name == "Beak Den");
        Assert.False(den.Ownable);
        Assert.Equal(["beasts-f"], den.AllowedOwners);
        var poi = nodes.Single(n => n.Name == "Old Statue");
        Assert.False(poi.Ownable);
        Assert.Null(poi.Owner);
        Assert.Empty(poi.AllowedOwners);
    }

    [Fact]
    public void The_same_data_gives_the_same_nodes()
    {
        var a = VanillaNodes.Build(Data());
        var b = VanillaNodes.Build(Data());
        Assert.Equal(a.Select(n => (n.Id, string.Join(",", n.AllowedOwners), string.Join(",", n.Versions.Select(v => v.Record)))),
            b.Select(n => (n.Id, string.Join(",", n.AllowedOwners), string.Join(",", n.Versions.Select(v => v.Record)))));
    }
}
