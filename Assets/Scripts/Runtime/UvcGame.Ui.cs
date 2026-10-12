using System.Collections.Generic;
using UnityEngine;
using Uvc.Core;

namespace Uvc.Prototype
{
    /// <summary>Grey-box IMGUI layer. Scales with screen height and respects the safe area.</summary>
    public sealed partial class UvcGame
    {
        private const float ReferenceHeight = 720f;
        private const float ButtonHeight = 44f;
        private const int MaxMenuActions = 4;

        private readonly List<Rect> uiRects = new();
        private GUIStyle panelStyle;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle smallStyle;
        private GUIStyle buttonStyle;
        private GUIStyle centeredTitleStyle;
        private GUIStyle slotStyle;
        private GUIStyle tinyStyle;
        private Texture2D panelTexture;

        private string toast;
        private float toastHideAt;
        private string menuNotice;
        private string dialogueSpeaker;
        private string dialogueSpeakerId;
        private string dialogueText;
        private bool dialogueEndsDay;
        private DayResult dayResult;
        private string confirmAction;

        private Interactable menuTarget;
        private Vector2 menuAnchor;
        private bool contextExpanded;

        private string minigameAction;
        private readonly List<int> stockAnswers = new();
        private readonly List<(KafedraUnit Unit, int Lane)> matchDecisions = new();

        private float UiScale => Mathf.Max(0.6f, Screen.height / ReferenceHeight) * settings.uiScale;

        private void ShowToast(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            toast = message;
            toastHideAt = Time.unscaledTime + 6f;
        }

        private bool IsOverUi(Vector2 screenPosition)
        {
            Vector2 gui = new(screenPosition.x / UiScale, (Screen.height - screenPosition.y) / UiScale);
            foreach (Rect rect in uiRects)
            {
                if (rect.Contains(gui))
                {
                    return true;
                }
            }

            return false;
        }

        private void OpenContextMenu(Interactable target, Vector2 screenPosition)
        {
            bool any = false;
            foreach (string actionId in target.ActionIds)
            {
                any |= DayRules.Check(state, actionId).Availability != ActionAvailability.Hidden;
            }

            if (!any)
            {
                ShowToast($"{target.Title}: сейчас здесь нечего делать.");
                return;
            }

            menuTarget = target;
            menuAnchor = new Vector2(screenPosition.x / UiScale, (Screen.height - screenPosition.y) / UiScale);
            contextExpanded = false;
            OpenModal(Modal.ContextMenu);
        }

        private void BeginMinigame(string actionId)
        {
            minigameAction = actionId;
            stockAnswers.Clear();
            matchDecisions.Clear();
            OpenModal(Modal.Minigame);
        }

        private void FinishMinigame(string choice)
        {
            string actionId = minigameAction;
            minigameAction = null;
            modal = Modal.None;
            ApplyOutcome(DayRules.Execute(state, actionId, choice));
        }

        private void CloseSettings()
        {
            settings.Save();
            modal = settingsReturn;
        }

        private void OnGUI()
        {
            EnsureStyles();
            float scale = UiScale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            Rect safe = Screen.safeArea;
            Rect area = Rect.MinMaxRect(safe.xMin / scale, (Screen.height - safe.yMax) / scale, safe.xMax / scale, (Screen.height - safe.yMin) / scale);
            uiRects.Clear();

            if (modal is not (Modal.MainMenu or Modal.ConfirmNewGame) && !(modal == Modal.Settings && settingsReturn == Modal.MainMenu))
            {
                DrawHud(area);
            }

            switch (modal)
            {
                case Modal.MainMenu: DrawMainMenu(width, height); break;
                case Modal.ConfirmNewGame: DrawConfirmNewGame(width, height); break;
                case Modal.Settings: DrawSettings(width, height); break;
                case Modal.Pause: DrawPause(width, height); break;
                case Modal.ContextMenu: DrawContextMenu(area); break;
                case Modal.Dialogue: DrawDialogue(area); break;
                case Modal.Minigame: DrawMinigame(width, height); break;
                case Modal.Confirm: DrawConfirm(width, height); break;
                case Modal.Journal: DrawJournal(width, height); break;
                case Modal.Inventory: DrawInventory(width, height); break;
                case Modal.EveningPrompt: DrawEveningPrompt(width, height); break;
                case Modal.Summary: DrawSummary(width, height); break;
                case Modal.Distraction: DrawDistraction(area); break;
            }
        }

