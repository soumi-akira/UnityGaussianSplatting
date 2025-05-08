using UnityEngine;
using System.IO;
using System;
using System.Collections;
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

        Debug.Log($"Asset Loaded");

        target = GetComponent<GaussianSplatRenderer>();
        
        target.InjectAsset(asset);
        
        StartCoroutine(CountdownAndClearAsset(3));
    }

    IEnumerator CountdownAndClearAsset(int seconds)
    {
        for (int i = seconds; i > 0; i--)
        {
            Debug.Log($"Count down to clear: {i}");
            yield return new WaitForSeconds(1f);
        }
        ClearAsset();
    }

    void ClearAsset()
    {
        Debug.Log("Clear asset");
        target.InjectAsset(null);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
