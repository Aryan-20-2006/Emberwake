using UnityEngine;

public class CameraFollow2D : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Follow")]
    public Vector3 offset = new Vector3(0f, 1f, -10f);
    [Range(1f, 20f)] public float smoothSpeed = 10f;
    public bool followX = true;
    public bool followY = true;

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector3 desiredPosition = target.position + offset;
        Vector3 currentPosition = transform.position;

        if (!followX)
        {
            desiredPosition.x = currentPosition.x;
        }

        if (!followY)
        {
            desiredPosition.y = currentPosition.y;
        }

        float t = 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime);
        transform.position = Vector3.Lerp(currentPosition, desiredPosition, t);
    }
}
