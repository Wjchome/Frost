using System;
using System.Collections.Generic;
using System.Linq;

namespace Frostline
{
    public sealed partial class Expedition
    {
        public int JunkAttackBonus => TurnOrder().Where(pos => !pos.Enemy)
            .Select(At).Where(unit => HasActiveAbility(unit, EffectTrigger.WhileActive, EffectKind.JunkPower))
            .Sum(unit => unit.Attack);

        private static bool HasActiveAbility(Unit unit, EffectTrigger trigger, EffectKind effect) =>
            unit != null && unit.Ink == 0 && unit.Type.Ability.Trigger == trigger && unit.Type.Ability.Effect == effect;

        private void RefreshAuras()
        {
            for (int side = 0; side < 2; side++)
            {
                int attack = 0, thorns = 0;
                for (int row = 0; row < Rows; row++)
                for (int slot = 0; slot < Slots; slot++)
                {
                    Unit unit = board[side, row, slot];
                    if (HasActiveAbility(unit, EffectTrigger.WhileActive, EffectKind.AttackUp))
                        attack += unit.Type.Ability.Amount + unit.AbilityBonus;
                    if (HasActiveAbility(unit, EffectTrigger.WhileActive, EffectKind.Thorns))
                        thorns += unit.Type.Ability.Amount + unit.AbilityBonus;
                }

                for (int row = 0; row < Rows; row++)
                for (int slot = 0; slot < Slots; slot++)
                    if (board[side, row, slot] is Unit unit)
                    {
                        unit.AuraAttack = attack;
                        unit.AuraThorns = thorns;
                    }
            }
        }

        private void GenerateJunk(int count)
        {
            if (junkCard == null)
                return;
            for (int i = 0; i < count; i++)
                hand.Add(junkCard);
        }

        private void RemoveIfDefeated(Position pos, Unit victim)
        {
            if (victim.Health > 0)
                return;
            if (!pos.Enemy && HasActiveAbility(victim, EffectTrigger.Death, EffectKind.Junk))
                GenerateJunk(victim.Type.Ability.Amount + victim.AbilityBonus);
            Set(pos, null);
            if (victim.Leader)
            {
                Phase = BattlePhase.Lost;
                Notice = $"{Captain.Name}倒下。";
            }
        }

        private IEnumerable<BattleBeat> BoostRandomAlly(Position source, int amount)
        {
            var allies = TurnOrder().Where(pos => pos.Enemy == source.Enemy && !pos.Equals(source) && At(pos) != null).ToArray();
            if (allies.Length == 0)
                yield break;
            Position chosen = allies[RollTarget(allies.Length)];
            if (RollTarget(2) == 0)
            {
                foreach (var beat in ApplyEffect(source, chosen, EffectKind.AttackUp, amount))
                    yield return beat;
            }
            else
            {
                Unit ally = At(chosen);
                ally.BattleHealth += amount;
                ally.Health += amount;
                Notice = $"{ally.Type.Name} · 本场最大生命 +{amount}";
                yield return new BattleBeat(source, chosen, BattleEffect.Recovery, $"最大生命 +{amount}");
            }
        }

        private IEnumerable<BattleBeat> RewardAllyKill(Position attacker, int kills)
        {
            foreach (Position pos in TurnOrder())
            {
                if (pos.Enemy != attacker.Enemy || At(pos) is not Unit supporter)
                    continue;
                foreach (var beat in TriggerAbility(pos, attacker, supporter, EffectTrigger.AllyKill, kills))
                    yield return beat;
            }
        }

        private bool CanSpendJunk(Card card)
        {
            int cost = card.Technique?.JunkCost ?? 0;
            return cost == 0 || hand.Count(value => value.IsJunk) >= cost;
        }

        private void SpendJunk(int cost)
        {
            for (int i = hand.Count - 1; i >= 0 && cost > 0; i--)
            {
                if (!hand[i].IsJunk)
                    continue;
                hand.RemoveAt(i);
                cost--;
            }
        }

        private IReadOnlyList<Position> TechniqueTargets(Card card, Position anchor)
        {
            Unit unit = At(anchor);
            CardTechnique rules = card.Technique;
            if (rules.Target == CardTarget.None)
                return !anchor.Enemy && unit != null ? new[] { anchor } : Array.Empty<Position>();
            if (unit == null)
                return Array.Empty<Position>();
            if (rules.Target == CardTarget.AllUnits)
                return TurnOrder().Where(pos => At(pos) != null).ToArray();
            if (rules.Target is CardTarget.Enemy or CardTarget.AllEnemies or CardTarget.HealthyEnemies)
            {
                if (!anchor.Enemy)
                    return Array.Empty<Position>();
            }
            if (rules.Target is CardTarget.Ally or CardTarget.AllAllies)
            {
                if (anchor.Enemy)
                    return Array.Empty<Position>();
            }
            if (rules.Target == CardTarget.HealthyEnemies && unit.Health != unit.MaxHealth)
                return Array.Empty<Position>();
            if (rules.Target is CardTarget.AllEnemies or CardTarget.AllAllies or CardTarget.HealthyEnemies)
                return TurnOrder().Where(pos => pos.Enemy == anchor.Enemy && At(pos) is Unit target &&
                    (rules.Target != CardTarget.HealthyEnemies || target.Health == target.MaxHealth)).ToArray();
            return rules.Row || rules.RandomTarget ? OccupiedRow(anchor) : new[] { anchor };
        }

