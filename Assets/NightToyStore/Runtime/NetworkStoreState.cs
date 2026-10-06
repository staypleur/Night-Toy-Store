using Unity.Netcode;
using UnityEngine;

namespace NightToyStore
{
    public sealed class NetworkStoreState : NetworkBehaviour
    {
        public readonly NetworkVariable<int> Seed=new NetworkVariable<int>(0);
        public readonly NetworkVariable<int> Unlocked=new NetworkVariable<int>(0);
        public readonly NetworkVariable<int> Picked=new NetworkVariable<int>(0);
        public readonly NetworkVariable<int> OwnedKeys=new NetworkVariable<int>(0);
        public readonly NetworkVariable<bool> SharedKeys=new NetworkVariable<bool>(false);
        public int RejectedInteractions { get; private set; }
        public bool TestVerified { get; private set; }
        [Rpc(SendTo.Server,RequireOwnership=true)]
        public void ReportTestRpc(string fingerprint,int unlocked,int picked)
        {
            if(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"-nts-store-test")<0) return;
            TestVerified=fingerprint==ProceduralStore.Instance.Layout.Fingerprint && unlocked==Session.StoreUnlocked && picked==Session.StorePicked;
        }
        PrototypeSession Session => Object.FindFirstObjectByType<PrototypeSession>();
        public override void OnNetworkSpawn()
        {
            if(!Session.UseProceduralStore) return;
            Seed.OnValueChanged+=Changed;Unlocked.OnValueChanged+=Changed;Picked.OnValueChanged+=Changed;
            if(IsServer)
            {
                Seed.Value=Session.StoreSeed;Unlocked.Value=Session.StoreUnlocked;Picked.Value=Session.StorePicked;
                SharedKeys.Value=Session.ShareStoreKeys;
                if(SharedKeys.Value) OwnedKeys.Value=Session.StoreKeys;
            }
            Refresh();
        }
        void Changed(int previous,int current) => Refresh();
        void Refresh()
        {
            if(ProceduralStore.Instance==null || Seed.Value==0) return;
            ProceduralStore.Instance.Ensure(Seed.Value);
            ProceduralStore.Instance.Apply(Unlocked.Value,Picked.Value);
        }
        void Update()
        {
            if(!IsSpawned || !IsOwner || Seed.Value==0 || !Application.isFocused) return;
            if(Input.GetKeyDown(KeyCode.E)) { int target=NearestInteraction();if(target!=0) InteractRpc(target); }
        }
        public int NearestInteraction()
        {
            var map=ProceduralStore.Instance;if(map==null || map.Layout==null) return 0;
            int result=0;float distance=2.2f;
            for(int room=1;room<=3;room++)
            {
                if(map.KeyVisible(room))
                { float d=HorizontalDistance(transform.position,map.Layout.Keys[room-1]);if(d<distance){distance=d;result=room;} }
                if(map.DoorBlocked(room))
                { float d=HorizontalDistance(transform.position,map.Layout.Door(room));if(d<distance){distance=d;result=-room;} }
            }
            return result;
        }
        static float HorizontalDistance(Vector3 a,Vector3 b) { a.y=b.y=0;return Vector3.Distance(a,b); }
        [Rpc(SendTo.Server,RequireOwnership=true)]
        public void InteractRpc(int target)
        {
            if(Seed.Value==0 || target==0 || target < -3 || target>3) { RejectedInteractions++;return; }
            var session=Session;var map=ProceduralStore.Instance;int room=Mathf.Abs(target),bit=1<<(room-1);
            Vector3 destination=target>0?map.Layout.Keys[room-1]:map.Layout.Door(room);
            if(HorizontalDistance(transform.position,destination)>2.2f) { RejectedInteractions++;return; }
            if(target>0)
            {
                if(room==map.Layout.StartRoom || (session.StorePicked & bit)!=0 || !KeyPathClear(destination)) { RejectedInteractions++;return; }
                session.StorePicked|=bit;
                OwnedKeys.Value|=bit;
                if(SharedKeys.Value) session.StoreKeys|=bit;
                Debug.Log($"NTS_KEY_PICKUP owner={OwnerClientId} room={room}");
            }
            else
            {
                if((OwnedKeys.Value & bit)==0 || (session.StoreUnlocked & bit)!=0) { RejectedInteractions++;return; }
                session.StoreUnlocked|=bit;
                Debug.Log($"NTS_ROOM_UNLOCK owner={OwnerClientId} room={room}");
            }
            foreach(var state in FindObjectsByType<NetworkStoreState>(FindObjectsSortMode.None))
            {
                state.Unlocked.Value=session.StoreUnlocked;state.Picked.Value=session.StorePicked;
                if(SharedKeys.Value) state.OwnedKeys.Value=session.StoreKeys;
            }
        }
        bool KeyPathClear(Vector3 destination)
        {
            Vector3 start=transform.position+Vector3.up*.8f;destination.y=start.y;
            foreach(var hit in Physics.RaycastAll(start,(destination-start).normalized,Vector3.Distance(start,destination)))
                if(hit.collider.GetComponentInParent<NetworkToyPlayer>()==null) return false;
            return true;
        }
        void OnGUI()
        {
            if(!IsSpawned || !IsOwner || Seed.Value==0) return;
            string inventory="";for(int room=1;room<=3;room++) if((OwnedKeys.Value & 1<<(room-1))!=0) inventory+=room+" ";
            GUI.Label(new Rect(24,466,600,25),"Control room keys: "+(inventory==""?"none":inventory)+(SharedKeys.Value?" (team shared)":" (carried by you)"));
            int target=NearestInteraction();
            if(target!=0) GUI.Box(new Rect(Screen.width/2-180,Screen.height-90,360,35),target>0?"E: collect control room "+target+" key":(OwnedKeys.Value & 1<<(-target-1))!=0?"E: unlock control room "+(-target):"Control room "+(-target)+" key required");
        }
        public override void OnNetworkDespawn()
        {
            // Prototype recovery: a disconnect must not permanently strand an unopened room.
            if(IsServer && Seed.Value!=0 && !SharedKeys.Value && !NetworkManager.ShutdownInProgress)
            {
                var session=Session;int lost=OwnedKeys.Value & ~session.StoreUnlocked;
                session.StorePicked &= ~lost;
                foreach(var state in FindObjectsByType<NetworkStoreState>(FindObjectsSortMode.None))
                    if(state!=this && state.IsSpawned) state.Picked.Value=session.StorePicked;
            }
            Seed.OnValueChanged-=Changed;Unlocked.OnValueChanged-=Changed;Picked.OnValueChanged-=Changed;
        }
    }
}
