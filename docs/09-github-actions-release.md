# GitHub Actions release build

The release workflow builds three assets:

```text
oh_so_hero.apworld
OhSoHeroArchipelago-Client.zip
OhSoHeroArchipelago-Release.zip
```

## Why the mod build needs a self-hosted runner

The C# mod references Unity and Oh So Hero DLLs:

```text
OhSoHero_Data/Managed/Assembly-CSharp.dll
OhSoHero_Data/Managed/Fungus.dll
OhSoHero_Data/Managed/UnityEngine*.dll
```

These files must not be committed to the public repository and must not be shipped in the GitHub release.

Because of that, the `build-client` job runs on a Windows self-hosted GitHub Actions runner. That runner must be installed on a machine that already has the game and BepInEx installed.

## Required runner setup

Install a GitHub Actions self-hosted runner for the repository on Windows.

The runner must have these labels:

```text
self-hosted
Windows
```

The game path defaults to:

```text
D:\SteamLibrary\steamapps\common\Oh So Hero!
```

If the game is installed elsewhere, set a repository variable:

```text
OH_SO_HERO_GAME_DIR
```

Example value:

```text
D:\SteamLibrary\steamapps\common\Oh So Hero!
```

## What the workflow copies locally

During the build, the workflow copies these private references into the temporary checkout:

```text
lib/BepInEx/core/BepInEx.dll
lib/BepInEx/core/0Harmony.dll
```

It also points MSBuild to the local game directory through `Directory.Build.local.props`.

These files are used only during the runner build. They are not committed and are not added to the release zip.

## Release process

Create and push a version tag:

```bash
git tag v1.0.1
git push origin v1.0.1
```

The workflow will:

1. build the APWorld on GitHub-hosted Ubuntu;
2. build the mod/client zip on the self-hosted Windows runner;
3. publish all release assets to the GitHub Release.

If the self-hosted runner is offline, the release workflow will wait for it.
