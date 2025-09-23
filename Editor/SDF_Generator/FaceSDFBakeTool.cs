using UnityEngine;
using UnityEditor;
using System.IO;

public class FaceSDFBakeTool : EditorWindow
{
    public GameObject targetObject;
    public Camera bakeCamera;
    public Light bakeLight;

    // インスペクターで指定するライト角度のリスト
    public Vector3[] lightAngles = new Vector3[] {
        new Vector3(0, 0, 0),
        new Vector3(0, 90, 0),
        new Vector3(0, 180, 0),
        new Vector3(0, 270, 0)
    };

    public int textureSize = 512;
    public string saveFolder = "Assets/BakedSDF";

    [MenuItem("Tools/Face SDF Bake Tool")]
    public static void ShowWindow()
    {
        GetWindow<FaceSDFBakeTool>("Face SDF Bake Tool");
    }

    void OnGUI()
    {
        targetObject = (GameObject)EditorGUILayout.ObjectField("Target Object", targetObject, typeof(GameObject), true);
        bakeCamera = (Camera)EditorGUILayout.ObjectField("Bake Camera", bakeCamera, typeof(Camera), true);
        bakeLight = (Light)EditorGUILayout.ObjectField("Bake Light", bakeLight, typeof(Light), true);

        SerializedObject so = new SerializedObject(this);
        SerializedProperty prop = so.FindProperty("lightAngles");
        EditorGUILayout.PropertyField(prop, true);
        so.ApplyModifiedProperties();

        textureSize = EditorGUILayout.IntField("Texture Size", textureSize);
        saveFolder = EditorGUILayout.TextField("Save Folder", saveFolder);

        if (GUILayout.Button("Bake"))
        {
            Bake();
        }
    }

    void Bake()
    {
        if (targetObject == null || bakeCamera == null || bakeLight == null)
        {
            Debug.LogError("Target, Camera, or Light not assigned!");
            return;
        }

        if (!Directory.Exists(saveFolder))
        {
            Directory.CreateDirectory(saveFolder);
        }

        for (int i = 0; i < lightAngles.Length; i++)
        {
            // ライトの角度を設定
            bakeLight.transform.rotation = Quaternion.Euler(lightAngles[i]);

            RenderTexture rt = new RenderTexture(textureSize, textureSize, 24);
            bakeCamera.targetTexture = rt;
            Texture2D tex = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);

            bakeCamera.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, textureSize, textureSize), 0, 0);
            tex.Apply();

            byte[] bytes = tex.EncodeToPNG();
            string filename = Path.Combine(saveFolder, $"SDF_{i}.png");
            File.WriteAllBytes(filename, bytes);

            bakeCamera.targetTexture = null;
            RenderTexture.active = null;
            DestroyImmediate(rt);
            DestroyImmediate(tex);

            Debug.Log($"Saved: {filename}");
        }
    }
}
