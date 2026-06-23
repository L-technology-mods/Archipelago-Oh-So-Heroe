using System;
using System.Collections.Generic;
using OSH;

namespace OhSoHeroArchipelago;

internal static class ZoneAccessManager
{
    private static readonly HashSet<EnvironmentID> ControlledZones =
        new HashSet<EnvironmentID>
        {
            EnvironmentID.SheoIslandsBeach,
            EnvironmentID.AliSurfShack,
            EnvironmentID.TreewishForest,
            EnvironmentID.SoutheastBeach,
            EnvironmentID.CasHouse,
            EnvironmentID.BraskJungle,
            EnvironmentID.BraskPalace,
            EnvironmentID.ForbiddenBayou,
            EnvironmentID.HirotoDojo,
            EnvironmentID.DojoSecretBasement,
            EnvironmentID.BayouCabin,
            EnvironmentID.DyabalCabin,
            EnvironmentID.SmolBeach,
            EnvironmentID.LoodCityWestAvenue,
            EnvironmentID.OctoSushiPlace,
            EnvironmentID.SuperImportTechAndMore,
            EnvironmentID.LoodCityPark,
        };

    internal static bool ShouldBlock(EnvironmentID destination)
    {
        ArchipelagoClient client = Plugin.Client;
        if (client == null || !client.HasActiveSlotState ||
            !ControlledZones.Contains(destination))
        {
            return false;
        }

        string zoneName = destination.ToString();
        if (client.IsZoneUnlocked(zoneName))
        {
            Plugin.Log.LogInfo($"Zone access allowed: {zoneName}");
            return false;
        }

        Plugin.Log.LogWarning(
            $"Zone access blocked: {zoneName} requires Access_{zoneName}");
        return true;
    }

    internal static void OnGameplayZoneLoaded(EnvironmentID currentZone)
    {
        ArchipelagoClient client = Plugin.Client;
        if (client == null || !client.HasActiveSlotState ||
            !ControlledZones.Contains(currentZone))
        {
            return;
        }

        string currentZoneName = currentZone.ToString();
        if (client.IsZoneUnlocked(currentZoneName))
        {
            client.RememberUnlockedZone(currentZoneName);
            return;
        }

        EnvironmentID fallback = GetFallbackZone(client);
        Plugin.Log.LogWarning(
            $"Loaded locked zone {currentZoneName}; returning to {fallback}.");
        _0G.G.App.SetGameplayLevel(fallback, _0G.AlphaBravo.Alpha);
    }

    private static EnvironmentID GetFallbackZone(ArchipelagoClient client)
    {
        string rememberedZone = client.LastUnlockedZone;
        if (!string.IsNullOrEmpty(rememberedZone) &&
            Enum.TryParse(rememberedZone, out EnvironmentID parsedZone) &&
            ControlledZones.Contains(parsedZone) &&
            client.IsZoneUnlocked(rememberedZone))
        {
            return parsedZone;
        }

        return EnvironmentID.SheoIslandsBeach;
    }
}
