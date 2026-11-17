using Meitou.Simulation;

namespace Rivalry;

/// <summary>
/// Hit squads speak (docs/rivalry-core.md "Hunter barks"): a member of a pursuing squad (<see cref="BuiltIn.Pursue"/>, closing in or attacking) that starts
/// a fight with someone who is not the player's says a "distracted" line, so the player sees why the squad is held up; when the squad first comes to
/// blows with one of the player's characters, its named leader (or the member, if the leader cannot) speaks: of its last fight, of a predecessor handed
/// to the law, or its first words (<see cref="HunterLines"/>). Display only: lines go through <see cref="World.Bark"/>, and this class's state is neither
/// hashed nor saved, as nothing the simulation does depends on it.
/// </summary>
internal sealed class HunterBarks(CombatSystem? combat)
{
    /// <summary>
    /// Ticks between looks at the hunters' fights (half a second at 30 ticks a second).
    /// </summary>
    const int CheckEveryTicks = 15;

    /// <summary>
    /// Members remembered past this count are pruned of those no longer in the world.
    /// </summary>
    const int PruneAbove = 256;

    /// <summary>
    /// Whom each hunter fought at the last look, so a line is said when a fight starts, not every look.
    /// </summary>
    readonly Dictionary<CharacterId, CharacterId> _foes = [];

    /// <summary>
    /// The tick each run's squad last spoke, for <see cref="RivalryOptions.BarkCooldownSeconds"/>.
    /// </summary>
    readonly Dictionary<int, long> _spokeAt = [];

    /// <summary>
    /// The runs whose squad has come to blows with the player and said so.
    /// </summary>
    readonly HashSet<int> _engaged = [];

    /// <summary>
    /// One look at the pursuing squads' fights (a serial step).
    /// </summary>
    public void Step(World world, IReadOnlyList<BehaviourRun> runs, RivalRoster rivals, RivalryOptions options)
    {
        if (!options.HunterBarks || combat is null || world.Tick % CheckEveryTicks != 0) return;

        foreach (var run in runs)
        {
            if (run.Behaviour != BuiltIn.Pursue || run.Phase is not (PursueBehaviour.Closing or PursueBehaviour.Attacking)) continue;
            if (SquadDriver.SquadOf(world, run.Platoon) is not { } squad) continue;

            Watch(world, run, squad, rivals, options);
        }

        Prune(world, runs);
    }

    /// <summary>
    /// Each fighting member's new foe: the player's, the squad's opening words once; anyone else, a distracted line at most once per cooldown.
    /// </summary>
    void Watch(World world, BehaviourRun run, Squad squad, RivalRoster rivals, RivalryOptions options)
    {
        foreach (var member in squad.Members)
        {
            if (NewFoe(world, member) is not { } foe) continue;

            if (IsPlayers(world, foe)) Engage(world, run, squad, member, rivals);
            else if (MaySpeak(world, run.Id, options)) Say(world, run, member, Line(world, run, member, HunterLines.Distracted, rivals, options));
        }
    }

    /// <summary>
    /// The squad's first blow against the player: the leader speaks from its record when it can, else the member says an engage line.
    /// </summary>
    void Engage(World world, BehaviourRun run, Squad squad, CharacterId member, RivalRoster rivals)
    {
        if (!_engaged.Add(run.Id)) return;

        var leaderId = Leader(world, run);
        bool leaderSpeaks = leaderId is { } l && squad.Members.Contains(l) && Able(world, l);
        var speaker = leaderSpeaks ? leaderId!.Value : member;
        string kind = leaderSpeaks ? LeaderKind(world, run, rivals) : HunterLines.Engage;
        Say(world, run, speaker, Line(world, run, speaker, kind, rivals, null));
    }

    /// <summary>
    /// What the leader opens with: back after a fight, its last outcome; new, and its faction lost a leader to the law, the reckoning; else its first
    /// words.
    /// </summary>
    static string LeaderKind(World world, BehaviourRun run, RivalRoster rivals)
    {
        if (Record(world, run, rivals) is not { } leader) return HunterLines.First;
        if (leader.Remembered != RivalOutcome.None) return HunterLines.ForReturn(leader.Remembered);

        return rivals.LastReckoning(leader.Faction) is null ? HunterLines.First : HunterLines.Reckoning;
    }

