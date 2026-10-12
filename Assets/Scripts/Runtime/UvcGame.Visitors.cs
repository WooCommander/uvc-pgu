using UnityEngine;
using Uvc.Core;

namespace Uvc.Prototype
{
    /// <summary>World side of the visitors: Bragar with his bag, Prodanov with chess and students.</summary>
    public sealed partial class UvcGame
    {
        private const float DistractionDistance = 2.6f;
        private static readonly Color BragarColor = new(0.5f, 0.5f, 0.52f);
        private static readonly Vector3 BagPosition = new(6.6f, 0.2f, -15.4f);

        private static readonly Vector3[] BragarPassRoute =
        {
            new(-15f, 0f, 1.5f), new(5f, 0f, 1.5f), new(5f, 0f, -12f), new(6.8f, 0f, -14.6f),
            new(5f, 0f, -12f), new(5f, 0f, 1.5f), new(-15f, 0f, 1.5f)
        };

        private static readonly Vector3[] BragarSearchRoute =
        {
            new(9.35f, 0f, -9.5f), new(6.8f, 0f, -14.6f), new(9.35f, 0f, -15.3f), new(7f, 0f, -10f), new(9.35f, 0f, -12f)
        };

        private RouteActor bragarPasser;
        private RouteActor bragarSearcher;
        private GameObject bragarBag;
        private NpcActor prodanov;

        private void CreateProdanovCircle()
        {
            Quaternion rotation = CameraRig.Rotation;
            Vector3 away = new(-15f, 0f, 1.6f);
            prodanov = NpcActor.Create(NpcIds.Prodanov, new Color(0.45f, 0.3f, 0.25f), Vector3.zero, rotation,
                new ScheduleEntry(540, away),
                new ScheduleEntry(DayRules.ProdanovFrom, new Vector3(-2.6f, 0f, 0.8f)),
                new ScheduleEntry(DayRules.ProdanovUntil, away));
            AddNpc(prodanov, ActionIds.Talk(NpcIds.Prodanov), ActionIds.ProdanovChess);

            AddNpc(NpcActor.Create(NpcIds.StudentA, new Color(0.9f, 0.85f, 0.75f), Vector3.zero, rotation,
                    new ScheduleEntry(540, away + Vector3.right * 0.8f),
                    new ScheduleEntry(DayRules.ProdanovFrom, new Vector3(-1.7f, 0f, 1.1f)),
                    new ScheduleEntry(DayRules.ProdanovUntil, away + Vector3.right * 0.8f)),
                ActionIds.Talk(NpcIds.StudentA));
            AddNpc(NpcActor.Create(NpcIds.StudentB, new Color(0.35f, 0.4f, 0.3f), Vector3.zero, rotation,
                    new ScheduleEntry(540, away + Vector3.right * 1.6f),
                    new ScheduleEntry(DayRules.ProdanovFrom, new Vector3(-3.5f, 0f, 1.1f)),
                    new ScheduleEntry(DayRules.ProdanovUntil, away + Vector3.right * 1.6f)),
                ActionIds.Talk(NpcIds.StudentB));
        }

        private void HandleVisitorTick(TickResult tick)
        {
            if (tick.BragarPassed)
            {
                if (bragarPasser == null)
                {
                    bragarPasser = RouteActor.Create(NpcIds.Bragar, BragarColor, CameraRig.Rotation, false, BragarPassRoute);
                }

                ShowToast("Брагарь прошёл в дальнюю аудиторию с двумя сумками… а вернулся с одной.");
            }

            if (tick.BragarSearchStarted)
            {
                ShowToast("Брагарь вернулся и ищет сумку в ПК 7. Пока он там — никаких игр.");
                SaveNow();
            }

            if (tick.BragarLeft && !string.IsNullOrEmpty(state.bragarMessage))
            {
                ShowToast(state.bragarMessage);
                SaveNow();
            }

            if (tick.BragarPassed || tick.BragarSearchStarted || tick.BragarLeft)
            {
                SyncVisitors();
            }
        }

