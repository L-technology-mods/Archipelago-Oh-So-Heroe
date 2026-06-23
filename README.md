# Oh So Hero! Archipelago

Archipelago integration for **Oh So Hero!**.

The project contains two parts:

- a BepInEx client plugin loaded by the Unity game;
- an Archipelago APWorld used by the generator, launcher, tracker, and server.

Current target:

- Game: Oh So Hero!, Unity Mono Windows build.
- Archipelago: 0.6.7.
- BepInEx: 5 Mono x64.
- Plugin version: 0.16.8 RC22.

## Repository layout

```text
OhSoHeroArchipelago/
  ArchipelagoClient.cs        AP connection, received items, location sending, goals
  Plugin.cs                   BepInEx entrypoint and Harmony patches
  ItemApplier.cs              Applies AP items to the game
  DeathLinkManager.cs         DeathLink behavior
  ZoneAccessManager.cs        Zone unlock checks and forced returns
  LocationNameMapper.cs       Internal location name -> readable AP name
  apworld/oh_so_hero/         Python APWorld source
  docs/                       Development and debugging documentation
  release/                    Release notes and install guide
  tools/                      Local debug helpers
```

## What should be committed

Commit source and data:

- `*.cs`
- `apworld/`
- `docs/`
- `release/`
- `tools/`
- `README.md`
- `.gitignore`
- `.gitattributes`

Do not commit generated or local files:

- `bin/`
- `obj/`
- packaged release zips;
- generated `.apworld` files;
- local game files;
- third-party DLLs under `lib/`.

## Build

1. Install BepInEx 5 Mono x64 for the game.
2. Copy local compile references:

```text
lib/BepInEx/core/BepInEx.dll
lib/BepInEx/core/0Harmony.dll
```

3. Create a local MSBuild config file named `Directory.Build.local.props`:

```xml
<Project>
  <PropertyGroup>
    <OhSoHeroGameDir>C:\Path\To\Oh So Hero!</OhSoHeroGameDir>
  </PropertyGroup>
</Project>
```

This file is ignored by git.

4. Build:

```powershell
dotnet build -c Release
```

Output:

```text
bin/Release/netstandard2.1/OhSoHeroArchipelago.dll
```

## Install client manually

Copy the built DLL to:

```text
Oh So Hero!/BepInEx/plugins/OhSoHeroArchipelago/OhSoHeroArchipelago.dll
```

The release package also includes a ready-to-copy client folder.

## Install APWorld

Copy:

```text
oh_so_hero.apworld
```

to:

```text
Archipelago/custom_worlds/oh_so_hero.apworld
```

After changing location names, item names, or rules, regenerate the seed. Old rooms are not compatible with renamed AP locations.

## Debugging

Start here:

- [Code map](docs/07-code-map.md)
- [Debug workflow](docs/08-debug-workflow.md)
- [Tests and debugging checklist](docs/06-tests-et-debogage.md)

The main game log is:

```text
Oh So Hero!/BepInEx/LogOutput.log
```
