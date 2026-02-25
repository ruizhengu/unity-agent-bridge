Whenever you create or modify a `.cs` file, you MUST verify that your code compiles by running the command: `unity-agent-cli check`.

- If it returns exit code `0`, your code is good and compilation succeeded.
- If it returns exit code `1`, read the compiler errors in the console output, fix the C# files, and run `unity-agent-cli check` again autonomously. Do this in a loop until the compile succeeds.
