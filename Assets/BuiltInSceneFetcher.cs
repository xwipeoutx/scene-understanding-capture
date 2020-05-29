using System;
using System.Threading.Tasks;
using Microsoft.MixedReality.SceneUnderstanding;
using SceneUnderstandingScenes;

public class BuiltInSceneFetcher : ISceneFetcher
{
    private readonly Func<SceneSnapshot> getSceneSnapshot;
    public SceneSnapshot snapshot;

    public BuiltInSceneFetcher(Func<SceneSnapshot> getSceneSnapshot)
    {
        this.getSceneSnapshot = getSceneSnapshot;
        this.snapshot = snapshot;
    }

    public Scene FetchScene()
    {
        return Task.Run(() => getSceneSnapshot().Scene).GetAwaiter().GetResult();
    }

    public byte[] FetchSceneBytes()
    {
        return Task.Run(() => getSceneSnapshot().sceneBytes).GetAwaiter().GetResult();
    }
}