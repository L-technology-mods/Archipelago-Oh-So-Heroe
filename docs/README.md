# Development guide

This documentation explains how the Oh So Hero! Archipelago project is structured, built, tested, and debugged.

It is written so the project can be maintained without relying on the original chat history.

## Recommended reading order

1. [Code map](07-code-map.md)
2. [Debug workflow](08-debug-workflow.md)

## Current project state

- Game: Oh So Hero!, Unity Mono Windows build.
- Plugin: BepInEx 5, C# `netstandard2.1`.
- Archipelago: tested with 0.6.7.
- Current package: 0.16.8 RC22.
- APWorld locations: 259.
- APWorld items: 259.
- Player-facing location names are readable.
- Internal game names are still used for detection and translated through `LocationNameMapper`.

## Important directories

```text
OhSoHeroArchipelago/
  README.md                  Public project overview
  Plugin.cs                  BepInEx entrypoint and Harmony patches
  ArchipelagoClient.cs       Archipelago network client
  ItemApplier.cs             Received item behavior
  ZoneAccessManager.cs       Zone access enforcement
  DeathLinkManager.cs        DeathLink behavior
  LocationNameMapper.cs      Internal names <-> readable AP locations
  apworld/oh_so_hero/        APWorld source
  docs/                      Development documentation
  release/                   Release notes and install guide
  tools/                     Local debug scripts
```

## Safety notes

- Do not commit game files.
- Do not commit extracted game assets.
- Do not commit copied/decompiled game source.
- Do not commit generated build outputs.
- Do not commit packaged release zips unless using GitHub Releases.
- Do not commit third-party DLLs under `lib/`.
- Close the game before replacing the plugin DLL.
- Regenerate the seed after changing APWorld names, item pool, or logic.
