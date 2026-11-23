using System;
using System.Collections.Generic;
using System.Linq;

namespace Frostline
{
    public sealed class StartingLoadout
    {
        public readonly UnitType Captain;
        public readonly string DeckName, Strategy;
        public readonly IReadOnlyList<Card> Deck;
        public readonly IReadOnlyList<Card> Supplies;
        public readonly Card Reward;

        public StartingLoadout(UnitType captain, string deckName, string strategy, Card reward, Card[] supplies, params Card[] deck)
        {
            Captain = captain;
            DeckName = deckName;
            Strategy = strategy;
            Deck = Array.AsReadOnly(deck);
            Supplies = Array.AsReadOnly(supplies);
            Reward = reward;
        }
    }

    public sealed class RouteStage
    {
        public readonly string Title;
        public readonly IReadOnlyList<Encounter> Encounters;
        public readonly bool RecruitCompanions;

        public RouteStage(string title, Encounter[] encounters, bool recruitCompanions = false)
        {
            Title = title;
            Encounters = Array.AsReadOnly(encounters);
            RecruitCompanions = recruitCompanions;
        }
    }

    public sealed class ExpeditionFaction
    {
        public readonly string Name, Strategy;
        public readonly IReadOnlyList<UnitType> Leaders;
        public readonly IReadOnlyList<Card> StartingDeck, ExclusiveRecruits;
        public readonly IReadOnlyList<UnitType> OpeningCompanions;
        public readonly IReadOnlyList<Card> Items;
        private readonly Card[] deck, supplies;
        private readonly Card reward;

        public ExpeditionFaction(string name, string strategy, UnitType[] leaders, Card[] deck,
            Card[] supplies, Card[] exclusiveRecruits, Card reward, UnitType[] openingCompanions = null, Card[] items = null)
        {
            Name = name;
            Strategy = strategy;
            Leaders = Array.AsReadOnly(leaders);
            StartingDeck = Array.AsReadOnly(deck);
            ExclusiveRecruits = Array.AsReadOnly(exclusiveRecruits);
            OpeningCompanions = Array.AsReadOnly(openingCompanions ?? Array.Empty<UnitType>());
            Items = Array.AsReadOnly(items ?? Array.Empty<Card>());
            this.deck = deck;
            this.supplies = supplies;
            this.reward = reward;
        }

        public StartingLoadout CreateLoadout(UnitType leader) =>
            new(leader, Name, Strategy, reward, supplies, deck);
    }

    public sealed class DispatchRoute
    {
        private readonly IReadOnlyList<RouteStage> stages;
        public IReadOnlyList<StartingLoadout> Loadouts { get; private set; }
        public IReadOnlyList<ExpeditionFaction> Factions { get; }
        public IReadOnlyList<UnitType> Companions { get; }
        private readonly IReadOnlyList<Card> sharedRecruits;
        private readonly IReadOnlyList<Card> sharedItems;
        private readonly Card junk;
        private readonly Card fruit;
        public IReadOnlyList<(string Group, Card Card)> Catalog { get; }
        public IReadOnlyList<UnitType> OpeningCompanions => Faction?.OpeningCompanions.Count > 0 ? Faction.OpeningCompanions : Companions;
        private readonly Random draftRandom;

        public DispatchRoute(StartingLoadout[] loadouts, UnitType[] companions, RouteStage[] stages,
            ExpeditionFaction[] factions = null, Card[] sharedRecruits = null, int? draftSeed = null,
            (string Group, Card Card)[] catalog = null, Card[] sharedItems = null, Card junk = null, Card fruit = null)
        {
            Loadouts = Array.AsReadOnly(loadouts);
            Companions = Array.AsReadOnly(companions);
            this.stages = Array.AsReadOnly(stages);
            Factions = Array.AsReadOnly(factions ?? Array.Empty<ExpeditionFaction>());
            this.sharedRecruits = Array.AsReadOnly(sharedRecruits ?? Array.Empty<Card>());
            this.sharedItems = Array.AsReadOnly(sharedItems ?? Array.Empty<Card>());
            this.junk = junk;
            this.fruit = fruit;
            Catalog = Array.AsReadOnly(catalog ?? Array.Empty<(string, Card)>());
            draftRandom = draftSeed.HasValue ? new Random(draftSeed.Value) : new Random();
        }

        private readonly List<Card> deck = new();
        public int Stage { get; private set; } = 1;
        public int StageCount => stages.Count;
        public int DeckCount => deck.Count;
        public IReadOnlyList<Card> Deck => deck.AsReadOnly();
        public string StageTitle => stages[Stage - 1].Title;
        public IReadOnlyList<Encounter> EncounterChoices => stages[Stage - 1].Encounters;
        public ExpeditionFaction Faction { get; private set; }
        public bool RecruitCompanions => Faction != null && stages[Stage - 1].RecruitCompanions && Stage < StageCount;
        public StartingLoadout Loadout { get; private set; }
        public Card CaptainCard { get; private set; }
        public Card CompanionCard { get; private set; }
        public UnitType Companion => CompanionCard?.Unit;
        public Card Supply { get; private set; }
        public bool NeedsSupply => Loadout != null && Loadout.Supplies.Count != 0 && Supply == null;
        public IReadOnlyList<Card> Rewards { get; private set; } = Array.Empty<Card>();
        public Expedition Battle { get; private set; }
        public Pendant PendingPendant { get; private set; }
        public int PendantTargetCount => deck.Count + 2;
        private bool rewardClaimed;
        public bool CanClaimReward => Battle?.Phase == BattlePhase.Won && Stage < StageCount && !rewardClaimed;

