using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NightToyStore.Editor
{
    public static class PrototypeSetup
    {
        [MenuItem("Night Toy Store/Create Local Test Scene")]
        public static void CreateScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            System.IO.Directory.CreateDirectory("Assets/NightToyStore/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, "Assets/NightToyStore/Scenes/LocalTest.unity");
            EditorBuildSettings.scenes = new[] {
                new EditorBuildSettingsScene("Assets/NightToyStore/Scenes/LocalTest.unity", true)
            };
            var settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (settings.Length > 0)
            {
                var serialized = new SerializedObject(settings[0]);
                var input = serialized.FindProperty("activeInputHandler");
                if (input != null) { input.intValue = 0; serialized.ApplyModifiedProperties(); }
            }
            PlayerSettings.companyName = "Night Toy Store";
            PlayerSettings.productName = "Night Toy Store";
            AssetDatabase.SaveAssets();
            Debug.Log("Local test scene created. Press Play. This is not the multiplayer prototype.");
        }
    }
}
