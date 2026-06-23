using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using OSH;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OhSoHeroArchipelago;

[BepInPlugin(
    PluginGuid,
    PluginName,
    PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "ohsohero.archipelago";
    public const string PluginName = "Oh So Hero Archipelago";
    public const string PluginVersion = "0.16.8";

    internal static ManualLogSource Log { get; private set; }
    internal static ArchipelagoClient Client { get; private set; }
    internal static Plugin Instance { get; private set; }

    private ConfigEntry<string> _server;
    private ConfigEntry<string> _slot;
    private ConfigEntry<string> _password;
    private ConfigEntry<bool> _autoConnect;
    private ConfigEntry<string> _clientUuid;

    private Harmony _harmony;
    private string _routeLogPath;

    private void Awake()
    {
        Instance = this;
        Log = Logger;
        _server = Config.Bind("Archipelago", "Server", "localhost:38281",
            "Archipelago server address.");
        _slot = Config.Bind("Archipelago", "Slot", string.Empty,
            "Archipelago player slot name.");
        _password = Config.Bind("Archipelago", "Password", string.Empty,
            "Archipelago server password.");
        _autoConnect = Config.Bind("Archipelago", "AutoConnect", true,
            "Connect automatically when a slot name is configured.");
        _clientUuid = Config.Bind("Archipelago", "ClientUuid",
            Guid.NewGuid().ToString("N"), "Stable Archipelago client UUID.");

        Client = new ArchipelagoClient(
            _server.Value,
            _slot.Value,
            _password.Value,
            _clientUuid.Value,
            _autoConnect.Value);
        _harmony = new Harmony(PluginGuid);
        try
        {
            _harmony.PatchAll();
        }
        catch (Exception exception)
        {
            Logger.LogError($"Harmony patching failed: {exception}");
            throw;
        }
        string outputDirectory = Path.Combine(
            Paths.ConfigPath, "OhSoHeroArchipelago");
        Directory.CreateDirectory(outputDirectory);
        _routeLogPath = Path.Combine(outputDirectory, "scene_route.jsonl");
        AppendRouteEntry("session_start", string.Empty, string.Empty, string.Empty);
        SceneManager.sceneLoaded += OnSceneLoaded;
        Logger.LogInfo($"{PluginName} {PluginVersion} loaded.");

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid())
        {
            StartCoroutine(RecordSceneAfterLoad(activeScene));
        }
    }

    private void Update()
    {
        TrapManager.Update();
        Client?.Update();
        EventFlagSyncManager.Update();
        ProgressionLockManager.Update();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        AppendRouteEntry("session_end", string.Empty, string.Empty, string.Empty);
        Client?.Dispose();
        _harmony?.UnpatchSelf();
        Instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(RecordSceneAfterLoad(scene));
    }

    private IEnumerator RecordSceneAfterLoad(Scene scene)
    {
        yield return null;
        yield return null;

        GameplaySceneController controller =
            FindObjectOfType<GameplaySceneController>();
        string environmentId = controller == null
            ? string.Empty
            : controller.EnvironmentID.ToString();
        string areaFullName = controller == null
            ? string.Empty
            : controller.AreaFullName;

        AppendRouteEntry("scene_loaded", scene.name, environmentId, areaFullName);
        Logger.LogInfo(
            $"Route scene: {scene.name} | Environment: {environmentId} | Area: {areaFullName}");

        if (controller != null)
        {
            ZoneAccessManager.OnGameplayZoneLoaded(controller.EnvironmentID);
            VisitLocationManager.OnGameplayZoneLoaded(controller.EnvironmentID);
        }
    }

    private void AppendRouteEntry(
        string eventName,
        string sceneName,
        string environmentId,
        string areaFullName)
    {
        if (string.IsNullOrEmpty(_routeLogPath))
        {
            return;
        }

        try
        {
            string line =
                $"{{\"time_utc\":\"{DateTime.UtcNow:O}\",\"event\":\"{EscapeJsonValue(eventName)}\",\"scene_name\":\"{EscapeJsonValue(sceneName)}\",\"environment_id\":\"{EscapeJsonValue(environmentId)}\",\"area_full_name\":\"{EscapeJsonValue(areaFullName)}\"}}{Environment.NewLine}";
            File.AppendAllText(_routeLogPath, line);
        }
        catch (Exception exception)
        {
            Logger.LogError($"Could not write route log: {exception.Message}");
        }
    }

    private static string EscapeJsonValue(string value)
    {
        return (value ?? string.Empty)
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");
    }
}

