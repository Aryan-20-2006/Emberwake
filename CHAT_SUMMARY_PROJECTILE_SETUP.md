# Emberwake Projectile Setup Summary

Date: 2026-03-30

## Session outcome

Projectile throw flow is working again with the intended sequence:
1. Player collects orb.
2. Press F.
3. Player plays throw animation.
4. Projectile spawns and moves.
5. Spawned projectile plays SoulProjectile animation (not idle).

## Current key files

1. Assets/Scripts/PlayerMovement.cs
2. Assets/Scripts/ProjectileMover2D.cs
3. Assets/Scripts/LightOrbCollectible.cs
4. Assets/Player Animator.controller
5. Assets/Orb.controller
6. Assets/Prefabs/SoulProjectile.prefab
7. Assets/Scenes/SampleScene.unity

## Important fixes made in this session

### 1) Throw state/clip recovery

- Player Animator throw state name in use: Throw.
- Throw state clip is now bound to Throw.anim.
- A previously missing throw clip GUID reference was replaced with a valid clip.

### 2) Projectile spawn timing behavior

- Spawn-at-start behavior is disabled.
- Spawn now waits for throw progression and then releases projectile.
- Added timing fields in PlayerMovement to tune release timing:
  - throwConsumeDelay
  - throwSpawnNormalizedTime
  - throwSpawnMaxDelay

### 3) Projectile animation state on spawned prefab

- Spawned projectile was showing idle animation due to shared controller defaults.
- On spawn, code now forces projectile Animator to play SoulProjectile state.

### 4) Input robustness

- Fire/move/jump input now supports Input System keyboard and legacy fallback.
- This avoids F key appearing dead when Keyboard.current is unavailable.

## Current known-good serialized values

### In SampleScene PlayerMovement

- fireTriggerName: FireProjectile
- fireStateName: Throw
- thrownProjectilePrefab: Assets/Prefabs/SoulProjectile.prefab
- spawnProjectileAtThrowStart: 0
- throwConsumeDelay: 0.12
- throwSpawnNormalizedTime: 0.62
- throwSpawnMaxDelay: 0.75

### Animator snappiness tuning applied

- Throw state speed: 1.5
- FireProjectile+HasOrb transition into Throw duration: 0.08

## Animator/controller notes

### Player Animator controller

- Uses FireProjectile trigger.
- Uses HasOrb bool condition for entering Throw.
- Includes state names: Throw, Idle, Running, Jump, Glide, Flight, SoulProjectile.

### Orb controller

- Handles orb visual states (Soul_Idle, SoulPickup, SoulProjectile).
- Used by orb visual and by projectile prefab Animator currently.

## Prefab notes

### SoulProjectile prefab

- Has SpriteRenderer, Rigidbody2D, Collider2D, ProjectileMover2D.
- Animator component exists and uses Orb controller.
- Projectile movement is driven by ProjectileMover2D.Initialize at spawn.

## Troubleshooting checklist for next session

1. If F does nothing:
  - Confirm Game view focus.
  - Check PlayerMovement debug text: Last fire result.
  - Confirm hasOrb true after pickup.
2. If throw anim does not show:
  - Verify Player Animator Throw state still points to Throw.anim.
  - Verify fireStateName is Throw in scene component.
3. If projectile appears but is idle animation:
  - Ensure spawn code still forces SoulProjectile state on spawned Animator.
4. If projectile does not spawn:
  - Verify thrownProjectilePrefab reference is not null.
  - Verify throw timing values are not excessively high.

## Suggested next improvements (optional)

1. Move projectile spawn to an Animation Event in Throw.anim for exact frame-locked release.
2. Split projectile into its own dedicated Animator controller to avoid orb idle state coupling.
3. Remove now-unneeded fallback logic once final animator setup is fully stable.
