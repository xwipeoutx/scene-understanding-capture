using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.MixedReality.SceneUnderstanding;
using SceneUnderstandingScenes;
using TMPro;
using UnityEngine;

public class DoSomething : MonoBehaviour
{
    [SerializeField] Transform worldParent;
    [SerializeField] TextMeshPro[] texts;
    [SerializeField] TextMeshProUGUI[] textsGui;

    [SerializeField] GameObject oneMetreQuad;
    [SerializeField] GameObject justASphere;

    [SerializeField] Material floorMaterial;
    [SerializeField] Material wallMaterial;
    [SerializeField] Material platformMaterial;
    [SerializeField] Material otherMaterial;
    [SerializeField] Material ceilingMaterial;
    [SerializeField] private SceneSnapshotWrapper fallbackScene;

    private Queue<string> otherMessages = new Queue<string>();

    private ISceneUnderstander sceneUnderstander;

    private void OnEnable()
    {
        sceneUnderstander = SceneObserver.IsSupported()
            ? (ISceneUnderstander) new SceneUnderstander()
            : new FakeSceneUnderstander(fallbackScene.sceneSnapshot);
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

        var sceneToWorld = scene.originMatrix;
        var scenePosition = sceneToWorld.GetColumn(3);
        var sceneRotation = sceneToWorld.rotation;
        worldParent.transform.SetPositionAndRotation(scenePosition, sceneRotation);
        
        var sceneObjectKinds = new[] {SceneObjectKind.Floor, SceneObjectKind.Wall, SceneObjectKind.Ceiling, SceneObjectKind.Platform};
        foreach (var sceneObject in scene.Scene.SceneObjects.Where(o => o.Quads.Any() && sceneObjectKinds.Contains(o.Kind)))
        {
            var gameObj = new GameObject($"{sceneObject.Kind}-{sceneObject.Id}");
            gameObj.transform.SetParent(worldParent);
            var sceneObjectTransform = sceneObject.GetLocationAsMatrix().ToUnity();

            var position = sceneObjectTransform.GetColumn(3);
            var rotation = sceneObjectTransform.rotation;
            gameObj.transform.localPosition = position;
            gameObj.transform.localRotation = rotation;

            foreach (var quad in sceneObject.Quads)
            {
                var quadObj = Instantiate(oneMetreQuad, gameObj.transform);
                quadObj.name = $"Quad-{quad.Id}";
                quadObj.transform.localScale = new Vector3(quad.Extents.X, quad.Extents.Y, 1);
            }

            SetMaterials(gameObj, sceneObject);
        }
    }

    private SceneSnapshot gizmoScene;
    private void OnDrawGizmos()
    {
        if (gizmoScene == null)
        {
            gizmoScene = fallbackScene.sceneSnapshot;
        }
        
        if (gizmoScene == null)
            return;
        
        
        var sceneToUnityMatrix = gizmoScene.originMatrix;

        var floor = gizmoScene.Scene.SceneObjects.First(o => o.Kind == SceneObjectKind.Floor);
        var floorQuad = floor.Quads[0];
        Gizmos.color = Color.magenta;
        Gizmos.matrix = sceneToUnityMatrix * floor.GetLocationAsMatrix().ToUnity();
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(floorQuad.Extents.X*0.95f, floorQuad.Extents.Y*0.95f, 0.03f));
        
        foreach (var obj in gizmoScene.Scene.SceneObjects)
        {
            var baseColor = Color.white;
            switch (obj.Kind)
            {
                case SceneObjectKind.Wall:
                    baseColor = Color.white;
                    break;
                case SceneObjectKind.Floor:
                    baseColor = Color.red;
                    break;
                case SceneObjectKind.Ceiling:
                    baseColor = Color.green;
                    break;
                case SceneObjectKind.Platform:
                    Gizmos.color = Color.yellow;
                    break;
                case SceneObjectKind.Unknown:
                case SceneObjectKind.World:
                case SceneObjectKind.CompletelyInferred:
                case SceneObjectKind.Background:
                    Gizmos.color = Color.cyan;
                    break;
                default:
                    continue;
            }

            var localPositionMatrix = obj.GetLocationAsMatrix().ToUnity();
            
            Gizmos.color = baseColor;
            Gizmos.matrix = sceneToUnityMatrix * localPositionMatrix;
            foreach (var quad in obj.Quads)
            {
                var quadExtents = new Vector3(quad.Extents.X, quad.Extents.Y, 0.01f);
                Gizmos.DrawWireCube(Vector3.zero, quadExtents);
            }
        }
    }

    private Matrix4x4 SceneToUnityTransform(Scene scene)
    {
        if (scene == null)
            return Matrix4x4.identity;

        if (Application.isEditor)
        {
            return FloorDrivenSceneToUnityTransform(scene);
        }


        var sceneToUnityTransform = TransformUtils.GetSceneToUnityTransform(scene.OriginSpatialGraphNodeId, true);

        if (!sceneToUnityTransform.HasValue)
        {
            return FloorDrivenSceneToUnityTransform(scene);
        }

        // stupid method above does this for us. NO! PUT IT BACK!
        sceneToUnityTransform = TransformUtils.ConvertRightHandedMatrix4x4ToLeftHanded(sceneToUnityTransform.Value);
        return sceneToUnityTransform.Value.ToUnity();
    }

    private Matrix4x4 FloorDrivenSceneToUnityTransform(Scene scene)
    {
        var floor = scene.SceneObjects.FirstOrDefault(o => o.Kind == SceneObjectKind.Floor);
        if (floor == null)
            return Matrix4x4.identity;

        var mainMatrix = floor.GetLocationAsMatrix().ToUnity();
        var sceneToUnityMatrix = mainMatrix.inverse;
        sceneToUnityMatrix = Matrix4x4.Rotate(Quaternion.Euler(90, 0, 0)) * sceneToUnityMatrix;
        return sceneToUnityMatrix;
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