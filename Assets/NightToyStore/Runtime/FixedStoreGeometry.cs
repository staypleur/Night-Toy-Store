using System.Collections.Generic;
using UnityEngine;

namespace NightToyStore
{
    public sealed partial class ProceduralStore
    {
        void BuildFixedGeometry()
        {
            geometry.name="Fixed toy store / user sketch v1";
            var plan=Layout.Fixed;
            // Merge each floor row; the union has no overlapping floors or raised thresholds.
            for(int z=-40;z<45;z++)
            {
                int x=-55;
                while(x<60)
                {
                    if(!plan.Tiles.Contains(new Vector2Int(x,z))) { x++;continue; }
                    int first=x;while(plan.Tiles.Contains(new Vector2Int(x,z))) x++;
                    Vector3 center=new Vector3((first+x)*.5f,-.25f,z+.5f);
                    var floor=Block("Worn tile floor",center,new Vector3(x-first,.5f,1),new Color(.29f,.31f,.28f));
                    floor.GetComponent<Renderer>().sharedMaterial.SetFloat("_TileFloor",1);
                    Block("Ceiling",center+Vector3.up*3.95f,new Vector3(x-first,.15f,1),new Color(.07f,.075f,.065f));
                }
            }
            foreach(var tile in plan.Tiles)
            {
                foreach(var direction in new[]{Vector2Int.left,Vector2Int.right,Vector2Int.up,Vector2Int.down})
                {
                    if(plan.Tiles.Contains(tile+direction)) continue;
                    var center=new Vector3(tile.x+.5f+direction.x*.5f,1.8f,tile.y+.5f+direction.y*.5f);
                    Block("Exterior wall",center,direction.x!=0?new Vector3(.22f,3.6f,1):new Vector3(1,3.6f,.22f),new Color(.22f,.2f,.16f));
                }
            }
            foreach(var room in plan.Rooms)
            {
                if(room.control>0) FixedControlRoom(room);
                else
                {
                    // Conservative corner props leave the sketch's corridor lanes open.
                    Vector3 corner=room.Center+new Vector3(-room.bounds.width*.5f+1.5f,.7f,-room.bounds.height*.5f+1.5f);
                    Block(room.breaker>0?"Breaker cabinet "+room.breaker:"Old toy shelf",corner,new Vector3(1,1.4f,.6f),room.breaker>0?new Color(.27f,.35f,.32f):new Color(.3f,.22f,.12f));
                    if(room.breaker>0)
                    {
                        var root=new GameObject("Breaker number "+room.breaker);root.transform.SetParent(geometry.transform,false);
                        root.transform.position=corner+new Vector3(0,.4f,-.32f);Digit(root.transform,room.breaker,Vector3.zero);
                    }
                }
                Block("Old ceiling lamp",room.Center+Vector3.up*3.55f,new Vector3(1.5f,.08f,.28f),new Color(.66f,.58f,.4f),false);
            }
        }
        void FixedControlRoom(FixedStorePlan.Room room)
        {
            Vector3 center=room.Center;
            Color wallColor=new Color(.22f,.2f,.16f);
            // CCTV faces the board. Door widths and windows are 2m; vertical dimensions are trial values.
            foreach(int side in new[]{-1,1})
            {
                float wallX=center.x+side*5;
                Block("Control front/back wall",center+new Vector3(0,1.8f,side*5),new Vector3(10,3.6f,.22f),wallColor);
                // Side wall: window [-2,0], door [0,2]; both face the same corridor.
                foreach(var span in new[]{new Vector2(-5,-2),new Vector2(2,5)})
                    Block("Control side wall",new Vector3(wallX,1.8f,center.z+(span.x+span.y)*.5f),new Vector3(.22f,3.6f,span.y-span.x),wallColor);
                Block("Door lintel",new Vector3(wallX,3.2f,center.z+1),new Vector3(.22f,.8f,2),wallColor);
                Block("Window sill",new Vector3(wallX,.4f,center.z-1),new Vector3(.22f,.8f,2),wallColor);
                Block("Window header",new Vector3(wallX,3.05f,center.z-1),new Vector3(.22f,1.1f,2),wallColor);
                // Invisible glass collider lets players look through but prevents stepping/jumping through.
                var glass=new GameObject("Control room window glass");glass.transform.SetParent(geometry.transform,false);
                glass.transform.position=new Vector3(wallX,1.65f,center.z-1);
                glass.AddComponent<BoxCollider>().size=new Vector3(.1f,1.7f,2);
                var door=Block("Locked control room "+room.control,new Vector3(wallX,1.4f,center.z+1),new Vector3(.15f,2.8f,2),new Color(.26f,.32f,.29f));
                if(!doors.TryGetValue(room.control,out var list)) doors[room.control]=list=new List<GameObject>();list.Add(door);
                var camera=Block("Door camera",new Vector3(wallX-side*.2f,2.9f,center.z+2.35f),new Vector3(.4f,.3f,.55f),new Color(.07f,.085f,.08f),false);
                Block("Camera lens",camera.transform.position+Vector3.right*side*.23f,new Vector3(.08f,.18f,.18f),new Color(.08f,.22f,.19f),false);
                foreach(int facing in new[]{-1,1})
                {
                    var sign=new GameObject("Control number "+room.control);sign.transform.SetParent(geometry.transform,false);
                    sign.transform.position=new Vector3(wallX+facing*.15f,2.15f,center.z+1);
                    sign.transform.rotation=Quaternion.Euler(0,facing>0?270:90,0);Digit(sign.transform,room.control,Vector3.zero);
                }
            }
            Block("CCTV desk 7m",center+new Vector3(0,1,-4.4f),new Vector3(7,.18f,1),new Color(.3f,.2f,.12f));
            for(int i=-1;i<=1;i++)
            {
                Block("Monitor casing",center+new Vector3(i*2.3f,1.65f,-4.6f),new Vector3(2.15f,1.1f,.18f),new Color(.055f,.065f,.06f),false);
                Block("CCTV decoration",center+new Vector3(i*2.3f,1.65f,-4.49f),new Vector3(1.98f,.92f,.025f),new Color(.12f,.3f,.23f),false);
            }
            Block("Chalkboard 6m",center+new Vector3(0,2,4.8f),new Vector3(6,1.5f,.1f),new Color(.055f,.12f,.08f),false);
            Block("Chair seat",center+new Vector3(0,.55f,-2.9f),new Vector3(.8f,.18f,.8f),new Color(.24f,.16f,.1f));
            Block("Chair back",center+new Vector3(0,.95f,-2.5f),new Vector3(.8f,.8f,.12f),new Color(.24f,.16f,.1f));
        }
        void DrawFixedMap()
        {
            var box=new Rect(Screen.width-670,12,655,410);
            GUI.Box(box,"FIXED SKETCH MAP / F2 / yellow: you / cyan: team");
            var labelStyle=new GUIStyle(GUI.skin.label){fontSize=11};
            Rect PlanRect(Rect world) => new Rect(box.x+20+(world.xMin+53)*5,box.y+40+(40-world.yMax)*4,world.width*5,world.height*4);
            GUI.color=new Color(.25f,.3f,.29f);
            foreach(var corridor in Layout.Fixed.Corridors) GUI.DrawTexture(PlanRect(corridor),Texture2D.whiteTexture);
            foreach(var room in Layout.Fixed.Rooms)
            {
                GUI.color=room.control>0?(Unlocked & 1<<(room.control-1))!=0?new Color(.28f,.65f,.5f):new Color(.6f,.32f,.25f):room.breaker>0?new Color(.55f,.46f,.26f):new Color(.32f,.36f,.3f);
                Rect rect=PlanRect(room.bounds);GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=Color.white;
                string label=room.control>0?"C"+room.control:room.breaker>0?"B"+room.breaker:
                    room.name.StartsWith("Party room")?"P"+room.name[room.name.Length-1]:
                    room.name.StartsWith("Junction")?"J-"+room.name[room.name.Length-1]:
                    room.name.StartsWith("Storage")?"S"+room.name[room.name.Length-1]:
                    room.name=="Nursery"?"Baby":room.name=="Large party room 1"?"Party L1":room.name=="Main hall"?"Main":room.name=="Sub hall"?"Sub":room.name.Split(' ')[0];
                GUI.Label(rect,label,labelStyle);
            }
            GUI.color=Color.white;
            GUI.Label(new Rect(box.x+20,box.y+380,620,24),"C: control / B: breaker / S: storage / J: junction / P: party room / Baby: nursery");
            foreach(var actor in FindObjectsByType<NetworkToyPlayer>(FindObjectsSortMode.None))
            {
                Vector3 p=actor.transform.position;
                var rect=PlanRect(new Rect(p.x-.3f,p.z-.3f,.6f,.6f));
                GUI.color=actor.IsOwner?Color.yellow:Color.cyan;GUI.DrawTexture(rect,Texture2D.whiteTexture);
            }
            GUI.color=Color.white;
        }
    }
}
