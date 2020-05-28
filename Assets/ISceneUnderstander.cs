using Microsoft.MixedReality.SceneUnderstanding;
using SceneUnderstandingScenes;

public interface ISceneUnderstander
{
    int SceneHash { get; }

    int SearchRadius { get; set; }
    string CurrentState { get; }
    int SceneSize { get; }
    int SceneObjectCount { get; }

    SceneSnapshot MostRecentScene { get; }
    
    void StartPolling();
    void StopPolling();
    void TakeSnapshot();
}