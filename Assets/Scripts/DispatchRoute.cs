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

        public RouteStage(string title, Encounter[] encounters)
        {
            Title = title;
            Encounters = Array.AsReadOnly(encounters);
        }
    }

    public sealed class DispatchRoute
    {
        private readonly IReadOnlyList<RouteStage> stages;
        public IReadOnlyList<StartingLoadout> Loadouts { get; }
        public IReadOnlyList<UnitType> Companions { get; }

        public DispatchRoute(StartingLoadout[] loadouts, UnitType[] companions, RouteStage[] stages)
        {
            Loadouts = Array.AsReadOnly(loadouts);
            Companions = Array.AsReadOnly(companions);
            this.stages = Array.AsReadOnly(stages);
        }

        private readonly List<Card> deck = new();
        public int Stage { get; private set; } = 1;
        public int StageCount => stages.Count;
        public int DeckCount => deck.Count;
        public IReadOnlyList<Card> Deck => deck.AsReadOnly();
        public string StageTitle => stages[Stage - 1].Title;
        public IReadOnlyList<Encounter> EncounterChoices => stages[Stage - 1].Encounters;
        public StartingLoadout Loadout { get; private set; }
        public Card CaptainCard { get; private set; }
        public Card CompanionCard { get; private set; }
        public UnitType Companion => CompanionCard?.Unit;
        public Card Supply { get; private set; }
        public IReadOnlyList<Card> Rewards { get; private set; } = Array.Empty<Card>();
        public Expedition Battle { get; private set; }
        public Pendant PendingPendant { get; private set; }
        public int PendantTargetCount => deck.Count + 2;
        private bool rewardClaimed;
        public bool CanClaimReward => Battle?.Phase == BattlePhase.Won && Stage < StageCount && !rewardClaimed;

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
            if (Loadout == null || Companion != null || index < 0 || index >= Companions.Count)
                return false;
            CompanionCard = new Card(Companions[index]).Recruit();
            return true;
        }

        public bool ChooseEncounter(int index)
        {
            if (Supply == null || Battle != null || index < 0 || index >= EncounterChoices.Count)
                return false;
            Encounter site = EncounterChoices[index];
            Rewards = Stage < StageCount ? Array.AsReadOnly(new[] { Loadout.Reward }.Concat(site.RewardCards).ToArray())
                : Array.Empty<Card>();
            Battle = new Expedition(site, deck, CaptainCard, CompanionCard);
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
            Battle = new Expedition(Battle.Location, deck, CaptainCard, CompanionCard);
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
