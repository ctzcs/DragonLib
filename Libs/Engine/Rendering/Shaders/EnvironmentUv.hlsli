float2 EnvironmentUv(float3 direction)
{
    direction = normalize(direction);
    return float2(atan2(direction.z, direction.x) / 6.28318530718 + .5, acos(clamp(direction.y, -1, 1)) / 3.14159265359);
}
float3 RotateEnvironment(float3 direction, float angle)
{
    float s = sin(angle), c = cos(angle);
    return float3(c * direction.x - s * direction.z, direction.y, s * direction.x + c * direction.z);
}
