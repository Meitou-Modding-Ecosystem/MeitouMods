# Meitou mods

Code mods for [MeitouClient](https://github.com/TheIvoryDisaster/MeitouClient), one folder per mod. GPL-3.0-or-later.

| Mod | Id | Docs |
| --- | --- | --- |
| Rivalry Core | `rivalry.core` | [docs/rivalry-core.md](docs/rivalry-core.md) |

## Layout

- `engine/`: MeitouClient as a git submodule, pinned to the commit the mods build against. Until the world hooks are merged upstream it
  points at the fork's hooks branch.
- `<Mod>/`: the mod's project and `meitou.json`. `<Mod>.Tests/`: its tests (synthetic worlds, no game install needed).
- `Directory.Build.props` imports the engine's, so mods build with the game's settings (warnings are errors).

## Build

```
git clone --recurse-submodules <this repo>
dotnet build
dotnet test
```

After a pull that moves the engine pin: `git submodule update --init`.

## Branches and releases

- Work goes in PRs to `beta`; CI (build and tests) must pass. `beta` is merged into `main` once it has been tested in play. Production is promoted through a PR to `release`, with two approvals including a code owner.
- A mod is released by pushing a tag `<mod-slug>-v<version>` on `release` (e.g. `rivalry-core-v0.2.0`; slugs in
  `.github/workflows/release.yml`). CI builds that mod and publishes `<Mod>-<version>.zip`, holding the folder to drop into `Kenshi/mods/`.
  The version must match the mod's `meitou.json`.
- A mod folder also needs a `<Mod>.mod` file. A mod without data ships none; make an empty one in FCS until the mod has one in the repo.