[HarmonyPatch(typeof(OhSoAttacker), "TryAttack")]
internal static class SubmissiveTrapAttackPatch
{
    private static float _lastLockedLustLogTime = -10f;

    [HarmonyPrefix]
    private static bool Prefix(
        OhSoAttacker __instance,
        OhSoAttackAbilityUse __0)
    {
        OhSoCharacter player = OhSoCharacter.FirstPlayerCharacter;
        if (__instance == null || player == null || __instance != player.Attacker)
        {
            return true;
        }

        if (TrapManager.IsAttackBlocked)
        {
            return false;
        }

        if (__0?.attackAbility?.IsLewdAttack == true &&
            Plugin.Client?.HasReceivedItem("LustAttackAbility") != true)
        {
            if (Time.unscaledTime - _lastLockedLustLogTime >= 1f)
            {
                _lastLockedLustLogTime = Time.unscaledTime;
                Plugin.Log.LogInfo("Lust Attack is locked by Archipelago.");
            }
            return false;
        }

        return true;
    }
}

[HarmonyPatch(
    typeof(_0G.InventoryManager),
    nameof(_0G.InventoryManager.AddItemQty),
    new[] { typeof(int), typeof(float), typeof(float) })]
internal static class VanillaRandomizedItemPatch
{
    [HarmonyPrefix]
    private static bool Prefix(int __0)
    {
        if (!ProgressionLockManager.ShouldBlockVanillaItem(__0))
        {
            return true;
        }

        Plugin.Log.LogInfo($"Blocked vanilla randomized item: {(ItemID)__0}");
        return false;
    }
}

[HarmonyPatch(
    typeof(_0G.InventoryManager),
    nameof(_0G.InventoryManager.ToggleSkill),
    new[] { typeof(int), typeof(bool) })]
internal static class VanillaRandomizedSkillPatch
{
    [HarmonyPrefix]
    private static bool Prefix(int __0, bool __1)
    {
        if (!ProgressionLockManager.ShouldBlockVanillaSkill(__0, __1))
        {
            return true;
        }

        Plugin.Log.LogInfo($"Blocked vanilla randomized skill: {(ItemID)__0}");
        return false;
    }
}

[HarmonyPatch(typeof(GameplayHUD), nameof(GameplayHUD.ShowMap))]
internal static class AutoMapGameplayPatch
{
    [HarmonyPrefix]
    private static bool Prefix()
    {
        return Plugin.Client?.HasReceivedItem("AutoMap") == true;
    }
}

[HarmonyPatch(typeof(PauseScreen), "ViewMap")]
internal static class AutoMapPausePatch
{
    [HarmonyPrefix]
    private static bool Prefix()
    {
        return Plugin.Client?.HasReceivedItem("AutoMap") == true;
    }
}

[HarmonyPatch(typeof(OhSoDamageTaker), "OnKnockedOut")]
internal static class BatesAllScenesGoalGuardPatch
{
    private const float ReturnDelaySeconds = 9f;

    [HarmonyPriority(Priority.First)]
    [HarmonyPrefix]
    private static bool Prefix(OhSoDamageTaker __instance)
    {
        _0G.GameObjectBody body = __instance?.Body;
        if (body == null || (CharacterID)body.CharacterID != CharacterID.Bates)
        {
            return true;
        }

        if (Plugin.Client?.IsCollectAllScenesGoalIncomplete() != true)
        {
            return true;
        }

        __instance.HP = Math.Max(__instance.HPMin + 1f, __instance.HPMax);
        Plugin.Log.LogWarning(
            "Bates KO blocked: collect_all_scenes goal is not complete. Punishing and returning player.");
        BatesTrapManager.TryPlay();
        Plugin.Instance?.StartCoroutine(ReturnPlayerAfterPunishment());
        return false;
    }

