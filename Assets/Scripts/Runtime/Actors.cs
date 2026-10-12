using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Uvc.Core;

namespace Uvc.Prototype
{
    /// <summary>Shared body + NavMeshAgent setup for every walking character.</summary>
    public abstract class Walker : MonoBehaviour
    {
        protected NavMeshAgent Agent { get; private set; }

        protected void Setup(string displayName, Color color, float height, float speed, Quaternion labelRotation)
        {
            GameObject body = Prim.Shape(transform, "Body", PrimitiveType.Capsule, Vector3.zero, new Vector3(0.75f, height * 0.5f, 0.75f),
                Prim.Material("Actor", color));
            body.transform.localPosition = Vector3.zero;
            Prim.Label(displayName, transform.position + Vector3.up * (height * 0.5f + 0.35f), labelRotation, 0.06f, Color.white, transform);

            Agent = gameObject.AddComponent<NavMeshAgent>();
            Agent.baseOffset = height * 0.5f;
            Agent.height = height;
            Agent.radius = 0.35f;
            Agent.speed = speed;
            Agent.acceleration = 24f;
            Agent.angularSpeed = 0f;
            Agent.updateRotation = false;
            Agent.stoppingDistance = 0.05f;
            Agent.autoBraking = true;
        }

        /// <summary>Measured to the path end directly: remainingDistance can lag a frame after SetPath.</summary>
        public bool IsMoving => Agent.pathPending || Agent.hasPath && FlatDistanceTo(Agent.destination) > 0.15f;

        public void Warp(Vector3 position)
        {
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                Agent.Warp(hit.position);
            }
        }

