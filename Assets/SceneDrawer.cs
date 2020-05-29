using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.MixedReality.SceneUnderstanding;
using SceneUnderstandingScenes;
using UnityEngine;

public class SceneDrawer : MonoBehaviour
{
    [SerializeField] Material floorMaterial;
    [SerializeField] Material wallMaterial;
    [SerializeField] Material platformMaterial;
    [SerializeField] Material otherMaterial;
    [SerializeField] Material ceilingMaterial;

    [SerializeField] SceneSnapshotWrapper gizmoScene;
    private SceneSnapshot currentScene;

    private Dictionary<Guid, GameObject> sceneObjects;
    private Dictionary<Guid, GameObject> sceneGeometries;

    void OnEnable()
    {
        Clear();
        sceneObjects = new Dictionary<Guid, GameObject>();
        sceneGeometries = new Dictionary<Guid, GameObject>();
        currentScene = null;
    }

    private void OnDisable()
    {
        currentScene = null;
    }

    private void OnDestroy()
    {
        currentScene = null;
    }

    public void DrawScene(SceneSnapshot scene)
    {
        currentScene = scene;
        
        var sceneToWorld = scene.originMatrix;
        var scenePosition = sceneToWorld.GetColumn(3);
        var sceneRotation = sceneToWorld.rotation;
        transform.SetPositionAndRotation(scenePosition, sceneRotation);
        
        var foundSceneObjects = new HashSet<Guid>();
        var foundSceneGeometries = new HashSet<Guid>();
        
        var sceneObjectKinds = new[] {SceneObjectKind.Floor, SceneObjectKind.Wall, SceneObjectKind.Ceiling, SceneObjectKind.Platform};
        foreach (var sceneObject in scene.Scene.SceneObjects.Where(o => o.Quads.Any() && sceneObjectKinds.Contains(o.Kind)))
        {
            foundSceneObjects.Add(sceneObject.Id);
            var componentTransform = CreateOrUpdateSceneObject(sceneObject);

            foreach (var quad in sceneObject.Quads)
            {
                foundSceneGeometries.Add(quad.Id);
                CreateOrUpdateQuad(componentTransform.transform, quad);
            }

            SetMaterials(componentTransform, sceneObject);
        }

        foreach (var id in sceneObjects.Keys.Where(id => !foundSceneObjects.Contains(id)).ToArray())
        {
            Destroy(sceneObjects[id]);
            sceneObjects.Remove(id);
        }
        
        foreach (var id in sceneGeometries.Keys.Where(id => !foundSceneGeometries.Contains(id)).ToArray())
        {
            Destroy(sceneGeometries[id]);
            sceneGeometries.Remove(id);
        }
    }

    private GameObject CreateOrUpdateQuad(Transform sceneObjectTransform, SceneQuad quad)
    {
        GameObject quadObj;
        if (sceneGeometries.ContainsKey(quad.Id))
        {
            quadObj = sceneGeometries[quad.Id];
        }
        else
        {
            quadObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quadObj.transform.SetParent(sceneObjectTransform, false);
            quadObj.name = $"Quad-{quad.Id}";
            sceneGeometries.Add(quad.Id, quadObj);
        }
        quadObj.transform.localScale = new Vector3(quad.Extents.X, quad.Extents.Y, 1);
        return quadObj;
    }
    
    private GameObject CreateOrUpdateSceneObject(SceneObject sceneObject)
    {
        var sceneObjectTransform = sceneObject.GetLocationAsMatrix().ToUnity();
        var position = sceneObjectTransform.GetColumn(3);
        var rotation = sceneObjectTransform.rotation;

        GameObject gameObj;
        if (sceneObjects.ContainsKey(sceneObject.Id))
        {
            gameObj = sceneObjects[sceneObject.Id];
        }
        else
        {
            gameObj = new GameObject($"{sceneObject.Kind}-{sceneObject.Id}");
            gameObj.transform.SetParent(transform);
            sceneObjects.Add(sceneObject.Id, gameObj);
        }
        
        gameObj.transform.localPosition = position;
        gameObj.transform.localRotation = rotation;
        return gameObj;
    }

    public void Clear()
    {
        sceneObjects = new Dictionary<Guid, GameObject>();
        sceneGeometries = new Dictionary<Guid, GameObject>();
        var infiniteStopper = 100;
        while (transform.childCount > 0 && infiniteStopper-- > 0)
            DestroyImmediate(transform.GetChild(0).gameObject);
    }

    private void OnDrawGizmos()
    {
        var scene = Application.isPlaying ? (currentScene ?? gizmoScene.sceneSnapshot) : gizmoScene.sceneSnapshot;
        if (scene == null)
            return;
        
        var sceneToUnityMatrix = scene.originMatrix;

        var floor = scene.Scene.SceneObjects.FirstOrDefault(o => o.Kind == SceneObjectKind.Floor);
        if (floor == null)
            return;
        
        var floorQuad = floor.Quads[0];
        Gizmos.color = Color.magenta;
        Gizmos.matrix = sceneToUnityMatrix * floor.GetLocationAsMatrix().ToUnity();
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(floorQuad.Extents.X*0.95f, floorQuad.Extents.Y*0.95f, 0.03f));
        
        foreach (var obj in scene.Scene.SceneObjects)
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
}