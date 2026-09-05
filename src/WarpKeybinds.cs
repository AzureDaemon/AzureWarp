using LethalCompanyInputUtils.Api;
using LethalCompanyInputUtils.BindingPathEnums;
using UnityEngine.InputSystem;

namespace AzureWarp;

/// <summary>
/// Rebindable controls (keyboard + controller) via LethalCompanyInputUtils.
/// Default is F4 to match the mod this replaces; fully rebindable in the in-game menu.
/// </summary>
public class WarpKeybinds : LcInputActions
{
    [InputAction(KeyboardControl.F4, Name = "Warp to Ship (Chaos Gate)")]
    public InputAction WarpToShip { get; set; } = null!;
}
