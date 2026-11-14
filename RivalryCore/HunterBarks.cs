using Meitou.Simulation;

namespace Rivalry;

/// <summary>
/// Hunters speak (docs/rivalry-core.md "Hunter barks"): a member of a hunting squad that starts a fight with anyone but its quarry says
/// <see cref="RivalryOptions.OffTargetLine"/> over its head. Display only: the line goes through <see cref="World.Bark"/>, and this class's state
/// (whom each member last fought, when each squad last spoke) is neither hashed nor saved, as nothing the simulation does depends on it.
/// </summary>
internal sealed class HunterBarks(CombatSystem? combat, SquadMemory? memory)
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
    /// The tick each squad last spoke, for <see cref="RivalryOptions.BarkCooldownSeconds"/>.
    /// </summary>
    readonly Dictionary<int, long> _spokeAt = [];

    /// <summary>
    /// One look at the hunting squads' fights (a serial step). A hunting squad is one of a faction whose grudge against a player character is at
    /// least <see cref="RivalryOptions.HuntThreshold"/> and that holds its TEMP_ENEMY tag on that character, which is what a hunt sets.
    /// </summary>
    public void Step(World world, NotorietyLedger ledger, RivalryOptions options)
    {
        if (!options.HunterBarks || combat is null || memory is null || world.Tick % CheckEveryTicks != 0) return;

        var hunted = Hunted(ledger, options);
        if (hunted.Count == 0) return;

        foreach (var squad in world.Squads.All)
        {
            if (squad.Dead || squad.Faction == world.Player.Faction || !hunted.TryGetValue(squad.Faction, out var marks)) continue;
            if (!memory.AnyTempEnemy(squad.Id) || Quarry(world, squad, marks) is not { } quarry) continue;

            Watch(world, squad, quarry, options);
        }

        Prune(world);
    }

    /// <summary>
    /// Per faction, the characters it holds a hunting grudge against, largest grudge first.
    /// </summary>
    static Dictionary<int, List<CharacterId>> Hunted(NotorietyLedger ledger, RivalryOptions options) =>
        ledger.Entries
            .Where(g => g.Value >= options.HuntThreshold)
            .OrderByDescending(g => g.Value)
            .ThenBy(g => g.Key)
            .GroupBy(g => g.Key.Faction)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Key.Who).ToList());

    /// <summary>
    /// The squad's quarry: the first of its faction's hunted characters it holds TEMP_ENEMY and who is still in the world.
    /// </summary>
    CharacterId? Quarry(World world, Squad squad, List<CharacterId> marks)
    {
        foreach (var who in marks)
            if (world.Characters.IsAlive(who) && memory!.HasShort(world, squad.Id, who, MemoryTag.TempEnemy)) return who;

        return null;
    }

    /// <summary>
    /// Each standing member's fight: one that has just turned on someone other than the quarry says the off-target line, at most one member of the
    /// squad per cooldown.
    /// </summary>
    void Watch(World world, Squad squad, CharacterId quarry, RivalryOptions options)
    {
        foreach (var member in squad.Members)
        {
            if (NewFoe(world, member) is not { } foe || foe == quarry) continue;
            if (!MaySpeak(world, squad.Id, options)) continue;

            world.Bark(member, OffTargetLine(world, quarry, options));
            _spokeAt[squad.Id] = world.Tick;
        }
    }

    /// <summary>
    /// The character <paramref name="member"/> fights when it started on them since the last look; null when it fights nobody, is down, or fights
    /// the same one as before.
    /// </summary>
    CharacterId? NewFoe(World world, CharacterId member)
    {
        var table = world.Characters;
        if (!table.TryResolveNext(member, out int slot) || table.Cold(slot) is not { Medical.Incapacitated: false })
        {
            _foes.Remove(member);
            return null;
        }

        var (targetSlot, generation) = combat!.TargetOf(slot, member.Generation);
        if (targetSlot < 0)
        {
            _foes.Remove(member);
            return null;
        }

        var foe = new CharacterId(targetSlot, generation);
        if (_foes.TryGetValue(member, out var before) && before == foe) return null;

        _foes[member] = foe;
        return foe;
    }

    /// <summary>
    /// Whether the squad's last line is at least <see cref="RivalryOptions.BarkCooldownSeconds"/> old.
    /// </summary>
    bool MaySpeak(World world, int squad, RivalryOptions options)
    {
        long cooldown = (long)(options.BarkCooldownSeconds / world.TickSeconds);
        return !_spokeAt.TryGetValue(squad, out long at) || world.Tick - at >= cooldown;
    }

    /// <summary>
    /// The off-target line with <c>{target}</c> replaced by the quarry's name.
    /// </summary>
    static string OffTargetLine(World world, CharacterId quarry, RivalryOptions options)
    {
        string name = world.Characters.TryResolveNext(quarry, out int slot) ? world.Characters.Cold(slot)?.Name ?? "" : "";
        return options.OffTargetLine.Replace("{target}", name, StringComparison.Ordinal);
    }

    /// <summary>
    /// Drops remembered fights of members no longer in the world once there are many.
    /// </summary>
    void Prune(World world)
    {
        if (_foes.Count <= PruneAbove) return;

        foreach (var gone in _foes.Keys.Where(m => !world.Characters.IsAlive(m)).ToList()) _foes.Remove(gone);
    }
}
