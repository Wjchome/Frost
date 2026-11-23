using System;
using UnityEngine;

namespace Frostline
{
    [CreateAssetMenu(menuName = "Frostline/关卡")]
    public sealed class EncounterConfig : ScriptableObject
    {
        [Serializable]
        public sealed class Enemy
        {
            public CardConfig unit;
            [Range(1, Expedition.Rows)] public int row = 1;
        }

        [Serializable]
        public sealed class Wave
        {
            public Enemy[] enemies = Array.Empty<Enemy>();
        }

        public string displayName;
        public Texture2D landscape;
        [TextArea] public string briefing;
        [Min(1)] public int reinforcementInterval = 4;
        public Wave[] waves = Array.Empty<Wave>();
        public Pendant pendantReward;
        [Tooltip("这两张牌会与主角专属奖励组成战后三选一。")]
        public CardConfig[] rewardCards = Array.Empty<CardConfig>();
    }
}
