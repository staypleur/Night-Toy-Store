using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightToyStore
{
    // Version 1: nine handcrafted module slots with randomized adjacency and themes.
    public sealed class StoreLayout
    {
        public const float ModuleSize = 8, DoorWidth = 2.8f;
        public readonly int Seed, StartRoom;
        public readonly int[] ControlCells = new int[3];
        public readonly Vector3[] Keys = new Vector3[3];
        public readonly List<int> Edges = new List<int>();
        public readonly int[] Themes = new int[9];
        public StoreLayout(int seed)
        {
            Seed = seed;
            var random = new System.Random(seed);
            var corners = new List<int> { 0, 2, 6, 8 };
            Shuffle(corners, random);
            for(int i=0;i<3;i++) ControlCells[i]=corners[i];
            StartRoom=random.Next(1,4);
            var publicCells=new List<int>();
            for(int i=0;i<9;i++) if(Room(i)==0) publicCells.Add(i);
            var visited=new HashSet<int>();
            ConnectPublic(4,visited,random);
            // Additional loops keep routes varied while retaining guaranteed connectivity.
            foreach(int cell in publicCells)
                foreach(int other in Neighbors(cell))
                    if(Room(other)==0 && random.Next(3)==0) AddEdge(cell,other);
            foreach(int cell in ControlCells)
            {
                var neighbors=Neighbors(cell);
                Shuffle(neighbors,random);
                AddEdge(cell,neighbors[0]);
            }
            Shuffle(publicCells,random);
            for(int i=0;i<3;i++) Keys[i]=Center(publicCells[i])+new Vector3(1.6f,0,-1.4f);
            foreach(int cell in publicCells) Themes[cell]=random.Next(3);
            Edges.Sort();
            if(!Validate()) throw new InvalidOperationException("Invalid store layout seed " + seed);
        }
        void ConnectPublic(int cell,HashSet<int> visited,System.Random random)
        {
            visited.Add(cell);
            var neighbors=Neighbors(cell);
            Shuffle(neighbors,random);
            foreach(int other in neighbors)
                if(Room(other)==0 && !visited.Contains(other)) { AddEdge(cell,other); ConnectPublic(other,visited,random); }
        }
        static void Shuffle(List<int> values,System.Random random)
        {
            for(int i=values.Count-1;i>0;i--) { int j=random.Next(i+1); int v=values[i];values[i]=values[j];values[j]=v; }
        }
        static List<int> Neighbors(int cell)
        {
            var result=new List<int>();
            if(cell%3>0) result.Add(cell-1);
            if(cell%3<2) result.Add(cell+1);
            if(cell/3>0) result.Add(cell-3);
            if(cell/3<2) result.Add(cell+3);
            return result;
        }
        static int Edge(int a,int b) => Mathf.Min(a,b)*9+Mathf.Max(a,b);
        void AddEdge(int a,int b) { int edge=Edge(a,b); if(!Edges.Contains(edge)) Edges.Add(edge); }
        public bool Connected(int a,int b) => Edges.Contains(Edge(a,b));
        public int Room(int cell) => Array.IndexOf(ControlCells,cell)+1;
        public static Vector3 Center(int cell) => new Vector3((cell%3-1)*ModuleSize,0,(cell/3-1)*ModuleSize);
        public Vector3 Spawn(int slot) => Center(ControlCells[StartRoom-1])+new Vector3((slot-1.5f)*1.6f,.08f,0);
        public Vector3 Door(int room)
        {
            int cell=ControlCells[room-1];
            foreach(int other in Neighbors(cell)) if(Connected(cell,other)) return (Center(cell)+Center(other))*.5f;
            throw new InvalidOperationException("Room without doorway");
        }
        public bool Validate()
        {
            if(DoorWidth < NetworkToyPlayer.BallRadius*2+.5f) return false;
            var reached=new HashSet<int>();var queue=new Queue<int>();
            queue.Enqueue(ControlCells[StartRoom-1]);
            while(queue.Count>0)
            {
                int cell=queue.Dequeue();if(!reached.Add(cell)) continue;
                foreach(int other in Neighbors(cell))
                    if(Connected(cell,other) && (Room(other)==0 || Room(other)==StartRoom)) queue.Enqueue(other);
            }
            for(int i=0;i<9;i++) if(Room(i)==0 && !reached.Contains(i)) return false;
            for(int room=1;room<=3;room++)
            {
                if(!Edges.Contains(Edge(ControlCells[room-1],NeighborAtDoor(room)))) return false;
                var key=Keys[room-1];
                int cell=Mathf.RoundToInt(key.x/ModuleSize+1)+3*Mathf.RoundToInt(key.z/ModuleSize+1);
                if(!reached.Contains(cell)) return false;
            }
            return true;
        }
        int NeighborAtDoor(int room)
        {
            int cell=ControlCells[room-1];
            foreach(int other in Neighbors(cell)) if(Connected(cell,other)) return other;
            return -1;
        }
        public string Fingerprint => Seed+":"+StartRoom+":"+string.Join(",",ControlCells)+":"+string.Join(",",Edges)+":"+string.Join(",",Themes);
        [Serializable] sealed class ExportData
        {
            public int seed,startRoom;public int[] controlCells,edges,themes;public Vector3[] keys;
        }
        public string ToJson() => JsonUtility.ToJson(new ExportData {seed=Seed,startRoom=StartRoom,controlCells=ControlCells,edges=Edges.ToArray(),themes=Themes,keys=Keys},true);
    }
}
