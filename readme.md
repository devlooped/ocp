# ocp

[![Version](https://img.shields.io/nuget/vpre/ocp.svg?color=royalblue)](https://www.nuget.org/packages/ocp)
[![Downloads](https://img.shields.io/nuget/dt/ocp.svg?color=darkmagenta)](https://www.nuget.org/packages/ocp)
[![EULA](https://img.shields.io/badge/EULA-OSMF-blue?labelColor=black&color=C9FF30)](https://github.com/devlooped/oss/blob/main/osmfeula.txt)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/devlooped/oss/blob/main/license.txt)

**ocp** is an OpenAI-compatible HTTP endpoint backed by the [GitHub Copilot SDK](https://github.com/github/copilot-sdk). It lets any client that speaks the OpenAI Chat Completions API — including [Grok](https://github.com/xai-org/grok-cli) — route requests through your GitHub Copilot subscription instead of a separate provider key.

Under the hood, ocp starts a local ASP.NET server and proxies chat to Copilot sessions. Model discovery comes from the SDK at runtime, so the available models match what your Copilot plan and CLI expose.

<!-- #content -->

## Prerequisites

- A [GitHub Copilot](https://github.com/features/copilot) subscription
- Authenticated Copilot CLI credentials (`copilot` login, or `GH_TOKEN` / `GITHUB_TOKEN` / `COPILOT_GITHUB_TOKEN` in the environment)

## Install & Run

```bash
dotnet tool install -g ocp
ocp
```

## Run without installing

```bash
dnx -y ocp
```

On startup, ocp prints the working directory, the models Copilot exposes, and the listen URL (default `http://localhost:11434`):

```
ocp: using working directory: C:\Users\you\.ocp
ocp: Copilot client started.
ocp: models: gpt-5.5, gpt-5-mini, claude-sonnet-4.6, gpt-5.3-codex
ocp: listening on http://localhost:11434 (OpenAI compatible)
```

Options:

| Flag | Description |
|------|-------------|
| `--cwd <path>` | Copilot working directory (defaults to `~/.ocp`) |
| `--help` | Show usage |

Install from source:

```bash
dotnet pack src/ocp/ocp.csproj
dotnet tool install -g --add-source ./bin ocp
```

## API

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/v1/models` | OpenAI-compatible model list from Copilot |
| `POST` | `/v1/chat/completions` | Chat completion (supports `stream: true`) |
| `GET` | `/` | Health check |

Example:

```bash
curl http://localhost:11434/v1/chat/completions \
  -H "Content-Type: application/json" \
  -d '{"model":"gpt-5.5","messages":[{"role":"user","content":"Hello"}]}'
```

The `model` field must be a Copilot model id returned by `/v1/models`. ocp does not require an API key, though clients may send one anyway.

## Configure Grok (`[model.*]` in `config.toml`)

Add entries under `~/.grok/config.toml`. Each `[model.<picker-name>]` block tells Grok how to reach ocp; the `model` field is the Copilot model id sent in chat requests.

**1. Start ocp** and note the port from its startup log (11434 by default).

**2. Add a shared base URL** and one section per Copilot model you want in the picker:

```toml
[models]
default = "copilot-gpt-5.5"

# OpenAI models via Copilot
[model.copilot-gpt-5.5]
model = "gpt-5.5"
base_url = "http://localhost:11434/v1"
name = "GPT-5.5 (Copilot)"
description = "GitHub Copilot — OpenAI GPT-5.5"
context_window = 200000

[model.copilot-gpt-5-mini]
model = "gpt-5-mini"
base_url = "http://localhost:11434/v1"
name = "GPT-5 mini (Copilot)"
description = "Fast, lower-cost Copilot model"

[model.copilot-gpt-5.3-codex]
model = "gpt-5.3-codex"
base_url = "http://localhost:11434/v1"
name = "GPT-5.3 Codex (Copilot)"
description = "Code-focused Copilot model"

[model.copilot-gpt-5.4]
model = "gpt-5.4"
base_url = "http://localhost:11434/v1"
name = "GPT-5.4 (Copilot)"

# Anthropic models via Copilot
[model.copilot-claude-sonnet-4.6]
model = "claude-sonnet-4.6"
base_url = "http://localhost:11434/v1"
name = "Claude Sonnet 4.6 (Copilot)"
context_window = 200000

[model.copilot-claude-opus-4.6]
model = "claude-opus-4.6"
base_url = "http://localhost:11434/v1"
name = "Claude Opus 4.6 (Copilot)"
context_window = 200000

# Google models via Copilot
[model.copilot-gemini-2.5-pro]
model = "gemini-2.5-pro"
base_url = "http://localhost:11434/v1"
name = "Gemini 2.5 Pro (Copilot)"
```

**3. Use the models in Grok:**

```bash
grok models                  # lists custom entries alongside built-ins
/model copilot-gpt-5.5       # switch in the TUI
grok -p "refactor this" -m copilot-gpt-5.5
```

### Notes

- **Model ids** — Use the exact ids from `ocp`'s startup line or `GET /v1/models`. Availability depends on your Copilot plan; see [GitHub's supported models](https://docs.github.com/en/copilot/reference/ai-models/supported-models).
- **Section name vs model id** — `[model.copilot-gpt-5.5]` is Grok's picker id; `model = "gpt-5.5"` is what ocp forwards to Copilot. They can differ, but keeping them aligned is easier to reason about.
- **Port conflicts** — The default port 11434 is also used by Ollama. If both run locally, ocp tries successive ports automatically; update `base_url` to match the URL ocp prints.
- **`api_backend`** — Omit it (defaults to `chat_completions`), which matches ocp's `/v1/chat/completions` endpoint.
- **No API key** — ocp has no auth middleware. You do not need `api_key` or `env_key` unless another proxy sits in front.

<!-- #content -->
---
<!-- include https://github.com/devlooped/sponsors/raw/main/footer.md -->