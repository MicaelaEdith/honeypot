using UnityEngine;

/// <summary>
/// Nave circular con tres cañones en los costados. Dispara rápido y fuerte,
/// y puede dañar por contacto si contactDamage esta activo (opcional).
/// </summary>
public class EnemyShooter : EnemyTemplate
{
    [Header("Cañones")]
    [Tooltip("Velocidad de los proyectiles en unidades por segundo.")]
    [SerializeField] private float projectileSpeed = 150f;
    [Tooltip("Radio donde nacen los proyectiles alrededor de la nave.")]
    [SerializeField] private float muzzleRadius = 45f;
    [Tooltip("Angulo (grados) entre los tres cañones laterales.")]
    [SerializeField] private float spreadAngle = 120f;

    protected override void AttackTick()
    {
        if (!CanAttack())
        {
            return;
        }

        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.forward;
        }
        forward.Normalize();

        for (int i = -1; i <= 1; i++)
        {
            Quaternion rotation = Quaternion.Euler(0f, spreadAngle * i, 0f);
            Vector3 offset = rotation * forward * muzzleRadius;
            Vector3 muzzle = transform.position + offset;

            Vector3 direction = player != null ? player.position - muzzle : forward;
            direction.y = 0f;
            direction.Normalize();

            SpawnEnemyProjectile(muzzle, direction, projectileSpeed, damageAmount);
        }
    }
}