# Life Agent — Audio Lifelogging Pipeline

A .NET 10 implementation of the Life Agent's audio lifelogging subsystem, designed to capture, transcribe, and structure continuous audio from an [Omi pendant](https://www.omi.me/) into searchable conversational memory.

## Architecture

```
Omi Pendant → BLE → Phone App → WebSocket → OmiWebSocketListener
  → DeepgramStreamingClient (nova-3 streaming ASR)
    → ConversationSegmenter (temporal gap detection)
      → SpeakerIdentificationService (cosine similarity matching)
        → TranscriptStructuringService (LLM: summary, topics, entities, action items)
          → SqliteConversationalMemory (FTS5 full-text search)
          → SqliteEventStore (append-only event log)
```

## Project Structure

```
LifeAgent.slnx                          # Solution file
LifeAgent.Core/                          # Zero external dependencies
  Models/
    LifeTask.cs                          # Task entity, enums (Priority, Status, Origin)
    TaskResult.cs                        # Worker execution result
    UserProfile.cs                       # Preferences, trust levels, audio settings
  Events/
    LifeEvent.cs                         # Base event record with ULID generation
    TaskEvents.cs                        # Task lifecycle, human interaction, proactivity
    AudioEvents.cs                       # AudioSegmentTranscribed, SpeakerIdentified, etc.
  Audio/
    TranscriptModels.cs                  # TranscriptSegment, Conversation
    SpeakerGallery.cs                    # SpeakerProfile, SpeakerEmbedding, SpeakerMatch
    AudioPipelineOptions.cs              # Full pipeline configuration
  IWorkerAgent.cs                        # Worker agent interface
  IEventStore.cs                         # Append-only event store interface
  IConversationalMemory.cs               # Conversational memory interface (FTS, CRUD)

LifeAgent.Audio/                         # Audio pipeline services
  Deepgram/
    DeepgramStreamingClient.cs           # Deepgram v5 SDK WebSocket streaming
  Diarization/
    SpeakerIdentificationService.cs      # SIMD-accelerated cosine similarity
  Pipeline/
    OmiWebSocketListener.cs              # WebSocket server for Omi pendant audio
    ConversationSegmenter.cs             # Groups utterances by silence gaps
    TranscriptStructuringService.cs      # LLM-based extraction (GPT-4o-mini)
    SqliteConversationalMemory.cs        # SQLite + FTS5 conversational store
    AudioPipelineService.cs              # BackgroundService orchestrator
  AudioLifelogAgent.cs                   # IWorkerAgent: recall, digest, enrollment
  AudioServiceExtensions.cs              # DI registration

LifeAgent.App/                           # Application host
  Program.cs                             # Host builder, DI wiring, config chain
  SqliteEventStore.cs                    # Append-only event store (SQLite, WAL)
  Diagnostics.cs                         # OpenTelemetry ActivitySource
  appsettings.json                       # Full configuration template
  appsettings.Development.json           # Dev overrides
```

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Deepgram API key](https://console.deepgram.com/) (for real-time transcription)
- [OpenAI API key](https://platform.openai.com/) (for transcript structuring)
- [Omi pendant](https://www.omi.me/) + companion phone app

## Getting Started

### 1. Build

```bash
dotnet build
```

### 2. Configure

Set API keys via user secrets (recommended):

```bash
cd LifeAgent.App
dotnet user-secrets set "Audio:DeepgramApiKey" "your-deepgram-key"
dotnet user-secrets set "AI:ApiKey" "your-openai-key"
```

Or via environment variables:

```bash
export AUDIO__DEEPGRAMAPIKEY=your-deepgram-key
export AI__APIKEY=your-openai-key
```

### 3. Run

```bash
dotnet run --project LifeAgent.App
```

The audio pipeline starts a WebSocket server on `ws://0.0.0.0:8765/audio/` by default. Configure the Omi companion app to stream audio to this endpoint.

### 4. Connect Omi Pendant

In the Omi Flutter app, configure the WebSocket audio endpoint to point to your machine's IP on port 8765. The pendant streams 16-bit PCM audio at 16kHz mono via BLE to the phone, which relays it over WebSocket.

## Key Design Decisions

- **Privacy-first**: Raw audio is never stored. Only transcript text enters the conversational memory.
- **Event-sourced**: All state changes emit events to the append-only event store.
- **SQLite + FTS5**: Full-text search over transcripts with no external database dependency.
- **SIMD speaker matching**: Cosine similarity uses `System.Numerics.Vector<float>` for hardware-accelerated embedding comparison.
- **Layered architecture**: Core has zero external dependencies; Audio depends only on Deepgram SDK + SQLite; App wires everything together.

## Configuration Reference

See [appsettings.json](LifeAgent.App/appsettings.json) for all available options. Key sections:

| Section | Description |
|---------|-------------|
| `Audio:DeepgramApiKey` | Deepgram API key |
| `Audio:DeepgramModel` | ASR model (default: `nova-3`) |
| `Audio:WebSocketPort` | Omi listener port (default: `8765`) |
| `Audio:EnableDiarization` | Speaker diarization (default: `true`) |
| `Audio:ConversationGapSeconds` | Silence gap to split conversations (default: `120`) |
| `Audio:SpeakerConfidenceThreshold` | Cosine similarity threshold (default: `0.35`) |
| `Audio:StructuringModel` | LLM for summaries (default: `gpt-4o-mini`) |
| `AI:ApiKey` | OpenAI API key for structuring |
