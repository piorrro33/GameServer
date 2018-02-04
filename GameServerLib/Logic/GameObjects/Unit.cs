using LeagueSandbox.GameServer.Core.Logic;
using System;
using System.Collections.Generic;
using System.Numerics;
using LeagueSandbox.GameServer.Logic.Items;
using LeagueSandbox.GameServer.Logic.Content;
using LeagueSandbox.GameServer.Logic.Players;
using LeagueSandbox.GameServer.Logic.Scripting.CSharp;
using LeagueSandbox.GameServer.Logic.API;
using System.Linq;

namespace LeagueSandbox.GameServer.Logic.GameObjects
{
    public enum DamageType : byte
    {
        DAMAGE_TYPE_PHYSICAL = 0x0,
        DAMAGE_TYPE_MAGICAL = 0x1,
        DAMAGE_TYPE_TRUE = 0x2
    }

    public enum DamageText : byte
    {
        DAMAGE_TEXT_INVULNERABLE = 0x0,
        DAMAGE_TEXT_DODGE = 0x2,
        DAMAGE_TEXT_CRITICAL = 0x3,
        DAMAGE_TEXT_NORMAL = 0x4,
        DAMAGE_TEXT_MISS = 0x5
    }

    public enum DamageSource
    {
        DAMAGE_SOURCE_ATTACK,
        DAMAGE_SOURCE_SPELL,
        DAMAGE_SOURCE_SUMMONER_SPELL, // Ignite shouldn't destroy Banshee's
        DAMAGE_SOURCE_PASSIVE // Red/Thornmail shouldn't as well
    }

    public enum AttackType : byte
    {
        ATTACK_TYPE_RADIAL,
        ATTACK_TYPE_MELEE,
        ATTACK_TYPE_TARGETED
    }

    public enum MoveOrder
    {
        MOVE_ORDER_MOVE,
        MOVE_ORDER_ATTACKMOVE
    }

    public enum ShieldType : byte
    {
        GREEN_SHIELD = 0x01,
        MAGIC_SHIELD = 0x02,
        NORMAL_SHIELD = 0x03
    }

    public class Unit : GameObject
    {
        internal const float DETECT_RANGE = 475.0f;
        internal const int EXP_RANGE = 1400;
        internal const long UPDATE_TIME = 500;

        public Stats Stats { get; }
        public InventoryManager Inventory { get; protected set; }
        protected static readonly ItemManager ItemManager = Program.ResolveDependency<ItemManager>();
        protected static readonly PlayerManager PlayerManager = Program.ResolveDependency<PlayerManager>();
        protected static readonly CSharpScriptEngine ScriptEngine = Program.ResolveDependency<CSharpScriptEngine>();

        private static readonly Random Random = new Random();

        public CharData CharData { get; }
        public SpellData AASpellData { get; protected set; }
        public float AutoAttackDelay { get; set; }
        public float AutoAttackProjectileSpeed { get; set; }
        private float _autoAttackCurrentCooldown;
        private float _autoAttackCurrentDelay;
        public bool IsAttacking { protected get; set; }
        public bool IsModelUpdated { get; set; }
        public bool IsMelee { get; set; }
        protected internal bool HasMadeInitialAttack;
        private bool _nextAttackFlag;
        public Unit DistressCause { get; protected set; }
        private float _statUpdateTimer;
        private uint _autoAttackProjId;
        public MoveOrder MoveOrder { get; set; }

        /// <summary>
        /// Unit we want to attack as soon as in range
        /// </summary>
        public AttackableUnit TargetUnit { get; set; }
        public AttackableUnit AutoAttackTarget { get; set; }

        public bool IsDead { get; protected set; }

        private string _model;
        public string Model
        {
            get { return _model; }
            set
            {
                _model = value;
                IsModelUpdated = true;
            }
        }

        private bool _isNextAutoCrit;
        protected readonly Logger Logger = Program.ResolveDependency<Logger>();

