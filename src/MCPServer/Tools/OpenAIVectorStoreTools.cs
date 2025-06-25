using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;
using System.Text.Json;
using MCPServer.ToolApproval;

namespace MCPServer.Tools;

[McpServerToolType]
public class OpenAIVectorStoreTools
{
    private static readonly HttpClient _client = new()
    {
        Timeout = TimeSpan.FromSeconds(60)
    };

    static OpenAIVectorStoreTools()
    {
        _client.DefaultRequestHeaders.Add("User-Agent", "AgentAlpha-MCP-Server/1.0");
    }

    [McpServerTool(Name = "openai_create_vector_store"), Description("Create a new OpenAI vector store for code analysis.")]
    [RequiresApproval]
    public static string CreateVectorStore(
        string name,
        int? expiresAfterDays = null,
        string? metadata = null)
    {
        var args = new Dictionary<string, object?>
        {
            ["name"] = name,
            ["expiresAfterDays"] = expiresAfterDays,
            ["metadata"] = metadata
        };

        var approved = ToolApprovalManager.Instance.EnsureApproved("openai_create_vector_store", args);
        if (!approved)
        {
            return "Error: Tool execution was denied by approval system.";
        }

        try
        {
            var apiKey = ApiCredentialsManager.Instance.GetOpenAiApiKey();
            if (string.IsNullOrEmpty(apiKey))
            {
                return "Error: OpenAI API key not configured. Please set OPENAI_API_KEY environment variable.";
            }
            var url = "https://api.openai.com/v1/vector_stores";
            
            var requestData = new Dictionary<string, object>
            {
                ["name"] = name
            };

            if (expiresAfterDays.HasValue)
            {
                requestData["expires_after"] = new Dictionary<string, object>
                {
                    ["anchor"] = "last_active_at",
                    ["days"] = expiresAfterDays.Value
                };
            }

            if (!string.IsNullOrEmpty(metadata))
            {
                try
                {
                    var metadataDict = JsonSerializer.Deserialize<Dictionary<string, object>>(metadata);
                    if (metadataDict != null)
                    {
                        requestData["metadata"] = metadataDict;
                    }
                }
                catch
                {
                    // If metadata is not valid JSON, treat as a single key-value pair
                    requestData["metadata"] = new Dictionary<string, object> { ["description"] = metadata };
                }
            }

            var jsonContent = JsonSerializer.Serialize(requestData);
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
            };
            
            request.Headers.Add("Authorization", $"Bearer {apiKey}");
            request.Headers.Add("OpenAI-Beta", "assistants=v2");

            var response = _client.Send(request);
            var content = response.Content.ReadAsStringAsync().Result;

            if (!response.IsSuccessStatusCode)
            {
                return $"Error: OpenAI API returned {response.StatusCode}: {content}";
            }

            var responseData = JsonSerializer.Deserialize<JsonElement>(content);
            var vectorStoreId = responseData.GetProperty("id").GetString() ?? "";
            var status = responseData.GetProperty("status").GetString() ?? "";

            return $"Successfully created vector store:\nID: {vectorStoreId}\nName: {name}\nStatus: {status}";
        }
        catch (Exception ex)
        {
            return $"Error creating vector store: {ex.Message}";
        }
    }

    [McpServerTool(Name = "openai_upload_file_to_vector_store"), Description("Upload a file to an OpenAI vector store.")]
    [RequiresApproval]
    public static string UploadFileToVectorStore(
        string vectorStoreId,
        string filePath,
        string? purpose = "assistants")
    {
        var args = new Dictionary<string, object?>
        {
            ["vectorStoreId"] = vectorStoreId,
            ["filePath"] = filePath,
            ["purpose"] = purpose
        };

        var approved = ToolApprovalManager.Instance.EnsureApproved("openai_upload_file_to_vector_store", args);
        if (!approved)
        {
            return "Error: Tool execution was denied by approval system.";
        }

        try
        {
            var apiKey = ApiCredentialsManager.Instance.GetOpenAiApiKey();
            if (string.IsNullOrEmpty(apiKey))
            {
                return "Error: OpenAI API key not configured. Please set OPENAI_API_KEY environment variable.";
            }
            if (!File.Exists(filePath))
            {
                return $"Error: File not found: {filePath}";
            }

            // First, upload the file
            var uploadUrl = "https://api.openai.com/v1/files";
            using var fileContent = new MultipartFormDataContent();
            using var fileStream = File.OpenRead(filePath);
            using var streamContent = new StreamContent(fileStream);
            
            fileContent.Add(streamContent, "file", Path.GetFileName(filePath));
            fileContent.Add(new StringContent(purpose ?? "assistants"), "purpose");

            using var uploadRequest = new HttpRequestMessage(HttpMethod.Post, uploadUrl)
            {
                Content = fileContent
            };
            
            uploadRequest.Headers.Add("Authorization", $"Bearer {apiKey}");

            var uploadResponse = _client.Send(uploadRequest);
            var uploadContent = uploadResponse.Content.ReadAsStringAsync().Result;

            if (!uploadResponse.IsSuccessStatusCode)
            {
                return $"Error uploading file: {uploadResponse.StatusCode}: {uploadContent}";
            }

            var uploadData = JsonSerializer.Deserialize<JsonElement>(uploadContent);
            var fileId = uploadData.GetProperty("id").GetString() ?? "";

            // Now add the file to the vector store
            var vectorStoreUrl = $"https://api.openai.com/v1/vector_stores/{vectorStoreId}/files";
            var vectorStoreData = new Dictionary<string, object>
            {
                ["file_id"] = fileId
            };

            var jsonContent = JsonSerializer.Serialize(vectorStoreData);
            using var vectorStoreRequest = new HttpRequestMessage(HttpMethod.Post, vectorStoreUrl)
            {
                Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
            };
            
            vectorStoreRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
            vectorStoreRequest.Headers.Add("OpenAI-Beta", "assistants=v2");

            var vectorStoreResponse = _client.Send(vectorStoreRequest);
            var vectorStoreContent = vectorStoreResponse.Content.ReadAsStringAsync().Result;

            if (!vectorStoreResponse.IsSuccessStatusCode)
            {
                return $"Error adding file to vector store: {vectorStoreResponse.StatusCode}: {vectorStoreContent}";
            }

            var vectorStoreResponseData = JsonSerializer.Deserialize<JsonElement>(vectorStoreContent);
            var status = vectorStoreResponseData.GetProperty("status").GetString();

            return $"Successfully uploaded file to vector store:\nFile ID: {fileId}\nVector Store ID: {vectorStoreId}\nStatus: {status}";
        }
        catch (Exception ex)
        {
            return $"Error uploading file to vector store: {ex.Message}";
        }
    }

    [McpServerTool(Name = "openai_query_vector_store"), Description("Query a vector store for similar content using a text query.")]
    [RequiresApproval]
    public static string QueryVectorStore(
        string vectorStoreId,
        string query,
        int maxResults = 5,
        string? assistantInstructions = null)
    {
        var args = new Dictionary<string, object?>
        {
            ["vectorStoreId"] = vectorStoreId,
            ["query"] = query,
            ["maxResults"] = maxResults,
            ["assistantInstructions"] = assistantInstructions
        };

        var approved = ToolApprovalManager.Instance.EnsureApproved("openai_query_vector_store", args);
        if (!approved)
        {
            return "Error: Tool execution was denied by approval system.";
        }

        try
        {
            var apiKey = ApiCredentialsManager.Instance.GetOpenAiApiKey();
            if (string.IsNullOrEmpty(apiKey))
            {
                return "Error: OpenAI API key not configured. Please set OPENAI_API_KEY environment variable.";
            }
            
            // Create a temporary assistant with the vector store attached
            var assistantUrl = "https://api.openai.com/v1/assistants";
            var assistantData = new Dictionary<string, object>
            {
                ["name"] = "Code Review Assistant",
                ["instructions"] = assistantInstructions ?? "You are a helpful code review assistant. Analyze the provided code and answer questions about it.",
                ["model"] = "gpt-4-turbo-preview",
                ["tools"] = new object[] { new { type = "file_search" } },
                ["tool_resources"] = new
                {
                    file_search = new
                    {
                        vector_store_ids = new[] { vectorStoreId }
                    }
                }
            };

            var assistantJsonContent = JsonSerializer.Serialize(assistantData);
            using var assistantRequest = new HttpRequestMessage(HttpMethod.Post, assistantUrl)
            {
                Content = new StringContent(assistantJsonContent, Encoding.UTF8, "application/json")
            };
            
            assistantRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
            assistantRequest.Headers.Add("OpenAI-Beta", "assistants=v2");

            var assistantResponse = _client.Send(assistantRequest);
            var assistantContent = assistantResponse.Content.ReadAsStringAsync().Result;

            if (!assistantResponse.IsSuccessStatusCode)
            {
                return $"Error creating assistant: {assistantResponse.StatusCode}: {assistantContent}";
            }

            var assistantResponseData = JsonSerializer.Deserialize<JsonElement>(assistantContent);
            var assistantId = assistantResponseData.GetProperty("id").GetString();

            try
            {
                // Create a thread
                var threadUrl = "https://api.openai.com/v1/threads";
                using var threadRequest = new HttpRequestMessage(HttpMethod.Post, threadUrl)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json")
                };
                
                threadRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
                threadRequest.Headers.Add("OpenAI-Beta", "assistants=v2");

                var threadResponse = _client.Send(threadRequest);
                var threadContent = threadResponse.Content.ReadAsStringAsync().Result;

                if (!threadResponse.IsSuccessStatusCode)
                {
                    return $"Error creating thread: {threadResponse.StatusCode}: {threadContent}";
                }

                var threadResponseData = JsonSerializer.Deserialize<JsonElement>(threadContent);
                var threadId = threadResponseData.GetProperty("id").GetString();

                try
                {
                    // Add a message to the thread
                    var messageUrl = $"https://api.openai.com/v1/threads/{threadId}/messages";
                    var messageData = new Dictionary<string, object>
                    {
                        ["role"] = "user",
                        ["content"] = query
                    };

                    var messageJsonContent = JsonSerializer.Serialize(messageData);
                    using var messageRequest = new HttpRequestMessage(HttpMethod.Post, messageUrl)
                    {
                        Content = new StringContent(messageJsonContent, Encoding.UTF8, "application/json")
                    };
                    
                    messageRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
                    messageRequest.Headers.Add("OpenAI-Beta", "assistants=v2");

                    var messageResponse = _client.Send(messageRequest);
                    var messageContent = messageResponse.Content.ReadAsStringAsync().Result;

                    if (!messageResponse.IsSuccessStatusCode)
                    {
                        return $"Error creating message: {messageResponse.StatusCode}: {messageContent}";
                    }

                    // Run the assistant
                    var runUrl = $"https://api.openai.com/v1/threads/{threadId}/runs";
                    var runData = new Dictionary<string, object>
                    {
                        ["assistant_id"] = assistantId ?? ""
                    };

                    var runJsonContent = JsonSerializer.Serialize(runData);
                    using var runRequest = new HttpRequestMessage(HttpMethod.Post, runUrl)
                    {
                        Content = new StringContent(runJsonContent, Encoding.UTF8, "application/json")
                    };
                    
                    runRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
                    runRequest.Headers.Add("OpenAI-Beta", "assistants=v2");

                    var runResponse = _client.Send(runRequest);
                    var runContent = runResponse.Content.ReadAsStringAsync().Result;

                    if (!runResponse.IsSuccessStatusCode)
                    {
                        return $"Error creating run: {runResponse.StatusCode}: {runContent}";
                    }

                    var runResponseData = JsonSerializer.Deserialize<JsonElement>(runContent);
                    var runId = runResponseData.GetProperty("id").GetString();

                    // Poll for completion
                    var maxAttempts = 30; // 30 seconds
                    for (int i = 0; i < maxAttempts; i++)
                    {
                        Thread.Sleep(1000); // Wait 1 second

                        var statusUrl = $"https://api.openai.com/v1/threads/{threadId}/runs/{runId}";
                        using var statusRequest = new HttpRequestMessage(HttpMethod.Get, statusUrl);
                        statusRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
                        statusRequest.Headers.Add("OpenAI-Beta", "assistants=v2");

                        var statusResponse = _client.Send(statusRequest);
                        var statusContent = statusResponse.Content.ReadAsStringAsync().Result;

                        if (statusResponse.IsSuccessStatusCode)
                        {
                            var statusData = JsonSerializer.Deserialize<JsonElement>(statusContent);
                            var status = statusData.GetProperty("status").GetString();

                            if (status == "completed")
                            {
                                // Get the messages
                                var messagesUrl = $"https://api.openai.com/v1/threads/{threadId}/messages";
                                using var messagesRequest = new HttpRequestMessage(HttpMethod.Get, messagesUrl);
                                messagesRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
                                messagesRequest.Headers.Add("OpenAI-Beta", "assistants=v2");

                                var messagesResponse = _client.Send(messagesRequest);
                                var messagesContent = messagesResponse.Content.ReadAsStringAsync().Result;

                                if (messagesResponse.IsSuccessStatusCode)
                                {
                                    var messagesData = JsonSerializer.Deserialize<JsonElement>(messagesContent);
                                    var messages = messagesData.GetProperty("data").EnumerateArray();
                                    
                                    var assistantMessage = messages.FirstOrDefault(m => 
                                        m.GetProperty("role").GetString() == "assistant");

                                    if (assistantMessage.ValueKind != JsonValueKind.Undefined)
                                    {
                                        var content = assistantMessage.GetProperty("content").EnumerateArray().First();
                                        var text = content.GetProperty("text").GetProperty("value").GetString();
                                        return $"Query result from vector store:\n\n{text}";
                                    }
                                }
                                break;
                            }
                            else if (status == "failed" || status == "cancelled" || status == "expired")
                            {
                                return $"Run failed with status: {status}";
                            }
                        }
                    }

                    return "Query timed out. The assistant may still be processing.";
                }
                finally
                {
                    // Clean up thread (optional, as it will be cleaned up by OpenAI eventually)
                }
            }
            finally
            {
                // Clean up assistant
                var deleteAssistantUrl = $"https://api.openai.com/v1/assistants/{assistantId}";
                using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, deleteAssistantUrl);
                deleteRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
                deleteRequest.Headers.Add("OpenAI-Beta", "assistants=v2");
                
                _client.Send(deleteRequest); // Fire and forget cleanup
            }
        }
        catch (Exception ex)
        {
            return $"Error querying vector store: {ex.Message}";
        }
    }

    [McpServerTool(Name = "openai_list_vector_stores"), Description("List all vector stores in the OpenAI account.")]
    [RequiresApproval]
    public static string ListVectorStores(int limit = 20)
    {
        var args = new Dictionary<string, object?>
        {
            ["limit"] = limit
        };

        var approved = ToolApprovalManager.Instance.EnsureApproved("openai_list_vector_stores", args);
        if (!approved)
        {
            return "Error: Tool execution was denied by approval system.";
        }

        try
        {
            var apiKey = ApiCredentialsManager.Instance.GetOpenAiApiKey();
            if (string.IsNullOrEmpty(apiKey))
            {
                return "Error: OpenAI API key not configured. Please set OPENAI_API_KEY environment variable.";
            }
            var url = $"https://api.openai.com/v1/vector_stores?limit={limit}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            
            request.Headers.Add("Authorization", $"Bearer {apiKey}");
            request.Headers.Add("OpenAI-Beta", "assistants=v2");

            var response = _client.Send(request);
            var content = response.Content.ReadAsStringAsync().Result;

            if (!response.IsSuccessStatusCode)
            {
                return $"Error: OpenAI API returned {response.StatusCode}: {content}";
            }

            var responseData = JsonSerializer.Deserialize<JsonElement>(content);
            var vectorStores = responseData.GetProperty("data").EnumerateArray();
            
            var result = new StringBuilder();
            result.AppendLine("Vector Stores:");
            result.AppendLine();

            foreach (var store in vectorStores)
            {
                var id = store.GetProperty("id").GetString();
                var name = store.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : "Unnamed";
                var status = store.GetProperty("status").GetString();
                var createdAt = store.GetProperty("created_at").GetInt64();
                var fileCount = store.GetProperty("file_counts").GetProperty("total").GetInt32();

                var createdDate = DateTimeOffset.FromUnixTimeSeconds(createdAt).ToString("yyyy-MM-dd HH:mm:ss");

                result.AppendLine($"📁 {name} (ID: {id})");
                result.AppendLine($"   Status: {status}");
                result.AppendLine($"   Files: {fileCount}");
                result.AppendLine($"   Created: {createdDate}");
                result.AppendLine();
            }

            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"Error listing vector stores: {ex.Message}";
        }
    }

    [McpServerTool(Name = "openai_delete_vector_store"), Description("Delete a vector store.")]
    [RequiresApproval]
    public static string DeleteVectorStore(string vectorStoreId)
    {
        var args = new Dictionary<string, object?>
        {
            ["vectorStoreId"] = vectorStoreId
        };

        var approved = ToolApprovalManager.Instance.EnsureApproved("openai_delete_vector_store", args);
        if (!approved)
        {
            return "Error: Tool execution was denied by approval system.";
        }

        try
        {
            var apiKey = ApiCredentialsManager.Instance.GetOpenAiApiKey();
            if (string.IsNullOrEmpty(apiKey))
            {
                return "Error: OpenAI API key not configured. Please set OPENAI_API_KEY environment variable.";
            }
            
            var url = $"https://api.openai.com/v1/vector_stores/{vectorStoreId}";
            using var request = new HttpRequestMessage(HttpMethod.Delete, url);
            
            request.Headers.Add("Authorization", $"Bearer {apiKey}");
            request.Headers.Add("OpenAI-Beta", "assistants=v2");

            var response = _client.Send(request);
            var content = response.Content.ReadAsStringAsync().Result;

            if (!response.IsSuccessStatusCode)
            {
                return $"Error: OpenAI API returned {response.StatusCode}: {content}";
            }

            var responseData = JsonSerializer.Deserialize<JsonElement>(content);
            var deleted = responseData.GetProperty("deleted").GetBoolean();

            return deleted 
                ? $"Successfully deleted vector store: {vectorStoreId}"
                : $"Failed to delete vector store: {vectorStoreId}";
        }
        catch (Exception ex)
        {
            return $"Error deleting vector store: {ex.Message}";
        }
    }
}