using UnityEngine;
using System.IO;
using System;
using GaussianSplatting.Runtime;
using GaussianSplatting.Runtime.Utils;

public class Test : MonoBehaviour
{
    GaussianSplatRenderer target;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, "chairs.ply");
        GaussianSplatRuntimeAssetCreator creator = new GaussianSplatRuntimeAssetCreator();
        creator.SetQualityLevel(DataQuality.Medium);
        var asset = creator.CreateAsset("test", filePath, false);

        Debug.Log($"Asset Name: {asset.name}");

        target = GetComponent<GaussianSplatRenderer>();
        
        target.InjectAsset(asset);
        
        // 10秒後にnullアセットを注入
        Invoke("ClearAsset", 10f);
    }

    // 10秒後に呼び出されるメソッド
    void ClearAsset()
    {
        Debug.Log("Clearing asset...");
        target.InjectAsset(null);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