        private float _timerUpdate;

        public bool IsCastingSpell { get; set; }

        private List<UnitCrowdControl> CrowdControlList { get; }

        public Unit(
            string model,
            Stats stats,
            int collisionRadius = 40,
            float x = 0,
            float y = 0,
            int visionRadius = 0,
            uint netId = 0
        ) : base(x, y, collisionRadius, visionRadius, netId)

        {
            Stats = stats;
            Model = model;
            CharData = Game.Config.ContentManager.GetCharData(Model);
            Stats.LoadStats(CharData);
            AutoAttackDelay = 0;
            AutoAttackProjectileSpeed = 500;
            IsMelee = CharData.IsMelee;
            Stats.CurrentMana = stats.ManaPoints.Total;
            Stats.CurrentHealth = stats.HealthPoints.Total;
            Stats.AttackSpeedMultiplier.BaseValue = 1.0f;

            if (CharData.PathfindingCollisionRadius > 0)
            {
                CollisionRadius = CharData.PathfindingCollisionRadius;
            }
            else if (collisionRadius > 0)
            {
                CollisionRadius = collisionRadius;
            }
            else
            {
                CollisionRadius = 40;
            }
            
            CrowdControlList = new List<UnitCrowdControl>();
        }

        public override void OnAdded()
        {
            base.OnAdded();
            Game.ObjectManager.AddVisionUnit(this);
        }

        public override void OnRemoved()
        {
            base.OnRemoved();
            Game.ObjectManager.RemoveVisionUnit(this);
        }

        public void ApplyCrowdControl(UnitCrowdControl cc)
        {
            if (cc.IsTypeOf(CrowdControlType.Stun) || cc.IsTypeOf(CrowdControlType.Root))
            {
                this.StopMovement();
            }
            CrowdControlList.Add(cc);
        }
        public void RemoveCrowdControl(UnitCrowdControl cc)
        {
            CrowdControlList.Remove(cc);
        }
        
        public void ClearAllCrowdControl()
        {
            CrowdControlList.Clear();
        }
        
        public bool HasCrowdControl(CrowdControlType ccType)
        {
            return CrowdControlList.FirstOrDefault((cc)=>cc.IsTypeOf(ccType)) != null;
        }
        
        public void AddStatModifier(ChampionStatModifier statModifier)
        {
            Stats.AddModifier(statModifier);
        }

        public void UpdateStatModifier(ChampionStatModifier statModifier)
        {
            Stats.UpdateModifier(statModifier);
        }

        public void RemoveStatModifier(ChampionStatModifier statModifier)
        {
            Stats.RemoveModifier(statModifier);
        }
        
        public void StopMovement()
        {
            SetWaypoints(new List<Vector2> { this.GetPosition(), this.GetPosition() });
        }

        public override void update(float diff)
        {
            _timerUpdate += diff;
            if (_timerUpdate >= UPDATE_TIME)
            {
                _timerUpdate = 0;
            }

            foreach(UnitCrowdControl cc in CrowdControlList)
            {
                cc.Update(diff);
            }
            CrowdControlList.RemoveAll((cc)=>cc.IsDead());

            var onUpdate = ScriptEngine.GetStaticMethod<Action<Unit, double>>(Model, "Passive", "OnUpdate");
            onUpdate?.Invoke(this, diff);

            UpdateAutoAttackTarget(diff);

            base.update(diff);

            _statUpdateTimer += diff;
            if (_statUpdateTimer >= 500)
            { // update Stats (hpregen, manaregen) every 0.5 seconds
                Stats.update(_statUpdateTimer);
                _statUpdateTimer = 0;
            }
        }

