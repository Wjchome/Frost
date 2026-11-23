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
            public bool recruitCompanions;
            public EncounterConfig[] encounters = Array.Empty<EncounterConfig>();
        }

        public Loadout[] loadouts = Array.Empty<Loadout>();
        public FactionConfig[] factions = Array.Empty<FactionConfig>();
        public CardConfig[] companions = Array.Empty<CardConfig>();
        public CardConfig[] sharedRecruits = Array.Empty<CardConfig>();
        public CardConfig[] sharedItems = Array.Empty<CardConfig>();
        public CardConfig[] catalogCards = Array.Empty<CardConfig>();
        public CardConfig junkCard;
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
                        data.targetsEnemy, data.bonusAgainst, data.bonusDamage, data.technique);
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

            bool useFactions = factions.Length > 0;
            if ((!useFactions && loadouts.Length == 0) || companions.Length == 0 || stages.Length == 0 ||
                stages.Any(stage => stage.encounters.Length == 0))
                throw new InvalidOperationException("The expedition must contain loadouts, companions, encounters and starting supplies.");

            var routeStages = stages.Select(stage => new RouteStage(stage.title,
                stage.encounters.Select(ReadEncounter).ToArray(), stage.recruitCompanions)).ToArray();
            var startingCompanions = companions.Select(ReadUnit).ToArray();
            if (!useFactions)
            {
                if (loadouts.Any(loadout => loadout.startingDeck.Length == 0 || loadout.startingSupplies.Length == 0))
                    throw new InvalidOperationException("Each loadout must have a starting deck and starting supplies.");
                var startingLoadouts = loadouts.Select(loadout => new StartingLoadout(
                    ReadUnit(loadout.captain), loadout.deckName, loadout.strategy, ReadCard(loadout.signatureReward),
                    loadout.startingSupplies.Select(ReadCard).ToArray(), loadout.startingDeck.Select(ReadCard).ToArray())).ToArray();
                return new DispatchRoute(startingLoadouts, startingCompanions, routeStages);
            }

            Card[] ReadRecruits(IEnumerable<CardConfig> sources)
            {
                var result = sources.Select(ReadCard).Distinct().ToArray();
                if (result.Any(card => card.Unit == null))
                    throw new InvalidOperationException("Recruitment pools must reference deployable units.");
                return result;
            }

            var shared = ReadRecruits(sharedRecruits);
            bool hasRecruitment = routeStages.Take(routeStages.Length - 1).Any(stage => stage.RecruitCompanions);
            if (hasRecruitment && shared.Length == 0)
                throw new InvalidOperationException("Recruitment requires a shared companion.");
            var exclusive = new HashSet<Card>(shared);
            var routeFactions = factions.Select(config =>
            {
                if (config == null)
                    throw new InvalidOperationException("A faction reference is missing.");
                var leaders = config.leaders.Select(ReadUnit).Distinct().ToArray();
                var recruits = ReadRecruits(config.exclusiveRecruits);
                if (leaders.Length < 3 || config.startingDeck.Length == 0 ||
                    (hasRecruitment && recruits.Length < 2))
                    throw new InvalidOperationException($"{config.name} needs three leaders, a deck and two exclusive recruits for recruitment.");
                if (recruits.Any(card => !exclusive.Add(card)))
                    throw new InvalidOperationException($"{config.name} uses an exclusive recruit that already belongs to another pool.");
                return new ExpeditionFaction(config.displayName, config.strategy, leaders,
                    config.startingDeck.Select(ReadCard).ToArray(), config.startingSupplies.Select(ReadCard).ToArray(),
                    recruits, ReadCard(config.signatureReward), config.openingCompanions.Select(ReadUnit).ToArray(),
                    config.itemRewards.Select(ReadCard).ToArray());
            }).ToArray();

            return new DispatchRoute(Array.Empty<StartingLoadout>(), startingCompanions, routeStages, routeFactions, shared,
                catalog: catalogCards.Select(value => (value.catalogGroup, ReadCard(value))).ToArray(),
                sharedItems: sharedItems.Select(ReadCard).ToArray(), junk: junkCard == null ? null : ReadCard(junkCard));
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
