using BepInEx.Configuration;
using LethalConfig;
using LethalConfig.ConfigItems;

namespace AzureWarp;

/// <summary>
/// Config, surfaced in the in-game LethalConfig menu.
/// NOTE: gameplay-affecting values (KeepHeldItems, cooldown) are HOST-authoritative —
/// the host's values are what actually apply during a session (see WarpManager). The
/// keybind itself is a per-client preference.
/// </summary>
public class WarpConfig
{
    public readonly ConfigEntry<bool> KeepHeldItems;
    public readonly ConfigEntry<bool> PlayBuildupAnimation;
    public readonly ConfigEntry<bool> UseVanillaCooldown;
    public readonly ConfigEntry<float> CustomCooldownSeconds;
    public readonly ConfigEntry<bool> KeepItemsOnInverseWarp;
    public readonly ConfigEntry<bool> OverrideInverseCooldown;
    public readonly ConfigEntry<float> InverseCooldownSeconds;

    public WarpConfig(ConfigFile cfg)
    {
        KeepHeldItems = cfg.Bind(
            "General", "KeepHeldItems", true,
            "Keep your held items when you warp to the ship. Server-authoritative: your items move with you rather than being dropped, so nothing duplicates.");

        PlayBuildupAnimation = cfg.Bind(
            "General", "PlayBuildupAnimation", true,
            "Play the ship teleporter's ~3 second beam-up buildup before warping (spin-up sound + beam animation), just like the vanilla teleporter. You are NOT protected during the charge \u2014 like a .hack return scroll, you can be caught mid-warp. Turn OFF for an instant beam flash.");

        UseVanillaCooldown = cfg.Bind(
            "Cooldown", "UseVanillaCooldown", true,
            "Use the ship teleporter's standard 10s cooldown for warps.");

        CustomCooldownSeconds = cfg.Bind(
            "Cooldown", "CustomCooldownSeconds", 10f,
            "Cooldown (seconds) used only when UseVanillaCooldown is false.");

        KeepItemsOnInverseWarp = cfg.Bind(
            "Inverse Teleporter", "KeepItemsOnInverseWarp", true,
            "When you use the ship's INVERSE teleporter (the one that sends you INTO the facility), keep your held items instead of dropping them on the ship floor. HOST-AUTHORITATIVE: the host's value is auto-synced to everyone in the lobby, so items never duplicate. Does not touch the regular teleporter.");

        OverrideInverseCooldown = cfg.Bind(
            "Inverse Teleporter", "OverrideInverseCooldown", false,
            "Override the ship's INVERSE teleporter cooldown. Vanilla is 210 seconds. Turn this ON to use InverseCooldownSeconds instead. HOST-AUTHORITATIVE (the host's value applies to the whole lobby).");

        InverseCooldownSeconds = cfg.Bind(
            "Inverse Teleporter", "InverseCooldownSeconds", 10f,
            "Inverse teleporter cooldown in seconds, used only when OverrideInverseCooldown is ON. Set to 10 to match the regular teleporter. (Vanilla default is 210.)");

        // All values are read LIVE at warp time (host-authoritative), never cached at load,
        // so none require a restart — pass requiresRestart:false so the in-game toggle applies immediately.
        LethalConfigManager.AddConfigItem(new BoolCheckBoxConfigItem(KeepHeldItems, false));
        LethalConfigManager.AddConfigItem(new BoolCheckBoxConfigItem(PlayBuildupAnimation, false));
        LethalConfigManager.AddConfigItem(new BoolCheckBoxConfigItem(UseVanillaCooldown, false));
        LethalConfigManager.AddConfigItem(new FloatInputFieldConfigItem(CustomCooldownSeconds, false));
        LethalConfigManager.AddConfigItem(new BoolCheckBoxConfigItem(KeepItemsOnInverseWarp, false));
        LethalConfigManager.AddConfigItem(new BoolCheckBoxConfigItem(OverrideInverseCooldown, false));
        LethalConfigManager.AddConfigItem(new FloatInputFieldConfigItem(InverseCooldownSeconds, false));
    }
}
