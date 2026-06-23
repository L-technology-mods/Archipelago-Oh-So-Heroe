# Debug workflow

Use this checklist when something breaks.

## 1. Confirm versions

Check the BepInEx log:

```text
Oh So Hero!/BepInEx/LogOutput.log
```

Look for:

```text
OhSoHeroArchipelago
Connected to Archipelago
Seed:
```

If the plugin version is not the expected version, the wrong DLL is installed.

## 2. Confirm the seed uses the matching APWorld

If locations were renamed, a new seed is required.

Symptoms of old APWorld / new client mismatch:

- `Unknown Archipelago location`;
- checks detected locally but not sent;
- tracker names do not match current source.

Fix:

1. Install the latest `oh_so_hero.apworld`.
2. Regenerate the seed.
3. Start a new room.

## 3. Debug a missing location check

Search `Plugin.cs` for the location source.

Examples:

```text
Defeat_
Pickup_
TurnIn_
Visit_
Buy_
_Talked
_EnemyGauntlet
GameplayAnimationLocationDetector
```

Then add temporary logging near the detection point:

```csharp
Plugin.Log.LogInfo($"Detected internal location: {locationName}");
```

Expected next logs:

```text
Location sent: <readable name> (<id>)
```

If you see `Unknown Archipelago location`, check `LocationNameMapper.cs` and `apworld/oh_so_hero/data/location_name_mapping.json`.

## 4. Debug a received item that does nothing

Check log order:

```text
Applied AP item #...
```

If the item is received but not applied, inspect:

```text
ArchipelagoClient.ProcessReceivedItems
ItemApplier.TryApply
```

Common causes:

- item name mismatch;
- game object not loaded yet;
- ability requires a forced save flag update;
- item already processed in `client_state.json`.

## 5. Debug zone access

Files:

```text
ZoneAccessManager.cs
Plugin.cs
```

Useful checks:

- Does the player have `Access_<Zone>`?
- Does the transition patch detect the correct target zone?
- Is the previous safe zone valid?
- Is the player being returned after loading into a locked zone?

## 6. Debug traps

Files:

```text
ItemApplier.cs
Plugin.cs
DeathLinkManager.cs
```

Test traps in these states:

- normal gameplay;
- combat;
- already in a scene;
- loading screen;
- KO/game over;
- menu open.

Traps must not permanently break controls or enemy state.

## 7. Debug collect_all_scenes

The server sees readable names.
The client goal logic converts them back to internal names before filtering.

Relevant code:

```text
ArchipelagoClient.CountSceneGoalLocations
ArchipelagoClient.IsSceneGoalLocation
LocationNameMapper.ToInternal
```

Bates scenes are excluded.
Pickups, talks, gauntlets, enemy defeats, visits, buys, and turn-ins are excluded.

## 8. Local build commands

Build:

```powershell
dotnet build -c Release
```

Deploy DLL only:

```powershell
Copy-Item `
  -LiteralPath ".\bin\Release\netstandard2.1\OhSoHeroArchipelago.dll" `
  -Destination "C:\Path\To\Oh So Hero!\BepInEx\plugins\OhSoHeroArchipelago\OhSoHeroArchipelago.dll" `
  -Force
```

Do not copy while the game is running. Windows can lock the DLL.

## 9. Before publishing a release

Required checks:

1. `dotnet build -c Release`
2. Generate a test seed.
3. Install APWorld locally.
4. Install client locally.
5. Launch game and confirm plugin version in log.
6. Verify one received item.
7. Verify one sent location.
8. Verify chosen goal can complete.
