using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.MixedReality.SceneUnderstanding;
using UnityEditor;
using UnityEngine;

namespace SceneUnderstandingScenes
{
    [Serializable]
    public class SceneSnapshot
    {
        public const int MagicNumber = 0x12345678;
        public bool isMatrixInferred;
        public Matrix4x4 originMatrix;
        
        [HideInInspector]
        public byte[] sceneBytes;

        private Scene _scene;
        public Scene Scene => _scene ?? (_scene = Scene.Deserialize(sceneBytes));
        
        public static SceneSnapshot Deserialize(byte[] bytes)
        {
            // Format
            // 4 bytes: Magic number
            // 4 bytes: Header size (int)
            // header: 
            //   4 bytes: headerVersion (int)
            //   1 byte: hasMatrix: (bool)
            //   43 bytes: matrix: (float)
            // [the rest]: serialized scene
            var sceneSnapshot = new SceneSnapshot();
            sceneSnapshot.originMatrix = new Matrix4x4();

            var offset = 0;
            var magicNumber = BitConverter.ToInt32(bytes, offset);

            var isWrapped = magicNumber == MagicNumber;
            if (isWrapped)
            {
                offset += sizeof(int);
                    
                var headerSize = BitConverter.ToInt32(bytes, offset);
                offset += sizeof(int);

                var headerVersion = BitConverter.ToInt32(bytes, offset);
                offset += sizeof(int);

                var hasMatrix = BitConverter.ToBoolean(bytes, offset);
                offset += sizeof(bool);

                sceneSnapshot.isMatrixInferred = !hasMatrix;
                if (hasMatrix)
                {
                    for (var i = 0; i < 16; i++)
                    {
                        sceneSnapshot.originMatrix[i] = BitConverter.ToSingle(bytes, offset);
                        offset += sizeof(float);
                    }
                }
            }
            else
            {
                sceneSnapshot.isMatrixInferred = true;
            }

            sceneSnapshot.sceneBytes = bytes.Skip(offset).ToArray();
            
            if (sceneSnapshot.isMatrixInferred)
                sceneSnapshot.originMatrix = InferMatrixFromSceneFloor(sceneSnapshot.Scene);
            
            return sceneSnapshot;
        }

        public byte[] Serialize()
        {
            return Serialize(sceneBytes, isMatrixInferred ? originMatrix : (Matrix4x4?)null);
        }
        
        public static SceneSnapshot Create(Scene scene)
        {
            var snapshot = new SceneSnapshot();
            var sceneOriginMatrix = ReadOriginMatrixFromScene(scene);
            snapshot.originMatrix = sceneOriginMatrix.HasValue ? sceneOriginMatrix.Value : InferMatrixFromSceneFloor(scene);
            snapshot.isMatrixInferred = sceneOriginMatrix == null;
            snapshot._scene = scene;
            return snapshot;
        }
        
        public static SceneSnapshot CreateFromDevice(byte[] sceneBytes)
        {
            var scene = Scene.Deserialize(sceneBytes);
            var snapshot = Create(scene);
            snapshot.sceneBytes = sceneBytes;
            return snapshot;
        }

        public Matrix4x4 ReadOrInferOriginMatrix(Scene scene)
        {
            return ReadOriginMatrixFromScene(scene) 
                   ?? InferMatrixFromSceneFloor(scene);
        }

        public static byte[] SerializeFromDevice(Scene scene, byte[] sceneBytes)
        {
            var originMatrix = ReadOriginMatrixFromScene(scene);
            return Serialize(sceneBytes, originMatrix);
        }

        private static byte[] Serialize(byte[] sceneBytes, Matrix4x4? originMatrix)
        {
            byte[] headerBytes;
            using (var headerStream = new MemoryStream())
            using (var writer = new StreamWriter(headerStream))
            {
                writer.Write(1); // header version
                writer.Write(originMatrix.HasValue);

                if (originMatrix.HasValue)
                {
                    for (var i = 0; i < 16; i++)
                    {
                        writer.Write(originMatrix.Value[i]);
                    }
                }

                headerBytes = headerStream.ToArray();
            }

            using (var stream = new MemoryStream())
            {
                using (var writer = new StreamWriter(stream, Encoding.Default, 1_000_000, true))
                {
                    writer.Write(MagicNumber);
                    writer.Write(headerBytes.Length);
                }

                stream.Write(headerBytes, 0, headerBytes.Length);
                stream.Write(sceneBytes, 0, sceneBytes.Length);
                return stream.ToArray();
            }
        }

        public static Matrix4x4? ReadOriginMatrixFromScene(Scene scene)
        {
            #if WINDOWS_UWP
                var nodeId = scene.OriginSpatialGraphNodeId;
                var sceneSpatialCoordinateSystem = Windows.Perception.Spatial.Preview.SpatialGraphInteropPreview.CreateCoordinateSystemForNode(nodeId);
                var unitySpatialCoordinateSystem = (Windows.Perception.Spatial.SpatialCoordinateSystem)System.Runtime.InteropServices.Marshal.GetObjectForIUnknown(
                    UnityEngine.XR.WSA.WorldManager.GetNativeISpatialCoordinateSystemPtr());

                return sceneSpatialCoordinateSystem.TryGetTransformTo(unitySpatialCoordinateSystem)?.ToUnity();
            #endif

            return null;
        }
        
        public static Matrix4x4 InferMatrixFromSceneFloor(Scene scene)
        {
            var floor = scene.SceneObjects.FirstOrDefault(o => o.Kind == SceneObjectKind.Floor);
            if (floor == null)
                return Matrix4x4.identity;

            var mainMatrix = floor.GetLocationAsMatrix().ToUnity();
            var sceneToUnityMatrix = mainMatrix.inverse;
            sceneToUnityMatrix = Matrix4x4.Rotate(Quaternion.Euler(90, 0, 0)) * sceneToUnityMatrix;
            return sceneToUnityMatrix;
        }
    }
}