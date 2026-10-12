using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using Uvc.Core;

namespace Uvc.Prototype
{
    public sealed class RoomInfo
    {
        public RoomInfo(string id, string name, float xMin, float zMin, float xMax, float zMax, bool open, bool corridor = false, Vector3? door = null)
        {
            Id = id;
            Name = name;
            Bounds = Rect.MinMaxRect(xMin, zMin, xMax, zMax);
            Open = open;
            Corridor = corridor;
            Door = door;
        }

        public string Id { get; }
        public string Name { get; }
        public Rect Bounds { get; }
        public bool Open { get; }
        public bool Corridor { get; }
        public Vector3? Door { get; }
        public Vector3 Center => new(Bounds.center.x, 0f, Bounds.center.y);

        public bool Contains(Vector3 point) => Bounds.Contains(new Vector2(point.x, point.z));
    }

    /// <summary>
    /// Grey-box second floor of building B, following images/map.png (no exact scale).
    /// Street side is +Z, the yard is −Z. Units are metres.
    /// </summary>
    public sealed class FloorLayout
    {
        public const float WallHeight = 1.2f;
        private const float WallThickness = 0.15f;
        private const float DoorWidth = 1.6f;

        public static readonly Vector3 PlayerSpawn = new(-10f, 0f, 1.5f);

        public readonly List<RoomInfo> Rooms = new()
        {
            new RoomInfo("corridor", "Коридор", -16f, 0f, 8f, 3f, true, true),
            new RoomInfo("corridor_uvc", "Коридор УВЦ", 4f, -16f, 6f, 0f, true, true),
            new RoomInfo("k1", "К1 · учебная аудитория", -16f, 3f, -12f, 9f, true, door: new Vector3(-14f, 0f, 3f)),
            new RoomInfo("k2", "К2", -12f, 3f, -8f, 9f, false),
            new RoomInfo("k3", "К3", -8f, 3f, -4f, 9f, false),
            new RoomInfo("dept", "Кафедры", -4f, 3f, 0f, 9f, false),
            new RoomInfo("pc", "ПК · рабочая комната", 0f, 3f, 4f, 9f, true, door: new Vector3(2f, 0f, 3f)),
            new RoomInfo("director", "Кабинет руководителя", 4f, 3f, 8f, 9f, true, door: new Vector3(6f, 0f, 3f)),
            new RoomInfo("toilet", "Туалет", 8f, 3f, 12f, 9f, false),
            new RoomInfo("stairs_end", "Торцевая лестница", 8f, 0f, 12f, 3f, false),
            new RoomInfo("stairs_main", "Парадная лестница", -16f, -5f, -8f, 0f, false),
            new RoomInfo("rest", "ПК 4 · комната отдыха", -1f, -8f, 4f, 0f, true, door: new Vector3(4f, 0f, -4f)),
            new RoomInfo("pc6", "ПК 6", 6f, -8f, 11f, 0f, false),
            new RoomInfo("pc5", "ПК 5", -1f, -16f, 4f, -8f, false),
            new RoomInfo("game", "ПК 7 · дальняя аудитория", 6f, -16f, 11f, -8f, true, door: new Vector3(6f, 0f, -12f)),
            new RoomInfo("store", "Каморка · склад", 3f, -20f, 7f, -16f, true, door: new Vector3(5f, 0f, -16f))
        };

        public static readonly Rect CameraBounds = Rect.MinMaxRect(-17f, -21f, 13f, 10f);

        private static readonly Dictionary<string, string> PropIcons = new()
        {
            ["Принтер"] = "printer",
            ["Игровое место"] = "desk",
            ["Стол"] = "party_table",
            ["Чайник"] = "tea_cabinet",
            ["Стеллаж"] = "shelf"
        };

        public readonly List<Interactable> Interactables = new();
        private readonly Transform root;
        private readonly Quaternion labelRotation;

        public FloorLayout(Transform root, Quaternion labelRotation)
        {
            this.root = root;
            this.labelRotation = labelRotation;
        }

        public RoomInfo RoomAt(Vector3 point)
        {
            // Rooms win over corridors on shared edges.
            RoomInfo corridor = null;
            foreach (RoomInfo room in Rooms)
            {
                if (!room.Contains(point))
                {
                    continue;
                }

                if (!room.Corridor)
                {
                    return room;
                }

                corridor ??= room;
            }

            return corridor;
        }

