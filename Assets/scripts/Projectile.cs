using UnityEngine;

[DisallowMultipleComponent]
public class Projectile : MonoBehaviour
{
    [SerializeField] private float lifeTime = 5f;
    [SerializeField] private float impactVfxLifeTime = 2.5f;
    [Tooltip("Daño que aplica al impactar un IDamagable. Lo pisa el enemigo al instanciar su bala.")]
    public float damage = 10f;
    [Tooltip("Si esta activo solo se aplica daño al TankController y no a cualquier IDamagable. Lo activan las balas enemigas que reutilizan este prefab: sin el filtro una nave lastimaria a las otras y se mataria con su propio disparo.")]
    public bool playerOnly;

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
        if (playerOnly && collider.GetComponentInParent<TankController>() == null)
        {
            return;
        }

        IDamagable damagable = collider.GetComponent<IDamagable>();
        if (damagable == null)
        {
            damagable = collider.GetComponentInParent<IDamagable>();
        }
        damagable?.TakeDamage(damage);
    }
}
