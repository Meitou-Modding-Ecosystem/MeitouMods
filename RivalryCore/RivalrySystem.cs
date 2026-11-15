using System.Globalization;
using Meitou.Modding;
using Meitou.Simulation;
using Meitou.Simulation.Combat;
using Meitou.Simulation.Dialogue;

namespace Rivalry;

/// <summary>Assess at the next serial step, whatever the cadence (the debug row's "assess now").</summary>
public sealed record AssessNowCommand : SimCommand;

/// <summary>
/// Fires a rule on the target with <see cref="Key"/> (its first target when null), whatever its conditions, chance and cooldown say (the debug row).
/// </summary>
public sealed record FireRuleCommand(string Rule, string? Key) : SimCommand;

/// <summary>
/// Starts a behaviour for a player town (its name, number or key; null: the first) with a faction (its name or string id) acting (the debug rows).
/// </summary>
public sealed record StartBehaviourCommand(string Behaviour, string? Town, string Faction, BehaviourArgs Args) : SimCommand;

/// <summary>Adds to the grudge a faction (its name or string id) holds against characters (the debug row; dependents use <see cref="Decisions.Grudge"/>).</summary>
public sealed record GrudgeCommand(IReadOnlyList<CharacterId> Characters, string Faction, float Amount) : SimCommand;

/// <summary>
/// Rivalry Core in one world (docs/rivalry-core.md): the grudge ledger fed by hits, the behaviours under way, and at each assessment the
/// inspection, the rules, the directors and their decisions, all in serial steps. Its state goes into the world's hash and the save.
/// </summary>
public sealed partial class RivalrySystem : ICombatHitObserver, IStateHashed, IModSaveData, IRivalryState
{
    public const int HistoryLength = 64;

    readonly PopulationData _data;
    readonly IReadOnlyList<IDirector> _directors;
    readonly RivalryOptions _settings;
    readonly RngPurpose _purpose;
    readonly Action<string> _log;
    readonly Inspector _inspector;
    readonly Catalogue _catalogue;
    readonly Actions _actions;
    readonly RuleEngine _rules;
    readonly BehaviourRuns _runs;
    readonly PopulationSystem? _population;
    readonly HunterBarks _barks;
    readonly Cooldowns _cooldowns = new();
    readonly HashSet<string> _faulted = new(StringComparer.Ordinal);
    readonly List<string> _history = [];
    readonly List<SimCommand> _asked = [];
    RivalryOptions _options;
    World? _world;
    double _nextAssess = double.NaN;
    double _lastAssess = double.NaN;
    bool _assessNow;

    internal RivalrySystem(SystemBuildContext context, Registry registry, RivalryOptions settings, RngPurpose purpose, Action<string> log)
    {
        _data = context.Data;
        _directors = [.. registry.Directors.Select(e => e.Value)];
        _settings = settings;
        _purpose = purpose;
        _log = log;
        _options = settings.Copy();
        _inspector = new Inspector(_data, [.. registry.TownProbes.Select(e => (e.Name, e.Value))], [.. registry.CharacterProbes.Select(e => (e.Name, e.Value))]);
        _population = context.Find<PopulationSystem>();
        _catalogue = new Catalogue(_data, _population);
        var memory = context.Find<DialogueSystem>()?.Memory;
        _actions = new Actions(_data, _population, memory, Notoriety, _catalogue);
        _barks = new HunterBarks(context.Find<CombatSystem>());
        _rules = new RuleEngine(registry, log);
        _runs = new BehaviourRuns(_data, registry.Behaviours.ToDictionary(e => e.Name, e => e.Value, StringComparer.Ordinal), log);
    }

    /// <summary>
    /// Rivalry Core for one world with its built-ins and whatever <paramref name="register"/> adds under the mod id <paramref name="modId"/>
    /// (tests, tools; the game goes through <see cref="RivalryMod"/>).
    /// </summary>
    public static RivalrySystem Create(SystemBuildContext context, RivalryOptions settings, RngPurpose purpose, Action<string> log,
        string modId = "test", Action<IRivalryRegistry>? register = null)
    {
        var registry = new Registry();
        BuiltIn.Register(registry.For(RivalryMod.Id, "rivalry."));
        register?.Invoke(registry.For(modId, modId + "."));
        return new RivalrySystem(context, registry, settings, purpose, log);
    }