    private static IEnumerator ReturnPlayerAfterPunishment()
    {
        float endTime = Time.unscaledTime + ReturnDelaySeconds;
        while (Time.unscaledTime < endTime || BatesTrapManager.IsPlaying)
        {
            yield return null;
        }

        OhSoAppManager app = _0G.G.App;
        if (app == null || !app.IsGameplay || app.IsTransitioningScene)
        {
            yield break;
        }

        Plugin.Log.LogWarning(
            "Returning player to LoodCityWestAvenue after blocked Bates victory.");
        app.SetGameplayLevel(EnvironmentID.LoodCityWestAvenue, _0G.AlphaBravo.Alpha);
    }
}

[HarmonyPatch(typeof(OhSoAttacker), "Attack")]
internal static class SubmissiveTrapInputAttackPatch
{
    [HarmonyPrefix]
    private static bool Prefix(
        OhSoAttacker __instance,
        ref OhSoAttack __result)
    {
        OhSoCharacter player = OhSoCharacter.FirstPlayerCharacter;
        if (__instance == null || player == null || __instance != player.Attacker ||
            !TrapManager.IsAttackBlocked)
        {
            return true;
        }

        __result = null;
        TrapManager.LogBlockedAttack();
        return false;
    }
}

[HarmonyPatch(typeof(OhSoDamageTaker), "OnKnockedOut")]
internal static class PlayerKnockedOutDeathLinkPatch
{
    [HarmonyPrefix]
    private static void Prefix(OhSoDamageTaker __instance)
    {
        OhSoCharacter player = OhSoCharacter.FirstPlayerCharacter;
        if (__instance == null || player == null ||
            __instance != player.DamageTaker)
        {
            return;
        }

        if (DeathLinkManager.ConsumeIncomingDeathSuppression())
        {
            Plugin.Log.LogInfo("Incoming DeathLink KO was not echoed.");
            return;
        }

        Plugin.Log.LogWarning("Player KO detected; queueing DeathLink.");
        Plugin.Client?.SendDeathLink();
    }
}

[HarmonyPatch(typeof(OhSoDamageTaker), "OnKnockedOut")]
internal static class EnemyDefeatedLocationPatch
{
    private static readonly HashSet<CharacterID> TrackedEnemies =
        new HashSet<CharacterID>
        {
            CharacterID.Daku,
            CharacterID.Amaru,
            CharacterID.Lonoe,
            CharacterID.Goliath,
            CharacterID.Blowey,
            CharacterID.Gatis,
            CharacterID.ODeere,
            CharacterID.Stier,
            CharacterID.Signal,
            CharacterID.Bax,
            CharacterID.Puca,
            CharacterID.Haya,
            CharacterID.Bates,
        };

    [HarmonyPrefix]
    private static void Prefix(OhSoDamageTaker __instance)
    {
        _0G.GameObjectBody body = __instance?.Body;
        if (body == null || !body.IsEnemyOrBoss)
        {
            return;
        }

        CharacterID characterId = (CharacterID)body.CharacterID;
        if (!TrackedEnemies.Contains(characterId))
        {
            return;
        }

        string locationName = $"Defeat_{characterId}";
        Plugin.Log.LogInfo($"Enemy defeat check detected: {locationName}");
        Plugin.Client?.CompleteLocation(locationName);
    }
}

[HarmonyPatch(
    typeof(OhSoAppManager),
    nameof(OhSoAppManager.SetGameplayLevel),
    new[] { typeof(EnvironmentID), typeof(_0G.AlphaBravo) })]
internal static class GameplayLevelAccessPatch
{
    [HarmonyPrefix]
    private static bool Prefix(EnvironmentID __0)
    {
        return !ZoneAccessManager.ShouldBlock(__0);
    }
}

[HarmonyPatch(
    typeof(OhSoSaveManager),
    nameof(OhSoSaveManager.SetGameEventFlag))]
