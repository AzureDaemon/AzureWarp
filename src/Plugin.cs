using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine.InputSystem;

namespace AzureWarp;

[BepInPlugin(Guid, "AzureWarp", Version)]
[BepInDependency("com.rune580.LethalCompanyInputUtils")]
[BepInDependency("ainavt.lc.lethalconfig")]
[BepInDependency("Xilophor.StaticNetcodeLib")]
public class Plugin : BaseUnityPlugin
{
    public const string Guid = "com.azurecore.azurewarp";
    public const string Version = "0.3.0";

    internal static ManualLogSource Log = null!;
    internal static WarpConfig Cfg = null!;
    internal static WarpKeybinds Keybinds = null!;
    internal static Plugin Instance = null!;

    private void Awake()
    {
        Instance = this;
        Log = Logger;
        Cfg = new WarpConfig(base.Config);
        Keybinds = new WarpKeybinds();
        Keybinds.WarpToShip.performed += OnWarpPressed;

        new Harmony(Guid).PatchAll();
        Log.LogInfo($"AzureWarp {Version} loaded — Chaos Gate online. Press to warp home.");
    }

    private void OnWarpPressed(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;
        WarpManager.TryLocalWarp();
    }
}
