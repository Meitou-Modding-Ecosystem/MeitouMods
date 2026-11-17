using Meitou.Modding;

namespace Husbandry;

/// <summary>
/// The player's settings (docs/husbandry.md "Settings"), shown under the mod's row on the Settings screen. The system copies them at each check.
/// </summary>
public sealed class HusbandrySettings
{
    /// <summary>
    /// The pen's radius round a post, in game units (about 10 cm each). 100 is a circle the area of four large farms (a large farm's crop grid is
    /// about 90 x 90).
    /// </summary>
    [Setting("Pen radius", Hint = "How far hitched animals may roam from their post, in game units (100 = four large farms).",
        Min = 30, Max = 600, Step = 10)]
    public int PenRadius { get; set; } = 100;

    /// <summary>
    /// Whether hitched animals stroll about inside the pen while idle.
    /// </summary>
    [Setting("Grazing", Hint = "Idle hitched animals wander about inside their pen.")]
    public bool Grazing { get; set; } = true;

    /// <summary>
    /// Whether hitched pairs breed.
    /// </summary>
    [Setting("Breeding", Hint = "Hitched adult pairs of the same kind try for a pup.")]
    public bool Breeding { get; set; } = true;

    /// <summary>
    /// Game days between one female's tries.
    /// </summary>
    [Setting("Days between tries", Hint = "Each paired female tries this often.", Min = 1, Max = 30, Step = 1)]
    public int DaysBetweenTries { get; set; } = 3;

    /// <summary>
    /// The chance of a pup when both parents are well fed (hunger 2 or more), in percent.
    /// </summary>
    [Setting("Chance well fed (%)", Min = 0, Max = 100, Step = 5)]
    public int ChanceWellFed { get; set; } = 50;

    /// <summary>
    /// The chance when the hungrier parent is hungry (hunger 1 to 2), in percent.
    /// </summary>
    [Setting("Chance hungry (%)", Min = 0, Max = 100, Step = 5)]
    public int ChanceHungry { get; set; } = 25;

    /// <summary>
    /// The chance when the hungrier parent is starving (hunger below 1), in percent.
    /// </summary>
    [Setting("Chance starving (%)", Min = 0, Max = 100, Step = 5)]
    public int ChanceStarving { get; set; } = 10;

    /// <summary>
    /// A copy, for the simulation to read without the settings page changing it mid-check.
    /// </summary>
    public HusbandrySettings Copy() => (HusbandrySettings)MemberwiseClone();
}
