using System;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using UnityEngine;

namespace Mirror
{
    // Handles network messages on client and server
    public delegate void NetworkMessageDelegate(NetworkConnection conn, NetworkReader reader, int channelId);

    // channels are const ints instead of an enum so people can add their own
    // channels (can't extend an enum otherwise).
    //
    // note that Mirror is slowly moving towards quake style networking which
    // will only require reliable for handshake, and unreliable for the rest.
    // so eventually we can change this to an Enum and transports shouldn't
    // add custom channels anymore.
    public static class Channels
    {
        public const int Reliable = 0;   // ordered
        public const int Unreliable = 1; // unordered
    }

    public static class Utils
    {
        // detect headless / dedicated server mode
        // SystemInfo.graphicsDeviceType is never null in the editor.
        // UNITY_SERVER works in builds for all Unity versions 2019 LTS and later.
        // For Unity 2019 / 2020, there is no way to detect Server Build checkbox
        //    state in Build Settings, so they never auto-start headless server / client.
        // UNITY_SERVER works in the editor in Unity 2021 LTS and later
        //    because that's when Dedicated Server platform was added.
        // It is intentional for editor play mode to auto-start headless server / client
        //    when Dedicated Server platform is selected in the editor so that editor
        //    acts like a headless build to every extent possible for testing / debugging.
        public static bool IsHeadless() => true;

        // detect WebGL mode
        public const bool IsWebGL = false;

        // detect Debug mode
        public const bool IsDebug =
#if DEBUG
            true;
#else
            false;
#endif

        public static uint GetTrueRandomUInt()
        {
            // use Crypto RNG to avoid having time based duplicates
            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
            {
                byte[] bytes = new byte[4];
                rng.GetBytes(bytes);
                return BitConverter.ToUInt32(bytes, 0);
            }
        }

        // pretty print bytes as KB/MB/GB/etc. from DOTSNET
        // long to support > 2GB
        // divides by floats to return "2.5MB" etc.
        public static string PrettyBytes(long bytes)
        {
            // bytes
            if (bytes < 1024)
                return $"{bytes} B";
            // kilobytes
            else if (bytes < 1024L * 1024L)
                return $"{(bytes / 1024f):F2} KB";
            // megabytes
            else if (bytes < 1024 * 1024L * 1024L)
                return $"{(bytes / (1024f * 1024f)):F2} MB";
            // gigabytes
            return $"{(bytes / (1024f * 1024f * 1024f)):F2} GB";
        }

        // pretty print seconds as hours:minutes:seconds(.milliseconds/100)s.
        // double for long running servers.
        public static string PrettySeconds(double seconds)
        {
            TimeSpan t = TimeSpan.FromSeconds(seconds);
            string res = "";
            if (t.Days > 0) res += $"{t.Days}d";
            if (t.Hours > 0) res += $"{(res.Length > 0 ? " " : "")}{t.Hours}h";
            if (t.Minutes > 0) res += $"{(res.Length > 0 ? " " : "")}{t.Minutes}m";
            // 0.5s, 1.5s etc. if any milliseconds. 1s, 2s etc. if any seconds
            if (t.Milliseconds > 0) res += $"{(res.Length > 0 ? " " : "")}{t.Seconds}.{(t.Milliseconds / 100)}s";
            else if (t.Seconds > 0) res += $"{(res.Length > 0 ? " " : "")}{t.Seconds}s";
            // if the string is still empty because the value was '0', then at least
            // return the seconds instead of returning an empty string
            return res != "" ? res : "0s";
        }

        // create local connections pair and connect them
        public static void CreateLocalConnections(
            out LocalConnectionToClient connectionToClient,
            out LocalConnectionToServer connectionToServer)
        {
            connectionToServer = new LocalConnectionToServer();
            connectionToClient = new LocalConnectionToClient();
            connectionToServer.connectionToClient = connectionToClient;
            connectionToClient.connectionToServer = connectionToServer;
        }
    }
}
