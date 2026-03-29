using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider2D))]
public class LightOrbCollectible : MonoBehaviour
{
    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string collectTriggerName = "Collect";
    [SerializeField] private string finalStateName = "SoulProjectile";
    [SerializeField] private bool waitForFinalState = true;
    [SerializeField] private float maxSequenceWait = 1.2f;
    [SerializeField] private bool useFixedDestroyDelay = true;
    [SerializeField] private float fixedDestroyDelay = 0.45f;
    [SerializeField] private float fallbackDestroyDelay = 0.25f;
    [SerializeField] private bool verboseLogs = true;

    private Collider2D triggerCollider;
    private bool collected;
    private int collectTriggerHash;
    private int finalStateShortHash;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider2D>();
        triggerCollider.isTrigger = true;

        if (animator == null)
            animator = GetComponent<Animator>();

        collectTriggerHash = Animator.StringToHash(collectTriggerName);
        finalStateShortHash = Animator.StringToHash(finalStateName);

        if (verboseLogs)
        {
            var rb = GetComponent<Rigidbody2D>();
            Debug.Log($"[LightOrbCollectible] Ready on '{name}'. Collider isTrigger={triggerCollider.isTrigger}, hasRigidbody={rb != null}, layer={LayerMask.LayerToName(gameObject.layer)}");
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryCollectFromCollider(other, "OnTriggerEnter2D");
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        // Fallback for cases where overlap already existed before gameplay started.
        TryCollectFromCollider(other, "OnTriggerStay2D");
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Fallback when trigger settings are misconfigured in editor.
        if (collision.collider != null)
            TryCollectFromCollider(collision.collider, "OnCollisionEnter2D");
    }

    private void TryCollectFromCollider(Collider2D other, string source)
    {
        if (collected || other == null)
            return;

        PlayerMovement playerMovement = other.GetComponent<PlayerMovement>() ??
                                       other.GetComponentInParent<PlayerMovement>();

        if (verboseLogs)
        {
            var otherHasPlayerMovement = other.GetComponent<PlayerMovement>() != null;
            var parentHasPlayerMovement = other.GetComponentInParent<PlayerMovement>() != null;
            Debug.Log($"[LightOrbCollectible] Contact from '{other.name}' via {source}. tag={other.tag}, layer={LayerMask.LayerToName(other.gameObject.layer)}, hasPlayerMovement={otherHasPlayerMovement}, parentHasPlayerMovement={parentHasPlayerMovement}");
        }

        // Accept either a Player tag or a PlayerMovement component on this collider or its parent.
        bool isPlayer = other.CompareTag("Player") ||
                        playerMovement != null;

        if (!isPlayer)
        {
            if (verboseLogs)
                Debug.Log("[LightOrbCollectible] Contact ignored because collider is not recognized as player.");
            return;
        }

        collected = true;
        triggerCollider.enabled = false;

        if (playerMovement != null)
            playerMovement.GiveOrb();

        if (verboseLogs)
            Debug.Log($"[LightOrbCollectible] Collected by '{other.name}' via {source}.");

        if (animator != null)
        {
            animator.SetTrigger(collectTriggerHash);

            if (useFixedDestroyDelay)
            {
                Destroy(gameObject, Mathf.Max(0.05f, fixedDestroyDelay));
            }
            else
            {
                StartCoroutine(DestroyAfterAnimationSequence());
            }
        }
        else
        {
            if (verboseLogs)
                Debug.LogWarning("[LightOrbCollectible] Animator missing; only destroy delay will run.");

            Destroy(gameObject, fallbackDestroyDelay);
        }
    }

    private IEnumerator DestroyAfterAnimationSequence()
    {
        if (animator == null)
        {
            Destroy(gameObject, fallbackDestroyDelay);
            yield break;
        }

        if (!waitForFinalState || string.IsNullOrWhiteSpace(finalStateName))
        {
            Destroy(gameObject, GetClipLengthByName("projectile"));
            yield break;
        }

        float expectedDuration = GetClipLengthByName("pickup") + GetClipLengthByName("projectile");
        float adaptiveWait = Mathf.Max(0.1f, expectedDuration + 0.1f);
        float timeoutAt = Time.time + Mathf.Min(Mathf.Max(0.1f, maxSequenceWait), adaptiveWait);
        bool enteredFinalState = false;

        while (Time.time < timeoutAt)
        {
            if (animator == null)
                yield break;

            var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            bool isFinalState =
                stateInfo.shortNameHash == finalStateShortHash ||
                stateInfo.IsName(finalStateName) ||
                stateInfo.IsName("Base Layer." + finalStateName);

            if (isFinalState)
            {
                enteredFinalState = true;
                if (stateInfo.normalizedTime >= 1f)
                    break;
            }
            else if (enteredFinalState)
            {
                // Left the target state, so the sequence has finished.
                break;
            }

            yield return null;
        }

        if (verboseLogs && !enteredFinalState)
        {
            Debug.LogWarning($"[LightOrbCollectible] Final state '{finalStateName}' not reached before timeout. Destroying with fallback timing.");
            Destroy(gameObject, Mathf.Max(0.05f, GetClipLengthByName("projectile")));
            yield break;
        }

        Destroy(gameObject);
    }

    private float GetClipLengthByName(string nameContains)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return fallbackDestroyDelay;

        var clips = animator.runtimeAnimatorController.animationClips;
        foreach (var clip in clips)
        {
            if (clip.name.ToLowerInvariant().Contains(nameContains.ToLowerInvariant()))
                return Mathf.Max(clip.length, 0.05f);
        }

        return fallbackDestroyDelay;
    }
}