internal static class GameEventLocationPatch
{
    [HarmonyPostfix]
    private static void Postfix(OhSoGameEventFlag flag, bool value)
    {
        if (!value || _0G.G.App == null || !_0G.G.App.IsGameplay ||
            GameplayHUD.Instance == null)
        {
            return;
        }

        string locationName = flag.ToString();
        bool isDiscussion =
            locationName.Contains("_Talked", StringComparison.Ordinal) &&
            !locationName.EndsWith("_Obsolete", StringComparison.Ordinal);
        bool isGauntlet =
            locationName.Contains("_EnemyGauntlet", StringComparison.Ordinal);

        if (!isDiscussion && !isGauntlet)
        {
            return;
        }

        Plugin.Log.LogInfo($"Game event check detected: {locationName}");
        Plugin.Client?.CompleteLocation(locationName);
    }
}

internal static class EventFlagSyncManager
{
    private static readonly TimeSpan SyncInterval = TimeSpan.FromSeconds(5);
    private static readonly OhSoGameEventFlag[] TrackedFlags =
        Enum.GetValues(typeof(OhSoGameEventFlag))
            .Cast<OhSoGameEventFlag>()
            .Where(IsTrackedFlag)
            .ToArray();
    private static readonly HashSet<OhSoGameEventFlag> SentFlags =
        new HashSet<OhSoGameEventFlag>();
    private static DateTime _nextSyncUtc;

    internal static void Update()
    {
        if (DateTime.UtcNow < _nextSyncUtc)
        {
            return;
        }

        _nextSyncUtc = DateTime.UtcNow + SyncInterval;

        if (Plugin.Client?.HasActiveSlotState != true ||
            _0G.G.App == null || !_0G.G.App.IsGameplay ||
            _0G.G.Save == null)
        {
            return;
        }

        foreach (OhSoGameEventFlag flag in TrackedFlags)
        {
            if (SentFlags.Contains(flag))
            {
                continue;
            }

            bool flagSet;
            try
            {
                flagSet = _0G.G.Save.GetGameEventFlag(flag, false);
            }
            catch (Exception exception)
            {
                Plugin.Log.LogWarning(
                    $"Could not read game event flag {flag}: {exception.Message}");
                continue;
            }

            if (!flagSet)
            {
                continue;
            }

            string locationName = flag.ToString();
            SentFlags.Add(flag);
            Plugin.Log.LogInfo(
                $"Existing game event check synced: {locationName}");
            Plugin.Client?.CompleteLocation(locationName);
        }
    }

    private static bool IsTrackedFlag(OhSoGameEventFlag flag)
    {
        string name = flag.ToString();
        bool isDiscussion =
            name.Contains("_Talked", StringComparison.Ordinal) &&
            !name.EndsWith("_Obsolete", StringComparison.Ordinal);
        bool isGauntlet =
            name.Contains("_EnemyGauntlet", StringComparison.Ordinal);
        return isDiscussion || isGauntlet;
    }
}

internal static class VisitLocationManager
{
    internal static void OnGameplayZoneLoaded(EnvironmentID environmentID)
    {
        if (Plugin.Client?.HasActiveSlotState != true)
        {
            return;
        }

        switch (environmentID)
        {
            case EnvironmentID.DojoSecretBasement:
                Plugin.Log.LogInfo(
                    "Visit check detected: Visit_DojoSecretBasement");
                Plugin.Client?.CompleteLocation("Visit_DojoSecretBasement");
                break;
        }
    }
}

[HarmonyPatch(
    typeof(_0G.InventoryManager),
    nameof(_0G.InventoryManager.AddItemInstanceCollected))]
internal static class ImportantPickupLocationPatch
{
    private static readonly Dictionary<int, string> PickupLocations =
        new Dictionary<int, string>
        {
            [100001] = "Pickup_TreewishForest_SensualBodyPaint_100001",
            [100002] = "Pickup_TreewishForest_SlideAbility_100002",
            [100003] = "Pickup_TreewishForest_MaxHPUp_100003",
            [130001] = "Pickup_BraskJungle_LeatherBelt_130001",
            [130002] = "Pickup_BraskJungle_MaxSPUp_130002",
            [170001] = "Pickup_LoodCityWestAvenue_MaxHPUp_170001",
        };

