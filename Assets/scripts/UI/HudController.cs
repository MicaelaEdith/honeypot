using UnityEngine;

/// <summary>
/// Une el HUD con el juego. Es el UNICO script de la UI que sabe que existe un
/// TankController: las tres vistas reciben (actual, maxima) o (cantidad) y no
/// consultan nada.
///
/// Por que no un GameManager con la vida global: la vida ya tiene dueno, que es el
/// TankController. Guardarla tambien en un singleton son dos fuentes de verdad que
/// divergen en cuanto una se actualiza y la otra no (respawn, cura, cambio de
/// escena). El evento OnHealthChanged mantiene una sola.
///
/// Lo que si es estado de partida (bombas, puntos, olas) va en un GameManager, y
/// este script seria el que se suscribe a el. Hoy no hay nada que alimente
/// ese tipo de fuente.
///
/// Esto lo hace el HudController y no TankController: el tanque no tiene que saber
/// que existe una UI, asi que el dia que se quite el HUD (por ejemplo en una
/// pantalla de intro) no hay nada que desconectar.
///
/// Orden de inicio: OnEnable corre antes que Start, asi que se suscribe ahi y
/// ademas empuja el valor una vez en Start. Con cualquiera de los dos ordenes
/// posibles entre el Awake/Start del tanque y los suyos, el ultimo que escribe es
/// el correcto: si el tanque Todavia no arranco, su Start dispara el evento y
/// corrige lo que el HUD haya pintado de mas.
/// </summary>
[DisallowMultipleComponent]
public class HudController : MonoBehaviour
{
    [Tooltip("Si se deja vacio se busca el primero de la escena al Awake.")]
    [SerializeField] private TankController tank;

    [SerializeField] private HealthBarView healthBar;
    [SerializeField] private BeeStatusView bee;
    [SerializeField] private BombCounterView bombs;

    private void Awake()
    {
        ResolveTank();
    }

    private void OnEnable()
    {
        ResolveTank();

        if (tank != null)
        {
            tank.OnHealthChanged -= HandleHealth;
            tank.OnHealthChanged += HandleHealth;
        }

        Debug.Log($"[HudController] tank={(tank != null ? tank.name : "NULL")} " +
                  $"healthBar={(healthBar != null)} bee={(bee != null)} bombs={(bombs != null)}", this);
    }

    private void OnDisable()
    {
        if (tank != null)
        {
            tank.OnHealthChanged -= HandleHealth;
        }
    }

    private void Start()
    {
        ResolveTank();
        PushCurrentHealth();
    }

    private void ResolveTank()
    {
        if (tank == null)
        {
            tank = FindFirstObjectByType<TankController>();
        }
    }

    private void PushCurrentHealth()
    {
        if (tank != null)
        {
            HandleHealth(tank.CurrentHealth, tank.MaxHealth);
        }
    }

    private void HandleHealth(float current, float max)
    {
        // Sin esto un reference roto se come el error en silencio y la barra queda
        // clavada sin que se note ningun fallo.
        if (healthBar == null)
        {
            Debug.LogError("[HudController] healthBar sin asignar: la barra no se va a mover.", this);
        }
        else
        {
            healthBar.SetHealth(current, max);
        }

        if (bee != null)
        {
            bee.SetHealth(current, max);
        }

        Debug.Log($"[HudController] vida {current}/{max}", this);
    }
}