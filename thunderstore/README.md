# AzureWarp

**Portable Chaos Gates for Lethal Company.** Bind a key and warp yourself back to the ship through the ship's teleporter — without losing your loot.

A clean-room, multiplayer-first replacement for older "teleport key" mods.

## Features
- **Rebindable key + controller support** (via LethalCompanyInputUtils). Defaults to F4.
- **Keep your held items** when you warp (on by default) — your items move with you; nothing is duplicated or dropped.
- **Keep your items through the inverse teleporter too** (new in 0.2.0) — the ship's inverse teleporter no longer drops your gear on the floor; carry it into the facility with you.
- **Multiplayer-safe by design** — every warp is host-authoritative, so it won't cause the item-ownership desyncs that plague client-side teleport hacks.
- **Respects the teleporter** — no teleporter bought, no warp. Shares the teleporter's cooldown (configurable).
- **In-game config** via LethalConfig.

## Config
- `KeepHeldItems` (default true) — keep items when warping to the ship.
- `PlayBuildupAnimation` (default true) — ~3s teleporter beam-up charge before the warp, or instant flash if off.
- `UseVanillaCooldown` / `CustomCooldownSeconds` — warp cooldown.
- `KeepItemsOnInverseWarp` (default true) — keep items when using the ship's inverse teleporter. Host-authoritative: the host's value applies to everyone.
- `OverrideInverseCooldown` (default false) + `InverseCooldownSeconds` (default 10) — shorten the inverse teleporter's long 210s vanilla cooldown. Turn the override on and set the seconds (10 matches the regular teleporter). Host-authoritative.

## Notes
Warping *to* the ship (the main feature) uses the regular teleporter. The inverse-teleporter option (0.2.0) only changes whether it drops your items — it doesn't add a new inverse-warp keybind. All gameplay-affecting settings are host-authoritative, so in multiplayer the host's values apply to the whole lobby.

---
By **AzureCore**. Built the right way: our own code, no reuploads.