    [HarmonyPostfix]
    private static void Postfix(int instanceID)
    {
        if (!PickupLocations.TryGetValue(instanceID, out string locationName))
        {
            return;
        }

        Plugin.Log.LogInfo(
            $"Important pickup check detected: {locationName}");
        Plugin.Client?.CompleteLocation(locationName);
    }
}

[HarmonyPatch(typeof(SlidingDoor), "OnTriggerEnter")]
internal static class HirotoDojoKeyDoorLocationPatch
{
    private const string LocationName =
        "TurnIn_HirotoDojo_HirotoDojoKey";

    [HarmonyPostfix]
    private static void Postfix(SlidingDoor __instance)
    {
        if (__instance == null ||
            __instance.RequiredItem != ItemID.HirotoDojoKey ||
            Plugin.Client?.HasActiveSlotState != true ||
            SceneManager.GetActiveScene().name != "HirotoDojo")
        {
            return;
        }

        Plugin.Log.LogInfo(
            $"Hiroto Dojo key door check detected: {LocationName}");
        Plugin.Client?.CompleteLocation(LocationName);
    }
}

[HarmonyPatch(typeof(BuyItem), "OnEnter")]
internal static class UniquePurchaseLocationPatch
{
    private static readonly Dictionary<ItemID, string> PurchaseLocations =
        new Dictionary<ItemID, string>
        {
            [ItemID.Drink1] = "Buy_MirillBar_Drink1",
            [ItemID.Drink2] = "Buy_MirillBar_Drink2",
        };

    [HarmonyPrefix]
    private static void Prefix(BuyItem __instance, out float __state)
    {
        __state = 0f;
        if (__instance == null || _0G.G.Inv == null)
        {
            return;
        }

        ItemID item = Traverse.Create(__instance).Field<ItemID>("item").Value;
        if (!PurchaseLocations.ContainsKey(item))
        {
            return;
        }

        __state = _0G.G.Inv.GetItemQty(item, 0f, false);
    }

    [HarmonyPostfix]
    private static void Postfix(BuyItem __instance, float __state)
    {
        if (__instance == null || _0G.G.Inv == null ||
            Plugin.Client?.HasActiveSlotState != true)
        {
            return;
        }

        ItemID item = Traverse.Create(__instance).Field<ItemID>("item").Value;
        if (!PurchaseLocations.TryGetValue(item, out string locationName))
        {
            return;
        }

        float after = _0G.G.Inv.GetItemQty(item, 0f, false);
        if (after <= __state)
        {
            return;
        }

        Plugin.Log.LogInfo(
            $"Unique purchase check detected: {locationName}");
        Plugin.Client?.CompleteLocation(locationName);
    }
}

[HarmonyPatch(
    typeof(_0G.InventoryManager),
    nameof(_0G.InventoryManager.RemoveItemQty),
    new[] { typeof(ItemID), typeof(float) })]
internal static class RedKeyCardTurnInLocationPatch
{
    private const string LocationName =
        "TurnIn_LoodCityWestAvenue_RedKeyCard";

    [HarmonyPrefix]
    private static void Prefix(
        _0G.InventoryManager __instance,
        ItemID itemID,
        out float __state)
    {
        __state = 0f;
        if (__instance == null || itemID != ItemID.RedKeyCard)
        {
            return;
        }

        __state = __instance.GetItemQty(ItemID.RedKeyCard, 0f, false);
    }

    [HarmonyPostfix]
    private static void Postfix(
        _0G.InventoryManager __instance,
        ItemID itemID,
        float __state)
    {
        if (__instance == null || itemID != ItemID.RedKeyCard ||
            Plugin.Client?.HasActiveSlotState != true)
        {
            return;
        }

        float after = __instance.GetItemQty(ItemID.RedKeyCard, 0f, false);
        if (__state <= after)
        {
            return;
        }

        Plugin.Log.LogInfo(
            $"Red Key Card turn-in check detected: {LocationName}");
        Plugin.Client?.CompleteLocation(LocationName);
    }
}

