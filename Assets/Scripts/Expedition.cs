using System;
using System.Collections.Generic;

namespace Frostline
{
    public enum CardAction { Deploy, Strike, Freeze, Heal, Hasten, ApplyEffect }
    public enum BattlePhase { Planning, Resolving, Won, Lost }
    public enum BattleEffect { Windup, Attack, Ice, Recovery, Arrival, Guard, Status, Burst, Retaliation, Burning }
    public enum AttackPattern { Front, Barrage, Longshot, Aimless }
    public enum EffectTrigger { Deploy, Hit, Kill, WhenHit }
    public enum EffectKind { None, Heal, Shell, Snow, Spice, Overload, Ink, Weakness, Block, EffectBonus, Thorns, Burn, AttackGrowth }
    public enum Pendant { None, WarmStone, TuningRing }

    public static class Pendants
    {
        public static int Health(Pendant pendant) => pendant == Pendant.WarmStone ? 2 : 0;
        public static int Effect(Pendant pendant) => pendant == Pendant.TuningRing ? 1 : 0;

        public static string Name(Pendant pendant) => pendant switch
        {
            Pendant.WarmStone => "暖石挂坠",
            Pendant.TuningRing => "调律挂坠",
            _ => ""
        };

        public static string Description(Pendant pendant) => pendant switch
        {
            Pendant.WarmStone => $"最大生命 +{Health(pendant)}",
            Pendant.TuningRing => $"触发能力数值 +{Effect(pendant)}",
            _ => ""
        };
    }

    public readonly struct UnitAbility
    {
        public readonly EffectTrigger Trigger;
        public readonly EffectKind Effect;
        public readonly int Amount;

        public UnitAbility(EffectTrigger trigger, EffectKind effect, int amount) =>
            (Trigger, Effect, Amount) = (trigger, effect, amount);

        public string Timing => Trigger switch
        {
            EffectTrigger.Deploy => "部署",
            EffectTrigger.Hit => "命中",
            EffectTrigger.Kill => "击倒",
            EffectTrigger.WhenHit => "受击",
            _ => ""
        };

        public string Describe(int bonus)
        {
            string effect = Effect == EffectKind.Heal ? "回血" : Effects.Name(Effect);
            string target = Trigger == EffectTrigger.WhenHit ? " · 给攻击者" : " · ";
            return $"{Timing}{target}{effect} +{Amount + bonus}";
        }
    }

    public static class Effects
    {
        public static string Name(EffectKind effect) => effect switch
        {
            EffectKind.Heal => "生命",
            EffectKind.Shell => "护壳",
            EffectKind.Snow => "雪球",
            EffectKind.Spice => "辣椒",
            EffectKind.Overload => "过载",
            EffectKind.Ink => "墨水",
            EffectKind.Weakness => "虚弱",
            EffectKind.Block => "格挡",
            EffectKind.EffectBonus => "效果",
            EffectKind.Thorns => "反伤",
            EffectKind.Burn => "灼烧",
            EffectKind.AttackGrowth => "基础攻击",
            _ => ""
        };

        public static string Rule(EffectKind effect) => effect switch
        {
            EffectKind.Spice => "每层使攻击 +1，打完整次攻击后清除。",
            EffectKind.Overload => "层数达到当前生命时引爆，对自身和同排队友造成等量伤害。",
            EffectKind.Ink => "停用触发能力，每回合结束减少 1 层。",
            EffectKind.Weakness => "每层使攻击 −1（最低为 0），打完整次攻击后清除。",
            EffectKind.Block => "每层抵消一次伤害，先于护壳消耗。",
            EffectKind.EffectBonus => "本场战斗提升触发能力的数值，撤回后保留。",
            EffectKind.Thorns => "承受一次有伤害的单位攻击后，若仍存活，对攻击者造成等同层数的反伤，再减少 1 层。",
            EffectKind.Burn => "回合末受到等同层数的伤害，再减少 1 层。",
            _ => ""
        };
    }

    public sealed class UnitType
    {
        public readonly string Name, Art;
        public readonly int Power, Health, Period, Frenzy;
        public readonly AttackPattern Pattern;
        public readonly UnitAbility Ability;
        public readonly bool ResistSnow;

        public UnitType(string name, string art, int power, int health, int period,
            AttackPattern pattern = AttackPattern.Front, int frenzy = 1,
            UnitAbility ability = default, bool resistSnow = false)
        {
            (Name, Art, Power, Health, Period) = (name, art, power, health, period);
            (Pattern, Frenzy, Ability, ResistSnow) = (pattern, frenzy, ability, resistSnow);
        }
    }

    // Shared by one recruited card and its deployments for the lifetime of the expedition.
    internal sealed class UnitProgress
    {
        public int Attack { get; private set; }