        /// <summary>Moves only when a full path exists; otherwise stays put (no teleport).</summary>
        public bool TryMoveTo(Vector3 point)
        {
            if (!NavMesh.SamplePosition(point, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
            {
                return false;
            }

            NavMeshPath path = new();
            if (!Agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete)
            {
                return false;
            }

            Agent.SetPath(path);
            return true;
        }

        public void Stop()
        {
            if (Agent.isOnNavMesh)
            {
                Agent.ResetPath();
            }
        }

        public float FlatDistanceTo(Vector3 point)
        {
            Vector3 offset = transform.position - point;
            offset.y = 0f;
            return offset.magnitude;
        }

        public Vector3 FeetPosition => transform.position - Vector3.up * Agent.baseOffset;
    }

    public sealed class PlayerActor : Walker
    {
        public static PlayerActor Create(Vector3 position, Quaternion labelRotation)
        {
            PlayerActor player = new GameObject("Техник").AddComponent<PlayerActor>();
            player.transform.position = position;
            player.Setup("Вы", Prim.Material("Player").color, 1.8f, 4.5f, labelRotation);
            player.Agent.avoidancePriority = 10;
            player.Warp(position);
            return player;
        }
    }

    public readonly struct ScheduleEntry
    {
        public ScheduleEntry(float fromMinute, Vector3 point)
        {
            FromMinute = fromMinute;
            Point = point;
        }

        public float FromMinute { get; }
        public Vector3 Point { get; }
    }

    /// <summary>NPC following a time schedule; returns to it after talks and events.</summary>
    public sealed class NpcActor : Walker
    {
        private readonly List<ScheduleEntry> schedule = new();
        private Vector3 eveningSeat;
        private Vector3 currentTarget = Vector3.positiveInfinity;
        private float fleeUntil;
        private float stuckSeconds;

        public string NpcId { get; private set; }

        public static NpcActor Create(string npcId, Color color, Vector3 eveningSeat, Quaternion labelRotation, params ScheduleEntry[] entries)
        {
            NpcActor npc = new GameObject(NpcIds.DisplayName(npcId)).AddComponent<NpcActor>();
            npc.NpcId = npcId;
            npc.eveningSeat = eveningSeat;
            npc.schedule.AddRange(entries);
            npc.transform.position = entries[0].Point;
            npc.Setup(NpcIds.DisplayName(npcId), color, 1.7f, 1.8f, labelRotation);
            npc.Agent.avoidancePriority = 50;
            npc.Warp(entries[0].Point);
            return npc;
        }

        public void PlaceForState(DayState state)
        {
            Warp(ScheduledPoint(state));
            currentTarget = Vector3.positiveInfinity;
        }

        /// <summary>Discrete schedule update — called a few times per second, not every frame.</summary>
        public void UpdateSchedule(DayState state, float interval)
        {
            if (Time.time < fleeUntil)
            {
                return;
            }

            Vector3 target = ScheduledPoint(state);
            if ((target - currentTarget).sqrMagnitude > 0.01f)
            {
                currentTarget = target;
                stuckSeconds = 0f;
                TryMoveTo(target);
                return;
            }

            // Stuck in a doorway behind someone: re-path to a free neighbouring point.
            if (IsMoving && Agent.velocity.sqrMagnitude < 0.01f)
            {
                stuckSeconds += interval;
                if (stuckSeconds > 3f)
                {
                    stuckSeconds = 0f;
                    Vector2 jitter = Random.insideUnitCircle * 0.8f;
                    TryMoveTo(target + new Vector3(jitter.x, 0f, jitter.y));
                }
            }
            else
            {
                stuckSeconds = 0f;
            }
        }

        public void Flee(Vector3 point, float seconds)
        {
            fleeUntil = Time.time + seconds;
            currentTarget = Vector3.positiveInfinity;
            Agent.speed = 3.5f;
            TryMoveTo(point);
            Invoke(nameof(RestoreSpeed), seconds);
        }

        private void RestoreSpeed() => Agent.speed = 1.8f;

        private Vector3 ScheduledPoint(DayState state)
        {
            bool partyGuest = NpcId == NpcIds.Serega || state.HasFlag(Flags.Invited(NpcId));
            if (state.clock.IsEvening && partyGuest && state.IsQuestAccepted(QuestIds.Party))
            {
                return eveningSeat;
            }

            Vector3 point = schedule[0].Point;
            foreach (ScheduleEntry entry in schedule)
            {
                if (state.clock.minutes >= entry.FromMinute)
                {
                    point = entry.Point;
                }
            }

            return point;
        }
    }

    /// <summary>A visitor that walks a fixed route: once (then leaves the floor) or in a loop.</summary>
    public sealed class RouteActor : Walker
    {
        private Vector3[] route;
        private bool loop;
        private int nextPoint;
        private float idleSeconds;

        public static RouteActor Create(string npcId, Color color, Quaternion labelRotation, bool loop, params Vector3[] route)
        {
            RouteActor actor = new GameObject(NpcIds.DisplayName(npcId)).AddComponent<RouteActor>();
            actor.route = route;
            actor.loop = loop;
            actor.transform.position = route[0];
            actor.Setup(NpcIds.DisplayName(npcId), color, 1.75f, 1.6f, labelRotation);
            actor.Agent.avoidancePriority = 40;
            actor.Warp(route[0]);
            actor.nextPoint = 1 % route.Length;
            return actor;
        }

        private void Update()
        {
            if (IsMoving)
            {
                idleSeconds = 0f;
                return;
            }

            // Short pause at each point: looking around for the bag.
            idleSeconds += Time.deltaTime;
            if (idleSeconds < (loop ? 2.5f : 0.2f))
            {
                return;
            }

            idleSeconds = 0f;
            if (nextPoint >= route.Length)
            {
                if (!loop)
                {
                    Destroy(gameObject);
                    return;
                }

                nextPoint = 0;
            }

            TryMoveTo(route[nextPoint++]);
        }

        public void Leave()
        {
            Destroy(gameObject);
        }
    }

    public sealed class MaimeskulActor : Walker
    {
        private static readonly Vector3[] Patrol =
        {
            new(-14f, 0f, 1.5f), new(-7f, 0f, 1.5f), new(1f, 0f, 1.5f), new(5f, 0f, 1.5f),
            new(5f, 0f, -6f), new(5f, 0f, -14f), new(2f, 0f, 6f), new(1.5f, 0f, -3f), new(8f, 0f, -12f)
        };

        private int nextPoint;

        public static MaimeskulActor Create(Vector3 position, Quaternion labelRotation)
        {
            MaimeskulActor actor = new GameObject("Маймескул").AddComponent<MaimeskulActor>();
            actor.transform.position = position;
            actor.Setup("Маймескул", Prim.Material("Special").color, 2f, 2.2f, labelRotation);
            actor.Agent.avoidancePriority = 5;
            actor.Warp(position);
            actor.nextPoint = Random.Range(0, Patrol.Length);
            DustPuff.Spawn(actor.FeetPosition, 14, 1.6f);
            return actor;
        }

        private void Update()
        {
            if (!IsMoving)
            {
                nextPoint = (nextPoint + 1 + Random.Range(0, 3)) % Patrol.Length;
                TryMoveTo(Patrol[nextPoint]);
            }
        }

        public void Vanish()
        {
            DustPuff.Spawn(FeetPosition, 14, 1.6f);
            Destroy(gameObject);
        }
    }

    /// <summary>Cartoon dust cloud: a few spheres that swell and disappear.</summary>
    public sealed class DustPuff : MonoBehaviour
    {
        private float age;
        private Vector3 velocity;
        private float lifetime;

        public static void Spawn(Vector3 position, int count = 8, float spread = 1f)
        {
            for (int index = 0; index < count; index++)
            {
                Vector3 offset = new(Random.Range(-spread, spread) * 0.4f, Random.Range(0.1f, 0.6f), Random.Range(-spread, spread) * 0.4f);
                GameObject sphere = Prim.Shape(null, "Dust", PrimitiveType.Sphere, position + offset, Vector3.one * 0.25f, Prim.Material("Dust"));
                Destroy(sphere.GetComponent<Collider>());
                DustPuff puff = sphere.AddComponent<DustPuff>();
                puff.velocity = new Vector3(offset.x, Random.Range(0.4f, 1.2f), offset.z) * spread;
                puff.lifetime = Random.Range(0.6f, 1.1f);
            }
        }

        private void Update()
        {
            age += Time.unscaledDeltaTime;
            transform.position += velocity * Time.unscaledDeltaTime;
            transform.localScale = Vector3.one * Mathf.Lerp(0.25f, 0.9f, age / lifetime);
            if (age >= lifetime)
            {
                Destroy(gameObject);
            }
        }
    }
}
