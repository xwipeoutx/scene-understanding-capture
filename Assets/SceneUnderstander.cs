using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.MixedReality.SceneUnderstanding;
using Microsoft.MixedReality.Toolkit.Utilities;
using SceneUnderstandingScenes;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Windows;

public interface ISceneFetcher
{
    Scene FetchScene();
    byte[] FetchSceneBytes();
}

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

public class BuiltInSceneFetcher : ISceneFetcher
{
    public SceneSnapshot snapshot;

    public BuiltInSceneFetcher(SceneSnapshot snapshot)
    {
        this.snapshot = snapshot;
    }

    public Scene FetchScene()
    {
        return Task.Run(() => snapshot.Scene).GetAwaiter().GetResult();
    }

    public byte[] FetchSceneBytes()
    {
        return Task.Run(() => snapshot.sceneBytes).GetAwaiter().GetResult();
    }
}

public class SceneFetcher : ISceneFetcher
{
    public int SearchRadius { get; set; } = 5;
    public string CurrentState { get; private set; } = "Not started";

    private Scene lastScene = null;
    
    public Scene FetchScene()
    {
        var querySettings = Prepare();

        SceneUnderstandingState.UpdateState("Querying scene");
        var fetchSceneTask = lastScene == null 
            ? SceneObserver.ComputeAsync(querySettings, SearchRadius) 
            : SceneObserver.ComputeAsync(querySettings, SearchRadius, lastScene);
        
        var scene = fetchSceneTask.GetAwaiter().GetResult();
        SceneUnderstandingState.UpdateState("Scene bytes queried");
        return scene;
    }
    
    public byte[] FetchSceneBytes()
    {
        var querySettings = Prepare();

        SceneUnderstandingState.UpdateState("Querying scene bytes...");
        var fetchSceneTask = SceneObserver.ComputeSerializedAsync(querySettings, SearchRadius);
        var sceneBuffer = fetchSceneTask.GetAwaiter().GetResult();
        SceneUnderstandingState.UpdateState("Scene bytes queried");
        
        var data = new byte[sceneBuffer.Size];
        sceneBuffer.GetData(data);
        return data;
    }

    private SceneQuerySettings Prepare()
    {
        SceneUnderstandingState.UpdateState("Requesting access");
        var status = SceneObserver.RequestAccessAsync().GetAwaiter().GetResult();

        if (status != SceneObserverAccessStatus.Allowed)
        {
            SceneUnderstandingState.UpdateState($"FAILED - no access: {status}");
            throw new Exception($"Expected to get access.  Actually got: " + status);
        }

        var querySettings = new SceneQuerySettings()
        {
            EnableWorldMesh = false,
            EnableSceneObjectMeshes = false,
            EnableSceneObjectQuads = true,
            RequestedMeshLevelOfDetail = SceneMeshLevelOfDetail.Coarse,
            EnableOnlyObservedSceneObjects = false
        };
        return querySettings;
    }
}

public class SceneManager
{
    public ISceneFetcher sceneFetcher;
    private bool isPolling;
    
    public Scene Scene { get; private set; } 
    public UnityEvent onScene = new UnityEvent();
    
    public SceneManager(ISceneFetcher sceneFetcher)
    {
        this.sceneFetcher = sceneFetcher;
        isPolling = false;
    }

    public async void StartPolling(int updateFrequencyMs)
    {
        SceneUnderstandingState.UpdateState("Started Polling");
        isPolling = true;
        await new WaitForBackgroundThread();
        try
        {
            while (isPolling)
            {
                SceneUnderstandingState.UpdateState("Fetching a scene");
                Scene = sceneFetcher.FetchScene();
                SceneUnderstandingState.UpdateState("Waiting for main thread");
                await new WaitForUpdate();
                SceneUnderstandingState.UpdateState("Invoking callbacks");
                onScene.Invoke();
                SceneUnderstandingState.UpdateState("Waiting for background for next poll");
                await new WaitForBackgroundThread();
                SceneUnderstandingState.UpdateState("Waiting for next poll");
                await Task.Delay(updateFrequencyMs);
            }
            SceneUnderstandingState.UpdateState("Polling ended gracefully");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            Debug.LogError("Error in scene understanding: " + ex.Message);
            SceneUnderstandingState.UpdateErrors(ex, "Error when polling");
            await new WaitForUpdate();
        }
    }

    public void StopPolling()
    {
        SceneUnderstandingState.UpdateState("Stopping Polling");
        isPolling = false;
    }

    public void TakeSnapshot()
    {
        SceneUnderstandingState.UpdateState("Taking snapshot");
        Scene = sceneFetcher.FetchScene();
        onScene.Invoke();
    }
}

public class SceneUnderstander : ISceneUnderstander
{
    private bool shouldStop;

    public int SceneHash { get; private set; } = 0;
    public int SearchRadius { get; set; } = 5;
    public string CurrentState { get; private set; } = "Not started";
    public SceneSnapshot MostRecentScene { get; private set; }

    private Scene Scene => MostRecentScene?.Scene;

    public int SceneSize => MostRecentScene?.sceneBytes?.Length ?? -1;
    public int SceneObjectCount => Scene?.SceneObjects?.Count ?? -1;

    public void StartPolling()
    {
        shouldStop = false;
        Poll();
    }

    public void StopPolling()
    {
        shouldStop = true;
    }

    private async Task Poll()
    {
        await new WaitForBackgroundThread();
        try
        {
            while (!shouldStop)
            {
                GetOnce();
                await new WaitForBackgroundThread();
                await Task.Delay(2000);
            }
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            Debug.LogError("Error in scene understanding: " + ex.Message);
            UpdateState("Exception: " + ex.Message);
            await new WaitForUpdate();
        }
    }

    private void GetOnce()
    {
        UpdateState("Requesting access");
        var status = SceneObserver.RequestAccessAsync().GetAwaiter().GetResult();

        if (status != SceneObserverAccessStatus.Allowed)
        {
            UpdateState($"FAILED - no access: {status}");
            throw new Exception($"Expected to get access.  Actually got: " + status);
        }

        //UpdateState("Access granted, waiting a bit");
        //await Task.Delay(100);

        UpdateState("Access granted! Querying scene...");
        var querySettings = new SceneQuerySettings()
        {
            EnableWorldMesh = false,
            EnableSceneObjectMeshes = false,
            EnableSceneObjectQuads = true,
            RequestedMeshLevelOfDetail = SceneMeshLevelOfDetail.Coarse,
            EnableOnlyObservedSceneObjects = false
        };

        var buffer = SceneObserver.ComputeSerializedAsync(querySettings, SearchRadius).GetAwaiter().GetResult();

        UpdateState("Got scene buffer, deserializing");
        var bytes = new byte[buffer.Size];
        buffer.GetData(bytes);

        UpdateState("Waiting for update");
        MostRecentScene = SceneSnapshot.CreateFromDevice(bytes);
        UpdateState("Scene Loaded");

        SceneHash++;
    }

    public void TakeSnapshot()
    {
        var filename = $"{Application.persistentDataPath}/serialized-scene-{DateTime.Now.ToFileTime()}.suscene";
        GetOnce();
        var bytes = MostRecentScene.Serialize();
        File.WriteAllBytes(filename,  bytes);
    }

    void UpdateState(string message)
    {
        CurrentState = $"{message} (Thread {Thread.CurrentThread.ManagedThreadId})";
    }
}