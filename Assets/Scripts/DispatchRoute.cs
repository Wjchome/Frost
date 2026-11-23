using System;
using System.Collections.Generic;

namespace Frostline
{
    public sealed class StartingLoadout
    {
        public readonly UnitType Captain;
        public readonly string DeckName;
        public readonly IReadOnlyList<Card> Deck;

        public StartingLoadout(UnitType captain, string deckName, params Card[] deck)
        {
            Captain = captain;
            DeckName = deckName;
            Deck = Array.AsReadOnly(deck);
        }
    }

    public sealed class DispatchRoute
    {
        private static readonly IReadOnlyList<Encounter> LowSites =
            Array.AsReadOnly(new[] { Encounter.Valley, Encounter.Outpost });
        private static readonly IReadOnlyList<Encounter> HighSites =
            Array.AsReadOnly(new[] { Encounter.Bridge, Encounter.Ridge });
        public static readonly IReadOnlyList<Card> Rewards =
            Array.AsReadOnly(new[] { Catalog.GullyGuide, Catalog.HeaterCore, Catalog.MagneticScreen });

        public static readonly IReadOnlyList<StartingLoadout> Loadouts = Array.AsReadOnly(new[]
        {
            new StartingLoadout(Catalog.Captain, "巡路牌组", Catalog.Starter),
            new StartingLoadout(
                new UnitType("铆桥队长", "mechanic", 2, 13, 3, pattern: AttackPattern.Barrage,
                    ability: new(EffectTrigger.Hit, EffectKind.Overload, 2)),
                "过载牌组",
                new(Catalog.Chipper), Catalog.Flux, Catalog.Spear, new(Catalog.Guide), Catalog.Brace, Catalog.Patch,
                Catalog.Flux, Catalog.Dye, Catalog.Dulling, Catalog.ChiliOil, Catalog.Chill, Catalog.Calibration,
                new(Catalog.Runner), Catalog.Whistle, Catalog.Patch, Catalog.Spear),
            new StartingLoadout(
                new UnitType("霜径领航员", "courier", 2, 12, 3, pattern: AttackPattern.Longshot,
                    ability: new(EffectTrigger.Hit, EffectKind.Snow, 2)),
                "雪球牌组",
                new(Catalog.Guide), Catalog.Chill, Catalog.Chill, Catalog.Spear, Catalog.Whistle, new(Catalog.Runner),
                Catalog.Chill, Catalog.Patch, Catalog.Dye, Catalog.Dulling, Catalog.Calibration, Catalog.ChiliOil,
                Catalog.Brace, Catalog.Spear, Catalog.Patch, new(Catalog.Chipper))
        });

        public static readonly IReadOnlyList<UnitType> Companions = Array.AsReadOnly(new[]
        {
            new UnitType("寻路幼兽", "hound", 1, 5, 3, ability: new(EffectTrigger.Hit, EffectKind.Snow, 1)),
            new UnitType("巡灯蜂", "drone", 1, 4, 2, pattern: AttackPattern.Longshot,
                ability: new(EffectTrigger.Hit, EffectKind.Ink, 2)),
            new UnitType("雪绒盾卫", "warden", 1, 6, 3, ability: new(EffectTrigger.Deploy, EffectKind.Shell, 2))
        });

        private readonly List<Card> deck = new();
        public int Stage { get; private set; } = 1;
        public int StageCount => 2;
        public int DeckCount => deck.Count;
        public IReadOnlyList<Card> Deck => deck.AsReadOnly();
        public IReadOnlyList<Encounter> EncounterChoices => Stage == 1 ? LowSites : HighSites;
        public StartingLoadout Loadout { get; private set; }
        public UnitType Companion { get; private set; }
        public Expedition Battle { get; private set; }
        public Pendant PendingPendant { get; private set; }
        public bool CanClaimReward => Battle?.Phase == BattlePhase.Won && Stage == 1 && PendingPendant == Pendant.None;

        public bool ChooseLoadout(int index)
        {
            if (Loadout != null || index < 0 || index >= Loadouts.Count)
                return false;
            Loadout = Loadouts[index];
            deck.AddRange(Loadout.Deck);
            return true;
        }

        public bool ChooseCompanion(int index)
        {
            if (Loadout == null || Companion != null || index < 0 || index >= Companions.Count)
                return false;
            Companion = Companions[index];
            return true;
        }

        public bool ChooseEncounter(int index)
        {
            if (Companion == null || Battle != null || index < 0 || index >= EncounterChoices.Count)
                return false;
            Battle = new Expedition(EncounterChoices[index], deck, Loadout.Captain, Companion);
            return true;
        }

        public bool RestartBattle()
        {
            if (Battle == null || Battle.Phase is BattlePhase.Won or BattlePhase.Resolving)
                return false;
            Battle = new Expedition(Battle.Location, deck, Loadout.Captain, Companion);
            return true;
        }

        public bool ClaimReward(int index)
        {
            if (!CanClaimReward || index < 0 || index >= Rewards.Count)
                return false;
            deck.Insert(0, Rewards[index]);
            PendingPendant = Battle.Location.RewardPendant;
            return true;
        }

        public bool CanEquipPendant(int index) =>
            PendingPendant != Pendant.None && index >= 0 && index < deck.Count &&
            deck[index].Unit != null && deck[index].Pendant == Pendant.None &&
            (PendingPendant != Pendant.TuningRing || deck[index].Unit.Ability.Effect != EffectKind.None);

        public bool EquipPendant(int index)
        {
            if (!CanEquipPendant(index))
                return false;
            deck[index] = new Card(deck[index].Unit, pendant: PendingPendant);
            PendingPendant = Pendant.None;
            Stage++;
            Battle = null;
            return true;
        }
    }
}
