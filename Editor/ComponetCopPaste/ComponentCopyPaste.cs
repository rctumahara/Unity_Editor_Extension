using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class ComponentCopyPaste : EditorWindow
{
    private List<Component> copiedComponents = new List<Component>();
    private GameObject pasteTarget;
    private bool[] selectedComponents;

    [MenuItem("Custom Tools/Copy Components")]
    private static void CopyComponents()
    {
        ComponentCopyPaste window = GetWindow<ComponentCopyPaste>("Copy Components");
        window.ShowUtility();
    }

    [MenuItem("Custom Tools/Paste Components")]
    private static void PasteComponents()
    {
        ComponentCopyPaste window = GetWindow<ComponentCopyPaste>("Paste Components");
        window.ShowUtility();
    }

    private void OnGUI()
    {
        GUILayout.Label("Copy/Paste Components", EditorStyles.boldLabel);

        if (GUILayout.Button("Copy Components"))
        {
            if (Selection.activeGameObject != null)
            {
                copiedComponents = new List<Component>(Selection.activeGameObject.GetComponents<Component>());
                selectedComponents = new bool[copiedComponents.Count];
                Debug.Log("Components copied!");
            }
            else
            {
                Debug.LogWarning("No object selected to copy components from.");
            }
        }

        if (copiedComponents.Count > 0)
        {
            EditorGUILayout.LabelField("Copied Components:");
            EditorGUI.indentLevel++;
            for (int i = 0; i < copiedComponents.Count; i++)
            {
                selectedComponents[i] = EditorGUILayout.ToggleLeft(copiedComponents[i].GetType().Name, selectedComponents[i]);
            }
            EditorGUI.indentLevel--;
        }

        if (GUILayout.Button("Paste Components"))
        {
            if (copiedComponents.Count > 0 && pasteTarget != null)
            {
                for (int i = 0; i < copiedComponents.Count; i++)
                {
                    if (selectedComponents[i])
                    {
                        Component componentToPaste = copiedComponents[i];
                        Component existingComponent = pasteTarget.GetComponent(componentToPaste.GetType());
                        if (existingComponent == null)
                        {
                            UnityEditorInternal.ComponentUtility.CopyComponent(componentToPaste);
                            UnityEditorInternal.ComponentUtility.PasteComponentAsNew(pasteTarget);
                        }
                        else
                        {
                            Debug.LogWarning("Component " + componentToPaste.GetType().Name + " already exists on " + pasteTarget.name);
                        }
                    }
                }
                Debug.Log("Components pasted to " + pasteTarget.name);
            }
            else
            {
                Debug.LogWarning("No components copied or no paste target selected.");
            }
        }

        EditorGUI.BeginChangeCheck();
        pasteTarget = EditorGUILayout.ObjectField("Paste Target", pasteTarget, typeof(GameObject), true) as GameObject;
        if (EditorGUI.EndChangeCheck())
        {
            if (pasteTarget != null)
            {
                Debug.Log("Paste target set to " + pasteTarget.name);
            }
        }
    }
}
