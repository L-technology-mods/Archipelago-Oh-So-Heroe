from dataclasses import dataclass

from Options import Choice, PerGameCommonOptions, Range, Toggle


class Goal(Choice):
    """Determines the condition required to complete the game."""

    display_name = "Goal"
    option_defeat_bates = 0
    option_collect_all_scenes = 1
    default = 0


class TrapPercentage(Range):
    """Percentage of otherwise empty filler slots replaced by traps."""

    display_name = "Trap Percentage"
    range_start = 0
    range_end = 100
    default = 10


class SubmissiveTrapDuration(Range):
    """Number of seconds during which Submissive Trap blocks attacks."""

    display_name = "Submissive Trap Duration"
    range_start = 3
    range_end = 30
    default = 10


class DeathLink(Toggle):
    """Share player knockouts with other DeathLink players."""

    display_name = "Death Link"
    default = 0


@dataclass
class OhSoHeroOptions(PerGameCommonOptions):
    goal: Goal
    trap_percentage: TrapPercentage
    submissive_trap_duration: SubmissiveTrapDuration
    death_link: DeathLink
