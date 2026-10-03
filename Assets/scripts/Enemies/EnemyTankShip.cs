using UnityEngine;

/// <summary>
/// Nave con un cañon fijo al frente, tipo tanque de guerra. El cuerpo entero gira
/// hacia el player, pero la bala sale en la direccion real del cañon con una
/// convergencia menor al 100%: si el tanque la adelanta y la rodea, el disparo
/// pasa de largo y hay que esquivarlo. Ese es el juego contra esta nave.
///
/// No hay gate de "solo disparo si estoy alineado": antes lo habia (aimTolerance)
/// y se combinaba con el cooldown de CanAttack, que se consumia en el intento fallido,
/// asi que cuando el morro no estaba a 4 grados la nave no disparaba y no pasaba
/// nada visible. Ahora dispara siempre y el error de punteria se paga esquivando.
/// </summary>
public class EnemyTankShip : EnemyTemplate
{
    [Header("Cañon")]
    [Tooltip("Boca del cañon. Dejalo vacio y se calcula con el tamaño del casco hacia adelante, a la altura del casco.")]
    [SerializeField] private Transform muzzle;
    [Tooltip("Distancia desde el centro hasta la boca si muzzle esta vacio. 0 = se calcula con el tamaño real del casco.")]
    [SerializeField] private float muzzleLength = 0f;
    [Tooltip("0 = la bala sale exactamente segun el cañon (rodearla la hace fallar). 1 = apunta perfecto al tanque. El valor bajo es el que hace viable el flanqueo.")]
    [Range(0f, 1f)]
    [SerializeField] private float aimConvergence = 0.2f;
    [Tooltip("Desvio aleatorio del disparo en grados, para que no salga una linea perfecta.")]
    [SerializeField] private float aimJitter = 2.5f;

    protected override void AttackTick()
    {
        if (!CanAttack() || player == null)
        {
            return;
        }

        Vector3 bodyCenter = PlayerAimPoint();
        Vector3 hullCenter = HullCenter();

        // El rumbo se resuelve primero (sin vertical) porque la boca del cañon
        // automatico se mide sobre el AABB del casco y necesita la direccion.
        Vector3 heading = HeadingToward(bodyCenter, hullCenter);
        Vector3 bore = BoreAlong(heading, hullCenter);

        // El tiempo de vuelo real se calcula antes del adelanto, porque depende de
        // como se resuelva la velocidad: con projectileSpeed absoluto (que es lo que
        // usa nave00) el vuelo es distancia/velocidad y NO el flightTime fijo. Usar
        // el fijo adelantearia de mas a cualquier distancia.
        float speed = ProjectileSpeedTo(bore, bodyCenter);
        float reach = Vector3.Distance(bore, bodyCenter);
        float flight = speed > 0.0001f ? reach / speed : FlightTime;

        // Con adelante: no se apunta a donde esta el tanque ahora sino a donde va a
        // estar cuando llegue la bala. El rumbo tambien se resuelve hacia ese punto
        // (no solo la vertical), asi el adelanto corrige el error lateral y no solo
        // que la bala no se hunda en el piso.
        Vector3 aimPoint = LeadAimPoint(flight);
        heading = HeadingToward(aimPoint, hullCenter);

        Vector3 direction = DirectionAlong(heading, bore, aimPoint);
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        direction = Quaternion.Euler(
            Random.Range(-aimJitter, aimJitter),
            Random.Range(-aimJitter, aimJitter),
            0f) * direction.normalized;

        SpawnPlayerStyleProjectile(bore, direction, aimPoint, damageAmount);

        LogShot(bore, heading, direction, aimPoint, hullCenter, speed, reach, flight);
    }

    private Vector3 BoreAlong(Vector3 heading, Vector3 hullCenter)
    {
        return muzzle != null
            ? muzzle.position
            : hullCenter + heading * MuzzleDistanceAlong(heading, muzzleLength);
    }

