using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class HoneyPotActivator : MonoBehaviour
{
    [Header("Honeypot")]
    [Tooltip("Prefab del honeypot que se instancia al activar.")]
    [SerializeField] private GameObject honeypotPrefab;

    [Tooltip("Cuantos honeypots tiene el player al iniciar.")]
    [SerializeField] private int maxHoneypots = 10;

    [Header("Activacion")]
    [Tooltip("Grados de giro acumulados (sin W/S) necesarios para disparar la trampa.")]
    [SerializeField] private float degreesToActivate = 360f;

    [Tooltip("Offset desde el pivote del tanque al spawnear el honeypot.")]
    [SerializeField] private Vector3 spawnOffset = Vector3.zero;

    [Tooltip("Tiempo minimo entre dos activaciones.")]
    [SerializeField] private float cooldown = 0.3f;

    public event System.Action<int> OnHoneypotCountChanged;

    public int RemainingHoneypots => remainingHoneypots;

    private int remainingHoneypots;
    private float accumulatedDegrees;
    private float previousYaw;
    private float nextActivationTime;

    private void Start()
    {
        Debug.Log($"[HoneyPotActivator] Start: honeypotPrefab={(honeypotPrefab != null ? honeypotPrefab.name : "NULL")}, degreesToActivate={degreesToActivate}, maxHoneypots={maxHoneypots}", this);
        remainingHoneypots = maxHoneypots;
        previousYaw = transform.eulerAngles.y;
        OnHoneypotCountChanged?.Invoke(remainingHoneypots);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        bool throttling = keyboard.wKey.isPressed || keyboard.sKey.isPressed;
        bool turning = keyboard.aKey.isPressed || keyboard.dKey.isPressed;

        if (throttling)
        {
            if (accumulatedDegrees > 0f)
            {
                Debug.Log($"[HoneyPotActivator] W/S apretado: acumulador reiniciado ({accumulatedDegrees:F1}° descartados).", this);
            }
            accumulatedDegrees = 0f;
        }
        else if (turning)
        {
            float yaw = transform.eulerAngles.y;
            float delta = Mathf.Abs(Mathf.DeltaAngle(previousYaw, yaw));
            accumulatedDegrees += delta;
            Debug.Log($"[HoneyPotActivator] girando: yaw={yaw:F1} Δ={delta:F2} acum={accumulatedDegrees:F1}/{degreesToActivate}", this);

            if (accumulatedDegrees >= degreesToActivate)
            {
                accumulatedDegrees = 0f;
                TryActivate();
            }
        }
        else if (accumulatedDegrees > 0f)
        {
            Debug.Log($"[HoneyPotActivator] sin girar: acumulador en {accumulatedDegrees:F1}° (no resetea).", this);
        }

        previousYaw = transform.eulerAngles.y;
    }

    private void TryActivate()
    {
        if (Time.time < nextActivationTime)
        {
            return;
        }

        if (honeypotPrefab == null)
        {
            Debug.LogError("[HoneyPotActivator] honeypotPrefab sin asignar.", this);
            return;
        }

        if (remainingHoneypots <= 0)
        {
            Debug.Log("[HoneyPotActivator] No quedan honeypots.", this);
            return;
        }

        Instantiate(honeypotPrefab, transform.position + spawnOffset, Quaternion.identity);
        remainingHoneypots--;
        nextActivationTime = Time.time + cooldown;
        OnHoneypotCountChanged?.Invoke(remainingHoneypots);
        Debug.Log($"[HoneyPotActivator] Honeypot spawneado. Quedan: {remainingHoneypots}", this);
    }
}
