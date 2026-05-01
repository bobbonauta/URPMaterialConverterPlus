using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// ════════════════════════════════════════════════════════════════════════════
//  URP MATERIAL CONVERTER PLUS
//
//  Converte materiali Built-in (Standard, Legacy/*, Sprites, Unlit) → URP equivalente.
//  Modifica in place i .mat (niente duplicati _URP.mat). Mappa correttamente
//  emission, transparency, normal, metallic, smoothness.
//
//  Vantaggi rispetto a Unity Render Pipeline Converter:
//    - Single-pass (5-30 secondi vs ore)
//    - Solo i materiali, niente reimport asset
//    - Log dettagliato cosa cambia
//    - Opzione backup prima della conversione
//
//  Menu: Tools > URP Material Converter Plus
// ════════════════════════════════════════════════════════════════════════════

public class URPMaterialConverterPlus : EditorWindow
{
    [SerializeField] private string targetFolder = "Assets";
    [SerializeField] private bool createBackup = true;
    [SerializeField] private bool dryRun = false;
    [SerializeField] private bool convertSubfolders = true;
    [SerializeField] private bool skipAlreadyURP = true;

    [MenuItem("Tools/URP Material Converter Plus")]
    public static void ShowWindow()
    {
        var w = GetWindow<URPMaterialConverterPlus>("URP Mat Converter+");
        w.minSize = new Vector2(420f, 320f);
    }

    private static readonly Dictionary<string, string> ShaderMap = new Dictionary<string, string>
    {
        // Standard → URP/Lit (PBR)
        { "Standard",                                       "Universal Render Pipeline/Lit" },
        { "Standard (Specular setup)",                      "Universal Render Pipeline/Lit" },

        // Legacy → URP/Lit o Simple Lit
        { "Legacy Shaders/Diffuse",                         "Universal Render Pipeline/Simple Lit" },
        { "Legacy Shaders/Bumped Diffuse",                  "Universal Render Pipeline/Simple Lit" },
        { "Legacy Shaders/Specular",                        "Universal Render Pipeline/Lit" },
        { "Legacy Shaders/Bumped Specular",                 "Universal Render Pipeline/Lit" },
        { "Legacy Shaders/VertexLit",                       "Universal Render Pipeline/Simple Lit" },
        { "Legacy Shaders/Self-Illumin/Diffuse",            "Universal Render Pipeline/Simple Lit" },
        { "Legacy Shaders/Self-Illumin/Bumped Diffuse",     "Universal Render Pipeline/Simple Lit" },
        { "Legacy Shaders/Transparent/Diffuse",             "Universal Render Pipeline/Simple Lit" },
        { "Legacy Shaders/Transparent/Bumped Diffuse",      "Universal Render Pipeline/Simple Lit" },
        { "Legacy Shaders/Transparent/Cutout/Diffuse",      "Universal Render Pipeline/Simple Lit" },
        { "Legacy Shaders/Transparent/Cutout/Bumped Diffuse", "Universal Render Pipeline/Simple Lit" },

        // Mobile → URP/Simple Lit (più leggero)
        { "Mobile/Diffuse",                                 "Universal Render Pipeline/Simple Lit" },
        { "Mobile/Bumped Diffuse",                          "Universal Render Pipeline/Simple Lit" },
        { "Mobile/Bumped Specular",                         "Universal Render Pipeline/Simple Lit" },
        { "Mobile/VertexLit",                               "Universal Render Pipeline/Simple Lit" },

        // Unlit → URP/Unlit
        { "Unlit/Texture",                                  "Universal Render Pipeline/Unlit" },
        { "Unlit/Color",                                    "Universal Render Pipeline/Unlit" },
        { "Unlit/Transparent",                              "Universal Render Pipeline/Unlit" },
        { "Unlit/Transparent Cutout",                       "Universal Render Pipeline/Unlit" },

        // Particles → URP/Particles/Unlit (semplificato)
        { "Particles/Standard Surface",                     "Universal Render Pipeline/Particles/Lit" },
        { "Particles/Standard Unlit",                       "Universal Render Pipeline/Particles/Unlit" },
    };

