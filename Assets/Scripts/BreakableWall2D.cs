using UnityEngine;

[DisallowMultipleComponent]
public class BreakableWall2D : MonoBehaviour
{
    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string breakTriggerName = "BreakNow";

    [Header("Lifecycle")]
    [SerializeField] private bool disappearAfterBreak = true;
    [SerializeField] private bool disappearOnAnimationEvent = true;
    [SerializeField] private float disappearDelaySeconds = 0.2f;

    [Header("Collision")]
    [SerializeField] private Collider2D intactCollider;
    [SerializeField] private Collider2D brokenGroundCollider;
    [SerializeField] private bool swapToBrokenCollider = false;
    [SerializeField] private bool swapColliderOnAnimationEvent = false;
    [SerializeField] private Collider2D blockingCollider;
    [SerializeField] private bool disableColliderOnBreak = true;

    [Header("Debug")]
    [SerializeField] private bool verboseLogs = false;

    private bool isBroken;
    private int breakTriggerHash;

    public bool IsBroken => isBroken;

    private void Awake()
    {
        ResolveReferences();
        breakTriggerHash = Animator.StringToHash(breakTriggerName.Trim());
    }

    private void OnValidate()
    {
        ResolveReferences();
        breakTriggerHash = Animator.StringToHash((breakTriggerName ?? string.Empty).Trim());
    }

    public bool TryBreak()
    {
        if (isBroken)
            return false;

        isBroken = true;

        if (!swapColliderOnAnimationEvent)
            ApplyPostBreakColliderState();

        bool triggered = TrySetBreakTrigger();

        if (!triggered && verboseLogs)
            Debug.LogWarning($"[BreakableWall2D] Trigger '{breakTriggerName}' not found on '{name}'.");

        if (verboseLogs)
            Debug.Log($"[BreakableWall2D] Wall '{name}' broke.");

        if (disappearAfterBreak && !disappearOnAnimationEvent)
            Destroy(gameObject, Mathf.Max(0f, disappearDelaySeconds));

        return true;
    }

    public void DisableBlockingCollider()
    {
        if (blockingCollider != null)
            blockingCollider.enabled = false;
    }

    // Optional Animation Event hook from the Break clip.
    public void OnBreakVisualSettled()
    {
        ApplyPostBreakColliderState();
    }

    // Optional Animation Event hook from the last frame of the Break clip.
    public void OnBreakAnimationFinished()
    {
        if (!disappearAfterBreak)
            return;

        Destroy(gameObject);
    }

    private bool TrySetBreakTrigger()
    {
        if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrWhiteSpace(breakTriggerName))
            return false;

        string triggerName = breakTriggerName.Trim();

        foreach (var parameter in animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == triggerName)
            {
                animator.SetTrigger(breakTriggerHash);
                return true;
            }
        }

        return false;
    }

    private void ResolveReferences()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (animator == null)
            animator = GetComponentInParent<Animator>();

        if (intactCollider == null)
            intactCollider = GetComponent<Collider2D>();

        if (intactCollider == null)
            intactCollider = GetComponentInChildren<Collider2D>(true);

        if (intactCollider == null)
            intactCollider = GetComponentInParent<Collider2D>();

        if (blockingCollider == null)
            blockingCollider = intactCollider;
    }

    private void ApplyPostBreakColliderState()
    {
        if (swapToBrokenCollider)
        {
            if (intactCollider != null)
                intactCollider.enabled = false;

            if (blockingCollider != null && blockingCollider != intactCollider)
                blockingCollider.enabled = false;

            if (brokenGroundCollider != null)
                brokenGroundCollider.enabled = true;

            return;
        }

        if (disableColliderOnBreak)
            DisableBlockingCollider();

        if (brokenGroundCollider != null)
            brokenGroundCollider.enabled = false;

        if (intactCollider == null)
            return;

        if (!disableColliderOnBreak && !swapToBrokenCollider)
            intactCollider.enabled = true;
    }
}
