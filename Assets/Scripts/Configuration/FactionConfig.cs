using System;
using UnityEngine;

namespace Frostline
{
    [CreateAssetMenu(menuName = "Frostline/部族")]
    public sealed class FactionConfig : ScriptableObject
    {
        public string displayName;
        [TextArea] public string strategy;
        public CardConfig[] leaders = Array.Empty<CardConfig>();
        public CardConfig[] openingCompanions = Array.Empty<CardConfig>();
        public CardConfig[] startingDeck = Array.Empty<CardConfig>();
        public CardConfig[] startingSupplies = Array.Empty<CardConfig>();
        public CardConfig[] exclusiveRecruits = Array.Empty<CardConfig>();
        public CardConfig[] itemRewards = Array.Empty<CardConfig>();
        public CardConfig signatureReward;
    }
}