        // ---------- HUD ----------

        private void DrawHud(Rect area)
        {
            // Layout follows the screen mock-up images/game.png.
            if (area.width >= 1100f)
            {
                Rect title = Panel(new Rect(area.x + 10f, area.y + 10f, 330f, 44f));
                GUI.Label(new Rect(title.x + 14f, title.y + 9f, title.width - 28f, 28f), "УВЦ: ДОЖИТЬ ДО ПЯТНИЦЫ", titleStyle);
            }

            Rect clock = Panel(new Rect(area.center.x - 130f, area.y + 10f, 260f, 44f));
            string time = $"ПЯТНИЦА · {state.clock.Format()}" + (state.clock.IsEvening ? " · ВЕЧЕР" : string.Empty);
            GUI.Label(new Rect(clock.x + 10f, clock.y + 8f, clock.width - 20f, 28f), time, centeredTitleStyle);

            float columnX = area.xMax - 310f;
            Rect stats = Panel(new Rect(columnX, area.y + 10f, 300f, 82f));
            DrawBar(new Rect(stats.x + 12f, stats.y + 12f, 276f, 16f), "Энергия", state.stats.energy, new Color(0.35f, 0.8f, 0.4f));
            DrawBar(new Rect(stats.x + 12f, stats.y + 34f, 276f, 16f), "Настроение", state.stats.mood, new Color(0.95f, 0.8f, 0.3f));
            DrawBar(new Rect(stats.x + 12f, stats.y + 56f, 276f, 16f), "Бардак", state.stats.mess, new Color(0.9f, 0.35f, 0.3f));

            List<string> objectives = DayRules.Objectives(state);
            int shown = Mathf.Min(3, objectives.Count);
            Rect tasks = Panel(new Rect(columnX, stats.yMax + 6f, 300f, 40f + shown * 40f));
            GUI.Label(new Rect(tasks.x + 12f, tasks.y + 8f, 276f, 26f), "ЗАДАЧИ", titleStyle);
            for (int index = 0; index < shown; index++)
            {
                GUI.Label(new Rect(tasks.x + 12f, tasks.y + 36f + index * 40f, 276f, 40f), "☐ " + objectives[index], smallStyle);
            }

            string[] labels = { "Задачи (J)", "Сумка (I)", "К герою", "Пауза" };
            for (int index = 0; index < labels.Length; index++)
            {
                Rect button = Register(new Rect(columnX + index % 2 * 152f, tasks.yMax + 6f + index / 2 * 46f, 148f, 40f));
                if (!GUI.Button(button, labels[index], buttonStyle) || modal != Modal.None)
                {
                    continue;
                }

                switch (index)
                {
                    case 0: OpenModal(Modal.Journal); break;
                    case 1: OpenModal(Modal.Inventory); break;
                    case 2:
                        cameraRig.Following = true;
                        break;
                    case 3: OpenModal(Modal.Pause); break;
                }
            }

            float bottom = Mathf.Min(DrawQuickBar(area), DrawEventBar(area));
            if (!string.IsNullOrEmpty(toast) && Time.unscaledTime < toastHideAt && modal is not (Modal.Dialogue or Modal.Distraction))
            {
                float toastWidth = Mathf.Min(640f, area.width - 20f);
                Rect toastRect = Panel(new Rect(area.center.x - toastWidth * 0.5f, bottom - 72f, toastWidth, 64f));
                GUI.Label(new Rect(toastRect.x + 14f, toastRect.y + 8f, toastRect.width - 28f, 50f), toast, bodyStyle);
            }
        }

