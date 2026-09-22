using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public abstract class EnemyTemplate : MonoBehaviour, IDamagable
{
    [Header("Stats")]
    [Tooltip("Vida maxima del enemigo.")]
    [SerializeField] private float maxHealth = 60f;
    [Tooltip("Distancia desde la cual detecta al player y lo persigue.")]
    [SerializeField] private float detectionRange = 60f;
    [Tooltip("Distancia a la que entra en estado de ataque.")]
    [SerializeField] protected float attackRange = 30f;
    [Tooltip("Daño por golpe/proyectil.")]
    [SerializeField] protected float damageAmount = 12f;
    [SerializeField] private float attackCooldown = 1.2f;
    [Tooltip("Altura de vuelo sobre la navegacion. 0 = pegado al piso.")]
    [SerializeField] private float hoverHeight = 15f;

    [Header("Rotacion")]
    [Tooltip("Si esta activo, rota el cuerpo hacia el player (updateRotation del agente desactivada).")]
    [SerializeField] private bool rotateTowardPlayer = true;
    [SerializeField] private float rotateSpeed = 120f;
    [Tooltip("Eje local del modelo que apunta hacia el morro (forward visual de la nave).\nDefault (0,0,1). Si el modelo tiene la nariz 'parada' (tilt -90 en X), usa (0,1,0).")]
    [SerializeField] private Vector3 modelForward = Vector3.forward;

    [Header("Choque")]
    [Tooltip("Si esta activo, daña al player por contacto mientras esta en rango de ataque.")]
    [SerializeField] private bool contactDamage = false;
    [SerializeField] private float contactRange = 40f;
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
    /// proyectado sobre el plano XZ). Si el eje no da, cae a transform.forward.
    /// </summary>
    protected Vector3 ForwardFlat
    {
        get
        {
            Vector3 forward = transform.TransformDirection(modelForward);
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = transform.forward;
                forward.y = 0f;
            }
            return forward.sqrMagnitude < 0.0001f ? Vector3.forward : forward.normalized;
        }
    }

    private static TankController cachedPlayer;

    private float currentHealth;
    private float nextAttackTime;
    private float nextContactTime;
    private Vector3? lurePosition;
    private Vector3 sinkStart;
    private float sinkEndTime;
    private bool sinkStarted;

    protected virtual void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.updateRotation = !rotateTowardPlayer;
        agent.baseOffset = hoverHeight;
        player = FindPlayer();
        currentHealth = maxHealth;
        FitColliderToMesh();
    }

    protected virtual void Update()
    {
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

    protected GameObject SpawnEnemyProjectile(Vector3 position, Vector3 direction, float speed, float damage)
    {
        GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        projectile.name = "EnemyProjectile";
        projectile.transform.localScale = Vector3.one * EnemyProjectileScale();
        projectile.transform.SetPositionAndRotation(
            position,
            Quaternion.LookRotation(direction.normalized, Vector3.up));

        Collider collider = projectile.GetComponent<Collider>();
        if (collider != null)
        {
            collider.isTrigger = false;
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
        script.Launch(direction, speed);

        return projectile;
    }

    private static float EnemyProjectileScale()
    {
        return 20f;
    }

    private void UpdateIdleState()
    {
        if (Vector3.Distance(transform.position, TargetPosition()) <= detectionRange)
        {
            currentState = EnemyState.Chase;
            agent.isStopped = false;
        }
    }

    private void UpdateChaseState()
    {
        if (IsLured)
        {
            agent.SetDestination(TargetPosition());
            agent.isStopped = false;
            return;
        }

        float distance = Vector3.Distance(transform.position, TargetPosition());

        if (distance > detectionRange)
        {
            currentState = EnemyState.Idle;
            agent.isStopped = true;
        }
        else if (distance <= attackRange)
        {
            currentState = EnemyState.Attack;
            agent.isStopped = true;
        }
        else
        {
            agent.SetDestination(TargetPosition());
            agent.isStopped = false;
        }

        RotateIfNeeded(TargetPosition());
    }

    private void UpdateAttackState()
    {
        if (IsLured)
        {
            currentState = EnemyState.Chase;
            agent.isStopped = false;
            return;
        }

        float distance = Vector3.Distance(transform.position, TargetPosition());

        if (distance > detectionRange)
        {
            currentState = EnemyState.Idle;
            agent.isStopped = true;
            return;
        }

        if (distance > attackRange)
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
        if (!contactDamage || distance > contactRange || Time.time < nextContactTime)
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
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            return playerObject.transform;
        }

        if (cachedPlayer == null)
        {
            cachedPlayer = FindFirstObjectByType<TankController>();
        }
        return cachedPlayer != null ? cachedPlayer.transform : null;
    }
}