    /// <summary>
    /// Print de diagnostico del disparo: posiciones exactas (tanque, punto apuntado,
    /// boca) y sobre todo el angulo entre la direccion real y la que apuntaria al
    /// tanque. Si eso es 0 el tiro va derecho al punto; si es ~18 grados es la
    /// convergencia 0.2 haciendo su trabajo (el flanqueo), no un error.
    /// </summary>
    private void LogShot(Vector3 bore, Vector3 heading, Vector3 direction, Vector3 aimPoint, Vector3 hullCenter, float speed, float reach, float flight)
    {
        Vector3 toAim = aimPoint - bore;
        float deviation = toAim.sqrMagnitude > 0.0001f ? Vector3.Angle(direction, toAim) : 0f;

        Bounds tank = PlayerBounds();
        Vector3 nose = ForwardFlat;
        Vector3 body = tank.center;

        Debug.Log(
            $"[{name}] disparo principal\n" +
            $"  tanque.pivote   = {player.position}   (origen del transform)\n" +
            $"  tanque.bounds   = min {tank.min} / max {tank.max}  ({(tank.size.x):F1} x {(tank.size.y):F1} x {(tank.size.z):F1} u)\n" +
            $"  tanque.cuerpo   = {body}   (centro de los bounds)\n" +
            $"  desvio.pivote   = {(Vector2.Distance(new Vector2(player.position.x, player.position.z), new Vector2(body.x, body.z))):F2} u   <-- tiene que dar 0; si no, el rig esta corrido\n" +
            $"  casco.centro    = {hullCenter}   radio = {HullRadius():F1} u   rangoataque = {AttackRange():F1} u\n" +
            $"  boca.disparo    = {bore}   ({(muzzle != null ? "muzzle explicito" : "automatica")})\n" +
            $"  morro.resuelto  = {nose}\n" +
            $"  rumbo           = {heading}\n" +
            $"  dir.bala        = {direction}\n" +
            $"  punta.con.adel  = {aimPoint}   (adelantado {flight:F2}s)\n" +
            $"  alcance         = {reach:F1} u   velocidad = {speed:F1} u/s   vuelo = {flight:F2} s\n" +
            $"  DESVIO dir->aim = {deviation:F1} grados   (convergence = {aimConvergence}, jitter = {aimJitter})",
            this);
    }

    /// <summary>
    /// Rumbo horizontal del disparo: el del cañon (ForwardFlat) mezclado con el
    /// bearing real al tanque segun aimConvergence. Con 0 sale recto segun el cañon
    /// y flanquear la nave lo hace fallar; con 1 persigue al tanque sin error.
    /// </summary>
    private Vector3 HeadingToward(Vector3 aimPoint, Vector3 from)
    {
        Vector3 bearing = aimPoint - from;
        bearing.y = 0f;
        if (bearing.sqrMagnitude < 0.0001f)
        {
            Vector3 nose = ForwardFlat;
            return nose.sqrMagnitude > 0.0001f ? nose : Vector3.forward;
        }
        bearing.Normalize();

        Vector3 barrel = ForwardFlat;
        if (barrel.sqrMagnitude < 0.0001f)
        {
            return bearing;
        }

        return Vector3.Slerp(barrel, bearing, aimConvergence).normalized;
    }

    /// <summary>
    /// La vertical no pasa por la convergencia: siempre converge a la altura del
    /// objetivo. Si no, la bala sale plana desde la altura de la nave (que esta por
    /// encima del tanque) y le pasa por arriba sin tocarlo.
    /// </summary>
    private static Vector3 DirectionAlong(Vector3 heading, Vector3 from, Vector3 aimPoint)
    {
        float horizontal = new Vector3(aimPoint.x - from.x, 0f, aimPoint.z - from.z).magnitude;
        Vector3 direction = heading * horizontal + Vector3.up * (aimPoint.y - from.y);
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : heading;
    }
}
