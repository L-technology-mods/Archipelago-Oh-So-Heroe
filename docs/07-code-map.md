# Code map

This file explains where to look when debugging the project.

## Runtime flow

```text
Game event
  -> Harmony patch in Plugin.cs
  -> ArchipelagoClient.CompleteLocation(...)
  -> LocationNameMapper.ToDisplay(...)
  -> Archipelago server location check
```

Received items follow the opposite path:

```text
Archipelago server item
  -> ArchipelagoClient.OnItemReceived(...)
  -> ArchipelagoClient.ProcessReceivedItems()
  -> ItemApplier.TryApply(...)
  -> Game state mutation
```

## Main C# files

### Plugin.cs

Responsibilities:

- BepInEx entrypoint.
- Config loading.
- Harmony patch registration.
- Detecting game events and turning them into internal location names.
- Trap trigger hooks.
- Pickup/dialogue/gauntlet/scene/enemy detection patches.

When a location is not sent, start here.

Search for:

```text
CompleteLocation(
```

Each call is one source of AP checks.

### ArchipelagoClient.cs

Responsibilities:

- Connect and login to Archipelago.
- Read slot data.
- Queue received AP items.
- Send checked locations.
- Keep recovery locations if disconnected.
- Evaluate victory goals.
- Handle DeathLink messages.

When the game detects a check but the server does not receive it, debug here.

Important methods:

```text
Connect
Update
CompleteLocation
SendPendingLocations
EvaluateGoal
CountSceneGoalLocations
```

### ItemApplier.cs

Responsibilities:

- Convert AP item names into game effects.
- Unlock abilities.
- Unlock zones.
- Apply traps.
- Avoid reapplying already processed items.

When an item is received but does not change the game, debug here.

### LocationNameMapper.cs

Responsibilities:

- Translate internal game/check names to readable Archipelago names.
- Translate readable names back to internal names for goal logic.

This file is generated from:

```text
apworld/oh_so_hero/data/location_name_mapping.json
```

Internal names are still used by code and detection.
Readable names are used by the APWorld/server/tracker.

### ZoneAccessManager.cs

Responsibilities:

- Decide whether a zone is unlocked.
- Keep a safe previous zone.
- Force the player back if they enter a locked zone.

When a player can access something too early, debug here and the transition patches in `Plugin.cs`.

### DeathLinkManager.cs

Responsibilities:

- Apply incoming DeathLink as an in-game KO.
- Avoid looping received deaths back to Archipelago.

## Main APWorld files

### apworld/oh_so_hero/__init__.py

Archipelago world implementation.

Responsibilities:

- Create regions.
- Create locations.
- Create item pool.
- Set completion rules.
- Fill slot data for the client.

### apworld/oh_so_hero/data.py

Data loader and logic helper.

Responsibilities:

- Load JSON data.
- Build item/location IDs.
- Map readable location names back to zones.
- Define item classifications.

### apworld/oh_so_hero/options.py

Player YAML options:

- goal;
- trap percentage;
- trap duration;
- DeathLink.

### apworld/oh_so_hero/client.py

Launcher integration shown in Archipelago Launcher.

It starts the game and validates the client install.

## JSON data files

### archipelago_items.json

All AP items.

Includes:

- progression items;
- useful items;
- fillers;
- traps.

### archipelago_locations.json

All AP-visible locations.

These names are readable player-facing names.

### location_name_mapping.json

Mapping from internal names to readable AP names.

This is the important bridge between the C# client and the APWorld.

### world_logic.json

High-level progression/zone data.

### enemy_checks.json

Enemy defeat checks.

### event_checks.json

Dialogue and gauntlet event flags.

### important_pickup_checks.json

Important vanilla pickups that create AP checks.

## Debug rule

Always identify which side failed:

1. Did the game event happen?
2. Did a Harmony patch log it?
3. Did `CompleteLocation` receive the internal name?
4. Did `LocationNameMapper` convert it to the readable AP name?
5. Did `SendPendingLocations` find an AP location ID?
6. Did the server mark it checked?

Do not change logic before knowing which step failed.
