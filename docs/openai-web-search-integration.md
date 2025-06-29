# OpenAI Web Search Integration for AgentAlpha

## Overview

This document describes the integration of OpenAI's builtin web search tool into the AgentAlpha agent system. The web search functionality allows AgentAlpha to access current information from the internet, making it capable of answering questions about recent events, current data, and real-time information.

## Problem Statement

Previously, AgentAlpha was limited to its training data and could not access current information from the web. This limitation prevented it from:
- Answering questions about recent news or events
- Providing current stock prices, weather, or other real-time data
- Finding the latest information on evolving topics
- Accessing up-to-date documentation or resources

## Solution Architecture

### Core Components

The web search integration adds several new components to the AgentAlpha system:

#### 1. WebSearchTool Model
A new model class (`WebSearchTool.cs`) that represents the OpenAI web search configuration:
- Supports the `web_search_preview` tool type
- Configurable user location for geographically relevant results
- Adjustable search context size (low, medium, high)
- Converts to OpenAI `ToolDefinition` format

#### 2. Enhanced ToolSelector
The `ToolSelector` service has been extended with:
- `ShouldIncludeWebSearch()` method to detect web search requirements
- Integration of web search keywords in heuristic mapping
- Automatic inclusion of web search tool when relevant tasks are detected
- Separation of MCP tools from built-in OpenAI tools

#### 3. Configuration Integration
Web search configuration is integrated into the `AgentConfiguration` class:
- Default web search tool settings
- Support for user location configuration
- Configurable search context size

#### 4. Intelligent Tool Selection
The system automatically detects when web search is needed based on task keywords:
- **Web search triggers**: "web", "search", "internet", "online", "news", "current", "latest", "recent", "today", "real-time", "live", "browse", "website", "url", "google", "find"
- **Smart exclusion**: Web search is not included for purely computational or file-based tasks
- **Efficient selection**: Web search is added as an additional tool without displacing essential tools

## Implementation Details

### WebSearchTool Configuration

```csharp
public class WebSearchTool
{
    public string Type { get; set; } = "web_search_preview";
    public WebSearchUserLocation? UserLocation { get; set; }
    public string? SearchContextSize { get; set; }
}

public class WebSearchUserLocation
{
    public string Type { get; set; } = "approximate";
    public string? Country { get; set; }
    public string? City { get; set; }
    public string? Region { get; set; }
    public string? Timezone { get; set; }
}
```

### Tool Selection Logic

The web search tool is automatically included when:
1. The user's task contains web search keywords
2. There are available tool slots (respects `MaxToolsPerRequest` limits)
3. The task requires current or real-time information

Example tasks that trigger web search:
- "What's the latest news today?"
- "Find current stock prices for Apple"
- "Search for recent developments in AI"
- "What's happening online right now?"

### API Integration

The web search tool integrates seamlessly with OpenAI's Responses API:

```json
{
  "model": "gpt-4.1",
  "tools": [
    {
      "type": "web_search_preview",
      "user_location": {
        "type": "approximate",
        "country": "US",
        "city": "New York",
        "region": "NY"
      },
      "search_context_size": "medium"
    }
  ],
  "input": "What was a positive news story from today?"
}
```

## Benefits

### Enhanced Capabilities
- **Current Information**: Access to real-time data and recent events
- **Broader Knowledge**: Ability to find information beyond training data
- **Real-time Responses**: Up-to-date answers to time-sensitive questions

### Seamless Integration
- **Automatic Detection**: No need for users to explicitly request web search
- **Contextual Usage**: Only activates when relevant to the task
- **Efficient Resource Usage**: Respects tool limits and selection priorities

### Cost Optimization
- **Smart Selection**: Only includes web search when needed
- **Configurable Context**: Adjustable search context size for cost control
- **Token Efficiency**: Minimal impact on overall token usage

## Configuration Options

### Environment Variables
The web search tool can be configured through the existing AgentConfiguration system:

```csharp
var config = new AgentConfiguration
{
    WebSearch = new WebSearchTool
    {
        SearchContextSize = "medium",
        UserLocation = new WebSearchUserLocation
        {
            Country = "US",
            City = "San Francisco",
            Region = "CA"
        }
    }
};
```

### Search Context Sizes
- **`low`**: Fastest response, lowest cost, least context
- **`medium`**: Balanced response time, cost, and context (default)
- **`high`**: Slowest response, highest cost, most comprehensive context

## Usage Examples

### News and Current Events
```
User: "What are the top news stories today?"
Agent: [Automatically includes web search tool and provides current headlines]
```

### Real-time Data
```
User: "What's the current weather in New York?"
Agent: [Uses web search to find current weather conditions]
```

### Recent Developments
```
User: "Find the latest updates on the Mars rover mission"
Agent: [Searches for recent space mission news and updates]
```

### Stock and Financial Data
```
User: "What are the current stock prices for tech companies?"
Agent: [Retrieves real-time stock market information]
```

## Technical Implementation Notes

### Model Compatibility
The web search tool works with OpenAI models that support the Responses API:
- `gpt-4.1` (recommended)
- `o4-mini`
- `gpt-4.1` (limited support)

### Limitations
- Web search is not supported in `gpt-4.1-nano`
- Search results are subject to OpenAI's web search rate limits
- User location configuration is not supported for deep research models

### Error Handling
The system gracefully handles web search failures:
- Falls back to available MCP tools if web search fails
- Continues operation without web search if the tool is unavailable
- Provides clear error messages when web search encounters issues

## Testing

The web search integration includes comprehensive tests:
- Unit tests for `WebSearchTool` model and configuration
- Integration tests for `ToolSelector` keyword detection
- End-to-end tests for web search tool selection
- Validation tests for configuration scenarios

## Future Enhancements

### Planned Improvements
1. **Smart Caching**: Cache recent search results to reduce API calls
2. **User Preferences**: Allow users to configure web search preferences
3. **Search Filtering**: Add domain filtering and result type preferences
4. **Analytics**: Track web search usage and effectiveness
5. **Advanced Location**: GPS-based location detection for mobile scenarios

### Integration Opportunities
1. **Session Persistence**: Remember search context across conversation turns
2. **Planning Integration**: Use web search in multi-step planning scenarios
3. **Tool Chaining**: Combine web search with other tools for complex workflows
4. **Result Processing**: Enhanced processing of web search results

## Conclusion

The OpenAI web search integration transforms AgentAlpha from a static knowledge agent into a dynamic, real-time information assistant. By automatically detecting when current information is needed and seamlessly integrating web search capabilities, AgentAlpha can now handle a much broader range of user requests while maintaining its efficiency and ease of use.

The implementation follows the existing architectural patterns in AgentAlpha, ensuring consistency and maintainability while providing powerful new capabilities for accessing current information from the web.