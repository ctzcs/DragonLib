cbuffer VertexMatrixBlock : register(b0, space1) { float4x4 Matrix; };
cbuffer CameraBlock : register(b1, space1) { float4x4 CameraMatrix; };
cbuffer GutBlock : register(b0, space3)
{
    float4 Light;      // xy world position, z height, w intensity
    float4 LightColor;
    float4 Surface;    // x normal strength, y wetness, z ambient, w display mode
    float4 Options;    // x light radius, y warm fill enabled, z light marker enabled, w height decode range
};
Texture2D<float4> Albedo : register(t0, space2);
SamplerState AlbedoSampler : register(s0, space2);
Texture2D<float4> Normals : register(t1, space2);
SamplerState NormalSampler : register(s1, space2);
Texture2D<float4> MaterialMap : register(t2, space2);
SamplerState MaterialSampler : register(s2, space2);

struct VsInput
{
    float2 Position : TEXCOORD0;
    float2 TexCoord : TEXCOORD1;
    float4 Color : TEXCOORD2;
    float4 Type : TEXCOORD4;
};
struct VsOutput
{
    float2 TexCoord : TEXCOORD0;
    float4 Position : SV_Position;
};
VsOutput vertex_main(VsInput input)
{
    VsOutput output;
    output.TexCoord = input.TexCoord;
    output.Position = mul(Matrix, mul(CameraMatrix, float4(input.Position, 0, 1)));
    return output;
}

float3 pointLight(float3 p, float3 n, float3 base, float roughness, float wetMask, float3 lightPosition,
                  float3 color, float intensity, float radius)
{
    float3 delta = lightPosition - p;
    float dist = length(delta);
    float3 l = delta / max(dist, .001);
    float attenuation = pow(saturate(1 - dist / radius), 2);
    // A small wrap term softens the terminator on the raised, translucent-looking tissue.
    float diffuse = saturate((dot(n, l) + .40) / 1.40);
    // Orthographic viewer sits along +Z. The moving halfway vector shifts the wet highlights.
    float3 halfway = normalize(l + float3(0, 0, 1));
    float facing = saturate(dot(n, halfway));
    // Broad, subdued highlights keep the surface soft rather than metallic. A small
    // second lobe retains a wet glint without tracing every crease with a sharp wire.
    float specular = (pow(facing, lerp(26, 10, roughness)) * .17
        + pow(facing, 80) * .025) * Surface.y * wetMask * diffuse;
    return (base * diffuse + specular) * color * intensity * attenuation;
}

float4 fragment_main(VsOutput input) : SV_Target0
{
    float2 uv = input.TexCoord;
    float2 world = (uv - .5) * float2(28, 16);
    float4 albedo = Albedo.Sample(AlbedoSampler, uv);
    float4 maps = MaterialMap.Sample(MaterialSampler, uv);
    float3 encodedNormal = Normals.Sample(NormalSampler, uv).xyz;
    float3 n = encodedNormal * 2 - 1;
    n = normalize(float3(n.xy * Surface.x, max(n.z, .001)));
    float3 backdrop = float3(.007, .014, .012) + float3(.004, .006, .002)
        * (.5 + .5 * sin(world.x * .7 + sin(world.y)));
    if (Surface.w > .5)
    {
        float3 debug = Surface.w < 1.5 ? albedo.rgb : Surface.w < 2.5 ? maps.rrr : encodedNormal;
        return float4(lerp(backdrop, debug, albedo.a), 1);
    }
    // Albedo authored as sRGB; do lighting in linear space, then encode the final image.
    float3 base = pow(albedo.rgb, 2.2);
    float3 p = float3(world, maps.r * Options.w);
    float3 lit = base * Surface.z * maps.b;
    // Highlight control follows the local crevice mask, independently of the broad arch height.
    float wetMask = smoothstep(.60, .98, maps.b);
    lit += pointLight(p, n, base, maps.g, wetMask, Light.xyz, LightColor.rgb, Light.w, Options.x);
    lit += pointLight(p, n, base, maps.g, wetMask, float3(11, -4, 4.8), float3(1, .42, .10),
        .9 * Options.y, 18);
    float3 color = lerp(backdrop, lit, albedo.a);
    // Small analytic halo makes the controlled light position visible in the empty space too.
    float d = length(world - Light.xy);
    color += LightColor.rgb * Options.z * (.018 * exp(-d * d / 2)
        + .8 * (1 - smoothstep(.07, .12, d)));
    return float4(pow(saturate(color), 1 / 2.2), 1);
}
