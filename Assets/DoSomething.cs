using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.MixedReality.SceneUnderstanding;
using TMPro;
using UnityEngine;

public class DoSomething : MonoBehaviour
{
    [SerializeField] TextMeshPro[] texts;
    [SerializeField] TextMeshProUGUI[] textsGui;

    [SerializeField] GameObject oneMetreQuad;
    [SerializeField] GameObject justASphere;

    [SerializeField] Material floorMaterial;
    [SerializeField] Material wallMaterial;
    [SerializeField] Material platformMaterial;
    [SerializeField] Material otherMaterial;
    [SerializeField] Material ceilingMaterial;
    [SerializeField] private UnityEngine.Object sceneAsset;

    private Queue<string> otherMessages = new Queue<string>();

    private ISceneUnderstander sceneUnderstander;

    private void OnEnable()
    {
        sceneUnderstander = SceneObserver.IsSupported()
            ? (ISceneUnderstander) new SceneUnderstander()
            : new FakeSceneUnderstander(SceneToLoad);
    }

    private void OnDisable()
    {
        sceneUnderstander?.StopPolling();
        sceneUnderstander = null;
    }

    private void OnDestroy()
    {
        sceneUnderstander?.StopPolling();
        sceneUnderstander = null;
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
        sceneUnderstander.StartPolling();
    }

    public void TakeSnapshot()
    {
        sceneUnderstander.TakeSnapshot();
    }

    public void DrawScene()
    {
        if (sceneUnderstander.MostRecentScene == null)
        {
            AppendLine("No scene to draw!");
            return;
        }

        var scene = sceneUnderstander.MostRecentScene;
        int infiniteStopper = 100;
        while (transform.childCount > 0 && infiniteStopper-- > 0)
            DestroyImmediate(transform.GetChild(0).gameObject);

        var floor = scene.SceneObjects.FirstOrDefault(o => o.Kind == SceneObjectKind.Floor);
        var orientation = Quaternion.identity;

        if (floor != null)
        {
            var floorOrientation = floor.Orientation.ToUnity();
            AppendLine("Floor orientation = " + floorOrientation.eulerAngles);

            orientation = Quaternion.Inverse(floorOrientation);
            var correction = Quaternion.FromToRotation(Vector3.back, Vector3.up);
            orientation = correction * orientation;
        }

        var sceneObjectKinds = new[] {SceneObjectKind.Floor, SceneObjectKind.Wall, SceneObjectKind.Ceiling, SceneObjectKind.Platform};
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
                foreach (var meshRenderer in meshes)
                {
                    meshRenderer.sharedMaterial = ceilingMaterial;
                }

                break;
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

    private string SceneToLoad
    {
        get
        {
#if UNITY_EDITOR
            var sceneToLoad = UnityEditor.AssetDatabase.GetAssetPath(sceneAsset);
#else
            var sceneToLoad = $"C:/Code/sandbox/scene-understanding-test/Scene Understanding/Assets/SceneUnderstandingScenes/SU-Purple.bytes";
#endif
            return sceneToLoad;
        }
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
        var initialStats = $@"State: {sceneUnderstander.CurrentState}
Scene: {sceneUnderstander.SceneHash}: {sceneUnderstander.SceneObjectCount} objects ({sceneUnderstander.SearchRadius}m radius)
Data size: {sceneUnderstander.SceneSize}
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