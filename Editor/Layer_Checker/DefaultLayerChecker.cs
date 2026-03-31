using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class DefaultLayerChecker : EditorWindow
{
    private List<GameObject> _defaultLayerObjects = new List<GameObject>();
    private Vector2 _scrollPosition;

    [MenuItem("Tools/Defaultレイヤーチェッカー")]
    public static void ShowWindow()
    {
        GetWindow<DefaultLayerChecker>("Layer Checker");
    }

    private void OnGUI()
    {
        GUILayout.Space(10);

        // 検索ボタン
        if (GUILayout.Button("シーン内のDefaultレイヤーを検索", GUILayout.Height(35)))
        {
            ScanScene();
        }

        GUILayout.Space(10);
        EditorGUILayout.LabelField($"該当オブジェクト: {_defaultLayerObjects.Count} 件", EditorStyles.boldLabel);
        GUILayout.Space(5);

        // リスト表示エリア（スクロール可能）
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUI.skin.box);
        {
            if (_defaultLayerObjects.Count == 0)
            {
                EditorGUILayout.LabelField("該当するオブジェクトはありません。");
            }
            else
            {
                for (int i = 0; i < _defaultLayerObjects.Count; i++)
                {
                    GameObject go = _defaultLayerObjects[i];

                    // 万が一シーンから削除された場合の null チェック
                    if (go == null) continue;

                    EditorGUILayout.BeginHorizontal();
                    {
                        // オブジェクト名をクリックで Hierarchy 上のオブジェクトを選択＆ Ping
                        if (GUILayout.Button(go.name, EditorStyles.label, GUILayout.ExpandWidth(true)))
                        {
                            Selection.activeGameObject = go;
                            EditorGUIUtility.PingObject(go); // Hierarchyでピカッと光らせる
                        }

                        // オブジェクトフィールド（ドラッグ＆ドロップや参照確認用）
                        EditorGUILayout.ObjectField(go, typeof(GameObject), true, GUILayout.Width(150));
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
        }
        EditorGUILayout.EndScrollView();
    }

    private void ScanScene()
    {
        _defaultLayerObjects.Clear();

        // シーン内のすべての GameObject を取得
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>()
            .Where(go => go.hideFlags == HideFlags.None && !EditorUtility.IsPersistent(go))
            .ToArray();

        foreach (GameObject go in allObjects)
        {
            if (go.layer == 0) // Default レイヤー
            {
                _defaultLayerObjects.Add(go);
            }
        }
    }
}
