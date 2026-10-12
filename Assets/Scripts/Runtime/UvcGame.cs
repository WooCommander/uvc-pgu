using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using Uvc.Core;
using Random = UnityEngine.Random;

namespace Uvc.Prototype
{
    public static class UvcBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateGame()
        {
            if (UnityEngine.Object.FindAnyObjectByType<UvcGame>() == null)
            {
                new GameObject("UVC Game").AddComponent<UvcGame>();
            }
        }
    }

    /// <summary>
    /// Connects the pure day rules (Uvc.Core) with the grey-box world, input, camera and saves.
    /// UI lives in UvcGame.Ui.cs.
    /// </summary>
    public sealed partial class UvcGame : MonoBehaviour
    {
        private const float AutosaveInterval = 60f;
        private const float ScheduleInterval = 0.5f;
        private const float ReactionDistance = 2.2f;
        private const float TapMaxMove = 20f;
        private const float TapMaxSeconds = 0.6f;

        private enum Modal
        {
            None,
            MainMenu,
            ConfirmNewGame,
            Settings,
            Pause,
            ContextMenu,
            Dialogue,
            Minigame,
            Confirm,
            Journal,
            Inventory,
            EveningPrompt,
            Summary,
            Distraction
        }

        private DayState state;
        private DayState savedState;
        private FloorLayout layout;
        private CameraRig cameraRig;
        private PlayerActor player;
        private MaimeskulActor maimeskul;
        private readonly List<NpcActor> npcs = new();
        private Settings settings;

        private Modal modal = Modal.MainMenu;
        private Modal settingsReturn = Modal.MainMenu;
        private Interactable pendingTarget;
        private string pendingAction;
        private Vector3 pendingTargetLastPosition;
        private int pendingRetries;

        private float autosaveTimer;
        private float scheduleTimer;
        private float reactionTimer;
        private GameObject moveMarker;
        private float markerHideAt;

        private Vector2 touchStart;
        private float touchStartTime;
        private bool touchMoved;
        private bool multiTouch;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            settings = Settings.Load();
            cameraRig = new CameraRig(Camera.main != null ? Camera.main : new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>());

            layout = new FloorLayout(new GameObject("Floor").transform, CameraRig.Rotation);
            layout.Build();

            player = PlayerActor.Create(FloorLayout.PlayerSpawn, CameraRig.Rotation);
            CreateNpcs();
            moveMarker = Prim.Shape(null, "Move marker", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.5f, 0.02f, 0.5f), Prim.Material("Marker"));
            Destroy(moveMarker.GetComponent<Collider>());
            moveMarker.SetActive(false);

            state = DayState.CreateNew(0);
            cameraRig.SnapTo(player.transform.position);

            LoadStatus status = SaveSystem.Load(out savedState);
            menuNotice = status switch
            {
                LoadStatus.RestoredFromBackup => "Основное сохранение было повреждено — восстановлена резервная копия.",
                LoadStatus.Corrupted => "Сохранение повреждено и не может быть загружено.",
                LoadStatus.NewerSchema => "Сохранение создано более новой версией игры. Оно не будет перезаписано без подтверждения.",
                _ => null
            };
            modal = Modal.MainMenu;
        }

        private void CreateNpcs()
        {
            Quaternion rotation = CameraRig.Rotation;
            AddNpc(NpcActor.Create(NpcIds.Boss, new Color(0.55f, 0.55f, 0.8f), new Vector3(6.5f, 0f, 8.2f), rotation,
                    new ScheduleEntry(540, new Vector3(6.5f, 0f, 8.2f)),
                    new ScheduleEntry(780, new Vector3(2.5f, 0f, -2.5f)),
                    new ScheduleEntry(820, new Vector3(6.5f, 0f, 8.2f))),
                ActionIds.Talk(NpcIds.Boss), ActionIds.BossTakeTask, ActionIds.BossDeliver);

            AddNpc(NpcActor.Create(NpcIds.Serega, new Color(0.3f, 0.7f, 0.45f), new Vector3(0.4f, 0f, -5.8f), rotation,
                    new ScheduleEntry(540, new Vector3(2.8f, 0f, -2f)),
                    new ScheduleEntry(630, new Vector3(3.2f, 0f, 5.2f)),
                    new ScheduleEntry(780, new Vector3(2.8f, 0f, -2f)),
                    new ScheduleEntry(840, new Vector3(3.2f, 0f, 5.2f)),
                    new ScheduleEntry(1020, new Vector3(2.8f, 0f, -2f))),
                ActionIds.Talk(NpcIds.Serega), ActionIds.SeregaParty);

            AddNpc(NpcActor.Create(NpcIds.Andryukha, new Color(0.85f, 0.35f, 0.3f), new Vector3(3.6f, 0f, -5.8f), rotation,
                    new ScheduleEntry(540, new Vector3(-4f, 0f, 1.6f)),
                    new ScheduleEntry(660, new Vector3(9.35f, 0f, -15f)),
                    new ScheduleEntry(720, new Vector3(-4f, 0f, 1.6f)),
                    new ScheduleEntry(840, new Vector3(3f, 0f, -1f)),
                    new ScheduleEntry(900, new Vector3(-4f, 0f, 1.6f))),
                ActionIds.Talk(NpcIds.Andryukha), ActionIds.Invite(NpcIds.Andryukha), ActionIds.AndryukhaSecret,
                ActionIds.AndryukhaRitual, ActionIds.AndryukhaLastErrand);

            AddNpc(NpcActor.Create(NpcIds.John, new Color(0.3f, 0.5f, 0.9f), new Vector3(3f, 0f, -7.2f), rotation,
                    new ScheduleEntry(540, new Vector3(9.35f, 0f, -9f))),
                ActionIds.Talk(NpcIds.John), ActionIds.JohnAdapter, ActionIds.Invite(NpcIds.John));

            AddNpc(NpcActor.Create(NpcIds.Vigo, new Color(0.9f, 0.8f, 0.3f), new Vector3(1f, 0f, -7.2f), rotation,
                    new ScheduleEntry(540, new Vector3(2.2f, 0f, 7.2f)),
                    new ScheduleEntry(750, new Vector3(0.8f, 0f, -6.8f)),
                    new ScheduleEntry(795, new Vector3(2.2f, 0f, 7.2f))),
                ActionIds.Talk(NpcIds.Vigo), ActionIds.VigoDiagnose, ActionIds.Invite(NpcIds.Vigo));

            AddNpc(NpcActor.Create(NpcIds.Lecturer, new Color(0.7f, 0.5f, 0.8f), Vector3.zero, rotation,
                    new ScheduleEntry(540, new Vector3(-12.6f, 0f, 6.5f))),
                ActionIds.Talk(NpcIds.Lecturer));

            AddNpc(NpcActor.Create(NpcIds.Storekeeper, new Color(0.6f, 0.45f, 0.3f), Vector3.zero, rotation,
                    new ScheduleEntry(540, new Vector3(6.2f, 0f, -17.2f)),
                    new ScheduleEntry(900, new Vector3(-1f, 0f, 1.5f)),
                    new ScheduleEntry(940, new Vector3(6.2f, 0f, -17.2f))),
                ActionIds.Talk(NpcIds.Storekeeper), ActionIds.StockSort);

            // Background student: the far room never sleeps.
            AddNpc(NpcActor.Create(NpcIds.Bogdan, new Color(0.25f, 0.25f, 0.28f), Vector3.zero, rotation,
                    new ScheduleEntry(540, new Vector3(9.35f, 0f, -13f)),
                    new ScheduleEntry(720, new Vector3(1.5f, 0f, -2.5f)),
                    new ScheduleEntry(760, new Vector3(9.35f, 0f, -13f))),
                ActionIds.Talk(NpcIds.Bogdan));

            CreateProdanovCircle();
        }

        private void AddNpc(NpcActor npc, params string[] actions)
        {
            npc.gameObject.AddComponent<Interactable>().ConfigurePerson(npc, npc.NpcId, NpcIds.DisplayName(npc.NpcId), actions);
            npcs.Add(npc);
        }

        private void Update()
        {
            bool paused = modal != Modal.None;
            Time.timeScale = paused ? 0f : 1f;
            HandleKeyboard();

            if (!paused)
            {
                HandlePointer();
                TickRules();
                UpdatePending();
                UpdateWorld();
            }

            cameraRig.Update(player.transform.position, Time.unscaledDeltaTime);
            if (moveMarker.activeSelf && (Time.unscaledTime > markerHideAt || !player.IsMoving))
            {
                moveMarker.SetActive(false);
            }
        }

        private void TickRules()
        {
            TickResult tick = DayRules.Tick(state, Time.deltaTime);
            if (tick.MaimeskulExpired)
            {
                DespawnMaimeskul();
                ShowToast("Срок вышел: Маймескул растворился в воздухе. Временные эффекты сняты.");
                SaveNow();
            }

            HandleVisitorTick(tick);

            if (tick.PartyOpened)
            {
                ShowToast("Серёга ищет вас: есть разговор о вечере.");
            }

            if (tick.DayOver)
            {
                EndDay();
                return;
            }

            if (tick.EveningStarted)
            {
                OpenModal(Modal.EveningPrompt);
            }
        }

        private void UpdateWorld()
        {
            scheduleTimer += Time.deltaTime;
            if (scheduleTimer >= ScheduleInterval)
            {
                foreach (NpcActor npc in npcs)
                {
                    npc.UpdateSchedule(state, scheduleTimer);
                }

                scheduleTimer = 0f;
            }

            if (maimeskul != null)
            {
                reactionTimer += Time.deltaTime;
                if (reactionTimer >= 0.25f)
                {
                    reactionTimer = 0f;
                    UpdateMaimeskulReactions();
                }
            }

            UpdateProdanovDistraction();

            autosaveTimer += Time.deltaTime;
            if (autosaveTimer >= AutosaveInterval)
            {
                SaveNow();
            }
        }

        private void UpdateMaimeskulReactions()
        {
            Vector3 origin = maimeskul.FeetPosition;
            foreach (NpcActor npc in npcs)
            {
                if (npc.FlatDistanceTo(origin) < ReactionDistance && DayRules.TryMaimeskulReaction(state, npc.NpcId, out string message))
                {
                    Vector3 away = npc.FeetPosition - origin;
                    away.y = 0f;
                    away = away.sqrMagnitude < 0.01f ? Random.insideUnitSphere : away.normalized;
                    npc.Flee(npc.FeetPosition + away * 5f, 5f);
                    DustPuff.Spawn(npc.FeetPosition);
                    ShowToast(message);
                }
            }

            if (player.FlatDistanceTo(origin) < ReactionDistance && DayRules.TryMaimeskulReaction(state, NpcIds.Player, out string playerMessage))
            {
                DustPuff.Spawn(player.FeetPosition);
                ShowToast(playerMessage);
            }
        }

        // ---------- Input ----------

        private void HandleKeyboard()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                HandleBack();
            }
            else if (keyboard.iKey.wasPressedThisFrame)
            {
                ToggleModal(Modal.Inventory);
            }
            else if (keyboard.jKey.wasPressedThisFrame)
            {
                ToggleModal(Modal.Journal);
            }
        }

        private void HandleBack()
        {
            switch (modal)
            {
                case Modal.None:
                    OpenModal(Modal.Pause);
                    break;
                case Modal.Settings:
                    CloseSettings();
                    break;
                case Modal.ConfirmNewGame:
                    modal = Modal.MainMenu;
                    break;
                case Modal.Dialogue:
                    CloseDialogue();
                    break;
                case Modal.Distraction:
                    DeclineProdanov();
                    break;
                case Modal.ContextMenu:
                case Modal.Minigame:
                case Modal.Confirm:
                case Modal.Journal:
                case Modal.Inventory:
                case Modal.Pause:
                    modal = Modal.None;
                    break;
            }
        }

        private void ToggleModal(Modal target)
        {
            if (modal == target)
            {
                modal = Modal.None;
            }
            else if (modal == Modal.None)
            {
                OpenModal(target);
            }
        }

        private void OpenModal(Modal target)
        {
            modal = target;
            if (target != Modal.ContextMenu)
            {
                contextExpanded = false;
            }
        }

        private void HandlePointer()
        {
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 position = mouse.position.ReadValue();
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f && !IsOverUi(position))
                {
                    cameraRig.Zoom(scroll > 0f ? 0.9f : 1.1f);
                }

                if (mouse.middleButton.isPressed)
                {
                    cameraRig.Pan(mouse.delta.ReadValue());
                }

                if (mouse.leftButton.wasPressedThisFrame)
                {
                    Tap(position);
                }
            }

            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen == null)
            {
                return;
            }

            int active = 0;
            Vector2 deltaSum = Vector2.zero;
            UnityEngine.InputSystem.Controls.TouchControl first = null;
            UnityEngine.InputSystem.Controls.TouchControl second = null;
            foreach (UnityEngine.InputSystem.Controls.TouchControl touch in touchscreen.touches)
            {
                if (!touch.press.isPressed)
                {
                    continue;
                }

                active++;
                deltaSum += touch.delta.ReadValue();
                if (first == null) first = touch;
                else if (second == null) second = touch;
            }

            if (active >= 2 && first != null && second != null)
            {
                multiTouch = true;
                cameraRig.Pan(deltaSum / active);
                Vector2 a = first.position.ReadValue();
                Vector2 b = second.position.ReadValue();
                float now = Vector2.Distance(a, b);
                float before = Vector2.Distance(a - first.delta.ReadValue(), b - second.delta.ReadValue());
                if (now > 1f && before > 1f)
                {
                    cameraRig.Zoom(before / now);
                }

                return;
            }

            var primary = touchscreen.primaryTouch;
            if (primary.press.wasPressedThisFrame)
            {
                touchStart = primary.position.ReadValue();
                touchStartTime = Time.unscaledTime;
                touchMoved = false;
                multiTouch = false;
            }

            if (primary.press.isPressed && Vector2.Distance(primary.position.ReadValue(), touchStart) > TapMaxMove)
            {
                touchMoved = true;
            }

            if (primary.press.wasReleasedThisFrame && !touchMoved && !multiTouch && Time.unscaledTime - touchStartTime < TapMaxSeconds)
            {
                Tap(primary.position.ReadValue());
            }
        }

        private void Tap(Vector2 screenPosition)
        {
            if (IsOverUi(screenPosition))
            {
                return;
            }

            Ray ray = cameraRig.Camera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 200f))
            {
                return;
            }

            Interactable target = hit.collider.GetComponentInParent<Interactable>();
            if (target != null)
            {
                OpenContextMenu(target, screenPosition);
                return;
            }

            RoomInfo room = layout.RoomAt(hit.point);
            if (room != null && !room.Open)
            {
                ShowToast($"{room.Name}: закрыто.");
                return;
            }

            CancelPending();
            if (player.TryMoveTo(hit.point))
            {
                cameraRig.Following = true;
                moveMarker.transform.position = new Vector3(hit.point.x, 0.02f, hit.point.z);
                moveMarker.SetActive(true);
                markerHideAt = Time.unscaledTime + 1.5f;
            }
            else
            {
                ShowToast("Туда не пройти.");
            }
        }

        // ---------- Actions ----------

        private void ChooseAction(Interactable target, string actionId)
        {
            modal = Modal.None;
            CancelPending();
            pendingTarget = target;
            pendingAction = actionId;
            pendingTargetLastPosition = target.ApproachPoint;
            pendingRetries = 0;
            cameraRig.Following = true;

            if (player.FlatDistanceTo(target.ApproachPoint) <= target.InteractRange)
            {
                ArriveAtPending();
                return;
            }

            if (!player.TryMoveTo(target.ApproachPoint))
            {
                CancelPending();
                ShowToast($"Не подойти к «{target.Title}»: путь перекрыт.");
                return;
            }

            ShowToast($"{target.Title}: {ActionCatalog.Title(actionId)}…");
        }

        private void UpdatePending()
        {
            if (pendingTarget == null)
            {
                pendingAction = null;
                return;
            }

            Vector3 approach = pendingTarget.ApproachPoint;
            float distance = player.FlatDistanceTo(approach);
            if (distance <= pendingTarget.InteractRange)
            {
                ArriveAtPending();
                return;
            }

            bool targetMoved = (approach - pendingTargetLastPosition).sqrMagnitude > 0.5f;
            if (targetMoved || !player.IsMoving)
            {
                pendingTargetLastPosition = approach;
                if (!player.IsMoving && distance < 1.2f)
                {
                    ArriveAtPending();
                }
                else if (!targetMoved && ++pendingRetries > 3 || !player.TryMoveTo(approach))
                {
                    ShowToast($"Не подойти к «{pendingTarget.Title}».");
                    CancelPending();
                }
            }
        }

        private void ArriveAtPending()
        {
            Interactable target = pendingTarget;
            string actionId = pendingAction;
            CancelPending();
            player.Stop();
            StartAction(target, actionId);
        }

        private void CancelPending()
        {
            pendingTarget = null;
            pendingAction = null;
        }

        private void StartAction(Interactable target, string actionId)
        {
            ActionCheck check = DayRules.Check(state, actionId);
            if (!check.IsReady)
            {
                ShowToast(check.Reason ?? $"{target.Title}: сейчас недоступно.");
                return;
            }

            switch (ActionCatalog.Kind(actionId))
            {
                case ActionKind.Choice:
                    BeginMinigame(actionId);
                    break;
                case ActionKind.Confirm:
                    confirmAction = actionId;
                    OpenModal(Modal.Confirm);
                    break;
                default:
                    ApplyOutcome(DayRules.Execute(state, actionId));
                    break;
            }
        }

        private void ApplyOutcome(ActionOutcome outcome)
        {
            if (outcome.MaimeskulStarted)
            {
                SpawnMaimeskul();
            }

            if (outcome.MaimeskulEnded)
            {
                DespawnMaimeskul();
            }

            SyncVisitors();

            if (outcome.Autosave)
            {
                SaveNow();
            }

            if (!string.IsNullOrEmpty(outcome.Speaker))
            {
                dialogueSpeaker = outcome.Speaker;
                dialogueSpeakerId = outcome.SpeakerId;
                dialogueText = outcome.Message;
                dialogueEndsDay = outcome.EndsDay;
                OpenModal(Modal.Dialogue);
                return;
            }

            ShowToast(outcome.Message);
            if (outcome.EndsDay)
            {
                EndDay();
            }
        }

        private void CloseDialogue()
        {
            modal = Modal.None;
            if (dialogueEndsDay)
            {
                dialogueEndsDay = false;
                EndDay();
            }
        }

        private void EndDay()
        {
            if (!state.HasFlag(Flags.DayFinished))
            {
                DayRules.FinishDay(state);
            }

            CancelPending();
            player.Stop();
            dayResult = DayResults.Compute(state);
            SaveNow();
            OpenModal(Modal.Summary);
        }

        private void SpawnMaimeskul()
        {
            // A single instance, whatever the save/load history.
            if (maimeskul == null)
            {
                maimeskul = MaimeskulActor.Create(new Vector3(5f, 0f, 1.5f), CameraRig.Rotation);
                maimeskul.gameObject.AddComponent<Interactable>().ConfigurePerson(maimeskul, NpcIds.Maimeskul, "Маймескул", new[] { ActionIds.Talk(NpcIds.Maimeskul) });
            }
        }

        private void DespawnMaimeskul()
        {
            if (maimeskul != null)
            {
                maimeskul.Vanish();
                maimeskul = null;
            }
        }

        // ---------- Session ----------

        private void StartNewGame()
        {
            DayState fresh = DayState.CreateNew((Environment.TickCount & int.MaxValue) | 1);
            SaveSystem.Delete();
            TrySave(() => SaveSystem.SaveDayStart(fresh));
            BeginSession(fresh);
            SaveNow();
            ShowToast("Пятница, 09:00. Руководитель ждёт в кабинете.");
        }

        private void RestartDay()
        {
            DayState snapshot = SaveSystem.LoadDayStart() ?? DayState.CreateNew(state.seed);
            BeginSession(snapshot);
            SaveNow();
            ShowToast("День начат заново.");
        }

        private void BeginSession(DayState loaded)
        {
            state = loaded;
            savedState = null;
            menuNotice = null;
            CancelPending();
            DespawnMaimeskul();
            autosaveTimer = 0f;

            Vector3 spawn = state.hasPlayerPosition ? new Vector3(state.playerX, 0f, state.playerZ) : FloorLayout.PlayerSpawn;
            player.Stop();
            player.Warp(spawn);
            foreach (NpcActor npc in npcs)
            {
                npc.PlaceForState(state);
            }

            if (state.maimeskul.active)
            {
                SpawnMaimeskul();
            }

            ResetVisitors();
            cameraRig.Following = true;
            cameraRig.SnapTo(player.transform.position);
            modal = Modal.None;

            if (state.HasFlag(Flags.DayFinished))
            {
                dayResult = DayResults.Compute(state);
                OpenModal(Modal.Summary);
            }
        }

        private void SaveNow()
        {
            autosaveTimer = 0f;
            if (!InSession)
            {
                return;
            }

            Vector3 feet = player.FeetPosition;
            state.hasPlayerPosition = true;
            state.playerX = feet.x;
            state.playerZ = feet.z;
            TrySave(() => SaveSystem.Save(state));
        }

        private void TrySave(Action save)
        {
            try
            {
                save();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Debug.LogWarning($"UVC save failed: {exception.Message}");
                ShowToast("Не удалось сохранить игру.");
            }
        }

        /// <summary>The preview state shown behind the main menu has seed 0 and is never saved.</summary>
        private bool InSession => state != null && state.seed != 0;

        private void OnApplicationPause(bool paused)
        {
            if (paused && InSession)
            {
                SaveNow();
                if (modal == Modal.None)
                {
                    OpenModal(Modal.Pause);
                }
            }
        }

        private void OnApplicationQuit()
        {
            if (InSession)
            {
                SaveNow();
            }
        }
    }
}
