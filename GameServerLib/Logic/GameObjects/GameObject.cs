using LeagueSandbox.GameServer.Core.Logic;
using LeagueSandbox.GameServer.Logic.Enet;
using LeagueSandbox.GameServer.Logic.GameObjects;
using LeagueSandbox.GameServer.Logic.Packets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using LeagueSandbox.GameServer.Logic.Packets.PacketDefinitions.S2C;
using LeagueSandbox.GameServer.Logic.Packets.PacketHandlers;

namespace LeagueSandbox.GameServer.Logic
{
    public class GameObject : Target
    {
        public uint NetId { get; private set; }
        protected float XVector, YVector;

        /// <summary>
        /// Current target the object running to (can be coordinates or an object)
        /// </summary>
        public Target Target { get; set; }

        public List<Vector2> Waypoints { get; private set; }
        public int CurWaypoint { get; private set; }
        private TeamId _team;

        public TeamId Team
        {
            get { return _team; }
            internal set
            {
                _visibleByTeam[_team] = false;
                _team = value;
                _visibleByTeam[_team] = true;
                
                if (!Game.IsRunning)
                    return;
                
                var p = new SetTeam(this as Unit, value);
                Game.PacketHandlerManager.broadcastPacket(p, Channel.CHL_S2C);
            }
        }

        public bool MovementUpdated { get; set; }
        public bool ToBeRemoved { get; set; }
        public int AttackerCount { get; private set; }
        public float CollisionRadius { get; set; }
        protected Vector2 Direction;
        public float VisionRadius { get; protected set; }
        public bool IsDashing { get; protected set; }
        public override bool IsSimpleTarget { get { return false; } }
        protected float DashSpeed { get; set; }
        private readonly Dictionary<TeamId, bool> _visibleByTeam;
        protected readonly Game Game = Program.ResolveDependency<Game>();
        protected NetworkIdManager NetworkIdManager = Program.ResolveDependency<NetworkIdManager>();

        public GameObject(float x, float y, int collisionRadius, int visionRadius = 0, uint netId = 0) : base(x, y)
        {
            if (netId != 0)
            {
                NetId = netId; // Custom netId
            }
            else
            {
                NetId = NetworkIdManager.GetNewNetID(); // Let the base class (this one) asign a netId
            }
            Target = null;
            CollisionRadius = collisionRadius;
            VisionRadius = visionRadius;
            Waypoints = new List<Vector2>();

            _visibleByTeam = new Dictionary<TeamId, bool>();
            var teams = Enum.GetValues(typeof(TeamId)).Cast<TeamId>();
            foreach (var team in teams)
            {
                _visibleByTeam.Add(team, false);
            }

            Team = TeamId.TEAM_NEUTRAL;
            MovementUpdated = false;
            ToBeRemoved = false;
            AttackerCount = 0;
            IsDashing = false;
        }

        public virtual void OnAdded()
        {
            Game.Map.CollisionHandler.AddObject(this);
        }

        public virtual void OnRemoved()
        {
            Game.Map.CollisionHandler.RemoveObject(this);
        }

        public virtual void OnCollision(GameObject collider) { }

        /// <summary>
        /// Moves the object depending on its target, updating its coordinate.
        /// </summary>
        /// <param name="diff">The amount of milliseconds the object is supposed to move</param>
        public void Move(float diff)
        {
            if (Target == null)
            {
                Direction = new Vector2();
                return;
            }

            var to = new Vector2(Target.X, Target.Y);
            var cur = new Vector2(X, Y); //?

            var goingTo = to - cur;
            Direction = Vector2.Normalize(goingTo);
            if (float.IsNaN(Direction.X) || float.IsNaN(Direction.Y))
            {
                Direction = new Vector2(0, 0);
            }

            // Replaced GetMoveSpeed() with its return value
            // TODO: ???
            var moveSpeed = 0.0f;
            if (IsDashing)
            {
                moveSpeed = DashSpeed;
            }

            var deltaMovement = (moveSpeed) * 0.001f * diff;

            var xx = Direction.X * deltaMovement;
            var yy = Direction.Y * deltaMovement;

            X += xx;
            Y += yy;

            // If the target was a simple point, stop when it is reached

            if (GetDistanceTo(Target) < deltaMovement * 2)
            {
                if (this is Projectile && !Target.IsSimpleTarget)
                {
                    return;
                }

                if (IsDashing)
                {
                    if (this is Unit)
                    {
                        var u = this as Unit;

                        var animList = new List<string>();
                        Game.PacketNotifier.NotifySetAnimation(u, animList);
                    }

                    Target = null;
                }
                else if (++CurWaypoint >= Waypoints.Count)
                {
                    Target = null;
                }
                else
                {
                    Target = new Target(Waypoints[CurWaypoint]);
                }

                if (IsDashing)
                {
                    IsDashing = false;
                }
            }
        }

        public void CalculateVector(float xtarget, float ytarget)
        {
            XVector = xtarget - X;
            YVector = ytarget - Y;

            if (XVector == 0 && YVector == 0)
                return;

            var toDivide = Math.Abs(XVector) + Math.Abs(YVector);
            XVector /= toDivide;
            YVector /= toDivide;
        }

        public virtual void update(float diff)
        {
            Move(diff);
        }

        public void SetWaypoints(List<Vector2> newWaypoints)
        {
            Waypoints = newWaypoints;

            SetPosition(Waypoints[0].X, Waypoints[0].Y);
            MovementUpdated = true;
            if (Waypoints.Count == 1)
            {
                Target = null;
                return;
            }

            Target = new Target(Waypoints[1]);
            CurWaypoint = 1;
        }

        public virtual void SetToBeRemoved()
        {
            ToBeRemoved = true;
        }

        public virtual void SetPosition(float x, float y)
        {
            X = x;
            Y = y;

            Target = null;
        }

        public virtual void SetPosition(Vector2 vec)
        {
            X = vec.X;
            Y = vec.Y;
            Target = null;
        }

        public virtual float GetZ()
        {
            return Game.Map.NavGrid.GetHeightAtLocation(X, Y);
        }

        public bool IsCollidingWith(GameObject o)
        {
            return GetDistanceToSqr(o) < (CollisionRadius + o.CollisionRadius) * (CollisionRadius + o.CollisionRadius);
        }

        public void IncrementAttackerCount()
        {
            ++AttackerCount;
        }
        
        public void DecrementAttackerCount()
        {
            --AttackerCount;
        }

        public bool IsVisibleByTeam(TeamId team)
        {
            return team == Team || _visibleByTeam[team];
        }

        public void SetVisibleByTeam(TeamId team, bool visible)
        {
            _visibleByTeam[team] = visible;
            if (this is Unit)
            {
                Game.PacketNotifier.NotifyUpdatedStats(this as Unit, false);
            }
        }

        public void DashToTarget(Target t, float dashSpeed, float followTargetMaxDistance, float backDistance, float travelTime)
        {
            // TODO: Take into account the rest of the arguments
            IsDashing = true;
            DashSpeed = dashSpeed;
            Target = t;
            Waypoints.Clear();
        }

        public void SetDashingState(bool state) {
            IsDashing = state;
        }
    }
}
