using Meitou.Data.Gameplay;
using Meitou.Simulation;

namespace Rivalry;

/// <summary>
/// What a director sees at one assessment: the report, the grudges, the world (read only), the data, the tuning and a deterministic random source.
/// </summary>
public sealed class Assessment
{
    readonly RngPurpose _purpose;
    readonly Catalogue _catalogue;

    /// <summary>
    /// The behaviours under way.
    /// </summary>
    readonly IReadOnlyList<BehaviourRun> _runs;

    /// <summary>
    /// The party caps.
    /// </summary>
    readonly Parties _parties;

    /// <summary>
    /// The decisions recorded so far at this assessment, not carried out yet: they count as parties out.
    /// </summary>
    readonly IReadOnlyList<Decision> _pending;

    internal Assessment(World world, PopulationData data, PlayerReport report, NotorietyLedger notoriety, RivalryOptions options, RngPurpose purpose,
        Catalogue catalogue, RivalRoster rivals, IReadOnlyList<BehaviourRun> runs, Parties parties, IReadOnlyList<Decision> pending)
    {
        _catalogue = catalogue;
        _runs = runs;
        _parties = parties;
        _pending = pending;
        Rivals = rivals;
        World = world;
        Data = data;
        Report = report;
        Notoriety = notoriety;
        Options = options;
        _purpose = purpose;
    }

    /// <summary>The running world. Read only: changes go through <see cref="Decisions"/>.</summary>
    public World World { get; }

    public PopulationData Data { get; }
    public PlayerReport Report { get; }
    public NotorietyLedger Notoriety { get; }
    public RivalryOptions Options { get; }

    /// <summary>
    /// The named leaders of hit squads, the leaders handed to the law, and when each faction's last hit squad finished. Read only here.
    /// </summary>
    public RivalRoster Rivals { get; }

    /// <summary>
    /// Whether one of the faction's hit squads (<see cref="BuiltIn.Pursue"/>) is on its way or fighting.
    /// </summary>
    public bool HitSquadUnderway(int faction) => _runs.Any(r => r.Faction == faction && PursueBehaviour.HitSquadUnderway(r));

    /// <summary>
    /// How many parties of the kind are out (docs/rivalry-core.md "Party caps"): runs under way, one-shot decisions and other mods' claims still
    /// counting, and the decisions recorded earlier at this assessment.
    /// </summary>
    public int PartiesOut(PartyKind kind) =>
        _parties.Count(kind, _runs, Report, GameHours) + _pending.Count(d => _parties.KindOf(d) == kind);

    /// <summary>
    /// Whether <paramref name="source"/> (a rule's or director's name) may send one more party of the kind now: under the cap, or under its
    /// override. Check it before claiming a cooldown: a start over the cap is refused when carried out, and the cooldown would be spent.
    /// </summary>
    public bool MayStart(PartyKind kind, string source) => _parties.Admits(kind, source, PartiesOut(kind), Options);

    /// <summary>
    /// The party kind of a behaviour by name; none when it is not registered.
    /// </summary>
    public PartyKind KindOf(string behaviour) => _parties.KindOf(behaviour);

    /// <summary>
    /// Game hours since the faction's last hit squad finished; infinity when none has.
    /// </summary>
    public double HoursSinceHitSquad(int faction) =>
        Faction(faction) is { } f ? Rivals.HoursSinceEnded(f.Id, GameHours) : double.PositiveInfinity;

    /// <summary>
    /// How many of the faction's squads in the world a grudge hunt could mark the character for now: within <see cref="RivalryOptions.HuntRadius"/>,
    /// leader alive, in no campaign, not a town's own unless <see cref="RivalryOptions.UseTownSquads"/>.
    /// </summary>
    public int SquadsNear(int faction, CharacterId who) => World.Characters.TryResolveNext(who, out int slot)
        ? Actions.Candidates(World, faction, World.Characters.Next[slot].Position, Options.HuntRadius, Options.UseTownSquads).Count()
        : 0;

    public int PlayerFaction => Report.PlayerFaction;
    public double GameHours => World.GameHours;

    /// <summary>A uniform number in [0, 1), the same for the same world seed, tick and key: never <c>Random</c>, which would break the determinism check.</summary>
    public float Roll(string key) => Rng.Float(Rng.Hash(World.Seed, (ulong)World.Tick, _purpose, Rng.StableHash(key)));

    public float Relation(int faction) => Known(faction) ? Data.Relations.Get(faction, PlayerFaction) : 0;

    public bool IsHostile(int faction) => Known(faction) && Data.Relations.IsHostile(faction, PlayerFaction);

    /// <summary>An ally of the player and not the player's own faction.</summary>
    public bool IsAlly(int faction) => Known(faction) && faction != PlayerFaction && Data.Relations.IsAlly(faction, PlayerFaction);

    /// <summary>Real factions other than the player's (FCS <c>not real</c> ones left out), in data order.</summary>
    public IEnumerable<int> OtherFactions()
    {
        for (int f = 0; f < Data.Factions.Count; f++)
            if (f != PlayerFaction && !Data.Factions[f].NotReal) yield return f;
    }

    /// <summary>The campaign of a kind a raid or reinforcement by the faction would request (<see cref="Catalogue.CampaignFor"/>); null when it has none.</summary>
    public string? CampaignFor(int faction, CampaignKey key) => _catalogue.CampaignFor(faction, key);

    public FactionData? Faction(int faction) => faction >= 0 && faction < Data.Factions.Count ? Data.Factions[faction] : null;

    bool Known(int faction) => PlayerFaction >= 0 && Data.Relations.Contains(faction) && Data.Relations.Contains(PlayerFaction);
}
