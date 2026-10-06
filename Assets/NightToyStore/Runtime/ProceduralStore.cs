using System.Collections.Generic;
using UnityEngine;

namespace NightToyStore
{
    public sealed class ProceduralStore : MonoBehaviour
    {
        public static ProceduralStore Instance { get; private set; }
        public StoreLayout Layout { get; private set; }
        readonly Dictionary<int,GameObject> doors=new Dictionary<int,GameObject>();
        readonly Dictionary<int,GameObject> keys=new Dictionary<int,GameObject>();
        readonly List<Material> materials=new List<Material>();
        GameObject geometry;
        bool showMap;
        public int Unlocked { get; private set; }
        public int Picked { get; private set; }
        void Awake() { Instance=this; }
        public void Ensure(int seed)
        {
            if(seed==0 || Layout!=null && Layout.Seed==seed) return;
            if(geometry!=null) Destroy(geometry);
            foreach(var material in materials) Destroy(material);
            materials.Clear();doors.Clear();keys.Clear();
            Layout=new StoreLayout(seed);
            geometry=new GameObject("Modular toy store / seed "+seed);
            geometry.transform.SetParent(transform,false);
            var floor=Block("Worn tile floor",new Vector3(0,-.25f,0),new Vector3(24,.5f,24),new Color(.29f,.31f,.28f));
            floor.GetComponent<Renderer>().sharedMaterial.SetFloat("_TileFloor",1);
            for(int cell=0;cell<9;cell++)
            {
                Vector3 center=StoreLayout.Center(cell);
                int room=Layout.Room(cell);
                if(cell%3==0) Wall(center+Vector3.left*4,false,false,0);
                if(cell/3==0) Wall(center+Vector3.back*4,true,false,0);
                bool east=cell%3<2 && Layout.Connected(cell,cell+1);
                bool north=cell/3<2 && Layout.Connected(cell,cell+3);
                Wall(center+Vector3.right*4,false,east,room!=0?room:cell%3<2?Layout.Room(cell+1):0);
                Wall(center+Vector3.forward*4,true,north,room!=0?room:cell/3<2?Layout.Room(cell+3):0);
                Block("Ceiling",center+Vector3.up*3.7f,new Vector3(8,.15f,8),new Color(.07f,.075f,.065f));
                Block("Old ceiling lamp",center+new Vector3(0,3.6f,0),new Vector3(1.5f,.08f,.28f),new Color(.66f,.58f,.4f),false);
                if(room>0) ControlRoom(center,room);
                else PublicRoom(center,Layout.Themes[cell]);
            }
            for(int room=1;room<=3;room++)
            {
                if(room==Layout.StartRoom) continue;
                var key=new GameObject("Control room "+room+" key");
                key.transform.SetParent(geometry.transform,false);
                key.transform.position=Layout.Keys[room-1];
                keys[room]=key;
                Part(key.transform,"Key ring",new Vector3(0,.85f,0),new Vector3(.25f,.25f,.06f),new Color(.78f,.56f,.16f));
                Part(key.transform,"Key stem",new Vector3(.22f,.85f,0),new Vector3(.3f,.065f,.06f),new Color(.78f,.56f,.16f));
                Digit(key.transform,room,new Vector3(0,1.35f,0));
            }
            Apply(1<<(Layout.StartRoom-1),0);
            Debug.Log("NTS_STORE_LAYOUT "+Layout.Fingerprint);
        }
        void Wall(Vector3 center,bool horizontal,bool connected,int room)
        {
            var color=new Color(.22f,.2f,.16f);
            if(!connected)
            { Block("Wall",center+Vector3.up*1.8f,horizontal?new Vector3(8,3.6f,.22f):new Vector3(.22f,3.6f,8),color);return; }
            float length=(8-StoreLayout.DoorWidth)*.5f,offset=(8+StoreLayout.DoorWidth)*.25f;
            Vector3 axis=horizontal?Vector3.right:Vector3.forward;
            foreach(int side in new[]{-1,1})
                Block("Doorway wall",center+axis*offset*side+Vector3.up*1.8f,
                    horizontal?new Vector3(length,3.6f,.22f):new Vector3(.22f,3.6f,length),color);
            Block("Lintel",center+Vector3.up*3.2f,horizontal?new Vector3(StoreLayout.DoorWidth,.8f,.22f):new Vector3(.22f,.8f,StoreLayout.DoorWidth),color);
            if(room>0)
            {
                var door=Block("Locked control room "+room,center+Vector3.up*1.4f,
                    horizontal?new Vector3(StoreLayout.DoorWidth,2.8f,.15f):new Vector3(.15f,2.8f,StoreLayout.DoorWidth),new Color(.26f,.32f,.29f));
                doors[room]=door;
                var sign=new GameObject("Room number");sign.transform.SetParent(geometry.transform,false);
                sign.transform.position=center+Vector3.up*2.2f;
                sign.transform.rotation=Quaternion.Euler(0,horizontal?0:90,0);
                Digit(sign.transform,room,new Vector3(0,0,-.14f));
                // Signs on both sides remain useful after the barrier is unlocked.
                sign=new GameObject("Room number reverse");sign.transform.SetParent(geometry.transform,false);
                sign.transform.position=center+Vector3.up*2.2f;
                sign.transform.rotation=Quaternion.Euler(0,horizontal?180:270,0);
                Digit(sign.transform,room,new Vector3(0,0,-.14f));
            }
        }
        void ControlRoom(Vector3 center,int room)
        {
            var root=new GameObject("Control room "+room+" furniture");root.transform.SetParent(geometry.transform,false);root.transform.position=center;
            Part(root.transform,"Desk",new Vector3(0,1,2.3f),new Vector3(3.2f,.15f,.9f),new Color(.3f,.2f,.12f));
            for(int i=-1;i<=1;i++)
            {
                Part(root.transform,"Monitor casing",new Vector3(i,1.48f,2.4f),new Vector3(.78f,.62f,.16f),new Color(.055f,.065f,.06f));
                Part(root.transform,"CCTV decoration",new Vector3(i,1.48f,2.3f),new Vector3(.65f,.47f,.025f),new Color(.12f,.3f,.23f));
            }
            Part(root.transform,"Chalkboard",new Vector3(2.7f,2.1f,2.6f),new Vector3(1.9f,1.3f,.1f),new Color(.055f,.12f,.08f));
        }
        void PublicRoom(Vector3 center,int theme)
        {
            // Furniture stays inside the module corners; all four connector lanes stay clear.
            foreach(int side in new[]{-1,1})
            {
                Vector3 position=center+new Vector3(side*2.6f,0,2.6f);
                if(theme==2)
                { Block("Repair workbench",position+Vector3.up*.8f,new Vector3(1.3f,1.6f,1.1f),new Color(.29f,.22f,.13f));continue; }
                Block(theme==0?"Old toy shelf":"Stockroom crates",position+Vector3.up*.75f,new Vector3(1.1f,1.5f,1.1f),new Color(.3f,.22f,.12f));
                for(int i=0;i<3;i++)
                    Block("Toy package",position+new Vector3(0,.4f+i*.45f,-.58f),new Vector3(.7f,.28f,.06f),new Color(.25f+i*.07f,.26f,.23f),false);
            }
        }
        GameObject Block(string name,Vector3 position,Vector3 scale,Color color,bool collider=true)
        {
            var block=GameObject.CreatePrimitive(PrimitiveType.Cube);block.name=name;
            block.transform.SetParent(geometry.transform,false);block.transform.position=position;block.transform.localScale=scale;
            var material=PrototypeMaterials.Create(color);materials.Add(material);block.GetComponent<Renderer>().sharedMaterial=material;
            if(!collider) { block.GetComponent<Collider>().enabled=false;Destroy(block.GetComponent<Collider>()); }
            return block;
        }
        void Part(Transform parent,string name,Vector3 position,Vector3 scale,Color color)
        {
            var block=Block(name,Vector3.zero,scale,color,false);block.transform.SetParent(parent,false);block.transform.localPosition=position;
        }
        void Digit(Transform parent,int number,Vector3 offset)
        {
            string segments=number==1?"12":number==2?"01346":"01236";
            Vector3[] pos={new Vector3(0,.3f,0),new Vector3(.16f,.15f,0),new Vector3(.16f,-.15f,0),new Vector3(0,-.3f,0),new Vector3(-.16f,-.15f,0),new Vector3(-.16f,.15f,0),Vector3.zero};
            foreach(char segment in segments)
            { int i=segment-'0';Part(parent,"Room digit",offset+pos[i],i==0||i==3||i==6?new Vector3(.3f,.045f,.025f):new Vector3(.045f,.25f,.025f),new Color(.84f,.71f,.42f)); }
        }
        public void Apply(int unlocked,int picked)
        {
            Unlocked=unlocked;Picked=picked;
            foreach(var pair in doors) pair.Value.SetActive((unlocked & 1<<(pair.Key-1))==0);
            foreach(var pair in keys) pair.Value.SetActive((picked & 1<<(pair.Key-1))==0);
        }
        public bool DoorBlocked(int room) => doors.TryGetValue(room,out var door) && door.activeSelf;
        public bool KeyVisible(int room) => keys.TryGetValue(room,out var key) && key.activeSelf;
        void Update() { if(Input.GetKeyDown(KeyCode.F2)) showMap=!showMap; }
        void OnGUI()
        {
            if(Layout==null) return;
            GUI.Box(new Rect(12,402,620,62),$"TEST MAP / seed {Layout.Seed} / start control room {Layout.StartRoom}");
            GUI.Label(new Rect(24,429,590,24),"E: nearby key / unlock control room. F2: test floor plan. CCTV/board: decoration.");
            if(!showMap) return;
            float x=Screen.width-330;
            GUI.Box(new Rect(x,12,316,334),"TEST FLOOR PLAN");
            for(int cell=0;cell<9;cell++)
            {
                int room=Layout.Room(cell);
                GUI.color=room==0?new Color(.32f,.36f,.3f):(Unlocked & 1<<(room-1))!=0?new Color(.28f,.65f,.5f):new Color(.6f,.32f,.25f);
                var rect=new Rect(x+20+cell%3*96,54+(2-cell/3)*90,82,76);
                GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=Color.white;
                GUI.Label(rect,room>0?"Control "+room:Layout.Themes[cell]==0?"Toys":Layout.Themes[cell]==1?"Stock":"Workshop");
            }
            foreach(int edge in Layout.Edges)
            {
                int a=edge/9,b=edge%9;
                Vector2 p=new Vector2(x+61+a%3*96,92+(2-a/3)*90),q=new Vector2(x+61+b%3*96,92+(2-b/3)*90);
                GUI.DrawTexture(p.x!=q.x ? new Rect(Mathf.Min(p.x,q.x)+41,p.y-3,14,6) :
                    new Rect(p.x-3,Mathf.Min(p.y,q.y)+38,6,14),Texture2D.whiteTexture);
            }
        }
        void OnDestroy() { if(Instance==this) Instance=null;foreach(var material in materials) Destroy(material); }
    }
}
