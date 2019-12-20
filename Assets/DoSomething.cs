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

    [SerializeField] GameObject oneMetreQuad;
    [SerializeField] GameObject justASphere;

    [SerializeField] Material floorMaterial;
    [SerializeField] Material wallMaterial;
    [SerializeField] Material platformMaterial;
    [SerializeField] Material otherMaterial;


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

            int infiniteStopper = 100;
            while (transform.childCount > 0 && infiniteStopper-- > 0)
                DestroyImmediate(transform.GetChild(0).gameObject);

            var floor = scene.SceneObjects.FirstOrDefault(o => o.Kind == SceneObjectKind.Floor);
            var orientation = Quaternion.identity;

            if (floor != null)
            {
                orientation =  Quaternion.Inverse(floor.Orientation.ToUnity());
                if (SceneObserver.IsSupported())
                {
                    var correction = Quaternion.FromToRotation(Vector3.back, Vector3.up);
                    orientation = correction * orientation;
                }
            }
            
            var sceneObjectKinds = new[] { SceneObjectKind.Floor, SceneObjectKind.Wall, SceneObjectKind.Ceiling, SceneObjectKind.Platform};
            foreach (var sceneObject in scene.SceneObjects.Where(o => o.Quads.Any() && sceneObjectKinds.Contains(o.Kind)))
            {
                var gameObj = new GameObject($"{sceneObject.Kind}-{sceneObject.Id}");
                gameObj.transform.parent = transform;
                var trsMatrix = sceneObject.GetLocationAsMatrix().ToUnity();
                
                Vector3 position = trsMatrix.GetColumn(3);
                var rotation = Quaternion.LookRotation(
                    trsMatrix.GetColumn(2),
                    trsMatrix.GetColumn(1)
                );

                gameObj.transform.position = orientation * position;
                gameObj.transform.rotation = orientation * rotation;
                
                foreach (var quad in sceneObject.Quads)
                {
                    var quadObj = Instantiate(oneMetreQuad, gameObj.transform);
                    quadObj.name = $"Quad-{quad.Id}";
                    quadObj.transform.localScale = new Vector3(quad.Extents.X, quad.Extents.Y, 1);
                }

                SetMaterials(gameObj, sceneObject);
            }
        }
        catch (Exception e)
        {
            AppendLine("*** Error: " + e.Message);
        }
    }

    private void SetMaterials(GameObject gameObj, SceneObject sceneObject)
    {
        var meshes = gameObj.GetComponentsInChildren<MeshRenderer>();
        switch (sceneObject.Kind)
        {
            case SceneObjectKind.Wall:
                foreach (var meshRenderer in meshes)
                {
                    meshRenderer.sharedMaterial = wallMaterial;
                }

                break;
            case SceneObjectKind.Ceiling:
            case SceneObjectKind.Floor:
                foreach (var meshRenderer in meshes)
                {
                    meshRenderer.sharedMaterial = floorMaterial;
                }

                break;
            case SceneObjectKind.Platform:
                foreach (var meshRenderer in meshes)
                {
                    meshRenderer.sharedMaterial = platformMaterial;
                }

                break;
                
            case SceneObjectKind.Background:
            case SceneObjectKind.Unknown:
            case SceneObjectKind.World:
            case SceneObjectKind.CompletelyInferred:
            default:
                foreach (var meshRenderer in meshes)
                {
                    meshRenderer.sharedMaterial = otherMaterial;
                }

                break;
        }
    }

    private async Task<Scene> DoSomeFakeObservations()
    {
        await Task.Delay(1000);
        AppendLine("Waited a bit");

        await Task.Run(() => Thread.Sleep(100));

        AppendLine("Waited a bit in the background");

        var sceneToLoad = $"C:/Code/sandbox/scene-understanding-test/Scene Understanding/Assets/SceneUnderstandingScenes/SU-Purple.bytes";
        var bytes = File.ReadAllBytes(sceneToLoad);

        return Scene.Deserialize(bytes);

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

        AppendLine("Waiting a bit");
        await Task.Delay(100);

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
        
        AppendLine("Waiting a bit");
        await Task.Delay(2000);

        var filename = $"{Application.persistentDataPath}/serialized-scene-{DateTime.Now.ToFileTime()}.bin";
        AppendLine($"Saving to {filename}");
        AppendLine("Waiting a bit");
        await Task.Delay(2000);
        File.WriteAllBytes(filename, data);
        AppendLine($"Saved to {filename}");
        AppendLine("Waiting a bit");
        await Task.Delay(2000);

        var scene = Scene.Deserialize(data);
        AppendLine($"Summary: {scene.SceneObjects.Count} objects");
        return scene;
    }
    
}