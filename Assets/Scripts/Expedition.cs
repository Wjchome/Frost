using System;
using System.Collections.Generic;

namespace Frostline
{
    public enum CardAction { Deploy, Strike, Freeze, Heal, Hasten }
    public enum BattlePhase { Planning, Resolving, Won, Lost }
    public enum BattleEffect { Windup, Attack, Ice, Recovery, Arrival, Guard }
    public enum AttackPattern { Front, Barrage, Longshot, Aimless }
    public enum UnitSkill { None, DeployShell, HealOnKill, SnowOnHit }

    public sealed class UnitType
    {
        public readonly string Name, Art;
        public readonly int Power, Health, Period, Frenzy;
        public readonly AttackPattern Pattern;
        public readonly UnitSkill Skill;
        public readonly bool ResistSnow;

        public UnitType(string name, string art, int power, int health, int period,
            AttackPattern pattern = AttackPattern.Front, int frenzy = 1,
            UnitSkill skill = UnitSkill.None, bool resistSnow = false)
        {
            (Name, Art, Power, Health, Period) = (name, art, power, health, period);
            (Pattern, Frenzy, Skill, ResistSnow) = (pattern, frenzy, skill, resistSnow);
        }
    }

    public sealed class Unit
    {
        public readonly UnitType Type;
        public readonly bool Leader;
        public int Health, Counter, Frozen, Shell;

        public Unit(UnitType type, bool leader = false)
        {
            Type = type;
            Leader = leader;
            Health = type.Health;
            Counter = type.Period;
        }

        public Unit Copy() => (Unit)MemberwiseClone();
    }

    public sealed class Card
    {
        public readonly string Name, Art, Description;
        public readonly CardAction Action;
        public readonly int Amount, Damage;
        public int DeploymentHealth { get; }
        public readonly UnitType Unit;

        public Card(UnitType unit, int? deploymentHealth = null) : this(unit.Name, unit.Art, CardAction.Deploy, 0, "部署到己方空位。")
        {
            Unit = unit;
            DeploymentHealth = deploymentHealth ?? unit.Health;
        }

        public Card(string name, string art, CardAction action, int amount, string description, int damage = 0)
        {
            (Name, Art, Action, Amount, Description, Damage) = (name, art, action, amount, description, damage);
        }
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

    public static class Catalog
    {
        public static readonly UnitType Captain = new("岚哨长", "scout", 3, 12, 3, skill: UnitSkill.HealOnKill);
        public static readonly UnitType Guide = new("渡崖人", "porter", 2, 9, 3, skill: UnitSkill.DeployShell);
        public static readonly UnitType Chipper = new("凿冰师", "mechanic", 4, 6, 3, pattern: AttackPattern.Barrage);
        public static readonly UnitType Runner = new("飞索信使", "courier", 2, 5, 2, frenzy: 2);
        public static readonly UnitType Hound = new("风窟兽", "hound", 2, 5, 3, pattern: AttackPattern.Aimless);
        public static readonly UnitType Probe = new("冰隙蜂", "drone", 1, 4, 2, pattern: AttackPattern.Longshot);
        public static readonly UnitType Guard = new("崖脊卫", "warden", 3, 7, 3, skill: UnitSkill.SnowOnHit);
        public static readonly UnitType Boss = new("裂谷巨像", "ram", 3, 15, 4, pattern: AttackPattern.Barrage, resistSnow: true);

        public static readonly Card Spear = new("破冰矛", "spear", CardAction.Strike, 0, "造成 2 点伤害。", damage: 2);
        public static readonly Card Chill = new("冷凝罐", "chill", CardAction.Freeze, 2, "造成 1 点伤害，施加 2 层雪球。", damage: 1);
        public static readonly Card Patch = new("急救包", "repair", CardAction.Heal, 2, "全体友方恢复 2 点生命并清除雪球。");
        public static readonly Card Whistle = new("催行哨", "whistle", CardAction.Hasten, 2, "倒计时减少 2，归零立刻行动。");

        public static readonly Card[] Starter =
        {
            new(Guide), Spear, Chill, new(Runner), Patch, Whistle, Spear, Chill, new(Chipper), Patch
        };
    }

    public sealed class Expedition
    {
        public const int Rows = 2;
        public const int Slots = 3;
        public const int HandSize = 6;
        public const int RefreshPeriod = 4;
        public const int WaveSize = 3;
        private const int RecallHealing = 5;
        private const int WaveInterval = 4;

