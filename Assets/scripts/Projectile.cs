using UnityEngine;

[DisallowMultipleComponent]
public class Projectile : MonoBehaviour
{
    [SerializeField] private float lifeTime = 5f;
    [SerializeField] private float impactVfxLifeTime = 2.5f;
    [Tooltip("Daño que aplica al impactar un IDamagable.")]
    [SerializeField] private float damage = 10f;

    public GameObject impactVfx;
    public float impactVfxScale = 1f;

    private bool consumed;

    private void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    private void OnCollisionEnter(Collision collision)
    {
        Vector3 point = collision.contactCount > 0 ? collision.contacts[0].point : transform.position;
        Impact(collision.collider, point);
    }

    private void OnTriggerEnter(Collider other)
    {
        Impact(other, transform.position);
    }

    private void Impact(Collider collider, Vector3 point)
    {
        if (consumed)
        {
            return;
        }
        consumed = true;

        ApplyDamage(collider);

        if (impactVfx != null)
        {
            GameObject vfx = Instantiate(impactVfx, point, Quaternion.identity);
            vfx.transform.localScale *= impactVfxScale;
            Destroy(vfx, impactVfxLifeTime);
        }
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
}
