using System;
using System.Collections.Generic;
using System.Linq;

namespace Frostline
{
    public struct TerrainTile
    {
        public int Attack, TemporaryAttack, Poison, Weakness;
        internal int TemporaryChangedTurn;
        public bool HasEffects => Attack != 0 || TemporaryAttack > 0 || Poison > 0 || Weakness > 0;

        public string Describe()
        {
            var parts = new List<string>(4);
            if (Attack != 0) parts.Add($"攻 {Attack:+0;-0}");
            if (TemporaryAttack > 0) parts.Add($"临攻 +{TemporaryAttack}");
            if (Poison > 0) parts.Add($"毒 {Poison}");
            if (Weakness > 0) parts.Add($"虚弱 {Weakness}");
            return string.Join(" · ", parts);
        }

        internal bool SameEffects(TerrainTile other) => Attack == other.Attack && TemporaryAttack == other.TemporaryAttack &&
            Poison == other.Poison && Weakness == other.Weakness;
    }

    public sealed partial class Expedition
    {
        private TerrainTile[,,] terrain = new TerrainTile[2, Rows, Slots];

        public TerrainTile TerrainAt(Position position) => position.Valid
            ? terrain[position.Enemy ? 1 : 0, position.Row, position.Slot] : default;

        private static int TerrainMultiplier(Unit unit) => HasActiveAbility(unit, EffectTrigger.WhileActive, EffectKind.TerrainAffinity)
            ? unit.Type.Ability.Amount + unit.AbilityBonus : 1;

        internal int TerrainAttackAt(Position position, Unit unit)
        {
            TerrainTile tile = TerrainAt(position);
            return (tile.Attack + tile.TemporaryAttack) * TerrainMultiplier(unit);
        }

        private void SetTerrain(Position position, TerrainTile tile)
        {
            terrain[position.Enemy ? 1 : 0, position.Row, position.Slot] = tile;
            if (At(position) is Unit unit)
                unit.TerrainAttack = TerrainAttackAt(position, unit);
        }

        private void RefreshTerrainAttack(bool enemy, int row)
        {
            for (int slot = 0; slot < Slots; slot++)
            {
                var position = new Position(enemy, row, slot);
                if (At(position) is Unit unit)
                    unit.TerrainAttack = TerrainAttackAt(position, unit);
            }
        }

        private IEnumerable<BattleBeat> AddTerrain(Position source, IEnumerable<Position> targets, int attack = 0,
            int temporary = 0, int poison = 0, int weakness = 0)
        {
            if (attack == 0 && temporary == 0 && poison == 0 && weakness == 0)
                yield break;

            foreach (Position target in targets)
            {
                TerrainTile tile = TerrainAt(target);
                tile.Attack += attack;
                tile.Poison += poison;
                tile.Weakness += weakness;
                if (temporary > 0)
                {
                    if (tile.TemporaryAttack == 0)
                        tile.TemporaryChangedTurn = Turn;
                    tile.TemporaryAttack += temporary;
                }
                SetTerrain(target, tile);
                Notice = $"格子效果 · {tile.Describe()}";
                yield return new BattleBeat(source, target, BattleEffect.Status, "格子效果");
            }
            foreach (Position position in TurnOrder())
                if (At(position) is Unit observer && HasActiveAbility(observer, EffectTrigger.TerrainCreated, EffectKind.TerrainGrowth))
                    foreach (var beat in ApplyEffect(source, position, EffectKind.AttackUp, observer.Type.Ability.Amount + observer.AbilityBonus))
                        yield return beat;
        }

