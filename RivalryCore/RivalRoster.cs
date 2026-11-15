using System.Globalization;
using System.Text;
using Meitou.Simulation;

namespace Rivalry;

/// <summary>
/// How a rival leader's last fight with the player ended (docs/rivalry-core.md "Rival leaders").
/// </summary>
public enum RivalOutcome
{
    /// <summary>
    /// No fight yet.
    /// </summary>
    None,

    /// <summary>
    /// The quarry went down or died.
    /// </summary>
    Won,

    /// <summary>
    /// Time ran out before either side was beaten.
    /// </summary>
    Withdrew,

    /// <summary>
    /// The squad was beaten and the leader got up and walked off.
    /// </summary>
    Beaten,

    /// <summary>
    /// The squad was beaten and the player left the leader lying alive (it despawned or the watch ended with it free).
    /// </summary>
    LeftForDead,

    /// <summary>
    /// The player carried the leader off or caged it.
    /// </summary>
    Captured,

    /// <summary>
    /// The leader got free of the player's hold.
    /// </summary>
    Escaped,

    /// <summary>
    /// The leader died.
    /// </summary>
    Killed,

    /// <summary>
    /// The player handed the leader, alive, to the law.
    /// </summary>
    TurnedIn,

    /// <summary>
    /// The leader joined the player's faction.
    /// </summary>
    Recruited,
}

/// <summary>
/// Where a rival leader is now: only a <see cref="Free"/> one is sent again.
/// </summary>
public enum RivalFate
{
    /// <summary>
    /// Out in the world, free to be sent.
    /// </summary>
    Free,

    /// <summary>
    /// Held by the player.
    /// </summary>
    Captive,

    /// <summary>
    /// Dead.
    /// </summary>
    Dead,

    /// <summary>
    /// In a cell of the law.
    /// </summary>
    Jailed,

    /// <summary>
    /// One of the player's own.
    /// </summary>
    Recruited,
}

/// <summary>
/// One named leader of a faction's hit squads and its record against the player: what a later personality and recruitment build on. Saved and hashed with
/// the roster.
/// </summary>
public sealed class RivalLeader
{
    /// <summary>
    /// The leader's name, unique within its faction.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The faction's string id.
    /// </summary>
    public required string Faction { get; init; }

    /// <summary>
    /// The portrait serial of the character that last carried the name (0: none yet).
    /// </summary>
    public uint Serial { get; set; }

    /// <summary>
    /// Whether a run has the leader out now.
    /// </summary>
    public bool Busy { get; set; }

    /// <summary>
    /// Times sent.
    /// </summary>
    public int Dispatches { get; set; }

    /// <summary>
    /// Fights the leader won (the quarry went down), lost (the squad was beaten), and broke off.
    /// </summary>
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int Withdrawals { get; set; }

    /// <summary>
    /// Times the player left the leader lying, captured it, and it got free.
    /// </summary>
    public int LeftForDead { get; set; }
    public int Captures { get; set; }
    public int Escapes { get; set; }

    /// <summary>
    /// Squad members dead when the leader's fights ended, in all and in the last one.
    /// </summary>
    public int ComradesLost { get; set; }
    public int LastComradesLost { get; set; }

    /// <summary>
    /// How the last fight ended, whom it was after, and the game hour it ended.
    /// </summary>
    public RivalOutcome LastOutcome { get; set; }
    public string LastQuarry { get; set; } = "";
    public double LastHour { get; set; } = double.NaN;

    /// <summary>
    /// Where the leader is now.
    /// </summary>
    public RivalFate Fate { get; set; }

    /// <summary>
    /// The outcome a returning leader speaks of: the last fight's, or <see cref="RivalOutcome.None"/> on its first dispatch.
    /// </summary>
    public RivalOutcome Remembered => Dispatches <= 1 ? RivalOutcome.None : LastOutcome;
}

/// <summary>
/// A leader the player handed to the law, alive, and the grudge it cleared: kept for when that faction's grudge heats up again.
/// </summary>
public sealed record Reckoning(string Faction, string Leader, string Quarry, double Hour, float GrudgeCleared);

/// <summary>
/// The named leaders of every faction's hit squads, the leaders handed to the law, and when each faction's last hit squad finished (docs/rivalry-core.md
/// "Rival leaders"). Factions by string id, so a save survives a changed mod list. Serial steps only.
/// </summary>
public sealed class RivalRoster
{
    #region Leaders

    /// <summary>
    /// The leaders, in the order named.
    /// </summary>
    readonly List<RivalLeader> _leaders = [];

    /// <summary>
    /// The leaders, in the order named.
    /// </summary>
    public IReadOnlyList<RivalLeader> Leaders => _leaders;

    /// <summary>
    /// The faction's leader with the name; null when none.
    /// </summary>
    public RivalLeader? Find(string faction, string name) => _leaders.FirstOrDefault(l => l.Faction == faction && l.Name == name);

