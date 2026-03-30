using UnityEngine;

public class ProjectileMover2D : MonoBehaviour
{
    [Header("Despawn Animation")]
    [SerializeField] private LayerMask surfaceLayers = ~0;
    [SerializeField] private bool ignoreTriggerContacts = true;
    [SerializeField] private float impactArmDelay = 0.06f;
    [SerializeField] private string impactStateName = "SoulExplode";
    [SerializeField] private string dissipateStateName = "SoulDissipate";
    [SerializeField] private float fallbackDestroyDelay = 0.15f;

    [Header("Visual Facing")]
    [SerializeField] private bool flipSpriteByDirection = true;
    [SerializeField] private bool rightDirectionIsFlipXFalse = true;

    [Header("Launch Smoothing")]
    [SerializeField] private float launchRampDuration = 0.03f;

    private Vector2 direction = Vector2.right;
    private float speed = 8f;
    private float lifetime = 2f;
    private float spawnedAt;
    private bool initialized;
    private bool despawnStarted;
    private float destroyAt;
    private float impactChecksStartAt;
    private float launchStartedAt;

    private Rigidbody2D rb;
    private Animator animator;
    private Collider2D projectileCollider;
    private SpriteRenderer projectileSprite;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        projectileCollider = GetComponent<Collider2D>();
        projectileSprite = GetComponent<SpriteRenderer>();
        spawnedAt = Time.time;
        destroyAt = -1f;
    }

    public void Initialize(Vector2 moveDirection, float moveSpeed, float lifeSeconds, Collider2D ownerCollider = null)
    {
        direction = moveDirection.sqrMagnitude > 0.0001f ? moveDirection.normalized : Vector2.right;
        speed = Mathf.Max(0f, moveSpeed);
        lifetime = Mathf.Max(0.01f, lifeSeconds);
        spawnedAt = Time.time;
        initialized = true;
        despawnStarted = false;
        destroyAt = -1f;
        impactChecksStartAt = Time.time + Mathf.Max(0f, impactArmDelay);
        launchStartedAt = Time.time;

        if (projectileCollider != null)
        {
            projectileCollider.enabled = true;

            if (ownerCollider != null)
            {
                Collider2D[] ownerColliders = ownerCollider.transform.root.GetComponentsInChildren<Collider2D>(true);
                foreach (Collider2D ownerPart in ownerColliders)
                {
                    if (ownerPart != null)
                        Physics2D.IgnoreCollision(projectileCollider, ownerPart, true);
                }
            }
        }

        if (rb != null)
        {
            rb.simulated = true;
            rb.linearVelocity = direction * (speed * GetLaunchSpeedMultiplier());
        }

        ApplyVisualFacing();
    }

    private void Update()
    {
        if (despawnStarted)
        {
            if (Time.time >= destroyAt)
                Destroy(gameObject);

            return;
        }

        if (!initialized)
            return;

        if (rb == null)
            transform.position += (Vector3)(direction * (speed * GetLaunchSpeedMultiplier()) * Time.deltaTime);

        if (Time.time - spawnedAt >= lifetime)
            StartDespawn(isImpact: false);
    }

    private void FixedUpdate()
    {
        if (!initialized || despawnStarted || rb == null)
            return;

        rb.linearVelocity = direction * (speed * GetLaunchSpeedMultiplier());
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision == null)
            return;

        TryImpact(collision.collider);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryImpact(other);
    }

    private void TryImpact(Collider2D other)
    {
        if (!initialized || despawnStarted || other == null)
            return;

        if (Time.time < impactChecksStartAt)
            return;

        // Never treat the shooter/player hierarchy as an impact surface.
        if (other.GetComponentInParent<PlayerMovement>() != null)
            return;

        if (ignoreTriggerContacts && other.isTrigger)
            return;

        int otherLayerMask = 1 << other.gameObject.layer;
        if ((surfaceLayers.value & otherLayerMask) == 0)
            return;

        StartDespawn(isImpact: true);
    }

    private void StartDespawn(bool isImpact)
    {
        if (despawnStarted)
            return;

        despawnStarted = true;
        initialized = false;

        if (projectileCollider != null)
            projectileCollider.enabled = false;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.simulated = false;
        }

        float delay = Mathf.Max(0.02f, fallbackDestroyDelay);

        if (!TryPlayState(isImpact ? impactStateName : dissipateStateName, ref delay) && isImpact)
            TryPlayState(dissipateStateName, ref delay);

        destroyAt = Time.time + Mathf.Max(0.02f, delay);
    }

    private bool TryPlayState(string stateName, ref float delay)
    {
        if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrWhiteSpace(stateName))
            return false;

        int shortHash = Animator.StringToHash(stateName);
        int baseLayerHash = Animator.StringToHash("Base Layer." + stateName);

        if (!animator.HasState(0, shortHash) && !animator.HasState(0, baseLayerHash))
            return false;

        animator.Play(stateName, 0, 0f);
        delay = GetClipLengthByName(stateName);
        return true;
    }

    private float GetClipLengthByName(string clipName)
    {
        if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrWhiteSpace(clipName))
            return Mathf.Max(0.02f, fallbackDestroyDelay);

        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;

        foreach (AnimationClip clip in clips)
        {
            if (string.Equals(clip.name, clipName, System.StringComparison.OrdinalIgnoreCase))
                return Mathf.Max(0.02f, clip.length);
        }

        foreach (AnimationClip clip in clips)
        {
            if (clip.name.ToLowerInvariant().Contains(clipName.ToLowerInvariant()))
                return Mathf.Max(0.02f, clip.length);
        }

        return Mathf.Max(0.02f, fallbackDestroyDelay);
    }

    private void ApplyVisualFacing()
    {
        if (!flipSpriteByDirection || projectileSprite == null)
            return;

        bool movingLeft = direction.x < 0f;
        projectileSprite.flipX = rightDirectionIsFlipXFalse ? movingLeft : !movingLeft;
    }

    private float GetLaunchSpeedMultiplier()
    {
        if (launchRampDuration <= 0f)
            return 1f;

        float t = Mathf.Clamp01((Time.time - launchStartedAt) / launchRampDuration);
        return Mathf.SmoothStep(0f, 1f, t);
    }
}
