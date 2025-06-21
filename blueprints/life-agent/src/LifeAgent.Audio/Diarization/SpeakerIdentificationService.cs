using System.Numerics;
using LifeAgent.Core.Audio;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LifeAgent.Audio.Diarization;

/// <summary>
/// Speaker identification service that matches utterance embeddings against an enrolled
/// speaker gallery using cosine similarity on ECAPA-TDNN 192-dim vectors.
///
/// In the full pipeline, ECAPA-TDNN inference runs server-side (SpeechBrain ONNX export
/// or a Python sidecar). This service handles the gallery management and matching logic.
///
/// For Phase 5 MVP, speaker labels come from Deepgram's built-in diarization
/// (speaker 0, 1, 2...) and are matched to names post-hoc via embedding comparison.
/// </summary>
public sealed class SpeakerIdentificationService
{
    private readonly AudioPipelineOptions _options;
    private readonly ILogger<SpeakerIdentificationService> _logger;

    /// <summary>
    /// In-memory speaker gallery. Loaded from persistence on startup,
    /// updated on enrollment. Thread-safe reads via immutable reference swap.
    /// </summary>
    private volatile IReadOnlyList<SpeakerProfile> _gallery = [];

    public SpeakerIdentificationService(
        IOptions<AudioPipelineOptions> options,
        ILogger<SpeakerIdentificationService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Load the speaker gallery from persisted profiles (called on startup).
    /// </summary>
    public void LoadGallery(IReadOnlyList<SpeakerProfile> profiles)
    {
        _gallery = profiles;
        _logger.LogInformation("Speaker gallery loaded: {Count} enrolled speakers", profiles.Count);
    }

    /// <summary>
    /// Match an utterance embedding against the enrolled gallery.
    /// Returns the best match if cosine similarity exceeds the configured threshold.
    /// </summary>
    public SpeakerMatch? Identify(ReadOnlySpan<float> utteranceEmbedding)
    {
        if (_gallery.Count == 0)
            return null;

        SpeakerMatch? best = null;

        foreach (var profile in _gallery)
        {
            // Compare against all enrolled embeddings for this speaker
            // (multiple samples improve robustness across conditions)
            var maxSimilarity = float.MinValue;

            foreach (var enrollment in profile.Embeddings)
            {
                var similarity = CosineSimilarity(utteranceEmbedding, enrollment.Vector);
                if (similarity > maxSimilarity)
                    maxSimilarity = similarity;
            }

            var isConfident = maxSimilarity >= _options.SpeakerIdThreshold;

            if (best is null || maxSimilarity > best.CosineSimilarity)
            {
                best = new SpeakerMatch(
                    profile.Name,
                    maxSimilarity,
                    isConfident);
            }
        }

        if (best is not null && best.IsConfident)
        {
            _logger.LogDebug("[SPEAKER-ID] Identified: {Name} (similarity={Sim:F3})",
                best.SpeakerName, best.CosineSimilarity);
            return best;
        }

        _logger.LogDebug("[SPEAKER-ID] No confident match (best={Sim:F3}, threshold={Thresh:F3})",
            best?.CosineSimilarity ?? 0f, _options.SpeakerIdThreshold);
        return null;
    }

    /// <summary>
    /// Enroll a new speaker from a voice sample embedding.
    /// </summary>
    public SpeakerProfile Enroll(string name, float[] embedding, TimeSpan sampleDuration, string? note = null)
    {
        var profile = new SpeakerProfile
        {
            Name = name,
            Embeddings =
            [
                new SpeakerEmbedding
                {
                    Vector = embedding,
                    SampleDuration = sampleDuration,
                    Note = note
                }
            ]
        };

        // Atomic swap — readers see either old or new gallery, never partial state
        var updatedGallery = new List<SpeakerProfile>(_gallery) { profile };
        _gallery = updatedGallery;

        _logger.LogInformation("[SPEAKER-ID] Enrolled new speaker: {Name} (embedding dim={Dim}, sample={Duration:F1}s)",
            name, embedding.Length, sampleDuration.TotalSeconds);

        return profile;
    }

    /// <summary>
    /// Add an additional embedding to an existing speaker (improves recognition in varied conditions).
    /// </summary>
    public void AddEmbedding(string speakerName, float[] embedding, TimeSpan sampleDuration, string? note = null)
    {
        var existing = _gallery.FirstOrDefault(p => p.Name == speakerName);
        if (existing is null)
        {
            _logger.LogWarning("[SPEAKER-ID] Cannot add embedding — speaker not found: {Name}", speakerName);
            return;
        }

        existing.Embeddings.Add(new SpeakerEmbedding
        {
            Vector = embedding,
            SampleDuration = sampleDuration,
            Note = note
        });

        _logger.LogInformation("[SPEAKER-ID] Added embedding to {Name} (total={Count})",
            speakerName, existing.Embeddings.Count);
    }

    /// <summary>
    /// Compute cosine similarity between two vectors.
    /// Uses SIMD-accelerated operations via System.Numerics when available.
    /// </summary>
    internal static float CosineSimilarity(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        if (a.Length != b.Length)
            throw new ArgumentException($"Embedding dimension mismatch: {a.Length} vs {b.Length}");

        float dot = 0, normA = 0, normB = 0;

        // Use SIMD acceleration where possible
        int i = 0;
        int simdLength = Vector<float>.Count;
        int vectorizable = a.Length - (a.Length % simdLength);

        var dotVec = Vector<float>.Zero;
        var normAVec = Vector<float>.Zero;
        var normBVec = Vector<float>.Zero;

        for (; i < vectorizable; i += simdLength)
        {
            var va = new Vector<float>(a.Slice(i, simdLength));
            var vb = new Vector<float>(b.Slice(i, simdLength));
            dotVec += va * vb;
            normAVec += va * va;
            normBVec += vb * vb;
        }

        dot = Vector.Dot(dotVec, Vector<float>.One);
        normA = Vector.Dot(normAVec, Vector<float>.One);
        normB = Vector.Dot(normBVec, Vector<float>.One);

        // Handle remainder
        for (; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        var denominator = MathF.Sqrt(normA) * MathF.Sqrt(normB);
        return denominator == 0 ? 0f : dot / denominator;
    }
}
