# AzureWarp

**Portable Chaos Gates for Lethal Company.** Bind a key and warp yourself back to the ship through the ship's teleporter — without losing your loot.

A clean-room, multiplayer-first replacement for older "teleport key" mods (built independently — no reuploaded code).

## Features
- Rebindable key + controller support (via LethalCompanyInputUtils). Defaults to F4.
- Keep your held items when you warp (on by default) — items move with you, nothing duplicates or drops.
- Keep your items through the ship's inverse teleporter too — it no longer strips your gear on the way into the facility.
- Optional override for the inverse teleporter's long 210s vanilla cooldown.
- Owner-authoritative ~3s buildup animation (toggleable) — like a .hack return scroll, you can be caught mid-charge and the warp fizzles cleanly with no desync.
- Every warp is host-authoritative by design — avoids the item-ownership desyncs that plague client-side teleport mods.
- In-game config via LethalConfig.

See [`thunderstore/README.md`](thunderstore/README.md) for the player-facing feature/config rundown as published on Thunderstore.

## Building from source

Requires .NET SDK (netstandard2.1 target) and the LethalCompany.GameLibs.Steam NuGet package (pulled automatically via `PackageReference`).

```
dotnet build -c Release
```

### Local dependencies (`libs/`)

Three mod dependencies are referenced by local `HintPath` rather than NuGet, since they're not published as build-time NuGet packages:

- `LethalCompanyInputUtils.dll` (Rune580)
- `LethalConfig.dll` (AinaVT)
- `Xilophor.StaticNetcodeLib.dll` (xilophor)

These are **not included in this repo** (third-party binaries, not ours to redistribute). Before building, copy them into a `libs/` folder at the repo root — easiest source is your own r2modman profile's `BepInEx/plugins/<mod>/` folder, or download the mods directly from Thunderstore and pull the DLL out of the package.

```
AzureWarp/
  libs/
    LethalCompanyInputUtils.dll
    LethalConfig.dll
    Xilophor.StaticNetcodeLib.dll
```

### Packaging for Thunderstore / local import

```
powershell -File build_package.ps1
```

Builds Release, stages `thunderstore/_stage/`, and zips it to `AzureCore-AzureWarp-<version>.zip` at the repo root (ready for r2modman's "Import local mod" or Thunderstore upload).

## Design notes for contributors
- All gameplay-affecting config (keep-items, cooldowns, buildup toggle) is **host-authoritative** — the host's value is synced to clients (`WarpManager`/`InversePatches`). Don't add client-trusted gameplay state; it will desync.
- Anti-duplication rule: never `Destroy`/respawn or reparent held items. Keeping items means *not* calling the game's drop method for that transition — items simply ride the networked player transform.
- Avoid driving vanilla Animator trigger state machines (e.g. the ship teleporter's "Teleport A/B/C") from our own independently-timed coroutines — the animator graph expects the full vanilla-controlled sequence and will stick/linger if only partially triggered. Prefer independent SFX/particle cosmetics we fully own the timing of.

## License / attribution
Built by **AzureCore**. Not affiliated with Zeekerss or the original ShipTeleportKey mod authors (delisted, no license — this is an independent clean-room implementation).