[HarmonyPatch(
    typeof(ItemData),
    nameof(ItemData.Collect),
    new[] { typeof(Collider), typeof(int) })]
internal static class HirotoDojoKeyPickupLocationPatch
{
    private const string LocationName = "Pickup_HirotoDojo_HirotoDojoKey";

    [HarmonyPostfix]
    private static void Postfix(ItemData __instance)
    {
        if (__instance == null ||
            __instance.ItemID != (int)ItemID.HirotoDojoKey)
        {
            return;
        }

        GameplaySceneController controller =
            UnityEngine.Object.FindObjectOfType<GameplaySceneController>();
        if (controller == null ||
            controller.EnvironmentID != EnvironmentID.HirotoDojo)
        {
            return;
        }

        Plugin.Log.LogInfo(
            $"Hiroto Dojo key pickup check detected: {LocationName}");
        Plugin.Client?.CompleteLocation(LocationName);
    }
}

[HarmonyPatch(
    typeof(ItemData),
    nameof(ItemData.Collect),
    new[] { typeof(Collider), typeof(int) })]
internal static class ZeroInstanceImportantPickupLocationPatch
{
    private const string LocationName =
        "Pickup_TreewishForest_DamageNumbersAbility_0";

    [HarmonyPostfix]
    private static void Postfix(ItemData __instance, int instanceID)
    {
        if (__instance == null || instanceID != 0 ||
            __instance.ItemID != (int)ItemID.DamageNumbersAbility)
        {
            return;
        }

        GameplaySceneController controller =
            UnityEngine.Object.FindObjectOfType<GameplaySceneController>();
        if (controller == null ||
            controller.EnvironmentID != EnvironmentID.TreewishForest)
        {
            return;
        }

        Plugin.Log.LogInfo(
            $"Zero-instance pickup check detected: {LocationName}");
        Plugin.Client?.CompleteLocation(LocationName);
    }
}

[HarmonyPatch(typeof(GalleryScene), "SetupEntityMenu")]
internal static class GalleryCatalogExportPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        try
        {
            var entityNames = new SortedSet<string>(StringComparer.Ordinal);
            var animationNames = new SortedSet<string>(StringComparer.Ordinal);
            var cutsceneNames = new SortedSet<string>(StringComparer.Ordinal);

            if (OhSoGalleryData.EntityNames != null)
            {
                foreach (string entityName in OhSoGalleryData.EntityNames)
                {
                    entityNames.Add(entityName);
                    try
                    {
                        OhSoGalleryData.Entity entity =
                            OhSoGalleryData.GetEntity(entityName);
                        if (entity.IsCharacter)
                        {
                            AddNames(animationNames,
                                OhSoGalleryData.GetCharacterAnimationNames(entityName));
                        }

                        if (entity.IsEnvironment)
                        {
                            AddNames(animationNames,
                                OhSoGalleryData.GetEnvironmentAnimationNames(entityName));
                        }
                    }
                    catch (Exception exception)
                    {
                        Plugin.Log.LogWarning(
                            $"Could not export gallery entity {entityName}: {exception.Message}");
                    }
                }
            }

            var cutsceneField = AccessTools.Field(
                typeof(OhSoGalleryData), "s_AnimationCutscenes");
            if (cutsceneField?.GetValue(null) is IDictionary cutsceneDictionary)
            {
                foreach (object key in cutsceneDictionary.Keys)
                {
                    if (key is string name)
                    {
                        cutsceneNames.Add(name);
                        animationNames.Add(name);
                    }
                }
            }

            string outputDirectory = Path.Combine(
                Paths.ConfigPath, "OhSoHeroArchipelago");
            Directory.CreateDirectory(outputDirectory);

            string outputPath = Path.Combine(
                outputDirectory, "animation_catalog.json");
            File.WriteAllText(outputPath, BuildJson(
                entityNames, animationNames, cutsceneNames));

            Plugin.Log.LogInfo(
                $"Gallery catalog exported: {animationNames.Count} animations to {outputPath}");
        }
        catch (Exception exception)
        {
            Plugin.Log.LogError($"Gallery catalog export failed: {exception}");
        }
    }

    private static void AddNames(ISet<string> destination, IEnumerable<string> names)
    {
        if (names == null)
        {
            return;
        }

        foreach (string name in names.Where(name => !string.IsNullOrEmpty(name)))
        {
            destination.Add(name);
        }
    }

    private static string BuildJson(
        IEnumerable<string> entityNames,
        IEnumerable<string> animationNames,
        IEnumerable<string> cutsceneNames)
    {
        var builder = new StringBuilder();
        builder.AppendLine("{");
        AppendArray(builder, "entities", entityNames, true);
        AppendArray(builder, "animations", animationNames, true);
        AppendArray(builder, "cutscenes", cutsceneNames, false);
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static void AppendArray(
        StringBuilder builder,
        string propertyName,
        IEnumerable<string> values,
        bool appendComma)
    {
        string[] escapedValues = values
            .Select(value => $"    \"{EscapeJson(value)}\"")
            .ToArray();

        builder.AppendLine($"  \"{propertyName}\": [");
        builder.AppendLine(string.Join(",\n", escapedValues));
        builder.Append("  ]");
        builder.AppendLine(appendComma ? "," : string.Empty);
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");
    }
}

