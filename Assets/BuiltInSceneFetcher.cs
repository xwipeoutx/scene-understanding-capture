using System.Threading.Tasks;
using Microsoft.MixedReality.SceneUnderstanding;
using SceneUnderstandingScenes;

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