using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using OSH;
using UnityEngine.SceneManagement;

namespace OhSoHeroArchipelago;

internal static class DeathLinkManager
{
    private static bool _suppressNextLocalDeath;

    internal static bool TryApply(DeathLink deathLink)
    {
        OhSoAppManager app = _0G.G.App;
        OhSoCharacter player = OhSoCharacter.FirstPlayerCharacter;
        string sceneName = SceneManager.GetActiveScene().name;

        if (app == null || !app.IsGameplay || app.IsTransitioningScene ||
            sceneName == "LoadingScreen" || GameplayHUD.Instance == null ||
            player == null || !player.isActiveAndEnabled ||
            player.DamageTaker == null || player.DamageTaker.IsKnockedOut)
        {
            return false;
        }

        _suppressNextLocalDeath = true;
        player.DamageTaker.HP = player.DamageTaker.HPMin;
        Plugin.Log.LogWarning(
            $"DeathLink received from {deathLink.Source}: {deathLink.Cause}");
        return true;
    }

    internal static bool ConsumeIncomingDeathSuppression()
    {
        if (!_suppressNextLocalDeath)
        {
            return false;
        }

        _suppressNextLocalDeath = false;
        return true;
    }
}