        public UnitProgress(int attack) => Attack = attack;

        public void IncreaseAttack(int amount) => Attack += amount;
    }

    public sealed class Unit
    {
        public readonly UnitType Type;
        public readonly bool Leader;
        public readonly Pendant Pendant;
        public int Health, Counter, Frozen, Shell;
        public int Spice, Overload, Ink, Weakness, Block, EffectBonus;
        public int Thorns, Burn;
        internal UnitProgress Progress { get; private set; }
        public int BaseAttack => Progress.Attack;
        public int Attack => Math.Max(0, BaseAttack + Spice - Weakness);
        public int MaxHealth => Type.Health + Pendants.Health(Pendant);
        public int AbilityBonus => EffectBonus + Pendants.Effect(Pendant);

        public Unit(UnitType type, bool leader = false, Pendant pendant = Pendant.None)
            : this(type, leader, pendant, new UnitProgress(type.Power)) { }

        internal Unit(UnitType type, bool leader, Pendant pendant, UnitProgress progress)
        {
            Type = type;
            Leader = leader;
            Pendant = pendant;
            Progress = progress;
            Health = MaxHealth;
            Counter = type.Period;
        }

        public Unit Copy()
        {
            var copy = (Unit)MemberwiseClone();
            copy.Progress = new UnitProgress(BaseAttack);
            return copy;
        }
    }

    public sealed class Card
    {
        public readonly string Name, Art;
        public readonly CardAction Action;
        public readonly int Amount, Damage, BonusDamage;
        public readonly EffectKind Effect, BonusAgainst;
        public readonly bool TargetsEnemy;
        public int DeploymentHealth { get; }
        public int DeploymentEffectBonus { get; }
        public int DeploymentAttack => progress?.Attack ?? Unit?.Power ?? 0;
        public readonly UnitType Unit;
        public readonly Pendant Pendant;
        private readonly UnitProgress progress;

        public string Description => Action switch
        {
            CardAction.Deploy => "部署到己方空位。",
            CardAction.Strike => BonusDamage > 0
                ? $"造成 {Damage} 点伤害。目标有{Effects.Name(BonusAgainst)}时，伤害 +{BonusDamage}。"
                : $"造成 {Damage} 点伤害。",
            CardAction.Freeze => $"造成 {Damage} 点伤害，施加 {Amount} 层雪球。",
            CardAction.Heal => $"全体友方恢复 {Amount} 点生命并清除雪球。",
            CardAction.Hasten => $"倒计时减少 {Amount}，归零立刻行动。",
            CardAction.ApplyEffect when Effect == EffectKind.EffectBonus => $"使一名友方的触发能力数值 +{Amount}。",
            CardAction.ApplyEffect when Effect == EffectKind.AttackGrowth => $"一名友方在本次远征中永久获得 {Amount} 点基础攻击。",
            CardAction.ApplyEffect => $"给{(TargetsEnemy ? "敌方" : "友方")}施加 {Effects.Name(Effect)} +{Amount}。",
            _ => ""
        };

        public Card(UnitType unit, int? deploymentHealth = null, int effectBonus = 0, Pendant pendant = Pendant.None)
            : this(unit, deploymentHealth, effectBonus, pendant, null) { }

        private Card(UnitType unit, int? deploymentHealth, int effectBonus, Pendant pendant, UnitProgress progress)
            : this(unit.Name, unit.Art, CardAction.Deploy, 0)
        {
            Unit = unit;
            Pendant = pendant;
            DeploymentHealth = deploymentHealth ?? unit.Health + Pendants.Health(pendant);
            DeploymentEffectBonus = effectBonus;
            this.progress = progress;
        }

        public Card(string name, string art, CardAction action, int amount, int damage = 0,
            EffectKind effect = EffectKind.None, bool targetsEnemy = false, EffectKind bonusAgainst = EffectKind.None, int bonusDamage = 0)
        {
            (Name, Art, Action, Amount, Damage) = (name, art, action, amount, damage);
            (Effect, TargetsEnemy) = (effect, targetsEnemy);
            (BonusAgainst, BonusDamage) = (bonusAgainst, bonusDamage);
        }

        public int DamageAgainst(Unit target) => Damage + (BonusAgainst switch
        {
            EffectKind.Snow when target.Frozen > 0 => BonusDamage,
            EffectKind.Overload when target.Overload > 0 => BonusDamage,
            _ => 0
        });

        internal Card Recruit() => Unit == null ? this
            : new Card(Unit, null, 0, Pendant, new UnitProgress(Unit.Power));

        internal Card Equip(Pendant pendant) => new Card(Unit, null, 0, pendant, progress);

        internal static Card FromRecall(Unit unit, int health) =>
            new Card(unit.Type, health, unit.EffectBonus, unit.Pendant, unit.Progress);

