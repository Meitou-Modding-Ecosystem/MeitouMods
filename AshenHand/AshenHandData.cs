using Meitou.Data;
using Meitou.Data.Characters;
using Meitou.Data.Fcs;

namespace AshenHand;

/// <summary>
/// The order's records (docs/ashen-hand.md "Data file"): the faction, a thief and a hunter, a squad of each, and the ledger their leaders carry. The
/// people are copies of the game's own Dust Bandit (gear, body and looks) and the ledger of its first book item, so the file is built from the
/// installed game and holds no game content of its own, only the copied fields and links by id.
/// </summary>
public static class AshenHandData
{
    /// <summary>
    /// The data file's name; a record's id ends with it.
    /// </summary>
    public const string FileName = "AshenHand.mod";

    /// <summary>
    /// The record ids.
    /// </summary>
    public const string Faction = "1-" + FileName, Thief = "2-" + FileName, Hunter = "3-" + FileName, ThiefSquad = "4-" + FileName,
        HunterSquad = "5-" + FileName, Ledger = "6-" + FileName;

    /// <summary>
    /// The game's character the order's people are copied from, by its name in the data files.
    /// </summary>
    public const string Model = "Dust Bandit";

    /// <summary>
    /// The ledger's text: a hunter's log of days spent on one target.
    /// </summary>
    public const string LedgerText = "A soot-stained notebook bound in grey cloth, marked with an ash-black handprint. Its pages log days of watching"
        + " one mark: where they slept, when they ate, which roads they took and who walked with them, each entry signed with the same initials."
        + " Proof that its owner stalked the mark for days. The law would want to see it.\n[Right-click to hand it to the law: a bounty on its"
        + " owner with every lawful faction]";

    /// <summary>
    /// Builds the data file from the game's records. Throws when the game has no <see cref="Model"/> character.
    /// </summary>
    public static FcsFile Build(GameDatabase game)
    {
        var model = CharacterAppearance.Find(game, Model, FcsRecordType.Character)
            ?? throw new InvalidOperationException($"The game data has no CHARACTER '{Model}'.");

        var book = Book(game);
        var file = new FcsFile { Author = "MeitouMods", Description = "The Ashen Hand: its faction, thieves and hunters. Generated; see docs/ashen-hand.md." };
        file.Records.AddRange([
            FactionRecord(),
            Person(model, Thief, "Ashen Hand Thief", armed: false),
            Person(model, Hunter, "Ashen Hand Hunter", armed: true),
            SquadRecord(ThiefSquad, "Ashen Hand thieves", Thief, 1, 4),
            SquadRecord(HunterSquad, "Ashen Hand hunters", Hunter, 3, 5),
            LedgerRecord(book)]);
        for (int k = 0; k < file.Records.Count; k++) file.Records[k].Id = k + 1;

        file.NextId = file.Records.Count + 1;
        return file;
    }

    /// <summary>
    /// The game's book the ledger is copied from (look, icon, size and weight): the ITEM named "Books", else the first ITEM by id whose name has
    /// "book" in it; null when the game has none.
    /// </summary>
    static GameRecord? Book(GameDatabase game)
    {
        var books = game.OfType(FcsRecordType.Item)
            .Where(r => r.Name.Contains("book", StringComparison.OrdinalIgnoreCase))
            .OrderBy(r => r.Name == "Books" ? 0 : 1)
            .ThenBy(r => r.StringId, StringComparer.Ordinal)
            .ToList();
        return books.FirstOrDefault();
    }

    /// <summary>
    /// The ledger: the book's fields and links (none without a book: no look, one cell, 0.2 kg), its own name and text, one per stack, worth
    /// little and no trade good.
    /// </summary>
    static FcsRecord LedgerRecord(GameRecord? book)
    {
        var record = New(Ledger, FcsRecordType.Item, "Ashen Hand Ledger");
        record.Floats["weight kg"] = 0.2f;
        if (book is not null)
        {
            CopyFields(book, record);
            foreach (string list in book.ReferenceLists) record.References[list] = [.. book.GetReferences(list).Select(Link)];
        }

        record.Strings["description"] = LedgerText;
        record.Ints["stackable"] = 1;
        record.Ints["value"] = 20;
        record.Bools["trade item"] = false;
        return record;
    }

    /// <summary>
    /// A new record, as a mod file adds one.
    /// </summary>
    static FcsRecord New(string id, FcsRecordType type, string name) => new() { StringId = id, Flags = 0x10, Name = name, RecordType = type };

    /// <summary>
    /// The faction: neutral to everyone, with no towns and no squads of its own roaming.
    /// </summary>
    static FcsRecord FactionRecord()
    {
        var record = New(Faction, FcsRecordType.Faction, "The Ashen Hand");
        record.Ints["default relation"] = 0;
        record.Ints["roaming population"] = 0;
        return record;
    }

    /// <summary>
    /// One of the order's people: the model's fields and links, in the order's faction, without the model's talk; unarmed for a thief.
    /// </summary>
    static FcsRecord Person(GameRecord model, string id, string name, bool armed)
    {
        var record = New(id, FcsRecordType.Character, name);
        CopyFields(model, record);
        foreach (string list in model.ReferenceLists)
        {
            if (list.StartsWith("dialog", StringComparison.OrdinalIgnoreCase) || !armed && list is "weapons" or "crossbows") continue;

            record.References[list] = [.. model.GetReferences(list).Select(Link)];
        }

        record.References["faction"] = [new FcsReference(Faction, 0, 0, 0)];
        return record;
    }

    /// <summary>
    /// A squad of <paramref name="min"/> to <paramref name="max"/> of one character, with no package: Rivalry Core drives it.
    /// </summary>
    static FcsRecord SquadRecord(string id, string name, string member, int min, int max)
    {
        var record = New(id, FcsRecordType.SquadTemplate, name);
        record.References["squad"] = [new FcsReference(member, min, max, 0)];
        record.References["faction"] = [new FcsReference(Faction, 0, 0, 0)];
        return record;
    }

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