    public PlayerReport Report { get; private set; } = PlayerReport.Empty;
    public NotorietyLedger Notoriety { get; } = new();

    /// <summary>
    /// The named leaders of hit squads, the leaders handed to the law, and each faction's last hit squad.
    /// </summary>
    public RivalRoster Rivals { get; } = new();

    /// <summary>A copy, safe to read from the host's thread.</summary>
    public IReadOnlyList<string> History { get; private set; } = [];

    /// <summary>
    /// The behaviours under way, as lines; a copy, safe to read from the host's thread.
    /// </summary>
    public IReadOnlyList<string> Runs { get; private set; } = [];

    /// <summary>
    /// The rules' names, in evaluation order.
    /// </summary>
    public IEnumerable<string> RuleNames => _rules.Names;

    public void Inputs(World world, IReadOnlyList<SimCommand> commands)
    {
        _options = _settings.Copy();
        foreach (var command in commands)
        {
            if (command is AssessNowCommand) _assessNow = true;
            else if (command is GrudgeCommand g) AddGrudge(g);
            else if (command is FireRuleCommand or StartBehaviourCommand) _asked.Add(command);
        }
    }

    public void SlowWorld(World world)
    {
        _world = world;
        ResolveSaved(world);
        CheckKills(world);
        StepRuns(world);
        _barks.Step(world, _runs.All, Rivals, _options);
        RunAsked(world);
        if (double.IsNaN(_nextAssess)) _nextAssess = world.GameHours;
        if (!_assessNow && world.GameHours < _nextAssess) return;

        _assessNow = false;
        Assess(world);
    }

    void AddGrudge(GrudgeCommand g)
    {
        int faction = FactionByName(g.Faction);
        if (faction < 0) return;
        foreach (var who in g.Characters) Notoriety.Add(faction, who, g.Amount);
    }

    int FactionByName(string text)
    {
        if (_data.FactionIndex(text) is >= 0 and var byId) return byId;
        for (int f = 0; f < _data.Factions.Count; f++)
            if (string.Equals(_data.Factions[f].Name, text.Trim(), StringComparison.OrdinalIgnoreCase)) return f;
        return -1;
    }

    /// <summary>
    /// Steps the behaviours under way and publishes their lines.
    /// </summary>
    void StepRuns(World world)
    {
        if (_runs.All.Count > 0) _runs.Step(Context(world));
        Rivals.Idle(_runs.All.Where(r => r.Args.Text("leaderName") is not null).Select(LeaderOf));
        PublishRuns();
    }

    /// <summary>
    /// The faction and leader a run names, for <see cref="RivalRoster.Idle"/>.
    /// </summary>
    (string Faction, string Name) LeaderOf(BehaviourRun run)
    {
        string faction = run.Faction >= 0 && run.Faction < _data.Factions.Count ? _data.Factions[run.Faction].Id : "";
        return (faction, run.Args.Text("leaderName")!);
    }

    /// <summary>
    /// The runs as lines for the debug row.
    /// </summary>
    void PublishRuns()
    {
        if (_runs.All.Count == 0 && Runs.Count == 0) return;

        Runs = [.. _runs.All.Select(r => $"run {r.Id} {r.Behaviour} town {r.Town} platoon {r.Platoon} phase {r.Phase} until {r.Until:0.#} h")];
    }

    /// <summary>
    /// The debug rows' asks, in the order they came: a rule fired, a behaviour started.
    /// </summary>
    void RunAsked(World world)
    {
        if (_asked.Count == 0) return;

        Report = _inspector.Inspect(world);
        var decided = new List<Decision>();
        foreach (var command in _asked)
        {
            if (command is FireRuleCommand fire) Remember($"debug: {_rules.Fire(NewAssessment(world), fire.Rule, TownKey(fire.Key), decided, _cooldowns)}");
            else if (command is StartBehaviourCommand start) Start(start, decided);
        }

        _asked.Clear();
        foreach (var decision in decided) Carry(world, decision);
    }

