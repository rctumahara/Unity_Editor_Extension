using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;

// メッシュ側に「どこに保存したか」を覚えさせる
public class TerrainMeshLink : MonoBehaviour
{
    public Terrain sourceTerrain;
    public string bakedTexturePath;
    public string bakedMeshPath;
}

public class TerrainToMeshFull : EditorWindow
{
    private Terrain targetTerrain;
    private int resolutionReduction = 4;
    private bool useConvex = false;
    private int[] textureSizes = { 64, 128, 256, 512, 1024, 2048 };
    private int selectedSizeIndex = 4;

    private string[] allShaderNames;
    private int selectedShaderIndex = 0;
    private int selectedLayer = 0;
    private string exportDirectory = "Assets";

    [MenuItem("Tools/Terrain Full Converter")]
    public static void ShowWindow() => GetWindow<TerrainToMeshFull>("Terrain Converter");

    void OnEnable()
    {
        allShaderNames = ShaderUtil.GetAllShaderInfo()
            .Select(s => s.name)
            .Where(n => !n.StartsWith("Hidden/"))
            .OrderBy(n => n)
            .ToArray();

        for (int i = 0; i < allShaderNames.Length; i++)
        {
            if (allShaderNames[i] == "Universal Render Pipeline/Lit")
            {
                selectedShaderIndex = i;
                break;
            }
        }
    }

    void OnGUI()
    {
        GUILayout.Label("Terrain to Mesh Converter (Full Auto Clean)", EditorStyles.boldLabel);
        targetTerrain = (Terrain)EditorGUILayout.ObjectField("対象のTerrain", targetTerrain, typeof(Terrain), true);

        EditorGUILayout.BeginHorizontal();
        exportDirectory = EditorGUILayout.TextField("保存先フォルダ", exportDirectory);
        if (GUILayout.Button("選択", GUILayout.Width(50)))
        {
            string path = EditorUtility.OpenFolderPanel("保存先フォルダを選択", "Assets", "");
            if (!string.IsNullOrEmpty(path))
            {
                if (path.StartsWith(Application.dataPath)) {
                    exportDirectory = "Assets" + path.Substring(Application.dataPath.Length);
                }
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);
        resolutionReduction = EditorGUILayout.IntSlider("リダクション", resolutionReduction, 1, 8);
        string[] sizeOptions = { "64", "128", "256", "512", "1024", "2048" };
        selectedSizeIndex = EditorGUILayout.Popup("テクスチャ解像度 (px)", selectedSizeIndex, sizeOptions);
        if (allShaderNames != null && allShaderNames.Length > 0)
        {
            selectedShaderIndex = EditorGUILayout.Popup("使用シェーダー", selectedShaderIndex, allShaderNames);
        }
        selectedLayer = EditorGUILayout.LayerField("出力レイヤー", selectedLayer);
        useConvex = EditorGUILayout.Toggle("Convex (通常はOFF)", useConvex);

        EditorGUILayout.Space(10);
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("【変換】Mesh化 + PNG + Collider", GUILayout.Height(50))) ExecuteFullConvert();

        GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
        if (GUILayout.Button("【復元】全て戻す (PNG/Mesh削除込)", GUILayout.Height(30))) RevertCurrent();
    }

    void ExecuteFullConvert()
    {
        if (targetTerrain == null) return;
        RemoveExistingLinkedMesh(targetTerrain);

        // ディレクトリがなければ作成
        if (!Directory.Exists(exportDirectory)) Directory.CreateDirectory(exportDirectory);

        string pngPath = Path.Combine(exportDirectory, targetTerrain.name + "_Baked.png").Replace("\\", "/");
        BakeTexture(targetTerrain, textureSizes[selectedSizeIndex], pngPath);

        Mesh mesh = CreateMesh(targetTerrain.terrainData, resolutionReduction);
        string meshPath = Path.Combine(exportDirectory, targetTerrain.name + "_Mesh.asset").Replace("\\", "/");
        AssetDatabase.CreateAsset(mesh, meshPath);

        GameObject meshObj = new GameObject(targetTerrain.name + "_ConvertedMesh");
        meshObj.transform.position = targetTerrain.transform.position;
        meshObj.transform.rotation = targetTerrain.transform.rotation;
        meshObj.layer = selectedLayer;
        meshObj.isStatic = true;

        TerrainMeshLink link = meshObj.AddComponent<TerrainMeshLink>();
        link.sourceTerrain = targetTerrain;
        link.bakedTexturePath = pngPath;
        link.bakedMeshPath = meshPath;

        meshObj.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer mr = meshObj.AddComponent<MeshRenderer>();
        Shader targetShader = Shader.Find(allShaderNames[selectedShaderIndex]);
        Material mat = new Material(targetShader);
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);

        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        else if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
        else mat.mainTexture = tex;
        mr.material = mat;

        MeshCollider col = meshObj.AddComponent<MeshCollider>();
        col.sharedMesh = mesh;
        col.convex = useConvex;

