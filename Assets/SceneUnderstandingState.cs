using System;
using System.Collections.Generic;
using System.Threading;

public static class SceneUnderstandingState
{
    public static string CurrentState { get; private set; }
    public static string CurrentError { get; private set; }
    public static List<string> Logs;


    public static void UpdateState(string message)
    {
        CurrentState = $"{message} (Thread {Thread.CurrentThread.ManagedThreadId})";
    }
    
    public static void UpdateErrors(Exception e, string message)
    {
        CurrentError = $"Exception: {message}: {e.Message}";
    }
}