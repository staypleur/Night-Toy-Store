using System.Collections.Generic;
using UnityEngine;

namespace NightToyStore
{
    public sealed partial class ProceduralStore
    {
        public const float RoomHeight=4.8f;
        readonly Dictionary<int,GameObject[]> defenseDoors=new Dictionary<int,GameObject[]>();
        readonly Dictionary<int,GameObject> markerModels=new Dictionary<int,GameObject>();
        readonly Dictionary<int,GameObject> batteryBars=new Dictionary<int,GameObject>();
        readonly Dictionary<string,LineRenderer> boardLines=new Dictionary<string,LineRenderer>();
        readonly Vector4[] lightPositions=new Vector4[6],lightDirections=new Vector4[6];
        int renderedSegments;
        void AddRoomTarget(GameObject item,int room,RoomAction action,int side=0)
        { var target=item.AddComponent<RoomInteractable>();target.Room=room;target.Action=action;target.Side=side; }
        void BuildRoomControls(int room,Vector3 center)
        {
            defenseDoors[room]=new GameObject[2];
            for(int side=0;side<2;side++)
            {
                float sign=side==0?-1:1;
                var door=Block("Powered defense door "+room+" / "+side,center+new Vector3(sign*4.98f,1.4f,1),new Vector3(.12f,2.8f,2),new Color(.18f,.24f,.23f));
                door.SetActive(false);defenseDoors[room][side]=door;
                foreach(var action in new[]{RoomAction.Door,RoomAction.Light})
                {
                    var button=Block(action+" switch",NetworkControlRoom.SwitchPoint(room,side,action),new Vector3(.14f,.28f,.28f),
                        action==RoomAction.Door?new Color(.7f,.18f,.12f):new Color(.8f,.7f,.35f));
                    AddRoomTarget(button,room,action,side);
                }
                var fixture=Block("Entrance light fixture",center+new Vector3(sign*5.25f,2.6f,1),new Vector3(.35f,.3f,.4f),new Color(.46f,.44f,.36f),false);
                var lamp=fixture.AddComponent<Light>();lamp.type=LightType.Spot;lamp.range=11;lamp.spotAngle=70;lamp.intensity=0;
                fixture.transform.rotation=Quaternion.LookRotation(new Vector3(sign,-.18f,0));
            }
            Block("Room battery cabinet",center+new Vector3(-3.95f,.65f,-4.1f),new Vector3(.65f,1.3f,.75f),new Color(.17f,.24f,.2f));
            batteryBars[room]=Block("Battery indicator",center+new Vector3(-3.95f,.9f,-3.71f),new Vector3(.46f,.16f,.02f),new Color(.3f,.8f,.42f),false);
            Block("Marker tray",center+new Vector3(2.8f,.84f,3.8f),new Vector3(.55f,.12f,.32f),new Color(.32f,.24f,.14f));
            var marker=Block("Room "+room+" marker",NetworkControlRoom.MarkerPoint(room),new Vector3(.1f,.1f,.38f),new Color(.9f,.88f,.73f));
            markerModels[room]=marker;AddRoomTarget(marker,room,RoomAction.Marker);
        }
        public bool DoorwayClear(int room,int side)
        {
            var center=Layout.Doors(room)[side]+Vector3.up*1.4f;
            foreach(var hit in Physics.OverlapBox(center,new Vector3(.45f,1.39f,.99f)))
                if(hit.GetComponentInParent<NetworkToyPlayer>()!=null) return false;
            return true;
        }
        public bool DefenseDoorClosed(int room,int side)=>defenseDoors.TryGetValue(room,out var pair) && pair[side].activeSelf;
        public void ApplyRoomState(NetworkControlRoom world)
        {
            if(Layout==null || !Layout.IsFixed || world.Rooms.Count!=3) return;
            for(int room=1;room<=3;room++)
            {
                var state=world.Rooms[room-1];
                if(defenseDoors.TryGetValue(room,out var pair))
                    for(int side=0;side<2;side++) pair[side].SetActive(state.Battery>0 && (state.Closed & 1<<side)!=0);
                if(batteryBars.TryGetValue(room,out var bar)) { bar.transform.localScale=new Vector3(.46f*state.Battery/100,.16f,.02f);bar.SetActive(state.Battery>0); }
                if(markerModels.TryGetValue(room,out var marker))
                {
                    bool held=state.MarkerOwner!=RoomPower.Nobody;
                    marker.GetComponent<Collider>().enabled=!held;
                    if(!held) marker.transform.position=NetworkControlRoom.MarkerPoint(room);
                    else foreach(var actor in FindObjectsByType<NetworkToyPlayer>(FindObjectsSortMode.None))
                        if(actor.OwnerClientId==state.MarkerOwner) marker.transform.position=actor.transform.position+Quaternion.Euler(0,actor.LookHeading.Value,0)*new Vector3(.4f,1,.4f);
                }
            }
            for(;renderedSegments<world.Drawing.Count;renderedSegments++)
            {
                var segment=world.Drawing[renderedSegments];string key=segment.Room+":"+segment.Owner+":"+segment.Stroke;
                if(!boardLines.TryGetValue(key,out var line))
                {
                    var root=new GameObject("Board stroke "+key);root.transform.SetParent(geometry.transform,false);line=root.AddComponent<LineRenderer>();
                    line.useWorldSpace=true;line.startWidth=line.endWidth=.028f;line.numCapVertices=3;line.numCornerVertices=3;
                    var material=PrototypeMaterials.Create(new Color(.9f,.88f,.76f));material.SetFloat("_Emission",.75f);materials.Add(material);
                    line.sharedMaterial=material;line.positionCount=1;line.SetPosition(0,NetworkControlRoom.BoardWorld(segment.Room,segment.From));boardLines[key]=line;
                }
                int count=line.positionCount;line.positionCount=count+1;
                Vector3 point=NetworkControlRoom.BoardWorld(segment.Room,segment.To);
                if(segment.From==segment.To) point+=Vector3.right*.005f;
                line.SetPosition(count,point);
            }
        }
        void UpdateRoomLights()
        {
            var world=NetworkControlRoom.World;double now=world!=null?world.NetworkManager.ServerTime.Time:0;
            for(int room=1;room<=3;room++) for(int side=0;side<2;side++)
            {
                int index=(room-1)*2+side;float sign=side==0?-1:1;
                bool on=world!=null && world.Rooms[room-1].Lit(side,now);
                Vector3 pos=Layout.ControlCenter(room)+new Vector3(sign*5.25f,2.6f,1);
                var direction=new Vector3(sign,-.18f,0).normalized;
                lightPositions[index]=new Vector4(pos.x,pos.y,pos.z,11);
                lightDirections[index]=new Vector4(direction.x,direction.y,direction.z,on?1:0);
            }
            Shader.SetGlobalVectorArray("_DoorLightPosRange",lightPositions);Shader.SetGlobalVectorArray("_DoorLightDirOn",lightDirections);
            if(world!=null) ApplyRoomState(world);
        }
        void ResetRoomVisuals()
        { defenseDoors.Clear();markerModels.Clear();batteryBars.Clear();boardLines.Clear();renderedSegments=0; }
    }
}
