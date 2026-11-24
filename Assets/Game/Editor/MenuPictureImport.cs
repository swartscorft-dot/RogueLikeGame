using UnityEditor;
using UnityEngine;

namespace Roguelike.EditorTools
{
    /// <summary>
    /// The import of the start menu's pictures (work order 117): every texture under <see cref="Folder"/> is imported as it is
    /// drawn, at its own size and shape, on every import. Kristian chose "A2: plakaten" of the pictures of her for the character
    /// selection and said "Ja til installering" of an upscaler to make it sharp (2026-11-24): the file is 3840 by 2160, and work
    /// order 114's menu reads its width and height to crop it to cover the screen (<c>CoverCrop</c>), so nothing may shrink,
    /// rescale or squash it:
    /// - Non-Power of 2: None (the default rounds 3840 by 2160 to 4096 by 2048 and squashes her);
    /// - Max Size 4096 (the default 2048 would halve it);
    /// - no mip maps (the order's: the menu draws it at the screen's size, never far off);
    /// - Clamp, Bilinear, sRGB (as the pictures were judged: work order 114's round 1 read one as linear and it came out pale);
    /// - Read/Write off (nothing reads its pixels on the CPU: it would only double its memory);
    /// - compression High Quality (BC7 on a PC, 1 byte a pixel: 7.9 MiB in memory and in a build for 3840 by 2160, where none
    ///   would take 31.6 MiB as RGBA32; BC7 keeps a drawing's flat fields and ink lines without the blocks of the normal kind).
    /// </summary>
    public sealed class MenuPictureImport : AssetPostprocessor
    {
        /// <summary>The folder the start menu loads its pictures from (Resources/UI/Menu, by name: <c>StartMenuValues.PictureName</c>).</summary>
        public const string Folder = "Assets/Game/Resources/UI/Menu/";
        /// <summary>The largest side kept: the picture is 3840 wide.</summary>
        public const int MaxSize = 4096;

        /// <summary>True for a path the rule takes.</summary>
        public static bool Takes(string assetPath) => assetPath != null && assetPath.Replace('\\', '/').StartsWith(Folder, System.StringComparison.OrdinalIgnoreCase);

        void OnPreprocessTexture()
        {
            if (!Takes(assetPath)) return;
            var importer = (TextureImporter)assetImporter;
            Apply(importer);
        }

        /// <summary>The settings, on an importer (the rule's, and for a test that checks the committed ones).</summary>
        public static void Apply(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.sRGBTexture = true;
            importer.isReadable = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.maxTextureSize = MaxSize;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.streamingMipmaps = false;
        }
    }
}
