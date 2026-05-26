using System;
using System.Diagnostics;

namespace UnityEngine
{
    public static class Time
    {
        private static Stopwatch _timer = new Stopwatch();

        private static double _currTime = 0d;
        private static double _lastTime = 0d;

        static Time()
        {
            _timer.Start();
        }

        public static float time { get => (float)_currTime; }
        public static float unscaledTime => time;


        public static float deltaTime { get => (float)(_currTime - _lastTime); }
        public static float unscaledDeltaTime => deltaTime;

        public static void Update()
        {
            _lastTime = _currTime;
            _currTime = _timer.Elapsed.TotalSeconds;
        }
    }
}
