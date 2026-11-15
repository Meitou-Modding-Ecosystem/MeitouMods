namespace Rivalry;

/// <summary>
/// What a hit squad says (docs/rivalry-core.md "Hunter barks"): lists in its run's text arguments, entries separated by <c>|</c>, each with a generic
/// default. <c>{target}</c> is the quarry's name, <c>{name}</c> the speaker's, <c>{leader}</c> the leader the player last handed to the law, <c>{fallen}</c>
/// the squad members dead after the leader's last fight.
/// </summary>
public static class HunterLines
{
    /// <summary>
    /// The arguments: held up fighting anyone but the player; a member opening the fight with the player; the leader opening it on its first dispatch,
    /// after a predecessor was handed to the law, and back after each outcome of its last fight.
    /// </summary>
    public const string Distracted = "say.distracted", Engage = "say.engage", First = "say.first", Reckoning = "say.reckoning",
        AfterWon = "say.won", AfterWithdrew = "say.withdrew", AfterBeaten = "say.beaten", AfterLeftForDead = "say.leftForDead",
        AfterEscaped = "say.escaped";

    /// <summary>
    /// The defaults, by argument.
    /// </summary>
    static readonly Dictionary<string, string> Defaults = new(StringComparer.Ordinal)
    {
        [Engage] = "There you are, {target}.",
        [First] = "I am {name}. Remember it, {target}.",
        [Reckoning] = "You handed {leader} to the law, {target}. We remember.",
        [AfterWon] = "I put you down once, {target}. Again, then.",
        [AfterWithdrew] = "We broke off last time, {target}. Not today.",
        [AfterBeaten] = "You beat me once, {target}. Once.",
        [AfterLeftForDead] = "You left me for dead, {target}. You should have made sure.",
        [AfterEscaped] = "Your ropes didn't hold me, {target}.",
    };

    /// <summary>
    /// The argument a returning leader speaks from, by what it remembers; <see cref="RivalOutcome.None"/> (first dispatch) gives <see cref="First"/>.
    /// </summary>
    public static string ForReturn(RivalOutcome remembered) => remembered switch
    {
        RivalOutcome.Won => AfterWon,
        RivalOutcome.Withdrew => AfterWithdrew,
        RivalOutcome.Beaten => AfterBeaten,
        RivalOutcome.LeftForDead => AfterLeftForDead,
        RivalOutcome.Escaped => AfterEscaped,
        _ => First,
    };

    /// <summary>
    /// One line of the run's list <paramref name="kind"/> (or its default; for <see cref="Distracted"/> <paramref name="fallback"/>), picked by
    /// <paramref name="pick"/>, with its placeholders filled.
    /// </summary>
    public static string Pick(BehaviourArgs args, string kind, ulong pick, IReadOnlyDictionary<string, string> fill, string? fallback = null)
    {
        var lines = RivalLeaders.Split(args.Text(kind));
        string line = lines.Count > 0 ? lines[(int)(pick % (ulong)lines.Count)] : Defaults.GetValueOrDefault(kind, fallback ?? "");
        foreach (var (key, value) in fill) line = line.Replace("{" + key + "}", value, StringComparison.Ordinal);

        return line;
    }
}
