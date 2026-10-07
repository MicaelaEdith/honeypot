using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public abstract class EnemyTemplate : MonoBehaviour, IDamagable
{
    [Header("Stats")]
    [Tooltip("Vida maxima del enemigo.")]
    [SerializeField] private float maxHealth = 60f;
    [Tooltip("Distancia desde la cual detecta al player y lo persigue. 0 = se calcula con el tamaño del casco.")]
    [SerializeField] private float detectionRange = 0f;
    [Tooltip("Distancia a la que entra en estado de ataque. 0 = se calcula con el tamaño del casco.")]
    [SerializeField] protected float attackRange = 0f;
    [Tooltip("Daño por golpe/proyectil.")]
    [SerializeField] protected float damageAmount = 12f;
    [SerializeField] private float attackCooldown = 1.2f;
    [Tooltip("Altura de vuelo sobre la navegacion. 0 = pegado al piso.")]
    [SerializeField] private float hoverHeight = 15f;

    [Header("Escala relativa al casco")]
    [Tooltip("Los FBX vienen con escala raiz enorme (tanque ~200u, naves ~290u), asi que un rango o un proyectil en numeros absolutos queda en cualquier escala. Con estos factores todo se deriva del radio real del casco. Un valor > 0 en el campo absoluto correspondiente pisa al factor.")]
    [SerializeField] private float detectionRangeFactor = 2.5f;
    [Tooltip("Rango de ataque como multiplicador del radio del casco. attackRange > 0 lo pisa.")]
    [SerializeField] private float attackRangeFactor = 1.5f;
    [Tooltip("Diametro de la bala como fraccion del radio del casco. projectileScale > 0 lo pisa.")]
    [SerializeField] private float projectileSizeFactor = 0.05f;
    [Tooltip("Margen extra de la boca del cañon, como fraccion del radio del casco (0 = sin margen).")]
    [SerializeField] private float muzzleMarginFactor = 0.1f;
    [Tooltip("Segundos que tarda la bala en llegar al objetivo. projectileSpeed > 0 lo pisa.")]
    [SerializeField] private float flightTime = 1.2f;

    /// <summary>Segundos de vuelo configurados, para los prints de diagnostico.</summary>
    protected float FlightTime => flightTime;
    [Tooltip("Velocidad de los proyectiles en unidades por segundo. 0 = se deriva del tiempo de vuelo.")]
    [SerializeField] private float projectileSpeed = 0f;
    [Tooltip("Diametro de los proyectiles enemigos en unidades de mundo. 0 = se deriva del casco.")]
    [SerializeField] private float projectileScale = 0f;

    [Header("Proyectil estilo player")]
    [Tooltip("Mismo prefab que usa el tanque, para que la bala se vea igual. Vacio = se cae en SpawnEnemyProjectile (esfera simple sin VFX).")]
    [SerializeField] private GameObject projectilePrefab;
    [Tooltip("Mismo VFX de impacto que usa el tanque. Vacio = la bala no genera VFX al impactar.")]
    [SerializeField] private GameObject impactVfxPrefab;
    [Tooltip("Diametro del VFX de impacto como multiple del diametro de la bala. El prefab ImpactVFX esta authoring para ~5.5u, asi que se reescala a esta medida.")]
    [SerializeField] private float impactVfxDiameterFactor = 2f;
    [Tooltip("Diametro natural del prefab de VFX de impacto, en unidades de mundo.")]
    [SerializeField] private float impactVfxReferenceDiameter = 5.5f;
    [Tooltip("Multiplicador extra sobre el VFX de impacto.")]
    [SerializeField] private float impactVfxScale = 1f;

    [Header("Rotacion")]
    [Tooltip("Si esta activo, rota el cuerpo hacia el player (updateRotation del agente desactivada).")]
    [SerializeField] private bool rotateTowardPlayer = true;
    [SerializeField] private float rotateSpeed = 120f;
    [Tooltip("Eje local del modelo que apunta hacia el morro (forward visual de la nave).\nDefault (0,0,1). Si el modelo tiene la nariz 'parada' (tilt -90 en X), usa (0,1,0).")]
    [SerializeField] private Vector3 modelForward = Vector3.forward;

    [Header("Choque")]
    [Tooltip("Si esta activo, daña al player por contacto mientras esta en rango de ataque.")]
    [SerializeField] private bool contactDamage = false;
    [Tooltip("Alcance del contacto en unidades de mundo. 0 = usa el rango de ataque.")]
    [SerializeField] private float contactRange = 0f;
    [SerializeField] private float contactCooldown = 1f;

    [Header("Muerte")]
    [Tooltip("VFX propio de la muerte. Vacio = Resources/DeathVFX, y si no existe se cae al VFX de impacto.")]
    [SerializeField] private GameObject deathVfxPrefab;
    [SerializeField] private float deathVfxScaleMultiplier = 5.7f;
    [Tooltip("Cantidad de explosiones que se instancian al morir (la primera centrada, las demas con offset).")]
    [SerializeField] private int deathVfxExplosionCount = 3;
    [Tooltip("Demora entre explosiones consecutivas, en segundos.")]
    [SerializeField] private float deathVfxExplosionDelay = 0.12f;
    [Tooltip("Radio del offset aleatorio de las explosiones secundarias, como fraccion del radio del casco.")]
    [SerializeField] private float deathVfxExplosionSpread = 0.4f;
    [Tooltip("Multiplica la cantidad de particulas (bursts y rateOverTime) de cada explosion.")]
    [SerializeField] private float deathVfxParticleMultiplier = 4f;
    [Tooltip("Cuanto se agranda la nave antes de estallar (1 = no se agranda).")]
    [SerializeField] private float deathSwellScale = 1.15f;
    [Tooltip("Cantidad de pulsos (agranda/achica) antes de estallar; estalla en el ultimo pico.")]
    [SerializeField] private int deathSwellPulses = 3;
    [Tooltip("Duracion de la secuencia de pulsos previa a la explosion, en segundos.")]
    [SerializeField] private float deathSwellDuration = 0.5f;

    [Header("Separacion")]
    [Tooltip("Radio de evitacion entre naves como fraccion del radio real del casco (1 = cascos apenas sin tocarse).")]
    [SerializeField] private float avoidanceRadiusFactor = 1f;

    protected NavMeshAgent agent;
    protected EnemyState currentState { get; private set; } = EnemyState.Idle;
    protected Transform player { get; private set; }
    protected bool IsLured => lurePosition.HasValue;
    public EnemyState CurrentState => currentState;

    /// <summary>
    /// Direccion horizontal hacia la que apunta el morro de la nave (modelForward
    /// proyectado sobre el plano XZ). Nunca devuelve un vector de mundo constante:
    /// si el eje configurado colapsa a vertical prueba ejes locales alternativos,
    /// luego la velocidad real del agente y, si nada da, Vector3.zero (no girar).
    /// Asi un modelForward mal configurado jamas produce giro infinito.
    /// </summary>
    protected Vector3 ForwardFlat
    {
        get
        {
            Vector3 flat = ProjectFlat(modelForward);
            if (flat.sqrMagnitude >= 0.0001f)
            {
                return flat;
            }

            Vector3[] fallbackAxes = { Vector3.forward, Vector3.up, Vector3.right };
            foreach (Vector3 axis in fallbackAxes)
            {
                flat = ProjectFlat(axis);
                if (flat.sqrMagnitude >= 0.0001f)
                {
                    return flat;
                }
            }

            if (agent != null)
            {
                flat = agent.velocity;
                flat.y = 0f;
                if (flat.sqrMagnitude >= 0.0001f)
                {
                    return flat.normalized;
                }
            }

            return Vector3.zero;
        }
    }

    private Vector3 ProjectFlat(Vector3 localAxis)
    {
        Vector3 world = transform.TransformDirection(localAxis);
        world.y = 0f;
        return world.sqrMagnitude >= 0.0001f ? world.normalized : Vector3.zero;
    }

    private static TankController cachedPlayer;

    private GameObject playerStyleProjectile;
    private GameObject playerStyleImpactVfx;
    private Vector3 aimSample;
    private float aimSampleTime;
    private bool hasAimSample;

    private float currentHealth;
    private float nextAttackTime;
    private float nextContactTime;
    private Vector3? lurePosition;
    private float sinkEndTime;
    private bool sinkStarted;
    private Vector3 deathStartScale;
    private bool hoverChecked;
    private float hullRadius;
    private Bounds hullBounds;

    /// <summary>
    /// Radio horizontal del casco en unidades de mundo, medido sobre los bounds
    /// reales de los renderers. Es la referencia de escala del juego: los FBX
    /// vienen con escala raiz de 100~250 (tanque ~200u, naves ~290u), asi que
    /// todo lo relativo (rangos, tamaño de bala, boca del cañon) se deriva de
    /// aca en vez de usar numeros absolutos que quedan en cualquier escala.
    /// </summary>
    protected float HullRadius()
    {
        return Mathf.Max(hullRadius, 0.0001f);
    }

    /// <summary>
    /// Bounds del casco en unidades de mundo (mismo origen que HullRadius).
    /// </summary>
    protected Bounds HullBounds()
    {
        return hullBounds;
    }

    protected float DetectionRange()
    {
        return detectionRange > 0f ? detectionRange : HullRadius() * Mathf.Max(detectionRangeFactor, 0f);
    }

    protected float AttackRange()
    {
        return attackRange > 0f ? attackRange : HullRadius() * Mathf.Max(attackRangeFactor, 0f);
    }

    /// <summary>
    /// Diametro de la bala en unidades de mundo.
    /// </summary>
    protected float ProjectileDiameter()
    {
        if (projectileScale > 0f)
        {
            return projectileScale;
        }
        return HullRadius() * Mathf.Max(projectileSizeFactor, 0f);
    }

    /// <summary>
    /// Velocidad para que la bala llegue al objetivo en flightTime segundos.
    /// Con velocidad fija a 90~150 u/s la bala cruzaba los 30u de attackRange en
    /// 0.2s y no se veía casi nada; normalizando el tiempo de vuelo se ve siempre
    /// el mismo tiempo, sin importar la distancia ni la escala del modelo.
    /// </summary>
    protected float ProjectileSpeedTo(Vector3 from, Vector3 to)
    {
        if (projectileSpeed > 0f)
        {
            return projectileSpeed;
        }
        float seconds = Mathf.Max(flightTime, 0.05f);
        return Mathf.Max(Vector3.Distance(from, to) / seconds, 0.0001f);
    }

    /// <summary>
    /// Distancia del centro hasta la boca del cañon en esa direccion. Con el valor
    /// manual en 0 se usa el punto de apoyo del AABB del casco mas un margen: la
    /// bala nace siempre fuera del hull. Adentro, la fisica la expulsa en cualquier
    /// direccion y EnemyProjectile la destruye al primer contacto con cualquier cosa.
    /// </summary>
    protected float MuzzleDistanceAlong(Vector3 direction, float manualDistance)
    {
        if (manualDistance > 0f)
        {
            return manualDistance;
        }

        Vector3 extents = HullBounds().extents;
        Vector3 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        float support = Mathf.Abs(dir.x) * extents.x
                      + Mathf.Abs(dir.y) * extents.y
                      + Mathf.Abs(dir.z) * extents.z;

        return support * (1f + Mathf.Max(muzzleMarginFactor, 0f));
    }

    /// <summary>
    /// Escala del VFX de impacto para que mida impactVfxDiameterFactor veces la bala.
    /// Misma formula que CannonController.ImpactVfxScale, para que el prefab del
    /// tanque y el del enemigo den exactamente la misma nube.
    /// </summary>
    protected float ImpactVfxScale(float shellDiameter)
    {
        float reference = Mathf.Max(impactVfxReferenceDiameter, 0.0001f);
        float ratio = shellDiameter * Mathf.Max(impactVfxDiameterFactor, 0f) / reference;
        return ratio * Mathf.Max(impactVfxScale, 0f);
    }

    /// <summary>
    /// Diametro en unidades de mundo de un objeto recien instanciado, medido sobre
    /// sus colliders (ya incluye la escala local que traiga el prefab). Permite
    /// corregir por multiplicacion en vez de pisar localScale.
    /// </summary>
    protected static float WorldDiameter(GameObject target)
    {
        float max = 0f;
        foreach (Collider other in target.GetComponentsInChildren<Collider>())
        {
            Vector3 size = other.bounds.size;
            max = Mathf.Max(max, Mathf.Max(size.x, Mathf.Max(size.y, size.z)));
        }
        return max;
    }

    /// <summary>
    /// <summary>
    /// Punto al que apuntarle al player: el centro de sus renderers, igual que
    /// HullBounds() para los enemigos. No se usa el CharacterController porque su
    /// centro se guarda en unidades locales del player y el tanque tiene el modelo
    /// corrido 4.35u del pivote: con el CharacterController la nave apuntaba al
    /// aire. Se recalcula en cada disparo (cada 3s) porque el tanque se mueve:
    /// cachear el punto en mundo lo dejaba viejo.
    /// </summary>
    protected Vector3 PlayerAimPoint()
    {
        if (player == null)
        {
            return transform.position;
        }

        Bounds bounds = PlayerBounds();
        if (bounds.size.sqrMagnitude > 0.0001f)
        {
            return bounds.center;
        }

        return PlayerControllerCenter();
    }

    /// <summary>
    /// Bounds del player en unidades de mundo, o un rect vacio si no tiene renderers
    /// (recien instanciado, o con los modelos apagados).
    /// </summary>
    protected Bounds PlayerBounds()
    {
        Bounds bounds = default;
        bool found = false;

        if (player == null)
        {
            return bounds;
        }

        foreach (Renderer renderer in player.GetComponentsInChildren<Renderer>())
        {
            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return bounds;
    }

    private Vector3 PlayerControllerCenter()
    {
        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller == null)
        {
            return player.position;
        }

        float scale = Mathf.Max(Mathf.Abs(player.lossyScale.y), 0.0001f);
        return player.position + player.up * (controller.center.y + controller.height * 0.5f) * scale;
    }

    /// <summary>
    /// Centro horizontal del casco a la altura real de los renderers. El pivote del
    /// enemigo esta apoyado en el navmesh, asi que sin esto la boca del cañon
    /// nace varias unidades por debajo del centro de la nave.
    /// </summary>
    protected Vector3 HullCenter()
    {
        Bounds bounds = HullBounds();
        return bounds.size.sqrMagnitude > 0.0001f ? bounds.center : transform.position;
    }

    protected virtual void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.updateRotation = !rotateTowardPlayer;
        // updateUpAxis es independiente de updateRotation: en true el agente re-alinea
        // el up del transform a la normal de la superficie cada frame, y sobre una
        // ladera del navmesh tumba la nave de costado y la deja volteada. Las naves
        // siempre van con el up al mundo; la inclinacion la aporta el modelo.
        agent.updateUpAxis = false;
        agent.baseOffset = HoverToBaseOffset(hoverHeight);
        player = FindPlayer();
        currentHealth = maxHealth;
        FitColliderToMesh();
        // El radio de evitacion del agente multiplica por la escala raiz (misma
        // regla que HoverToBaseOffset); con los valores de prefab (~0.4u de mundo)
        // las naves se veian como puntos y se atravesaban. Con el radio real del
        // casco la evitacion built-in las hace curvar y rodearse.
        float rootScale = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z), 0.0001f);
        agent.radius = HullRadius() * Mathf.Max(avoidanceRadiusFactor, 0f) / rootScale;
        PlaceOnNavMesh();
        ResolvePlayerStyleAssets();
    }

    /// <summary>
    /// Prefabs con los que dispara este enemigo. Si los campos del Inspector estan
    /// vacios se toman del cañon del tanque (CannonController), para que la nave
    /// use el mismo prefab de bala y el mismo VFX de impacto sin tener que cablearlos
    /// uno por uno en cada prefab enemigo. Lo explicito del Inspector manda.
    /// </summary>
    private void ResolvePlayerStyleAssets()
    {
        playerStyleProjectile = projectilePrefab;
        playerStyleImpactVfx = impactVfxPrefab;

        if (playerStyleProjectile != null && playerStyleImpactVfx != null)
        {
            return;
        }

        if (cachedPlayer == null)
        {
            return;
        }

        CannonController cannon = cachedPlayer.GetComponentInChildren<CannonController>();
        if (cannon == null)
        {
            return;
        }

        if (playerStyleProjectile == null)
        {
            playerStyleProjectile = cannon.ProjectilePrefab;
        }
        if (playerStyleImpactVfx == null)
        {
            playerStyleImpactVfx = cannon.ImpactVfxPrefab;
        }
    }

    /// <summary>
    /// El NavMeshAgent multiplica radius/height/baseOffset por la escala del
    /// transform (por eso el radio de nave02 es 0.003 y no 0.5). Los enemigos
    /// vienen de FBX con escala raiz enorme (nave02 ~180), asi que asignar el
    /// hoverHeight tal cual lo convierte en unidades de mundo: 1.5 x 180.43 =
    /// 270u de altura y la nave se va volando. Convertimos de unidades de
    /// mundo (que es lo que dice el tooltip) a unidades locales del agente.
    /// </summary>
    private float HoverToBaseOffset(float worldHover)
    {
        if (worldHover <= 0f)
        {
            return 0f;
        }

        float scale = Mathf.Max(Mathf.Abs(transform.lossyScale.y), 0.0001f);
        return worldHover / scale;
    }

    /// <summary>
    /// Chequea una sola vez que la altura real sobre el navmesh sea la pedida.
    /// Si vuelve a dispararse el desfase es la escala del agente, no el navmesh.
    /// </summary>
    private void VerifyHoverOffset()
    {
        hoverChecked = true;

        if (hoverHeight <= 0f || agent == null || !agent.isOnNavMesh)
        {
            return;
        }

        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5f, agent.areaMask))
        {
            return;
        }

        float real = transform.position.y - hit.position.y;
        if (Mathf.Abs(real - hoverHeight) > 1f)
        {
            Debug.LogWarning(
                $"[EnemyTemplate] {name}: hoverHeight {hoverHeight:F1} quedo en " +
                $"{real:F1}u sobre el navmesh (baseOffset {agent.baseOffset} x escala " +
                $"{transform.lossyScale.y:F1}).", this);
        }
    }

    protected virtual void Update()
    {
        if (!hoverChecked)
        {
            VerifyHoverOffset();
        }

        if (player == null)
        {
            return;
        }

        switch (currentState)
        {
            case EnemyState.Idle: UpdateIdleState(); break;
            case EnemyState.Chase: UpdateChaseState(); break;
            case EnemyState.Attack: UpdateAttackState(); break;
            case EnemyState.Death: UpdateDeathState(); break;
        }
    }

    public void TakeDamage(float damage)
    {
        if (currentState == EnemyState.Death)
        {
            return;
        }

        currentHealth -= Mathf.Max(0f, damage);
        Debug.Log($"[{name}] recibio {damage} de daño. Vida: {currentHealth}", this);

        if (currentHealth <= 0f)
        {
            currentState = EnemyState.Death;
        }
    }

    public void SetLurePosition(Vector3 position)
    {
        lurePosition = position;
    }

    public void ClearLure()
    {
        lurePosition = null;
    }

    protected bool CanAttack()
    {
        if (Time.time < nextAttackTime)
        {
            return false;
        }
        nextAttackTime = Time.time + attackCooldown;
        return true;
    }

    protected void ApplyDamageToPlayer(float amount)
    {
        if (player == null)
        {
            return;
        }

        Collider collider = player.GetComponentInChildren<Collider>();
        IDamagable damagable = player.GetComponent<IDamagable>();
        if (damagable == null && collider != null)
        {
            damagable = collider.GetComponent<IDamagable>();
        }
        damagable?.TakeDamage(amount);
    }

    /// <summary>
    /// Crea la bala enemiga. El tamaño y la velocidad no se reciben: se derivan del
    /// casco y de la distancia al objetivo, asi ningun cañon puede disparar con
    /// numeros absolutos que no correspondan a la escala de la escena.
    /// </summary>
    protected GameObject SpawnEnemyProjectile(Vector3 position, Vector3 direction, Vector3 target, float damage)
    {
        GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        projectile.name = "EnemyProjectile";
        projectile.transform.localScale = Vector3.one * ProjectileDiameter();
        projectile.transform.SetPositionAndRotation(
            position,
            Quaternion.LookRotation(direction.normalized, Vector3.up));

        Collider collider = projectile.GetComponent<Collider>();
        if (collider != null)
        {
            collider.isTrigger = false;

            // La bala no puede chocar con el casco del enemigo que la dispara: si
            // nace pegada al hull la física la expulsa en cualquier dirección y
            // EnemyProjectile la destruye al primer contacto con cualquier cosa.
            foreach (Collider own in GetComponentsInChildren<Collider>())
            {
                Physics.IgnoreCollision(collider, own, true);
            }
        }

        Rigidbody body = projectile.GetComponent<Rigidbody>();
        if (body == null)
        {
            body = projectile.AddComponent<Rigidbody>();
        }
        body.useGravity = false;
        body.isKinematic = false;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        EnemyProjectile script = projectile.AddComponent<EnemyProjectile>();
        script.damage = damage;
        script.Launch(direction, ProjectileSpeedTo(position, target));

        return projectile;
    }

    /// <summary>
    /// Crea la bala exactamente igual a la del tanque: mismo prefab, mismo escalado
    /// por medicion del clon, mismo VFX de impacto y mismo tiempo de vuelo. Lo unico
    /// que cambia es que sale con playerOnly, asi una nave no lastima a las otras ni
    /// se mata con su propio disparo.
    /// Si no hay prefab de bala disponible cae en SpawnEnemyProjectile (esfera simple
    /// sin VFX), para que un enemigo mal configurado siga disparando igual.
    /// </summary>
    protected GameObject SpawnPlayerStyleProjectile(Vector3 position, Vector3 direction, Vector3 target, float damage)
    {
        if (playerStyleProjectile == null)
        {
            return SpawnEnemyProjectile(position, direction, target, damage);
        }

        Vector3 shot = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        GameObject projectile = Instantiate(playerStyleProjectile);
        projectile.name = "EnemyShell";

        float diameter = ProjectileDiameter();
        float current = WorldDiameter(projectile);
        projectile.transform.localScale *= current > 0.0001f ? diameter / current : 1f;

        projectile.transform.SetPositionAndRotation(position, LookRotationSafe(shot));

        Collider collider = projectile.GetComponent<Collider>();
        if (collider != null)
        {
            collider.isTrigger = false;

            foreach (Collider own in GetComponentsInChildren<Collider>())
            {
                Physics.IgnoreCollision(collider, own, true);
            }
        }

        Rigidbody body = projectile.GetComponent<Rigidbody>();
        if (body == null)
        {
            body = projectile.AddComponent<Rigidbody>();
        }
        // El prefab del tanque viene con useGravity en true: sin esto la bala enemiga
        // se cae al piso en vez de ir recta.
        body.useGravity = false;
        body.isKinematic = false;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.linearVelocity = shot * ProjectileSpeedTo(position, target);

        Projectile script = projectile.GetComponent<Projectile>();
        if (script == null)
        {
            script = projectile.AddComponent<Projectile>();
        }
        script.damage = damage;
        script.playerOnly = true;
        script.impactVfx = playerStyleImpactVfx;
        script.impactVfxScale = ImpactVfxScale(diameter);

        return projectile;
    }

    /// <summary>
    /// Quaternion.LookRotation elige mal el up cuando la direccion es casi vertical
    /// (tira null reference). Aca se cambia el up de referencia en ese caso.
    /// </summary>
    protected static Quaternion LookRotationSafe(Vector3 direction)
    {
        Vector3 normalized = direction.normalized;
        Vector3 up = Mathf.Abs(Vector3.Dot(normalized, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up;
        return Quaternion.LookRotation(normalized, up);
    }

    private void UpdateIdleState()
    {
        if (IsLured)
        {
            currentState = EnemyState.Chase;
            SetStopped(false);
            return;
        }

        if (Vector3.Distance(transform.position, TargetPosition()) <= DetectionRange())
        {
            currentState = EnemyState.Chase;
            SetStopped(false);
        }
    }

    private void UpdateChaseState()
    {
        if (IsLured)
        {
            TrySetDestination(TargetPosition());
            SetStopped(false);
            return;
        }

        float distance = Vector3.Distance(transform.position, TargetPosition());

        if (distance > DetectionRange())
        {
            currentState = EnemyState.Idle;
            SetStopped(true);
        }
        else if (distance <= AttackRange())
        {
            currentState = EnemyState.Attack;
            SetStopped(true);
        }
        else
        {
            TrySetDestination(TargetPosition());
            SetStopped(false);
        }

        RotateIfNeeded(TargetPosition());
    }

    private void UpdateAttackState()
    {
        if (IsLured)
        {
            currentState = EnemyState.Chase;
            SetStopped(false);
            return;
        }

        float distance = Vector3.Distance(transform.position, TargetPosition());

        if (distance > DetectionRange())
        {
            currentState = EnemyState.Idle;
            SetStopped(true);
            return;
        }

        if (distance > AttackRange())
        {
            currentState = EnemyState.Chase;
            return;
        }

        RotateIfNeeded(TargetPosition());
        TryContactDamage(distance);
        AttackTick();
    }

    protected virtual void AttackTick()
    {
    }

    private void UpdateDeathState()
    {
        if (!sinkStarted)
        {
            sinkStarted = true;
            agent.enabled = false;
            sinkEndTime = Time.time + deathSwellDuration;
            deathStartScale = transform.localScale;
            SetCollidersEnabled(false);
            OnDeathStarted();
        }

        float duration = Mathf.Max(deathSwellDuration, 0.0001f);
        float t = Mathf.Clamp01(1f - (sinkEndTime - Time.time) / duration);

        if (Time.time < sinkEndTime)
        {
            float pulses = Mathf.Max(deathSwellPulses, 1);
            float pulse = Mathf.Abs(Mathf.Sin(t * Mathf.PI * (pulses - 0.5f)));
            float amplitude = Mathf.Lerp(0.6f, 1f, t);
            transform.localScale = deathStartScale * Mathf.Lerp(1f, deathSwellScale, pulse * amplitude);
        }
        else
        {
            SpawnDeathExplosion();
            Destroy(gameObject);
        }
    }

    private void SpawnDeathExplosion()
    {
        GameObject deathVfx = deathVfxPrefab != null
            ? deathVfxPrefab
            : Resources.Load<GameObject>("DeathVFX");
        if (deathVfx == null)
        {
            deathVfx = playerStyleImpactVfx;
        }
        if (deathVfx == null)
        {
            return;
        }

        Bounds live = LiveHullBounds();
        Vector3 deathPos = live.size.sqrMagnitude > 0.0001f ? live.center : transform.position;
        float hullR = HullRadius();
        float deathScale = ImpactVfxScale(hullR * 2f) * deathVfxScaleMultiplier;
        int count = Mathf.Max(deathVfxExplosionCount, 1);

        for (int i = 0; i < count; i++)
        {
            Vector3 offset = Vector3.zero;
            if (i > 0)
            {
                Vector2 circle = Random.insideUnitCircle * hullR * deathVfxExplosionSpread;
                offset = new Vector3(circle.x, Random.Range(-0.5f, 0.5f) * hullR * deathVfxExplosionSpread, circle.y);
            }

            float delay = i * deathVfxExplosionDelay;
            float scale = deathScale * (i == 0 ? 1f : 0.8f);
            GameObject vfx = Instantiate(deathVfx, deathPos + offset, Quaternion.identity);

            foreach (ParticleSystem ps in vfx.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = ps.main;
                main.startDelay = delay;
                main.startSizeMultiplier *= scale;

                ParticleSystem.EmissionModule emission = ps.emission;
                emission.rateOverTimeMultiplier *= deathVfxParticleMultiplier;
                emission.rateOverDistanceMultiplier *= deathVfxParticleMultiplier;
                for (int b = 0; b < emission.burstCount; b++)
                {
                    ParticleSystem.Burst burst = emission.GetBurst(b);
                    float baseCount = Mathf.Max(burst.count.constant, burst.count.constantMin, 1f);
                    float boosted = baseCount * deathVfxParticleMultiplier;
                    burst.count = new ParticleSystem.MinMaxCurve(boosted, boosted);
                    emission.SetBurst(b, burst);
                }
            }

            Debug.Log($"[{name}] DeathVFX {i + 1}/{count} spawn={vfx.name} pos={vfx.transform.position} hullR={hullR:F2} scale={scale:F2} delay={delay:F2}", this);
            Destroy(vfx, 4f + delay);
        }
    }

    /// <summary>Hook para que GameManager (Etapa D) cuente bajas sin acoplarse. </summary>
    protected virtual void OnDeathStarted()
    {
    }

    /// <summary>
    /// Dibuja el morro resuelto (modelForward proyectado al piso), el wire del casco
    /// y un punto en el centro de cada renderer hijo. Con modelForward mal puesto
    /// (estos FBX entran espejados en X, asi que el morro real puede ser el eje
    /// contrario) la nave persigue al player pero se ve de espaldas; esto lo
    /// muestra en 2 segundos en vez de adivinarlo.
    /// Mide los bounds al vuelo porque hullBounds/hullRadius solo se llenan en
    /// Start(), y el gizmo tiene que servir con el Editor cerrado.
    /// </summary>
    protected void OnDrawGizmosSelected()
    {
        Vector3 nose = ForwardFlat;
        if (nose.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Bounds bounds = GizmoHullBounds();
        float radius = Mathf.Max(bounds.extents.x, bounds.extents.z);
        if (radius < 0.0001f)
        {
            return;
        }

        Gizmos.color = new Color(1f, 1f, 0f, 0.35f);
        Gizmos.DrawWireCube(bounds.center, bounds.size);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(bounds.center, bounds.center + nose * radius * 1.4f);
        Gizmos.DrawWireSphere(bounds.center + nose * radius * 1.4f, radius * 0.08f);

        Gizmos.color = Color.cyan;
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
        {
            Gizmos.DrawWireSphere(renderer.bounds.center, radius * 0.05f);
        }
    }

    private Bounds LiveHullBounds()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return new Bounds(transform.position, Vector3.one);
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    private Bounds GizmoHullBounds()
    {
        if (Application.isPlaying && hullBounds.size.sqrMagnitude > 0.0001f)
        {
            return hullBounds;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return new Bounds(transform.position, Vector3.one);
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    private void TryContactDamage(float distance)
    {
        // 40u de contacto absolutos quedan mas adentro que el propio casco
        // (radio ~145u), asi que el contacto se mide contra el rango de ataque.
        float reach = contactRange > 0f ? contactRange : AttackRange();

        if (!contactDamage || distance > reach || Time.time < nextContactTime)
        {
            return;
        }

        nextContactTime = Time.time + contactCooldown;
        ApplyDamageToPlayer(damageAmount);
    }

    private void RotateIfNeeded(Vector3 target)
    {
        if (!rotateTowardPlayer)
        {
            return;
        }

        Vector3 direction = target - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }
        direction.Normalize();

        Vector3 current = ForwardFlat;
        if (current.sqrMagnitude < 0.0001f)
        {
            return;
        }
        current.Normalize();

        float angle = Vector3.SignedAngle(current, direction, Vector3.up);
        float step = Mathf.Clamp(angle, -rotateSpeed * Time.deltaTime, rotateSpeed * Time.deltaTime);
        if (Mathf.Abs(step) < 0.0001f)
        {
            return;
        }
        transform.Rotate(Vector3.up, step, Space.World);
    }

    /// Punto al que se acerca y rota la nave: el CUERPO del player (centro de sus
    /// bounds), no su pivote. El tanque tiene el FBX del modelo corrido 4.35u del
    /// origen (m_LocalPosition 3.4359/0.01/2.667 en el Player), asi que apuntar al
    /// player.position hacia que la nave le apuntaba al aire: con el tanque a 4.5u
    /// de la boca eso son 40 grados de error de rumbo y ningun tiro llegaba.
    /// Tambien corrige los rangos de deteccion/ataque/contacto, que midian hasta la
    /// pivote. La trampa de miel manda una posicion explicita y manda sobre todo.
    /// </summary>
    /// Anticipa donde va a estar el player cuando llegue la bala.
    ///
    /// El tiempo de vuelo esta normalizado (ProjectileSpeedTo = distancia /
    /// flightTime), asi que un disparo siempre tarda ~1s en llegar. Sin adelante la
    /// nave apunta a donde el tanque ESTABA y falla practicamente siempre contra un
    /// blanco que se mueve: el tanque mide 2.4u y en 1.05s recorre varios cuerpos.
    /// Se estima la velocidad del player con dos muestras consecutivas del punto
    /// apuntado (el tanque va con CharacterController, no hay Rigidbody que leer).
    ///
    /// No-defauda el flanqueo: si el tanque se mueve para sacarse de la linea de
    /// tiro, el adelanto lo apunta a donde se va a sacar igual.
    /// </summary>
    protected Vector3 LeadAimPoint(float seconds)
    {
        Vector3 aim = PlayerAimPoint();

        if (seconds > 0.05f && hasAimSample)
        {
            float dt = Time.time - aimSampleTime;
            if (dt >= 0.05f && dt <= 3f)
            {
                Vector3 velocity = (aim - aimSample) / dt;
                return aim + velocity * seconds;
            }
        }

        aimSample = aim;
        aimSampleTime = Time.time;
        hasAimSample = true;
        return aim;
    }

    private Vector3 TargetPosition()
    {
        if (lurePosition.HasValue)
        {
            return lurePosition.Value;
        }
        return player != null ? PlayerAimPoint() : transform.position;
    }

    /// <summary>
    /// Coloca al agente sobre el navmesh mas cercano a su spawn si este cayo
    /// fuera (por escalas/posiciones manuales). Evita "SetDestination can
    /// only be called on an active agent that has been placed on a NavMesh".
    /// </summary>
    private void PlaceOnNavMesh()
    {
        if (agent == null || agent.isOnNavMesh)
        {
            return;
        }

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 50f, agent.areaMask))
        {
            if (Mathf.Abs(hit.position.y - transform.position.y) <= 20f)
            {
                agent.Warp(hit.position);
            }
            else
            {
                Debug.LogWarning($"[EnemyTemplate] {name} descarto punto del navmesh demasiado alto (Y={hit.position.y:F1}).", this);
            }
        }
        else
        {
            Debug.LogWarning($"[EnemyTemplate] {name} no encontro navmesh cerca del spawn.", this);
        }
    }

    private bool TrySetDestination(Vector3 position)
    {
        return agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh && agent.SetDestination(position);
    }

    private void SetStopped(bool stopped)
    {
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
        {
            return;
        }
        agent.isStopped = stopped;
    }

    private void SetCollidersEnabled(bool enabled)
    {
        foreach (Collider collider in GetComponentsInChildren<Collider>())
        {
            collider.enabled = enabled;
        }
    }

    /// <summary>
    /// Redimensiona el collider del enemigo para que cubra el mesh visible.
    /// Evita que las balas atraviesen el casco cuando el collider quedo
    /// chico por escalas del modelo/prefab.
    /// </summary>
    private void FitColliderToMesh()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return;
        }

        Bounds worldBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            worldBounds.Encapsulate(renderers[i].bounds);
        }

        // Cacheado antes de tocar el collider: es la referencia de escala que usan
        // rangos, proyectiles y bocas de cañon.
        hullBounds = worldBounds;
        hullRadius = Mathf.Max(worldBounds.extents.x, worldBounds.extents.z);

        Collider collider = GetComponent<Collider>();
        if (collider == null)
        {
            return;
        }

        Vector3 localCenter = transform.InverseTransformPoint(worldBounds.center);
        Vector3 localSize = new Vector3(
            worldBounds.size.x / Mathf.Max(transform.lossyScale.x, 0.0001f),
            worldBounds.size.y / Mathf.Max(transform.lossyScale.y, 0.0001f),
            worldBounds.size.z / Mathf.Max(transform.lossyScale.z, 0.0001f));

        if (collider is BoxCollider box)
        {
            box.center = localCenter;
            box.size = localSize;
        }
        else if (collider is SphereCollider sphere)
        {
            sphere.center = localCenter;
            sphere.radius = Mathf.Max(localSize.x, Mathf.Max(localSize.y, localSize.z)) * 0.5f;
        }
    }

    private static Transform FindPlayer()
    {
        if (cachedPlayer == null)
        {
            cachedPlayer = FindFirstObjectByType<TankController>();
        }
        if (cachedPlayer != null)
        {
            return cachedPlayer.transform;
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        return playerObject != null ? playerObject.transform : null;
    }
}