    /// <summary>
    /// The leader to send for the faction: the free, idle one that fought most recently and whose last body is not standing in the world (see
    /// <paramref name="standing"/>), else a new one named from <paramref name="names"/> (picked at <paramref name="roll"/>; numbered once all are used).
    /// </summary>
    public RivalLeader Pick(string faction, IReadOnlyList<string> names, float roll, Func<uint, bool> standing)
    {
        var returning = _leaders
            .Where(l => l.Faction == faction && l.Fate == RivalFate.Free && !l.Busy && !standing(l.Serial))
            .OrderByDescending(l => double.IsNaN(l.LastHour) ? double.MinValue : l.LastHour)
            .FirstOrDefault();
        if (returning is not null) return returning;

        var leader = new RivalLeader { Name = NewName(faction, names, roll), Faction = faction };
        _leaders.Add(leader);
        return leader;
    }

    /// <summary>
    /// A name no leader of the faction has: the list's entry at the roll or the next unused after it; all used, the entry at the roll numbered.
    /// </summary>
    string NewName(string faction, IReadOnlyList<string> names, float roll)
    {
        var list = names.Count > 0 ? names : ["Hunter"];
        int start = Math.Clamp((int)(roll * list.Count), 0, list.Count - 1);
        for (int k = 0; k < list.Count; k++)
        {
            string name = list[(start + k) % list.Count];
            if (Find(faction, name) is null) return name;
        }

        for (int n = 2; ; n++)
        {
            string numbered = $"{list[start]} {Numeral(n)}";
            if (Find(faction, numbered) is null) return numbered;
        }
    }

    /// <summary>
    /// A small number as a Roman numeral (a leader's successor: "the Second" reads too long over a head).
    /// </summary>
    static string Numeral(int n)
    {
        (int Value, string Text)[] steps = [(1000, "M"), (900, "CM"), (500, "D"), (400, "CD"), (100, "C"), (90, "XC"), (50, "L"), (40, "XL"), (10, "X"),
            (9, "IX"), (5, "V"), (4, "IV"), (1, "I")];
        var text = new StringBuilder();
        foreach (var (value, part) in steps)
        {
            while (n >= value)
            {
                text.Append(part);
                n -= value;
            }
        }

        return text.ToString();
    }

    /// <summary>
    /// Marks idle every busy leader no run names (a run dropped without finishing).
    /// </summary>
    public void Idle(IEnumerable<(string Faction, string Name)> onRuns)
    {
        if (!_leaders.Any(l => l.Busy)) return;

        var named = onRuns.ToHashSet();
        foreach (var leader in _leaders.Where(l => l.Busy && !named.Contains((l.Faction, l.Name)))) leader.Busy = false;
    }

    #endregion

    #region Reckonings

    /// <summary>
    /// The leaders handed to the law, oldest first.
    /// </summary>
    readonly List<Reckoning> _reckonings = [];

    /// <summary>
    /// The leaders handed to the law, oldest first.
    /// </summary>
    public IReadOnlyList<Reckoning> Reckonings => _reckonings;

    /// <summary>
    /// Records a leader handed to the law.
    /// </summary>
    public void Reckon(Reckoning reckoning) => _reckonings.Add(reckoning);

    /// <summary>
    /// The faction's latest reckoning; null when none.
    /// </summary>
    public Reckoning? LastReckoning(string faction) => _reckonings.LastOrDefault(r => r.Faction == faction);

    #endregion

    #region Pacing

    /// <summary>
    /// The game hour each faction's last hit squad finished (beaten, won, broke off or never arrived), by faction string id.
    /// </summary>
    readonly SortedDictionary<string, double> _ended = new(StringComparer.Ordinal);

    /// <summary>
    /// Notes that one of the faction's hit squads finished now.
    /// </summary>
    public void Ended(string faction, double hour) => _ended[faction] = hour;

    /// <summary>
    /// Game hours since the faction's last hit squad finished; infinity when none has.
    /// </summary>
    public double HoursSinceEnded(string faction, double now) => _ended.TryGetValue(faction, out double at) ? now - at : double.PositiveInfinity;

    #endregion

    #region Saving

    /// <summary>
    /// The roster's layout version inside the sidecar.
    /// </summary>
    const int Version = 1;

    /// <summary>
    /// Writes the roster.
    /// </summary>
    public void Write(BinaryWriter w)
    {
        w.Write(Version);
        w.Write(_leaders.Count);
        foreach (var leader in _leaders) WriteLeader(leader, w);

        w.Write(_reckonings.Count);
        foreach (var r in _reckonings) WriteReckoning(r, w);

        w.Write(_ended.Count);
        foreach (var (faction, hour) in _ended)
        {
            w.Write(faction);
            w.Write(hour);
        }
    }