        private IEnumerable<BattleBeat> PlayTechnique(Card card, Position anchor)
        {
            var rules = card.Technique;
            SpendJunk(rules.JunkCost);
            GenerateJunk(rules.JunkOutput);
            if (rules.Target == CardTarget.None)
            {
                if (rules.JunkPerTurn > 0)
                {
                    JunkPerTurn += rules.JunkPerTurn;
                    Notice = $"已装备{card.Name}，每回合垃圾 +{JunkPerTurn}。";
                    yield return new BattleBeat(anchor, anchor, BattleEffect.Status, "装备");
                }
                if (card.Effect == EffectKind.Draw)
                {
                    int before = hand.Count;
                    DrawCards(card.Amount);
                    Notice = $"{card.Name} · 抽牌 {hand.Count - before}";
                    yield return new BattleBeat(anchor, anchor, BattleEffect.Status, "抽牌");
                }
                yield break;
            }

            for (int hit = 0; hit < rules.Hits && Phase != BattlePhase.Lost; hit++)
            {
                Position pick = anchor;
                if (rules.RandomTarget && At(pick) == null)
                {
                    var remaining = OccupiedRow(anchor);
                    if (remaining.Count == 0)
                        yield break;
                    pick = remaining[0];
                }
                var targets = TechniqueTargets(card, pick);
                if (rules.RandomTarget && targets.Count > 0)
                    targets = new[] { targets[RollTarget(targets.Count)] };
                if (targets.Count == 0)
                    yield break;
                if (card.HasAttack)
                    yield return new BattleBeat(anchor, targets[0], BattleEffect.Windup, card.Name, targets, cardStrike: true);
                foreach (Position target in targets)
                {
                    Unit victim = At(target);
                    if (victim == null)
                        continue;
                    if (card.HasAttack)
                    {
                        int before = victim.Health;
                        yield return Hit(anchor, target, card.DamageAgainst(victim) + (card.IsJunk ? JunkAttackBonus : 0), card.Name);
                        foreach (var beat in CheckOverload(target))
                            yield return beat;
                        if (rules.HealFront && before > victim.Health)
                        {
                            Position? front = Front(false, target.Row) ?? Front(false, 1 - target.Row);
                            if (front.HasValue)
                                foreach (var beat in ApplyEffect(anchor, front.Value, EffectKind.Heal, before - victim.Health))
                                    yield return beat;
                        }
                        if (At(target) == null)
                            continue;
                        foreach (var beat in TriggerAbility(target, target, victim, EffectTrigger.WhenHit))
                            yield return beat;
                        if (card.IsJunk)
                            foreach (var beat in TriggerAbility(target, target, victim, EffectTrigger.WhenJunkHit))
                                yield return beat;
                    }
                    if (card.Effect != EffectKind.None)
                        foreach (var beat in ApplyEffect(anchor, target, card.Effect, card.Amount))
                            yield return beat;
                    if (rules.Secondary != EffectKind.None)
                        foreach (var beat in ApplyEffect(anchor, target, rules.Secondary, rules.SecondaryAmount))
                            yield return beat;
                    if (rules.DamageCountdown > 0)
                        foreach (var beat in ApplyEffect(anchor, target, EffectKind.Countdown, rules.DamageCountdown))
                            yield return beat;
                    if (Phase == BattlePhase.Lost)
                        yield break;
                    if (At(target) is Unit actor && actor.Counter == 0 && actor.Frozen == 0)
                        foreach (var beat in Trigger(target, actor))
                            yield return beat;
                }
            }
        }

        private void DiscardAfterUse(Card card)
        {
            if (card.Action != CardAction.Deploy && card.Action != CardAction.Copy && card.Technique?.Consume != true &&
                card.Technique?.JunkPerTurn is not > 0)
                discard.Add(card.Technique?.GainAfterUse is int bonus && bonus > 0 ? card.WithDamage(card.Damage + bonus) : card);
        }

        public bool CanPlayOnHand(int source, int target)
        {
            if (Opening || Phase != BattlePhase.Planning || source < 0 || source >= hand.Count ||
                target < 0 || target >= hand.Count || source == target)
                return false;
            Card card = hand[source];
            return CanSpendJunk(card) && (card.Action == CardAction.Copy || (card.Technique?.CanBuffHand == true && hand[target].HasAttack));
        }

        public IEnumerable<BattleBeat> PlayOnHand(int source, int target)
        {
            if (!CanPlayOnHand(source, target))
                yield break;
            Card card = hand[source];
            Card other = hand[target];
            Phase = BattlePhase.Resolving;
            if (card.Action == CardAction.Copy)
            {
                hand.Add(other.CopyInBattle());
                Notice = $"已复制 {other.Name}。";
            }
            else
            {
                hand[target] = other.WithDamage(other.Damage + card.Amount);
                Notice = $"{other.Name} · 本场伤害 +{card.Amount}";
            }
            hand.RemoveAt(source);
            DiscardAfterUse(card);
            foreach (var beat in Resolve())
                yield return beat;
        }
    }
}
