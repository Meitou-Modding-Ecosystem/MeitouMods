using Meitou.Data.Fcs;

// Writes a .mod with a header and no records: the data file a code mod's folder needs when the mod adds no data (engine docs/modding.md).
// Argument: the file to write.
if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: EmptyMod <output .mod>");
    return 1;
}

string output = Path.GetFullPath(args[0]);
FcsWriter.WriteFile(new FcsFile(), output);
Console.WriteLine($"Wrote {output}");
return 0;