        public void UpdateAutoAttackTarget(float diff)
        {
            if (HasCrowdControl(CrowdControlType.Disarm) || HasCrowdControl(CrowdControlType.Stun))
            {
                return;
            }
            if (IsDead)
            {
                if (TargetUnit != null)
                {
                    SetTargetUnit(null);
                    AutoAttackTarget = null;
                    IsAttacking = false;
                    Game.PacketNotifier.NotifySetTarget(this, null);
                    HasMadeInitialAttack = false;
                }
                return;
            }

            if (TargetUnit != null)
            {
                if (TargetUnit.IsDead || !Game.ObjectManager.TeamHasVisionOn(Team, TargetUnit))
                {
                    SetTargetUnit(null);
                    IsAttacking = false;
                    Game.PacketNotifier.NotifySetTarget(this, null);
                    HasMadeInitialAttack = false;

                }
                else if (IsAttacking && AutoAttackTarget != null)
                {
                    _autoAttackCurrentDelay += diff / 1000.0f;
                    if (_autoAttackCurrentDelay >= AutoAttackDelay / Stats.AttackSpeedMultiplier.Total)
                    {
                        if (!IsMelee)
                        {
                            var p = new Projectile(
                                X,
                                Y,
                                5,
                                this,
                                AutoAttackTarget,
                                null,
                                AutoAttackProjectileSpeed,
                                "",
                                0,
                                _autoAttackProjId
                            );
                            Game.ObjectManager.AddObject(p);
                            Game.PacketNotifier.NotifyShowProjectile(p);
                        }
                        else
                        {
                            AutoAttackHit(AutoAttackTarget);
                        }
                        _autoAttackCurrentCooldown = 1.0f / (Stats.GetTotalAttackSpeed());
                        IsAttacking = false;
                    }

                }
                else if (GetDistanceTo(TargetUnit) <= Stats.Range.Total)
                {
                    RefreshWaypoints();
                    _isNextAutoCrit = Random.Next(0, 100) < Stats.CriticalChance.Total * 100;
                    if (_autoAttackCurrentCooldown <= 0)
                    {
                        IsAttacking = true;
                        _autoAttackCurrentDelay = 0;
                        _autoAttackProjId = NetworkIdManager.GetNewNetID();
                        AutoAttackTarget = TargetUnit;

                        if (!HasMadeInitialAttack)
                        {
                            HasMadeInitialAttack = true;
                            Game.PacketNotifier.NotifyBeginAutoAttack(
                                this,
                                TargetUnit,
                                _autoAttackProjId,
                                _isNextAutoCrit
                            );
                        }
                        else
                        {
                            _nextAttackFlag = !_nextAttackFlag; // The first auto attack frame has occurred
                            Game.PacketNotifier.NotifyNextAutoAttack(
                                this,
                                TargetUnit,
                                _autoAttackProjId,
                                _isNextAutoCrit,
                                _nextAttackFlag
                                );
                        }

                        var attackType = IsMelee ? AttackType.ATTACK_TYPE_MELEE : AttackType.ATTACK_TYPE_TARGETED;
                        Game.PacketNotifier.NotifyOnAttack(this, TargetUnit, attackType);
                    }

                }
                else
                {
                    RefreshWaypoints();
                }

            }
            else if (IsAttacking)
            {
                if (AutoAttackTarget == null
                    || AutoAttackTarget.IsDead
                    || !Game.ObjectManager.TeamHasVisionOn(Team, AutoAttackTarget)
                )
                {
                    IsAttacking = false;
                    HasMadeInitialAttack = false;
                    AutoAttackTarget = null;
                }
            }

            if (_autoAttackCurrentCooldown > 0)
            {
                _autoAttackCurrentCooldown -= diff / 1000.0f;
            }
        }

        public override void OnCollision(GameObject collider)
        {
            base.OnCollision(collider);
            if (collider == null)
            {
                var onCollideWithTerrain = ScriptEngine.GetStaticMethod<Action<Unit>>(Model, "Passive", "onCollideWithTerrain");
                onCollideWithTerrain?.Invoke(this);
            }
            else
            {
                var onCollide = ScriptEngine.GetStaticMethod<Action<Unit, Unit>>(Model, "Passive", "onCollide");
                onCollide?.Invoke(this, collider as Unit);
            }
        }

