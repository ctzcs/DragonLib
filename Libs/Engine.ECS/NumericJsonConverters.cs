using System;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Engine.ECS;

/// <summary>
/// Quaternion / Matrix4x4 的 STJ 转换器：只序列化真实数据字段，跳过 IsIdentity、
/// Translation 这类派生属性（否则存盘值带只读属性，既难看也让差量比较不稳定）。
/// 注册在 PrefabSerializer.Options 上，prefab/level 的组件序列化共用。
/// </summary>
public sealed class QuaternionConverter : JsonConverter<Quaternion>
{
    public override Quaternion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        float x = 0f, y = 0f, z = 0f, w = 1f;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;
            if (reader.TokenType != JsonTokenType.PropertyName)
                continue;
            var name = reader.GetString();
            reader.Read();
            switch (name)
            {
                case "X": x = reader.GetSingle(); break;
                case "Y": y = reader.GetSingle(); break;
                case "Z": z = reader.GetSingle(); break;
                case "W": w = reader.GetSingle(); break;
            }
        }
        return new Quaternion(x, y, z, w);
    }

    public override void Write(Utf8JsonWriter writer, Quaternion value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("X", value.X);
        writer.WriteNumber("Y", value.Y);
        writer.WriteNumber("Z", value.Z);
        writer.WriteNumber("W", value.W);
        writer.WriteEndObject();
    }
}

public sealed class Matrix4x4Converter : JsonConverter<Matrix4x4>
{
    public override Matrix4x4 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var m = default(Matrix4x4);
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;
            if (reader.TokenType != JsonTokenType.PropertyName)
                continue;
            var name = reader.GetString();
            reader.Read();
            switch (name)
            {
                case "M11": m.M11 = reader.GetSingle(); break;
                case "M12": m.M12 = reader.GetSingle(); break;
                case "M13": m.M13 = reader.GetSingle(); break;
                case "M14": m.M14 = reader.GetSingle(); break;
                case "M21": m.M21 = reader.GetSingle(); break;
                case "M22": m.M22 = reader.GetSingle(); break;
                case "M23": m.M23 = reader.GetSingle(); break;
                case "M24": m.M24 = reader.GetSingle(); break;
                case "M31": m.M31 = reader.GetSingle(); break;
                case "M32": m.M32 = reader.GetSingle(); break;
                case "M33": m.M33 = reader.GetSingle(); break;
                case "M34": m.M34 = reader.GetSingle(); break;
                case "M41": m.M41 = reader.GetSingle(); break;
                case "M42": m.M42 = reader.GetSingle(); break;
                case "M43": m.M43 = reader.GetSingle(); break;
                case "M44": m.M44 = reader.GetSingle(); break;
            }
        }
        return m;
    }

    public override void Write(Utf8JsonWriter writer, Matrix4x4 value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("M11", value.M11);
        writer.WriteNumber("M12", value.M12);
        writer.WriteNumber("M13", value.M13);
        writer.WriteNumber("M14", value.M14);
        writer.WriteNumber("M21", value.M21);
        writer.WriteNumber("M22", value.M22);
        writer.WriteNumber("M23", value.M23);
        writer.WriteNumber("M24", value.M24);
        writer.WriteNumber("M31", value.M31);
        writer.WriteNumber("M32", value.M32);
        writer.WriteNumber("M33", value.M33);
        writer.WriteNumber("M34", value.M34);
        writer.WriteNumber("M41", value.M41);
        writer.WriteNumber("M42", value.M42);
        writer.WriteNumber("M43", value.M43);
        writer.WriteNumber("M44", value.M44);
        writer.WriteEndObject();
    }
}
