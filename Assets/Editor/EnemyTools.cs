#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using System.Linq;

public static class EnemyTools
{
    private const int TargetHP = 100;

    [MenuItem("Tools/Enemies/Set Max HP To 100 in Open Scenes")]
    public static void SetMaxHPInOpenScenes()
    {
        int changed = 0;
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            var roots = scene.GetRootGameObjects();
            foreach (var r in roots)
            {
                var enemies = r.GetComponentsInChildren<Enemy>(true);
                foreach (var e in enemies)
                {
                    var so = new SerializedObject(e);
                    var prop = so.FindProperty("maxHealth");
                    if (prop != null && prop.intValue != TargetHP)
                    {
                        prop.intValue = TargetHP;
                        so.ApplyModifiedProperties();
                        EditorUtility.SetDirty(e);
                        changed++;
                    }
                }
            }
        }

        if (changed > 0)
        {
            Debug.Log($"Updated {changed} Enemy instances in open scenes to MaxHP={TargetHP}.");
            EditorSceneManager.MarkAllScenesDirty();
        }
        else Debug.Log("No Enemy instances needed updating in open scenes.");
    }

    [MenuItem("Tools/Enemies/Set Max HP To 100 In All Prefabs (Project)")]
    public static void SetMaxHPInPrefabs()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab");
        int changed = 0;
        foreach (var g in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) continue;
            var enemies = go.GetComponentsInChildren<Enemy>(true);
            if (enemies == null || enemies.Length == 0) continue;
            bool any = false;
            foreach (var e in enemies)
            {
                var so = new SerializedObject(e);
                var prop = so.FindProperty("maxHealth");
                if (prop != null && prop.intValue != TargetHP)
                {
                    prop.intValue = TargetHP;
                    so.ApplyModifiedProperties();
                    any = true;
                }
            }
            if (any)
            {
                EditorUtility.SetDirty(go);
                AssetDatabase.SaveAssets();
                changed++;
            }
        }
        Debug.Log($"Updated {changed} prefabs to MaxHP={TargetHP}.");
    }

    [MenuItem("Tools/Enemies/Print Enemy Stats Summary")]
    public static void PrintSummary()
    {
        var enemies = Object.FindObjectsByType<Enemy>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Debug.Log($"Found {enemies.Length} Enemy instances in scenes.");
        foreach (var e in enemies)
        {
            Debug.Log($"{e.name}: maxHP={GetField(e, "maxHealth")}, dmgMult={GetField(e, "damageTakenMultiplier")}");
        }
    }

    [MenuItem("Tools/Enemies/Assign Selected HPBar Prefab To All Enemies")]
    public static void AssignSelectedHPBarPrefab()
    {
        var obj = Selection.activeObject as GameObject;
        if (obj == null)
        {
            Debug.LogError("Select an HPBar prefab asset in the Project window before running this.");
            return;
        }

        // Verify selected asset has HPBarSprite
        var hpbar = obj.GetComponent<HPBarSprite>();
        if (hpbar == null)
        {
            Debug.LogError("Selected prefab does not contain an HPBarSprite component.");
            return;
        }

        int changedScenes = 0;
        // Update open scene instances
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            var roots = scene.GetRootGameObjects();
            foreach (var r in roots)
            {
                var enemies = r.GetComponentsInChildren<Enemy>(true);
                foreach (var e in enemies)
                {
                    var so = new SerializedObject(e);
                    var prop = so.FindProperty("hpBarPrefab");
                    if (prop != null)
                    {
                        prop.objectReferenceValue = obj;
                        so.ApplyModifiedProperties();
                        EditorUtility.SetDirty(e);
                        changedScenes++;
                    }
                }
            }
        }

        int changedPrefabs = 0;
        // Update prefabs in project
        string[] guids = AssetDatabase.FindAssets("t:Prefab");
        foreach (var g in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) continue;
            var enemies = go.GetComponentsInChildren<Enemy>(true);
            if (enemies == null || enemies.Length == 0) continue;
            bool any = false;
            foreach (var e in enemies)
            {
                var so = new SerializedObject(e);
                var prop = so.FindProperty("hpBarPrefab");
                if (prop != null)
                {
                    prop.objectReferenceValue = obj;
                    so.ApplyModifiedProperties();
                    any = true;
                }
            }
            if (any)
            {
                EditorUtility.SetDirty(go);
                AssetDatabase.SaveAssets();
                changedPrefabs++;
            }
        }

        Debug.Log($"Assigned HPBar prefab '{obj.name}' to {changedScenes} scene instances and {changedPrefabs} prefabs.");
    }

    private static object GetField(object obj, string name)
    {
        var t = obj.GetType();
        var f = t.GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return f != null ? f.GetValue(obj) : null;
    }
}
#endif