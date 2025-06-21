# MCP Server Transport Modes: A Critical Comparison

> Based on the official [MCP C# SDK v1.1.0](https://csharp.sdk.modelcontextprotocol.io/) documentation, the [MCP protocol specification (2025-11-25)](https://modelcontextprotocol.io/specification/2025-11-25/basic/transports), and community experience. Last updated: March 2026.

## Overview

The MCP protocol defines two standard transport mechanisms, plus the C# SDK adds additional in-process options. The protocol itself is transport-agnostic—any channel that supports bidirectional JSON-RPC message exchange can work.

| Transport | Package Required | Process Model | Status |
|---|---|---|---|
| **stdio** | `ModelContextProtocol` | Child process | Standard, recommended for local |
| **Streamable HTTP** | `ModelContextProtocol.AspNetCore` | Remote HTTP server | Standard, recommended for remote |
| **SSE (HTTP+SSE)** | `ModelContextProtocol.AspNetCore` | Remote HTTP server | **Legacy**, superseded by Streamable HTTP |
| **In-Memory (Stream)** | `ModelContextProtocol.Core` | In-process | SDK-only, for testing/embedding |

---

## 1. stdio Transport

### How it works

The client launches the MCP server as a **child subprocess**. Communication happens over the process's `stdin` (client→server) and `stdout` (server→client). Messages are newline-delimited JSON-RPC. The server can write logs to `stderr`.

### C# SDK setup

**Server:**
```csharp
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();
await builder.Build().RunAsync();
```

**Client:**
```csharp
var transport = new StdioClientTransport(new StdioClientTransportOptions
{
    Command = "dotnet",
    Arguments = ["run", "--project", "MyServer"],
    ShutdownTimeout = TimeSpan.FromSeconds(10)
});
await using var client = await McpClient.CreateAsync(transport);
```

### Official tradeoffs

| Aspect | Detail |
|---|---|
| **Direction** | Fully bidirectional |
| **Session resumption** | N/A (process lifecycle = session lifecycle) |
| **Authentication** | Process-level (OS user context) |
| **Best for** | Local tools, CLI integrations |

### Critical analysis & community view

**Strengths:**
- **Simplest model.** No networking, no ports, no auth configuration. The client spawns the server, communicates via pipes, and kills it when done. Zero infrastructure.
- **Security by isolation.** The server runs with the client's OS permissions. No network surface to attack. No DNS rebinding. No CORS. This is why the MCP spec says "clients SHOULD support stdio whenever possible."
- **Universal client support.** Every MCP client (Claude Desktop, VS Code Copilot, Cursor, etc.) supports stdio. It's the baseline.
- **No port conflicts.** Multiple instances can coexist trivially since there's no shared port.

**Weaknesses:**
- **1:1 coupling.** One client = one server process. No sharing a server across multiple clients or applications. Every client spawns its own instance.
- **Local only.** The server must be on the same machine. No remote access without wrapping in SSH or similar.
- **Cold start cost.** Every connection spawns a new process. For .NET, this means JIT warmup on each launch (mitigated by AOT publishing, which the SDK supports).
- **No horizontal scaling.** Since the server is a child process, you can't put a load balancer in front of it or run multiple replicas.
- **Debugging difficulty.** You can't easily attach a debugger to a process that another tool spawned. Community workaround: log heavily to stderr or files (see the `TestServerWithHosting` sample with Serilog file logging).

**Community sentiment:** stdio is the pragmatic default. Most MCP servers in the wild (especially those used with Claude Desktop and VS Code) use stdio because it "just works" and client configuration is trivial — you just point to an executable. The overwhelming advice is: **start with stdio, only move to HTTP when you have a concrete reason** (multi-client, remote access, or you're building a hosted service).

---

## 2. Streamable HTTP Transport

### How it works

The server runs as a standalone HTTP server (ASP.NET Core) exposing a single MCP endpoint. Clients send JSON-RPC messages as `POST` requests. The server can respond with either a single JSON response (`application/json`) or open an SSE stream (`text/event-stream`) to stream multiple messages back. Clients can also `GET` the endpoint to open a persistent SSE stream for server-initiated messages.

This is the **current recommended transport for remote/network servers** as of protocol version 2025-11-25.

### C# SDK setup

**Server:**
```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();
var app = builder.Build();
app.MapMcp();           // Default route: /
// app.MapMcp("/mcp");  // Or custom route
app.Run();
```

**Client:**
```csharp
var transport = new HttpClientTransport(new HttpClientTransportOptions
{
    Endpoint = new Uri("https://my-server.example.com/mcp"),
    TransportMode = HttpTransportMode.StreamableHttp, // or AutoDetect (default)
});
await using var client = await McpClient.CreateAsync(transport);
```

### Official tradeoffs

| Aspect | Detail |
|---|---|
| **Direction** | Bidirectional (POST + optional SSE streaming) |
| **Session resumption** | ✓ Supported via `MCP-Session-Id` and `Last-Event-ID` |
| **Authentication** | HTTP auth (OAuth, API keys, custom headers) |
| **Best for** | Remote servers, multi-client scenarios, hosted services |

### Key features

- **Session management:** Server can assign an `MCP-Session-Id` header, enabling stateful sessions. Clients include this on all subsequent requests. Sessions can be resumed after disconnections.
- **Resumability:** SSE events can carry IDs, allowing clients to resume from the last received event using `Last-Event-ID`.
- **Flexible response:** Server chooses per-request whether to respond with a single JSON object or open an SSE stream — enabling both simple request/response and complex streaming patterns.
- **Backward compatibility:** `MapMcp()` automatically serves both Streamable HTTP and legacy SSE endpoints (`{route}/sse` and `{route}/message`).

### Critical analysis & community view

**Strengths:**
- **Multi-client support.** One server instance serves many clients simultaneously. This is the unlock for shared infrastructure — deploy an MCP server once, connect from many agents/tools.
- **Remote access.** The server can be on a different machine, in the cloud, behind a reverse proxy.
- **Session resumption.** Survives network blips. The client can reconnect and pick up where it left off. This matters for long-running tool operations.
- **Full ASP.NET Core ecosystem.** You get middleware, DI, OpenTelemetry, health checks, rate limiting, authentication — the entire ASP.NET Core stack. The `AspNetCoreMcpServer` sample demonstrates OpenTelemetry tracing/metrics integration.
- **Scalability.** Can be load-balanced, containerized, deployed to Kubernetes, etc.

**Weaknesses:**
- **Complexity.** You now need to think about ports, TLS, authentication, CORS, DNS rebinding protections. The spec explicitly warns about DNS rebinding attacks when running locally and requires `Origin` header validation.
- **Infrastructure overhead.** You need to host and manage a web server. For a simple local tool, this is massive overkill.
- **Security surface.** Any HTTP endpoint is a potential attack vector. The spec mandates: validate `Origin` headers, bind to localhost when running locally, implement proper auth.
- **Client support varies.** Not all MCP clients support HTTP transports yet. stdio is universal; Streamable HTTP is newer and still being adopted.
- **Per-session state management.** If your server is stateful (per-session DI scopes, per-session tools), this is your responsibility to manage. The SDK's `AspNetCoreMcpPerSessionTools` sample shows how, but it's non-trivial.

**Community sentiment:** Streamable HTTP is viewed as the "production" transport. People reach for it when building MCP servers that will be shared across teams or deployed as services. The main complaint is that it's newer and some clients still don't support it well — particularly older MCP client libraries that only speak SSE. The auto-detection fallback (`HttpTransportMode.AutoDetect`) is praised for smoothing this transition. There's growing enthusiasm around using it for "MCP as a service" patterns where organizations deploy centralized tool servers.

---

## 3. SSE Transport (Legacy)

### How it works

The original HTTP transport from protocol version 2024-11-05. Uses a dedicated SSE endpoint (`/sse`) for server→client streaming and a separate POST endpoint (`/message`) for client→server messages. The SSE connection must be established first, and the server sends a special `endpoint` event telling the client where to POST.

### C# SDK setup

**Server:** Same as Streamable HTTP — `MapMcp()` serves both automatically. No separate configuration needed.

**Client:**
```csharp
var transport = new HttpClientTransport(new HttpClientTransportOptions
{
    Endpoint = new Uri("https://my-server.example.com/sse"),
    TransportMode = HttpTransportMode.Sse,
    MaxReconnectionAttempts = 5,
    DefaultReconnectionInterval = TimeSpan.FromSeconds(1)
});
await using var client = await McpClient.CreateAsync(transport);
```

### Official tradeoffs

| Aspect | Detail |
|---|---|
| **Direction** | Server→client stream + client→server POST (asymmetric) |
| **Session resumption** | ✗ Not supported |
| **Authentication** | HTTP auth (OAuth, headers) |
| **Best for** | **Legacy compatibility only** |

### Critical analysis & community view

**Strengths:**
- **Wide existing support.** Many MCP clients and servers in the wild still use SSE because it was the original HTTP transport. If you're connecting to older servers, you may need it.
- **Free on the server side.** In the C# SDK, `MapMcp()` serves SSE automatically alongside Streamable HTTP. Zero extra work to support legacy clients.

**Weaknesses:**
- **Officially deprecated.** The SDK docs explicitly state: "The SSE transport is considered legacy. The Streamable HTTP transport is the recommended approach." New implementations should not target SSE.
- **No session resumption.** If the SSE connection drops, you lose state and must reconnect from scratch.
- **Asymmetric design.** Two separate endpoints for two directions is architecturally messy. The Streamable HTTP transport unifies this into a single endpoint.
- **Long-lived connections.** The SSE stream must stay open, which consumes server resources and doesn't play well with load balancers that have idle timeouts.
- **Two-endpoint complexity.** The client must first GET to establish SSE, receive the POST endpoint dynamically, then send messages there. This is a more fragile handshake than Streamable HTTP's single-endpoint model.

**Community sentiment:** SSE is dying but not dead. It served its purpose as the original HTTP transport and is being actively replaced by Streamable HTTP. The C# SDK's approach of serving both from the same `MapMcp()` call is well-received — it means you support legacy clients for free without dual configuration. The consensus is: **don't build new servers targeting SSE, but don't worry about supporting SSE clients because the server handles it automatically.**

---

## 4. In-Memory / Stream Transport (SDK-only)

### How it works

Not part of the MCP specification. The C# SDK provides `StreamServerTransport` and `StreamClientTransport` that communicate over any `Stream` — including in-memory pipes. Client and server run in the same process.

### C# SDK setup

```csharp
Pipe clientToServerPipe = new(), serverToClientPipe = new();

// Server
await using McpServer server = McpServer.Create(
    new StreamServerTransport(
        clientToServerPipe.Reader.AsStream(),
        serverToClientPipe.Writer.AsStream()),
    new McpServerOptions()
    {
        ToolCollection = [McpServerTool.Create(
            (string arg) => $"Echo: {arg}", new() { Name = "Echo" })]
    });
_ = server.RunAsync();

// Client (same process)
await using McpClient client = await McpClient.CreateAsync(
    new StreamClientTransport(
        clientToServerPipe.Writer.AsStream(),
        serverToClientPipe.Reader.AsStream()));
```

### Critical analysis & community view

**Strengths:**
- **Testing.** This is the primary use case. You can write integration tests for your MCP server without spawning processes or opening ports.
- **Embedding.** If your application IS the client, you can embed the MCP server directly without any process or network overhead.
- **Zero latency.** No serialization overhead beyond JSON-RPC, no network hops.

**Weaknesses:**
- **Non-standard.** Not part of the MCP specification. Other SDKs may not have an equivalent.
- **Single-process only.** No remote access, no multi-client.
- **Limited use cases.** Really only useful for testing and in-process embedding.

**Community sentiment:** Appreciated as a testing utility. People use it to write fast, reliable integration tests for MCP tools without the flakiness of process management or networking. Not discussed much beyond that context.

---

## 5. Streamable HTTP: Authentication Deep Dive

The Streamable HTTP transport is the only transport where authentication matters at the protocol level. stdio runs as a child process (inheriting OS permissions), and in-memory is in-process. But HTTP servers face the open network — so how do you protect them?

### What the MCP spec says about auth

The MCP specification (2025-11-25) defines an authorization model based on:

1. **OAuth 2.0 Protected Resource Metadata** ([RFC 9728](https://datatracker.ietf.org/doc/rfc9728/)): The MCP server advertises itself as a protected resource at `/.well-known/oauth-protected-resource`. This document tells clients which authorization server to use and what scopes are available.

2. **OAuth 2.0 Resource Indicators** ([RFC 8707](https://datatracker.ietf.org/doc/rfc8707/)): The access token's `aud` (audience) claim should match the MCP server's resource URL, preventing token misuse across services.

3. **Dynamic Client Registration** ([RFC 7591](https://datatracker.ietf.org/doc/rfc7591/)): MCP clients can register themselves with the authorization server on-the-fly, which is important because MCP clients are diverse (Claude Desktop, VS Code, custom agents) and can't all be pre-registered.

4. **Standard OAuth 2.0 flows**: Authorization code + PKCE for interactive users, client credentials for service-to-service.

The key insight: **the MCP server is NOT the authorization server.** The MCP server is a _resource server_ that _validates_ tokens issued by an external authorization server. This is the standard OAuth 2.0 pattern.

### How the C# SDK implements auth

The SDK provides three building blocks in `ModelContextProtocol.AspNetCore`:

#### 1. `AddMcp()` — Protected Resource Metadata

```csharp
builder.Services.AddAuthentication(options =>
{
    options.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.Authority = "https://login.microsoftonline.com/{tenant-id}/v2.0";
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidAudience = "api://your-mcp-server-client-id",
        ValidIssuer = "https://login.microsoftonline.com/{tenant-id}/v2.0",
    };
})
.AddMcp(options =>
{
    options.ResourceMetadata = new()
    {
        AuthorizationServers = { "https://login.microsoftonline.com/{tenant-id}/v2.0" },
        ScopesSupported = ["mcp:tools"],
        ResourceDocumentation = "https://docs.example.com/mcp",
    };
});
```

This serves the `/.well-known/oauth-protected-resource` endpoint automatically. When an unauthenticated client hits the MCP endpoint, the `McpAuth` challenge scheme returns a `401` with a `WWW-Authenticate` header that directs the client to discover the protected resource metadata, find the authorization server, and acquire a token.

#### 2. `RequireAuthorization()` — Protecting the endpoint

```csharp
app.UseAuthentication();
app.UseAuthorization();
app.MapMcp().RequireAuthorization();
```

Standard ASP.NET Core authorization. Every MCP request must carry a valid Bearer token.

#### 3. Client-side OAuth flow

The C# SDK client supports OAuth out of the box:

```csharp
var transport = new HttpClientTransport(new()
{
    Endpoint = new Uri("https://my-mcp-server.example.com/mcp"),
    OAuth = new()
    {
        RedirectUri = new Uri("http://localhost:1179/callback"),
        AuthorizationRedirectDelegate = HandleAuthorizationUrlAsync,
        DynamicClientRegistration = new()
        {
            ClientName = "MyMcpClient",
        },
    }
});
```

The `AuthorizationRedirectDelegate` is a callback you provide — it receives the authorization URL, opens a browser for user consent, spins up a local HTTP listener to capture the redirect, and returns the auth code. The SDK handles token exchange, caching, and refresh.

### The On-Behalf-Of (OBO) pattern with Azure Entra

This is the scenario: you have an MCP server that acts as a **middle-tier API**. A user authorizes the MCP client (e.g., an AI agent) to call the MCP server. The MCP server then needs to call a **downstream API** (e.g., Microsoft Graph, your own Azure API, a third-party service) _on behalf of that same user_.

```
┌──────────────┐     Token A      ┌──────────────┐     Token B      ┌──────────────┐
│   MCP Client │ ──────────────── │  MCP Server  │ ──────────────── │ Downstream   │
│ (AI Agent /  │   (aud: MCP)     │ (Middle-Tier)│   (aud: API)     │ API (e.g.    │
│  VS Code)    │                  │              │                  │ Graph, your  │
└──────────────┘                  └──────────────┘                  │ service)     │
       │                                 │                          └──────────────┘
       │  1. User logs in               │  2. Exchange Token A
       │     via browser                │     for Token B via OBO
       ▼                                ▼
┌──────────────────────────────────────────────────────────────────┐
│                    Azure Entra ID (Authorization Server)         │
│                    login.microsoftonline.com/{tenant}/v2.0       │
└──────────────────────────────────────────────────────────────────┘
```

**The flow:**

1. **User authenticates** with the MCP client → gets Token A (audience = MCP server)
2. **MCP client calls MCP server** with Token A in the `Authorization: Bearer` header
3. **MCP server validates Token A** (standard JWT validation)
4. **MCP server exchanges Token A for Token B** using the OAuth 2.0 OBO grant, requesting scopes for the downstream API
5. **MCP server calls downstream API** with Token B
6. **Downstream API sees the user's identity** in Token B — it's as if the user called directly

### What makes the Token A → Token B exchange possible?

Step 4 is the magic — and the most misunderstood part. The MCP server can't just hand any token to Entra and get a new one back. Three things must be true for the exchange to work:

#### Requirement 1: The MCP Server must prove its own identity (it's a "confidential client")

The OBO token exchange is an HTTP POST to Entra's token endpoint:

```
POST https://login.microsoftonline.com/{tenant}/oauth2/v2.0/token

grant_type=urn:ietf:params:oauth:grant-type:jwt-bearer
&client_id=<MCP Server's client ID>
&client_secret=<MCP Server's secret>        ← THIS
&assertion=<Token A>                         ← the user's incoming token
&scope=https://graph.microsoft.com/User.Read
&requested_token_use=on_behalf_of
```

Notice `client_secret` (or a `client_assertion` for certificate-based auth). **The MCP server must authenticate itself to Entra** — proving it is who it claims to be. This is why the MCP Server app registration needs a client secret or certificate. Without this credential, Entra refuses the exchange. 

This is by design: OBO only works for **confidential clients** (servers that can keep a secret). A browser SPA cannot do OBO because it can't securely store a secret.

#### Requirement 2: The MCP Server must have delegated API permissions declared

In the MCP Server's app registration, under **API permissions**, you must add the downstream API's **delegated** permissions. For example:

- **Microsoft Graph → Delegated → User.Read** (to read the user's profile)
- **Microsoft Graph → Delegated → Mail.Read** (to read the user's email)
- **Your Custom API → Delegated → Projects.Read** (a scope name you defined under Expose an API → Add a scope on your downstream API's app registration)

These must be _delegated_ permissions (not application permissions). Application permissions represent the app acting as itself; delegated permissions represent the app acting on behalf of a user — which is exactly what OBO does.

**This step does NOT grant the MCP server access by itself.** It declares _intent_: "this application may need these permissions when acting on behalf of a user." The actual grant comes from consent.

#### Requirement 3: Someone must consent to the downstream permissions

This is where it gets interesting. There are **two consent boundaries** in the OBO chain, and both must be satisfied:

**Consent Boundary 1: User → MCP Client → MCP Server**
- The user must consent to the MCP Client accessing the MCP Server's `access_as_user` scope
- This happens when the user logs in via the browser and sees "MCP Client wants to access MCP Server API on your behalf"

**Consent Boundary 2: MCP Server → Downstream API**
- Someone must consent to the MCP Server accessing the downstream API's scopes (e.g., `User.Read` on Graph)
- This is the part most people miss

There are three ways to satisfy Consent Boundary 2:

| Method | How | When to use |
|---|---|---|
| **Admin consent** | A tenant admin clicks "Grant admin consent" on the MCP Server's API permissions page in Entra | Most common for enterprise. Admin approves once, all users in the tenant are covered. No user-facing consent prompt for downstream permissions. |
| **Combined consent** (`knownClientApplications`) | Add the MCP Client's client ID to the MCP Server's `knownClientApplications` manifest property. When the user logs in and the client requests `.default` scope, Entra shows a _single combined consent screen_ listing permissions for both the client→server AND server→downstream API. | Best UX — user sees one consent prompt covering the entire chain. Requires manifest editing. |
| **Incremental/dynamic consent** | The first time the OBO exchange happens and the user hasn't consented, Entra returns an `interaction_required` error. The MCP server must send this back to the client, which re-prompts the user. | Fallback mechanism. Worse UX — user might see multiple consent prompts. Some MCP clients may not handle this well. |

**In practice, most enterprise deployments use admin consent.** An Entra admin goes to the MCP Server's app registration → API permissions → clicks "Grant admin consent for [tenant]." Done. Every user in the tenant can now use OBO without seeing any consent screen for downstream permissions. They'll still see a consent screen for the MCP Client→MCP Server relationship (unless that's also admin-consented or pre-authorized).

#### Summary: What does the MCP Server "need"?

| Requirement | Where it's configured | Who does it |
|---|---|---|
| **Client secret or certificate** | MCP Server app registration → Certificates & secrets | Developer |
| **Delegated API permissions declared** | MCP Server app registration → API permissions → Add (e.g., Graph `User.Read`) | Developer |
| **Consent for downstream API** | MCP Server app registration → API permissions → "Grant admin consent" | Tenant admin (or user via combined/incremental consent) |
| **Incoming token has correct audience** | MCP Server app registration → Expose an API → Application ID URI (must match Token A's `aud`) | Developer |
| **An actual user token** | Token A must be a _user_ token, not an app-only token. OBO only works with delegated (user) tokens. | Enforced by Entra |

The MCP Server does **not** need tenant-level admin roles or special tenant privileges for itself. It needs:
1. Its own credential (secret/cert) — so it can authenticate to Entra
2. Delegated permissions declared — so Entra knows what scopes it might request via OBO  
3. Consent — so Entra knows someone authorized those permissions

Think of it this way: the MCP Server is saying to Entra, _"This user gave me this token. I have my own credentials proving I'm a legitimate app. The user (or an admin) has consented to me accessing Graph on their behalf. Please give me a token for Graph."_ Entra verifies all three conditions and issues Token B.

### Concrete examples: The three consent scenarios

Let's walk through the same MCP server in three different scenarios. The server exposes a `GetMyEmails` tool that calls Microsoft Graph's `Mail.Read` endpoint on behalf of the user.

#### Prerequisites: The `.default` scope and custom delegated permissions

Before diving into the scenarios, two foundational concepts need to be clear — especially since Scenario B depends on both.

##### The `.default` meta-scope

The `.default` scope is a special scope in Microsoft Identity Platform. It is not a permission itself — it's a **meta-scope** that resolves to actual permissions at token issuance time.

When a client requests `api://mcp-server-app-id/.default`, it means: _"Give me all the permissions that have been statically configured (and consented to) for this client → this resource combination."_

| | Named scopes | `.default` |
|---|---|---|
| **Request example** | `scope=api://mcp-server/access_as_user` | `scope=api://mcp-server/.default` |
| **What you get** | Only the specific scope(s) requested | All statically assigned & consented permissions |
| **Incremental consent** | Yes — request new scopes over time | No — all-or-nothing based on what's configured |
| **Combined consent** | No | Yes — triggers the combined consent UI when `knownClientApplications` is configured |
| **Client credentials flow** | Not supported | **Required** — the only way to request permissions in client credentials |

**Why `.default` matters for Scenario B:** Combined consent only triggers when the client requests `.default` and the client's app ID is in the MCP Server's `knownClientApplications`. If the client requests a named scope like `access_as_user` instead of `.default`, the user only sees consent for that one scope — the downstream permissions are not included, and the OBO call later fails with `AADSTS65001`.

**What ends up in the token:**

With `.default` (delegated):
```json
{ "scp": "access_as_user" }
```
All permissions configured and consented for that client-resource pair are included. With `.default` in client credentials (app-only):
```json
{ "roles": ["Mail.ReadWrite.All"] }
```
Application permissions use the `roles` claim, not `scp`.

##### Custom delegated permissions on downstream APIs

The examples in this document use Microsoft Graph permissions like `User.Read` and `Mail.Read`, but **any API can define its own custom delegated permissions**. This is exactly what the downstream API team does when their API is not Microsoft Graph.

Custom scopes are defined in the downstream API's App Registration under **Expose an API** → **Add a scope**. Each scope has:

| Field | Purpose | Example |
|---|---|---|
| **Scope name** | The identifier used in code and token requests | `Projects.Read` |
| **Who can consent** | Whether users can consent, or only admins | "Admins and users" for Scenario B |
| **Admin consent display name** | What admins see | "Read user projects" |
| **Admin consent description** | Longer description for admins | "Allows the app to read the signed-in user's projects" |
| **User consent display name** | What users see in the consent prompt | "Read your projects" |
| **User consent description** | Longer description for users | "Allow this app to see your projects in Project Tracker" |

The full scope URI follows the pattern: `api://{client-id-of-downstream-api}/Projects.Read`

Custom scopes are **indistinguishable from Microsoft's first-party scopes** in the consent UI. Users see the display name and description — they can't tell if a scope was defined by Microsoft or by the downstream API team. The combined consent screen in Scenario B shows all scopes together:

```
"MCP AI Agent Client" would like to:

  ✅ Access MCP Project Tools as you          ← MCP Server scope
  ✅ Read your projects                       ← Custom downstream API scope
  ✅ Read your mail                           ← Microsoft Graph scope
```

---

#### Scenario A: Admin consent (the enterprise default)

**What the MCP server operator does (one-time setup):**

1. Creates the "MCP Server API" app registration in Entra
2. Adds a client secret
3. Goes to **Expose an API** → adds `access_as_user` scope
4. Goes to **API permissions** → clicks **Add a permission** → **Microsoft Graph** → **Delegated permissions** → checks `Mail.Read` → clicks **Add permissions**
5. The permissions page now shows:

   | Permission | Type | Status |
   |---|---|---|
   | Microsoft Graph / Mail.Read | Delegated | ⚠️ Not granted for Contoso |

6. The operator (who is also a tenant admin, or asks a tenant admin to do this) clicks **"Grant admin consent for Contoso"** → confirms → status changes to:

   | Permission | Type | Status |
   |---|---|---|
   | Microsoft Graph / Mail.Read | Delegated | ✅ Granted for Contoso |

7. Deploys the MCP server. Done.

**What the user experiences:**

1. User tells their AI agent: "Show me my latest emails"
2. The agent calls the MCP server's `GetMyEmails` tool
3. The MCP server returns `401` → the MCP client opens a browser
4. User sees the Azure Entra login page → signs in with their work account
5. User sees a **single consent screen**:

   > **"MCP Client" would like to:**
   > - Access MCP Server API on your behalf

   (Notice: **no mention of Mail.Read** — the admin already consented to that for the whole tenant)

6. User clicks "Accept"
7. Browser redirects back, shows "Authorized! You can close this tab."
8. The agent retries the tool call with the token → MCP server does OBO → gets Token B with Mail.Read → calls Graph → returns the emails
9. **Every subsequent tool call works without any prompts** until the token expires

**Why this is the most common:** In an enterprise, a tenant admin approves the MCP server's downstream permissions once. Hundreds of users can then use it without individually consenting to Mail.Read. The admin has visibility and control.

---

#### Scenario B: Combined consent via `knownClientApplications`

**What the MCP server operator does (one-time setup):**

Steps 1-4 same as Scenario A (register app, add secret, expose API, add Mail.Read delegated permission). But instead of step 5-6 (admin consent), the operator does something different:

5. Opens the MCP Server API's **Manifest** in the Entra portal (or uses the Microsoft Graph API)
6. Finds the `knownClientApplications` array and adds the MCP Client's application ID:

   ```json
   "knownClientApplications": [
       "11111111-2222-3333-4444-555555555555"  // MCP Client's app ID
   ]
   ```

7. Saves the manifest
8. Does **NOT** click "Grant admin consent" — leaves the permissions un-granted
9. Deploys the MCP server

**What the user experiences:**

1. User tells their AI agent: "Show me my latest emails"
2. Agent calls `GetMyEmails` → `401` → browser opens → user signs in
3. User sees a **combined consent screen** (this is the key difference):

   > **"MCP Client" would like to:**
   > - Access MCP Server API on your behalf
   > - Read your mail _(Microsoft Graph)_

   Both the client→server permission AND the server→Graph permission appear in one prompt, because the MCP Client is a `knownClientApplication` of the MCP Server.

4. User clicks "Accept" — this grants consent for _both_ boundaries at once
5. Browser redirects back → tool works → emails returned

**Why use this over admin consent:** Useful when you _don't_ have admin privileges in the tenant, or when you want each user to explicitly agree to the downstream permissions. Also useful in multi-tenant scenarios where you can't admin-consent in every customer's tenant — each user consents on first use.

**The catch:** If the MCP Client's client ID isn't in `knownClientApplications`, the combined consent won't work. And the client must request the `.default` scope (not individual scopes) to trigger the combined consent UI. Also, the `knownClientApplications` approach becomes hard to scale if you have many different MCP clients — you'd need to add each one's ID to the manifest.

---

#### Scenario C: Incremental consent (the fallback / worst UX)

**What the MCP server operator does:**

Steps 1-4 same as Scenario A. But:

5. The operator does **NOT** click admin consent
6. Does **NOT** configure `knownClientApplications`
7. Just deploys the server and hopes for the best

**What the user experiences:**

1. User tells their AI agent: "Show me my latest emails"
2. Agent calls `GetMyEmails` → `401` → browser opens → user signs in
3. User sees a consent screen:

   > **"MCP Client" would like to:**
   > - Access MCP Server API on your behalf

   (Only the client→server permission — nothing about Mail.Read)

4. User clicks "Accept" → browser redirects → client gets Token A → sends to MCP server
5. MCP server tries the OBO exchange: sends Token A + its credentials to Entra, requesting `Mail.Read` scope
6. **Entra returns an error:**

   ```json
   {
     "error": "interaction_required",
     "error_description": "AADSTS65001: The user or administrator has not consented to use the application with ID '...' named 'MCP Server API'. Send an interactive authorization request for this user and resource.",
     "suberror": "consent_required"
   }
   ```

7. Now the MCP server must surface this error back to the MCP client. In a well-built setup, this comes back as a 401 with a `WWW-Authenticate` header containing a `claims` challenge
8. The MCP client must handle this: it opens the browser _again_, this time with the claims challenge in the auth request
9. User sees a _second_ consent screen:

   > **"MCP Server API" would like to:**
   > - Read your mail _(Microsoft Graph)_

10. User clicks "Accept" (again) → gets a new token → client retries → OBO succeeds → emails returned

**Why this is the worst option:**
- **Two browser popups.** The user is interrupted twice. Some users will think something is broken.
- **MCP client must implement claims challenge handling.** Many MCP clients (especially early ones) don't handle `interaction_required` errors gracefully. They might just show "tool call failed."
- **Race condition risk.** If multiple tools trigger different downstream scope requirements, the user might see a cascade of consent prompts.

**When it actually happens:** This scenario is most common when a developer tests locally without admin consent, or when an MCP server is deployed to a new tenant where nobody has set up admin consent yet. It's a "it works but it's ugly" situation.

---

#### Side-by-side comparison

| | Scenario A: Admin Consent | Scenario B: Combined Consent | Scenario C: Incremental |
|---|---|---|---|
| **User sees consent prompts** | 1 (client→server only) | 1 (combined: client→server + server→downstream) | 2+ (separate prompts) |
| **Requires tenant admin** | Yes | No | No |
| **Operator setup effort** | Low (one click) | Medium (edit manifest) | None (but bad UX) |
| **Works for multi-tenant** | Only in tenants where admin consents | Yes (each user consents) | Yes (each user consents, twice) |
| **MCP client complexity** | Low | Low | High (must handle claims challenge) |
| **Recommended for** | Enterprise internal tools | SaaS / external-facing MCP servers | Never (fallback only) |

### Security deep dive: Can the MCP server operator act as any user?

This is the critical trust question when the MCP server team and the downstream API team are different organizations/teams. Let's be precise about what each scenario actually grants the MCP server operator.

#### The fundamental constraint: OBO requires a real user token

The MCP server **cannot initiate OBO on its own**. It needs Token A — a valid, non-expired access token that was issued to a specific user who actually authenticated via a browser. The MCP server operator cannot fabricate this token. They don't know the user's password, don't have their MFA device, and can't bypass the browser-based authentication.

This means: **the MCP server can only act on behalf of users who actively authenticate to the MCP client and whose tokens reach the MCP server.** It cannot "sweep" all users in a tenant.

However — and this is where it gets nuanced — once a user's token arrives at the MCP server, what the operator can do with it depends on the consent model.

#### Scenario A (Admin consent): The trust concern

When a tenant admin clicks "Grant admin consent" for the MCP Server's Mail.Read permission:

**What this actually grants:** Every user in the tenant who authenticates to the MCP Client will have their token eligible for OBO exchange to get Mail.Read — _without that individual user ever seeing or approving "Read your mail."_

**The risk:** The MCP server operator writes the code. They control what happens once Token A arrives. They could:
- Exchange it for a Graph token and read the user's mail — even if the specific MCP tool the user invoked has nothing to do with mail
- Log Token B and reuse it until it expires (typically 60-90 minutes)
- Call APIs beyond what the user intended, as long as the scopes were admin-consented

**The user's visibility:** The user sees one consent prompt saying "Access MCP Server API on your behalf." They have **no visibility** that the MCP server will also read their mail. The admin consented on their behalf — the user was never asked about Mail.Read specifically.

**What mitigates this:**
- **It's still delegated access.** OBO tokens are scoped to what the authenticating user has access to. If User A can't access User B's mailbox, the MCP server can't either — even with Mail.Read consent. The token carries User A's identity and permissions.
- **The MCP server needs its own credentials.** The operator must safeguard their client secret/certificate. Token A alone is not enough — you need both Token A AND the MCP server's credential to do the OBO exchange.
- **Audit logs exist.** Azure Entra records every token issuance. The downstream API team can see that tokens are being issued to the MCP Server's app ID via OBO. Microsoft Graph has its own audit logs showing which app accessed which user's data.
- **The admin who consented is accountable.** Admin consent is a deliberate act. The admin is saying: "I trust this application with these permissions for all users." This is a governance decision.

**Bottom line for Scenario A:** Admin consent is efficient but it requires **strong trust in the MCP server operator.** The downstream API team should evaluate the MCP server's app registration before the admin grants consent. This is no different from trusting any third-party enterprise SaaS app — once you admin-consent to Salesforce reading your users' calendars, Salesforce's code determines what it does with that access.

#### Scenario B (Combined consent): The user decides per-person

**What this actually grants:** Each individual user sees ALL downstream permissions in the consent screen and must explicitly click "Accept" before the MCP server can do OBO. No user is opted in until they personally approve.

**The risk is lower:** 
- Each user sees "Read your mail" in the consent prompt and can choose to decline
- If a user denies consent, the MCP server gets `consent_required` when attempting OBO — it simply cannot read that user's mail
- There's no blanket tenant-wide grant

**What the MCP server operator can still do (after user consent):**
- The same as Scenario A for that specific user — exchange their token for downstream access, potentially call APIs beyond what the specific tool needs
- The scope of exposure is limited to users who individually consented

**Bottom line for Scenario B:** This is the best model when you **don't fully trust the MCP server operator** or when users should have individual choice. Each user controls their own exposure. The downstream API team doesn't need to trust a blanket admin decision.

#### Scenario C (Incremental): Similar to B, but messier

Same security properties as Scenario B — each user individually consents to the downstream permissions. The only difference is UX (two prompts instead of one). The trust model is identical.

#### The OBO trust chain: What Entra ID validates before issuing Token B

It's worth being explicit about what Entra ID checks during the OBO exchange. The downstream API doesn't blindly trust the MCP server — the trust is established through a chain of explicit verifications. When the MCP Server calls `AcquireTokenOnBehalfOf`, Entra ID validates **all five** of these before minting Token B:

| # | Check | What it verifies |
|---|---|---|
| 1 | Is Token A valid and not expired? | JWT signature, `exp` claim |
| 2 | Is Token A's audience (`aud`) the MCP Server? | `aud` == MCP Server's app ID — confirms the token was intended for this server, not stolen from elsewhere |
| 3 | Is the caller actually the MCP Server? | `client_id` + `client_secret` (or certificate) match the MCP Server's app registration |
| 4 | Does the MCP Server have permission to access the downstream API with the requested scopes? | An API permissions grant exists on the MCP Server's app registration for those scopes |
| 5 | Has consent been granted (admin or user) for the MCP Server to act on behalf of users toward this downstream API? | A consent record exists — either tenant-wide (Scenario A) or per-user (Scenario B/C) |

If **all five** pass → Token B is issued with:
- `aud` = downstream API
- `sub` / `oid` = the original user's identity (Jane)
- `scp` = the requested delegated scopes
- `azp` = MCP Server's client ID (so the downstream API can see _who_ made the OBO request)

If **any** fail → `AADSTS` error, no token issued.

**What if an attacker steals Token A?** Even if someone intercepts Token A, they **cannot** perform the OBO exchange because they don't have the MCP Server's `client_secret` or certificate private key, nor a registered app with an API permission grant for the downstream API. Token A alone is only useful for calling the MCP Server — which is exactly what it was issued for.

The trust is explicit, not implicit:

| Trust Mechanism | Who Configures It | What It Proves |
|---|---|---|
| API permission grant (MCP Server → downstream API) | Developer + admin (or user consent) | The MCP Server is allowed to request downstream API scopes |
| Admin/user consent | Tenant admin or end user | An authorized person approved the permission |
| Client credential (secret/cert) | MCP Server developer | The OBO caller is genuinely the MCP Server |
| Token A audience check | Entra ID (automatic) | Token A was intended for the MCP Server |

**These checks are identical across Scenario A, B, and C.** The only difference between scenarios is how consent (check #5) is obtained — admin-granted, user-granted via combined consent, or user-granted incrementally. Once a consent record exists, the OBO exchange works the same way regardless of how consent was obtained.

#### What can the downstream API team do to protect themselves?

If you own the downstream API and a separate team's MCP server wants to call your API via OBO:

| Control | How | What it does |
|---|---|---|
| **Require user assignment** | Downstream API's enterprise app → Properties → "Assignment required?" = Yes | Only explicitly assigned users/groups can obtain tokens for your API. Even if the MCP server does OBO, Entra will refuse to issue Token B unless the user is assigned to your app. |
| **Restrict which apps can call you** | Downstream API's enterprise app → assign specific client app service principals via PowerShell (`New-MgServicePrincipalAppRoleAssignment`) | Only the MCP server's app ID (or a whitelist of app IDs) can obtain tokens for your API. Blocks unauthorized apps from using OBO to reach you. |
| **Validate the `azp` / `appid` claim** | In your API code, check the `azp` (v2.0) or `appid` (v1.0) claim in the incoming token | Tells you which application made the OBO request. You can reject requests from unknown app IDs at the code level. |
| **Use Conditional Access** | Create Conditional Access policies targeting your API's app registration | Require MFA, compliant devices, specific locations, etc. before tokens are issued for your API — regardless of how the calling app obtained them. |
| **Audit sign-in logs** | Entra admin center → Sign-in logs, filter by your API's resource ID | See exactly which apps, users, and IP addresses obtained tokens for your API and when. |
| **Scope granularity** | Define fine-grained scopes (e.g., `data.read.basic` vs `data.read.all`) | Limit what the MCP server can request. If you only expose `data.read.basic`, the MCP server can't escalate to `data.read.all` even via OBO. |

#### The key question to ask for each scenario

| Question | Scenario A (Admin) | Scenario B (Combined) | Scenario C (Incremental) |
|---|---|---|---|
| Can the MCP server access a user's data without that user ever authenticating? | **No.** Always needs a user token. | **No.** | **No.** |
| Can the MCP server access data the user didn't explicitly consent to (downstream scopes)? | **Yes** — admin consented on their behalf. The user never saw "Read your mail." | **No** — user saw and approved every permission. | **No** — user saw and approved every permission (twice). |
| Can the MCP server access data beyond what was shown in the consent prompt (within consented scopes)? | **Yes** — the code determines what it does with the scoped token. | **Yes** — same. Consent grants the scope, not a specific tool's behavior. | **Yes** — same. |
| Who is accountable for the downstream access grant? | **Tenant admin.** | **Individual user.** | **Individual user.** |
| Can the downstream API team block this? | **Yes** — require user assignment, restrict calling apps, Conditional Access. | **Yes** — same controls. | **Yes** — same controls. |

#### In your scenario: recommendation

Since the MCP server operator is a **different team** from the downstream API team, you probably want:

1. **Scenario B (combined consent)** — so individual users authorize the full permission chain, and the MCP server operator can't gain access without each user's buy-in 

2. **The downstream API team should enable "Assignment required"** on their API's enterprise app — this means even if a user consents, they also need to be explicitly assigned to the downstream API. Double gate.

3. **The downstream API team should validate the `azp` claim** in incoming tokens — so they can whitelist the MCP server's app ID and reject any unknown callers

4. **Use fine-grained scopes** — don't expose a single broad `data.readwrite` scope. Expose `data.read`, `data.write`, `data.read.sensitive` separately so you can control exactly what the MCP server can request via OBO

5. **Audit regularly** — Entra sign-in logs show every OBO exchange. The downstream API team should monitor which apps are obtaining tokens for their API

### Scenario B complete walkthrough: Three teams, full detail

This section walks through every step for the combined consent OBO flow. There are three parties involved, each on a different team:

- **The Downstream API Team** — owns an API called "Project Tracker API" that stores per-user project data
- **The MCP Server Team** — builds an MCP server with tools like `GetMyProjects` and `CreateProject` that call the Project Tracker API on behalf of whichever user authenticated
- **The User** — a person who uses an AI agent (e.g., VS Code Copilot) and wants to say "show me my projects"

The goal: the user clicks one button, sees one consent screen, and the MCP server can call the Project Tracker API as that user. No admin consent. No blanket tenant-wide grants. Each user individually authorizes.

---

#### Part 1: What the Downstream API Team does

The Project Tracker API team needs to make their API "OBO-accessible" — they need to tell Azure Entra what scopes exist and let external apps request them.

**Step 1.1: Register the API in Entra (if not already done)**

1. Go to [Microsoft Entra admin center](https://entra.microsoft.com/) → **Entra ID → App registrations → New registration**
2. Name: `Project Tracker API`
3. Supported account types: "Accounts in this organizational directory only" (single-tenant) — or multi-tenant if the MCP server is in a different tenant
4. No redirect URI (it's an API)
5. Click **Register** → note the **Application (client) ID** (e.g., `aaaa1111-bb22-cc33-dd44-eeee5555ffff`)

**Step 1.2: Expose the API with delegated scopes**

1. Go to **Expose an API**
2. Click **Add** next to Application ID URI → set it to `api://aaaa1111-bb22-cc33-dd44-eeee5555ffff` (default) → **Save**
3. Click **Add a scope**:
   - Scope name: `Projects.Read`
   - Who can consent: **Admins and users** ← this is critical for Scenario B: users must be able to consent themselves
   - Admin consent display name: "Read your projects"
   - Admin consent description: "Allows the application to read the signed-in user's projects"
   - User consent display name: "Read your projects"
   - User consent description: "Allow this app to see your projects in Project Tracker"
   - State: Enabled → **Add scope**
4. Repeat for `Projects.Write`:
   - Scope name: `Projects.Write`
   - Who can consent: **Admins and users**
   - Admin consent display name: "Create and modify your projects"
   - User consent display name: "Create and modify your projects"
   - User consent description: "Allow this app to create and edit your projects in Project Tracker"
   - State: Enabled → **Add scope**

The API now exposes two delegated scopes:
- `api://aaaa1111-bb22-cc33-dd44-eeee5555ffff/Projects.Read`
- `api://aaaa1111-bb22-cc33-dd44-eeee5555ffff/Projects.Write`

**Step 1.3: (Optional but recommended) Set "Assignment required"**

This gives the API team a kill switch — even if the MCP server does a valid OBO exchange, Entra will refuse to issue Token B unless the user is assigned to the Project Tracker API.

1. Go to **Entra ID → Enterprise applications** (not App registrations)
2. Find "Project Tracker API" in the list
3. Go to **Properties**
4. Set **Assignment required?** to **Yes** → **Save**
5. Go to **Users and groups** → **Add user/group** → add the users/groups who should be allowed to use this API

**Step 1.4: Validate calling applications in API code**

In the Project Tracker API's code, check which app is calling via OBO:

```csharp
[Authorize]
[ApiController]
[Route("api/projects")]
public class ProjectsController : ControllerBase
{
    [HttpGet]
    [RequiredScope("Projects.Read")]
    public IActionResult GetProjects()
    {
        // Check which application is making the OBO call
        var callingAppId = User.FindFirstValue("azp")    // v2.0 tokens
                        ?? User.FindFirstValue("appid"); // v1.0 tokens
        
        // Optional: whitelist allowed calling apps
        var allowedApps = new[] { "bbbb2222-cc33-dd44-ee55-ffff6666aaaa" }; // MCP Server's app ID
        if (!allowedApps.Contains(callingAppId))
        {
            return Forbid("Calling application is not authorized");
        }

        // The user's identity is in the token — use it to scope data
        var userId = User.FindFirstValue("oid"); // user's object ID
        var projects = _projectService.GetProjectsForUser(userId);
        return Ok(projects);
    }
}
```

**What the API team is NOT doing:** They are not granting the MCP server any special access. They're simply:
- Exposing scopes that _any_ authorized app can request via OBO
- Optionally restricting which users can get tokens for their API
- Optionally whitelisting which app IDs they accept
- The actual "authorization grant" happens when each individual user consents

---

#### Part 2: What the MCP Server Team does

The MCP server team needs to: register their app, configure it for OBO, set up `knownClientApplications` for combined consent, and write the tools.

**Step 2.1: Register the MCP Server in Entra**

1. Go to **Entra ID → App registrations → New registration**
2. Name: `MCP Server - Project Tools`
3. Supported account types: "Accounts in this organizational directory only"
4. No redirect URI needed
5. Click **Register** → note the **Application (client) ID** (e.g., `bbbb2222-cc33-dd44-ee55-ffff6666aaaa`)

**Step 2.2: Create a credential**

1. Go to **Certificates & secrets**
2. Click **New client secret** → set description: "MCP Server Production" → expiry: 24 months → **Add**
3. **Copy the secret value immediately** (it won't be shown again): e.g., `xYz...secret...789`

(For production: use a certificate or managed identity instead of a secret)

**Step 2.3: Expose an API scope for the MCP Client→MCP Server boundary**

1. Go to **Expose an API**
2. Set Application ID URI: `api://bbbb2222-cc33-dd44-ee55-ffff6666aaaa` → **Save**
3. **Add a scope**:
   - Scope name: `access_as_user`
   - Who can consent: **Admins and users**
   - Admin consent display name: "Access MCP Project Tools as you"
   - User consent display name: "Access MCP Project Tools as you"
   - User consent description: "Allow this AI agent to use MCP Project Tools on your behalf"
   - State: Enabled → **Add scope**

**Step 2.4: Declare delegated permissions for the downstream API**

1. Go to **API permissions** → **Add a permission**
2. Click **My APIs** (or **APIs my organization uses**) → find "Project Tracker API"
3. Select **Delegated permissions** → check `Projects.Read` and `Projects.Write` → **Add permissions**
4. **Do NOT click "Grant admin consent"** — this is Scenario B, so users will consent individually

The permissions page now shows:

| Permission | Type | Status |
|---|---|---|
| Project Tracker API / Projects.Read | Delegated | ⚠️ Not granted |
| Project Tracker API / Projects.Write | Delegated | ⚠️ Not granted |

This is correct. The "not granted" status means admin consent was not given. Each user will consent individually via the combined consent screen.

**Step 2.5: Register the MCP Client as a known client application**

This is the key step that enables combined consent. Without it, the user would need to consent twice (Scenario C).

1. Go to the MCP Server's app registration → **Manifest** (in the sidebar)
2. Find the `knownClientApplications` property (it's an empty array `[]`)
3. Add the MCP Client's application ID:

```json
"knownClientApplications": [
    "cccc3333-dd44-ee55-ff66-aaaa7777bbbb"
]
```

4. **Save** the manifest

What this does: when the MCP Client requests the `.default` scope for the MCP Server, Entra's consent UI will **combine** the permissions that the MCP Client needs (access_as_user on the MCP Server) with the permissions that the MCP Server needs (Projects.Read, Projects.Write on the downstream API) into a single consent prompt.

If you have multiple MCP clients (e.g., a VS Code extension, a web app, a CLI tool), add all their app IDs to this array.

**Step 2.6: Register the MCP Client app in Entra**

1. Go to **Entra ID → App registrations → New registration**
2. Name: `MCP AI Agent Client`
3. Supported account types: Same as MCP Server
4. Redirect URI: `http://localhost:1179/callback` (type: Web) — or whatever your MCP client listens on
5. Click **Register** → note the **Application (client) ID** (e.g., `cccc3333-dd44-ee55-ff66-aaaa7777bbbb`)

6. Go to **API permissions** → **Add a permission** → **My APIs** → select "MCP Server - Project Tools"
7. Select **Delegated permissions** → check `access_as_user` → **Add permissions**
8. **Do NOT click "Grant admin consent"**

The MCP Client now has permission to request `access_as_user` on the MCP Server. Since the MCP Server has `knownClientApplications` configured, when the user logs in through the MCP Client and the client requests `api://bbbb2222-cc33-dd44-ee55-ffff6666aaaa/.default`, the consent screen will show the combined permissions.

**Step 2.7: Write the MCP Server code**

`appsettings.json`:
```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "YOUR_TENANT_ID",
    "ClientId": "bbbb2222-cc33-dd44-ee55-ffff6666aaaa",
    "ClientCredentials": [
      {
        "SourceType": "ClientSecret",
        "ClientSecret": "xYz...secret...789"
      }
    ]
  },
  "ProjectTrackerApi": {
    "BaseUrl": "https://projecttracker.contoso.com",
    "Scopes": [ "api://aaaa1111-bb22-cc33-dd44-eeee5555ffff/Projects.Read", "api://aaaa1111-bb22-cc33-dd44-eeee5555ffff/Projects.Write" ]
  }
}
```

`Program.cs`:
```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using ModelContextProtocol.AspNetCore.Authentication;
using ModelContextProtocol.Server;
using System.ComponentModel;

var builder = WebApplication.CreateBuilder(args);

// Authentication: validate incoming tokens + enable OBO for downstream API
builder.Services.AddAuthentication(options =>
{
    options.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddMicrosoftIdentityWebApi(builder.Configuration, "AzureAd")
    .EnableTokenAcquisitionToCallDownstreamApi()
    .AddDownstreamApi("ProjectTracker", builder.Configuration.GetSection("ProjectTrackerApi"))
    .AddInMemoryTokenCaches();

// MCP protected resource metadata — tells MCP clients where to authenticate
builder.Services.AddAuthentication()
    .AddMcp(options =>
    {
        options.ResourceMetadata = new()
        {
            AuthorizationServers =
            {
                $"https://login.microsoftonline.com/{builder.Configuration["AzureAd:TenantId"]}/v2.0"
            },
            ScopesSupported = ["api://bbbb2222-cc33-dd44-ee55-ffff6666aaaa/access_as_user"],
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapMcp().RequireAuthorization();
app.Run();

[McpServerToolType]
public class ProjectTools
{
    [McpServerTool, Description("Gets the authenticated user's projects from Project Tracker")]
    public static async Task<string> GetMyProjects(IDownstreamApi projectTrackerApi)
    {
        var response = await projectTrackerApi.CallApiForUserAsync(
            "ProjectTracker",
            options => { options.RelativePath = "api/projects"; });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    [McpServerTool, Description("Creates a new project for the authenticated user")]
    public static async Task<string> CreateProject(
        string name,
        string description,
        IDownstreamApi projectTrackerApi)
    {
        var response = await projectTrackerApi.CallApiForUserAsync(
            "ProjectTracker",
            options =>
            {
                options.HttpMethod = HttpMethod.Post;
                options.RelativePath = "api/projects";
            },
            new { name, description });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
}
```

**What happens inside `CallApiForUserAsync`:**

1. Microsoft.Identity.Web extracts the incoming Bearer token from the current HttpContext
2. It POSTs to Entra's token endpoint:
   ```
   POST https://login.microsoftonline.com/{tenant}/oauth2/v2.0/token
   
   grant_type=urn:ietf:params:oauth:grant-type:jwt-bearer
   &client_id=bbbb2222-cc33-dd44-ee55-ffff6666aaaa          ← MCP Server's app ID
   &client_secret=xYz...secret...789                         ← MCP Server's credential
   &assertion={Token A}                                       ← the user's token
   &scope=api://aaaa1111-bb22-cc33-dd44-eeee5555ffff/Projects.Read
   &requested_token_use=on_behalf_of
   ```
3. Entra validates:
   - Is the MCP Server's client_id + secret correct? ✅
   - Is Token A valid and not expired? ✅  
   - Does Token A's audience match the MCP Server? (aud = `api://bbbb2222-...`) ✅
   - Has the user (or an admin) consented to the MCP Server having `Projects.Read` on the Project Tracker API? ✅ (the user consented via the combined consent screen)
4. Entra issues **Token B** with:
   - `aud` = `api://aaaa1111-bb22-cc33-dd44-eeee5555ffff` (Project Tracker API)
   - `sub` / `oid` = the original user's identity
   - `scp` = `Projects.Read`
   - `azp` = `bbbb2222-cc33-dd44-ee55-ffff6666aaaa` (the MCP Server's app ID — so the downstream API can see who did the OBO exchange)
5. Microsoft.Identity.Web calls the Project Tracker API with `Authorization: Bearer {Token B}`
6. The token is cached — subsequent calls for the same user skip the exchange until the token expires

---

#### Part 3: What the User does

This is the simplest part — the user just uses their AI agent.

**Step 3.1: First-time use**

1. User opens VS Code and tells Copilot (or another AI agent): _"Show me my projects from Project Tracker"_

2. The AI agent determines it needs the `GetMyProjects` MCP tool → sends a request to the MCP server at `https://mcp-server.contoso.com/mcp`

3. The MCP server returns **401 Unauthorized** with headers:
   ```
   HTTP/1.1 401 Unauthorized
   WWW-Authenticate: Bearer
   ```
   The MCP client also discovers `/.well-known/oauth-protected-resource`:
   ```json
   {
     "resource": "https://mcp-server.contoso.com/mcp",
     "authorization_servers": ["https://login.microsoftonline.com/{tenant}/v2.0"],
     "scopes_supported": ["api://bbbb2222-cc33-dd44-ee55-ffff6666aaaa/access_as_user"]
   }
   ```

4. The MCP client constructs an authorization request and **opens the user's browser** to:
   ```
   https://login.microsoftonline.com/{tenant}/oauth2/v2.0/authorize?
     client_id=cccc3333-dd44-ee55-ff66-aaaa7777bbbb        ← MCP Client's app ID
     &response_type=code
     &redirect_uri=http://localhost:1179/callback
     &scope=api://bbbb2222-cc33-dd44-ee55-ffff6666aaaa/.default openid profile
     &code_challenge=...                                     ← PKCE
     &code_challenge_method=S256
   ```

   Note the scope is `.default` — this is what triggers combined consent when `knownClientApplications` is configured.

5. The user's **browser opens** to the Microsoft Entra login page:
   
   ```
   ┌─────────────────────────────────────────────┐
   │           Sign in to your account            │
   │                                               │
   │   [ user@contoso.com ]                        │
   │   [ ••••••••••••••• ]                         │
   │                                               │
   │           [ Sign in ]                         │
   └─────────────────────────────────────────────┘
   ```
   
   User enters their email and password (and MFA if required).

6. After authentication, the user sees the **combined consent screen**:

   ```
   ┌─────────────────────────────────────────────────────┐
   │                                                       │
   │   "MCP AI Agent Client" would like to:                │
   │                                                       │
   │   ✅ Access MCP Project Tools as you                  │
   │      (MCP Server - Project Tools)                     │
   │                                                       │
   │   ✅ Read your projects                               │
   │      (Project Tracker API)                            │
   │                                                       │
   │   ✅ Create and modify your projects                  │
   │      (Project Tracker API)                            │
   │                                                       │
   │                                                       │
   │   [ Accept ]              [ Cancel ]                  │
   │                                                       │
   │   By accepting, you allow this app to access your     │
   │   data as described above.                            │
   └─────────────────────────────────────────────────────┘
   ```

   **This is the combined consent at work.** Because the MCP Client is in the MCP Server's `knownClientApplications`, and the client requested `.default`, Entra shows **all** permissions in the chain:
   - The MCP Client's permission on the MCP Server (`access_as_user`)
   - The MCP Server's permissions on the Project Tracker API (`Projects.Read`, `Projects.Write`)

   The user sees exactly what they're granting. No surprises. No hidden scopes.

7. User clicks **"Accept"**

8. The browser redirects to `http://localhost:1179/callback?code=0.AAAA...` 

9. The user sees a confirmation page in the browser:
   ```
   ┌──────────────────────────────┐
   │                                │
   │   ✅ Authorized!               │
   │                                │
   │   You can close this tab.      │
   │                                │
   └──────────────────────────────┘
   ```

10. Back in VS Code, the MCP client:
    - Exchanges the authorization code for Token A (audience = MCP Server)
    - Retries the `GetMyProjects` tool call with `Authorization: Bearer {Token A}` 
    - The MCP server validates Token A, does the OBO exchange for Token B, calls the Project Tracker API, and returns the user's projects

11. The AI agent shows: _"Here are your projects: ..."_

**Step 3.2: Every subsequent use**

1. User says: _"Create a new project called 'Q3 Planning'"_
2. AI agent calls `CreateProject` MCP tool
3. MCP client sends request with cached Token A (no browser, no prompts)
4. MCP server does OBO → Token B → calls Project Tracker API → project created
5. AI agent says: _"Done! Created project 'Q3 Planning'"_

**No consent screens. No browser. Fully seamless.** Tokens are cached and reused until they expire (typically 60-90 minutes, then the refresh token gets a new one transparently).

**Step 3.3: If the user wants to revoke access**

1. User goes to [https://myaccount.microsoft.com](https://myaccount.microsoft.com) → **Privacy** → **App permissions**
2. Finds "MCP AI Agent Client" and "MCP Server - Project Tools"
3. Clicks **Revoke** → the consent is removed
4. Next time the MCP tool runs, the MCP server's OBO exchange fails with `consent_required`
5. The user is prompted to consent again (or they don't, and the tool just stops working for them)

---

#### Summary: Who does what?

| Step | Who | What they do | When |
|---|---|---|---|
| Expose API scopes (`Projects.Read`, `Projects.Write`) | Downstream API team | Entra portal: Expose an API → Add scopes, set "Admins and users" can consent | Once during API setup |
| (Optional) Enable assignment required | Downstream API team | Enterprise apps → Properties → Assignment required = Yes | Once, for security |
| (Optional) Whitelist MCP Server's app ID in code | Downstream API team | Validate `azp` claim in token | In API code |
| Register MCP Server app | MCP Server team | Entra portal: new app registration + client secret | Once |
| Expose `access_as_user` scope on MCP Server | MCP Server team | Entra portal: Expose an API → Add scope | Once |
| Declare downstream API delegated permissions | MCP Server team | Entra portal: API permissions → Add Project Tracker API scopes | Once |
| Add MCP Client to `knownClientApplications` | MCP Server team | Entra portal: Manifest → edit JSON | Once per MCP client |
| Register MCP Client app | MCP Server team (or client team) | Entra portal: new app registration + redirect URI | Once per MCP client |
| Add MCP Server's `access_as_user` to MCP Client's permissions | MCP Server team (or client team) | Entra portal: MCP Client → API permissions → Add MCP Server scope | Once |
| Click "Accept" on combined consent screen | User | Browser, during first-time auth | Once (cached) |
| Revoke access (optional) | User | myaccount.microsoft.com → App permissions → Revoke | Whenever they want |

#### Does Scenario B require a tenant admin?

Short answer: **the core setup does not, but tenant-level policies can block it.**

Here's every action in Scenario B, and exactly what Entra role is needed:

| Action | Requires tenant admin? | What role is actually needed |
|---|---|---|
| **Create an app registration** | No, by default | Any user (unless tenant admin disabled "Users can register applications") |
| **Add delegated scopes to your own app** (Expose an API → Add a scope) | No | App registration owner |
| **Add a client secret/certificate** | No | App registration owner |
| **Edit the manifest** (`knownClientApplications`) | No | App registration owner |
| **Add delegated API permissions** to your app (API permissions → Add) | No | App registration owner — this only _declares_ intent, doesn't grant anything |
| **NOT clicking "Grant admin consent"** | N/A | Nobody — that's the whole point of Scenario B |
| **User clicking "Accept" on the consent screen** | No | The individual user |
| **(Optional) Enable "Assignment required"** | **Yes** | Cloud Application Administrator or higher (this is on the Enterprise app, not the App registration) |
| **(Optional) Assign users/groups to the enterprise app** | **Yes** | Cloud Application Administrator or higher |

**So the mandatory parts of Scenario B — creating app registrations, exposing scopes, configuring OBO, editing the manifest — can all be done by app registration owners without admin involvement.**

The optional security hardening steps ("Assignment required", user/group assignment) do require admin, but these are protections the _downstream API team_ adds on their side, not something the MCP server team needs.

**However, there's one critical tenant-level setting that can break Scenario B:**

**"User consent settings"** — found in the Entra admin center under **Enterprise applications → Consent and permissions → User consent settings**. This controls whether users can consent to apps at all. The options are:

| Setting | Effect on Scenario B |
|---|---|
| **"Allow user consent for apps"** | ✅ Scenario B works. Users see the combined consent screen and can click Accept. |
| **"Allow user consent for apps from verified publishers, for selected permissions"** | ⚠️ Scenario B works **only if** the MCP Server's publisher is verified AND the requested permissions are in the "allowed" set. Custom API scopes like `Projects.Read` may not be in the allowed set. |
| **"Do not allow user consent"** | ❌ Scenario B is **blocked**. Users cannot consent to anything. The consent screen shows "You need admin approval" instead of an Accept button. Every consent must go through an admin consent workflow or be admin-granted. |

**Most enterprise tenants restrict user consent** to some degree. This is a common security hardening step. If your tenant has user consent disabled, Scenario B degrades to either:
- **Admin consent workflow:** Users can _request_ consent, and an admin approves/denies the request. This adds a delay but preserves the per-user visibility.
- **Scenario A (admin consent):** The admin pre-approves everything, and users never see a consent screen.

**Another setting that can block you:** **"Users can register applications"** — found in **Entra ID → User settings**. If set to "No," only users with Application Administrator or Cloud Application Administrator roles can create app registrations. This affects both the MCP server team and the downstream API team.

**Bottom line:** Scenario B is designed to work without a tenant admin, and all the app registration configuration can be done by owners. But the tenant admin controls whether _users_ are allowed to consent. If user consent is disabled in the tenant, you'll need admin involvement regardless of which scenario you choose — the admin either pre-consents (Scenario A) or approves individual consent requests.

### End-to-end setup: Azure Entra + MCP Server + OBO

Here's every step to make this work, from zero to a running MCP server that calls Microsoft Graph on behalf of the authenticated user.

#### Step 1: Create two Entra App Registrations

You need **two** app registrations in the [Microsoft Entra admin center](https://entra.microsoft.com/):

**App Registration A: "MCP Server API"** (the middle-tier)
1. Go to **Entra ID → App registrations → New registration**
2. Name: `MCP Server API`
3. Supported account types: "Accounts in this organizational directory only"
4. No redirect URI needed (it's an API, not a user-facing app)
5. After creation, note the **Application (client) ID** and **Directory (tenant) ID**
6. Go to **Expose an API**:
   - Set the Application ID URI: `api://{client-id}` (or a custom URI)
   - Add a scope: `access_as_user` — "Allows the MCP server to act on your behalf"
   - Who can consent: "Admins and users"
7. Go to **Certificates & secrets**:
   - Create a **client secret** (or upload a certificate — certificates are more secure for production)
   - Save the secret value
8. Go to **API permissions**:
   - Add **Microsoft Graph → Delegated → User.Read** (or whatever downstream API permissions you need)
   - Add any other downstream API permissions
   - Click **Grant admin consent** for the tenant

**App Registration B: "MCP Client"** (the AI agent / frontend)
1. New registration: `MCP Client`
2. Supported account types: "Accounts in this organizational directory only"
3. Redirect URI: `http://localhost:1179/callback` (or wherever your client listens)
4. Go to **API permissions**:
   - Add **My APIs → MCP Server API → Delegated → access_as_user**
   - Grant admin consent
5. Optional but recommended: In the **MCP Server API** registration, go to **Expose an API → Authorized client applications** and add the MCP Client's client ID with the `access_as_user` scope. This pre-authorizes the client so users aren't prompted for consent.

#### Step 2: Configure the MCP Server

Install the required packages:

```
dotnet add package ModelContextProtocol.AspNetCore
dotnet add package Microsoft.Identity.Web
```

`appsettings.json`:
```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "YOUR_TENANT_ID",
    "ClientId": "YOUR_MCP_SERVER_CLIENT_ID",
    "ClientCredentials": [
      {
        "SourceType": "ClientSecret",
        "ClientSecret": "YOUR_MCP_SERVER_SECRET"
      }
    ]
  },
  "DownstreamApi": {
    "BaseUrl": "https://graph.microsoft.com/v1.0",
    "Scopes": [ "User.Read" ]
  }
}
```

`Program.cs`:
```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using ModelContextProtocol.AspNetCore.Authentication;
using ModelContextProtocol.Server;
using System.ComponentModel;

var builder = WebApplication.CreateBuilder(args);

// 1. Configure Azure Entra authentication + OBO token acquisition
builder.Services.AddAuthentication(options =>
{
    options.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddMicrosoftIdentityWebApi(builder.Configuration, "AzureAd")
    .EnableTokenAcquisitionToCallDownstreamApi()
    .AddDownstreamApi("GraphApi", builder.Configuration.GetSection("DownstreamApi"))
    .AddInMemoryTokenCaches()
    .Services
// 2. Add MCP protected resource metadata (tells clients where to auth)
.AddAuthentication()
.AddMcp(options =>
{
    options.ResourceMetadata = new()
    {
        AuthorizationServers =
        {
            $"https://login.microsoftonline.com/{builder.Configuration["AzureAd:TenantId"]}/v2.0"
        },
        ScopesSupported = ["api://YOUR_MCP_SERVER_CLIENT_ID/access_as_user"],
    };
});

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();

// 3. Configure MCP server with tools
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.MapMcp().RequireAuthorization();

app.Run();

// 4. Tool that calls Microsoft Graph on behalf of the user
[McpServerToolType]
public class UserProfileTool
{
    [McpServerTool, Description("Gets the authenticated user's profile from Microsoft Graph")]
    public static async Task<string> GetMyProfile(IDownstreamApi graphApi)
    {
        // This automatically uses the OBO flow!
        // Microsoft.Identity.Web takes the incoming bearer token,
        // exchanges it for a Graph token via OBO, and calls the API.
        var response = await graphApi.CallApiForUserAsync("GraphApi", options =>
        {
            options.RelativePath = "me";
        });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
}
```

**What `Microsoft.Identity.Web` does behind the scenes:**

1. `.AddMicrosoftIdentityWebApi()` validates incoming JWT bearer tokens against your Entra tenant
2. `.EnableTokenAcquisitionToCallDownstreamApi()` enables the OBO flow — it registers `ITokenAcquisition` which can exchange an incoming `access_token` for a new token targeting a different API
3. `.AddDownstreamApi()` provides `IDownstreamApi` which combines token acquisition + HTTP calls into a single abstraction
4. When `CallApiForUserAsync` is called, it:
   - Extracts the incoming bearer token from the current `HttpContext`
   - POSTs to `https://login.microsoftonline.com/{tenant}/oauth2/v2.0/token` with `grant_type=urn:ietf:params:oauth:grant-type:jwt-bearer` and the user's token as the `assertion`
   - Gets back a new token with `aud=https://graph.microsoft.com`
   - Calls Graph with that token
   - Caches the token for future requests from the same user

#### Step 3: Configure the MCP Client

If you're building a custom MCP client (not using Claude Desktop or VS Code Copilot):

```csharp
var transport = new HttpClientTransport(new()
{
    Endpoint = new Uri("https://your-mcp-server.example.com/mcp"),
    OAuth = new()
    {
        RedirectUri = new Uri("http://localhost:1179/callback"),
        AuthorizationRedirectDelegate = async (authUrl, redirectUri, ct) =>
        {
            // Open browser for user login
            Process.Start(new ProcessStartInfo(authUrl.ToString()) { UseShellExecute = true });

            // Listen for the callback
            using var listener = new HttpListener();
            listener.Prefixes.Add(redirectUri.GetLeftPart(UriPartial.Authority) + "/");
            listener.Start();
            var context = await listener.GetContextAsync();
            var code = HttpUtility.ParseQueryString(context.Request.Url!.Query)["code"];

            // Send a nice response to the browser
            var response = Encoding.UTF8.GetBytes("<html><body><h1>Authorized!</h1><p>You can close this tab.</p></body></html>");
            context.Response.ContentLength64 = response.Length;
            context.Response.ContentType = "text/html";
            await context.Response.OutputStream.WriteAsync(response, ct);
            context.Response.Close();

            return code;
        },
        DynamicClientRegistration = new()
        {
            ClientName = "My MCP Agent",
        },
    }
});

await using var client = await McpClient.CreateAsync(transport);
// Now call tools — auth is handled automatically
var result = await client.CallToolAsync("GetMyProfile", new());
```

If you're using **VS Code Copilot** or **Claude Desktop** as the MCP client, they handle the OAuth browser flow themselves — you just configure the MCP server URL and the client will discover auth requirements from the `/.well-known/oauth-protected-resource` metadata.

### Low-level MSAL OBO: When you need direct control

The end-to-end setup above uses `Microsoft.Identity.Web`'s high-level abstractions (`IDownstreamApi`, `CallApiForUserAsync`), which handle OBO automatically. But sometimes you need direct control over the OBO exchange — for example, when integrating with a custom downstream API that has non-standard error handling, when you need to inspect the token before using it, or when injecting into MCP tool methods where DI doesn't provide `IDownstreamApi`.

#### MCP Client: Acquiring Token A with MSAL directly

```csharp
using Microsoft.Identity.Client;

const string clientId = "cccc3333-dd44-ee55-ff66-aaaa7777bbbb";     // MCP Client's app registration
const string tenantId = "your-tenant-id";
const string authority = $"https://login.microsoftonline.com/{tenantId}";

// For Scenario B combined consent, use .default to trigger combined consent UI
// For Scenario A (admin pre-consented), you can use named scopes instead
string[] mcpServerScopes = ["api://bbbb2222-cc33-dd44-ee55-ffff6666aaaa/.default"];

var pca = PublicClientApplicationBuilder
    .Create(clientId)
    .WithAuthority(authority)
    .WithRedirectUri("http://localhost:1179/callback")
    .Build();

// Try silent first (cached token), fall back to interactive (browser)
AuthenticationResult authResult;
try
{
    var accounts = await pca.GetAccounts();
    authResult = await pca.AcquireTokenSilent(mcpServerScopes, accounts.FirstOrDefault())
        .ExecuteAsync();
}
catch (MsalUiRequiredException)
{
    authResult = await pca.AcquireTokenInteractive(mcpServerScopes)
        .ExecuteAsync();
}

// Token A — audience is the MCP Server
string tokenA = authResult.AccessToken;

// Attach to every MCP request
var httpClient = new HttpClient();
httpClient.DefaultRequestHeaders.Authorization =
    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenA);
```

**Token A contains:**
```json
{
  "aud": "api://bbbb2222-cc33-dd44-ee55-ffff6666aaaa",
  "sub": "jane-user-object-id",
  "scp": "access_as_user",
  "name": "Jane Developer"
}
```

#### MCP Server: Exchanging Token A for Token B via OBO

When you don't use `IDownstreamApi`, you can perform the OBO exchange directly with MSAL's `IConfidentialClientApplication`:

```csharp
// Register in DI (Program.cs)
builder.Services.AddSingleton<IConfidentialClientApplication>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    return ConfidentialClientApplicationBuilder
        .Create(config["AzureAd:ClientId"])
        .WithClientSecret(config["AzureAd:ClientCredentials:0:ClientSecret"])
        .WithAuthority($"https://login.microsoftonline.com/{config["AzureAd:TenantId"]}")
        .Build();
});
```

Inside an MCP tool:

```csharp
[McpServerToolType]
public class ProjectTools
{
    private static readonly string[] DownstreamScopes =
        ["api://aaaa1111-bb22-cc33-dd44-eeee5555ffff/Projects.Read"];

    [McpServerTool, Description("Gets the user's projects")]
    public static async Task<string> GetMyProjects(
        IConfidentialClientApplication msalClient,
        IHttpContextAccessor httpContextAccessor)
    {
        // Step 1: Extract Token A from the incoming request
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("No HTTP context");

        string tokenA = httpContext.Request.Headers.Authorization
            .ToString()
            .Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase);

        // Step 2: Exchange Token A for Token B via OBO
        var userAssertion = new UserAssertion(tokenA);

        AuthenticationResult oboResult = await msalClient
            .AcquireTokenOnBehalfOf(DownstreamScopes, userAssertion)
            .ExecuteAsync();

        string tokenB = oboResult.AccessToken;

        // Step 3: Call the downstream API with Token B
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenB);

        var response = await httpClient.GetAsync("https://projecttracker.contoso.com/api/projects");
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }
}
```

**Token B contains:**
```json
{
  "aud": "api://aaaa1111-bb22-cc33-dd44-eeee5555ffff",
  "sub": "jane-user-object-id",
  "oid": "jane-user-object-id",
  "scp": "Projects.Read",
  "azp": "bbbb2222-cc33-dd44-ee55-ffff6666aaaa",
  "name": "Jane Developer"
}
```

The `azp` (authorized party) claim tells the downstream API which application performed the OBO exchange. The downstream API can use this for audit logging or to whitelist specific callers.

**When to use low-level MSAL vs. Microsoft.Identity.Web:**

| Use case | Approach |
|---|---|
| Standard REST downstream API with JSON responses | `IDownstreamApi` + `CallApiForUserAsync` (high-level) |
| Need to inspect Token B claims before calling | `IConfidentialClientApplication` + `AcquireTokenOnBehalfOf` (low-level) |
| Multiple downstream APIs with different error handling | Low-level per-API, or named `IDownstreamApi` configurations |
| Non-HTTP downstream (e.g., gRPC, SDK-based) | Low-level — get Token B, pass it to the SDK |
| MCP tool where DI doesn't provide `IDownstreamApi` | Low-level — register `IConfidentialClientApplication` in DI |

### The user experience

From the user's perspective:

1. The AI agent tries to use an MCP tool
2. The MCP server returns `401 Unauthorized` with protected resource metadata
3. The MCP client opens a browser to `https://login.microsoftonline.com/...` with the Azure Entra login page
4. The user signs in (may see a consent screen like "MCP Client wants to access MCP Server API on your behalf")
5. The browser redirects back to the client with an auth code
6. The client exchanges the code for a token and retries the MCP call
7. The MCP server receives the token, validates it, and (if using OBO) exchanges it for a downstream token
8. The tool executes with the user's identity propagated to downstream APIs

**Subsequent calls reuse the cached token** — the user only authenticates once per session.

### Critical analysis: When to use OBO vs. other patterns

| Pattern | When to use | How it works |
|---|---|---|
| **No auth** | Local stdio servers, trusted networks | Server runs with OS permissions, no tokens |
| **API key / static token** | Simple shared servers, internal tools | Server validates a pre-shared secret in headers |
| **OAuth 2.0 (direct)** | MCP server IS the final API | Client gets a token for the MCP server, server uses it directly |
| **OAuth 2.0 + OBO** | MCP server calls downstream APIs as the user | Client gets token A for MCP server; server exchanges for token B targeting downstream API |
| **OAuth 2.0 + Client Credentials** | MCP server calls downstream APIs as itself (not as user) | Server uses its own identity regardless of who called it. No user delegation. |

**OBO is the right choice when:**
- Your MCP tools need to call APIs that are scoped to the specific user (read _their_ emails, access _their_ files, query _their_ data)
- You want audit trails that show which user performed which action through the MCP server
- The downstream API enforces per-user permissions (e.g., Microsoft Graph, SharePoint, custom APIs with RBAC)

**OBO is the wrong choice when:**
- The MCP server only needs its own identity (use client credentials instead)
- You're calling APIs that don't support user-delegated tokens
- You want simplicity and don't need per-user scoping (use API keys)

### Clarification: Delegated scopes vs. App Roles — they are NOT the same thing

Throughout this document, `User.Read`, `Mail.Read`, `Projects.Read`, `access_as_user`, etc. are all **delegated scopes** (also called "delegated permissions" or just "scopes"). These are fundamentally different from **App Roles** (also called "application permissions"). This distinction matters for OBO.

#### Quick definitions

| | Delegated Scopes | App Roles |
|---|---|---|
| **What they represent** | What an app can do _on behalf of a signed-in user_ | What an app can do _as itself_ (no user), OR a role assigned to a user for authorization |
| **Where they're defined** | App registration → **Expose an API** → Add a scope | App registration → **App roles** → Create app role |
| **How they're requested** | As OAuth `scope` parameter (e.g., `scope=User.Read Mail.Read`) | Assigned via admin consent on the client app's service principal |
| **Token claim** | `scp` claim (e.g., `"scp": "User.Read Mail.Read"`) | `roles` claim (e.g., `"roles": ["Mail.ReadWrite.All"]`) |
| **Who can consent** | Users or admins (depending on the scope's configuration) | **Admins only** — always |
| **User context** | Yes — the app's access is limited to what the user can access | No user context (app-only) — or used for role-based authorization within a user token |
| **Works with OBO?** | **Yes** — this is what OBO is designed for | **No** — OBO requires a user token with delegated scopes. App-only tokens cannot be used in OBO. |

#### Why this matters for the MCP + OBO scenario

**OBO exclusively uses delegated scopes.** The entire pattern is:

1. User authenticates → Token A has `scp: "access_as_user"` (a delegated scope)
2. MCP server exchanges Token A for Token B requesting `scope=Projects.Read` (a delegated scope)
3. Token B arrives with `scp: "Projects.Read"` — still delegated, still tied to the user

If the downstream API only defined `Projects.Read` as an **App Role** instead of a delegated scope, the OBO exchange would fail because:
- App Roles can't be requested via the `scope` parameter in an OBO exchange
- App Roles require admin consent and are assigned to the application's service principal, not requested at runtime per-user

#### What about Microsoft Graph permissions like `User.Read`?

Microsoft Graph exposes _both_ forms for many operations:

| Operation | Delegated scope | App Role (application permission) |
|---|---|---|
| Read user's profile | `User.Read` (scp) | `User.Read.All` (roles) |
| Read user's mail | `Mail.Read` (scp) | `Mail.Read` (roles) |
| Read all files in tenant | — | `Files.Read.All` (roles) |

When you see `User.Read` in this document, it's always the **delegated scope** version — the one that appears in the `scp` claim and works with OBO. The App Role version (`User.Read.All`) would give the application access to _every_ user's profile without any user being signed in — that's a completely different trust model (client credentials flow, not OBO).

#### So when ARE App Roles useful?

App Roles serve a different purpose — they're for **authorization within an API**, not for requesting access via OBO:

1. **App-only access (client credentials flow):** A background service needs to read all users' calendars without any user signing in. It uses the `Calendars.Read` App Role (application permission) with client credentials.

2. **Role-based authorization on the API side:** Your downstream API may define App Roles like `Admin`, `Reader`, `Writer` and assign them to users. These show up in the `roles` claim of the user's token. The API code checks `User.IsInRole("Admin")` to decide what operations are allowed. This is _orthogonal_ to the OBO scopes — you can have both `scp: "Projects.Read"` AND `roles: ["Admin"]` in the same token.

#### For your MCP + OBO setup: use delegated scopes everywhere

The entire consent and OBO pipeline in this document uses:
- **Delegated scopes** on the MCP Server (`access_as_user`)
- **Delegated scopes** on the downstream API (`Projects.Read`, `Projects.Write`)

You _cannot_ substitute App Roles here. If you defined `Projects.Read` as an App Role instead of a delegated scope:
- Users could not consent to it individually (App Roles require admin consent)
- It wouldn't appear in the combined consent screen
- The OBO `scope=` parameter wouldn't work with it
- You'd be granting the MCP Server app-level access to all users' data, which defeats the purpose of per-user OBO

**TL;DR:** Everything in this document's OBO flow is delegated scopes (`scp` claim). App Roles (`roles` claim) are a separate concept used for app-only access or role-based authorization within APIs. Don't mix them up.

### Downstream API: Validating Token B and authorizing requests

The downstream API doesn't know or care that OBO happened. Token B arrives as a standard JWT bearer token, and the API validates it with normal ASP.NET Core authentication middleware. This section shows the complete authorization code for a downstream API that uses both scope-based and role-based authorization.

#### Basic setup: JWT validation and scope-based authorization

```csharp
// === DOWNSTREAM API (e.g., Project Tracker API) ===

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;

var builder = WebApplication.CreateBuilder(args);

// Validate incoming tokens against this API's app registration
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

// appsettings.json:
// {
//   "AzureAd": {
//     "Instance": "https://login.microsoftonline.com/",
//     "TenantId": "your-tenant-id",
//     "ClientId": "aaaa1111-bb22-cc33-dd44-eeee5555ffff",
//     "Audience": "api://aaaa1111-bb22-cc33-dd44-eeee5555ffff"
//   }
// }

// Define authorization policies based on delegated scopes
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ReadProjects", policy =>
        policy.RequireScope("Projects.Read", "Projects.ReadWrite"));

    options.AddPolicy("WriteProjects", policy =>
        policy.RequireScope("Projects.ReadWrite"));
});

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();

// Apply policies to endpoints
app.MapGet("/api/projects", GetProjects).RequireAuthorization("ReadProjects");
app.MapPost("/api/projects", CreateProject).RequireAuthorization("WriteProjects");
app.MapDelete("/api/projects/{id}", DeleteProject).RequireAuthorization("WriteProjects");

app.Run();
```

#### Endpoint handlers: Accessing the user's identity from OBO tokens

```csharp
static async Task<IResult> GetProjects(
    ClaimsPrincipal user,
    ProjectDbContext db)
{
    // The user's identity flows through OBO — same user who signed in at the MCP Client
    var userId = user.FindFirstValue("oid")
        ?? user.FindFirstValue("sub");

    // "azp" tells you which app called via OBO (the MCP Server)
    var callingApp = user.FindFirstValue("azp");

    // "scp" contains the delegated scopes
    var scopes = user.FindFirstValue("scp"); // "Projects.Read"

    var projects = await db.Projects
        .Where(p => p.OwnerId == userId)
        .ToListAsync();

    return Results.Ok(projects);
}

static async Task<IResult> DeleteProject(
    string id,
    ClaimsPrincipal user,
    ProjectDbContext db)
{
    var userId = user.FindFirstValue("oid");
    var project = await db.Projects.FindAsync(id);

    if (project is null) return Results.NotFound();

    // Row-level security: users can only delete their own projects
    if (project.OwnerId != userId)
        return Results.Forbid();

    db.Projects.Remove(project);
    await db.SaveChangesAsync();
    return Results.NoContent();
}
```

#### Controller-based alternative with `[RequiredScope]` attribute

```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize]  // All endpoints require a valid token
public class ProjectsController : ControllerBase
{
    [HttpGet]
    [RequiredScope("Projects.Read", "Projects.ReadWrite")]
    public async Task<IActionResult> GetProjects()
    {
        var userId = User.FindFirstValue("oid");
        var projects = await _db.Projects
            .Where(p => p.OwnerId == userId)
            .ToListAsync();
        return Ok(projects);
    }

    [HttpPost]
    [RequiredScope("Projects.ReadWrite")]
    public async Task<IActionResult> CreateProject(CreateProjectRequest request)
    {
        // Only tokens with Projects.ReadWrite can reach here
        var userId = User.FindFirstValue("oid");
        var project = new Project { Name = request.Name, OwnerId = userId };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetProjects), project);
    }
}
```

#### What the downstream API sees in Token B

Token B looks like any other delegated token. The `azp` claim is the only indicator that it came via OBO:

```json
{
  "aud": "api://aaaa1111-bb22-cc33-dd44-eeee5555ffff",
  "iss": "https://login.microsoftonline.com/{tenant}/v2.0",
  "sub": "jane-user-object-id",
  "oid": "jane-user-object-id",
  "name": "Jane Developer",
  "scp": "Projects.Read",
  "azp": "bbbb2222-cc33-dd44-ee55-ffff6666aaaa",   // ← the MCP Server that performed OBO
  "azpacr": "1",                                     // ← 1 = client secret, 2 = certificate
  "exp": 1742700000,
  "iat": 1742696400
}
```

#### Three layers of authorization

A well-designed downstream API enforces authorization at three levels:

| Layer | What It Does | Where | Example |
|---|---|---|---|
| **Authentication** | Validates JWT signature, issuer, audience, expiry | `AddMicrosoftIdentityWebApi` middleware | Rejects expired or tampered tokens |
| **Scope authorization** | Checks `scp` claim for required delegated permissions | `RequireScope` policy or `[RequiredScope]` attribute | Ensures Token B has `Projects.Read` |
| **Business authorization** | Row-level / domain logic specific to the application | Inside the endpoint handler | Users can only see their own projects |

The downstream API doesn't need to know that OBO happened. It validates Token B like any other bearer token — the OBO complexity is entirely between the MCP Server and Entra ID.

### App Roles in OBO tokens: `[Authorize(Roles = "FOO")]` works too

The delegated scopes section above covers the `scp` claim, which is the primary mechanism for OBO. But some downstream APIs use **App Roles** for authorization — `[Authorize(Roles = "FOO")]` instead of `[RequiredScope("Projects.Read")]`. This is a different authorization mechanism, but it works with OBO tokens.

#### How roles differ from scopes

| | Delegated Scopes (`scp`) | App Roles (`roles`) |
|---|---|---|
| **Claim name** | `scp` | `roles` |
| **Defined where** | **Expose an API** → Scopes | **App roles** blade in the app registration |
| **What it represents** | What the *app* can do *on behalf of* the user | What the *user* (or app) *is* — their role |
| **Who assigns it** | Consent (admin or user) | Admin assigns users/groups to the role in **Enterprise Applications** |
| **Source of truth** | Consent grant | User → role assignment on the service principal |
| **ASP.NET Core syntax** | `[RequiredScope("Projects.Read")]` | `[Authorize(Roles = "FOO")]` |
| **MCP Server controls it?** | Indirectly (requests scopes) | **No** — roles follow the user, not the calling app |

#### How roles end up in Token B

Roles are assigned to users, not requested by apps. The MCP Server has no influence over the `roles` claim:

1. **The downstream API defines an App Role** in its app registration → **App roles** → Create:
   - Display name: `Project Admin`
   - Value: `Project.Admin`
   - Allowed member types: Users/Groups

2. **A tenant admin assigns Jane the role** in Enterprise Applications → select the downstream API → **Users and groups** → Add → select Jane → select role: `Project.Admin`

3. **When OBO happens**, Entra ID automatically includes the role in Token B:

```json
{
  "aud": "api://aaaa1111-bb22-cc33-dd44-eeee5555ffff",
  "sub": "jane-user-object-id",
  "scp": "Projects.Read",                // ← from consent (scope)
  "roles": ["Project.Admin"],            // ← from user assignment (role)
  "azp": "bbbb2222-cc33-dd44-ee55-ffff6666aaaa"
}
```

The critical point: **roles follow the user, not the client app.** Entra ID checks "Is Jane assigned the `Project.Admin` role on this API?" — regardless of whether she's calling directly or through OBO. The MCP Server cannot request, add, or elevate roles.

#### Combining scopes and roles on the downstream API

A downstream API can enforce both simultaneously:

```csharp
[HttpDelete("{id}")]
[RequiredScope("Projects.ReadWrite")]     // App must have delegated permission (consent-based)
[Authorize(Roles = "Project.Admin")]      // User must have the admin role (assignment-based)
public async Task<IActionResult> DeleteProject(string id)
{
    // Only reachable if:
    // 1. Token has scp containing "Projects.ReadWrite" — the MCP Server was consented to this
    // 2. Token has roles containing "Project.Admin" — the user was assigned this role
    // Both conditions are enforced independently
    
    var project = await _db.Projects.FindAsync(id);
    if (project is null) return Results.NotFound();
    _db.Projects.Remove(project);
    await _db.SaveChangesAsync();
    return NoContent();
}
```

#### What the MCP Server needs to do differently for roles

**Nothing.** The MCP Server's OBO code is identical whether or not the downstream API uses roles:

```csharp
var result = await msalClient
    .AcquireTokenOnBehalfOf(
        ["api://aaaa1111-bb22-cc33-dd44-eeee5555ffff/Projects.Read"],
        userAssertion)
    .ExecuteAsync();
```

The MCP Server only requests **scopes**. It never mentions roles. Entra ID automatically includes any roles the user has on the target API. The MCP Server doesn't need to know that the downstream API uses role-based authorization.

#### If the user doesn't have the required role

The OBO exchange still **succeeds** — Token B is issued with the delegated scopes. But if the downstream API checks for a role the user doesn't have:

```
Token B:  "scp": "Projects.Read"     ← present (consent exists)
          "roles": []                ← absent (no role assigned)

API:      [Authorize(Roles = "Project.Admin")]  → 403 Forbidden
```

This is correct behavior — the downstream API controls its own authorization independently of the MCP Server. The MCP Server gets a valid token, but the downstream API rejects the request based on its own authorization rules.

### Downstream API authorization patterns: What the MCP Server implementer will encounter

Not every downstream API uses the same authorization model. This section catalogs every realistic pattern you'll encounter as the MCP Client + MCP Server implementer, what each means for your OBO implementation, and what is (and isn't) your responsibility.

#### Pattern 1: Delegated scopes only (`scp` claim)

The most common pattern and the one this document is built around. The API defines scopes under "Expose an API" and checks them with `[RequiredScope]` or `RequireScope()` policies.

```csharp
[RequiredScope("Projects.Read")]
public IActionResult GetProjects() { ... }
```

| Component | Action |
|---|---|
| **MCP Server app registration** | Add the downstream API's delegated scopes under **API permissions** |
| **MCP Server code** | Request those scopes in the OBO exchange: `scope=api://{api}/Projects.Read` |
| **MCP Client** | No awareness needed — authenticate the user and get Token A |
| **Consent** | Admin consent (Scenario A) or combined consent (Scenario B) for the downstream scopes |

> **Reference:** [Protect a web API — Verify scopes and app roles](https://learn.microsoft.com/en-us/entra/identity-platform/scenario-protected-web-api-verification-scope-app-roles?tabs=aspnetcore) · [Expose scopes (delegated permissions)](https://learn.microsoft.com/en-us/entra/identity-platform/quickstart-configure-app-expose-web-apis)

---

#### Pattern 2: App Roles only (`roles` claim via `[Authorize(Roles = "...")]`)

The API doesn't define any delegated scopes. It only defines App Roles and checks `User.IsInRole()` or `[Authorize(Roles)]`.

```csharp
[Authorize(Roles = "DataReader")]
public IActionResult GetData() { ... }
```

| Component | Action |
|---|---|
| **MCP Server app registration** | You still need _some_ delegated scope to do OBO. If the API doesn't expose any, **OBO cannot work** — there's nothing valid to put in the `scope=` parameter. |
| **Workaround** | Ask the downstream API team to add at least one delegated scope (even a minimal `user_impersonation` passthrough scope). If they expose `.default`, you can request that — but `.default` resolves to statically configured delegated scopes, so if none exist, it resolves to nothing. |
| **Role assignment** | A tenant admin must assign users to the App Role on the downstream API's enterprise app. You can't do this — only the downstream API team or tenant admin can. |
| **Fallback** | Use client credentials flow instead of OBO. But then you call as the _app_, not the user — the downstream API sees your MCP Server's identity, not Jane's. The API would need to grant your app an Application Permission (App Role assigned to the app). This is a completely different trust model. |

> **Reference:** [App roles — Declare roles for an application](https://learn.microsoft.com/en-us/entra/identity-platform/howto-add-app-roles-in-apps) · [Assign users and groups to roles](https://learn.microsoft.com/en-us/entra/identity-platform/howto-add-app-roles-in-apps#assign-users-and-groups-to-roles) · [AADSTS65001 error reference](https://learn.microsoft.com/en-us/entra/identity-platform/reference-error-codes#aadsts-error-codes)

---

#### Pattern 3: Delegated scopes + App Roles combined

The API enforces both: the token needs the right scope AND the right role.

```csharp
[RequiredScope("Data.ReadWrite")]
[Authorize(Roles = "Admin")]
public IActionResult DeleteData() { ... }
```

| Component | Action |
|---|---|
| **MCP Server app registration** | Add the delegated scope (`Data.ReadWrite`) under API permissions |
| **MCP Server code** | Request the delegated scope via OBO. Roles arrive automatically based on the user's assignment — the MCP Server does not request or influence them. |
| **Consent** | Handle consent for the delegated scope (Scenario A/B/C) |
| **Role assignment** | Not your responsibility — the downstream API team or tenant admin assigns users to roles |
| **Error handling** | OBO succeeds and you get Token B with `scp: "Data.ReadWrite"`, but if the user lacks the `Admin` role → the downstream API returns `403 Forbidden`. Surface clearly: _"You don't have the Admin role required for this operation."_ |

> **Reference:** [Verify scopes and app roles in a protected web API](https://learn.microsoft.com/en-us/entra/identity-platform/scenario-protected-web-api-verification-scope-app-roles?tabs=aspnetcore) · [How roles flow through OBO tokens](https://learn.microsoft.com/en-us/entra/identity-platform/optional-claims-reference)

---

#### Pattern 4: Calling app whitelist (`azp` claim validation)

The API checks which application performed the OBO exchange and rejects unknown callers.

```csharp
var callingApp = User.FindFirstValue("azp");
if (!_allowedApps.Contains(callingApp))
    return Forbid();
```

| Component | Action |
|---|---|
| **MCP Server** | Nothing in code — your MCP Server's app ID automatically appears in the `azp` claim of Token B |
| **Coordination** | Contact the downstream API team and ask them to add your MCP Server's Application (client) ID to their whitelist |
| **Error behavior** | Your OBO exchange succeeds (Entra issues Token B), but the downstream API rejects the request at the application level with `403`. There's no Entra-level error — the rejection is purely in the API's code. |

> **Reference:** [Access token claims reference — azp claim](https://learn.microsoft.com/en-us/entra/identity-platform/access-token-claims-reference#payload-claims) · [Restrict your app to a set of users](https://learn.microsoft.com/en-us/entra/identity-platform/howto-restrict-your-app-to-a-set-of-users)

---

#### Pattern 5: Assignment-required APIs

The downstream API's enterprise app has **"Assignment required?" = Yes**. Even with a valid OBO exchange, Entra refuses to issue Token B unless the user is explicitly assigned.

| Component | Action |
|---|---|
| **MCP Server** | Nothing in code — this is enforced at the Entra level during the OBO exchange |
| **Coordination** | The downstream API team or tenant admin must assign your users to their enterprise app under **Users and groups** |
| **Error handling** | OBO fails with `AADSTS50105: The signed in user is not assigned to a role for the application`. Surface as: _"Your admin needs to grant you access to [API name]."_ |

> **Reference:** [Require user assignment — Enterprise app properties](https://learn.microsoft.com/en-us/entra/identity/enterprise-apps/assign-user-or-group-access-portal) · [AADSTS50105 error](https://learn.microsoft.com/en-us/entra/identity-platform/reference-error-codes#aadsts-error-codes)

---

#### Pattern 6: Conditional Access policies

The tenant has Conditional Access rules like: "Require MFA when accessing the downstream API" or "Only allow access from compliant devices." These are evaluated during the OBO exchange.

| Component | Action |
|---|---|
| **MCP Server** | Handle the `claims challenge`. When OBO fails with `interaction_required`, extract the `claims` from the MSAL exception and return it to the MCP Client via a `WWW-Authenticate` header. |
| **MCP Client** | Re-trigger authentication with the claims challenge — the user completes stepped-up auth (MFA, device compliance) in the browser, then retries. |
| **Complexity** | **High.** This is the hardest pattern to handle correctly. Many MCP clients don't support claims challenges yet. |

```csharp
try
{
    var result = await msalClient
        .AcquireTokenOnBehalfOf(scopes, userAssertion)
        .ExecuteAsync();
}
catch (MsalUiRequiredException ex) when (ex.Classification == UiRequiredExceptionClassification.ConsentRequired)
{
    // Consent not granted — user (or admin) hasn't approved the downstream scopes
    // → Surface: "You need to grant permission to access [API]"
    throw;
}
catch (MsalUiRequiredException ex)
{
    // Conditional Access — extract claims and send back to client
    httpContext.Response.Headers.Append("WWW-Authenticate",
        $"Bearer claims=\"{ex.Claims}\", error=\"insufficient_claims\"");
    throw;
}
```

> **Reference:** [Handle Conditional Access and claims challenges](https://learn.microsoft.com/en-us/entra/identity-platform/v2-conditional-access-dev-guide) · [Web API that calls web APIs — Handle errors: Conditional Access and claims challenges](https://learn.microsoft.com/en-us/entra/identity-platform/scenario-web-api-call-api-app-configuration?tabs=aspnetcore#handle-errors) · [Conditional Access overview](https://learn.microsoft.com/en-us/entra/identity/conditional-access/overview)

---

#### Pattern 7: API keys / custom auth (no Entra)

The downstream API doesn't use Entra at all. It has its own auth system.

```
Authorization: ApiKey sk-abc123...
// or
X-API-Key: sk-abc123...
```

| Component | Action |
|---|---|
| **MCP Server** | No OBO needed. Store the API key securely (Azure Key Vault, environment variables). Call the API directly with the key. |
| **MCP Client** | Still authenticates to the MCP Server (Token A). The downstream call is fully decoupled from OBO. |
| **User identity** | Lost. The downstream API sees the MCP Server's API key, not the user. If you need per-user scoping, pass user context in the request body or custom headers. |

```csharp
[McpServerTool, Description("Search documents")]
public static async Task<string> SearchDocs(string query, IConfiguration config)
{
    using var httpClient = new HttpClient();
    httpClient.DefaultRequestHeaders.Add("X-API-Key", config["SearchApi:ApiKey"]);

    var response = await httpClient.GetAsync(
        $"https://search.example.com/api/search?q={Uri.EscapeDataString(query)}");
    response.EnsureSuccessStatusCode();
    return await response.Content.ReadAsStringAsync();
}
```

> **Reference:** [Azure Key Vault provider for Secret Manager](https://learn.microsoft.com/en-us/aspnet/core/security/key-vault-configuration) · [Safe storage of app secrets in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets)

---

#### Pattern 8: OAuth 2.0 but not Entra (Auth0, Okta, etc.)

The downstream API uses a different identity provider. Tokens come from `https://dev-xyz.auth0.com` or `https://org.okta.com`.

| Component | Action |
|---|---|
| **MCP Server** | Entra OBO **cannot** exchange Token A for a token from a different identity provider. You need a different approach. |
| **Option A: Token exchange (RFC 8693)** | If the downstream IdP supports the OAuth 2.0 Token Exchange grant, you might exchange your Entra token for their token. This is uncommon — most IdPs don't support cross-provider token exchange. |
| **Option B: Separate auth flow** | The MCP Server authenticates to the downstream API independently — either via client credentials on the other IdP, or by triggering a separate user auth flow. |
| **Option C: Service account / API key** | Most common in practice. Get a service account or API key. Per-user identity is lost. |

> **Reference:** [RFC 8693 — OAuth 2.0 Token Exchange](https://datatracker.ietf.org/doc/html/rfc8693) · [Auth0 Token Exchange](https://auth0.com/docs/get-started/authentication-and-authorization-flow/token-exchange-flow) · [Okta API Access Management](https://developer.okta.com/docs/concepts/api-access-management/)

---

#### Pattern 9: Multi-tenant APIs

The downstream API's app registration has "Supported account types" set to "Accounts in any organizational directory" (multi-tenant). It validates tokens from any Entra tenant.

| Component | Action |
|---|---|
| **MCP Server app registration** | May also need to be multi-tenant, depending on whether MCP Server and downstream API are in the same tenant |
| **MCP Server code** | OBO works the same — the scope URI doesn't change |
| **Consent** | Admin consent (Scenario A) only covers _your_ tenant. For the downstream API in another tenant, that tenant's admin must independently consent. Scenario B (combined consent) works better — each user from any tenant consents individually on first use. |
| **Service principal provisioning** | When a user from Tenant B first consents to a multi-tenant app, Entra creates a service principal in Tenant B automatically. This can be blocked by the other tenant's policies (admins can disable "Users can consent to apps"). |

> **Reference:** [Make your app multi-tenant](https://learn.microsoft.com/en-us/entra/identity-platform/howto-convert-app-to-be-multi-tenant) · [Understanding consent in multi-tenant apps](https://learn.microsoft.com/en-us/entra/identity-platform/consent-framework) · [Service principal provisioning during consent](https://learn.microsoft.com/en-us/entra/identity-platform/how-applications-are-added#what-are-service-principals-and-where-do-they-come-from)

---

#### Summary: MCP Server implementer's quick reference

| Downstream API Pattern | OBO Works? | What You Must Do | What's Not Your Problem |
|---|---|---|---|
| **Delegated scopes** | Yes | Declare scopes, handle consent | — |
| **App Roles only (no scopes)** | **No** | Ask API team to add a delegated scope | Role assignment |
| **Scopes + Roles** | Yes (scopes part) | Declare scopes, handle consent | Role assignment — admin does this |
| **`azp` whitelist** | Yes, but API may reject | Coordinate with API team to whitelist your app ID | Their whitelist management |
| **Assignment required** | OBO fails if user unassigned | Surface the `AADSTS50105` error clearly | User assignment — admin does this |
| **Conditional Access** | OBO may fail | Handle claims challenges, support re-auth | CA policy definition |
| **API key / no Entra** | N/A (no OBO) | Store key in Key Vault, call directly | — |
| **Non-Entra OAuth** | No (OBO is Entra-only) | Separate auth mechanism per IdP | — |
| **Multi-tenant** | Yes | Handle cross-tenant consent | Tenant-level policies in other tenants |

#### MCP Server OBO error handling reference

Every `AADSTS` error the MCP Server should handle during OBO:

```csharp
try
{
    var result = await msalClient
        .AcquireTokenOnBehalfOf(scopes, userAssertion)
        .ExecuteAsync();
}
catch (MsalServiceException ex) when (ex.ErrorCode == "AADSTS65001")
{
    // Consent not granted — user or admin hasn't approved the downstream scopes
    // → "You need to grant permission to access [API]"
}
catch (MsalServiceException ex) when (ex.ErrorCode == "AADSTS50105")
{
    // User not assigned to the downstream API (assignment required = Yes)
    // → "Your admin needs to grant you access to [API]"
}
catch (MsalServiceException ex) when (ex.ErrorCode == "AADSTS700024")
{
    // Token A audience mismatch — Token A's aud doesn't match MCP Server's app ID
    // → Developer error: check App ID URI configuration
}
catch (MsalServiceException ex) when (ex.ErrorCode == "AADSTS7000215")
{
    // Invalid client secret or certificate
    // → Developer error: MCP Server credential is wrong or expired
}
catch (MsalUiRequiredException ex)
{
    // Conditional Access, MFA required, or stepped-up auth needed
    // → Pass claims challenge back to MCP Client for re-auth
}
catch (MsalServiceException ex) when (ex.ErrorCode == "AADSTS501481")
{
    // Token A is an app-only token (client credentials), not a user token
    // → OBO only works with delegated (user) tokens
}
```

> **Reference:** [Entra ID authentication and authorization error codes](https://learn.microsoft.com/en-us/entra/identity-platform/reference-error-codes#aadsts-error-codes) · [MSAL.NET error handling](https://learn.microsoft.com/en-us/entra/msal/dotnet/advanced/exceptions/msal-error-handling) · [OBO flow error responses](https://learn.microsoft.com/en-us/entra/identity-platform/v2-oauth2-on-behalf-of-flow#error-response-example)

### Gotchas and practical considerations

1. **Consent propagation.** The OBO flow requires that the user has consented to _both_ the client→MCP server scope _and_ the MCP server→downstream API scope. Use `knownClientApplications` in the MCP Server's app manifest plus `.default` scope to trigger combined consent in a single prompt.

2. **Token caching is critical.** OBO token exchanges hit Entra's token endpoint on every request without caching. `Microsoft.Identity.Web` handles this with `.AddInMemoryTokenCaches()` (single-instance) or `.AddDistributedTokenCaches()` (multi-instance with Redis/SQL). For production, always use distributed caches.

3. **Conditional Access.** If the downstream API requires MFA or device compliance, the OBO exchange will fail with `interaction_required`. The MCP server must surface this error back to the client with a `WWW-Authenticate` header containing the `claims` challenge, so the client can re-authenticate with stepped-up auth. This is non-trivial to handle correctly.

4. **Token size.** Entra tokens can be large (2-4 KB). They travel with every MCP request. For high-frequency tool calls, this adds bandwidth overhead.

5. **Scope alignment.** The `aud` (audience) of the token the client sends must match the MCP server's Application ID URI. If there's a mismatch, token validation fails silently. Double-check `ValidAudience` matches what you set in "Expose an API."

6. **Dynamic Client Registration vs. pre-registration.** The MCP spec supports dynamic client registration, but Azure Entra does **not** support RFC 7591 dynamic client registration natively. In practice, you'll need to pre-register your MCP clients as app registrations, or use a proxy/custom authorization server that supports dynamic registration. This is a real friction point for the "any MCP client can connect" vision.

7. **MCP auth spec and Azure Entra alignment.** The MCP auth spec was designed to be provider-agnostic. Azure Entra works well as the authorization server because it supports standard OAuth 2.0 code + PKCE, JWT bearer tokens, and the OBO grant. But the MCP spec's dynamic client registration expectation doesn't align perfectly with Entra's model. The practical approach: pre-register known clients and configure `ScopesSupported` to match your Entra scopes.

8. **Per-session tools.** The C# SDK's `AspNetCoreMcpPerSessionTools` sample shows how to inject per-session state. Combined with auth, you can create tools that are personalized to the authenticated user — each MCP session gets its own DI scope and you can access `HttpContext.User` to get claims.

---

## Decision Matrix

| Scenario | Recommended Transport | Why |
|---|---|---|
| Local tool for a single AI client (Claude Desktop, Copilot, etc.) | **stdio** | Simplest setup, universal support, secure by default |
| Shared service for multiple clients/teams | **Streamable HTTP** | Multi-client, remote access, scalable |
| Production cloud deployment | **Streamable HTTP** | Full ASP.NET Core stack, auth, observability, load balancing |
| Supporting older MCP clients | **Streamable HTTP** (serves SSE automatically) | `MapMcp()` handles both; no extra work |
| Integration testing | **In-Memory Stream** | Fast, no I/O, no process management |
| You're not sure yet | **stdio** | Start simple, migrate to HTTP when you need to |

## Package Selection Quick Reference

| If you need... | Install... |
|---|---|
| stdio server + DI/hosting | `ModelContextProtocol` + `Microsoft.Extensions.Hosting` |
| HTTP server (Streamable HTTP + SSE) | `ModelContextProtocol.AspNetCore` |
| Client only or low-level server | `ModelContextProtocol.Core` |
| In-memory/stream transport | `ModelContextProtocol.Core` (included) |

## Sources

- [MCP C# SDK Transports Documentation](https://csharp.sdk.modelcontextprotocol.io/concepts/transports/transports.html)
- [MCP Protocol Specification — Transports](https://modelcontextprotocol.io/specification/2025-11-25/basic/transports)
- [MCP Protocol Specification — Authorization](https://modelcontextprotocol.io/specification/2025-11-25/basic/authorization)
- [C# SDK GitHub — Samples](https://github.com/modelcontextprotocol/csharp-sdk/tree/main/samples)
- [C# SDK — ProtectedMcpServer Sample](https://github.com/modelcontextprotocol/csharp-sdk/tree/main/samples/ProtectedMcpServer)
- [C# SDK — ProtectedMcpClient Sample](https://github.com/modelcontextprotocol/csharp-sdk/tree/main/samples/ProtectedMcpClient)
- [C# SDK Getting Started Guide](https://csharp.sdk.modelcontextprotocol.io/concepts/getting-started.html)
- [Azure Entra — OAuth 2.0 On-Behalf-Of Flow](https://learn.microsoft.com/en-us/entra/identity-platform/v2-oauth2-on-behalf-of-flow)
- [Azure Entra — OBO Flow Error Responses](https://learn.microsoft.com/en-us/entra/identity-platform/v2-oauth2-on-behalf-of-flow#error-response-example)
- [Azure Entra — Web API that calls web APIs: Code Configuration](https://learn.microsoft.com/en-us/entra/identity-platform/scenario-web-api-call-api-app-configuration)
- [Azure Entra — Register an Application](https://learn.microsoft.com/en-us/entra/identity-platform/quickstart-register-app)
- [Azure Entra — Expose a Web API (Delegated Scopes)](https://learn.microsoft.com/en-us/entra/identity-platform/quickstart-configure-app-expose-web-apis)
- [Azure Entra — Protect a Web API: Verify Scopes and App Roles](https://learn.microsoft.com/en-us/entra/identity-platform/scenario-protected-web-api-verification-scope-app-roles?tabs=aspnetcore)
- [Azure Entra — App Roles: Declare Roles for an Application](https://learn.microsoft.com/en-us/entra/identity-platform/howto-add-app-roles-in-apps)
- [Azure Entra — Access Token Claims Reference (azp, scp, roles)](https://learn.microsoft.com/en-us/entra/identity-platform/access-token-claims-reference#payload-claims)
- [Azure Entra — Optional Claims Reference](https://learn.microsoft.com/en-us/entra/identity-platform/optional-claims-reference)
- [Azure Entra — Restrict App to a Set of Users (Assignment Required)](https://learn.microsoft.com/en-us/entra/identity/enterprise-apps/assign-user-or-group-access-portal)
- [Azure Entra — Handle Conditional Access and Claims Challenges](https://learn.microsoft.com/en-us/entra/identity-platform/v2-conditional-access-dev-guide)
- [Azure Entra — Conditional Access Overview](https://learn.microsoft.com/en-us/entra/identity/conditional-access/overview)
- [Azure Entra — Authentication Error Codes (AADSTS Reference)](https://learn.microsoft.com/en-us/entra/identity-platform/reference-error-codes#aadsts-error-codes)
- [Azure Entra — Make Your App Multi-Tenant](https://learn.microsoft.com/en-us/entra/identity-platform/howto-convert-app-to-be-multi-tenant)
- [Azure Entra — Consent Framework for Multi-Tenant Apps](https://learn.microsoft.com/en-us/entra/identity-platform/consent-framework)
- [Azure Entra — Service Principals and Provisioning](https://learn.microsoft.com/en-us/entra/identity-platform/how-applications-are-added#what-are-service-principals-and-where-do-they-come-from)
- [MSAL.NET Error Handling](https://learn.microsoft.com/en-us/entra/msal/dotnet/advanced/exceptions/msal-error-handling)
- [RFC 8693 — OAuth 2.0 Token Exchange](https://datatracker.ietf.org/doc/html/rfc8693)
- [Microsoft.Identity.Web Wiki](https://github.com/AzureAD/microsoft-identity-web/wiki)
- [ASP.NET Core — Safe Storage of App Secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets)
- [ASP.NET Core — Azure Key Vault Configuration Provider](https://learn.microsoft.com/en-us/aspnet/core/security/key-vault-configuration)
