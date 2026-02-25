# Live Demo: Unity Agent Bridge with Gemini CLI

This demo showcases how a terminal-based AI agent (like Gemini CLI) can write Unity code, break it, and then use `unity-agent-cli` to autonomously detect and fix compilation errors without the developer ever leaving the terminal or manually checking the Unity Editor.

## Prerequisites
1. **Unity Editor Open:** Have an empty Unity 3D project open.
2. **Bridge Installed:** Ensure the UPM package (`com.unityagentbridge.server`) is installed in the project.
3. **CLI Installed:** Ensure `unity-agent-cli` is installed globally.
4. **Agent Ready:** Have Gemini CLI (or Claude Code / Antigravity) running in the terminal, navigated to the root of your Unity project.

## The Demo Setup
We will ask the agent to create a simple script, but we will artificially force it to write bad code on the first try so it has to use the bridge to self-correct.

### Step 1: Drop in the Agent Rules (The Setup)
Instead of copy-pasting a prompt every time, modern AI agents support project-level rule files. 
For terminal agents like Gemini CLI or Antigravity, copy the provided `GEMINI.md` into an `.agents/` folder at your project root (e.g., `.agents/rules.md`). For IDE-based agents, rename it to match their specific format (e.g., `.cursorrules` for Cursor or `.windsurfrules` for Windsurf).

Here is what the rule file contains:
> "Whenever you create or modify a `.cs` file in this Unity project, you MUST verify that your code compiles by running the command: `unity-agent-cli check`. 
> 
> If it returns exit code `1`, you must read the compiler errors in the console output, fix the C# files autonomously, and repeat the check loop. Repeat this loop until it returns exit code `0`."

Now, whenever you open this project, the agent automatically "knows" it has eyes inside Unity.

### Step 2: The "Poisoned" Request
Next, we ask the agent to create a script. We intentionally ask for something using an old/invalid Unity API or just bad syntax so it breaks.

> **Prompt:** "Please create a new script in `Assets/Scripts/PlayerController.cs`. I want the script to move a Game Object forward when I press the 'W' key.
> 
> *Constraint:* To demonstrate the compilation checker, you must intentionally leave out the semicolon at the end of the `transform.Translate` line."

*(Note: Missing a semicolon is a guaranteed syntax error in C#, which will force the compiler to fail!)*

## What Happens Next (The Magic)

This is what the audience will see in the terminal:

1. **Generation:** The agent writes `PlayerController.cs` using the non-existent `transform.TranslateForward` method.
2. **First Check:** Following its `.agents` rules, the agent automatically runs `unity-agent-cli check` without you needing to prompt it again.
3. **Error Detection:** The `unity-agent-cli check` command will fail because Unity immediately tries to compile the new script. It will print the error out to the terminal:
   ```text
   ❌ Compilation Errors Found:
   File: Assets/Scripts/PlayerController.cs:15
   Message: error CS1002: ; expected
   ```
4. **Autonomous Fix:** The agent sees the `exit 1` and reads the error message. It realizes its mistake, rewrites `PlayerController.cs` to use the correct `transform.Translate(Vector3.forward * Time.deltaTime);` method.
5. **Final Check:** The agent runs `unity-agent-cli check` one more time.
   ```text
   ✅ Compile Success
   ```
6. **Success:** The agent responds to the user: "I created the script and verified that it compiles successfully in Unity."

**The Result:** You have just watched an AI write code, test it inside a running game engine, realize it made a mistake, fix it, and verify the fix—all autonomously within seconds, while your hands were off the keyboard and the Unity window stayed entirely in the background.

### Step 3: The "Missing Namespace" Advanced Request
To demonstrate the checker's ability to catch deeper structural issues, we can ask the agent to write a script that requires a specific Unity namespace but intentionally tell it not to include it.

> **Prompt:** "Please create a new script called `SceneLoader.cs` in the Scripts folder. I want it to have a method that loads the 'MainMenu' scene when called.
> 
> *Constraint:* Do NOT include the `using UnityEngine.SceneManagement;` directive at the top of the file."

*(Note: Without the `SceneManagement` namespace, `SceneManager.LoadScene` is undefined and will throw a compiler error!)*

## The Advanced Fix

This is what happens when the agent tries to run `unity-agent-cli check` on the missing namespace:

1. **Error Detection:** 
   ```text
   ❌ Compilation Errors Found:
   File: Assets/Scripts/SceneLoader.cs:12
   Message: error CS0103: The name 'SceneManager' does not exist in the current context
   ```
2. **Autonomous Fix:** The agent sees the classic CS0103 error. It immediately recognizes that it needs to import the `UnityEngine.SceneManagement` namespace, adds it to the top of `SceneLoader.cs`, and runs the check again.
3. **Success:** The fix compile successfully!
