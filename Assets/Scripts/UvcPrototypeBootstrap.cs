using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Uvc.Prototype
{
    public static class UvcPrototypeBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreatePrototype()
        {
            if (Object.FindFirstObjectByType<PrototypeController>() != null)
            {
                return;
            }

            new GameObject("UVC Prototype").AddComponent<PrototypeController>();
        }
    }

    public sealed class PrototypeController : MonoBehaviour
    {
        private static readonly Dictionary<string, Material> Materials = new();
        private readonly List<PrototypeInteractable> interactables = new();
        private readonly HashSet<string> inventory = new();
        private PrototypePlayer player;
        private Camera mainCamera;
        private PrototypeInteractable pendingInteraction;
        private string hint = "Нажмите на пол, чтобы идти. Нажмите на объект, чтобы взаимодействовать.";
        private string objective = "Посмотреть расписание в приёмной.";
        private float gameMinutes = 9 * 60;
        private int energy = 100;
        private int mood = 75;
        private int questStep;
        private bool briefingEventShown;
        private bool inspectionEventShown;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            BuildWorld();
        }

        private void Update()
        {
            gameMinutes = Mathf.Min(gameMinutes + Time.deltaTime * 0.5f, 20 * 60);
            HandlePointer();
            ResolveArrival();
            TriggerScheduledEvents();
        }

        private void OnGUI()
        {
            GUI.Box(new Rect(18, 18, 420, 150), "УВЦ: дожить до пятницы");
            GUI.Label(new Rect(34, 52, 300, 24), $"Пятница · {Mathf.FloorToInt(gameMinutes / 60):00}:{Mathf.FloorToInt(gameMinutes % 60):00}");
            GUI.Label(new Rect(34, 78, 360, 24), $"Энергия: {energy}   Настроение: {mood}");
            GUI.Label(new Rect(34, 104, 370, 36), $"Задача: {objective}");
            if (inventory.Count > 0)
            {
                GUI.Label(new Rect(34, 140, 370, 22), $"Инвентарь: {string.Join(", ", inventory)}");
            }
            GUI.Box(new Rect(18, Screen.height - 76, Mathf.Min(620, Screen.width - 36), 58), hint);
        }

        private void HandlePointer()
        {
            if (!WasPointerPressed(out Vector2 pointerPosition))
            {
                return;
            }

            Ray ray = mainCamera.ScreenPointToRay(pointerPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 200f))
            {
                return;
            }

            PrototypeInteractable interactable = hit.collider.GetComponentInParent<PrototypeInteractable>();
            if (interactable != null)
            {
                pendingInteraction = interactable;
                player.MoveTo(interactable.ApproachPoint);
                hint = $"{interactable.Title}: {interactable.Action}. Герой идёт к объекту.";
                return;
            }

            pendingInteraction = null;
            player.MoveTo(hit.point);
            hint = "Идём к выбранной точке.";
        }

        private void ResolveArrival()
        {
            if (pendingInteraction == null || !player.HasReached(pendingInteraction.ApproachPoint))
            {
                return;
            }

            Interact(pendingInteraction);
            pendingInteraction = null;
        }

        private void Interact(PrototypeInteractable interactable)
        {
            switch (interactable.ActionId)
            {
                case PrototypeAction.Schedule:
                    questStep = Mathf.Max(questStep, 1);
                    objective = "Забрать набор инструментов у серверной стойки.";
                    hint = "В 10:00 сломался принтер. Сначала нужен набор инструментов.";
                    break;
                case PrototypeAction.ServerRack:
                    if (questStep < 1)
                    {
                        hint = "Сначала уточните задачу в расписании.";
                        break;
                    }

                    inventory.Add("Набор инструментов");
                    questStep = Mathf.Max(questStep, 2);
                    objective = "Починить принтер в мастерской.";
                    hint = "Набор инструментов получен.";
                    break;
                case PrototypeAction.Printer:
                    if (!inventory.Contains("Набор инструментов"))
                    {
                        hint = "Для ремонта нужен набор инструментов из серверной.";
                        break;
                    }

                    questStep = Mathf.Max(questStep, 3);
                    energy = Mathf.Max(0, energy - 12);
                    objective = "Проверить проектор в аудитории.";
                    hint = "Принтер снова печатает. Проектор ждёт проверки.";
                    break;
                case PrototypeAction.Projector:
                    if (questStep < 3)
                    {
                        hint = "Сначала завершите ремонт принтера.";
                        break;
                    }

                    questStep = 4;
                    energy = Mathf.Max(0, energy - 8);
                    mood = Mathf.Min(100, mood + 8);
                    objective = "Основные поручения выполнены. Можно отдохнуть или зайти в игровую.";
                    hint = "Проектор работает. Основная цепочка поручений завершена!";
                    break;
                case PrototypeAction.CoffeeMachine:
                    energy = Mathf.Min(100, energy + 18);
                    mood = Mathf.Min(100, mood + 5);
                    hint = "Кофе готов: энергия и настроение восстановлены.";
                    break;
                case PrototypeAction.GameTerminal:
                    mood = Mathf.Min(100, mood + 12);
                    energy = Mathf.Max(0, energy - 5);
                    hint = "Короткий матч сыгран. Настроение поднялось.";
                    break;
            }
        }

        private void TriggerScheduledEvents()
        {
            if (!briefingEventShown && gameMinutes >= 9 * 60 + 5)
            {
                briefingEventShown = true;
                hint = "Серёга: «После принтера загляни в аудиторию — проектор опять капризничает»";
            }

            if (!inspectionEventShown && gameMinutes >= 9 * 60 + 15)
            {
                inspectionEventShown = true;
                mood = Mathf.Max(0, mood - 4);
                hint = "Событие: завхоз начал обход. Основная задача остаётся доступной.";
            }
        }

        private static bool WasPointerPressed(out Vector2 pointerPosition)
        {
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            {
                pointerPosition = Touchscreen.current.primaryTouch.position.ReadValue();
                return true;
            }

            if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
            {
                pointerPosition = Pointer.current.position.ReadValue();
                return true;
            }

            pointerPosition = default;
            return false;
        }

        private void BuildWorld()
        {
            mainCamera = Camera.main;
            mainCamera.transform.SetPositionAndRotation(new Vector3(16f, 20f, -18f), Quaternion.Euler(52f, -38f, 0f));
            mainCamera.orthographic = true;
            mainCamera.orthographicSize = 12f;
            mainCamera.backgroundColor = new Color(0.08f, 0.12f, 0.17f);

            CreateMaterial("Floor", new Color(0.30f, 0.33f, 0.36f));
            CreateMaterial("Wall", new Color(0.68f, 0.70f, 0.66f));
            CreateMaterial("Room", new Color(0.22f, 0.34f, 0.42f));
            CreateMaterial("Accent", new Color(0.93f, 0.56f, 0.20f));
            CreateMaterial("Npc", new Color(0.33f, 0.72f, 0.58f));

            CreatePrimitive("Floor", PrimitiveType.Cube, new Vector3(0f, -0.3f, 0f), new Vector3(24f, 0.5f, 14f), "Floor");
            CreatePrimitive("North wall", PrimitiveType.Cube, new Vector3(0f, 1.4f, 6.8f), new Vector3(24f, 3f, 0.3f), "Wall");
            CreatePrimitive("South wall", PrimitiveType.Cube, new Vector3(0f, 1.4f, -6.8f), new Vector3(24f, 3f, 0.3f), "Wall");

            string[] roomNames = { "Приёмная", "Мастерская", "Аудитория", "Серверная", "Кухня", "Игровая" };
            string[] objectNames = { "Расписание", "Принтер", "Проектор", "Стойка", "Кофемашина", "Терминал" };
            string[] actions = { "Посмотреть задачи", "Починить", "Проверить кабель", "Диагностировать", "Сделать кофе", "Запустить матч" };
            PrototypeAction[] actionIds = { PrototypeAction.Schedule, PrototypeAction.Printer, PrototypeAction.Projector, PrototypeAction.ServerRack, PrototypeAction.CoffeeMachine, PrototypeAction.GameTerminal };

            for (int index = 0; index < roomNames.Length; index++)
            {
                float x = -9.5f + index * 3.8f;
                CreateRoom(x, roomNames[index]);
                AddInteractable(objectNames[index], actions[index], actionIds[index], new Vector3(x, 0.75f, 2.3f));
            }

            GameObject playerObject = CreatePrimitive("Техник", PrimitiveType.Capsule, new Vector3(-9.5f, 0.9f, -1.2f), new Vector3(0.8f, 1.8f, 0.8f), "Accent");
            player = playerObject.AddComponent<PrototypePlayer>();
            CreateNpc("Андрюха", new Vector3(-7f, 0.9f, -1.8f), new Vector3(-7f, 0.9f, 0.7f), new Vector3(-3.5f, 0.9f, 0.7f));
            CreateNpc("Серёга", new Vector3(-1.5f, 0.9f, -1.8f), new Vector3(-1.5f, 0.9f, 0.7f), new Vector3(2f, 0.9f, 0.7f));
            CreateNpc("Джон", new Vector3(4f, 0.9f, -1.8f), new Vector3(4f, 0.9f, 0.7f), new Vector3(7.5f, 0.9f, 0.7f));
            CreateNpc("Vigo", new Vector3(8.5f, 0.9f, -1.8f), new Vector3(8.5f, 0.9f, 0.7f), new Vector3(5f, 0.9f, 0.7f));
        }

        private void CreateNpc(string npcName, params Vector3[] route)
        {
            GameObject npcObject = CreatePrimitive(npcName, PrimitiveType.Capsule, route[0], new Vector3(0.65f, 1.5f, 0.65f), "Npc");
            npcObject.AddComponent<PrototypeNpc>().Configure(route);
            CreateLabel(npcName, route[0] + new Vector3(0f, 1.15f, 0f));
        }

        private void CreateRoom(float x, string roomName)
        {
            CreatePrimitive(roomName + " back", PrimitiveType.Cube, new Vector3(x, 1.2f, 4.9f), new Vector3(3.5f, 2.5f, 0.25f), "Wall");
            CreatePrimitive(roomName + " left", PrimitiveType.Cube, new Vector3(x - 1.65f, 1.2f, 3.2f), new Vector3(0.25f, 2.5f, 3.5f), "Wall");
            CreatePrimitive(roomName + " right", PrimitiveType.Cube, new Vector3(x + 1.65f, 1.2f, 3.2f), new Vector3(0.25f, 2.5f, 3.5f), "Wall");
            CreateLabel(roomName, new Vector3(x, 2.7f, 4.65f));
        }

        private void AddInteractable(string title, string action, PrototypeAction actionId, Vector3 position)
        {
            GameObject item = CreatePrimitive(title, PrimitiveType.Cube, position, new Vector3(1f, 1.5f, 0.8f), "Room");
            PrototypeInteractable interactable = item.AddComponent<PrototypeInteractable>();
            interactable.Configure(title, action, actionId, position + new Vector3(0f, 0f, -1.15f));
            interactables.Add(interactable);
            CreateLabel(title, position + new Vector3(0f, 1.1f, 0f));
        }

        private static GameObject CreatePrimitive(string objectName, PrimitiveType type, Vector3 position, Vector3 scale, string materialName)
        {
            GameObject gameObject = GameObject.CreatePrimitive(type);
            gameObject.name = objectName;
            gameObject.transform.SetPositionAndRotation(position, Quaternion.identity);
            gameObject.transform.localScale = scale;
            gameObject.GetComponent<Renderer>().material = Materials[materialName];
            return gameObject;
        }

        private static void CreateMaterial(string materialName, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Materials[materialName] = new Material(shader) { color = color };
        }

        private static void CreateLabel(string text, Vector3 position)
        {
            GameObject label = new(text);
            label.transform.position = position;
            TextMesh textMesh = label.AddComponent<TextMesh>();
            textMesh.text = text;
            textMesh.characterSize = 0.22f;
            textMesh.fontSize = 42;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.color = Color.white;
            label.transform.rotation = Quaternion.Euler(65f, 0f, 0f);
        }
    }

    public sealed class PrototypePlayer : MonoBehaviour
    {
        private Vector3 destination;

        private void Start() => destination = transform.position;

        private void Update()
        {
            transform.position = Vector3.MoveTowards(transform.position, destination, 4.5f * Time.deltaTime);
        }

        public void MoveTo(Vector3 point)
        {
            destination = new Vector3(point.x, transform.position.y, point.z);
        }

        public bool HasReached(Vector3 point)
        {
            Vector3 horizontalOffset = transform.position - point;
            horizontalOffset.y = 0f;
            return horizontalOffset.sqrMagnitude < 0.06f;
        }
    }

    public enum PrototypeAction
    {
        Schedule,
        Printer,
        Projector,
        ServerRack,
        CoffeeMachine,
        GameTerminal
    }

    public sealed class PrototypeNpc : MonoBehaviour
    {
        private Vector3[] route;
        private int destinationIndex;

        public void Configure(Vector3[] patrolRoute)
        {
            route = patrolRoute;
        }

        private void Update()
        {
            if (route == null || route.Length == 0)
            {
                return;
            }

            Vector3 destination = route[destinationIndex];
            transform.position = Vector3.MoveTowards(transform.position, destination, 1.4f * Time.deltaTime);
            if ((transform.position - destination).sqrMagnitude < 0.02f)
            {
                destinationIndex = (destinationIndex + 1) % route.Length;
            }
        }
    }

    public sealed class PrototypeInteractable : MonoBehaviour
    {
        public string Title { get; private set; }
        public string Action { get; private set; }
        public PrototypeAction ActionId { get; private set; }
        public Vector3 ApproachPoint { get; private set; }

        public void Configure(string title, string action, PrototypeAction actionId, Vector3 approachPoint)
        {
            Title = title;
            Action = action;
            ActionId = actionId;
            ApproachPoint = approachPoint;
        }
    }
}
