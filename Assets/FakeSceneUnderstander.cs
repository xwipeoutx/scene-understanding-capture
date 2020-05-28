using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.MixedReality.SceneUnderstanding;
using Microsoft.MixedReality.Toolkit.Utilities;
using SceneUnderstandingScenes;
using UnityEngine;
using UnityEngine.Windows;

public class FakeSceneUnderstander : ISceneUnderstander
{
    private readonly SceneSnapshot fallbackScene;
    private readonly string resourcePath;

    public FakeSceneUnderstander(SceneSnapshot fallbackScene)
    {
        this.fallbackScene = fallbackScene;
        this.resourcePath = resourcePath;
    }
    
    public void StartPolling()
    {
        Poll();
    }
    
    public void StopPolling()
    {
    }

    public void TakeSnapshot()
    {
        try
        {
            SceneHash = 1;
            UpdateState("Loading Resource");
            UpdateState("Deserializing Resource");
            MostRecentScene =  fallbackScene;
            UpdateState("Scene Loaded");
        }
        catch (Exception e)
        {
            Debug.LogError($"Error polling.  Last state was {CurrentState}");
            Debug.LogException(e);
        }
    }

    public int SceneHash { get; private set; } = 0;
    public int SearchRadius { get; set; } = 5;
    public string CurrentState { get; private set; } = "Not started";
    public SceneSnapshot MostRecentScene { get; private set;}
    
    public int SceneSize => MostRecentScene?.sceneBytes?.Length ?? -1;
    public int SceneObjectCount => MostRecentScene?.Scene?.SceneObjects?.Count ?? -1;

    private async Task Poll()
    {
        try
        {
            await new WaitForBackgroundThread();
            TakeSnapshot();
        }
        catch (Exception e)
        {
            Debug.LogError($"Error polling.  Last state was {CurrentState}");
            Debug.LogException(e);
            await new WaitForUpdate();
        }
    }

    void UpdateState(string message)
    {
        CurrentState = $"{message} (Thread {Thread.CurrentThread.ManagedThreadId})";
    }
}