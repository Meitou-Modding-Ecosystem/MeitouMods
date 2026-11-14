using Meitou.Modding;
using Rivalry;

namespace AshenHand;

/// <summary>
/// The Ashen Hand's entry (docs/ashen-hand.md): two rules for Rivalry Core. Thieves visit a wealthy outpost; hunters come for a character the
/// order holds a grudge against.
/// </summary>
public sealed class AshenHandMod : IMod
{
    /// <summary>
    /// The mod's manifest id; the rules' names start with it.
    /// </summary>
    public const string Id = "ashen.hand";

    /// <summary>
    /// The rules.
    /// </summary>
    public const string ThievesRule = "ashen.hand.thieves", HuntersRule = "ashen.hand.hunters";

    /// <summary>
    /// What a thief squad means to carry off, in cats; hours before the same outpost is visited again, and the same character hunted again.
    /// </summary>
    public const double ThiefGoal = 1000, ThiefCooldownHours = 72, HunterCooldownHours = 48;

    /// <summary>
    /// The player's settings.
    /// </summary>
    public AshenHandSettings Settings { get; } = new();

    public void Initialize(ModContext c)
    {
        c.AddSettings(Settings);
        if (c.Data.Find(AshenHandData.Faction) is null) c.LogError("AshenHand.mod has no records: generate it (docs/ashen-hand.md \"Data file\").");

        var rivalry = c.Dependency<IRivalryApi>(RivalryMod.Id) ?? throw new InvalidOperationException("The Ashen Hand needs Rivalry Core.");
        Register(rivalry.For(c.Id), Settings);
    }

    /// <summary>
    /// Adds the order's rules to Rivalry Core.
    /// </summary>
    public static void Register(IRivalryRegistry registry, AshenHandSettings settings)
    {
        registry.AddRule(Thieves(settings));
        registry.AddRule(Hunters(settings));
    }

    /// <summary>
    /// Thieves set out for a calm outpost once the player is rich enough and the outpost holds enough: 1 to 4 of them, unarmed, stealth 40 to
    /// 60, martial arts 30 to 50.
    /// </summary>
    static Rule Thieves(AshenHandSettings settings) => new()
    {
        Name = ThievesRule,
        Conditions = [BuiltIn.Calm],
        When = (a, t) => a.Report.Wealth >= settings.WealthMin && Holds(a, t.Town) >= settings.HoldMin,
        Behaviour = BuiltIn.Burgle,
        Enabled = _ => settings.Thieves,
        ChancePerDay = _ => settings.ThiefChancePerDay,
        CooldownHours = _ => ThiefCooldownHours,
        Faction = (a, _) => a.Data.FactionIndex(AshenHandData.Faction),
        Args = (_, _) => new BehaviourArgs()
            .Set("template", AshenHandData.ThiefSquad)
            .Set("goal", ThiefGoal)
            .Set("stealthMin", 40)
            .Set("stealthMax", 60)
            .Set("martialArtsMin", 30)
            .Set("martialArtsMax", 50),
        Notice = (a, t) => $"Word of the wealth at {a.Report.TownOf(t.Town)?.Name} has reached the Ashen Hand",
    };

    /// <summary>
    /// Hunters come for a standing character the order's grudge against has reached Rivalry Core's hunt threshold.
    /// </summary>
    static Rule Hunters(AshenHandSettings settings) => new()
    {
        Name = HuntersRule,
        Target = BuiltIn.CharacterTarget,
        When = (a, t) => a.Data.FactionIndex(AshenHandData.Faction) is >= 0 and var order
            && a.Notoriety.Grudge(order, t.Character) >= a.Options.HuntThreshold,
        Behaviour = BuiltIn.Pursue,
        Enabled = _ => settings.Hunters,
        CooldownHours = _ => HunterCooldownHours,
        Faction = (a, _) => a.Data.FactionIndex(AshenHandData.Faction),
        Args = (_, _) => new BehaviourArgs()
            .Set("template", AshenHandData.HunterSquad)
            .Set("meleeAttackMin", 35)
            .Set("meleeAttackMax", 55)
            .Set("meleeDefenceMin", 35)
            .Set("meleeDefenceMax", 55)
            .Set("strengthMin", 35)
            .Set("strengthMax", 55)
            .Set("toughnessMin", 35)
            .Set("toughnessMax", 55),
        Notice = (_, t) => $"The Ashen Hand has sent hunters for {t.Key}",
    };

    /// <summary>
    /// What an outpost holds: the player's cats, the goods in its containers and what its people carry (worn gear aside).
    /// </summary>
    static long Holds(Assessment a, int town) => a.Report.Money
        + (a.Report.TownOf(town)?.StoredValue ?? 0)
        + a.Report.Characters.Where(c => c.Town == town).Sum(c => c.CarriedValue);
}
