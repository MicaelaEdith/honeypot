using UnityEngine;

public class HoneyPotBeeAnimator : MonoBehaviour
{
    [Header("Referencias de abejas (hijos del prefab)")]
    [SerializeField] private Transform bee001;
    [SerializeField] private Transform bee002;
    [SerializeField] private Transform bee003;
    [SerializeField] private Transform bee004;

    [Header("Animacion")]
    [Tooltip("Altura maxima que suben/bajan (en unidades locales).")]
    [SerializeField] private float amplitude = 0.08f;

    [Tooltip("Velocidad de bee.001 y bee.003 (suben y luego bajen).")]
    [SerializeField] private float speedFast = 1.8f;

    [Tooltip("Velocidad de bee.002 y bee.004 (bajan y luego suban, un poco mas lenta).")]
    [SerializeField] private float speedSlow = 1.2f;

    private float baseY001;
    private float baseY002;
    private float baseY003;
    private float baseY004;

    private void Start()
    {
        if (bee001 != null)
        {
            baseY001 = bee001.localPosition.y;
        }

        if (bee002 != null)
        {
            baseY002 = bee002.localPosition.y;
        }

        if (bee003 != null)
        {
            baseY003 = bee003.localPosition.y;
        }

        if (bee004 != null)
        {
            baseY004 = bee004.localPosition.y;
        }
    }

    private void Update()
    {
        float time = Time.time;

        // bee.001 y bee.003: suben un poco y luego bajen
        if (bee001 != null)
        {
            float offset = Mathf.Sin(time * speedFast) * amplitude;
            Vector3 pos = bee001.localPosition;
            pos.y = baseY001 + offset;
            bee001.localPosition = pos;
        }

        if (bee003 != null)
        {
            float offset = Mathf.Sin(time * speedFast) * amplitude;
            Vector3 pos = bee003.localPosition;
            pos.y = baseY003 + offset;
            bee003.localPosition = pos;
        }

        // bee.002 y bee.004: bajen y luego suban (desfase de 180°) y un poco mas lentas
        if (bee002 != null)
        {
            float offset = Mathf.Sin(time * speedSlow + Mathf.PI) * amplitude;
            Vector3 pos = bee002.localPosition;
            pos.y = baseY002 + offset;
            bee002.localPosition = pos;
        }

        if (bee004 != null)
        {
            float offset = Mathf.Sin(time * speedSlow + Mathf.PI) * amplitude;
            Vector3 pos = bee004.localPosition;
            pos.y = baseY004 + offset;
            bee004.localPosition = pos;
        }
    }
}