        private Unit[,,] board = new Unit[2, Rows, Slots];
        private uint targetRandom = 0x890ca3a1;
        private readonly Random random = new(2471);
        private readonly List<Card> draw = new(Catalog.Starter);
        private readonly List<Card> discard = new();
        private readonly List<Card> hand = new();

        public BattlePhase Phase { get; private set; }
        public int Turn { get; private set; } = 1;
        public int Wave { get; private set; } = 1;
        public int Reinforcements { get; private set; } = WaveInterval;
        public int Refresh { get; private set; } = RefreshPeriod;
        public bool Opening { get; private set; } = true;
        public string Notice { get; private set; } = "部署队长，随后抽取开局手牌。";

        public IReadOnlyList<Card> Hand => hand;
        public int DrawCount => draw.Count;
        public int DiscardCount => discard.Count;

        public Expedition()
        {
            hand.Add(new Card(Catalog.Captain));
            Set(new Position(true, 0, 0), new Unit(Catalog.Hound));
            Set(new Position(true, 1, 0), new Unit(Catalog.Probe));
        }

        public Unit At(Position pos) => pos.Valid ? board[pos.Enemy ? 1 : 0, pos.Row, pos.Slot] : null;

        public bool CanPlay(int index, Position pos)
        {
            if (Phase != BattlePhase.Planning || index < 0 || index >= hand.Count || !pos.Valid)
                return false;

            Unit unit = At(pos);
            return hand[index].Action switch
            {
                CardAction.Deploy => !pos.Enemy && unit == null,
                CardAction.Strike or CardAction.Freeze => pos.Enemy && unit != null,
                CardAction.Heal => !pos.Enemy && unit != null,
                CardAction.Hasten => !pos.Enemy && unit != null && unit.Counter > 0 && unit.Frozen == 0,
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

            if (hand[index].Action == CardAction.Heal)
            {
                var targets = new List<Position>();
                foreach (Position pos in TurnOrder())
                {
                    if (!pos.Enemy && At(pos) is Unit unit && (unit.Health < unit.Type.Health || unit.Frozen > 0))
                        targets.Add(pos);
                }
                return targets;
            }
            return new[] { anchor };
        }

        public IReadOnlyList<Position> ForecastOrder()
        {
            var order = new List<Position>();
            if (Opening || Phase != BattlePhase.Planning)
                return order;

            // Resolve the next natural countdown on a copy. Only the battlefield and primitive
            // fields are touched by Resolve, including the independent Aimless random state.
            var forecast = (Expedition)MemberwiseClone();
            forecast.board = new Unit[2, Rows, Slots];
            foreach (Position pos in TurnOrder())
                forecast.Set(pos, At(pos)?.Copy());

            foreach (BattleBeat step in forecast.Resolve())
            {
                if (step.Effect == BattleEffect.Windup && !order.Contains(step.Source))
                    order.Add(step.Source);
            }
            return order;
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
            Phase == BattlePhase.Planning && !source.Enemy && At(source) is Unit unit && !unit.Leader;

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
            int health = Math.Min(unit.Type.Health, unit.Health + RecallHealing);
            discard.Add(new Card(unit.Type, health));
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
                    var deployed = new Unit(card.Unit, leader: Opening) { Health = card.DeploymentHealth };
                    Set(target, deployed);
                    Notice = $"{card.Name}上场。";
                    yield return new BattleBeat(target, target, BattleEffect.Arrival, "部署");
                    if (Opening)
                    {
                        Opening = false;
                        FillHand();
                        Notice = "出牌或提前敲换牌铃，推进一回合。";
                        Phase = BattlePhase.Planning;
                        yield break;
                    }
                    if (deployed.Type.Skill == UnitSkill.DeployShell)
                    {
                        deployed.Shell += 2;
                        Notice = $"{card.Name}获得 2 点护壳。";
                        yield return new BattleBeat(target, target, BattleEffect.Guard, "护壳 +2");
                    }
                    break;
                case CardAction.Strike:
                case CardAction.Freeze:
                    Notice = $"{card.Name}准备命中{unit.Type.Name}。";
                    yield return new BattleBeat(target, target, BattleEffect.Windup, card.Name, cardStrike: true);
                    yield return Hit(target, target, card.Damage, card.Name);
                    if (card.Action == CardAction.Freeze && At(target) != null)
                        yield return ApplySnow(target, target, unit, card.Amount);
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
            }
            if (card.Action != CardAction.Deploy)
                discard.Add(card);
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
            if (Wave == WaveSize && !HasEnemies())
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
            if (tickRefresh)
                Refresh = Math.Max(0, Refresh - 1);
            if (Wave < WaveSize && (--Reinforcements == 0 || !HasEnemies()))
            {
                foreach (BattleBeat beat in Reinforce())
                    yield return beat;
            }

            Turn++;
            Phase = Wave == WaveSize && !HasEnemies() ? BattlePhase.Won : BattlePhase.Planning;
        }

