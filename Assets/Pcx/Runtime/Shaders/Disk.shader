// Pcx - Point cloud importer & renderer for Unity
// https://github.com/keijiro/Pcx

Shader "Point Cloud/Disk"
{
    Properties
    {
        _Tint("Tint", Color) = (0.5, 0.5, 0.5, 0)
        _PointSize("Point Size", Float) = 0.05
        [Toggle] _Distance("Apply Distance", Float) = 1
    }
    SubShader
    {
        //Tags { "RenderType"="Opaque" }
        Tags { "Queue" = "AlphaTest" "RenderType" = "TransparentCutout" }
       // Cull Off
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            //Blend SrcAlpha OneMinusSrcAlpha
            
            CGPROGRAM
            #pragma vertex Vertex
            #pragma geometry Geometry
            #pragma fragment Fragment
            #pragma multi_compile_fog
            #pragma multi_compile _ UNITY_COLORSPACE_GAMMA
            #pragma multi_compile _ _DISTANCE_ON
            #pragma multi_compile _ _COMPUTE_BUFFER
            #include "Disk.cginc"
            ENDCG
        }/*
        Pass
        {
            Tags { "LightMode"="ShadowCaster" }
            CGPROGRAM
            #pragma vertex Vertex
            #pragma geometry Geometry
            #pragma fragment Fragment
            #pragma multi_compile _ _COMPUTE_BUFFER
            #pragma multi_compile _ _DISTANCE_ON
            #define PCX_SHADOW_CASTER 1
            #include "Disk.cginc"
            ENDCG
        }*/
    }
    CustomEditor "Pcx.DiskMaterialInspector"
}
