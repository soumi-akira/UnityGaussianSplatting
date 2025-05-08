// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using GaussianSplatting.Editor.Utils;
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

namespace GaussianSplatting.Editor
{
    [BurstCompile]
    public class GaussianSplatAssetCreator : EditorWindow
    {
        const string kProgressTitle = "Creating Gaussian Splat Asset";
        const string kCamerasJson = "cameras.json";
        const string kPrefQuality = "nesnausk.GaussianSplatting.CreatorQuality";
        const string kPrefOutputFolder = "nesnausk.GaussianSplatting.CreatorOutputFolder";

        readonly FilePickerControl m_FilePicker = new();

        [SerializeField] string m_InputFile;
        [SerializeField] bool m_ImportCameras = true;

        [SerializeField] string m_OutputFolder = "Assets/GaussianAssets";
        [SerializeField] DataQuality m_Quality = DataQuality.Medium;
        [SerializeField] GaussianSplatAsset.VectorFormat m_FormatPos;
        [SerializeField] GaussianSplatAsset.VectorFormat m_FormatScale;
        [SerializeField] GaussianSplatAsset.ColorFormat m_FormatColor;
        [SerializeField] GaussianSplatAsset.SHFormat m_FormatSH;

        string m_ErrorMessage;
        string m_PrevFilePath;
        int m_PrevVertexCount;
        long m_PrevFileSize;

        bool isUsingChunks =>
            m_FormatPos != GaussianSplatAsset.VectorFormat.Float32 ||
            m_FormatScale != GaussianSplatAsset.VectorFormat.Float32 ||
            m_FormatColor != GaussianSplatAsset.ColorFormat.Float32x4 ||
            m_FormatSH != GaussianSplatAsset.SHFormat.Float32;

        [MenuItem("Tools/Gaussian Splats/Create GaussianSplatAsset")]
        public static void Init()
        {
            var window = GetWindowWithRect<GaussianSplatAssetCreator>(new Rect(50, 50, 360, 340), false, "Gaussian Splat Creator", true);
            window.minSize = new Vector2(320, 320);
            window.maxSize = new Vector2(1500, 1500);
            window.Show();
        }

        void Awake()
        {
            m_Quality = (DataQuality)EditorPrefs.GetInt(kPrefQuality, (int)DataQuality.Medium);
            m_OutputFolder = EditorPrefs.GetString(kPrefOutputFolder, "Assets/GaussianAssets");
        }

        void OnEnable()
        {
            ApplyQualityLevel();
        }

