using Meitou.Data.Fcs;

// Writes an empty .mod file at each path given: a code-only mod's folder still needs one for the game to list it (README "Branches and
// releases"). The play build (Play.csproj) calls it for mods whose .mod is missing.
foreach (string path in args)
{
    var file = new FcsFile { Author = "MeitouMods", Description = $"{Path.GetFileNameWithoutExtension(path)}: code only." };
    FcsWriter.WriteFile(file, path);
    Console.WriteLine($"Wrote empty {path}");
}

return 0;
