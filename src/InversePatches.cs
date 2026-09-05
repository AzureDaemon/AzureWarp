using GameNetcodeStuff;
using HarmonyLib;
using Unity.Netcode;

namespace AzureWarp;

/// <summary>
/// Keep held items when using the ship's INVERSE teleporter (the one that sends you INTO the facility).
///
/// Vanilla behaviour: <c>ShipTeleporter.TeleportPlayerOutWithInverseTeleporter</c> calls
/// <c>PlayerControllerB.DropAllHeldItems(true, false, false, false, zero, zero, zero, zero, zero)</c>
/// PURELY to drop your gear. Verified from the v81 IL: the player-state flags
/// (isInElevator / isInHangarShipRoom / isInsideFactory) and the actual <c>TeleportPlayer</c> move are
/// done separately in the method body — NOT inside DropAllHeldItems. So suppressing only that one
/// drop call keeps the items on the player and leaves the teleport fully intact.
///
/// We scope the suppression with a guard flag that is live ONLY for the duration of that single
/// (synchronous) method, so no other drop — death, manual Q-drop, or the regular teleporter — is
/// ever touched. A finalizer clears the flag even if the game method throws.
///
/// Host-authoritative: the drop runs locally on each client, so we gate on the host's synced value
/// (<see cref="WarpManager.EffectiveInverseKeepItems"/>). If clients disagreed, one keeping while
/// another dropped would desync and could duplicate the item.
///
/// Anti-duplication: we never Destroy/respawn or reparent items — we simply DON'T drop them, so the
/// held instances ride the (networked) player transform into the facility. Same principle as the
/// regular AzureWarp warp.
/// </summary>
[HarmonyPatch]
internal static class InversePatches
{
    /// <summary>True only while inside TeleportPlayerOutWithInverseTeleporter with keep-items in effect.</summary>
    private static bool _suppressDrop;

    private static bool _connectHooked;

    // --- Drop suppression, scoped to the inverse-teleport method ---

    [HarmonyPatch(typeof(ShipTeleporter), "TeleportPlayerOutWithInverseTeleporter")]
    [HarmonyPrefix]
    private static void InverseWarp_Prefix()
    {
        _suppressDrop = WarpManager.EffectiveInverseKeepItems();
    }

    // Finalizer: guarantees the flag clears even if the game method throws mid-way.
    [HarmonyPatch(typeof(ShipTeleporter), "TeleportPlayerOutWithInverseTeleporter")]
    [HarmonyFinalizer]
    private static void InverseWarp_Finalizer()
    {
        _suppressDrop = false;
    }

    [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.DropAllHeldItems))]
    [HarmonyPrefix]
    private static bool DropAllHeldItems_Prefix()
    {
        if (_suppressDrop)
        {
            Plugin.Log.LogInfo("[InverseWarp] Kept held items through the inverse teleporter (drop suppressed).");
            return false; // skip the vanilla drop; items ride the player into the facility
        }
        return true; // normal drop for every other caller
    }

    // --- Inverse teleporter cooldown override ---

    // Vanilla ShipTeleporter.Awake does `cooldownTime = cooldownAmount` (inverse = 210s). We set the
    // inverse teleporter's cooldownAmount to the configured value (public field), and clamp the live
    // cooldownTime (private) so the initial/current cooldown reflects the new value too. cooldownAmount
    // is re-read into cooldownTime on every use (PressTeleportButtonClientRpc), so subsequent cooldowns
    // are correct automatically.
    [HarmonyPatch(typeof(ShipTeleporter), "Awake")]
    [HarmonyPostfix]
    private static void ShipTeleporter_Awake_Postfix(ShipTeleporter __instance)
    {
        if (__instance != null && __instance.isInverseTeleporter)
            ApplyInverseCooldown(__instance);
    }

    /// <summary>Apply the configured cooldown to one inverse teleporter. No-op if override is off. Host-authoritative value.</summary>
    internal static void ApplyInverseCooldown(ShipTeleporter tp)
    {
        if (tp == null || !tp.isInverseTeleporter) return;
        var (over, seconds) = WarpManager.EffectiveInverseCooldown();
        if (!over) return;

        tp.cooldownAmount = seconds;
        // Clamp the live countdown (private field) so a longer initial cooldown isn't left hanging.
        var ct = Traverse.Create(tp).Field("cooldownTime");
        if (ct.FieldExists() && ct.GetValue<float>() > seconds)
            ct.SetValue(seconds);

        Plugin.Log.LogInfo($"[InverseWarp] Inverse teleporter cooldown set to {seconds}s.");
    }

    /// <summary>Re-apply to any currently-spawned inverse teleporter (used when the host's value syncs after Awake ran).</summary>
    internal static void ApplyInverseCooldownToActive()
    {
        foreach (var tp in UnityEngine.Object.FindObjectsOfType<ShipTeleporter>())
            ApplyInverseCooldown(tp);
    }

    // --- Host-authoritative setting sync ---

    // On the host, broadcast the current settings when the session's StartOfRound comes up, and
    // subscribe once so late-joining clients get them the moment they connect. Clients apply the
    // synced values; the host always uses its own live config.
    [HarmonyPatch(typeof(StartOfRound), "Start")]
    [HarmonyPostfix]
    private static void StartOfRound_Start_Postfix()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;

        WarpManager.BroadcastInverseKeepItems();
        WarpManager.BroadcastInverseCooldown();

        if (!_connectHooked)
        {
            _connectHooked = true;
            nm.OnClientConnectedCallback += _ =>
            {
                WarpManager.BroadcastInverseKeepItems();
                WarpManager.BroadcastInverseCooldown();
            };
        }
    }
}
