# URP Troubleshooting — Reference completa errori comuni

> Documento di riferimento per chiunque sviluppi su Universal Render Pipeline (URP). Copre i problemi piu` ricorrenti con cause documentate, sintomi osservabili e soluzioni testate dalla community e dalla documentazione Unity ufficiale.
> Versione di riferimento: URP 14 / 16 / 17 (Unity 2022.3 LTS, Unity 6 / 6000.x).

---

## 1. Materiali Pink / Magenta

### Errore: Materiali pink dopo conversione Built-in -> URP
**Sintomo**: tutti (o molti) materiali appaiono di colore magenta acceso in Scene/Game view.
**Causa probabile**: lo shader assegnato al materiale non e` compatibile con la pipeline attiva. URP non riconosce gli shader Built-in (es. `Standard`, `Standard (Specular)`, `Legacy`, `Mobile/*`).
**Soluzione**:
1. `Window > Rendering > Render Pipeline Converter` -> dropdown su `Built-in to URP` -> `Initialize Converters` -> `Convert Assets`.
2. In alternativa: selezionare i materiali e usare `Edit > Rendering > Materials > Convert Selected Built-in Materials to URP`.
3. Per shader custom scritti in CG/HLSL: il converter NON li migra. Vanno riscritti per URP o ricreati in Shader Graph.