        private IEnumerable<BattleBeat> ResolveTerrainAbility(Position source, Unit unit, int amount, int secondary)
        {
            TerrainTile tile = TerrainAt(source);
            switch (unit.Type.Ability.Effect)
            {
                case EffectKind.TerrainAnchor:
                    if (unit.TerrainPrepared)
                        yield break;
                    unit.TerrainPrepared = true;
                    foreach (var beat in AddTerrain(source, new[] { source }, attack: amount))
                        yield return beat;
                    break;
                case EffectKind.TerrainTransfer:
                    int moved = Math.Min(Math.Max(0, unit.BaseAttack + unit.BattleAttack), amount);
                    unit.BattleAttack -= moved;
                    foreach (var beat in AddTerrain(source, new[] { source }, attack: moved))
                        yield return beat;
                    break;
                case EffectKind.TerrainCannon:
                    foreach (var beat in AddTerrain(source, new[] { source }, attack: -amount, weakness: secondary))
                        yield return beat;
                    break;
                case EffectKind.TerrainGambit:
                    foreach (var beat in AddTerrain(source, new[] { source }, temporary: amount, poison: secondary))
                        yield return beat;
                    break;
                case EffectKind.TerrainAbsorb:
                    if (!tile.HasEffects)
                        yield break;
                    SetTerrain(source, default);
                    unit.BattleAttack += tile.Attack;
                    if (tile.TemporaryAttack > 0)
                    {
                        unit.TemporaryAttackChangedTurn = unit.TemporaryAttack == 0 ? tile.TemporaryChangedTurn
                            : Math.Min(unit.TemporaryAttackChangedTurn, tile.TemporaryChangedTurn);
                        unit.TemporaryAttack += tile.TemporaryAttack;
                    }
                    Notice = $"{unit.Type.Name} · 吸收 {tile.Describe()}";
                    yield return new BattleBeat(source, source, BattleEffect.Status, "吸收格子");
                    foreach (var beat in TransferTerrainStatuses(source, unit, tile))
                        yield return beat;
                    break;
            }
        }

        private IEnumerable<BattleBeat> TransferTerrainStatuses(Position position, Unit unit, TerrainTile tile)
        {
            int multiplier = TerrainMultiplier(unit);
            if (tile.Poison > 0)
                foreach (var beat in ApplyEffect(position, position, EffectKind.Poison, tile.Poison * multiplier))
                    yield return beat;
            if (tile.Weakness > 0)
                foreach (var beat in ApplyEffect(position, position, EffectKind.Weakness, tile.Weakness * multiplier))
                    yield return beat;
        }

        private IEnumerable<BattleBeat> ResolveTerrainBeforeActions()
        {
            foreach (Position position in TurnOrder())
            {
                if (At(position) is not Unit unit)
                    continue;
                TerrainTile tile = TerrainAt(position);
                if (tile.Poison == 0 && tile.Weakness == 0)
                    continue;
                int removed = HasActiveAbility(unit, EffectTrigger.TerrainTurnStart, EffectKind.TerrainDetox)
                    ? Math.Min(tile.Poison, unit.Type.Ability.Amount + unit.AbilityBonus) : 0;
                tile.Poison -= removed;
                TerrainTile transfer = tile;
                tile.Poison = tile.Weakness = 0;
                SetTerrain(position, tile);
                if (removed > 0)
                    foreach (var beat in ApplyEffect(position, position, EffectKind.Heal, removed))
                        yield return beat;
                foreach (var beat in TransferTerrainStatuses(position, unit, transfer))
                    yield return beat;
            }
        }

        private void DecayTerrainAttack()
        {
            foreach (Position position in TurnOrder())
            {
                TerrainTile tile = TerrainAt(position);
                if (tile.TemporaryAttack > 0 && tile.TemporaryChangedTurn < Turn)
                {
                    tile.TemporaryAttack--;
                    SetTerrain(position, tile);
                }
                if (At(position) is Unit unit && unit.TemporaryAttack > 0 && unit.TemporaryAttackChangedTurn < Turn)
                    unit.TemporaryAttack--;
            }
        }

        private IReadOnlyDictionary<Position, TerrainTile> TerrainChanges(Expedition forecast) => TurnOrder()
            .Where(pos => !TerrainAt(pos).SameEffects(forecast.TerrainAt(pos)))
            .ToDictionary(pos => pos, forecast.TerrainAt);
    }
}
