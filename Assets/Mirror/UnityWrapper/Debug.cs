using System;

namespace UnityEngine
{
    public static class Debug
    {
        public static void Log(string msg) => Console.WriteLine(msg);

        public static void LogWarning(string msg) => Console.WriteLine("WARN: {0}", msg);

        public static void LogError(string msg) => Console.WriteLine("ERROR: {0}", msg);
        public static void LogError(string msg, object o) => Console.WriteLine("ERROR: {0} in {1}", msg, o);

        public static void LogException(Exception e) => Console.WriteLine("Exception: {0}", e);
        public static void LogException(Exception e, object o) => Console.WriteLine("Exception: {0} in {1}", e, o);

        public static void Assert(bool val, string msg)
        {
            if (!val)
                Debug.LogError(msg);
        }
    }
}
