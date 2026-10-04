using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Re-hornea las NavMeshSurface de las escenas abiertas y verifica en numero (y no
/// a ojo) tres cosas que rompen el nivel en silencio:
///
///  1. Que el navmesh horneado quede PLANO. Si algo se sube a la montana o a un
///     decor, los enemigos quedan caminandolo en el aire.
///  2. Que la caja de horneado (Collect Objects = Volume) no este recortando
///     parte del nivel: eso se ve bien en pantalla pero no se puede recorrer.
///  3. Que los NavMeshModifier apunten al area que dicen (Not Walkable, etc).
///
/// Uso: Tools > Honeypot > ReBake NavMesh
///
/// NO usa InitializeOnLoad ni toca la jerarquia: solo BuildNavMesh + SaveAssets.
/// </summary>
public static class NavMeshRebake
{
    /// <summary>
    /// Diferencia de Y maxima tolerada dentro del navmesh horneado. El piso es
    /// plano, asi que cualquier altura extra es algo que se colaron (ladera,
    /// decor o montana mal marcada). 3m de tolerancia por el voxel size y por
    /// escalones chicos.
    /// </summary>
    private const float MaxWalkableHeightSpan = 3f;

    /// <summary>Tolerancia en metros al comparar bounds contra el volumen.</summary>
    private const float BoundsTolerance = 0.5f;

    [MenuItem("Tools/Honeypot/ReBake NavMesh")]
    public static void ReBake()
    {
        NavMeshSurface[] surfaces = Object.FindObjectsByType<NavMeshSurface>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        if (surfaces.Length == 0)
        {
            Debug.LogWarning("[ReBake] No hay NavMeshSurface en las escenas abiertas.");
            return;
        }

        ReportModifierAreas();

        foreach (NavMeshSurface surface in surfaces)
        {
            Bake(surface);
        }

        ReportBakedNavMesh(surfaces);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[ReBake] Listo, {surfaces.Length} superficie(s) horneada(s) y guardada(s).");
    }

    private static void Bake(NavMeshSurface surface)
    {
        Transform owner = surface.transform;
        string path = surface.navMeshData != null
            ? AssetDatabase.GetAssetPath(surface.navMeshData)
            : "(sin asset)";

        Debug.Log(
            $"[ReBake] {owner.name} | collect={surface.collectObjects} " +
            $"center={surface.center} size={surface.size} " +
            $"layerMask={surface.layerMask.value} -> {path}");

        ReportVolumeFit(surface);

        surface.BuildNavMesh();

        if (surface.navMeshData == null)
        {
            Debug.LogError($"[ReBake] {owner.name} quedo sin NavMeshData.", owner);
        }
    }

    /// <summary>
    /// El check de "plano" tiene que mirar el navmesh YA HORNEADO, no sourceBounds.
    /// sourceBounds es la caja de los objetosviously recogidos: si la superficie
    /// es enorme, sourceBounds.span da Alto aunque el navmesh este impecable en
    /// el piso. La triangulacion del NavMesh es lo que de verdad caminan las
    /// naves, asi que ahi se ve si algo quedo flotando en altura.
    /// </summary>
    private static void ReportBakedNavMesh(NavMeshSurface[] surfaces)
    {
        NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();

        if (triangulation.vertices == null || triangulation.vertices.Length == 0)
        {
            Debug.LogError(
                "[ReBake] El navmesh horneado quedo VACIO. Suele ser Collect Objects mal " +
                "configurado, un layerMask que no incluye el piso, o un area marcada como " +
                "Not Walkable en todo.");
            return;
        }

        float minY = float.MaxValue;
        float maxY = float.MinValue;
        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minZ = float.MaxValue;
        float maxZ = float.MinValue;

        foreach (Vector3 vertex in triangulation.vertices)
        {
            if (vertex.y < minY) minY = vertex.y;
            if (vertex.y > maxY) maxY = vertex.y;
            if (vertex.x < minX) minX = vertex.x;
            if (vertex.x > maxX) maxX = vertex.x;
            if (vertex.z < minZ) minZ = vertex.z;
            if (vertex.z > maxZ) maxZ = vertex.z;
        }

        float spanY = maxY - minY;

        Debug.Log(
            $"[ReBake] NavMesh horneado: {triangulation.vertices.Length} vertices, " +
            $"Y {minY:F1}..{maxY:F1} (span {spanY:F1}), " +
            $"X {minX:F0}..{maxX:F0}, Z {minZ:F0}..{maxZ:F0}.");

        if (spanY > MaxWalkableHeightSpan)
        {
            Debug.LogWarning(
                $"[ReBake] El navmesh NO es plano: hay superficie caminable entre Y={minY:F1} " +
                $"y Y={maxY:F1} (span {spanY:F1} > {MaxWalkableHeightSpan:F1}). " +
                "Algo quedo horneado en altura: montana o decor sin marcar como Not Walkable. " +
                "Para un limite que no se pueda cruzar: NavMeshModifier con Override Area = " +
                "Not Walkable (NO Ignore From Build, que lo deja invisible para las naves).");
        }
        else
        {
            Debug.Log(
                $"[ReBake] NavMesh plano en Y={maxY:F1}: no quedo superficie caminable en altura, " +
                "asi que nada puede trepar el limite.");
        }
    }

    /// <summary>
    /// Loguea el area real de cada NavMeshModifier, para confirmar por nombre y no
    /// por indice que area 1 sea efectivamente Not Walkable.
    /// </summary>
    private static void ReportModifierAreas()
    {
        NavMeshModifier[] modifiers = Object.FindObjectsByType<NavMeshModifier>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        if (modifiers.Length == 0)
        {
            Debug.LogWarning(
                "[ReBake] No hay NavMeshModifier: nada esta excluido ni marcado como " +
                "Not Walkable, todo lo que se ve se puede caminar.");
            return;
        }

        foreach (NavMeshModifier modifier in modifiers)
        {
            string area = modifier.overrideArea
                ? $"{modifier.area} = '{AreaName(modifier.area)}'"
                : $"default ({AreaName(0)})";

            Debug.Log(
                $"[ReBake] NavMeshModifier '{modifier.name}': ignoreFromBuild={modifier.ignoreFromBuild} " +
                $"applyToChildren={modifier.applyToChildren} area={area}", modifier);
        }
    }

    /// <summary>
    /// Con Collect Objects = Volume, la caja center/size es lo UNICO que decide que
    /// se hornea. Si el Ground (ya agrandado) se sale de la caja, esa porcion no
    /// entra al navmesh: el suelo se ve bien pero las naves no la pueden recorrer.
    /// Comparamos los bounds en numero para no pedirle que lo mida a ojo.
    /// </summary>
    private static void ReportVolumeFit(NavMeshSurface surface)
    {
        if (surface.collectObjects != CollectObjects.Volume)
        {
            Debug.Log(
                $"[ReBake] {surface.transform.name}: collectObjects={surface.collectObjects}, " +
                "no usa caja de volumen, asi que no hay recorte por tamaño.", surface);
            return;
        }

        if (surface.useGeometry != NavMeshCollectGeometry.RenderMeshes)
        {
            Debug.LogWarning(
                $"[ReBake] {surface.transform.name}: el chequeo de recorte solo esta implementado " +
                "para Render Meshes; con Physics Colliders revisar el volumen a mano.", surface);
            return;
        }

        Transform owner = surface.transform;
        Vector3 scale = owner.lossyScale;
        Vector3 size = Vector3.Scale(surface.size, Abs(scale));
        Bounds volume = new Bounds(owner.TransformPoint(surface.center), size);

        Debug.Log(
            $"[ReBake] {owner.name}: volumen de horneado en mundo " +
            $"X {volume.min.x:F0}..{volume.max.x:F0}, " +
            $"Y {volume.min.y:F0}..{volume.max.y:F0}, " +
            $"Z {volume.min.z:F0}..{volume.max.z:F0}.", owner);

        int candidates = 0;
        int excluded = 0;
        int inLayer = 0;
        int clippedXZ = 0;
        int clippedY = 0;
        bool hasIncluded = false;
        Bounds included = default;
        List<string> outXZ = new List<string>();

        Renderer[] renderers = Object.FindObjectsByType<Renderer>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        foreach (Renderer renderer in renderers)
        {
            candidates++;

            if (!IsInLayerMask(surface.layerMask, renderer.gameObject.layer))
            {
                continue;
            }

            inLayer++;

            if (IsExcludedFromBuild(renderer.transform, surface.agentTypeID))
            {
                excluded++;
                continue;
            }

            Bounds bounds = renderer.bounds;

            if (!hasIncluded)
            {
                included = bounds;
                hasIncluded = true;
            }
            else
            {
                included.Encapsulate(bounds);
            }

            bool fitsX = bounds.min.x >= volume.min.x - BoundsTolerance
                && bounds.max.x <= volume.max.x + BoundsTolerance;
            bool fitsZ = bounds.min.z >= volume.min.z - BoundsTolerance
                && bounds.max.z <= volume.max.z + BoundsTolerance;
            bool fitsY = bounds.min.y >= volume.min.y - BoundsTolerance
                && bounds.max.y <= volume.max.y + BoundsTolerance;

            if (!fitsX || !fitsZ)
            {
                clippedXZ++;
                outXZ.Add(
                    $"{renderer.name}(X {bounds.min.x:F0}..{bounds.max.x:F0} " +
                    $"Z {bounds.min.z:F0}..{bounds.max.z:F0})");
            }
            else if (!fitsY)
            {
                // Alto de mas no es recorte: el volumen corta la punta de la montana y
                // da igual, porque para carvar alcanza con la parte que esta a altura
                // de piso. Lo que rompe el nivel es quedarse sin ancho.
                clippedY++;
            }
        }

        if (clippedXZ > 0)
        {
            Debug.LogWarning(
                $"[ReBake] {owner.name}: {clippedXZ} objeto(s) se salen del volumen en X/Z y NO " +
                "entran al navmesh. Dos salidas: agrandar Size en el inspector, o poner " +
                "Collect Objects en All Game Objects / Current Object Hierarchy para que deje " +
                $"de depender de una caja fija. Recortados: {string.Join(", ", outXZ)}.", owner);
        }

        if (hasIncluded)
        {
            Debug.Log(
                $"[ReBake] {owner.name}: renderes activos={candidates}, en layerMask={inLayer}, " +
                $"NavMeshModifier los excluye={excluded}, fuera del volumen en X/Z={clippedXZ}, " +
                $"pasan la Y del volumen (quedan truncados, no es problema)={clippedY}. " +
                $"Union de los incluidos: X {included.min.x:F0}..{included.max.x:F0}, " +
                $"Z {included.min.z:F0}..{included.max.z:F0}.", owner);
        }
    }

    /// <summary>
    /// Traduce el indice de area a nombre. El proyecto no define areas custom en
    /// TagManager, asi que van las de Unity por defecto: 0 Walkable, 1 Not Walkable.
    /// Loguear el nombre es lo que confirma que el indice del modifier apunte a
    /// Not Walkable y no a cualquier otra cosa.
    /// </summary>
    private static string AreaName(int area)
    {
        string[] names = NavMesh.GetAreaNames();

        if (names == null || area < 0 || area >= names.Length)
        {
            return $"#{area} (fuera de rango)";
        }

        return names[area];
    }

    /// <summary>
    /// Replica la regla del horneado: un NavMeshModifier alcanza a su propio GameObject
    /// siempre, y a los hijos solo si tiene Apply To Children. Se ignora completo si el
    /// tipo de agente de la superficie no esta en su lista de agentes afectados.
    /// </summary>
    private static bool IsExcludedFromBuild(Transform target, int agentTypeID)
    {
        for (Transform current = target; current != null; current = current.parent)
        {
            NavMeshModifier modifier = current.GetComponent<NavMeshModifier>();

            if (modifier == null || !modifier.ignoreFromBuild)
            {
                continue;
            }

            if (!modifier.AffectsAgentType(agentTypeID))
            {
                continue;
            }

            if (current != target && !modifier.applyToChildren)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    /// <summary>LayerMask no tiene Contains: el chequeo es probar el bit del layer.</summary>
    private static bool IsInLayerMask(LayerMask mask, int layer)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    private static Vector3 Abs(Vector3 v)
    {
        return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }
}