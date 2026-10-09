using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NightToyStore
{
    public sealed class PrototypeStoreProbe : MonoBehaviour
    {
        IEnumerator Start()
        {
            var session=GetComponent<PrototypeSession>();var manager=session.Manager;
            float deadline=Time.realtimeSinceStartup+30;
            while(manager.LocalClient==null || manager.LocalClient.PlayerObject==null || ProceduralStore.Instance.Layout==null)
            { if(Time.realtimeSinceStartup>deadline){Fail("map load timeout");yield break;}yield return null; }
            var actor=manager.LocalClient.PlayerObject.GetComponent<NetworkToyPlayer>();
            var state=actor.GetComponent<NetworkStoreState>();var map=ProceduralStore.Instance;
            bool fixedMap=map.Layout.IsFixed;
            if(!manager.IsHost)
            {
                while(state.Unlocked.Value!=7)
                { if(Time.realtimeSinceStartup>deadline){Fail("client unlock timeout");yield break;}yield return null; }
                yield return new WaitForSeconds(.2f);
                for(int room=1;room<=3;room++) if(map.DoorBlocked(room) || map.KeyVisible(room)) {Fail("client geometry state");yield break;}
                if(map.Layout.Fingerprint!=new StoreLayout(state.Seed.Value,fixedMap).Fingerprint) {Fail("client layout mismatch");yield break;}
                if(state.SharedKeys.Value && state.OwnedKeys.Value!=state.Picked.Value) {Fail("shared key inventory");yield break;}
                if(!state.SharedKeys.Value && state.OwnedKeys.Value!=0) {Fail("personal keys leaked to another player");yield break;}
                state.ReportTestRpc(map.Layout.Fingerprint,state.Unlocked.Value,state.Picked.Value);
                Debug.Log("NTS_STORE_CLIENT_PASS same layout, replicated keys and doors including late join");
                yield break;
            }
            var starts=new HashSet<int>();var shapes=new HashSet<string>();
            int sampleCount=fixedMap?30:2000;
            for(int seed=1;seed<=sampleCount;seed++)
            {
                var layout=new StoreLayout(seed,fixedMap);
                if(!layout.Validate() || layout.Fingerprint!=new StoreLayout(seed,fixedMap).Fingerprint) {Fail("layout validation/determinism");yield break;}
                starts.Add(layout.StartRoom);shapes.Add(layout.Fingerprint.Substring(layout.Fingerprint.IndexOf(':')+1));
            }
            if(starts.Count!=3 || (fixedMap?shapes.Count!=3:shapes.Count<100)) {Fail("layout diversity / fixed geometry");yield break;}
            Debug.Log($"NTS_STORE_LAYOUTS_PASS {sampleCount} seeds, deterministic, three start rooms, reachable keys, ball doorway clearance");
            while(manager.ConnectedClientsIds.Count<3)
            { if(Time.realtimeSinceStartup>deadline){Fail("connection timeout");yield break;}yield return null; }
            yield return new WaitForSeconds(.5f);
            Vector3 start=map.Layout.ControlCenter(map.Layout.StartRoom);
            foreach(var player in FindObjectsByType<NetworkToyPlayer>(FindObjectsSortMode.None))
                if(Mathf.Abs(player.transform.position.x-start.x)>3 || Mathf.Abs(player.transform.position.z-start.z)>3)
                {Fail($"players not in same starting room owner={player.OwnerClientId} position={player.transform.position} expected={start} role={player.Role.Value}");yield break;}
            var rooms=new List<int>();for(int room=3;room>=1;room--) if(room!=map.Layout.StartRoom) rooms.Add(room);
            int rejected=state.RejectedInteractions;
            state.InteractRpc(rooms[0]);
            if(state.RejectedInteractions==rejected) {Fail("remote key pickup allowed");yield break;}
            foreach(int room in rooms)
            {
                Vector3 direction=map.Layout.IsFixed?Vector3.right:(map.Layout.ControlCenter(room)-map.Layout.Door(room)).normalized;
                Place(actor,map.Layout.Door(room)-direction*1.2f);
                rejected=state.RejectedInteractions;state.InteractRpc(-room);
                if(state.RejectedInteractions==rejected || !map.DoorBlocked(room)) {Fail("door opened without matching key");yield break;}
                Place(actor,map.Layout.Keys[room-1]);
                state.InteractRpc(room);
                if((state.OwnedKeys.Value & 1<<(room-1))==0 || map.KeyVisible(room)) {Fail("key pickup failed");yield break;}
                Place(actor,map.Layout.Door(room)-direction*1.2f);
                state.InteractRpc(-room);
                if(map.DoorBlocked(room)) {Fail("key did not unlock door");yield break;}
                yield return new WaitForSeconds(.2f);
                foreach(var doorway in map.Layout.Doors(room))
                    foreach(var hit in Physics.SphereCastAll(doorway-direction*1.6f+Vector3.up*.75f,
                        NetworkToyPlayer.BallRadius,direction,3.2f))
                        if(hit.collider.GetComponentInParent<NetworkToyPlayer>()==null) {Fail("ball blocked in open doorway");yield break;}
            }
            while(manager.ConnectedClientsIds.Count<4)
            { if(Time.realtimeSinceStartup>deadline){Fail("late join timeout");yield break;}yield return null; }
            while(true)
            {
                int reports=0;foreach(var remote in FindObjectsByType<NetworkStoreState>(FindObjectsSortMode.None)) if(remote.TestVerified) reports++;
                if(reports>=3) break;
                if(Time.realtimeSinceStartup>deadline){Fail("client verification timeout");yield break;}yield return null;
            }
            Debug.Log("NTS_STORE_HOST_PASS shared start, matching keys, arbitrary unlock order, collision clearance, state replication and late join");
            Application.Quit(0);
        }
        static void Place(NetworkToyPlayer actor,Vector3 position)
        {
            var controller=actor.GetComponent<CharacterController>();controller.enabled=false;
            actor.transform.position=position+Vector3.up*.08f;controller.enabled=true;Physics.SyncTransforms();
        }
        static void Fail(string reason) {Debug.LogError("NTS_STORE_FAIL "+reason);Application.Quit(2);}
    }
}
