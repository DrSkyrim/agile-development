using UnityEngine;

namespace UrbanGenesis.Game
{
    /// <summary>
    /// Makes materials from code, so the project needs no material assets yet.
    /// Each method tries the URP shader first and falls back to a built-in one if it cannot be found.
    /// </summary>
    public static class MaterialFactory
    {
        /// <summary>Opaque unlit material showing a texture (used for the ground).</summary>
        public static Material CreateUnlitTextured(Texture texture)
        {
            Shader shader = FindShader("Universal Render Pipeline/Unlit", "Unlit/Texture", "Sprites/Default");
            var material = new Material(shader);
            material.mainTexture = texture;
            material.color = Color.white;
            return material;
        }

        /// <summary>Lit gray material for building cubes.</summary>
        public static Material CreateLitGray(float brightness)
        {
            Shader shader = FindShader("Universal Render Pipeline/Lit", "Standard", "Sprites/Default");
            var material = new Material(shader);
            var color = new Color(brightness, brightness, brightness, 1f);
            material.color = color;

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.15f);
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0f);
            }

            return material;
        }

        /// <summary>
        /// Material that honours vertex colours and alpha (roads, lot tints, tool previews). Things with a lower
        /// <paramref name="renderQueue"/> are drawn first, so roads (2100) sit under lot tints (2200) and previews (3000).
        /// </summary>
        public static Material CreateVertexColorOverlay(int renderQueue)
        {
            Shader shader = FindShader("Sprites/Default", "Universal Render Pipeline/Unlit");
            var material = new Material(shader);
            material.renderQueue = renderQueue;
            return material;
        }

        private static Shader FindShader(params string[] names)
        {
            foreach (string name in names)
            {
                Shader shader = Shader.Find(name);
                if (shader != null)
                {
                    return shader;
                }
            }

            Debug.LogError("None of the shaders could be found: " + string.Join(", ", names));
            return Shader.Find("Hidden/InternalErrorShader");
        }
    }
}
