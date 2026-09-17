using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public class TankController : MonoBehaviour
{
    [Header("Movimiento")]
    [Tooltip("Velocidad de avance/retroceso en unidades por segundo.")]
    [SerializeField] private float moveSpeed = 20f;
    [Tooltip("Velocidad de giro del casco en grados por segundo.")]
    [SerializeField] private float turnSpeed = 90f;
    [Tooltip("Aceleracion de caida para mantener el tanque apoyado.")]
    [SerializeField] private float gravity = 40f;
    [SerializeField] private bool invertForward = false;
    [SerializeField] private bool invertTurn = false;

    [Header("Frente del casco")]
    [Tooltip("Dejar en cero para detectarlo solo a partir del cañon.")]
    [SerializeField] private Vector3 bodyForwardLocal = Vector3.zero;

    [Header("Collider")]
    [SerializeField] private bool autoFitController = true;
    [Range(0.5f, 1f)]
    [SerializeField] private float fitWidthFactor = 0.8f;

    private CharacterController controller;
    private CannonController cannon;
    private Vector3 forwardLocal;
    private float verticalSpeed;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        cannon = GetComponentInChildren<CannonController>();
    }

    private void Start()
    {
        forwardLocal = ResolveForward();
        if (autoFitController)
        {
            FitController();
        }
    }

    private void Update()
    {
        float throttle = 0f;
        float turn = 0f;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.wKey.isPressed) throttle += 1f;
            if (keyboard.sKey.isPressed) throttle -= 1f;
            if (keyboard.aKey.isPressed) turn -= 1f;
            if (keyboard.dKey.isPressed) turn += 1f;
        }

        if (invertForward) throttle = -throttle;
        if (invertTurn) turn = -turn;

        transform.Rotate(Vector3.up, turn * turnSpeed * Time.deltaTime, Space.World);

        Vector3 forward = transform.TransformDirection(forwardLocal);
        forward.y = 0f;
        if (forward.sqrMagnitude > 0.0001f)
        {
            forward.Normalize();
        }

        if (controller.isGrounded && verticalSpeed < 0f)
        {
            verticalSpeed = -2f;
        }
        verticalSpeed -= gravity * Time.deltaTime;

        Vector3 motion = forward * (throttle * moveSpeed) + Vector3.up * verticalSpeed;
        controller.Move(motion * Time.deltaTime);
    }

    private Vector3 ResolveForward()
    {
        if (bodyForwardLocal.sqrMagnitude > 0.0001f)
        {
            Vector3 manual = bodyForwardLocal;
            manual.y = 0f;
            if (manual.sqrMagnitude > 0.0001f)
            {
                return manual.normalized;
            }
        }

        if (cannon != null)
        {
            Vector3 barrel = cannon.BarrelDirection;
            barrel.y = 0f;
            if (barrel.sqrMagnitude > 0.0001f)
            {
                Vector3 local = transform.InverseTransformDirection(barrel.normalized);
                local.y = 0f;
                if (local.sqrMagnitude > 0.0001f)
                {
                    return local.normalized;
                }
            }
        }

        return Vector3.forward;
    }

    private void FitController()
    {
        if (!TryGetBodyBounds(out Bounds world))
        {
            return;
        }

        Vector3 a = transform.InverseTransformPoint(world.min);
        Vector3 b = transform.InverseTransformPoint(world.max);
        Vector3 min = Vector3.Min(a, b);
        Vector3 max = Vector3.Max(a, b);
        Vector3 size = max - min;

        float radius = Mathf.Min(size.x, size.z) * 0.5f * fitWidthFactor;
        radius = Mathf.Max(radius, 0.1f);
        float height = Mathf.Max(size.y, radius * 2.05f);

        controller.radius = radius;
        controller.height = height;
        controller.center = new Vector3(
            (min.x + max.x) * 0.5f,
            min.y + height * 0.5f,
            (min.z + max.z) * 0.5f);
        controller.stepOffset = height * 0.1f;
        controller.skinWidth = radius * 0.1f;
    }

    private bool TryGetBodyBounds(out Bounds bounds)
    {
        bounds = default;
        bool has = false;

        Transform excluded = cannon != null ? cannon.CannonRoot : null;
        if (excluded == transform)
        {
            excluded = null;
        }

        foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
        {
            if (IsInside(renderer.transform, excluded))
            {
                continue;
            }

            if (!has)
            {
                bounds = renderer.bounds;
                has = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return has;
    }

    private static bool IsInside(Transform candidate, Transform ancestor)
    {
        if (ancestor == null)
        {
            return false;
        }

        Transform current = candidate;
        while (current != null)
        {
            if (current == ancestor)
            {
                return true;
            }
            current = current.parent;
        }
        return false;
    }
}
