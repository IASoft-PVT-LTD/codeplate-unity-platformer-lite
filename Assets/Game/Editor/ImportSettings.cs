using UnityEditor;
using UnityEngine;

namespace CodePlate
{
    /// <summary>
    /// Import settings for everything under Assets/Resources/Art and Audio, applied automatically
    /// when Unity imports a file, so new or replaced art stays crisp pixel art (point filter, no
    /// compression, no mipmaps, no resizing) without anyone touching .meta files.
    /// For high-resolution (non-pixel) art, change Filter to Bilinear here
    /// (docs/recipes/reskin-and-rebrand.md).
    /// </summary>
    sealed class ImportSettings : AssetPostprocessor
    {
        const string ArtRoot = Game.Core.ProjectPaths.Art + "/";
        const string AudioRoot = Game.Core.ProjectPaths.Audio + "/";
        public const FilterMode Filter = FilterMode.Point;

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtRoot)) return;
            var t = (TextureImporter)assetImporter;
            t.textureType = TextureImporterType.Sprite;
            t.spriteImportMode = SpriteImportMode.Single;
            t.spritePixelsPerUnit = 16;
            t.filterMode = Filter;
            t.mipmapEnabled = false;
            t.textureCompression = TextureImporterCompression.Uncompressed;
            t.alphaIsTransparency = true;
            t.npotScale = TextureImporterNPOTScale.None;
            t.maxTextureSize = 4096;
            t.wrapMode = TextureWrapMode.Clamp;
        }

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(AudioRoot)) return;
            var a = (AudioImporter)assetImporter;
            bool music = System.IO.Path.GetFileName(assetPath).StartsWith("music_");
            var s = a.defaultSampleSettings;
            s.loadType = music ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = music ? 0.6f : 0.8f;
            a.defaultSampleSettings = s;
            a.forceToMono = true;
        }
    }
}
