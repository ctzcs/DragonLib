using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Engine;

[JsonConverter(typeof(JsonConverter))]
public struct Vector2Int : IEquatable<Vector2Int>, IFormattable
    {
        public int X
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => m_X;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => m_X = value;
        }


        public int Y
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => m_Y;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => m_Y = value;
        }

        private int m_X;
        private int m_Y;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector2Int(int x, int y)
        {
            m_X = x;
            m_Y = y;
        }

        // Set x and y components of an existing Vector.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(int x, int y)
        {
            m_X = x;
            m_Y = y;
        }

        // Access the /x/ or /y/ component using [0] or [1] respectively.
        public int this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                switch (index)
                {
                    case 0: return X;
                    case 1: return Y;
                    default:
                        throw new IndexOutOfRangeException(String.Format("Invalid Vector2Int index addressed: {0}!", index));
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                switch (index)
                {
                    case 0: X = value; break;
                    case 1: Y = value; break;
                    default:
                        throw new IndexOutOfRangeException(String.Format("Invalid Vector2Int index addressed: {0}!", index));
                }
            }
        }

        // Returns the length of this vector (RO).
        public float magnitude { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return Mathf.Sqrt((float)(X * X + Y * Y)); } }

        // Returns the squared length of this vector (RO).
        public int sqrMagnitude { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return X * X + Y * Y; } }

        // Returns the distance between /a/ and /b/.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Distance(Vector2Int a, Vector2Int b)
        {
            float diff_x = a.X - b.X;
            float diff_y = a.Y - b.Y;

            return (float)Math.Sqrt(diff_x * diff_x + diff_y * diff_y);
        }

        // Returns a vector that is made from the smallest components of two vectors.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2Int Min(Vector2Int lhs, Vector2Int rhs) { return new Vector2Int(Math.Min(lhs.X, rhs.X), Math.Min(lhs.Y, rhs.Y)); }

        // Returns a vector that is made from the largest components of two vectors.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2Int Max(Vector2Int lhs, Vector2Int rhs) { return new Vector2Int(Math.Max(lhs.X, rhs.X), Math.Max(lhs.Y, rhs.Y)); }

        // Multiplies two vectors component-wise.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2Int Scale(Vector2Int a, Vector2Int b) { return new Vector2Int(a.X * b.X, a.Y * b.Y); }

        // Multiplies every component of this vector by the same component of /scale/.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Scale(Vector2Int scale) { X *= scale.X; Y *= scale.Y; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clamp(Vector2Int min, Vector2Int max)
        {
            X = Math.Max(min.X, X);
            X = Math.Min(max.X, X);
            Y = Math.Max(min.Y, Y);
            Y = Math.Min(max.Y, Y);
        }

        // Converts a Vector2Int to a [[Vector2]].
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Vector2(Vector2Int v)
        {
            return new Vector2(v.X, v.Y);
        }

        /*// Converts a Vector2Int to a [[Vector3Int]].
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator Vector3Int(Vector2Int v)
        {
            return new Vector3Int(v.x, v.y, 0);
        }*/

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2Int FloorToInt(Vector2 v)
        {
            return new Vector2Int(
                Mathf.FloorToInt(v.X),
                Mathf.FloorToInt(v.Y)
            );
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2Int CeilToInt(Vector2 v)
        {
            return new Vector2Int(
                Mathf.CeilToInt(v.X),
                Mathf.CeilToInt(v.Y)
            );
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2Int RoundToInt(Vector2 v)
        {
            return new Vector2Int(
                Mathf.RoundToInt(v.X),
                Mathf.RoundToInt(v.Y)
            );
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2Int operator-(Vector2Int v)
        {
            return new Vector2Int(-v.X, -v.Y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2Int operator+(Vector2Int a, Vector2Int b)
        {
            return new Vector2Int(a.X + b.X, a.Y + b.Y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2Int operator-(Vector2Int a, Vector2Int b)
        {
            return new Vector2Int(a.X - b.X, a.Y - b.Y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2Int operator*(Vector2Int a, Vector2Int b)
        {
            return new Vector2Int(a.X * b.X, a.Y * b.Y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2Int operator*(int a, Vector2Int b)
        {
            return new Vector2Int(a * b.X, a * b.Y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2Int operator*(Vector2Int a, int b)
        {
            return new Vector2Int(a.X * b, a.Y * b);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2Int operator/(Vector2Int a, int b)
        {
            return new Vector2Int(a.X / b, a.Y / b);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator==(Vector2Int lhs, Vector2Int rhs)
        {
            return lhs.X == rhs.X && lhs.Y == rhs.Y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator!=(Vector2Int lhs, Vector2Int rhs)
        {
            return !(lhs == rhs);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object other)
        {
            if (other is Vector2Int v)
                return Equals(v);
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Vector2Int other)
        {
            return X == other.X && Y == other.Y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
           const int p1 = 73856093;
           const int p2 = 83492791;
           return (X * p1) ^ (Y * p2);
        }

        /// *listonly*
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString()
        {
            return ToString(null, null);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ToString(string format)
        {
            return ToString(format, null);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ToString(string format, IFormatProvider formatProvider)
        {
            if (formatProvider == null)
                formatProvider = CultureInfo.InvariantCulture.NumberFormat;
            return $"({X.ToString(format, formatProvider)}, {Y.ToString(format, formatProvider)})";
        }

        public static Vector2Int Zero { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; } = new Vector2Int(0, 0);
        public static Vector2Int One {[MethodImpl(MethodImplOptions.AggressiveInlining)] get; } = new Vector2Int(1, 1);
        public static Vector2Int Up { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; } = new Vector2Int(0, 1);
        public static Vector2Int Down { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; } = new Vector2Int(0, -1);
        public static Vector2Int Left { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; } = new Vector2Int(-1, 0);
        public static Vector2Int Right { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; } = new Vector2Int(1, 0);
        
        public sealed class JsonConverter : JsonConverter<Vector2Int>
        {
            public override Vector2Int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.String)
                {
                    var s = reader.GetString() ?? "0,0";
                    return ParseKey(s);
                }
                if (reader.TokenType == JsonTokenType.StartObject)
                {
                    int x = 0, y = 0;
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                    {
                        if (reader.TokenType == JsonTokenType.PropertyName)
                        {
                            var name = reader.GetString();
                            reader.Read();
                            if (string.Equals(name, "X", StringComparison.OrdinalIgnoreCase)) x = reader.GetInt32();
                            else if (string.Equals(name, "Y", StringComparison.OrdinalIgnoreCase)) y = reader.GetInt32();
                            else reader.Skip();
                        }
                    }
                    return new Vector2Int(x, y);
                }
                return new Vector2Int(0, 0);
            }

            public override void Write(Utf8JsonWriter writer, Vector2Int value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();
                writer.WriteNumber("X", value.X);
                writer.WriteNumber("Y", value.Y);
                writer.WriteEndObject();
            }

            public override Vector2Int ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                var s = reader.GetString() ?? "0,0";
                return ParseKey(s);
            }

            public override void WriteAsPropertyName(Utf8JsonWriter writer, Vector2Int value, JsonSerializerOptions options)
            {
                writer.WritePropertyName($"{value.X},{value.Y}");
            }

            static Vector2Int ParseKey(string s)
            {
                var sep = s.Contains('|') ? '|' : ',';
                var parts = s.Split(sep);
                if (parts.Length != 2) return new Vector2Int(0, 0);
                int x = 0, y = 0;
                int.TryParse(parts[0], out x);
                int.TryParse(parts[1], out y);
                return new Vector2Int(x, y);
            }
        }
    }

