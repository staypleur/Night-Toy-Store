using UnityEngine;

namespace NightToyStore
{
    public static class PrototypeMaterials
    {
        public static Material Create(Color color)
        {
            var shader = Resources.Load<Shader>("ToyPrototype");
            if (shader == null || (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null && !shader.isSupported))
                throw new System.InvalidOperationException("Prototype surface shader missing or unsupported.");
            return new Material(shader) { color = color };
        }
    }
}
