# Pull Request Review Tools

This document describes the new MCP tools added to support pull request reviews from GitHub and Azure DevOps, including AI-powered code analysis using OpenAI Vector Stores.

## Overview

The pull request review tools enable AgentAlpha to:
- Fetch pull request details and changes from GitHub and Azure DevOps
- Analyze code changes for quality, security, and best practices
- Generate structured review comments
- Use AI-powered analysis with OpenAI Vector Stores for enhanced insights
- Provide comprehensive review reports

## Tool Categories

### 1. GitHub Tools (`GitHubTools`)

Tools for interacting with GitHub's REST API for pull request reviews.

#### `github_get_pull_request`
Get detailed information about a specific pull request.

**Parameters:**
- `owner` (string): Repository owner/organization
- `repo` (string): Repository name
- `pullNumber` (int): Pull request number
- `token` (string, optional): GitHub personal access token

**Example:**
```
github_get_pull_request owner=microsoft repo=vscode pullNumber=123 token=ghp_xxx
```

#### `github_get_pull_request_files`
Get list of files changed in a pull request with change statistics.

**Parameters:**
- `owner` (string): Repository owner/organization
- `repo` (string): Repository name
- `pullNumber` (int): Pull request number
- `token` (string, optional): GitHub personal access token

#### `github_get_pull_request_diff`
Get the full diff content of a pull request.

**Parameters:**
- `owner` (string): Repository owner/organization
- `repo` (string): Repository name
- `pullNumber` (int): Pull request number
- `token` (string, optional): GitHub personal access token

#### `github_get_pull_request_comments`
Get all comments on a pull request.

**Parameters:**
- `owner` (string): Repository owner/organization
- `repo` (string): Repository name
- `pullNumber` (int): Pull request number
- `token` (string, optional): GitHub personal access token

#### `github_list_pull_requests`
List pull requests in a repository.

**Parameters:**
- `owner` (string): Repository owner/organization
- `repo` (string): Repository name
- `state` (string, default="open"): PR state (open, closed, all)
- `page` (int, default=1): Page number for pagination
- `perPage` (int, default=10): Results per page
- `token` (string, optional): GitHub personal access token

#### `github_post_pull_request_comment`
Post a comment on a pull request.

**Parameters:**
- `owner` (string): Repository owner/organization
- `repo` (string): Repository name
- `pullNumber` (int): Pull request number
- `body` (string): Comment content
- `token` (string): GitHub personal access token (required for posting)
- `path` (string, optional): File path for line-specific comments
- `line` (int, optional): Line number for line-specific comments
- `side` (string, default="RIGHT"): Side of diff (LEFT, RIGHT)

### 2. Azure DevOps Tools (`AzureDevOpsTools`)

Tools for interacting with Azure DevOps REST API for pull request reviews.

#### `azdo_get_pull_request`
Get detailed information about a specific pull request.

**Parameters:**
- `organization` (string): Azure DevOps organization
- `project` (string): Project name
- `repository` (string): Repository name
- `pullRequestId` (int): Pull request ID
- `token` (string): Personal access token

#### `azdo_get_pull_request_commits`
Get commits in a pull request.

**Parameters:**
- `organization` (string): Azure DevOps organization
- `project` (string): Project name
- `repository` (string): Repository name
- `pullRequestId` (int): Pull request ID
- `token` (string): Personal access token

#### `azdo_get_pull_request_changes`
Get file changes in a pull request.

**Parameters:**
- `organization` (string): Azure DevOps organization
- `project` (string): Project name
- `repository` (string): Repository name
- `pullRequestId` (int): Pull request ID
- `token` (string): Personal access token

#### `azdo_get_pull_request_threads`
Get comment threads on a pull request.

**Parameters:**
- `organization` (string): Azure DevOps organization
- `project` (string): Project name
- `repository` (string): Repository name
- `pullRequestId` (int): Pull request ID
- `token` (string): Personal access token

#### `azdo_list_pull_requests`
List pull requests in a repository.

**Parameters:**
- `organization` (string): Azure DevOps organization
- `project` (string): Project name
- `repository` (string): Repository name
- `status` (string, default="active"): PR status (active, completed, etc.)
- `top` (int, default=10): Number of results to return
- `token` (string): Personal access token

#### `azdo_post_pull_request_comment`
Post a comment thread on a pull request.

**Parameters:**
- `organization` (string): Azure DevOps organization
- `project` (string): Project name
- `repository` (string): Repository name
- `pullRequestId` (int): Pull request ID
- `content` (string): Comment content
- `token` (string): Personal access token
- `filePath` (string, optional): File path for file-specific comments
- `line` (int, optional): Line number for line-specific comments

### 3. OpenAI Vector Store Tools (`OpenAIVectorStoreTools`)

Tools for managing OpenAI Vector Stores to enhance code analysis with AI.

#### `openai_create_vector_store`
Create a new vector store for code analysis.

**Parameters:**
- `name` (string): Vector store name
- `apiKey` (string): OpenAI API key
- `expiresAfterDays` (int, optional): Expiration time in days
- `metadata` (string, optional): JSON metadata or description

