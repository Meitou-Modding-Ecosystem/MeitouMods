using Meitou.Modding;

namespace AshenHand;

/// <summary>
/// The player's settings (docs/ashen-hand.md "Settings"), shown under the mod's row on the Settings screen.
/// </summary>
public sealed class AshenHandSettings
{
    /// <summary>
    /// Whether thieves come.
    /// </summary>
    [Setting("Thieves", Hint = "The Ashen Hand sends thieves to a wealthy outpost.")]
    public bool Thieves { get; set; } = true;

    /// <summary>
    /// Whether hunters come.
    /// </summary>
    [Setting("Hunters", Hint = "The Ashen Hand sends hunters after a character it holds a grudge against.")]
    public bool Hunters { get; set; } = true;

    /// <summary>
    /// The player's net worth, buildings counted, from which thieves come.
    /// </summary>
    [Setting("Thieves from net worth", Hint = "Cats, gear, stored goods and buildings together.", Min = 0, Max = 500000, Step = 5000)]
    public int WealthMin { get; set; } = 40000;

    /// <summary>
    /// What an outpost must hold in cats and goods to be worth a visit.
    /// </summary>
    [Setting("Outpost holds at least", Hint = "Cats and goods at the outpost.", Min = 0, Max = 100000, Step = 1000)]
    public int HoldMin { get; set; } = 10000;

    /// <summary>
    /// The chance per game day that thieves set out for an outpost worth it.
    /// </summary>
    [Setting("Thief chance per day", Min = 0, Max = 1, Step = 0.05)]
    public float ThiefChancePerDay { get; set; } = 0.2f;
}
