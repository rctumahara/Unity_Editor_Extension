using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public class CreateFolders : EditorWindow
{
    private string parentFolderPath = "Assets"; // �����̐e�t�H���_�p�X
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

        // �e�t�H���_�̃p�X��I��
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Parent Path", parentFolderPath);
        if (GUILayout.Button("Select"))
        {
            string selectedPath = EditorUtility.OpenFolderPanel("Select Parent Folder", Application.dataPath, "");
            if (!string.IsNullOrEmpty(selectedPath))
            {
                // ��΃p�X��Unity��Assets�p�X�`���ɕϊ�
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

        // �e�t�H���_�������
        parentFolderName = EditorGUILayout.TextField("Parent Folder Name", parentFolderName);

        // �q�t�H���_�����X�g��ҏW
        GUILayout.Label("Child Folder Names:");
        for (int i = 0; i < childFolderNames.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            childFolderNames[i] = EditorGUILayout.TextField($"Child {i + 1}", childFolderNames[i]);

            // �폜�{�^��
            if (GUILayout.Button("-", GUILayout.Width(30)))
            {
                childFolderNames.RemoveAt(i);
                i--; // ���X�g�X�V�ɑΉ����邽�߃C���f�b�N�X�𒲐�
            }
            EditorGUILayout.EndHorizontal();
        }

        // �t�H���_�ǉ��{�^��
        if (GUILayout.Button("Add Child Folder"))
        {
            childFolderNames.Add($"Child{childFolderNames.Count + 1}");
        }

        // �쐬�{�^��
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

        // �e�t�H���_�̃t���p�X
        string parentFullPath = parentFolderPath + "/" + parentFolderName;

        // �e�t�H���_�̍쐬
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

        // �q�t�H���_�̍쐬
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
