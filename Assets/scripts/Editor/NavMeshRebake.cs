using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Re-hornea las NavMeshSurface de las escenas abiertas y loguea los bounds
/// resultantes, para verificar en numero (y no a ojo) que el navmesh quedo
/// plano sobre el piso. Es el equivalente al click en "Bake" del inspector,
/// pero sobre todas las superficies de golpe y escribiendo el asset a disco.
///
/// Uso: Tools > Honeypot > ReBake NavMesh
///
/// NO usa InitializeOnLoad ni toca la jerarquia: solo BuildNavMesh + SaveAssets.
/// </summary>
public static class NavMeshRebake
{
    private const float MaxFlatHeightSpan = 10f;

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

        foreach (NavMeshSurface surface in surfaces)
        {
            Bake(surface);
        }

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

        surface.BuildNavMesh();

        if (surface.navMeshData == null)
        {
            Debug.LogError($"[ReBake] {owner.name} quedo sin NavMeshData.", owner);
            return;
        }

        Bounds bounds = surface.navMeshData.sourceBounds;
        float span = bounds.max.y - bounds.min.y;

        if (span > MaxFlatHeightSpan)
        {
            Debug.LogWarning(
                $"[ReBake] {owner.name}: el navmesh NO es plano " +
                $"(Y de {bounds.min.y:F1} a {bounds.max.y:F1}, span {span:F1}). " +
                "Se colaron laderas/decor: revisar layerMask y NavMeshModifier.", owner);
        }
        else
        {
            Debug.Log(
                $"[ReBake] {owner.name}: navmesh plano en Y={bounds.center.y:F1} " +
                $"(X {bounds.min.x:F0}..{bounds.max.x:F0}, Z {bounds.min.z:F0}..{bounds.max.z:F0}).", owner);
        }
    }
}
