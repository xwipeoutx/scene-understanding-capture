using System;
using System.Threading.Tasks;
using Microsoft.MixedReality.SceneUnderstanding;
using Microsoft.MixedReality.Toolkit.Utilities;
using UnityEngine;
using UnityEngine.Windows;

public interface ISceneUnderstander
{
    int SceneHash { get; }

    int SearchRadius { get; set; }
    string CurrentState { get; }
    byte[] SceneData { get; }
    Scene MostRecentScene { get; }
    int SceneSize { get; }
    int SceneObjectCount { get; }
    void StartPolling();
    void StopPolling();
    void TakeSnapshot();
}

public class FakeSceneUnderstander : ISceneUnderstander
{
    private readonly string resourcePath;

    public FakeSceneUnderstander(string resourcePath)
    {
        this.resourcePath = resourcePath;
    }
    
    public void StartPolling()
    {
        new TaskFactory().StartNew(Poll);
    }
    
    public void StopPolling()
    {
    }

    public void TakeSnapshot()
    {
        
    }

    public int SceneHash { get; private set; } = 0;
    public int SearchRadius { get; set; } = 5;
    public string CurrentState { get; private set; } = "Not started";
    public byte[] SceneData { get; private set; }
    public Scene MostRecentScene { get; private set;}
    
    public int SceneSize => SceneData?.Length ?? -1;
    public int SceneObjectCount => MostRecentScene?.SceneObjects?.Count ?? -1;

    private async Task Poll()
    {
        SceneHash = 1;
        CurrentState = "Loading resource";
        await new WaitForUpdate();
        await Task.Delay(1000);
        await new WaitForUpdate();
        SceneData = File.ReadAllBytes(resourcePath);
        CurrentState = "Deserializing resource";
        await new WaitForUpdate();
        await Task.Delay(500);
        await new WaitForUpdate();
        MostRecentScene = Scene.Deserialize(SceneData);
        CurrentState = "Scene Loaded";
    }
}

public class SceneUnderstander : ISceneUnderstander
{
    private bool shouldStop;

    public int SceneHash { get; private set; } = 0;
    public int SearchRadius { get; set; } = 5;
    public string CurrentState { get; private set; } = "Not started";
    public byte[] SceneData { get; private set; }
    public Scene MostRecentScene { get; private set; }

    public int SceneSize => SceneData?.Length ?? -1;
    public int SceneObjectCount => MostRecentScene?.SceneObjects?.Count ?? -1;
    
    public void StartPolling()
    {
        shouldStop = false;
        Task.Run(Poll);
    }
    
    public void StopPolling()
    {
        shouldStop = true;
    }

    private async Task Poll()
    {
        while (!shouldStop)
        {
            CurrentState = "Requesting access";
            var status = await SceneObserver.RequestAccessAsync();

            if (status != SceneObserverAccessStatus.Allowed)
            {
                CurrentState = $"FAILED - no access: {status}";
                throw new Exception($"Expected to get access.  Actually got: " + status);
            }

            CurrentState = "Access granted, waiting a bit";
            await Task.Delay(100);

            CurrentState = "Access granted! Querying scene...";
            var querySettings = new SceneQuerySettings()
            {
                EnableWorldMesh = false,
                EnableSceneObjectMeshes = false,
                EnableSceneObjectQuads = true,
                RequestedMeshLevelOfDetail = SceneMeshLevelOfDetail.Coarse,
                EnableOnlyObservedSceneObjects = false
            };

            var buffer = await SceneObserver.ComputeSerializedAsync(querySettings, SearchRadius);

            CurrentState = "Got scene buffer, deserializing";
            var bytes = new byte[buffer.Size];
            buffer.GetData(bytes);
            SceneData = bytes;
            
            MostRecentScene = Scene.Deserialize(bytes);
            CurrentState = "Scene Loaded";

            SceneHash++;
        }
    }

    public void TakeSnapshot()
    {
        var filename = $"{Application.persistentDataPath}/serialized-scene-{DateTime.Now.ToFileTime()}.bin";
        File.WriteAllBytes(filename, SceneData);
    }
}