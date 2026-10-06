using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MenuController : MonoBehaviour
{
    [Header("Botones")]
    public Button btn_play;
    public Button btn_settings;
    public Button btn_quit;
    public Button btn_quitSettings;

    [Header("Panel Settings")]
    public GameObject panel_settings;

    [Header("Escena")]
    public string escena_a_cargar;

    private void Start()
    {
        btn_play.onClick.AddListener(CargarEscena);
        btn_settings.onClick.AddListener(AbrirSettings);
        btn_quit.onClick.AddListener(SalirJuego);
        btn_quitSettings.onClick.AddListener(CerrarSettings);
    }

    private void CargarEscena()
    {
        SceneManager.LoadScene(escena_a_cargar);
    }

    private void AbrirSettings()
    {
        panel_settings.SetActive(true);
    }

    private void CerrarSettings()
    {
        panel_settings.SetActive(false);
    }

    private void SalirJuego()
    {
        Application.Quit();
    }
}