using System.Globalization;
using System.Text;
using Meitou.Modding;
using Meitou.Simulation;
using Meitou.Simulation.Population;

namespace Husbandry;

/// <summary>
/// Every female's breeding clock falls due at the next check (the debug row's "try breeding now").
/// </summary>
public sealed record BreedNowCommand : SimCommand;

/// <summary>
/// Husbandry in one world (docs/husbandry.md): the pens kept and breeding run in serial steps, the hitches and breeding clocks saved in the mod's
/// sidecar and hashed.
/// </summary>
public sealed class HusbandrySystem : ITickSystem, IStateHashed, IModSaveData
{
    #region Ticking

    /// <summary>
    /// Ticks between pen checks of one animal (one game second at 30 ticks a second), staggered by slot.
    /// </summary>
    public const int PenEveryTicks = 30;

    /// <summary>
    /// Ticks between breeding passes (ten game seconds).
    /// </summary>
    public const int BreedEveryTicks = 300;

    /// <summary>
    /// The player's settings, copied at each pass.
    /// </summary>
    readonly HusbandrySettings _settings;

    /// <summary>
    /// The population, which makes the pups.
    /// </summary>
    readonly PopulationSystem? _population;

    /// <summary>
    /// The mod's random purpose.
    /// </summary>
    readonly RngPurpose _purpose;

    /// <summary>
    /// Breeding's clocks.
    /// </summary>
    readonly Breeding _breeding;

    /// <summary>
    /// Set by <see cref="BreedNowCommand"/> until the next breeding pass.
    /// </summary>
    bool _breedNow;

    /// <summary>
    /// The latest view for the debug rows (written in the tick, read on the host's thread).
    /// </summary>
    volatile string _view = "No hitched animals yet.";

    /// <summary>
    /// A system for one world.
    /// </summary>
    public HusbandrySystem(SystemBuildContext context, HusbandrySettings settings, RngPurpose purpose, Action<string> log)
    {
        _settings = settings;
        _purpose = purpose;
        _population = context.Find<PopulationSystem>();
        _breeding = new Breeding(purpose, log);
    }

    /// <summary>
    /// The debug rows' view of the hitched animals and the latest tries.
    /// </summary>
    public string View => _view;

    public void Inputs(World world, IReadOnlyList<SimCommand> commands)
    {
        foreach (var command in commands)
            if (command is BreedNowCommand) _breedNow = true;
    }

    public void SlowWorld(World world)
    {
        RestoreHitches(world);
        var settings = _settings.Copy();
        var hitched = Hitches.All(world);
        foreach (var h in hitched)
            if ((h.Slot + world.Tick) % PenEveryTicks == 0) Pens.Keep(world, h, settings, _purpose);

        if (!_breedNow && world.Tick % BreedEveryTicks != 0) return;

        if (settings.Breeding) _breeding.Run(world, hitched, settings, _population, _breedNow);
        _breedNow = false;
        _view = Describe(world, hitched);
    }

    /// <summary>
    /// The debug view: each hitched animal with its post, race, sex, age, hunger and next try, then the latest tries.
    /// </summary>
    string Describe(World world, List<Hitched> hitched)
    {
        if (hitched.Count == 0) return "No hitched animals.";

        var text = new StringBuilder();
        foreach (var h in hitched)
        {
            bool female = Hitches.Female(h.Cold, _purpose);
            string next = h.Cold.PortraitSerial is { } s && _breeding.NextTry.TryGetValue(s, out double at)
                ? $", next try in {Math.Max(0, at - world.GameHours).ToString("0.#", CultureInfo.InvariantCulture)} h"
                : "";
            text.AppendLine($"{h.Cold.Name} ({h.Cold.Race?.Name}, {(female ? "female" : "male")}, age {h.Cold.Age:0.00}, "
                + $"{Hitches.Fed(Hitches.Hunger(h.Cold))}) at {h.Post.Id}{next}");
        }

        foreach (string line in _breeding.Recent) text.AppendLine(line);
        return text.ToString();
    }

    #endregion

    #region Saving

    /// <summary>
    /// The sidecar layout's version.
    /// </summary>
    public const int SaveVersion = 1;

    /// <summary>
    /// Hitches read from a save, waiting for the first tick to find their characters by portrait serial.
    /// </summary>
    List<(uint Serial, string Post)>? _pending;

    /// <summary>
    /// The world the last tick ran on, for the save's hitches.
    /// </summary>
    World? _world;

    public void Write(Stream stream)
    {
        using var w = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        w.Write(SaveVersion);
        var hitches = SavedHitches();
        w.Write(hitches.Count);
        foreach (var (serial, post) in hitches)
        {
            w.Write(serial);
            w.Write(post);
        }

        w.Write(_breeding.NextTry.Count);
        foreach (var (serial, next) in _breeding.NextTry)
        {
            w.Write(serial);
            w.Write(next);
        }
    }

    public void Read(Stream stream)
    {
        using var r = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        if (r.ReadInt32() != SaveVersion) return;

        _pending = [];
        int hitches = r.ReadInt32();
        for (int k = 0; k < hitches; k++) _pending.Add((r.ReadUInt32(), r.ReadString()));

        _breeding.NextTry.Clear();
        int clocks = r.ReadInt32();
        for (int k = 0; k < clocks; k++) _breeding.NextTry[r.ReadUInt32()] = r.ReadDouble();
    }

    /// <summary>
    /// Every hitch of the last world ticked, and those still waiting for their characters.
    /// </summary>
    List<(uint Serial, string Post)> SavedHitches()
    {
        var list = new List<(uint, string)>(_pending ?? []);
        if (_world is not { } world) return list;

        var table = world.Characters;
        for (int s = 0; s < table.HighWater; s++)
        {
            if (table.Next[s].Alive && table.Cold(s) is { PortraitSerial: { } serial } cold && Hitches.PostOf(cold) is { } post)
                list.Add((serial, post));
        }

        return list;
    }

    /// <summary>
    /// Puts the saved hitches back on their characters at the first tick after a load (the engine keeps no mod jobs in its save).
    /// </summary>
    void RestoreHitches(World world)
    {
        _world = world;
        if (_pending is not { } pending) return;

        _pending = null;
        var posts = pending.ToDictionary(p => p.Serial, p => p.Post);
        var table = world.Characters;
        for (int s = 0; s < table.HighWater; s++)
        {
            if (table.Next[s].Alive && table.Cold(s) is { IsPlayer: true, PortraitSerial: { } serial } cold && posts.TryGetValue(serial, out string? post))
                Hitches.Hitch(cold, post);
        }
    }

    #endregion

    #region Hashing

    public void Hash(ref StateHasher hasher)
    {
        hasher.Add(_breeding.NextTry.Count);
        foreach (var (serial, next) in _breeding.NextTry)
        {
            hasher.Add(serial);
            hasher.Add(next);
        }

        hasher.Add(_breedNow);
        hasher.Add(_pending?.Count ?? -1);
        HashHitches(ref hasher);
    }

    /// <summary>
    /// The hitches on the characters (the engine hashes no mod jobs).
    /// </summary>
    void HashHitches(ref StateHasher hasher)
    {
        if (_world is not { } world) return;

        var table = world.Characters;
        for (int s = 0; s < table.HighWater; s++)
        {
            if (!table.Next[s].Alive || table.Cold(s) is not { } cold || Hitches.PostOf(cold) is not { } post) continue;

            hasher.Add(s);
            hasher.Add(Rng.StableHash(post));
        }
    }

    #endregion
}