        internal Unit Deploy(bool leader) => CreateUnit(leader, progress ?? new UnitProgress(DeploymentAttack));

        internal Unit PreviewDeployment(bool leader) => CreateUnit(leader, new UnitProgress(DeploymentAttack));

        internal Card CopyForForecast() => Unit == null ? this
            : new Card(Unit, DeploymentHealth, DeploymentEffectBonus, Pendant, new UnitProgress(DeploymentAttack));

        private Unit CreateUnit(bool leader, UnitProgress state) => new(Unit, leader, Pendant, state)
        {
            Health = DeploymentHealth,
            EffectBonus = DeploymentEffectBonus
        };
    }

    public readonly struct Position : IEquatable<Position>
    {
        public readonly bool Enemy;
        public readonly int Row, Slot;

        public Position(bool enemy, int row, int slot)
        {
            (Enemy, Row, Slot) = (enemy, row, slot);
        }

        public bool Valid => Row >= 0 && Row < Expedition.Rows && Slot >= 0 && Slot < Expedition.Slots;
        public bool Equals(Position other) => Enemy == other.Enemy && Row == other.Row && Slot == other.Slot;
        public override bool Equals(object other) => other is Position p && Equals(p);
        public override int GetHashCode() => (Enemy, Row, Slot).GetHashCode();
    }

    public readonly struct BattleBeat
    {
        public readonly Position Source, Target;
        public readonly BattleEffect Effect;
        public readonly string Mark;
        public readonly IReadOnlyList<Position> Targets;
        public readonly bool CardStrike;
        public readonly IReadOnlyList<string> ImpactMarks;

        public BattleBeat(Position source, Position target, BattleEffect effect, string mark,
            IReadOnlyList<Position> targets = null, bool cardStrike = false, IReadOnlyList<string> impactMarks = null)
        {
            (Source, Target, Effect, Mark) = (source, target, effect, mark);
            Targets = targets ?? new[] { target };
            CardStrike = cardStrike;
            ImpactMarks = impactMarks;
        }
    }

    public readonly struct EnemySpawn
    {
        public readonly UnitType Unit;
        public readonly int Row;

        public EnemySpawn(UnitType unit, int row) => (Unit, Row) = (unit, row);
    }

    public sealed class Encounter
    {
        public readonly string Name, Landscape, Briefing;
        public readonly int WaveInterval;
        public readonly Pendant RewardPendant;
        public readonly IReadOnlyList<Card> RewardCards;
        private readonly IReadOnlyList<EnemySpawn>[] waves;
        public int WaveCount => waves.Length;

        public Encounter(string name, string landscape, string briefing, int waveInterval, Pendant pendant,
            Card[] rewardCards, EnemySpawn[][] waves)
        {
            Name = name;
            Landscape = landscape;
            Briefing = briefing;
            WaveInterval = waveInterval;
            RewardPendant = pendant;
            RewardCards = Array.AsReadOnly(rewardCards);
            this.waves = Array.ConvertAll(waves, wave => (IReadOnlyList<EnemySpawn>)Array.AsReadOnly(wave));
        }

        public IReadOnlyList<EnemySpawn> UnitsInWave(int wave) => waves[wave - 1];
    }

    public sealed class Expedition
    {
        public const int Rows = 2;
        public const int Slots = 3;
        public const int HandSize = 6;
        public const int RefreshPeriod = 4;
        private const int RecallHealing = 5;

        private Unit[,,] board = new Unit[2, Rows, Slots];
        private uint targetRandom = 0x890ca3a1;
        private readonly Random random = new(2471);
        private readonly List<Card> draw;
        private readonly Card captainCard;
        private List<Card> discard = new();
        private List<Card> hand = new();

        public BattlePhase Phase { get; private set; }
        public int Turn { get; private set; } = 1;
        public int Wave { get; private set; } = 1;
        public int Reinforcements { get; private set; }
        public int Refresh { get; private set; } = RefreshPeriod;
        public bool Opening { get; private set; } = true;
        public string Notice { get; private set; } = "部署队长，随后抽取开局手牌。";

        public Encounter Location { get; }
        public UnitType Captain => captainCard.Unit;
        public UnitType Companion { get; }
        public IReadOnlyList<Card> Hand => hand;
        public IReadOnlyList<Card> DrawPile => draw.AsReadOnly();
        public IReadOnlyList<Card> DiscardPile => discard.AsReadOnly();
        public int DrawCount => draw.Count;
        public int DiscardCount => discard.Count;
        public IReadOnlyList<EnemySpawn> NextWave =>
            Wave < Location.WaveCount ? Location.UnitsInWave(Wave + 1) : Array.Empty<EnemySpawn>();

