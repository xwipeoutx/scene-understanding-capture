using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.MixedReality.SceneUnderstanding;
using TMPro;
using UnityEngine;
using UnityEngine.Windows;

public class DoSomething : MonoBehaviour
{
    [SerializeField] TextMeshPro text;

    void Start()
    {
        AppendLine("Loaded");
    }
    
    public void DoTheThing()
    {
        AppendLine("Button clicked");
        Task.Factory.StartNew(DoTheThingAsync, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private async Task DoTheThingAsync()
    {
        try
        {
            Scene scene;
            if (SceneObserver.IsSupported())
            {
                AppendLine("Observer Supported - doing real observations");
                scene = await DoSomeRealObservations();
            }
            else
            {
                AppendLine("Observer not supported - doing fake observations");
                scene = await DoSomeFakeObservations();
            }
        }
        catch (Exception e)
        {
            AppendLine("*** Error: " + e.Message);
        }
    }

    private async Task<Scene> DoSomeFakeObservations()
    {
        await Task.Delay(1000);
        AppendLine("Waited a bit");

        await Task.Run(() => Thread.Sleep(1000));
        
        AppendLine("Waited a bit in the background");

        throw new NotSupportedException("No fakes here sonny");
    }

    private void AppendLine(string line)
    {
        var lines = text.text.Split('\n')
            .Append(line)
            .Take(10);

        var newLines = string.Join(Environment.NewLine, lines);
        text.text = newLines;
    }

    private async Task<Scene> DoSomeRealObservations()
    {
        AppendLine("Requesting access");
        var status = await SceneObserver.RequestAccessAsync();

        if (status != SceneObserverAccessStatus.Allowed)
        {
            throw new Exception($"Expected to get access.  Actually got: " + status);
        }
        
        AppendLine("Access granted! Querying scene...");
        var querySettings = new SceneQuerySettings()
        {
            EnableWorldMesh = true,
            EnableSceneObjectMeshes = true,
            EnableSceneObjectQuads = true,
            RequestedMeshLevelOfDetail = SceneMeshLevelOfDetail.Medium,
            EnableOnlyObservedSceneObjects = false
        };
        var computed = await SceneObserver.ComputeSerializedAsync(querySettings, 10);
        
        AppendLine("Query done! Got" + computed.Size + " bytes");
        var data = new byte[computed.Size];

        var filename = $"{Application.persistentDataPath}/serialized-scene-{DateTime.Now.ToFileTime()}.bin";
        AppendLine($"Saving to {filename}");
        File.WriteAllBytes(filename, data);
        AppendLine($"Saved to {filename}");

        var scene = Scene.Deserialize(data);
        AppendLine($"Summary: {scene.SceneObjects.Count} objects");
        return scene;
    }
}