        /// <summary>Hero portrait and the first five bag slots. Returns the top edge.</summary>
        private float DrawQuickBar(Rect area)
        {
            const float size = 88f;
            const float slot = 64f;
            float y = area.yMax - size - 10f;
            Rect portrait = Panel(new Rect(area.x + 10f, y, size, size));
            DrawPicture(new Rect(portrait.x + 4f, portrait.y + 4f, size - 8f, size - 8f), Art.Portrait(NpcIds.Player), "Вы");

            for (int index = 0; index < 5; index++)
            {
                Rect cell = Register(new Rect(portrait.xMax + 8f + index * (slot + 8f), y + size - slot, slot, slot));
                string label = (index + 1).ToString();
                if (index < state.inventory.slots.Count)
                {
                    ItemStack stack = state.inventory.slots[index];
                    label = ItemCatalog.NameOf(stack.itemId) + (stack.count > 1 ? $" ×{stack.count}" : string.Empty);
                }

                if (GUI.Button(cell, label, slotStyle) && modal == Modal.None)
                {
                    OpenModal(Modal.Inventory);
                }
            }

            return y;
        }

        /// <summary>Timer of the active large event (only Maimeskul for now). Returns the top edge.</summary>
        private float DrawEventBar(Rect area)
        {
            float width = Mathf.Min(400f, area.width - 500f);
            if (!state.maimeskul.active || width < 260f)
            {
                return area.yMax;
            }

            float remaining = state.maimeskul.Remaining(state.clock.Absolute);
            Rect panel = Panel(new Rect(Mathf.Max(area.center.x - width * 0.5f, area.x + 480f), area.yMax - 84f, width, 74f));
            DrawPicture(new Rect(panel.x + 6f, panel.y + 6f, 62f, 62f), Art.Portrait(NpcIds.Maimeskul), "М");
            GUI.Label(new Rect(panel.x + 78f, panel.y + 8f, width - 90f, 26f), "МАЙМЕСКУЛ ВЕРНУЛСЯ · " + GameClock.FormatDuration(remaining), titleStyle);
            DrawFill(new Rect(panel.x + 78f, panel.y + 40f, width - 92f, 12f), remaining / DayRules.MaimeskulDuration, new Color(0.85f, 0.25f, 0.25f));
            GUI.Label(new Rect(panel.x + 78f, panel.y + 52f, width - 90f, 20f), "игровое время", tinyStyle);
            return panel.y;
        }

        private void DrawBar(Rect rect, string label, int value, Color color)
        {
            GUI.Label(new Rect(rect.x, rect.y - 3f, 110f, 22f), label, smallStyle);
            Rect track = new(rect.x + 100f, rect.y + 2f, rect.width - 140f, rect.height - 4f);
            DrawFill(track, value / 100f, color);
            GUI.Label(new Rect(track.xMax + 8f, rect.y - 3f, 36f, 22f), value.ToString(), smallStyle);
        }

