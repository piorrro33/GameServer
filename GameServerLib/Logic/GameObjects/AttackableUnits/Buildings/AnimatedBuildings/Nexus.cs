using LeagueSandbox.GameServer.Logic.Enet;

namespace LeagueSandbox.GameServer.Logic.GameObjects
{
    public class Nexus : ObjAnimatedBuilding
    {
        public Nexus(
            string model,
            TeamId team,
            int collisionRadius = 40,
            float x = 0,
            float y = 0,
            int visionRadius = 0,
            uint netId = 0
        ) : base(model, new BuildingStats(), collisionRadius, x, y, visionRadius, netId)
        {
            Stats.CurrentHealth = 5500;
            Stats.HealthPoints.BaseValue = 5500;

            Team = team;
        }

        public override void Die(Unit killer)
        {
            Game.Stop();
            Game.PacketNotifier.NotifyGameEnd(this);
        }

        public override void RefreshWaypoints()
        {

        }

        public override void SetToBeRemoved()
        {

        }
    }
}
