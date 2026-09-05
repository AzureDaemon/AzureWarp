using System.Linq;
using GameNetcodeStuff;
using StaticNetcodeLib;
using Unity.Netcode;
using UnityEngine;

namespace AzureWarp;

/// <summary>
/// Host-authoritative warp with an owner-authoritative buildup.
///
/// Flow:
///   press -> TryLocalWarp() -> RequestWarpServerRpc(clientId)          [owner -> host]
///   host validates (teleporter present, off cooldown, not already on ship), starts cooldown:
///     * buildup ON  -> BeginWarpClientRpc(...)                          [host -> all clients]
///                       every client plays the ~3s spin cosmetics; ONLY the warping player's
///                       own client runs the 3s timer and, if still alive, calls
///                       CompleteWarpServerRpc()                          [owner -> host]
///                       -> ExecuteWarpClientRpc(...)                     [host -> all clients]
///     * buildup OFF -> ExecuteWarpClientRpc(...) immediately            [host -> all clients]
///
/// Why owner-authoritative completion: the warping client is the only one that authoritatively
/// knows whether it survived the charge. Deciding there (instead of each client independently
/// re-checking death state, which can desync) guarantees the teleport fires exactly once and
/// atomically on every client — no "some clients moved me, some didn't" divergence.
///
/// Anti-duplication rule: we NEVER Destroy/respawn items and NEVER move GrabbableObjects directly.
/// When keeping items, we simply DON'T drop them — held items ride the (networked) player transform.
/// </summary>
[StaticNetcode]
public static class WarpManager
{
    private const float BuildupSeconds = 3f; // matches the vanilla teleporter's beam-up spin.

    // Single shared cooldown, tracked host-side (host is the only one that runs the ServerRpc bodies).
    private static float _cooldownUntil;

    // --- Inverse teleporter keep-items (host-authoritative, synced to clients) ---
    // The inverse-teleport drop happens locally on each client (game code, not our RPC), so every
    // client must agree on whether to keep items or one keeps while another drops -> desync/dupe.
    // The host broadcasts its setting; clients gate their drop-suppression on the synced value.
    private static bool? _syncedInverseKeepItems;

