using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Genera la jerarquia del HUD por codigo, para no tener que arrastrar 12 objetos
/// a mano ni editar el YAML de la escena (los RectTransform son especialmente
/// propensos a errores silenciosos que solo se ven jugando).
///
/// Uso: Tools > Honeypot > Build HUD
///
/// Reejecutable: borra solo lo que genero antes (HUD y el controller) y lo vuelve
/// a crear, asi que se puede tocar una constante de este archivo y regenerar.
///
/// NO deja el panel de pausa: ese va con botones y todavia no existe. El
/// GraphicRaycaster y el EventSystem (con InputSystemUIInputModule) ya estan en la
/// escena, asi que despues solo hay que agregar el boton y su onClick.
///
/// NO guarda la escena: marca el cambio y deja que lo guardes vos, para no pisar
/// trabajo que tengas sin guardar.
/// </summary>
public static class BuildHud
{
    private const string UiSheetPath = "Assets/ui/sprites.png";
    private const string RootName = "HUD";

    private const float Margin = 24f;
    private const float BarWidth = 320f;
    private const float BarHeight = 28f;
    private const float BeeSize = 96f;
    private const float BeeGap = 12f;
    private const float BombIconSize = 64f;

    /// <summary>
    /// Cuantos sprites del sheet son la abeja. El sexto NO es del HUD: es la abeja
    /// muerta para el cartel de game over, que se arma al final y no se muestra
    /// mientras se juega (se juega con la abeja viva).
    /// </summary>
    private const int ExpectedBeeSprites = 5;

    private static readonly Color TrackColor = new Color(0.08f, 0.08f, 0.09f, 0.85f);
    private static readonly Color FillColor = new Color(0.85f, 0.25f, 0.22f, 1f);

    [MenuItem("Tools/Honeypot/Build HUD")]
    public static void Build()
    {
        Canvas canvas = FindOrCreateCanvas();
        if (canvas == null)
        {
            return;
        }

        Sprite[] beeSprites = BeeSprites();
        if (beeSprites.Length == 0)
        {
            Debug.LogWarning(
                $"[BuildHud] No hay sprites de abeja en {UiSheetPath}. " +
                "Corre Tools > Honeypot > Slice UI Sprites. La UI se genera igual, " +
                "pero la abeja queda sin imagen.");
        }

        Transform previous = canvas.transform.Find(RootName);
        if (previous != null)
        {
            Object.DestroyImmediate(previous.gameObject);
        }

        HudController stale = canvas.GetComponent<HudController>();
        if (stale != null)
        {
            Object.DestroyImmediate(stale);
        }

        RectTransform root = NewRect(RootName, canvas.transform);
        Stretch(root, 0f, 0f, 0f, 0f);

        RectTransform topLeft = NewRect("TopLeft", root);
        Anchor(topLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(Margin, -Margin), Vector2.zero);

        RectTransform topRight = NewRect("TopRight", root);
        Anchor(topRight, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-Margin, -Margin), Vector2.zero);

        HealthBarView bar = BuildHealthBar(topLeft);
        BeeStatusView bee = BuildBee(topLeft, beeSprites);
        BombCounterView bombs = BuildBombs(topRight);