    /// <summary>
    /// A line of the run's list, with its placeholders filled.
    /// </summary>
    static string Line(World world, BehaviourRun run, CharacterId speaker, string kind, RivalRoster rivals, RivalryOptions? options)
    {
        var leader = Record(world, run, rivals);
        var fill = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["target"] = run.Args.Text("quarryName") ?? "",
            ["name"] = NameOf(world, speaker),
            ["leader"] = leader is null ? "" : rivals.LastReckoning(leader.Faction)?.Leader ?? "",
            ["fallen"] = (leader?.LastComradesLost ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        ulong pick = Rng.Mix((ulong)world.Tick ^ (ulong)run.Id << 32 ^ (ulong)speaker.Slot << 16);
        return HunterLines.Pick(run.Args, kind, pick, fill, options?.OffTargetLine);
    }

    /// <summary>
    /// Says a line over the speaker's head and notes when the squad spoke.
    /// </summary>
    void Say(World world, BehaviourRun run, CharacterId speaker, string line)
    {
        if (line.Length == 0) return;

        world.Bark(speaker, line);
        _spokeAt[run.Id] = world.Tick;
    }

    /// <summary>
    /// The run's leader in the roster; null when it has none.
    /// </summary>
    static RivalLeader? Record(World world, BehaviourRun run, RivalRoster rivals)
    {
        if (run.Args.Text("leaderName") is not { } name) return null;

        return rivals.Leaders.FirstOrDefault(l => l.Name == name && l.Serial == (uint)run.Args.Number("leaderSerial"));
    }

    /// <summary>
    /// The run's leader's character id; null when it is not in the world.
    /// </summary>
    static CharacterId? Leader(World world, BehaviourRun run)
    {
        uint serial = (uint)run.Args.Number("leaderSerial");
        if (serial == 0) return null;

        var table = world.Characters;
        if (SquadDriver.SquadOf(world, run.Platoon) is not { } squad) return null;

        foreach (var id in squad.Members)
            if (table.TryResolveNext(id, out int slot) && table.Cold(slot)?.PortraitSerial == serial) return id;

        return null;
    }

    /// <summary>
    /// Whether a character is up and about.
    /// </summary>
    static bool Able(World world, CharacterId id) =>
        world.Characters.TryResolveNext(id, out int slot) && world.Characters.Cold(slot) is { Medical.Incapacitated: false };

    /// <summary>
    /// Whether a character is the player's.
    /// </summary>
    static bool IsPlayers(World world, CharacterId id) =>
        world.Characters.TryResolveNext(id, out int slot) && world.Characters.Cold(slot) is { } c
        && (c.IsPlayer || world.Player.Faction >= 0 && c.Faction == world.Player.Faction);

    /// <summary>
    /// A character's name; empty when it is gone.
    /// </summary>
    static string NameOf(World world, CharacterId id) =>
        world.Characters.TryResolveNext(id, out int slot) ? world.Characters.Cold(slot)?.Name ?? "" : "";

    /// <summary>
    /// The character <paramref name="member"/> fights when it started on them since the last look; null when it is not in combat, is down, fights
    /// someone down or gone, or fights the same one as before.
    /// </summary>
    CharacterId? NewFoe(World world, CharacterId member)
    {
        var table = world.Characters;
        if (!table.TryResolveNext(member, out int slot) || table.Cold(slot) is not { Medical.Incapacitated: false, InCombat: true })
        {
            _foes.Remove(member);
            return null;
        }

        var (targetSlot, generation) = combat!.TargetOf(slot, member.Generation);
        var foe = new CharacterId(targetSlot, generation);
        if (targetSlot < 0 || !Able(world, foe))
        {
            _foes.Remove(member);
            return null;
        }
        if (_foes.TryGetValue(member, out var before) && before == foe) return null;

        _foes[member] = foe;
        return foe;
    }

    /// <summary>
    /// Whether the run's squad last spoke at least <see cref="RivalryOptions.BarkCooldownSeconds"/> ago.
    /// </summary>
    bool MaySpeak(World world, int run, RivalryOptions options)
    {
        long cooldown = (long)(options.BarkCooldownSeconds / world.TickSeconds);
        return !_spokeAt.TryGetValue(run, out long at) || world.Tick - at >= cooldown;
    }

    /// <summary>
    /// Drops remembered fights of members no longer in the world once there are many, and the state of runs that ended.
    /// </summary>
    void Prune(World world, IReadOnlyList<BehaviourRun> runs)
    {
        if (_foes.Count > PruneAbove)
            foreach (var gone in _foes.Keys.Where(m => !world.Characters.IsAlive(m)).ToList()) _foes.Remove(gone);

        if (_engaged.Count + _spokeAt.Count == 0) return;

        var live = runs.Select(r => r.Id).ToHashSet();
        _engaged.RemoveWhere(id => !live.Contains(id));
        foreach (int id in _spokeAt.Keys.Where(id => !live.Contains(id)).ToList()) _spokeAt.Remove(id);
    }
}
