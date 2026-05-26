using System;

namespace UnityEngine
{
    public static class Mathf
    {
        public static int Max(int a1, int a2) => Math.Max(a1, a2);

        public static int Clamp(int v, int min, int max)
        {
            if (v > max)
                return max;
            if (v < min)
                return min;

            return v;
        }
        public static float Clamp(float v, float min, float max)
        {
            if (v > max)
                return max;
            if (v < min)
                return min;

            return v;
        }

        public static int RoundToInt(double d) => (int)Math.Floor(d + 0.5d);
        public static float Lerp(float a, float b, float t) => Clamp(a + (b - a) * t, 0, 1);

        // Gemini cooked these
        public static ushort FloatToHalf(float v)
        {
            // Get the raw 32 bits of the float
            uint f32 = BitConverter.ToUInt32(BitConverter.GetBytes(v), 0);

            // Extract sign, exponent, and mantissa
            uint sign = (f32 >> 16) & 0x8000;
            int exponent = (int)((f32 >> 23) & 0xFF) - 127;
            uint mantissa = f32 & 0x007FFFFF;

            // Handle Zero or Denormals
            if (exponent <= -15)
            {
                return (ushort)sign;
            }

            // Handle Infinity or NaN
            if (exponent > 15)
            {
                return (ushort)(sign | 0x7C00);
            }

            // Standard Case: Re-bias exponent for 5-bits and shift mantissa to 10-bits
            exponent += 15;
            return (ushort)(sign | (uint)(exponent << 10) | (mantissa >> 13));
        }
        public static float HalfToFloat(ushort v)
        {
            uint sign = (uint)(v & 0x8000) << 16;
            int exponent = (v & 0x7C00) >> 10;
            uint mantissa = (uint)(v & 0x03FF) << 13;

            // Case: Zero
            if (exponent == 0 && mantissa == 0)
            {
                return BitConverter.ToSingle(BitConverter.GetBytes(sign), 0);
            }

            // Case: Infinity or NaN
            if (exponent == 0x1F)
            {
                return BitConverter.ToSingle(BitConverter.GetBytes(sign | 0x7F800000 | mantissa), 0);
            }

            // Standard Case: Re-bias the exponent
            exponent = exponent - 15 + 127;

            uint res = sign | (uint)(exponent << 23) | mantissa;
            return BitConverter.ToSingle(BitConverter.GetBytes(res), 0);
        }
    }
}