    /// <summary>
    /// Writes one leader.
    /// </summary>
    static void WriteLeader(RivalLeader l, BinaryWriter w)
    {
        w.Write(l.Name);
        w.Write(l.Faction);
        w.Write(l.Serial);
        w.Write(l.Busy);
        foreach (int count in Counts(l)) w.Write(count);

        w.Write((int)l.LastOutcome);
        w.Write(l.LastQuarry);
        w.Write(l.LastHour);
        w.Write((int)l.Fate);
    }

    /// <summary>
    /// A leader's counters, in save and hash order.
    /// </summary>
    static int[] Counts(RivalLeader l) =>
        [l.Dispatches, l.Wins, l.Losses, l.Withdrawals, l.LeftForDead, l.Captures, l.Escapes, l.ComradesLost, l.LastComradesLost];

    /// <summary>
    /// Writes one reckoning.
    /// </summary>
    static void WriteReckoning(Reckoning r, BinaryWriter w)
    {
        w.Write(r.Faction);
        w.Write(r.Leader);
        w.Write(r.Quarry);
        w.Write(r.Hour);
        w.Write(r.GrudgeCleared);
    }

    /// <summary>
    /// Reads a roster, replacing this one; a newer layout is skipped and leaves it empty.
    /// </summary>
    public void Read(BinaryReader r)
    {
        Clear();
        if (r.ReadInt32() is not Version) return;

        int leaders = r.ReadInt32();
        for (int i = 0; i < leaders; i++) _leaders.Add(ReadLeader(r));

        int reckonings = r.ReadInt32();
        for (int i = 0; i < reckonings; i++) _reckonings.Add(ReadReckoning(r));

        int ended = r.ReadInt32();
        for (int i = 0; i < ended; i++) _ended[r.ReadString()] = r.ReadDouble();
    }

    /// <summary>
    /// Reads one reckoning.
    /// </summary>
    static Reckoning ReadReckoning(BinaryReader r) => new(r.ReadString(), r.ReadString(), r.ReadString(), r.ReadDouble(), r.ReadSingle());

    /// <summary>
    /// Reads one leader.
    /// </summary>
    static RivalLeader ReadLeader(BinaryReader r)
    {
        var l = new RivalLeader { Name = r.ReadString(), Faction = r.ReadString(), Serial = r.ReadUInt32(), Busy = r.ReadBoolean() };
        l.Dispatches = r.ReadInt32();
        l.Wins = r.ReadInt32();
        l.Losses = r.ReadInt32();
        l.Withdrawals = r.ReadInt32();
        l.LeftForDead = r.ReadInt32();
        l.Captures = r.ReadInt32();
        l.Escapes = r.ReadInt32();
        l.ComradesLost = r.ReadInt32();
        l.LastComradesLost = r.ReadInt32();
        l.LastOutcome = (RivalOutcome)r.ReadInt32();
        l.LastQuarry = r.ReadString();
        l.LastHour = r.ReadDouble();
        l.Fate = (RivalFate)r.ReadInt32();
        return l;
    }

    /// <summary>
    /// Empties the roster.
    /// </summary>
    public void Clear()
    {
        _leaders.Clear();
        _reckonings.Clear();
        _ended.Clear();
    }

    /// <summary>
    /// The roster into the world's hash.
    /// </summary>
    public void Hash(ref StateHasher h)
    {
        h.Add(_leaders.Count);
        foreach (var l in _leaders)
        {
            h.Add(Rng.StableHash(l.Faction + "/" + l.Name));
            h.Add((int)l.Serial);
            foreach (int count in Counts(l)) h.Add(count);

            h.Add((int)l.LastOutcome);
            h.Add((int)l.Fate);
        }

        h.Add(_reckonings.Count);
        h.Add(_ended.Count);
        foreach (var (_, hour) in _ended) h.Add(hour);
    }

    #endregion

    #region Describing

    /// <summary>
    /// The roster as lines for the debug row.
    /// </summary>
    public IEnumerable<string> Describe()
    {
        var inv = CultureInfo.InvariantCulture;
        foreach (var l in _leaders)
        {
            yield return string.Format(inv, "{0} ({1}): {2}, sent {3}, won {4}, lost {5}, broke off {6}, left for dead {7}, captured {8}, escaped {9}, "
                + "comrades lost {10}; last {11} vs {12}", l.Name, l.Faction, l.Fate, l.Dispatches, l.Wins, l.Losses, l.Withdrawals, l.LeftForDead,
                l.Captures, l.Escapes, l.ComradesLost, l.LastOutcome, l.LastQuarry);
        }

        foreach (var r in _reckonings)
        {
            const string format = "reckoning: {0} handed {1} to the law at {2:0.#} h, clearing grudge {3:0.#}";
            yield return string.Format(inv, format, r.Quarry, r.Leader, r.Hour, r.GrudgeCleared);
        }
    }

    #endregion
}
