using System;
using System.Collections;
using System.Collections.Generic;
using Archipelago.MultiClient.Net.Models;
using OSH;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OhSoHeroArchipelago;

internal static class ItemApplier
{
    private const string ZonePrefix = "Access_";

    private static readonly Dictionary<string, ItemID> GameItems =
        new Dictionary<string, ItemID>(StringComparer.Ordinal)
        {
            ["Zenni"] = ItemID.Zenni,
            ["Zenni3Pack"] = ItemID.Zenni3Pack,
            ["DamageNumbersAbility"] = ItemID.DamageNumbersAbility,
            ["SlideAbility"] = ItemID.SlideAbility,
            ["ButtStompAbility"] = ItemID.ButtStompAbility,
            ["DodgeRollAbility"] = ItemID.DodgeRollAbility,
            ["FapOnCommandAbility"] = ItemID.FapOnCommandAbility,
            ["ThrowMovesAbility"] = ItemID.ThrowMovesAbility,
            ["SensualBodyPaint"] = ItemID.SensualBodyPaint,
            ["LeatherBelt"] = ItemID.LeatherBelt,
            ["StaminaUpLevel1"] = ItemID.StaminaUpLevel1,
            ["TripleDamage"] = ItemID.TripleDamage,
            ["LazurliteMirror"] = ItemID.LazurliteMirror,
            ["HirotoDojoKey"] = ItemID.HirotoDojoKey,
            ["RedKeyCard"] = ItemID.RedKeyCard,
            ["MaxHPUp"] = ItemID.MaxHPUp,
            ["MaxSPUp"] = ItemID.MaxSPUp,
            ["AutoMap"] = ItemID.AutoMap,
            ["Medkit"] = ItemID.Medkit,
            ["Chillpill"] = ItemID.Chillpill,
            ["CharmSexLoot"] = ItemID.CharmSexLoot,
            ["PrimarySexLoot"] = ItemID.PrimarySexLoot,
            ["Drink1"] = ItemID.Drink1,
            ["Drink2"] = ItemID.Drink2,
            ["CharmSexBossLoot"] = ItemID.CharmSexBossLoot,
            ["LootGemHP"] = ItemID.LootGemHP,
            ["LootGemLP"] = ItemID.LootGemLP,
            ["LootGemSP"] = ItemID.LootGemSP,
            ["LootGemOV"] = ItemID.LootGemOV,
            ["IsadoraCrateLoot"] = ItemID.IsadoraCrateLoot,
        };

    internal static bool IsApplyingArchipelagoItem { get; private set; }

    internal static bool TryApply(ItemInfo item, SlotState state)
    {
        string name = item.ItemName;
        if (string.IsNullOrEmpty(name))
        {
            Plugin.Log.LogError("Received an AP item without a name.");
            return true;
        }

        state.ReceivedItems.Add(name);

        if (name.StartsWith(ZonePrefix, StringComparison.Ordinal))
        {
            state.UnlockedZones.Add(name.Substring(ZonePrefix.Length));
            return true;
        }

        if (name == "Nothing" || name == "OhSoSnack" ||
            name == "LustAttackAbility")
        {
            return true;
        }

        if (name.EndsWith("Trap", StringComparison.Ordinal))
        {
            return TrapManager.TryApply(name);
        }

        if (!GameItems.TryGetValue(name, out ItemID itemId))
        {
            Plugin.Log.LogWarning($"Unknown AP item ignored: {name}");
            return true;
        }

        _0G.InventoryManager inventory = _0G.G.Inv;
        if (inventory == null)
        {
            return false;
        }

        if (ShouldDelayWhilePlayerKnockedOut(itemId))
        {
            return false;
        }

        try
        {
            IsApplyingArchipelagoItem = true;
            int numericId = (int)itemId;
            if (numericId >= 100 && numericId <= 105)
            {
                if (inventory.GetItemQty(itemId, 0f, false) < 1f)
                {
                    inventory.SetItemQty(itemId, 1f);
                }
                inventory.ToggleSkill(numericId, true);
            }
            else
            {
                inventory.AddItemQty(itemId, 1f, 0f);
            }

            return true;
        }
        catch (Exception exception)
        {
            Plugin.Log.LogError(
                $"Could not apply AP item {name}: {exception.Message}");
            return false;
        }
        finally
        {
            IsApplyingArchipelagoItem = false;
        }
    }

