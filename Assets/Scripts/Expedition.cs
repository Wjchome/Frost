using System;
using System.Collections.Generic;

namespace Frostline
{
    public enum CardAction { Deploy, Strike, Freeze, Heal, Hasten, ApplyEffect, Shift, Technique, Copy }
    public enum BattlePhase { Planning, Resolving, Won, Lost }
    public enum BattleEffect { Windup, Attack, Ice, Recovery, Arrival, Guard, Status, Burst, Retaliation, Burning }
    public enum AttackPattern { Front, Barrage, Longshot, Aimless, Support, AllEnemies }
    public enum EffectTrigger { Deploy, Hit, Kill, WhenHit, Act, WhileActive, AllyKill, Death, WhenJunkHit, SnowApplied, CardDestroyed, WhileWounded, Attack, TurnEnd }
    public enum EffectKind { None, Heal, Shell, Snow, Spice, Overload, Ink, Weakness, Block, EffectBonus, Thorns, Burn, AttackGrowth, Exposed, HealthGrowth,
        AttackUp, Frenzy, Aimless, Barrage, Countdown, MaxCountdown, Draw, Junk, RecycleDraw, RandomStat, JunkPower, Cleanse, Hurt,
        Retaliate, TeamSpice, AttackDown, StickyFruit, BattleHealthUp, BattleHealthDown, MaxCountdownUp, Poison, SnowOnBlock,
        Corruption, AllEnemiesPattern, SelfAttack, TeamHealth, WeakenRow, AbsorbRow, HurtForFrenzy, ExtraAllyTick, DamageVsLowerHealth,
        CountdownUp }
    public enum CardTarget { Enemy, Ally, Either, AllEnemies, AllAllies, HealthyEnemies, None, AllUnits, RandomAlly, RandomEnemy, SacrificeAlly }
    public enum JunkOperation { None, DoubleAndStrike, Recycle, BuffAttack, BuffFrenzy }
    public enum Pendant { None, WarmStone, TuningRing, SwiftClasp }

    public static class Pendants
    {
        public static int Health(Pendant pendant) => pendant == Pendant.WarmStone ? 2 : 0;
        public static int Effect(Pendant pendant) => pendant == Pendant.TuningRing ? 1 : 0;
        public static int Speed(Pendant pendant) => pendant == Pendant.SwiftClasp ? 1 : 0;

        public static string Name(Pendant pendant) => pendant switch
        {
            Pendant.WarmStone => "暖石挂坠",
            Pendant.TuningRing => "调律挂坠",
            Pendant.SwiftClasp => "疾行扣",
            _ => ""
        };

        public static string Description(Pendant pendant) => pendant switch
        {
            Pendant.WarmStone => $"最大生命 +{Health(pendant)}",
            Pendant.TuningRing => $"触发能力数值 +{Effect(pendant)}",
            Pendant.SwiftClasp => $"最大倒计时 −{Speed(pendant)}，最低为 1",
            _ => ""
        };
    }

