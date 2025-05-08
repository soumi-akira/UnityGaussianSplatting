// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using GaussianSplatting.Runtime;
using GaussianSplatting.Runtime.Utils;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace GaussianSplatting.Runtime
{
    [BurstCompile]
    public class GaussianSplatRuntimeAssetCreator
    {
        const string kCamerasJson = "cameras.json";

        DataQuality m_Quality = DataQuality.Medium;
        GaussianSplatAsset.VectorFormat m_FormatPos;
        GaussianSplatAsset.VectorFormat m_FormatScale;
        GaussianSplatAsset.ColorFormat m_FormatColor;
        GaussianSplatAsset.SHFormat m_FormatSH;

        bool isUsingChunks =>
            m_FormatPos != GaussianSplatAsset.VectorFormat.Float32 ||
            m_FormatScale != GaussianSplatAsset.VectorFormat.Float32 ||
            m_FormatColor != GaussianSplatAsset.ColorFormat.Float32x4 ||
            m_FormatSH != GaussianSplatAsset.SHFormat.Float32;

        public void SetQualityLevel(DataQuality quality)
        {
            m_Quality = quality;

            if(m_Quality == DataQuality.Custom) return;
            var res = Quality.GetFormatFromQualityLevel(m_Quality);
            
            m_FormatPos = res.FormatPos;
            m_FormatScale = res.FormatScale;
            m_FormatColor = res.FormatColor;
            m_FormatSH = res.FormatSH;
        }

        public unsafe void CreateAsset(string inputFile, bool importCameras = false){
            GaussianSplatAsset.CameraInfo[] cameras = GaussianSplatAssetCreateTask.LoadJsonCamerasFile(inputFile, kCamerasJson, importCameras);
            NativeArray<InputSplatData> inputSplats = GaussianSplatAssetCreateTask.LoadInputSplatFile(inputFile);

            CreateAsset(inputSplats, cameras);
        }

        public unsafe void CreateAsset(NativeArray<InputSplatData> inputSplats, GaussianSplatAsset.CameraInfo[] cameras = null)
        {
            if (inputSplats.Length == 0)
            {
                Debug.LogError("No splats found in input file.");
                return;
            }

            float3 boundsMin, boundsMax;
            var boundsJob = new GaussianSplatAssetCreateTask.CalcBoundsJob
            {
                m_BoundsMin = &boundsMin,
                m_BoundsMax = &boundsMax,
                m_SplatData = inputSplats
            };
            boundsJob.Schedule().Complete();

            GaussianSplatAssetCreateTask.ReorderMorton(inputSplats, boundsMin, boundsMax);

            // cluster SHs
            NativeArray<int> splatSHIndices = default;
            NativeArray<GaussianSplatAsset.SHTableItemFloat16> clusteredSHs = default;
            if (m_FormatSH >= GaussianSplatAsset.SHFormat.Cluster64k)
            {
                GaussianSplatAssetCreateTask.ClusterSHs(inputSplats, m_FormatSH, out clusteredSHs, out splatSHIndices, null);
            }

            // GaussianSplatAsset asset = ScriptableObject.CreateInstance<GaussianSplatAsset>();
            // asset.Initialize(inputSplats.Length, m_FormatPos, m_FormatScale, m_FormatColor, m_FormatSH, boundsMin, boundsMax, cameras);
            // asset.name = baseName;

            // var dataHash = new Hash128((uint)asset.splatCount, (uint)asset.formatVersion, 0, 0);
            var dataHash = new Hash128();

            NativeArray<byte> chunkData = default;
            NativeArray<byte> posData = default;
            NativeArray<byte> otherData = default;
            NativeArray<byte> colorData = default;
            NativeArray<byte> shData = default;

            // if we are using full lossless (FP32) data, then do not use any chunking, and keep data as-is
            bool useChunks = isUsingChunks;
            if (useChunks)
                GaussianSplatAssetCreateTask.CreateChunkData(inputSplats, out chunkData, ref dataHash);
            GaussianSplatAssetCreateTask.CreatePositionsData(m_FormatPos, inputSplats, out posData, ref dataHash);
            GaussianSplatAssetCreateTask.CreateOtherData(m_FormatScale, inputSplats, out otherData, ref dataHash, splatSHIndices);
            GaussianSplatAssetCreateTask.CreateColorData(m_FormatColor, inputSplats, out colorData, ref dataHash);
            GaussianSplatAssetCreateTask.CreateSHData(m_FormatSH, inputSplats, out shData, ref dataHash, clusteredSHs);
            // asset.SetDataHash(dataHash);

            splatSHIndices.Dispose();
            clusteredSHs.Dispose();
            inputSplats.Dispose();

            // asset.SetAssetFiles(
            //     useChunks ? AssetDatabase.LoadAssetAtPath<TextAsset>(pathChunk) : null,
            //     AssetDatabase.LoadAssetAtPath<TextAsset>(pathPos),
            //     AssetDatabase.LoadAssetAtPath<TextAsset>(pathOther),
            //     AssetDatabase.LoadAssetAtPath<TextAsset>(pathCol),
            //     AssetDatabase.LoadAssetAtPath<TextAsset>(pathSh));
        }
    }
}
