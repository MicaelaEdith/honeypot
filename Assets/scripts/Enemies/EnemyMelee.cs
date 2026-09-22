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
            transform.Rotate(Vector3.forward, rollSpeed * Time.deltaTime, Space.Self);
        }
    }
}