    private static bool ShouldDelayWhilePlayerKnockedOut(ItemID itemId)
    {
        OhSoCharacter player = OhSoCharacter.FirstPlayerCharacter;
        if (player?.DamageTaker?.IsKnockedOut != true)
        {
            return false;
        }

        switch (itemId)
        {
            case ItemID.MaxHPUp:
            case ItemID.MaxSPUp:
            case ItemID.Medkit:
            case ItemID.Chillpill:
            case ItemID.LootGemHP:
            case ItemID.LootGemSP:
            case ItemID.LootGemLP:
            case ItemID.LootGemOV:
                Plugin.Log.LogInfo(
                    $"Delaying AP item while player is KO: {itemId}");
                return true;
            default:
                return false;
        }
    }
}

internal static class ProgressionLockManager
{
    private static readonly HashSet<int> RandomizedVanillaItems =
        new HashSet<int>
        {
            (int)ItemID.DamageNumbersAbility,
            (int)ItemID.SlideAbility,
            (int)ItemID.ButtStompAbility,
            (int)ItemID.DodgeRollAbility,
            (int)ItemID.FapOnCommandAbility,
            (int)ItemID.ThrowMovesAbility,
            (int)ItemID.SensualBodyPaint,
            (int)ItemID.LeatherBelt,
            (int)ItemID.StaminaUpLevel1,
            (int)ItemID.TripleDamage,
            (int)ItemID.LazurliteMirror,
            (int)ItemID.HirotoDojoKey,
            (int)ItemID.RedKeyCard,
            (int)ItemID.MaxHPUp,
            (int)ItemID.MaxSPUp,
            (int)ItemID.AutoMap,
        };

    private static readonly Dictionary<string, ItemID> LockedSkills =
        new Dictionary<string, ItemID>(StringComparer.Ordinal)
        {
            ["DamageNumbersAbility"] = ItemID.DamageNumbersAbility,
            ["SlideAbility"] = ItemID.SlideAbility,
            ["ButtStompAbility"] = ItemID.ButtStompAbility,
            ["DodgeRollAbility"] = ItemID.DodgeRollAbility,
            ["FapOnCommandAbility"] = ItemID.FapOnCommandAbility,
            ["ThrowMovesAbility"] = ItemID.ThrowMovesAbility,
        };

    private static float _nextEnforcementTime;
    private static readonly object AutoMapVisibilityLock = new object();
    private static GameplayHUD _lockedHud;

    internal static bool ShouldBlockVanillaItem(int itemId)
    {
        return Plugin.Client?.HasActiveSlotState == true &&
            !ItemApplier.IsApplyingArchipelagoItem &&
            RandomizedVanillaItems.Contains(itemId);
    }

    internal static bool ShouldBlockVanillaSkill(int itemId, bool enabled)
    {
        if (!enabled || ItemApplier.IsApplyingArchipelagoItem ||
            Plugin.Client?.HasActiveSlotState != true)
        {
            return false;
        }

        foreach (KeyValuePair<string, ItemID> entry in LockedSkills)
        {
            if ((int)entry.Value == itemId)
            {
                return !Plugin.Client.HasReceivedItem(entry.Key);
            }
        }

        return false;
    }

