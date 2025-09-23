using UnityEngine;
using UnityEditor;

public class SDFMergeTool : EditorWindow
{
    [System.Serializable]
    public class TextureSlot
    {
        public Texture2D texture;
        [Range(0f, 2f)] public float brightness = 1f; // 明るさ調整 (0=真っ暗, 1=そのまま, 2=2倍)
    }

    private int slotCount = 4; // 初期スロット数
    public TextureSlot[] textures = new TextureSlot[0];
    private Texture2D mergedTexture;

    [MenuItem("Tools/SDF Merge Tool")]
    static void Open()
    {
        GetWindow<SDFMergeTool>("SDF Merge Tool");
    }

    void OnGUI()
    {
        GUILayout.Label("SDFテクスチャ設定", EditorStyles.boldLabel);

        // スロット数を指定
        int newCount = EditorGUILayout.IntField("Slot Count", slotCount);
        if (newCount != slotCount && newCount >= 0)
        {
            slotCount = newCount;
            System.Array.Resize(ref textures, slotCount);
            for (int i = 0; i < textures.Length; i++)
            {
                if (textures[i] == null) textures[i] = new TextureSlot();
            }
        }

        GUILayout.Space(5);

        // 各スロットのUI
        for (int i = 0; i < textures.Length; i++)
        {
            EditorGUILayout.BeginVertical("box");
            textures[i].texture = (Texture2D)EditorGUILayout.ObjectField($"Slot {i}", textures[i].texture, typeof(Texture2D), false);
            textures[i].brightness = EditorGUILayout.Slider("Brightness", textures[i].brightness, 0f, 2f);
            EditorGUILayout.EndVertical();
        }

        GUILayout.Space(10);

        if (GUILayout.Button("Merge Textures to One"))
        {
            mergedTexture = MergeTextures();
            if (mergedTexture != null)
            {
                string path = EditorUtility.SaveFilePanel("保存先を選択", "Assets", "SDF_Merged.png", "png");
                if (!string.IsNullOrEmpty(path))
                {
                    System.IO.File.WriteAllBytes(path, mergedTexture.EncodeToPNG());
                    AssetDatabase.Refresh();
                    Debug.Log($"✅ SDFマージ完了: {path}");
                }
            }
        }
    }

    Texture2D MergeTextures()
    {
        if (textures.Length == 0 || textures[0].texture == null) return null;

        int width = textures[0].texture.width;
        int height = textures[0].texture.height;
        Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false);

        Color[] basePixels = new Color[width * height];
        for (int i = 0; i < basePixels.Length; i++)
            basePixels[i] = Color.black;

        // 各スロットをマージ
        foreach (var slot in textures)
        {
            if (slot == null || slot.texture == null) continue;
            if (!slot.texture.isReadable)
            {
                Debug.LogError($"❌ テクスチャ {slot.texture.name} が Read/Write Enabled ではありません！");
                continue;
            }

            Color[] pixels = slot.texture.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                basePixels[i] += pixels[i] * slot.brightness;
            }
        }

        result.SetPixels(basePixels);
        result.Apply();
        return result;
    }
}
