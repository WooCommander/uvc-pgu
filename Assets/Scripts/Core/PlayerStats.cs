using System;
using System.Collections.Generic;

namespace Uvc.Core
{
    [Serializable]
    public sealed class Relationship
    {
        public string npcId;
        public int value;
    }

    [Serializable]
    public sealed class PlayerStats
    {
        public int energy = 80;
        public int mood = 65;
        public int bossReputation = 50;
        public int studentReputation = 50;
        public int mess = 15;
        public List<Relationship> relationships = new();

        public float WorkDurationMultiplier => energy < 20 ? 1.25f : 1f;

        public void ChangeEnergy(int delta) => energy = Clamp(energy + delta, 0, 100);
        public void ChangeMood(int delta) => mood = Clamp(mood + delta, 0, 100);
        public void ChangeBossReputation(int delta) => bossReputation = Clamp(bossReputation + delta, 0, 100);
        public void ChangeStudentReputation(int delta) => studentReputation = Clamp(studentReputation + delta, 0, 100);
        public void ChangeMess(int delta) => mess = Clamp(mess + delta, 0, 100);

        public int GetRelationship(string npcId)
        {
            Relationship record = relationships.Find(r => r.npcId == npcId);
            return record?.value ?? 0;
        }

        public void ChangeRelationship(string npcId, int delta)
        {
            Relationship record = relationships.Find(r => r.npcId == npcId);
            if (record == null)
            {
                record = new Relationship { npcId = npcId };
                relationships.Add(record);
            }

            record.value = Clamp(record.value + delta, -100, 100);
        }

        private static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
    }
}
