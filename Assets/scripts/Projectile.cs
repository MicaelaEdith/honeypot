using UnityEngine;

public class Projectile : MonoBehaviour
{
    [SerializeField] private float lifeTime = 5f;
    public GameObject impactVfx;

    private void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    private void OnCollisionEnter(Collision collision)
    {
        SpawnImpact(collision.contacts.Length > 0 ? collision.contacts[0].point : transform.position);
    }

    private void OnTriggerEnter(Collider other)
    {
        SpawnImpact(transform.position);
    }

    private void SpawnImpact(Vector3 point)
    {
        if (impactVfx != null)
        {
            GameObject vfx = Instantiate(impactVfx, point, Quaternion.identity);
            Destroy(vfx, 2.5f);
        }
        Destroy(gameObject);
    }
}