using Microsoft.MixedReality.SceneUnderstanding;

public interface ISceneFetcher
{
    Scene FetchScene();
    byte[] FetchSceneBytes();
}