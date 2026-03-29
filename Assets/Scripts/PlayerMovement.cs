using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 6f;
    public float jumpForce = 12f;
    public float glideFallSpeed = -2f;
    public float glideHoldTime = 0.16f;
    public float flightForce = 5f;
    public float flightBoost = 1.5f;

    public float flightHoldTime = 0.2f;

    public float acceleration = 20f;
    public float deceleration = 25f;
    public float airControlMultiplier = 0.5f;

    [Header("Projectile")]
    public float fireCooldown = 0.1f;
    public string fireTriggerName = "FireProjectile";
    public string fireStateName = "Throw";
    public bool hasOrb;
    public string hasOrbParameterName = "HasOrb";
    public float throwConsumeDelay = 0.18f;
    [Range(0f, 1f)] public float throwSpawnNormalizedTime = 0.8f;
    public float throwSpawnMaxDelay = 0.75f;

    [Header("Thrown Projectile")]
    public GameObject thrownProjectilePrefab;
    public Transform projectileSpawnPoint;
    public Vector2 projectileSpawnOffset = new Vector2(0.6f, 0.1f);
    public float projectileSpeed = 10f;
    public float projectileLifetime = 1.4f;
    public bool spawnProjectileAtThrowStart = false;

    [Header("Debug")]
    public bool showInputDebug = true;

    public Transform groundCheck;
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayer;

    private Rigidbody2D rb;
    private Animator animator;
    private SpriteRenderer spriteRenderer;

    private float moveInput;
    private bool isGrounded;
    private bool isGliding;
    private bool isFlying;

    private float spaceHoldTimer;
    private float airHoldTimer;
    private bool jumpPressedThisFrame;
    private bool jumpHeld;
    private bool wantsFlight;
    private bool hasFlightTriggeredThisHold;
    private float fireCooldownTimer;
    private int fireTriggerHash;
    private int hasOrbParameterHash;
    private bool throwInProgress;
    private bool throwFinalizePending;
    private bool projectileSpawnedThisThrow;
    private float throwFinalizeAtUnscaledTime;
    private float throwFinalizeTimeoutAtUnscaledTime;
    private string lastFireDebugMessage = "none";

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        fireTriggerHash = Animator.StringToHash(fireTriggerName);
        hasOrbParameterHash = Animator.StringToHash(hasOrbParameterName);

        if (rb == null)
        {
            Debug.LogError("PlayerMovement requires a Rigidbody2D component.");
            enabled = false;
            return;
        }

        if (groundCheck == null)
        {
            Debug.LogWarning("PlayerMovement groundCheck is not assigned. Grounded checks will fail.");
        }

        if (animator == null)
        {
            Debug.LogWarning("PlayerMovement Animator not found. Animation parameters will not update.");
        }
        else if (animator.runtimeAnimatorController == null)
        {
            Debug.LogWarning("PlayerMovement Animator has no controller assigned. Assign Player Animator.controller on the Animator component.");
        }

        if (spriteRenderer == null)
        {
            Debug.LogWarning("PlayerMovement SpriteRenderer not found. Sprite flipping will not update.");
        }

        SetAnimatorHasOrb(hasOrb);
    }

    void Update()
    {
        if (fireCooldownTimer > 0f)
            fireCooldownTimer -= Time.deltaTime;

        var keyboard = Keyboard.current;

        bool leftPressed;
        bool rightPressed;
        bool jumpIsHeld;
        bool jumpPressedNow;
        bool firePressedNow;

        if (keyboard != null)
        {
            leftPressed = keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed;
            rightPressed = keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed;
            jumpIsHeld = keyboard.spaceKey.isPressed;
            jumpPressedNow = keyboard.spaceKey.wasPressedThisFrame;
            firePressedNow = keyboard.fKey.wasPressedThisFrame;
        }
        else
        {
            // Fallback when Input System keyboard is unavailable or disabled.
            leftPressed = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
            rightPressed = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);
            jumpIsHeld = Input.GetKey(KeyCode.Space);
            jumpPressedNow = Input.GetKeyDown(KeyCode.Space);
            firePressedNow = Input.GetKeyDown(KeyCode.F);
        }

        // Movement input
        moveInput = (rightPressed ? 1f : 0f) - (leftPressed ? 1f : 0f);

        // Ground check
        isGrounded = groundCheck != null && Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        // SPACE HOLD TIMER
        jumpHeld = jumpIsHeld;
        jumpPressedThisFrame |= jumpPressedNow;

        if (jumpHeld)
        {
            spaceHoldTimer += Time.deltaTime;
            if (!isGrounded)
                airHoldTimer += Time.deltaTime;
        }
        else
        {
            spaceHoldTimer = 0;
            airHoldTimer = 0;
            hasFlightTriggeredThisHold = false;
        }

        if (isGrounded)
        {
            airHoldTimer = 0;
        }

        wantsFlight = jumpHeld &&
                      isGrounded &&
                      !hasFlightTriggeredThisHold &&
                      spaceHoldTimer > flightHoldTime;

        // GLIDE (in air)
        isGliding = jumpHeld &&
                    !isGrounded &&
                    airHoldTimer >= glideHoldTime &&
                    rb.linearVelocity.y < 0;

        // Keep flight active while rising so animator doesn't fall back to idle mid-air.
        bool isAscending = rb.linearVelocity.y > 0.05f;
        isFlying = wantsFlight || (!isGrounded && !isGliding && isAscending);

        // Sprite flipping
        if (spriteRenderer != null)
        {
            if (moveInput > 0)
                spriteRenderer.flipX = false;
            else if (moveInput < 0)
                spriteRenderer.flipX = true;
        }

        bool firePressedThisFrame = firePressedNow;
        if (firePressedThisFrame && throwInProgress)
        {
            lastFireDebugMessage = "Throw in progress";
        }
        else if (firePressedThisFrame && !hasOrb)
        {
            lastFireDebugMessage = "F pressed but no orb";
        }
        else if (firePressedThisFrame && fireCooldownTimer <= 0f)
        {
            bool throwTriggered = TriggerProjectileAnimation();
            if (throwTriggered)
            {
                throwInProgress = true;
                projectileSpawnedThisThrow = false;
                if (spawnProjectileAtThrowStart)
                    projectileSpawnedThisThrow = SpawnThrownProjectile();

                throwFinalizePending = true;
                throwFinalizeAtUnscaledTime = Time.unscaledTime + Mathf.Max(0.01f, throwConsumeDelay);
                throwFinalizeTimeoutAtUnscaledTime = Time.unscaledTime + Mathf.Max(throwSpawnMaxDelay, throwConsumeDelay + 0.05f);
                fireCooldownTimer = fireCooldown;
                lastFireDebugMessage = "Throw started";
            }
        }
        else if (firePressedThisFrame)
        {
            lastFireDebugMessage = "F pressed but cooldown active";
        }

        UpdateThrowLifecycle();

        UpdateAnimator();
    }

    void FixedUpdate()
    {
        float targetSpeed = moveInput * moveSpeed;
        float control = isGrounded ? 1f : airControlMultiplier;

        float currentX = rb.linearVelocity.x;
        float currentY = rb.linearVelocity.y;

        float accelLerp = 1f - Mathf.Exp(-acceleration * Mathf.Max(0f, control) * Time.fixedDeltaTime);
        float decelLerp = 1f - Mathf.Exp(-deceleration * Time.fixedDeltaTime);

        float newX;
        if (Mathf.Abs(moveInput) > 0.01f)
        {
            newX = Mathf.Lerp(currentX, targetSpeed, accelLerp);
        }
        else
        {
            newX = Mathf.Lerp(currentX, 0f, decelLerp);
        }

        float newY = currentY;

        if (wantsFlight)
        {
            newX = moveInput * moveSpeed * flightBoost;
            newY = flightForce;
            hasFlightTriggeredThisHold = true;
            jumpPressedThisFrame = false;
        }
        else if (jumpPressedThisFrame && isGrounded)
        {
            newY = jumpForce;
            jumpPressedThisFrame = false;
        }

        if (isGliding)
        {
            newY = glideFallSpeed;
        }

        rb.linearVelocity = new Vector2(newX, newY);
    }

    private void UpdateAnimator()
    {
        if (animator == null || rb == null)
            return;

        // Only drive run speed while grounded to avoid run animation popping during flight/glide.
        float groundedSpeed = isGrounded ? Mathf.Abs(rb.linearVelocity.x) : 0f;
        bool animatorGrounded = isGrounded && !isFlying && !isGliding;

        animator.SetFloat("Speed", groundedSpeed);
        animator.SetBool("isGrounded", animatorGrounded);
        animator.SetFloat("yVelocity", rb.linearVelocity.y);
        animator.SetBool("isGliding", isGliding);
        animator.SetBool("isFlying", isFlying);
    }

    private bool TriggerProjectileAnimation()
    {
        if (animator == null)
        {
            lastFireDebugMessage = "Animator missing";
            return false;
        }

        if (animator.runtimeAnimatorController == null)
        {
            Debug.LogWarning("PlayerMovement fire ignored because Animator has no controller assigned.");
            lastFireDebugMessage = "Animator controller missing";
            return false;
        }

        string stateToPlay = ResolveConfiguredFireState();

        bool firedByTrigger = TryFireAnimatorTrigger(fireTriggerName) ||
                             TryFireAnimatorTrigger("FireProjectile") ||
                             TryFireAnimatorTrigger("Fire Proje") ||
                             TryFireAnimatorTrigger("Fire Projectile");

        if (!string.IsNullOrWhiteSpace(stateToPlay))
        {
            // Crossfade gives deterministic playback even if transition conditions are misconfigured.
            animator.CrossFadeInFixedTime(stateToPlay, 0.02f, 0, 0f);
            lastFireDebugMessage = $"Played fire state: {stateToPlay}";
            return true;
        }

        if (firedByTrigger)
        {
            lastFireDebugMessage = "Trigger set but no fire state found";
            Debug.LogWarning($"PlayerMovement set fire trigger but could not find a fire state. Expected '{fireStateName}' or fallback 'SoulProjectile'.");
            return false;
        }

        Debug.LogWarning($"PlayerMovement fire failed. Missing trigger '{fireTriggerName}' and missing state '{fireStateName}' in Animator.");
        lastFireDebugMessage = "No trigger and no fire state";
        return false;
    }

    private string ResolveConfiguredFireState()
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return null;

        if (HasAnimatorState(fireStateName))
            return fireStateName;

        // Common project naming fallback for the player's throw state.
        if (!string.Equals(fireStateName, "Throw") && HasAnimatorState("Throw"))
            return "Throw";

        if (!string.Equals(fireStateName, "ThrowOrb") && HasAnimatorState("ThrowOrb"))
            return "ThrowOrb";

        if (!string.Equals(fireStateName, "SoulProjectile") && HasAnimatorState("SoulProjectile"))
            return "SoulProjectile";

        return null;
    }

    private bool HasAnimatorState(string stateName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(stateName))
            return false;

        return animator.HasState(0, Animator.StringToHash("Base Layer." + stateName)) ||
               animator.HasState(0, Animator.StringToHash(stateName));
    }

    private bool TryFireAnimatorTrigger(string triggerName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(triggerName))
            return false;

        if (animator.runtimeAnimatorController == null)
            return false;

        foreach (var parameter in animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == triggerName)
            {
                int triggerHash = triggerName == fireTriggerName ? fireTriggerHash : Animator.StringToHash(triggerName);

                // Reset before setting so repeated key presses retrigger reliably.
                animator.ResetTrigger(triggerHash);
                animator.SetTrigger(triggerHash);
                return true;
            }
        }

        return false;
    }

    public void GiveOrb()
    {
        hasOrb = true;
        throwInProgress = false;
        throwFinalizePending = false;
        projectileSpawnedThisThrow = false;
        SetAnimatorHasOrb(hasOrb);
        lastFireDebugMessage = "Orb collected";
    }

    private void UpdateThrowLifecycle()
    {
        if (!throwFinalizePending)
            return;

        if (Time.unscaledTime < throwFinalizeAtUnscaledTime)
            return;

        if (!projectileSpawnedThisThrow)
        {
            bool reachedThrowSpawnMoment = HasReachedThrowSpawnMoment();
            bool timedOutWaiting = Time.unscaledTime >= throwFinalizeTimeoutAtUnscaledTime;

            if (!reachedThrowSpawnMoment && !timedOutWaiting)
                return;

            projectileSpawnedThisThrow = SpawnThrownProjectile();
            if (!projectileSpawnedThisThrow && !timedOutWaiting)
                return;
        }

        throwFinalizePending = false;

        hasOrb = false;
        throwInProgress = false;
        SetAnimatorHasOrb(hasOrb);
        if (projectileSpawnedThisThrow)
            lastFireDebugMessage = "Orb thrown";

        ForceExitFromThrowState();
        projectileSpawnedThisThrow = false;
    }

    private bool HasReachedThrowSpawnMoment()
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return true;

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        float threshold = Mathf.Clamp01(throwSpawnNormalizedTime);

        bool inConfiguredState = stateInfo.IsName(fireStateName) || stateInfo.IsName("Base Layer." + fireStateName);
        bool inThrowState = stateInfo.IsName("Throw") || stateInfo.IsName("Base Layer.Throw");
        bool inThrowOrbState = stateInfo.IsName("ThrowOrb") || stateInfo.IsName("Base Layer.ThrowOrb");

        if (inConfiguredState || inThrowState || inThrowOrbState)
            return stateInfo.normalizedTime >= threshold;

        // If we already left the throw state after min delay, allow spawn.
        return !animator.IsInTransition(0);
    }

    private bool SpawnThrownProjectile()
    {
        if (thrownProjectilePrefab == null)
        {
            Debug.LogWarning("PlayerMovement throw skipped because thrownProjectilePrefab is not assigned.");
            lastFireDebugMessage = "No projectile prefab assigned";
            return false;
        }

        float facing = 1f;
        if (spriteRenderer != null)
            facing = spriteRenderer.flipX ? -1f : 1f;

        Vector3 spawnPosition = projectileSpawnPoint != null
            ? projectileSpawnPoint.position
            : transform.position + new Vector3(projectileSpawnOffset.x * facing, projectileSpawnOffset.y, 0f);

        GameObject projectile = Instantiate(thrownProjectilePrefab, spawnPosition, Quaternion.identity);

        // Projectile prefab can share a controller whose default state is idle.
        // Force the moving projectile animation state when spawned.
        var projectileAnimator = projectile.GetComponent<Animator>();
        if (projectileAnimator != null)
        {
            int projectileStateHash = Animator.StringToHash("SoulProjectile");
            if (projectileAnimator.HasState(0, projectileStateHash) ||
                projectileAnimator.HasState(0, Animator.StringToHash("Base Layer.SoulProjectile")))
            {
                projectileAnimator.Play("SoulProjectile", 0, 0f);
            }
        }

        var mover = projectile.GetComponent<ProjectileMover2D>();
        if (mover == null)
            mover = projectile.AddComponent<ProjectileMover2D>();

        mover.Initialize(new Vector2(facing, 0f), projectileSpeed, projectileLifetime);
        lastFireDebugMessage = "Projectile spawned";
        return true;
    }

    private void ForceExitFromThrowState()
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return;

        string locomotionState = Mathf.Abs(moveInput) > 0.05f ? "Running" : "Idle";
        if (HasAnimatorState(locomotionState))
            animator.CrossFadeInFixedTime(locomotionState, 0.04f, 0, 0f);
    }

    private void SetAnimatorHasOrb(bool value)
    {
        if (animator == null || string.IsNullOrWhiteSpace(hasOrbParameterName))
            return;

        foreach (var parameter in animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Bool && parameter.name == hasOrbParameterName)
            {
                animator.SetBool(hasOrbParameterHash, value);
                return;
            }
        }
    }

    private void OnGUI()
    {
        if (!showInputDebug)
            return;

        string info =
            $"Input Debug\\n" +
            $"Game view focused: {Application.isFocused}\\n" +
            $"Keyboard current: {Keyboard.current != null}\\n" +
            $"Cooldown: {fireCooldownTimer:F2}\\n" +
            $"Has orb: {hasOrb}\\n" +
            $"Throw in progress: {throwInProgress}\\n" +
            $"Throw finalize pending: {throwFinalizePending}\\n" +
            $"Projectile prefab set: {thrownProjectilePrefab != null}\\n" +
            $"Grounded: {isGrounded}\\n" +
            $"Last fire result: {lastFireDebugMessage}";

        GUI.Box(new Rect(10, 10, 340, 205), info);
    }
}