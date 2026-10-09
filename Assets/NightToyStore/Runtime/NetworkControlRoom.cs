using System;
using Unity.Netcode;
using UnityEngine;

namespace NightToyStore
{
    public enum RoomAction { Seat, Door, Light, Marker, Board }
    public sealed class RoomInteractable : MonoBehaviour
    { public int Room, Side; public RoomAction Action; }

    public struct RoomPower : INetworkSerializable, IEquatable<RoomPower>
    {
        public float Battery;
        public bool Started;
        public int Closed;
        public double LeftLightUntil, RightLightUntil;
        public ulong SeatOwner, MarkerOwner;
        public const ulong Nobody = ulong.MaxValue;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T:IReaderWriter
        {
            serializer.SerializeValue(ref Battery);serializer.SerializeValue(ref Started);serializer.SerializeValue(ref Closed);
            serializer.SerializeValue(ref LeftLightUntil);serializer.SerializeValue(ref RightLightUntil);
            serializer.SerializeValue(ref SeatOwner);serializer.SerializeValue(ref MarkerOwner);
        }
        public bool Equals(RoomPower other) => Battery==other.Battery && Started==other.Started && Closed==other.Closed &&
            LeftLightUntil==other.LeftLightUntil && RightLightUntil==other.RightLightUntil && SeatOwner==other.SeatOwner && MarkerOwner==other.MarkerOwner;
        public bool Lit(int side,double now) => Battery>0 && (side==0?LeftLightUntil:RightLightUntil)>now;
        public float DrainMultiplier(double now) => 1+.2f*((Closed&1)!=0?1:0)+.2f*((Closed&2)!=0?1:0)+.1f*(Lit(0,now)?1:0)+.1f*(Lit(1,now)?1:0);
        // Integrates expiring lights exactly, even if a server frame spans the expiry time.
        public void Advance(double now,float seconds)
        {
            if(!Started || Battery<=0) return;
            float baseMultiplier=1+.2f*((Closed&1)!=0?1:0)+.2f*((Closed&2)!=0?1:0);
            double litSeconds=Math.Clamp(LeftLightUntil-now,0,seconds)+Math.Clamp(RightLightUntil-now,0,seconds);
            Battery=Mathf.Max(0,Battery-(float)(100.0/420*(seconds*baseMultiplier+.1*litSeconds)));
            if(Battery<=0) { Closed=0;LeftLightUntil=RightLightUntil=0; }
        }
    }
    public struct BoardSegment : INetworkSerializable, IEquatable<BoardSegment>
    {
        public int Room;public ulong Owner;public uint Stroke;public Vector2 From,To;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T:IReaderWriter
        { serializer.SerializeValue(ref Room);serializer.SerializeValue(ref Owner);serializer.SerializeValue(ref Stroke);serializer.SerializeValue(ref From);serializer.SerializeValue(ref To); }
        public bool Equals(BoardSegment other)=>Room==other.Room && Owner==other.Owner && Stroke==other.Stroke && From==other.From && To==other.To;
    }