    private void OnGUI()
    {
        EditorGUILayout.LabelField("URP Material Converter Plus", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Converte materiali Built-in/Legacy/Mobile/Unlit → URP equivalenti.\n" +
            "Modifica IN PLACE (niente duplicati _URP.mat).\n" +
            "Esegui PRIMA un Dry Run per vedere cosa cambierebbe.",
            MessageType.Info);

        EditorGUILayout.Space(8f);

        EditorGUILayout.BeginHorizontal();
        targetFolder = EditorGUILayout.TextField("Cartella target", targetFolder);
        if (GUILayout.Button("...", GUILayout.Width(30f)))
        {
            string sel = EditorUtility.OpenFolderPanel("Cartella target", Application.dataPath, "");
            if (!string.IsNullOrEmpty(sel) && sel.StartsWith(Application.dataPath))
                targetFolder = "Assets" + sel.Substring(Application.dataPath.Length);
        }
        EditorGUILayout.EndHorizontal();

        convertSubfolders = EditorGUILayout.Toggle("Includi sottocartelle", convertSubfolders);
        skipAlreadyURP    = EditorGUILayout.Toggle("Salta i material già URP", skipAlreadyURP);
        createBackup      = EditorGUILayout.Toggle("Crea backup .mat.bak", createBackup);
        dryRun            = EditorGUILayout.Toggle("Dry Run (solo log, niente cambia)", dryRun);

        EditorGUILayout.Space(12f);

        GUI.backgroundColor = dryRun ? new Color(0.4f, 0.6f, 0.8f) : new Color(0.3f, 0.8f, 0.3f);
        if (GUILayout.Button(dryRun ? "ESEGUI DRY RUN" : "ESEGUI CONVERSIONE", GUILayout.Height(40f)))
        {
            ExecuteConversion();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(6f);
        EditorGUILayout.HelpBox(
            "Per progetti con migliaia di materiali, consigliato eseguire su sottocartelle " +
            "(es. solo 'Assets/Asset Esterni/Synty/PolygonNature/Materials').",
            MessageType.None);
    }

    private void ExecuteConversion()
    {
        if (!AssetDatabase.IsValidFolder(targetFolder))
        {
            Debug.LogError($"[URPConverter+] Cartella '{targetFolder}' non valida.");
            return;
        }

        var searchOpt = convertSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        string absPath = Path.GetFullPath(targetFolder);
        var files = Directory.GetFiles(absPath, "*.mat", searchOpt);

        Debug.Log($"[URPConverter+] {(dryRun ? "[DRY RUN] " : "")}Trovati {files.Length} .mat in '{targetFolder}'");

        int converted = 0, skipped = 0, alreadyURP = 0, errors = 0, unmapped = 0;
        var unmappedShaders = new HashSet<string>();

        try
        {
            for (int i = 0; i < files.Length; i++)
            {
                string assetPath = "Assets" + files[i].Substring(Application.dataPath.Length).Replace("\\", "/");
                EditorUtility.DisplayProgressBar(
                    "URP Converter+",
                    $"{Path.GetFileName(assetPath)} ({i + 1}/{files.Length})",
                    (float)i / files.Length);

                Material mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
                if (mat == null) { skipped++; continue; }
                if (mat.shader == null) { skipped++; continue; }

                string currentShader = mat.shader.name;

                if (currentShader.StartsWith("Universal Render Pipeline/"))
                {
                    if (skipAlreadyURP) { alreadyURP++; continue; }
                }

                if (!ShaderMap.TryGetValue(currentShader, out string newShaderName))
                {
                    unmapped++;
                    unmappedShaders.Add(currentShader);
                    continue;
                }

                Shader newShader = Shader.Find(newShaderName);
                if (newShader == null)
                {
                    Debug.LogError($"[URPConverter+] Shader URP non trovato: '{newShaderName}'. URP installato?");
                    errors++;
                    continue;
                }

                if (dryRun)
                {
                    Debug.Log($"[URPConverter+ DRY] {assetPath}: {currentShader} → {newShaderName}");
                    converted++;
                    continue;
                }

                try
                {
                    if (createBackup)
                    {
                        string bakPath = files[i] + ".bak";
                        if (!File.Exists(bakPath)) File.Copy(files[i], bakPath);
                    }

                    ConvertMaterial(mat, newShader, currentShader);
                    EditorUtility.SetDirty(mat);
                    converted++;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[URPConverter+] Errore su {assetPath}: {ex.Message}");
                    errors++;
                }
            }

            if (!dryRun)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        // Report finale
        var report = new System.Text.StringBuilder();
        report.AppendLine($"[URPConverter+] {(dryRun ? "DRY RUN — " : "")}REPORT FINALE");
        report.AppendLine($"  Totale .mat trovati: {files.Length}");
        report.AppendLine($"  Convertiti: {converted}");
        report.AppendLine($"  Già URP (skipped): {alreadyURP}");
        report.AppendLine($"  Skipped (no shader): {skipped}");
        report.AppendLine($"  Errori: {errors}");
        report.AppendLine($"  Shader non mappati: {unmapped}");
        if (unmappedShaders.Count > 0)
        {
            report.AppendLine("  Lista shader non gestiti (verifica manuale o aggiungi al map):");
            foreach (var s in unmappedShaders)
                report.AppendLine($"    - {s}");
        }
        Debug.Log(report.ToString());

        EditorUtility.DisplayDialog(
            "URP Converter+",
            $"{(dryRun ? "[DRY RUN]\n" : "")}Convertiti: {converted}\nGià URP: {alreadyURP}\n" +
            $"Errori: {errors}\nShader non gestiti: {unmapped}\n\nVedi Console per dettagli.",
            "OK");
    }

    private void ConvertMaterial(Material mat, Shader newShader, string oldShaderName)
    {
        // Salva proprietà rilevanti prima del cambio
        Texture mainTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
        Vector2 mainScale = mat.HasProperty("_MainTex") ? mat.GetTextureScale("_MainTex") : Vector2.one;
        Vector2 mainOffset = mat.HasProperty("_MainTex") ? mat.GetTextureOffset("_MainTex") : Vector2.zero;
        Color color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;

        Texture bumpMap = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
        float bumpScale = mat.HasProperty("_BumpScale") ? mat.GetFloat("_BumpScale") : 1f;

        Texture metallicMap = mat.HasProperty("_MetallicGlossMap") ? mat.GetTexture("_MetallicGlossMap") : null;
        float metallic = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0f;
        float glossiness = mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness") : 0.5f;
        float smoothness = mat.HasProperty("_Smoothness") ? mat.GetFloat("_Smoothness") : glossiness;

        Texture occMap = mat.HasProperty("_OcclusionMap") ? mat.GetTexture("_OcclusionMap") : null;
        float occStrength = mat.HasProperty("_OcclusionStrength") ? mat.GetFloat("_OcclusionStrength") : 1f;

        Texture emissionMap = mat.HasProperty("_EmissionMap") ? mat.GetTexture("_EmissionMap") : null;
        Color emissionColor = mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black;

        float cutoff = mat.HasProperty("_Cutoff") ? mat.GetFloat("_Cutoff") : 0.5f;

        // Determina surface type / blend mode dal shader name
        bool isTransparent = oldShaderName.Contains("Transparent") && !oldShaderName.Contains("Cutout");
        bool isAlphaCutout = oldShaderName.Contains("Cutout");

        // Cambia shader
        mat.shader = newShader;

        // Riapplica proprietà mappate
        if (mat.HasProperty("_BaseMap"))
        {
            if (mainTex != null) mat.SetTexture("_BaseMap", mainTex);
            mat.SetTextureScale("_BaseMap", mainScale);
            mat.SetTextureOffset("_BaseMap", mainOffset);
        }
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);

        if (mat.HasProperty("_BumpMap") && bumpMap != null)
            mat.SetTexture("_BumpMap", bumpMap);
        if (mat.HasProperty("_BumpScale"))
            mat.SetFloat("_BumpScale", bumpScale);

        if (mat.HasProperty("_MetallicGlossMap") && metallicMap != null)
            mat.SetTexture("_MetallicGlossMap", metallicMap);
        if (mat.HasProperty("_Metallic"))
            mat.SetFloat("_Metallic", metallic);
        if (mat.HasProperty("_Smoothness"))
            mat.SetFloat("_Smoothness", smoothness);

        if (mat.HasProperty("_OcclusionMap") && occMap != null)
            mat.SetTexture("_OcclusionMap", occMap);
        if (mat.HasProperty("_OcclusionStrength"))
            mat.SetFloat("_OcclusionStrength", occStrength);

        if (mat.HasProperty("_EmissionMap") && emissionMap != null)
            mat.SetTexture("_EmissionMap", emissionMap);
        if (mat.HasProperty("_EmissionColor"))
            mat.SetColor("_EmissionColor", emissionColor);
        if (emissionColor.maxColorComponent > 0.01f)
            mat.EnableKeyword("_EMISSION");

        // Surface type / blend
        if (mat.HasProperty("_Surface"))
        {
            if (isTransparent)
            {
                mat.SetFloat("_Surface", 1f); // Transparent
                mat.SetFloat("_Blend", 0f);   // Alpha
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            else if (isAlphaCutout)
            {
                mat.SetFloat("_Surface", 0f); // Opaque
                mat.SetFloat("_AlphaClip", 1f);
                mat.SetFloat("_Cutoff", cutoff);
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }
            else
            {
                mat.SetFloat("_Surface", 0f);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = -1;
            }
        }
    }
}
