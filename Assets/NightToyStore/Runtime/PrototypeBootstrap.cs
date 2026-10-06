using UnityEngine;

namespace NightToyStore
{
    public static class PrototypeBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Start()
        {
            if (Object.FindFirstObjectByType<PrototypePlayer>() != null) return;
            var session = Object.FindFirstObjectByType<PrototypeSession>();
            bool networkScene = session != null;
            if(networkScene && session.UseProceduralStore)
            {
                new GameObject("Procedural store").AddComponent<ProceduralStore>();
                return;
            }
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
            {
                float x = -6 + i * 3;
                var obstacle = MakeBlock("Temporary shelf", new Vector3(x, 1, 4), new Vector3(1, 2, 3));
                obstacle.GetComponent<Renderer>().enabled = false;
                for (int side=-1;side<=1;side+=2)
                    Decor("Shelf post",new Vector3(x+side*.47f,1,4),new Vector3(.06f,2,3),new Color(.22f,.16f,.1f));
                for(int level=0;level<4;level++)
                {
                    Decor("Shelf board",new Vector3(x,.15f+level*.55f,4),new Vector3(1,.06f,3),new Color(.37f,.27f,.16f));
                    for(int toy=0;toy<3;toy++)
                        Decor("Toy carton",new Vector3(x,.34f+level*.55f,3.05f+toy*.85f),new Vector3(.62f,.32f,.55f),
                            new Color(.25f+level*.06f,.24f+toy*.07f,.2f+i*.035f));
                }
            }
            Decor("Ceiling",new Vector3(0,4.1f,0),new Vector3(24,.2f,24),new Color(.07f,.085f,.09f));
            for(int i=0;i<4;i++)
                Decor("Ceiling fixture",new Vector3(-8+i*5,3.96f,0),new Vector3(2,.07f,.35f),new Color(.64f,.59f,.43f));
            Decor("Security desk",new Vector3(7,1,8),new Vector3(3.6f,.15f,1.2f),new Color(.27f,.2f,.13f));
            for(int side=-1;side<=1;side+=2)
                Decor("Desk leg",new Vector3(7+side*1.5f,.48f,8),new Vector3(.15f,.96f,.8f),new Color(.16f,.19f,.19f));
            for(int i=0;i<3;i++)
            {
                Decor("Monitor casing",new Vector3(6+i,1.5f,8.25f),new Vector3(.8f,.65f,.18f),new Color(.075f,.09f,.09f));
                Decor("CCTV display",new Vector3(6+i,1.5f,8.14f),new Vector3(.68f,.5f,.015f),new Color(.1f,.3f,.24f));
                Decor("Monitor stand",new Vector3(6+i,1.18f,8.25f),new Vector3(.15f,.3f,.15f),new Color(.12f,.14f,.13f));
            }
            Decor("Chalkboard frame",new Vector3(7,2.3f,11.65f),new Vector3(4,1.7f,.1f),new Color(.31f,.21f,.12f));
            Decor("Chalkboard",new Vector3(7,2.3f,11.57f),new Vector3(3.8f,1.5f,.06f),new Color(.055f,.12f,.095f));
            if (Object.FindFirstObjectByType<PrototypeSession>() == null)
                new GameObject("Local player").AddComponent<PrototypePlayer>();
        }

        static GameObject MakeBlock(string name, Vector3 position, Vector3 scale)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.position = position;
            block.transform.localScale = scale;
            block.GetComponent<Renderer>().sharedMaterial = PrototypeMaterials.Create(
                name == "Floor" ? new Color(.32f, .36f, .4f) :
                name == "Temporary shelf" ? new Color(.45f, .31f, .2f) : new Color(.24f, .29f, .34f));
            if(name == "Floor") block.GetComponent<Renderer>().sharedMaterial.SetFloat("_TileFloor",1);
            return block;
        }

        static void Decor(string name, Vector3 position, Vector3 scale, Color color)
        {
            var block=MakeBlock(name,position,scale);
            block.GetComponent<Collider>().enabled=false;
            Object.Destroy(block.GetComponent<Collider>());
            var material=block.GetComponent<Renderer>().sharedMaterial;
            material.color=color;
        }
    }
}
