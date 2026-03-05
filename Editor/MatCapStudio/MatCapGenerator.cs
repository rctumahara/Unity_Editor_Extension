using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

public class MatCapStudio : EditorWindow
{
    private enum BlendMode { Multiply, Add, Overlay, Screen, Replace }
    private enum MappingMode { Normal, Polar }

    [System.Serializable]
    public class PointLightLayer {
        public bool enabled = true;
        public bool isExpanded = true;
        public Gradient gradient = new Gradient();
        public Vector2 offset = new Vector2(0.3f, 0.3f);
        public float scale = 0.5f;
        public float intensity = 1.0f;

        public PointLightLayer() {
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.0f, 0.5f) }
            );
        }
    }

    private BlendMode blendMode = BlendMode.Replace;
    private MappingMode mappingMode = MappingMode.Normal;
    private string fileName = "NewMatCap";
    private string savePath = "Assets";
    private int[] sizeOptions = { 64, 128, 256, 512, 1024, 2048 };
    private int selectedSizeIndex = 3;

    private Gradient baseGradient = new Gradient();
    private Vector2 baseOffset = Vector2.zero;
    private float baseScale = 1.0f;

    private List<PointLightLayer> lightLayers = new List<PointLightLayer>();

    private Texture2D sourceTexture;
    [Range(0, 1)] private float textureOpacity = 0.5f;
    [Range(0.1f, 10f)] private float polarTiling = 1.0f;

    private Texture2D previewTex;
    private Vector2 scrollPos;

    private bool baseSettingsExpanded = true;
    private bool lightSettingsExpanded = true;
    private bool textureSettingsExpanded = true;

    [MenuItem("Tools/MatCap Studio")]
    public static void ShowWindow() => GetWindow<MatCapStudio>("MatCap Studio");

    private void OnEnable() {
        if (baseGradient.colorKeys.Length <= 2) {
            baseGradient.SetKeys(new GradientColorKey[] { new GradientColorKey(new Color(0.2f,0.2f,0.2f), 0.0f), new GradientColorKey(Color.black, 1.0f) }, new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(1.0f, 1.0f) });
        }
        if (lightLayers.Count == 0) lightLayers.Add(new PointLightLayer());
        RefreshTexture();
    }

    private void OnGUI() {
        // タイトルのみのシンプルなヘッダー
        EditorGUILayout.Space(5);
        GUILayout.Label("🎨 MatCap Studio", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        EditorGUI.BeginChangeCheck();

        // --- 1. Base Gradient ---
        baseSettingsExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(baseSettingsExpanded, "1. Base Gradient");
        if (baseSettingsExpanded) {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            baseGradient = EditorGUILayout.GradientField("Gradient", baseGradient);
            baseOffset = EditorGUILayout.Vector2Field("Offset", baseOffset);
            baseScale = EditorGUILayout.Slider("Size (Scale)", baseScale, 0.1f, 5.0f);
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.Space(2);

        // --- 2. Point Light Layers ---
        lightSettingsExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(lightSettingsExpanded, $"2. Point Lights ({lightLayers.Count})");
        if (lightSettingsExpanded) {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            for (int i = 0; i < lightLayers.Count; i++) {
                var light = lightLayers[i];
                EditorGUILayout.BeginHorizontal();
                light.isExpanded = EditorGUILayout.Foldout(light.isExpanded, $"Light #{i + 1}", true);
                if (GUILayout.Button("Copy", EditorStyles.miniButton, GUILayout.Width(40))) { CopyLight(i); break; }
                if (GUILayout.Button("X", EditorStyles.miniButton, GUILayout.Width(25))) { lightLayers.RemoveAt(i); break; }
                EditorGUILayout.EndHorizontal();

                if (light.isExpanded) {
                    EditorGUI.indentLevel++;
                    light.enabled = EditorGUILayout.Toggle("Enabled", light.enabled);
                    if (light.enabled) {
                        light.gradient = EditorGUILayout.GradientField("Color", light.gradient);
                        light.offset = EditorGUILayout.Vector2Field("Position", light.offset);
                        light.scale = EditorGUILayout.Slider("Size", light.scale, 0.01f, 2.0f);
                        light.intensity = EditorGUILayout.Slider("Intensity", light.intensity, 0f, 2f);
                    }
                    EditorGUI.indentLevel--;
                    EditorGUILayout.Space(5);
                }
            }
            if (GUILayout.Button("+ Add New Light")) lightLayers.Add(new PointLightLayer());
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.Space(2);

        // --- 3. Texture Overlay ---
                textureSettingsExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(textureSettingsExpanded, "3. Texture Overlay");
                if (textureSettingsExpanded) {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    sourceTexture = (Texture2D)EditorGUILayout.ObjectField("Texture", sourceTexture, typeof(Texture2D), false);
                    if (sourceTexture != null) {
                        CheckTextureSettings(sourceTexture);
                        mappingMode = (MappingMode)EditorGUILayout.EnumPopup("Mapping", mappingMode);

                        // 修正：Normal、PolarどちらのモードでもTilingを表示するようにしました
                        polarTiling = EditorGUILayout.Slider("Tiling", polarTiling, 0.1f, 10f);

                        blendMode = (BlendMode)EditorGUILayout.EnumPopup("Blend", blendMode);
                        textureOpacity = EditorGUILayout.Slider("Opacity", textureOpacity, 0f, 1f);
                    }
                    EditorGUILayout.EndVertical();
                }
                EditorGUILayout.EndFoldoutHeaderGroup();

        if (EditorGUI.EndChangeCheck()) RefreshTexture();

        EditorGUILayout.Space(10);

        // --- プレビュー ---
        if (previewTex != null) {
            float pSize = Mathf.Min(position.width - 60, 200);
            Rect r = GUILayoutUtility.GetRect(pSize, pSize);
            r.width = r.height = pSize;
            r.x = (position.width - pSize) / 2;
            EditorGUI.DrawRect(new Rect(r.x-2, r.y-2, r.width+4, r.height+4), Color.black);
            GUI.DrawTexture(r, previewTex, ScaleMode.ScaleToFit);
        }

        EditorGUILayout.Space(10);

        // --- Save Settings ---
        EditorGUILayout.LabelField("Save Settings", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        selectedSizeIndex = EditorGUILayout.Popup("Resolution", selectedSizeIndex, System.Array.ConvertAll(sizeOptions, x => x.ToString()));
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.TextField("Path", savePath);
        if (GUILayout.Button("...", GUILayout.Width(30))) {
            string folder = EditorUtility.OpenFolderPanel("Save Folder", savePath, "");
            if (!string.IsNullOrEmpty(folder) && folder.Contains(Application.dataPath))
                savePath = "Assets" + folder.Replace(Application.dataPath, "");
        }
        EditorGUILayout.EndHorizontal();
        fileName = EditorGUILayout.TextField("Name", fileName);
        if (GUILayout.Button("Bake as PNG", GUILayout.Height(35))) BakeAsPNG();
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndScrollView();
    }

    private void RefreshTexture() {
        int res = 256;
        if (previewTex == null || previewTex.width != res) previewTex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        previewTex.SetPixels(CalculatePixels(res));
        previewTex.Apply();
    }

    private Color[] CalculatePixels(int res) {
            Color[] colors = new Color[res * res];
            float center = res / 2f;
            for (int y = 0; y < res; y++) {
                for (int x = 0; x < res; x++) {
                    float nx = (x - center) / center;
                    float ny = (y - center) / center;
                    float r2_raw = nx * nx + ny * ny;
                    if (r2_raw > 1.0f) { colors[y * res + x] = Color.clear; continue; }

                    float dist1 = Vector2.Distance(new Vector2(nx, ny), baseOffset) / baseScale;
                    Color col = baseGradient.Evaluate(Mathf.Clamp01(dist1));

                    foreach (var light in lightLayers) {
                        if (!light.enabled) continue;
                        float distL = Vector2.Distance(new Vector2(nx, ny), light.offset) / light.scale;
                        Color pCol = light.gradient.Evaluate(Mathf.Clamp01(distL));
                        col += pCol * pCol.a * light.intensity;
                    }

                    if (sourceTexture != null && sourceTexture.isReadable) {
                        float u = 0, v = 0;
                        if (mappingMode == MappingMode.Normal) {
                            // Normalモードでもタイリングを適用
                            u = (nx * 0.5f + 0.5f) * polarTiling;
                            v = (ny * 0.5f + 0.5f) * polarTiling;
                        }
                        else {
                            float nz = Mathf.Sqrt(1.0f - r2_raw);
                            u = (Mathf.Atan2(nx, nz) / (2f * Mathf.PI) + 0.5f) * polarTiling;
                            v = (Mathf.Asin(ny) / Mathf.PI + 0.5f) * polarTiling;
                        }
                        Color texCol = sourceTexture.GetPixelBilinear(u, v);
                        col = ApplyBlend(col, texCol, textureOpacity);
                    }
                    colors[y * res + x] = new Color(col.r, col.g, col.b, 1.0f);
                }
            }
            return colors;
        }

    private Color ApplyBlend(Color b, Color t, float alpha) {
        Color res = b;
        switch (blendMode) {
            case BlendMode.Multiply: res = b * t; break;
            case BlendMode.Add:      res = b + t; break;
            case BlendMode.Overlay:  res = new Color(b.r < 0.5f ? 2*b.r*t.r : 1-2*(1-b.r)*(1-t.r), b.g < 0.5f ? 2*b.g*t.g : 1-2*(1-b.g)*(1-t.g), b.b < 0.5f ? 2*b.b*t.b : 1-2*(1-b.b)*(1-t.b)); break;
            case BlendMode.Screen:   res = Color.white - (Color.white - b) * (Color.white - t); break;
            case BlendMode.Replace:  res = t; break;
        }
        return Color.Lerp(b, res, alpha);
    }

    private void CopyLight(int index) {
        var l = lightLayers[index];
        var newL = new PointLightLayer() { enabled = l.enabled, offset = l.offset, scale = l.scale, intensity = l.intensity, isExpanded = true };
        newL.gradient = new Gradient(); newL.gradient.SetKeys(l.gradient.colorKeys, l.gradient.alphaKeys);
        lightLayers.Insert(index + 1, newL);
    }

    private void CheckTextureSettings(Texture2D tex) {
        string path = AssetDatabase.GetAssetPath(tex);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && !importer.isReadable) {
            if (GUILayout.Button("Fix Read/Write")) { importer.isReadable = true; importer.SaveAndReimport(); RefreshTexture(); }
        }
    }

    private void BakeAsPNG() {
        int size = sizeOptions[selectedSizeIndex];
        Texture2D bakeTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        bakeTex.SetPixels(CalculatePixels(size)); bakeTex.Apply();
        string fullPath = Path.Combine(Application.dataPath.Replace("Assets", ""), savePath, fileName + ".png");
        File.WriteAllBytes(fullPath, bakeTex.EncodeToPNG());
        AssetDatabase.Refresh(); DestroyImmediate(bakeTex);
        Debug.Log($"<b>MatCap Studio:</b> {savePath}/{fileName}.png を出力しました！");
    }
}
