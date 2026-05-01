using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// ════════════════════════════════════════════════════════════════════════════
//  PINK MATERIAL HUNTER
//
//  Scansiona scene/progetto e identifica TUTTI i materiali rosa/rotti.
//  Reasons rilevati:
//    - Shader == Hidden/InternalErrorShader (rotto vero)
//    - Shader == null (mat orfano)
//    - mat.shader.isSupported == false (compilazione fallita su questa platform)
//    - Material slot null su MeshRenderer
//    - Shader Built-in non compatibile URP (Standard, Legacy)
//
//  UI: lista cliccabile, ping al GameObject in Hierarchy, jump al .mat in Project.
//
//  Menu: Tools > Pink Material Hunter
// ════════════════════════════════════════════════════════════════════════════

public class PinkMaterialHunter : EditorWindow
{
    public enum ScanScope { ActiveScene, AllOpenScenes, EntireProject }
    public enum ProblemKind
    {
        BrokenShader,           // Hidden/InternalErrorShader
        MissingShader,          // shader == null
        UnsupportedShader,      // shader.isSupported == false
        NullMaterialSlot,       // renderer ha slot vuoto
        BuiltinShaderInURP,     // Standard, Legacy/* — non rosa ma da convertire
        MissingMainTexture,     // shader vuole texture, mat ha null
    }

    [System.Serializable]
    public class PinkReport
    {
        public GameObject gameObject;     // null se report e' su Material asset (project scan)
        public string scenePath;          // path nella hierarchy
        public Material material;
        public string materialAssetPath;
        public ProblemKind kind;
        public string detail;
    }

    [SerializeField] private ScanScope scope = ScanScope.ActiveScene;
    [SerializeField] private bool detectBuiltinAsPink = false;
    [SerializeField] private bool detectMissingTextures = false;

    private List<PinkReport> reports = new List<PinkReport>();
    private Vector2 scroll;
    private string filter = "";

    [MenuItem("Tools/Pink Material Hunter")]
    public static void ShowWindow()
    {
        var w = GetWindow<PinkMaterialHunter>("Pink Hunter");
        w.minSize = new Vector2(540f, 460f);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Pink Material Hunter", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Identifica materiali rosa/rotti. Click su 'Ping' per evidenziare in Hierarchy o Project.",
            MessageType.Info);

        EditorGUILayout.Space(4f);
        scope = (ScanScope)EditorGUILayout.EnumPopup("Scope scansione", scope);
        detectBuiltinAsPink = EditorGUILayout.ToggleLeft(
            "Considera anche shader Built-in (Standard/Legacy) come problemi",
            detectBuiltinAsPink);
        detectMissingTextures = EditorGUILayout.ToggleLeft(
            "Verifica texture mancanti (BaseMap null su shader Lit)",
            detectMissingTextures);

        EditorGUILayout.Space(6f);
        GUI.backgroundColor = new Color(0.85f, 0.4f, 0.7f);
        if (GUILayout.Button("ESEGUI SCANSIONE", GUILayout.Height(35f)))
            ExecuteScan();
        GUI.backgroundColor = Color.white;

        if (reports.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "Nessun report ancora. Clicca 'ESEGUI SCANSIONE'.\n" +
                "Se dopo scan il count e' 0, la scena e' pulita.",
                MessageType.None);
            return;
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Trovati {reports.Count} problemi", EditorStyles.boldLabel);
        if (GUILayout.Button("Copy paths to clipboard", GUILayout.Width(180f)))
            CopyPathsToClipboard();
        if (GUILayout.Button("Select all in scene", GUILayout.Width(150f)))
            SelectAllInScene();
        EditorGUILayout.EndHorizontal();

        filter = EditorGUILayout.TextField("Filter (cerca per nome/shader)", filter);

