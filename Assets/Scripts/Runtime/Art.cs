using System.Collections.Generic;
using UnityEngine;

namespace Uvc.Prototype
{
    /// <summary>
    /// Placeholder 2D art cut from the concept sheets in /images (see Resources/Placeholders/README.md).
    /// Missing files simply return null and the UI falls back to text.
    /// </summary>
    public static class Art
    {
        private static readonly Dictionary<string, Texture2D> Cache = new();

        public static Texture2D Portrait(string characterId) => Load("Placeholders/Portraits/" + characterId);

        public static Texture2D Prop(string propKey) => string.IsNullOrEmpty(propKey) ? null : Load("Placeholders/Props/" + propKey);

        private static Texture2D Load(string path)
        {
            if (!Cache.TryGetValue(path, out Texture2D texture))
            {
                texture = Resources.Load<Texture2D>(path);
                Cache[path] = texture;
            }

            return texture;
        }
    }
}
