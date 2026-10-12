using UnityEngine;

namespace Uvc.Prototype
{
    /// <summary>A world object or NPC the hero can approach and act on.</summary>
    public sealed class Interactable : MonoBehaviour
    {
        private Vector3 approachPoint;

        public string Title { get; private set; }
        public string[] ActionIds { get; private set; }
        public Walker Person { get; private set; }
        public string IconKey { get; private set; }

        public Texture2D Icon => Person != null ? Art.Portrait(IconKey) : Art.Prop(IconKey);

        public Vector3 ApproachPoint => Person != null ? Person.FeetPosition : approachPoint;

        /// <summary>Objects need the exact approach point; people can be addressed from a short distance.</summary>
        public float InteractRange => Person != null ? 2.1f : 0.5f;

        public void ConfigureObject(string title, Vector3 approach, string[] actionIds, string propKey)
        {
            Title = title;
            IconKey = propKey;
            approachPoint = approach;
            ActionIds = actionIds;
        }

        public void ConfigurePerson(Walker person, string characterId, string title, string[] actionIds)
        {
            Person = person;
            IconKey = characterId;
            Title = title;
            ActionIds = actionIds;
        }
    }
}
