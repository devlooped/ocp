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
# Sponsors 

<!-- sponsors.md -->
[![Clarius Org](https://avatars.githubusercontent.com/u/71888636?v=4&s=39 "Clarius Org")](https://github.com/clarius)
[![MFB Technologies, Inc.](https://avatars.githubusercontent.com/u/87181630?v=4&s=39 "MFB Technologies, Inc.")](https://github.com/MFB-Technologies-Inc)
[![SandRock](https://avatars.githubusercontent.com/u/321868?u=99e50a714276c43ae820632f1da88cb71632ec97&v=4&s=39 "SandRock")](https://github.com/sandrock)
[![DRIVE.NET, Inc.](https://avatars.githubusercontent.com/u/15047123?v=4&s=39 "DRIVE.NET, Inc.")](https://github.com/drivenet)
[![Keith Pickford](https://avatars.githubusercontent.com/u/16598898?u=64416b80caf7092a885f60bb31612270bffc9598&v=4&s=39 "Keith Pickford")](https://github.com/Keflon)
[![Thomas Bolon](https://avatars.githubusercontent.com/u/127185?u=7f50babfc888675e37feb80851a4e9708f573386&v=4&s=39 "Thomas Bolon")](https://github.com/tbolon)
[![Kori Francis](https://avatars.githubusercontent.com/u/67574?u=3991fb983e1c399edf39aebc00a9f9cd425703bd&v=4&s=39 "Kori Francis")](https://github.com/kfrancis)
[![Reuben Swartz](https://avatars.githubusercontent.com/u/724704?u=2076fe336f9f6ad678009f1595cbea434b0c5a41&v=4&s=39 "Reuben Swartz")](https://github.com/rbnswartz)
[![Jacob Foshee](https://avatars.githubusercontent.com/u/480334?v=4&s=39 "Jacob Foshee")](https://github.com/jfoshee)
[![](https://avatars.githubusercontent.com/u/33566379?u=bf62e2b46435a267fa246a64537870fd2449410f&v=4&s=39 "")](https://github.com/Mrxx99)
[![Eric Johnson](https://avatars.githubusercontent.com/u/26369281?u=41b560c2bc493149b32d384b960e0948c78767ab&v=4&s=39 "Eric Johnson")](https://github.com/eajhnsn1)
[![Jonathan ](https://avatars.githubusercontent.com/u/5510103?u=98dcfbef3f32de629d30f1f418a095bf09e14891&v=4&s=39 "Jonathan ")](https://github.com/Jonathan-Hickey)
[![Ken Bonny](https://avatars.githubusercontent.com/u/6417376?u=569af445b6f387917029ffb5129e9cf9f6f68421&v=4&s=39 "Ken Bonny")](https://github.com/KenBonny)
[![Simon Cropp](https://avatars.githubusercontent.com/u/122666?v=4&s=39 "Simon Cropp")](https://github.com/SimonCropp)
[![agileworks-eu](https://avatars.githubusercontent.com/u/5989304?v=4&s=39 "agileworks-eu")](https://github.com/agileworks-eu)
[![Zheyu Shen](https://avatars.githubusercontent.com/u/4067473?v=4&s=39 "Zheyu Shen")](https://github.com/arsdragonfly)
[![Vezel](https://avatars.githubusercontent.com/u/87844133?v=4&s=39 "Vezel")](https://github.com/vezel-dev)
[![ChilliCream](https://avatars.githubusercontent.com/u/16239022?v=4&s=39 "ChilliCream")](https://github.com/ChilliCream)
[![4OTC](https://avatars.githubusercontent.com/u/68428092?v=4&s=39 "4OTC")](https://github.com/4OTC)
[![domischell](https://avatars.githubusercontent.com/u/66068846?u=0a5c5e2e7d90f15ea657bc660f175605935c5bea&v=4&s=39 "domischell")](https://github.com/DominicSchell)
[![Adrian Alonso](https://avatars.githubusercontent.com/u/2027083?u=129cf516d99f5cb2fd0f4a0787a069f3446b7522&v=4&s=39 "Adrian Alonso")](https://github.com/adalon)
[![torutek](https://avatars.githubusercontent.com/u/33917059?v=4&s=39 "torutek")](https://github.com/torutek)
[![Ryan McCaffery](https://avatars.githubusercontent.com/u/16667079?u=c0daa64bb5c1b572130e05ae2b6f609ecc912d4d&v=4&s=39 "Ryan McCaffery")](https://github.com/mccaffers)
[![Seika Logiciel](https://avatars.githubusercontent.com/u/2564602?v=4&s=39 "Seika Logiciel")](https://github.com/SeikaLogiciel)
[![Andrew Grant](https://avatars.githubusercontent.com/devlooped-user?s=39 "Andrew Grant")](https://github.com/wizardness)
[![eska-gmbh](https://avatars.githubusercontent.com/devlooped-team?s=39 "eska-gmbh")](https://github.com/eska-gmbh)
[![Geodata AS](https://avatars.githubusercontent.com/u/5946299?v=4&s=39 "Geodata AS")](https://github.com/geodata-no)


<!-- sponsors.md -->
[![Sponsor this project](https://avatars.githubusercontent.com/devlooped-sponsor?s=118 "Sponsor this project")](https://github.com/sponsors/devlooped)

[Learn more about GitHub Sponsors](https://github.com/sponsors)

<!-- https://github.com/devlooped/sponsors/raw/main/footer.md -->
