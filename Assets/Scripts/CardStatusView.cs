using System.Collections.Generic;
using System.Linq;

namespace Frostline
{
    // Card chips and the hover inspector read the same live unit values.
    internal readonly struct CardStatus
    {
        public readonly EffectKind Effect;
        public readonly int Amount;
        public readonly string Rule;
        public string Name => CardStatusView.Name(Effect);
        public string Icon => CardStatusView.Icon(Effect);
        public string Value => Effect == EffectKind.Frenzy ? $"×{Amount}" :
            Effect is EffectKind.TemporaryAttack or EffectKind.TerrainAttackUp ? $"{Amount:+0;-0}" : Amount.ToString();
        public string Summary => $"{Name} {Value} · {Rule}";

        public CardStatus(EffectKind effect, int amount, string rule = null)
        {
            Effect = effect;
            Amount = amount;
            Rule = rule ?? Effects.Rule(effect);
        }
    }

    internal static class CardStatusView
    {
        public const string ResourceRoot = "CardArt/Interface/";
        public const int CompactLimit = 4;

        public static IEnumerable<CardStatus> Read(Unit unit)
        {
            if (unit.DamageThisTurn + unit.DamageNextTurn > 0)
                yield return new CardStatus(EffectKind.DelayedDamage, unit.DamageThisTurn + unit.DamageNextTurn,
                    $"本回合末受到 {unit.DamageThisTurn} 点伤害；下回合末受到 {unit.DamageNextTurn} 点伤害。");
            if (unit.Frozen > 0)
                yield return new CardStatus(EffectKind.Snow, unit.Frozen, "暂停倒计时和反击，每拍减少 1 层。");
            if (unit.Overload > 0)
                yield return new CardStatus(EffectKind.Overload, unit.Overload,
                    $"达到当前生命 {unit.Health} 时引爆。" + Effects.Rule(EffectKind.Overload));
            foreach (var (effect, amount) in new[]
            {
                (EffectKind.Poison, unit.Poison), (EffectKind.Corruption, unit.Corruption),
                (EffectKind.Haze, unit.Haze), (EffectKind.Ink, unit.Ink),
                (EffectKind.Weakness, unit.Weakness), (EffectKind.Burn, unit.Burn),
                (EffectKind.Exposed, unit.Exposed)
            })
                if (amount > 0) yield return new CardStatus(effect, amount);
            if (unit.Shell > 0)
                yield return new CardStatus(EffectKind.Shell, unit.Shell, "受到伤害时先扣护壳，再扣生命；格挡优先于护壳。");
            if (unit.Block > 0) yield return new CardStatus(EffectKind.Block, unit.Block);
            if (unit.Spice > 0) yield return new CardStatus(EffectKind.Spice, unit.Spice);
            if (unit.PoisonOnAttack > 0) yield return new CardStatus(EffectKind.PoisonAttack, unit.PoisonOnAttack);
            if (unit.TemporaryAttack > 0)
                yield return new CardStatus(EffectKind.TemporaryAttack, unit.TemporaryAttack);
            if (unit.TotalThorns > 0 || unit.ThornsDivisor > 0)
                yield return new CardStatus(EffectKind.Thorns, unit.TotalThorns, unit.ThornsDivisor > 0
                    ? $"当前攻击 {unit.Attack} ÷ {unit.ThornsDivisor}，向下取整，再加其他反伤。" + Effects.Rule(EffectKind.Thorns)
                    : null);
            if (unit.EffectBonus > 0) yield return new CardStatus(EffectKind.EffectBonus, unit.EffectBonus);
            if (unit.Frenzy > 1)
                yield return new CardStatus(EffectKind.Frenzy, unit.Frenzy, $"每次行动连续攻击 {unit.Frenzy} 次。");
            if (unit.PermanentJunk > 0)
                yield return new CardStatus(EffectKind.PermanentJunk, unit.PermanentJunk,
                    $"目标在场时，每回合开始向你的手牌生成 {unit.PermanentJunk} 张垃圾。");
            if (unit.TerrainAttack != 0)
                yield return new CardStatus(EffectKind.TerrainAttackUp, unit.TerrainAttack, "当前站位提供的攻击修正，换位后按新格子重新计算。");
            if (unit.ScheduledHealing.Count > 0)
            {
                var pulses = unit.ScheduledHealing.GroupBy(pulse => pulse.Turn).OrderBy(group => group.Key).ToArray();
                yield return new CardStatus(EffectKind.Heal, pulses[0].Sum(pulse => pulse.Amount),
                    string.Join("；", pulses.Select(group => $"第 {group.Key} 回合末恢复 {group.Sum(pulse => pulse.Amount)} 点生命")) + "。");
            }
        }

        public static IEnumerable<CardStatus> Read(Card card)
        {
            var seen = new HashSet<EffectKind>();
            foreach (var (effect, amount) in new[]
            {
                (card.Effect, card.Amount),
                (card.Technique?.Secondary ?? EffectKind.None, card.Technique?.SecondaryAmount ?? 0),
                (card.Technique?.ThirdEffect ?? EffectKind.None, card.Technique?.ThirdAmount ?? 0)
            })
            {
                string rule = Effects.Rule(effect);
                if (rule.Length > 0 && seen.Add(effect))
                    yield return new CardStatus(effect, amount, rule);
            }
        }

