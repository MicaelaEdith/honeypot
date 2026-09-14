using UnityEngine;
using UnityEngine.InputSystem;

public class TankController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 90f;
    [SerializeField] private bool invertForward = false;
    [SerializeField] private bool invertTurning = false;

    private Vector3 front;

    private void Start()
    {
        front = DetectFront();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        bool keyW = keyboard != null && keyboard.wKey.isPressed;
        bool keyS = keyboard != null && keyboard.sKey.isPressed;
        bool keyA = keyboard != null && keyboard.aKey.isPressed;
        bool keyD = keyboard != null && keyboard.dKey.isPressed;

        float turnAmount = 0f;
        if (keyA) turnAmount -= 1f;
        if (keyD) turnAmount += 1f;
        if (invertTurning) turnAmount = -turnAmount;
        float yaw = turnAmount * rotationSpeed * Time.deltaTime;
        transform.Rotate(Vector3.up, yaw, Space.World);
        front = Quaternion.AngleAxis(yaw, Vector3.up) * front;

        float throttle = 0f;
        if (keyW) throttle += 1f;
        if (keyS) throttle -= 1f;
        if (invertForward) throttle = -throttle;

        transform.position += front * (throttle * moveSpeed * Time.deltaTime);
    }

    private Vector3 DetectFront()
    {
        Transform canon = FindCanon(transform);
        if (canon != null)
        {
            Renderer renderer = canon.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                Vector3 extents = renderer.localBounds.extents;
                Vector3 axis = extents.x >= extents.y && extents.x >= extents.z
                    ? Vector3.right
                    : (extents.y >= extents.z ? Vector3.up : Vector3.forward);
                Vector3 dir = renderer.transform.TransformDirection(axis);
                dir.y = 0f;
                dir.Normalize();
                if (Vector3.Dot(renderer.bounds.center - canon.position, dir) < 0f)
                    dir = -dir;
                if (dir.sqrMagnitude > 0.01f)
                    return dir;
            }
            Vector3 canonFront = canon.forward;
            canonFront.y = 0f;
            if (canonFront.sqrMagnitude > 0.01f)
                return canonFront.normalized;
        }

        Vector3 fwd = transform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude > 0.01f)
            return fwd.normalized;

        Vector3 right = transform.right;
        right.y = 0f;
        return right.sqrMagnitude > 0.01f ? right.normalized : Vector3.forward;
    }

    private Transform FindCanon(Transform root)
    {
        foreach (Transform child in root)
        {
            if (IsCanonName(child.name))
                return child;
            Transform found = FindCanon(child);
            if (found != null)
                return found;
        }
        return null;
    }

    private bool IsCanonName(string name)
    {
        string n = name.ToLowerInvariant();
        return n.Contains("canion") || n.Contains("cañon") || n.Contains("cañón")
            || n.Contains("canon") || n.Contains("cannon") || n.Contains("turret")
            || n.Contains("torreta") || n.Contains("gun") || n.Contains("barrel");
    }
}