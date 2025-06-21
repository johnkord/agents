using LifeAgent.Audio.Diarization;
using LifeAgent.Audio.Pipeline;
using LifeAgent.Core;
using LifeAgent.Core.Audio;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LifeAgent.Audio;

/// <summary>
/// DI registration for the audio lifelogging pipeline.
/// Call <see cref="AddAudioLifelogging"/> in Program.cs to wire everything up.
/// </summary>
public static class AudioServiceExtensions
{
    /// <summary>
    /// Registers all audio lifelogging services: Deepgram streaming, speaker identification,
    /// conversation segmentation, transcript structuring, conversational memory (SQLite),
    /// and the pipeline background service.
    /// </summary>
    public static IServiceCollection AddAudioLifelogging(
        this IServiceCollection services,
        string conversationalMemoryDbPath = "data/conversational-memory.db")
    {
        // Configuration
        services.AddOptions<AudioPipelineOptions>()
            .BindConfiguration(AudioPipelineOptions.SectionName);

        // Core services
        services.AddSingleton<SpeakerIdentificationService>();
        services.AddSingleton<ConversationSegmenter>();
        services.AddSingleton<TranscriptStructuringService>();

        // Conversational memory (SQLite)
        services.AddSingleton<IConversationalMemory>(sp =>
        {
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>()
                .CreateLogger<SqliteConversationalMemory>();

            // Ensure data directory exists
            var dir = Path.GetDirectoryName(conversationalMemoryDbPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var memory = new SqliteConversationalMemory(conversationalMemoryDbPath, logger);
            // Initialize schema synchronously in factory — runs once on first resolution
            memory.InitializeAsync().GetAwaiter().GetResult();
            return memory;
        });

        // Background service — the pipeline orchestrator
        services.AddHostedService<AudioPipelineService>();

        return services;
    }
}
