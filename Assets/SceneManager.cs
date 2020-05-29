using System;
using System.Threading.Tasks;
using Microsoft.MixedReality.SceneUnderstanding;
using Microsoft.MixedReality.Toolkit.Utilities;
using UnityEngine;
using UnityEngine.Events;

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