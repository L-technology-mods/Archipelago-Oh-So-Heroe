# Local BepInEx references

This directory is intentionally kept without DLL files in git.

For local compilation, copy these files here:

```text
lib/BepInEx/core/BepInEx.dll
lib/BepInEx/core/0Harmony.dll
```

You can get them from a BepInEx 5 Mono x64 install, or from the installed game client package.

The project also references Unity/game DLLs from the local `OhSoHeroGameDir` MSBuild property:

```text
$(OhSoHeroGameDir)\OhSoHero_Data\Managed
```

Set that property in an ignored `Directory.Build.local.props` file at the repository root.
