using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PrototypeSceneSetup
{
    private const string PrefsKey = "Honeypot.PrototypeSetup.V5";
    private const string ImpactVfxVersion = "4";
    private const string ImpactVfxVersionKey = "Honeypot.ImpactVfx.Version";

    [InitializeOnLoadMethod]
    private static void RegisterAutoSetup()
    {
        if (EditorPrefs.GetBool(PrefsKey, false))
        {
            return;
        }

        EditorApplication.update += PollUntilReady;
    }

    private static double lastAttempt;

    private static void PollUntilReady()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.update -= PollUntilReady;
            return;
        }

        double now = EditorApplication.timeSinceStartup;
        if (now - lastAttempt < 1.0)
        {
            return;
        }
        lastAttempt = now;

        Scene scene = EditorSceneManager.GetActiveScene();
        if (!scene.isLoaded || string.IsNullOrEmpty(scene.path))
        {
            return;
        }

        if (!HasTank(scene))
        {
            if (now > 30.0)
            {
                EditorApplication.update -= PollUntilReady;
            }
            return;
        }

        EditorApplication.update -= PollUntilReady;
        ApplySetup(scene);
    }

    [MenuItem("Tools/Honeypot/Setup escena prototipo")]
    private static void RunFromMenu()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (!scene.isLoaded || string.IsNullOrEmpty(scene.path))
        {
            EditorUtility.DisplayDialog("Honeypot", "No hay ninguna escena abierta.", "OK");
            return;
        }
        ApplySetup(scene);
    }

    private static void ApplySetup(Scene scene)
    {
        bool dirty = false;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == "Plane")
            {
                root.name = "Ground";
                dirty = true;
            }

            if (root.name.IndexOf("tank", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (root.GetComponent<TankController>() == null)
                {
                    root.AddComponent<TankController>();
                    dirty = true;
                }
                if (root.GetComponent<TurretController>() == null)
                {
                    root.AddComponent<TurretController>();
                    dirty = true;
                }
                if (root.GetComponent<CannonController>() == null)
                {
                    root.AddComponent<CannonController>();
                    dirty = true;
                }
                if (EnsureProjectileAssigned(root))
                {
                    dirty = true;
                }
                if (EnsureImpactVfxAssigned(root))
                {
                    dirty = true;
                }
            }
        }

        if (dirty)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Honeypot: scripts asignados al tanque y 'Plane' renombrado a 'Ground'. Escena guardada.");
        }
        else
        {
            Debug.Log("Honeypot: no se encontró nada para configurar. Revisá que la escena tenga el tanque.");
        }

        EditorPrefs.SetBool(PrefsKey, true);
    }

    [MenuItem("Tools/Honeypot/Crear prefab de proyectil")]
    private static void CreateProjectilePrefabFromMenu()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (!scene.isLoaded || string.IsNullOrEmpty(scene.path))
        {
            EditorUtility.DisplayDialog("Honeypot", "No hay ninguna escena abierta.", "OK");
            return;
        }

        bool dirty = false;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name.IndexOf("tank", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (EnsureProjectileAssigned(root))
                {
                    dirty = true;
                }
            }
        }
        if (dirty)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        Debug.Log("Honeypot: prefab de proyectil listo en Assets/Prefabs/Projectile.prefab.");
    }

    private static bool EnsureProjectileAssigned(GameObject tank)
    {
        CannonController cannon = tank != null ? tank.GetComponent<CannonController>() : null;
        GameObject prefab = LoadOrCreateProjectilePrefab();
        if (cannon == null || prefab == null)
        {
            return false;
        }

        SerializedObject so = new SerializedObject(cannon);
        SerializedProperty prop = so.FindProperty("projectilePrefab");
        if (prop == null || prop.objectReferenceValue == prefab)
        {
            return false;
        }
        prop.objectReferenceValue = prefab;
        so.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    private static GameObject LoadOrCreateProjectilePrefab()
    {
        const string path = "Assets/Prefabs/Projectile.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab != null)
        {
            return prefab;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }

        GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        temp.name = "Projectile";
        temp.transform.localScale = Vector3.one * 0.2f;
        temp.AddComponent<Projectile>();
        temp.AddComponent<Rigidbody>();

        prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
        Object.DestroyImmediate(temp);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return prefab;
    }

    [MenuItem("Tools/Honeypot/Crear prefab de impacto")]
    private static void CreateImpactVfxFromMenu()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (!scene.isLoaded || string.IsNullOrEmpty(scene.path))
        {
            EditorUtility.DisplayDialog("Honeypot", "No hay ninguna escena abierta.", "OK");
            return;
        }

        bool dirty = false;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name.IndexOf("tank", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (EnsureImpactVfxAssigned(root))
                {
                    dirty = true;
                }
            }
        }
        if (dirty)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        Debug.Log("Honeypot: VFX de impacto listo en Assets/Prefabs/ImpactVFX.prefab.");
    }

    private static bool EnsureImpactVfxAssigned(GameObject tank)
    {
        CannonController cannon = tank != null ? tank.GetComponent<CannonController>() : null;
        GameObject prefab = LoadOrCreateImpactVfx();
        if (cannon == null || prefab == null)
        {
            return false;
        }

        SerializedObject so = new SerializedObject(cannon);
        SerializedProperty prop = so.FindProperty("impactVfxPrefab");
        if (prop == null || prop.objectReferenceValue == prefab)
        {
            return false;
        }
        prop.objectReferenceValue = prefab;
        so.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    private static GameObject LoadOrCreateImpactVfx()
    {
        const string path = "Assets/Prefabs/ImpactVFX.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab != null && EditorPrefs.GetString(ImpactVfxVersionKey, "") == ImpactVfxVersion)
        {
            return prefab;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }

        Material material = GetOrCreateImpactMaterial();

        GameObject root = new GameObject("ImpactVFX");
        CreateSmokeParticles(root, material);
        CreateSparkParticles(root, material);
        CreateFlashParticles(root, material);

        prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        EditorPrefs.SetString(ImpactVfxVersionKey, ImpactVfxVersion);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return prefab;
    }

    private static Material GetOrCreateImpactMaterial()
    {
        const string path = "Assets/Prefabs/ImpactParticle.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
        {
            return material;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
        }
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }
        material = new Material(shader);
        material.name = "ImpactParticle";
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static Color ColorFromHex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color color);
        return color;
    }

    private static void CreateSmokeParticles(GameObject root, Material material)
    {
        GameObject go = new GameObject("Humo");
        go.transform.SetParent(root.transform, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();

        ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;

        ParticleSystem.MainModule main = ps.main;
        main.duration = 0.6f;
        main.loop = false;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.11f);
        main.gravityModifier = -0.18f;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, (short)Random.Range(8, 12))
        });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.08f;

        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.3f));

        ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = FadeGradient(ColorFromHex("#0a0a0a"), ColorFromHex("#646464"), 0.55f, 0.3f);
    }

    private static void CreateSparkParticles(GameObject root, Material material)
    {
        GameObject go = new GameObject("Chispas");
        go.transform.SetParent(root.transform, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();

        ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;

        ParticleSystem.MainModule main = ps.main;
        main.duration = 0.4f;
        main.loop = false;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 5.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
        main.gravityModifier = 5f;
        main.startColor = new ParticleSystem.MinMaxGradient(
            ColorFromHex("#f1f2a0"),
            ColorFromHex("#5c8947"));

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, (short)Random.Range(16, 24))
        });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.12f;

        ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = FadeGradient(Color.white, Color.white, 1f, 0f);
    }

    private static void CreateFlashParticles(GameObject root, Material material)
    {
        GameObject go = new GameObject("Destello");
        go.transform.SetParent(root.transform, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();

        ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;

        ParticleSystem.MainModule main = ps.main;
        main.duration = 0.1f;
        main.loop = false;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.07f, 0.1f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.4f);
        main.startColor = ColorFromHex("#f1f2a0");

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, (short)1)
        });

        ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = FadeGradient(Color.white, Color.white, 1f, 0f);

        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.15f));
    }

    private static ParticleSystem.MinMaxGradient FadeGradient(Color start, Color mid, float startAlpha, float midAlpha)
    {
        Gradient gradient = new Gradient();
        gradient.colorKeys = new[]
        {
            new GradientColorKey(start, 0f),
            new GradientColorKey(mid, 0.5f),
            new GradientColorKey(mid, 1f)
        };
        gradient.alphaKeys = new[]
        {
            new GradientAlphaKey(startAlpha, 0f),
            new GradientAlphaKey(midAlpha, 0.5f),
            new GradientAlphaKey(0f, 1f)
        };
        return new ParticleSystem.MinMaxGradient(gradient);
    }

    private static bool HasTank(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name.IndexOf("tank", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }
        return false;
    }
}