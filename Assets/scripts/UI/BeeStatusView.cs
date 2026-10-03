using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Estado de la abeja: 5 sprites, de sana a casi muerta, que se elegian por la
/// salud del tanque en pasos de 20.
///
/// Dos decisiones importantes:
///
/// 1. El indice se CALCULA de la vida, nunca se acumula. Con un contador, un
///    proyectil que saca 40 de un saque se comia dos pasos de una y la abeja
///    mentia, y una cura la desincronizaba para siempre. Calcularlo es idempotente:
///    el mismo dano, por muchas veces que se aplique, da el mismo resultado.
///
/// 2. El rebote corre en Update con un timer, no con corrutina ni Animator. El
///    dano por contacto del melee puede llegar dos veces seguidas, y dos corrutinas
///    peleandose por la escala del RectTransform dan un tirone visible.
///
/// No lee nada del tanque: recibe (actual, maxima). Por eso el dia de hoy mostrar
/// la salud del tanque y el de mañana una vida propia de la abeja son el mismo
/// codigo en esta clase: solo cambia que le pasa el HudController.
/// </summary>
[DisallowMultipleComponent]
public class BeeStatusView : MonoBehaviour
{
    [Tooltip("La Image cuyo sprite se cambia. Si se deja vacio se busca el primer Image hijo.")]
    [SerializeField] private Image beeIcon;

    [Tooltip("Sprites ordenados de sana a casi muerta (5). El indice sale de la vida, no del orden de llegada del dano.")]
    [SerializeField] private Sprite[] beeSprites = new Sprite[0];

    [Tooltip("Escala maxima del rebote al recibir dano.")]
    [Range(1f, 2f)]
    [SerializeField] private float punchScale = 1.25f;

    [Tooltip("Duracion del rebote en segundos.")]
    [SerializeField] private float punchSeconds = 0.25f;

    private int currentIndex = -1;
    private float lastHealth;
    private float maxHealth = 1f;
    private float punchTimer;
    private RectTransform iconRect;
    private Vector3 baseScale = Vector3.one;

    private void Awake()
    {
        if (beeIcon == null)
        {
            beeIcon = GetComponentInChildren<Image>();
        }

        if (beeIcon != null)
        {
            iconRect = beeIcon.rectTransform;
            baseScale = iconRect.localScale;
        }
    }

    private void OnDisable()
    {
        punchTimer = 0f;
        if (iconRect != null)
        {
            iconRect.localScale = baseScale;
        }
    }

    public void SetSprites(Sprite[] sprites)
    {
        beeSprites = sprites ?? new Sprite[0];
        currentIndex = -1;
    }

    /// <summary>
    /// Estado inicial sin rebote: para cuando la UI se activa con la vida ya
    /// puesta (respawn, carga de escena) y no queremos que la abeja "reaccione"
    /// a un valor que en realidad no es dano.
    /// </summary>
    public void ResetTo(float current, float max)
    {
        maxHealth = Mathf.Max(max, 0.0001f);
        lastHealth = Mathf.Clamp(current, 0f, maxHealth);
        ApplyState(lastHealth);
    }

    public void SetHealth(float current, float max)
    {
        maxHealth = Mathf.Max(max, 0.0001f);
        float clamped = Mathf.Clamp(current, 0f, maxHealth);

        if (clamped < lastHealth)
        {
            punchTimer = Mathf.Max(punchSeconds, 0.0001f);
        }

        lastHealth = clamped;
        ApplyState(clamped);
    }

    private void ApplyState(float current)
    {
        if (beeIcon == null || beeSprites == null || beeSprites.Length == 0)
        {
            return;
        }

        int index = StateIndex(current);
        if (index == currentIndex)
        {
            return;
        }

        currentIndex = index;
        Sprite sprite = beeSprites[index];
        if (sprite != null)
        {
            beeIcon.sprite = sprite;
        }
    }

    /// <summary>
    /// Con maxHealth 100 y 5 sprites el paso sale de 100/5 = 20, no de un 20
    /// escrito a mano: si el dia de mañana la vida maxima es 150 los pasos siguen
    /// repartiendo los 5 estados en partes iguales.
    /// El clamp final cubre la vida en 0, que daria un paso de mas.
    /// </summary>
    private int StateIndex(float current)
    {
        int count = beeSprites.Length;
        if (count <= 1)
        {
            return 0;
        }

        float step = maxHealth / count;
        if (step <= 0.0001f)
        {
            return 0;
        }

        return Mathf.Clamp(Mathf.FloorToInt((maxHealth - current) / step), 0, count - 1);
    }

    private void Update()
    {
        if (punchTimer <= 0f || iconRect == null)
        {
            return;
        }

        punchTimer -= Time.deltaTime;
        if (punchTimer <= 0f)
        {
            iconRect.localScale = baseScale;
            return;
        }

        // Sin(pi * t) va 0 -> 1 -> 0 con t de 1 a 0: el rebote sale y vuelve solo.
        float t = Mathf.Clamp01(punchTimer / Mathf.Max(punchSeconds, 0.0001f));
        float bump = Mathf.Sin(t * Mathf.PI);
        iconRect.localScale = baseScale * Mathf.Lerp(1f, punchScale, bump);
    }
}