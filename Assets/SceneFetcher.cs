using System;
using Microsoft.MixedReality.SceneUnderstanding;

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