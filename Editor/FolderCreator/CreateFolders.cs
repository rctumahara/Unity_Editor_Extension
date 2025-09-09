using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public class CreateFolders : EditorWindow
{
    private string parentFolderPath = "Assets"; // 初期の親フォルダパス
    private string parentFolderName = "ParentFolder";
    private List<string> childFolderNames = new List<string> { "Child1", "Child2", "Child3", "Child4", "Child5" };

    [MenuItem("Tools/Create Folder Structure")]
    public static void ShowWindow()
    {
        GetWindow<CreateFolders>("Folder Creator");
    }

    private void OnGUI()
    {
        GUILayout.Label("Folder Structure Settings", EditorStyles.boldLabel);

        // 親フォルダのパスを選択
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Parent Path", parentFolderPath);
        if (GUILayout.Button("Select"))
        {
            string selectedPath = EditorUtility.OpenFolderPanel("Select Parent Folder", Application.dataPath, "");
            if (!string.IsNullOrEmpty(selectedPath))
            {
                // 絶対パスをUnityのAssetsパス形式に変換
                if (selectedPath.StartsWith(Application.dataPath))
                {
                    parentFolderPath = "Assets" + selectedPath.Substring(Application.dataPath.Length);
                }
                else
                {
                    Debug.LogError("Selected folder is outside of the Assets directory!");
                }
            }
        }
        EditorGUILayout.EndHorizontal();

        // 親フォルダ名を入力
        parentFolderName = EditorGUILayout.TextField("Parent Folder Name", parentFolderName);

        // 子フォルダ名リストを編集
        GUILayout.Label("Child Folder Names:");
        for (int i = 0; i < childFolderNames.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            childFolderNames[i] = EditorGUILayout.TextField($"Child {i + 1}", childFolderNames[i]);

            // 削除ボタン
            if (GUILayout.Button("-", GUILayout.Width(30)))
            {
                childFolderNames.RemoveAt(i);
                i--; // リスト更新に対応するためインデックスを調整
            }
            EditorGUILayout.EndHorizontal();
        }

        // フォルダ追加ボタン
        if (GUILayout.Button("Add Child Folder"))
        {
            childFolderNames.Add($"Child{childFolderNames.Count + 1}");
        }

        // 作成ボタン
        if (GUILayout.Button("Create Folders"))
        {
            CreateFolderStructure();
        }
    }

    private void CreateFolderStructure()
    {
        if (string.IsNullOrEmpty(parentFolderPath) || !AssetDatabase.IsValidFolder(parentFolderPath))
        {
            Debug.LogError("Invalid parent folder path.");
            return;
        }

        // 親フォルダのフルパス
        string parentFullPath = parentFolderPath + "/" + parentFolderName;

        // 親フォルダの作成
        if (!AssetDatabase.IsValidFolder(parentFullPath))
        {
            string parentBasePath = System.IO.Path.GetDirectoryName(parentFullPath);
            string folderName = System.IO.Path.GetFileName(parentFullPath);

            if (!AssetDatabase.IsValidFolder(parentBasePath))
            {
                Debug.LogError($"Invalid parent folder path: {parentBasePath}");
                return;
            }

            AssetDatabase.CreateFolder(parentBasePath, folderName);
        }

        // 子フォルダの作成
        foreach (string childName in childFolderNames)
        {
            string childPath = parentFullPath + "/" + childName;
            if (!AssetDatabase.IsValidFolder(childPath))
            {
                AssetDatabase.CreateFolder(parentFullPath, childName);
            }
        }

        AssetDatabase.Refresh();
        Debug.Log($"Folder structure created under: {parentFullPath}");
    }
}
