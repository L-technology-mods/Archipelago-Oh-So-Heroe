from typing import Dict, List

from BaseClasses import Entrance, Item, ItemClassification, Location, Region
from worlds.AutoWorld import World
from worlds.generic.Rules import set_rule
from worlds.LauncherComponents import (
    Component,
    Type as ComponentType,
    components,
    icon_paths,
    launch_subprocess,
)

from .data import (
    ITEM_NAME_TO_ID,
    ITEM_ROWS,
    LOCATION_NAMES,
    LOCATION_NAME_TO_ID,
    PROGRESSION_ITEMS,
    TRAP_NAMES,
    USEFUL_ITEMS,
    VICTORY_EVENT_LOCATION,
    WORLD_LOGIC,
    location_zone,
)
from .options import OhSoHeroOptions


GAME_NAME = "Oh So Hero!"


def launch_client(*args: str) -> None:
    from .client import launch

    launch_subprocess(launch, name="OhSoHeroClient", args=args)


components.append(
    Component(
        "Oh So Hero Client",
        game_name=GAME_NAME,
        func=launch_client,
        component_type=ComponentType.CLIENT,
        supports_uri=True,
        icon="oh_so_hero",
        description="Configure the BepInEx client and launch Oh So Hero!",
    )
)
icon_paths["oh_so_hero"] = f"ap:{__name__}/icons/oh_so_hero.png"


class OhSoHeroItem(Item):
    game = GAME_NAME


class OhSoHeroLocation(Location):
    game = GAME_NAME


class OhSoHeroWorld(World):
    """Archipelago integration for Oh So Hero!."""

    game = GAME_NAME
    options_dataclass = OhSoHeroOptions
    options: OhSoHeroOptions
    topology_present = True

    item_name_to_id = ITEM_NAME_TO_ID
    location_name_to_id = LOCATION_NAME_TO_ID

    item_name_groups = {
        "Zone Access": {
            row["name"] for row in ITEM_ROWS if row.get("kind") == "zone_access"
        },
        "Traps": set(TRAP_NAMES),
    }

    def create_item(self, name: str) -> OhSoHeroItem:
        if name in PROGRESSION_ITEMS:
            classification = ItemClassification.progression
        elif name in USEFUL_ITEMS:
            classification = ItemClassification.useful
        elif name in TRAP_NAMES:
            classification = ItemClassification.trap
        else:
            classification = ItemClassification.filler
        return OhSoHeroItem(
            name, classification, self.item_name_to_id[name], self.player
        )

    def create_event(self, name: str) -> OhSoHeroItem:
        return OhSoHeroItem(
            name, ItemClassification.progression, None, self.player
        )

    def create_regions(self) -> None:
        regions: Dict[str, Region] = {
            "Menu": Region("Menu", self.player, self.multiworld)
        }
        for zone in WORLD_LOGIC["active_zones"]:
            regions[zone] = Region(zone, self.player, self.multiworld)

        for location_name in LOCATION_NAMES:
            zone = location_zone(location_name)
            if zone.startswith("UNMAPPED"):
                raise ValueError(
                    f"Location {location_name} has no confirmed zone ({zone})."
                )
            regions[zone].locations.append(
                OhSoHeroLocation(
                    self.player,
                    location_name,
                    self.location_name_to_id[location_name],
                    regions[zone],
                )
            )

        victory_region = regions["LoodCityPark"]
        victory_location = OhSoHeroLocation(
            self.player,
            VICTORY_EVENT_LOCATION,
            None,
            victory_region,
        )
        victory_location.place_locked_item(self.create_event("Victory"))
        victory_region.locations.append(victory_location)

        self.multiworld.regions.extend(regions.values())
        regions["Menu"].connect(regions[WORLD_LOGIC["starting_zone"]])

        for rule in WORLD_LOGIC["zone_rules"]:
            zone = rule["zone"]
            if zone == WORLD_LOGIC["starting_zone"]:
                continue
            parent = self._parent_zone(zone)
            entrance = Entrance(
                self.player, f"Enter {zone}", regions[parent]
            )
            entrance.connect(regions[zone])
            required = tuple(rule.get("requires", []))
            set_rule(
                entrance,
                lambda state, required=required: state.has_all(
                    required, self.player
                ),
            )
            regions[parent].exits.append(entrance)

    def _parent_zone(self, zone: str) -> str:
        parents = {
            "AliSurfShack": "SheoIslandsBeach",
            "TreewishForest": "SheoIslandsBeach",
            "SoutheastBeach": "TreewishForest",
            "CasHouse": "SoutheastBeach",
            "BraskJungle": "TreewishForest",
            "BraskPalace": "BraskJungle",
            "ForbiddenBayou": "BraskJungle",
            "HirotoDojo": "ForbiddenBayou",
            "DojoSecretBasement": "HirotoDojo",
            "BayouCabin": "ForbiddenBayou",
            "DyabalCabin": "ForbiddenBayou",
            "SmolBeach": "ForbiddenBayou",
            "LoodCityWestAvenue": "SmolBeach",
            "OctoSushiPlace": "LoodCityWestAvenue",
            "SuperImportTechAndMore": "LoodCityWestAvenue",
            "LoodCityPark": "LoodCityWestAvenue",
        }
        return parents[zone]

    def create_items(self) -> None:
        start_item = self.create_item("Access_SheoIslandsBeach")
        self.multiworld.push_precollected(start_item)

        pool_names: List[str] = []
        for row in ITEM_ROWS:
            if row.get("trap") or row.get("precollected"):
                continue
            pool_names.extend([row["name"]] * int(row.get("count", 1)))

        random_location_count = len(
            self.multiworld.get_unfilled_locations(self.player)
        )
        remaining = random_location_count - len(pool_names)
        if remaining < 0:
            raise ValueError("The fixed item pool is larger than the location pool.")

        trap_count = round(remaining * self.options.trap_percentage.value / 100)
        for index in range(trap_count):
            pool_names.append(TRAP_NAMES[index % len(TRAP_NAMES)])
        pool_names.extend(["OhSoSnack"] * (remaining - trap_count))

        self.multiworld.itempool.extend(map(self.create_item, pool_names))

    def set_rules(self) -> None:
        if self.options.goal == self.options.goal.option_defeat_bates:
            goal_location = self.multiworld.get_location(
                VICTORY_EVENT_LOCATION, self.player
            )
        else:
            goal_location = self.multiworld.get_location(
                VICTORY_EVENT_LOCATION, self.player
            )
            required = tuple(PROGRESSION_ITEMS)
            set_rule(
                goal_location,
                lambda state: state.has_all(required, self.player),
            )

        self.multiworld.completion_condition[self.player] = (
            lambda state: state.has("Victory", self.player)
        )

    def fill_slot_data(self) -> Dict[str, object]:
        return {
            "goal": self.options.goal.current_key,
            "trap_percentage": self.options.trap_percentage.value,
            "submissive_trap_duration": self.options.submissive_trap_duration.value,
            "death_link": bool(self.options.death_link.value),
            "scene_goal_required_count": 158,
            "scene_goal_excluded_prefixes": ["Bates"],
        }