    public readonly struct UnitAbility
    {
        public readonly EffectTrigger Trigger;
        public readonly EffectKind Effect;
        public readonly int Amount, SecondaryAmount;

        public UnitAbility(EffectTrigger trigger, EffectKind effect, int amount, int secondaryAmount = 0) =>
            (Trigger, Effect, Amount, SecondaryAmount) = (trigger, effect, amount, secondaryAmount);

        public string Timing => Trigger switch
        {
            EffectTrigger.Deploy => "部署",
            EffectTrigger.Hit => "命中",
            EffectTrigger.Kill => "击倒",
            EffectTrigger.WhenHit => "受击",
            EffectTrigger.Act => "行动",
            EffectTrigger.WhileActive => "在场上时",
            EffectTrigger.AllyKill => "友军击杀",
            EffectTrigger.Death => "死亡",
            EffectTrigger.WhenJunkHit => "被垃圾命中",
            EffectTrigger.SnowApplied => "雪球施加",
            EffectTrigger.CardDestroyed => "卡牌摧毁",
            EffectTrigger.WhileWounded => "每个回合受伤状态触发",
            EffectTrigger.Attack => "每次攻击后",
            EffectTrigger.TurnEnd => "每回合结束时",
            _ => ""
        };

        public string Describe(int bonus, int? attack = null)
        {
            int amount = Amount + bonus;
            string timing = Trigger switch
            {
                EffectTrigger.WhileActive => "每次上场时",
                EffectTrigger.WhileWounded => "每回合结束时，若当前生命低于上限",
                _ => $"{Timing}时"
            };
            switch (Effect)
            {
                case EffectKind.RandomStat: return $"受击时，随机另一名友军的攻击或最大生命 +{amount}（本场）";
                case EffectKind.Draw: return $"{timing}，抽 {amount} 张牌";
                case EffectKind.Junk: return $"{timing}，向手牌生成 {amount} 张垃圾";
                case EffectKind.RecycleDraw: return $"行动时摧毁手中所有垃圾，每张抽 {amount} 张牌";
                case EffectKind.JunkPower:
                    return attack.HasValue ? $"每次上场时，本场所有垃圾的伤害 +{attack.Value}（按上场时的攻击力）"
                        : "每次上场时，本场所有垃圾增加等同于上场时攻击力的伤害";
                case EffectKind.Retaliate: return "被单位攻击后，若存活且未被雪球冻住，就以当前攻击力反击";
                case EffectKind.TeamSpice: return $"受击时，所有友军获得 {amount} 层辣椒";
                case EffectKind.AttackDown: return $"受击时，自身攻击 −{amount}（本场）";
                case EffectKind.StickyFruit: return $"受击时，将 {amount} 张黏黏水果加入玩家手牌";
                case EffectKind.SnowOnBlock: return $"行动后及自身消耗格挡时，给所有敌方施加 {amount} 层雪球。消耗格挡时还会反击攻击者";
                case EffectKind.MaxCountdownUp: return $"行动后，本场自身最大倒计时 +{amount}，攻击 +{SecondaryAmount + bonus}";
                case EffectKind.SelfAttack: return "每次攻击后，按这次攻击的攻击力攻击自己";
                case EffectKind.TeamHealth: return $"行动后，其他友军本场最大生命和当前生命 +{amount}";
                case EffectKind.WeakenRow: return $"行动后，给整排友军（包括自己）施加 {amount} 层虚弱";
                case EffectKind.AbsorbRow: return $"行动后，从同排每名其他友军吸收至多 {amount} 点攻击和最大生命（友军至少保留 1 点最大生命），自身增加实际吸收值";
                case EffectKind.HurtForFrenzy: return $"每回合结束时，失去 {amount} 点生命，若存活则本场狂热 +{SecondaryAmount + bonus}";
                case EffectKind.ExtraAllyTick: return $"行动后，友军额外推进 {amount} 拍（每回合仅由正常行动触发）";
                case EffectKind.DamageVsLowerHealth: return $"攻击当前生命低于自身的目标时，伤害 ×{amount}";
            }
            if (Trigger == EffectTrigger.Attack && Effect == EffectKind.Shell)
                return $"每次攻击后，自身获得 {amount} 点护壳";
            if (Effect == EffectKind.AttackUp)
            {
                if (Trigger == EffectTrigger.SnowApplied)
                    return $"有单位获得雪球时，自身攻击 +{amount}（本场）";
                if (Trigger == EffectTrigger.CardDestroyed)
                    return $"有牌被摧毁时，自身攻击 +{amount}（本场）";
                if (Trigger == EffectTrigger.WhileWounded)
                    return $"{timing}，自身本场攻击 +{amount}";
                if (Trigger is EffectTrigger.WhenHit or EffectTrigger.Act)
                    return $"{Timing}时自身攻击 +{amount}（本场）";
            }
            if (Trigger == EffectTrigger.Act && Effect == EffectKind.Shell)
                return $"行动时，自身获得 {amount} 点护甲";
            if (Trigger == EffectTrigger.AllyKill)
                return $"任意友军单位击倒敌人时，给击杀者 {amount} 点护盾";
            if (Trigger == EffectTrigger.WhileActive && Effect is EffectKind.AttackUp or EffectKind.Thorns)
                return $"每次上场时，所有在场友军获得 {amount} 点{Effects.Name(Effect)}";
            if (Effect == EffectKind.Frenzy)
                return $"{timing}，自身狂热 +{amount}（本场）";
            string effect = Effect == EffectKind.Heal ? "回血" : Effects.Name(Effect);
            string target = Trigger == EffectTrigger.WhenHit ? "给攻击者" : "";
            return $"{timing}，{target}{effect} +{amount}";
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
            EffectKind.Exposed => "易伤",
            EffectKind.HealthGrowth => "最大生命",
            EffectKind.AttackUp => "攻击",
            EffectKind.Frenzy => "狂热",
            EffectKind.Aimless => "无法瞄准",
            EffectKind.Barrage => "横扫",
            EffectKind.Countdown => "倒计时",
            EffectKind.MaxCountdown => "最大倒计时",
            EffectKind.Draw => "抽牌",
            EffectKind.Junk => "垃圾",
            EffectKind.RecycleDraw => "回收抽牌",
            EffectKind.RandomStat => "随机属性",
            EffectKind.JunkPower => "垃圾攻击",
            EffectKind.Cleanse => "净化",
            EffectKind.Hurt => "失去生命",
            EffectKind.Retaliate => "反击",
            EffectKind.TeamSpice => "辣椒",
            EffectKind.AttackDown => "攻击",
            EffectKind.StickyFruit => "黏黏水果",
            EffectKind.BattleHealthUp or EffectKind.BattleHealthDown => "最大生命",
            EffectKind.MaxCountdownUp => "最大倒计时",
            EffectKind.Poison => "中毒",
            EffectKind.SnowOnBlock => "雪球",
            EffectKind.Corruption => "腐化",
            EffectKind.AllEnemiesPattern => "全体打击",
            EffectKind.TeamHealth => "生命",
            EffectKind.WeakenRow => "虚弱",
            EffectKind.AbsorbRow => "吸收",
            EffectKind.HurtForFrenzy => "狂热",
            EffectKind.ExtraAllyTick => "额外行动",
            EffectKind.DamageVsLowerHealth or EffectKind.SelfAttack => "攻击",
            EffectKind.CountdownUp => "倒计时",
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
            EffectKind.Exposed => "下次未被格挡的伤害加上当前层数，然后清空易伤。护壳照常吸收伤害。",
            EffectKind.Poison => "回合末失去等同于层数的生命，然后减少 1 层。",
            EffectKind.Corruption => "受到未被格挡的伤害时，伤害翻倍，然后减少 1 层。",
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
        public int Health { get; private set; }

        public UnitProgress(int attack, int health) => (Attack, Health) = (attack, health);

        public void IncreaseAttack(int amount) => Attack += amount;
        public void IncreaseHealth(int amount) => Health += amount;
    }

    public sealed class Unit
    {
        public readonly UnitType Type;
        public readonly bool Leader;
        public readonly Pendant Pendant;
        public int Health, Counter, Frozen, Shell;
        public int Spice, Overload, Ink, Weakness, Block, EffectBonus;
        public int Thorns, Burn, Exposed, Poison, Corruption;
        public int BattleAttack, BattleHealth, BonusFrenzy, CounterReduction;
        public bool Aimless, Barrage, AllEnemies;
        internal List<(int Turn, int Amount)> ScheduledHealing = new();
        internal UnitProgress Progress { get; private set; }
        public int BaseAttack => Progress.Attack;
        public int Attack => Math.Max(0, BaseAttack + BattleAttack + Spice - Weakness);
        public int MaxHealth => Progress.Health + Pendants.Health(Pendant) + BattleHealth;
        public int Period => Type.Period == 0 ? 0 : Math.Max(1, Type.Period - Pendants.Speed(Pendant) - CounterReduction);
        public int Frenzy => Type.Frenzy + BonusFrenzy;
        public AttackPattern Pattern => AllEnemies ? AttackPattern.AllEnemies : Aimless ? AttackPattern.Aimless :
            Barrage ? AttackPattern.Barrage : Type.Pattern;
        public int AbilityBonus => EffectBonus + Pendants.Effect(Pendant);

        public Unit(UnitType type, bool leader = false, Pendant pendant = Pendant.None)
            : this(type, leader, pendant, new UnitProgress(type.Power, type.Health)) { }

        internal Unit(UnitType type, bool leader, Pendant pendant, UnitProgress progress)
        {
            Type = type;
            Leader = leader;
            Pendant = pendant;
            Progress = progress;
            Health = MaxHealth;
            Counter = Period;
        }

        public Unit Copy()
        {
            var copy = (Unit)MemberwiseClone();
            copy.Progress = new UnitProgress(BaseAttack, Progress.Health);
            copy.ScheduledHealing = new List<(int Turn, int Amount)>(ScheduledHealing);
            return copy;
        }
    }

    [Serializable]
    public sealed class CardTechnique
    {
        public CardTarget Target;
        public bool Row, Consume, Junk, CanBuffHand;
        public int JunkCost, JunkOutput, JunkPerTurn, Hits = 1, GainAfterUse, DamageCountdown;
        public bool RandomTarget, HealFront;
        public EffectKind Secondary;
        public int SecondaryAmount;
        public EffectKind ThirdEffect;
        public int ThirdAmount;
        public int ImmediateHits, Uses, OpponentBlock;
        public int[] HealSchedule;
        public JunkOperation JunkAction;
        public string Describe(int damage, EffectKind effect, int amount, int bonusHits, int uses, int? junkCount = null)
        {
            var parts = new List<string>();
            if (Hits + bonusHits > 1)
                parts.Add($"狂热 ×{Hits + bonusHits}");
            if (JunkAction == JunkOperation.DoubleAndStrike)
            {
                string bonus = damage > 0 ? $"另加 {damage} 点强化伤害" : "";
                string hit = junkCount.HasValue ? $"造成 {damage + junkCount.Value} 点伤害（手中垃圾 {junkCount.Value} 张{bonus}）"
                    : $"造成等于手中垃圾数的伤害{bonus}";
                parts.Add($"{hit}，再复制手中每张垃圾");
            }
            else if (damage > 0 || Junk)
                parts.Add($"造成 {damage} 点伤害");
            if (JunkAction == JunkOperation.Recycle) parts.Add($"摧毁手中所有垃圾，每张抽 {amount} 张牌");
            if (JunkAction == JunkOperation.BuffAttack) parts.Add($"手中每张垃圾的本场伤害 +{amount}");
            if (JunkAction == JunkOperation.BuffFrenzy) parts.Add($"手中每张垃圾的本场狂热 +{amount}");
            if (Target == CardTarget.SacrificeAlly) parts.Add("吞噬一名非领袖友军，随机另一名友军获得等同于其当前狂热次数的狂热");
            string primary = DescribeEffect(effect, amount);
            if (primary.Length > 0)
                parts.Add(primary);
            string secondary = DescribeEffect(Secondary, SecondaryAmount);
            if (secondary.Length > 0)
                parts.Add(secondary);
            string third = DescribeEffect(ThirdEffect, ThirdAmount);
            if (third.Length > 0)
                parts.Add(third);
            if (ImmediateHits > 0) parts.Add($"立即攻击 {ImmediateHits} 次");
            if (HealSchedule is { Length: > 0 })
                parts.Add($"从本回合结束起，每回合依次恢复 {string.Join("、", HealSchedule)} 点生命");
            if (CanBuffHand) parts.Add("可用于友方单位或有攻击的手牌");
            if (Row) parts.Add("横扫同排");
            if (RandomTarget) parts.Add("无法瞄准");
            if (Target == CardTarget.HealthyEnemies) parts.Add("攻击所有满生命的敌人");
            if (Target == CardTarget.AllAllies) parts.Add("作用于所有友军");
            if (Target == CardTarget.AllUnits) parts.Add("作用于场上所有单位");
            if (Target == CardTarget.RandomAlly) parts.Add("目标为随机友军");
            if (Target == CardTarget.RandomEnemy) parts.Add("目标为随机敌军");
            if (OpponentBlock > 0) parts.Add($"另使一名随机敌军格挡 +{OpponentBlock}");
            if (DamageCountdown > 0) parts.Add($"让该敌人倒计时 −{DamageCountdown}");
            if (HealFront) parts.Add("最前方友军恢复等同于实际生命伤害的生命");
            if (JunkCost > 0) parts.Add($"需要并摧毁手中 {JunkCost} 张垃圾");
            if (JunkOutput > 0) parts.Add($"生成 {JunkOutput} 张垃圾");
            if (GainAfterUse > 0) parts.Add($"每次使用后本场伤害 +{GainAfterUse}");
            if (JunkPerTurn > 0) parts.Add($"装备后，本场每回合开始生成 {JunkPerTurn} 张垃圾到手牌");
            else if (Consume) parts.Add("消耗（本场移出）");
            if (Uses > 0) parts.Add($"剩余 {uses}/{Uses} 次，用完消耗（本场移出）");
            return string.Join("。", parts) + "。";
        }

        private static string DescribeEffect(EffectKind effect, int amount) => effect switch
        {
            EffectKind.None => "",
            EffectKind.AttackUp => $"本场攻击 +{amount}",
            EffectKind.AttackDown => $"本场攻击 −{amount}",
            EffectKind.Countdown => $"倒计时 −{amount}",
            EffectKind.CountdownUp => $"当前倒计时 +{amount}",
            EffectKind.MaxCountdown => $"本场最大倒计时 −{amount}",
            EffectKind.MaxCountdownUp => $"本场最大倒计时 +{amount}",
            EffectKind.BattleHealthUp => $"本场最大生命及当前生命 +{amount}",
            EffectKind.BattleHealthDown => $"本场最大生命 −{amount}（最低为 1）",
            EffectKind.EffectBonus => $"本场触发能力数值 +{amount}",
            EffectKind.Heal => $"恢复 {amount} 点生命",
            EffectKind.Draw => $"抽 {amount} 张牌",
            EffectKind.Aimless => "使目标无法瞄准（本场）",
            EffectKind.Barrage => "赋予横扫（本场）",
            EffectKind.AllEnemiesPattern => "本场攻击目标改为全体敌军",
            EffectKind.Frenzy => $"本场狂热 +{amount}",
            EffectKind.Cleanse => "清除负面状态",
            EffectKind.Hurt => $"失去 {amount} 点生命",
            _ => $"{Effects.Name(effect)} +{amount}"
        };
    }

    public sealed class Card
    {
        public readonly string Name, Art;
        public readonly CardAction Action;
        public readonly int Amount, Damage, BonusDamage;
        public readonly EffectKind Effect, BonusAgainst;
        public readonly bool TargetsEnemy;
        public readonly CardTechnique Technique;
        public int UsesRemaining { get; }
        public int BonusHits { get; }
        private readonly int? deploymentHealth;
        public int DeploymentMaxHealth => Unit == null ? 0 : (progress?.Health ?? Unit.Health) + Pendants.Health(Pendant);
        public int DeploymentHealth => deploymentHealth ?? DeploymentMaxHealth;
        public int DeploymentEffectBonus { get; }
        public int DeploymentAttack => progress?.Attack ?? Unit?.Power ?? 0;
        public readonly UnitType Unit;
        public readonly Pendant Pendant;
        private readonly UnitProgress progress;

        public string Description => Describe();

        public string Describe(int extraDamage = 0, int? junkCount = null) => Action switch
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
            CardAction.ApplyEffect when Effect == EffectKind.HealthGrowth => $"一名友方在本次远征中永久获得 {Amount} 点最大生命，并回复等量生命。",
            CardAction.ApplyEffect => $"给{(TargetsEnemy ? "敌方" : "友方")}施加 {Effects.Name(Effect)} +{Amount}。",
            CardAction.Shift => "把一名敌人拉到另一排的第一个空位。",
            CardAction.Technique => Technique.Describe(Damage + extraDamage, Effect, Amount, BonusHits, UsesRemaining, junkCount),
            CardAction.Copy => "复制一张手牌到手中。消耗（本场移出）。",
            _ => ""
        };

        public Card(UnitType unit, int? deploymentHealth = null, int effectBonus = 0, Pendant pendant = Pendant.None)
            : this(unit, deploymentHealth, effectBonus, pendant, null) { }

        private Card(UnitType unit, int? deploymentHealth, int effectBonus, Pendant pendant, UnitProgress progress)
            : this(unit.Name, unit.Art, CardAction.Deploy, 0)
        {
            Unit = unit;
            Pendant = pendant;
            this.deploymentHealth = deploymentHealth;
            DeploymentEffectBonus = effectBonus;
            this.progress = progress;
        }

        public Card(string name, string art, CardAction action, int amount, int damage = 0,
            EffectKind effect = EffectKind.None, bool targetsEnemy = false, EffectKind bonusAgainst = EffectKind.None,
            int bonusDamage = 0, CardTechnique technique = null, int? remainingUses = null, int bonusHits = 0)
        {
            (Name, Art, Action, Amount, Damage) = (name, art, action, amount, damage);
            (Effect, TargetsEnemy) = (effect, targetsEnemy);
            (BonusAgainst, BonusDamage) = (bonusAgainst, bonusDamage);
            Technique = technique;
            UsesRemaining = remainingUses ?? technique?.Uses ?? 0;
            BonusHits = bonusHits;
        }

        public bool IsJunk => Technique?.Junk == true;
        public bool HasAttack => Action is CardAction.Strike or CardAction.Freeze ||
            (Action == CardAction.Technique && (Damage > 0 || IsJunk || Technique.JunkAction == JunkOperation.DoubleAndStrike));

        internal Card WithDamage(int damage) =>
            new(Name, Art, Action, Amount, damage, Effect, TargetsEnemy, BonusAgainst, BonusDamage, Technique, UsesRemaining, BonusHits);

        internal Card WithBonusHits(int hits) =>
            new(Name, Art, Action, Amount, Damage, Effect, TargetsEnemy, BonusAgainst, BonusDamage, Technique, UsesRemaining, hits);

        internal Card SpendUse() =>
            new(Name, Art, Action, Amount, Damage, Effect, TargetsEnemy, BonusAgainst, BonusDamage, Technique, UsesRemaining - 1, BonusHits);

        internal Card CopyInBattle() => Unit == null ? this
            : new Card(Unit, DeploymentHealth, DeploymentEffectBonus, Pendant, CopyProgress());

        public int DamageAgainst(Unit target) => Damage + (BonusAgainst switch
        {
            EffectKind.Snow when target.Frozen > 0 => BonusDamage,
            EffectKind.Overload when target.Overload > 0 => BonusDamage,
            EffectKind.Shell when target.Shell > 0 => BonusDamage,
            _ => 0
        });

        internal Card Recruit() => Unit == null ? this
            : new Card(Unit, null, 0, Pendant, new UnitProgress(Unit.Power, Unit.Health));

        internal Card Equip(Pendant pendant) => new Card(Unit, null, 0, pendant, progress);

        internal static Card FromRecall(Unit unit, int health) =>
            new Card(unit.Type, health, unit.EffectBonus, unit.Pendant, unit.Progress);

        private UnitProgress CopyProgress() => new(DeploymentAttack, progress?.Health ?? Unit.Health);

        internal Unit Deploy(bool leader) => CreateUnit(leader, progress ?? CopyProgress());

        internal Unit PreviewDeployment(bool leader) => CreateUnit(leader, CopyProgress());

        internal Card CopyForForecast() => Unit == null ? this
            : new Card(Unit, DeploymentHealth, DeploymentEffectBonus, Pendant, CopyProgress());

        private Unit CreateUnit(bool leader, UnitProgress state)
        {
            var unit = new Unit(Unit, leader, Pendant, state) { EffectBonus = DeploymentEffectBonus };
            unit.Health = Math.Min(unit.MaxHealth, DeploymentHealth);
            return unit;
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
        public readonly bool CardStrike, Blocked;
        public readonly IReadOnlyList<string> ImpactMarks;

        public BattleBeat(Position source, Position target, BattleEffect effect, string mark,
            IReadOnlyList<Position> targets = null, bool cardStrike = false, IReadOnlyList<string> impactMarks = null, bool blocked = false)
        {
            (Source, Target, Effect, Mark) = (source, target, effect, mark);
            Targets = targets ?? new[] { target };
            CardStrike = cardStrike;
            Blocked = blocked;
            ImpactMarks = impactMarks;
        }
    }

    public readonly struct UnitForecast
    {
        public readonly Position Origin;
        public readonly Position? Destination;
        public readonly int Health, Shell, Block, Snow, Attack, Poison, Corruption, Period, Counter, Frenzy, AbilityBonus, MaxHealth;

        public bool Defeated => !Destination.HasValue;

        public UnitForecast(Position origin, Position? destination, Unit unit)
        {
            Origin = origin;
            Destination = destination;
            (Health, Shell, Block, Snow, Attack) = (unit.Health, unit.Shell, unit.Block, unit.Frozen, unit.Attack);
            (Poison, Period, Counter, Frenzy, AbilityBonus, MaxHealth) =
                (unit.Poison, unit.Period, unit.Counter, unit.Frenzy, unit.AbilityBonus, unit.MaxHealth);
            Corruption = unit.Corruption;
        }
    }

    public sealed class TurnPreview
    {
        public readonly IReadOnlyList<Position> Actions;
        public readonly IReadOnlyList<(Position Position, UnitType Unit)> Arrivals;
        public readonly IReadOnlyList<UnitForecast> Units;
        public readonly BattlePhase Phase;

        public TurnPreview(IReadOnlyList<Position> actions, IReadOnlyList<(Position, UnitType)> arrivals,
            IReadOnlyList<UnitForecast> units, BattlePhase phase)
        {
            (Actions, Arrivals, Units, Phase) = (actions, arrivals, units, phase);
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

    public sealed partial class Expedition
    {
        public const int Rows = 2;
        public const int Slots = 3;
        public const int HandSize = 6;
        public const int RefreshPeriod = 4;
        private const int RecallHealing = 5;

        private Unit[,,] board = new Unit[2, Rows, Slots];
        private uint targetRandom = 0x890ca3a1;
        private uint shuffleRandom = 2471;
        private int extraAllyTicks;
        private bool advancingAllies;
        private List<Card> draw;
        private readonly Card captainCard;
        private readonly Card junkCard;
        private readonly Card stickyFruit;
        private List<Card> discard = new();
        private List<Card> hand = new();

        public BattlePhase Phase { get; private set; }
        public int Turn { get; private set; } = 1;
        public int JunkPerTurn { get; private set; }
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

        internal Expedition(Encounter location, IEnumerable<Card> deck, Card captain, Card companion, Card junk = null, Card fruit = null)
        {
            Location = location;
            Reinforcements = location.WaveInterval;
            draw = new List<Card>(deck);
            captainCard = captain;
            junkCard = junk;
            stickyFruit = fruit;
            hand.Add(captainCard);
            Companion = companion?.Unit;
            if (Companion != null)
            {
                hand.Add(companion);
                Notice = "免费部署队长和随从，随后抽取开局手牌。";
            }
            var initialEnemies = new List<Position>(PlaceEnemies(location.UnitsInWave(1)));
            foreach (Position position in initialEnemies)
                if (At(position) is Unit unit)
                    foreach (var beat in TriggerAbility(position, position, unit, EffectTrigger.WhileActive)) { }
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
                CardAction.ApplyEffect when card.Effect is EffectKind.AttackGrowth or EffectKind.HealthGrowth => !pos.Enemy && unit != null,
                CardAction.ApplyEffect => pos.Enemy == card.TargetsEnemy && unit != null &&
                    (card.Effect != EffectKind.EffectBonus || unit.Type.Ability.Effect != EffectKind.None),
                CardAction.Shift => pos.Enemy && unit != null && ShiftDestination(pos).HasValue,
                CardAction.Technique => TechniqueTargets(card, pos).Count > 0 && CanSpendJunk(card),
                _ => false
            };
        }

        // Return every possible target for Aimless. The real attack rolls once per hit.
        public IReadOnlyList<Position> AttackTargets(Position source, UnitType type)
        {
            AttackPattern pattern = At(source)?.Pattern ?? type.Pattern;
            if (pattern == AttackPattern.AllEnemies)
                return PositionsOnSide(!source.Enemy);
            if (pattern == AttackPattern.Support)
            {
                for (int slot = 0; slot < Slots; slot++)
                {
                    var ally = new Position(source.Enemy, source.Row, slot);
                    if (!ally.Equals(source) && At(ally) != null)
                        return new[] { ally };
                }
                return Array.Empty<Position>();
            }

            var targets = new List<Position>(Slots);
            int row = Front(!source.Enemy, source.Row).HasValue ? source.Row : 1 - source.Row;
            for (int slot = 0; slot < Slots; slot++)
            {
                var pos = new Position(!source.Enemy, row, slot);
                if (At(pos) != null)
                    targets.Add(pos);
            }

            if (targets.Count <= 1 || pattern is AttackPattern.Barrage or AttackPattern.Aimless)
                return targets;

            int index = pattern == AttackPattern.Longshot ? targets.Count - 1 : 0;
            return new[] { targets[index] };
        }

        public IReadOnlyList<Position> CardTargets(int index, Position anchor)
        {
            if (!CanPlay(index, anchor))
                return Array.Empty<Position>();

            Card card = hand[index];
            if (card.Action == CardAction.Technique)
                return TechniqueTargets(card, anchor);
            if (card.Action == CardAction.Shift && ShiftDestination(anchor) is Position destination)
                return new[] { anchor, destination };
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

        private Position? ShiftDestination(Position source)
        {
            for (int slot = 0; slot < Slots; slot++)
            {
                var destination = new Position(source.Enemy, 1 - source.Row, slot);
                if (At(destination) == null)
                    return destination;
            }
            return null;
        }

        public (IReadOnlyList<Position> Actions, IReadOnlyList<(Position Position, UnitType Unit)> Arrivals) ForecastTurn(
            int cardIndex, Position? target)
        {
            var preview = PreviewTurn(cardIndex, target);
            return (preview.Actions, preview.Arrivals);
        }

        public TurnPreview PreviewTurn(int cardIndex, Position? target)
        {
            var actions = new List<Position>();
            var arrivals = new List<(Position, UnitType)>();
            var units = new List<UnitForecast>();
            if (Opening || Phase != BattlePhase.Planning)
                return new TurnPreview(actions, arrivals, units, Phase);

            var forecast = (Expedition)MemberwiseClone();
            forecast.board = new Unit[2, Rows, Slots];
            forecast.hand = hand.ConvertAll(card => card.CopyForForecast());
            forecast.draw = draw.ConvertAll(card => card.CopyForForecast());
            forecast.discard = discard.ConvertAll(card => card.CopyForForecast());
            var origins = new List<(Position Position, Unit Unit)>();
            foreach (Position pos in TurnOrder())
            {
                if (At(pos) is not Unit live)
                    continue;

                Unit copy = live.Copy();
                origins.Add((pos, copy));
                forecast.Set(pos, copy);
            }

            IEnumerable<BattleBeat> steps;
            if (target.HasValue && CanPlay(cardIndex, target.Value))
            {
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
            var destinations = new Dictionary<Unit, Position>();
            foreach (Position pos in TurnOrder())
                if (forecast.At(pos) is Unit survivor)
                    destinations.Add(survivor, pos);
            foreach (var origin in origins)
            {
                Position? destination = destinations.TryGetValue(origin.Unit, out Position pos) ? pos : null;
                units.Add(new UnitForecast(origin.Position, destination, origin.Unit));
            }
            return new TurnPreview(actions, arrivals, units, forecast.Phase);
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
                    if (At(target) == deployed)
                        foreach (BattleBeat effect in TriggerAbility(target, target, deployed, EffectTrigger.WhileActive))
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
                    var impact = Hit(target, target, card.DamageAgainst(unit), card.Name);
                    yield return impact;
                    foreach (BattleBeat reaction in ResolveBlockReaction(impact))
                        yield return reaction;
                    foreach (BattleBeat burst in CheckOverload(target))
                        yield return burst;
                    foreach (BattleBeat reaction in ResolveItemHit(target, card.IsJunk))
                        yield return reaction;
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
                case CardAction.Shift:
                    Position destination = ShiftDestination(target).Value;
                    Set(target, null);
                    Set(destination, unit);
                    Notice = $"{unit.Type.Name}被移到{(destination.Row == 0 ? "上排" : "下排")}。";
                    yield return new BattleBeat(target, destination, BattleEffect.Status, "换排");
                    break;
                case CardAction.Technique:
                    foreach (BattleBeat effect in PlayTechnique(card, target))
                        yield return effect;
                    break;
            }
            DiscardAfterUse(card);
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

            foreach (var beat in AdvanceCountdowns())
                yield return beat;
            if (Phase == BattlePhase.Lost)
                yield break;

            advancingAllies = true;
            int queuedTicks = extraAllyTicks;
            extraAllyTicks = 0;
            try
            {
                for (int i = 0; i < queuedTicks && Phase != BattlePhase.Lost && HasEnemies(); i++)
                    foreach (var beat in AdvanceCountdowns(alliesOnly: true))
                        yield return beat;
            }
            finally
            {
                advancingAllies = false;
            }
            if (Phase == BattlePhase.Lost)
                yield break;

            foreach (BattleBeat burn in ResolveBurns())
                yield return burn;
            foreach (BattleBeat poison in ResolvePoison())
                yield return poison;
            foreach (Position pos in TurnOrder())
            {
                if (Phase == BattlePhase.Lost || (Wave == Location.WaveCount && !HasEnemies()))
                    break;
                if (At(pos) is Unit unit)
                {
                    foreach (var beat in ResolveScheduledHealing(pos, unit))
                        yield return beat;
                    foreach (var beat in TriggerAbility(pos, pos, unit, EffectTrigger.TurnEnd))
                        yield return beat;
                    if (At(pos) == unit && unit.Health < unit.MaxHealth)
                        foreach (var beat in TriggerAbility(pos, pos, unit, EffectTrigger.WhileWounded))
                            yield return beat;
                }
            }
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
                if (Reinforcements == 0 && HasRoomForNextWave())
                {
                    foreach (BattleBeat beat in Reinforce())
                        yield return beat;
                }
            }

            Turn++;
            Phase = Wave == Location.WaveCount && !HasEnemies() ? BattlePhase.Won : BattlePhase.Planning;
            if (Phase == BattlePhase.Planning)
                GenerateJunk(JunkPerTurn);
        }

        private IEnumerable<BattleBeat> AdvanceCountdowns(bool alliesOnly = false)
        {
            var snowedAtCountdown = new List<Unit>();
            foreach (Position pos in TurnOrder())
            {
                if (alliesOnly && pos.Enemy)
                    continue;
                Unit attacker = At(pos);
                if (attacker == null)
                    continue;

                if (attacker.Frozen > 0)
                {
                    snowedAtCountdown.Add(attacker);
                    continue;
                }

                if (attacker.Period == 0)
                    continue;

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
        }

        private IEnumerable<BattleBeat> Trigger(Position source, Unit attacker, int maxHits = int.MaxValue)
        {
            attacker.Counter = attacker.Period;
            if (attacker.Pattern == AttackPattern.Support)
            {
                var allies = AttackTargets(source, attacker.Type);
                if (attacker.Ink == 0 && allies.Count != 0)
                {
                    Position target = allies[0];
                    Notice = $"{attacker.Type.Name}准备援护{At(target).Type.Name}。";
                    yield return new BattleBeat(source, target, BattleEffect.Windup, "援护");
                    foreach (BattleBeat effect in TriggerAbility(source, target, attacker, EffectTrigger.Act))
                        yield return effect;
                }
                yield break;
            }

            bool attacked = false;
            int hits = Math.Min(attacker.Frenzy, maxHits);
            for (int hit = 1; hit <= hits && attacker.Frozen == 0; hit++)
            {
                IReadOnlyList<Position> targets = AttackTargets(source, attacker.Type);
                if (targets.Count == 0)
                    break;

                attacked = true;
                if (attacker.Pattern == AttackPattern.Aimless && targets.Count > 1)
                    targets = new[] { targets[RollTarget(targets.Count)] };

                string action = hits > 1 ? $"狂热 {hit}/{hits}" :
                    attacker.Pattern == AttackPattern.Front ? "攻击" : PatternName(attacker.Pattern);
                Notice = $"{attacker.Type.Name} · {action}";
                yield return new BattleBeat(source, targets[0], BattleEffect.Windup, action, targets);

                int defeated = 0;
                int attackPower = attacker.Attack;
                var marks = new List<string>(targets.Count);
                var blocked = new List<BattleBeat>();
                foreach (Position target in targets)
                {
                    var impact = Hit(source, target, UnitAttackDamage(attacker, At(target), attackPower), attacker.Type.Name);
                    marks.Add(impact.Mark);
                    if (impact.Blocked)
                        blocked.Add(impact);
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
                {
                    foreach (BattleBeat effect in TriggerAbility(source, source, attacker, EffectTrigger.Kill, defeated))
                        yield return effect;
                    foreach (BattleBeat effect in RewardAllyKill(source, defeated))
                        yield return effect;
                }

                foreach (var impact in blocked)
                    foreach (var reaction in ResolveBlockReaction(impact))
                        yield return reaction;
                if (Phase == BattlePhase.Lost || At(source) == null)
                    yield break;
                if (HasActiveAbility(attacker, EffectTrigger.Attack, EffectKind.SelfAttack))
                {
                    var impact = Hit(source, source, attackPower, attacker.Type.Name);
                    yield return impact;
                    foreach (var burst in CheckOverload(source))
                        yield return burst;
                }
                else
                {
                    foreach (var effect in TriggerAbility(source, source, attacker, EffectTrigger.Attack))
                        yield return effect;
                }
                if (Phase == BattlePhase.Lost || At(source) == null)
                    yield break;
                foreach (BattleBeat reaction in ResolveReactions(source, targets, attackPower > 0))
                    yield return reaction;
                if (Phase == BattlePhase.Lost || At(source) == null)
                    yield break;
            }

            if (attacked)
            {
                attacker.Spice = 0;
                attacker.Weakness = 0;
                foreach (BattleBeat effect in TriggerAbility(source, source, attacker, EffectTrigger.Act))
                    yield return effect;
                if (HasActiveAbility(attacker, EffectTrigger.Act, EffectKind.MaxCountdownUp))
                    attacker.Counter = attacker.Period;
            }
        }

        private IEnumerable<BattleBeat> ResolveReactions(Position attacker, IReadOnlyList<Position> targets, bool triggersThorns)
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

                int thorns = defender.Thorns;
                if (thorns == 0 || !triggersThorns)
                    continue;
                if (defender.Thorns > 0)
                    defender.Thorns--;
                var impact = Hit(target, attacker, thorns, "反伤", BattleEffect.Retaliation);
                yield return impact;
                foreach (var reaction in ResolveBlockReaction(impact))
                    yield return reaction;
                if (At(attacker) == null && At(target) != null && Phase != BattlePhase.Lost)
                    foreach (BattleBeat effect in RewardAllyKill(target, 1))
                        yield return effect;
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

                var impact = Hit(pos, pos, unit.Burn--, "灼烧", BattleEffect.Burning);
                yield return impact;
                foreach (var reaction in ResolveBlockReaction(impact))
                    yield return reaction;
                foreach (BattleBeat burst in CheckOverload(pos))
                    yield return burst;
            }
        }

        private IEnumerable<BattleBeat> ResolvePoison()
        {
            foreach (Position pos in TurnOrder())
            {
                if (Phase == BattlePhase.Lost)
                    yield break;
                Unit unit = At(pos);
                if (unit == null || unit.Poison <= 0)
                    continue;
                int amount = unit.Poison--;
                unit.Health = Math.Max(0, unit.Health - amount);
                RemoveIfDefeated(pos, unit);
                if (Phase != BattlePhase.Lost)
                    Notice = $"{unit.Type.Name}因中毒失去 {amount} 点生命。";
                yield return new BattleBeat(pos, pos, BattleEffect.Status, $"中毒 −{amount}");
                foreach (BattleBeat burst in CheckOverload(pos))
                    yield return burst;
            }
        }

        private IEnumerable<BattleBeat> ApplySnowToOpponents(Position source, int amount)
        {
            foreach (Position enemy in PositionsOnSide(!source.Enemy))
                foreach (var beat in ApplyEffect(source, enemy, EffectKind.Snow, amount))
                    yield return beat;
        }

        private IEnumerable<BattleBeat> ResolveBlockReaction(BattleBeat impact)
        {
            if (!impact.Blocked || Phase == BattlePhase.Lost || At(impact.Target) is not Unit defender ||
                defender.Ink > 0 || defender.Type.Ability.Effect != EffectKind.SnowOnBlock)
                yield break;
            foreach (var beat in ApplySnowToOpponents(impact.Target, defender.Type.Ability.Amount + defender.AbilityBonus))
                yield return beat;
            if (defender.Frozen > 0 || impact.Source.Enemy == impact.Target.Enemy || At(impact.Source) == null)
                yield break;
            var retaliation = Hit(impact.Target, impact.Source, defender.Attack, defender.Type.Name, BattleEffect.Retaliation);
            yield return retaliation;
            foreach (var reaction in ResolveBlockReaction(retaliation))
                yield return reaction;
            if (At(impact.Source) == null && Phase != BattlePhase.Lost)
                foreach (var reward in RewardAllyKill(impact.Target, 1))
                    yield return reward;
            foreach (var burst in CheckOverload(impact.Source))
                yield return burst;
        }

        private IEnumerable<BattleBeat> TriggerAbility(Position source, Position target, Unit unit, EffectTrigger trigger, int times = 1)
        {
            UnitAbility ability = unit.Type.Ability;
            if (unit.Ink > 0 || ability.Effect == EffectKind.None || ability.Trigger != trigger)
                return Array.Empty<BattleBeat>();
            if (ability.Effect is EffectKind.Frenzy or EffectKind.Draw or EffectKind.Junk or EffectKind.RecycleDraw or EffectKind.RandomStat)
                target = source;
            if ((ability.Effect is EffectKind.AttackUp or EffectKind.AttackDown or EffectKind.TeamSpice or EffectKind.StickyFruit)
                && trigger == EffectTrigger.WhenHit)
                target = source;
            if ((ability.Effect == EffectKind.Retaliate || ability.Effect == EffectKind.Snow && trigger == EffectTrigger.WhenHit)
                && source.Equals(target))
                return Array.Empty<BattleBeat>();
            int amount = (ability.Amount + unit.AbilityBonus) * times;
            if (trigger == EffectTrigger.WhileActive && ability.Effect is EffectKind.AttackUp or EffectKind.Thorns or EffectKind.JunkPower)
                return ApplyEntryBuff(source, unit, amount);
            if (ability.Effect == EffectKind.DamageVsLowerHealth)
                return Array.Empty<BattleBeat>();
            if (ability.Effect is EffectKind.MaxCountdownUp or EffectKind.TeamHealth or EffectKind.WeakenRow or
                EffectKind.AbsorbRow or EffectKind.HurtForFrenzy or EffectKind.ExtraAllyTick)
                return ResolveUnitAbility(source, unit, amount);
            return ApplyEffect(source, target, ability.Effect, amount);
        }

        public static string PatternName(AttackPattern pattern) => pattern switch
        {
            AttackPattern.Barrage => "横扫",
            AttackPattern.Longshot => "远射",
            AttackPattern.Aimless => "无法瞄准",
            AttackPattern.Support => "援护",
            AttackPattern.AllEnemies => "攻击全体敌军",
            _ => "普通攻击"
        };

        private int RollTarget(int count)
            => Roll(ref targetRandom, count);

        private static int Roll(ref uint state, int count)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (int)(state % (uint)count);
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
                    if (amount > 0)
                        GainAttackOnEvent(EffectTrigger.SnowApplied);
                    feedback = BattleEffect.Ice;
                    break;
                case EffectKind.Spice: unit.Spice += amount; break;
                case EffectKind.TeamSpice:
                    foreach (Position ally in TurnOrder())
                        if (ally.Enemy == target.Enemy && At(ally) is Unit teammate)
                            teammate.Spice += amount;
                    break;
                case EffectKind.StickyFruit:
                    if (stickyFruit == null)
                        yield break;
                    for (int i = 0; i < amount; i++)
                        hand.Add(stickyFruit);
                    break;
                case EffectKind.Retaliate:
                    if (At(source) is not Unit counterattacker || source.Enemy == target.Enemy || counterattacker.Frozen > 0)
                        yield break;
                    var retaliation = Hit(source, target, counterattacker.Attack, counterattacker.Type.Name, BattleEffect.Retaliation);
                    yield return retaliation;
                    foreach (var reaction in ResolveBlockReaction(retaliation))
                        yield return reaction;
                    foreach (BattleBeat burst in CheckOverload(target))
                        yield return burst;
                    if (At(target) == null && Phase != BattlePhase.Lost)
                        foreach (BattleBeat reward in RewardAllyKill(source, 1))
                            yield return reward;
                    yield break;
                case EffectKind.Overload: unit.Overload += amount; break;
                case EffectKind.Ink: unit.Ink += amount; break;
                case EffectKind.Weakness: unit.Weakness += amount; break;
                case EffectKind.Block: unit.Block += amount; break;
                case EffectKind.EffectBonus: unit.EffectBonus += amount; break;
                case EffectKind.Thorns: unit.Thorns += amount; break;
                case EffectKind.Burn: unit.Burn += amount; break;
                case EffectKind.Poison: unit.Poison += amount; break;
                case EffectKind.Corruption: unit.Corruption += amount; break;
                case EffectKind.Exposed: unit.Exposed += amount; break;
                case EffectKind.AttackUp: unit.BattleAttack += amount; break;
                case EffectKind.AttackDown: unit.BattleAttack -= amount; break;
                case EffectKind.Frenzy: unit.BonusFrenzy += amount; break;
                case EffectKind.Aimless: unit.Aimless = true; break;
                case EffectKind.Barrage: unit.Barrage = true; break;
                case EffectKind.AllEnemiesPattern: unit.AllEnemies = true; break;
                case EffectKind.Countdown: unit.Counter = Math.Max(0, unit.Counter - amount); break;
                case EffectKind.CountdownUp:
                    if (unit.Period == 0) yield break;
                    unit.Counter += amount;
                    break;
                case EffectKind.MaxCountdown:
                    if (unit.Period == 0) yield break;
                    unit.CounterReduction += Math.Min(amount, unit.Period - 1);
                    unit.Counter = Math.Min(unit.Counter, unit.Period);
                    break;
                case EffectKind.MaxCountdownUp:
                    if (unit.Period == 0) yield break;
                    unit.CounterReduction -= amount;
                    break;
                case EffectKind.BattleHealthUp:
                    unit.BattleHealth += amount;
                    unit.Health += amount;
                    feedback = BattleEffect.Recovery;
                    break;
                case EffectKind.BattleHealthDown:
                    amount = Math.Min(amount, unit.MaxHealth - 1);
                    unit.BattleHealth -= amount;
                    unit.Health = Math.Min(unit.Health, unit.MaxHealth);
                    break;
                case EffectKind.SnowOnBlock:
                    foreach (var beat in ApplySnowToOpponents(source, amount))
                        yield return beat;
                    yield break;
                case EffectKind.Draw:
                    if (target.Enemy) yield break;
                    DrawCards(amount);
                    break;
                case EffectKind.Junk:
                    if (target.Enemy) yield break;
                    GenerateJunk(amount);
                    break;
                case EffectKind.RecycleDraw:
                    if (target.Enemy) yield break;
                    int destroyed = hand.RemoveAll(card => card.IsJunk);
                    GainAttackOnEvent(EffectTrigger.CardDestroyed, destroyed);
                    DrawCards(destroyed * amount);
                    break;
                case EffectKind.RandomStat:
                    foreach (BattleBeat boost in BoostRandomAlly(source, amount))
                        yield return boost;
                    yield break;
                case EffectKind.Cleanse:
                    unit.Frozen = unit.Ink = unit.Weakness = unit.Overload = unit.Burn =
                        unit.Exposed = unit.Poison = unit.Corruption = 0;
                    unit.Aimless = false;
                    feedback = BattleEffect.Recovery;
                    break;
                case EffectKind.Hurt:
                    unit.Health = Math.Max(0, unit.Health - amount);
                    RemoveIfDefeated(target, unit);
                    break;
                case EffectKind.AttackGrowth:
                    if (target.Enemy)
                        yield break;
                    unit.Progress.IncreaseAttack(amount);
                    break;
                case EffectKind.HealthGrowth:
                    if (target.Enemy)
                        yield break;
                    unit.Progress.IncreaseHealth(amount);
                    unit.Health += amount;
                    feedback = BattleEffect.Recovery;
                    break;
                default: yield break;
            }

            string label = effect switch
            {
                EffectKind.Snow when amount == 0 => "抗雪球",
                EffectKind.Aimless or EffectKind.Barrage or EffectKind.Cleanse or EffectKind.AllEnemiesPattern => Effects.Name(effect),
                EffectKind.Countdown or EffectKind.MaxCountdown or EffectKind.AttackDown or EffectKind.BattleHealthDown => $"{Effects.Name(effect)} −{amount}",
                EffectKind.TeamSpice => $"全体友军辣椒 +{amount}",
                EffectKind.Hurt => $"生命 −{amount}",
                _ => $"{Effects.Name(effect)} +{amount}"
            };
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
            var blocked = new List<BattleBeat>();
            foreach (Position target in targets)
            {
                var impact = Hit(origin, target, damage, "过载");
                marks.Add(impact.Mark);
                if (impact.Blocked)
                    blocked.Add(impact);
            }
            Notice = Phase == BattlePhase.Lost ? $"{Captain.Name}倒下。" : $"{unit.Type.Name}过载引爆 · 同排伤害 {damage}";
            yield return new BattleBeat(origin, origin, BattleEffect.Burst, "过载", targets, impactMarks: marks);
            foreach (var impact in blocked)
                foreach (var reaction in ResolveBlockReaction(impact))
                    yield return reaction;

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
            var positions = new List<Position>(units.Count);
            foreach (Position position in PlaceEnemies(units))
            {
                positions.Add(position);
                yield return new BattleBeat(position, position, BattleEffect.Arrival, "增援");
            }
            foreach (Position position in positions)
                if (At(position) is Unit unit)
                    foreach (var beat in TriggerAbility(position, position, unit, EffectTrigger.WhileActive))
                        yield return beat;
        }

        private IEnumerable<Position> PlaceEnemies(IReadOnlyList<EnemySpawn> enemies)
        {
            foreach (EnemySpawn enemy in enemies)
            {
                int slot = 0;
                while (slot < Slots && At(new Position(true, enemy.Row, slot)) != null)
                    slot++;
                if (slot == Slots)
                    throw new InvalidOperationException("The encounter schedules more enemies than there are slots.");
                var position = new Position(true, enemy.Row, slot);
                Set(position, new Unit(enemy.Unit));
                yield return position;
            }
        }

        private BattleBeat Hit(Position source, Position target, int amount, string name, BattleEffect feedback = BattleEffect.Attack)
        {
            Unit victim = At(target);
            if (amount > 0 && victim.Block > 0)
            {
                victim.Block--;
                Notice = $"{victim.Type.Name}消耗 1 层格挡，抵消 {amount} 点伤害。";
                return new BattleBeat(source, target, feedback, "格挡 −1", blocked: true);
            }

            int exposed = amount > 0 ? victim.Exposed : 0;
            bool corrupted = amount > 0 && victim.Corruption > 0;
            int adjusted = AdjustIncomingDamage(victim, amount);
            if (amount > 0)
            {
                victim.Exposed = 0;
                if (corrupted)
                    victim.Corruption--;
            }

            int absorbed = Math.Min(victim.Shell, adjusted);
            victim.Shell -= absorbed;
            int healthDamage = adjusted - absorbed;
            victim.Health = Math.Max(0, victim.Health - healthDamage);
            Notice = $"{name}对{victim.Type.Name}造成 {healthDamage} 点伤害。";
            if (exposed > 0)
                Notice += $"易伤 +{exposed}。";
            if (corrupted)
                Notice += "腐化使伤害翻倍，腐化 −1。";
            if (absorbed > 0)
                Notice += $"护壳吸收 {absorbed} 点。";
            if (victim.Health == 0)
            {
                RemoveIfDefeated(target, victim);
                if (!victim.Leader)
                    Notice += "目标退场。";
            }

            string mark = healthDamage > 0 ? $"−{healthDamage}" : absorbed > 0 ? $"护壳 −{absorbed}" : "0";
            return new BattleBeat(source, target, feedback, mark);
        }

        private static int AdjustIncomingDamage(Unit victim, int amount) =>
            amount <= 0 ? 0 : (amount + victim.Exposed) * (victim.Corruption > 0 ? 2 : 1);

        public static int PreviewHealthDamage(Unit victim, int amount) =>
            victim.Block > 0 ? 0 : Math.Min(victim.Health, Math.Max(0, AdjustIncomingDamage(victim, amount) - victim.Shell));

        private static int UnitAttackDamage(Unit attacker, Unit victim, int attackPower) =>
            attacker.Ink == 0 && attacker.Type.Ability.Effect == EffectKind.DamageVsLowerHealth && victim.Health < attacker.Health
                ? attackPower * (attacker.Type.Ability.Amount + attacker.AbilityBonus)
                : attackPower;

        private void FillHand() => DrawCards(Math.Max(0, HandSize - hand.Count));

        private void DrawCards(int count)
        {
            while (count-- > 0 && draw.Count + discard.Count > 0)
            {
                if (draw.Count == 0)
                {
                    draw.AddRange(discard);
                    discard.Clear();
                    for (int i = draw.Count - 1; i > 0; i--)
                    {
                        int other = Roll(ref shuffleRandom, i + 1);
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

        private bool HasRoomForNextWave()
        {
            for (int row = 0; row < Rows; row++)
            {
                int required = 0;
                foreach (EnemySpawn enemy in NextWave)
                    if (enemy.Row == row)
                        required++;
                if (required + OccupiedRow(new Position(true, row, 0)).Count > Slots)
                    return false;
            }
            return true;
        }

        private void Set(Position pos, Unit unit)
        {
            board[pos.Enemy ? 1 : 0, pos.Row, pos.Slot] = unit;
        }
    }
}
