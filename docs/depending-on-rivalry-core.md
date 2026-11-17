# Depending on Rivalry Core

How a code mod builds on Rivalry Core (`rivalry.core`): reference it, declare it, register rules, and send parties within its caps. Contract
and details: [rivalry-core.md](rivalry-core.md); loader rules: engine
[modding-dependencies.md](https://github.com/Meitou-Modding-Ecosystem/MeitouClient/blob/master/docs/modding-dependencies.md). An agent skill
walks through the same steps: [.claude/skills/rivalry-core-dependent](../.claude/skills/rivalry-core-dependent/SKILL.md).

## 1. Get it and reference it at build time, never ship it

| Source | Use it for | Where |
| --- | --- | --- |
| Commit-pinned source | building against current `beta` before any release | this repo as a git submodule of yours, pinned to a commit |
| Beta zip | testers' installs; a DLL to compile against without a submodule | prereleases `rivalry-core-<version>-beta.<commit>` (one per beta commit that changes the framework, never moved) and `rivalry-core-beta` (always the newest) |
| Release zip | published mods | releases `rivalry-core-v<version>`, tagged on `release` |

In every case:

- Reference Rivalry Core with `Private="false"`, and never list `RivalryCore.dll` in your own `assemblies`: the loader refuses the mod. A
  stray copy in your folder is ignored; the loaded `rivalry.core` serves the types, so every dependent shares one framework, one grudge
  ledger and one set of party caps.
- Build against the same engine commit Rivalry Core was built against: the `engine/` pin of the MeitouMods commit, which every zip's
  release notes name.
- The version lives only in `RivalryCore/meitou.json`; the DLL takes it at build time. A beta zip carries the manifest's version, so your
  `dependencies` range accepts it, but later beta builds can carry the same version: pin the tag, not the version.

### In this repo

`<ProjectReference Include="../RivalryCore/RivalryCore.csproj" Private="false" />`, beside the engine references (see
`AshenHand/AshenHand.csproj`).

### Commit-pinned source

**In your mod repo's root:**

```sh
git submodule add https://github.com/Meitou-Modding-Ecosystem/MeitouMods.git MeitouMods
git -C MeitouMods checkout <commit on beta>
git submodule update --init --recursive
git add .gitmodules MeitouMods
git commit -m "Pin Rivalry Core to <commit on beta>"
```

In your mod's `.csproj` (one folder below your repo's root), reference the engine inside the submodule, so your mod and Rivalry Core build
against one engine:

```xml
<ItemGroup>
  <ProjectReference Include="../MeitouMods/engine/src/Meitou.Core/Meitou.Core.csproj" Private="false" />
  <ProjectReference Include="../MeitouMods/engine/src/Meitou.Data/Meitou.Data.csproj" Private="false" />
  <ProjectReference Include="../MeitouMods/engine/src/Meitou.Simulation/Meitou.Simulation.csproj" Private="false" />
  <ProjectReference Include="../MeitouMods/engine/src/Meitou.Game/Meitou.Game.csproj" Private="false" />
  <ProjectReference Include="../MeitouMods/RivalryCore/RivalryCore.csproj" Private="false" />
</ItemGroup>
```

Import `MeitouMods/engine/Directory.Build.props` from your own `Directory.Build.props`, so your mod builds with the engine's settings.

To install the pinned Rivalry Core for play, download the beta zip of that commit if it has one; otherwise, **in your mod repo's root**:

```sh
dotnet publish MeitouMods/RivalryCore -c Release -o "<Kenshi>/mods/RivalryCore"
dotnet run --project MeitouMods/tools/EmptyMod -c Release -- "<Kenshi>/mods/RivalryCore/RivalryCore.mod"
```

To move the pin, **in your mod repo's root**:

```sh
git -C MeitouMods fetch origin beta
git -C MeitouMods checkout <new commit>
git submodule update --init --recursive
git commit -am "Move Rivalry Core to <new commit>"
```

### Zip

Each zip holds the `RivalryCore/` folder (`RivalryCore.dll`, `RivalryCore.pdb`, `meitou.json` and an empty `RivalryCore.mod`).

| Zip | Download |
| --- | --- |
| Newest beta | `https://github.com/Meitou-Modding-Ecosystem/MeitouMods/releases/download/rivalry-core-beta/RivalryCore-beta.zip` |
| One beta build | `https://github.com/Meitou-Modding-Ecosystem/MeitouMods/releases/download/rivalry-core-<version>-beta.<commit>/RivalryCore-<version>-beta.<commit>.zip` |
| Release | `https://github.com/Meitou-Modding-Ecosystem/MeitouMods/releases/download/rivalry-core-v<version>/RivalryCore-<version>.zip` |