**Fonti**:
- [Unity Manual - Converting your shaders (URP 16)](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@16.0/manual/upgrading-your-shaders.html)
- [Unity Support - Resolving pink materials](https://support.unity.com/hc/en-us/articles/34732602666260-Resolving-pink-materials-when-importing-assets-into-Unity)

### Errore: Conversione materiali eseguita ma resta pink
**Sintomo**: dopo aver eseguito il converter i materiali continuano ad apparire magenta.
**Causa probabile**: il Render Pipeline Asset non e` assegnato in `Graphics` e/o in `Quality Settings`. Il converter ha bisogno di una pipeline attiva valida.
**Soluzione**:
1. `Edit > Project Settings > Graphics` -> assegnare URP Asset a `Default Render Pipeline`.
2. `Edit > Project Settings > Quality` -> per **ogni** Quality Level assegnare `Render Pipeline Asset`.
3. Riavviare il converter.

**Fonti**:
- [Unity Discussions - URP default materials are magenta](https://discussions.unity.com/t/urp-default-materials-are-magenta-pink-and-i-cant-convert-them/879231)
- [Bugnet Blog - Fix Unity SRP errors](https://bugnet.io/blog/fix-unity-scriptable-render-pipeline-errors)

### Errore: Asset Store pack (Synty / Polytope / POLYGON) tutto magenta
**Sintomo**: dopo import di un pack i prefab appaiono pink.
**Causa probabile**: il pack include materiali con shader Standard Built-in.
**Soluzione**: selezionare la cartella materiali del pack e applicare `Edit > Rendering > Materials > Convert Selected Built-in Materials to URP`. Per shader proprietari (es. Synty alcuni custom), riassegnare manualmente `URP/Lit` o `URP/Simple Lit`.

**Fonti**:
- [Discussions - Basically all assets pink in URP](https://discussions.unity.com/t/basically-all-assets-are-pink-in-urp/864047)
- [umi studio - Fixing Pink Materials URP](https://umistudioblog.com/en/fixing-pink-materials-in-unity-urp-step-by-step-guide/)

---

## 2. Render Pipeline Asset

### Errore: "No SRP active" / pipeline null at runtime
**Sintomo**: scene rendering fallback al Built-in, console warning sulla pipeline null.
**Causa probabile**: pipeline asset assegnato in Graphics ma non in qualche Quality Level (Unity usa il valore Quality se non null, altrimenti Graphics; un Quality Level con campo null *non* riceve fallback in alcuni casi).
**Soluzione**: assicurarsi che TUTTI i Quality Level abbiano l'URP Asset assegnato, oppure tutti vuoti per usare il Default Graphics. Mai mischiare URP Asset diversi tra Quality Level (genera shader variants stripping inconsistenti in build).

**Fonti**:
- [Unity Manual - Change or detect active RP](https://docs.unity3d.com/Manual/srp-setting-render-pipeline-asset.html)
- [Discussions - Project Settings Quality RP Asset](https://discussions.unity.com/t/project-settings-quality-rendering-render-pipeline-asset/817312)

### Errore: switch URP Asset da script non funziona
**Sintomo**: cambiare `GraphicsSettings.defaultRenderPipeline` o `QualitySettings.renderPipeline` a runtime non aggiorna effettivamente la pipeline.
**Causa probabile**: bug noto in alcune versioni 2022.x; il cambio richiede rebuild dell'asset di Quality.
**Soluzione**: cambiare via `QualitySettings.SetQualityLevel()` invece di toccare direttamente l'asset, oppure aggiornare a versione patched.

**Fonti**: [Issue Tracker - URP asset cannot be assigned](https://issuetracker.unity3d.com/issues/urp-asset-cannot-be-assigned-to-default-render-pipeline-property)

---

## 3. Shader Compilation & Variant Stripping

### Errore: Shader stripped in build -> materiali pink solo in Player
**Sintomo**: tutto OK in Editor, in build standalone/mobile alcuni materiali sono pink. Console: "Shader X variant Y not found".
**Causa probabile**: Unity stripping aggressivo elimina varianti necessarie a runtime (keyword non riferite da scene incluse).
**Soluzione**:
1. `Edit > Project Settings > Graphics` -> sezione "Shader Stripping" -> abilitare logging (Editor.log -> cercare "ShaderStrippingReport").
2. Aggiungere lo shader in `Always Included Shaders` (Graphics settings).
3. Sostituire `shader_feature` con `multi_compile` per le keyword critiche a runtime (es. set da script).
4. Per Addressables/AssetBundle: verificare che build Addressables e player build usino lo **stesso** URP Pipeline Asset.

**Fonti**:
- [Bugnet Blog - URP Shader not rendering in build](https://bugnet.io/blog/fix-unity-urp-shader-not-rendering-build)
- [Unity Manual - Strip shader variants](https://docs.unity3d.com/Manual/shader-variant-stripping.html)
- [Discussions - URP shader not found in built game](https://discussions.unity.com/t/urp-shader-not-found-in-built-game/791233)

### Errore: GPU Instancing causa "macro redefinition" o INSTANCING_ON undefined
**Sintomo**: errori di compilazione shader graph quando si abilita "Enable GPU Instancing" sul materiale.
**Causa probabile**: bug noto Shader Graph + GPU Instancing in URP/HDRP (alcune versioni 2021/2022). La keyword `INSTANCING_ON` non viene definita correttamente o ridefinita.
**Soluzione**: aggiornare URP package, evitare combinazione SRP Batcher + GPU Instancing sullo stesso material (URP preferisce SRP Batcher e ignora il flag instancing). Per forzare instancing, rendere lo shader incompatibile con SRP Batcher (vedi sez. 18).

**Fonti**:
- [Issue Tracker - URP shader compile error GPU instancing](https://issuetracker.unity3d.com/issues/universalrp-shader-compilation-error-when-using-gpu-instancing)
- [Discussions - INSTANCING_ON not defined URP](https://discussions.unity.com/t/key-word-instancing_on-doesnt-be-defined-when-running-for-custom-gpu-instancing-shader-in-urp/951497)

---

## 4. GPU Resident Drawer / BatchRendererGroup

### Errore: GPU Resident Drawer disattivato con warning "BatchRendererGroup variants stripped"
**Sintomo**: console warning: `GPU Resident Drawer disabled because BatchRendererGroup Variants are stripped`. Le mesh non beneficiano del drawer.
**Causa probabile**: in Unity 6 il default `Strip Unused Variants` rimuove le varianti DOTS Instancing necessarie al BRG.
**Soluzione**:
1. `Edit > Project Settings > Graphics` -> sezione Shader Stripping -> impostare **`BatchRendererGroup Variants` = `Keep All`**.
2. Disabilitare `Strip Unused Variants` (URP global setting).
3. Nell'URP Asset attivo: abilitare `SRP Batcher` e impostare `Rendering > GPU Resident Drawer` = `Instanced Drawing`.
4. Conseguenza: build time piu` lungo (varianti BRG compilate).

**Fonti**:
- [Unity Manual - Enable GPU Resident Drawer URP](https://docs.unity3d.com/Manual/urp/gpu-resident-drawer.html)
- [The Knights of U - GPU Resident Drawer Unity 6](https://theknightsofu.com/boost-performance-of-your-game-in-unity-6-with-gpu-resident-drawer/)

---

## 5. Lighting / Reflection Probes / Light Probes / GI

### Errore: oggetti dinamici non ricevono GI bounce
**Sintomo**: personaggi/oggetti animati appaiono "piatti" rispetto allo scenario baked.
**Causa probabile**: gli oggetti dinamici non sono coperti da Light Probes o non hanno `Receive Global Illumination = Light Probes`.
**Soluzione**: posizionare un Light Probe Group nella scena, baking lighting, sui MeshRenderer dinamici impostare `Light Probes = Blend Probes`. Per oggetti grandi usare APV (vedi sotto) o Light Probe Proxy Volumes.

**Fonti**: [Unity Manual - Light Probes moving objects](https://docs.unity3d.com/Manual/LightProbes-MovingObjects.html)

### Errore: Lit Shader non usa Baked Lightmaps OPPURE Baked Lit non usa Reflection Probes
**Sintomo**: scelta "esclusiva": o lightmap o reflection probe, mai entrambi sullo stesso oggetto.
**Causa probabile**: limitazione storica di URP (precedente APV). `Lit` ignorava lightmap baked in alcune configurazioni; `Baked Lit` non sample-a reflection probes.
**Soluzione**: usare URP `Lit` con `Lighting Mode = Mixed`, baking corretto, **e** usare APV (Adaptive Probe Volumes) introdotto in Unity 6 per copertura GI uniforme. In alternativa custom shader graph che combina entrambi.

**Fonti**:
- [Discussions - Baked Lighting and Reflection Probes URP](https://discussions.unity.com/t/baked-lighting-and-reflection-probes/1703783)
- [Unity Blog - GI updates 2022.2](https://blog.unity.com/engine-platform/global-illumination-updates-in-2022-2)

### Errore: Adaptive Probe Volumes (APV) non bake-ano
**Sintomo**: "Generate Lighting" non produce dati APV; scena resta nera o senza GI indiretta.
**Causa probabile**: configurazione incompleta. APV richiede setup specifico in URP.
**Soluzione**:
1. `Window > Rendering > Lighting > Scene` -> abilitare `Baked Global Illumination`.
2. Tab `Adaptive Probe Volumes` -> `Baking Mode = Single Scene` (o Multi-Scene).
3. Per ogni Light: `Mode = Mixed` o `Baked`.
4. Per ogni MeshRenderer: `Contribute Global Illumination = ON`, `Receive Global Illumination = Light Probes`.
5. Aggiungere un GameObject `Adaptive Probe Volume` che copre la zona.
6. `Generate Lighting`.

**Fonti**:
- [Unity Manual - Use APV in URP](https://docs.unity3d.com/Manual/urp/probevolumes-use.html)
- [Discussions - Unity 6 APV bakes failing](https://discussions.unity.com/t/unity-6-adaptive-light-probe-bakes-failing/1573623)

### Errore: Reflection Probe non vede APV
**Sintomo**: APV bake-ato ma le reflection probes mostrano illuminazione diversa.
**Causa probabile**: bug noto, le reflection probes in alcune versioni non sample-ano APV durante il loro bake.
**Soluzione**: rebake reflection probes DOPO APV; in Unity 6.0.x aggiornare a patch piu` recenti.

**Fonti**: [Discussions - Reflection probe does not see APV](https://discussions.unity.com/t/reflection-probe-does-not-see-lighting-from-baked-adaptive-probe-volumes-apv/1682927)

---

## 6. Post-Processing

### Errore: Post-Processing Stack v2 (PPv2) non funziona piu`
**Sintomo**: dopo migrazione a URP 8+ il `Post Process Layer` non e` piu` aggiungibile, gli effetti spariscono.
**Causa probabile**: URP 8+ NON supporta PPv2. URP usa un Volume framework integrato (talvolta detto PPv3).
**Soluzione**:
1. Rimuovere il pacchetto `Post Processing` dal Package Manager.
2. Sulla camera: abilitare il flag `Post Processing` nel componente Camera.
3. Creare un GameObject con componente `Volume` (Global o Local), assegnare un Volume Profile, aggiungere effetti via `Add Override` (Bloom, Tonemapping, Color Adjustments...).

**Fonti**:
- [Cyan - URP Post Processing](https://cyangamedev.wordpress.com/2020/06/22/urp-post-processing/)
- [Discussions - URP post processing Profile v2 not working](https://discussions.unity.com/t/urp-post-processing-profile-v2-not-working/801416)

### Errore: Volume Profile assegnato ma effetti invisibili
**Sintomo**: Bloom/Tonemap nel volume ma scena non cambia.
**Causa probabile**:
- Camera ha `Post Processing` disabilitato.
- Volume Layer mask non corrispondente alla camera `Volume Mask`.
- Override non `enabled` nel profile (toggle a sinistra di ogni proprieta`).
- Bloom richiede HDR: nell'URP Asset, sezione Quality, abilitare `HDR`.
**Soluzione**: verificare i 4 punti sopra in ordine. Spesso basta abilitare l'override individuale.

**Fonti**: [Discussions - URP post processing not showing](https://discussions.unity.com/t/urp-post-processing-not-showing/763449)

---

## 7. Terrain Rendering

### Errore: Terrain pink in build (ma OK in Editor)
**Sintomo**: in Play mode in Editor il terrain renderizza, in build standalone diventa pink/magenta.
**Causa probabile**: shader Terrain stripped in build perche` non referenziato direttamente da una scena al build time.
**Soluzione**:
1. Aggiungere `Universal Render Pipeline/Terrain/Lit` in `Always Included Shaders` (Graphics settings).
2. Verificare che le Terrain Layers abbiano material override `URP/Terrain/Lit`.

**Fonti**: [Discussions - URP play vs build terrains pink](https://discussions.unity.com/t/urp-play-vs-build-terrains-layers-details-are-pinks/927195)

### Errore: Tree billboard pink (LOD billboard)
**Sintomo**: alberi vicini OK, da lontano diventano pink (fase billboard).
**Causa probabile**: Tree Creator / Nature Soft Occlusion shader NON e` supportato in URP. Solo SpeedTree8 lo e`.
**Soluzione**: convertire alberi a SpeedTree (formato `.st`) o sostituire con prefab tree. Per i tree esistenti, riassegnare materiale `Universal Render Pipeline/Nature/SpeedTree8` o `SpeedTree8_PBRLit`.

**Fonti**:
- [Issue Tracker - URP pink billboard renderer](https://issuetracker.unity3d.com/issues/urp-pink-shaders-appear-near-camera-when-using-billboard-renderer)
- [Discussions - Tree Nature Soft Occlusion URP](https://discussions.unity.com/t/case-1227083-tree-nature-soft-occlusion-shader-in-urp/776047)

### Errore: Terrain Detail (grass) non si renderizza in build
**Sintomo**: erba visibile in Editor, assente in build.
**Causa probabile**: shader `Hidden/TerrainEngine/Details/...` stripped.
**Soluzione**: aggiungere shader detail in `Always Included Shaders`. In URP recenti i detail meshes richiedono GPU Instancing supported sul prefab/material, altrimenti fallback fallisce.

**Fonti**: [Issue Tracker - URP does not render terrain details in build](https://issuetracker.unity3d.com/issues/urp-does-not-render-terrain-details-when-the-project-is-built)

### Errore: SpeedTree pink dopo upgrade a URP
**Sintomo**: SpeedTree models magenta dopo passaggio a URP.
**Causa probabile**: shader `SpeedTree7`/`SpeedTree8` Built-in non compatibili.
**Soluzione**: in Unity 2021.2+ esistono `SpeedTree/PBRLit` e `SpeedTree/PBRLitBillboard` per URP. Sul prefab SpeedTree premere "Apply & Generate Materials" oppure assegnare manualmente `Packages/com.unity.render-pipelines.universal/Shaders/Nature/SpeedTree8_PBRLit`. Clear baked data se rimangono cached.

**Fonti**:
- [Discussions - SpeedTree shader issues URP](https://discussions.unity.com/t/speedtree-shader-issues-with-urp/806692)
- [Issue Tracker - SpeedTree shaders not SRP batcher compatible](https://issuetracker.unity3d.com/issues/urp-speedtree7-slash-speedtree8-shaders-are-not-srp-batcher-compatible)

---

## 8. Shadows / Cascade

### Errore: Shadows non castate da additional lights (point/spot)
**Sintomo**: solo la directional light proietta shadow, le altre no.
**Causa probabile**: nell'URP Asset, sezione `Lighting`, `Cast Shadows` disabilitato per Additional Lights, oppure shadow atlas troppo piccolo.
**Soluzione**:
1. URP Asset -> `Lighting > Additional Lights = Per Pixel`, `Cast Shadows = ON`.
2. Aumentare `Additional Lights Shadow Atlas Resolution` (default 4096 puo` essere insufficiente con tante luci).
3. Verificare che la light specifica abbia `Shadow Type = Soft/Hard Shadows`.

**Fonti**: [Unity Manual - Optimize shadow rendering URP](https://docs.unity3d.com/Manual/shadows-optimization.html)

### Errore: Shadow cascade artifact (banding al confine cascate)
**Sintomo**: linea/anello visibile dove una cascade cambia all'altra.
**Causa probabile**: cascade splits non bilanciati; problema piu` evidente in build vs Editor.
**Soluzione**:
1. URP Asset -> `Shadows > Cascade Count = 4`, regolare manualmente Split 1/2/3.
2. Aumentare `Max Distance` o `Depth Bias`/`Normal Bias`.
3. Considerare cascade blending (URP recente).

**Fonti**:
- [Discussions - Shadow cascades weird bug build](https://discussions.unity.com/t/urp-shadow-cascades-weird-bug-behaviour-in-build/1583700)
- [Unity Manual - Configure shadow cascades](https://docs.unity3d.com/Manual/shadow-cascades-use.html)

### Errore: Cascade Count = 1 fa sparire le shadows
**Sintomo**: impostando 1 cascade nessuna ombra viene proiettata.
**Causa probabile**: bug noto in alcune versioni URP.
**Soluzione**: usare almeno 2 cascade o aggiornare URP.

**Fonti**: [Discussions - How do you remove cascade shadows URP](https://discussions.unity.com/t/how-do-you-remove-cascade-shadows-in-urp-1-cascade-also-not-working/933827)

---

## 9. HDR / Tonemapping

### Errore: Scena "washed out" / colori slavati con Tonemapping Neutral
**Sintomo**: tutto appare smorto, contrasto basso.
**Causa probabile**: il Tonemapper Neutral riduce la luminanza globale dando aspetto greyer; ACES e` piu` filmico/contrastato.
**Soluzione**: Volume Profile -> override `Tonemapping > Mode = ACES` (Filmic) per look cinematografico, o `Neutral` per stile arte preservata. Aggiustare `Color Adjustments > Post Exposure` per recuperare brightness.

**Fonti**:
- [Discussions - Explain Unity tone mapping](https://discussions.unity.com/t/explain-the-unity-tone-mapping/817464)
- [Forum - Custom tonemapping URP](https://forum.unity.com/threads/how-to-do-custom-tone-mapping-instead-of-neutral-aces-in-urp.849280/)

### Errore: HDR Output non funziona / banding su display HDR
**Sintomo**: target HDR display ma output appare LDR o saturato.
**Causa probabile**: configurazione Paper White/Min/Max non impostata.
**Soluzione**: Volume Profile -> abilitare `HDR Output`; configurare `Paper White`, `Min Nits`, `Max Nits` in base al display. URP Asset -> `Quality > HDR = ON`, formato `R11G11B10` o `R16G16B16A16` per maggior precisione.

**Fonti**: [Unity Manual - HDR Output URP](https://docs.unity3d.com/Manual/urp/post-processing/hdr-output.html)

---

## 10. Anti-Aliasing (MSAA / SMAA / FXAA / TAA)

### Errore: MSAA non ha effetto
**Sintomo**: edges aliased nonostante MSAA = 4x.
**Causa probabile**:
- Rendering path `Deferred`: MSAA NON e` supportato in deferred (limitazione hardware).
- Mobile: se `Opaque Texture = ON` su platform senza StoreAndResolve, MSAA viene ignorato.
- Post-processing TAA attivo (incompatibile con MSAA).
**Soluzione**: usare `Forward` o `Forward+`; disabilitare Opaque Texture o usare SMAA/FXAA per mobile; non usare TAA con MSAA.

**Fonti**:
- [Unity Manual - Anti-aliasing URP](https://docs.unity3d.com/Manual/urp/anti-aliasing.html)
- [Discussions - URP and Anti-Aliasing](https://discussions.unity.com/t/urp-and-anti-aliasing/866011)

### Errore: SMAA / FXAA non visibile
**Sintomo**: cambiata l'opzione AA sulla camera ma niente cambia.
**Causa probabile**: `Anti-aliasing` impostato sulla camera ma `Post Processing` disabilitato sulla camera; URP applica SMAA/FXAA come post-step.
**Soluzione**: abilitare `Post Processing` sul componente Camera + selezionare `Anti-aliasing > FXAA`/`SMAA`.

**Fonti**: [Discussions - Where is AA post processing URP](https://discussions.unity.com/t/solved-where-is-the-anti-aliasing-post-processing-effect-on-urp/764232)

---

## 11. Decals

### Errore: "Current renderer has no Decal Feature added"
**Sintomo**: messaggio sul componente Decal Projector, decal invisibile.
**Causa probabile**: il Decal Renderer Feature non e` nel Universal Renderer Asset attivo (puo` essere aggiunto a quello sbagliato se ci sono piu` renderer).
**Soluzione**:
1. Selezionare il `Universal Renderer Data` in uso dall'URP Asset attivo (controllare Quality Settings -> URP Asset -> Renderer List).
2. `Add Renderer Feature > Decal`.
3. Verificare che il materiale del proiettore usi `Shader Graphs/Decal`.

**Fonti**: [Discussions - Decal feature not added](https://discussions.unity.com/t/trying-to-get-decals-working-getting-the-message-the-current-renderer-has-no-decal-feature-added-when-im-pretty-sure-i-added-a-decal-feature-to-the-renderer/265764)

### Errore: Decals non renderizzati in Deferred
**Sintomo**: tutto OK in Forward, in Deferred niente decals.
**Causa probabile**: bug noto - Screen Space decals non funzionano con Deferred+ in alcune versioni 2022/2023.
**Soluzione**: nel Decal Renderer Feature impostare `Technique = DBuffer` invece di Screen Space; in alternativa rimanere in Forward+.

**Fonti**: [Issue Tracker - URP Screen Space decals not rendering Deferred](https://issuetracker.unity3d.com/issues/urp-decals-are-not-rendering-when-deferred-rendering-path-is-set-and-screen-space-technique-is-used)

---

## 12. Camera Stacking

### Errore: Post-processing "duplicato" / artefatti su camera stack
**Sintomo**: bloom o tonemap sembrano applicati due volte; UI overlay alterata da PP della scena.
**Causa probabile**: URP applica post-processing solo all'ULTIMA camera dello stack. Se due camere hanno `Post Processing = ON`, si genera comportamento anomalo.
**Soluzione**: abilitare Post Processing **SOLO** sulla camera Overlay finale (l'ultima dello stack). Le Base camera devono averlo OFF se in stack.

**Fonti**:
- [Unity Manual - Camera stacking URP](https://docs.unity3d.com/Manual/urp/cameras/camera-stacking-concepts.html)
- [Discussions - Separate Post-Processes Camera Stacking](https://discussions.unity.com/t/separate-post-processes-on-stacked-cameras-post-process-duplication-issue-urp/1576974)

### Errore: PP separati per Game vs UI (impossibile nativamente)
**Sintomo**: voglio bloom solo sul gameplay, no su UI.
**Causa probabile**: limitazione architetturale URP (PP eseguito una sola volta a fine stack).
**Soluzione**: workaround tramite Render Texture (camera A renderizza scena -> RT -> camera B compone con UI senza PP) o custom render pass.

**Fonti**: [Discussions - Camera stacking URP UI post processing](https://discussions.unity.com/t/camera-stacking-urp-how-to-apply-different-post-processing-to-the-ui/777130)

---

## 13. Renderer Features Custom

### Errore: ScriptableRendererFeature non viene eseguita
**Sintomo**: aggiunta una feature custom nel Renderer Data, ma il render pass non si esegue mai.
**Causa probabile** (in ordine di frequenza):
1. La feature non e` aggiunta al Renderer Data **attivo** (controllare URP Asset -> Renderer in uso).
2. `EnqueuePass` non chiamato in `AddRenderPasses`.
3. Material o Mesh referenziati sono null -> Unity skippa il pass silenziosamente.
4. `renderPassEvent` impostato dopo che il target buffer non e` piu` valido.
5. Unity 6: API obsolete (`Execute`, `OnCameraSetup`) richiedono migrazione a RenderGraph.
**Soluzione**: verificare i 5 punti, controllare Frame Debugger per confermare che il pass venga schedulato.

**Fonti**:
- [Unity Manual - Custom render pass workflow URP](https://docs.unity3d.com/Manual/urp/renderer-features/custom-rendering-pass-workflow-in-urp.html)
- [Discussions - URP Custom Render Feature not working](https://discussions.unity.com/t/urp-custom-render-feature-not-working/273935)
- [Cyanilux - Custom Renderer Features](https://www.cyanilux.com/tutorials/custom-renderer-features/)

---

## 14. Volumetric Fog / Distance Fog

### Errore: Volumetric Fog non disponibile in URP
**Sintomo**: cercato VolumetricFog override nel Volume Profile, non presente.
**Causa probabile**: URP NON ha volumetric fog nativo (a differenza di HDRP). Solo distance fog standard (Lighting > Environment > Fog) e local volumetric fog disponibile in URP recenti (Unity 6) come renderer feature.
**Soluzione**:
- Per distance fog: `Window > Rendering > Lighting > Environment > Fog` (Linear/Exp/Exp2).
- Per volumetric/lighting volumetrico: usare custom render pass (es. ssell/UnityURPVolumetricFog), asset store (Buto, AERO), o URP Local Volumetric Fog (Unity 6).

**Fonti**:
- [VertexFragment - Custom URP Volumetric Fog](https://www.vertexfragment.com/ramblings/urp-volumetric-fog/)
- [GitHub - sinnwrig/URP-Fog-Volumes](https://github.com/sinnwrig/URP-Fog-Volumes)

### Errore: Tree billboard ignora la fog
**Sintomo**: alberi vicini con fog, billboard distanti renderizzati senza fog (colore pieno innaturale).
**Causa probabile**: bug noto - i billboard tree generati dal terrain system NON applicano fog automaticamente.
**Soluzione**: alzare `Billboard Start = Tree Distance` per disabilitare gli impostor (mai billboard), oppure aggiungere LOD Group ai tree prefab cosi che usino LOD veri invece del default impostor.

**Fonti**:
- [Discussions - Tree billboards fog and transparency](https://discussions.unity.com/t/tree-billboards-fog-and-transparency-issue/698393)
- [Discussions - Fog ignored by billboarded trees](https://discussions.unity.com/t/fog-ignored-by-billboarded-trees-in-unity-5/134222)

---

## 15. Performance / SRP Batcher

### Errore: URP piu` lento del Built-in
**Sintomo**: stessa scena, URP rende a fps inferiori.
**Causa probabile**: scenari frequenti:
- Forward+ usato con poche luci (overhead clustering > 6 lights).
- SRP Batcher abilitato ma materiali non compatibili (frequent batch break).
- Static/Dynamic batching disabilitati erroneamente.
- Post-processing pesante (Bloom HDR + TAA) su mobile.
**Soluzione**:
1. Frame Debugger: contare draw calls e SRP Batcher batches.
2. Profilare: se < 6 luci usare Forward classico.
3. Verificare shader compatibility: Inspector dello shader -> "SRP Batcher: Compatible".
4. Su mobile evitare Bloom heavy / TAA / SSAO.

**Fonti**:
- [Issue Tracker - URP performance worse with SRP Batcher](https://issuetracker.unity3d.com/issues/urp-performance-is-worse-when-srp-batcher-is-enabled-in-the-urp-asset)
- [TheGameDev.Guru - Forward+ Unity URP](https://thegamedev.guru/unity-gpu-performance/forward-plus/)

### Errore: GPU Instancing non funziona ("Saved by batching: 0")
**Sintomo**: anche con `Enable GPU Instancing` sui materiali, Frame Debugger mostra 0 instances.
**Causa probabile**: URP preferisce SRP Batcher su Instancing. Se shader e` SRP-Batcher-compatible, instancing viene ignorato.
**Soluzione**:
- Per forzare instancing: rendere il materiale incompatibile con SRP Batcher (es. usare MaterialPropertyBlock per renderer specifico, oppure shader senza CBUFFER UnityPerMaterial).
- In genere accettare SRP Batcher (CPU win simile).

**Fonti**:
- [Unity Manual - Remove SRP Batcher compat URP](https://docs.unity3d.com/Manual/SRPBatcher-Incompatible.html)
- [Discussions - SRP Batcher GPU Instancing confusion](https://discussions.unity.com/t/urp-mobile-srp-batcher-gpu-instancing-confusion-node-have-different-shaders-saved-by-batching-0-instancing-not-showing/1693371)

---

## 16. Build / Editor Errors

### Errore: BuildPlayer fallisce con "Shader X not found"
**Sintomo**: build interrotta, log: shader URP referenziato non trovato.
**Causa probabile**: shader stripped + Always Included Shaders incompleto, oppure URP package corrotto.
**Soluzione**: Reimport URP package; aggiungere shader mancanti in Graphics > Always Included; pulire `Library/ShaderCache`.

**Fonti**: [Discussions - URP shader not found in built game](https://discussions.unity.com/t/urp-shader-not-found-in-built-game/791233)

### Errore: PaintContext.ApplyDelayedActions exception su Terrain
**Sintomo**: editor exception dopo painting su terrain con URP.
**Causa probabile**: bug di sincronizzazione editor terrain + URP shader compile, talvolta accentuato da Cache Server / Accelerator.
**Soluzione**: chiudere Unity, eliminare `Library/`, riaprire. Se persiste, reimportare il TerrainData asset.

**Fonti**: [Issue Tracker - URP Terrain Lit base pass shader does not compile](https://issuetracker.unity3d.com/issues/urp-terrain-slash-lit-base-pass-shader-does-not-compile)

### Errore: Material Upgrade Tool freeze infinito
**Sintomo**: `Edit > Rendering > Materials > Convert All Built-in Materials to URP` parte e non termina (12+ ore segnalate).
**Causa probabile**: bug noto in 2021.2 / 2022.3 / Unity 6 quando il global RP asset non e` settato o quando ci sono asset corrotti.
**Soluzione**:
1. Settare URP Asset in Graphics PRIMA di lanciare il converter.
2. Usare `Window > Rendering > Render Pipeline Converter` (piu` recente, batch-able) invece del menu legacy.
3. Convertire in lotti piccoli (selezionando cartelle). Se freeza, killare Unity, riavviare.
4. **Alternativa**: usare questo tool ([URP Material Converter Plus](https://github.com/bobbonauta/URPMaterialConverterPlus)) che non si blocca mai.

**Fonti**:
- [Issue Tracker - URP Converter freezes during conversion](https://issuetracker.unity3d.com/issues/urp-built-in-to-urp-render-pipeline-converter-freezes-during-material-conversion)
- [Discussions - URP Material Converter loading forever](https://discussions.unity.com/t/urp-material-converter-loading-forever-stuck-unity-2021-2-beta/852775)

### Errore: Reimport loop infinito dopo aggiunta URP
**Sintomo**: dopo install URP, Unity reimporta all'infinito shadergraphs/materials.
**Causa probabile**: cache invalidation + asset URP-incompatibili in pipeline non-URP.
**Soluzione**: chiudere Unity, eliminare `Library/`, riaprire. Verificare versione Unity (fixed in 6000.0.44f1). Disattivare Cache Server temporaneamente.

**Fonti**:
- [Issue Tracker - URP infinite import loops](https://issuetracker.unity3d.com/issues/general-assetdb-urp-adding-urp-to-the-project-causes-reimports-of-many-assets-and-goes-into-infinite-import-loops)
- [Discussions - Cache error upgrading packages](https://discussions.unity.com/t/cache-error-when-upgrading-packages-results-in-invalid-scripts-which-then-causes-asset-corruption/933434)

---

## 17. Custom Shader Migration

### Errore: Surface Shader pink dopo migrazione URP
**Sintomo**: `#pragma surface` shader appare magenta.
**Causa probabile**: URP NON supporta Surface Shaders (sono Built-in only).
**Soluzione**: riscrivere come vertex/fragment shader con includes URP, oppure ricreare in Shader Graph. Steps minimi:
1. `CGPROGRAM` -> `HLSLPROGRAM`, `ENDCG` -> `ENDHLSL`.
2. Tag: `"RenderPipeline" = "UniversalPipeline"`.
3. Include `Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl` (+ `Lighting.hlsl` se PBR).
4. Material properties dentro `CBUFFER_START(UnityPerMaterial) ... CBUFFER_END` (richiesto per SRP Batcher).
5. Struct `v2f` -> `Varyings`, `appdata` -> `Attributes`.
6. LightMode tags per ogni Pass: `UniversalForward`, `ShadowCaster`, `DepthOnly`, `DepthNormals`.

**Fonti**:
- [Unity Manual - Upgrade custom shaders URP](https://docs.unity3d.com/Manual/urp/urp-shaders/birp-urp-custom-shader-upgrade-guide.html)
- [Cyanilux - URP Shader Code](https://www.cyanilux.com/tutorials/urp-shader-code/)
- [Unity Blog - Migrating built-in shaders to URP](https://blog.unity.com/engine-platform/migrating-built-in-shaders-to-the-universal-render-pipeline)

---

## 18. Particle Shader Migration

### Errore: Particles/Standard Unlit pink in URP
**Sintomo**: particelle Built-in con shader `Particles/Standard Unlit` appaiono magenta.
**Causa probabile**: shader non URP-compatible.
**Soluzione**: cambiare a `Universal Render Pipeline/Particles/Unlit` (o `/Lit` per particelle illuminate). Verificare i flag `Surface Type` (Opaque/Transparent), `Blending Mode`, e `Color Mode` (per il vertex color delle particle).

**Fonti**:
- [Unity Manual - Particles Unlit Shader URP](https://docs.unity3d.com/Manual/urp/particles-unlit-shader.html)
- [GitHub - NovaShader (URP particle multi-shader)](https://github.com/CyberAgentGameEntertainment/NovaShader)

---

## 19. TextMesh Pro

### Errore: TMP testo pink in URP
**Sintomo**: tutti i `TextMeshPro` mostrano caratteri magenta.
**Causa probabile**: TMP usa per default i suoi shader Built-in `TextMeshPro/Distance Field` non compatibili. Il `Distance Field Surface` Shader e` un Surface Shader -> NON funziona in URP.
**Soluzione**:
1. Importare gli shader URP per TMP: `Window > TextMeshPro > Import TMP Essential Resources` (in Unity 6 spesso gia` URP-aware).
2. Sui material preset TMP cambiare shader a `TextMeshPro/Distance Field SSD` (URP) o `TMP_SDF-URP Lit`.
3. Per material preset esistenti rigenerati con `Reset` material in inspector dopo cambio shader.

**Fonti**:
- [Issue Tracker - TMP Distance Field Surface pink in URP](https://issuetracker.unity3d.com/issues/tmp-distance-field-surface-shader-is-pink-when-using-urp)
- [Discussions - Custom TMP URP shader Unity 6](https://discussions.unity.com/t/upgraded-to-6000-0-and-my-custom-tmp-urp-shader-rendered-failed/947435)

---

## 20. Skybox

### Errore: Skybox/Procedural Built-in pink in URP
**Sintomo**: skybox procedurale appare a quadri pink.
**Causa probabile**: il vecchio `Skybox/Procedural` non e` URP-friendly in alcune build / piattaforme; meglio usare cubemap o shader graph.
**Soluzione**:
- Usare materiale `Skybox/Cubemap` con cubemap HDR.
- Per procedural custom: Shader Graph con Master Node `Unlit`, abilitare `Two Sided`, assegnare a `Lighting > Environment > Skybox Material`.
- In URP recente esiste anche `Skybox/Panoramic` per equirettangolari.

**Fonti**:
- [Unity Manual - Configure skybox shader](https://docs.unity3d.com/Manual/skybox-shaders.html)
- [Tim Coster - URP Procedural Skybox](https://timcoster.com/2019/09/03/unity-shadergraph-skybox-quick-tutorial/)

---

## 21. Water Shaders

### Errore: Water Built-in (Standard Asset) pink
**Sintomo**: water prefabs Unity Standard Assets non funzionano in URP.
**Causa probabile**: shader water Built-in usa GrabPass (non supportato URP) e Surface Shader.
**Soluzione**: in URP non esiste GrabPass. Sostituire con: Unity 6 `Water System` (package), oppure soluzioni community/asset:
- KWS Water (asset store).
- `URP Water` di staggart.
- Stylized Water by Alexander Ameye.
- Per refraction custom: abilitare `Opaque Texture` nell'URP Asset, sample-are `_CameraOpaqueTexture` nello shader graph.

**Fonti**:
- [Ameye - Stylized Water Shader](https://ameye.dev/notes/stylized-water-shader/)
- [GitHub - aniruddhahar/URP-WaterShaders](https://github.com/aniruddhahar/URP-WaterShaders)

### Errore: Refraction in water non funziona
**Sintomo**: water trasparente non rifrange l'oggetto sotto.
**Causa probabile**: `Opaque Texture` disabilitato nell'URP Asset.
**Soluzione**: URP Asset -> `Rendering > Opaque Texture = ON`. Su mobile considerare costo (extra blit). `Opaque Downsampling = None` se artefatti rosso/blu.

**Fonti**: [Stylized Water Troubleshooting](https://alexander-ameye.gitbook.io/stylized-water/support/troubleshooting)

---

## 22. URP + HDRP Conflicts

### Errore: HDRP e URP entrambi installati -> errori di compilazione
**Sintomo**: `Both HDRP and URP packages are installed` o errori shader compilation.
**Causa probabile**: i due pacchetti non sono compatibili nello stesso progetto contemporaneamente (alcune dipendenze in conflitto).
**Soluzione**: scegliere UNA pipeline. Rimuovere l'altra dal Package Manager. Cancellare `Library/`. Reinstallare solo la pipeline desiderata + reimportare asset.

**Fonti**:
- [Discussions - Switching between pipelines](https://discussions.unity.com/t/switching-between-pipelines-default-upr-and-hdrp-when-build/866999)
- [Unity Manual - Best Practice making believable visuals](https://docs.unity3d.com/Manual/BestPracticeMakingBelievableVisuals0.html)

---

## 23. Light Cookies

### Errore: Light Cookie su spot/point light non visibile in URP
**Sintomo**: assegnata cookie texture alla light, scena non mostra il pattern.
**Causa probabile**:
- Versioni URP < 12 non supportavano cookie su additional lights.
- Point light richiede Cubemap (non 2D).
- Cookie Atlas Format non impostato a Color (necessario per cookie a colori).
**Soluzione**:
1. Aggiornare a URP 12+ (Unity 2021.2+).
2. URP Asset -> `Lighting > Cookie Atlas Format = Color`/`Color High` (HDR).
3. Per point light usare cubemap; per spot, texture 2D con tipo "Spotlight".

**Fonti**: [Discussions - Light Cookies URP 2021.3](https://discussions.unity.com/t/light-cookies-in-urp-unity-2021-3-11f1/899022)

---

## 24. Forward / Forward+ / Deferred

### Errore: scelta sbagliata di rendering path -> performance pessima
**Sintomo**: scena con molte luci lenta in Forward, oppure scena con poche luci lenta in Forward+.
**Causa probabile**: rendering path mal scelto.
**Soluzione (regole guida)**:
- **Forward** (classico): scene con < 6 luci real-time, mobile, target low-end. Limite 8 additional lights per oggetto.
- **Forward+**: > 6 luci real-time, fino a 16/32/256 light per camera. Costo clustering fisso, conviene oltre 6 luci.
- **Deferred**: tante luci dinamiche, no transparency in render path principale, no MSAA. Costo G-buffer fisso. NO Rendering Layers in alcuni scenari.
Cambio in URP Renderer Asset -> `Rendering Path`.

**Fonti**:
- [Unity Manual - Choose rendering path URP](https://docs.unity3d.com/Manual/urp/rendering-paths-comparison.html)
- [TheGameDev.Guru - Forward+ Unity URP 14](https://thegamedev.guru/unity-gpu-performance/forward-plus/)

---

## 25. Transparency / Sorting

### Errore: oggetti trasparenti renderizzati in ordine sbagliato
**Sintomo**: trasparenze "tagliate" o un oggetto trasparente sparisce dietro un altro.
**Causa probabile**: trasparenti ordinati per distanza-centro, non per pixel. Mesh complesse con triangoli sovrapposti soffrono auto-overlap.
**Soluzione**:
- Spezzare mesh complesse in pezzi separati.
- Usare `Render Queue` (3000+ = Transparent) per forzare ordine.
- `Sorting Group` su gerarchie 2D/UI.
- Custom `TransparencySortMode` se camera ortografica.
- Per VFX: usare `Sorting Priority` su Particle System.

**Fonti**:
- [Unity Scripting - TransparencySortMode](https://docs.unity3d.com/ScriptReference/TransparencySortMode.html)
- [Issue Tracker - URP VFX cannot be sorted with transparent](https://issuetracker.unity3d.com/issues/urp-sorting-vfx-cannot-be-sorted-with-other-transparent-objects-in-urp)

---

## 26. SSAO (Screen Space Ambient Occlusion)

### Errore: SSAO aggiunto come renderer feature ma non visibile
**Sintomo**: feature aggiunta, no effetto.
**Causa probabile**:
- Material di alcuni shader non ha la pass DepthNormals -> SSAO con `Normal Source = Depth Normals` salta.
- Camera senza Post Processing.
**Soluzione**:
1. Renderer Data -> `Add Renderer Feature > Screen Space Ambient Occlusion`.
2. Camera: `Post Processing = ON`.
3. Se shader custom: aggiungere pass `DepthNormals` o usare `Normal Source = Depth` (ricostruisce dal depth, meno accurato ma universale).
4. `Direct Lighting Strength` > 0 per vederlo anche in zone illuminate.

**Fonti**: [Unity Manual - Configure SSAO URP](https://docs.unity3d.com/Manual/urp/ssao-renderer-feature-reference.html)

---

## 27. Depth / Opaque Texture

### Errore: Shader graph che usa `Scene Depth` o `Scene Color` mostra nero/strani valori
**Sintomo**: nodo Scene Depth / Scene Color in Shader Graph non funziona.
**Causa probabile**: Depth Texture / Opaque Texture disabilitati nell'URP Asset.
**Soluzione**: URP Asset -> `Rendering > Depth Texture = ON`, `Opaque Texture = ON`. Su mobile valutare costo (extra pass copy).

**Fonti**: [Discussions - URP depth texture doesn't work](https://discussions.unity.com/t/urp-depth-texture-doesnt-work/890184)

---

## Appendice — Checklist diagnostica rapida

Quando incontri un problema URP, verifica nell'ordine:

1. **URP Asset assegnato** in Graphics + ogni Quality Level.
2. **Renderer Data corretto** in uso (URP Asset puo` avere piu` renderer).
3. **Shader compatibili** (no Built-in/Standard, no Surface Shaders, no GrabPass).
4. **Always Included Shaders** comprende tutti gli shader URP necessari (Lit, SimpleLit, Particles, Terrain, SpeedTree8 PBRLit, TMP_SDF-URP).
5. **URP Asset settings** corretti: Depth Texture / Opaque Texture / HDR / SRP Batcher / Rendering Path adeguati.
6. **Volume + Camera Post Processing**: entrambi attivi e Layer Mask coerente.
7. **Renderer Features** (Decal, SSAO, custom) aggiunte al renderer giusto.
8. **Shader stripping**: log abilitato (`Editor.log` -> `ShaderStrippingReport`) per debug build.
9. **Library cache**: in caso di stranezze persistenti, chiudere Unity ed eliminare `Library/` (rebuild ~5-30 min).
10. **Frame Debugger** (`Window > Analysis > Frame Debugger`) per vedere chi disegna cosa, quando, e perche` un batch si rompe.

---

## Contributi

Hai trovato un errore URP non coperto qui? Apri una PR o issue su [URPMaterialConverterPlus](https://github.com/bobbonauta/URPMaterialConverterPlus) — questo doc cresce con la community.