        public static IEnumerable<CardStatus> Read(TerrainTile tile)
        {
            if (tile.Attack != 0) yield return new CardStatus(EffectKind.TerrainAttackUp, tile.Attack);
            if (tile.TemporaryAttack > 0) yield return new CardStatus(EffectKind.TerrainTemporaryAttack, tile.TemporaryAttack);
            if (tile.Poison > 0) yield return new CardStatus(EffectKind.TerrainPoison, tile.Poison);
            if (tile.Weakness > 0)
                yield return new CardStatus(EffectKind.TerrainWeakness, tile.Weakness);
            if (tile.Spice > 0)
                yield return new CardStatus(EffectKind.TerrainSpice, tile.Spice);
            if (tile.Frenzy > 0)
                yield return new CardStatus(EffectKind.TerrainFrenzy, tile.Frenzy, "占格单位获得格子提供的狂热，换位后留在原格。");
        }

        public static string Name(EffectKind effect) => effect switch
        {
            EffectKind.TemporaryAttack => "临时攻击",
            EffectKind.TerrainAttackUp => "地形攻击",
            EffectKind.EffectBonus => "效果增强",
            EffectKind.Heal => "持续恢复",
            EffectKind.TerrainTemporaryAttack => "格子临时攻击",
            EffectKind.TerrainPoison => "格子中毒",
            EffectKind.TerrainAttackDown => "格子攻击",
            EffectKind.GroundPoison => "中毒转移",
            EffectKind.TerrainFrenzy => "格子狂热",
            _ => Effects.Name(effect)
        };

        public static string ColorClass(EffectKind effect) => effect switch
        {
            EffectKind.Snow => "effect-snow", EffectKind.Shell => "effect-shell",
            EffectKind.Spice or EffectKind.TeamSpice or EffectKind.TerrainSpice => "effect-spice",
            EffectKind.Overload or EffectKind.MutualOverload or EffectKind.OverloadDamage => "effect-overload",
            EffectKind.Ink => "effect-ink", EffectKind.Weakness or EffectKind.TerrainWeakness => "effect-weakness",
            EffectKind.Block => "effect-block", EffectKind.Thorns => "effect-thorns",
            EffectKind.Burn => "effect-burn", EffectKind.Poison or EffectKind.PoisonAttack => "effect-poison",
            EffectKind.Corruption => "effect-corruption", EffectKind.Exposed => "effect-exposed",
            EffectKind.Haze => "effect-haze", EffectKind.DelayedDamage => "effect-delayed",
            EffectKind.PermanentJunk => "effect-junk", EffectKind.TemporaryAttack => "effect-temporary",
            EffectKind.Frenzy => "effect-frenzy", EffectKind.TerrainAttackUp => "effect-terrain",
            _ => "effect-bonus"
        };

        public static string Icon(EffectKind effect) => effect switch
        {
            EffectKind.Heal or EffectKind.Cleanse or EffectKind.PoisonTreatment => "heal",
            EffectKind.Snow or EffectKind.SnowOnBlock or EffectKind.SnowAndAttackUp => "snow",
            EffectKind.Shell => "shell", EffectKind.Spice or EffectKind.TeamSpice or EffectKind.TerrainSpice => "spice",
            EffectKind.Overload or EffectKind.MutualOverload or EffectKind.OverloadDamage or EffectKind.OverloadOpposite or EffectKind.RowBlast => "overload",
            EffectKind.Ink => "ink", EffectKind.Weakness or EffectKind.WeakenRow or EffectKind.TerrainWeakness => "weakness",
            EffectKind.Block => "block", EffectKind.Thorns => "barbs", EffectKind.Burn => "burn",
            EffectKind.Poison or EffectKind.TerrainPoison or EffectKind.TerrainDetox or EffectKind.GroundPoison or EffectKind.PoisonBurst or EffectKind.PoisonAttack => "poison",
            EffectKind.Corruption => "corruption", EffectKind.Exposed => "exposed", EffectKind.Haze => "haze",
            EffectKind.DelayedDamage => "delayed", EffectKind.TemporaryAttack or EffectKind.TerrainTemporaryAttack => "temporary",
            EffectKind.Frenzy or EffectKind.HurtForFrenzy or EffectKind.TerrainFrenzy => "frenzy",
            EffectKind.Draw or EffectKind.RecycleDraw or EffectKind.ExtraAllyTick => "draw",
            EffectKind.Junk or EffectKind.JunkPower or EffectKind.PermanentJunk => "junk",
            EffectKind.Retaliate => "counter",
            EffectKind.HalfCounterattack => "barbs",
            EffectKind.Summon or EffectKind.SummonBoost or EffectKind.CopyAlly or EffectKind.MimicEnemy => "summon",
            EffectKind.Countdown or EffectKind.MaxCountdown or EffectKind.MaxCountdownUp or EffectKind.CountdownUp => "timer",
            EffectKind.HealthGrowth or EffectKind.BattleHealthUp or EffectKind.BattleHealthDown or EffectKind.TeamHealth or EffectKind.DrainAlly or EffectKind.AbsorbRow or EffectKind.DevourRow => "health",
            EffectKind.TerrainAnchor or EffectKind.TerrainAffinity or EffectKind.TerrainAbsorb or EffectKind.TerrainGrowth or
                EffectKind.TerrainTransfer or EffectKind.TerrainCannon or EffectKind.TerrainGambit or
                EffectKind.TerrainAttackUp or EffectKind.TerrainAttackDown or EffectKind.TerrainSwap => "terrain",
            EffectKind.AttackUp or EffectKind.AttackDown or EffectKind.AttackGrowth or EffectKind.TriggerAttack or
                EffectKind.DamageVsLowerHealth or EffectKind.SelfAttack or EffectKind.HealthAttackUp => "attack",
            EffectKind.Sacrifice => "consume",
            _ => "bonus"
        };
    }
}
