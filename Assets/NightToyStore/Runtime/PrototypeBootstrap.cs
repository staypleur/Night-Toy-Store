using UnityEngine;

namespace NightToyStore
{
    public static class PrototypeBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Start()
        {
            if (Object.FindFirstObjectByType<PrototypePlayer>() != null) return;
            bool networkScene = Object.FindFirstObjectByType<PrototypeSession>() != null;
            if (!networkScene)
                foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                    camera.gameObject.SetActive(false);
            RenderSettings.ambientLight = new Color(.12f, .13f, .17f);
            MakeBlock("Floor", new Vector3(0, -.25f, 0), new Vector3(24, .5f, 24));
            MakeBlock("North", new Vector3(0, 2, 12), new Vector3(24, 4, .5f));
            MakeBlock("South", new Vector3(0, 2, -12), new Vector3(24, 4, .5f));
            MakeBlock("East", new Vector3(12, 2, 0), new Vector3(.5f, 4, 24));
            MakeBlock("West", new Vector3(-12, 2, 0), new Vector3(.5f, 4, 24));
            for (int i = 0; i < 5; i++)
                MakeBlock("Temporary shelf", new Vector3(-6 + i * 3, 1, 4), new Vector3(1, 2, 3));
            if (Object.FindFirstObjectByType<PrototypeSession>() == null)
                new GameObject("Local player").AddComponent<PrototypePlayer>();
        }

        static void MakeBlock(string name, Vector3 position, Vector3 scale)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.position = position;
            block.transform.localScale = scale;
            block.GetComponent<Renderer>().sharedMaterial = PrototypeMaterials.Create(
                name == "Floor" ? new Color(.32f, .36f, .4f) :
                name == "Temporary shelf" ? new Color(.45f, .31f, .2f) : new Color(.24f, .29f, .34f));
        }
    }
}
