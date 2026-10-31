using UnityEditor;

namespace UrbanGenesis.EditorTools
{
    /// <summary>
    /// Imports the HUD icons (Resources/UI/Icons) as crisp, uncompressed UI textures. The icons are white;
    /// the style sheet tints them, so they must keep their transparency and not be colour-compressed.
    /// </summary>
    public sealed class UiIconImporter : AssetPostprocessor
    {
        private const string IconFolder = "Assets/Resources/UI/Icons/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(IconFolder))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
        }
    }
}
