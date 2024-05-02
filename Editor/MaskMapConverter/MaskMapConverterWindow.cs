using UnityEditor;
using UnityEngine;

public class MaskMapCreator : EditorWindow
{
    private Texture2D metalnessMap;
    private Texture2D smoothnessMap;
    private Texture2D aoMap;
    private Texture2D emissionMap;
    private string savePath = "Assets/MaskMap.png";

    // スムースネス反転のフラグ
    private bool invertSmoothness = false;

    [MenuItem("Tools/Mask Map Creator")]
    public static void ShowWindow()
    {
        GetWindow<MaskMapCreator>("Mask Map Creator");
    }

    void OnGUI()
    {
        GUILayout.Label("Mask Map Creator", EditorStyles.boldLabel);

        // 各マップを選択するスロット
        metalnessMap = (Texture2D)EditorGUILayout.ObjectField("Metalness Map (R)", metalnessMap, typeof(Texture2D), false);
        smoothnessMap = (Texture2D)EditorGUILayout.ObjectField("Smoothness Map (G)", smoothnessMap, typeof(Texture2D), false);
        aoMap = (Texture2D)EditorGUILayout.ObjectField("AO Map (B)", aoMap, typeof(Texture2D), false);
        emissionMap = (Texture2D)EditorGUILayout.ObjectField("Emission Map (A)", emissionMap, typeof(Texture2D), false);

        // スムースネスマップの反転チェックボックス
        invertSmoothness = EditorGUILayout.Toggle("Invert Smoothness Map", invertSmoothness);

        // 保存パスを設定
        savePath = EditorGUILayout.TextField("Save Path", savePath);

        // コンバートボタン
        if (GUILayout.Button("Create Mask Map"))
        {
            CreateMaskMap();
        }
    }

    void CreateMaskMap()
    {
        // テクスチャが選択されているか確認
        if (metalnessMap == null || smoothnessMap == null || aoMap == null || emissionMap == null)
        {
            Debug.LogError("Please assign all maps.");
            return;
        }

        // テクスチャのサイズ確認
        int width = metalnessMap.width;
        int height = metalnessMap.height;

        // 新しいマスクマップテクスチャを作成
        Texture2D maskMap = new Texture2D(width, height, TextureFormat.ARGB32, false);

        // 各ピクセルをループしてマスクマップを生成
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // メタルネスマップからRチャンネルの値を取得
                float r = metalnessMap.GetPixel(x, y).r;

                // スムースネスマップからGチャンネルの値を取得
                float g = smoothnessMap.GetPixel(x, y).g;

                // 反転フラグに基づいてスムースネスマップの色を反転
                if (invertSmoothness)
                {
                    g = 1 - g; // 色を反転
                }

                // アンビエントオクルージョンマップからBチャンネルの値を取得
                float b = aoMap.GetPixel(x, y).b;

                // エミッションマップからグレースケールの値を取得してAチャンネルに設定
                float a = emissionMap.GetPixel(x, y).grayscale;

                // マスクマップのピクセルを設定
                Color maskColor = new Color(r, g, b, a);
                maskMap.SetPixel(x, y, maskColor);
            }
        }

        // マスクマップを適用
        maskMap.Apply();

        // 保存パスにマスクマップを保存
        byte[] pngData = maskMap.EncodeToPNG();
        System.IO.File.WriteAllBytes(savePath, pngData);
        AssetDatabase.Refresh();

        Debug.Log($"Mask map saved to {savePath}");
    }
}
