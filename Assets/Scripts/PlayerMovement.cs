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
    public string fireStateName = "SoulProjectile";

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
    private string lastFireDebugMessage = "none";

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        fireTriggerHash = Animator.StringToHash(fireTriggerName);

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
    }

    void Update()
    {
        if (fireCooldownTimer > 0f)
            fireCooldownTimer -= Time.deltaTime;

        if (Keyboard.current == null)
        {
            moveInput = 0f;
            jumpHeld = false;
            jumpPressedThisFrame = false;
            wantsFlight = false;
            isFlying = false;
            isGliding = false;

            UpdateAnimator();
            return;
        }

        var keyboard = Keyboard.current;

        // Movement input
        bool leftPressed = keyboard.aKey.isPressed;
        bool rightPressed = keyboard.dKey.isPressed;
        moveInput = (rightPressed ? 1f : 0f) - (leftPressed ? 1f : 0f);

        // Ground check
        isGrounded = groundCheck != null && Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        // SPACE HOLD TIMER
        jumpHeld = keyboard.spaceKey.isPressed;
        jumpPressedThisFrame |= keyboard.spaceKey.wasPressedThisFrame;

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

        bool firePressedThisFrame = keyboard.fKey.wasPressedThisFrame;
        if (firePressedThisFrame && fireCooldownTimer <= 0f)
        {
            TriggerProjectileAnimation();
            fireCooldownTimer = fireCooldown;
        }
        else if (firePressedThisFrame)
        {
            lastFireDebugMessage = "F pressed but cooldown active";
        }

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

    private void TriggerProjectileAnimation()
    {
        if (animator == null)
        {
            lastFireDebugMessage = "Animator missing";
            return;
        }

        if (animator.runtimeAnimatorController == null)
        {
            Debug.LogWarning("PlayerMovement fire ignored because Animator has no controller assigned.");
            lastFireDebugMessage = "Animator controller missing";
            return;
        }

        bool firedByTrigger = TryFireAnimatorTrigger(fireTriggerName) ||
                             TryFireAnimatorTrigger("FireProjectile") ||
                             TryFireAnimatorTrigger("Fire Proje") ||
                             TryFireAnimatorTrigger("Fire Projectile");

        if (firedByTrigger)
        {
            lastFireDebugMessage = "Triggered fire parameter";
            return;
        }

        bool hasConfiguredState = animator.HasState(0, Animator.StringToHash("Base Layer." + fireStateName)) ||
                                  animator.HasState(0, Animator.StringToHash(fireStateName));

        if (hasConfiguredState)
        {
            // Safe fallback when a trigger parameter is missing or misnamed.
            animator.CrossFadeInFixedTime(fireStateName, 0.02f, 0, 0f);
            lastFireDebugMessage = "CrossFaded to fire state";
            return;
        }

        Debug.LogWarning($"PlayerMovement fire failed. Missing trigger '{fireTriggerName}' and missing state '{fireStateName}' in Animator.");
        lastFireDebugMessage = "No trigger and no fire state";
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

    private void OnGUI()
    {
        if (!showInputDebug)
            return;

        string info =
            $"Input Debug\\n" +
            $"Game view focused: {Application.isFocused}\\n" +
            $"Keyboard current: {Keyboard.current != null}\\n" +
            $"Cooldown: {fireCooldownTimer:F2}\\n" +
            $"Grounded: {isGrounded}\\n" +
            $"Last fire result: {lastFireDebugMessage}";

        GUI.Box(new Rect(10, 10, 320, 130), info);
    }
}