        public Expedition(Encounter location, IEnumerable<Card> deck, UnitType captain, UnitType companion = null)
            : this(location, deck, new Card(captain).Recruit(), companion == null ? null : new Card(companion).Recruit()) { }

        internal Expedition(Encounter location, IEnumerable<Card> deck, Card captain, Card companion)
        {
            Location = location;
            Reinforcements = location.WaveInterval;
            draw = new List<Card>(deck);
            captainCard = captain;
            hand.Add(captainCard);
            Companion = companion?.Unit;
            if (Companion != null)
            {
                hand.Add(companion);
                Notice = "免费部署队长和随从，随后抽取开局手牌。";
            }
            foreach (EnemySpawn enemy in location.UnitsInWave(1))
                AddEnemy(enemy.Row, enemy.Unit);
        }

        public Unit At(Position pos) => pos.Valid ? board[pos.Enemy ? 1 : 0, pos.Row, pos.Slot] : null;

        public bool IsCaptainCard(Card card) => ReferenceEquals(card, captainCard);

        public bool CanPlay(int index, Position pos)
        {
            if (Phase != BattlePhase.Planning || index < 0 || index >= hand.Count || !pos.Valid)
                return false;

            Unit unit = At(pos);
            Card card = hand[index];
            return card.Action switch
            {
                CardAction.Deploy => !pos.Enemy && unit == null,
                CardAction.Strike or CardAction.Freeze => pos.Enemy && unit != null,
                CardAction.Heal => !pos.Enemy && unit != null,
                CardAction.Hasten => !pos.Enemy && unit != null && unit.Counter > 0 && unit.Frozen == 0,
                CardAction.ApplyEffect when card.Effect == EffectKind.AttackGrowth => !pos.Enemy && unit != null,
                CardAction.ApplyEffect => pos.Enemy == card.TargetsEnemy && unit != null &&
                    (card.Effect != EffectKind.EffectBonus || unit.Type.Ability.Effect != EffectKind.None),
                _ => false
            };
        }

        // Return every possible target for Aimless. The real attack rolls once per hit.
        public IReadOnlyList<Position> AttackTargets(Position source, UnitType type)
        {
            var targets = new List<Position>(Slots);
            int row = Front(!source.Enemy, source.Row).HasValue ? source.Row : 1 - source.Row;
            for (int slot = 0; slot < Slots; slot++)
            {
                var pos = new Position(!source.Enemy, row, slot);
                if (At(pos) != null)
                    targets.Add(pos);
            }

            if (targets.Count <= 1 || type.Pattern is AttackPattern.Barrage or AttackPattern.Aimless)
                return targets;

            int index = type.Pattern == AttackPattern.Longshot ? targets.Count - 1 : 0;
            return new[] { targets[index] };
        }

        public IReadOnlyList<Position> CardTargets(int index, Position anchor)
        {
            if (!CanPlay(index, anchor))
                return Array.Empty<Position>();

            Card card = hand[index];
            if (card.Action == CardAction.Heal)
            {
                var targets = new List<Position>();
                foreach (Position pos in TurnOrder())
                {
                    if (!pos.Enemy && At(pos) is Unit unit && (unit.Health < unit.MaxHealth || unit.Frozen > 0))
                        targets.Add(pos);
                }
                return targets;
            }
            if (card.Effect == EffectKind.Overload && At(anchor).Overload + card.Amount >= At(anchor).Health)
                return OccupiedRow(anchor);
            return new[] { anchor };
        }

        public (IReadOnlyList<Position> Actions, IReadOnlyList<(Position Position, UnitType Unit)> Arrivals) ForecastTurn(
            int cardIndex, Position? target)
        {
            var actions = new List<Position>();
            var arrivals = new List<(Position, UnitType)>();
            if (Opening || Phase != BattlePhase.Planning)
                return (actions, arrivals);

            // After opening deployment, playing a card does not touch the draw pile or shuffle RNG.
            // Aimless uses a value-type RNG state, so forecasting does not consume a live roll.
            var forecast = (Expedition)MemberwiseClone();
            forecast.board = new Unit[2, Rows, Slots];
            foreach (Position pos in TurnOrder())
                forecast.Set(pos, At(pos)?.Copy());

            IEnumerable<BattleBeat> steps;
            if (target.HasValue && CanPlay(cardIndex, target.Value))
            {
                forecast.hand = hand.ConvertAll(card => card.CopyForForecast());
                forecast.discard = new List<Card>(discard);
                steps = forecast.Play(cardIndex, target.Value);
            }
            else
                steps = forecast.Resolve();

            foreach (BattleBeat step in steps)
            {
                if (step.Effect == BattleEffect.Windup && !step.CardStrike && !actions.Contains(step.Source))
                    actions.Add(step.Source);
                if (step.Effect == BattleEffect.Arrival && step.Target.Enemy)
                    arrivals.Add((step.Target, forecast.At(step.Target).Type));
            }
            return (actions, arrivals);
        }

