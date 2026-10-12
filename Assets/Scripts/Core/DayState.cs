using System;
using System.Collections.Generic;

namespace Uvc.Core
{
    public enum QuestStatus
    {
        Unavailable,
        Available,
        Accepted,
        Completed,
        RewardClaimed,
        Failed,
        Cancelled
    }

    [Serializable]
    public sealed class QuestRecord
    {
        public string id;
        public QuestStatus status;
    }

    [Serializable]
    public sealed class Cooldown
    {
        public string id;
        public float until;
    }

    [Serializable]
    public sealed class MaimeskulState
    {
        public bool active;
        public float startedAt;
        public float expiresAt;
        public bool endedEarly;
        public List<Cooldown> reactionCooldowns = new();

        public float Remaining(float absoluteNow) => active ? Math.Max(0f, expiresAt - absoluteNow) : 0f;
    }

    /// <summary>All mutable state of one game day. Serialized as the save file.</summary>
    [Serializable]
    public sealed class DayState
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public int seed;
        public GameClock clock = new();
        public PlayerStats stats = new();
        public Inventory inventory = new();
        public List<string> flags = new();
        public List<QuestRecord> quests = new();
        public List<Cooldown> cooldowns = new();
        public string printerFault;
        public string projectorInput;
        public int printerFailures;
        public int projectorFailures;
        public int stockFailures;
        public int matchesPlayed;
        public int matchesWon;
        public string partyOutcome;
        public BragarPhase bragarPhase;
        public float bragarSearchEndsAt;
        public string bragarMessage;
        public int chessPlayed;
        public bool hasPlayerPosition;
        public float playerX;
        public float playerZ;
        public MaimeskulState maimeskul = new();

        public static DayState CreateNew(int seed)
        {
            Random random = new(seed);
            DayState state = new()
            {
                seed = seed,
                printerFault = Faults.All[random.Next(Faults.All.Length)],
                projectorInput = ProjectorInputs.All[random.Next(ProjectorInputs.All.Length)]
            };
            state.SetQuest(QuestIds.Evening, QuestStatus.Available);
            state.SetQuest(QuestIds.Party, QuestStatus.Unavailable);
            state.SetQuest(QuestIds.Maimeskul, QuestStatus.Unavailable);
            state.SetQuest(QuestIds.LastErrand, QuestStatus.Unavailable);
            return state;
        }

        public bool HasFlag(string flag) => flags.Contains(flag);

        public void SetFlag(string flag)
        {
            if (!flags.Contains(flag))
            {
                flags.Add(flag);
            }
        }

        public QuestStatus GetQuest(string questId)
        {
            QuestRecord record = quests.Find(q => q.id == questId);
            return record?.status ?? QuestStatus.Unavailable;
        }

        public void SetQuest(string questId, QuestStatus status)
        {
            QuestRecord record = quests.Find(q => q.id == questId);
            if (record == null)
            {
                quests.Add(new QuestRecord { id = questId, status = status });
                return;
            }

            record.status = status;
        }

        public bool IsQuestAccepted(string questId) => GetQuest(questId) == QuestStatus.Accepted;

        public bool IsOnCooldown(string id) => FindCooldown(cooldowns, id) > clock.Absolute;

        public void StartCooldown(string id, float gameMinutes) => SetCooldown(cooldowns, id, clock.Absolute + gameMinutes);

        public int InvitedCount
        {
            get
            {
                int count = 0;
                foreach (string friend in NpcIds.Friends)
                {
                    if (HasFlag(Flags.Invited(friend)))
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        internal static float FindCooldown(List<Cooldown> list, string id)
        {
            Cooldown record = list.Find(c => c.id == id);
            return record?.until ?? float.MinValue;
        }

        internal static void SetCooldown(List<Cooldown> list, string id, float until)
        {
            Cooldown record = list.Find(c => c.id == id);
            if (record == null)
            {
                list.Add(new Cooldown { id = id, until = until });
                return;
            }

            record.until = until;
        }
    }

    public static class ProjectorInputs
    {
        public const string Hdmi1 = "hdmi1";
        public const string Hdmi2 = "hdmi2";
        public const string Vga = "vga";

        public static readonly string[] All = { Hdmi1, Hdmi2, Vga };

        public static string Label(string input) => input switch
        {
            Hdmi1 => "HDMI 1",
            Hdmi2 => "HDMI 2",
            Vga => "VGA",
            _ => input
        };
    }
}
