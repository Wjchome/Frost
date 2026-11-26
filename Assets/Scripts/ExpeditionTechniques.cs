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
            for (int side = 0; side < board.GetLength(0); side++)
            {
                int attack = 0, thorns = 0, summonAttack = 0, summonHealth = 0;
                for (int row = 0; row < Rows; row++)
                for (int slot = 0; slot < Slots; slot++)
                {
                    Unit unit = board[side, row, slot];
                    if (HasActiveAbility(unit, EffectTrigger.WhileActive, EffectKind.AttackUp))
                        attack += unit.Type.Ability.Amount + unit.AbilityBonus;
                    if (HasActiveAbility(unit, EffectTrigger.WhileActive, EffectKind.Thorns))
                        thorns += unit.Type.Ability.Amount + unit.AbilityBonus;
                    if (HasActiveAbility(unit, EffectTrigger.WhileActive, EffectKind.SummonBoost))
                    {
                        summonAttack += unit.Type.Ability.Amount + unit.AbilityBonus;
                        summonHealth += unit.Type.Ability.SecondaryAmount + unit.AbilityBonus;
                    }
                }

                for (int row = 0; row < Rows; row++)
                for (int slot = 0; slot < Slots; slot++)
                    if (board[side, row, slot] is Unit unit)
                    {
                        unit.AuraAttack = attack + (unit.Type.Summon ? summonAttack : 0);
                        unit.AuraThorns = thorns;
                        // Gaining the aura heals by the same amount; losing it only trims health above the new maximum.
                        int health = unit.Type.Summon ? summonHealth : 0;
                        int change = health - unit.AuraHealth;
                        unit.AuraHealth = health;
                        if (change > 0)
                            unit.Health += change;
                        else if (change < 0)
                            unit.Health = Math.Min(unit.Health, unit.MaxHealth);
                    }
                for (int row = 0; row < Rows; row++)
                    RefreshTerrainAttack(side == 1, row);
            }
        }

        private void GenerateJunk(int count)
        {
            if (junkCard == null)
                return;
            for (int i = 0; i < count; i++)
                hand.Add(junkCard);
        }

        public int SummonAllies => TurnOrder().Count(pos => !pos.Enemy && At(pos) is Unit unit && unit.Type.Summon);

        // Extra damage a card currently deals, shown on its face and used when it is played.
        public int CardDamageBonus(Card card) =>
            (card.IsJunk ? JunkAttackBonus : 0) + (card.Technique?.DamagePerSummon ?? 0) * SummonAllies;

        private void RemoveIfDefeated(Position pos, Unit victim, bool sacrificed = false)
        {
            if (victim.Health > 0)
                return;
            if (sacrificed && !pos.Enemy && !victim.Leader)
                pendingSacrifices++;
            if (!pos.Enemy && HasActiveAbility(victim, EffectTrigger.Death, EffectKind.Junk))
                GenerateJunk(victim.Type.Ability.Amount + victim.AbilityBonus);
            if (HasActiveAbility(victim, EffectTrigger.Death, EffectKind.RowBlast))
                pendingBlasts.Add((pos, victim));
            Set(pos, null);
            if (victim.FromHand && victim.Type.Summon && !victim.Leader)
                discard.Add(Card.FromSummon(victim));
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
                case EffectKind.DevourRow:
                    foreach (var beat in DevourRow(source))
                        yield return beat;
                    break;
                case EffectKind.DrainAlly:
                    foreach (var beat in DrainAlly(source, amount))
                        yield return beat;
                    break;
                case EffectKind.HurtForFrenzy:
                    selfInflicted = true;
                    try
                    {
                        foreach (var beat in ApplyEffect(source, source, EffectKind.Hurt, amount))
                            yield return beat;
                    }
                    finally
                    {
                        selfInflicted = false;
                    }
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

        private IEnumerable<BattleBeat> DevourRow(Position source)
        {
            int attack = 0, health = 0;
            foreach (Position ally in OccupiedRow(source))
            {
                if (ally.Equals(source) || At(ally) is not Unit victim)
                    continue;
                attack += Math.Max(0, victim.BaseAttack + victim.BattleAttack);
                health += victim.Health;
                foreach (var beat in Sacrifice(ally))
                    yield return beat;
                if (Phase == BattlePhase.Lost || At(source) == null)
                    yield break;
            }
            if (attack > 0)
                foreach (var beat in ApplyEffect(source, source, EffectKind.AttackUp, attack))
                    yield return beat;
            if (health > 0)
                foreach (var beat in ApplyEffect(source, source, EffectKind.BattleHealthUp, health))
                    yield return beat;
        }

        private IEnumerable<BattleBeat> DrainAlly(Position source, int amount)
        {
            if (amount <= 0)
                yield break;
            var donors = PositionsOnSide(source.Enemy).Where(pos => !pos.Equals(source) && !At(pos).Leader).ToArray();
            if (donors.Length == 0)
                yield break;

            int total = 0;
            var marks = new List<string>(donors.Length);
            foreach (Position donor in donors)
            {
                Unit victim = At(donor);
                int taken = Math.Min(amount, victim.Health);
                victim.Health -= taken;
                total += taken;
                marks.Add($"生命 −{taken}");
                RemoveIfDefeated(donor, victim, sacrificed: !donor.Enemy);
            }
            Notice = $"{At(source)?.Type.Name ?? "吸取者"}从 {donors.Length} 名友军身上共夺取 {total} 点生命。";
            yield return new BattleBeat(source, donors[0], BattleEffect.Status, marks[0], donors, impactMarks: marks);
            foreach (var beat in ResolveDeathEvents())
                yield return beat;
            if (total > 0 && At(source) != null && Phase != BattlePhase.Lost)
                foreach (var beat in ApplyEffect(source, source, EffectKind.BattleHealthUp, total))
                    yield return beat;
        }

        // A sacrificed unit dies like any other unit, then the player's sacrifice reactions act.
        private IEnumerable<BattleBeat> Sacrifice(Position target)
        {
            Unit victim = At(target);
            victim.Health = 0;
            RemoveIfDefeated(target, victim, sacrificed: true);
            if (!victim.Leader)
                Notice = $"{victim.Type.Name}被献祭。";
            yield return new BattleBeat(target, target, BattleEffect.Status, "献祭");
            foreach (var beat in ResolveDeathEvents())
                yield return beat;
        }

        // Death blasts resolve first, then each pending sacrifice lets the player's sacrifice reactions act once.
        private IEnumerable<BattleBeat> ResolveDeathEvents()
        {
            while (Phase != BattlePhase.Lost && (pendingBlasts.Count > 0 || pendingSacrifices > 0))
            {
                if (pendingBlasts.Count == 0)
                {
                    pendingSacrifices--;
                    foreach (Position pos in TurnOrder())
                    {
                        if (Phase == BattlePhase.Lost)
                            yield break;
                        if (!pos.Enemy && At(pos) is Unit ally && HasActiveAbility(ally, EffectTrigger.AllySacrificed, EffectKind.TriggerAttack))
                            foreach (var beat in Trigger(pos, ally))
                                yield return beat;
                    }
                    continue;
                }
                var (origin, bomb) = pendingBlasts[0];
                pendingBlasts.RemoveAt(0);
                int damage = bomb.Type.Ability.Amount + bomb.AbilityBonus;
                List<Position> targets = OccupiedRow(origin);
                var marks = new List<string>(targets.Count);
                var blocked = new List<BattleBeat>();
                foreach (Position target in targets)
                {
                    var impact = Hit(origin, target, damage, bomb.Type.Name);
                    marks.Add(impact.Mark);
                    if (impact.Blocked)
                        blocked.Add(impact);
                }
                Notice = Phase == BattlePhase.Lost ? $"{Captain.Name}倒下。" : $"{bomb.Type.Name}爆炸 · 同排伤害 {damage}";
                yield return new BattleBeat(origin, origin, BattleEffect.Burst, "爆炸", targets, impactMarks: marks);
                foreach (var impact in blocked)
                    foreach (var reaction in ResolveBlockReaction(impact))
                        yield return reaction;
                foreach (Position target in targets)
                    foreach (BattleBeat burst in CheckOverload(target))
                        yield return burst;
            }
        }

        private Position? EmptySlot(bool enemy, int row)
        {
            for (int slot = 0; slot < Slots; slot++)
            {
                var pos = new Position(enemy, row, slot);
                if (At(pos) == null)
                    return pos;
            }
            return null;
        }

        // Summoned units belong to the player wherever they stand and return to the discard pile when they die.
        private IEnumerable<BattleBeat> SummonUnit(Position pos, UnitType type)
        {
            Unit unit = new Card(type).Recruit().Deploy(false);
            unit.FromHand = true;
            Set(pos, unit);
            Notice = $"召唤{type.Name}。";
            yield return new BattleBeat(pos, pos, BattleEffect.Arrival, "召唤");
            foreach (var beat in TriggerAbility(pos, pos, unit, EffectTrigger.Deploy))
                yield return beat;
        }

        private static UnitType SummonedCopy(UnitType type, int power, int health) =>
            new(type.Name, type.Art, power, Math.Max(1, health), type.Period, type.Pattern, type.Frenzy, type.Ability,
                type.ResistSnow, type.Counterattack, summon: true);

        private UnitType MimicEnemy(int health)
        {
            var foes = PositionsOnSide(true);
            if (foes.Length == 0)
                return null;
            UnitType type = At(foes[RollTarget(foes.Length)]).Type;
            return SummonedCopy(type, type.Power, health);
        }

        private IEnumerable<BattleBeat> SummonCopy(Position original, Unit unit)
        {
            Position? spot = EmptySlot(original.Enemy, original.Row) ?? EmptySlot(original.Enemy, 1 - original.Row);
            if (!spot.HasValue)
                yield break;
            foreach (var beat in SummonUnit(spot.Value, SummonedCopy(unit.Type, unit.BaseAttack, unit.Progress.Health)))
                yield return beat;
        }

        private IEnumerable<BattleBeat> BoostRandomAlly(Position source, int amount)
        {
            var allies = TurnOrder().Where(pos => pos.Enemy == source.Enemy && !pos.Equals(source) && At(pos) != null).ToArray();
            if (allies.Length == 0)
                yield break;
            Position chosen = allies[RollTarget(allies.Length)];
            foreach (var beat in ApplyEffect(source, chosen, EffectKind.AttackUp, amount))
                yield return beat;
            foreach (var beat in ApplyEffect(source, chosen, EffectKind.BattleHealthUp, amount))
                yield return beat;
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
            if (rules.Target == CardTarget.EmptySlot)
            {
                bool enemySide = card.Effect == EffectKind.Summon && rules.SummonUnit?.DeployOnEnemy == true;
                bool ready = card.Effect == EffectKind.MimicEnemy
                    ? PositionsOnSide(true).Length > 0
                    : card.Effect == EffectKind.Summon && rules.SummonUnit != null;
                return ready && anchor.Enemy == enemySide && CanPlaceAt(anchor) ? new[] { anchor } : Array.Empty<Position>();
            }
            if (unit == null)
                return Array.Empty<Position>();
            if ((card.Effect is EffectKind.Sacrifice or EffectKind.CopyAlly) && (anchor.Enemy || unit.Leader))
                return Array.Empty<Position>();
            if (card.Effect == EffectKind.CopyAlly && !EmptySlot(false, 0).HasValue && !EmptySlot(false, 1).HasValue)
                return Array.Empty<Position>();
            if (card.Effect == EffectKind.EffectBonus && unit.Type.Ability.Effect == EffectKind.None)
                return Array.Empty<Position>();
            if (card.Effect == EffectKind.OverloadDamage && unit.Overload == 0)
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
            return damage + CardDamageBonus(card) +
                (card.Technique?.JunkAction == JunkOperation.DoubleAndStrike ? hand.Count(value => value.IsJunk) : 0);
        }

        private IEnumerable<BattleBeat> SacrificeAlly(Position target)
        {
            Unit victim = At(target);
            int frenzy = victim.Frenzy;
            foreach (var beat in Sacrifice(target))
                yield return beat;
            Notice = $"已吞噬{victim.Type.Name}。";
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
            if (rules.Target == CardTarget.EmptySlot)
            {
                UnitType summoned = card.Effect == EffectKind.MimicEnemy ? MimicEnemy(card.Amount) : rules.SummonUnit;
                if (summoned != null)
                    foreach (var beat in SummonUnit(anchor, summoned))
                        yield return beat;
                yield break;
            }
            if (rules.Target == CardTarget.SacrificeAlly)
            {
                foreach (var beat in SacrificeAlly(anchor))
                    yield return beat;
                yield break;
            }
            if (rules.Target is CardTarget.RandomAlly or CardTarget.RandomEnemy)
            {
                for (int hit = 0; hit < rules.Hits + card.BonusHits && Phase != BattlePhase.Lost; hit++)
                    foreach (var beat in ApplyToRandomUnit(anchor, rules.Target == CardTarget.RandomEnemy, card.Effect, card.Amount))
                        yield return beat;
                if (rules.OpponentBlock > 0)
                    foreach (var beat in ApplyToRandomUnit(anchor, true, EffectKind.Block, rules.OpponentBlock))
                        yield return beat;
                yield break;
            }
            if (rules.Target == CardTarget.None)
            {
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
                        if (rules.KillIsSacrifice && target.Enemy && victim.Health == 0)
                            pendingSacrifices++;
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
                    if (rules.JunkPerTurn > 0)
                        foreach (var beat in ApplyEffect(anchor, target, EffectKind.PermanentJunk, rules.JunkPerTurn))
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

        // Runs once per unit at the end of every turn. A kill counts as a sacrifice because the player's own card caused it.
        private IEnumerable<BattleBeat> ResolveDelayedDamage(Position pos, Unit unit)
        {
            int amount = unit.DamageThisTurn;
            unit.DamageThisTurn = unit.DamageNextTurn;
            unit.DamageNextTurn = 0;
            if (amount <= 0)
                yield break;
            var impact = Hit(pos, pos, amount, Effects.Name(EffectKind.DelayedDamage));
            yield return impact;
            foreach (var reaction in ResolveBlockReaction(impact))
                yield return reaction;
            foreach (var burst in CheckOverload(pos))
                yield return burst;
        }

        // The ally and the front enemy across from it each gain the overload, then both double it. Bursts are checked after both change.
        private IEnumerable<BattleBeat> MutualOverload(Position source, Position ally, int amount)
        {
            if (At(ally) == null)
                yield break;
            Position? foe = Front(!ally.Enemy, ally.Row) ?? Front(!ally.Enemy, 1 - ally.Row);
            Position[] marked = foe.HasValue ? new[] { foe.Value, ally } : new[] { ally };
            foreach (Position pos in marked)
            {
                Unit unit = At(pos);
                int before = unit.Overload;
                unit.Overload = (before + amount) * 2;
                Notice = $"{unit.Type.Name} · 过载 {before} → {unit.Overload}";
                yield return new BattleBeat(source, pos, BattleEffect.Status, $"过载 {before}→{unit.Overload}");
            }
            foreach (Position pos in marked)
                foreach (var burst in CheckOverload(pos))
                    yield return burst;
        }

        // Damage equal to the target's overload, then the overload halves (rounded down) before the burst check.
        private IEnumerable<BattleBeat> DischargeOverload(Position source, Position target)
        {
            Unit unit = At(target);
            if (unit == null || unit.Overload <= 0)
                yield break;
            string name = Effects.Name(EffectKind.OverloadDamage);
            yield return new BattleBeat(source, target, BattleEffect.Windup, name, cardStrike: true);
            var impact = Hit(source, target, unit.Overload, name);
            yield return impact;
            foreach (var reaction in ResolveBlockReaction(impact))
                yield return reaction;
            if (At(target) != unit || Phase == BattlePhase.Lost)
                yield break;
            int before = unit.Overload;
            unit.Overload /= 2;
            Notice = $"{unit.Type.Name} · 过载 {before} → {unit.Overload}";
            yield return new BattleBeat(source, target, BattleEffect.Status, $"过载 {before}→{unit.Overload}");
            foreach (var burst in CheckOverload(target))
                yield return burst;
            foreach (var reaction in ResolveItemHit(target, false))
                yield return reaction;
        }

        private void DiscardAfterUse(Card card)
        {
            if (card.Action == CardAction.Deploy)
                return;
            if (card.Action == CardAction.Copy || card.Technique?.Consume == true)
            {
                exhausted.Add(card);
                return;
            }
            if (card.Technique?.JunkPerTurn is > 0)
                return;
            if (card.Technique?.Uses is > 0)
            {
                card = card.SpendUse();
                if (card.UsesRemaining <= 0)
                {
                    exhausted.Add(card);
                    return;
                }
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
