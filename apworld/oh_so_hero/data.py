import json
import pkgutil
from typing import Any, Dict, List, Set


ITEM_BASE_ID = 772_000_000
LOCATION_BASE_ID = 772_100_000


def load_json(name: str) -> Dict[str, Any]:
    payload = pkgutil.get_data(__package__, f"data/{name}")
    if payload is None:
        raise FileNotFoundError(f"Missing APWorld resource: data/{name}")
    return json.loads(payload.decode("utf-8-sig"))


ITEM_ROWS: List[Dict[str, Any]] = load_json("archipelago_items.json")["items"]
LOCATION_NAMES: List[str] = load_json("archipelago_locations.json")["locations"]
LOCATION_NAME_ALIASES: Dict[str, str] = load_json("location_name_mapping.json")[
    "old_to_new"
]
DISPLAY_TO_INTERNAL_LOCATION = {
    display_name: internal_name
    for internal_name, display_name in LOCATION_NAME_ALIASES.items()
}
WORLD_LOGIC: Dict[str, Any] = load_json("world_logic.json")
EVENT_CHECKS: Dict[str, Any] = load_json("event_checks.json")
ENEMY_CHECKS: Dict[str, Any] = load_json("enemy_checks.json")
PICKUP_CHECKS: Dict[str, Any] = load_json("important_pickup_checks.json")

TRAP_NAMES = tuple(row["name"] for row in ITEM_ROWS if row.get("trap"))
ZONE_ACCESS_ITEMS: Set[str] = {
    row["name"] for row in ITEM_ROWS if row.get("kind") == "zone_access"
}
PROGRESSION_ITEMS: Set[str] = ZONE_ACCESS_ITEMS | {
    "SlideAbility",
    "ButtStompAbility",
    "HirotoDojoKey",
    "RedKeyCard",
}
USEFUL_ITEMS: Set[str] = {
    row["name"]
    for row in ITEM_ROWS
    if row.get("kind") in {"skill", "equipment", "upgrade", "key_item", "item"}
} - PROGRESSION_ITEMS

ITEM_NAMES = [row["name"] for row in ITEM_ROWS] + ["Nothing"]
ITEM_NAME_TO_ID = {
    name: ITEM_BASE_ID + index for index, name in enumerate(ITEM_NAMES)
}

VICTORY_EVENT_LOCATION = "Victory Event"
ALL_LOCATION_NAMES = LOCATION_NAMES
LOCATION_NAME_TO_ID = {
    name: LOCATION_BASE_ID + index
    for index, name in enumerate(ALL_LOCATION_NAMES)
}


ENTITY_ZONES = {
    "Daku": "SheoIslandsBeach",
    "Amaru": "SheoIslandsBeach",
    "Ket": "SheoIslandsBeach",
    "Kaylee": "SheoIslandsBeach",
    "Xiao": "SheoIslandsBeach",
    "Bryce": "SheoIslandsBeach",
    "Koji": "SheoIslandsBeach",
    "Lonoe": "TreewishForest",
    "Goliath": "TreewishForest",
    "KRen": "TreewishForest",
    "Leon": "TreewishForest",
    "Cas": "CasHouse",
    "Jack": "SoutheastBeach",
    "Tiburon": "SoutheastBeach",
    "Domino": "SoutheastBeach",
    "Korko": "SoutheastBeach",
    "Lance": "SoutheastBeach",
    "Lemiel": "SoutheastBeach",
    "Lago": "SoutheastBeach",
    "Biro": "SoutheastBeach",
    "Chad": "SoutheastBeach",
    "Brask": "BraskJungle",
    "Blowey": "ForbiddenBayou",
    "Gatis": "ForbiddenBayou",
    "ODeere": "ForbiddenBayou",
    "Dan": "ForbiddenBayou",
    "Griff": "ForbiddenBayou",
    "Dyabal": "DyabalCabin",
    "Bax": "HirotoDojo",
    "Puca": "HirotoDojo",
    "NinjaClan": "HirotoDojo",
    "Haya": "HirotoDojo",
    "Ajax": "HirotoDojo",
    "Cian": "HirotoDojo",
    "Foxel": "HirotoDojo",
    "OTary": "HirotoDojo",
    "Stier": "LoodCityWestAvenue",
    "Signal": "LoodCityWestAvenue",
    "Ray": "LoodCityWestAvenue",
    "Octo": "OctoSushiPlace",
    "Kyomu": "LoodCityWestAvenue",
    "Blitz": "LoodCityWestAvenue",
    "Bjorne": "LoodCityWestAvenue",
    "Volte": "LoodCityWestAvenue",
    "Jaer": "LoodCityWestAvenue",
    "VeeneAndNeem": "LoodCityWestAvenue",
    "Trace": "LoodCityWestAvenue",
    "Hamill": "LoodCityWestAvenue",
    "Myriad": "LoodCityWestAvenue",
    "Ryuta": "LoodCityWestAvenue",
    "Liath": "LoodCityWestAvenue",
    "Lin": "LoodCityPark",
    "Bates": "LoodCityPark",
    "Joe": "SheoIslandsBeach",
}

EXPLICIT_LOCATION_ZONES = {
    check["location"]: check["zone"] for check in ENEMY_CHECKS["checks"]
}
EXPLICIT_LOCATION_ZONES.update({
    check["name"]: check["zone"] for check in EVENT_CHECKS["gauntlet_flags"]
})
EXPLICIT_LOCATION_ZONES.update({
    check["location"]: check["zone"] for check in PICKUP_CHECKS["checks"]
})
EXPLICIT_LOCATION_ZONES.update({
    "Pickup_HirotoDojo_HirotoDojoKey": "HirotoDojo",
    "Visit_DojoSecretBasement": "DojoSecretBasement",
    "TurnIn_HirotoDojo_HirotoDojoKey": "HirotoDojo",
    "TurnIn_LoodCityWestAvenue_RedKeyCard": "LoodCityWestAvenue",
    "Buy_MirillBar_Drink1": "LoodCityWestAvenue",
    "Buy_MirillBar_Drink2": "LoodCityWestAvenue",
    "BraskJungle_GameOver01": "BraskJungle",
    "ForbiddenBayou_GameOver01": "ForbiddenBayou",
    "HirotoDojo_GameOver01": "HirotoDojo",
    "LoodCityWestAvenue_GameOver01": "LoodCityWestAvenue",
    "SheoIslandsBeach_GameOver01": "SheoIslandsBeach",
    "SoutheastBeach_GameOver01": "SoutheastBeach",
    "TreewishForest_GameOver01": "TreewishForest",
    "Ket_SecretBeachBall": "SheoIslandsBeach",
    "SheoIslandsBeachSign01_Talked": "SheoIslandsBeach",
})


def location_zone(name: str) -> str:
    internal_name = DISPLAY_TO_INTERNAL_LOCATION.get(name, name)
    name = internal_name
    if name in EXPLICIT_LOCATION_ZONES:
        return EXPLICIT_LOCATION_ZONES[name]
    if name.startswith("Pickup_"):
        return "UNMAPPED_PICKUP"
    prefix = name.split("_", 1)[0]
    if prefix.endswith("01") and name.endswith("_Talked"):
        prefix = prefix[:-2]
    for entity, zone in ENTITY_ZONES.items():
        if prefix == entity or name.startswith(entity):
            return zone
    return "UNMAPPED"
