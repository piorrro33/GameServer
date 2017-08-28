using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using LeagueSandbox.GameServer.Core.Logic;
using LeagueSandbox.GameServer.Logic.Content;
using LeagueSandbox.GameServer.Logic.Packets;
using LeagueSandbox.GameServer.Logic.Packets.PacketHandlers;
using LeagueSandbox.GameServer.Logic.Players;

namespace LeagueSandbox.GameServer.Logic.GameObjects.Other.Pathfinder
{
    class Pathfinder : IDisposable
    {
        public static List<Node> BaseNodeList { get; private set; }
        private static Game _game = Program.ResolveDependency<Game>();
        private static Logger _logger = Program.ResolveDependency<Logger>();
        private static PlayerManager _playerManager = Program.ResolveDependency<PlayerManager>();

        private const bool DEBUG_PF = true;

        public Node StartNode { get; private set; }
        public Node EndNode { get; private set; }
        public List<Node> Nodes;

        public static void Initialize(NavGrid grid)
        {
            BaseNodeList = new List<Node>();
            foreach (var cell in grid.Cells)
            {
                if (cell.HasFlag(grid, NavigationGridCellFlags.NotPassable))
                {
                    continue;
                }

                BaseNodeList.Add(new Node(cell));
            }
        }

        public Pathfinder(Vector2 startPosition, Vector2 endPosition)
        {
            Nodes = new List<Node>();
            foreach (var baseNode in BaseNodeList)
            {
                Nodes.Add(new Node(baseNode.GridPosition, endPosition));
            }
            startPosition.X = (int)startPosition.X;
            startPosition.Y = (int)startPosition.Y;
            endPosition.X = (int)endPosition.X;
            endPosition.Y = (int)endPosition.Y;

            StartNode = Nodes.First(x => x.GridPosition == startPosition);
            EndNode = Nodes.First(x => x.GridPosition == endPosition);
            StartNode.State = NodeState.Open;
        }

        public static List<Vector2> FindPath(Vector2 start, Vector2 end)
        {
            if (!_game.Map.NavGrid.IsAnythingBetween(start, end))
            {
                return new List<Vector2>
                {
                    start,
                    end
                };
            }

            var sw = new Stopwatch();
            if (DEBUG_PF)
            {
                sw.Start();
            }
            var path = new List<Vector2>();
            var grid = _game.Map.NavGrid;
            var gridStart = grid.TranslateToNavGrid(new Content.Vector<float> {X = start.X, Y = start.Y});
            var gridEnd = grid.TranslateToNavGrid(new Content.Vector<float> {X = end.X, Y = end.Y});
            start.X = gridStart.X;
            start.Y = gridStart.Y;
            end.X = gridEnd.X;
            end.Y = gridEnd.Y;
            using (var pf = new Pathfinder(start, end))
            {
                if (pf.Search(pf.StartNode))
                {
                    var tempList = new List<Node>();
                    var node = pf.EndNode;
                    while (node.Parent != null)
                    {
                        tempList.Add(node);
                        node = node.Parent;
                    }

                    tempList.Reverse();

                    path.Add(start);
                    foreach (var gridLoc in tempList)
                    {
                        if (DEBUG_PF)
                        {
                            new Task(() =>
                            {
                                var packet = new AttentionPingAns(_playerManager.GetPlayers().First().Item2, new AttentionPing(gridLoc.GamePosition.X, gridLoc.GamePosition.Y, 0, Pings.Ping_Default));
                                _game.PacketHandlerManager.broadcastPacket(packet, Channel.CHL_S2C);
                            }).Start();
                        }
                        path.Add(gridLoc.GamePosition);
                    }
                }
            }
            if (DEBUG_PF)
            {
                sw.Stop();
                Program.ResolveDependency<Logger>().LogCoreInfo($"Pathfinding took {sw.ElapsedMilliseconds} ms.");
            }

            return path;
        }

        public bool Search(Node currentNode)
        {
            currentNode.State = NodeState.Closed;
            var nextNodes = GetAdjacentNodes(currentNode);

            nextNodes.Sort((n1, n2) => n1.FCost.CompareTo(n2.FCost));
            foreach (var nextNode in nextNodes)
            {
                if (nextNode.GridPosition == EndNode.GridPosition)
                {
                    return true;
                }

                if (Search(nextNode))
                {
                    return true;
                }
            }

            return false;
        }

        public List<Node> GetAdjacentNodes(Node from)
        {
            var nodes = new List<Node>();
            var nextNodes = GetAdjacentWalkableHintedNodes(from.GridPosition);

            if (Vector2.Distance(from.GridPosition, EndNode.GridPosition) > 25 && nextNodes.Any())
            {
                foreach (var node in nextNodes)
                {
                    if (node.State == NodeState.Closed)
                    {
                        continue;
                    }

                    if (node.State == NodeState.Open)
                    {
                        var traversalCost = Vector2.Distance(node.GridPosition, node.Parent.GridPosition);
                        var tempG = from.GCost + traversalCost;
                        if (tempG < node.GCost)
                        {
                            node.Parent = from;
                            nodes.Add(node);
                        }
                    }
                    else
                    {
                        node.Parent = from;
                        node.State = NodeState.Open;
                        nodes.Add(node);
                    }
                }
            }
            else
            {
                var nextPositions = GetAdjacentLocations(from.GridPosition);
                foreach (var position in nextPositions)
                {
                    var node = Nodes.FirstOrDefault(x => x.GridPosition == position);
                    if (node == null)
                    {
                        continue;
                    }

                    if (node.State == NodeState.Closed)
                    {
                        continue;
                    }

                    if (node.State == NodeState.Open)
                    {
                        var traversalCost = Vector2.Distance(node.GridPosition, node.Parent.GridPosition);
                        var tempG = from.GCost + traversalCost;
                        if (tempG < node.GCost)
                        {
                            node.Parent = from;
                            nodes.Add(node);
                        }
                    }
                    else
                    {
                        node.Parent = from;
                        node.State = NodeState.Open;
                        nodes.Add(node);
                    }
                }
            }

            return nodes;
        }

        public static IEnumerable<Vector2> GetAdjacentLocations(Vector2 fromLocation, int move = 1)
        {
            return new[]
            {
                new Vector2(fromLocation.X - move, fromLocation.Y - move), // sw
                new Vector2(fromLocation.X - move, fromLocation.Y), // w
                new Vector2(fromLocation.X - move, fromLocation.Y + move), // nw
                new Vector2(fromLocation.X, fromLocation.Y + move), // n
                new Vector2(fromLocation.X + move, fromLocation.Y + move), // ne
                new Vector2(fromLocation.X + move, fromLocation.Y), // e
                new Vector2(fromLocation.X + move, fromLocation.Y - move), // se
                new Vector2(fromLocation.X, fromLocation.Y - move) // s
            };
        }

        public List<Node> GetAdjacentWalkableHintedNodes(Vector2 from)
        {
            var ret = new List<Node>();

            foreach (var position in GetAdjacentLocations(from, 10))
            {
                var node = Nodes.FirstOrDefault(x => x.GridPosition == position);
                
                if (node != null)
                {
                    var fromToGame =
                        _game.Map.NavGrid.TranslateFromNavGrid(new Content.Vector<float> {X = from.X, Y = from.Y});

                    if (_game.Map.NavGrid.IsAnythingBetween(
                        new Vector2(fromToGame.X, fromToGame.Y),
                        node.GamePosition
                    ))
                    {
                        ret.Add(node);
                    }
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
