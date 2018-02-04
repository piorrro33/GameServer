using Newtonsoft.Json.Linq;

namespace LeagueSandbox.GameServer.Logic.GameObjects
{
    public class Placeable : Unit
    {
        public string Name { get; private set; }
        public Unit Owner { get; private set; } // We'll probably want to change this in the future

        public Placeable(
            Unit owner,
            float x,
            float y,
            string model,
            string name,
            uint netId = 0
        ) : base(model, new Stats(), 40, x, y, 0, netId)
        {
            Team = owner.Team;

            Owner = owner;

            SetVisibleByTeam(Team, true);

            MoveOrder = MoveOrder.MOVE_ORDER_MOVE;

            Name = name;
        }

        public override void OnAdded()
        {
            base.OnAdded();
            Game.PacketNotifier.NotifySpawn(this);
        }

        public override bool IsInDistress()
        {
            return DistressCause != null;
        }
    }
}
