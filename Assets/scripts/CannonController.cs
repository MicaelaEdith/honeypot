using UnityEngine;
using UnityEngine.InputSystem;

public class CannonController : MonoBehaviour
{
    [SerializeField] private float projectileSpeed = 15f;
    [SerializeField] private float fireCooldown = 0.25f;
    [SerializeField] private float muzzleOffset = 0.5f;
    [SerializeField] private float projectileScale = 0.2f;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private GameObject impactVfxPrefab;

    private Transform turret;
    private float cooldownTimer;

    private void Start()
    {
        turret = FindCanon(transform);
        if (turret == null)
        {
            turret = transform;
        }
    }

    private void Update()
    {
        cooldownTimer = Mathf.Max(0f, cooldownTimer - Time.deltaTime);

        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
        {
            return;
        }
        if (cooldownTimer > 0f)
        {
            return;
        }
        cooldownTimer = fireCooldown;

        Vector3 spawn;
        Vector3 dir;
        if (!GetBarrelSpot(out spawn, out dir))
        {
            Vector3 target = GetMouseGroundPoint();
            Vector3 from = turret.position;
            dir = target - from;
            if (dir.sqrMagnitude < 0.0001f)
            {
                return;
            }
            dir.Normalize();
            spawn = from + dir * muzzleOffset;
        }

        bool fromPrefab = projectilePrefab != null;
        GameObject sphere = fromPrefab
            ? Instantiate(projectilePrefab)
            : GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.transform.position = spawn;
        if (!fromPrefab)
        {
            sphere.transform.localScale = Vector3.one * projectileScale;
        }

        Collider collider = sphere.GetComponent<Collider>();
        if (collider != null)
        {
            collider.isTrigger = false;
        }

        Rigidbody body = sphere.GetComponent<Rigidbody>();
        if (body == null)
        {
            body = sphere.AddComponent<Rigidbody>();
        }
        body.collisionDetectionMode = CollisionDetectionMode.Continuous;
        body.linearVelocity = dir * projectileSpeed;

        if (!fromPrefab)
        {
            sphere.AddComponent<Projectile>();
        }
        Projectile projectile = sphere.GetComponent<Projectile>();
        if (projectile != null)
        {
            projectile.impactVfx = impactVfxPrefab;
        }
    }

    private bool GetBarrelSpot(out Vector3 spawn, out Vector3 dir)
    {
        spawn = default;
        dir = default;

        Renderer best = null;
        float bestLength = 0f;
        foreach (Renderer renderer in turret.GetComponentsInChildren<Renderer>())
        {
            Vector3 extents = renderer.localBounds.extents;
            float length = Mathf.Max(extents.x, Mathf.Max(extents.y, extents.z));
            if (length > bestLength)
            {
                bestLength = length;
                best = renderer;
            }
        }
        if (best == null)
        {
            return false;
        }

        Vector3 ext = best.localBounds.extents;
        Vector3 axis = ext.x >= ext.y && ext.x >= ext.z
            ? Vector3.right
            : (ext.y >= ext.z ? Vector3.up : Vector3.forward);
        dir = best.transform.TransformDirection(axis).normalized;
        if (dir.y < -0.99f || dir.y > 0.99f)
        {
            return false;
        }

        Vector3 pivot = turret.position;
        Bounds bounds = best.bounds;
        Vector3[] corners =
        {
            bounds.min,
            new Vector3(bounds.max.x, bounds.min.y, bounds.min.z),
            new Vector3(bounds.min.x, bounds.max.y, bounds.min.z),
            new Vector3(bounds.min.x, bounds.min.y, bounds.max.z),
            new Vector3(bounds.max.x, bounds.max.y, bounds.min.z),
            new Vector3(bounds.max.x, bounds.min.y, bounds.max.z),
            new Vector3(bounds.min.x, bounds.max.y, bounds.max.z),
            bounds.max
        };

        float forward = 0f;
        float backward = 0f;
        foreach (Vector3 corner in corners)
        {
            forward = Mathf.Max(forward, Vector3.Dot(corner - pivot, dir));
            backward = Mathf.Max(backward, Vector3.Dot(pivot - corner, dir));
        }

        if (backward > forward)
        {
            dir = -dir;
            forward = backward;
        }

        spawn = pivot + dir * Mathf.Max(forward, muzzleOffset);
        return true;
    }

    private Vector3 GetMouseGroundPoint()
    {
        Camera cam = Camera.main;
        Vector3 mousePosition = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector3.zero;
        Ray ray = cam.ScreenPointToRay(mousePosition);
        Plane plane = new Plane(Vector3.up, Vector3.zero);
        if (plane.Raycast(ray, out float distance))
        {
            return ray.GetPoint(distance);
        }
        return ray.origin + ray.direction.normalized * 10f;
    }

    private Transform FindCanon(Transform root)
    {
        foreach (Transform child in root)
        {
            if (IsCanonName(child.name))
            {
                return child;
            }
            Transform found = FindCanon(child);
            if (found != null)
            {
                return found;
            }
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