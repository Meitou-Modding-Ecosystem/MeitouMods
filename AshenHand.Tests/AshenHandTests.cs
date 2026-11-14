using System.Numerics;
using Meitou.Data;
using Meitou.Data.Fcs;
using Meitou.Data.Gameplay;
using Meitou.Modding;
using Meitou.Simulation;
using Meitou.Simulation.Population;
using Rivalry;
using SimWorld = Meitou.Simulation.World;

namespace AshenHand.Tests;

/// <summary>
/// The Ashen Hand on synthetic data: the generated data file and the two rules.
/// </summary>
public class AshenHandTests
{
    /// <summary>
    /// Where the player's town stands.
    /// </summary>
    static readonly Vector3 TownAt = new(2000, 150, 2000);

    /// <summary>
    /// A new record, as a mod file adds one.
    /// </summary>
    static FcsRecord Rec(string id, FcsRecordType type, string name) => new() { StringId = id, Flags = 0x10, Name = name, RecordType = type };

    /// <summary>
    /// A game with the player's faction and a Dust Bandit who has a faction, a race, clothes, a weapon and talk; then the order's file on top.
    /// </summary>
    static GameDatabase Game()
    {
        var bandit = Rec("10-a", FcsRecordType.Character, AshenHandData.Model);
        bandit.Ints["strength"] = 12;
        bandit.References["faction"] = [new FcsReference("11-a", 0, 0, 0)];
        bandit.References["race"] = [new FcsReference("12-a", 100, 0, 0)];
        bandit.References["clothing"] = [new FcsReference("13-a", 1, 100, 0)];
        bandit.References["weapons"] = [new FcsReference("14-a", 1, 0, 0)];
        bandit.References["dialogue package"] = [new FcsReference("15-a", 0, 0, 0)];

        var file = new FcsFile();
        file.Records.AddRange([
            Rec(FactionData.PlayerId, FcsRecordType.Faction, "Nameless"),
            Rec("11-a", FcsRecordType.Faction, "Dust Bandits"),
            bandit]);
        var db = new GameDatabase();
        db.Apply(file, "test.mod");
        db.Apply(AshenHandData.Build(db), AshenHandData.FileName);
        return db;
    }

    /// <summary>
    /// A world with one player town, Rivalry Core as its only system and the order's rules in it.
    /// </summary>
    static (SimWorld World, RivalrySystem System, PopulationData Data) Make(AshenHandSettings settings)
    {
        var data = PopulationData.Create(Game(), []);
        var system = RivalrySystem.Create(new SystemBuildContext(data, []), new RivalryOptions(), (RngPurpose)0x80001234u, _ => { }, AshenHandMod.Id,
            r => AshenHandMod.Register(r, settings));
        var world = new SimWorld(new WorldSettings { Seed = 3, Threads = 1, PublishSnapshots = false, MinPartitionSize = 1 },
            new OpenGroundWalkability((_, _) => 150), [system]);
        world.Player.Faction = data.FactionIndex(FactionData.PlayerId);
        world.Buildings.AddTown(TownAt, world.Player.Faction, "Home");
        return (world, system, data);
    }

    /// <summary>
    /// Asks for an assessment and runs it.
    /// </summary>
    static void Assess(SimWorld world)
    {
        world.Commands.Enqueue(new AssessNowCommand { Tick = world.Tick });
        world.RunTicks(2);
    }

    [Fact]
    public void The_data_file_copies_the_model_into_the_order_and_disarms_the_thief()
    {
        var db = Game();
        var thief = db.Find(AshenHandData.Thief)!;
        var hunter = db.Find(AshenHandData.Hunter)!;

        Assert.Equal(12, thief.GetInt("strength"));
        Assert.Equal("13-a", Assert.Single(thief.GetReferences("clothing")).TargetStringId);
        Assert.Equal("12-a", Assert.Single(thief.GetReferences("race")).TargetStringId);
        Assert.Equal(AshenHandData.Faction, Assert.Single(thief.GetReferences("faction")).TargetStringId);
        Assert.Empty(thief.GetReferences("weapons"));
        Assert.Empty(thief.GetReferences("dialogue package"));
        Assert.Equal("14-a", Assert.Single(hunter.GetReferences("weapons")).TargetStringId);

        var thieves = SquadTemplate.From(db.Find(AshenHandData.ThiefSquad)!);
        Assert.Equal(new RecordLink(AshenHandData.Thief, 1, 4, 0), Assert.Single(thieves.Squad));
        Assert.Equal(AshenHandData.Faction, thieves.Faction);
    }

    [Fact]
    public void The_data_file_reads_back_as_written()
    {
        var built = AshenHandData.Build(Game());
        using var stream = new MemoryStream();
        FcsWriter.Write(built, stream);
        stream.Position = 0;
        var read = FcsReader.Read(stream);

        Assert.Equal(built.Records.Select(r => (r.StringId, r.Name, r.Type)), read.Records.Select(r => (r.StringId, r.Name, r.Type)));
        Assert.Equal(built.Records[1].References.Keys, read.Records[1].References.Keys);
    }

    [Fact]
    public void Thieves_set_out_only_for_a_rich_player_whose_outpost_holds_enough()
    {
        var settings = new AshenHandSettings { ThiefChancePerDay = float.MaxValue, HoldMin = 50000 };
        var (world, system, _) = Make(settings);

        world.Player.Money = 30000;
        Assess(world);
        Assert.DoesNotContain(system.History, l => l.StartsWith(AshenHandMod.ThievesRule));   // below the net worth

        world.Player.Money = 45000;
        Assess(world);
        Assert.DoesNotContain(system.History, l => l.StartsWith(AshenHandMod.ThievesRule));   // rich, but the outpost holds too little

        settings.HoldMin = 10000;
        Assess(world);
        Assert.Contains(system.History, l => l.StartsWith(AshenHandMod.ThievesRule + ": burgle:"));
    }

    [Fact]
    public void Hunters_come_for_a_character_the_order_holds_a_grudge_against()
    {
        var (world, system, data) = Make(new AshenHandSettings());
        var hot = new CharacterHot { Alive = true, Position = TownAt };
        var cold = new CharacterCold { Name = "Me", Faction = world.Player.Faction, IsPlayer = true, RecordId = "r-Me" };
        var me = world.Characters.Spawn(hot, cold, world.Tick);

        world.RunTicks(2);
        Assess(world);
        Assert.DoesNotContain(system.History, l => l.StartsWith(AshenHandMod.HuntersRule));

        system.Notoriety.Add(data.FactionIndex(AshenHandData.Faction), me, 100);
        Assess(world);
        Assert.Contains(system.History, l => l.StartsWith(AshenHandMod.HuntersRule + ": pursue:"));
    }
}
