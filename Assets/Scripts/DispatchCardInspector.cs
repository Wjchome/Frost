using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Frostline
{
    public sealed partial class DispatchBoard
    {
        private VisualElement cardInspector;
        private bool inspectorHovered;
        private IVisualElementScheduledItem hideInspector;
        private PanelSettings cardPanelSettings;
        private int cardViewportHeight;

        private void BindCardInspector()
        {
            cardInspector = Element("card-inspector");
            cardInspector.name = "card-inspector";
            cardInspector.pickingMode = PickingMode.Position;
            cardInspector.style.display = DisplayStyle.None;
            cardInspector.RegisterCallback<PointerEnterEvent>(_ =>
            {
                inspectorHovered = true;
                hideInspector?.Pause();
            });
            cardInspector.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                inspectorHovered = false;
                UpdateCardInspector();
            });
            screen.Add(cardInspector);
            cardPanelSettings = GetComponent<UIDocument>().panelSettings;
            screen.RegisterCallback<GeometryChangedEvent>(_ => UpdateCardScale());
            UpdateCardScale();
        }

        private void UpdateCardScale()
        {
            int height = cardPanelSettings?.targetTexture != null ? cardPanelSettings.targetTexture.height : Screen.height;
            if (height == cardViewportHeight) return;
            cardViewportHeight = height;
            screen.EnableInClassList("compact-cards", height < 820);
        }

        private void DismissCardInspector()
        {
            hideInspector?.Pause();
            inspectorHovered = false;
            cardInspector.style.display = DisplayStyle.None;
        }

        private VisualElement StatusChip(CardStatus status)
        {
            var chip = Stat(status.Icon, status.Value, "effect-chip");
            chip.AddToClassList(CardStatusView.ColorClass(status.Effect));
            chip.tooltip = status.Summary;
            chip.userData = status;
            chip.pickingMode = PickingMode.Position;
            return chip;
        }

        private void UpdateCardInspector()
        {
            if (cardInspector == null) return;
            if (showingTitle || game == null || game.Phase != BattlePhase.Planning || deckPage.HasValue ||
                confirmRestart || ghost != null || selectedUnit.HasValue)
            {
                DismissCardInspector();
                return;
            }

            Unit unit = hoveredSlot.HasValue ? game.At(hoveredSlot.Value) : null;
            TerrainTile terrain = hoveredSlot.HasValue ? game.TerrainAt(hoveredSlot.Value) : default;
            Card card = hoveredCard >= 0 && hoveredCard < game.Hand.Count ? game.Hand[hoveredCard] : null;
            Card targetedCard = hoveredSlot.HasValue && selectedCard >= 0 && selectedCard < game.Hand.Count
                ? game.Hand[selectedCard] : null;
            if (unit == null && card?.Unit != null) unit = DeploymentPreview(card, game.IsCaptainCard(card));
            if (unit == null && card == null && targetedCard == null && !terrain.HasEffects)
            {
                if (!inspectorHovered)
                {
                    hideInspector?.Pause();
                    // Let the pointer cross the small gap from the card to the list of statuses.
                    hideInspector = cardInspector.schedule.Execute(() =>
                    {
                        if (!inspectorHovered) DismissCardInspector();
                    }).StartingIn(180);
                }
                return;
            }
            hideInspector?.Pause();

            VisualElement anchor = hoveredSlot.HasValue ? slots[hoveredSlot.Value] : cards[hoveredCard];
            CardStatus[] active = unit == null ? Array.Empty<CardStatus>() : CardStatusView.Read(unit).ToArray();
            cardInspector.Clear();
            cardInspector.Add(Text(unit?.Type.Name ?? card?.Name ?? targetedCard?.Name ?? "格子效果", "inspector-title"));
            if (targetedCard != null && game.CanPlay(selectedCard, hoveredSlot.Value))
                cardInspector.Add(Text(targetedCard.Name + " · " + CardDetails(targetedCard, unit), "inspector-description"));
            if (unit != null)
            {
                cardInspector.Add(Text($"生命 {unit.Health}/{unit.MaxHealth}   攻击 {(unit.Type.Passive ? "—" : unit.Attack.ToString())}" +
                    (unit.Period > 0 ? $"   倒计时 {unit.Counter}/{unit.Period}" : ""), "inspector-vitals"));
                string ability = unit.Type.Ability.Effect == EffectKind.None ? "" :
                    unit.Type.Ability.Describe(unit.AbilityBonus, unit.Attack, unit.Pattern, unit.Health);
                if (ability.Length > 0)
                    cardInspector.Add(Text(ability, "inspector-description"));
                if (unit.Type.Summon)
                    cardInspector.Add(Text("召唤 · 无法召回，阵亡后满生命回到弃牌堆。", "inspector-note"));
                if (unit.HasCounterattack)
                    cardInspector.Add(Text($"反击 · 当前攻击 {unit.CounterattackPower}，受单位攻击并存活时触发。", "inspector-note"));
                if (unit.Type.ResistSnow)
                    cardInspector.Add(Text("抗雪球 · 最多承受 1 层雪球。", "inspector-note"));
                if (unit.Ink > 0)
                    cardInspector.Add(Text("墨水生效中 · 触发能力已停用", "inspector-warning"));
            }
            else if (card != null)
                cardInspector.Add(Text(CardDetails(card, includeRules: false), "inspector-description"));

            CardStatus[] definitions = unit == null && card != null ? CardStatusView.Read(card).ToArray() : Array.Empty<CardStatus>();
            AddInspectorStatuses(active.Length > 0 ? active : definitions, active.Length > 0,
                active.Length > 0 ? $"当前状态 · {active.Length}" : "效果说明");

            CardStatus[] ground = CardStatusView.Read(terrain).ToArray();
            AddInspectorStatuses(ground, true, "格子效果");
            float width = Mathf.Min(298f, screen.layout.width * 0.33f);
            Vector2 origin = screen.WorldToLocal(anchor.worldBound.position);
            float left = origin.x + anchor.layout.width + 10f;
            if (left + width > screen.layout.width - 12f) left = origin.x - width - 10f;
            // Keep hand-card descriptions above the hand and battlefield descriptions next to the unit.
            float estimatedHeight = Mathf.Min(screen.layout.height - 110f,
                155f + Mathf.Min(360f, (Math.Max(active.Length, definitions.Length) + ground.Length) * 76f));
            float top = card != null ? origin.y - estimatedHeight - 12f : origin.y;
            cardInspector.style.width = width;
            cardInspector.style.maxHeight = Mathf.Max(200f, screen.layout.height - 110f);
            cardInspector.style.left = Mathf.Clamp(left, 10f, Mathf.Max(10f, screen.layout.width - width - 10f));
            cardInspector.style.top = Mathf.Clamp(top, 90f, Mathf.Max(90f, screen.layout.height - estimatedHeight - 12f));
            cardInspector.style.display = DisplayStyle.Flex;
        }

        private void AddInspectorStatuses(CardStatus[] statuses, bool live, string title)
        {
            if (statuses.Length == 0) return;
            cardInspector.Add(Text(title, "inspector-section"));
            var list = new ScrollView(ScrollViewMode.Vertical)
            {
                name = "inspector-statuses",
                horizontalScrollerVisibility = ScrollerVisibility.Hidden,
                verticalScrollerVisibility = ScrollerVisibility.Auto
            };
            list.AddToClassList("inspector-statuses");
            foreach (CardStatus status in statuses)
            {
                var row = Element("inspector-status");
                row.AddToClassList(CardStatusView.ColorClass(status.Effect));
                var heading = Element("inspector-status-heading");
                heading.Add(Icon(status.Icon, "inspector-status-icon"));
                heading.Add(Text(live ? $"{status.Name} {status.Value}" : status.Name, "inspector-status-name"));
                row.Add(heading);
                row.Add(Text(status.Rule, "inspector-status-rule"));
                list.Add(row);
            }
            cardInspector.Add(list);
        }
    }
}
