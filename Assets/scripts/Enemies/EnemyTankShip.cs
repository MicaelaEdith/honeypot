using UnityEngine;

/// <summary>
/// Nave con un cañon fijo al frente, tipo tanque de guerra. Apunta el morro
/// hacia el player y dispara en linea recta cuando esta alineada.
/// </summary>
public class EnemyTankShip : EnemyTemplate
{
    [Header("Cañon")]
    [Tooltip("Boca del cañon. Dejalo vacio y se calcula con muzzleLength hacia adelante.")]
    [SerializeField] private Transform muzzle;
    [Tooltip("Distancia desde el centro hasta la boca si muzzle esta vacio.")]
    [SerializeField] private float muzzleLength = 60f;
    [SerializeField] private float projectileSpeed = 110f;
    [Tooltip("Angulo maximo de desalineacion para poder disparar.")]
    [SerializeField] private float aimTolerance = 4f;

    protected override void AttackTick()
    {
        if (!CanAttack() || player == null)
        {
            return;
        }

        Vector3 direction = player.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }
        direction.Normalize();

        if (Vector3.Angle(ForwardFlat, direction) > aimTolerance)
        {
            return;
        }

        Vector3 muzzlePosition = muzzle != null ? muzzle.position : transform.position + direction * muzzleLength;
        SpawnEnemyProjectile(muzzlePosition, direction, projectileSpeed, damageAmount);
    }
}