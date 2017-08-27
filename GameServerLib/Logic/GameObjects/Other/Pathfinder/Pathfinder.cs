using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using LeagueSandbox.GameServer.Core.Logic;
using LeagueSandbox.GameServer.Logic.Content;

namespace LeagueSandbox.GameServer.Logic.GameObjects.Other.Pathfinder
{
    class Pathfinder : IDisposable
    {
        public static List<Node> BaseNodeList { get; private set; }
        private static Game _game = Program.ResolveDependency<Game>();
        private static Logger _logger = Program.ResolveDependency<Logger>();

        public Node StartNode { get; private set; }
        public Node EndNode { get; private set; }
        public List<Node> Nodes;

        public static void Initialize()
        {

        }

        public Pathfinder(Vector2 startPosition, Vector2 endPosition)
        {
            var grid = _game.Map.NavGrid;
            Nodes = BaseNodeList;

            Content.Vector<float> VectorToCustom(Vector2 vec)
            {
                return new Content.Vector<float> {X = vec.X, Y = vec.Y};
            }

            Vector2 CustomToVector(Content.Vector<float> vec)
            {
                return new Vector2(vec.X, vec.Y);
            }

            var customVect = grid.TranslateToNavGrid(VectorToCustom(startPosition));
            var startCell = grid.GetCell((short)customVect.X, (short)customVect.Y);
            var startNode = Nodes.FirstOrDefault(x => x.Position.X == startCell.X && x.Position.Y == startCell.Y);

            customVect = grid.TranslateToNavGrid(VectorToCustom(endPosition));
            var endCell = grid.GetCell((short)customVect.X, (short)customVect.Y);
            var endNode = Nodes.FirstOrDefault(x => x.Position.X == endCell.X && x.Position.Y == endCell.Y);

            if (startNode == null || endNode == null)
            {
                return;
            }

            foreach (var node in Nodes)
            {
                if (node == startNode || node == endNode)
                {
                    continue;
                }
                node.HCost = Node.GetTraversalCost(node.Position, endNode.Position);
                node.GCost = Node.GetTraversalCost(node.Position, startNode.Position);
            }
        }

        public bool Search(Node currentNode)
        {
            currentNode.State = NodeState.Closed;
            var nextNodes = GetAdjacentNodes(currentNode).ToList();
            nextNodes.Sort((n1, n2) => n1.FCost.CompareTo(n2.FCost));
            foreach (var nextNode in nextNodes)
            {
                if (nextNode.Position == EndNode.Position)
                {
                    return true;
                }
                else // I know this else is redundant but is here for more readability
                {
                    if (Search(nextNode))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public static List<Vector2> FindPath(Vector2 start, Vector2 end)
        {
            var sw = new Stopwatch();
            sw.Start();
            using (var pf = new Pathfinder(start, end))
            {
                var path = new List<Vector2>();
                if (pf.Search(pf.StartNode))
                {
                    var node = pf.EndNode;
                    while (node.Parent != null)
                    {
                        path.Add(node.Position);
                        node = node.Parent;
                    }
                    path.Reverse();
                }
                sw.Stop();
                _logger.LogCoreInfo($"Pathfinder found path between {start} and {end} in {sw.ElapsedMilliseconds} ms.");
                return path;
            }
        }

        public IEnumerable<Node> GetAdjacentNodes(Node from)
        {
            var ret = new List<Node>();
            foreach (var node in from.Neighbors)
            {
                if (node.State == NodeState.Closed)
                {
                    continue;
                }

                if (node.State == NodeState.Open)
                {
                    var traversalCost = Node.GetTraversalCost(node.Position, node.Parent.Position);
                    var gTemp = from.GCost + traversalCost;
                    if (gTemp < node.GCost)
                    {
                        node.Parent = from;
                        ret.Add(node);
                    }
                }
                else
                {
                    node.Parent = from;
                    node.State = NodeState.Open;
                    ret.Add(node);
                }
            }

            return ret;
        }

        public void Dispose()
        {
            Nodes = null;
        }
    }
}