        /// <summary>Makes the floor match the rules state: the bag lies there, Bragar searches, or neither.</summary>
        private void SyncVisitors()
        {
            bool bagOnFloor = DayRules.IsBagOnFloor(state);
            if (bagOnFloor && bragarBag == null)
            {
                bragarBag = Prim.Box(null, "Сумка Брагаря", BagPosition, new Vector3(0.6f, 0.4f, 0.35f), "Furniture");
                bragarBag.AddComponent<Interactable>().ConfigureObject("Сумка Брагаря", BagPosition + new Vector3(0f, 0f, 0.9f),
                    new[] { ActionIds.BagTake }, null);
            }
            else if (!bagOnFloor && bragarBag != null)
            {
                Destroy(bragarBag);
                bragarBag = null;
            }

            bool searching = DayRules.IsBragarSearching(state);
            if (searching && bragarSearcher == null)
            {
                bragarSearcher = RouteActor.Create(NpcIds.Bragar, BragarColor, CameraRig.Rotation, true, BragarSearchRoute);
                bragarSearcher.gameObject.AddComponent<Interactable>().ConfigurePerson(bragarSearcher, NpcIds.Bragar, NpcIds.DisplayName(NpcIds.Bragar),
                    new[] { ActionIds.BragarReturnBag, ActionIds.Talk(NpcIds.Bragar) });
            }
            else if (!searching && bragarSearcher != null)
            {
                bragarSearcher.Leave();
                bragarSearcher = null;
            }
        }

        private void ResetVisitors()
        {
            if (bragarPasser != null)
            {
                bragarPasser.Leave();
                bragarPasser = null;
            }

            SyncVisitors();
        }

        /// <summary>Prodanov calls out to a hero who walks too close to his chess circle.</summary>
        private void UpdateProdanovDistraction()
        {
            if (prodanov == null || player.FlatDistanceTo(prodanov.FeetPosition) > DistractionDistance)
            {
                return;
            }

            if (pendingTarget != null && pendingTarget.Person == prodanov)
            {
                return;
            }

            if (DayRules.TryProdanovDistraction(state))
            {
                // The errand is only interrupted: the hero resumes it after the conversation.
                player.Stop();
                OpenModal(Modal.Distraction);
            }
        }

        private void DrawDistraction(Rect area)
        {
            float width = Mathf.Min(720f, area.width - 20f);
            Rect panel = Panel(new Rect(area.center.x - width * 0.5f, area.yMax - 220f, width, 210f));
            Texture2D portrait = Art.Portrait(NpcIds.Prodanov);
            float textX = panel.x + 18f;
            if (portrait != null)
            {
                GUI.DrawTexture(new Rect(panel.x + 12f, panel.y + 12f, 140f, 140f), portrait, ScaleMode.ScaleAndCrop);
                textX = panel.x + 168f;
            }

            GUI.Label(new Rect(textX, panel.y + 12f, panel.xMax - textX - 18f, 28f), NpcIds.DisplayName(NpcIds.Prodanov), titleStyle);
            GUI.Label(new Rect(textX, panel.y + 46f, panel.xMax - textX - 18f, 90f), DayRules.ProdanovInvitation, bodyStyle);

            float buttonY = panel.yMax - ButtonHeight - 10f;
            if (GUI.Button(new Rect(textX, buttonY, 240f, ButtonHeight), "Сыграть партию (30 мин)", buttonStyle))
            {
                modal = Modal.None;
                StartAction(prodanov.GetComponent<Interactable>(), ActionIds.ProdanovChess);
            }
            else if (GUI.Button(new Rect(panel.xMax - 228f, buttonY, 216f, ButtonHeight), "Некогда (−3 мин)", buttonStyle))
            {
                DeclineProdanov();
            }
        }

        private void DeclineProdanov()
        {
            modal = Modal.None;
            ApplyOutcome(DayRules.Execute(state, ActionIds.ProdanovDecline));
        }
    }
}
