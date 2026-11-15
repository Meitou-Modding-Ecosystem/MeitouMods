using Meitou.Data.Fcs;
using Meitou.Modding;
using Meitou.Simulation;

namespace Husbandry;

/// <summary>
/// Husbandry's entry (docs/husbandry.md): the Hitching Post job, the system that keeps pens and breeds, the settings and the debug rows.
/// </summary>
public sealed class HusbandryMod : IMod
{
    /// <summary>
    /// The mod's manifest id.
    /// </summary>
    public const string Id = "husbandry";

    /// <summary>
    /// The player's settings.
    /// </summary>
    public HusbandrySettings Settings { get; } = new();

    /// <summary>
    /// The system of the latest world, for the debug rows.
    /// </summary>
    HusbandrySystem? _latest;

    public void Initialize(ModContext c)
    {
        c.AddSettings(Settings);
        if (c.Data.Find(HusbandryData.Post) is null) c.LogError("Husbandry.mod has no records: generate it (docs/husbandry.md \"Data file\").");

        c.AddJob(Hitches.Kind);
        c.AddSystem("husbandry", ctx => _latest = new HusbandrySystem(ctx, Settings, c.RngPurpose, c.Log), SystemAnchor.End);
        c.AddDebugCommand("Husbandry: hitched animals", _ => Told(c, _latest?.View ?? NoWorld));
        c.AddDebugCommand("Husbandry: try breeding now", d =>
        {
            d.Enqueue(new BreedNowCommand { Tick = d.Tick });
            return "Every paired female tries at the next check.";
        });
        LogAnimals(c);
    }

    /// <summary>
    /// The load scan: every animal kind in the merged data (mods' too) can be hitched, since the job takes any of the player's animals; the races
    /// are logged, robots marked as not breeding.
    /// </summary>
    static void LogAnimals(ModContext c)
    {
        var races = c.Records((int)FcsRecordType.AnimalCharacter)
            .SelectMany(a => a.GetReferences("race").Take(1))
            .Select(r => c.Data.Find(r.TargetStringId))
            .OfType<Meitou.Data.GameRecord>()
            .DistinctBy(r => r.StringId)
            .OrderBy(r => r.Name, StringComparer.Ordinal)
            .Select(r => r.GetBool("is robot") ? r.Name + " (robot: no breeding)" : r.Name)
            .ToList();
        int kinds = c.Records((int)FcsRecordType.AnimalCharacter).Count();
        c.Log($"husbandry: {kinds} animal kinds in {races.Count} races can be hitched: {string.Join(", ", races)}");
    }

    /// <summary>
    /// Logs a debug answer too (the status line shows only its start).
    /// </summary>
    static string Told(ModContext c, string answer)
    {
        c.Log(answer);
        return answer;
    }

    /// <summary>
    /// The debug answer before the first world.
    /// </summary>
    const string NoWorld = "no world yet";
}
