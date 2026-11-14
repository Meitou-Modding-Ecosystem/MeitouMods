using System.Numerics;
using Meitou.Simulation;

namespace Rivalry;

/// <summary>
/// Hunters come for one character (docs/rivalry-core.md "Pursue"): a squad appears out of sight of its quarry, walks to wherever the quarry is
/// now, and attacks. Texts: <c>template</c> (a SQUAD_TEMPLATE). Numbers: <c>hours</c> before it gives up (24), <c>minRadius</c> (800),
/// <c>maxRadius</c> (1500), and skill ranges (<see cref="SquadDriver.RollSkills"/>). The quarry is the target's character; it is not saved
/// (slots change on load), so a loaded run goes for the standing player character with the faction's largest grudge.
/// </summary>
public sealed class PursueBehaviour : IBehaviour
{
    /// <summary>
    /// The phases: 0 spawning, 1 waiting for the platoon to load, 2 closing in, 3 attacking.
    /// </summary>
    public const int Spawning = 0, Loading = 1, Closing = 2, Attacking = 3;

    /// <summary>
    /// How long a spawn is tried and a platoon may take to load, in game hours.
    /// </summary>
    public const double SpawnWindowHours = 24, LoadWindowHours = 24;

    /// <summary>
    /// How near the quarry the hunters stop walking and attack.
    /// </summary>
    public const float StrikeRange = 150;

    /// <summary>
    /// The quarry of each run in this world's process, by run id; lost on load.
    /// </summary>
    readonly Dictionary<(World, int), CharacterId> _quarry = [];

    public string Name => BuiltIn.Pursue;

    public string Start(BehaviourContext context, BehaviourRequest request)
    {
        if (context.Population is null) return "pursue: no population";
        if (request.Args.Text("template") is null) return "pursue: no template";
        if (!context.World.Characters.TryResolveNext(request.Target.Character, out _)) return "pursue: quarry gone";

        var run = context.Begin(this, request, context.World.GameHours + SpawnWindowHours);
        _quarry[(context.World, run.Id)] = request.Target.Character;
        Step(context, run);
        return $"pursue: run {run.Id} after {context.Report.Find(request.Target.Character)?.Name}"
            + (run.Phase == Spawning ? ", waiting for a spot" : $", platoon {run.Platoon}");
    }

    public bool Step(BehaviourContext context, BehaviourRun run)
    {
        if (run.Phase >= Closing) SquadDriver.Keep(context.World, run.Platoon);

        bool goes = run.Phase switch
        {
            Spawning => Spawn(context, run),
            Loading => Load(context, run),
            _ => Chase(context, run),
        };
        if (!goes) _quarry.Remove((context.World, run.Id));
        return goes;
    }

    /// <summary>
    /// The run's quarry while it stands; none when it is down, dead or gone.
    /// </summary>
    CharacterId Quarry(BehaviourContext context, BehaviourRun run)
    {
        if (!_quarry.TryGetValue((context.World, run.Id), out var id))
        {
            id = context.Report.Characters
                .Where(c => !c.Down)
                .OrderByDescending(c => context.Notoriety.Grudge(run.Faction, c.Id))
                .ThenBy(c => c.Name, StringComparer.Ordinal)
                .Select(c => c.Id)
                .DefaultIfEmpty(CharacterId.None)
                .First();
            _quarry[(context.World, run.Id)] = id;
        }

        var table = context.World.Characters;
        return table.TryResolveNext(id, out int slot) && table.Cold(slot) is { Medical.Incapacitated: false } ? id : CharacterId.None;
    }

    /// <summary>
    /// Phase 0: spawns the platoon in the ring round the quarry and holds it.
    /// </summary>
    bool Spawn(BehaviourContext context, BehaviourRun run)
    {
        var world = context.World;
        if (context.Population is not { } population || Quarry(context, run) is not { IsNone: false } quarry) return false;

        var position = world.Characters.Next[quarry.Slot].Position;
        var at = new Vector2(position.X, position.Z);
        int site = VisitBehaviour.NearestSite(context.Data, at);
        int platoon = site < 0 ? -1 : population.SpawnPlatoon(world, run.Faction, run.Args.Text("template")!, site, at,
            (float)run.Args.Number("minRadius", 800), (float)run.Args.Number("maxRadius", 1500));
        if (platoon < 0) return site >= 0 && world.GameHours < run.Until;

        PopulationSystem.HoldPlatoon(world, platoon, true);
        run.Platoon = platoon;
        run.Phase = Loading;
        run.Until = world.GameHours + LoadWindowHours;
        return true;
    }

    /// <summary>
    /// Phase 1: once the squad is in the world it is taken from the AI and remembers its quarry as an enemy.
    /// </summary>
    bool Load(BehaviourContext context, BehaviourRun run)
    {
        var world = context.World;
        if (world.Platoons.Find(run.Platoon) is null) return false;
        if (SquadDriver.SquadOf(world, run.Platoon) is not { } squad) return world.GameHours < run.Until || SquadDriver.GiveBack(world, run.Platoon);

        SquadDriver.Hold(world, run.Platoon, squad, held: true);
        SquadDriver.RollSkills(context, run, squad);
        if (Quarry(context, run) is { IsNone: false } quarry)
            context.System<DialogueSystem>()?.Memory.Flag(world, squad.Id, quarry, MemoryTag.TempEnemy);

        run.Phase = Closing;
        run.Until = world.GameHours + run.Args.Number("hours", 24);
        return true;
    }

    /// <summary>
    /// Phases 2 and 3: the hunters run to where the quarry is now; in range they are ordered to attack and left to the fight. The quarry down or
    /// gone, every hunter down, or time up: the squad is given back to the game.
    /// </summary>
    bool Chase(BehaviourContext context, BehaviourRun run)
    {
        var world = context.World;
        var table = world.Characters;
        if (SquadDriver.SquadOf(world, run.Platoon) is not { } squad) return SquadDriver.GiveBack(world, run.Platoon);

        var hunters = SquadDriver.Able(world, squad);
        var quarry = Quarry(context, run);
        if (hunters.Count == 0 || quarry.IsNone || world.GameHours >= run.Until)
        {
            SquadDriver.Hold(world, run.Platoon, squad, held: false);
            return false;
        }

        var at = table.Next[quarry.Slot].Position;
        bool near = hunters.Any(h => SquadDriver.Distance(world, h, at) <= StrikeRange);
        if (!near)
        {
            foreach (int hunter in hunters) SquadDriver.WalkTo(world, hunter, at, run: true, redirect: true);
            run.Phase = Closing;
            return true;
        }

        var idle = hunters.Where(h => !table.Cold(h)!.InCombat).Select(table.IdOf).ToList();
        if (idle.Count > 0) world.Defer(new AttackOrder(idle, quarry) { Tick = world.Tick + 1, Source = TaskSource.Npc });
        run.Phase = Attacking;
        return true;
    }
}
