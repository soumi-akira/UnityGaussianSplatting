// SPDX-License-Identifier: MIT

using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace GaussianSplatting.Runtime
{
    public class GaussianSplatRuntimeAsset
    {
        public const int kCurrentVersion = 2023_10_20;
        public const int kChunkSize = 256;
        public const int kTextureWidth = 2048; // allows up to 32M splats on desktop GPU (2k width x 16k height)
        public const int kMaxSplats = 8_600_000; // mostly due to 2GB GPU buffer size limit when exporting a splat (2GB / 248B is just over 8.6M)

        [SerializeField] int m_FormatVersion;
        [SerializeField] int m_SplatCount;
        [SerializeField] Vector3 m_BoundsMin;
        [SerializeField] Vector3 m_BoundsMax;
        [SerializeField] Hash128 m_DataHash;

        public int formatVersion => m_FormatVersion;
        public int splatCount => m_SplatCount;
        public Vector3 boundsMin => m_BoundsMin;
        public Vector3 boundsMax => m_BoundsMax;
        public Hash128 dataHash => m_DataHash;

        public void Initialize(int splats, GaussianSplatAsset.VectorFormat formatPos, GaussianSplatAsset.VectorFormat formatScale, GaussianSplatAsset.ColorFormat formatColor, GaussianSplatAsset.SHFormat formatSh, Vector3 bMin, Vector3 bMax, GaussianSplatAsset.CameraInfo[] cameraInfos)
        {
            m_SplatCount = splats;
            m_FormatVersion = kCurrentVersion;
            m_PosFormat = formatPos;
            m_ScaleFormat = formatScale;
            m_ColorFormat = formatColor;
            m_SHFormat = formatSh;
            m_Cameras = cameraInfos;
            m_BoundsMin = bMin;
            m_BoundsMax = bMax;
        }

        public void SetDataHash(Hash128 hash)
        {
            m_DataHash = hash;
        }

        public void SetAssetFiles(TextAsset dataChunk, TextAsset dataPos, TextAsset dataOther, TextAsset dataColor, TextAsset dataSh)
        {
            m_ChunkData = dataChunk;
            m_PosData = dataPos;
            m_OtherData = dataOther;
            m_ColorData = dataColor;
            m_SHData = dataSh;
        }

        [SerializeField] GaussianSplatAsset.VectorFormat m_PosFormat = GaussianSplatAsset.VectorFormat.Norm11;
        [SerializeField] GaussianSplatAsset.VectorFormat m_ScaleFormat = GaussianSplatAsset.VectorFormat.Norm11;
        [SerializeField] GaussianSplatAsset.SHFormat m_SHFormat = GaussianSplatAsset.SHFormat.Norm11;
        [SerializeField] GaussianSplatAsset.ColorFormat m_ColorFormat;

        [SerializeField] TextAsset m_PosData;
        [SerializeField] TextAsset m_ColorData;
        [SerializeField] TextAsset m_OtherData;
        [SerializeField] TextAsset m_SHData;
        // Chunk data is optional (if data formats are fully lossless then there's no chunking)
        [SerializeField] TextAsset m_ChunkData;

        [SerializeField] GaussianSplatAsset.CameraInfo[] m_Cameras;

        public GaussianSplatAsset.VectorFormat posFormat => m_PosFormat;
        public GaussianSplatAsset.VectorFormat scaleFormat => m_ScaleFormat;
        public GaussianSplatAsset.SHFormat shFormat => m_SHFormat;
        public GaussianSplatAsset.ColorFormat colorFormat => m_ColorFormat;

        public TextAsset posData => m_PosData;
        public TextAsset colorData => m_ColorData;
        public TextAsset otherData => m_OtherData;
        public TextAsset shData => m_SHData;
        public TextAsset chunkData => m_ChunkData;
        public GaussianSplatAsset.CameraInfo[] cameras => m_Cameras;
    }
}