        void OnGUI()
        {
            EditorGUILayout.Space();
            GUILayout.Label("Input data", EditorStyles.boldLabel);
            var rect = EditorGUILayout.GetControlRect(true);
            m_InputFile = m_FilePicker.PathFieldGUI(rect, new GUIContent("Input PLY/SPZ File"), m_InputFile, "ply,spz", "PointCloudFile");
            m_ImportCameras = EditorGUILayout.Toggle("Import Cameras", m_ImportCameras);

            if (m_InputFile != m_PrevFilePath && !string.IsNullOrWhiteSpace(m_InputFile))
            {
                m_PrevVertexCount = 0;
                m_ErrorMessage = null;
                try
                {
                    m_PrevVertexCount = GaussianFileReader.ReadFileHeader(m_InputFile);
                }
                catch (Exception ex)
                {
                    m_ErrorMessage = ex.Message;
                }

                m_PrevFileSize = File.Exists(m_InputFile) ? new FileInfo(m_InputFile).Length : 0;
                m_PrevFilePath = m_InputFile;
            }

            if (m_PrevVertexCount > 0)
                EditorGUILayout.LabelField("File Size", $"{EditorUtility.FormatBytes(m_PrevFileSize)} - {m_PrevVertexCount:N0} splats");
            else
                GUILayout.Space(EditorGUIUtility.singleLineHeight);

            EditorGUILayout.Space();
            GUILayout.Label("Output", EditorStyles.boldLabel);
            rect = EditorGUILayout.GetControlRect(true);
            string newOutputFolder = m_FilePicker.PathFieldGUI(rect, new GUIContent("Output Folder"), m_OutputFolder, null, "GaussianAssetOutputFolder");
            if (newOutputFolder != m_OutputFolder)
            {
                m_OutputFolder = newOutputFolder;
                EditorPrefs.SetString(kPrefOutputFolder, m_OutputFolder);
            }

            var newQuality = (DataQuality) EditorGUILayout.EnumPopup("Quality", m_Quality);
            if (newQuality != m_Quality)
            {
                m_Quality = newQuality;
                EditorPrefs.SetInt(kPrefQuality, (int)m_Quality);
                ApplyQualityLevel();
            }

            long sizePos = 0, sizeOther = 0, sizeCol = 0, sizeSHs = 0, totalSize = 0;
            if (m_PrevVertexCount > 0)
            {
                sizePos = GaussianSplatAsset.CalcPosDataSize(m_PrevVertexCount, m_FormatPos);
                sizeOther = GaussianSplatAsset.CalcOtherDataSize(m_PrevVertexCount, m_FormatScale);
                sizeCol = GaussianSplatAsset.CalcColorDataSize(m_PrevVertexCount, m_FormatColor);
                sizeSHs = GaussianSplatAsset.CalcSHDataSize(m_PrevVertexCount, m_FormatSH);
                long sizeChunk = isUsingChunks ? GaussianSplatAsset.CalcChunkDataSize(m_PrevVertexCount) : 0;
                totalSize = sizePos + sizeOther + sizeCol + sizeSHs + sizeChunk;
            }

            const float kSizeColWidth = 70;
            EditorGUI.BeginDisabledGroup(m_Quality != DataQuality.Custom);
            EditorGUI.indentLevel++;
            GUILayout.BeginHorizontal();
            m_FormatPos = (GaussianSplatAsset.VectorFormat)EditorGUILayout.EnumPopup("Position", m_FormatPos);
            GUILayout.Label(sizePos > 0 ? EditorUtility.FormatBytes(sizePos) : string.Empty, GUILayout.Width(kSizeColWidth));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            m_FormatScale = (GaussianSplatAsset.VectorFormat)EditorGUILayout.EnumPopup("Scale", m_FormatScale);
            GUILayout.Label(sizeOther > 0 ? EditorUtility.FormatBytes(sizeOther) : string.Empty, GUILayout.Width(kSizeColWidth));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            m_FormatColor = (GaussianSplatAsset.ColorFormat)EditorGUILayout.EnumPopup("Color", m_FormatColor);
            GUILayout.Label(sizeCol > 0 ? EditorUtility.FormatBytes(sizeCol) : string.Empty, GUILayout.Width(kSizeColWidth));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            m_FormatSH = (GaussianSplatAsset.SHFormat) EditorGUILayout.EnumPopup("SH", m_FormatSH);
            GUIContent shGC = new GUIContent();
            shGC.text = sizeSHs > 0 ? EditorUtility.FormatBytes(sizeSHs) : string.Empty;
            if (m_FormatSH >= GaussianSplatAsset.SHFormat.Cluster64k)
            {
                shGC.tooltip = "Note that SH clustering is not fast! (3-10 minutes for 6M splats)";
                shGC.image = EditorGUIUtility.IconContent("console.warnicon.sml").image;
            }
            GUILayout.Label(shGC, GUILayout.Width(kSizeColWidth));
            GUILayout.EndHorizontal();
            EditorGUI.indentLevel--;
            EditorGUI.EndDisabledGroup();
            if (totalSize > 0)
                EditorGUILayout.LabelField("Asset Size", $"{EditorUtility.FormatBytes(totalSize)} - {(double) m_PrevFileSize / totalSize:F2}x smaller");
            else
                GUILayout.Space(EditorGUIUtility.singleLineHeight);


            EditorGUILayout.Space();
            GUILayout.BeginHorizontal();
            GUILayout.Space(30);
            if (GUILayout.Button("Create Asset"))
            {
                CreateAsset();
            }
            GUILayout.Space(30);
            GUILayout.EndHorizontal();

            if (!string.IsNullOrWhiteSpace(m_ErrorMessage))
            {
                EditorGUILayout.HelpBox(m_ErrorMessage, MessageType.Error);
            }
        }

        void ApplyQualityLevel()
        {
            if(m_Quality == DataQuality.Custom) return;
            var res = Quality.GetFormatFromQualityLevel(m_Quality);
            
            m_FormatPos = res.FormatPos;
            m_FormatScale = res.FormatScale;
            m_FormatColor = res.FormatColor;
            m_FormatSH = res.FormatSH;
        }

        static T CreateOrReplaceAsset<T>(T asset, string path) where T : UnityEngine.Object
        {
            T result = AssetDatabase.LoadAssetAtPath<T>(path);
            if (result == null)
            {
                AssetDatabase.CreateAsset(asset, path);
                result = asset;
            }
            else
            {
                if (typeof(Mesh).IsAssignableFrom(typeof(T))) { (result as Mesh)?.Clear(); }
                EditorUtility.CopySerialized(asset, result);
            }
            return result;
        }

        static bool ClusterSHProgress(float val)
        {
            EditorUtility.DisplayProgressBar(kProgressTitle, $"Cluster SHs ({val:P0})", 0.2f + val * 0.5f);
            return true;
        }

