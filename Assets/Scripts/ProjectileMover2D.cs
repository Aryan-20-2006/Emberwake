using UnityEngine;

public class ProjectileMover2D : MonoBehaviour
{
    private Vector2 direction = Vector2.right;
    private float speed = 8f;
    private float lifetime = 2f;
    private float spawnedAt;
    private bool initialized;

    private Rigidbody2D rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spawnedAt = Time.time;
    }

    public void Initialize(Vector2 moveDirection, float moveSpeed, float lifeSeconds)
    {
        direction = moveDirection.sqrMagnitude > 0.0001f ? moveDirection.normalized : Vector2.right;
        speed = Mathf.Max(0f, moveSpeed);
        lifetime = Mathf.Max(0.01f, lifeSeconds);
        spawnedAt = Time.time;
        initialized = true;

        if (rb != null)
            rb.linearVelocity = direction * speed;
    }

    private void Update()
    {
        if (!initialized)
            return;

        if (rb == null)
            transform.position += (Vector3)(direction * speed * Time.deltaTime);

        if (Time.time - spawnedAt >= lifetime)
            Destroy(gameObject);
    }

    private void FixedUpdate()
    {
        if (!initialized || rb == null)
            return;

        rb.linearVelocity = direction * speed;
    }
}
