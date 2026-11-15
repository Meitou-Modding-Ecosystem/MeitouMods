using Rivalry;

namespace AshenHand;

/// <summary>
/// What the order's hunters are called and say (docs/ashen-hand.md "Hunters"): the names its squad leaders take and the starter lines Rivalry Core's
/// barks pick from (<see cref="HunterLines"/>), in the order's voice: a cult that tends a buried Second Empire machine under the Ashlands and
/// remembers every debt. Original writing; placeholders <c>{target}</c>, <c>{name}</c>, <c>{leader}</c>, <c>{fallen}</c>.
/// </summary>
public static class AshenHandVoice
{
    /// <summary>
    /// The names the order's squad leaders take, one per leader for as long as it lives.
    /// </summary>
    public static readonly string[] LeaderNames =
    [
        "Murren Ashtongue", "Kesh Charfinger", "Old Sorrow", "Velka Ashveil", "Dorn Greysalt", "Mother Kiln", "Yoru Blackwick", "Sef the Unburied",
        "Harrow Dustmaw", "Ilsa Pyre", "Tekk Nine-Burns", "Brother Soot", "Orrin Cinderjaw", "Hollow Vesna", "Taavi Smokeborn", "Jessa Coalhand",
    ];

    /// <summary>
    /// Lines by Rivalry Core's argument (<see cref="HunterLines"/>).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string[]> Lines = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        [HunterLines.Distracted] =
        [
            "Out of my way. My business is with {target}.",
            "You're not the one I was sent for. Fall down and stay down.",
            "Every breath I spend on you, {target} walks further. Die faster.",
            "The ash takes you first. Then {target}.",
        ],
        [HunterLines.Engage] =
        [
            "There. The Hand remembers you, {target}.",
            "You bled the Hand. Now the Hand bleeds you.",
            "No walls out here, {target}. No guards. Just us and the dust.",
            "Hold still. It's quicker for both of us.",
        ],
        [HunterLines.First] =
        [
            "I am {name}. Remember it, {target}, for the little while you've got.",
            "The one who sleeps under the ash wants your bones, {target}. {name} came to collect.",
            "{name} of the Ashen Hand. Nobody sends me twice.",
        ],
        [HunterLines.Reckoning] =
        [
            "You sold {leader} to the law for a pouch of cats, {target}. The Hand pays its debts.",
            "{leader} rots in a cell because of you. I've come to settle it.",
        ],
        [HunterLines.AfterWon] =
        [
            "Back on your feet, {target}? I dropped you once. The dust remembers.",
            "Last time you folded like wet paper. Let's see what you've learned.",
        ],
        [HunterLines.AfterWithdrew] =
        [
            "You ran me ragged across half the desert, {target}. My legs have healed.",
            "We broke off last time. Not tonight.",
        ],
        [HunterLines.AfterBeaten] =
        [
            "You put me in the dirt once, {target}. I got up. That's the difference between us.",
            "I buried {fallen} of mine after our last dance. I've been counting the days.",
        ],
        [HunterLines.AfterLeftForDead] =
        [
            "You left me in the sand for the beak things, {target}. Should've finished it.",
            "Two days I lay out there holding my guts in. Two days to think about you, {target}.",
        ],
        [HunterLines.AfterEscaped] =
        [
            "Your cage had a weak lock, {target}. So does your skull.",
            "I've chewed through worse than your ropes. Miss me?",
        ],
    };

    /// <summary>
    /// The towns the hunters set out from, the order owning none of its own: the Ashlands, where the dig is. **Unknown** whether the game names a town
    /// so; none found, Rivalry Core uses its fallback travel time.
    /// </summary>
    public const string Home = "Black Desert City";

    /// <summary>
    /// Adds the names, lines and home to a hunt's arguments.
    /// </summary>
    public static BehaviourArgs Voice(this BehaviourArgs args)
    {
        args.Set("home", Home);
        args.Set("leaderNames", string.Join(RivalLeaders.ListSeparator, LeaderNames));
        foreach (var (kind, lines) in Lines) args.Set(kind, string.Join(RivalLeaders.ListSeparator, lines));

        return args;
    }
}
