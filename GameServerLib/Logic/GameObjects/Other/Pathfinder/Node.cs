using System.Collections.Generic;
using System.Numerics;
using LeagueSandbox.GameServer.Core.Logic;
using LeagueSandbox.GameServer.Logic.Content;

namespace LeagueSandbox.GameServer.Logic.GameObjects.Other.Pathfinder
{
    public class Node
    {
        private Node _parent;
        private static Game _game = Program.ResolveDependency<Game>();
        public Vector2 GridPosition { get; set; }

        public Vector2 GamePosition
        {
            get
            {
                var realPos =
                    _game.Map.NavGrid.TranslateFromNavGrid(
                        new Content.Vector<float> {X = GridPosition.X, Y = GridPosition.Y}
                    );
                return new Vector2(realPos.X, realPos.Y);
            }
        }

        public Node Parent
        {
            get => _parent;
            set
            {
                _parent = value;
                GCost = _parent.GCost + Vector2.Distance(GridPosition, _parent.GridPosition);
            }
        }
        public float GCost { get; set; }
        public float HCost { get; set; }
        public float FCost => GCost + HCost;
        public NodeState State { get; set; } = NodeState.NotTested;

        public Node(float x, float y, Vector2 endLocation)
        {
            GridPosition = new Vector2(x, y);
            HCost = Vector2.Distance(GridPosition, endLocation);
        }

        public Node(float x, float y)
        {
            GridPosition = new Vector2(x, y);
        }

        public Node(Vector2 position, Vector2 endPosition) : this(position.X, position.Y, endPosition)
        {
            
        }

        public Node(Vector2 position) : this(position.X, position.Y)
        {
            
        }

        public Node(NavGridCell cell, Vector2 endLocation) : this(cell.X, cell.Y, endLocation)
        {

        }

        public Node(NavGridCell cell) : this(cell.X, cell.Y)
        {

        }

        public override string ToString()
        {
            return $"{GridPosition}:{State}";
        }
    }

    public enum NodeState
    {
        Open,
        Closed,
        NotTested
    }
}
