using UnityEngine;

namespace NightToyStore
{
    public sealed partial class NetworkControlRoom
    {
        bool dragging;
        float nextPaint;
        Vector2 previousPaint;
        public bool ConsumesInteractInput=>BoardFocus.Value || SeatedRoom.Value!=0 || CurrentTarget()!=null;
        RoomInteractable CurrentTarget()
        {
            if(actor.OwnerCamera==null || ProceduralStore.Instance==null || ProceduralStore.Instance.Layout==null || !ProceduralStore.Instance.Layout.IsFixed) return null;
            var camera=actor.OwnerCamera;
            if(Physics.Raycast(camera.transform.position,camera.transform.forward,out var hit,2.4f))
            {
                var target=hit.collider.GetComponentInParent<RoomInteractable>();if(target!=null) return target;
            }
            foreach(var target in FindObjectsByType<RoomInteractable>(FindObjectsSortMode.None))
                if((target.Action==RoomAction.Seat || target.Action==RoomAction.Marker) && target.gameObject.activeInHierarchy && target.GetComponent<Collider>().enabled &&
                    Vector2.Distance(new Vector2(transform.position.x,transform.position.z),new Vector2(target.transform.position.x,target.transform.position.z))<1.1f) return target;
            return null;
        }
        void UpdateOwnerInput()
        {
            if(!Application.isFocused || actor.IsAutomated) return;
            if(BoardFocus.Value)
            { if(Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E)) { dragging=false;CloseBoardRpc(); }return; }
            if(Input.GetKeyDown(KeyCode.G) && MarkerRoom.Value!=0) DropMarkerRpc();
            if(SeatedRoom.Value!=0)
            {
                int room=SeatedRoom.Value;
                if(Input.GetKeyDown(KeyCode.E)) ActionRpc(room,0,(int)RoomAction.Seat);
                // Console faces -Z, so screen-left is the room's +X side.
                if(Input.GetKeyDown(KeyCode.J)) ActionRpc(room,1,(int)RoomAction.Door);
                if(Input.GetKeyDown(KeyCode.L)) ActionRpc(room,0,(int)RoomAction.Door);
                if(Input.GetKeyDown(KeyCode.U)) ActionRpc(room,1,(int)RoomAction.Light);
                if(Input.GetKeyDown(KeyCode.O)) ActionRpc(room,0,(int)RoomAction.Light);
            }
            else if(Input.GetKeyDown(KeyCode.E))
            { var target=CurrentTarget();if(target!=null) ActionRpc(target.Room,target.Side,(int)target.Action); }
        }
        void OnGUI()
        {
            if(!IsSpawned || !IsOwner || World==null) return;
            var world=World;
            float x=Screen.width-312;
            GUI.Box(new Rect(x,435,298,132),"CONTROL ROOM BATTERIES");
            for(int i=0;i<3;i++)
            {
                var value=world.Rooms[i];
                GUI.Label(new Rect(x+12,463+i*27,280,25),$"Room {i+1}: {value.Battery:F1}% / "+(value.Started?$"{value.DrainMultiplier(NetworkManager.ServerTime.Time):F1}x":"unused"));
            }
            if(SeatedRoom.Value!=0)
                GUI.Box(new Rect(Screen.width/2-310,Screen.height-155,620,50),"J / L: left / right door    U / O: left / right light\nE: leave chair");
            else if(!BoardFocus.Value)
            {
                var target=CurrentTarget();
                if(target!=null)
                {
                    string instruction=actor.Role.Value==3?"Tennis ball: CCTV only (CCTV coming later)":target.Action==RoomAction.Seat?"E: sit at console":
                        target.Action==RoomAction.Marker?"E: take marker":target.Action==RoomAction.Board?"E: write on board (marker required)":
                        target.Action==RoomAction.Door?"E: toggle door":"E: flash entrance light (2 seconds)";
                    GUI.Box(new Rect(Screen.width/2-240,Screen.height-92,480,34),instruction);
                }
                if(MarkerRoom.Value!=0) GUI.Label(new Rect(24,520,620,25),"Holding room "+MarkerRoom.Value+" marker. Approach board, E to write; G to return marker.");
            }
            if(!string.IsNullOrEmpty(status)) GUI.Label(new Rect(24,549,650,25),status);
            if(BoardFocus.Value) DrawBoardPanel();
        }
        void DrawBoardPanel()
        {
            int depth=GUI.depth;GUI.depth=-1000;
            GUI.color=new Color(.025f,.035f,.03f);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=Color.white;
            float width=Mathf.Min(1000,Screen.width-100),height=width*.3f;
            var canvas=new Rect((Screen.width-width)/2,(Screen.height-height)/2,width,height);
            GUI.Box(new Rect(canvas.x-15,canvas.y-85,width+30,height+145),"ROOM "+MarkerRoom.Value+" BOARD");
            GUI.Label(new Rect(canvas.x,canvas.y-53,width,35),"Hold left mouse and draw. Esc / E: stop writing. G after leaving: return marker.");
            GUI.color=actor.Role.Value==1?Color.black:new Color(.025f,.1f,.075f);GUI.DrawTexture(canvas,Texture2D.whiteTexture);GUI.color=Color.white;
            if(actor.Role.Value!=1)
                foreach(var segment in World.Drawing)
                    if(segment.Room==MarkerRoom.Value) DrawUILine(new Vector2(canvas.x+segment.From.x*width,canvas.y+(1-segment.From.y)*height),
                        new Vector2(canvas.x+segment.To.x*width,canvas.y+(1-segment.To.y)*height));
            else GUI.Label(new Rect(canvas.x,canvas.y+height+12,width,25),"Grandmother writes by touch; the drawing is not visible to her.");
            var current=Event.current;
            bool down=current.type==EventType.MouseDown && current.button==0 && canvas.Contains(current.mousePosition);
            if(down) dragging=true;
            if(current.type==EventType.MouseUp) dragging=false;
            if(down || dragging && current.type==EventType.MouseDrag && Time.unscaledTime>=nextPaint)
            {
                var point=new Vector2(Mathf.Clamp01((current.mousePosition.x-canvas.x)/width),Mathf.Clamp01(1-(current.mousePosition.y-canvas.y)/height));
                if(down || (point-previousPaint).sqrMagnitude>.000005f)
                { DrawRpc(point,down);nextPaint=Time.unscaledTime+.04f;previousPaint=point; }
                current.Use();
            }
            GUI.depth=depth;
        }
        static void DrawUILine(Vector2 from,Vector2 to)
        {
            var saved=GUI.matrix;var delta=to-from;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg,from);
            GUI.DrawTexture(new Rect(from.x,from.y-1.5f,Mathf.Max(3,delta.magnitude),3),Texture2D.whiteTexture);GUI.matrix=saved;
        }
    }
}
