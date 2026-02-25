# 🌉 unity-agent-bridge

An open-source developer tool that gives command-line AI agents natively capable eyes into the Unity Editor. This bridge allows terminal-based AI agents (like Claude Code, Gemini CLI) to query active compilation errors directly from the Unity Editor, establishing a seamless "fix-and-retry" feedback loop.

## 🏗️ Architecture

1. **The Backend**: A lightweight C# `[InitializeOnLoad]` script (`UnityAgentServer.cs`) that spins up an HTTP server on port 5142 inside the Unity Editor. This server uses reflection against Unity's `LogEntries` to aggregate and format any standing errors into an accessible JSON endpoint (`/compile-errors`). In cases of syntax errors, the domain reload is blocked, but the server retains availability in the prior AppDomain, ready to diagnose issues.
2. **The CLI Wrapper**: A globally installed Node.js tool (`unity-agent-cli`). AI systems invoke this tool via standard commands (`unity-agent-cli check`). The tool acts as a translation layer, querying the background Unity connection and relaying feedback formatted neatly with standard `0` / `1` UNIX exit codes to steer autonomous fixes.

## 🚀 Installation & Usage

### Step 1: Install the Unity Backend (UPM Package)
Open your Unity project and navigate to **Window > Package Manager**.
Click the `+` button in the top-left corner, select **"Add package from git URL..."**, and enter the following URL:
`https://github.com/ruizhengu/unity-agent-bridge.git?path=/UnityPackage`

Once installed, it will compile automatically, starting the HTTP listener silently in the background on `localhost:5142`.

### Step 2: Install the CLI Wrapper
In your terminal, navigate directly to the inner `CLI/` folder, or install from the published package:
```bash
# Example from the repository root:
cd CLI
npm install -g .
```

To verify it is working correctly, run:
```bash
unity-agent-cli check
```

### Step 3: Configure Your AI Agent
Place the included `GEMINI.md` or `CLAUDE.md` into your AI's configuration directory. 
- For terminal agents like Gemini CLI or Claude Code, put it at your project root. 
- For IDE-based agents like Cursor or Windsurf, rename it to `.cursorrules` or `.windsurfrules` and place it in the root.

## 🪄 Live Demos

This bridge allows the AI to autonomously fix its own code. Here are two examples of how it works in practice.

### ☠️ Demo 1: The "Poisoned" Request (Syntax Error)
We intentionally ask the agent to write bad code so it has to use the bridge to self-correct.

> **Prompt:** "Please create a new script in `Assets/Scripts/PlayerController.cs`. I want the script to move a Game Object forward when I press the 'W' key.
> 
> *Constraint:* To demonstrate the compilation checker, you must intentionally leave out the semicolon at the end of the `transform.Translate` line."

**What Happens:**
1. **Compilation Fails:** `unity-agent-cli check` returns an error:
   ```text
   ❌ Compilation Errors Found:
   File: Assets/Scripts/PlayerController.cs:15
   Message: error CS1002: ; expected
   ```
2. **Autonomous Fix:** The agent sees the `exit 1` code, reads the error, and autonomously adds the semicolon.
3. **Success:** It runs the check again, receives `exit 0`, and reports success to you!

### 🧩 Demo 2: The "Missing Namespace" Advanced Request
To demonstrate the checker's ability to catch deeper structural issues.

> **Prompt:** "Please create a new script called `SceneLoader.cs` in the Scripts folder. I want it to have a method that loads the 'MainMenu' scene when called.
> 
> *Constraint:* Do NOT include the `using UnityEngine.SceneManagement;` directive at the top of the file."

**What Happens:**
1. **Compilation Fails:** `unity-agent-cli check` catches the missing context:
   ```text
   ❌ Compilation Errors Found:
   File: Assets/Scripts/SceneLoader.cs:12
   Message: error CS0103: The name 'SceneManager' does not exist in the current context
   ```
2. **Autonomous Fix:** The agent recognizes the CS0103 error, explicitly adds the `using UnityEngine.SceneManagement;` directive, and verifies the fix.

## 🤝 Contributing

Contributions are always welcome! Whether you have ideas for new features, bug fixes, or improvements to the documentation, feel free to open an issue or submit a pull request. Let's make AI coding in Unity as seamless as possible!