        private static IEnumerable<Position> TurnOrder()
        {
            for (int side = 1; side >= 0; side--)
            for (int row = 0; row < Rows; row++)
            for (int slot = 0; slot < Slots; slot++)
                yield return new Position(side == 1, row, slot);
        }

        public bool CanMove(Position source, Position target) =>
            Phase == BattlePhase.Planning && !source.Enemy && !target.Enemy && target.Valid &&
            !source.Equals(target) && At(source) != null;

        public bool CanRedraw => !Opening && Phase == BattlePhase.Planning && hand.Count + draw.Count + discard.Count > 0;

        public bool CanRecall(Position source) =>
            !Opening && Phase == BattlePhase.Planning && !source.Enemy && At(source) is Unit unit && !unit.Leader;

        public bool Move(Position source, Position target)
        {
            if (!CanMove(source, target))
                return false;

            Unit moving = At(source);
            Set(source, At(target));
            Set(target, moving);
            Notice = $"{moving.Type.Name}已换位，未消耗回合。";
            return true;
        }

        public bool Recall(Position source)
        {
            if (!CanRecall(source))
                return false;

            Unit unit = At(source);
            int health = Math.Min(unit.MaxHealth, unit.Health + RecallHealing);
            discard.Add(Card.FromRecall(unit, health));
            Set(source, null);
            Notice = $"{unit.Type.Name}撤回，恢复至 {health} 点生命。";
            return true;
        }

        public IEnumerable<BattleBeat> Play(int index, Position target)
        {
            if (!CanPlay(index, target))
                yield break;

            Phase = BattlePhase.Resolving;
            Card card = hand[index];
            hand.RemoveAt(index);
            Unit unit = At(target);
            switch (card.Action)
            {
                case CardAction.Deploy:
                    var deployed = card.Deploy(IsCaptainCard(card));
                    Set(target, deployed);
                    Notice = $"{card.Name}上场。";
                    yield return new BattleBeat(target, target, BattleEffect.Arrival, "部署");
                    foreach (BattleBeat effect in TriggerAbility(target, target, deployed, EffectTrigger.Deploy))
                        yield return effect;
                    if (Opening)
                    {
                        if (hand.Count == 0)
                        {
                            Opening = false;
                            FillHand();
                            Notice = "出牌或提前敲换牌铃，推进一回合。";
                        }
                        else
                            Notice = $"部署{hand[0].Name}后抽取开局手牌。";
                        Phase = BattlePhase.Planning;
                        yield break;
                    }
                    break;
                case CardAction.Strike:
                case CardAction.Freeze:
                    Notice = $"{card.Name}准备命中{unit.Type.Name}。";
                    yield return new BattleBeat(target, target, BattleEffect.Windup, card.Name, cardStrike: true);
                    yield return Hit(target, target, card.DamageAgainst(unit), card.Name);
                    foreach (BattleBeat burst in CheckOverload(target))
                        yield return burst;
                    if (card.Action == CardAction.Freeze && At(target) != null)
                    {
                        foreach (BattleBeat effect in ApplyEffect(target, target, EffectKind.Snow, card.Amount))
                            yield return effect;
                    }
                    break;
                case CardAction.Heal:
                    foreach (BattleBeat recovery in HealAll(card.Amount))
                        yield return recovery;
                    break;
                case CardAction.Hasten:
                    unit.Counter = Math.Max(0, unit.Counter - card.Amount);
                    Notice = $"{unit.Type.Name}的倒计时减少 {card.Amount}。";
                    yield return new BattleBeat(target, target, BattleEffect.Recovery, "加速");
                    if (unit.Counter == 0)
                    {
                        foreach (BattleBeat attack in Trigger(target, unit))
                            yield return attack;
                    }
                    break;
                case CardAction.ApplyEffect:
                    foreach (BattleBeat effect in ApplyEffect(target, target, card.Effect, card.Amount))
                        yield return effect;
                    break;
            }
            if (card.Action != CardAction.Deploy)
                discard.Add(card);
            if (Phase == BattlePhase.Lost)
                yield break;
            foreach (BattleBeat beat in Resolve())
                yield return beat;
        }

        public IEnumerable<BattleBeat> Redraw()
        {
            if (!CanRedraw)
                yield break;

            bool free = Refresh == 0;
            discard.AddRange(hand);
            hand.Clear();
            FillHand();
            Refresh = RefreshPeriod;
            Notice = free ? "已换牌，本次未消耗回合。" : "已换牌，推进 1 拍。";
            if (free)
                yield break;

            Phase = BattlePhase.Resolving;
            foreach (BattleBeat beat in Resolve(tickRefresh: false))
                yield return beat;
        }