        private static void DrawFill(Rect track, float fraction, Color color)
        {
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.15f);
            GUI.DrawTexture(track, Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(track.x, track.y, track.width * Mathf.Clamp01(fraction), track.height), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        /// <summary>Draws a placeholder picture, or its caption when the file is missing.</summary>
        private void DrawPicture(Rect rect, Texture2D texture, string fallback)
        {
            if (texture != null)
            {
                GUI.DrawTexture(rect, texture, ScaleMode.ScaleAndCrop);
            }
            else
            {
                GUI.Label(rect, fallback, centeredTitleStyle);
            }
        }

        // ---------- Interaction windows ----------

        private void DrawContextMenu(Rect area)
        {
            if (menuTarget == null)
            {
                modal = Modal.None;
                return;
            }

            List<(string Id, ActionCheck Check)> actions = new();
            foreach (string actionId in menuTarget.ActionIds)
            {
                ActionCheck check = DayRules.Check(state, actionId);
                if (check.Availability != ActionAvailability.Hidden)
                {
                    actions.Add((actionId, check));
                }
            }

            int visible = contextExpanded ? actions.Count : Mathf.Min(MaxMenuActions, actions.Count);
            bool needsMore = actions.Count > visible;
            float rowHeight = ButtonHeight + 22f;
            const float header = 80f;
            float height = header + visible * rowHeight + (needsMore ? ButtonHeight + 6f : 0f) + ButtonHeight + 10f;
            float width = 300f;
            float x = Mathf.Clamp(menuAnchor.x + 16f, area.xMin + 8f, area.xMax - width - 8f);
            float y = Mathf.Clamp(menuAnchor.y - 40f, area.yMin + 8f, area.yMax - height - 8f);
            Rect panel = Panel(new Rect(x, y, width, height));
            Texture2D icon = menuTarget.Icon;
            float titleX = panel.x + 14f;
            if (icon != null)
            {
                GUI.DrawTexture(new Rect(panel.x + 10f, panel.y + 8f, 64f, 64f), icon, ScaleMode.ScaleAndCrop);
                titleX = panel.x + 84f;
            }

            GUI.Label(new Rect(titleX, panel.y + 24f, panel.xMax - titleX - 10f, 28f), menuTarget.Title, titleStyle);

            float rowY = panel.y + header;
            for (int index = 0; index < visible; index++)
            {
                (string actionId, ActionCheck check) = actions[index];
                GUI.enabled = check.IsReady;
                string label = (check.IsReady ? "▶ " : "✕ ") + ActionCatalog.Title(actionId);
                if (GUI.Button(new Rect(panel.x + 12f, rowY, width - 24f, ButtonHeight), label, buttonStyle))
                {
                    GUI.enabled = true;
                    ChooseAction(menuTarget, actionId);
                    return;
                }

                GUI.enabled = true;
                if (!check.IsReady)
                {
                    GUI.Label(new Rect(panel.x + 16f, rowY + ButtonHeight, width - 32f, 22f), check.Reason, smallStyle);
                }

                rowY += rowHeight;
            }

            if (needsMore && GUI.Button(new Rect(panel.x + 12f, rowY, width - 24f, ButtonHeight), "Ещё…", buttonStyle))
            {
                contextExpanded = true;
            }

            if (needsMore)
            {
                rowY += ButtonHeight + 6f;
            }

            if (GUI.Button(new Rect(panel.x + 12f, rowY, width - 24f, ButtonHeight), "Закрыть", buttonStyle))
            {
                modal = Modal.None;
            }
        }

        private void DrawDialogue(Rect area)
        {
            float width = Mathf.Min(720f, area.width - 20f);
            Rect panel = Panel(new Rect(area.center.x - width * 0.5f, area.yMax - 220f, width, 210f));
            Texture2D portrait = string.IsNullOrEmpty(dialogueSpeakerId) ? null : Art.Portrait(dialogueSpeakerId);
            float textX = panel.x + 18f;
            if (portrait != null)
            {
                GUI.DrawTexture(new Rect(panel.x + 12f, panel.y + 12f, 140f, 140f), portrait, ScaleMode.ScaleAndCrop);
                textX = panel.x + 168f;
            }

            GUI.Label(new Rect(textX, panel.y + 12f, panel.xMax - textX - 18f, 28f), dialogueSpeaker, titleStyle);
            GUI.Label(new Rect(textX, panel.y + 46f, panel.xMax - textX - 18f, 110f), dialogueText, bodyStyle);
            if (GUI.Button(new Rect(panel.xMax - 172f, panel.yMax - ButtonHeight - 10f, 160f, ButtonHeight), "Дальше", buttonStyle))
            {
                CloseDialogue();
            }
        }

        private void DrawMinigame(float width, float height)
        {
            Rect panel = CenterPanel(width, height, 600f, 420f);
            string title = minigameAction switch
            {
                ActionIds.PrinterRepair => "Ремонт принтера",
                ActionIds.ProjectorSetup => "Настройка проектора",
                ActionIds.StockSort => "Разбор оборудования",
                ActionIds.TerminalPlay => "КафедраCraft",
                ActionIds.ProdanovChess => "Шахматы с Продановым",
                _ => "Мини-игра"
            };
            GUI.Label(new Rect(panel.x + 18f, panel.y + 12f, panel.width - 36f, 28f), title, titleStyle);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 44f, panel.width - 36f, 120f), DayRules.MinigameIntro(state, minigameAction), bodyStyle);
            Rect body = new(panel.x + 18f, panel.y + 170f, panel.width - 36f, panel.height - 230f);

