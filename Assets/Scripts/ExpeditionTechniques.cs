using System;
using System.Collections.Generic;
using System.Linq;

namespace Frostline
{
    public sealed partial class Expedition
    {
        public int JunkAttackBonus { get; private set; }

        private static bool HasActiveAbility(Unit unit, EffectTrigger trigger, EffectKind effect) =>
            unit != null && unit.Ink == 0 && unit.Type.Ability.Trigger == trigger && unit.Type.Ability.Effect == effect;

        private IEnumerable<BattleBeat> ApplyEntryBuff(Position position, Unit source, int amount)
        {
            EffectKind effect = source.Type.Ability.Effect;
            if (effect == EffectKind.JunkPower)
            {
                if (!position.Enemy && source.Attack > 0)
                {
                    int bonus = source.Attack;
                    JunkAttackBonus += bonus;
                    string label = $"本场垃圾伤害 +{bonus}";
                    Notice = $"{source.Type.Name} · {label}";
                    yield return new BattleBeat(position, position, BattleEffect.Status, label);
                }
                yield break;
            }

            foreach (Position ally in PositionsOnSide(position.Enemy))
                foreach (var beat in ApplyEffect(position, ally, effect, amount))
                    yield return beat;
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
            GainAttackOnEvent(EffectTrigger.CardDestroyed);
            if (victim.Leader)
            {
                Phase = BattlePhase.Lost;
                Notice = $"{Captain.Name}倒下。";
            }
        }

        private void GainAttackOnEvent(EffectTrigger trigger, int times = 1)
        {
            if (times == 0)
                return;
            foreach (Position position in TurnOrder())
                if (At(position) is Unit unit && HasActiveAbility(unit, trigger, EffectKind.AttackUp))
                    unit.BattleAttack += (unit.Type.Ability.Amount + unit.AbilityBonus) * times;
        }

        private IEnumerable<BattleBeat> ResolveItemHit(Position target, bool isJunk)
        {
            if (At(target) is not Unit victim || Phase == BattlePhase.Lost)
                yield break;
            foreach (var beat in TriggerAbility(target, target, victim, EffectTrigger.WhenHit))
                yield return beat;
            if (isJunk)
                foreach (var beat in TriggerAbility(target, target, victim, EffectTrigger.WhenJunkHit))
                    yield return beat;
        }

        private IEnumerable<BattleBeat> ResolveUnitAbility(Position source, Unit unit, int amount)
        {
            int secondary = unit.Type.Ability.SecondaryAmount + unit.AbilityBonus;
            switch (unit.Type.Ability.Effect)
            {
                case EffectKind.MaxCountdownUp:
                    foreach (var beat in ApplyEffect(source, source, EffectKind.MaxCountdownUp, amount))
                        yield return beat;
                    foreach (var beat in ApplyEffect(source, source, EffectKind.AttackUp, secondary))
                        yield return beat;
                    break;
                case EffectKind.TeamHealth:
                    foreach (Position ally in PositionsOnSide(source.Enemy))
                        if (!ally.Equals(source))
                            foreach (var beat in ApplyEffect(source, ally, EffectKind.BattleHealthUp, amount))
                                yield return beat;
                    break;
                case EffectKind.WeakenRow:
                    foreach (Position ally in OccupiedRow(source))
                        foreach (var beat in ApplyEffect(source, ally, EffectKind.Weakness, amount))
                            yield return beat;
                    break;
                case EffectKind.AbsorbRow:
                    foreach (var beat in AbsorbRow(source, amount))
                        yield return beat;
                    break;
                case EffectKind.HurtForFrenzy:
                    foreach (var beat in ApplyEffect(source, source, EffectKind.Hurt, amount))
                        yield return beat;
                    if (At(source) != null && Phase != BattlePhase.Lost)
                        foreach (var beat in ApplyEffect(source, source, EffectKind.Frenzy, secondary))
                            yield return beat;
                    break;
                case EffectKind.ExtraAllyTick:
                    if (advancingAllies || amount <= 0)
                        yield break;
                    extraAllyTicks += amount;
                    Notice = $"{unit.Type.Name} · 友军额外推进 {amount} 拍";
                    yield return new BattleBeat(source, source, BattleEffect.Status, $"友军 +{amount} 拍");
                    break;
            }
        }