        /// <summary>
        /// This is called by the AA projectile when it hits its target
        /// </summary>
        public virtual void AutoAttackHit(AttackableUnit target)
        {
            if (HasCrowdControl(CrowdControlType.Blind)) {
                target.TakeDamage(this, 0, DamageType.DAMAGE_TYPE_PHYSICAL,
                                             DamageSource.DAMAGE_SOURCE_ATTACK,
                                             DamageText.DAMAGE_TEXT_MISS);
                return;
            }

            var damage = Stats.AttackDamage.Total;
            if (_isNextAutoCrit)
            {
                damage *= Stats.getCritDamagePct();
            }

            var onAutoAttack = ScriptEngine.GetStaticMethod<Action<Unit, Unit>>(Model, "Passive", "OnAutoAttack");
            onAutoAttack?.Invoke(this, target);

            target.TakeDamage(this, damage, DamageType.DAMAGE_TYPE_PHYSICAL,
                DamageSource.DAMAGE_SOURCE_ATTACK,
                _isNextAutoCrit);
        }
        
        public virtual void Die(Unit killer)
        {
            SetToBeRemoved();
            Game.ObjectManager.StopTargeting(this);

            Game.PacketNotifier.NotifyNpcDie(this, killer);

            var onDie = ScriptEngine.GetStaticMethod<Action<Unit, Unit>>(Model, "Passive", "OnDie");
            onDie?.Invoke(this, killer);

            var exp = Game.Map.MapGameScript.GetExperienceFor(this);
            var champs = Game.ObjectManager.GetChampionsInRange(this, EXP_RANGE, true);
            //Cull allied champions
            champs.RemoveAll(l => l.Team == Team);

            if (champs.Count > 0)
            {
                float expPerChamp = exp / champs.Count;
                foreach (var c in champs)
                {
                    c.Stats.Experience += expPerChamp;
                    Game.PacketNotifier.NotifyAddXP(c, expPerChamp);
                }
            }

            if (killer != null)
            {
                var cKiller = killer as Champion;

                if (cKiller == null)
                    return;

                var gold = Game.Map.MapGameScript.GetGoldFor(this);
                if (gold <= 0)
                {
                    return;
                }

                cKiller.Stats.Gold += gold;
                Game.PacketNotifier.NotifyAddGold(cKiller, this, gold);

                if (cKiller.KillDeathCounter < 0)
                {
                    cKiller.ChampionGoldFromMinions += gold;
                    Logger.LogCoreInfo($"Adding gold form minions to reduce death spree: {cKiller.ChampionGoldFromMinions}");
                }

                if (cKiller.ChampionGoldFromMinions >= 50 && cKiller.KillDeathCounter < 0)
                {
                    cKiller.ChampionGoldFromMinions = 0;
                    cKiller.KillDeathCounter += 1;
                }
            }

            if (IsDashing)
            {
                IsDashing = false;
            }
        }

        public virtual bool IsInDistress()
        {
            return false; //return DistressCause;
        }

        public void SetTargetUnit(AttackableUnit target)
        {
            if (target == null) // If we are unsetting the target (moving around)
            {
                if (TargetUnit != null) // and we had a target
                    TargetUnit.DistressCause = null; // Unset the distress call
                                                      // TODO: Replace this with a delay?

                IsAttacking = false;
            }
            else
            {
                target.DistressCause = this; // Otherwise set the distress call
            }

            TargetUnit = target;
            RefreshWaypoints();
        }

