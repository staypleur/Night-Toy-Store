using System.Collections.Generic;
using UnityEngine;

namespace NightToyStore
{
    // Editable first art pass made from meshes; these are not final character assets.
    public sealed class ToyVisuals : MonoBehaviour
    {
        readonly List<Material> materials = new List<Material>();
        static readonly Color Dark = new Color(.055f, .065f, .07f);
        public static GameObject Create(int role, Transform parent)
        {
            var root = new GameObject("Toy art - " + (ToyRole)role);
            root.transform.SetParent(parent, false);
            var art = root.AddComponent<ToyVisuals>();
            Color cream = new Color(.68f, .59f, .42f);
            if (role == 0)
            {
                art.Part("Radio casing", PrimitiveType.Cube, new Vector3(0,.85f,0), new Vector3(.75f,1.05f,.35f), new Color(.22f,.42f,.43f));
                art.Part("Speaker", PrimitiveType.Cube, new Vector3(0,.68f,.19f), new Vector3(.54f,.42f,.03f), Dark);
                for (int i=0;i<6;i++) art.Part("Speaker grille", PrimitiveType.Cube, new Vector3(0,.52f+i*.065f,.22f), new Vector3(.47f,.015f,.02f), cream);
                art.Part("Dial", PrimitiveType.Sphere, new Vector3(.2f,1.17f,.22f), Vector3.one*.16f, cream);
                art.Part("Frequency display", PrimitiveType.Cube, new Vector3(-.12f,1.17f,.2f), new Vector3(.29f,.14f,.03f), new Color(.4f,.75f,.58f));
                art.Part("Antenna", PrimitiveType.Cylinder, new Vector3(-.22f,1.65f,0), new Vector3(.025f,.3f,.025f), cream);
                art.Limbs(new Color(.19f,.25f,.25f));
            }
            else if (role == 1)
            {
                art.Part("Dress", PrimitiveType.Capsule, new Vector3(0,.65f,0), new Vector3(.66f,.6f,.52f), new Color(.25f,.19f,.31f));
                art.Part("Head", PrimitiveType.Sphere, new Vector3(0,1.4f,0), new Vector3(.5f,.52f,.45f), cream);
                art.Part("Hair bun", PrimitiveType.Sphere, new Vector3(0,1.55f,-.22f), Vector3.one*.32f, new Color(.48f,.49f,.47f));
                for (int i=-1;i<=1;i+=2)
                {
                    art.Part("Button eye", PrimitiveType.Sphere, new Vector3(i*.11f,1.43f,.21f), new Vector3(.13f,.13f,.045f), Dark);
                    for(int j=-1;j<=1;j+=2) art.Part("Button stitch", PrimitiveType.Cube,new Vector3(i*.11f,1.43f+j*.02f,.24f),new Vector3(.07f,.012f,.008f),cream);
                }
                art.Part("Cane",PrimitiveType.Cylinder,new Vector3(.43f,.56f,.15f),new Vector3(.04f,.55f,.04f),new Color(.29f,.15f,.075f));
                art.Part("Cane handle",PrimitiveType.Cube,new Vector3(.35f,1.1f,.15f),new Vector3(.19f,.065f,.07f),cream);
            }
            else if (role == 2)
            {
                art.Part("Rabbit body",PrimitiveType.Capsule,new Vector3(0,.38f,0),new Vector3(.48f,.34f,.38f),cream);
                art.Part("Rabbit head",PrimitiveType.Sphere,new Vector3(0,.8f,0),new Vector3(.5f,.45f,.4f),cream);
                for(int i=-1;i<=1;i+=2)
                {
                    art.Part("Ear",PrimitiveType.Capsule,new Vector3(i*.14f,1.13f,0),new Vector3(.12f,.23f,.1f),cream);
                    art.Part("Eye",PrimitiveType.Sphere,new Vector3(i*.11f,.85f,.185f),Vector3.one*.07f,Dark);
                }
                art.Part("Closed zipper",PrimitiveType.Cube,new Vector3(0,.72f,.2f),new Vector3(.29f,.04f,.025f),Dark);
                for(int i=0;i<8;i++) art.Part("Zipper tooth",PrimitiveType.Cube,new Vector3(-.12f+i*.035f,.72f,.22f),new Vector3(.015f,.045f,.01f),new Color(.58f,.57f,.48f));
                art.Part("Zipper pull",PrimitiveType.Cube,new Vector3(.16f,.67f,.22f),new Vector3(.045f,.1f,.025f),Dark);
            }
            else
            {
                float radius=NetworkToyPlayer.BallRadius;
                art.Part("Tennis ball",PrimitiveType.Sphere,Vector3.up*radius,Vector3.one*radius*2,new Color(.63f,.75f,.15f));
                for(int i=-1;i<=1;i+=2)
                {
                    art.Part("Eye white",PrimitiveType.Sphere,new Vector3(i*.2f,.95f,.55f),new Vector3(.2f,.21f,.09f),cream);
                    art.Part("Eye",PrimitiveType.Sphere,new Vector3(i*.2f,.95f,.6f),new Vector3(.085f,.1f,.035f),Dark);
                }
                art.Part("Nose",PrimitiveType.Sphere,new Vector3(0,.81f,.63f),Vector3.one*.1f,cream);
                art.Part("Mouth",PrimitiveType.Cube,new Vector3(0,.68f,.65f),new Vector3(.25f,.055f,.03f),Dark);
            }
            return root;
        }
        void Limbs(Color color)
        {
            for(int i=-1;i<=1;i+=2)
            {
                Part("Leg",PrimitiveType.Capsule,new Vector3(i*.2f,.2f,0),new Vector3(.15f,.19f,.16f),color);
                Part("Arm",PrimitiveType.Capsule,new Vector3(i*.48f,.85f,0),new Vector3(.14f,.3f,.14f),color);
            }
        }
        void Part(string name,PrimitiveType type,Vector3 position,Vector3 scale,Color color)
        {
            var part=GameObject.CreatePrimitive(type);
            part.name=name;
            var collider = part.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            part.transform.SetParent(transform,false);
            part.transform.localPosition=position;
            part.transform.localScale=scale;
            var material=PrototypeMaterials.Create(color);
            materials.Add(material);
            part.GetComponent<Renderer>().sharedMaterial=material;
        }
        void OnDestroy() { foreach(var material in materials) Destroy(material); }
    }
}

