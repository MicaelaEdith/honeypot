using UnityEngine;

[DisallowMultipleComponent]
public class EnemyProjectile : MonoBehaviour
{
    [SerializeField] private float lifeTime = 4f;
    [SerializeField] private float impactVfxLifeTime = 2.5f;

    public float damage = 15f;
    public float speed = 120f;
    public GameObject impactVfx;

    private bool consumed;

    private void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    public void Launch(Vector3 direction, float launchSpeed)
    {
        speed = launchSpeed;
        Rigidbody body = GetComponent<Rigidbody>();
        if (body == null)
        {
            body = gameObject.AddComponent<Rigidbody>();
        }
        body.useGravity = false;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.linearVelocity = direction.normalized * speed;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (consumed)
        {
            return;
        }
        consumed = true;

        Vector3 point = collision.contactCount > 0 ? collision.contacts[0].point : transform.position;
        ApplyDamage(collision.collider);
        SpawnImpact(point);
        Destroy(gameObject);
    }

    private void ApplyDamage(Collider collider)
    {
        IDamagable damagable = collider.GetComponent<IDamagable>();
        if (damagable == null)
        {
            damagable = collider.GetComponentInParent<IDamagable>();
        }
        damagable?.TakeDamage(damage);
    }

    private void SpawnImpact(Vector3 point)
    {
        if (impactVfx == null)
        {
            return;
        }

        GameObject vfx = Instantiate(impactVfx, point, Quaternion.identity);
        Destroy(vfx, impactVfxLifeTime);
    }
}