        private IEnumerable<BattleBeat> Trigger(Position source, Unit attacker)
        {
            attacker.Counter = attacker.Type.Period;
            for (int hit = 1; hit <= attacker.Type.Frenzy && attacker.Frozen == 0; hit++)
            {
                IReadOnlyList<Position> targets = AttackTargets(source, attacker.Type);
                if (targets.Count == 0)
                    yield break;

                if (attacker.Type.Pattern == AttackPattern.Aimless && targets.Count > 1)
                    targets = new[] { targets[RollTarget(targets.Count)] };

                string action = attacker.Type.Frenzy > 1 ? $"狂热 {hit}/{attacker.Type.Frenzy}" :
                    attacker.Type.Pattern == AttackPattern.Front ? "攻击" : PatternName(attacker.Type.Pattern);
                Notice = $"{attacker.Type.Name} · {action}";
                yield return new BattleBeat(source, targets[0], BattleEffect.Windup, action, targets);

                int defeated = 0;
                var marks = new List<string>(targets.Count);
                foreach (Position target in targets)
                {
                    marks.Add(Hit(source, target, attacker.Type.Power, attacker.Type.Name).Mark);
                    if (At(target) == null)
                        defeated++;
                }

                // Barrage applies its whole hit together, then presents one impact over the row.
                yield return new BattleBeat(source, targets[0], BattleEffect.Attack, marks[0], targets, impactMarks: marks);

                if (Phase == BattlePhase.Lost)
                {
                    Notice = "岚哨长倒下。";
                    yield break;
                }

                if (attacker.Type.Skill == UnitSkill.SnowOnHit)
                {
                    foreach (Position target in targets)
                    {
                        if (At(target) is Unit survivor)
                            yield return ApplySnow(source, target, survivor, 1);
                    }
                }

                if (attacker.Type.Skill == UnitSkill.HealOnKill && defeated > 0)
                {
                    int healed = Math.Min(attacker.Type.Health - attacker.Health, defeated * 2);
                    if (healed > 0)
                    {
                        attacker.Health += healed;
                        Notice = $"{attacker.Type.Name}击倒敌人，恢复 {healed} 点生命。";
                        yield return new BattleBeat(source, source, BattleEffect.Recovery, $"生命 +{healed}");
                    }
                }
            }
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

        private BattleBeat ApplySnow(Position source, Position target, Unit unit, int amount)
        {
            int snow = unit.Type.ResistSnow ? Math.Min(1, unit.Frozen + amount) : unit.Frozen + amount;
            int added = snow - unit.Frozen;
            unit.Frozen = snow;
            Notice = added > 0 ? $"{unit.Type.Name}获得 {added} 层雪球。" : $"{unit.Type.Name}的雪球已达上限。";
            return new BattleBeat(source, target, BattleEffect.Ice, added > 0 ? $"雪球 +{added}" : "抗雪球");
        }

        private IEnumerable<BattleBeat> HealAll(int amount)
        {
            bool restored = false;
            for (int row = 0; row < Rows; row++)
            for (int slot = 0; slot < Slots; slot++)
            {
                var position = new Position(false, row, slot);
                Unit unit = At(position);
                if (unit == null || (unit.Health == unit.Type.Health && unit.Frozen == 0))
                    continue;

                int healed = Math.Min(amount, unit.Type.Health - unit.Health);
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
            ++Wave;
            Reinforcements = WaveInterval;
            Notice = Wave == WaveSize ? "裂谷巨像现身，会攻击同排所有队员。" : "第二波敌人抵达。";
            UnitType first = Wave == WaveSize ? Catalog.Boss : Catalog.Guard;
            UnitType second = Wave == WaveSize ? Catalog.Probe : Catalog.Hound;
            yield return AddEnemy(0, first);
            yield return AddEnemy(1, second);
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

        private BattleBeat Hit(Position source, Position target, int amount, string name)
        {
            Unit victim = At(target);
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
                    Notice = "岚哨长倒下。";
                }
                else
                    Notice += "目标退场。";
            }

            string mark = healthDamage > 0 ? $"−{healthDamage}" : $"护壳 −{absorbed}";
            return new BattleBeat(source, target, BattleEffect.Attack, mark);
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

        private void Set(Position pos, Unit unit)
        {
            board[pos.Enemy ? 1 : 0, pos.Row, pos.Slot] = unit;
        }
    }
}
