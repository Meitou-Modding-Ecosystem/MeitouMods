using AshenHand;
using Meitou.Content;
using Meitou.Data;
using Meitou.Data.Fcs;

// Writes AshenHand.mod from the unmodded game's records. Arguments: the game's folder (else KENSHI_PATH or meitou.local.json), then the file to
// write (else AshenHand.mod in the working directory).
var install = args.Length > 0 ? GameInstall.Open(args[0]) : GameInstall.Locate();
if (install is null)
{
    Console.Error.WriteLine("No game install: pass the game's folder, or set KENSHI_PATH.");
    return 1;
}

string output = Path.GetFullPath(args.Length > 1 ? args[1] : AshenHandData.FileName);
var file = AshenHandData.Build(GameDatabase.Load(LoadOrder.BaseGame(install)));
FcsWriter.WriteFile(file, output);
Console.WriteLine($"Wrote {file.Records.Count} records to {output}");
return 0;
