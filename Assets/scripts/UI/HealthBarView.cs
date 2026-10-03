using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Barra de vida del tanque.
///
/// Anima el ANCHO del RectTransform, no el fillAmount del Image, y a proposito:
/// en el fuente de uGUI, Image.OnPopulateMesh hace esto primero
///
///     if (activeSprite == null) { base.OnPopulateMesh(toFill); return; }
///
/// o sea que si el Image no tiene sprite, fillAmount se ignora por completo y se
/// dibuja el rectangulo entero. Una barra de color solido no necesita ninguna
/// imagen, asi que pedirle un sprite solo para que el relleno funcione seria
/// agregar un asset y una dependencia al pedo.
///
/// El texto es opcional (si no esta asignado no pasa nada), asi que si no lo
/// queres en pantalla se puede borrar el objeto del todo.
/// </summary>
[DisallowMultipleComponent]
public class HealthBarView : MonoBehaviour
{
    [Tooltip("Image del relleno. Se anima su RectTransform, no su fillAmount. Si se deja vacio se busca el primer Image hijo.")]
    [SerializeField] private Image fill;

    [Tooltip("Opcional. Numero de vida actual.")]
    [SerializeField] private Text healthText;

    [Tooltip("Segundos que tarda la barra en vaciarse. 0 = se pega al valor nuevo.")]
    [SerializeField] private float fillSeconds = 0.2f;

    private RectTransform fillRect;

    /// <summary>Ancho con la barra llena. Se mide una vez y de ahi se escala.</summary>
    private float fullWidth;

    private float targetFill = 1f;
    private float shownFill = 1f;

    private void Awake()
    {
        if (fill == null)
        {
            fill = GetComponentInChildren<Image>();
        }

        if (fill != null)
        {
            fillRect = fill.rectTransform;
        }
    }

    private void Start()
    {
        if (fillRect != null)
        {
            // El RectTransform del Fill viene con anchors (0,0)-(0,1), asi que
            // sizeDelta.x ES el ancho y no hay que hacer cuentas con el padre.
            fullWidth = fillRect.sizeDelta.x;
        }

        Debug.Log($"[HealthBarView] vivo. fill={(fill != null ? fill.name : "NULL")} " +
                  $"anchoFull={(fillRect != null ? fillRect.sizeDelta.x.ToString() : "-")} " +
                  $"fillSeconds={fillSeconds}", this);
        Apply();
    }

    private void OnDisable()
    {
        shownFill = targetFill;
        Apply();
    }

    public void SetHealth(float current, float max)
    {
        targetFill = max > 0.0001f ? Mathf.Clamp01(current / max) : 0f;

        if (healthText != null)
        {
            healthText.text = Mathf.CeilToInt(Mathf.Max(current, 0f)).ToString();
        }
    }

    private void Update()
    {
        if (fillSeconds <= 0.0001f)
        {
            shownFill = targetFill;
            Apply();
            return;
        }

        if (Mathf.Abs(shownFill - targetFill) < 0.0005f)
        {
            return;
        }

        shownFill = Mathf.MoveTowards(shownFill, targetFill, Time.deltaTime / fillSeconds);
        Apply();
    }

    private void Apply()
    {
        if (fillRect == null)
        {
            Debug.LogError("[HealthBarView] fill sin asignar y no se encontro ningun Image hijo.", this);
            return;
        }

        fillRect.sizeDelta = new Vector2(fullWidth * shownFill, fillRect.sizeDelta.y);
    }
}