Shader "Kaleidos/Forest_Test Sky URP" {
    Properties {
        _Zoom ("Zoom", Float) = 1
        _Pan ("Pan", Vector) = (0,0,0,0)
        _Rotation ("Rotation", Float) = 0
        _Tint ("Tint", Color) = (1,1,1,1)
        _Speed ("Speed", Float) = 1
    }
    SubShader {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Pass {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float _Zoom;
            float4 _Pan;
            float _Rotation;
            float4 _Tint;
            float _Speed;

            #define uTime (_Time.y * _Speed)
            #define uResolution float2(640.0, 360.0)

#define time uTime
static const float noiseScale = 6.0;
#define screenSize uResolution
#define sampleOffset (float2)(0.0)
#define renderSize uResolution
static const float cellSize = 1.0;
static const float octaves = 6.0;
static const float gain = 0.5;

#define morphPhase (uTime*0.12)
#define scrollSpeed float2(0.05, 0.08)
static const float2 loopD = float2(0.0, 0.0);
static const float loopZ = 0.0;
static const float pinchStrength = -0.7;
#define warpPhase (uTime*0.05)
static const float rotate = 22.5;

static const float4 PALETTE[110] = {
  float4(0.296471,0.105957,0.484375,1.0),
  float4(0.296471,0.105957,0.484375,1.0),
  float4(0.296471,0.105957,0.484375,1.0),
  float4(0.296471,0.105957,0.484375,1.0),
  float4(0.296471,0.105957,0.484375,1.0),
  float4(0.296471,0.105957,0.484375,1.0),
  float4(0.296471,0.105957,0.484375,1.0),
  float4(0.296471,0.105957,0.484375,1.0),
  float4(0.296471,0.105957,0.484375,1.0),
  float4(0.296471,0.105957,0.484375,1.0),
  float4(0.296471,0.105957,0.484375,1.0),
  float4(0.296471,0.105957,0.484375,1.0),
  float4(0.296471,0.105957,0.484375,1.0),
  float4(0.296471,0.105957,0.484375,1.0),
  float4(0.296471,0.105957,0.484375,1.0),
  float4(0.296471,0.105957,0.484375,1.0),
  float4(0.296471,0.105957,0.484375,1.0),
  float4(0.296471,0.105957,0.484375,1.0),
  float4(0.296471,0.105957,0.484375,1.0),
  float4(0.296471,0.105957,0.484375,1.0),
  float4(0.296471,0.105957,0.484375,1.0),
  float4(0.296471,0.105957,0.484375,1.0),
  float4(0.296471,0.105957,0.484375,1.0),
  float4(0.296471,0.105957,0.484375,1.0),
  float4(0.296471,0.105957,0.484375,1.0),
  float4(0.296471,0.105957,0.484375,1.0),
  float4(0.321996,0.0939941,0.546875,1.0),
  float4(0.167755,0.140381,0.359375,1.0),
  float4(0.167755,0.140381,0.359375,1.0),
  float4(0.167755,0.140381,0.359375,1.0),
  float4(0.167755,0.140381,0.359375,1.0),
  float4(0.167755,0.140381,0.359375,1.0),
  float4(0.167755,0.140381,0.359375,1.0),
  float4(0.167755,0.140381,0.359375,1.0),
  float4(0.167755,0.140381,0.359375,1.0),
  float4(0.167755,0.140381,0.359375,1.0),
  float4(0.167755,0.140381,0.359375,1.0),
  float4(0.167755,0.140381,0.359375,1.0),
  float4(0.167755,0.140381,0.359375,1.0),
  float4(0.167755,0.140381,0.359375,1.0),
  float4(0.167755,0.140381,0.359375,1.0),
  float4(0.167755,0.140381,0.359375,1.0),
  float4(0.167755,0.140381,0.359375,1.0),
  float4(0.167755,0.140381,0.359375,1.0),
  float4(0.321996,0.0939941,0.546875,1.0),
  float4(0.321996,0.0939941,0.546875,1.0),
  float4(0.321996,0.0939941,0.546875,1.0),
  float4(0.482033,0.250244,0.640625,1.0),
  float4(0.482033,0.250244,0.640625,1.0),
  float4(0.482033,0.250244,0.640625,1.0),
  float4(0.482033,0.250244,0.640625,1.0),
  float4(0.482033,0.250244,0.640625,1.0),
  float4(0.482033,0.250244,0.640625,1.0),
  float4(0.321996,0.0939941,0.546875,1.0),
  float4(0.321996,0.0939941,0.546875,1.0),
  float4(0.321996,0.0939941,0.546875,1.0),
  float4(0.321996,0.0939941,0.546875,1.0),
  float4(0.613006,0.582275,0.828125,1.0),
  float4(0.613006,0.582275,0.828125,1.0),
  float4(0.613006,0.582275,0.828125,1.0),
  float4(0.613006,0.582275,0.828125,1.0),
  float4(0.613006,0.582275,0.828125,1.0),
  float4(0.613006,0.582275,0.828125,1.0),
  float4(0.613006,0.582275,0.828125,1.0),
  float4(0.863358,0.777832,0.921875,1.0),
  float4(0.863358,0.777832,0.921875,1.0),
  float4(0.863358,0.777832,0.921875,1.0),
  float4(0.240625,0.0410156,0.4375,1.0),
  float4(0.240625,0.0410156,0.4375,1.0),
  float4(0.240625,0.0410156,0.4375,1.0),
  float4(0.240625,0.0410156,0.4375,1.0),
  float4(0.240625,0.0410156,0.4375,1.0),
  float4(0.240625,0.0410156,0.4375,1.0),
  float4(0.240625,0.0410156,0.4375,1.0),
  float4(0.240625,0.0410156,0.4375,1.0),
  float4(0.240625,0.0410156,0.4375,1.0),
  float4(0.240625,0.0410156,0.4375,1.0),
  float4(0.240625,0.0410156,0.4375,1.0),
  float4(0.240625,0.0410156,0.4375,1.0),
  float4(0.240625,0.0410156,0.4375,1.0),
  float4(0.240625,0.0410156,0.4375,1.0),
  float4(0.240625,0.0410156,0.4375,1.0),
  float4(0.240625,0.0410156,0.4375,1.0),
  float4(0.240625,0.0410156,0.4375,1.0),
  float4(0.240625,0.0410156,0.4375,1.0),
  float4(0.240625,0.0410156,0.4375,1.0),
  float4(0.240625,0.0410156,0.4375,1.0),
  float4(0.240625,0.0410156,0.4375,1.0),
  float4(0.240625,0.0410156,0.4375,1.0),
  float4(0.240625,0.0410156,0.4375,1.0),
  float4(0.240625,0.0410156,0.4375,1.0),
  float4(0.240625,0.0410156,0.4375,1.0),
  float4(0.863358,0.777832,0.921875,1.0),
  float4(0.863358,0.777832,0.921875,1.0),
  float4(0.240625,0.0410156,0.4375,1.0),
  float4(0.240625,0.0410156,0.4375,1.0),
  float4(0.240625,0.0410156,0.4375,1.0),
  float4(0.240625,0.0410156,0.4375,1.0),
  float4(0.240625,0.0410156,0.4375,1.0),
  float4(0.240625,0.0410156,0.4375,1.0),
  float4(0.240625,0.0410156,0.4375,1.0),
  float4(0.240625,0.0410156,0.4375,1.0),
  float4(0.240625,0.0410156,0.4375,1.0),
  float4(0.240625,0.0410156,0.4375,1.0),
  float4(0.240625,0.0410156,0.4375,1.0),
  float4(0.240625,0.0410156,0.4375,1.0),
  float4(0.240625,0.0410156,0.4375,1.0),
  float4(0.240625,0.0410156,0.4375,1.0),
  float4(0.240625,0.0410156,0.4375,1.0),
  float4(0.863358,0.777832,0.921875,1.0)
};
float4 paletteLookup(float x){
  int i = clamp(int(clamp(x,0.0,1.0)*256.0),0,255);
  return PALETTE[clamp(int(float(i)/255.0*110.0),0,109)];
}

#define TAU 6.28318530718
#define MAX_OCTAVES 10
#define MORPH_DESYNC_AMP   1.6
#define MORPH_DESYNC_SCALE 2.5

float2 frameC(float2 uv) {
    float2 r = renderSize;
    return (uv - 0.5) * screenSize / r.y;
}
float2 frameUV(float2 c) {
    float2 r = renderSize;
    return 0.5 + c * r.y / screenSize;
}

float2 toPolar(float2 uv) {
    float2 c = frameC(uv);
    return float2(atan2(c.y, c.x) / TAU, length(c));
}

float hash3(float3 p) {
    p = frac(p * float3(127.1, 311.7, 74.7));
    p += dot(p, p.yxz + 19.19);
    return frac((p.x + p.y) * p.z);
}

float vnoise3(float3 p) {
    float3 i = floor(p);
    float3 f = frac(p);
    float3 u = f * f * (3.0 - 2.0 * f);
    return lerp(
        lerp(lerp(hash3(i + float3(0,0,0)), hash3(i + float3(1,0,0)), u.x),
            lerp(hash3(i + float3(0,1,0)), hash3(i + float3(1,1,0)), u.x), u.y),
        lerp(lerp(hash3(i + float3(0,0,1)), hash3(i + float3(1,0,1)), u.x),
            lerp(hash3(i + float3(0,1,1)), hash3(i + float3(1,1,1)), u.x), u.y),
        u.z
    );
}

float basisSample(float3 p, float2 d, float pz) {

    return vnoise3(p) * 2.0 - 1.0;
}

float fbm3(float3 p) {
    float v = 0.0, a = 0.5, norm = 0.0;
    float2  d  = loopD;
    float pz = loopZ;
    int oct = int(octaves + 0.5);
    for (int i = 0; i < MAX_OCTAVES; i++) {
        if (i >= oct) break;
        float sn = basisSample(p, d, pz);
        float t;
        {
            t = sn * 0.5 + 0.5;
        }
        v    += a * t;
        norm += a;
        p     = p * 2.0 + float3(5.3, 1.7, 3.1);
        d    *= 2.0;
        pz   *= 2.0;
        a    *= gain;
    }
    return v / norm;
}

float fbmPolar(float2 pc, float mz) {
    float x01 = pc.x + 0.5;
    float yz  = pc.y * noiseScale + time * scrollSpeed.y;
    float dx  = time * scrollSpeed.x;
    float3  pa  = float3(x01 * noiseScale + dx, yz, mz);
    float3  pb  = float3((x01 - 1.0) * noiseScale + dx, yz, mz);
    return lerp(fbm3(pa), fbm3(pb), x01);
}

#define PINCH_R     0.8
#define PINCH_K     0.9
#define PINCH_PULSE 0.5

float whash2(float2 p) {
    p = frac(p * float2(127.1, 311.7));
    p += dot(p, p.yx + 19.19);
    return frac((p.x + p.y) * 43.32);
}
float wnoise2(float2 p) {
    float2 i = floor(p), f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(whash2(i + float2(0.0, 0.0)), whash2(i + float2(1.0, 0.0)), u.x),
               lerp(whash2(i + float2(0.0, 1.0)), whash2(i + float2(1.0, 1.0)), u.x), u.y);
}





float2 warpUV(float2 uv) {

    float2  c   = frameC(uv);
    float r   = length(c);
    if (r > 1e-5) {
        float k  = pinchStrength * (1.0 + PINCH_PULSE * sin(warpPhase * TAU));
        float rn = min(r / PINCH_R, 1.0);
        float factor;
        if (k >= 0.0) {
            factor = pow(rn, k * PINCH_K);
        } else {
            factor = 1.0 + (-k) * PINCH_K * (1.0 - rn * rn); 
        }
        c *= factor;
    }
    return frameUV(c);
}

float2 rotateUV(float2 uv) {
    
    float ang = rotate * TAU / 360.0;
    float asp = screenSize.x / screenSize.y;
    float2  c   = (uv - 0.5) * float2(asp, 1.0);
    float s = sin(ang), co = cos(ang);
    c = mul(c, float2x2(co, -s, s, co));
    return 0.5 + c / float2(asp, 1.0);
}

float fieldN(float2 uv, float mzExtra) {
    float mz = morphPhase + mzExtra;

    mz += MORPH_DESYNC_AMP * (wnoise2(uv * MORPH_DESYNC_SCALE) - 0.5);
    return fbmPolar(toPolar(uv), mz);
}

float4 shade(float2 screen_coords) {
    float2 block = floor((screen_coords + sampleOffset) / cellSize) * cellSize;
    float2 uv0   = warpUV(block / screenSize);
    float mzAdd = 0.0, valAdd = 0.0;
    float2 uvDisp = (float2)(0.0);

    float n = fieldN(rotateUV(uv0 + uvDisp), mzAdd);
    n = clamp(n + valAdd, 0.0, 1.0);
    return paletteLookup(n);
}


            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionHCS : SV_POSITION; float3 dir : TEXCOORD0; };
            Varyings vert(Attributes IN){
                Varyings o;
                o.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                o.dir = IN.positionOS.xyz;
                return o;
            }

            float4 frag(Varyings i) : SV_Target {
                float3 d = normalize(i.dir);
                float2 uv = float2(d.x, -d.z) / max(1.0 + d.y, 1e-3) * 0.5;
                float s = sin(_Rotation), cs = cos(_Rotation);
                uv = mul(uv, float2x2(cs, -s, s, cs));
                uv = uv / _Zoom + _Pan.xy;
                float4 c = shade((uv + 0.5) * float2(640.0, 360.0));
                return float4(c.rgb * _Tint.rgb, 1.0);
            }
            ENDHLSL
        }
    }
}