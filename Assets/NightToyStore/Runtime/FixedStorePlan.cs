using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightToyStore
{
    // Fixed adjacency traced from the user's sketch. Non-control-room dimensions are trial sizes.
    public sealed class FixedStorePlan
    {
        [Serializable] public sealed class Room
        {
            public string name, label;
            public Rect bounds;
            public int control, breaker;
            public Vector3 Center => new Vector3(bounds.center.x, 0, bounds.center.y);
        }
        public readonly List<Room> Rooms = new List<Room>();
        public readonly List<Rect> Corridors = new List<Rect>();
        public readonly HashSet<Vector2Int> Tiles = new HashSet<Vector2Int>();
        public const float CorridorWidth = 4;
        public FixedStorePlan()
        {
            Add("Main hall", "메인홀", 27,-8,24,24);
            Add("Sub hall", "서브홀", -20,18,16,14);
            Add("Large party room 1", "파티룸 대형1", -39,6,18,10);
            Add("Kitchen", "부엌", 28,18,14,14);
            Add("Storage 1", "창고 1",47,-30,8,8);
            Add("Storage 2", "창고 2",-43,24,8,8);
            Add("Doll room", "인형방",-20,33,6,6);
            Add("Chess room", "체스방",13,34,6,6);
            Add("Lego room", "레고방",-9,-3,6,6);
            Add("Party room 1", "파티룸1",6,-3,6,6);
            Add("Party room 2", "파티룸2",47,0,6,6);
            Add("Nursery", "유아방",6,-30,6,6);
            Add("Junction A", "분기 A",-42,-30,6,6);
            Add("Junction B", "분기 B",-42,-12,6,6);
            Add("Junction C", "분기 C",-20,-12,6,6);
            Add("Junction D", "분기 D",-10,-30,6,6);
            Add("Control 1", "관제실 1",1,6,10,10,1);
            Add("Control 2", "관제실 2",45,18,10,10,2);
            Add("Control 3", "관제실 3",-30,-30,10,10,3);
            Add("Breaker 1", "관제실 1 두꺼비집",8,-20,6,6,0,1);
            Add("Breaker 2", "관제실 2 두꺼비집",-29,-20,6,6,0,2);
            Add("Breaker 3", "관제실 3 두꺼비집",13,26,6,6,0,3);
            Path(-42,-30,-42,-12); Path(-42,-12,-42,6);
            Path(-42,-12,-20,-12); Path(-42,-30,-10,-30);
            Path(-20,-12,-20,18); Path(-20,6,28,6);
            Path(-20,-12,15,-12); Path(-10,-30,-10,-12);
            Path(-29,-12,-29,-20); Path(-9,-12,-9,-3);
            Path(6,-12,6,-3); Path(8,-12,8,-20);
            Path(6,-30,47,-30); Path(27,-30,27,-8);
            Path(-20,18,28,18); Path(-20,18,-20,33);
            Path(-43,24,-34,24); Path(-34,24,-34,18); Path(-34,18,-20,18);
            Path(28,6,28,18); Path(13,18,13,26);
            Path(28,18,54,18); Path(28,18,28,34);
            Path(13,34,54,34); Path(54,34,54,0);
            Path(27,0,54,0);
            foreach(var room in Rooms) Fill(room.bounds);
            foreach(var corridor in Corridors) Fill(corridor);
        }
        void Add(string name,string label,float x,float z,float width,float depth,int control=0,int breaker=0)
            => Rooms.Add(new Room {name=name,label=label,bounds=new Rect(x-width/2,z-depth/2,width,depth),control=control,breaker=breaker});
        void Path(float x,float z,float endX,float endZ)
        {
            Corridors.Add(new Rect(Mathf.Min(x,endX)-CorridorWidth/2,Mathf.Min(z,endZ)-CorridorWidth/2,
                Mathf.Abs(endX-x)+CorridorWidth,Mathf.Abs(endZ-z)+CorridorWidth));
        }
        void Fill(Rect rect)
        {
            for(int x=Mathf.FloorToInt(rect.xMin);x<rect.xMax;x++)
                for(int z=Mathf.FloorToInt(rect.yMin);z<rect.yMax;z++)
                    if(rect.Contains(new Vector2(x+.5f,z+.5f))) Tiles.Add(new Vector2Int(x,z));
        }
        public Room Control(int number) => Rooms.Find(room=>room.control==number);
        public Vector3[] Doors(int room) => new[] { Control(room).Center+Vector3.left*5+Vector3.forward,Control(room).Center+Vector3.right*5+Vector3.forward };
        public bool Validate(int start,Vector3[] keys)
        {
            var reached=new HashSet<Vector2Int>();var queue=new Queue<Vector2Int>();
            var center=Control(start).Center;queue.Enqueue(new Vector2Int(Mathf.FloorToInt(center.x),Mathf.FloorToInt(center.z)));
            while(queue.Count>0)
            {
                var tile=queue.Dequeue();if(reached.Contains(tile) || !Tiles.Contains(tile)) continue;
                bool locked=false;
                for(int room=1;room<=3;room++) if(room!=start && Control(room).bounds.Contains(new Vector2(tile.x+.5f,tile.y+.5f))) locked=true;
                if(locked) continue;
                reached.Add(tile);
                foreach(var direction in new[]{Vector2Int.left,Vector2Int.right,Vector2Int.up,Vector2Int.down}) queue.Enqueue(tile+direction);
            }
            foreach(var room in Rooms)
                if(room.control==0 && !reached.Contains(new Vector2Int(Mathf.FloorToInt(room.Center.x),Mathf.FloorToInt(room.Center.z)))) return false;
            foreach(var key in keys) if(!reached.Contains(new Vector2Int(Mathf.FloorToInt(key.x),Mathf.FloorToInt(key.z)))) return false;
            return true;
        }
        [Serializable] sealed class Export
        { public string version="fixed-sketch-v1";public int seed,startRoom;public Room[] rooms;public Rect[] corridors;public Vector3[] keys; }
        public string ToJson(int seed,int start,Vector3[] keys) => JsonUtility.ToJson(new Export {seed=seed,startRoom=start,rooms=Rooms.ToArray(),corridors=Corridors.ToArray(),keys=keys},true);
        public string GeometryFingerprint => Hash128.Compute(ToJson(0,0,null)).ToString();
    }
}
