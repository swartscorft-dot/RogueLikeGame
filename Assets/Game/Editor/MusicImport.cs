using System;
using UnityEditor;
using UnityEngine;

namespace Roguelike.EditorTools
{
    /// <summary>
    /// How his music is imported (work order 116): every clip under <see cref="Folder"/> exactly as the file holds it, so that a
    /// loop goes round to the sample. PCM (no compression: Vorbis and MP3 pad a clip's start and end, which would shift every
    /// round), its own sample rate (44.1 kHz, never resampled at import), stereo kept, decompressed on load and loaded before
    /// the scene needs it (PlayScheduled needs the data in memory to start on its sample), not in the background. About 5.3 MB
    /// for Fight.wav and 2.6 MB for Explore.wav in memory. Set by this as each clip is imported; nothing is changed by hand.
    /// </summary>
    public sealed class MusicImport : AssetPostprocessor
    {
        public const string Folder = "Assets/Game/Resources/Music/";

        /// <summary>The settings a music clip is imported with.</summary>
        public static AudioImporterSampleSettings Settings(AudioImporterSampleSettings s)
        {
            s.loadType = AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.PCM;
            s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            s.preloadAudioData = true;
            return s;
        }

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(Folder, StringComparison.Ordinal)) return;
            var importer = (AudioImporter)assetImporter;
            importer.defaultSampleSettings = Settings(importer.defaultSampleSettings);
            importer.forceToMono = false;
            importer.loadInBackground = false;
            importer.ambisonic = false;
        }
    }
}