        EditorGUILayout.Space(4f);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawHeader();
        for (int i = 0; i < reports.Count; i++)
        {
            var r = reports[i];
            if (!MatchesFilter(r)) continue;
            DrawReportRow(r, i);
        }
        EditorGUILayout.EndScrollView();
    }

    private void DrawHeader()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("Tipo", EditorStyles.miniBoldLabel, GUILayout.Width(120f));
        GUILayout.Label("GameObject / Material", EditorStyles.miniBoldLabel, GUILayout.ExpandWidth(true));
        GUILayout.Label("Shader", EditorStyles.miniBoldLabel, GUILayout.Width(180f));
        GUILayout.Label("Azioni", EditorStyles.miniBoldLabel, GUILayout.Width(160f));
        EditorGUILayout.EndHorizontal();
    }

    private void DrawReportRow(PinkReport r, int index)
    {
        EditorGUILayout.BeginHorizontal(index % 2 == 0 ? EditorStyles.helpBox : GUIStyle.none);

        Color colorBackup = GUI.color;
        GUI.color = ColorForKind(r.kind);
        GUILayout.Label(r.kind.ToString(), GUILayout.Width(120f));
        GUI.color = colorBackup;

        string label = r.gameObject != null
            ? r.scenePath
            : (r.materialAssetPath ?? "(nullo)");
        GUILayout.Label(label, GUILayout.ExpandWidth(true));

        string shaderName = r.material != null && r.material.shader != null
            ? r.material.shader.name
            : "—";
        GUILayout.Label(shaderName, GUILayout.Width(180f));

        if (GUILayout.Button("Ping GO", GUILayout.Width(70f)))
        {
            if (r.gameObject != null)
            {
                EditorGUIUtility.PingObject(r.gameObject);
                Selection.activeGameObject = r.gameObject;
            }
        }
        if (GUILayout.Button("Ping Mat", GUILayout.Width(80f)))
        {
            if (r.material != null) EditorGUIUtility.PingObject(r.material);
        }

        EditorGUILayout.EndHorizontal();
    }

    private bool MatchesFilter(PinkReport r)
    {
        if (string.IsNullOrEmpty(filter)) return true;
        string f = filter.ToLowerInvariant();
        if (r.scenePath != null && r.scenePath.ToLowerInvariant().Contains(f)) return true;
        if (r.materialAssetPath != null && r.materialAssetPath.ToLowerInvariant().Contains(f)) return true;
        if (r.material != null && r.material.shader != null &&
            r.material.shader.name.ToLowerInvariant().Contains(f)) return true;
        return false;
    }

    private Color ColorForKind(ProblemKind k)
    {
        switch (k)
        {
            case ProblemKind.BrokenShader:        return new Color(1f, 0.3f, 0.3f);
            case ProblemKind.MissingShader:       return new Color(1f, 0.4f, 0.4f);
            case ProblemKind.UnsupportedShader:   return new Color(1f, 0.5f, 0.2f);
            case ProblemKind.NullMaterialSlot:    return new Color(1f, 0.6f, 0.2f);
            case ProblemKind.BuiltinShaderInURP:  return new Color(1f, 0.8f, 0.2f);
            case ProblemKind.MissingMainTexture:  return new Color(0.7f, 0.7f, 1f);
            default: return Color.white;
        }
    }

    private void CopyPathsToClipboard()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var r in reports)
        {
            sb.Append(r.kind).Append("\t");
            sb.Append(r.scenePath ?? r.materialAssetPath ?? "—").Append("\t");
            sb.Append(r.material != null && r.material.shader != null ? r.material.shader.name : "—").Append("\t");
            sb.AppendLine(r.detail ?? "");
        }
        EditorGUIUtility.systemCopyBuffer = sb.ToString();
        Debug.Log($"[PinkHunter] {reports.Count} righe copiate nella clipboard.");
    }

    private void SelectAllInScene()
    {
        var gos = reports.Where(r => r.gameObject != null).Select(r => r.gameObject).ToArray();
        Selection.objects = gos;
        Debug.Log($"[PinkHunter] Selezionati {gos.Length} GameObject in scena.");
    }

    // ════════════════════════════════════════════════════════════════════════
    //  SCAN LOGIC
    // ════════════════════════════════════════════════════════════════════════

    private void ExecuteScan()
    {
        reports.Clear();

        switch (scope)
        {
            case ScanScope.ActiveScene:
                ScanScene(SceneManager.GetActiveScene());
                break;
            case ScanScope.AllOpenScenes:
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    ScanScene(SceneManager.GetSceneAt(i));
                break;
            case ScanScope.EntireProject:
                ScanProjectMaterials();
                break;
        }

        var summary = new System.Text.StringBuilder();
        summary.AppendLine($"[PinkHunter] Scan completato. {reports.Count} problemi.");
        var byKind = reports.GroupBy(r => r.kind).OrderByDescending(g => g.Count());
        foreach (var g in byKind)
            summary.AppendLine($"  {g.Key}: {g.Count()}");
        Debug.Log(summary.ToString());
    }

    private void ScanScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return;

        var renderers = new List<Renderer>();
        var roots = scene.GetRootGameObjects();
        foreach (var root in roots)
        {
            renderers.AddRange(root.GetComponentsInChildren<Renderer>(includeInactive: true));
        }

        foreach (var rend in renderers)
        {
            if (rend == null) continue;
            var mats = rend.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                AnalyzeMaterial(mats[i], rend.gameObject, i);
            }
        }
    }

    private void ScanProjectMaterials()
    {
        string[] guids = AssetDatabase.FindAssets("t:Material");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;
            AnalyzeMaterial(mat, null, 0, path);
        }
    }

    private void AnalyzeMaterial(Material mat, GameObject go, int slotIndex, string assetPathOverride = null)
    {
        string assetPath = assetPathOverride ?? (mat != null ? AssetDatabase.GetAssetPath(mat) : null);

        if (mat == null)
        {
            if (go != null)
            {
                reports.Add(new PinkReport
                {
                    gameObject = go,
                    scenePath = GetGameObjectPath(go) + $" [slot {slotIndex}]",
                    material = null,
                    materialAssetPath = null,
                    kind = ProblemKind.NullMaterialSlot,
                    detail = "Material slot null sul renderer"
                });
            }
            return;
        }

        if (mat.shader == null)
        {
            reports.Add(new PinkReport
            {
                gameObject = go,
                scenePath = go != null ? GetGameObjectPath(go) : null,
                material = mat,
                materialAssetPath = assetPath,
                kind = ProblemKind.MissingShader,
                detail = "Material ha shader null"
            });
            return;
        }

        string shaderName = mat.shader.name;

        if (shaderName == "Hidden/InternalErrorShader")
        {
            reports.Add(new PinkReport
            {
                gameObject = go,
                scenePath = go != null ? GetGameObjectPath(go) : null,
                material = mat,
                materialAssetPath = assetPath,
                kind = ProblemKind.BrokenShader,
                detail = "Shader perso → ROSA garantito"
            });
            return;
        }

        if (!mat.shader.isSupported)
        {
            reports.Add(new PinkReport
            {
                gameObject = go,
                scenePath = go != null ? GetGameObjectPath(go) : null,
                material = mat,
                materialAssetPath = assetPath,
                kind = ProblemKind.UnsupportedShader,
                detail = $"Shader '{shaderName}' non supportato (compilation fail / pipeline mismatch)"
            });
            return;
        }

        if (detectBuiltinAsPink)
        {
            if (shaderName == "Standard" ||
                shaderName == "Standard (Specular setup)" ||
                shaderName.StartsWith("Legacy Shaders/") ||
                shaderName.StartsWith("Mobile/"))
            {
                reports.Add(new PinkReport
                {
                    gameObject = go,
                    scenePath = go != null ? GetGameObjectPath(go) : null,
                    material = mat,
                    materialAssetPath = assetPath,
                    kind = ProblemKind.BuiltinShaderInURP,
                    detail = $"Built-in shader '{shaderName}' — convertibile via URP Material Converter Plus"
                });
                return;
            }
        }

        if (detectMissingTextures && shaderName.StartsWith("Universal Render Pipeline/Lit"))
        {
            if (mat.HasProperty("_BaseMap") && mat.GetTexture("_BaseMap") == null)
            {
                reports.Add(new PinkReport
                {
                    gameObject = go,
                    scenePath = go != null ? GetGameObjectPath(go) : null,
                    material = mat,
                    materialAssetPath = assetPath,
                    kind = ProblemKind.MissingMainTexture,
                    detail = "URP/Lit ma _BaseMap null (potrebbe apparire flat/colored)"
                });
            }
        }
    }

    private static string GetGameObjectPath(GameObject go)
    {
        if (go == null) return "(null)";
        var path = go.name;
        var t = go.transform.parent;
        while (t != null)
        {
            path = t.name + "/" + path;
            t = t.parent;
        }
        return path;
    }
}