        private IEnumerable<BattleBeat> AbsorbRow(Position source, int limit)
        {
            int attack = 0, health = 0;
            foreach (Position ally in OccupiedRow(source))
            {
                if (ally.Equals(source))
                    continue;
                Unit donor = At(ally);
                int attackTaken = Math.Min(limit, Math.Max(0, donor.BaseAttack + donor.BattleAttack));
                int healthTaken = Math.Min(limit, donor.MaxHealth - 1);
                attack += attackTaken;
                health += healthTaken;
                if (attackTaken > 0)
                    foreach (var beat in ApplyEffect(source, ally, EffectKind.AttackDown, attackTaken))
                        yield return beat;
                if (healthTaken > 0)
                    foreach (var beat in ApplyEffect(source, ally, EffectKind.BattleHealthDown, healthTaken))
                        yield return beat;
            }
            if (attack > 0)
                foreach (var beat in ApplyEffect(source, source, EffectKind.AttackUp, attack))
                    yield return beat;
            if (health > 0)
                foreach (var beat in ApplyEffect(source, source, EffectKind.BattleHealthUp, health))
                    yield return beat;
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
                GainAttackOnEvent(EffectTrigger.CardDestroyed);
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
            if (card.Effect == EffectKind.EffectBonus && unit.Type.Ability.Effect == EffectKind.None)
                return Array.Empty<Position>();
            if ((card.Effect is EffectKind.MaxCountdownUp or EffectKind.CountdownUp) && unit.Period == 0)
                return Array.Empty<Position>();
            if (card.Effect == EffectKind.AllEnemiesPattern && unit.Pattern == AttackPattern.Support)
                return Array.Empty<Position>();
            if (rules.JunkAction != JunkOperation.None && !hand.Any(value => value.IsJunk))
                return Array.Empty<Position>();
            if (rules.ImmediateHits > 0 && (unit.Period == 0 || unit.Frozen > 0 || unit.Pattern == AttackPattern.Support ||
                AttackTargets(anchor, unit.Type).Count == 0))
                return Array.Empty<Position>();
            if (rules.Target == CardTarget.SacrificeAlly)
                return !anchor.Enemy && !unit.Leader && TurnOrder().Any(pos => !pos.Enemy && !pos.Equals(anchor) && At(pos) != null)
                    ? new[] { anchor } : Array.Empty<Position>();
            if (rules.Target is CardTarget.RandomAlly or CardTarget.RandomEnemy)
            {
                bool enemy = rules.Target == CardTarget.RandomEnemy;
                return anchor.Enemy != enemy ? Array.Empty<Position>() : PositionsOnSide(enemy);
            }
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

        private Position[] PositionsOnSide(bool enemy) =>
            TurnOrder().Where(pos => pos.Enemy == enemy && At(pos) != null).ToArray();

        private IEnumerable<BattleBeat> ApplyToRandomUnit(Position source, bool enemy, EffectKind effect, int amount)
        {
            var candidates = PositionsOnSide(enemy);
            if (candidates.Length > 0)
                foreach (var beat in ApplyEffect(source, candidates[RollTarget(candidates.Length)], effect, amount))
                    yield return beat;
        }

        public int PreviewCardDamage(Card card, Unit victim = null)
        {
            int damage = victim == null ? card.Damage : card.DamageAgainst(victim);
            return damage + (card.IsJunk ? JunkAttackBonus : 0) +
                (card.Technique?.JunkAction == JunkOperation.DoubleAndStrike ? hand.Count(value => value.IsJunk) : 0);
        }

        private IEnumerable<BattleBeat> SacrificeAlly(Position target)
        {
            Unit victim = At(target);
            int frenzy = victim.Frenzy;
            victim.Health = 0;
            RemoveIfDefeated(target, victim);
            Notice = $"已吞噬{victim.Type.Name}。";
            yield return new BattleBeat(target, target, BattleEffect.Status, "吞噬");
            foreach (var beat in ApplyToRandomUnit(target, false, EffectKind.Frenzy, frenzy))
                yield return beat;
        }

        private void ChangeJunkInHand(Card card, IReadOnlyList<Card> junkBefore)
        {
            switch (card.Technique.JunkAction)
            {
                case JunkOperation.DoubleAndStrike:
                    hand.AddRange(junkBefore);
                    break;
                case JunkOperation.Recycle:
                    int destroyed = hand.RemoveAll(value => value.IsJunk);
                    GainAttackOnEvent(EffectTrigger.CardDestroyed, destroyed);
                    DrawCards(destroyed * card.Amount);
                    break;
                case JunkOperation.BuffAttack:
                case JunkOperation.BuffFrenzy:
                    for (int i = 0; i < hand.Count; i++)
                    {
                        Card junk = hand[i];
                        if (junk.IsJunk)
                            hand[i] = card.Technique.JunkAction == JunkOperation.BuffAttack
                                ? junk.WithDamage(junk.Damage + card.Amount)
                                : junk.WithBonusHits(junk.BonusHits + card.Amount);
                    }
                    break;
            }
        }

        private IEnumerable<BattleBeat> PlayTechnique(Card card, Position anchor)
        {
            var rules = card.Technique;
            SpendJunk(rules.JunkCost);
            GenerateJunk(rules.JunkOutput);
            if (rules.Target == CardTarget.SacrificeAlly)
            {
                foreach (var beat in SacrificeAlly(anchor))
                    yield return beat;
                yield break;
            }
            if (rules.Target is CardTarget.RandomAlly or CardTarget.RandomEnemy)
            {
                foreach (var beat in ApplyToRandomUnit(anchor, rules.Target == CardTarget.RandomEnemy, card.Effect, card.Amount))
                    yield return beat;
                if (rules.OpponentBlock > 0)
                    foreach (var beat in ApplyToRandomUnit(anchor, true, EffectKind.Block, rules.OpponentBlock))
                        yield return beat;
                yield break;
            }
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
                if (rules.JunkAction != JunkOperation.None)
                {
                    ChangeJunkInHand(card, Array.Empty<Card>());
                    Notice = card.Name;
                    yield return new BattleBeat(anchor, anchor, BattleEffect.Status, card.Name);
                }
                yield break;
            }

            var junkBefore = rules.JunkAction == JunkOperation.DoubleAndStrike
                ? hand.Where(value => value.IsJunk).ToArray() : Array.Empty<Card>();
            for (int hit = 0; hit < rules.Hits + card.BonusHits && Phase != BattlePhase.Lost; hit++)
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
                        var impact = Hit(anchor, target, rules.JunkAction == JunkOperation.DoubleAndStrike
                            ? card.Damage + junkBefore.Length : PreviewCardDamage(card, victim), card.Name);
                        yield return impact;
                        foreach (var beat in ResolveBlockReaction(impact))
                            yield return beat;
                        foreach (var beat in CheckOverload(target))
                            yield return beat;
                        if (rules.HealFront && before > victim.Health)
                        {
                            Position? front = Front(false, target.Row) ?? Front(false, 1 - target.Row);
                            if (front.HasValue)
                                foreach (var beat in ApplyEffect(anchor, front.Value, EffectKind.Heal, before - victim.Health))
                                    yield return beat;
                        }
                        if (Phase == BattlePhase.Lost)
                            yield break;
                        if (At(target) == null)
                            continue;
                        foreach (var beat in ResolveItemHit(target, card.IsJunk))
                            yield return beat;
                    }
                    if (card.Effect != EffectKind.None)
                        foreach (var beat in ApplyEffect(anchor, target, card.Effect, card.Amount))
                            yield return beat;
                    if (rules.Secondary != EffectKind.None)
                        foreach (var beat in ApplyEffect(anchor, target, rules.Secondary, rules.SecondaryAmount))
                            yield return beat;
                    if (rules.ThirdEffect != EffectKind.None)
                        foreach (var beat in ApplyEffect(anchor, target, rules.ThirdEffect, rules.ThirdAmount))
                            yield return beat;
                    if (rules.HealSchedule is { Length: > 0 } && At(target) is Unit scheduled)
                    {
                        for (int i = 0; i < rules.HealSchedule.Length; i++)
                            scheduled.ScheduledHealing.Add((Turn + i, rules.HealSchedule[i]));
                    }
                    if (rules.DamageCountdown > 0)
                        foreach (var beat in ApplyEffect(anchor, target, EffectKind.Countdown, rules.DamageCountdown))
                            yield return beat;
                    if (Phase == BattlePhase.Lost)
                        yield break;
                    if (At(target) is Unit actor && actor.Period > 0 && actor.Frozen == 0 &&
                        (actor.Counter == 0 || rules.ImmediateHits > 0))
                        foreach (var beat in Trigger(target, actor, rules.ImmediateHits > 0 ? rules.ImmediateHits : int.MaxValue))
                            yield return beat;
                }
            }
            if (Phase != BattlePhase.Lost && rules.JunkAction == JunkOperation.DoubleAndStrike)
            {
                ChangeJunkInHand(card, junkBefore);
                Notice = $"{card.Name} · 复制了 {junkBefore.Length} 张垃圾";
                yield return new BattleBeat(anchor, anchor, BattleEffect.Status, "复制垃圾");
            }
        }

        private IEnumerable<BattleBeat> ResolveScheduledHealing(Position target, Unit unit)
        {
            int amount = 0;
            for (int i = unit.ScheduledHealing.Count - 1; i >= 0; i--)
            {
                var pending = unit.ScheduledHealing[i];
                if (pending.Turn != Turn)
                    continue;
                amount += pending.Amount;
                unit.ScheduledHealing.RemoveAt(i);
            }
            if (amount > 0)
                foreach (var beat in ApplyEffect(target, target, EffectKind.Heal, amount))
                    yield return beat;
        }

        private void DiscardAfterUse(Card card)
        {
            if (card.Action == CardAction.Deploy || card.Action == CardAction.Copy || card.Technique?.Consume == true ||
                card.Technique?.JunkPerTurn is > 0)
                return;
            if (card.Technique?.Uses is > 0)
            {
                if (card.UsesRemaining <= 1)
                    return;
                card = card.SpendUse();
            }
            if (card.Technique?.GainAfterUse is int bonus && bonus > 0)
                card = card.WithDamage(card.Damage + bonus);
            discard.Add(card);
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
