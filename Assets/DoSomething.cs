using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.MixedReality.SceneUnderstanding;
using SceneUnderstandingScenes;
using TMPro;
using UnityEngine;

public class DoSomething : MonoBehaviour
{
    [SerializeField] TextMeshPro[] texts;
    [SerializeField] TextMeshProUGUI[] textsGui;

    [SerializeField] private SceneSnapshotWrapper fallbackScene;
    [SerializeField] private SceneDrawer drawer;

    private Queue<string> otherMessages = new Queue<string>();

    private SceneManager sceneManager;

    private void OnEnable()
    {
        var sceneFetcher = SceneObserver.IsSupported()
            ? (ISceneFetcher) new SceneFetcher()
            : new BuiltInSceneFetcher(fallbackScene.sceneSnapshot);
        
        sceneManager = new SceneManager(sceneFetcher);
        sceneManager.onScene.AddListener(() => drawer.DrawScene(SceneSnapshot.Create(sceneManager.Scene)));
    }

    private void OnDisable()
    {
        if (sceneManager != null)
        {
            sceneManager.StopPolling();
            sceneManager = null;
        }
        drawer.Clear();
    }

    private void OnDestroy()
    {
        if (sceneManager != null)
        {
            sceneManager.StopPolling();
            sceneManager = null;
        }
        drawer.Clear();
    }

    void Start()
    {
        AppendLine("Loaded");
        Application.logMessageReceived += (condition, trace, type) => otherMessages.Enqueue($"{type}: {condition}");
    }

    void Update()
    {
        while (otherMessages.Any())
            AppendLine(otherMessages.Dequeue());

        SetLogText();
    }

    public void ToggleObservation()
    {
        sceneManager.StartPolling(1000);
    }

    public void TakeSnapshot()
    {
        sceneManager.TakeSnapshot();
    }
    
    private string currentLog = string.Empty;

    private void AppendLine(string line)
    {
        Console.WriteLine(line);

        var lines = currentLog.Split('\n')
            .Append(line)
            .Reverse()
            .Take(10)
            .Reverse();

        currentLog = string.Join(Environment.NewLine, lines);
        SetLogText();
    }

    private void SetLogText()
    {
         var initialStats = $@"State: {SceneUnderstandingState.CurrentState}
Last Error: {SceneUnderstandingState.CurrentError}
 Scene: {sceneManager.Scene?.SceneObjects?.Count} objects
 ";

        var logMessage = initialStats + currentLog;

        foreach (var text in texts)
        {
            text.text = logMessage;
        }

        foreach (var text in textsGui)
        {
            text.text = logMessage;
        }
    }
}