        HudController hud = canvas.gameObject.AddComponent<HudController>();
        Assign(hud, "tank", null);
        Assign(hud, "healthBar", bar);
        Assign(hud, "bee", bee);
        Assign(hud, "bombs", bombs);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[BuildHud] HUD generado. Recorda guardar la escena (Ctrl+S).");
    }

    private static Canvas FindOrCreateCanvas()
    {
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas != null)
        {
            Debug.Log($"[BuildHud] Usando el Canvas existente '{canvas.name}'.", canvas);
            return canvas;
        }

        Debug.LogError("[BuildHud] No hay ningun Canvas en la escena. Crealo con " +
                       "GameObject > UI > Canvas (Render Mode Screen Space Overlay) y volve a correr este menu.");
        return null;
    }

    // Ninguna Image del HUD tiene raycastTarget: con el GraphicRaycaster del Canvas
    // activado, si lo tuvieran se comerian el click del mouse y la punteria del
    // tanque dejaria de funcionar en esa zona de la pantalla.
    private static HealthBarView BuildHealthBar(RectTransform parent)
    {
        RectTransform bar = NewRect("TankHealthBar", parent);
        Anchor(bar, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(BarWidth, BarHeight));
        Image background = bar.gameObject.AddComponent<Image>();
        background.color = TrackColor;
        background.raycastTarget = false;

        RectTransform fillRect = NewRect("Fill", bar);
        Image fill = fillRect.gameObject.AddComponent<Image>();
        fill.color = FillColor;
        fill.raycastTarget = false;

        // El relleno se anima con el ANCHO del RectTransform, no con fillAmount:
        // si el Image no tiene sprite, uGUI ignora fillAmount y dibuja el rect
        // entero (Image.OnPopulateMesh hace base.OnPopulateMesh y return). Con
        // anchors (0,0)-(0,1) y pivot en x=0, sizeDelta.x es el ancho y al
        // achicarlo la barra se encoje desde la izquierda, con 2px de margen.
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.sizeDelta = new Vector2(BarWidth - 4f, -4f);
        fillRect.anchoredPosition = new Vector2(2f, 0f);

        Text text = BuildText("HealthText", bar, 18, TextAnchor.MiddleRight);
        Stretch(text.rectTransform, 6f, 4f, 8f, 4f);
        text.text = "100";

        HealthBarView view = bar.gameObject.AddComponent<HealthBarView>();
        Assign(view, "fill", fill);
        Assign(view, "healthText", text);
        return view;
    }

    private static BeeStatusView BuildBee(RectTransform parent, Sprite[] sprites)
    {
        RectTransform bee = NewRect("BeeStatus", parent);
        Anchor(bee, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, -(BarHeight + BeeGap)), new Vector2(BeeSize, BeeSize));

        Image icon = bee.gameObject.AddComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.color = Color.white;
        if (sprites.Length > 0)
        {
            icon.sprite = sprites[0];
        }

        BeeStatusView view = bee.gameObject.AddComponent<BeeStatusView>();
        Assign(view, "beeIcon", icon);
        AssignArray(view, "beeSprites", sprites);
        return view;
    }

    private static BombCounterView BuildBombs(RectTransform parent)
    {
        RectTransform widget = NewRect("BombWidget", parent);
        Anchor(widget, new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

        RectTransform iconRect = NewRect("BombIcon", widget);
        Anchor(iconRect, new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(BombIconSize, BombIconSize));
        Image icon = iconRect.gameObject.AddComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        // Todavia no hay asset de bomba. Un Image sin sprite pinta un quad blanco,
        // asi que va transparente y listo para que le asignen el icono.
        icon.sprite = null;
        icon.color = new Color(1f, 1f, 1f, 0f);

        Text text = BuildText("BombCount", widget, 26, TextAnchor.MiddleLeft);
        Anchor(text.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-(BombIconSize + 10f), 0f), new Vector2(90f, BombIconSize));
        text.text = "0";

        BombCounterView view = widget.gameObject.AddComponent<BombCounterView>();
        Assign(view, "countText", text);
        Assign(view, "bombIcon", icon);
        return view;
    }

    private static Text BuildText(string name, Transform parent, int size, TextAnchor align)
    {
        RectTransform rect = NewRect(name, parent);
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = "0";
        text.fontSize = size;
        text.alignment = align;
        text.color = Color.white;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    // ---- slices -------------------------------------------------------------

    /// <summary>
    /// Corta el sheet en los bloques de contenido real, detectados por columnas de
    /// alpha vacio. No assume celdas iguales: en el sheet real los anchos son
    /// distintos (211/184/175/217/202) y lo unico constante es el separador.
    /// </summary>
    [MenuItem("Tools/Honeypot/Slice UI Sprites")]
    public static void SliceUiSprites()
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(UiSheetPath);
        if (texture == null)
        {
            Debug.LogError($"[BuildHud] No encontre {UiSheetPath}.");
            return;
        }

        List<Rect> rects = DetectBlocks(texture);
        if (rects.Count == 0)
        {
            Debug.LogError($"[BuildHud] {UiSheetPath} no tiene contenido (columnas de alpha vacias en toda la imagen).");
            return;
        }

        TextureImporter importer = AssetImporter.GetAtPath(UiSheetPath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError($"[BuildHud] {UiSheetPath} no tiene TextureImporter.");
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.alphaIsTransparency = true;

        SerializedObject serialized = new SerializedObject(importer);
        SerializedProperty sheet = serialized.FindProperty("spriteSheet");
        SerializedProperty sprites = sheet.FindPropertyRelative("sprites");
        sprites.ClearArray();

        for (int i = 0; i < rects.Count; i++)
        {
            SerializedProperty element = sprites.GetArrayElementAtIndex(i);

            // Unity mide los rects desde abajo-izq y GetPixels32 desde arriba-izq,
            // hay que dar vuelta la Y. Ojo: no se puede escribir rects[i].y = ...
            // porque eso seria modificar el retorno del indexer.
            Rect flipped = rects[i];
            flipped.y = texture.height - flipped.y - flipped.height;

            element.FindPropertyRelative("name").stringValue = $"ui_{i:00}";
            element.FindPropertyRelative("rect").rectValue = flipped;
            element.FindPropertyRelative("alignment").intValue = (int)SpriteAlignment.Center;
            element.FindPropertyRelative("pivot").vector2Value = new Vector2(0.5f, 0.5f);
            element.FindPropertyRelative("border").vector4Value = Vector4.zero;

            // Segun la version internalID se serializa como int o como long, y pedir
            // longValue sobre un intProperty tira excepcion. Se pregunta.
            SerializedProperty id = element.FindPropertyRelative("internalID");
            if (id.propertyType == SerializedPropertyType.Integer)
            {
                id.longValue = 1000000L + i;
            }
            else
            {
                id.intValue = 1000000 + i;
            }

            element.FindPropertyRelative("spriteID").stringValue = string.Empty;
        }

        sheet.FindPropertyRelative("outline").arraySize = 0;
        sheet.FindPropertyRelative("physicsShape").arraySize = 0;
        sheet.FindPropertyRelative("bones").arraySize = 0;
        sheet.FindPropertyRelative("nameFileIdTable").ClearArray();

        serialized.ApplyModifiedProperties();
        importer.SaveAndReimport();
        AssetDatabase.ImportAsset(UiSheetPath, ImportAssetOptions.ForceUpdate);

        Debug.Log($"[BuildHud] {UiSheetPath} cortado en {rects.Count} sprites: " +
                  string.Join(", ", rects.ConvertAll(r => r.width + "x" + r.height)) +
                  ". Los primeros " + ExpectedBeeSprites + " son la abeja.");
    }

    private static List<Rect> DetectBlocks(Texture2D texture)
    {
        int width = texture.width;
        int height = texture.height;
        Color32[] pixels = texture.GetPixels32();

        List<Rect> rects = new List<Rect>();
        int start = -1;
        for (int x = 0; x <= width; x++)
        {
            bool solid = false;
            if (x < width)
            {
                for (int y = 0; y < height; y++)
                {
                    if (pixels[y * width + x].a > 16)
                    {
                        solid = true;
                        break;
                    }
                }
            }

            if (solid && start < 0)
            {
                start = x;
            }
            else if (!solid && start >= 0)
            {
                if (x - start >= 4)
                {
                    rects.Add(new Rect(start, 0f, x - start, height));
                }

                start = -1;
            }
        }

        for (int i = 0; i < rects.Count; i++)
        {
            Rect rect = rects[i];
            int minY = height;
            int maxY = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = (int)rect.x; x < (int)(rect.x + rect.width); x++)
                {
                    if (pixels[y * width + x].a > 16)
                    {
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                        break;
                    }
                }
            }

            rect.y = minY;
            rect.height = maxY - minY + 1;
            rects[i] = rect;
        }

        return rects;
    }

    private static Sprite[] BeeSprites()
    {
        List<Sprite> all = LoadSprites(UiSheetPath);
        if (all.Count == 0)
        {
            SliceUiSprites();
            all = LoadSprites(UiSheetPath);
        }

        // Los nombres van como ui_00, ui_01... asi que el orden alfabetico ES el
        // orden de izquierda a derecha del sheet, que es el orden de los estados.
        // El indice 0 es el que se muestra con la vida llena.
        int count = Mathf.Min(ExpectedBeeSprites, all.Count);
        Sprite[] result = new Sprite[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = all[i];
        }

        return result;
    }

    private static List<Sprite> LoadSprites(string path)
    {
        List<Sprite> sprites = new List<Sprite>();
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (asset is Sprite sprite)
            {
                sprites.Add(sprite);
            }
        }

        sprites.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return sprites;
    }

    // ---- helpers ------------------------------------------------------------

    private static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static void Anchor(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = pos;
    }

    private static void Assign(Component target, string field, object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(field);
        if (property == null)
        {
            Debug.LogError($"[BuildHud] {target.GetType().Name} no tiene el campo '{field}'.");
            return;
        }

        property.objectReferenceValue = value as Object;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AssignArray(Component target, string field, Sprite[] sprites)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(field);
        if (property == null)
        {
            Debug.LogError($"[BuildHud] {target.GetType().Name} no tiene el campo '{field}'.");
            return;
        }

        property.arraySize = sprites?.Length ?? 0;
        for (int i = 0; i < property.arraySize; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}