        unsafe void CreateAsset()
        {
            m_ErrorMessage = null;
            if (string.IsNullOrWhiteSpace(m_InputFile))
            {
                m_ErrorMessage = $"Select input PLY/SPZ file";
                return;
            }

            if (string.IsNullOrWhiteSpace(m_OutputFolder) || !m_OutputFolder.StartsWith("Assets/"))
            {
                m_ErrorMessage = $"Output folder must be within project, was '{m_OutputFolder}'";
                return;
            }
            Directory.CreateDirectory(m_OutputFolder);

            EditorUtility.DisplayProgressBar(kProgressTitle, "Reading data files", 0.0f);
            GaussianSplatAsset.CameraInfo[] cameras = GaussianSplatAssetCreateTask.LoadJsonCamerasFile(m_InputFile, kCamerasJson, m_ImportCameras);
            NativeArray<InputSplatData> inputSplats;
            try
            {
                inputSplats = GaussianSplatAssetCreateTask.LoadInputSplatFile(m_InputFile);
            }
            catch (Exception ex)
            {
                m_ErrorMessage = $"Error occurred while reading file: {ex.Message}";
                EditorUtility.ClearProgressBar();
                return;
            }
            if (inputSplats.Length == 0)
            {
                EditorUtility.ClearProgressBar();
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

            EditorUtility.DisplayProgressBar(kProgressTitle, "Morton reordering", 0.05f);
            GaussianSplatAssetCreateTask.ReorderMorton(inputSplats, boundsMin, boundsMax);

            // cluster SHs
            NativeArray<int> splatSHIndices = default;
            NativeArray<GaussianSplatAsset.SHTableItemFloat16> clusteredSHs = default;
            if (m_FormatSH >= GaussianSplatAsset.SHFormat.Cluster64k)
            {
                EditorUtility.DisplayProgressBar(kProgressTitle, "Cluster SHs", 0.2f);
                GaussianSplatAssetCreateTask.ClusterSHs(inputSplats, m_FormatSH, out clusteredSHs, out splatSHIndices, ClusterSHProgress);
            }

            string baseName = Path.GetFileNameWithoutExtension(FilePickerControl.PathToDisplayString(m_InputFile));

            EditorUtility.DisplayProgressBar(kProgressTitle, "Creating data objects", 0.7f);
            GaussianSplatAsset asset = ScriptableObject.CreateInstance<GaussianSplatAsset>();
            asset.Initialize(inputSplats.Length, m_FormatPos, m_FormatScale, m_FormatColor, m_FormatSH, boundsMin, boundsMax, cameras);
            asset.name = baseName;

            var dataHash = new Hash128((uint)asset.splatCount, (uint)asset.formatVersion, 0, 0);
            string pathChunk = $"{m_OutputFolder}/{baseName}_chk.bytes";
            string pathPos = $"{m_OutputFolder}/{baseName}_pos.bytes";
            string pathOther = $"{m_OutputFolder}/{baseName}_oth.bytes";
            string pathCol = $"{m_OutputFolder}/{baseName}_col.bytes";
            string pathSh = $"{m_OutputFolder}/{baseName}_shs.bytes";

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
            asset.SetDataHash(dataHash);

            if(useChunks) WriteFile(pathChunk, chunkData);
            WriteFile(pathPos, posData);
            WriteFile(pathOther, otherData);
            WriteFile(pathCol, colorData);
            WriteFile(pathSh, shData);

            splatSHIndices.Dispose();
            clusteredSHs.Dispose();
            inputSplats.Dispose();

            // files are created, import them so we can get to the imported objects, ugh
            EditorUtility.DisplayProgressBar(kProgressTitle, "Initial texture import", 0.85f);
            AssetDatabase.Refresh(ImportAssetOptions.ForceUncompressedImport);

            EditorUtility.DisplayProgressBar(kProgressTitle, "Setup data onto asset", 0.95f);
            asset.SetAssetFiles(
                useChunks ? AssetDatabase.LoadAssetAtPath<TextAsset>(pathChunk) : null,
                AssetDatabase.LoadAssetAtPath<TextAsset>(pathPos),
                AssetDatabase.LoadAssetAtPath<TextAsset>(pathOther),
                AssetDatabase.LoadAssetAtPath<TextAsset>(pathCol),
                AssetDatabase.LoadAssetAtPath<TextAsset>(pathSh));

            var assetPath = $"{m_OutputFolder}/{baseName}.asset";
            var savedAsset = CreateOrReplaceAsset(asset, assetPath);

            EditorUtility.DisplayProgressBar(kProgressTitle, "Saving assets", 0.99f);
            AssetDatabase.SaveAssets();
            EditorUtility.ClearProgressBar();

            Selection.activeObject = savedAsset;
        }

        void WriteFile(string path, NativeArray<byte> data)
        {
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            fs.Write(data);
            data.Dispose();
        }
    }
}