        targetTerrain.gameObject.SetActive(false);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    void RevertCurrent()
    {
        if (targetTerrain == null) return;

        // シーン内のすべてのメッシュリンクをチェック
        TerrainMeshLink[] links = FindObjectsOfType<TerrainMeshLink>(true);
        foreach (var link in links)
        {
            if (link.sourceTerrain == targetTerrain)
            {
                // ここが重要：オブジェクトが持っているパスを直接消しにいく
                if (!string.IsNullOrEmpty(link.bakedTexturePath)) DeleteFile(link.bakedTexturePath);
                if (!string.IsNullOrEmpty(link.bakedMeshPath)) DeleteFile(link.bakedMeshPath);

                link.sourceTerrain.gameObject.SetActive(true);
                DestroyImmediate(link.gameObject);
            }
        }

        // 保険：現在の設定フォルダにあるファイルも消しにいく
        DeleteFile(Path.Combine(exportDirectory, targetTerrain.name + "_Baked.png"));
        DeleteFile(Path.Combine(exportDirectory, targetTerrain.name + "_Mesh.asset"));

        targetTerrain.gameObject.SetActive(true);
        AssetDatabase.Refresh();
    }

    void RemoveExistingLinkedMesh(Terrain t)
    {
        TerrainMeshLink[] links = FindObjectsOfType<TerrainMeshLink>(true);
        foreach (var link in links)
        {
            if (link.sourceTerrain == t)
            {
                if (!string.IsNullOrEmpty(link.bakedTexturePath)) DeleteFile(link.bakedTexturePath);
                if (!string.IsNullOrEmpty(link.bakedMeshPath)) DeleteFile(link.bakedMeshPath);
                DestroyImmediate(link.gameObject);
            }
        }
    }

    void DeleteFile(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        path = path.Replace("\\", "/");
        if (AssetDatabase.LoadMainAssetAtPath(path) != null)
        {
            AssetDatabase.MoveAssetToTrash(path);
        }
    }

    // --- BakeTexture と CreateMesh は前回と同じですが、一応含めます ---
    void BakeTexture(Terrain terrain, int res, string savePath)
    {
        TerrainData data = terrain.terrainData;
        foreach (var layer in data.terrainLayers)
        {
            string assetPath = AssetDatabase.GetAssetPath(layer.diffuseTexture);
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null && !importer.isReadable) { importer.isReadable = true; importer.SaveAndReimport(); }
        }
        float[,,] alphamaps = data.GetAlphamaps(0, 0, data.alphamapWidth, data.alphamapHeight);
        TerrainLayer[] layers = data.terrainLayers;
        Texture2D resultTex = new Texture2D(res, res, TextureFormat.RGB24, false);
        for (int y = 0; y < res; y++) {
            for (int x = 0; x < res; x++) {
                float normX = (float)x / res; float normY = (float)y / res;
                int mapX = Mathf.FloorToInt(normX * (data.alphamapWidth - 1));
                int mapY = Mathf.FloorToInt(normY * (data.alphamapHeight - 1));
                Color finalColor = Color.black;
                for (int i = 0; i < layers.Length; i++) {
                    float alpha = alphamaps[mapY, mapX, i];
                    if (alpha > 0) {
                        Texture2D layerTex = layers[i].diffuseTexture;
                        float tilingX = normX * (data.size.x / layers[i].tileSize.x) + layers[i].tileOffset.x;
                        float tilingY = normY * (data.size.z / layers[i].tileSize.y) + layers[i].tileOffset.y;
                        finalColor += layerTex.GetPixelBilinear(tilingX, tilingY) * alpha;
                    }
                }
                resultTex.SetPixel(x, y, finalColor);
            }
        }
        resultTex.Apply();
        File.WriteAllBytes(savePath, resultTex.EncodeToPNG());
        AssetDatabase.Refresh();
    }

    Mesh CreateMesh(TerrainData data, int res)
    {
        int w = data.heightmapResolution; int h = data.heightmapResolution;
        List<Vector3> vertices = new List<Vector3>(); List<Vector2> uvs = new List<Vector2>();
        Vector3 meshScale = data.size; meshScale.x /= (w - 1); meshScale.z /= (h - 1);
        for (int y = 0; y < h; y += res) {
            for (int x = 0; x < w; x += res) {
                vertices.Add(new Vector3(x * meshScale.x, data.GetHeight(x, y), y * meshScale.z));
                uvs.Add(new Vector2((float)x / (w - 1), (float)y / (h - 1)));
            }
        }
        List<int> tris = new List<int>(); int cols = (w - 1) / res + 1; int rows = (h - 1) / res + 1;
        for (int y = 0; y < rows - 1; y++) {
            for (int x = 0; x < cols - 1; x++) {
                tris.Add(y * cols + x); tris.Add((y + 1) * cols + x); tris.Add(y * cols + x + 1);
                tris.Add((y + 1) * cols + x); tris.Add((y + 1) * cols + x + 1); tris.Add(y * cols + x + 1);
            }
        }
        Mesh mesh = new Mesh(); mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = vertices.ToArray(); mesh.uv = uvs.ToArray(); mesh.triangles = tris.ToArray();
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
    }
}
