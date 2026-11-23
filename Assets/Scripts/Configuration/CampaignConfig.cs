using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Frostline
{
    [CreateAssetMenu(menuName = "Frostline/远征")]
    public sealed class CampaignConfig : ScriptableObject
    {
        public const string ResourcePath = "Config/ExpeditionConfig";

        [Serializable]
        public sealed class Loadout
        {
            public CardConfig captain;
            public string deckName;
            [TextArea] public string strategy;
            [Tooltip("按列表顺序抽牌。同一张牌可拖入多次。")]
            public CardConfig[] startingDeck = Array.Empty<CardConfig>();
            public CardConfig[] startingSupplies = Array.Empty<CardConfig>();
            public CardConfig signatureReward;
        }

        [Serializable]
        public sealed class Stage
        {
            public string title;
            public EncounterConfig[] encounters = Array.Empty<EncounterConfig>();
        }

        public Loadout[] loadouts = Array.Empty<Loadout>();
        public CardConfig[] companions = Array.Empty<CardConfig>();
        public Stage[] stages = Array.Empty<Stage>();

        public DispatchRoute CreateRoute(IDictionary<string, Texture2D> textures)
        {
            var cards = new Dictionary<CardConfig, Card>();

            Card ReadCard(CardConfig data)
            {
                if (data == null)
                    throw new InvalidOperationException("A card reference is missing from the expedition configuration.");
                if (cards.TryGetValue(data, out Card cached))
                    return cached;

                string art = RegisterTexture(data.portrait, textures);
                Card card;
                if (data.action == CardAction.Deploy)
                {
                    if (data.health < 1 || data.period < 1 || data.frenzy < 1)
                        throw new InvalidOperationException($"{data.name} needs positive health, countdown and attack count.");
                    var unit = new UnitType(data.displayName, art, data.power, data.health, data.period, data.pattern,
                        data.frenzy, new UnitAbility(data.trigger, data.abilityEffect, data.abilityAmount), data.resistSnow);
                    card = new Card(unit);
                }
                else
                    card = new Card(data.displayName, art, data.action, data.amount, data.damage, data.effect,
                        data.targetsEnemy, data.bonusAgainst, data.bonusDamage);
                cards.Add(data, card);
                return card;
            }

            UnitType ReadUnit(CardConfig data)
            {
                return ReadCard(data).Unit ??
                    throw new InvalidOperationException($"{data.name} must reference a deployable unit.");
            }

            Encounter ReadEncounter(EncounterConfig site)
            {
                if (site == null)
                    throw new InvalidOperationException("An encounter reference is missing from the expedition configuration.");
                int maxEnemies = Expedition.Rows * Expedition.Slots;
                if (site.waves.Length == 0 || site.waves.Any(wave => wave.enemies.Length == 0 || wave.enemies.Length > maxEnemies))
                    throw new InvalidOperationException($"{site.name} must define between one and six enemies in each wave.");
                var waves = site.waves.Select(wave => wave.enemies.Select(enemy =>
                    new EnemySpawn(ReadUnit(enemy.unit), Mathf.Clamp(enemy.row, 1, Expedition.Rows) - 1)).ToArray()).ToArray();
                return new Encounter(site.displayName, RegisterTexture(site.landscape, textures), site.briefing,
                    Mathf.Max(1, site.reinforcementInterval), site.pendantReward, site.rewardCards.Select(ReadCard).ToArray(), waves);
            }

            if (loadouts.Length == 0 || companions.Length == 0 || stages.Length == 0 ||
                stages.Any(stage => stage.encounters.Length == 0) ||
                loadouts.Any(loadout => loadout.startingDeck.Length == 0 || loadout.startingSupplies.Length == 0))
                throw new InvalidOperationException("The expedition must contain loadouts, companions, encounters and starting supplies.");

            var startingLoadouts = loadouts.Select(loadout => new StartingLoadout(
                ReadUnit(loadout.captain), loadout.deckName, loadout.strategy, ReadCard(loadout.signatureReward),
                loadout.startingSupplies.Select(ReadCard).ToArray(), loadout.startingDeck.Select(ReadCard).ToArray())).ToArray();
            var routeStages = stages.Select(stage => new RouteStage(stage.title, stage.encounters.Select(ReadEncounter).ToArray())).ToArray();
            return new DispatchRoute(startingLoadouts, companions.Select(ReadUnit).ToArray(), routeStages);
        }

        private static string RegisterTexture(Texture2D texture, IDictionary<string, Texture2D> textures)
        {
            if (texture == null)
                return "";
            string key = "configured/" + texture.GetInstanceID();
            textures[key] = texture;
            return key;
        }
    }
}