#### `openai_upload_file_to_vector_store`
Upload a file to a vector store for analysis.

**Parameters:**
- `vectorStoreId` (string): Vector store ID
- `filePath` (string): Path to file to upload
- `apiKey` (string): OpenAI API key
- `purpose` (string, default="assistants"): File purpose

#### `openai_query_vector_store`
Query a vector store for code analysis insights.

**Parameters:**
- `vectorStoreId` (string): Vector store ID
- `query` (string): Analysis query
- `apiKey` (string): OpenAI API key
- `maxResults` (int, default=5): Maximum results to return
- `assistantInstructions` (string, optional): Custom assistant instructions

#### `openai_list_vector_stores`
List all vector stores in the account.

**Parameters:**
- `apiKey` (string): OpenAI API key
- `limit` (int, default=20): Maximum results to return

#### `openai_delete_vector_store`
Delete a vector store.

**Parameters:**
- `vectorStoreId` (string): Vector store ID to delete
- `apiKey` (string): OpenAI API key

### 4. Code Review Tools (`CodeReviewTools`)

High-level tools that combine multiple data sources for comprehensive code review.

#### `analyze_pull_request_for_review`
Comprehensive pull request analysis combining platform data with AI insights.

**Parameters:**
- `platform` (string): Platform type ("github" or "azuredevops")
- `organization` (string): Organization/owner name
- `project` (string): Project name (repo name for GitHub)
- `repository` (string): Repository name
- `pullRequestId` (int): Pull request ID/number
- `token` (string): Platform access token
- `openaiApiKey` (string, optional): OpenAI API key for AI analysis
- `vectorStoreId` (string, optional): Vector store ID for enhanced analysis

**Returns:**
- Pull request details
- File changes analysis
- Existing comments
- Code quality analysis
- AI-powered insights (if OpenAI credentials provided)
- Review recommendations

#### `generate_review_comment`
Generate a structured review comment for specific issues.

**Parameters:**
- `filePath` (string): File path where the issue is located
- `codeSnippet` (string): Relevant code snippet
- `issueType` (string): Type of issue (bug, performance, style, security, maintainability)
- `description` (string): Issue description
- `suggestion` (string, optional): Suggested fix

#### `extract_code_patterns`
Extract and analyze code patterns from pull request changes.

**Parameters:**
- `prFilesContent` (string): Content from pull request file changes

**Returns:**
- Detected code patterns
- Security concerns
- Performance issues
- Best practice violations
- File extension analysis

## Usage Examples

### Basic GitHub PR Review
```
# Get PR details
github_get_pull_request owner=microsoft repo=vscode pullNumber=123

# Get file changes
github_get_pull_request_files owner=microsoft repo=vscode pullNumber=123

# Get existing comments
github_get_pull_request_comments owner=microsoft repo=vscode pullNumber=123
```

### Comprehensive Review Analysis
```
# Analyze entire PR with AI insights
analyze_pull_request_for_review platform=github organization=microsoft project=vscode repository=vscode pullRequestId=123 token=ghp_xxx openaiApiKey=sk-xxx vectorStoreId=vs_xxx
```

### Azure DevOps Review
```
# Get Azure DevOps PR details
azdo_get_pull_request organization=myorg project=myproject repository=myrepo pullRequestId=456 token=xxx

# Get changes
azdo_get_pull_request_changes organization=myorg project=myproject repository=myrepo pullRequestId=456 token=xxx
```

### Vector Store Setup
```
# Create vector store for codebase analysis
openai_create_vector_store name="MyProject Code Analysis" apiKey=sk-xxx

# Upload files for analysis
openai_upload_file_to_vector_store vectorStoreId=vs_xxx filePath=/path/to/code.cs apiKey=sk-xxx

# Query for insights
openai_query_vector_store vectorStoreId=vs_xxx query="Analyze this pull request for security issues" apiKey=sk-xxx
```

## Security Considerations

- All tools require approval before execution (external API calls)
- API tokens are masked in logs and approval prompts
- No sensitive data is stored permanently
- Vector stores are cleaned up automatically when possible
- All network requests have timeouts to prevent hanging

## Configuration

Tools are automatically registered when the MCP server starts. No additional configuration is required beyond having the tools available.

## Authentication

- **GitHub**: Use personal access tokens with appropriate repository permissions
- **Azure DevOps**: Use personal access tokens with Code (read/write) permissions
- **OpenAI**: Use API keys with access to Vector Stores and Assistants API

## Error Handling

All tools include comprehensive error handling:
- API rate limiting detection
- Network timeout handling
- Invalid response format handling
- Missing permissions detection
- Graceful degradation when optional features are unavailable

## Limitations

- GitHub API rate limits apply (typically 5000 requests/hour for authenticated users)
- Azure DevOps API rate limits apply
- OpenAI API usage costs apply for vector store operations
- Large pull requests may hit API response size limits
- Some features require specific API permissions

## Future Enhancements

- Support for GitLab pull/merge requests
- Bitbucket pull request support
- Advanced code pattern detection
- Integration with static analysis tools
- Automated review posting based on analysis results
- Custom review templates and rules