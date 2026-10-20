using UnityEditor;
using UnityEngine;

namespace ProjectBootstrap
{
    // Import settings for the game's audio (Assets/Resources/Audio): compressed Vorbis everywhere, SFX mono and decompressed on load
    // (short clips play instantly), music streamed. Looping is set on the AudioSource by AudioManager. Preload is on.
    public class AudioImportPostprocessor : AssetPostprocessor
    {
        const string Root = "Assets/Resources/Audio/";

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(Root)) return;
            var importer = (AudioImporter)assetImporter;
            bool music = assetPath.StartsWith(Root + "music/");
            var settings = new AudioImporterSampleSettings();
            settings.preloadAudioData = !music;   // short sound effects are loaded with the scene; music streams
            settings.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = music ? 0.5f : 0.6f;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = !music;
            importer.loadInBackground = false;
        }
    }
}
