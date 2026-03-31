using UnityEngine;
using UnityEditor;

public class SelectionRenameEditor : EditorWindow
{
    private string removeString = "";
    private string addPrefix = "";
    private string addSuffix = "";

    [MenuItem("Tools/一括リネームツール")] // ご指定のToolsメニューに配置
    public static void ShowWindow()
    {
        // ウィンドウのサイズを固定して使いやすく
        SelectionRenameEditor window = GetWindow<SelectionRenameEditor>("リネームツール");
        window.minSize = new Vector2(300, 250);
    }

    private void OnGUI()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("ヒエラルキー選択オブジェクトを操作", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // --- 設定エリア ---
        EditorGUILayout.BeginVertical("box");

        removeString = EditorGUILayout.TextField("1. 削除する文字", removeString);
        EditorGUILayout.Space();

        addPrefix = EditorGUILayout.TextField("2. 先頭に追加", addPrefix);
        addSuffix = EditorGUILayout.TextField("3. 末尾に追加", addSuffix);

        EditorGUILayout.EndVertical();

        EditorGUILayout.Space();

        // --- 実行エリア ---
        GUI.backgroundColor = Color.cyan;
        if (GUILayout.Button("一括リネームを実行", GUILayout.Height(40)))
        {
            ExecuteCombinedRename();
        }
        GUI.backgroundColor = Color.white;

        if (GUILayout.Button("入力をクリア"))
        {
            removeString = addPrefix = addSuffix = "";
            GUI.FocusControl(null); // テキストフィールドのフォーカスを外す
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("ヒエラルキーでオブジェクトを複数選択してから実行してください。Undo (Ctrl+Z) に対応しています。", MessageType.Info);
    }

    private void ExecuteCombinedRename()
    {
        GameObject[] selectedObjects = Selection.gameObjects;

        if (selectedObjects.Length == 0)
        {
            EditorUtility.DisplayDialog("通知", "リネーム対象のオブジェクトを選択してください。", "OK");
            return;
        }

        // Undo履歴に登録（Ctrl+Zで戻せるようにする）
        Undo.RecordObjects(selectedObjects, "Bulk Rename Operation");

        int processedCount = 0;
        foreach (GameObject obj in selectedObjects)
        {
            string newName = obj.name;

            // 1. 文字列の削除（入力がある場合のみ）
            if (!string.IsNullOrEmpty(removeString))
            {
                newName = newName.Replace(removeString, "");
            }

            // 2. 前後の追加
            newName = addPrefix + newName + addSuffix;

            // 名前が変わった場合のみ適用
            if (obj.name != newName)
            {
                obj.name = newName;
                processedCount++;
            }
        }

        Debug.Log($"<color=cyan>[RenameTool]</color> {processedCount} 個のオブジェクトをリネームしました。");
    }
}
