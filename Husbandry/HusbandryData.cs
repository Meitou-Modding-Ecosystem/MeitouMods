using Meitou.Data;
using Meitou.Data.Fcs;

namespace Husbandry;

/// <summary>
/// The mod's records (docs/husbandry.md "Data file"): the Animal Husbandry research and the Hitching Post it unlocks. The post is a copy of the
/// game's own Prisoner Pole (look, collision, size, build cost) without its cage function, so the file is built from the installed game and holds
/// no game content of its own, only the copied fields and links by id.
/// </summary>
public static class HusbandryData
{
    /// <summary>
    /// The data file's name; a record's id ends with it.
    /// </summary>
    public const string FileName = "Husbandry.mod";

    /// <summary>
    /// The record ids.
    /// </summary>
    public const string Research = "1-" + FileName, Post = "2-" + FileName;

    /// <summary>
    /// The game's building the post is copied from: its string id, else its name.
    /// </summary>
    public const string ModelId = "18913-small_changes_otto.mod", ModelName = "Prisoner Pole";

    /// <summary>
    /// The post's text in the build menu.
    /// </summary>
    public const string PostText = "A stout post to tether animals to. Select your animals, right-click the post and choose Hitch here: they stay"
        + " within a pen round it, leaving only to eat at an Animal Feeder. Hitched adults of the same kind, a male and a female, try to breed every"
        + " few days, more often when well fed. Build an Animal Feeder nearby and keep it stocked.";

    /// <summary>
    /// The research's text.
    /// </summary>
    public const string ResearchText = "The basics of keeping animals at your base. Unlocks the Hitching Post: animals hitched to it keep to a pen"
        + " round it and breed when a male and a female of the same kind are there. Together with an Animal Feeder (Item Storage) to feed them, it is"
        + " all a ranch needs.";

    /// <summary>
    /// Builds the data file from the game's records. Throws when the game has no <see cref="ModelName"/> building.
    /// </summary>
    public static FcsFile Build(GameDatabase game)
    {
        var model = Model(game) ?? throw new InvalidOperationException($"The game data has no BUILDING '{ModelName}'.");

        var file = new FcsFile { Author = "MeitouMods", Description = "Husbandry: hitching posts and breeding. Generated; see docs/husbandry.md." };
        file.Records.AddRange([ResearchRecord(), PostRecord(model)]);
        for (int k = 0; k < file.Records.Count; k++) file.Records[k].Id = k + 1;

        file.NextId = file.Records.Count + 1;
        return file;
    }

    /// <summary>
    /// The building the post is copied from, or null.
    /// </summary>
    static GameRecord? Model(GameDatabase game) =>
        game.Find(ModelId) is { Type: FcsRecordType.Building } byId
            ? byId
            : game.OfType(FcsRecordType.Building)
                .Where(r => r.Name == ModelName)
                .OrderBy(r => r.StringId, StringComparer.Ordinal)
                .FirstOrDefault();

    /// <summary>
    /// The research: level 1, no blueprint, in the Core list, as quick as the game's Item Storage.
    /// </summary>
    static FcsRecord ResearchRecord()
    {
        var record = New(Research, FcsRecordType.Research, "Animal Husbandry");
        record.Ints["level"] = 1;
        record.Ints["money"] = 0;
        record.Ints["repeats"] = 0;
        record.Ints["time"] = 4;
        record.Ints["power capacity increase"] = 0;
        record.Ints["power increase"] = 0;
        record.Floats["production mult"] = 1;
        record.Floats["repeat mult"] = 1;
        record.Bools["blueprint only"] = false;
        record.Bools["is level upgrade"] = false;
        record.Strings["category"] = "Core";
        record.Strings["description"] = ResearchText;
        record.References["enable buildings"] = [new FcsReference(Post, 0, 0, 0)];
        return record;
    }

    /// <summary>
    /// The post: the model's fields and links but its function (the cage), in the build menu's farming list, with its own text.
    /// </summary>
    static FcsRecord PostRecord(GameRecord model)
    {
        var record = New(Post, FcsRecordType.Building, "Hitching Post");
        CopyFields(model, record);
        foreach (string list in model.ReferenceLists)
        {
            if (list == "functionality") continue;

            record.References[list] = [.. model.GetReferences(list).Select(Link)];
        }

        record.Strings["building category"] = "FARMING";
        record.Strings["building group"] = "";
        record.Strings["Description"] = PostText;
        record.Bools["creates player town"] = true;
        return record;
    }

    /// <summary>
    /// A new record, as a mod file adds one.
    /// </summary>
    static FcsRecord New(string id, FcsRecordType type, string name) => new() { StringId = id, Flags = 0x10, Name = name, RecordType = type };

    /// <summary>
    /// Copies every field of a merged record but its links.
    /// </summary>
    static void CopyFields(GameRecord from, FcsRecord to)
    {
        foreach (var (key, value) in from.Bools) to.Bools[key] = value;
        foreach (var (key, value) in from.Floats) to.Floats[key] = value;
        foreach (var (key, value) in from.Ints) to.Ints[key] = value;
        foreach (var (key, value) in from.Vector3S) to.Vector3S[key] = value;
        foreach (var (key, value) in from.Vector4S) to.Vector4S[key] = value;
        foreach (var (key, value) in from.Strings) to.Strings[key] = value;
        foreach (var (key, value) in from.Filenames) to.Filenames[key] = value;
    }

    /// <summary>
    /// A merged link as a file's.
    /// </summary>
    static FcsReference Link(GameReference link) => new(link.TargetStringId, link.Values.Value0, link.Values.Value1, link.Values.Value2);
}