    internal static void Update()
    {
        if (Time.unscaledTime < _nextEnforcementTime)
        {
            return;
        }

        _nextEnforcementTime = Time.unscaledTime + 0.5f;
        ArchipelagoClient client = Plugin.Client;
        _0G.InventoryManager inventory = _0G.G.Inv;
        if (client?.HasActiveSlotState != true || inventory == null)
        {
            return;
        }

        UpdateAutoMapVisibility(client);

        foreach (KeyValuePair<string, ItemID> entry in LockedSkills)
        {
            bool received = client.HasReceivedItem(entry.Key);
            float quantity = inventory.GetItemQty(entry.Value, 0f, false);
            bool enabled = inventory.HasSkillEnabled(entry.Value);
            if (received && quantity < 1f)
            {
                inventory.SetItemQty(entry.Value, 1f);
                inventory.ToggleSkill((int)entry.Value, true);
                Plugin.Log.LogInfo(
                    $"Restored received AP skill: {entry.Key}");
            }
            else if (!received && enabled)
            {
                inventory.ToggleSkill((int)entry.Value, false);
                Plugin.Log.LogInfo($"Locked skill until received: {entry.Key}");
            }
        }

        if (!client.HasReceivedItem("AutoMap") &&
            inventory.GetItemQty(ItemID.AutoMap, 0f, false) > 0f)
        {
            inventory.SetItemQty(ItemID.AutoMap, 0f);
            Plugin.Log.LogInfo("Locked item until received: AutoMap");
        }
    }

    private static void UpdateAutoMapVisibility(ArchipelagoClient client)
    {
        GameplayHUD hud = GameplayHUD.Instance;
        if (hud == null)
        {
            _lockedHud = null;
            return;
        }

        if (!client.HasReceivedItem("AutoMap"))
        {
            if (_lockedHud != hud)
            {
                hud.LockMiniMapVisibility(AutoMapVisibilityLock);
                _lockedHud = hud;
                Plugin.Log.LogInfo("Mini map hidden until AutoMap is received.");
            }
            return;
        }

        if (_lockedHud == hud)
        {
            hud.UnlockMiniMapVisibility(AutoMapVisibilityLock);
            _lockedHud = null;
            Plugin.Log.LogInfo("Mini map unlocked by AutoMap.");
        }
    }
}

internal static class TrapManager
{
    private const float SubmissiveDurationSeconds = 10f;
    private static float _attackBlockedUntil;
    private static float _lastBlockedAttackLogTime = -10f;

    internal static bool IsAttackBlocked =>
        Time.unscaledTime < _attackBlockedUntil;

    internal static bool SuppressLocationChecks => BatesTrapManager.IsPlaying;

    internal static void Update()
    {
        // This method intentionally keeps trap timers on Unity's main thread.
    }

    internal static void LogBlockedAttack()
    {
        if (Time.unscaledTime - _lastBlockedAttackLogTime < 0.5f)
        {
            return;
        }

        _lastBlockedAttackLogTime = Time.unscaledTime;
        Plugin.Log.LogInfo("SubmissiveTrap blocked a player attack.");
    }

    internal static bool TryApply(string name)
    {
        OhSoCharacter player = OhSoCharacter.FirstPlayerCharacter;
        OhSoAppManager app = _0G.G.App;
        string sceneName = SceneManager.GetActiveScene().name;
        if (app == null || !app.IsGameplay || app.IsTransitioningScene ||
            sceneName == "LoadingScreen" || GameplayHUD.Instance == null ||
            player == null || !player.isActiveAndEnabled)
        {
            return false;
        }

        switch (name)
        {
            case "FullLustTrap":
                return ApplyFullLust(player);

            case "SubmissiveTrap":
                ApplySubmissive();
                return true;

            case "BatesAttackTrap":
                return BatesTrapManager.TryPlay();

            default:
                Plugin.Log.LogWarning($"Unknown trap ignored: {name}");
                return true;
        }
    }

    private static bool ApplyFullLust(OhSoCharacter player)
    {
        OhSoDamageTaker damageTaker = player.DamageTaker;
        if (damageTaker == null)
        {
            return false;
        }

        damageTaker.LP = damageTaker.LPMax;
        Plugin.Log.LogWarning(
            $"FullLustTrap applied: LP={damageTaker.LP}/{damageTaker.LPMax}");
        return true;
    }

    private static void ApplySubmissive()
    {
        float start = Math.Max(Time.unscaledTime, _attackBlockedUntil);
        _attackBlockedUntil = start + SubmissiveDurationSeconds;
        Plugin.Log.LogWarning(
            $"SubmissiveTrap applied for {SubmissiveDurationSeconds:0} seconds.");
    }
}

