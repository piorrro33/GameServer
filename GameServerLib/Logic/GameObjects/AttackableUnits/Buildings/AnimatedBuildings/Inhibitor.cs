using System;
using LeagueSandbox.GameServer.Logic.Enet;

namespace LeagueSandbox.GameServer.Logic.GameObjects
{
    public class Inhibitor : ObjAnimatedBuilding
    {
        private System.Timers.Timer RespawnTimer;
        private InhibitorState State;
        private const double RESPAWN_TIMER = 5 * 60 * 1000;
        private const double RESPAWN_ANNOUNCE = 1 * 60 * 1000;
        private const float GOLD_WORTH = 50.0f;
        private DateTime TimerStartTime;
        public bool RespawnAnnounced { get; private set; } = true;

        // TODO assists
        public Inhibitor(
            string model,
            TeamId team,
            int collisionRadius = 40,
            float x = 0,
            float y = 0,
            int visionRadius = 0,
            uint netId = 0
        ) : base(model, new BuildingStats(), collisionRadius, x, y, visionRadius, netId)
        {
            Stats.CurrentHealth = 4000;
            Stats.HealthPoints.BaseValue = 4000;
            State = InhibitorState.Alive;
            Team = team;
        }
        public override void OnAdded()
        {
            base.OnAdded();
            Game.ObjectManager.AddInhibitor(this);
        }

        public override void Die(Unit killer)
        {
            var objects = Game.ObjectManager.GetObjects().Values;
            foreach (var obj in objects)
            {
                var u = obj as Unit;
                if (u != null && u.TargetUnit == this)
                {
                    u.SetTargetUnit(null);
                    u.AutoAttackTarget = null;
                    u.IsAttacking = false;
                    Game.PacketNotifier.NotifySetTarget(u, null);
                    u.HasMadeInitialAttack = false;
                }
            }

            if (RespawnTimer != null) //?
                RespawnTimer.Stop();

            RespawnTimer = new System.Timers.Timer(RESPAWN_TIMER) {AutoReset = false};

            RespawnTimer.Elapsed += (a, b) =>
            {
                Stats.CurrentHealth = Stats.HealthPoints.Total;
                setState(InhibitorState.Alive);
                IsDead = false;
            };
            RespawnTimer.Start();
            TimerStartTime = DateTime.Now;

            if (killer != null && killer is Champion)
            {
                var c = (Champion)killer;
                c.Stats.Gold += GOLD_WORTH;
                Game.PacketNotifier.NotifyAddGold(c, this, GOLD_WORTH);
            }

            setState(InhibitorState.Dead, killer);
            RespawnAnnounced = false;

            base.Die(killer);
        }

        public void setState(InhibitorState state, GameObject killer = null)
        {
            if (RespawnTimer != null && state == InhibitorState.Alive)
                RespawnTimer.Stop();

            State = state;
            Game.PacketNotifier.NotifyInhibitorState(this, killer);
        }

        public InhibitorState getState()
        {
            return State;
        }

        public double getRespawnTimer()
        {
            var diff = DateTime.Now - TimerStartTime;
            return RESPAWN_TIMER - diff.TotalMilliseconds;
        }

        public override void update(float diff)
        {
            if (!RespawnAnnounced && getState() == InhibitorState.Dead && getRespawnTimer() <= RESPAWN_ANNOUNCE)
            {
                Game.PacketNotifier.NotifyInhibitorSpawningSoon(this);
                RespawnAnnounced = true;
            }

            base.update(diff);
        }

        public override void RefreshWaypoints()
        {

        }

        public override void SetToBeRemoved()
        {

        }
    }

    public enum InhibitorState : byte
    {
        Dead = 0x00,
        Alive = 0x01
    }
}
