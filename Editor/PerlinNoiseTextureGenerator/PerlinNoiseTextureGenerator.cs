using UnityEngine;
using UnityEditor;

public class PerlinNoiseTextureGenerator : EditorWindow
{
    Texture2D generatedTexture;
    int width = 256; // テクスチャの幅
    int height = 256; // テクスチャの高さ
    float scale = 20f; // ノイズのスケール
    string textureName = "PerlinNoiseTexture"; // 保存するテクスチャの名前
    int seed = 0; // seed値

    [MenuItem("Window/Perlin Noise Texture Generator")]
    public static void ShowWindow()
    {
        GetWindow(typeof(PerlinNoiseTextureGenerator));
    }

    void OnGUI()
    {
        GUILayout.Label("Perlin Noise Texture Generator", EditorStyles.boldLabel);

        width = EditorGUILayout.IntField("Width", width);
        height = EditorGUILayout.IntField("Height", height);

        float newScale = EditorGUILayout.Slider("Scale", scale, 1f, 100f); // スライダーを使用する
        int newSeed = EditorGUILayout.IntField("Seed", seed); // seed値の入力フィールド

        // スケールやシードの値が変更されたらテクスチャを再生成してプレビューを更新
        if (newScale != scale || newSeed != seed)
        {
            scale = newScale;
            seed = newSeed;
            GeneratePerlinNoiseTexture();
            Repaint();
        }

        if (GUILayout.Button("Generate Texture"))
        {
            GeneratePerlinNoiseTexture();
        }

        textureName = EditorGUILayout.TextField("Texture Name", textureName);

        // プレビューを表示
        if (generatedTexture != null)
        {
            GUILayout.Label("Preview:");
            GUILayout.Label(generatedTexture, GUILayout.Width(position.width), GUILayout.Height(100));
        }
    }

    void GeneratePerlinNoiseTexture()
    {
        generatedTexture = new Texture2D(width, height);

        // ピクセルごとにノイズ値を計算し、テクスチャに適用
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float xCoord = (float)x / width * scale;
                float yCoord = (float)y / height * scale;

                // seed値を指定してPerlinノイズを生成
                float noiseValue = PerlinNoiseGenerator.GetPerlinNoise(xCoord, yCoord, scale / 10f, 3, 0.5f, seed);
                Color color = new Color(noiseValue, noiseValue, noiseValue);
                generatedTexture.SetPixel(x, y, color);
            }
        }

        generatedTexture.Apply();

        // テクスチャを保存
        string path = EditorUtility.SaveFilePanel("Save Perlin Noise Texture", "", textureName, "png");
        if (!string.IsNullOrEmpty(path)) // 保存パスが空ではないことを確認
        {
            byte[] bytes = generatedTexture.EncodeToPNG();
            System.IO.File.WriteAllBytes(path, bytes);
            AssetDatabase.Refresh();
            Debug.Log("Perlin Noise texture saved as: " + path);
        }
    }
}

public static class PerlinNoiseGenerator
{
    public static float GetPerlinNoise(float x, float y, float frequency, int octaves, float persistence, int seed)
    {
        float total = 0;
        float amplitude = 1;
        float maxValue = 0;

        System.Random prng = new System.Random(seed);
        float offsetX = prng.Next(-100000, 100000);
        float offsetY = prng.Next(-100000, 100000);

        for (int i = 0; i < octaves; i++)
        {
            float sampleX = (x + offsetX) * frequency;
            float sampleY = (y + offsetY) * frequency;

            float perlinValue = Mathf.PerlinNoise(sampleX, sampleY) * 2 - 1;
            total += perlinValue * amplitude;
            maxValue += amplitude;

            amplitude *= persistence;
            frequency *= 2;
        }

        return total / maxValue;
    }
}