internal static class BatesTrapManager
{
    private const float TrapDurationSeconds = 8f;
    private static readonly string[] AnimationNames =
    {
        "Bates_Joe_CSE",
        "Bates_Joe_CSP",
        "Bates_Joe_CSP2",
        "Bates_Joe_PSE",
        "Bates_Joe_PSP",
        "Bates_Joe_PSP2",
        "Bates_Joe_Sex_Dive",
        "Bates_Joe_Sex_High",
        "Bates_Joe_Sex_Low",
    };

    internal static bool IsPlaying { get; private set; }

    internal static bool TryPlay()
    {
        if (IsPlaying)
        {
            return false;
        }

        Plugin plugin = Plugin.Instance;
        OhSoCharacter player = OhSoCharacter.FirstPlayerCharacter;
        OhSoAppManager app = _0G.G.App;
        OhSoSexManager sexManager = _0G.G.Sex;
        string sceneName = SceneManager.GetActiveScene().name;
        if (plugin == null || app == null || !app.IsGameplay ||
            app.IsTransitioningScene || sceneName == "LoadingScreen" ||
            GameplayHUD.Instance == null ||
            player == null || !player.isActiveAndEnabled ||
            player.GraphicController == null || _0G.G.Obj == null)
        {
            return false;
        }

        OhSoCharacterStateMachine playerState = player.State;
        if ((sexManager != null && sexManager.IsSexActive) ||
            (playerState != null &&
                (playerState.isCharmSex ||
                 playerState.isPrimarySex ||
                 playerState.isSexInitiator)))
        {
            return false;
        }

        string animationName = AnimationNames[
            UnityEngine.Random.Range(0, AnimationNames.Length)];
        IsPlaying = true;
        plugin.StartCoroutine(PlayCoroutine(player, animationName));
        return true;
    }

    private static IEnumerator PlayCoroutine(
        OhSoCharacter player,
        string animationName)
    {
        string assetName = animationName + "_RasterAnimation";
        object inputLockOwner = typeof(BatesTrapManager);
        bool inputLocked = false;

        Plugin.Log.LogWarning(
            $"BatesAttackTrap started: {animationName}");

        try
        {
            _0G.CharacterDossier dossier = null;
            yield return _0G.G.Obj.LoadDocketAsync<_0G.CharacterDossier>(
                (int)CharacterID.Bates,
                loadedDossier => dossier = loadedDossier);

            if (dossier == null)
            {
                Plugin.Log.LogError("Bates character dossier is unavailable.");
                yield break;
            }

            yield return _0G.G.Obj.AddAnimationAsync(
                dossier,
                assetName,
                _0G.G.Obj.AccessForGameplay);

            if (!_0G.G.Obj.RasterAnimations.TryGetValue(
                assetName,
                out OhSoRasterAnimation animation))
            {
                Plugin.Log.LogError(
                    $"Bates animation was not loaded: {assetName}");
                yield break;
            }

            if (player == null || player.GraphicController == null)
            {
                Plugin.Log.LogWarning(
                    "BatesAttackTrap cancelled because the player changed during loading.");
                yield break;
            }

            player.InputLocker.AddLock(
                inputLockOwner,
                new _0G.Locker.Options());
            inputLocked = true;
            player.GraphicController.SetAnimation(
                _0G.AnimationContext.Priority,
                animation,
                null);

            float endTime = Time.unscaledTime + TrapDurationSeconds;
            while (Time.unscaledTime < endTime)
            {
                yield return null;
            }

            if (player != null && player.GraphicController != null)
            {
                player.GraphicController.EndAnimation(
                    _0G.AnimationContext.Priority);
            }

            Plugin.Log.LogWarning("BatesAttackTrap ended.");
        }
        finally
        {
            if (inputLocked && player != null)
            {
                player.InputLocker.RemoveLock(
                    inputLockOwner,
                    new _0G.Locker.Options());
            }

            IsPlaying = false;
        }
    }
}
