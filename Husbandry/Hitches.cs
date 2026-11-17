using Meitou.Simulation;
using Meitou.Simulation.Bodies;
using Meitou.Simulation.Construction;
using Meitou.Simulation.Factions;
using Meitou.Simulation.Production;

namespace Husbandry;

/// <summary>
/// One hitched animal as the checks see it: its slot, its character, the post it is hitched to.
/// </summary>
public readonly record struct Hitched(int Slot, CharacterCold Cold, BuildSite Post);

/// <summary>
/// The hitching post job (docs/husbandry.md "Hitching"): which animals hold it and on which post, and the animal facts the pen and breeding read.
/// </summary>
public static class Hitches
{
    /// <summary>
    /// The job's id (<see cref="ModJobKind.Id"/>).
    /// </summary>
    public const string JobId = "husbandry.hitch";

    /// <summary>
    /// The age from which an animal is an adult: the game's own "Adult" label (below 0.4 "Pup", below 0.6 "Teen"); elders count too.
    /// </summary>
    public const float AdultAge = 0.6f;

    /// <summary>
    /// The job as the engine offers it: on the Hitching Post, to animals.
    /// </summary>
    public static ModJobKind Kind => new(JobId, HusbandryData.Post, "Hitch here", "Hitched", ModJobTakers.Animals);

    /// <summary>
    /// The post a character is hitched to, or null.
    /// </summary>
    public static string? PostOf(CharacterCold cold) => cold.ModJobs.Find(j => j.Kind == JobId)?.Site;

    /// <summary>
    /// Hitches a character to a post (replacing any other post).
    /// </summary>
    public static void Hitch(CharacterCold cold, string post)
    {
        cold.ModJobs.RemoveAll(j => j.Kind == JobId);
        cold.ModJobs.Add(new ModJob(JobId, post));
    }

    /// <summary>
    /// Unhitches a character.
    /// </summary>
    public static void Unhitch(CharacterCold cold) => cold.ModJobs.RemoveAll(j => j.Kind == JobId);

    /// <summary>
    /// The living hitched animals whose post still stands finished; a hitch on a post that is gone or unfinished is taken off.
    /// </summary>
    public static List<Hitched> All(World world)
    {
        var list = new List<Hitched>();
        var table = world.Characters;
        for (int s = 0; s < table.HighWater; s++)
        {
            if (!table.Next[s].Alive || table.Cold(s) is not { IsPlayer: true, DiedTick: < 0 } cold || PostOf(cold) is not { } post) continue;

            if (world.Buildings.Find(post) is { Complete: true } site && site.Owner == world.Player.Faction) list.Add(new Hitched(s, cold, site));
            else Unhitch(cold);
        }

        return list;
    }

    /// <summary>
    /// Whether an animal is female. Neither the game nor its data give animals a sex, so the mod decides it once from the character's portrait
    /// serial (saved with the character), half and half.
    /// </summary>
    public static bool Female(CharacterCold cold, RngPurpose purpose) =>
        cold.PortraitSerial is { } serial && (Rng.Hash(0, serial, purpose, SexCounter) & 1) == 1;

    /// <summary>
    /// The counter of the sex roll.
    /// </summary>
    const ulong SexCounter = 0x5E7;

    /// <summary>
    /// Whether an animal is an adult (<see cref="AdultAge"/>).
    /// </summary>
    public static bool Adult(CharacterCold cold) => cold.Age >= AdultAge;

    /// <summary>
    /// Whether an animal can breed at all: a living animal of a race that is no robot.
    /// </summary>
    public static bool Breeds(CharacterCold cold) => SquadRegistry.IsAnimal(cold) && cold.Race is { IsRobot: false };

    /// <summary>
    /// The animal's hunger (3 full, 2 and more well fed, below 1 starving); 3 for one without a body.
    /// </summary>
    public static float Hunger(CharacterCold cold) => cold.Medical?.Hunger ?? 3;

    /// <summary>
    /// The words for a hunger level, as the breeding chance groups it.
    /// </summary>
    public static string Fed(float hunger) => hunger >= 2 ? "well fed" : hunger >= 1 ? "hungry" : "starving";

    /// <summary>
    /// Whether an animal is down (knocked out, carried, ragdolled) and so not walked anywhere.
    /// </summary>
    public static bool Down(CharacterCold cold) => CharacterFacts.Down(cold) || !cold.CarriedBy.IsNone;
}