        public virtual void RefreshWaypoints()
        {
            if (TargetUnit == null || (GetDistanceTo(TargetUnit) <= Stats.Range.Total && Waypoints.Count == 1))
                return;

            if (GetDistanceTo(TargetUnit) <= Stats.Range.Total - 2.0f)
            {
                SetWaypoints(new List<Vector2> { new Vector2(X, Y) });
            }
            else
            {
                var t = new Target(Waypoints[Waypoints.Count - 1]);
                if (t.GetDistanceTo(TargetUnit) >= 25.0f)
                {
                    SetWaypoints(new List<Vector2> { new Vector2(X, Y), new Vector2(TargetUnit.X, TargetUnit.Y) });
                }
            }
        }

        public static ClassifyUnit ClassifyTarget(Unit target)
        {
            if (target.TargetUnit != null && target.TargetUnit.IsInDistress()) // If an ally is in distress, target this unit. (Priority 1~5)
            {
                if (target is Champion && target.TargetUnit is Champion) // If it's a champion attacking an allied champion
                {
                    return ClassifyUnit.CHAMPION_ATTACKING_CHAMPION;
                }

                if (target is Minion && target.TargetUnit is Champion) // If it's a minion attacking an allied champion.
                {
                    return ClassifyUnit.MINION_ATTACKING_CHAMPION;
                }

                if (target is Minion && target.TargetUnit is Minion) // Minion attacking minion
                {
                    return ClassifyUnit.MINION_ATTACKING_MINION;
                }

                if (target is BaseTurret && target.TargetUnit is Minion) // Turret attacking minion
                {
                    return ClassifyUnit.TURRET_ATTACKING_MINION;
                }

                if (target is Champion && target.TargetUnit is Minion) // Champion attacking minion
                {
                    return ClassifyUnit.CHAMPION_ATTACKING_MINION;
                }
            }

            var p = target as Placeable;
            if (p != null)
            {
                return ClassifyUnit.PLACEABLE;
            }

            var m = target as Minion;
            if (m != null)
            {
                switch (m.getType())
                {
                    case MinionSpawnType.MINION_TYPE_MELEE:
                        return ClassifyUnit.MELEE_MINION;
                    case MinionSpawnType.MINION_TYPE_CASTER:
                        return ClassifyUnit.CASTER_MINION;
                    case MinionSpawnType.MINION_TYPE_CANNON:
                    case MinionSpawnType.MINION_TYPE_SUPER:
                        return ClassifyUnit.SUPER_OR_CANNON_MINION;
                }
            }

            if (target is BaseTurret)
            {
                return ClassifyUnit.TURRET;
            }

            if (target is Champion)
            {
                return ClassifyUnit.CHAMPION;
            }

            if (target is Inhibitor && !target.IsDead)
            {
                return ClassifyUnit.INHIBITOR;
            }

            if (target is Nexus)
            {
                return ClassifyUnit.NEXUS;
            }

            return ClassifyUnit.DEFAULT;
        }
    }

    public enum UnitAnnounces : byte
    {
        DEATH = 0x04,
        INHIBITOR_DESTROYED = 0x1F,
        INHIBITOR_ABOUT_TO_SPAWN = 0x20,
        INHIBITOR_SPAWNED = 0x21,
        TURRET_DESTROYED = 0x24,
        SUMMONER_DISCONNECTED = 0x47,
        SUMMONER_RECONNECTED = 0x48
    }

    public enum ClassifyUnit
    {
        CHAMPION_ATTACKING_CHAMPION = 1,
        MINION_ATTACKING_CHAMPION = 2,
        MINION_ATTACKING_MINION = 3,
        TURRET_ATTACKING_MINION = 4,
        CHAMPION_ATTACKING_MINION = 5,
        PLACEABLE = 6,
        MELEE_MINION = 7,
        CASTER_MINION = 8,
        SUPER_OR_CANNON_MINION = 9,
        TURRET = 10,
        CHAMPION = 11,
        INHIBITOR = 12,
        NEXUS = 13,
        DEFAULT = 14
    }
}
