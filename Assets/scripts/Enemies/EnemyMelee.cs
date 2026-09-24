using UnityEngine;

/// <summary>
/// Rueda gigante con pinchos (placeholder: esfera). Persigue al player y
/// lo daña por contacto mientras esta en rango. Modelo final se cambia
/// solo en el prefab, sin tocar codigo.
/// </summary>
public class EnemyMelee : EnemyTemplate
{
    [Header("Visual")]
    [Tooltip("Velocidad del giro visual mientras esta vivo.")]
    [SerializeField] private float rollSpeed = 180f;

    protected override void Update()
    {
        base.Update();

        if (currentState != EnemyState.Death)
        {
            Vector3 velocity = Vector3.ProjectOnPlane(agent.velocity, Vector3.up);
            Vector3 axis = Vector3.Cross(Vector3.up, velocity.normalized);
            if (axis.sqrMagnitude > 0.0001f)
            {
                transform.Rotate(axis.normalized, rollSpeed * Time.deltaTime, Space.World);
            }
        }
    }
}