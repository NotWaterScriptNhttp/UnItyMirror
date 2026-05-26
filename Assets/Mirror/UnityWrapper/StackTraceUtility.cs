using System;
using System.Text;
using System.Diagnostics;

namespace UnityEngine
{
    public static class StackTraceUtility
    {
		public static string ExtractStackTrace()
        {
            var sb = new StringBuilder();
            var st = new StackTrace(false);
            foreach (var f in st.GetFrames())
            {
                var m = f.GetMethod();
                if (m != null && m.DeclaringType is Type dt)
                {
                    if (!string.IsNullOrEmpty(dt.Namespace))
                        sb.Append(dt.Namespace + ".");

                    sb.Append(dt.Name + ":");
                    sb.Append(m.Name + "(");

                    bool addSep = false;
                    foreach (var p in m.GetParameters())
                    {
                        if (addSep)
                            sb.Append(", ");

                        addSep = true;
                        sb.Append(p.ParameterType.Name);
                    }
                    sb.Append(")\n");
                }
            }

            return sb.ToString();
        }
    }
}
