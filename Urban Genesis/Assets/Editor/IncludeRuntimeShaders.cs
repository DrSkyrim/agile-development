using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace UrbanGenesis.EditorTools
{
    /// <summary>
    /// The game builds its materials from code with Shader.Find, and a build only keeps shaders that some asset refers to.
    /// This adds the shaders the game looks up to Project Settings > Graphics > Always Included Shaders before every build
    /// (and on demand from the Urban Genesis menu), so they are not stripped from the standalone player.
    /// </summary>
    public sealed class IncludeRuntimeShaders : IPreprocessBuildWithReport
    {
        private static readonly string[] ShaderNames =
        {
            "Universal Render Pipeline/Unlit",
            "Universal Render Pipeline/Lit",
            "Sprites/Default",
            "Unlit/Texture",
        };

        public int callbackOrder { get { return 0; } }

        public void OnPreprocessBuild(BuildReport report)
        {
            Apply();
        }

        [MenuItem("Urban Genesis/Include runtime shaders in builds")]
        public static void Apply()
        {
            var serialized = new SerializedObject(GraphicsSettings.GetGraphicsSettings());
            SerializedProperty list = serialized.FindProperty("m_AlwaysIncludedShaders");
            if (list == null)
            {
                Debug.LogError("Could not find the Always Included Shaders list in the Graphics settings.");
                return;
            }

            int added = 0;
            foreach (string name in ShaderNames)
            {
                Shader shader = Shader.Find(name);
                if (shader == null)
                {
                    Debug.LogWarning("Shader not found, cannot add it to the build: " + name);
                    continue;
                }

                bool present = false;
                for (int i = 0; i < list.arraySize; i++)
                {
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                    {
                        present = true;
                        break;
                    }
                }

                if (!present)
                {
                    list.InsertArrayElementAtIndex(list.arraySize);
                    list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
                    added++;
                }
            }

            if (added > 0)
            {
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                Debug.Log("Added " + added + " shader(s) to Always Included Shaders.");
            }
        }
    }
}
