using System.IO;
using Microsoft.MixedReality.SceneUnderstanding;
using SceneUnderstandingScenes;
using UnityEditor.Experimental.AssetImporters;
using UnityEngine;

namespace Editor
{
    [ScriptedImporter(1, ".suscene")]
    public class SceneImporter : ScriptedImporter
    {
        public bool hello;
        
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var bytes = File.ReadAllBytes(ctx.assetPath);
            var sceneSnapshot = SceneSnapshot.Deserialize(bytes);

            var wrapper = ScriptableObject.CreateInstance<SceneSnapshotWrapper>();
            wrapper.sceneSnapshot = sceneSnapshot;
            
            ctx.AddObjectToAsset("Scene", wrapper);
            ctx.SetMainObject(wrapper);
        }
    }
}