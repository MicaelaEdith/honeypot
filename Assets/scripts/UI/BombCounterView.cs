using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bombas disponibles: una miniatura (el icono) al lado de un numero.
///
/// Todavia no hay nada que produzca el numero (no existe codigo de bombas en el
/// proyecto), asi que la vista esta completa pero sin fuente: HudController la
/// puede dejar en 0 o no llamarla hasta que exista el GameManager que la alimente.
/// Por eso el contador es un SetCount(int) y no una lectura del juego.
/// </summary>
[DisallowMultipleComponent]
public class BombCounterView : MonoBehaviour
{
    [Tooltip("Numero de bombas. Si se deja vacio se busca el primer Text hijo.")]
    [SerializeField] private Text countText;

    [Tooltip("Se atenua el icono cuando no queda ninguna, para que se note que no hay.")]
    [SerializeField] private Image bombIcon;

    [SerializeField] private Color emptyTint = new Color(1f, 1f, 1f, 0.35f);
    [SerializeField] private Color normalTint = Color.white;

    public void SetCount(int count)
    {
        int clamped = Mathf.Max(count, 0);

        if (countText != null)
        {
            countText.text = clamped.ToString();
        }

        if (bombIcon != null)
        {
            bombIcon.color = clamped > 0 ? normalTint : emptyTint;
        }
    }

    private void Awake()
    {
        if (countText == null)
        {
            countText = GetComponentInChildren<Text>();
        }

        if (bombIcon == null)
        {
            bombIcon = GetComponentInChildren<Image>();
        }
    }
}