            switch (minigameAction)
            {
                case ActionIds.StockSort:
                    DrawStockSort(body);
                    break;
                case ActionIds.TerminalPlay:
                    DrawMatch(body);
                    break;
                default:
                    List<ChoiceOption> choices = DayRules.Choices(state, minigameAction);
                    for (int index = 0; index < choices.Count; index++)
                    {
                        if (GUI.Button(new Rect(body.x, body.y + index * (ButtonHeight + 8f), body.width, ButtonHeight), choices[index].Label, buttonStyle))
                        {
                            FinishMinigame(choices[index].Id);
                            return;
                        }
                    }

                    break;
            }

            if (minigameAction != null &&
                GUI.Button(new Rect(panel.xMax - 172f, panel.yMax - ButtonHeight - 12f, 160f, ButtonHeight), "Отмена", buttonStyle))
            {
                minigameAction = null;
                modal = Modal.None;
                ShowToast("Отменено. Время и предметы не потрачены.");
            }
        }

        private void DrawStockSort(Rect body)
        {
            int index = stockAnswers.Count;
            if (index >= StockSorting.Items.Length)
            {
                FinishMinigame(StockSorting.Encode(stockAnswers));
                return;
            }

            GUI.Label(new Rect(body.x, body.y, body.width, 26f), $"Предмет {index + 1} из {StockSorting.Items.Length}: «{StockSorting.Items[index].Name}». Куда положить?", bodyStyle);
            float buttonWidth = (body.width - 16f) / StockSorting.Categories.Length;
            for (int category = 0; category < StockSorting.Categories.Length; category++)
            {
                if (GUI.Button(new Rect(body.x + category * (buttonWidth + 8f), body.y + 40f, buttonWidth, ButtonHeight), StockSorting.Categories[category], buttonStyle))
                {
                    stockAnswers.Add(category);
                }
            }
        }

        private void DrawMatch(Rect body)
        {
            List<string> picks = new();
            foreach ((KafedraUnit unit, int lane) in matchDecisions)
            {
                picks.Add($"{KafedraMatch.UnitName(unit)} → {KafedraMatch.LaneNames[lane].ToLowerInvariant()}");
            }

            GUI.Label(new Rect(body.x, body.y, body.width, 24f),
                $"Решения {matchDecisions.Count}/{KafedraMatch.Decisions}: " + (picks.Count > 0 ? string.Join("; ", picks) : "—"), smallStyle);

            if (matchDecisions.Count < KafedraMatch.Decisions)
            {
                float buttonWidth = (body.width - 16f) / 3f;
                for (int lane = 0; lane < 2; lane++)
                {
                    for (int unit = 0; unit < 3; unit++)
                    {
                        string label = $"{KafedraMatch.UnitName((KafedraUnit)unit)} {(lane == 0 ? "←" : "→")}";
                        Rect button = new(body.x + unit * (buttonWidth + 8f), body.y + 30f + lane * (ButtonHeight + 8f), buttonWidth, ButtonHeight);
                        if (GUI.Button(button, label, buttonStyle))
                        {
                            matchDecisions.Add(((KafedraUnit)unit, lane));
                        }
                    }
                }

                return;
            }

            if (GUI.Button(new Rect(body.x, body.y + 30f, 200f, ButtonHeight), "В бой! (30 мин)", buttonStyle))
            {
                FinishMinigame(KafedraMatch.Encode(matchDecisions));
                return;
            }

            if (GUI.Button(new Rect(body.x + 210f, body.y + 30f, 140f, ButtonHeight), "Сбросить", buttonStyle))
            {
                matchDecisions.Clear();
            }
        }

