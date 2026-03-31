# Emberwake Session Summary

Date: 2026-03-31

Update: 2026-04-01

## Session outcome

Projectile behavior was polished and is now working with improved feel:
1. Launches in the player facing direction.
2. Explodes on valid surface hit.
3. Dissipates when no surface is hit before lifetime expiry.
4. Visual orientation mirrors correctly for left/right shots.
5. Launch movement now has a short smooth ramp so the throw feels less abrupt.
6. Spawn feels cleaner across running/jumping due to better fallback spawn positioning.

## Main files updated today

1. Assets/Scripts/ProjectileMover2D.cs
2. Assets/Scripts/PlayerMovement.cs

## What was implemented

### 1) Projectile impact vs timeout despawn flow

In ProjectileMover2D:
- Added impact detection via collision/trigger callbacks.
- Added separate despawn paths:
  - Impact path: plays impact state if available.
  - Timeout path: plays dissipate state if available.
- Added fallback destroy delay when expected animation state is missing.

### 2) Immediate self-collision protection

To prevent instant explode at spawn:
- Projectile ignores owner/player colliders on initialize.
- Added short impact arm delay.
- Added explicit check to ignore colliders in PlayerMovement hierarchy.

### 3) Visual direction polish

In ProjectileMover2D:
- Added sprite flip-by-direction options so visuals look symmetric left/right:
  - flipSpriteByDirection
  - rightDirectionIsFlipXFalse

### 4) Launch smoothness polish

In ProjectileMover2D:
- Added launch speed ramp over a short duration:
  - launchRampDuration
- Uses smoothstep from 0 to full speed for first few frames.

### 5) Better spawn consistency while moving/jumping

In PlayerMovement:
- Added stable facing memory (lastFacingSign).
- Reworked fallback spawn position calculation when projectileSpawnPoint is not assigned:
  - Uses owner collider bounds edge + facing offset.
  - Optional airborne vertical lead from current y velocity.
- Added config fields:
  - useBoundsBasedProjectileSpawn
  - airborneSpawnLeadFactor
  - maxAirborneSpawnLead

## Current important inspector fields

### SoulProjectile prefab (ProjectileMover2D)

- surfaceLayers
- ignoreTriggerContacts
- impactArmDelay (current default in script: 0.06)
- impactStateName (default: SoulExplode)
- dissipateStateName (default: SoulDissipate)
- fallbackDestroyDelay (default: 0.15)
- flipSpriteByDirection (default: true)
- rightDirectionIsFlipXFalse (default: true)
- launchRampDuration (default: 0.03)

### Player in scene (PlayerMovement)

- projectileSpawnPoint (recommended to assign when possible)
- projectileSpawnOffset
- useBoundsBasedProjectileSpawn (default: true)
- airborneSpawnLeadFactor (default: 0.02)
- maxAirborneSpawnLead (default: 0.1)

## Notes about Animator setup

- Current code uses Animator.Play by state name for impact/dissipate.
- Required state names for best behavior:
  - SoulProjectile
  - SoulExplode
  - SoulDissipate
- Transitions are optional for this code path (state names are the key).

## Suggested quick baseline values

- impactArmDelay = 0.06
- launchRampDuration = 0.03
- airborneSpawnLeadFactor = 0.02
- maxAirborneSpawnLead = 0.10

## 2026-04-01 update (wall break obstacles)

### Session outcome

Projectile can now break wall obstacles with animator-driven feedback, and walls can disappear after the break animation finishes.

### Main files updated

1. Assets/Scripts/ProjectileMover2D.cs
2. Assets/Scripts/BreakableWall2D.cs

### What was implemented today

1. Added breakable wall component:
  - New script: BreakableWall2D.
  - Trigger-based animator hook using BreakNow.
  - One-time break guard to prevent repeated trigger spam.

2. Connected projectile impact to breakable walls:
  - Projectile impact now attempts to resolve BreakableWall2D from collider, parent, children, rigidbody, and root hierarchy.
  - If found, TryBreak is called before projectile impact despawn.

3. Collider handling for break event:
  - Supports disabling blocking collider on break.
  - Supports optional intact->broken collider swap.
  - Supports optional animation-event timing for collider swap.

4. Simplified animation flow:
  - Removed hard dependency on a separate Broken state.
  - Recommended setup is Intact -> Break, with Break ending on final frame.

5. Post-break lifecycle:
  - Added optional wall disappearance after break.
  - Supports exact timing via Animation Event method OnBreakAnimationFinished.
  - Supports fallback timed destroy when events are not used.

### Current recommended wall setup

1. Animator states: Intact -> Break.
2. Break clip: Loop Time off.
3. No transition out of Break (hold last frame) unless disappear-on-finish is enabled.
4. BreakNow trigger exists and matches script field exactly.
5. If disappearing after break, add Animation Event on last Break frame:
  - Function: OnBreakAnimationFinished.

## Next work (2026-04-02 target)

Build a fast single-orb collection loop where the same orb is both progress and wall-break currency.

### Target implementation order

1. Orb inventory counters:
  - heldOrbs: spendable currency.
  - totalCollected: lifetime progress count.

2. Wall break cost check:
  - Require heldOrbs >= breakCost (start at 1).
  - Spend heldOrbs when wall break succeeds.

3. Level target logic:
  - Win condition uses totalCollected >= target.
  - Spending should not reduce completion progress.

4. HUD pass:
  - Show Held Orbs.
  - Show Progress totalCollected/target.

5. Level design pass:
  - Place walls as orb sinks/obstacles.
  - Hide some orbs behind breakable walls.
  - Ensure at least one completable resource path.
