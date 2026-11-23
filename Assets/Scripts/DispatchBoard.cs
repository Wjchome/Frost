using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Frostline
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class DispatchBoard : MonoBehaviour
    {
        private readonly Dictionary<string, Texture2D> textures = new();
        private readonly Dictionary<Position, Button> slots = new();
        private readonly List<Button> cards = new();
        private readonly Dictionary<VisualElement, (Action Click, Action Select)> draggable = new();
        private VisualElement screen, formations, hand, overlay, choices;
        private Label turn, wave, waveUnits, objective, notice, hint, drawCount, discardCount, resultTitle, resultDetail, redrawNote;
        private Button redraw, recall, restart, orderButton;
        private Label orderNote;
        private bool showOrder;
        private int hoveredCard = -1;
        private Position? hoveredSlot;
        private Expedition game;
        private Coroutine resolving;
        private BattleBeat? beat;
        private int selectedCard = -1;
        private Position? selectedUnit;
        private bool confirmRestart;
        private Action beginDrag, clickDrag;
        private int dragPointer = -1;
        private Vector2 dragOrigin;
        private VisualElement ghost, dragSource;
        private Vector2 ghostPosition, ghostDestination;
        private float ghostTilt;
        private Coroutine returning;
        private VisualElement attackView;
        private Vector2 attackHome, attackContact;
        private float attackTilt;

        private void OnEnable()
        {
            Application.targetFrameRate = 60;
            var root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear();
            root.style.flexGrow = 1;
            var tree = Resources.Load<VisualTreeAsset>("UI/Expedition").Instantiate();
            tree.style.flexGrow = 1;
            root.Add(tree);
            screen = root.Q("screen");
            screen.styleSheets.Add(Resources.Load<StyleSheet>("UI/ExpeditionLayout"));
            screen.style.unityFontDefinition = FontDefinition.FromFont(Resources.Load<Font>("Fonts/FrostlineSans"));
            Paint(screen.Q("background"), "Art/canyon");

            formations = screen.Q("formations");
            hand = screen.Q("hand");
            overlay = screen.Q("overlay");
            choices = screen.Q("choices");
            objective = screen.Q<Label>("objective");
            turn = screen.Q<Label>("turn");
            wave = screen.Q<Label>("wave");
            waveUnits = screen.Q<Label>("wave-units");
            notice = screen.Q<Label>("notice");
            hint = screen.Q<Label>("hint");
            drawCount = screen.Q<Label>("draw-count");
            discardCount = screen.Q<Label>("discard-count");
            resultTitle = screen.Q<Label>("result-title");
            resultDetail = screen.Q<Label>("result-detail");
            redraw = screen.Q<Button>("redraw");

            redrawNote = screen.Q<Label>("redraw-note");
            recall = screen.Q<Button>("recall");
            restart = screen.Q<Button>("restart");
            orderButton = screen.Q<Button>("action-order");
            orderNote = screen.Q<Label>("order-note");
            orderButton.clicked += ToggleOrder;
            redraw.clicked += Redraw;

            recall.clicked += Recall;
            restart.clicked += () =>
            {
                confirmRestart = true;
                RenderOverlay();
            };
            var bellIcon = Element("bell-icon");
            Paint(bellIcon, "CardArt/icon_bell");
            redraw.Add(bellIcon);
            foreach (Button button in new[] { redraw, recall, restart, orderButton })
                button.focusable = false;

            screen.RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            screen.RegisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
            screen.RegisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
            screen.RegisterCallback<PointerCancelEvent>(_ =>
            {
                ClearDrag();
                ApplyTargets();
            }, TrickleDown.TrickleDown);
            screen.RegisterCallback<PointerLeaveEvent>(evt =>
            {
                if (evt.target != screen || ghost != null)
                    return;
                hoveredSlot = null;
                hoveredCard = -1;
                ApplyTargets();
            });
            ResetRun();
        }

        private void OnDisable()
        {
            if (resolving != null)
                StopCoroutine(resolving);
            resolving = null;
            ClearDrag();
            StopReturn();
            ClearAttackView();
        }

        private void Update()
        {
            if (ghost != null)
            {
                float blend = 1f - Mathf.Exp(-24f * Time.unscaledDeltaTime);
                ghostPosition = Vector2.Lerp(ghostPosition, ghostDestination, blend);
                float desiredTilt = Mathf.Clamp((ghostDestination.x - ghostPosition.x) * 0.13f, -12f, 12f);
                ghostTilt = Mathf.Lerp(ghostTilt, desiredTilt, blend);
                Place(ghost, ghostPosition);
                ghost.style.rotate = new Rotate(Angle.Degrees(ghostTilt));
            }

            if (game?.Phase != BattlePhase.Planning)
                return;
            if (Input.GetKeyDown(KeyCode.Tab) && !confirmRestart)
                ToggleOrder();

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
            {
                confirmRestart = false;
                ClearDrag();
                ClearSelection();
                RenderOverlay();
            }
            if (Input.GetKeyDown(KeyCode.Space) && !confirmRestart)
                Redraw();
        }

        private void ResetRun()
        {
            if (resolving != null)
                StopCoroutine(resolving);
            resolving = null;
            beat = null;
            confirmRestart = false;
            ClearDrag();
            StopReturn();
            ClearAttackView();
            showOrder = false;
            game = new Expedition();
            selectedCard = -1;
            selectedUnit = null;
            Render();
        }

        private void Redraw()
        {
            if (game.CanRedraw)
                BeginSequence(game.Redraw());
        }


        private void Recall()
        {
            if (selectedUnit.HasValue && game.Recall(selectedUnit.Value))
            {
                selectedUnit = null;
                Render();
            }
        }

        private void BeginSequence(IEnumerable<BattleBeat> sequence)
        {
            if (game.Phase != BattlePhase.Planning || resolving != null || confirmRestart)
                return;

            selectedCard = -1;
            selectedUnit = null;
            ClearDrag();
            StopReturn();
            resolving = StartCoroutine(Animate(sequence));
        }

        private IEnumerator Animate(IEnumerable<BattleBeat> sequence)
        {
            foreach (BattleBeat next in sequence)
            {
                beat = next;
                if (next.Effect == BattleEffect.Windup)
                    yield return PlayWindup(next);
                else
                {
                    Render();
                    yield return PlayImpact(next);
                }
            }

            beat = null;
            Render();
            // Always yield once so StartCoroutine has assigned its handle.
            yield return null;
            resolving = null;
        }

        private IEnumerator PlayWindup(BattleBeat action)
        {
            Render();
            yield return null;
            var target = slots[action.Target];
            var source = slots[action.Source];
            Rect targetRect = target.worldBound;
            Rect sourceRect = source.worldBound;
            Vector2 start = screen.WorldToLocal(sourceRect.position);
            Vector2 destination = screen.WorldToLocal(targetRect.position);
            if (action.CardStrike)
            {
                var flare = Element("strike-trail");
                flare.style.width = 12;
                flare.style.height = 65;
                screen.Add(flare);
                Vector2 finish = screen.WorldToLocal(targetRect.center) - new Vector2(6, 33);
                yield return Tween(0.16f, t =>
                {
                    Place(flare, finish + new Vector2(-95 * (1f - t), 100 * (1f - t)));
                    flare.style.opacity = t;
                });
                flare.RemoveFromHierarchy();
                yield break;
            }

            var actor = game.At(action.Source);
            if (actor == null)
                yield break;

            var motion = FloatingCard(sourceRect);
            DrawUnitFace(motion, actor);
            source.AddToClassList("motion-source");
            Vector2 direction = (destination - start).normalized;
            Vector2 recoil = start - direction * 22f;
            Vector2 end = Vector2.Lerp(start, destination, 0.72f);
            float tilt = direction.x * 12f;
            yield return Tween(0.12f, t =>
            {
                Place(motion, Vector2.Lerp(start, recoil, t));
                motion.style.rotate = new Rotate(Angle.Degrees(-tilt * t));
            });
            yield return Tween(0.18f, t =>
            {
                Place(motion, Vector2.Lerp(recoil, end, t * t));
                motion.style.rotate = new Rotate(Angle.Degrees(Mathf.Lerp(-tilt, tilt, t)));
            });
            attackView = motion;
            attackHome = start;
            attackContact = end;
            attackTilt = tilt;
        }

        private IEnumerator PlayImpact(BattleBeat action)
        {
            if (action.Effect != BattleEffect.Attack)
            {
                yield return new WaitForSecondsRealtime(action.Effect == BattleEffect.Arrival ? 0.25f : 0.32f);
                yield break;
            }

            yield return null;
            if (attackView != null)
                slots[action.Source].AddToClassList("motion-source");

            var targetViews = new List<Button>(action.Targets.Count);
            foreach (Position pos in action.Targets)
                targetViews.Add(slots[pos]);

            yield return Tween(0.29f, t =>
            {
                if (attackView != null)
                {
                    float returnProgress = 1f - Mathf.Pow(1f - t, 2f);
                    Place(attackView, Vector2.Lerp(attackContact, attackHome, returnProgress));
                    attackView.style.rotate = new Rotate(Angle.Degrees(attackTilt * (1f - returnProgress)));
                }

                float kick = Mathf.Sin(t * Mathf.PI * 6f) * (1f - t) * 12f;
                foreach (Button target in targetViews)
                {
                    target.style.translate = new Translate(kick, 0);
                    var mark = target.Q<Label>(className: "burst");
                    if (mark != null)
                    {
                        mark.style.translate = new Translate(0, -24f * t);
                        mark.style.opacity = Mathf.Clamp01((1f - t) * 3f);
                    }
                }
            });

            foreach (Button target in targetViews)
                target.style.translate = StyleKeyword.Null;
            if (attackView != null)
                slots[action.Source].RemoveFromClassList("motion-source");
            ClearAttackView();
        }

        private void ClearAttackView()
        {
            attackView?.RemoveFromHierarchy();
            attackView = null;
        }

        private static IEnumerator Tween(float duration, Action<float> apply)
        {
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                apply(elapsed / duration);
                yield return null;
            }
            apply(1f);
        }

        private void ClearSelection()
        {
            selectedCard = -1;
            selectedUnit = null;
            ApplyTargets();
        }

        private void SelectCard(int index, bool toggle)
        {
            if (game.Phase != BattlePhase.Planning)
                return;
            selectedCard = toggle && selectedCard == index ? -1 : index;
            selectedUnit = null;
            ApplyTargets();
        }

        private void SelectUnit(Position pos, bool toggle)
        {
            selectedCard = -1;
            selectedUnit = toggle && selectedUnit.HasValue && selectedUnit.Value.Equals(pos) ? null : pos;
            ApplyTargets();
        }

        private void UseSlot(Position pos)
        {
            if (game.Phase != BattlePhase.Planning)
                return;

            if (selectedCard >= 0)
            {
                if (game.CanPlay(selectedCard, pos))
                    BeginSequence(game.Play(selectedCard, pos));
                return;
            }

            if (selectedUnit.HasValue && game.Move(selectedUnit.Value, pos))
            {
                selectedUnit = null;
                Render();
                return;
            }

            if (!pos.Enemy && game.At(pos) != null)
                SelectUnit(pos, toggle: true);
        }

        private void Render()
        {
            bool planning = game.Phase == BattlePhase.Planning;
            objective.text = "保护岚哨长，击退三波敌人";
            turn.text = game.Opening ? "开局部署" : $"第 {game.Turn} 回合";
            wave.text = game.Wave == Expedition.WaveSize
                ? "最终波"
                : $"第 {game.Wave} 波  ·  增援 {game.Reinforcements} 拍";
            waveUnits.style.display = game.NextWave.Count == 0 ? DisplayStyle.None : DisplayStyle.Flex;
            waveUnits.text = string.Join("  +  ", game.NextWave.Select(unit => unit.Name));
            notice.text = game.Notice;
            drawCount.text = $"抽牌 {game.DrawCount}";
            discardCount.text = $"弃牌 {game.DiscardCount}";
            redraw.text = game.Refresh == 0 ? "免费换牌" : $"换牌  {game.Refresh}";
            redraw.SetEnabled(game.CanRedraw);
            redrawNote.text = game.Opening ? "部署队长后开启" : game.Refresh == 0 ? "空格敲铃" : "提前敲铃消耗一回合";
            redraw.EnableInClassList("redraw-ready", !game.Opening && game.Refresh == 0);
            restart.SetEnabled(planning);
            hoveredCard = -1;
            hoveredSlot = null;
            draggable.Clear();
            RenderFormation(planning);
            RenderHand(planning);
            ApplyTargets();
            RenderOverlay();
        }

        private void ToggleOrder()
        {
            if (game.Phase != BattlePhase.Planning || game.Opening)
                return;

            showOrder = !showOrder;
            ApplyOrder();
        }

        private void ApplyOrder()
        {
            bool visible = showOrder && game.Phase == BattlePhase.Planning && !game.Opening;
            orderButton.SetEnabled(game.Phase == BattlePhase.Planning && !game.Opening);
            orderButton.EnableInClassList("order-active", visible);
            orderButton.text = visible ? "隐藏顺序 [Tab]" : "行动顺序 [Tab]";
            orderNote.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            foreach (var slot in slots)
            {
                slot.Value.Q<Label>(className: "order-badge")?.RemoveFromHierarchy();
                slot.Value.Q<Label>(className: "arrival-badge")?.RemoveFromHierarchy();
            }

            if (!visible)
                return;

            bool previewCard = hoveredSlot.HasValue && game.CanPlay(selectedCard, hoveredSlot.Value);
            var (actions, arrivals) = game.ForecastTurn(selectedCard, hoveredSlot);
            orderNote.text = (previewCard ? "出牌后预览" : "下一拍预览") +
                (actions.Count == 0 ? " · 无单位行动" : "") +
                (arrivals.Count > 0 ? " · 本回合增援" : "");
            for (int i = 0; i < actions.Count; i++)
                slots[actions[i]].Add(Text((i + 1).ToString(), "order-badge"));
            foreach (var arrival in arrivals)
                slots[arrival.Position].Add(Text($"增援 · {arrival.Unit.Name}", "arrival-badge"));
        }

        private void RenderFormation(bool planning)
        {
            slots.Clear();
            formations.Clear();
            for (int row = 0; row < Expedition.Rows; row++)
            {
                var line = Element("formation");
                var crew = Element("side");
                crew.AddToClassList("allies");
                var opposing = Element("side");
                opposing.AddToClassList("enemies");
                var midline = Element("midline");
                midline.Add(Text(row == 0 ? "上排" : "下排", "row-label"));

                for (int slot = Expedition.Slots - 1; slot >= 0; slot--)
                    crew.Add(BuildSlot(new Position(false, row, slot), planning));
                for (int slot = 0; slot < Expedition.Slots; slot++)
                    opposing.Add(BuildSlot(new Position(true, row, slot), planning));
                line.Add(crew);
                line.Add(midline);
                line.Add(opposing);
                formations.Add(line);
            }
        }

        private Button BuildSlot(Position pos, bool planning)
        {
            Unit unit = game.At(pos);
            Button slot = ButtonWith(null, () => UseSlot(pos), "slot");
            slot.EnableInClassList("empty", unit == null);
            slot.EnableInClassList("hostile", pos.Enemy);
            slot.EnableInClassList("leader", unit?.Leader == true);
            slot.EnableInClassList("frozen", unit?.Frozen > 0);
            slots.Add(pos, slot);

            if (unit != null)
            {
                DrawUnitFace(slot, unit);
                if (planning && !pos.Enemy)
                    draggable.Add(slot, (() => UseSlot(pos), () => SelectUnit(pos, toggle: false)));
            }
            else
            {
                slot.Add(Text("＋", "empty-mark"));
            }

            if (planning)
            {
                slot.RegisterCallback<PointerEnterEvent>(_ => HoverSlot(pos));
                slot.RegisterCallback<PointerLeaveEvent>(_ =>
                {
                    if (ghost == null && hoveredSlot.HasValue && hoveredSlot.Value.Equals(pos))
                        HoverSlot(null);
                });
            }

            if (beat.HasValue)
            {
                BattleBeat resolved = beat.Value;
                if (resolved.Source.Equals(pos) && !resolved.CardStrike)
                    slot.AddToClassList("source");
                for (int i = 0; i < resolved.Targets.Count; i++)
                {
                    if (!resolved.Targets[i].Equals(pos))
                        continue;

                    if (resolved.Effect == BattleEffect.Windup)
                    {
                        slot.AddToClassList("incoming");
                        break;
                    }
                    Label mark = Text(resolved.ImpactMarks?[i] ?? resolved.Mark, "burst");
                    if (resolved.Effect == BattleEffect.Ice)
                        mark.AddToClassList("ice-burst");
                    if (resolved.Effect is BattleEffect.Burst or BattleEffect.Status)
                        mark.AddToClassList(resolved.Effect == BattleEffect.Burst ? "overload-burst" : "status-burst");
                    if (resolved.Effect is BattleEffect.Recovery or BattleEffect.Arrival or BattleEffect.Guard)
                        mark.AddToClassList("heal-burst");
                    slot.Add(mark);
                    break;
                }
            }
            return slot;
        }

        private void RenderHand(bool planning)
        {
            cards.Clear();
            hand.Clear();
            for (int i = 0; i < game.Hand.Count; i++)
            {
                int index = i;
                Card card = game.Hand[i];
                Button view = ButtonWith(null, () => SelectCard(index, toggle: true), "card");
                DrawHandFace(view, card);
                view.SetEnabled(planning);
                if (planning)
                {
                    draggable.Add(view, (() => SelectCard(index, toggle: true), () => SelectCard(index, toggle: false)));
                    view.RegisterCallback<PointerEnterEvent>(_ => HoverCard(index));
                    view.RegisterCallback<PointerLeaveEvent>(_ =>
                    {
                        if (ghost == null && hoveredCard == index)
                            HoverCard(-1);
                    });
                }
                cards.Add(view);
                hand.Add(view);
            }
        }

        private void HoverCard(int index)
        {
            if (ghost != null)
                return;

            hoveredCard = index;
            if (index >= 0)
                hoveredSlot = null;
            ApplyTargets();
        }

        private void HoverSlot(Position? pos)
        {
            if (ghost != null)
                return;

            hoveredSlot = pos;
            if (pos.HasValue)
                hoveredCard = -1;
            ApplyTargets();
        }

        private void ApplyTargets()
        {
            var affected = new HashSet<Position>();
            bool randomTarget = false;
            int cardIndex = hoveredCard >= 0 ? hoveredCard : selectedCard;
            Unit current = hoveredSlot.HasValue ? game.At(hoveredSlot.Value) : null;
            if (cardIndex >= 0 && hoveredSlot.HasValue)
            {
                foreach (Position pos in game.CardTargets(cardIndex, hoveredSlot.Value))
                    affected.Add(pos);
                if (affected.Count > 0 && game.Hand[cardIndex].Action == CardAction.Hasten &&
                    current.Counter <= game.Hand[cardIndex].Amount)
                {
                    foreach (Position pos in game.AttackTargets(hoveredSlot.Value, current.Type))
                        affected.Add(pos);
                    randomTarget = current.Type.Pattern == AttackPattern.Aimless;
                }
            }
            else if (cardIndex < 0 && !selectedUnit.HasValue && current != null)
            {
                foreach (Position pos in game.AttackTargets(hoveredSlot.Value, current.Type))
                    affected.Add(pos);
                randomTarget = current.Type.Pattern == AttackPattern.Aimless;
            }

            bool any = false;
            foreach (var entry in slots)
            {
                Position pos = entry.Key;
                bool valid = cardIndex >= 0 ? game.CanPlay(cardIndex, pos)
                    : selectedUnit.HasValue && game.CanMove(selectedUnit.Value, pos);
                any |= valid;
                entry.Value.EnableInClassList("target", valid);
                entry.Value.EnableInClassList("affected", affected.Contains(pos));
                entry.Value.EnableInClassList("possible", randomTarget && affected.Contains(pos));
                entry.Value.EnableInClassList("picked", selectedUnit.HasValue && selectedUnit.Value.Equals(pos));
                entry.Value.EnableInClassList("inspect", hoveredSlot.HasValue && hoveredSlot.Value.Equals(pos));
            }

            for (int i = 0; i < cards.Count; i++)
                cards[i].EnableInClassList("selected", i == selectedCard);

            bool canRecall = selectedUnit.HasValue && game.CanRecall(selectedUnit.Value);
            recall.SetEnabled(canRecall);
            recall.EnableInClassList("target", canRecall);
            hint.text = game.Phase == BattlePhase.Resolving ? "结算中…" :
                cardIndex >= 0 ? any ? CardDetails(game.Hand[cardIndex]) : "当前没有合法目标。" :
                selectedUnit.HasValue ? "选择己方位置换位，或拖到收回队员。换位和收回均免费。" :
                current != null ? UnitDetails(current) :
                game.Opening ? "拖队长牌到己方空位。部署不消耗回合。" : "悬停预览目标 · 空格敲换牌铃";
            ApplyOrder();
        }

        private static string CardDetails(Card card)
        {
            if (card.Unit != null)
                return card.Description + UnitDetails(DeploymentPreview(card, leader: false));
            return card.Description + Effects.Rule(card.Effect);
        }

        private static string UnitDetails(Unit unit)
        {
            UnitType type = unit.Type;
            string pattern = Expedition.PatternName(type.Pattern);
            if (type.Pattern == AttackPattern.Aimless)
                pattern += "（从高亮目标随机选一名）";
            string frenzy = type.Frenzy > 1 ? $" · 狂热 ×{type.Frenzy}" : "";
            string ability = type.Ability.Effect == EffectKind.None ? "" : " · " + type.Ability.Describe(unit.EffectBonus);
            string resistance = type.ResistSnow ? " · 最多承受 1 层雪球" : "";
            string ink = unit.Ink > 0 ? $" · 墨水 {unit.Ink}（触发能力停用）" : "";
            string overload = unit.Overload > 0 ? $" · 过载 {unit.Overload}/{unit.Health}，达到生命时引爆" : "";
            return pattern + frenzy + ability + resistance + ink + overload;
        }

        private void RenderOverlay()
        {
            bool finished = game.Phase == BattlePhase.Won || game.Phase == BattlePhase.Lost;
            overlay.style.display = finished || confirmRestart ? DisplayStyle.Flex : DisplayStyle.None;
            choices.Clear();
            if (confirmRestart)
            {
                resultTitle.text = "重开战斗？";
                resultDetail.text = "当前这场战斗将重新开始。";
                choices.Add(ButtonWith("继续战斗", () =>
                {
                    confirmRestart = false;
                    RenderOverlay();
                }, "button"));
                choices.Add(ButtonWith("确认重开", ResetRun, "button", "primary"));
            }
            else if (finished)
            {
                bool won = game.Phase == BattlePhase.Won;
                resultTitle.text = won ? "雪谷开路" : "队长倒下";
                resultDetail.text = won ? $"三波敌人全部击退。用时 {game.Turn - 1} 回合。"
                    : "战斗结束。可以调整布阵，重新挑战。";
                choices.Add(ButtonWith("再打一场", ResetRun, "button", "primary"));
            }
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || game.Phase != BattlePhase.Planning || confirmRestart)
                return;

            for (VisualElement element = evt.target as VisualElement; element != null; element = element.parent)
            {
                if (!draggable.TryGetValue(element, out var entry))
                    continue;

                beginDrag = entry.Select;
                clickDrag = entry.Click;
                dragSource = element;
                dragPointer = evt.pointerId;
                dragOrigin = evt.position;
                screen.CapturePointer(evt.pointerId);
                evt.StopImmediatePropagation();
                return;
            }
        }
        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (beginDrag == null || evt.pointerId != dragPointer)
                return;

            if (ghost == null)
            {
                if (Vector2.Distance(dragOrigin, evt.position) < 8)
                    return;
                hoveredCard = -1;
                beginDrag();
                StopReturn();
                Rect original = dragSource.worldBound;
                ghost = FloatingCard(original);
                if (selectedCard >= 0)
                    DrawHandFace(ghost, game.Hand[selectedCard]);
                else if (selectedUnit.HasValue && game.At(selectedUnit.Value) is Unit unit)
                    DrawUnitFace(ghost, unit);
                dragSource.AddToClassList("motion-source");
                ghostPosition = screen.WorldToLocal(original.position);
                ghostTilt = 0f;
                Place(ghost, ghostPosition);
            }

            Vector2 local = screen.WorldToLocal(evt.position);
            ghostDestination = local - new Vector2(dragSource.worldBound.width * 0.5f, 42f);
            Position? previousSlot = hoveredSlot;
            hoveredSlot = FindSlot(evt.position, out var pos) ? pos : null;
            bool valid = hoveredSlot.HasValue && (selectedCard >= 0 ? game.CanPlay(selectedCard, hoveredSlot.Value) :
                selectedUnit.HasValue && game.CanMove(selectedUnit.Value, hoveredSlot.Value));
            valid |= selectedUnit.HasValue && recall.worldBound.Contains(evt.position) && game.CanRecall(selectedUnit.Value);
            ghost.EnableInClassList("drop-ready", valid);
            if (!Nullable.Equals(previousSlot, hoveredSlot))
                ApplyTargets();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (evt.pointerId != dragPointer)
                return;

            evt.StopImmediatePropagation();
            if (ghost == null)
            {
                Action click = clickDrag;
                ClearDrag();
                click?.Invoke();
                return;
            }

            if (selectedUnit.HasValue && recall.worldBound.Contains(evt.position) && game.CanRecall(selectedUnit.Value))
            {
                ClearDrag();
                Recall();
                return;
            }

            if (FindSlot(evt.position, out var pos) &&
                (selectedCard >= 0 ? game.CanPlay(selectedCard, pos)
                    : selectedUnit.HasValue && game.CanMove(selectedUnit.Value, pos)))
            {
                ClearDrag();
                UseSlot(pos);
                return;
            }

            var card = ghost;
            var source = dragSource;
            Vector2 start = ghostPosition;
            float tilt = ghostTilt;
            ghost = null;
            ClearDrag();
            ClearSelection();
            returning = StartCoroutine(ReturnCard(card, source, start, tilt));
        }

        private bool FindSlot(Vector2 panelPosition, out Position pos)
        {
            foreach (var slot in slots)
            {
                if (slot.Value.worldBound.Contains(panelPosition))
                {
                    pos = slot.Key;
                    return true;
                }
            }

            pos = default;
            return false;
        }

        private VisualElement returnView;

        private IEnumerator ReturnCard(VisualElement card, VisualElement source, Vector2 start, float tilt)
        {
            returnView = card;
            Place(card, start);
            Vector2 end = screen.WorldToLocal(source.worldBound.position);
            yield return Tween(0.20f, t =>
            {
                float ease = 1f - Mathf.Pow(1f - t, 3f);
                Place(card, Vector2.Lerp(start, end, ease));
                card.style.rotate = new Rotate(Angle.Degrees(tilt * (1f - ease)));
                card.style.opacity = Mathf.Lerp(1f, 0.3f, ease);
            });
            card.RemoveFromHierarchy();
            returnView = null;
            returning = null;
        }

        private void StopReturn()
        {
            if (returning != null)
                StopCoroutine(returning);
            returning = null;
            returnView?.RemoveFromHierarchy();
            returnView = null;
        }

        private void ClearDrag()
        {
            int pointer = dragPointer;
            dragPointer = -1;
            if (pointer >= 0 && screen.HasPointerCapture(pointer))
                screen.ReleasePointer(pointer);
            ghost?.RemoveFromHierarchy();
            ghost = null;
            dragSource?.RemoveFromClassList("motion-source");
            dragSource = null;
            beginDrag = null;
            clickDrag = null;
            hoveredSlot = null;
        }

        private VisualElement FloatingCard(Rect bounds)
        {
            var card = Element("floating-card");
            card.style.width = bounds.width;
            card.style.height = bounds.height;
            Place(card, screen.WorldToLocal(bounds.position));
            screen.Add(card);
            return card;
        }

        private static void Place(VisualElement card, Vector2 position)
        {
            card.style.left = position.x;
            card.style.top = position.y;
        }

        private void DrawHandFace(VisualElement view, Card card)
        {
            if (card.Unit != null)
            {
                DrawUnitFace(view, DeploymentPreview(card, game.Opening));
                return;
            }

            view.AddToClassList("item-card");
            if (card.Action == CardAction.ApplyEffect)
            {
                var art = Element("effect-art");
                art.AddToClassList(EffectClass(card.Effect));
                art.Add(Text(Effects.Name(card.Effect), "effect-name"));
                art.Add(Text($"+{card.Amount}", "effect-amount"));
                view.Add(art);
            }
            else
                AddPortrait(view, card.Art);
            view.Add(Text(card.Name, "face-title"));
            view.Add(Text(card.Description, "card-desc"));
            if (card.Damage > 0)
                AddStat(view, "attack", card.Damage.ToString(), "attack-corner");
            switch (card.Action)
            {
                case CardAction.Freeze: AddStat(view, "snow", card.Amount.ToString(), "bottom-badge"); break;
                case CardAction.Heal: AddStat(view, "heal", card.Amount.ToString(), "bottom-badge"); break;
                case CardAction.Hasten: AddStat(view, "timer", card.Amount.ToString(), "bottom-badge"); break;
            }
        }

        private static Unit DeploymentPreview(Card card, bool leader) => new(card.Unit, leader)
        {
            Health = card.DeploymentHealth,
            EffectBonus = card.DeploymentEffectBonus
        };

        private void DrawUnitFace(VisualElement view, Unit unit)
        {
            UnitType type = unit.Type;
            view.AddToClassList("unit-face");
            var art = AddPortrait(view, type.Art);
            if (unit.Spice > 0 || unit.Overload > 0 || unit.Ink > 0 || unit.Weakness > 0 || unit.Block > 0 || unit.EffectBonus > 0)
            {
                var statuses = Element("effect-strip");
                AddEffectBadge(statuses, EffectKind.Spice, unit.Spice);
                AddEffectBadge(statuses, EffectKind.Overload, unit.Overload);
                AddEffectBadge(statuses, EffectKind.Ink, unit.Ink);
                AddEffectBadge(statuses, EffectKind.Weakness, unit.Weakness);
                AddEffectBadge(statuses, EffectKind.Block, unit.Block);
                AddEffectBadge(statuses, EffectKind.EffectBonus, unit.EffectBonus);
                art.Add(statuses);
            }
            view.Add(Text(type.Name, "face-title"));
            var traits = Element("traits");
            if (type.Pattern != AttackPattern.Front || type.ResistSnow)
            {
                var tags = Element("trait-row");
                if (type.Pattern != AttackPattern.Front)
                    tags.Add(Text(Expedition.PatternName(type.Pattern), "trait"));
                if (type.ResistSnow)
                    tags.Add(Text("抗雪球", "trait", "skill"));
                traits.Add(tags);
            }
            if (type.Ability.Effect != EffectKind.None)
            {
                var skill = Text(type.Ability.Describe(unit.EffectBonus), "trait", "skill", "ability-value");
                skill.EnableInClassList("silenced", unit.Ink > 0);
                traits.Add(skill);
            }
            view.Add(traits);
            AddStat(view, "health", $"{unit.Health}/{type.Health}", "health-corner");
            AddStat(view, "attack", unit.Attack.ToString(), "attack-corner");
            AddStat(view, "timer", unit.Counter.ToString(), "bottom-badge");
            if (type.Frenzy > 1)
                view.Add(Text($"狂热 ×{type.Frenzy}", "frenzy-badge"));
            if (unit.Leader)
                AddStat(view, "crown", "", "crown-badge");
            if (unit.Frozen > 0)
                AddStat(view, "snow", unit.Frozen.ToString(), "snow-badge");
            if (unit.Shell > 0)
                AddStat(view, "shell", unit.Shell.ToString(), "shell-badge");
        }

        private static string EffectClass(EffectKind effect) => effect switch
        {
            EffectKind.Spice => "effect-spice",
            EffectKind.Overload => "effect-overload",
            EffectKind.Ink => "effect-ink",
            EffectKind.Weakness => "effect-weakness",
            EffectKind.Block => "effect-block",
            _ => "effect-bonus"
        };

        private static void AddEffectBadge(VisualElement strip, EffectKind effect, int amount)
        {
            if (amount > 0)
                strip.Add(Text($"{Effects.Name(effect)} {amount}", "effect-badge", EffectClass(effect)));
        }

        private VisualElement AddPortrait(VisualElement view, string art)
        {
            var picture = Element("face-art");
            Paint(picture, $"CardArt/{art}");
            view.Add(picture);
            return picture;
        }

        private void AddStat(VisualElement view, string type, string value, string location)
        {
            var stat = Element("stat");
            stat.AddToClassList($"stat-{type}");
            stat.AddToClassList(location);
            var icon = Element("stat-icon");
            Paint(icon, $"CardArt/icon_{type}");
            stat.Add(icon);
            if (!string.IsNullOrEmpty(value))
                stat.Add(Text(value, "stat-value"));
            view.Add(stat);
        }

        private void Paint(VisualElement target, string name)
        {
            if (!textures.TryGetValue(name, out var texture))
            {
                texture = Resources.Load<Texture2D>(name);
                if (texture != null)
                    texture.filterMode = name.StartsWith("CardArt/", StringComparison.Ordinal) ? FilterMode.Bilinear : FilterMode.Point;
                textures.Add(name, texture);
            }

            target.style.backgroundImage = new StyleBackground(texture);
        }

        private static VisualElement Element(string css)
        {
            var element = new VisualElement { pickingMode = PickingMode.Ignore };
            element.AddToClassList(css);
            return element;
        }

        private static Label Text(string value, params string[] classes)
        {
            var label = new Label(value) { pickingMode = PickingMode.Ignore };
            foreach (string css in classes)
                label.AddToClassList(css);
            return label;
        }

        private static Button ButtonWith(string value, Action click, params string[] classes)
        {
            var button = new Button(click) { text = value, focusable = false };
            foreach (string css in classes)
                button.AddToClassList(css);
            return button;
        }
    }
}
