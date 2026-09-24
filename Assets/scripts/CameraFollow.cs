using UnityEngine;

/// <summary>
/// Camara de persecucion "detras del morro": se ubica siempre por detras
/// del rumbo del tanque (MovingForward) y lo mira, manteniendo el horizonte
/// nivelado. Es orgánica al girar y desliza suavemente en teleports
/// (respawn), sin heredar la rotacion del player.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Header("Referencia")]
    [Tooltip("Objetivo a seguir. Si queda vacio se busca el TankController.")]
    [SerializeField] private Transform target;

    [Header("Camara")]
    [Tooltip("Distancia horizontal detras del morro del tanque.")]
    [SerializeField] private float distance = 30f;
    [Tooltip("Altura de la camara sobre el tanque.")]
    [SerializeField] private float height = 17.5f;
    [Tooltip("Altura del punto del tanque que la camara enfoca.")]
    [SerializeField] private float lookHeight = 8f;
    [Tooltip("Suavizado del movimiento y la rotacion. 0 = pegada instantanea.")]
    [SerializeField] private float smoothing = 12f;
    [Tooltip("Velocidad maxima del movimiento de la camara (u/s). Limita el desliz en teleports/respawn.")]
    [SerializeField] private float maxSpeed = 60f;

    private Vector3 velocity;

    private void LateUpdate()
    {
        if (target == null)
        {
            TankController controller = FindFirstObjectByType<TankController>();
            if (controller != null)
            {
                target = controller.transform;
            }
            else
            {
                return;
            }
        }

        Vector3 forward = GetTankForward();
        Vector3 desired = target.position - forward * distance + Vector3.up * height;
        Vector3 lookPoint = target.position + Vector3.up * lookHeight;

        if (smoothing <= 0f)
        {
            transform.SetPositionAndRotation(desired, Quaternion.LookRotation(lookPoint - desired, Vector3.up));
            return;
        }

        Vector3 pos = Vector3.SmoothDamp(transform.position, desired, ref velocity, 1f / smoothing, maxSpeed);
        transform.position = pos;
        Vector3 direction = lookPoint - pos;
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(direction.normalized, Vector3.up),
            smoothing * Time.deltaTime);
    }

    private Vector3 GetTankForward()
    {
        TankController controller = target.GetComponent<TankController>();
        if (controller != null && controller.MovingForward.sqrMagnitude > 0.0001f)
        {
            return controller.MovingForward;
        }

        Vector3 fallback = target.forward;
        fallback.y = 0f;
        if (fallback.sqrMagnitude > 0.0001f)
        {
            return fallback.normalized;
        }
        return Vector3.forward;
    }
}