        private void DrawConfirm(float width, float height)
        {
            Rect panel = CenterPanel(width, height, 560f, 260f);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 12f, panel.width - 36f, 28f), ActionCatalog.Title(confirmAction), titleStyle);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 46f, panel.width - 36f, 140f), DayRules.ConfirmText(state, confirmAction), bodyStyle);
            if (GUI.Button(new Rect(panel.x + 18f, panel.yMax - ButtonHeight - 14f, 200f, ButtonHeight), "Да", buttonStyle))
            {
                string actionId = confirmAction;
                modal = Modal.None;
                ApplyOutcome(DayRules.Execute(state, actionId));
            }
            else if (GUI.Button(new Rect(panel.xMax - 218f, panel.yMax - ButtonHeight - 14f, 200f, ButtonHeight), "Не сейчас", buttonStyle))
            {
                modal = Modal.None;
            }
        }

        private void DrawJournal(float width, float height)
        {
            Rect panel = CenterPanel(width, height, 560f, 420f);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 12f, panel.width - 36f, 28f), "Задачи", titleStyle);
            float y = panel.y + 48f;
            foreach (string objective in DayRules.Objectives(state))
            {
                GUI.Label(new Rect(panel.x + 18f, y, panel.width - 36f, 26f), "• " + objective, bodyStyle);
                y += 28f;
            }

            y += 8f;
            GUI.Label(new Rect(panel.x + 18f, y, panel.width - 36f, 24f),
                $"Репутация у руководства {state.stats.bossReputation} · у студентов {state.stats.studentReputation}", smallStyle);
            if (GUI.Button(new Rect(panel.xMax - 172f, panel.yMax - ButtonHeight - 12f, 160f, ButtonHeight), "Закрыть", buttonStyle))
            {
                modal = Modal.None;
            }
        }

        private void DrawInventory(float width, float height)
        {
            Rect panel = CenterPanel(width, height, 560f, 420f);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 12f, panel.width - 36f, 28f), $"Сумка ({state.inventory.slots.Count}/{Inventory.SlotCount})", titleStyle);
            float cell = (panel.width - 36f - 3 * 8f) / 4f;
            for (int index = 0; index < Inventory.SlotCount; index++)
            {
                Rect slot = new(panel.x + 18f + index % 4 * (cell + 8f), panel.y + 50f + index / 4 * 58f, cell, 50f);
                GUI.Box(slot, GUIContent.none);
                if (index < state.inventory.slots.Count)
                {
                    ItemStack stack = state.inventory.slots[index];
                    string text = ItemCatalog.NameOf(stack.itemId) + (stack.count > 1 ? $" ×{stack.count}" : string.Empty);
                    GUI.Label(new Rect(slot.x + 6f, slot.y + 4f, slot.width - 12f, slot.height - 8f), text, smallStyle);
                }
            }

            List<string> questItems = new();
            foreach (ItemStack stack in state.inventory.questItems)
            {
                questItems.Add(ItemCatalog.NameOf(stack.itemId));
            }

            GUI.Label(new Rect(panel.x + 18f, panel.y + 176f, panel.width - 36f, 24f), "Квестовые предметы (нельзя выбросить):", bodyStyle);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 202f, panel.width - 36f, 60f), questItems.Count > 0 ? string.Join(", ", questItems) : "—", smallStyle);
            if (GUI.Button(new Rect(panel.xMax - 172f, panel.yMax - ButtonHeight - 12f, 160f, ButtonHeight), "Закрыть", buttonStyle))
            {
                modal = Modal.None;
            }
        }

        private void DrawEveningPrompt(float width, float height)
        {
            Rect panel = CenterPanel(width, height, 560f, 240f);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 12f, panel.width - 36f, 28f), "18:00 — рабочий день окончен", titleStyle);
            string text = state.IsQuestAccepted(QuestIds.Party)
                ? "Друзья собираются в комнате отдыха (ПК 4). До 20:00 можно провести вечер."
                : "Можно уйти домой или задержаться до 20:00.";
            GUI.Label(new Rect(panel.x + 18f, panel.y + 48f, panel.width - 36f, 100f), text, bodyStyle);
            if (GUI.Button(new Rect(panel.x + 18f, panel.yMax - ButtonHeight - 14f, 230f, ButtonHeight), "Остаться на вечер", buttonStyle))
            {
                modal = Modal.None;
            }
            else if (GUI.Button(new Rect(panel.xMax - 248f, panel.yMax - ButtonHeight - 14f, 230f, ButtonHeight), "Завершить день", buttonStyle))
            {
                EndDay();
            }
        }

        private void DrawSummary(float width, float height)
        {
            if (dayResult == null)
            {
                dayResult = DayResults.Compute(state);
            }

            Rect panel = CenterPanel(width, height, 620f, 460f);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 12f, panel.width - 36f, 28f), "Итоги дня: " + dayResult.Grade, titleStyle);
            float y = panel.y + 52f;
            foreach (string line in dayResult.Lines)
            {
                GUI.Label(new Rect(panel.x + 18f, y, panel.width - 36f, 44f), "• " + line, bodyStyle);
                y += line.Length > 60 ? 44f : 26f;
            }

            if (GUI.Button(new Rect(panel.x + 18f, panel.yMax - ButtonHeight - 14f, 220f, ButtonHeight), "Повторить день", buttonStyle))
            {
                RestartDay();
            }
            else if (GUI.Button(new Rect(panel.xMax - 238f, panel.yMax - ButtonHeight - 14f, 220f, ButtonHeight), "Новая пятница", buttonStyle))
            {
                StartNewGame();
            }
        }

        // ---------- Menus ----------

        private void DrawMainMenu(float width, float height)
        {
            Rect panel = CenterPanel(width, height, 460f, 360f);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 16f, panel.width - 36f, 30f), "УВЦ: ДОЖИТЬ ДО ПЯТНИЦЫ", titleStyle);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 50f, panel.width - 36f, 24f), "Серый прототип · этап B", smallStyle);
            float y = panel.y + 92f;
            GUI.enabled = savedState != null;
            if (GUI.Button(new Rect(panel.x + 40f, y, panel.width - 80f, ButtonHeight), "Продолжить", buttonStyle))
            {
                GUI.enabled = true;
                BeginSession(savedState);
                ShowToast("Смена восстановлена из сохранения.");
                return;
            }

            GUI.enabled = true;
            if (GUI.Button(new Rect(panel.x + 40f, y + 54f, panel.width - 80f, ButtonHeight), "Новая игра", buttonStyle))
            {
                if (SaveSystem.HasSave)
                {
                    modal = Modal.ConfirmNewGame;
                }
                else
                {
                    StartNewGame();
                }
            }

            if (GUI.Button(new Rect(panel.x + 40f, y + 108f, panel.width - 80f, ButtonHeight), "Настройки", buttonStyle))
            {
                settingsReturn = Modal.MainMenu;
                modal = Modal.Settings;
            }

            if (!string.IsNullOrEmpty(menuNotice))
            {
                GUI.Label(new Rect(panel.x + 18f, y + 166f, panel.width - 36f, 80f), menuNotice, smallStyle);
            }
        }

        private void DrawConfirmNewGame(float width, float height)
        {
            Rect panel = CenterPanel(width, height, 480f, 220f);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 14f, panel.width - 36f, 28f), "Новая игра", titleStyle);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 50f, panel.width - 36f, 80f), "Текущее прохождение будет удалено. Начать заново?", bodyStyle);
            if (GUI.Button(new Rect(panel.x + 18f, panel.yMax - ButtonHeight - 14f, 200f, ButtonHeight), "Да, заново", buttonStyle))
            {
                StartNewGame();
            }
            else if (GUI.Button(new Rect(panel.xMax - 218f, panel.yMax - ButtonHeight - 14f, 200f, ButtonHeight), "Отмена", buttonStyle))
            {
                modal = Modal.MainMenu;
            }
        }

        private void DrawSettings(float width, float height)
        {
            Rect panel = CenterPanel(width, height, 460f, 240f);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 14f, panel.width - 36f, 28f), "Настройки", titleStyle);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 60f, 200f, 24f), $"Размер интерфейса: {settings.uiScale:0.0}", bodyStyle);
            settings.uiScale = Mathf.Round(GUI.HorizontalSlider(new Rect(panel.x + 220f, panel.y + 68f, panel.width - 240f, 20f), settings.uiScale, 0.8f, 1.6f) * 10f) / 10f;
            GUI.Label(new Rect(panel.x + 18f, panel.y + 100f, panel.width - 36f, 40f), "Звук, графика, вибрация и язык появятся на этапе визуального среза.", smallStyle);
            if (GUI.Button(new Rect(panel.xMax - 172f, panel.yMax - ButtonHeight - 14f, 160f, ButtonHeight), "Готово", buttonStyle))
            {
                CloseSettings();
            }
        }

        private void DrawPause(float width, float height)
        {
            Rect panel = CenterPanel(width, height, 400f, 270f);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 14f, panel.width - 36f, 28f), "Пауза", titleStyle);
            if (GUI.Button(new Rect(panel.x + 40f, panel.y + 60f, panel.width - 80f, ButtonHeight), "Продолжить", buttonStyle))
            {
                modal = Modal.None;
            }
            else if (GUI.Button(new Rect(panel.x + 40f, panel.y + 114f, panel.width - 80f, ButtonHeight), "Настройки", buttonStyle))
            {
                settingsReturn = Modal.Pause;
                modal = Modal.Settings;
            }
            else if (GUI.Button(new Rect(panel.x + 40f, panel.y + 168f, panel.width - 80f, ButtonHeight), "Сохранить и выйти в меню", buttonStyle))
            {
                SaveNow();
                SaveSystem.Load(out savedState);
                modal = Modal.MainMenu;
            }
        }

        // ---------- Helpers ----------

        private Rect CenterPanel(float width, float height, float panelWidth, float panelHeight)
        {
            panelWidth = Mathf.Min(panelWidth, width - 20f);
            panelHeight = Mathf.Min(panelHeight, height - 20f);
            return Panel(new Rect((width - panelWidth) * 0.5f, (height - panelHeight) * 0.5f, panelWidth, panelHeight));
        }

        private Rect Panel(Rect rect)
        {
            Register(rect);
            GUI.Box(rect, GUIContent.none, panelStyle);
            return rect;
        }

        private Rect Register(Rect rect)
        {
            uiRects.Add(rect);
            return rect;
        }

        private void EnsureStyles()
        {
            if (panelStyle != null)
            {
                return;
            }

            panelTexture = new Texture2D(1, 1);
            panelTexture.SetPixel(0, 0, new Color(0.08f, 0.1f, 0.13f, 0.92f));
            panelTexture.Apply();
            panelStyle = new GUIStyle(GUI.skin.box) { normal = { background = panelTexture } };
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                normal = { textColor = new Color(1f, 0.78f, 0.32f) }
            };
            bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true, normal = { textColor = Color.white } };
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true, normal = { textColor = new Color(0.85f, 0.88f, 0.9f) } };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 16, wordWrap = true };
            centeredTitleStyle = new GUIStyle(titleStyle) { alignment = TextAnchor.MiddleCenter };
            slotStyle = new GUIStyle(GUI.skin.button) { fontSize = 12, wordWrap = true, alignment = TextAnchor.MiddleCenter };
            tinyStyle = new GUIStyle(smallStyle) { fontSize = 11 };
        }
    }
}