        public void Build()
        {
            foreach (RoomInfo room in Rooms)
            {
                Rect b = room.Bounds;
                string floorMaterial = room.Corridor ? "Corridor" : room.Open ? "Floor" : "Closed";
                Prim.Box(root, room.Id + " floor", new Vector3(b.center.x, -0.1f, b.center.y), new Vector3(b.width, 0.2f, b.height), floorMaterial);
                if (room.Corridor)
                {
                    continue;
                }

                BuildWalls(room);
                float labelZ = room.Open ? b.yMax - 0.6f : b.center.y;
                Prim.Label(room.Open ? room.Name : room.Name + "\n(закрыто)", new Vector3(b.center.x, 0.05f, labelZ), labelRotation, room.Open ? 0.09f : 0.08f,
                    room.Open ? new Color(1f, 0.9f, 0.6f) : new Color(0.6f, 0.6f, 0.6f));
            }

            // Corridor walls not covered by rooms.
            Wall(new Vector3(-16f, 0f, 0f), new Vector3(-16f, 0f, 3f));
            Wall(new Vector3(-8f, 0f, 0f), new Vector3(-1f, 0f, 0f));
            Prim.Label("ОКНА ↓ ДВОР", new Vector3(-4.5f, 0.05f, 0.5f), labelRotation, 0.06f, new Color(0.55f, 0.65f, 0.75f));

            BuildFurniture();

            NavMeshSurface surface = root.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
        }

        private void BuildFurniture()
        {
            // К1 — classroom.
            Prim.Box(root, "Доска", new Vector3(-14f, 1f, 8.8f), new Vector3(2.4f, 1.1f, 0.1f), "Board");
            Prim.Box(root, "Парта", new Vector3(-15f, 0.45f, 7.6f), new Vector3(1.2f, 0.9f, 0.6f), "Furniture");
            Prim.Box(root, "Парта", new Vector3(-13f, 0.45f, 7.6f), new Vector3(1.2f, 0.9f, 0.6f), "Furniture");
            AddObject("Проектор", new Vector3(-14f, 0.5f, 6f), new Vector3(1f, 1f, 0.6f), new Vector3(-14f, 0f, 4.8f), "Device",
                ActionIds.ProjectorSetup);

            // ПК — workroom.
            AddObject("Принтер", new Vector3(1f, 0.5f, 6.5f), new Vector3(0.9f, 1f, 0.7f), new Vector3(1f, 0f, 5.3f), "Device",
                ActionIds.PrinterInspect, ActionIds.PrinterRepair);
            Prim.Box(root, "Стол с компьютером", new Vector3(3f, 0.45f, 8.2f), new Vector3(1.6f, 0.9f, 0.8f), "Furniture");
            Prim.Box(root, "Монитор", new Vector3(3f, 1.15f, 8.3f), new Vector3(0.7f, 0.5f, 0.08f), "Device");

            // Кабинет руководителя.
            Prim.Box(root, "Стол руководителя", new Vector3(6.5f, 0.45f, 7f), new Vector3(1.6f, 0.9f, 0.8f), "Furniture");
            AddObject("Шкаф с папками", new Vector3(4.45f, 0.9f, 8.3f), new Vector3(0.6f, 1.8f, 1f), new Vector3(4.75f, 0f, 7f), "Furniture",
                ActionIds.CabinetJournal, ActionIds.CabinetReturnSheet);

            // ПК 4 — rest room.
            AddObject("Чайник", new Vector3(-0.6f, 0.45f, -1.5f), new Vector3(0.6f, 0.9f, 1.2f), new Vector3(0.6f, 0f, -1.5f), "Device",
                ActionIds.KettleTea);
            AddObject("Холодильник", new Vector3(-0.6f, 0.9f, -3.5f), new Vector3(0.7f, 1.8f, 0.7f), new Vector3(0.6f, 0f, -3.5f), "Light",
                ActionIds.FridgeBeer, ActionIds.FridgeLemonade);
            AddObject("Полка с кружками", new Vector3(-0.65f, 0.6f, -5.6f), new Vector3(0.5f, 1.2f, 1.2f), new Vector3(0.6f, 0f, -5.6f), "Furniture",
                ActionIds.MugsRitual);
            AddObject("Стол", new Vector3(2f, 0.45f, -5.8f), new Vector3(1.8f, 0.9f, 1.1f), new Vector3(2f, 0f, -4.5f), "Furniture",
                ActionIds.TableParty);

            // ПК 7 — far game room, six machines that never switch off.
            for (int row = 0; row < 2; row++)
            {
                for (int seat = 0; seat < 3; seat++)
                {
                    float x = row == 0 ? 8.2f : 10.5f;
                    float z = -10f - seat * 2f;
                    AddObject("Игровое место", new Vector3(x, 0.45f, z), new Vector3(0.6f, 0.9f, 1.1f), new Vector3(x - 1.1f, 0f, z), "Device",
                        ActionIds.TerminalPlay);
                }
            }

            // Каморка — storeroom.
            AddObject("Стеллаж", new Vector3(5f, 0.9f, -19.6f), new Vector3(3f, 1.8f, 0.5f), new Vector3(5f, 0f, -18.5f), "Furniture",
                ActionIds.ShelfScrewdriver, ActionIds.ShelfOpener, ActionIds.ShelfArchive);

            // Corridor.
            AddObject("Лавка", new Vector3(-6f, 0.25f, 0.35f), new Vector3(1.8f, 0.5f, 0.45f), new Vector3(-6f, 0f, 1.4f), "Furniture",
                ActionIds.BenchPass);
            Prim.Box(root, "Доска объявлений", new Vector3(-2f, 1f, 2.9f), new Vector3(1.4f, 0.9f, 0.08f), "Board");
        }