        private IEnumerable<BattleBeat> Resolve(bool tickRefresh = true)
        {
            if (Wave == Location.WaveCount && !HasEnemies())
            {
                Turn++;
                Phase = BattlePhase.Won;
                yield break;
            }

            var snowedAtCountdown = new List<Unit>();
            foreach (Position pos in TurnOrder())
            {
                Unit attacker = At(pos);
                if (attacker == null)
                    continue;

                if (attacker.Frozen > 0)
                {
                    snowedAtCountdown.Add(attacker);
                    continue;
                }

                attacker.Counter = Math.Max(0, attacker.Counter - 1);
                if (attacker.Counter != 0)
                    continue;

                foreach (BattleBeat attack in Trigger(pos, attacker))
                    yield return attack;
                if (Phase == BattlePhase.Lost)
                    yield break;
            }

            foreach (Unit unit in snowedAtCountdown)
                unit.Frozen = Math.Max(0, unit.Frozen - 1);
            foreach (BattleBeat burn in ResolveBurns())
                yield return burn;
            if (Phase == BattlePhase.Lost)
                yield break;

            foreach (Position pos in TurnOrder())
            {
                if (At(pos) is Unit unit && unit.Ink > 0)
                    unit.Ink--;
            }
            if (tickRefresh)
                Refresh = Math.Max(0, Refresh - 1);
            if (Wave < Location.WaveCount)
            {
                Reinforcements = HasEnemies() ? Math.Max(0, Reinforcements - 1) : 0;
                if (Reinforcements == 0 && FreeEnemySlots() >= NextWave.Count)
                {
                    foreach (BattleBeat beat in Reinforce())
                        yield return beat;
                }
            }

            Turn++;
            Phase = Wave == Location.WaveCount && !HasEnemies() ? BattlePhase.Won : BattlePhase.Planning;
        }

        private IEnumerable<BattleBeat> Trigger(Position source, Unit attacker)
        {
            attacker.Counter = attacker.Type.Period;
            bool attacked = false;
            for (int hit = 1; hit <= attacker.Type.Frenzy && attacker.Frozen == 0; hit++)
            {
                IReadOnlyList<Position> targets = AttackTargets(source, attacker.Type);
                if (targets.Count == 0)
                    break;

                attacked = true;
                if (attacker.Type.Pattern == AttackPattern.Aimless && targets.Count > 1)
                    targets = new[] { targets[RollTarget(targets.Count)] };

                string action = attacker.Type.Frenzy > 1 ? $"狂热 {hit}/{attacker.Type.Frenzy}" :
                    attacker.Type.Pattern == AttackPattern.Front ? "攻击" : PatternName(attacker.Type.Pattern);
                Notice = $"{attacker.Type.Name} · {action}";
                yield return new BattleBeat(source, targets[0], BattleEffect.Windup, action, targets);

                int defeated = 0;
                int attackPower = attacker.Attack;
                var marks = new List<string>(targets.Count);
                foreach (Position target in targets)
                {
                    marks.Add(Hit(source, target, attackPower, attacker.Type.Name).Mark);
                    if (At(target) == null)
                        defeated++;
                }

                // Barrage applies its whole hit together, then presents one impact over the row.
                yield return new BattleBeat(source, targets[0], BattleEffect.Attack, marks[0], targets, impactMarks: marks);

                if (Phase == BattlePhase.Lost)
                {
                    Notice = $"{Captain.Name}倒下。";
                    yield break;
                }

                foreach (Position target in targets)
                {
                    foreach (BattleBeat burst in CheckOverload(target))
                        yield return burst;
                    foreach (BattleBeat effect in TriggerAbility(source, target, attacker, EffectTrigger.Hit))
                        yield return effect;
                    if (Phase == BattlePhase.Lost)
                        yield break;
                }

                if (defeated > 0)
                    foreach (BattleBeat effect in TriggerAbility(source, source, attacker, EffectTrigger.Kill, defeated))
                        yield return effect;

                if (attackPower > 0)
                {
                    foreach (BattleBeat reaction in ResolveReactions(source, targets))
                        yield return reaction;
                    if (Phase == BattlePhase.Lost || At(source) == null)
                        yield break;
                }
            }

            if (attacked)
            {
                attacker.Spice = 0;
                attacker.Weakness = 0;
            }
        }

