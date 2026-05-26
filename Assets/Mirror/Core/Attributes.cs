using System;

namespace Mirror
{
    /// <summary>
    /// SyncVars are used to automatically synchronize a variable between the server and all clients. The direction of synchronization depends on the Sync Direction property, ServerToClient by default.
    /// <para>
    /// When Sync Direction is equal to ServerToClient, the value should be changed on the server side and synchronized to all clients.
    /// Otherwise, the value should be changed on the client side and synchronized to server and other clients.
    /// </para>
    /// <para>Hook parameter allows you to define a method to be invoked when gets an value update. Notice that the hook method will not be called on the change side.</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class SyncVarAttribute : Attribute
    {
        public string hook;
    }

    /// <summary>
    /// Only an active server will run this method.
    /// <para>Prints a warning if a client or in-active server tries to execute this method.</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class ServerAttribute : Attribute {}

    /// <summary>
    /// Only an active server will run this method.
    /// <para>No warning is thrown.</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class ServerCallbackAttribute : Attribute {}

    /// <summary>
    /// Only an active client will run this method.
    /// <para>Prints a warning if the server or in-active client tries to execute this method.</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class ClientAttribute : Attribute {}

    /// <summary>
    /// Only an active client will run this method.
    /// <para>No warning is printed.</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class ClientCallbackAttribute : Attribute {}

    /// <summary>
    /// Used to make a field readonly in the inspector
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class ReadOnlyAttribute : Attribute {}

    /// <summary>
    /// When defining multiple Readers/Writers for the same type, indicate which one Weaver must use.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class WeaverPriorityAttribute : Attribute {}
}
