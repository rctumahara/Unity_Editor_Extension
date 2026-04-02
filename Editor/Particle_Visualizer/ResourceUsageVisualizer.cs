using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine.Profiling;

public class ResourceUsageVisualizer : EditorWindow
{
    private Vector2 scrollPosition;
    private List<ParticleInfo> particleInfos = new List<ParticleInfo>();
    private List<MaterialData> materialDataList = new List<MaterialData>();
    private string prefabFileSize = "0 B";
    private string totalTextureMemory = "0 B";
    private GameObject lastSelection;

    [MenuItem("Window/Analysis/Resource Usage Visualizer")]
    public static void ShowWindow()
    {
        var window = GetWindow<ResourceUsageVisualizer>("Resource Visualizer");
        window.minSize = new Vector2(450, 600);
    }

    private void OnSelectionChange()
    {
        AnalyzeSelectedPrefab();
        Repaint();
    }

    private void AnalyzeSelectedPrefab()
    {
        particleInfos.Clear();
        materialDataList.Clear();
        long totalMemoryRaw = 0;

        GameObject target = Selection.activeGameObject;
        if (target == null) return;
        lastSelection = target;

        // --- 修正ポイント：Prefabのファイルパス取得 ---
        // PrefabUtility.GetPrefabAssetPath(target) を AssetDatabase.GetAssetPath(target) に変更
        string assetPath = AssetDatabase.GetAssetPath(target);

        // もしHierarchy上のインスタンスを選択している場合は、その元となるPrefabを探す
        if (string.IsNullOrEmpty(assetPath))
        {
            GameObject prefabParent = PrefabUtility.GetCorrespondingObjectFromSource(target);
            if (prefabParent != null)
            {
                assetPath = AssetDatabase.GetAssetPath(prefabParent);
            }
        }

        if (!string.IsNullOrEmpty(assetPath))
        {
            FileInfo fileInfo = new FileInfo(assetPath);
            if (fileInfo.Exists)
            {
                prefabFileSize = EditorUtility.FormatBytes(fileInfo.Length);
            }
        }

        // --- Particle System 解析 ---
        var particles = target.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in particles)
        {
            var emission = ps.emission;
            short burstMax = 0;
            if (emission.burstCount > 0)
            {
                ParticleSystem.Burst[] bursts = new ParticleSystem.Burst[emission.burstCount];
                emission.GetBursts(bursts);
                burstMax = bursts.Max(b => b.maxCount);
            }
            particleInfos.Add(new ParticleInfo {
                name = ps.name,
                emitterSummary = $"Rate: {emission.rateOverTime.constant} / Burst: {burstMax}"
            });
        }

        // --- マテリアル・テクスチャ・メモリ解析 ---
        var renderers = target.GetComponentsInChildren<Renderer>(true);
        var distinctMaterials = new HashSet<Material>();
        foreach (var r in renderers)
        {
            foreach (var mat in r.sharedMaterials) if (mat != null) distinctMaterials.Add(mat);
        }

        HashSet<Texture> allTextures = new HashSet<Texture>();
        foreach (var mat in distinctMaterials)
        {
            var data = new MaterialData { material = mat };
            Shader shader = mat.shader;
            for (int i = 0; i < ShaderUtil.GetPropertyCount(shader); i++)
            {
                if (ShaderUtil.GetPropertyType(shader, i) == ShaderUtil.ShaderPropertyType.TexEnv)
                {
                    Texture tex = mat.GetTexture(ShaderUtil.GetPropertyName(shader, i));
                    if (tex != null)
                    {
                        data.textures.Add(tex);
                        allTextures.Add(tex);
                    }
                }
            }
            materialDataList.Add(data);
        }

        foreach (var t in allTextures)
        {
            totalMemoryRaw += Profiler.GetRuntimeMemorySizeLong(t);
        }
        totalTextureMemory = EditorUtility.FormatBytes(totalMemoryRaw);
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            GUILayout.Label("Selected Prefab Info", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Target:", lastSelection?.name ?? "None");
            EditorGUILayout.LabelField("Prefab File Size:", prefabFileSize);

            GUI.color = new Color(0.7f, 1f, 0.7f);
            EditorGUILayout.LabelField("Total Texture VRAM:", totalTextureMemory, EditorStyles.boldLabel);
            GUI.color = Color.white;

            if (GUILayout.Button("Manual Refresh")) AnalyzeSelectedPrefab();
        }

        if (Selection.activeGameObject == null)
        {
            EditorGUILayout.HelpBox("Hierarchy上のPrefabを選択してください。", MessageType.Info);
            return;
        }

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        RenderHeader("▼ Particle Emitters");
        if (particleInfos.Count == 0) EditorGUILayout.LabelField("No Particle Systems found.", EditorStyles.miniLabel);
        foreach (var info in particleInfos)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(info.name, GUILayout.Width(180));
                EditorGUILayout.LabelField(info.emitterSummary, EditorStyles.miniLabel);
            }
        }

        EditorGUILayout.Space(10);
        RenderHeader("▼ Materials & Textures (Memory Usage)");
        foreach (var data in materialDataList)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.ObjectField(data.material, typeof(Material), false);
                EditorGUI.indentLevel++;
                foreach (var tex in data.textures.Distinct())
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        Rect texRect = EditorGUILayout.GetControlRect(GUILayout.Width(40), GUILayout.Height(40));
                        if (tex != null) GUI.DrawTexture(texRect, tex, ScaleMode.ScaleToFit);

                        EditorGUILayout.BeginVertical();
                        EditorGUILayout.ObjectField(tex, typeof(Texture), false);
                        long mem = Profiler.GetRuntimeMemorySizeLong(tex);
                        EditorGUILayout.LabelField($"{tex.width}x{tex.height} px | {EditorUtility.FormatBytes(mem)}", EditorStyles.miniLabel);
                        EditorGUILayout.EndVertical();
                    }
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.Space(4);
        }
        EditorGUILayout.EndScrollView();
    }

    private void RenderHeader(string title)
    {
        EditorGUILayout.LabelField(title, new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = new Color(0.3f, 0.7f, 1f) } });
    }

    private class ParticleInfo { public string name; public string emitterSummary; }
    private class MaterialData { public Material material; public List<Texture> textures = new List<Texture>(); }
}
