// Klassische Tetris-Schattierung für die abgeschrägten Blöcke (Mesh: Blöcke/BevelCube.asset).
// Jede Fläche wird nach ihrer Richtung in der Welt eingefärbt: zum Licht hin heller (Richtung Weiß),
// vom Licht weg dunkler (in der Blockfarbe). Die Schrägen (UV.x > 0.5 im Mesh) werden stärker
// schattiert als die Flächen. Weil mit der Welt-Normalen gerechnet wird, bleibt die helle Kante
// auch nach dem Drehen eines Steins oben.
//
// Zwei Durchgänge: erst die Rückseiten, dann die Vorderseiten. Bei undurchsichtigen Blöcken
// verdecken die Vorderseiten die Rückseiten; beim Glaskasten sieht man so auch die hinteren Kanten.
Shader "HumanTetris/BevelBlock"
{
    Properties
    {
        _Color ("Farbe", Color) = (1, 1, 1, 1)
        _LightDir ("Lichtrichtung (x = rechts, y = oben)", Vector) = (-0.4, 0.6, 0, 0)
        _FaceShade ("Schattierung Flächen", Range(0, 2)) = 1
        _BevelShade ("Schattierung Schrägen", Range(0, 2)) = 1.6
        _FaceAlpha ("Deckkraft Flächen", Range(0, 1)) = 1
        _BevelAlpha ("Deckkraft Schrägen", Range(0, 1)) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
        [Enum(Off, 0, On, 1)] _ZWrite ("ZWrite", Float) = 1
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    fixed4 _Color;
    float4 _LightDir;
    half _FaceShade;
    half _BevelShade;
    half _FaceAlpha;
    half _BevelAlpha;

    struct appdata
    {
        float4 vertex : POSITION;
        float3 normal : NORMAL;
        float2 uv : TEXCOORD0;
    };

    struct v2f
    {
        float4 pos : SV_POSITION;
        float3 worldNormal : TEXCOORD0;
        float bevel : TEXCOORD1;
        UNITY_FOG_COORDS(2)
    };

    v2f vert (appdata v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.worldNormal = UnityObjectToWorldNormal(v.normal);
        o.bevel = step(0.5, v.uv.x);
        UNITY_TRANSFER_FOG(o, o.pos);
        return o;
    }

    fixed4 frag (v2f i) : SV_Target
    {
        float3 n = normalize(i.worldNormal);
        float2 l = normalize(_LightDir.xy);
        float s = dot(n.xy, l) * lerp(_FaceShade, _BevelShade, i.bevel);

        fixed3 c = s > 0
            ? lerp(_Color.rgb, fixed3(1, 1, 1), saturate(s * 0.6))
            : _Color.rgb * saturate(1 + s * 0.8);

        fixed4 col = fixed4(c, _Color.a * lerp(_FaceAlpha, _BevelAlpha, i.bevel));
        UNITY_APPLY_FOG(i.fogCoord, col);
        return col;
    }
    ENDCG

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }

        // 1) Rückseiten (z.B. hintere Kanten des Glaskastens)
        Pass
        {
            Cull Front
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            ENDCG
        }

        // 2) Vorderseiten
        Pass
        {
            Cull Back
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            ENDCG
        }
    }
}
