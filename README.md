# unity-agent-bridge

An open-source developer tool that gives command-line AI agents natively capable eyes into the Unity Editor. This bridge allows terminal-based AI agents (like Claude Code, Gemini CLI, or Antigravity) to query active compilation errors directly from the Unity Editor, establishing a seamless "fix-and-retry" feedback loop.

## Architecture

1. **The Backend**: A lightweight C# `[InitializeOnLoad]` script (`UnityAgentServer.cs`) that spins up an HTTP server on port 5142 inside the Unity Editor. This server uses reflection against Unity's `LogEntries` to aggregate and format any standing errors into an accessible JSON enpoint (`/compile-errors`). In cases of syntax errors, the domain reload is blocked, but the server retains availability in the prior AppDomain, ready to diagnose issues.
2. **The CLI Wrapper**: A globally installed Node.js tool (`unity-agent-cli`). AI systems invoke this tool via standard commands (`unity-agent-cli check`). The tool acts as a translation layer, querying the background Unity connection and relaying feedback formatted neatly with standard `0` / `1` UNIX exit codes to steer autonomous fixes.

## Installation / Usage for Developers

### Step A: The Unity Backend (UPM Package)
Open your Unity project and navigate to **Window > Package Manager**.
Click the `+` button in the top-left corner, select **"Add package from git URL..."**, and enter the following URL:
`https://github.com/ruizhengu/unity-agent-bridge.git?path=/UnityPackage`

Once installed, it will compile automatically, starting the HTTP listener silently in the background on `localhost:5142`.

### Step B: The CLI Wrapper
In your terminal, navigate directly to the inner `CLI/` folder, or install from the published package:
```bash
# Example from the repository root:
cd CLI
npm install -g .
```

To verify it is working correctly while avoiding manual Unity log checks, run:
```bash
unity-agent-cli check
```

## AI Agent Integration

Place the included `GEMINI.md` into your AI's configuration directory. For terminal agents like Gemini CLI or Antigravity, put it inside an `.agents/` folder at your project root. For IDE-based agents like Cursor, rename it to `.cursorrules` and place it in the root. AI coding assistants fundamentally require Unix exit codes to dictate autonomous workflows. By pinging `unity-agent-cli`, agents can natively identify success (exit code `0`) or read terminal-dumped stack-traces containing compilation failures (exit code `1`), ensuring they rapidly self-correct any generated syntax anomalies.