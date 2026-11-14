using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace RoadsideHaven.Editor
{
    /// <summary>Subtracts road footprints from low, baked paving without modifying the source mesh asset.</summary>
    public static class AsterAccessMeshClipper
    {
        private struct Vertex
        {
            public Vector3 Position, Normal;
            public Vector2 UV;
            public Vector4 Tangent;
            public Color Color;
            public static Vertex Between(Vertex a, Vertex b, float t)
            {
                return new Vertex
                {
                    Position=Vector3.LerpUnclamped(a.Position,b.Position,t),
                    Normal=Vector3.LerpUnclamped(a.Normal,b.Normal,t),
                    UV=Vector2.LerpUnclamped(a.UV,b.UV,t),
                    Tangent=Vector4.LerpUnclamped(a.Tangent,b.Tangent,t),
                    Color=UnityEngine.Color.LerpUnclamped(a.Color,b.Color,t)
                };
            }
        }

        /// <param name="filter">Only the intended baked paving filter should be supplied.</param>
        /// <param name="cuts">Axis-aligned road footprints in world X/Z coordinates.</param>
        /// <param name="assetPath">A new mesh asset path in an existing Assets folder.</param>
        public static Mesh ClipLowGround(MeshFilter filter, Rect[] cuts, string assetPath)
        {
            if(filter==null||filter.sharedMesh==null)throw new ArgumentException("A paving mesh filter is required.",nameof(filter));
            if(cuts==null)throw new ArgumentNullException(nameof(cuts));
            if(string.IsNullOrEmpty(assetPath)||!assetPath.StartsWith("Assets/",StringComparison.Ordinal))throw new ArgumentException("The output must be a project asset path.",nameof(assetPath));
            var source=filter.sharedMesh;
            if(AssetDatabase.GetAssetPath(source)==assetPath)throw new ArgumentException("The source asset must remain unchanged.",nameof(assetPath));
            var positions=source.vertices;var normals=source.normals;var uv=source.uv;var tangents=source.tangents;var colors=source.colors;
            bool hasNormals=normals.Length==positions.Length,hasUV=uv.Length==positions.Length,hasTangents=tangents.Length==positions.Length,hasColors=colors.Length==positions.Length;
            var outputPositions=new List<Vector3>();var outputNormals=new List<Vector3>();var outputUV=new List<Vector2>();var outputTangents=new List<Vector4>();var outputColors=new List<Color>();
            var indices=new List<int>[source.subMeshCount];var world=filter.transform.localToWorldMatrix;
            for(int sub=0;sub<source.subMeshCount;sub++)
            {
                if(source.GetTopology(sub)!=MeshTopology.Triangles)throw new ArgumentException("Paving must use triangle topology.",nameof(filter));
                indices[sub]=new List<int>();var inputIndices=source.GetTriangles(sub);
                for(int i=0;i<inputIndices.Length;i+=3)
                {
                    var triangle=new List<Vertex>(3);bool low=true;
                    for(int n=0;n<3;n++)
                    {
                        int index=inputIndices[i+n];var v=new Vertex{Position=positions[index],Normal=hasNormals?normals[index]:Vector3.up,UV=hasUV?uv[index]:Vector2.zero,Tangent=hasTangents?tangents[index]:Vector4.zero,Color=hasColors?colors[index]:UnityEngine.Color.white};
                        if(world.MultiplyPoint3x4(v.Position).y>.25f)low=false;triangle.Add(v);
                    }
                    var polygons=new List<List<Vertex>>{triangle};
                    if(low)foreach(var cut in cuts)
                    {
                        if(cut.width<=0||cut.height<=0)continue;
                        var next=new List<List<Vertex>>();foreach(var polygon in polygons)Subtract(polygon,cut,world,next);polygons=next;if(polygons.Count==0)break;
                    }
                    foreach(var polygon in polygons)
                    {
                        int first=outputPositions.Count;
                        foreach(var v in polygon){outputPositions.Add(v.Position);if(hasNormals)outputNormals.Add(v.Normal.normalized);if(hasUV)outputUV.Add(v.UV);if(hasTangents)outputTangents.Add(v.Tangent);if(hasColors)outputColors.Add(v.Color);}
                        for(int n=1;n<polygon.Count-1;n++)
                        {
                            if(Vector3.Cross(polygon[n].Position-polygon[0].Position,polygon[n+1].Position-polygon[0].Position).sqrMagnitude<1e-12f)continue;
                            indices[sub].Add(first);indices[sub].Add(first+n);indices[sub].Add(first+n+1);
                        }
                    }
                }
            }
            var result=new Mesh{name=source.name+" - road clearance",indexFormat=outputPositions.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
            result.SetVertices(outputPositions);if(hasNormals)result.SetNormals(outputNormals);if(hasUV)result.SetUVs(0,outputUV);if(hasTangents)result.SetTangents(outputTangents);if(hasColors)result.SetColors(outputColors);
            result.subMeshCount=indices.Length;for(int sub=0;sub<indices.Length;sub++)result.SetTriangles(indices[sub],sub,false);
            if(!hasNormals)result.RecalculateNormals();result.RecalculateBounds();
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if(saved==null){AssetDatabase.CreateAsset(result,assetPath);saved=result;}
            else{saved.Clear();EditorUtility.CopySerialized(result,saved);UnityEngine.Object.DestroyImmediate(result);EditorUtility.SetDirty(saved);}
            saved.UploadMeshData(false);filter.sharedMesh=saved;EditorUtility.SetDirty(filter);return saved;
        }

        // Keep each outside fragment, then continue clipping the still-inside polygon.
        // The four resulting strips partition the triangle difference without overlapping area.
        private static void Subtract(List<Vertex> polygon,Rect rect,Matrix4x4 world,List<List<Vertex>> output)
        {
            var inside=polygon;
            for(int plane=0;plane<4&&inside.Count>=3;plane++)
            {
                var outside=Clip(inside,rect,plane,world,false);if(outside.Count>=3)output.Add(outside);
                inside=Clip(inside,rect,plane,world,true);
            }
        }
        private static List<Vertex> Clip(List<Vertex> input,Rect rect,int plane,Matrix4x4 world,bool keepInside)
        {
            var output=new List<Vertex>();if(input.Count==0)return output;float sign=keepInside?1:-1;
            var previous=input[input.Count-1];float previousDistance=Distance(world.MultiplyPoint3x4(previous.Position),rect,plane)*sign;
            foreach(var current in input)
            {
                float currentDistance=Distance(world.MultiplyPoint3x4(current.Position),rect,plane)*sign;bool a=previousDistance>=0,b=currentDistance>=0;
                if(a!=b){float fraction=previousDistance/(previousDistance-currentDistance);output.Add(Vertex.Between(previous,current,Mathf.Clamp01(fraction)));}
                if(b)output.Add(current);previous=current;previousDistance=currentDistance;
            }
            return output;
        }
        private static float Distance(Vector3 p,Rect rect,int plane)
        {
            switch(plane){case 0:return p.x-rect.xMin;case 1:return rect.xMax-p.x;case 2:return p.z-rect.yMin;default:return rect.yMax-p.z;}
        }
    }
}
