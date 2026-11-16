using Meitou.Simulation;

namespace Rivalry;

/// <summary>
/// Rivalry Core's public surface (docs/rivalry-core.md), implemented by the entry type: a dependent gets it with
/// <c>c.Dependency&lt;IRivalryApi&gt;("rivalry.core")</c> in its own <c>Initialize</c>.
/// </summary>
public interface IRivalryApi
{
    /// <summary>The tuning: the player's settings and the code-only values a dependent may set during its <c>Initialize</c>.</summary>
    RivalryOptions Options { get; }

    /// <summary>
    /// The registrations of the mod <paramref name="modId"/> (the dependent's <c>c.Id</c>): probes, conditions, target kinds, behaviours, rules and
    /// directors, each named with that id as prefix. Allowed only before the first world is built (during dependents' <c>Initialize</c>).
    /// </summary>
    IRivalryRegistry For(string modId);

    /// <summary>The framework's state in a world (its report and grudges); null when the world does not run it. Serial steps of that world only.</summary>
    IRivalryState? In(World world);
}

/// <summary>
/// A rule that looks at an assessment and decides what the world does about the player (docs/rivalry-core.md "Directors"). Runs in a serial step; it must
/// only read the world and record decisions, take randomness from <see cref="Assessment.Roll"/>, and keep no world state of its own (cooldowns live in
/// <see cref="Decisions.TryClaim"/>, which is saved and hashed).
/// </summary>
public interface IDirector
{
    string Name { get; }

    void Assess(Assessment assessment, Decisions decisions);
}

/// <summary>Rivalry Core's state in one world. Serial steps of that world only, except the copies (<see cref="History"/>, <see cref="Runs"/>,
/// <see cref="Parties"/>).</summary>
public interface IRivalryState
{
    /// <summary>The latest inspection; <see cref="PlayerReport.Empty"/> before the first.</summary>
    PlayerReport Report { get; }

    NotorietyLedger Notoriety { get; }

    /// <summary>
    /// The named leaders of hit squads and their records, the leaders handed to the law, and each faction's last hit squad.
    /// </summary>
    RivalRoster Rivals { get; }

    /// <summary>The latest decisions, newest last, as log lines (at most 64).</summary>
    IReadOnlyList<string> History { get; }

    /// <summary>
    /// The behaviours under way, as lines.
    /// </summary>
    IReadOnlyList<string> Runs { get; }

    /// <summary>
    /// The parties out now, hostile and friendly: Rivalry Core's runs and one-shot decisions, and other mods' claims (docs/rivalry-core.md
    /// "Party caps"). A copy, refreshed every slow step.
    /// </summary>
    IReadOnlyList<PartyView> Parties { get; }

    /// <summary>
    /// How many parties of the kind are out now.
    /// </summary>
    int PartiesOut(PartyKind kind);

    /// <summary>
    /// How many parties of the kind may be out at once (overrides aside): 1 hostile, the player's setting friendly.
    /// </summary>
    int PartyCap(PartyKind kind);

    /// <summary>
    /// Counts a party the calling mod runs itself under the caps, or renews its claim of the same source and key: true when it holds. False:
    /// the cap is full (send nothing now and ask again later). The claim lapses <see cref="PartyClaim.Hours"/> after it was made or renewed, so
    /// renew it while the party is out and release it when it ends.
    /// </summary>
    bool ClaimParty(PartyClaim claim);

    /// <summary>
    /// Ends a claim made with <see cref="ClaimParty"/>; false when there was none.
    /// </summary>
    bool ReleaseParty(string source, string key);
}
