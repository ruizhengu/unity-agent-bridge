using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.Collections.Concurrent;
using UnityEditor;
using UnityEngine;
using System.Reflection;

[InitializeOnLoad]
public class UnityAgentServer
{
    private static HttpListener listener;
    private static Thread serverThread;
    private static ConcurrentQueue<Action> mainThreadActions = new ConcurrentQueue<Action>();
    private static volatile bool isCompiling = false;
    private static List<CompileError> cachedErrors = new List<CompileError>();
    private static DateTime lastLogCheck = DateTime.MinValue;

    static UnityAgentServer()
    {
        EditorApplication.update += OnUpdate;
        StartServer();
        AppDomain.CurrentDomain.DomainUnload += (s, e) => { StopServer(); };
    }

    private static void OnUpdate()
    {
        isCompiling = EditorApplication.isCompiling;
        
        // Cache errors periodically so they are always ready for the local server
        if ((DateTime.Now - lastLogCheck).TotalSeconds > 1f)
        {
            UpdateCachedErrors();
            lastLogCheck = DateTime.Now;
        }

        while (mainThreadActions.TryDequeue(out Action action))
        {
            try { action?.Invoke(); } catch (Exception e) { Debug.LogError(e); }
        }
    }

    private static void StartServer()
    {
        if (listener != null && listener.IsListening)
            return;

        try
        {
            listener = new HttpListener();
            listener.Prefixes.Add("http://127.0.0.1:5142/");
            listener.Start();

            serverThread = new Thread(ListenForRequests);
            serverThread.IsBackground = true;
            serverThread.Start();
            
            Debug.Log("Unity Agent Server started on http://127.0.0.1:5142/");
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to start Unity Agent Server: " + e.Message);
        }
    }

    private static void StopServer()
    {
        EditorApplication.update -= OnUpdate;
        if (listener != null)
        {
            try { listener.Stop(); } catch { }
            try { listener.Close(); } catch { }
            listener = null;
        }
        if (serverThread != null && serverThread.IsAlive)
        {
            try { serverThread.Abort(); } catch { }
            serverThread = null;
        }
    }

    private static void ListenForRequests()
    {
        while (listener != null && listener.IsListening)
        {
            try
            {
                var context = listener.GetContext();
                ProcessRequest(context);
            }
            catch (Exception)
            {
                // Expected when stopping listener
            }
        }
    }

    private static void ProcessRequest(HttpListenerContext context)
    {
        try
        {
            if (context.Request.Url.AbsolutePath == "/compile-errors")
            {
                // Just return whatever the main thread has cached
                string json = ConvertToJsonArray(cachedErrors);
                SendJsonResponse(context, json);
            }
            else if (context.Request.Url.AbsolutePath == "/refresh")
            {
                mainThreadActions.Enqueue(() => { AssetDatabase.Refresh(); });
                SendJsonResponse(context, "{\"status\":\"ok\"}");
            }
            else if (context.Request.Url.AbsolutePath == "/ping")
            {
                SendJsonResponse(context, "{\"isCompiling\":" + (isCompiling ? "true" : "false") + "}");
            }
            else
            {
                context.Response.StatusCode = 404;
                context.Response.Close();
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Error handling request: " + e.Message);
            try
            {
                context.Response.StatusCode = 500;
                context.Response.Close();
            }
            catch { }
        }
    }

    private static void SendJsonResponse(HttpListenerContext context, string json)
    {
        byte[] buffer = Encoding.UTF8.GetBytes(json);
        context.Response.ContentType = "application/json";
        context.Response.ContentLength64 = buffer.Length;
        context.Response.OutputStream.Write(buffer, 0, buffer.Length);
        context.Response.OutputStream.Close();
    }

    private class CompileError
    {
        public string File;
        public int Line;
        public string Message;
    }

    private static void UpdateCachedErrors()
    {
        var errors = new List<CompileError>();
        
        try
        {
            var logEntriesType = Type.GetType("UnityEditor.LogEntries, UnityEditor.dll");
            if (logEntriesType == null) return;
            var getCountsByTypeMethod = logEntriesType.GetMethod("GetCountsByType", BindingFlags.Static | BindingFlags.Public);
            if (getCountsByTypeMethod == null) return;
            var startGettingEntriesMethod = logEntriesType.GetMethod("StartGettingEntries", BindingFlags.Static | BindingFlags.Public);
            if (startGettingEntriesMethod == null) return;
            var getEntryInternalMethod = logEntriesType.GetMethod("GetEntryInternal", BindingFlags.Static | BindingFlags.Public);
            if (getEntryInternalMethod == null) return;
            var endGettingEntriesMethod = logEntriesType.GetMethod("EndGettingEntries", BindingFlags.Static | BindingFlags.Public);
            if (endGettingEntriesMethod == null) return;
            var logEntryType = Type.GetType("UnityEditor.LogEntry, UnityEditor.dll");
            if (logEntryType == null) return;

            object logEntry = Activator.CreateInstance(logEntryType);
            var modeField = logEntryType.GetField("mode", BindingFlags.Instance | BindingFlags.Public);
            var messageField = logEntryType.GetField("message", BindingFlags.Instance | BindingFlags.Public);
            var fileField = logEntryType.GetField("file", BindingFlags.Instance | BindingFlags.Public);
            var lineField = logEntryType.GetField("line", BindingFlags.Instance | BindingFlags.Public);

            int errorCount = 0, warningCount = 0, logCount = 0;
            object[] countsArgs = new object[] { errorCount, warningCount, logCount };
            getCountsByTypeMethod.Invoke(null, countsArgs);

            int entries = 0;
            int retries = 0;
            while (retries < 4)
            {
                entries = (int)startGettingEntriesMethod.Invoke(null, null);
                if (entries > 0) break;
                endGettingEntriesMethod.Invoke(null, null);
                Thread.Sleep(250);
                retries++;
            }

            for (int i = 0; i < entries; i++)
            {
                getEntryInternalMethod.Invoke(null, new object[] { i, logEntry });

                int mode = (int)modeField.GetValue(logEntry);
                string message = (string)messageField.GetValue(logEntry);
                string file = (string)fileField.GetValue(logEntry);
                int line = (int)lineField.GetValue(logEntry);

                bool isCompileError = (mode == 272384) || message.Contains("error CS") || message.Contains("Assets/");
                
                if (isCompileError)
                {
                    if (!string.IsNullOrEmpty(file) && !message.StartsWith("Unity Agent Server"))
                    {
                        errors.Add(new CompileError { File = file, Line = line, Message = message });
                    }
                }
            }

            endGettingEntriesMethod.Invoke(null, null);
            cachedErrors = errors; // Atomically swap reference
        }
        catch (Exception e)
        {
            Debug.LogError("Error parsing Unity logs: " + e.Message);
        }
    }

    private static string ConvertToJsonArray(List<CompileError> errors)
    {
        var sb = new StringBuilder();
        sb.Append("[");
        for (int i = 0; i < errors.Count; i++)
        {
            var e = errors[i];
            sb.Append("{");
            sb.AppendFormat("\"File\":\"{0}\",", EscapeJson(e.File));
            sb.AppendFormat("\"Line\":{0},", e.Line);
            sb.AppendFormat("\"Message\":\"{0}\"", EscapeJson(e.Message));
            sb.Append("}");
            if (i < errors.Count - 1) sb.Append(",");
        }
        sb.Append("]");
        return sb.ToString();
    }

    private static string EscapeJson(string str)
    {
        if (str == null) return "";
        return str.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
    }
}