- To play: extract the zip into `Kenshi/mods/`, so the files sit in `Kenshi/mods/RivalryCore/`, and enable Rivalry Core above your mod.
- To compile against it: copy `RivalryCore.dll` from the zip to `lib/RivalryCore.dll` in your mod repo, and in your mod's `.csproj`
  (one folder below your repo's root) add `<Reference Include="RivalryCore" HintPath="../lib/RivalryCore.dll" Private="false" />` beside
  your engine references.

## 2. Declare it in `meitou.json`

```json
"apiVersion": 2,
"dependencies": [{ "id": "rivalry.core", "version": ">=0.6.0 <0.7.0" }]
```

- Before 1.0 a minor version may break the API, so cap the range at the next minor. 0.6.0 is the first version with party caps.
- Rivalry Core must sit above your mod in `data/mods.cfg`; the launcher and loader say "move X above it" otherwise.

## 3. Two ways players get it

Write the mod so both work; the code is the same.

| Delivery | What the player has | What you do |
| --- | --- | --- |
| Separate release, loaded first | the `RivalryCore/` folder from its release zip in `Kenshi/mods/`, enabled above your mod | link the release in your mod's page; declare the range |
| Baked into your download | a pipeline (planned) copies the unchanged `RivalryCore/` folder of a release zip next to your mod folder | still declare the dependency; never rename its id, change its manifest, or merge its DLL into yours |

- The engine loads one mod per id: when two downloads both bring `rivalry.core`, the first enabled copy loads and later copies are refused
  ("an id another mod took"). So a baked copy must stay `rivalry.core`: a renamed or merged copy would run a second framework with its own
  caps, and the one-hostile-party rule would break.
- The copy that loads must satisfy every dependent's range; with an older copy first, dependents needing newer are refused. Keep ranges as
  wide as your code allows.

## 4. Register in `Initialize`

```csharp
public void Initialize(ModContext c)
{
    var rivalry = c.Dependency<IRivalryApi>(RivalryMod.Id) ?? throw new InvalidOperationException("Needs Rivalry Core.");
    var mine = rivalry.For(c.Id);                     // every name starts with "<your id>."
    mine.AddRule(new Rule
    {
        Name = $"{c.Id}.collectors",
        Target = BuiltIn.CharacterTarget,
        When = (a, t) => a.Notoriety.Grudge(a.Data.FactionIndex("1234-mymod.mod"), t.Character) >= a.Options.HuntThreshold,
        Behaviour = BuiltIn.Pursue,                    // hostile: counted under the cap
        CooldownHours = _ => 48,
        Faction = (a, _) => a.Data.FactionIndex("1234-mymod.mod"),
        Args = (_, _) => new BehaviourArgs().Set("template", "5678-mymod.mod"),
    });
}
```

- Registrations only in `Initialize`; later ones throw. Duplicate or unprefixed names throw.
- Rivalry Core drops everything your mod registered if your `Initialize` throws after registering (`IModDependents`).
- Code in rules, conditions, targets and directors reads only; randomness from `Assessment.Roll(key)`; state only in cooldowns
  (`Decisions.TryClaim`) and runs. Behaviours keep state only in their `BehaviourRun`.
- Use `rivalry.Options` only for the code-only values it documents (`Homes`, `OwnHitSquads`, packages). The party caps are not options.

## 5. Parties and the caps

At most one hostile party is out against the player at a time, from all mods together; friendly parties up to the player's setting (1 to 10).
Details: [rivalry-core.md "Party caps"](rivalry-core.md#party-caps).

- **Using a built-in behaviour or decision**: nothing to do. `pursue`, `burgle`, `raid`, `hunt` are hostile; `visit`, `reinforce` friendly.
- **Your own behaviour**: declare `public PartyKind Party => PartyKind.Hostile;` (or `Friendly`). Override `Underway(run)` when a run stops
  counting before it ends (as a hit squad does once beaten). A behaviour that sends people and declares `None` slips past the cap: don't.
- **Your own director**: check `assessment.MayStart(PartyKind.Hostile, Name)` before `TryClaim`; a start over the cap is held when carried
  out and the cooldown is spent.
- **A party your mod runs itself** (its own pursuit code, not a Rivalry behaviour): claim a slot before sending, renew it while out,
  release it after.

```csharp
var state = rivalry.In(world);                        // in your system's serial step
var claim = new PartyClaim($"{c.Id}.bounty", PartyKind.Hostile, characterName, hours: 6, faction);
if (state?.ClaimParty(claim) != true) return;         // cap full: send nothing now, ask again later
// ... while the crew is out, call ClaimParty(claim) again within 6 h; when it ends:
state.ReleaseParty(claim.Source, claim.Key);
```

- **Going over the cap**: only on purpose, in `Initialize`: `mine.ExceedHostileCap($"{c.Id}.siege", 1, "the siege brings its own scouts")`.
  The source may send one more than the cap; its parties still count for everyone else. `Rivalry: parties` (F10) lists every override.
- **Seeing it**: `IRivalryState.Parties`, `PartiesOut(kind)`, `PartyCap(kind)`; F10 `Rivalry: parties` and `Rivalry: log` (`held: ...`).
