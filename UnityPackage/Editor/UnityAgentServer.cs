using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.Reflection;

[InitializeOnLoad]
public class UnityAgentServer
{
    private static HttpListener listener;
    private static Thread serverThread;

    static UnityAgentServer()
    {
        StartServer();
        AppDomain.CurrentDomain.DomainUnload += (s, e) => { StopServer(); };
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
                var errors = GetCompileErrors();
                string json = ConvertToJsonArray(errors);
                byte[] buffer = Encoding.UTF8.GetBytes(json);

                context.Response.ContentType = "application/json";
                context.Response.ContentLength64 = buffer.Length;
                context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                context.Response.OutputStream.Close();
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

    private class CompileError
    {
        public string File;
        public int Line;
        public string Message;
    }

    private static List<CompileError> GetCompileErrors()
    {
        var errors = new List<CompileError>();
        
        try
        {
            // Use reflection to access UnityEditor.LogEntries to get console errors
            var logEntriesType = Type.GetType("UnityEditor.LogEntries, UnityEditor.dll");
            if (logEntriesType == null) return errors;

            var getCountsByTypeMethod = logEntriesType.GetMethod("GetCountsByType", BindingFlags.Static | BindingFlags.Public);
            if (getCountsByTypeMethod == null) return errors;

            var startGettingEntriesMethod = logEntriesType.GetMethod("StartGettingEntries", BindingFlags.Static | BindingFlags.Public);
            if (startGettingEntriesMethod == null) return errors;

            var getEntryInternalMethod = logEntriesType.GetMethod("GetEntryInternal", BindingFlags.Static | BindingFlags.Public);
            if (getEntryInternalMethod == null) return errors;

            var endGettingEntriesMethod = logEntriesType.GetMethod("EndGettingEntries", BindingFlags.Static | BindingFlags.Public);
            if (endGettingEntriesMethod == null) return errors;

            var logEntryType = Type.GetType("UnityEditor.LogEntry, UnityEditor.dll");
            if (logEntryType == null) return errors;

            object logEntry = Activator.CreateInstance(logEntryType);
            
            var modeField = logEntryType.GetField("mode", BindingFlags.Instance | BindingFlags.Public);
            var messageField = logEntryType.GetField("message", BindingFlags.Instance | BindingFlags.Public);
            var fileField = logEntryType.GetField("file", BindingFlags.Instance | BindingFlags.Public);
            var lineField = logEntryType.GetField("line", BindingFlags.Instance | BindingFlags.Public);

            int errorCount = 0;
            int warningCount = 0;
            int logCount = 0;
            object[] countsArgs = new object[] { errorCount, warningCount, logCount };
            getCountsByTypeMethod.Invoke(null, countsArgs);

            int entries = (int)startGettingEntriesMethod.Invoke(null, null);

            for (int i = 0; i < entries; i++)
            {
                getEntryInternalMethod.Invoke(null, new object[] { i, logEntry });

                int mode = (int)modeField.GetValue(logEntry);
                
                // Identify compile errors via mode bitmask (includes generic errors and script compile errors)
                bool isError = (mode & (1 | 2 | 16 | 32 | 512 | 1024)) != 0;

                if (isError)
                {
                    string file = (string)fileField.GetValue(logEntry);
                    int line = (int)lineField.GetValue(logEntry);
                    string message = (string)messageField.GetValue(logEntry);

                    // We only care about errors that have an associated file
                    if (!string.IsNullOrEmpty(file))
                    {
                        errors.Add(new CompileError
                        {
                            File = file,
                            Line = line,
                            Message = message
                        });
                    }
                }
            }

            endGettingEntriesMethod.Invoke(null, null);
        }
        catch (Exception e)
        {
            Debug.LogError("Error parsing Unity logs: " + e.Message);
        }

        return errors;
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
