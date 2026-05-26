using System;

namespace UnityEngine
{
    public struct Quaternion
    {
        public static readonly Quaternion identityQuaternion = new Quaternion(0, 0, 0, 1);
        public static Quaternion identity => identityQuaternion;

        public float x, y, z, w;

        public Quaternion normalized
        {
            get
            {
                float n = Mathf.Sqrt(Dot(this, this));
                if (n < Mathf.Epsilon)
                    return identity;

                return new Quaternion(this.x / n, this.y / n, this.z / n, this.w / n);
            }
        }

        public static float Dot(Quaternion a, Quaternion b) => a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;

        public Quaternion(float x, float y, float z, float w)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
        }

        public float this[int idx]
        {
            get
            {
                switch (idx)
                {
                    case 0:
                        return x;
                    case 1:
                        return y;
                    case 2:
                        return z;
                    case 3:
                        return w;
                    default:
                        throw new IndexOutOfRangeException("Invalid Quaternion index!");
                }
            }
            set
            {
                switch (idx)
                {
                    case 0:
                        x = value;
                        break;
                    case 1:
                        y = value;
                        break;
                    case 2:
                        z = value;
                        break;
                    case 3:
                        w = value;
                        break;
                    default:
                        throw new IndexOutOfRangeException("Invalid Quaternion index!");
                }
            }
        }
    }
}
