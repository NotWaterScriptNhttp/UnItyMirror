using System;

namespace UnityEngine
{
    public static class Random
    {
        private static System.Random _rand = new System.Random();

        public static int Range(int min, int max) => _rand.Next(min, max);
    }
}