        private IEnumerable<BattleBeat> ResolveReactions(Position attacker, IReadOnlyList<Position> targets)
        {
            foreach (Position target in targets)
            {
                Unit defender = At(target);
                if (defender == null)
                    continue;

                foreach (BattleBeat reaction in TriggerAbility(target, attacker, defender, EffectTrigger.WhenHit))
                    yield return reaction;
                if (Phase == BattlePhase.Lost || At(attacker) == null)
                    yield break;

                if (defender.Thorns == 0)
                    continue;
                yield return Hit(target, attacker, defender.Thorns--, "反伤", BattleEffect.Retaliation);
                foreach (BattleBeat burst in CheckOverload(attacker))
                    yield return burst;
                if (Phase == BattlePhase.Lost || At(attacker) == null)
                    yield break;
            }
        }

        private IEnumerable<BattleBeat> ResolveBurns()
        {
            foreach (Position pos in TurnOrder())
            {
                if (Phase == BattlePhase.Lost || (Wave == Location.WaveCount && !HasEnemies()))
                    yield break;
                Unit unit = At(pos);
                if (unit == null || unit.Burn == 0)
                    continue;

                yield return Hit(pos, pos, unit.Burn--, "灼烧", BattleEffect.Burning);
                foreach (BattleBeat burst in CheckOverload(pos))
                    yield return burst;
            }
        }

        private IEnumerable<BattleBeat> TriggerAbility(Position source, Position target, Unit unit, EffectTrigger trigger, int times = 1)
        {
            UnitAbility ability = unit.Type.Ability;
            if (unit.Ink > 0 || ability.Effect == EffectKind.None || ability.Trigger != trigger)
                return Array.Empty<BattleBeat>();
            return ApplyEffect(source, target, ability.Effect, (ability.Amount + unit.AbilityBonus) * times);
        }

        public static string PatternName(AttackPattern pattern) => pattern switch
        {
            AttackPattern.Barrage => "横扫",
            AttackPattern.Longshot => "远射",
            AttackPattern.Aimless => "无法瞄准",
            _ => "普通攻击"
        };

        private int RollTarget(int count)
        {
            targetRandom ^= targetRandom << 13;
            targetRandom ^= targetRandom >> 17;
            targetRandom ^= targetRandom << 5;
            return (int)(targetRandom % (uint)count);
        }

        private IEnumerable<BattleBeat> ApplyEffect(Position source, Position target, EffectKind effect, int amount)
        {
            Unit unit = At(target);
            if (unit == null)
                yield break;

            BattleEffect feedback = BattleEffect.Status;
            switch (effect)
            {
                case EffectKind.Heal:
                    amount = Math.Min(amount, unit.MaxHealth - unit.Health);
                    if (amount == 0)
                        yield break;
                    unit.Health += amount;
                    feedback = BattleEffect.Recovery;
                    break;
                case EffectKind.Shell:
                    unit.Shell += amount;
                    feedback = BattleEffect.Guard;
                    break;
                case EffectKind.Snow:
                    if (unit.Type.ResistSnow)
                        amount = Math.Min(amount, 1 - unit.Frozen);
                    unit.Frozen += amount;
                    feedback = BattleEffect.Ice;
                    break;
                case EffectKind.Spice: unit.Spice += amount; break;
                case EffectKind.Overload: unit.Overload += amount; break;
                case EffectKind.Ink: unit.Ink += amount; break;
                case EffectKind.Weakness: unit.Weakness += amount; break;
                case EffectKind.Block: unit.Block += amount; break;
                case EffectKind.EffectBonus: unit.EffectBonus += amount; break;
                case EffectKind.Thorns: unit.Thorns += amount; break;
                case EffectKind.Burn: unit.Burn += amount; break;
                case EffectKind.AttackGrowth:
                    if (target.Enemy)
                        yield break;
                    unit.Progress.IncreaseAttack(amount);
                    break;
                default: yield break;
            }

            string label = effect == EffectKind.Snow && amount == 0 ? "抗雪球" : $"{Effects.Name(effect)} +{amount}";
            Notice = $"{unit.Type.Name} · {label}";
            yield return new BattleBeat(source, target, feedback, label);
            if (effect == EffectKind.Overload)
                foreach (BattleBeat burst in CheckOverload(target))
                    yield return burst;
        }

        private List<Position> OccupiedRow(Position anchor)
        {
            var targets = new List<Position>(Slots);
            for (int slot = 0; slot < Slots; slot++)
            {
                var pos = new Position(anchor.Enemy, anchor.Row, slot);
                if (At(pos) != null)
                    targets.Add(pos);
            }
            return targets;
        }

