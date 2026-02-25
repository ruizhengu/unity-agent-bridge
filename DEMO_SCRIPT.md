# Live Demo: Unity Agent Bridge with Gemini CLI

This demo showcases how a terminal-based AI agent (like Gemini CLI) can write Unity code, break it, and then use `unity-agent-cli` to autonomously detect and fix compilation errors without the developer ever leaving the terminal or manually checking the Unity Editor.

## Prerequisites
1. **Unity Editor Open:** Have an empty Unity 3D project open.
2. **Bridge Installed:** Ensure the UPM package (`com.unityagentbridge.server`) is installed in the project.
3. **CLI Installed:** Ensure `unity-agent-cli` is installed globally.
4. **Agent Ready:** Have Gemini CLI (or Claude Code / Antigravity) running in the terminal, navigated to the root of your Unity project.

## The Demo Setup
We will ask the agent to create a simple script, but we will artificially force it to write bad code on the first try so it has to use the bridge to self-correct.

### Step 1: Provide the System Prompt
First, give the agent the context it needs to use the bridge. You can copy-paste this directly into your terminal session:

> **Prompt:** "Before we begin, here is a strict rule: Whenever you create or modify a `.cs` file in this Unity project, you MUST verify that your code compiles by running the terminal command `unity-agent-cli check`. If it returns exit code `1`, you must read the compiler errors in the console output, fix the C# files autonomously, and run `unity-agent-cli check` again. Repeat this loop until it returns exit code `0`."

### Step 2: The "Poisoned" Request
Next, we ask the agent to create a script. We intentionally ask for something using an old/invalid Unity API or just bad syntax so it breaks.

> **Prompt:** "Please create a new script in `Assets/Scripts/PlayerController.cs`. I want the script to move a Game Object forward when I press the 'W' key. 
> 
> *Constraint:* To make the object move forward, you must use the method `transform.TranslateForward(Time.deltaTime)` instead of `transform.Translate(...)`."

*(Note: `transform.TranslateForward` does not exist in Unity, so this is guaranteed to cause a compile error!)*

## What Happens Next (The Magic)

This is what the audience will see in the terminal:

1. **Generation:** The agent writes `PlayerController.cs` containing the invalid `transform.TranslateForward(Time.deltaTime);` line.
2. **First Check:** Following its instructions, the agent automatically runs `unity-agent-cli check`.
3. **Error Detection:** The `unity-agent-cli check` command will fail because Unity immediately tries to compile the new script. It will print the error out to the terminal:
   ```text
   ❌ Compilation Errors Found:
   File: Assets/Scripts/PlayerController.cs:15
   Message: error CS1061: 'Transform' does not contain a definition for 'TranslateForward' and no accessible extension method...
   ```
4. **Autonomous Fix:** The agent sees the `exit 1` and reads the error message. It realizes its mistake, rewrites `PlayerController.cs` to use the correct `transform.Translate(Vector3.forward * Time.deltaTime);` method.
5. **Final Check:** The agent runs `unity-agent-cli check` one more time.
   ```text
   ✅ Compile Success
   ```
6. **Success:** The agent responds to the user: "I created the script and verified that it compiles successfully in Unity."

**The Result:** You have just watched an AI write code, test it inside a running game engine, realize it made a mistake, fix it, and verify the fix—all autonomously within seconds, while your hands were off the keyboard and the Unity window stayed entirely in the background.
