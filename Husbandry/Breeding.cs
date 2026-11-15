using System.Numerics;
using Meitou.Simulation;
using Meitou.Simulation.Population;

namespace Husbandry;

/// <summary>
/// Breeding at the posts (docs/husbandry.md "Breeding"). Hitched adults of one race at one post pair up: each female with a male there tries for a
/// pup every <see cref="HusbandrySettings.DaysBetweenTries"/> game days, the first try that long after she is paired. The chance follows the
/// hungrier of her and the best-fed male. A pup is born beside her: her record, aged <see cref="NewbornAge"/>, in her squad, hitched to her post.
/// </summary>
public sealed class Breeding(RngPurpose purpose, Action<string> log)
{
    /// <summary>
    /// A newborn's age (a "Pup" until 0.4).
    /// </summary>
    public const float NewbornAge = 0.05f;

    /// <summary>
    /// How far from the mother the pup is born, in game units.
    /// </summary>
    const float BirthOffset = 12;

    /// <summary>
    /// The counter salt of a try's roll.
    /// </summary>
    const ulong TrySalt = 0xB12D;

    /// <summary>
    /// Each paired female's next try, in game hours, by her portrait serial.
    /// </summary>
    public SortedDictionary<uint, double> NextTry { get; } = [];

    /// <summary>
    /// A line for each try since the last call (for the debug row).
    /// </summary>
    public List<string> Recent { get; } = [];

    /// <summary>
    /// One pass over the hitched animals: pairs noted, due females try. Serial step.
    /// </summary>
    public void Run(World world, List<Hitched> hitched, HusbandrySettings settings, PopulationSystem? population, bool now)
    {
        var paired = new HashSet<uint>();
        foreach (var group in Groups(hitched))
        {
            var males = group.Where(h => !Hitches.Female(h.Cold, purpose)).ToList();
            if (males.Count == 0) continue;

            foreach (var female in group.Where(h => Hitches.Female(h.Cold, purpose)))
            {
                uint serial = female.Cold.PortraitSerial!.Value;
                paired.Add(serial);
                if (Due(world, serial, settings, now)) Try(world, female, Best(males), settings, population);
            }
        }

        foreach (uint gone in NextTry.Keys.Where(k => !paired.Contains(k)).ToList()) NextTry.Remove(gone);
    }

    /// <summary>
    /// The adults that can breed, grouped by post and race.
    /// </summary>
    static IEnumerable<List<Hitched>> Groups(List<Hitched> hitched) =>
        hitched
            .Where(h => Hitches.Breeds(h.Cold) && Hitches.Adult(h.Cold) && h.Cold.PortraitSerial is not null && !Hitches.Down(h.Cold))
            .GroupBy(h => (h.Post.Id, h.Cold.Race!.StringId))
            .Select(g => g.ToList());

    /// <summary>
    /// The best-fed male.
    /// </summary>
    static Hitched Best(List<Hitched> males) => males.MaxBy(m => Hitches.Hunger(m.Cold));

    /// <summary>
    /// Whether the female's try is due; a newly paired female's clock starts now. A due try moves her clock on by the period.
    /// </summary>
    bool Due(World world, uint serial, HusbandrySettings settings, bool now)
    {
        double period = settings.DaysBetweenTries * 24.0;
        if (!NextTry.TryGetValue(serial, out double next))
        {
            NextTry[serial] = world.GameHours + period;
            return now;
        }

        if (!now && world.GameHours < next) return false;

        NextTry[serial] = Math.Max(next + period, world.GameHours + period / 2);
        return true;
    }

    /// <summary>
    /// The chance in percent for the hungrier parent's hunger.
    /// </summary>
    public static int Chance(float hunger, HusbandrySettings settings) =>
        hunger >= 2 ? settings.ChanceWellFed : hunger >= 1 ? settings.ChanceHungry : settings.ChanceStarving;

    /// <summary>
    /// One try: the roll, and on success the birth.
    /// </summary>
    void Try(World world, Hitched female, Hitched male, HusbandrySettings settings, PopulationSystem? population)
    {
        float hunger = Math.Min(Hitches.Hunger(female.Cold), Hitches.Hunger(male.Cold));
        int chance = Chance(hunger, settings);
        uint serial = female.Cold.PortraitSerial!.Value;
        float roll = Rng.Float(Rng.Hash(world.Seed, serial, purpose, TrySalt ^ (ulong)world.Tick)) * 100;
        bool success = roll < chance;

        Note($"{female.Cold.Name} and {male.Cold.Name} ({Hitches.Fed(hunger)}, {chance}%): {(success ? "a pup" : "no luck")}");
        if (success) Birth(world, female, population);
    }

    /// <summary>
    /// A pup beside its mother, hitched to her post, and the news.
    /// </summary>
    void Birth(World world, Hitched mother, PopulationSystem? population)
    {
        var at = world.Characters.Next[mother.Slot].Position;
        var spot = new Vector2(at.X + BirthOffset, at.Z);
        var pup = population?.SpawnAnimal(world, mother.Cold.RecordId, mother.Cold.SquadId, spot, NewbornAge) ?? CharacterId.None;
        if (pup.IsNone || !world.Characters.TryResolveNext(pup, out int slot) || world.Characters.Cold(slot) is not { } cold)
        {
            Note($"{mother.Cold.Name} could not give birth: the squad is gone or the faction is full");
            return;
        }

        Hitches.Hitch(cold, mother.Post.Id);
        world.Post($"{mother.Cold.Name} has given birth to a pup at the hitching post.");
    }

    /// <summary>
    /// Keeps a line for the debug row and the log.
    /// </summary>
    void Note(string line)
    {
        Recent.Add(line);
        if (Recent.Count > 32) Recent.RemoveAt(0);
        log("husbandry: " + line);
    }
}