internal static class GameplayAnimationLocationDetector
{
    private static readonly Dictionary<string, string> LocationAliases =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Brask/NSP"] = "Brask_JoeLonoe_Sex",
            ["KetSheoIslandsBeachSecret"] = "Ket_SecretBeachBall",
        };

    private static readonly HashSet<string> DetectedGameplayAnimations =
        new HashSet<string>(StringComparer.Ordinal);

    internal static void Record(OhSoRasterAnimation animation)
    {
        if (animation == null ||
            animation.GalleryType != OhSoGalleryType.Recordable)
        {
            return;
        }

        Record(animation.name);
        if (animation.LinkedAnimations == null)
        {
            return;
        }

        foreach (string linkedAnimation in animation.LinkedAnimations)
        {
            Record(linkedAnimation);
        }
    }

    internal static void Record(string animationName)
    {
        if (string.IsNullOrEmpty(animationName) ||
            TrapManager.SuppressLocationChecks ||
            SceneManager.GetActiveScene().name == "GalleryScene")
        {
            return;
        }

        const string rasterSuffix = "_RasterAnimation";
        string normalizedName = animationName.EndsWith(rasterSuffix)
            ? animationName.Substring(0, animationName.Length - rasterSuffix.Length)
            : animationName;
        if (normalizedName.StartsWith("LoadingScreen_", StringComparison.Ordinal))
        {
            return;
        }

        string canonicalName = normalizedName.Replace("_Legacy", "_");
        if (LocationAliases.TryGetValue(canonicalName, out string alias))
        {
            Plugin.Log.LogInfo(
                $"Animation alias applied: {canonicalName} -> {alias}");
            canonicalName = alias;
        }

        if (!DetectedGameplayAnimations.Add(canonicalName))
        {
            return;
        }

        Plugin.Log.LogInfo(
            $"Displayed gameplay animation detected: {canonicalName} | " +
            $"Unity scene: {SceneManager.GetActiveScene().name}");
        Plugin.Client?.CompleteLocation(canonicalName);
    }
}

[HarmonyPatch(
    typeof(OhSoGraphicController),
    nameof(OhSoGraphicController.SetAnimation),
    new[]
    {
        typeof(_0G.AnimationContext),
        typeof(OhSoRasterAnimation),
        typeof(OhSoGraphicController.AnimationEndHandler),
    })]
internal static class DisplayedCharacterAnimationLocationPatch
{
    [HarmonyPrefix]
    private static void Prefix(OhSoRasterAnimation __1)
    {
        GameplayAnimationLocationDetector.Record(__1);
    }
}

[HarmonyPatch(
    typeof(FullscreenAnimation),
    nameof(FullscreenAnimation.Play))]
internal static class DisplayedFullscreenAnimationLocationPatch
{
    [HarmonyPrefix]
    private static void Prefix(string animationName)
    {
        GameplayAnimationLocationDetector.Record(animationName);
    }
}
