// Pcx - Point cloud importer & renderer for Unity
// https://github.com/keijiro/Pcx

using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.Experimental.AssetImporters;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Pcx
{
    [ScriptedImporter(1, "pckd")]
    class SimthetiqPointCloudImporter : ScriptedImporter
    {
        #region ScriptedImporter implementation

        public enum ContainerType { Mesh, ComputeBuffer, Texture }

        [SerializeField] ContainerType _containerType = ContainerType.Mesh;

        public override void OnImportAsset(AssetImportContext context)
        {
            if (_containerType == ContainerType.Mesh)
            {
                // Mesh container
                // Create a prefab with MeshFilter/MeshRenderer.
                var mesh = ImportAsMesh(context.assetPath);

                var maingameObject = new GameObject();

                var meshFilter = maingameObject.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = mesh;

                var meshRenderer = maingameObject.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = GetDefaultMaterial();

                context.AddObjectToAsset("mesh", meshFilter.sharedMesh);
                context.AddObjectToAsset("prefab", maingameObject);
                context.SetMainObject(maingameObject);


            }/*
            else if (_containerType == ContainerType.ComputeBuffer)
            {
                // ComputeBuffer container
                // Create a prefab with PointCloudRenderer.
                var gameObject = new GameObject();
                var data = ImportAsPointCloudData(context.assetPath);

                var renderer = gameObject.AddComponent<PointCloudRenderer>();
                renderer.sourceData = data;

                context.AddObjectToAsset("prefab", gameObject);
                if (data != null) context.AddObjectToAsset("data", data);

                context.SetMainObject(gameObject);
            }
            else // _containerType == ContainerType.Texture
            {
                // Texture container
                // No prefab is available for this type.
                var data = ImportAsBakedPointCloud(context.assetPath);
                if (data != null)
                {
                    context.AddObjectToAsset("container", data);
                    context.AddObjectToAsset("position", data.positionMap);
                    context.AddObjectToAsset("color", data.colorMap);
                    context.SetMainObject(data);
                }
            }*/
        }

        #endregion

        #region Internal utilities

        static Material GetDefaultMaterial()
        {
            // Via package manager
            var path_upm = "Packages/jp.keijiro.pcx/Editor/Default Point.mat";
            // Via project asset database
            var path_prj = "Assets/Pcx/Editor/Default Point.mat";
            return AssetDatabase.LoadAssetAtPath<Material>(path_upm) ??
                   AssetDatabase.LoadAssetAtPath<Material>(path_prj);
        }

        #endregion

        #region Internal data structure
        class PCModelHeader_Offsets
        {
            public ulong offsetTree=0;
            public ulong offsetPointCloud=0;
        };

        class PCModelHeader
        {
            public ushort version=0;
            public uint nbSubPointCloud=0;
            public PCModelHeader_Offsets[] map = null;
        };

        class DataBody
        {
            public List<Vector3> vertices;
            public List<Color32> colors;

            public DataBody(int vertexCount)
            {
                vertices = new List<Vector3>(vertexCount);
                colors = new List<Color32>(vertexCount);
            }

            public void AddPoint(
                float x, float y, float z,
                byte r, byte g, byte b, byte a
            )
            {
                vertices.Add(new Vector3(x, y, z));
                colors.Add(new Color32(r, g, b, a));
            }
        }

        class PointCloudSub
        {
            public ulong m_numberOfPoints=0;
            public DataBody m_pointCloud = null;

          /*  public ulong m_numberOfNodes=0;
            public List<PointCloudKDNode> m_kdTree = new List<PointCloudKDNode>();       */     
        };

        #endregion

        #region Reader implementation

        PCModelHeader ReadDataHeader(BinaryReader reader)
        {
            PCModelHeader header = new PCModelHeader();

            header.version = reader.ReadUInt16();
            header.nbSubPointCloud = reader.ReadUInt32();

            header.map = new PCModelHeader_Offsets[header.nbSubPointCloud];

            for (uint i = 0; i < header.nbSubPointCloud; ++i)
            {
                PCModelHeader_Offsets offsets = new PCModelHeader_Offsets();
                offsets.offsetTree = reader.ReadUInt64();
                offsets.offsetPointCloud = reader.ReadUInt64();

                header.map[i] = offsets;
            }

            return header;
        }

        PointCloudSub[] ReadDataBody(PCModelHeader header, BinaryReader reader)
        {
            PointCloudSub[] pointDatasets = new PointCloudSub[header.nbSubPointCloud];            

            for (uint i = 0; i < header.nbSubPointCloud; ++i)
            {
                //ReadKDTree(file, header.map[i].offsetTree, i);
                PointCloudSub dataset = ReadPointCloud(reader, header.map[i].offsetPointCloud);

                if(dataset.m_numberOfPoints > 0)
                    pointDatasets[i] = dataset;
                else
                    pointDatasets[i] = null;
            }

            return pointDatasets;
        }

        PointCloudSub ReadPointCloud(BinaryReader reader, ulong offset)
        {
            PointCloudSub dataset = new PointCloudSub();

            reader.BaseStream.Seek((long)offset, SeekOrigin.Begin);

            dataset.m_numberOfPoints = reader.ReadUInt64();

            dataset.m_pointCloud = new DataBody((int)dataset.m_numberOfPoints);
            
            for (ulong i = 0; i < dataset.m_numberOfPoints; ++i)
            {
                // position
                float x = reader.ReadSingle();
                float y = reader.ReadSingle();
                float z = reader.ReadSingle();

                // normal
                float nx = reader.ReadSingle();
                float ny = reader.ReadSingle();
                float nz = reader.ReadSingle();

                // color
                byte r = reader.ReadByte();
                byte g = reader.ReadByte();
                byte b = reader.ReadByte();

                dataset.m_pointCloud.AddPoint(x, y, z,
                    r, g, b, 255);
            }

            return dataset;
        }

        Mesh ImportAsMesh(string path)
        {
            try
            {
                var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                var header = ReadDataHeader(new BinaryReader(stream));
                var body = ReadDataBody(header, new BinaryReader(stream));

                Mesh mesh = new Mesh();
                mesh.name = Path.GetFileNameWithoutExtension(path);
                mesh.indexFormat = IndexFormat.UInt32;

                mesh.subMeshCount = body.Count();

                List<Vector3> points = new List<Vector3>();
                List<Color32> colors = new List<Color32>();

                int[] startOffsets = new int[body.Count()];
                int[] countToCreate = new int[body.Count()];
                for (uint i = 0; i < body.Count(); ++i)
                {
                    startOffsets[i] = points.Count;
                    countToCreate[i] = body[i].m_pointCloud.vertices.Count;

                    points.AddRange(body[i].m_pointCloud.vertices);
                    body[i].m_pointCloud.vertices.Clear();

                    colors.AddRange(body[i].m_pointCloud.colors);
                    body[i].m_pointCloud.colors.Clear();
                }
                mesh.SetVertices(points);
                mesh.SetColors(colors);

                for (uint i = 0; i < body.Count(); ++i)
                {
                    mesh.SetIndices(
                        Enumerable.Range(startOffsets[i], countToCreate[i]).ToArray(),
                        MeshTopology.Points, (int)i);
                }

                    mesh.UploadMeshData(true);
                return mesh;
            }
            catch (Exception e)
            {
                Debug.LogError("Failed importing " + path + ". " + e.Message);
                return null;
            }
        }
        /*
        PointCloudData ImportAsPointCloudData(string path)
        {
            try
            {
                var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                var header = ReadDataHeader(new StreamReader(stream));
                var body = ReadDataBody(header, new BinaryReader(stream));
                var data = ScriptableObject.CreateInstance<PointCloudData>();
                data.Initialize(body.vertices, body.colors);
                data.name = Path.GetFileNameWithoutExtension(path);
                return data;
            }
            catch (Exception e)
            {
                Debug.LogError("Failed importing " + path + ". " + e.Message);
                return null;
            }
        }
        /*
        BakedPointCloud ImportAsBakedPointCloud(string path)
        {
            try
            {
                var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                var header = ReadDataHeader(new StreamReader(stream));
                var body = ReadDataBody(header, new BinaryReader(stream));
                var data = ScriptableObject.CreateInstance<BakedPointCloud>();
                data.Initialize(body.vertices, body.colors);
                data.name = Path.GetFileNameWithoutExtension(path);
                return data;
            }
            catch (Exception e)
            {
                Debug.LogError("Failed importing " + path + ". " + e.Message);
                return null;
            }
        }
        
        DataHeader ReadDataHeader(StreamReader reader)
        {
            var data = new DataHeader();
            var readCount = 0;

            // Magic number line ("ply")
            var line = reader.ReadLine();
            readCount += line.Length + 1;
            if (line != "ply")
                throw new ArgumentException("Magic number ('ply') mismatch.");

            // Data format: check if it's binary/little endian.
            line = reader.ReadLine();
            readCount += line.Length + 1;
            if (line != "format binary_little_endian 1.0")
                throw new ArgumentException(
                    "Invalid data format ('" + line + "'). " +
                    "Should be binary/little endian.");

            // Read header contents.
            for (var skip = false;;)
            {
                // Read a line and split it with white space.
                line = reader.ReadLine();
                readCount += line.Length + 1;
                if (line == "end_header") break;
                var col = line.Split();

                // Element declaration (unskippable)
                if (col[0] == "element")
                {
                    if (col[1] == "vertex")
                    {
                        data.vertexCount = Convert.ToInt32(col[2]);
                        skip = false;
                    }
                    else
                    {
                        // Don't read elements other than vertices.
                        skip = true;
                    }
                }

                if (skip) continue;

                // Property declaration line
                if (col[0] == "property")
                {
                    var prop = DataProperty.Invalid;

                    // Parse the property name entry.
                    switch (col[2])
                    {
                        case "red"  : prop = DataProperty.R8; break;
                        case "green": prop = DataProperty.G8; break;
                        case "blue" : prop = DataProperty.B8; break;
                        case "alpha": prop = DataProperty.A8; break;
                        case "x"    : prop = DataProperty.SingleX; break;
                        case "y"    : prop = DataProperty.SingleY; break;
                        case "z"    : prop = DataProperty.SingleZ; break;
                    }

                    // Check the property type.
                    if (col[1] == "char" || col[1] == "uchar" ||
                        col[1] == "int8" || col[1] == "uint8")
                    {
                        if (prop == DataProperty.Invalid)
                            prop = DataProperty.Data8;
                        else if (GetPropertySize(prop) != 1)
                            throw new ArgumentException("Invalid property type ('" + line + "').");
                    }
                    else if (col[1] == "short" || col[1] == "ushort" ||
                             col[1] == "int16" || col[1] == "uint16")
                    {
                        switch (prop)
                        {
                            case DataProperty.Invalid: prop = DataProperty.Data16; break;
                            case DataProperty.R8: prop = DataProperty.R16; break;
                            case DataProperty.G8: prop = DataProperty.G16; break;
                            case DataProperty.B8: prop = DataProperty.B16; break;
                            case DataProperty.A8: prop = DataProperty.A16; break;
                        }
                        if (GetPropertySize(prop) != 2)
                            throw new ArgumentException("Invalid property type ('" + line + "').");
                    }
                    else if (col[1] == "int"   || col[1] == "uint"   || col[1] == "float" ||
                             col[1] == "int32" || col[1] == "uint32" || col[1] == "float32")
                    {
                        if (prop == DataProperty.Invalid)
                            prop = DataProperty.Data32;
                        else if (GetPropertySize(prop) != 4)
                            throw new ArgumentException("Invalid property type ('" + line + "').");
                    }
                    else if (col[1] == "int64"  || col[1] == "uint64" ||
                             col[1] == "double" || col[1] == "float64")
                    {
                        switch (prop)
                        {
                            case DataProperty.Invalid: prop = DataProperty.Data64; break;
                            case DataProperty.SingleX: prop = DataProperty.DoubleX; break;
                            case DataProperty.SingleY: prop = DataProperty.DoubleY; break;
                            case DataProperty.SingleZ: prop = DataProperty.DoubleZ; break;
                        }
                        if (GetPropertySize(prop) != 8)
                            throw new ArgumentException("Invalid property type ('" + line + "').");
                    }
                    else
                    {
                        throw new ArgumentException("Unsupported property type ('" + line + "').");
                    }

                    data.properties.Add(prop);
                }
            }

            // Rewind the stream back to the exact position of the reader.
            reader.BaseStream.Position = readCount;

            return data;
        }

        DataBody ReadDataBody(DataHeader header, BinaryReader reader)
        {
            var data = new DataBody(header.vertexCount);

            float x = 0, y = 0, z = 0;
            Byte r = 255, g = 255, b = 255, a = 255;

            for (var i = 0; i < header.vertexCount; i++)
            {
                foreach (var prop in header.properties)
                {
                    switch (prop)
                    {
                        case DataProperty.R8: r = reader.ReadByte(); break;
                        case DataProperty.G8: g = reader.ReadByte(); break;
                        case DataProperty.B8: b = reader.ReadByte(); break;
                        case DataProperty.A8: a = reader.ReadByte(); break;

                        case DataProperty.R16: r = (byte)(reader.ReadUInt16() >> 8); break;
                        case DataProperty.G16: g = (byte)(reader.ReadUInt16() >> 8); break;
                        case DataProperty.B16: b = (byte)(reader.ReadUInt16() >> 8); break;
                        case DataProperty.A16: a = (byte)(reader.ReadUInt16() >> 8); break;

                        case DataProperty.SingleX: x = reader.ReadSingle(); break;
                        case DataProperty.SingleY: y = reader.ReadSingle(); break;
                        case DataProperty.SingleZ: z = reader.ReadSingle(); break;

                        case DataProperty.DoubleX: x = (float)reader.ReadDouble(); break;
                        case DataProperty.DoubleY: y = (float)reader.ReadDouble(); break;
                        case DataProperty.DoubleZ: z = (float)reader.ReadDouble(); break;

                        case DataProperty.Data8: reader.ReadByte(); break;
                        case DataProperty.Data16: reader.BaseStream.Position += 2; break;
                        case DataProperty.Data32: reader.BaseStream.Position += 4; break;
                        case DataProperty.Data64: reader.BaseStream.Position += 8; break;
                    }
                }

                data.AddPoint(x, y, z, r, g, b, a);
            }

            return data;
        }*/
    }

    #endregion
}
