using UnityEngine;
using UnityEngine.InputSystem;

public class TurretController : MonoBehaviour
{
    private static readonly string[] CandidateNames =
    {
        "canion", "cañon", "cañón", "canon", "cannon", "turret", "torreta", "gun", "barrel"
    };

    [SerializeField] private Transform turret;
    [SerializeField] private float rotationSpeed = 300f;
    [SerializeField] private bool snapToTarget = false;

    private Quaternion referenceRotation;
    private Vector3 referenceForward;

    private void Start()
    {
        if (turret == null)
        {
            turret = FindTurret(transform);
        }
        if (turret == null)
        {
            turret = transform;
        }

        referenceRotation = turret.rotation;
        referenceForward = turret.forward;
        referenceForward.y = 0f;
        if (referenceForward.sqrMagnitude < 0.0001f)
        {
            referenceForward = Vector3.forward;
        }
        referenceForward.Normalize();
    }

    private void Update()
    {
        AimAt(GetMouseWorldPoint());
    }

    public void AimAt(Vector3 target)
    {
        Vector3 toTarget = target - turret.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.0001f)
        {
            return;
        }
        toTarget.Normalize();

        float yaw = Vector3.SignedAngle(referenceForward, toTarget, Vector3.up);
        Quaternion desired = Quaternion.AngleAxis(yaw, Vector3.up) * referenceRotation;

        if (snapToTarget)
        {
            turret.rotation = desired;
        }
        else
        {
            turret.rotation = Quaternion.RotateTowards(turret.rotation, desired, rotationSpeed * Time.deltaTime);
        }
    }

    private Vector3 GetMouseWorldPoint()
    {
        Camera cam = Camera.main;
        Vector3 mousePosition = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector3.zero;
        Ray ray = cam.ScreenPointToRay(mousePosition);
        Plane plane = new Plane(Vector3.up, turret.position);
        if (plane.Raycast(ray, out float distance))
        {
            return ray.GetPoint(distance);
        }
        return turret.position + ray.direction.normalized * 10f;
    }

    private static Transform FindTurret(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        foreach (string candidate in CandidateNames)
        {
            Transform match = FindByNameRecursive(root, candidate);
            if (match != null)
            {
                return match;
            }
        }

        return FindByPartRecursive(root);
    }

    private static Transform FindByNameRecursive(Transform current, string name)
    {
        for (int i = 0; i < current.childCount; i++)
        {
            Transform child = current.GetChild(i);
            if (NameMatches(child, name))
            {
                return child;
            }
            Transform nested = FindByNameRecursive(child, name);
            if (nested != null)
            {
                return nested;
            }
        }
        return null;
    }

    private static Transform FindByPartRecursive(Transform current)
    {
        for (int i = 0; i < current.childCount; i++)
        {
            Transform child = current.GetChild(i);
            if (child.name.IndexOf("can", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return child;
            }
            Transform nested = FindByPartRecursive(child);
            if (nested != null)
            {
                return nested;
            }
        }
        return null;
    }

    private static bool NameMatches(Transform transform, string name)
    {
        if (string.IsNullOrEmpty(transform.name))
        {
            return false;
        }
        string lower = transform.name.ToLowerInvariant();
        return lower == name || lower == name + ".001" || lower.Contains(name);
    }
}