        public bool ChooseFaction(int index)
        {
            if (Faction != null || Loadout != null || index < 0 || index >= Factions.Count)
                return false;
            Faction = Factions[index];
            Loadouts = Array.AsReadOnly(DrawChoices(Faction.Leaders, 3).Select(Faction.CreateLoadout).ToArray());
            return true;
        }

        public bool ChooseLoadout(int index)
        {
            if (Loadout != null || index < 0 || index >= Loadouts.Count)
                return false;
            Loadout = Loadouts[index];
            CaptainCard = new Card(Loadout.Captain).Recruit();
            foreach (Card card in Loadout.Deck)
                deck.Add(card.Recruit());
            return true;
        }

        public bool ChooseCompanion(int index)
        {
            if (Loadout == null || Companion != null || index < 0 || index >= OpeningCompanions.Count)
                return false;
            CompanionCard = new Card(OpeningCompanions[index]).Recruit();
            return true;
        }

        public bool ChooseEncounter(int index)
        {
            if (CompanionCard == null || NeedsSupply || Battle != null || index < 0 || index >= EncounterChoices.Count)
                return false;
            Encounter site = EncounterChoices[index];
            Rewards = BuildRewards(site);
            Battle = new Expedition(site, deck, CaptainCard, CompanionCard, junk, fruit);
            return true;
        }

        public bool ChooseSupply(int index)
        {
            if (Companion == null || Supply != null || index < 0 || index >= Loadout.Supplies.Count)
                return false;
            Supply = Loadout.Supplies[index].Recruit();
            deck.Insert(0, Supply);
            return true;
        }

        public bool RestartBattle()
        {
            if (Battle == null || Battle.Phase is BattlePhase.Won or BattlePhase.Resolving)
                return false;
            Battle = new Expedition(Battle.Location, deck, CaptainCard, CompanionCard, junk, fruit);
            return true;
        }

        public bool ClaimReward(int index)
        {
            if (!CanClaimReward || index < 0 || index >= Rewards.Count)
                return false;
            rewardClaimed = true;
            deck.Insert(0, Rewards[index].Recruit());
            PendingPendant = Battle.Location.RewardPendant;
            if (!deck.Any(CanWearPendant) && !CanWearPendant(CaptainCard) && !CanWearPendant(CompanionCard))
                AdvanceStage();
            return true;
        }

        private IReadOnlyList<Card> BuildRewards(Encounter site)
        {
            if (Stage == StageCount)
                return Array.Empty<Card>();
            if (!RecruitCompanions)
            {
                if (Faction?.Items.Count > 0)
                    return Array.AsReadOnly(DrawChoices(Faction.Items, 2).Concat(DrawChoices(sharedItems, 1)).ToArray());
                return Array.AsReadOnly(new[] { Loadout.Reward }.Concat(site.RewardCards).ToArray());
            }

            var candidates = DrawChoices(Faction.ExclusiveRecruits, 2).Concat(DrawChoices(sharedRecruits, 1));
            return Array.AsReadOnly(candidates.ToArray());
        }

        private IEnumerable<T> DrawChoices<T>(IReadOnlyList<T> pool, int count)
        {
            var remaining = pool.Distinct().ToList();
            while (count-- > 0 && remaining.Count != 0)
            {
                int index = draftRandom.Next(remaining.Count);
                yield return remaining[index];
                remaining.RemoveAt(index);
            }
        }

        private bool CanWearPendant(Card card) =>
            PendingPendant != Pendant.None && card?.Unit != null && card.Pendant == Pendant.None &&
            (PendingPendant != Pendant.TuningRing || card.Unit.Ability.Effect != EffectKind.None) &&
            (PendingPendant != Pendant.SwiftClasp || card.Unit.Period > 1);

        public Card GetPendantTarget(int index)
        {
            if (index < 0 || index >= PendantTargetCount)
                return null;
            if (index < deck.Count)
                return deck[index];
            return index == deck.Count ? CaptainCard : CompanionCard;
        }

        public bool CanEquipPendant(int index) => CanWearPendant(GetPendantTarget(index));

        public bool EquipPendant(int index)
        {
            if (!CanEquipPendant(index))
                return false;
            Card equipped = GetPendantTarget(index).Equip(PendingPendant);
            if (index < deck.Count)
                deck[index] = equipped;
            else if (index == deck.Count)
                CaptainCard = equipped;
            else
                CompanionCard = equipped;
            AdvanceStage();
            return true;
        }

        private void AdvanceStage()
        {
            PendingPendant = Pendant.None;
            rewardClaimed = false;
            Stage++;
            Battle = null;
        }
    }
}
