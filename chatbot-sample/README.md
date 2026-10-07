# Chatbot sample — Streamlit + .NET 10

A minimal, runs-on-localhost chatbot: a Streamlit chat page talking to an ASP.NET Core
minimal API on .NET 10. Replies come from a rule-based mock engine, so there is no API
key and no network dependency.

```
Streamlit (8501)  ──POST /api/chat/stream──►  ASP.NET Core minimal API (5179)
   app.py             server-sent events         Program.cs → MockChatEngine.cs
```

For how and why it is built this way — request walkthrough, contracts, design
trade-offs, and how to swap in a real model — see **[ARCHITECTURE.md](ARCHITECTURE.md)**.

## Run it

Two terminals, from this folder:

```powershell
# terminal 1 — backend
.\run-backend.ps1        # http://localhost:5179

# terminal 2 — frontend (opens your browser automatically)
.\run-frontend.ps1       # http://localhost:8501
```

`run-frontend.ps1` opens <http://localhost:8501> in your default browser once the port
is up. Try `hello`, `what can you do`, `tell me a joke`, `how does this work`, or
anything else (it echoes with a word count).

### Without the scripts

```powershell
# backend — port comes from appsettings.json, so do not pass --urls (see below)
$env:DOTNET_ROOT = "$env:USERPROFILE\.dotnet"
$env:ASPNETCORE_ENVIRONMENT = "Development"
& "$env:USERPROFILE\.dotnet\dotnet.exe" run --project .\backend\ChatbotApi\ChatbotApi.csproj `
    --no-launch-profile

# frontend
.\frontend\.venv\Scripts\python.exe -m streamlit run .\frontend\app.py --server.port 8501
```

If the backend exits with `AddressInUseException`, an instance is already running.
`run-backend.ps1` checks for this and names the owning process instead; to find it by
hand: `Get-NetTCPConnection -State Listen -LocalPort 5179`.

### Why the browser auto-open is done by the script

Streamlit's built-in auto-open (`server.headless = false`) is preceded by an interactive
"enter your email address" prompt on first run, which blocks startup until you press
Enter. That prompt is normally suppressed by a `~/.streamlit/credentials.toml` file, but
on this machine that file gets deleted by an endpoint agent within minutes of being
created, so it can't be relied on. `run-frontend.ps1` therefore runs Streamlit with
`--server.headless true` (no prompt) and opens the browser itself from a background job
once the port answers. Config lives in
[frontend/.streamlit/config.toml](frontend/.streamlit/config.toml).

## Layout

| Path | What it is |
| --- | --- |
| [backend/ChatbotApi/Program.cs](backend/ChatbotApi/Program.cs) | Endpoints, CORS, DI |
| [backend/ChatbotApi/MockChatEngine.cs](backend/ChatbotApi/MockChatEngine.cs) | The "brain" — keyword rules + echo fallback |
| [backend/ChatbotApi/Models.cs](backend/ChatbotApi/Models.cs) | Request/response records |
| [frontend/app.py](frontend/app.py) | Streamlit chat UI and SSE client |

## API

`GET /health` → `{ "status": "ok", "model": "mock-echo-1", "serverTime": "..." }`

`POST /api/chat` — full reply in one response:

```powershell
$body = '{"messages":[{"role":"user","content":"hello"}]}'
Invoke-RestMethod http://localhost:5179/api/chat -Method Post -Body $body -ContentType 'application/json'
```

`POST /api/chat/stream` — same reply as server-sent events, one word per event.
Built with .NET 10's `TypedResults.ServerSentEvents`, which serialises an
`IAsyncEnumerable<string>` straight to the wire:

```
event: token
data: A

event: token
data:  SQL
```

Note the two spaces in `data:  SQL`: one belongs to the SSE framing and is stripped by
the client, the other is part of the token. The frontend strips exactly one, which is
why chunks concatenate back into correctly spaced text.

The backend is **stateless** — the frontend posts the whole conversation on every turn,
and history lives in Streamlit's `st.session_state`.

## Swapping the mock for a real model

`MockChatEngine` is the only piece that knows how replies are produced. Replace its two
methods — `Reply` and `StreamReply` — with a call to a model provider, register the new
class in [Program.cs](backend/ChatbotApi/Program.cs), and the endpoints and UI need no
changes.

## Notes on this machine's setup

- `winget` is blocked by group policy here, so the **.NET 10 SDK** was installed
  per-user via the official `dotnet-install.ps1` script into `%USERPROFILE%\.dotnet`
  (SDK 10.0.400). It sits alongside the system .NET 9 install and is not on `PATH`;
  `run-backend.ps1` points at it directly.
- **Python 3.12.10** was installed per-user (`%LOCALAPPDATA%\Programs\Python\Python312`),
  with Streamlit in `frontend\.venv`. Both are removable from Apps & features / by
  deleting those folders.
- To use a globally-installed `dotnet 10` or `python` instead, both run scripts fall
  back to whatever is on `PATH` when the per-user paths are absent.

## Changing ports

The backend port is set in exactly one place:
`Kestrel:Endpoints:Http:Url` in
[backend/ChatbotApi/appsettings.json](backend/ChatbotApi/appsettings.json). A `--urls`
argument would be silently overridden by that section (Kestrel logs
`Overriding address(es)`), so the run script deliberately does not pass one. If you move
the frontend off 8501, add its new origin to `AllowedOrigins` in `appsettings.json` or
the browser will block the request. The frontend reads the backend URL from the
`CHATBOT_API` environment variable, or you can edit it live in the sidebar.
