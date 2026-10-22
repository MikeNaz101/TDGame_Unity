# Optimization & Polish Pass – Notes

Everything below works with the existing scene and prefabs as-is. The "Optional wiring"
sections list Inspector fields you can hook up to unlock extra HUD/feedback features.

## Bugs fixed

| Problem | Fix |
|---|---|
| Towers never fired. Tower prefabs had no scripts and `TowerData.damage/range` were unused. | New `TowerController`. `TowerManager.BuildTower` adds it at runtime if the prefab lacks one, so Tower 1 / Tower 2 shoot immediately. |
| Queen minions broke wave counting (they decremented the counter without being counted), so waves ended early and could go negative. | Every enemy registers with `WaveManager` through `EnemyController.Initialize`; the wave waits for `EnemiesAlive == 0`. |
| Automatic weapons fired once per click. `Shoot()` cleared `inputScript.fire`, and the Fire action only reports press/release. | Removed the consume. Semi-auto still uses edge detection. |
| Sound investigation never worked: `SetDestination(target)` ran every frame and overwrote the sound destination. | Real `Investigating` state; enemy walks to the gunshot, waits `patrolWaitTime`, then returns to the Core. |
| "Last known player position" was the live player transform, so enemies that saw you once chased forever. | Stored as a `Vector3`; enemies give up after `EnemyData.loseInterestTime` or when they reach the spot. |
| `PatrolRoute.cs` had an unguarded `using UnityEditor;` which breaks player builds. | Wrapped in `#if UNITY_EDITOR`. |
| Reloading continued while building / using the PC and across weapon switches. | Reload is cancelled on disable and on switch. Reserve ammo is now remembered per weapon. |
| Build confirm failing (not enough scrap) closed the menu silently. | Menu stays open, cost text turns red, optional fail sound. |

## Performance

- **Enemy repathing**: `SetDestination` was called every frame per enemy. Now re-paths only when the goal moves > 0.5 m or every 0.25 s. This is the biggest CPU win with 20+ enemies.
- **No per-gunshot garbage**: each gunshot used to allocate a `GameObject` inside every distractible enemy. Replaced with a `Vector3` + flag.
- **Physics queries**: door scan and tower-proximity checks use `NonAlloc` buffers; distance checks use `sqrMagnitude`.
- **Build ghost**: one indicator instance reused (was Instantiate/Destroy per attempt) and colored via `MaterialPropertyBlock` (was leaking a material instance every frame).
- **Weapons**: one raycast per shot instead of two; tracer is a timer, not a coroutine per shot; gun models and reticles are instantiated once and toggled instead of destroyed/recreated on every switch; crosshair `SetActive` only on change.
- **HUD** is event-driven. No string concatenation per frame.
- **Typewriter** uses `maxVisibleCharacters` instead of rebuilding the string per character.
- **Tower targeting** iterates `EnemyController.ActiveEnemies` on a 0.15 s timer; no physics queries.
- Replaced deprecated `FindObjectOfType` with `FindFirstObjectByType`; singletons (`PlayerStats.Instance`, `WaveManager.Instance`, `CoreHealth.Instance`) remove most scene searches.

## New features

- **GameManager** (`Scripts/Core/GameManager.cs`): win/lose state, freezes time, unlocks the cursor, `R` restarts the scene. Core destroyed or player death → Game Over; all waves cleared → Victory. Add it to the `GameManager` object in the scene (it still works without it; the old behaviour is the fallback).
- **Endless mode** on `WaveManager`: `loopWavesWhenFinished` repeats the wave list with scaling health (+25%/loop), count (+20%/loop), and faster spawns. Turn it off to keep a finite level.
- **Wave rewards**: scrap bonus per cleared wave (`waveClearScrapBonus` + per-wave bonus).
- **Health regen** on `PlayerStats` (CoD style): `regenDelay` seconds after last hit, `regenRate` HP/s.
- **Towers**: targeting modes (Closest / First / Strongest / Weakest), turret head rotation, optional line-of-sight, tracer, sound, muzzle/impact effects, `Sell()` with refund. Range gizmo when selected.
- **Scrap pickups**: magnet to the player, spin/bob, auto-despawn after 60 s (set `lifetime` to 0 to disable).
- **Enemy feedback**: hit flash (property block, no material instances), optional hit/death sounds and death effect on `EnemyData`, `scrapDropCount`.
- **Weapon feedback**: optional headshot multiplier (collider name contains "head"), impact effects, empty-click auto-reload, `AddReserveAmmo()` for future ammo pickups.
- **Spawn points** are used round-robin so a wave spreads out instead of clumping.

## Optional wiring (Inspector)

Nothing here is required. Assign what you have:

- **HUDManager** (on the HUD prefab): `ammoText`, `weaponNameText`, `reloadingIndicator`, `hitMarker`, `killMarker`, `waveText`, `waveTimerText`, `enemiesRemainingText`, `announcementText`, `coreHealthSlider`, `coreHealthText`, `damageFlash` (full-screen Image), `healthText`. Existing `scrapText` / `healthSlider` keep working.
- **GameManager**: `gameOverPanel`, `victoryPanel`, `endReasonText`. Hook a UI button to `GameManager.Restart()`.
- **TowerController** (add to tower prefabs if you want): `turretHead` (rotating part), `firePoint`, a `LineRenderer` for the tracer. Without these the tower fires from 1 m above its base and creates its own tracer.
- **TowerData**: `targeting`, `turnSpeed`, `shootSound`, `tracerColor`, `impactEffect`, `sellRefund`.
- **EnemyData**: `loseInterestTime`, `deathEffect`, `deathSound`, `hitSound`, `scrapDropCount`, `corpseLifetime`.
- **WeaponData**: `headshotMultiplier`, `enemyImpactEffect`, `environmentImpactEffect`, `tracerDuration`.
- **PlayerStats**: `hurtSound`, `scrapPickupSound`, `cameraShake` (auto-found), regen settings.
- **BuildManager**: `_groundLayers` (defaults to Default), `buildFailSound`, indicator colors.
- **WaveManager**: endless-mode and reward settings.

## Behaviour changes to be aware of

- Holding the trigger on the Assault Rifle now actually auto-fires.
- Enemies that lose sight of you go back to the Core after ~5 s (`loseInterestTime`).
- Scrap on the ground disappears after 60 s unless the player or the Roomba grabs it.
- Waves loop forever by default (`loopWavesWhenFinished`). Set it to false for a finite level.
- `TowerManager.BuildTower` now returns the `GameObject` (null on failure) instead of `void`.
- `IDamageable` implementers no longer redeclare `transform`; the inherited one satisfies the interface.

## Suggested next steps for the game loop

1. Tower upgrades (TowerData `upgradeTo` chain, interact with E while looking at a tower; `TowerController.Sell()` already exists).
2. Ammo economy: buy ammo at the PC / wall-buys with scrap (`WeaponControllerHS.AddReserveAmmo`).
3. Enemy health bars or damage numbers using `EnemyController.OnAnyEnemyDamaged`.
4. Door repair / barricade rebuilding between waves (`DoorHealth` already exposes events).
5. Object pooling for enemies once wave sizes exceed ~50.
