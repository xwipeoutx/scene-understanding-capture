using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.MixedReality.SceneUnderstanding;
using Microsoft.MixedReality.Toolkit.Utilities;
using SceneUnderstandingScenes;
using UnityEngine;
using UnityEngine.Windows;

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