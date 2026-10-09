using System;
using System.IO;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NightToyStore.Editor
{
    public static class NetworkPrototypeSetup
    {
        [MenuItem("Night Toy Store/Create Network Test Scene")]
        public static void CreateScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                throw new InvalidOperationException("Scene creation cancelled.");
            Directory.CreateDirectory("Assets/NightToyStore/Prefabs");
            var player = new GameObject("Network toy");
            player.AddComponent<NetworkObject>();
            player.AddComponent<NetworkTransform>();
            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.6f;
            controller.center = Vector3.up * .8f;
            controller.radius = .3f;
            controller.minMoveDistance = 0;
            var body = player.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.linearDamping = .15f;
            var sphere = player.AddComponent<SphereCollider>();
            sphere.center = Vector3.up * .3f;
            sphere.radius = .3f;
            sphere.enabled = false;
            const string materialPath = "Assets/NightToyStore/Prefabs/BallBounce.asset";
            var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(materialPath);
            if (material == null) { material = new PhysicsMaterial("BallBounce"); AssetDatabase.CreateAsset(material, materialPath); }
            material.bounciness = .65f;
            material.bounceCombine = PhysicsMaterialCombine.Maximum;
            material.dynamicFriction = .04f;
            material.staticFriction = .04f;
            material.frictionCombine = PhysicsMaterialCombine.Minimum;
            EditorUtility.SetDirty(material);
            sphere.sharedMaterial = material;
            player.AddComponent<NetworkStoreState>();
            player.AddComponent<NetworkToyPlayer>();
            player.AddComponent<NetworkVoice>();
            player.AddComponent<NetworkControlRoom>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(player, "Assets/NightToyStore/Prefabs/NetworkToy.prefab");
            UnityEngine.Object.DestroyImmediate(player);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var session = new GameObject("Prototype session").AddComponent<PrototypeSession>();
            session.playerPrefab = prefab;
            EditorSceneManager.SaveScene(scene, "Assets/NightToyStore/Scenes/NetworkTest.unity");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(
                "Assets/NightToyStore/Scenes/NetworkTest.unity", true) };
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Night Toy Store/Build Network Test")]
        public static void PrepareAndBuild() { CreateScene(); Build(); }

        public static void Build()
        {
            Directory.CreateDirectory("Builds/NetworkTest");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { "Assets/NightToyStore/Scenes/NetworkTest.unity" },
                locationPathName = "Builds/NetworkTest/NightToyStore.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Network build failed: " + report.summary.result);
        }
    }
}
