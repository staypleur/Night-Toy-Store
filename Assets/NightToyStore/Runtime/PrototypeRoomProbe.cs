using System;
using System.Collections;
using UnityEngine;
using Unity.Netcode.Components;

namespace NightToyStore
{
    // Opt-in integration test: no microphone or normal gameplay automation.
    public sealed class PrototypeRoomProbe : MonoBehaviour
    {
        void Update() { if(Time.realtimeSinceStartup>40) {Debug.LogError("NTS_ROOM_FAIL watchdog");Application.Quit(4);} }
        void Place(NetworkToyPlayer player,Vector3 point)
        {
            var cc=player.GetComponent<CharacterController>();cc.enabled=false;
            player.GetComponent<Rigidbody>().position=point;
            player.GetComponent<NetworkTransform>().Teleport(point,Quaternion.identity,Vector3.one);
            cc.enabled=player.Role.Value!=3;Physics.SyncTransforms();
        }
        void Check(bool condition,string message) { if(!condition) throw new Exception("NTS_ROOM_FAIL "+message); }
        IEnumerator Start()
        {
            var session=GetComponent<PrototypeSession>();var manager=session.Manager;
            float deadline=Time.realtimeSinceStartup+32;
            while(NetworkControlRoom.World==null || manager.LocalClient?.PlayerObject==null)
            { if(Time.realtimeSinceStartup>deadline){Debug.LogError("NTS_ROOM_FAIL connection timeout");Application.Quit(4);yield break;}yield return null; }
            var owner=manager.LocalClient.PlayerObject.GetComponent<NetworkToyPlayer>();
            var control=owner.RoomControl;var world=NetworkControlRoom.World;var map=ProceduralStore.Instance;int room=map.Layout.StartRoom;
            if(!manager.IsHost)
            {
                bool late=Array.IndexOf(Environment.GetCommandLineArgs(),"-nts-room-late")>=0;
                int mask=0;bool doors=false,lights=false,drawn=false;
                while(Time.realtimeSinceStartup<deadline)
                {
                    var state=world.Rooms[room-1];
                    if(late && state.Battery==0 && world.Drawing.Count>=4 && control.MarkerRoom.Value==room)
                    { Check(!map.DefenseDoorClosed(room,0) && FindObjectsByType<LineRenderer>(FindObjectsSortMode.None).Length>=2,"late visuals");Debug.Log("NTS_ROOM_LATE_PASS persistent drawing and depleted room replay");manager.Shutdown();yield break; }
                    if(!late)
                    {
                        if(control.SeatedRoom.Value==room && !doors) {doors=true;owner.JumpRpc();control.ActionRpc(room,0,(int)RoomAction.Door);control.ActionRpc(room,1,(int)RoomAction.Door);}
                        if(state.Closed==3) mask|=1;
                        if(state.Closed==3 && !lights) {lights=true;control.ActionRpc(room,0,(int)RoomAction.Light);control.ActionRpc(room,1,(int)RoomAction.Light);}
                        if(state.Lit(0,manager.ServerTime.Time) && state.Lit(1,manager.ServerTime.Time)) mask|=2;
                        if(control.MarkerRoom.Value==room && !drawn) {drawn=true;control.ActionRpc(room,0,(int)RoomAction.Board);}
                        if(control.BoardFocus.Value && world.Drawing.Count==0)
                        {control.DrawRpc(new Vector2(.2f,.2f),true);control.DrawRpc(new Vector2(.8f,.8f),false);control.DrawRpc(new Vector2(.2f,.8f),true);control.DrawRpc(new Vector2(.8f,.2f),false);}
                        if(world.Drawing.Count>=4) mask|=4;
                        int started=0;foreach(var power in world.Rooms) if(power.Started) started++;
                        if(started>=2) mask|=8;
                        if(state.Battery==0 && !map.DefenseDoorClosed(room,0)) mask|=16;
                        control.ReportRoomTestRpc(mask);
                        if(mask==31) {Debug.Log("NTS_ROOM_CLIENT_PASS owner RPC doors lights drawing and battery replication");yield break;}
                    }
                    yield return new WaitForSeconds(.05f);
                }
                Debug.LogError("NTS_ROOM_FAIL client timeout mask="+mask);Application.Quit(4);yield break;
            }
            var idle=new RoomPower{Battery=100,Started=true};idle.Advance(0,210);Check(Mathf.Abs(idle.Battery-50)<.001f,"idle halfway");idle.Advance(210,210);Check(idle.Battery==0,"420 second depletion");
            var unused=new RoomPower{Battery=100};unused.Advance(0,1000);Check(unused.Battery==100,"unused room drain");
            foreach(int bits in new[]{1,3}) {var p=new RoomPower{Battery=100,Started=true,Closed=bits};p.Advance(0,100);Check(Mathf.Abs(p.Battery-(100-100f/420*100*(bits==1?1.2f:1.4f)))<.001f,"door multiplier");}
            var pulse=new RoomPower{Battery=100,Started=true,LeftLightUntil=2,RightLightUntil=2};pulse.Advance(0,4);Check(Mathf.Abs(pulse.Battery-(100-100f/420*4.4f))<.001f,"light expiry integration");
            yield return new WaitForSeconds(.3f);
            foreach(var p in world.Rooms) Check(p.Started==(world.Rooms.IndexOf(p)==room-1),"first entry only");
            NetworkToyPlayer remote=null;
            while(remote==null) {foreach(var p in FindObjectsByType<NetworkToyPlayer>(FindObjectsSortMode.None)) if(p.IsSpawned && !p.IsOwner) remote=p;yield return null;}
            yield return new WaitForSeconds(.3f);
            Place(remote,NetworkControlRoom.SeatPoint(room)+Vector3.forward);remote.RoomControl.HandleAction(room,0,(int)RoomAction.Seat);
            Check(remote.RoomControl.SeatedRoom.Value==room,"seat");
            int rejected=control.Rejections;Place(owner,NetworkControlRoom.SeatPoint(room)+Vector3.right);control.ActionRpc(room,0,(int)RoomAction.Seat);Check(control.Rejections>rejected,"exclusive seat");
            int jumps=remote.AcceptedJumps;
            float until=Time.time+4;while(world.Rooms[room-1].Closed!=3 && Time.time<until) yield return null;
            Check(world.Rooms[room-1].Closed==3 && map.DefenseDoorClosed(room,0),"client doors");
            Check(remote.AcceptedJumps==jumps,"seated jump rejected");
            until=Time.time+4;while(!world.Rooms[room-1].Lit(1,manager.ServerTime.Time) && Time.time<until) yield return null;
            Check(world.Rooms[room-1].Lit(1,manager.ServerTime.Time),"client lights");
            yield return new WaitForSeconds(2.2f);Check(!world.Rooms[room-1].Lit(0,manager.ServerTime.Time),"two second lights");
            remote.RoomControl.HandleAction(room,0,(int)RoomAction.Seat);Check(!remote.RoomControl.MovementBlocked,"stand up");
            Place(owner,NetworkControlRoom.SwitchPoint(room,0,RoomAction.Door)+Vector3.right*.5f);control.ActionRpc(room,0,(int)RoomAction.Door);Check((world.Rooms[room-1].Closed&1)==0,"local switch");
            Place(owner,map.Layout.ControlCenter(room));
            Place(remote,NetworkControlRoom.MarkerPoint(room)-Vector3.forward*.8f);remote.RoomControl.HandleAction(room,0,(int)RoomAction.Marker);
            until=Time.time+4;while(world.Drawing.Count<4 && Time.time<until) yield return null;
            Check(world.Drawing.Count==4 && remote.RoomControl.BoardFocus.Value,"client freehand drawing");
            int count=world.Drawing.Count;control.DrawRpc(new Vector2(float.NaN,0),false);Check(world.Drawing.Count==count,"unauthorized drawing rejected");
            remote.RoomControl.ReleaseTools();Check(remote.RoomControl.MarkerRoom.Value==0 && !remote.RoomControl.BoardFocus.Value,"marker return");
            owner.ChangeRoleRpc(3);yield return new WaitForSeconds(.1f);
            foreach(var action in new[]{RoomAction.Seat,RoomAction.Door,RoomAction.Light,RoomAction.Marker,RoomAction.Board}) {rejected=control.Rejections;control.ActionRpc(room,0,(int)action);Check(control.Rejections>rejected,"ball restriction "+action);}
            owner.ChangeRoleRpc(0);yield return new WaitForSeconds(.1f);
            int other=room%3+1;var store=owner.GetComponent<NetworkStoreState>();
            Place(owner,map.Layout.Keys[other-1]);store.InteractRpc(other);Place(owner,map.Layout.Door(other)-Vector3.right*1.2f);store.InteractRpc(-other);
            Place(owner,map.Layout.ControlCenter(other));yield return new WaitForSeconds(.3f);Check(world.Rooms[other-1].Started,"second room starts independently");
            float remaining=world.Rooms[room-1].Battery;yield return new WaitForSeconds(.3f);Check(world.Rooms[room-1].Battery<remaining,"drain continues after leaving");
            var empty=world.Rooms[room-1];empty.Battery=.01f;empty.Closed=3;empty.Advance(manager.ServerTime.Time,2);world.Rooms[room-1]=empty;
            yield return new WaitForSeconds(.3f);Check(world.Rooms[room-1].Battery==0 && !map.DefenseDoorClosed(room,1),"empty opens doors");
            until=Time.time+15;while((!remote.RoomControl.TestVerified || manager.ConnectedClientsIds.Count<3) && Time.time<until) yield return null;
            Check(remote.RoomControl.TestVerified && manager.ConnectedClientsIds.Count>=3,"client and late join");
            NetworkToyPlayer latePlayer=null;foreach(var p in FindObjectsByType<NetworkToyPlayer>(FindObjectsSortMode.None)) if(p!=owner && p!=remote) latePlayer=p;
            Place(latePlayer,NetworkControlRoom.MarkerPoint(room)-Vector3.forward*.8f);latePlayer.RoomControl.HandleAction(room,0,(int)RoomAction.Marker);
            Check(world.Rooms[room-1].MarkerOwner==latePlayer.OwnerClientId,"late marker pickup");
            until=Time.time+4;while(manager.ConnectedClientsIds.Count!=2 && Time.time<until) yield return null;
            Check(world.Rooms[room-1].MarkerOwner==RoomPower.Nobody,"disconnect returns marker");
            Place(owner,NetworkControlRoom.SeatPoint(room)+Vector3.forward);control.ActionRpc(room,0,(int)RoomAction.Seat);Check(control.SeatedRoom.Value==room,"depleted chair");
            owner.TryCapture(false);Check(control.SeatedRoom.Value==0 && world.Rooms[room-1].SeatOwner==RoomPower.Nobody,"capture releases chair");
            Debug.Log("NTS_ROOM_HOST_PASS exact drain, first entry, client switches, exclusive seating, writing, ball restrictions, depletion and late join");Application.Quit(0);
        }
    }
}
