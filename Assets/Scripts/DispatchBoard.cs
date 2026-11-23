using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Frostline
{
    [RequireComponent(typeof(UIDocument), typeof(AudioSource))]
    public sealed class DispatchBoard : MonoBehaviour
    {
        private enum DeckPage { Roster, Draw, Discard, Codex }

        private readonly Dictionary<string, Texture2D> textures = new();
        private readonly Dictionary<Position, Button> slots = new();
        private readonly List<Button> cards = new();
        private readonly Dictionary<VisualElement, (Action Click, Action Select)> draggable = new();
        private VisualElement screen, battleHud, formations, hand, overlay, dialog, choices;
        private VisualElement background, titleScreen;
        private VisualTreeAsset unitCardFace, itemCardFace;
        private Label titleGuide;
        private Button titleSound, battleSound;
        private readonly Dictionary<string, AudioClip> audioClips = new();
        private AudioSource audioSource;
        private bool showingTitle, showingGuide, soundEnabled;
        private Label locationTitle, turn, wave, waveUnits, objective, notice, hint, drawCount, discardCount;
        private Label resultEyebrow, resultTitle, resultDetail, redrawNote, junkPerTurnNote;
        private Button redraw, recall, restart, orderButton, changeLoadout;
        private Button inspectDeck, inspectDraw, inspectDiscard, routeDeck, rosterTab, drawTab, discardTab;
        private VisualElement deckOverlay, deckGrid, deckDetail;
        private ScrollView deckScroll;
        private Label deckSummary;
        private DeckPage? deckPage;
        private Button selectedDeckTile;
        private Label orderNote;
        private bool showOrder;
        private readonly List<VisualElement> forecastMarks = new();
        private int hoveredCard = -1;
        private Position? hoveredSlot;
        private DispatchRoute route;
        [SerializeField] private CampaignConfig configuration;
        private Expedition game;
        private Coroutine resolving;
        private BattleBeat? beat;
        private int selectedCard = -1;
        private Position? selectedUnit;
        private bool confirmRestart;
        private Action beginDrag, clickDrag;
        private int dragPointer = -1;
        private const float DragThreshold = 5f;
        private const float DropTolerance = 12f;
        private Vector2 dragOrigin, dragOffset;
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
            var document = GetComponent<UIDocument>();
            if (document.visualTreeAsset == null)
                document.visualTreeAsset = Resources.Load<VisualTreeAsset>("UI/Expedition");
            var root = document.rootVisualElement;
            root.style.flexGrow = 1;
            if (screen != null && ReferenceEquals(screen, root.Q("screen")))
            {
                root.Clear();
                document.visualTreeAsset.CloneTree(root);
            }
            screen = root.Q("screen");
            battleHud = screen.Q("battle-hud");
            unitCardFace = Resources.Load<VisualTreeAsset>("UI/UnitCardFace");
            itemCardFace = Resources.Load<VisualTreeAsset>("UI/ItemCardFace");
            background = screen.Q("background");
            BindTitleScreen();

            formations = screen.Q("formations");
            BindFormation();
            hand = screen.Q("hand");
            overlay = screen.Q("overlay");
            dialog = screen.Q("dialog");
            choices = screen.Q("choices");
            locationTitle = screen.Q<Label>("location-title");
            objective = screen.Q<Label>("objective");
            turn = screen.Q<Label>("turn");
            wave = screen.Q<Label>("wave");
            waveUnits = screen.Q<Label>("wave-units");
            notice = screen.Q<Label>("notice");
            hint = screen.Q<Label>("hint");
            drawCount = screen.Q<Label>("draw-count");
            discardCount = screen.Q<Label>("discard-count");
            resultEyebrow = screen.Q<Label>("result-eyebrow");
            resultTitle = screen.Q<Label>("result-title");
            resultDetail = screen.Q<Label>("result-detail");
            changeLoadout = screen.Q<Button>("change-loadout");
            changeLoadout.clicked += ResetRun;
            redraw = screen.Q<Button>("redraw");

            redrawNote = screen.Q<Label>("redraw-note");
            junkPerTurnNote = screen.Q<Label>("junk-per-turn-note");
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
            foreach (Button button in new[] { redraw, recall, restart, orderButton })
                button.focusable = false;
            BindDeckView();

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
            showingTitle = true;
            ResetRun();
        }

        private void BindTitleScreen()
        {
            titleScreen = screen.Q("title-screen");
            titleGuide = screen.Q<Label>("title-guide");
            showingGuide = false;
            titleSound = screen.Q<Button>("title-sound");
            battleSound = screen.Q<Button>("battle-sound");
            if (!TryGetComponent<AudioSource>(out audioSource))
                audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            if (FindFirstObjectByType<AudioListener>() == null)
                gameObject.AddComponent<AudioListener>();
            soundEnabled = PlayerPrefs.GetInt("Frostline.Sound", 1) != 0;
            UpdateSoundButtons();
            titleSound.clicked += ToggleSound;
            battleSound.clicked += ToggleSound;
            screen.Q<Button>("start-expedition").clicked += StartExpedition;
            screen.Q<Button>("title-help").clicked += () =>
            {
                PlayCue("paper");
                showingGuide = !showingGuide;
                titleGuide.style.display = showingGuide ? DisplayStyle.Flex : DisplayStyle.None;
            };
        }

        private void StartExpedition()
        {
            PlayCue("paper");
            showingTitle = false;
            ResetRun();
        }

        private void ToggleSound()
        {
            soundEnabled = !soundEnabled;
            if (!soundEnabled)
                audioSource.Stop();
            PlayerPrefs.SetInt("Frostline.Sound", soundEnabled ? 1 : 0);
            UpdateSoundButtons();
            PlayCue("paper");
        }

        private void UpdateSoundButtons() =>
            titleSound.text = battleSound.text = soundEnabled ? "音效 开" : "音效 关";

        private void PlayCue(string cue)
        {
            if (!soundEnabled)
                return;
            if (!audioClips.TryGetValue(cue, out AudioClip clip))
            {
                clip = Resources.Load<AudioClip>($"Audio/{cue}");
                audioClips.Add(cue, clip);
            }
            if (clip != null)
                audioSource.PlayOneShot(clip, 0.35f);
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
            if (deckPage == DeckPage.Codex)
            {
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.C))
                    CloseDeck();
                return;
            }
            if (Input.GetKeyDown(KeyCode.C) && CanOpenCodex)
            {
                ShowDeck(DeckPage.Codex);
                return;
            }
            if (showingTitle)
            {
                if (Input.GetKeyDown(KeyCode.Return))
                    StartExpedition();
                return;
            }
            if (ghost != null)
            {
                float blend = 1f - Mathf.Exp(-46f * Time.unscaledDeltaTime);
                ghostPosition = Vector2.Lerp(ghostPosition, ghostDestination, blend);
                float desiredTilt = Mathf.Clamp((ghostDestination.x - ghostPosition.x) * 0.09f, -7f, 7f);
                ghostTilt = Mathf.Lerp(ghostTilt, desiredTilt, blend);
                Place(ghost, ghostPosition);
                ghost.style.rotate = new Rotate(Angle.Degrees(ghostTilt));
            }

            if (deckPage.HasValue)
            {
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.B))
                    CloseDeck();
                return;
            }
            if (Input.GetKeyDown(KeyCode.B) && CanReviewDeck)
            {
                ShowDeck(DeckPage.Roster);
                return;
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
            if (Input.GetKeyDown(KeyCode.Space) && !confirmRestart && beginDrag == null)
                Redraw();
        }

        private void ResetRun()
        {
            if (configuration == null)
                configuration = Resources.Load<CampaignConfig>(CampaignConfig.ResourcePath);
            if (configuration == null)
                throw new InvalidOperationException($"Missing campaign configuration at Resources/{CampaignConfig.ResourcePath}.");
            route = configuration.CreateRoute(textures);
            LoadBattle();
        }

        private void RestartBattle()
        {
            if (route.RestartBattle())
                LoadBattle();
        }

        private void LoadBattle()
        {
            if (resolving != null)
                StopCoroutine(resolving);
            resolving = null;
            beat = null;
            confirmRestart = false;
            CloseDeck();
            ClearDrag();
            StopReturn();
            ClearAttackView();
            showOrder = false;
            game = route.Battle;
            selectedCard = -1;
            selectedUnit = null;
            Render();
        }

        private void Redraw()
        {
            if (!game.CanRedraw || resolving != null || confirmRestart || deckPage.HasValue)
                return;
            PlayCue("bell");
            BeginSequence(game.Redraw());
        }


        private void Recall()
        {
            if (!deckPage.HasValue && selectedUnit.HasValue && game.Recall(selectedUnit.Value))
            {
                selectedUnit = null;
                Render();
            }
        }

        private void BeginSequence(IEnumerable<BattleBeat> sequence)
        {
            if (game.Phase != BattlePhase.Planning || resolving != null || confirmRestart || deckPage.HasValue)
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
                yield return Tween(0.10f, t =>
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
            if (actor.Type.Pattern == AttackPattern.Support)
            {
                yield return new WaitForSecondsRealtime(0.09f);
                yield break;
            }

            var motion = FloatingCard(sourceRect);
            DrawUnitFace(motion, actor);
            source.AddToClassList("motion-source");
            Vector2 direction = (destination - start).normalized;
            Vector2 recoil = start - direction * 13f;
            Vector2 end = Vector2.Lerp(start, destination, 0.72f);
            float tilt = direction.x * 12f;
            yield return Tween(0.065f, t =>
            {
                Place(motion, Vector2.Lerp(start, recoil, t));
                motion.style.rotate = new Rotate(Angle.Degrees(-tilt * t));
            });
            yield return Tween(0.11f, t =>
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
            PlayCue(action.Effect switch
            {
                BattleEffect.Attack or BattleEffect.Retaliation or BattleEffect.Burst => "impact",
                BattleEffect.Arrival => "paper",
                _ => "effect"
            });
            if (action.Effect != BattleEffect.Attack)
            {
                yield return new WaitForSecondsRealtime(action.Effect == BattleEffect.Arrival ? 0.18f : 0.17f);
                yield break;
            }

            yield return null;
            if (attackView != null)
                slots[action.Source].AddToClassList("motion-source");

            var targetViews = new List<Button>(action.Targets.Count);
            foreach (Position pos in action.Targets)
                targetViews.Add(slots[pos]);

            yield return Tween(0.21f, t =>
            {
                if (attackView != null)
                {
                    float returnProgress = 1f - Mathf.Pow(1f - t, 2f);
                    Place(attackView, Vector2.Lerp(attackContact, attackHome, returnProgress));
                    attackView.style.rotate = new Rotate(Angle.Degrees(attackTilt * (1f - returnProgress)));
                }

                float kick = Mathf.Sin(t * Mathf.PI * 5f) * (1f - t) * 10f;
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
            if (game.Phase != BattlePhase.Planning || deckPage.HasValue)
                return;
            selectedCard = toggle && selectedCard == index ? -1 : index;
            selectedUnit = null;
            ApplyTargets();
        }

        private void SelectUnit(Position pos, bool toggle)
        {
            if (deckPage.HasValue)
                return;
            selectedCard = -1;
            selectedUnit = toggle && selectedUnit.HasValue && selectedUnit.Value.Equals(pos) ? null : pos;
            ApplyTargets();
        }

        private void UseSlot(Position pos)
        {
            if (game.Phase != BattlePhase.Planning || deckPage.HasValue)
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
            Paint(background, game?.Location.Landscape ?? "Art/cover");
            titleScreen.style.display = showingTitle ? DisplayStyle.Flex : DisplayStyle.None;
            battleHud.style.display = game == null ? DisplayStyle.None : DisplayStyle.Flex;
            if (showingTitle)
            {
                overlay.style.display = DisplayStyle.None;
                return;
            }
            if (game == null)
            {
                draggable.Clear();
                RenderOverlay();
                return;
            }
            bool planning = game.Phase == BattlePhase.Planning;
            locationTitle.text = game.Location.Name;
            objective.text = $"第 {route.Stage}/{route.StageCount} 战 · 卡组 {route.DeckCount} 张 · 保护{game.Captain.Name}";
            turn.text = game.Opening ? "开局部署" : $"第 {game.Turn} 回合";
            wave.text = game.Wave == game.Location.WaveCount
                ? "最终波"
                : game.Reinforcements == 0 ? $"第 {game.Wave} 波  ·  增援等待空位"
                : $"第 {game.Wave} 波  ·  增援 {game.Reinforcements} 拍";
            waveUnits.style.display = game.NextWave.Count == 0 ? DisplayStyle.None : DisplayStyle.Flex;
            waveUnits.text = string.Join("  +  ", game.NextWave.Select(enemy => enemy.Unit.Name));
            notice.text = game.Notice;
            drawCount.text = game.DrawCount.ToString();
            discardCount.text = game.DiscardCount.ToString();
            inspectDeck.SetEnabled(planning);
            inspectDraw.SetEnabled(planning);
            inspectDiscard.SetEnabled(planning);
            redraw.text = game.Refresh == 0 ? "免费换牌" : $"换牌  {game.Refresh}";
            redraw.SetEnabled(game.CanRedraw);
            redrawNote.text = game.Opening ? "开局部署后开启" : game.Refresh == 0 ? "空格敲铃" : "提前敲铃消耗一回合";
            junkPerTurnNote.style.display = game.JunkPerTurn > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            junkPerTurnNote.text = $"每回合垃圾 +{game.JunkPerTurn}";
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
            if (game.Phase != BattlePhase.Planning || game.Opening || deckPage.HasValue)
                return;

            showOrder = !showOrder;
            ApplyOrder();
        }

        private void ApplyOrder()
        {
            bool canPreview = game.Phase == BattlePhase.Planning && !game.Opening && !deckPage.HasValue;
            bool previewCard = canPreview && hoveredSlot.HasValue && game.CanPlay(selectedCard, hoveredSlot.Value);
            bool visible = canPreview && (showOrder || previewCard);
            orderButton.SetEnabled(canPreview);
            orderButton.EnableInClassList("order-active", showOrder && canPreview);
            orderButton.text = showOrder && canPreview ? "隐藏预览 [Tab]" : "行动预览 [Tab]";
            orderNote.style.visibility = visible ? Visibility.Visible : Visibility.Hidden;
            foreach (VisualElement mark in forecastMarks)
                mark.RemoveFromHierarchy();
            forecastMarks.Clear();

            if (!visible)
                return;

            var preview = game.PreviewTurn(selectedCard, previewCard ? hoveredSlot : null);
            orderNote.text = (previewCard ? $"{game.Hand[selectedCard].Name} · 出牌后预览" : "下一拍预览") +
                (preview.Phase == BattlePhase.Lost ? " · 队长将倒下" :
                    preview.Phase == BattlePhase.Won ? " · 预计获胜" :
                    preview.Arrivals.Count > 0 ? " · 本回合增援" : "");
            orderNote.EnableInClassList("danger", preview.Phase == BattlePhase.Lost);
            for (int i = 0; i < preview.Actions.Count; i++)
                AddForecastMark(slots[preview.Actions[i]], Text((i + 1).ToString(), "order-badge"));
            foreach (var arrival in preview.Arrivals)
                AddForecastMark(slots[arrival.Position], Text($"增援 · {arrival.Unit.Name}", "arrival-badge"));
            foreach (UnitForecast result in preview.Units)
                ShowUnitForecast(result);
        }

        private void ShowUnitForecast(UnitForecast result)
        {
            Unit unit = game.At(result.Origin);
            if (unit == null)
                return;

            string label;
            string style;
            if (result.Defeated)
                (label, style) = (unit.Leader ? "队长将倒下" : "预计击倒", "forecast-danger");
            else if (result.Health != unit.Health)
                (label, style) = ($"生命 {unit.Health} → {result.Health}", result.Health < unit.Health ? "forecast-danger" : "forecast-gain");
            else if (result.Block != unit.Block)
                (label, style) = ($"格挡 {unit.Block} → {result.Block}", "forecast-effect");
            else if (result.Shell != unit.Shell)
                (label, style) = ($"护壳 {unit.Shell} → {result.Shell}", "forecast-effect");
            else if (result.Snow != unit.Frozen)
                (label, style) = ($"雪球 {unit.Frozen} → {result.Snow}", "forecast-effect");
            else if (result.Attack != unit.Attack)
                (label, style) = ($"攻击 {unit.Attack} → {result.Attack}", "forecast-effect");
            else if (!result.Origin.Equals(result.Destination.Value))
                (label, style) = ("预计换排", "forecast-effect");
            else
                return;

            var slot = slots[result.Origin];
            var surface = slot.Q(className: "face-art") ?? slot;
            AddForecastMark(surface, Text(label, "forecast-chip", style));
        }

        private void AddForecastMark(VisualElement parent, VisualElement mark)
        {
            parent.Add(mark);
            forecastMarks.Add(mark);
        }

        private void BindFormation()
        {
            slots.Clear();
            BindRow(formations.Q("upper-row"), 0, "上排");
            BindRow(formations.Q("lower-row"), 1, "下排");
        }

        private void BindRow(VisualElement rowView, int row, string label)
        {
            rowView.Q<Label>("row-label").text = label;
            for (int position = Expedition.Slots - 1; position >= 0; position--)
                BindSlot(rowView.Q<Button>($"ally-{position}"), new Position(false, row, position));
            for (int position = 0; position < Expedition.Slots; position++)
                BindSlot(rowView.Q<Button>($"enemy-{position}"), new Position(true, row, position));
        }

        private void BindSlot(Button slot, Position pos)
        {
            slots.Add(pos, slot);
            slot.clicked += () => UseSlot(pos);
            slot.RegisterCallback<PointerEnterEvent>(_ =>
            {
                if (game?.Phase == BattlePhase.Planning)
                    HoverSlot(pos);
            });
            slot.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                if (game?.Phase == BattlePhase.Planning && ghost == null && hoveredSlot.HasValue &&
                    hoveredSlot.Value.Equals(pos))
                    HoverSlot(null);
            });
        }

        private void RenderFormation(bool planning)
        {
            foreach (var entry in slots)
                RenderSlot(entry.Key, entry.Value, planning);
        }

        private void RenderSlot(Position pos, Button slot, bool planning)
        {
            Unit unit = game.At(pos);
            slot.EnableInClassList("empty", unit == null);
            slot.EnableInClassList("unit-face", unit != null);
            slot.EnableInClassList("leader", unit?.Leader == true);
            slot.EnableInClassList("frozen", unit?.Frozen > 0);
            slot.RemoveFromClassList("source");
            slot.RemoveFromClassList("incoming");
            slot.RemoveFromClassList("motion-source");
            slot.style.translate = StyleKeyword.Null;
            slot.Q(className: "burst")?.RemoveFromHierarchy();
            var emptyMark = slot.Q(className: "empty-mark");
            if (emptyMark != null)
                emptyMark.style.display = unit == null ? DisplayStyle.Flex : DisplayStyle.None;

            if (unit != null)
            {
                DrawUnitFace(slot, unit);
                if (planning && !pos.Enemy)
                    draggable.Add(slot, (() => UseSlot(pos), () => SelectUnit(pos, toggle: false)));
            }
            else
            {
                slot.Q(className: "unit-content")?.RemoveFromHierarchy();
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
                    if (resolved.Effect is BattleEffect.Retaliation or BattleEffect.Burning)
                        mark.AddToClassList(resolved.Effect == BattleEffect.Burning ? "burn-burst" : "thorns-burst");
                    if (resolved.Effect is BattleEffect.Recovery or BattleEffect.Arrival or BattleEffect.Guard)
                        mark.AddToClassList("heal-burst");
                    slot.Add(mark);
                    break;
                }
            }
        }

        private void RenderHand(bool planning)
        {
            cards.Clear();
            hand.Clear();
            hand.EnableInClassList("many-cards", game.Hand.Count > Expedition.HandSize);
            for (int i = 0; i < game.Hand.Count; i++)
            {
                int index = i;
                Card card = game.Hand[i];
                Button view = ButtonWith(null, () => UseHandCard(index), "card");
                DrawHandFace(view, card);
                view.SetEnabled(planning);
                if (planning)
                {
                    draggable.Add(view, (() => UseHandCard(index), () => SelectCard(index, toggle: false)));
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

        private void UseHandCard(int index)
        {
            if (game.CanPlayOnHand(selectedCard, index))
                BeginSequence(game.PlayOnHand(selectedCard, index));
            else
                SelectCard(index, toggle: true);
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
            if (game == null)
                return;
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
                randomTarget = current.Pattern == AttackPattern.Aimless;
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
                bool focused = hoveredSlot.HasValue && hoveredSlot.Value.Equals(pos) &&
                    (selectedCard >= 0 || selectedUnit.HasValue);
                entry.Value.EnableInClassList("drop-valid", focused && valid);
                entry.Value.EnableInClassList("drop-invalid", focused && !valid);
            }

            for (int i = 0; i < cards.Count; i++)
            {
                cards[i].EnableInClassList("selected", i == selectedCard);
                cards[i].EnableInClassList("hand-target", game.CanPlayOnHand(selectedCard, i));
            }

            bool canRecall = selectedUnit.HasValue && game.CanRecall(selectedUnit.Value);
            recall.SetEnabled(canRecall);
            recall.EnableInClassList("target", canRecall);
            bool handTarget = selectedCard >= 0 && game.CanPlayOnHand(selectedCard, hoveredCard);
            if (selectedCard >= 0 && Enumerable.Range(0, cards.Count).Any(i => game.CanPlayOnHand(selectedCard, i)))
                any = true;
            if (handTarget)
                cardIndex = selectedCard;
            hint.text = game.Phase == BattlePhase.Resolving ? "结算中…" :
                cardIndex >= 0 ? any ? CardDetails(game.Hand[cardIndex],
                    hoveredSlot.HasValue && game.CanPlay(cardIndex, hoveredSlot.Value) ? current : null) : "当前没有合法目标。" :
                selectedUnit.HasValue ? "选择己方位置换位，或拖到收回队员。换位和收回均免费。" :
                current != null ? UnitDetails(current) :
                game.Opening ? "将戴冠的牌部署到己方空位，部署不消耗回合。" : "悬停预览目标 · 空格敲换牌铃";
            ApplyOrder();
        }

        private static string CardDetails(Card card, Unit target = null)
        {
            if (card.Unit != null)
                return card.Description + UnitDetails(DeploymentPreview(card, leader: false));
            string preview = "";
            if (target != null && card.Damage > 0 && (card.BonusDamage > 0 || target.Exposed > 0))
            {
                int damage = target.Block > 0 ? 0 : card.DamageAgainst(target) + target.Exposed;
                preview = $" 本次伤害 {damage}。";
            }
            return card.Description + Effects.Rule(card.Effect) + preview;
        }

        private static string UnitDetails(Unit unit)
        {
            UnitType type = unit.Type;
            string pattern = Expedition.PatternName(unit.Pattern);
            if (unit.Pattern == AttackPattern.Aimless)
                pattern += "（从高亮目标随机选一名）";
            if (type.Pattern == AttackPattern.Support)
                pattern += "（倒计时归零，给予同排最前方的其他友军）";
            string frenzy = unit.Frenzy > 1 ? $" · 狂热 ×{unit.Frenzy}" : "";
            string ability = type.Ability.Effect == EffectKind.None ? "" : " · " + type.Ability.Describe(unit.AbilityBonus);
            string resistance = type.ResistSnow ? " · 最多承受 1 层雪球" : "";
            string ink = unit.Ink > 0 ? $" · 墨水 {unit.Ink}（触发能力停用）" : "";
            string overload = unit.Overload > 0 ? $" · 过载 {unit.Overload}/{unit.Health}，达到生命时引爆" : "";
            string buffs = (unit.Spice > 0 ? $" · 辣椒 {unit.Spice}" : "") +
                (unit.Weakness > 0 ? $" · 虚弱 {unit.Weakness}" : "") +
                (unit.Block > 0 ? $" · 格挡 {unit.Block}" : "") +
                (unit.Frozen > 0 ? $" · 雪球 {unit.Frozen}" : "") +
                (unit.Shell > 0 ? $" · 护壳 {unit.Shell}" : "") +
                (unit.Thorns > 0 ? $" · 反伤 {unit.Thorns}" : "") +
                (unit.Burn > 0 ? $" · 灼烧 {unit.Burn}" : "") +
                (unit.Exposed > 0 ? $" · 易伤 {unit.Exposed}" : "") +
                (unit.EffectBonus > 0 ? $" · 效果 +{unit.EffectBonus}" : "");
            string pendant = unit.Pendant != Pendant.None
                ? $" · {Pendants.Name(unit.Pendant)} {Pendants.Description(unit.Pendant)}" : "";
            string growth = unit.BaseAttack > type.Power ? $" · 基础攻击 {unit.BaseAttack}" : "";
            return pattern + frenzy + ability + resistance + ink + overload + buffs + growth + pendant;
        }

        private void RenderOverlay()
        {
            bool selecting = game == null;
            bool selectingFaction = selecting && route.Factions.Count > 0 && route.Faction == null;
            bool selectingLoadout = selecting && route.Loadout == null;
            bool selectingCompanion = selecting && route.Loadout != null && route.Companion == null;
            bool selectingSupply = selecting && route.Companion != null && route.NeedsSupply;
            bool finished = game?.Phase is BattlePhase.Won or BattlePhase.Lost;
            bool reward = route.CanClaimReward && !confirmRestart;
            bool equipping = route.PendingPendant != Pendant.None;
            bool shopping = selecting || reward || equipping;
            overlay.style.display = selecting || finished || confirmRestart ? DisplayStyle.Flex : DisplayStyle.None;
            dialog.EnableInClassList("wide", shopping);
            changeLoadout.text = route.Factions.Count > 0 ? "返回部族选择" : "返回主角选择";
            changeLoadout.style.display = selecting && route.Stage == 1 && (!selectingLoadout || route.Faction != null)
                ? DisplayStyle.Flex : DisplayStyle.None;
            routeDeck.style.display = (selecting || finished) && route.Companion != null && !confirmRestart
                ? DisplayStyle.Flex : DisplayStyle.None;
            choices.Clear();
            choices.EnableInClassList("choice-grid", shopping);
            resultEyebrow.text = selectingLoadout || selectingCompanion || selectingSupply ? "霜线信使"
                : shopping ? $"霜线信使   /   第 {route.Stage} 段" : game.Location.Name;
            if (selectingFaction)
            {
                resultTitle.text = "选择部族";
                resultDetail.text = "部族决定起始牌组和途中可以招募的随从。";
                for (int i = 0; i < route.Factions.Count; i++)
                {
                    int index = i;
                    ExpeditionFaction faction = route.Factions[i];
                    var option = AddChoice(faction.Name, $"起始牌 {faction.StartingDeck.Count} 张", faction.Strategy, () =>
                    {
                        if (route.ChooseFaction(index))
                            RenderOverlay();
                    });
                    option.AddToClassList("loadout-choice");
                    AddPortrait(option, faction.Leaders[0].Art).SendToBack();
                    option.Add(Text(DeckList(faction.StartingDeck), "deck-preview"));
                }
            }
            else if (selectingLoadout)
            {
                resultTitle.text = route.Faction == null ? "选择主角和牌组" : "选择领袖";
                resultDetail.text = route.Faction == null ? "主角倒下时，战斗结束。"
                    : $"从三名候选中选择一名来带领{route.Faction.Name}。";
                for (int i = 0; i < route.Loadouts.Count; i++)
                {
                    int index = i;
                    var loadout = route.Loadouts[i];
                    var option = AddUnitChoice(loadout.Captain, loadout.DeckName, () =>
                    {
                        if (route.ChooseLoadout(index))
                            RenderOverlay();
                    });
                    option.AddToClassList("loadout-choice");
                    if (route.Faction == null)
                    {
                        option.Add(Text(loadout.Strategy, "loadout-strategy"));
                        option.Add(Text($"{loadout.Deck.Count} 张牌 · {DeckList(loadout.Deck)}", "deck-preview"));
                    }
                }
            }
            else if (selectingCompanion)
            {
                resultTitle.text = "选择开局伙伴";
                resultDetail.text = $"开局伙伴与{route.Loadout.Captain.Name}同行，可在每战开局免费部署。";
                for (int i = 0; i < route.OpeningCompanions.Count; i++)
                {
                    int index = i;
                    AddUnitChoice(route.OpeningCompanions[i], null, () =>
                    {
                        if (route.ChooseCompanion(index))
                            RenderOverlay();
                    });
                }
            }
            else if (selectingSupply)
            {
                resultTitle.text = "选择出发补给";
                resultDetail.text = $"加入一张起始牌。{route.Loadout.Strategy}";
                var supplies = route.Loadout.Supplies;
                for (int i = 0; i < supplies.Count; i++)
                {
                    int index = i;
                    AddSupplyChoice(supplies[i], () =>
                    {
                        if (route.ChooseSupply(index))
                            RenderOverlay();
                    });
                }
            }
            else if (selecting)
            {
                resultTitle.text = route.StageTitle;
                string party = $"{route.Loadout.Captain.Name}与{route.Companion.Name}携带 {route.DeckCount} 张卡牌。";
                resultDetail.text = route.Stage < route.StageCount
                    ? party + (route.RecruitCompanions ? "胜利后招募一名随从并获得挂坠。" : "胜利后获得一张新牌和一枚挂坠。")
                    : party + "选择最后一战。";
                for (int i = 0; i < route.EncounterChoices.Count; i++)
                {
                    int index = i;
                    Encounter site = route.EncounterChoices[i];
                    string summary = $"{site.WaveCount} 波 · 每 {site.WaveInterval} 拍增援";
                    if (site.RewardPendant != Pendant.None)
                        summary += $" · {Pendants.Name(site.RewardPendant)}";
                    var option = AddChoice(site.Name, summary, site.Briefing, () =>
                    {
                        if (route.ChooseEncounter(index))
                            LoadBattle();
                    });
                    var preview = Element("site-preview");
                    Paint(preview, site.Landscape);
                    option.Insert(0, preview);
                }
            }
            else if (confirmRestart)
            {
                resultTitle.text = "重开本战？";
                resultDetail.text = "保留途中获得的卡组奖励，从本战开局重新部署。";
                choices.Add(ButtonWith("继续战斗", () =>
                {
                    confirmRestart = false;
                    RenderOverlay();
                }, "button"));
                choices.Add(ButtonWith("确认重开", RestartBattle, "button", "primary"));
            }
            else if (reward)
            {
                resultTitle.text = route.RecruitCompanions ? "招募随从" : "选择一张新牌";
                resultDetail.text = route.RecruitCompanions
                    ? $"已攻克{game.Location.Name}。选一名随从加入队伍，下一战开局抽到。"
                    : $"已攻克{game.Location.Name}。选中的牌会在下一战开局抽到。";
                for (int i = 0; i < route.Rewards.Count; i++)
                {
                    int index = i;
                    Card card = route.Rewards[i];
                    string source = route.RecruitCompanions
                        ? route.Faction.ExclusiveRecruits.Contains(card) ? route.Faction.Name : "通用随从"
                        : null;
                    AddSupplyChoice(card, () =>
                    {
                        if (!route.ClaimReward(index))
                            return;
                        if (route.Battle == null)
                            LoadBattle();
                        else
                            RenderOverlay();
                    }, source);
                }
            }
            else if (equipping)
            {
                Pendant pendant = route.PendingPendant;
                resultTitle.text = Pendants.Name(pendant);
                resultDetail.text = $"{Pendants.Description(pendant)}。选择一名单位装备，挂坠会随卡牌进入下一战。";
                for (int i = 0; i < route.PendantTargetCount; i++)
                {
                    if (!route.CanEquipPendant(i))
                        continue;
                    int index = i;
                    Card card = route.GetPendantTarget(i);
                    var option = AddChoice(card.Name, Pendants.Description(pendant),
                        UnitDetails(DeploymentPreview(card.Equip(pendant), false)), () =>
                        {
                            if (route.EquipPendant(index))
                                LoadBattle();
                        });
                    AddPortrait(option, card.Art).SendToBack();
                }
            }
            else if (finished)
            {
                bool won = game.Phase == BattlePhase.Won;
                resultTitle.text = won ? "路线打通" : $"{game.Captain.Name}倒下";
                resultDetail.text = won ? $"{route.StageCount} 场战斗全部获胜。最终卡组 {route.DeckCount} 张。"
                    : $"探险队止步{game.Location.Name}。";
                if (!won)
                    choices.Add(ButtonWith("重试本战", RestartBattle, "button"));
                choices.Add(ButtonWith("重新出发", ResetRun, "button", "primary"));
                choices.Add(ButtonWith("返回标题", () =>
                {
                    showingTitle = true;
                    ResetRun();
                }, "button"));
            }
        }

        private Button AddUnitChoice(UnitType unit, string deckName, Action choose)
        {
            string stats = $"{unit.Health} 生命 · {unit.Power} 攻击 · {unit.Period} 拍";
            var option = AddChoice(unit.Name, deckName == null ? stats : $"{deckName} · {stats}",
                UnitDetails(new Unit(unit)), choose);
            AddPortrait(option, unit.Art).SendToBack();
            return option;
        }

        private static string DeckList(IReadOnlyList<Card> deck) => string.Join("、", deck.GroupBy(card => card.Name)
            .Select(group => group.Count() == 1 ? group.Key : $"{group.Key} ×{group.Count()}"));

        private void AddSupplyChoice(Card card, Action choose, string source = null)
        {
            var option = AddChoice(card.Name, source ?? (card.Unit == null ? "道具" : "队员"), CardDetails(card), choose);
            option.AddToClassList("supply-choice");
            AddCardArt(option, card).SendToBack();
        }

        private Button AddChoice(string title, string subtitle, string detail, Action choose)
        {
            var button = ButtonWith(null, () =>
            {
                PlayCue("paper");
                choose();
            }, "button", "selection-card");
            button.Add(Text(title, "selection-title"));
            button.Add(Text(subtitle, "selection-subtitle"));
            button.Add(Text(detail, "selection-detail"));
            choices.Add(button);
            return button;
        }

        private bool CanReviewDeck => route?.Companion != null && game?.Phase != BattlePhase.Resolving
            && resolving == null && !confirmRestart;

        private void BindDeckView()
        {
            deckOverlay = screen.Q("deck-overlay");
            deckGrid = screen.Q("deck-grid");
            deckDetail = screen.Q("deck-detail");
            deckScroll = screen.Q<ScrollView>("deck-scroll");
            deckSummary = screen.Q<Label>("deck-summary");
            inspectDeck = screen.Q<Button>("inspect-deck");
            inspectDraw = screen.Q<Button>("inspect-draw");
            inspectDiscard = screen.Q<Button>("inspect-discard");
            routeDeck = screen.Q<Button>("route-deck");
            rosterTab = screen.Q<Button>("deck-roster-tab");
            drawTab = screen.Q<Button>("deck-draw-tab");
            discardTab = screen.Q<Button>("deck-discard-tab");

            inspectDeck.clicked += () => ShowDeck(DeckPage.Roster);
            inspectDraw.clicked += () => ShowDeck(DeckPage.Draw);
            inspectDiscard.clicked += () => ShowDeck(DeckPage.Discard);
            routeDeck.clicked += () => ShowDeck(DeckPage.Roster);
            rosterTab.clicked += () => ShowDeck(DeckPage.Roster);
            drawTab.clicked += () => ShowDeck(DeckPage.Draw);
            discardTab.clicked += () => ShowDeck(DeckPage.Discard);
            foreach (string button in new[] { "title-codex", "battle-codex", "route-codex" })
                screen.Q<Button>(button).clicked += () => ShowDeck(DeckPage.Codex);
            screen.Q<Button>("close-deck").clicked += CloseDeck;
        }

        private void ShowDeck(DeckPage page)
        {
            if (page == DeckPage.Codex ? !CanOpenCodex : !CanReviewDeck || (page != DeckPage.Roster && game == null))
                return;

            ClearDrag();
            StopReturn();
            deckPage = page;
            deckOverlay.style.display = DisplayStyle.Flex;
            deckGrid.Clear();
            deckDetail.Clear();
            selectedDeckTile = null;
            deckScroll.scrollOffset = Vector2.zero;

            bool codex = page == DeckPage.Codex;
            screen.Q<Label>("deck-title").text = codex ? "卡牌图鉴" : "牌组一览";
            rosterTab.parent.style.display = codex ? DisplayStyle.None : DisplayStyle.Flex;
            screen.Q("codex-tabs").style.display = codex ? DisplayStyle.Flex : DisplayStyle.None;
            if (codex)
            {
                ShowCodexGroup(null);
                return;
            }

            bool roster = page == DeckPage.Roster;
            rosterTab.text = $"队伍与牌组  {route.DeckCount}";
            rosterTab.EnableInClassList("current", roster);
            drawTab.style.display = game == null ? DisplayStyle.None : DisplayStyle.Flex;
            discardTab.style.display = game == null ? DisplayStyle.None : DisplayStyle.Flex;
            if (game != null)
            {
                drawTab.text = $"抽牌堆  {game.DrawCount}";
                discardTab.text = $"弃牌堆  {game.DiscardCount}";
                drawTab.EnableInClassList("current", page == DeckPage.Draw);
                discardTab.EnableInClassList("current", page == DeckPage.Discard);
            }

            var source = page switch
            {
                DeckPage.Draw => game.DrawPile,
                DeckPage.Discard => game.DiscardPile,
                _ => route.Deck
            };
            deckSummary.text = page switch
            {
                DeckPage.Draw => "查看剩余卡牌。列表按卡名排列。",
                DeckPage.Discard => "抽牌堆见底时，这些牌将洗回抽牌堆。",
                _ => $"{route.Loadout.DeckName} · 2 张开局部署牌 · {source.Count} 张卡组牌"
            };

            if (roster)
            {
                AddDeckTile(route.CaptainCard, 1, "主角");
                AddDeckTile(route.CompanionCard, 1, "随从");
            }

            // Group only cards with identical combat values, including recalled units' health and pendants.
            var groups = source.GroupBy(card =>
                (card.Name, card.Unit, card.Action, card.Amount, card.Damage, card.Effect, card.TargetsEnemy,
                    card.DeploymentHealth, card.DeploymentAttack, card.DeploymentEffectBonus, card.Pendant, card.BonusAgainst, card.BonusDamage, card.Technique));
            foreach (var group in groups.OrderBy(group => group.Key.Name, StringComparer.Ordinal))
                AddDeckTile(group.First(), group.Count(), null);

            if (selectedDeckTile == null)
                deckDetail.Add(Text("这个牌堆是空的。", "deck-empty"));
        }

        private bool CanOpenCodex => route?.Catalog.Count > 0 && resolving == null && game?.Phase != BattlePhase.Resolving;

        private void ShowCodexGroup(string group)
        {
            var tabs = screen.Q("codex-tabs");
            tabs.Clear();
            var groups = route.Catalog.Select(entry => entry.Group).Distinct().Prepend(null);
            foreach (string option in groups)
            {
                var tab = ButtonWith(option ?? "全部", () => ShowCodexGroup(option), "button", "deck-tab");
                tab.EnableInClassList("current", option == group);
                tabs.Add(tab);
            }

            deckDetail.Clear();
            deckGrid.Clear();
            selectedDeckTile = null;
            deckScroll.scrollOffset = Vector2.zero;
            var entries = route.Catalog.Where(entry => group == null || entry.Group == group).ToArray();
            deckSummary.text = $"共 {entries.Length} 张牌。选中卡牌查看数值和效果。";
            foreach (var entry in entries)
                AddDeckTile(entry.Card, 1, null);
        }

        private void CloseDeck()
        {
            deckPage = null;
            deckOverlay.style.display = DisplayStyle.None;
        }

        private void AddDeckTile(Card card, int count, string role)
        {
            var tile = ButtonWith(null, null, "button", "deck-tile");
            tile.clicked += () => ShowDeckCard(tile, card, role);
            AddCardArt(tile, card);
            tile.Add(Text(card.Name, "deck-tile-title"));
            if (role != null || count > 1 || card.Pendant != Pendant.None)
            {
                var badge = Element("deck-tile-badge");
                if (card.Pendant != Pendant.None)
                    badge.Add(Icon("pendant", "deck-pendant"));
                if (role != null || count > 1)
                    badge.Add(Text(role ?? $"×{count}", "deck-tile-count"));
                tile.Add(badge);
            }
            deckGrid.Add(tile);
            if (selectedDeckTile == null)
                ShowDeckCard(tile, card, role);
        }

        private void ShowDeckCard(Button tile, Card card, string role)
        {
            selectedDeckTile?.RemoveFromClassList("current");
            selectedDeckTile = tile;
            selectedDeckTile.AddToClassList("current");
            deckDetail.Clear();
            deckDetail.Add(Text(role ?? (card.Unit != null ? "队员" : "道具"), "deck-card-role"));
            var face = Element("deck-card-face");
            DrawCardFace(face, card, role == "主角", role != null);
            deckDetail.Add(face);
            deckDetail.Add(Text(card.Unit == null ? CardDetails(card) : UnitDetails(DeploymentPreview(card, false)), "deck-card-description"));
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || game?.Phase != BattlePhase.Planning || confirmRestart || deckPage.HasValue)
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
                if (Vector2.Distance(dragOrigin, evt.position) < DragThreshold)
                    return;
                BeginHeldDrag(evt.position);
            }

            ghostDestination = screen.WorldToLocal(evt.position) - dragOffset;
            Position? previousSlot = hoveredSlot;
            hoveredSlot = FindDropSlot(evt.position, out var pos) ? pos : null;
            bool valid = hoveredSlot.HasValue && CanDropOn(hoveredSlot.Value);
            valid |= FindHandDrop(evt.position) >= 0;
            valid |= selectedUnit.HasValue && recall.worldBound.Contains(evt.position) && game.CanRecall(selectedUnit.Value);
            ghost.EnableInClassList("drop-ready", valid);
            ghost.EnableInClassList("drop-blocked", hoveredSlot.HasValue && !valid);
            if (!Nullable.Equals(previousSlot, hoveredSlot))
                ApplyTargets();
        }

        private void BeginHeldDrag(Vector2 pointer)
        {
            Rect original = dragSource.worldBound;
            dragOffset = screen.WorldToLocal(dragOrigin) - screen.WorldToLocal(original.position);
            hoveredCard = -1;
            beginDrag();
            StopReturn();
            ghost = FloatingCard(original);
            if (selectedCard >= 0)
                DrawHandFace(ghost, game.Hand[selectedCard]);
            else if (selectedUnit.HasValue && game.At(selectedUnit.Value) is Unit unit)
                DrawUnitFace(ghost, unit);
            dragSource.AddToClassList("motion-source");
            ghostPosition = ghostDestination = screen.WorldToLocal(pointer) - dragOffset;
            ghostTilt = 0f;
            Place(ghost, ghostPosition);
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (evt.pointerId != dragPointer)
                return;

            evt.StopImmediatePropagation();
            if (ghost == null)
            {
                if (Vector2.Distance(dragOrigin, evt.position) < DragThreshold)
                {
                    Action click = clickDrag;
                    ClearDrag();
                    click?.Invoke();
                    return;
                }
                BeginHeldDrag(evt.position);
            }

            if (selectedUnit.HasValue && recall.worldBound.Contains(evt.position) && game.CanRecall(selectedUnit.Value))
            {
                ClearDrag();
                Recall();
                return;
            }

            if (FindDropSlot(evt.position, out var pos) && CanDropOn(pos))
            {
                ClearDrag();
                UseSlot(pos);
                return;
            }

            int handTarget = FindHandDrop(evt.position);
            if (handTarget >= 0)
            {
                ClearDrag();
                UseHandCard(handTarget);
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

        private bool CanDropOn(Position pos) => selectedCard >= 0 ? game.CanPlay(selectedCard, pos)
            : selectedUnit.HasValue && game.CanMove(selectedUnit.Value, pos);

        private int FindHandDrop(Vector2 pointer)
        {
            for (int i = 0; i < cards.Count; i++)
                if (game.CanPlayOnHand(selectedCard, i) && cards[i].worldBound.Contains(pointer))
                    return i;
            return -1;
        }

        private bool FindDropSlot(Vector2 panelPosition, out Position pos)
        {
            foreach (var slot in slots)
            {
                if (slot.Value.worldBound.Contains(panelPosition))
                {
                    pos = slot.Key;
                    return true;
                }
            }

            bool found = false;
            float bestDistance = DropTolerance * DropTolerance;
            pos = default;
            foreach (var slot in slots)
            {
                if (!CanDropOn(slot.Key))
                    continue;

                Rect bounds = slot.Value.worldBound;
                var edge = new Vector2(Mathf.Clamp(panelPosition.x, bounds.xMin, bounds.xMax),
                    Mathf.Clamp(panelPosition.y, bounds.yMin, bounds.yMax));
                float distance = (edge - panelPosition).sqrMagnitude;
                if (distance > bestDistance)
                    continue;

                bestDistance = distance;
                pos = slot.Key;
                found = true;
            }
            return found;
        }

        private VisualElement returnView;

        private IEnumerator ReturnCard(VisualElement card, VisualElement source, Vector2 start, float tilt)
        {
            returnView = card;
            Place(card, start);
            Vector2 end = screen.WorldToLocal(source.worldBound.position);
            yield return Tween(0.16f, t =>
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

        private void DrawHandFace(VisualElement view, Card card) =>
            DrawCardFace(view, card, game.IsCaptainCard(card), game.Opening, card.IsJunk ? game.JunkAttackBonus : 0);

        private void DrawCardFace(VisualElement view, Card card, bool leader, bool crowned, int extraAttack = 0)
        {
            if (card.Unit != null)
            {
                DrawUnitFace(view, DeploymentPreview(card, leader), crowned);
                return;
            }

            view.AddToClassList("item-card");
            itemCardFace.CloneTree(view);
            var art = view.Q("item-portrait");
            Paint(art, card.Art);
            AddItemEffectBadge(art, card);
            view.Q<Label>("item-name").text = card.Name;
            var description = view.Q<Label>("item-description");
            description.text = card.Description;
            description.EnableInClassList("card-desc-long", card.Action == CardAction.Technique || card.Action == CardAction.Copy);
            if (card.HasAttack)
                art.Add(Stat("attack", (card.Damage + extraAttack).ToString(), "attack-corner"));
            var secondary = view.Q("item-secondary");
            switch (card.Action)
            {
                case CardAction.Freeze: secondary.Add(Stat("snow", card.Amount.ToString(), "rule-badge")); break;
                case CardAction.Heal: secondary.Add(Stat("heal", card.Amount.ToString(), "rule-badge")); break;
                case CardAction.Hasten: secondary.Add(Stat("timer", card.Amount.ToString(), "rule-badge")); break;
                case CardAction.Strike when card.BonusDamage > 0:
                    secondary.Add(Stat(EffectIcon(card.BonusAgainst), $"+{card.BonusDamage}", "rule-badge"));
                    break;
            }
            secondary.style.display = secondary.childCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private VisualElement AddCardArt(VisualElement view, Card card)
        {
            var art = AddPortrait(view, card.Art);
            AddItemEffectBadge(art, card);
            return art;
        }

        private void AddItemEffectBadge(VisualElement art, Card card)
        {
            if (card.Action == CardAction.ApplyEffect)
            {
                var badge = Stat(EffectIcon(card.Effect), $"+{card.Amount}", "item-effect-badge");
                badge.AddToClassList(EffectClass(card.Effect));
                art.Add(badge);
            }
        }

        private static Unit DeploymentPreview(Card card, bool leader) => card.PreviewDeployment(leader);

        private void DrawUnitFace(VisualElement view, Unit unit, bool crowned = false)
        {
            UnitType type = unit.Type;
            view.AddToClassList("unit-face");
            if (view.Q(className: "unit-content") == null)
                unitCardFace.CloneTree(view);
            var fill = view.Q("unit-health-fill");
            fill.style.width = Length.Percent(unit.Health * 100f / unit.MaxHealth);
            view.Q<Label>("unit-health").text = $"{unit.Health}/{unit.MaxHealth}";
            view.Q<Label>("unit-attack").text = unit.Attack.ToString();
            view.Q("unit-counter").style.display = unit.Period > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            view.Q<Label>("unit-counter-value").text = unit.Counter.ToString();

            var statuses = view.Q("unit-effects");
            statuses.Clear();
            if (unit.Leader || crowned)
                statuses.Add(Stat("crown", null, "effect-chip"));
            if (unit.Pendant != Pendant.None)
                statuses.Add(Stat("pendant", null, "effect-chip"));
            AddEffectBadge(statuses, EffectKind.Snow, unit.Frozen);
            AddEffectBadge(statuses, EffectKind.Shell, unit.Shell);
            AddEffectBadge(statuses, EffectKind.Spice, unit.Spice);
            AddEffectBadge(statuses, EffectKind.Overload, unit.Overload);
            AddEffectBadge(statuses, EffectKind.Ink, unit.Ink);
            AddEffectBadge(statuses, EffectKind.Weakness, unit.Weakness);
            AddEffectBadge(statuses, EffectKind.Block, unit.Block);
            AddEffectBadge(statuses, EffectKind.EffectBonus, unit.EffectBonus);
            AddEffectBadge(statuses, EffectKind.Thorns, unit.Thorns + unit.AuraThorns);
            AddEffectBadge(statuses, EffectKind.Burn, unit.Burn);
            AddEffectBadge(statuses, EffectKind.Exposed, unit.Exposed);
            statuses.style.display = statuses.childCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            statuses.EnableInClassList("dense", statuses.childCount > 10);

            Paint(view.Q("unit-portrait"), type.Art);
            view.Q<Label>("unit-name").text = type.Name;
            var tags = view.Q("unit-tags");
            tags.Clear();
            if (unit.Pattern != AttackPattern.Front)
                tags.Add(Text(Expedition.PatternName(unit.Pattern), "trait"));
            if (type.ResistSnow)
                tags.Add(Icon("snow", "resist-icon"));
            if (unit.Frenzy > 1)
                tags.Add(Text($"狂热 ×{unit.Frenzy}", "trait"));
            bool hasAbility = type.Ability.Effect != EffectKind.None;
            var skill = view.Q("unit-ability");
            skill.style.display = hasAbility ? DisplayStyle.Flex : DisplayStyle.None;
            view.Q("unit-traits").style.display = tags.childCount > 0 || hasAbility ? DisplayStyle.Flex : DisplayStyle.None;
            if (hasAbility)
            {
                view.Q<Label>("unit-trigger").text = type.Ability.Timing;
                Paint(view.Q("unit-ability-icon"), $"CardArt/icon_{EffectIcon(type.Ability.Effect)}");
                string value = type.Ability.Effect switch
                {
                    EffectKind.JunkPower or EffectKind.Retaliate => "",
                    EffectKind.AttackDown => $"−{type.Ability.Amount + unit.AbilityBonus}",
                    _ => $"+{type.Ability.Amount + unit.AbilityBonus}"
                };
                view.Q<Label>("unit-ability-amount").text = value;
                skill.EnableInClassList("silenced", unit.Ink > 0);
            }
        }

        private static string EffectClass(EffectKind effect) => effect switch
        {
            EffectKind.Spice => "effect-spice",
            EffectKind.Overload => "effect-overload",
            EffectKind.Ink => "effect-ink",
            EffectKind.Weakness => "effect-weakness",
            EffectKind.Block => "effect-block",
            EffectKind.Thorns => "effect-thorns",
            EffectKind.Burn => "effect-burn",
            EffectKind.Exposed => "effect-exposed",
            _ => "effect-bonus"
        };

        private static string EffectIcon(EffectKind effect) => effect switch
        {
            EffectKind.Heal => "heal",
            EffectKind.Snow => "snow",
            EffectKind.Shell => "shell",
            EffectKind.Spice => "spice",
            EffectKind.TeamSpice => "spice",
            EffectKind.Overload => "overload",
            EffectKind.Ink => "ink",
            EffectKind.Weakness => "weakness",
            EffectKind.Block => "block",
            EffectKind.Thorns => "barbs",
            EffectKind.Burn => "burn",
            EffectKind.AttackGrowth => "attack",
            EffectKind.AttackUp => "attack",
            EffectKind.AttackDown => "attack",
            EffectKind.Retaliate => "barbs",
            EffectKind.StickyFruit => "heal",
            EffectKind.Exposed => "exposed",
            EffectKind.HealthGrowth => "health",
            _ => "bonus"
        };

        private void AddEffectBadge(VisualElement strip, EffectKind effect, int amount)
        {
            if (amount > 0)
            {
                var chip = Stat(EffectIcon(effect), amount.ToString(), "effect-chip");
                if (effect is not EffectKind.Snow and not EffectKind.Shell)
                    chip.AddToClassList(EffectClass(effect));
                strip.Add(chip);
            }
        }

        private VisualElement AddPortrait(VisualElement view, string art)
        {
            var picture = Element("face-art");
            if (!string.IsNullOrEmpty(art))
                Paint(picture, art);
            view.Add(picture);
            return picture;
        }

        private VisualElement Icon(string type, string css)
        {
            var icon = Element(css);
            Paint(icon, $"CardArt/icon_{type}");
            return icon;
        }

        private VisualElement Stat(string type, string value, string location)
        {
            var stat = Element("stat");
            stat.AddToClassList($"stat-{type}");
            stat.AddToClassList(location);
            stat.Add(Icon(type, "stat-icon"));
            if (!string.IsNullOrEmpty(value))
                stat.Add(Text(value, "stat-value"));
            return stat;
        }

        private void Paint(VisualElement target, string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                target.style.backgroundImage = new StyleBackground(StyleKeyword.None);
                return;
            }
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