    // Host player's instance owns world state; each player's instance accepts only its owner's requests.
    public sealed partial class NetworkControlRoom : NetworkBehaviour
    {
        public NetworkList<RoomPower> Rooms;
        public NetworkList<BoardSegment> Drawing;
        public readonly NetworkVariable<int> SeatedRoom=new NetworkVariable<int>(0);
        public readonly NetworkVariable<int> MarkerRoom=new NetworkVariable<int>(0);
        public readonly NetworkVariable<bool> BoardFocus=new NetworkVariable<bool>(false);
        public int Rejections { get; private set; }
        public bool TestVerified { get; private set; }
        public bool MovementBlocked => SeatedRoom.Value!=0 || BoardFocus.Value;
        NetworkToyPlayer actor;
        double lastTick;
        float drawCredit=8,lastDrawTime;
        uint stroke;
        Vector2 lastPoint;
        bool strokeOpen;
        string status;
        public static NetworkControlRoom World
        {
            get { foreach(var control in FindObjectsByType<NetworkControlRoom>(FindObjectsSortMode.None))
                if(control.IsSpawned && control.OwnerClientId==Unity.Netcode.NetworkManager.ServerClientId && control.Rooms.Count==3) return control;return null; }
        }
        public static Vector3 SeatPoint(int room)=>ProceduralStore.Instance.Layout.ControlCenter(room)+new Vector3(0,.55f,-2.9f);
        public static Vector3 MarkerPoint(int room)=>ProceduralStore.Instance.Layout.ControlCenter(room)+new Vector3(2.8f,1,3.8f);
        public static Vector3 BoardPoint(int room)=>ProceduralStore.Instance.Layout.ControlCenter(room)+new Vector3(0,2.1f,4.73f);
        public static Vector3 SwitchPoint(int room,int side,RoomAction action)=>ProceduralStore.Instance.Layout.ControlCenter(room)+new Vector3(side==0?-4.75f:4.75f,1.15f,action==RoomAction.Door?2.5f:3.1f);
        public static Vector3 BoardWorld(int room,Vector2 uv)=>BoardPoint(room)+new Vector3((uv.x-.5f)*6,(uv.y-.5f)*1.8f,0);
        void Awake() { Rooms=new NetworkList<RoomPower>();Drawing=new NetworkList<BoardSegment>();actor=GetComponent<NetworkToyPlayer>(); }
        public override void OnNetworkSpawn()
        {
            Rooms.OnListChanged+=RoomsChanged;Drawing.OnListChanged+=DrawingChanged;BoardFocus.OnValueChanged+=FocusChanged;
            var session=UnityEngine.Object.FindFirstObjectByType<PrototypeSession>();
            if(IsServer && OwnerClientId==Unity.Netcode.NetworkManager.ServerClientId && session.UseProceduralStore && ProceduralStore.Instance.Layout.IsFixed)
                for(int i=0;i<3;i++) Rooms.Add(new RoomPower {Battery=100,SeatOwner=RoomPower.Nobody,MarkerOwner=RoomPower.Nobody});
            lastTick=NetworkManager.ServerTime.Time;
            RefreshWorld();
        }
        void RoomsChanged(NetworkListEvent<RoomPower> change)=>RefreshWorld();
        void DrawingChanged(NetworkListEvent<BoardSegment> change)=>RefreshWorld();
        void RefreshWorld()
        { if(Rooms.Count==3 && ProceduralStore.Instance!=null) ProceduralStore.Instance.ApplyRoomState(this); }
        void FocusChanged(bool before,bool after) { if(IsOwner) actor.SetInteractionCursor(after); }
        void Update()
        {
            if(!IsSpawned) return;
            if(IsServer && Rooms.Count==3)
            {
                double now=NetworkManager.ServerTime.Time;
                if(now-lastTick>=.1)
                {
                    for(int room=1;room<=3;room++)
                    {
                        var value=Rooms[room-1];
                        if(!value.Started)
                            foreach(var player in FindObjectsByType<NetworkToyPlayer>(FindObjectsSortMode.None))
                                if(player.IsSpawned && !player.Captured.Value && Inside(room,player.transform.position)) { value.Started=true;break; }
                        value.Advance(lastTick,(float)(now-lastTick));Rooms[room-1]=value;
                    }
                    lastTick=now;
                }
                foreach(var player in FindObjectsByType<NetworkControlRoom>(FindObjectsSortMode.None))
                    if(player.IsSpawned && (player.actor.Captured.Value || player.actor.Role.Value==3) && (player.SeatedRoom.Value!=0 || player.MarkerRoom.Value!=0)) player.ReleaseTools();
            }
            if(IsOwner) UpdateOwnerInput();
        }
        static bool Inside(int room,Vector3 p)=>ProceduralStore.Instance.Layout.Fixed.Control(room).bounds.Contains(new Vector2(p.x,p.z));
        bool CloseTo(Vector3 p,float range=2.4f)=>Vector3.Distance(transform.position+Vector3.up, p)<range;
        bool CanOperate(int room)=>World!=null && room>=1 && room<=3 && !actor.Captured.Value && actor.Role.Value!=3 &&
            (ProceduralStore.Instance.Unlocked & 1<<(room-1))!=0;
        void Reject(string reason) { Rejections++;StatusRpc(reason); }
        [Rpc(SendTo.Owner)] void StatusRpc(string reason) { status=reason; }
        [Rpc(SendTo.Server,RequireOwnership=true)]
        public void ActionRpc(int room,int side,int kind)
        { HandleAction(room,side,kind); }
        internal void HandleAction(int room,int side,int kind)
        {
            if(!IsServer) return;
            if(kind<0 || kind>(int)RoomAction.Board || !CanOperate(room) || side<0 || side>1) { Reject("Cannot use this control.");return; }
            var action=(RoomAction)kind;var world=World;var state=world.Rooms[room-1];
            if(action==RoomAction.Seat)
            {
                if(SeatedRoom.Value==room) { StandUp();return; }
                if(SeatedRoom.Value!=0 || state.SeatOwner!=RoomPower.Nobody || !Inside(room,transform.position) || !CloseTo(SeatPoint(room))) { Reject("Chair unavailable or too far away.");return; }
                if(MarkerRoom.Value!=0) ReleaseMarker();
                state=world.Rooms[room-1];state.SeatOwner=OwnerClientId;world.Rooms[room-1]=state;SeatedRoom.Value=room;
                actor.MoveToSeat(SeatPoint(room),true);actor.FaceConsoleRpc();return;
            }
            if(action==RoomAction.Marker)
            {
                if(state.MarkerOwner!=RoomPower.Nobody || MarkerRoom.Value!=0 || MovementBlocked || !Inside(room,transform.position) || !CloseTo(MarkerPoint(room))) { Reject("Marker unavailable or too far away.");return; }
                state.MarkerOwner=OwnerClientId;world.Rooms[room-1]=state;MarkerRoom.Value=room;return;
            }
            if(action==RoomAction.Board)
            {
                if(MarkerRoom.Value!=room || state.MarkerOwner!=OwnerClientId || !NearBoard(room)) { Reject("Take this room's marker and approach the board.");return; }
                BoardFocus.Value=true;return;
            }
            bool seated=SeatedRoom.Value==room && state.SeatOwner==OwnerClientId;
            if(!seated && (!Inside(room,transform.position) || !CloseTo(SwitchPoint(room,side,action),1.8f))) { Reject("Approach the switch or sit at the console.");return; }
            if(state.Battery<=0) { Reject("This room's battery is depleted.");return; }
            if(action==RoomAction.Door)
            {
                if((state.Closed & 1<<side)==0 && !ProceduralStore.Instance.DoorwayClear(room,side)) { Reject("Doorway obstructed.");return; }
                state.Closed^=1<<side;
            }
            else
            {
                double now=NetworkManager.ServerTime.Time;
                if(state.Lit(side,now)) return; // Pressing again does not extend the pulse.
                if(side==0) state.LeftLightUntil=now+2;else state.RightLightUntil=now+2;
            }
            world.Rooms[room-1]=state;
        }
        bool NearBoard(int room)=>Inside(room,transform.position) && Mathf.Abs(transform.position.x-ProceduralStore.Instance.Layout.ControlCenter(room).x)<3.2f &&
            Mathf.Abs(transform.position.z-BoardPoint(room).z)<2.2f;
        bool StandUp()
        {
            int room=SeatedRoom.Value;if(room==0) return true;
            Vector3 center=ProceduralStore.Instance.Layout.ControlCenter(room);
            foreach(float offset in new[]{0f,-1.2f,1.2f})
            {
                var point=center+new Vector3(offset,.08f,-1.2f);bool blocked=false;
                foreach(var hit in Physics.OverlapCapsule(point+Vector3.up*.35f,point+Vector3.up*1.3f,.32f))
                    if(hit.GetComponentInParent<NetworkToyPlayer>()!=actor) blocked=true;
                if(blocked) continue;
                var value=World.Rooms[room-1];value.SeatOwner=RoomPower.Nobody;World.Rooms[room-1]=value;
                SeatedRoom.Value=0;actor.MoveToSeat(point,false);return true;
            }
            Reject("Space in front of the chair is occupied.");return false;
        }
        public void ReleaseTools(bool reposition=true)
        {
            if(!IsServer || World==null) return;
            if(SeatedRoom.Value!=0)
            {
                int room=SeatedRoom.Value;var value=World.Rooms[room-1];value.SeatOwner=RoomPower.Nobody;World.Rooms[room-1]=value;
                SeatedRoom.Value=0;if(reposition) actor.MoveToSeat(ProceduralStore.Instance.Layout.ControlCenter(room)+new Vector3(0,.08f,-1.2f),false);
            }
            ReleaseMarker();
        }
        void ReleaseMarker()
        {
            if(MarkerRoom.Value!=0)
            { var value=World.Rooms[MarkerRoom.Value-1];value.MarkerOwner=RoomPower.Nobody;World.Rooms[MarkerRoom.Value-1]=value;MarkerRoom.Value=0; }
            BoardFocus.Value=false;strokeOpen=false;
        }
        [Rpc(SendTo.Server,RequireOwnership=true)] public void DropMarkerRpc() { if(World!=null) ReleaseMarker(); }
        [Rpc(SendTo.Server,RequireOwnership=true)] public void CloseBoardRpc() { BoardFocus.Value=false;strokeOpen=false; }
        [Rpc(SendTo.Server,RequireOwnership=true)]
        public void DrawRpc(Vector2 point,bool begin)
        {
            int room=MarkerRoom.Value;
            if(!CanOperate(room) || !BoardFocus.Value || World.Rooms[room-1].MarkerOwner!=OwnerClientId || !NearBoard(room) ||
                !float.IsFinite(point.x) || !float.IsFinite(point.y) || point.x<0 || point.x>1 || point.y<0 || point.y>1) { Reject("Cannot write here.");return; }
            drawCredit=Mathf.Min(8,drawCredit+(Time.unscaledTime-lastDrawTime)*40);lastDrawTime=Time.unscaledTime;
            if(drawCredit<1 || World.Drawing.Count>=4000) return;
            drawCredit--;
            if(begin) { stroke++;lastPoint=point;strokeOpen=true; }
            if(!strokeOpen) return;
            World.Drawing.Add(new BoardSegment {Room=room,Owner=OwnerClientId,Stroke=stroke,From=lastPoint,To=point});lastPoint=point;
        }
        [Rpc(SendTo.Server,RequireOwnership=true)] public void ReportRoomTestRpc(int mask) { if(Array.IndexOf(Environment.GetCommandLineArgs(),"-nts-room-test")>=0) TestVerified=mask==31; }
        public override void OnNetworkDespawn()
        {
            if(IsServer && !NetworkManager.ShutdownInProgress && OwnerClientId!=Unity.Netcode.NetworkManager.ServerClientId) ReleaseTools(false);
            Rooms.OnListChanged-=RoomsChanged;Drawing.OnListChanged-=DrawingChanged;BoardFocus.OnValueChanged-=FocusChanged;
            if(IsOwner && BoardFocus.Value) actor.SetInteractionCursor(false);
        }
    }
}