    /// <summary>
    /// The authoritative "keep items on inverse warp" value for THIS machine.
    /// Host uses its own live config (it IS the authority); clients use the last value synced
    /// from the host, falling back to their own config only in the brief pre-sync window.
    /// </summary>
    public static bool EffectiveInverseKeepItems()
    {
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsServer)
            return Plugin.Cfg.KeepItemsOnInverseWarp.Value;
        return _syncedInverseKeepItems ?? Plugin.Cfg.KeepItemsOnInverseWarp.Value;
    }

    /// <summary>Host -> all clients: push the authoritative inverse keep-items setting. Host-only; safe no-op otherwise.</summary>
    public static void BroadcastInverseKeepItems()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;
        try { SyncInverseKeepItemsClientRpc(Plugin.Cfg.KeepItemsOnInverseWarp.Value); }
        catch (System.Exception e) { Plugin.Log.LogWarning($"[InverseWarp] Setting broadcast deferred (network not ready yet): {e.Message}"); }
    }

    [ClientRpc]
    public static void SyncInverseKeepItemsClientRpc(bool keepItems)
    {
        _syncedInverseKeepItems = keepItems;
        Plugin.Log.LogInfo($"[InverseWarp] Host setting synced: KeepItemsOnInverseWarp={keepItems}.");
    }

    // --- Inverse teleporter cooldown override (host-authoritative, synced to clients) ---
    private static bool? _syncedOverrideInverseCooldown;
    private static float? _syncedInverseCooldownSeconds;

    /// <summary>Authoritative (override, seconds) for the inverse teleporter cooldown on THIS machine.</summary>
    public static (bool over, float seconds) EffectiveInverseCooldown()
    {
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsServer)
            return (Plugin.Cfg.OverrideInverseCooldown.Value, Plugin.Cfg.InverseCooldownSeconds.Value);
        return (_syncedOverrideInverseCooldown ?? Plugin.Cfg.OverrideInverseCooldown.Value,
                _syncedInverseCooldownSeconds ?? Plugin.Cfg.InverseCooldownSeconds.Value);
    }

    /// <summary>Host -> all clients: push the authoritative inverse cooldown override. Host-only; safe no-op otherwise.</summary>
    public static void BroadcastInverseCooldown()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;
        try { SyncInverseCooldownClientRpc(Plugin.Cfg.OverrideInverseCooldown.Value, Plugin.Cfg.InverseCooldownSeconds.Value); }
        catch (System.Exception e) { Plugin.Log.LogWarning($"[InverseWarp] Cooldown broadcast deferred (network not ready yet): {e.Message}"); }
    }

    [ClientRpc]
    public static void SyncInverseCooldownClientRpc(bool over, float seconds)
    {
        _syncedOverrideInverseCooldown = over;
        _syncedInverseCooldownSeconds = seconds;
        Plugin.Log.LogInfo($"[InverseWarp] Host cooldown synced: override={over}, seconds={seconds}.");
        // Re-apply to a teleporter that already spawned before this sync arrived.
        InversePatches.ApplyInverseCooldownToActive();
    }

    /// <summary>Called on the pressing client.</summary>
    public static void TryLocalWarp()
    {
        var local = GameNetworkManager.Instance?.localPlayerController;
        if (local == null || !local.isPlayerControlled || local.isPlayerDead) return;
        if (StartOfRound.Instance == null || StartOfRound.Instance.shipIsLeaving) return;

        RequestWarpServerRpc(local.actualClientId);
    }

    [ServerRpc(RequireOwnership = false)]
    public static void RequestWarpServerRpc(ulong clientId)
    {
        var teleporter = FindRegularTeleporter();
        if (teleporter == null) { DenyWarpClientRpc(clientId, "No teleporter on the ship."); return; }

        var now = Time.realtimeSinceStartup;
        if (now < _cooldownUntil)
        {
            var remaining = Mathf.CeilToInt(_cooldownUntil - now);
            DenyWarpClientRpc(clientId, $"Chaos Gate on cooldown ({remaining}s).");
            return;
        }

        var player = FindPlayer(clientId);
        if (player == null) return;
        if (player.isInHangarShipRoom) { DenyWarpClientRpc(clientId, "You're already on the ship."); return; }

        var cd = Plugin.Cfg.UseVanillaCooldown.Value ? 10f : Mathf.Max(0f, Plugin.Cfg.CustomCooldownSeconds.Value);
        _cooldownUntil = now + cd;

        var keepItems = Plugin.Cfg.KeepHeldItems.Value;
        var buildup = Plugin.Cfg.PlayBuildupAnimation.Value;
        var dest = TeleporterPad(teleporter);

        Plugin.Log.LogInfo($"[Host] Warp granted to clientId {clientId} (keepItems={keepItems}, buildup={buildup}, dest={dest}, cooldown={cd}s).");

        if (buildup)
            BeginWarpClientRpc(clientId);            // cosmetic charge on all; owner completes if it survives
        else
            ExecuteWarpClientRpc(clientId, keepItems, dest); // instant flash-warp
    }

    /// <summary>Runs on ALL clients: play the ~3s spin cosmetics. Only the owner runs the survival timer.</summary>
    [ClientRpc]
    public static void BeginWarpClientRpc(ulong clientId)
    {
        var player = FindPlayer(clientId);
        if (player == null) return;
        var tp = FindRegularTeleporter();

        if (tp != null)
        {
            // Ship-local buildup: observers standing near the teleporter hear the spin
            // ("someone's warping in"). NOTE: we deliberately do NOT trigger tp.teleporterAnimator
            // here. "Teleport A" belongs to vanilla's OWN button-press animation state machine
            // (driven by PressButtonEffects() on a full A->B->C progression with its own timing).
            // Firing "Teleport A" alone, on our independent 3s RPC timer, left the animator stuck
            // mid-transition -- the pad VFX didn't match our buildup and lingered after the warp.
            // We don't own that state machine's timing, so we don't drive it; SFX below is enough
            // of a cosmetic tell without fighting vanilla's animation graph.
            if (tp.shipTeleporterAudio != null && tp.teleporterSpinSFX != null)
                tp.shipTeleporterAudio.PlayOneShot(tp.teleporterSpinSFX);

            // Player-local buildup: the warping player is usually FAR from the ship, so the
            // ship-local effects above aren't audible to them. Play the spin on their own body so
            // the charge is felt (and gives anyone near them in the field a warning tell too).
            // NOTE: we deliberately do NOT play beamUpParticle here — that green beam is an ARRIVAL
            // effect (played in ExecuteWarpClientRpc). Playing it at charge-start made it fire ~3s
            // early and linger past the teleport. Match vanilla: beam plays once, at arrival.
            if (player.movementAudio != null && tp.teleporterSpinSFX != null)
                player.movementAudio.PlayOneShot(tp.teleporterSpinSFX);
        }

        // The player is NOT locked or protected during the charge — like a .hack return scroll,
        // getting caught mid-warp fizzles it. Only the warping player's own client decides survival.
        var isLocalOwner = player == GameNetworkManager.Instance?.localPlayerController;
        if (isLocalOwner)
            (tp != null ? (MonoBehaviour)tp : Plugin.Instance).StartCoroutine(OwnerChargeThenComplete(player, clientId));
    }

    private static System.Collections.IEnumerator OwnerChargeThenComplete(PlayerControllerB player, ulong clientId)
    {
        yield return new WaitForSeconds(BuildupSeconds);

        // Owner-authoritative fizzle: the Gate closed if you died, left, or the ship is leaving.
        if (player == null || player.isPlayerDead ||
            (StartOfRound.Instance != null && StartOfRound.Instance.shipIsLeaving))
        {
            Plugin.Log.LogInfo($"[Warp] Charge fizzled for clientId {clientId} (died/left/ship leaving during charge).");
            yield break;
        }

        CompleteWarpServerRpc(clientId);
    }

    /// <summary>Owner survived the charge -> host executes the synced move for everyone.</summary>
    [ServerRpc(RequireOwnership = false)]
    public static void CompleteWarpServerRpc(ulong clientId)
    {
        var teleporter = FindRegularTeleporter();
        if (teleporter == null) return;
        var player = FindPlayer(clientId);
        if (player == null || player.isInHangarShipRoom) return;

        ExecuteWarpClientRpc(clientId, Plugin.Cfg.KeepHeldItems.Value, TeleporterPad(teleporter));
    }

    /// <summary>Runs on ALL clients: the beam flash + the actual teleport, atomically.</summary>
    [ClientRpc]
    public static void ExecuteWarpClientRpc(ulong clientId, bool keepItems, Vector3 destination)
    {
        var player = FindPlayer(clientId);
        if (player == null) return;

        var isLocalOwner = player == GameNetworkManager.Instance?.localPlayerController;
        var tp = FindRegularTeleporter();

        // Beam flash: audio + the iconic green player particle + camera shake for the local player.
        if (tp != null && tp.shipTeleporterAudio != null && tp.teleporterBeamUpSFX != null)
            tp.shipTeleporterAudio.PlayOneShot(tp.teleporterBeamUpSFX);
        if (player.beamUpParticle != null)
            player.beamUpParticle.Play();
        if (tp != null && player.movementAudio != null && tp.beamUpPlayerBodySFX != null)
            player.movementAudio.PlayOneShot(tp.beamUpPlayerBodySFX);
        if (isLocalOwner)
            HUDManager.Instance?.ShakeCamera(ScreenShakeType.Big);

        // Anti-duplication: only the owning client drops (networked), and only when NOT keeping items.
        if (!keepItems && isLocalOwner)
            player.DropAllHeldItems();

        // Move on every client (mirrors the vanilla teleporter's synced move).
        player.TeleportPlayer(destination);
        player.isInElevator = true;
        player.isInHangarShipRoom = true;

        Plugin.Log.LogInfo($"[Warp] Executed warp for clientId {clientId} to {destination} (keepItems={keepItems}, localOwner={isLocalOwner}).");

        if (isLocalOwner)
            HUDManager.Instance?.DisplayTip("Chaos Gate", keepItems ? "Warped to the ship." : "Warped to the ship. Items left behind.");
    }

    [ClientRpc]
    public static void DenyWarpClientRpc(ulong clientId, string reason)
    {
        var local = GameNetworkManager.Instance?.localPlayerController;
        if (local == null || local.actualClientId != clientId) return;
        HUDManager.Instance?.DisplayTip("Chaos Gate", reason);
    }


    private static Vector3 TeleporterPad(ShipTeleporter tp)
        => tp.teleporterPosition != null ? tp.teleporterPosition.position : tp.transform.position;

    private static ShipTeleporter? FindRegularTeleporter()
        => UnityEngine.Object.FindObjectsOfType<ShipTeleporter>().FirstOrDefault(t => !t.isInverseTeleporter);

    private static PlayerControllerB? FindPlayer(ulong clientId)
        => StartOfRound.Instance?.allPlayerScripts?.FirstOrDefault(p => p != null && p.actualClientId == clientId);
}
