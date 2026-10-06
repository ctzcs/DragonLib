#include "PostFullscreen.hlsli"
cbuffer BloomSettings : register(b0, space3)
{
    float4 Blur; // UV direction, threshold, extract mode
    float4 Extract; // source texel size, soft knee, unused
};
Texture2D SourceTexture : register(t0, space2);
SamplerState SourceSampler : register(s0, space2);
float3 ExtractBright(float3 color)
{
    float brightness = max(color.r, max(color.g, color.b));
    float knee = max(Blur.z * Extract.z, .00001);
    float soft = clamp(brightness - Blur.z + knee, 0, 2 * knee);
    soft = soft * soft / (4 * knee);
    return color * max(brightness - Blur.z, soft) / max(brightness, .00001);
}
float4 fragment_main(VsOutput input) : SV_Target0
{
    if (Blur.w > .5)
    {
        float3 color = 0;
        // 四分之一分辨率，覆盖对应的 4x4 源像素，避免小的高亮从降采样中漏掉。
        for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
                color += ExtractBright(SourceTexture.Sample(SourceSampler, input.Uv + (float2(x, y) - 1.5) * Extract.xy).rgb);
        return float4(color / 16, 1);
    }
    float3 color = SourceTexture.Sample(SourceSampler, input.Uv).rgb * .227027;
    for (int i = 1; i <= 4; i++)
    {
        float weight = i == 1 ? .194595 : i == 2 ? .121622 : i == 3 ? .054054 : .016216;
        color += (SourceTexture.Sample(SourceSampler, input.Uv + Blur.xy * i).rgb +
            SourceTexture.Sample(SourceSampler, input.Uv - Blur.xy * i).rgb) * weight;
    }
    return float4(color, 1);
}
