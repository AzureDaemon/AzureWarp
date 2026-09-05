# Changelog

## 0.3.0
- **Inverse teleporter cooldown override** (new). The ship's inverse teleporter has a long 210-second vanilla cooldown; you can now shorten it. New config under "Inverse Teleporter": `OverrideInverseCooldown` (default off) + `InverseCooldownSeconds` (default 10, matching the regular teleporter). Host-authoritative — the host's value is synced to the whole lobby. Leaves the regular teleporter untouched.

## 0.2.1
- Fixed the warp beam VFX (and its sound) lingering a couple seconds after you teleport. The green beam particle was being played at the start of the buildup AND at arrival, so it fired early and overran the teleport. It now plays once, at arrival, matching the vanilla teleporter's timing.

## 0.2.0
- **Keep your held items through the INVERSE teleporter** (new). The ship's inverse teleporter normally drops everything you're carrying onto the ship floor — now you can carry it into the facility with you. Host-authoritative (the host's setting is auto-synced to the whole lobby), so nothing duplicates. New config: `KeepItemsOnInverseWarp` (default true, under "Inverse Teleporter"). The regular teleporter is unaffected.

## 0.1.0
- Bind a key to warp yourself to the ship. Rebindable (keyboard + controller) via InputUtils, defaults to F4.
- Keep-held-items on warp (host-authoritative, no duplication).
- Optional ~3s beam-up buildup animation, or instant flash-warp.
- Host-authoritative RPC flow with an owner-authoritative buildup (StaticNetcodeLib); die/leave mid-charge fizzles cleanly with no position desync.
- Shared teleporter cooldown (vanilla 10s or custom). In-game config via LethalConfig.
