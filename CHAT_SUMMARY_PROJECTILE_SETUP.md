# Emberwake Projectile Setup Summary

Date: 2026-03-25

## What was implemented in code

### Player fire input
File: Assets/Scripts/PlayerMovement.cs

- Fire key is F.
- Uses trigger name: FireProjectile.
- Uses fire state name: SoulProjectile.
- Fire cooldown is configurable (currently 0.1).
- Added robust checks/fallback for animator trigger and state availability.

### Orb collectible sequence
File: Assets/Scripts/LightOrbCollectible.cs

- Orb collection is driven by Collect trigger.
- Destroy timing logic was adjusted to avoid long delay behavior.
- Current setup supports fixed delay mode for predictable disappearance.

## Animator controller separation (important)

- Player fire animation belongs to Player Animator controller.
- Orb pickup/projectile sequence belongs to Orb controller.

Do not mix fire parameters between them.

## Final known-good Animator setup

### Player Animator (Assets/Player Animator.controller)

Parameters:
- Speed (float)
- isGrounded (bool)
- yVelocity (float)
- isGliding (bool)
- isFlying (bool)
- FireProjectile (trigger)

States:
- Idle
- Running
- Jump
- Glide
- Flight
- SoulProjectile

Transitions:
1) Any State -> SoulProjectile
- Condition: FireProjectile
- Has Exit Time: Off
- Transition Duration: 0
- Can Transition To Self: Off

2) SoulProjectile -> Idle
- Condition: Speed < 0.1
- Has Exit Time: On (or Off if you want immediate cancel)
- Exit Time: 0.95 (if Has Exit Time On)
- Transition Duration: 0.03

3) SoulProjectile -> Running
- Condition: Speed > 0.1
- Has Exit Time: On (or Off if you want immediate cancel)
- Exit Time: 0.95 (if Has Exit Time On)
- Transition Duration: 0.03

### Orb Animator (Assets/Orb.controller)

Parameters:
- Collect (trigger) only

States:
- Soul_Idle
- SoulPickup
- SoulProjectile

Transitions:
1) Soul_Idle -> SoulPickup
- Condition: Collect
- Has Exit Time: Off
- Transition Duration: 0

2) SoulPickup -> SoulProjectile
- No condition
- Has Exit Time: On
- Exit Time: 0.95
- Transition Duration: 0.03

3) SoulProjectile
- No outgoing transition required if script destroys orb

## Clip notes

File: Assets/SoulProjectile.anim

- Loop Time should be Off for one-shot playback.

## Troubleshooting notes from session

- If player appears frozen, check Console Error Pause and turn it off.
- Click Game view in Play mode to ensure keyboard focus.
- If Animator graph throws edge wake-up/null errors, delete and recreate only the affected transitions.
- If F does not animate, check Player Animator highlight, not Orb Animator highlight.

## To revisit later

- Adjust whether SoulProjectile exits immediately or near end:
  - Immediate cancel style: set Has Exit Time Off on SoulProjectile -> Idle/Running.
  - Full cast style: keep Has Exit Time On with Exit Time around 0.95.
