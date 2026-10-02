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
    [Tooltip("Velocidad de los proyectiles en unidades por segundo. 0 = se deriva del tiempo de vuelo.")]
    [SerializeField] private float projectileSpeed = 0f;
    [Tooltip("Diametro de los proyectiles enemigos en unidades de mundo. 0 = se deriva del casco.")]
    [SerializeField] private float projectileScale = 0f;

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
    [SerializeField] private float sinkSpeed = 4f;
    [SerializeField] private float sinkDuration = 1.5f;

    protected NavMeshAgent agent;
    protected EnemyState currentState { get; private set; } = EnemyState.Idle;
    protected Transform player { get; private set; }
    protected bool IsLured => lurePosition.HasValue;

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

    private float currentHealth;
    private float nextAttackTime;
    private float nextContactTime;
    private Vector3? lurePosition;
    private Vector3 sinkStart;
    private float sinkEndTime;
    private bool sinkStarted;
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
        PlaceOnNavMesh();
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

    private void UpdateIdleState()
    {
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
            sinkStart = transform.position;
            sinkEndTime = Time.time + sinkDuration;
            SetCollidersEnabled(false);
            OnDeathStarted();
        }

        if (Time.time < sinkEndTime)
        {
            transform.position = sinkStart + Vector3.down * (sinkSpeed * (Time.time - (sinkEndTime - sinkDuration)));
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>Hook para que GameManager (Etapa D) cuente bajas sin acoplarse. </summary>
    protected virtual void OnDeathStarted()
    {
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

    private Vector3 TargetPosition()
    {
        if (lurePosition.HasValue)
        {
            return lurePosition.Value;
        }
        return player != null ? player.position : transform.position;
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