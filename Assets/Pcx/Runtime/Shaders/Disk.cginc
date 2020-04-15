// Pcx - Point cloud importer & renderer for Unity
// https://github.com/keijiro/Pcx

#include "UnityCG.cginc"
#include "Common.cginc"

// Uniforms
half4 _Tint;
half _PointSize;
float4x4 _Transform;

#if _COMPUTE_BUFFER
StructuredBuffer<float4> _PointBuffer;
#endif

// Vertex input attributes
struct Attributes
{
#if _COMPUTE_BUFFER
    uint vertexID : SV_VertexID;
#else
    float4 position : POSITION;
    half3 color : COLOR;
#endif
};

// Fragment varyings
struct Varyings
{
    float4 position : SV_POSITION;
    nointerpolation float4 positionCenter : TEXTCOORD0;
    float2 position1 : TEXTCOORD1;
#if !PCX_SHADOW_CASTER
    half3 color : COLOR;
    UNITY_FOG_COORDS(0)
#endif
};

// Vertex phase
Varyings Vertex(Attributes input)
{
    // Retrieve vertex attributes.
#if _COMPUTE_BUFFER
    float4 pt = _PointBuffer[input.vertexID];
    float4 pos = mul(_Transform, float4(pt.xyz, 1));
    half3 col = PcxDecodeColor(asuint(pt.w));
#else
    float4 pos = input.position;
    half3 col = input.color;
#endif

#if !PCX_SHADOW_CASTER
    // Color space convertion & applying tint
    #if UNITY_COLORSPACE_GAMMA
        col *= _Tint.rgb * 2;
    #else
        col *= LinearToGammaSpace(_Tint.rgb) * 2;
        col = GammaToLinearSpace(col);
    #endif
#endif

    // Set vertex output.
    Varyings o;    
    o.position = UnityObjectToClipPos(pos);
    o.positionCenter = o.position;
#if !PCX_SHADOW_CASTER
    o.color = col;
    UNITY_TRANSFER_FOG(o, o.position);
#endif
    return o;
}

// Geometry phase
/*[maxvertexcount(36)]
void Geometry(point Varyings input[1], inout TriangleStream<Varyings> outStream)
{
    float4 origin = input[0].position;
    float2 extent = abs(UNITY_MATRIX_P._11_22 * _PointSize);

    // Copy the basic information.
    Varyings o = input[0];

    // Determine the number of slices based on the radius of the
    // point on the screen.
    float radius = extent.y / origin.w * _ScreenParams.y;
    uint slices = min((radius + 1) / 5, 4) + 2;

    // Slightly enlarge quad points to compensate area reduction.
    // Hopefully this line would be complied without branch.
    if (slices == 2) extent *= 1.2;

    // Top vertex
    o.position.y = origin.y + extent.y;
    o.position.xzw = origin.xzw;
    outStream.Append(o);

    UNITY_LOOP for (uint i = 1; i < slices; i++)
    {
        float sn, cs;
        sincos(UNITY_PI / slices * i, sn, cs);

        // Right side vertex
        o.position.xy = origin.xy + extent * float2(sn, cs);
        outStream.Append(o);

        // Left side vertex
        o.position.x = origin.x - extent.x * sn;
        outStream.Append(o);
    }

    // Bottom vertex
    o.position.x = origin.x;
    o.position.y = origin.y - extent.y;
    outStream.Append(o);

    outStream.RestartStrip();
}*/

[maxvertexcount(4)]
void Geometry(point Varyings input[1], inout TriangleStream<Varyings> outStream)
{
    float4 origin = input[0].position;
    float2 extent = 0.5*abs(UNITY_MATRIX_P._11_22 * _PointSize * origin.w);

    // Copy the basic information.
    Varyings o = input[0];
    o.positionCenter.xy = origin.xy;
    o.positionCenter.zw = extent;

    // Determine the number of slices based on the radius of the
    // point on the screen.
    o.position.zw = origin.zw;
    
    o.position.x = origin.x - extent.x;
    o.position.y = origin.y + extent.y;
    o.position1 = o.position.xy;
    outStream.Append(o);

    o.position.x = origin.x + extent.x;
    o.position.y = origin.y + extent.y;
    o.position1 = o.position.xy;
    outStream.Append(o);

    o.position.x = origin.x - extent.x;
    o.position.y = origin.y - extent.y;
    o.position1 = o.position.xy;
    outStream.Append(o);
    
    o.position.x = origin.x + extent.x;
    o.position.y = origin.y - extent.y;
    o.position1 = o.position.xy;
    outStream.Append(o);

    outStream.RestartStrip();
}

half4 Fragment(Varyings input) : SV_Target
{
#if PCX_SHADOW_CASTER
    return 0;
#else
    half4 c = half4(input.color, _Tint.a);

    clip( (1.-clamp(0.5*distance(input.position1, input.positionCenter.xy) / input.positionCenter.w,0.,1.)) - 0.5);
    //c.a = 1.-smoothstep(0.5,0.6,distance(input.position1, input.positionCenter.xy)/input.positionCenter.w);

    //UNITY_APPLY_FOG(input.fogCoord, c);
    return c;
#endif
}

