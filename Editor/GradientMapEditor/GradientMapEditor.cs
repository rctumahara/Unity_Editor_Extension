using UnityEngine;
using UnityEditor;

public class GradientMapEditor : EditorWindow
{
    Gradient gradient;
    Texture2D gradientTexture;
    int textureWidth = 256; // テクスチャの幅の初期値
    string textureName = "New Gradient Texture"; // テクスチャの名前の初期値
    bool horizontalGradient = false; // グラデーションの向き（横方向または縦方向）の初期値

    [MenuItem("Window/Gradient Map Editor")]
    public static void ShowWindow()
    {
        GetWindow(typeof(GradientMapEditor));
    }

    void OnGUI()
    {
        // グラデーションの生成
        if (gradient == null)
        {
            gradient = new Gradient();
            gradient.SetKeys(new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.black, 1f) }, new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        }

        // グラデーションエディター
        gradient = EditorGUILayout.GradientField("Gradient", gradient);

        // テクスチャの幅を設定するフィールド
        textureWidth = EditorGUILayout.IntField("Texture Width", textureWidth);

        // テクスチャ名を設定するフィールド
        textureName = EditorGUILayout.TextField("Texture Name", textureName);

        // グラデーションの向きを設定するトグル
        horizontalGradient = EditorGUILayout.Toggle("Horizontal Gradient", horizontalGradient);

        // テクスチャの生成
        if (GUILayout.Button("Create Gradient Texture"))
        {
            CreateGradientTexture();
        }

        // グラデーションテクスチャのプレビュー
        if (gradientTexture != null)
        {
            GUILayout.Label("Gradient Texture Preview:");
            GUILayout.Label(gradientTexture, GUILayout.Width(position.width), GUILayout.Height(50));
        }
    }

    void CreateGradientTexture()
    {
        if (horizontalGradient)
            gradientTexture = CreateHorizontalGradientTexture(textureWidth);
        else
            gradientTexture = CreateVerticalGradientTexture(textureWidth);

        // テクスチャをAssetsに保存
        string path = EditorUtility.SaveFilePanelInProject("Save Gradient Texture", textureName, "png", "Please enter a file name to save the texture to");
        if (path.Length != 0)
        {
            byte[] bytes = gradientTexture.EncodeToPNG();
            System.IO.File.WriteAllBytes(path, bytes);
            AssetDatabase.Refresh();
        }
    }

    Texture2D CreateVerticalGradientTexture(int width)
    {
        Texture2D texture = new Texture2D(1, width);
        texture.wrapMode = TextureWrapMode.Clamp;

        for (int i = 0; i < width; i++)
        {
            float t = Mathf.InverseLerp(0, width - 1, i);
            Color color = gradient.Evaluate(t);
            texture.SetPixel(0, i, color);
        }

        texture.Apply();
        return texture;
    }

    Texture2D CreateHorizontalGradientTexture(int width)
    {
        Texture2D texture = new Texture2D(width, 1);
        texture.wrapMode = TextureWrapMode.Clamp;

        for (int i = 0; i < width; i++)
        {
            float t = Mathf.InverseLerp(0, width - 1, i);
            Color color = gradient.Evaluate(t);
            texture.SetPixel(i, 0, color);
        }

        texture.Apply();
        return texture;
    }
}