    /// <summary>
    /// A debug start as a decision, or a log line when the player has no such town.
    /// </summary>
    void Start(StartBehaviourCommand start, List<Decision> decided)
    {
        if (TownOf(start.Town) is not { } town)
        {
            Remember($"debug: {start.Behaviour}: no player town {start.Town}");
            return;
        }

        var target = new RuleTarget($"town{town}") { Town = town, Faction = FactionByName(start.Faction) };
        decided.Add(new StartDecision("debug", start.Behaviour, target, start.Args));
    }

    /// <summary>
    /// A debug row's target key with a player town's name or number turned into its key (<c>town{number}</c>); any other text as typed.
    /// </summary>
    string? TownKey(string? key) => key is not null && TownOf(key) is { } town ? $"town{town}" : key;

    /// <summary>
    /// The number of the player town a debug row names: by key, number or name (case ignored); empty or null: the first; null when none matches.
    /// </summary>
    int? TownOf(string? text)
    {
        var towns = Report.Towns;
        if (string.IsNullOrWhiteSpace(text)) return towns.Count > 0 ? towns[0].Number : null;

        string name = text.Trim();
        string number = name.StartsWith("town", StringComparison.OrdinalIgnoreCase) ? name[4..] : name;
        var match = towns.FirstOrDefault(t => t.Number.ToString(CultureInfo.InvariantCulture) == number)
            ?? towns.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
        return match?.Number;
    }

    void Assess(World world)
    {
        double hours = double.IsNaN(_lastAssess) ? 0 : world.GameHours - _lastAssess;
        _lastAssess = world.GameHours;
        _nextAssess = world.GameHours + Math.Max(1, _options.AssessEveryHours);
        Notoriety.Cool(world.Characters, hours, _options.GrudgeCoolPerHour);
        _cooldowns.Expire(world.GameHours);
        Report = _inspector.Inspect(world);
        if (Report.PlayerFaction < 0) return;

        var decided = Decide(world);
        foreach (var decision in decided) Carry(world, decision);
    }

    Assessment NewAssessment(World world) => new(world, _data, Report, Notoriety, _options, _purpose, _catalogue, Rivals, _runs.All);

    BehaviourContext Context(World world) =>
        new(world, _data, Report, _options, _purpose, _actions, _runs, _population, _inspector, Notoriety, Rivals);

    List<Decision> Decide(World world)
    {
        var assessment = NewAssessment(world);
        var decided = new List<Decision>();
        _rules.Evaluate(assessment, decided, _cooldowns);
        foreach (var director in _directors)
        {
            if (_faulted.Contains(director.Name)) continue;
            try
            {
                director.Assess(assessment, new Decisions(director.Name, decided, _cooldowns, world.GameHours));
            }
            catch (Exception e)
            {
                _faulted.Add(director.Name);
                _log($"director {director.Name} threw and is off for this world: {e.Message}");
            }
        }
        return decided;
    }

    void Carry(World world, Decision decision)
    {
        string line;
        try
        {
            line = decision is StartDecision s
                ? _runs.Start(Context(world), s.Behaviour, new BehaviourRequest(s.Director, s.Target, s.Args))
                : _actions.Apply(world, decision, _options, Report);
        }
        catch (Exception e)
        {
            line = $"{decision.GetType().Name} failed: {e.Message}";
        }
        Remember($"{decision.Director}: {line}");
        PublishRuns();
    }

    void Remember(string line)
    {
        _log(line);
        _history.Add(line);
        if (_history.Count > HistoryLength) _history.RemoveAt(0);
        History = [.. _history];
    }

    public void Hash(ref StateHasher h)
    {
        Notoriety.Hash(ref h);
        _cooldowns.Hash(ref h);
        h.Add(_nextAssess);
        h.Add(_faulted.Count + _rules.Faulted);
        HashVictims(ref h);
        _runs.Hash(ref h);
        Rivals.Hash(ref h);
    }
}
