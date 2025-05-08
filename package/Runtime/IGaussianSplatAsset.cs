using UnityEngine;
using Unity.Collections;

namespace GaussianSplatting.Runtime
{
    public interface IGaussianSplatAsset
    {
        string name { get; }
        int splatCount { get; }
        IGaussianSplatData posData { get; }
        IGaussianSplatData otherData { get; }
        IGaussianSplatData shData { get; }
        IGaussianSplatData colorData { get; }
        IGaussianSplatData chunkData { get; }
        Vector3 boundsMin { get; }
        Vector3 boundsMax { get; }
        GaussianSplatAsset.VectorFormat posFormat { get; }
        GaussianSplatAsset.VectorFormat scaleFormat { get; }
        GaussianSplatAsset.SHFormat shFormat { get; }
        GaussianSplatAsset.ColorFormat colorFormat { get; }
        Hash128 dataHash { get; }
        int formatVersion { get; }
        GaussianSplatAsset.CameraInfo[] cameras { get; }

        void Dispose();
    }

    public interface IGaussianSplatData
    {
        long dataSize { get; }
        NativeArray<T> GetData<T>() where T : struct;
    }
}