        private void AddObject(string title, Vector3 position, Vector3 size, Vector3 approach, string material, params string[] actions)
        {
            GameObject item = Prim.Box(root, title, position, size, material);
            Interactable interactable = item.AddComponent<Interactable>();
            interactable.ConfigureObject(title, approach, actions, PropIcons.TryGetValue(title, out string icon) ? icon : null);
            Interactables.Add(interactable);
            Prim.Label(title, position + Vector3.up * (size.y * 0.5f + 0.35f), labelRotation, 0.07f, Color.white);
        }

        private void BuildWalls(RoomInfo room)
        {
            Rect b = room.Bounds;
            Vector3[] corners =
            {
                new(b.xMin, 0f, b.yMin),
                new(b.xMax, 0f, b.yMin),
                new(b.xMax, 0f, b.yMax),
                new(b.xMin, 0f, b.yMax)
            };

            for (int index = 0; index < 4; index++)
            {
                Vector3 from = corners[index];
                Vector3 to = corners[(index + 1) % 4];
                if (room.Door is { } door && IsOnSegment(door, from, to))
                {
                    Vector3 direction = (to - from).normalized;
                    Wall(from, door - direction * (DoorWidth * 0.5f));
                    Wall(door + direction * (DoorWidth * 0.5f), to);
                }
                else
                {
                    Wall(from, to);
                }
            }
        }

        private void Wall(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (length < 0.05f)
            {
                return;
            }

            bool alongX = Mathf.Abs(delta.x) > Mathf.Abs(delta.z);
            Vector3 size = alongX
                ? new Vector3(length + WallThickness, WallHeight, WallThickness)
                : new Vector3(WallThickness, WallHeight, length + WallThickness);
            Prim.Box(root, "Wall", (from + to) * 0.5f + Vector3.up * (WallHeight * 0.5f), size, "Wall");
        }

        private static bool IsOnSegment(Vector3 point, Vector3 from, Vector3 to)
        {
            Vector3 segment = to - from;
            float t = Vector3.Dot(point - from, segment) / segment.sqrMagnitude;
            return t > 0f && t < 1f && Vector3.Distance(from + segment * t, point) < 0.01f;
        }
    }

    /// <summary>Primitive and label factory with shared materials.</summary>
    public static class Prim
    {
        private static readonly Dictionary<string, Material> Materials = new();
        private static Font font;

        private static readonly Dictionary<string, Color> Palette = new()
        {
            ["Floor"] = new Color(0.36f, 0.38f, 0.40f),
            ["Corridor"] = new Color(0.45f, 0.42f, 0.36f),
            ["Closed"] = new Color(0.16f, 0.17f, 0.19f),
            ["Wall"] = new Color(0.72f, 0.74f, 0.70f),
            ["Furniture"] = new Color(0.45f, 0.29f, 0.18f),
            ["Device"] = new Color(0.22f, 0.36f, 0.48f),
            ["Light"] = new Color(0.86f, 0.89f, 0.84f),
            ["Board"] = new Color(0.12f, 0.30f, 0.20f),
            ["Player"] = new Color(0.95f, 0.58f, 0.20f),
            ["Npc"] = new Color(0.33f, 0.72f, 0.58f),
            ["Special"] = new Color(0.62f, 0.28f, 0.78f),
            ["Marker"] = new Color(1f, 0.85f, 0.3f),
            ["Dust"] = new Color(0.75f, 0.72f, 0.66f)
        };

        public static Material Material(string name, Color? overrideColor = null)
        {
            string key = overrideColor.HasValue ? name + ColorUtility.ToHtmlStringRGB(overrideColor.Value) : name;
            if (Materials.TryGetValue(key, out Material material))
            {
                return material;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader) { color = overrideColor ?? (Palette.TryGetValue(name, out Color color) ? color : Color.magenta) };
            Materials[key] = material;
            return material;
        }

        public static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size, string material)
        {
            return Shape(parent, name, PrimitiveType.Cube, position, size, Material(material));
        }

        public static GameObject Shape(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 size, Material material)
        {
            GameObject gameObject = GameObject.CreatePrimitive(type);
            gameObject.name = name;
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.SetPositionAndRotation(position, Quaternion.identity);
            gameObject.transform.localScale = size;
            gameObject.GetComponent<Renderer>().sharedMaterial = material;
            return gameObject;
        }

        public static TextMesh Label(string text, Vector3 position, Quaternion rotation, float size, Color color, Transform parent = null)
        {
            font ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GameObject label = new("Label " + text);
            label.transform.SetParent(parent, false);
            label.transform.SetPositionAndRotation(position, rotation);
            TextMesh textMesh = label.AddComponent<TextMesh>();
            textMesh.font = font;
            textMesh.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            textMesh.text = text;
            textMesh.characterSize = size;
            textMesh.fontSize = 48;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.color = color;
            return textMesh;
        }
    }
}