        private IEnumerable<BattleBeat> CheckOverload(Position origin)
        {
            Unit unit = At(origin);
            if (unit == null || unit.Overload < unit.Health || Phase == BattlePhase.Lost)
                yield break;

            int damage = unit.Overload;
            unit.Overload = 0;
            List<Position> targets = OccupiedRow(origin);
            var marks = new List<string>(targets.Count);
            foreach (Position target in targets)
                marks.Add(Hit(origin, target, damage, "过载").Mark);
            Notice = Phase == BattlePhase.Lost ? $"{Captain.Name}倒下。" : $"{unit.Type.Name}过载引爆 · 同排伤害 {damage}";
            yield return new BattleBeat(origin, origin, BattleEffect.Burst, "过载", targets, impactMarks: marks);

            foreach (Position target in targets)
                foreach (BattleBeat burst in CheckOverload(target))
                    yield return burst;
        }

        private IEnumerable<BattleBeat> HealAll(int amount)
        {
            bool restored = false;
            for (int row = 0; row < Rows; row++)
            for (int slot = 0; slot < Slots; slot++)
            {
                var position = new Position(false, row, slot);
                Unit unit = At(position);
                if (unit == null || (unit.Health == unit.MaxHealth && unit.Frozen == 0))
                    continue;

                int healed = Math.Min(amount, unit.MaxHealth - unit.Health);
                unit.Health += healed;
                unit.Frozen = 0;
                restored = true;
                Notice = $"{unit.Type.Name}恢复 {healed} 点生命并清除雪球。";
                yield return new BattleBeat(position, position, BattleEffect.Recovery, healed > 0 ? $"+{healed}" : "净化");
            }

            if (!restored)
                Notice = "全体友方生命已满且没有雪球。";
        }

        private IEnumerable<BattleBeat> Reinforce()
        {
            var units = NextWave;
            ++Wave;
            Reinforcements = Location.WaveInterval;
            Notice = $"第 {Wave} 波敌人抵达。";
            foreach (EnemySpawn enemy in units)
                yield return AddEnemy(enemy.Row, enemy.Unit);
        }

        private BattleBeat AddEnemy(int row, UnitType type)
        {
            for (int offset = 0; offset < Rows; offset++)
            for (int slot = 0; slot < Slots; slot++)
            {
                var pos = new Position(true, (row + offset) % Rows, slot);
                if (At(pos) != null)
                    continue;

                Set(pos, new Unit(type));
                return new BattleBeat(pos, pos, BattleEffect.Arrival, "增援");
            }

            throw new InvalidOperationException("The encounter schedules more enemies than there are slots.");
        }

        private BattleBeat Hit(Position source, Position target, int amount, string name, BattleEffect feedback = BattleEffect.Attack)
        {
            Unit victim = At(target);
            if (amount > 0 && victim.Block > 0)
            {
                victim.Block--;
                Notice = $"{victim.Type.Name}消耗 1 层格挡，抵消 {amount} 点伤害。";
                return new BattleBeat(source, target, feedback, "格挡 −1");
            }

            int absorbed = Math.Min(victim.Shell, amount);
            victim.Shell -= absorbed;
            int healthDamage = amount - absorbed;
            victim.Health = Math.Max(0, victim.Health - healthDamage);
            Notice = $"{name}对{victim.Type.Name}造成 {healthDamage} 点伤害。";
            if (absorbed > 0)
                Notice += $"护壳吸收 {absorbed} 点。";
            if (victim.Health == 0)
            {
                Set(target, null);
                if (victim.Leader)
                {
                    Phase = BattlePhase.Lost;
                    Notice = $"{Captain.Name}倒下。";
                }
                else
                    Notice += "目标退场。";
            }

            string mark = healthDamage > 0 ? $"−{healthDamage}" : absorbed > 0 ? $"护壳 −{absorbed}" : "0";
            return new BattleBeat(source, target, feedback, mark);
        }

        private void FillHand()
        {
            while (hand.Count < HandSize && draw.Count + discard.Count > 0)
            {
                if (draw.Count == 0)
                {
                    draw.AddRange(discard);
                    discard.Clear();
                    for (int i = draw.Count - 1; i > 0; i--)
                    {
                        int other = random.Next(i + 1);
                        (draw[i], draw[other]) = (draw[other], draw[i]);
                    }
                }

                hand.Add(draw[0]);
                draw.RemoveAt(0);
            }
        }

        private Position? Front(bool enemy, int row)
        {
            for (int slot = 0; slot < Slots; slot++)
            {
                var pos = new Position(enemy, row, slot);
                if (At(pos) != null)
                    return pos;
            }

            return null;
        }

        private bool HasEnemies() => Front(true, 0).HasValue || Front(true, 1).HasValue;

        private int FreeEnemySlots()
        {
            int empty = 0;
            for (int row = 0; row < Rows; row++)
            for (int slot = 0; slot < Slots; slot++)
                if (At(new Position(true, row, slot)) == null)
                    empty++;
            return empty;
        }

        private void Set(Position pos, Unit unit)
        {
            board[pos.Enemy ? 1 : 0, pos.Row, pos.Slot] = unit;
        }
    }
}
