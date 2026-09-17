using UnityEngine;

[DisallowMultipleComponent]
public class Projectile : MonoBehaviour
{
    [SerializeField] private float lifeTime = 5f;
    [SerializeField] private float impactVfxLifeTime = 2.5f;

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
        Impact(point);
    }

    private void OnTriggerEnter(Collider other)
    {
        Impact(transform.position);
    }

    private void Impact(Vector3 point)
    {
        if (consumed)
        {
            return;
        }
        consumed = true;

        if (impactVfx != null)
        {
            GameObject vfx = Instantiate(impactVfx, point, Quaternion.identity);
            vfx.transform.localScale *= impactVfxScale;
            Destroy(vfx, impactVfxLifeTime);
        }
        Destroy(gameObject);
    }
}
