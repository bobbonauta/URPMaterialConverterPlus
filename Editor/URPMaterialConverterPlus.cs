using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        // Legacy Particles
        { "Legacy Shaders/Particles/Additive",              "Universal Render Pipeline/Particles/Unlit" },
        { "Legacy Shaders/Particles/Additive (Soft)",       "Universal Render Pipeline/Particles/Unlit" },
        { "Legacy Shaders/Particles/Multiply",              "Universal Render Pipeline/Particles/Unlit" },
        { "Legacy Shaders/Particles/Alpha Blended",         "Universal Render Pipeline/Particles/Unlit" },
        { "Legacy Shaders/Particles/Alpha Blended Premultiply", "Universal Render Pipeline/Particles/Unlit" },
        { "Legacy Shaders/Particles/Anim Alpha Blended",    "Universal Render Pipeline/Particles/Unlit" },
        { "Legacy Shaders/Particles/VertexLit Blended",     "Universal Render Pipeline/Particles/Unlit" },

        // Vertex color unlit
        { "Unlit/VertexColor",                              "Universal Render Pipeline/Unlit" },
    };

    // Shader vendor che hanno gia' versioni URP-compatibili (NON convertire — sono ok cosi)
    private static readonly string[] URPCompatibleVendorPrefixes = new[]
    {
        "Universal Render Pipeline/",
        "Shader Graphs/",
        "Synty/",
        "SyntyStudios/",
        "Polytope Studio/",
        "Idyllic Fantasy Nature/",
        "OrcProudDefender/",
        "PampelGames/",
        "MicroSplat/",
        "MegaSplat/",
    };

    // Shader di sistema da NON convertire mai (skybox, UI, text)
    private static readonly string[] SystemShaderPrefixes = new[]
    {
        "Skybox/",
        "Mobile/Skybox",
        "GUI/",
        "UI/",
        "Sprites/",
        "TextMeshPro/",
        "Nature/SpeedTree",
        "Nature/Terrain/",
        "Custom/",         // custom user shader: lasciali stare
        "Demo/",           // demo asset shader
        "FX/",
    };

    // Marker di materiale ROTTO (shader perso/non risolto)
    private const string BrokenShaderName = "Hidden/InternalErrorShader";

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

        // Usa AssetDatabase per trovare i materiali (path-agnostic, Unity-native)
        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { targetFolder });

        // Ricava i path. Se convertSubfolders e' OFF, filtra solo top-level
        var assetPaths = new List<string>();
        foreach (string guid in guids)
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(p)) continue;
            if (!convertSubfolders)
            {
                string parent = Path.GetDirectoryName(p)?.Replace("\\", "/");
                string targetNorm = targetFolder.Replace("\\", "/").TrimEnd('/');
                if (parent != targetNorm) continue;
            }
            assetPaths.Add(p);
        }

        Debug.Log($"[URPConverter+] {(dryRun ? "[DRY RUN] " : "")}Trovati {assetPaths.Count} .mat in '{targetFolder}'");

        int converted = 0, skipped = 0, alreadyURP = 0, errors = 0;
        int compatibleCustom = 0, systemSkipped = 0, unmapped = 0;
        var unmappedShaders = new HashSet<string>();
        var brokenMaterials = new List<string>();
        var compatibleCustomShaders = new HashSet<string>();
        var systemSkippedShaders = new HashSet<string>();

        try
        {
            for (int i = 0; i < assetPaths.Count; i++)
            {
                string assetPath = assetPaths[i];
                EditorUtility.DisplayProgressBar(
                    "URP Converter+",
                    $"{Path.GetFileName(assetPath)} ({i + 1}/{assetPaths.Count})",
                    (float)i / assetPaths.Count);

                Material mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
                if (mat == null) { skipped++; continue; }
                if (mat.shader == null) { skipped++; continue; }

                string currentShader = mat.shader.name;

                // 1. Materiale ROTTO (shader perso/non risolto) — va fixato a mano
                if (currentShader == BrokenShaderName)
                {
                    brokenMaterials.Add(assetPath);
                    continue;
                }

                // 2. Gia' URP nativo
                if (currentShader.StartsWith("Universal Render Pipeline/"))
                {
                    if (skipAlreadyURP) { alreadyURP++; continue; }
                }

                // 3. Shader vendor URP-compatibili (Synty, Polytope, Idyllic, ecc.) — gia' funzionanti, NON convertire
                bool isVendorURP = false;
                foreach (var prefix in URPCompatibleVendorPrefixes)
                {
                    if (currentShader.StartsWith(prefix)) { isVendorURP = true; break; }
                }
                if (isVendorURP)
                {
                    compatibleCustom++;
                    compatibleCustomShaders.Add(currentShader);
                    continue;
                }

                // 4. Shader di sistema (Skybox, TextMeshPro, GUI, Sprites, Nature/Terrain, Nature/SpeedTree, Custom/, Demo/)
                bool isSystem = false;
                foreach (var prefix in SystemShaderPrefixes)
                {
                    if (currentShader.StartsWith(prefix)) { isSystem = true; break; }
                }
                if (isSystem)
                {
                    systemSkipped++;
                    systemSkippedShaders.Add(currentShader);
                    continue;
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
                        string fullPath = Path.Combine(
                            Path.GetDirectoryName(Application.dataPath) ?? "",
                            assetPath);
                        string bakPath = fullPath + ".bak";
                        if (File.Exists(fullPath) && !File.Exists(bakPath))
                            File.Copy(fullPath, bakPath);
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

        // Report finale strutturato
        var report = new System.Text.StringBuilder();
        report.AppendLine($"[URPConverter+] {(dryRun ? "DRY RUN — " : "")}REPORT FINALE");
        report.AppendLine($"  Totale .mat trovati:          {assetPaths.Count}");
        report.AppendLine($"  ✅ Convertiti (mapped → URP): {converted}");
        report.AppendLine($"  ✅ Già URP nativi (skipped):  {alreadyURP}");
        report.AppendLine($"  ✅ Vendor URP-compatible:     {compatibleCustom} (Synty/Polytope/Idyllic/ecc — gia' funzionanti)");
        report.AppendLine($"  ✅ System shader (skybox/UI): {systemSkipped} (intoccabili per design)");
        report.AppendLine($"  ⚠️ Materiali ROTTI:           {brokenMaterials.Count} (richiesto fix manuale)");
        report.AppendLine($"  ❓ Shader UNKNOWN:            {unmapped} (mai visti, da valutare)");
        report.AppendLine($"  ❌ Errori durante conversione: {errors}");
        report.AppendLine($"  Skipped (no shader/null mat): {skipped}");

        if (brokenMaterials.Count > 0)
        {
            report.AppendLine();
            report.AppendLine($"⚠️  MATERIALI ROTTI ({brokenMaterials.Count}) — questi sono i 'rosa' veri, fixare manualmente:");
            int max = Mathf.Min(brokenMaterials.Count, 30);
            for (int i = 0; i < max; i++) report.AppendLine($"    - {brokenMaterials[i]}");
            if (brokenMaterials.Count > max) report.AppendLine($"    ... e altri {brokenMaterials.Count - max}. Vedi log completo.");
            report.AppendLine("  Come fixare: apri il .mat nell'Inspector → assegna shader URP/Lit (o reimport del pack di provenienza).");
        }

        if (unmappedShaders.Count > 0)
        {
            report.AppendLine();
            report.AppendLine($"❓ SHADER UNKNOWN ({unmappedShaders.Count}) — non in mapping table, non whitelisted:");
            foreach (var s in unmappedShaders)
                report.AppendLine($"    - {s}");
            report.AppendLine("  Se sono shader URP-compatibili gia' funzionanti, aggiungili a URPCompatibleVendorPrefixes.");
            report.AppendLine("  Se sono Built-in/Legacy non coperti, aggiungili a ShaderMap.");
        }

        if (compatibleCustomShaders.Count > 0 && !dryRun == false /* sempre mostra in dry */)
        {
            report.AppendLine();
            report.AppendLine($"ℹ️ Vendor URP-compatible riconosciuti ({compatibleCustomShaders.Count} unici):");
            foreach (var s in compatibleCustomShaders) report.AppendLine($"    - {s}");
        }

        if (systemSkippedShaders.Count > 0)
        {
            report.AppendLine();
            report.AppendLine($"ℹ️ System shaders skippati ({systemSkippedShaders.Count} unici):");
            foreach (var s in systemSkippedShaders) report.AppendLine($"    - {s}");
        }

        Debug.Log(report.ToString());

        EditorUtility.DisplayDialog(
            "URP Converter+",
            $"{(dryRun ? "[DRY RUN]\n" : "")}" +
            $"Convertiti: {converted}\n" +
            $"Già URP: {alreadyURP}\n" +
            $"Vendor URP-compat: {compatibleCustom}\n" +
            $"System (skybox/UI): {systemSkipped}\n" +
            $"⚠️ ROTTI da fixare: {brokenMaterials.Count}\n" +
            $"❓ Unknown: {unmapped}\n" +
            $"Errori: {errors}\n\nVedi Console per dettagli.",
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
