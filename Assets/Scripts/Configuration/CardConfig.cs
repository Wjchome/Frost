using UnityEngine;

namespace Frostline
{
    [CreateAssetMenu(menuName = "Frostline/卡牌")]
    public sealed class CardConfig : ScriptableObject
    {
        [Header("通用")]
        public string displayName;
        public Texture2D portrait;
        public CardAction action;
        public string catalogGroup;

        [Header("单位")]
        [Min(0)] public int power;
        [Min(1)] public int health = 1;
        [Min(0)] public int period = 3;
        [Min(1)] public int frenzy = 1;
        public AttackPattern pattern;
        public bool resistSnow;
        public EffectTrigger trigger;
        public EffectKind abilityEffect;
        [Min(0)] public int abilityAmount;
        [Min(0)] public int abilitySecondaryAmount;

        [Header("道具")]
        public EffectKind effect;
        [Min(0)] public int amount;
        public bool targetsEnemy;
        [Min(0)] public int damage;
        public EffectKind bonusAgainst;
        [Min(0)] public int bonusDamage;
        public CardTechnique technique = new();
    }
}
