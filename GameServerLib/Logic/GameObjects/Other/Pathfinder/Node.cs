using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace LeagueSandbox.GameServer.Logic.GameObjects.Other.Pathfinder
{
    public class Node
    {
        private Node _parent;
        public Vector2 Position { get; set; }

        public Node Parent
        {
            get => _parent;
            set
            {
                _parent = value;
                GCost = _parent.GCost + GetTraversalCost(Position, _parent.Position);
            }
        }
        public List<Node> Neighbors { get; set; }
        public float GCost { get; set; }
        public float HCost { get; set; }
        public float FCost => GCost + HCost;
        public NodeState State { get; set; }

        public Node(int x, int y, Vector2 endLocation)
        {
            Position = new Vector2(x, y);
            State = NodeState.NotTested;
            HCost = GetTraversalCost(Position, endLocation);
            GCost = 0;
        }

        public Node(int x, int y)
        {
            Position = new Vector2(x, y);
            State = NodeState.NotTested;
        }

        public override string ToString()
        {
            return $"{Position}:{State}";
        }

        internal static float GetTraversalCost(Vector2 pos, Vector2 otherPos)
        {
            return Vector2.Distance(pos, otherPos);
        }
    }

    public enum NodeState
    {
        Open,
        Closed,
        NotTested
    }
}
