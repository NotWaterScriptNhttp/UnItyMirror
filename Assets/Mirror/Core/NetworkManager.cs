using System;

using UnityEngine;

namespace Mirror
{
    public enum NetworkManagerMode { Offline, ServerOnly, ClientOnly, Host }
    public enum HeadlessStartOptions { DoNothing, AutoStartServer, AutoStartClient }

    public class NetworkManager
    {
        /// <summary>Should the server auto-start when 'Server Build' is checked in build settings</summary>
        public HeadlessStartOptions headlessStartMode = HeadlessStartOptions.DoNothing;

        /// <summary>Server Update frequency, per second. Use around 60Hz for fast paced games like Counter-Strike to minimize latency. Use around 30Hz for games like WoW to minimize computations. Use around 1-10Hz for slow paced games like EVE.</summary>
        public int sendRate = 60;
        public int unreliableBaselineRate = 1;

        // quake sends unreliable messages twice to make up for message drops.
        // this double bandwidth, but allows for smaller buffer time / faster sync.
        // best to turn this off unless the game is extremely fast paced.
        public bool unreliableRedundancy = false;

        // client send rate follows server send rate to avoid errors for now
        /// <summary>Client Update frequency, per second. Use around 60Hz for fast paced games like Counter-Strike to minimize latency. Use around 30Hz for games like WoW to minimize computations. Use around 1-10Hz for slow paced games like EVE.</summary>
        // [Tooltip("Client broadcasts 'sendRate' times per second. Use around 60Hz for fast paced games like Counter-Strike to minimize latency. Use around 30Hz for games like WoW to minimize computations. Use around 1-10Hz for slow paced games like EVE.")]
        // public int clientSendRate = 30; // 33 ms

        /// <summary>Automatically switch to this scene upon going offline (on start / on disconnect / on shutdown).</summary>
        // transport layer
        public Transport transport;

        /// <summary>Server's address for clients to connect to.</summary>
        public string networkAddress = "localhost";

        /// <summary>The maximum number of concurrent network connections to support.</summary>
        public int maxConnections = 100;

        // Mirror global disconnect inactive option, independent of Transport.
        // not all Transports do this properly, and it's easiest to configure this just once.
        // this is very useful for some projects, keep it.
        public bool disconnectInactiveConnections;
        public float disconnectInactiveTimeout = 60f;
        public bool exceptionsDisconnect = true; // security by default

        public NetworkAuthenticator authenticator;

        public SnapshotInterpolationSettings snapshotSettings = new SnapshotInterpolationSettings();
        public ConnectionQualityMethod evaluationMethod;
        public float evaluationInterval = 3;

        /// <summary>The one and only NetworkManager</summary>
        public static NetworkManager singleton { get; internal set; }

        /// <summary>Number of active player objects across all connections on the server.</summary>
        public int numPlayers => NetworkServer.connections.Count;
        /// <summary>True if the server is running or client is connected/connecting.</summary>
        public bool isNetworkActive => NetworkServer.active || NetworkClient.active;

        // TODO remove this
        // internal for tests
        internal static NetworkConnection clientReadyConnection;

        // helper enum to know if we started the networkmanager as server/client/host.
        // -> this is necessary because when StartHost changes server scene to
        //    online scene, FinishLoadScene is called and the host client isn't
        //    connected yet (no need to connect it before server was fully set up).
        //    in other words, we need this to know which mode we are running in
        //    during FinishLoadScene.
        public NetworkManagerMode mode { get; private set; }

        // virtual so that inheriting classes' OnValidate() can call base.OnValidate() too
        public virtual void OnValidate()
        {
            // unreliable full send rate needs to be >= 0.
            // we need to have something to delta compress against.
            // it should also be <= sendRate otherwise there's no point.
            unreliableBaselineRate = Mathf.Clamp(unreliableBaselineRate, 1, sendRate);

            // always >= 0
            maxConnections = Mathf.Max(maxConnections, 0);
        }

        // virtual so that inheriting classes' Reset() can call base.Reset() too
        // Reset only gets called when the component is added or the user resets the component
        // Thats why we validate these things that only need to be validated on adding the NetworkManager here
        // If we would do it in OnValidate() then it would run this everytime a value changes
        public virtual void Reset() {}

        // virtual so that inheriting classes' Awake() can call base.Awake() too
        public virtual void Awake()
        {
            // Don't allow collision-destroyed second instance to continue.
            if (!InitializeSingleton()) return;

            // Apply configuration in Awake once already
            ApplyConfiguration();
        }

        // virtual so that inheriting classes' Start() can call base.Start() too
        public virtual void Start()
        {
            // Auto-start headless server or client.
            //
            // We can't do this in Awake because Awake is for initialization
            // and some transports might not be ready until Start.
            //
            // Auto-starting in Editor is useful for debugging, so that can
            // be enabled with editorAutoStart.
            if (Utils.IsHeadless())
                switch (headlessStartMode)
                {
                    case HeadlessStartOptions.AutoStartServer:
                        StartServer();
                        break;
                    case HeadlessStartOptions.AutoStartClient:
                        StartClient();
                        break;
                }
        }

        // make sure to call base.Update() when overwriting
        public virtual void Update()
        {
            ApplyConfiguration();
        }

        // virtual so that inheriting classes' LateUpdate() can call base.LateUpdate() too
        public virtual void LateUpdate() { }

        ////////////////////////////////////////////////////////////////////////

        // NetworkManager exposes some NetworkServer/Client configuration.
        // we apply it every Update() in order to avoid two sources of truth.
        // fixes issues where NetworkServer.sendRate was never set because
        // NetworkManager.StartServer was never called, etc.
        // => all exposed settings should be applied at all times if NM exists.
        void ApplyConfiguration()
        {
            NetworkServer.tickRate = sendRate;
            NetworkServer.unreliableBaselineRate = unreliableBaselineRate;
            NetworkServer.unreliableRedundancy = unreliableRedundancy;
            NetworkClient.snapshotSettings = snapshotSettings;
            NetworkClient.connectionQualityInterval = evaluationInterval;
            NetworkClient.connectionQualityMethod = evaluationMethod;
        }

        // full server setup code, without spawning objects yet
        void SetupServer()
        {
            // Debug.Log("NetworkManager SetupServer");
            InitializeSingleton();

            // apply settings before initializing anything
            NetworkServer.disconnectInactiveConnections = disconnectInactiveConnections;
            NetworkServer.disconnectInactiveTimeout = disconnectInactiveTimeout;
            NetworkServer.exceptionsDisconnect = exceptionsDisconnect;

            if (authenticator != null)
            {
                authenticator.OnStartServer();
                authenticator.OnServerAuthenticated += OnServerAuthenticated;
            }

            ConfigureHeadlessFrameRate();

            // start listening to network connections
            NetworkServer.Listen(maxConnections);

            // this must be after Listen(), since that registers the default message handlers
            RegisterServerMessages();

            // do not call OnStartServer here yet.
            // this is up to the caller. different for server-only vs. host mode.
        }

        /// <summary>Starts the server, listening for incoming connections.</summary>
        public void StartServer()
        {
            if (NetworkServer.active)
            {
                Debug.LogWarning("Server already started.");
                return;
            }

            mode = NetworkManagerMode.ServerOnly;

            // StartServer is inherently ASYNCHRONOUS (=doesn't finish immediately)
            //
            // Here is what it does:
            //   Listen
            //   if onlineScene:
            //       LoadSceneAsync
            //       ...
            //       FinishLoadSceneServerOnly
            //           SpawnObjects
            //   else:
            //       SpawnObjects
            //
            // there is NO WAY to make it synchronous because both LoadSceneAsync
            // and LoadScene do not finish loading immediately. as long as we
            // have the onlineScene feature, it will be asynchronous!

            SetupServer();
        }

        void SetupClient()
        {
            InitializeSingleton();

            // apply settings before initializing anything
            NetworkClient.exceptionsDisconnect = exceptionsDisconnect;
            // NetworkClient.sendRate = clientSendRate;

            if (authenticator != null)
            {
                authenticator.OnStartClient();
                authenticator.OnClientAuthenticated += OnClientAuthenticated;
            }

        }

        /// <summary>Starts the client, connects it to the server with networkAddress.</summary>
        public void StartClient()
        {
            // Do checks and short circuits before setting anything up.
            // If / when we retry, we won't have conflict issues.
            if (NetworkClient.active)
            {
                Debug.LogWarning("Client already started.");
                return;
            }

            if (string.IsNullOrWhiteSpace(networkAddress))
            {
                Debug.LogError("Must set the Network Address field in the manager");
                return;
            }

            mode = NetworkManagerMode.ClientOnly;

            SetupClient();

            // In case this is a headless client...
            ConfigureHeadlessFrameRate();

            RegisterClientMessages();

            NetworkClient.Connect(networkAddress);

            OnStartClient();
        }

        /// <summary>Starts the client, connects it to the server via Uri</summary>
        public void StartClient(Uri uri)
        {
            if (NetworkClient.active)
            {
                Debug.LogWarning("Client already started.");
                return;
            }

            mode = NetworkManagerMode.ClientOnly;

            SetupClient();

            RegisterClientMessages();

            // Debug.Log($"NetworkManager StartClient address:{uri}");
            networkAddress = uri.Host;

            NetworkClient.Connect(uri);

            OnStartClient();
        }

        /// <summary>Starts a network "host" - a server and client in the same application.</summary>
        public void StartHost()
        {
            if (NetworkServer.active || NetworkClient.active)
            {
                Debug.LogWarning("Server or Client already started.");
                return;
            }

            mode = NetworkManagerMode.Host;

            // StartHost is inherently ASYNCHRONOUS (=doesn't finish immediately)
            //
            // Here is what it does:
            //   Listen
            //   ConnectHost
            //   if onlineScene:
            //       LoadSceneAsync
            //       ...
            //       FinishLoadSceneHost
            //           FinishStartHost
            //               SpawnObjects
            //               StartHostClient      <= not guaranteed to happen after SpawnObjects if onlineScene is set!
            //                   ClientAuth
            //                       success: server sends changescene msg to client
            //   else:
            //       FinishStartHost
            //
            // there is NO WAY to make it synchronous because both LoadSceneAsync
            // and LoadScene do not finish loading immediately. as long as we
            // have the onlineScene feature, it will be asynchronous!

            // setup server first
            SetupServer();
            FinishStartHost();
        }

        // This may be set true in StartHost and is evaluated in FinishStartHost
        bool finishStartHostPending;

        // FinishStartHost is guaranteed to be called after the host server was
        // fully started and all the asynchronous StartHost magic is finished
        // (= scene loading), or immediately if there was no asynchronous magic.
        //
        // note: we don't really need FinishStartClient/FinishStartServer. the
        //       host version is enough.
        void FinishStartHost()
        {
            // ConnectHost needs to be called BEFORE SpawnObjects:
            // https://github.com/vis2k/Mirror/pull/1249/
            // -> this sets NetworkServer.localConnection.
            // -> localConnection needs to be set before SpawnObjects because:
            //    -> SpawnObjects calls OnStartServer in all NetworkBehaviours
            //       -> OnStartServer might spawn an object and set [SyncVar(hook="OnColorChanged")] object.color = green;
            //          -> this calls SyncVar.set (generated by Weaver), which has
            //             a custom case for host mode (because host mode doesn't
            //             get OnDeserialize calls, where SyncVar hooks are usually
            //             called):
            //
            //               if (!SyncVarEqual(value, ref color))
            //               {
            //                   if (NetworkServer.localClientActive && !getSyncVarHookGuard(1uL))
            //                   {
            //                       setSyncVarHookGuard(1uL, value: true);
            //                       OnColorChangedHook(value);
            //                       setSyncVarHookGuard(1uL, value: false);
            //                   }
            //                   SetSyncVar(value, ref color, 1uL);
            //               }
            //
            //          -> localClientActive needs to be true, otherwise the hook
            //             isn't called in host mode!
            //
            // TODO call this after spawnobjects and worry about the syncvar hook fix later?
            NetworkClient.ConnectHost();

            // invoke user callbacks AFTER ConnectHost has set .activeHost.
            // this way initialization can properly handle host mode.
            //
            // fixes: https://github.com/MirrorNetworking/Mirror/issues/3302
            // where [SyncVar] hooks wouldn't be called for objects spawned in
            // NetworkManager.OnStartServer, because .activeHost was still false.
            //
            // TODO is there a risk of someone connecting between Listen() and FinishStartHost()?
            OnStartServer();

            // call OnStartHost AFTER SetupServer. this way we can use
            // NetworkServer.Spawn etc. in there too. just like OnStartServer
            // is called after the server is actually properly started.
            OnStartHost();

            // connect client and call OnStartClient AFTER server scene was
            // loaded and all objects were spawned.
            // DO NOT do this earlier. it would cause race conditions where a
            // client will do things before the server is even fully started.
            //Debug.Log("StartHostClient called");
            SetupClient();
            RegisterClientMessages();

            // InvokeOnConnected needs to be called AFTER RegisterClientMessages
            // (https://github.com/vis2k/Mirror/pull/1249/)
            HostMode.InvokeOnConnected();

            OnStartClient();
        }

        /// <summary>This stops both the client and the server that the manager is using.</summary>
        public void StopHost()
        {
            OnStopHost();
            StopClient();
            StopServer();
        }

        /// <summary>Stops the server from listening and simulating the game.</summary>
        public void StopServer()
        {
            // return if already stopped to avoid recursion deadlock
            if (!NetworkServer.active)
                return;

            if (authenticator != null)
            {
                authenticator.OnServerAuthenticated -= OnServerAuthenticated;
                authenticator.OnStopServer();
            }

            OnStopServer();

            //Debug.Log("NetworkManager StopServer");
            NetworkServer.Shutdown();

            // set offline mode BEFORE changing scene so that FinishStartScene
            // doesn't think we need initialize anything.
            mode = NetworkManagerMode.Offline;
        }

        /// <summary>Stops and disconnects the client.</summary>
        public void StopClient()
        {
            if (mode == NetworkManagerMode.Offline)
                return;

            // For Host client, call OnServerDisconnect before NetworkClient.Disconnect
            // because we need NetworkServer.localConnection to not be null
            // NetworkClient.Disconnect will set it null.
            // Only call if localConnection is not null (it might be null if StartHost failed)
            if (mode == NetworkManagerMode.Host && NetworkServer.localConnection != null)
                OnServerDisconnect(NetworkServer.localConnection);

            // ask client -> transport to disconnect.
            // handle voluntary and involuntary disconnects in OnClientDisconnect.
            //
            //   StopClient
            //     NetworkClient.Disconnect
            //       Transport.Disconnect
            //         ...
            //       Transport.OnClientDisconnect
            //     NetworkClient.OnTransportDisconnect
            //   NetworkManager.OnClientDisconnect
            NetworkClient.Disconnect();
        }

        // called when quitting the application by closing the window / pressing
        // stop in the editor. virtual so that inheriting classes'
        // OnApplicationQuit() can call base.OnApplicationQuit() too
        // (this can't be in OnDestroy: https://github.com/MirrorNetworking/Mirror/issues/3952)
        public virtual void OnApplicationQuit()
        {
            // stop client first
            // (we want to send the quit packet to the server instead of waiting
            //  for a timeout)
            if (NetworkClient.isConnected)
            {
                StopClient();
                //Debug.Log("OnApplicationQuit: stopped client");
            }

            // stop server after stopping client (for proper host mode stopping)
            if (NetworkServer.active)
            {
                StopServer();
                //Debug.Log("OnApplicationQuit: stopped server");
            }

            // Call ResetStatics to reset statics and singleton
            ResetStatics();
        }

        /// <summary>Set the frame rate for a headless builds. Override to disable or modify.</summary>
        // useful for dedicated servers.
        // useful for headless benchmark clients.
        public virtual void ConfigureHeadlessFrameRate()
        {
            if (Utils.IsHeadless())
            {
                Application.targetFrameRate = sendRate;
                // Debug.Log($"Server Tick Rate set to {Application.targetFrameRate} Hz.");
            }
        }

        bool InitializeSingleton()
        {
            if (singleton != null && singleton == this)
                return true;

            singleton = this;
            return true;
        }

        void RegisterServerMessages()
        {
            NetworkServer.OnConnectedEvent = OnServerConnectInternal;
            NetworkServer.OnDisconnectedEvent = OnServerDisconnect;
            NetworkServer.OnErrorEvent = OnServerError;
            NetworkServer.OnTransportExceptionEvent = OnServerTransportException;

            // Network Server initially registers its own handler for this, so we replace it here.
            NetworkServer.ReplaceHandler<ReadyMessage>(OnServerReadyMessageInternal);
        }

        void RegisterClientMessages()
        {
            NetworkClient.OnConnectedEvent = OnClientConnectInternal;
            NetworkClient.OnDisconnectedEvent = OnClientDisconnectInternal;
            NetworkClient.OnErrorEvent = OnClientError;
            NetworkClient.OnTransportExceptionEvent = OnClientTransportException;

            // Don't require authentication because server may send NotReadyMessage from ServerChangeScene
            NetworkClient.RegisterHandler<NotReadyMessage>(OnClientNotReadyMessageInternal, false);
        }

        // This is the only way to clear the singleton, so another instance can be created.
        public static void ResetStatics()
        {
            // call StopHost if we have a singleton
            if (singleton != null)
                singleton.StopHost();

            // reset all statics
            clientReadyConnection = null;

            // and finally (in case it isn't null already)...
            singleton = null;
        }

        // virtual so that inheriting classes' OnDestroy() can call base.OnDestroy() too
        public virtual void OnDestroy() {}

        void OnServerConnectInternal(NetworkConnectionToClient conn)
        {
            //Debug.Log("NetworkManager.OnServerConnectInternal");

            if (authenticator != null)
            {
                // we have an authenticator - let it handle authentication
                authenticator.OnServerAuthenticate(conn);
            }
            else
            {
                // authenticate immediately
                OnServerAuthenticated(conn);
            }
        }

        // called after successful authentication
        // TODO do the NetworkServer.OnAuthenticated thing from x branch
        void OnServerAuthenticated(NetworkConnectionToClient conn)
        {
            //Debug.Log("NetworkManager.OnServerAuthenticated");

            // set connection to authenticated
            conn.isAuthenticated = true;
            OnServerConnect(conn);
        }

        void OnServerReadyMessageInternal(NetworkConnectionToClient conn, ReadyMessage msg)
        {
            //Debug.Log("NetworkManager.OnServerReadyMessageInternal");
            OnServerReady(conn);
        }

        void OnClientConnectInternal()
        {
            //Debug.Log("NetworkManager.OnClientConnectInternal");

            if (authenticator != null)
            {
                // we have an authenticator - let it handle authentication
                authenticator.OnClientAuthenticate();
            }
            else
            {
                // authenticate immediately
                OnClientAuthenticated();
            }
        }

        // called after successful authentication
        void OnClientAuthenticated()
        {
            //Debug.Log("NetworkManager.OnClientAuthenticated");

            // set connection to authenticated
            NetworkClient.connection.isAuthenticated = true;
            clientReadyConnection = NetworkClient.connection;

            // Call virtual method regardless of whether a scene change is expected or not.
            OnClientConnect();
        }

        // Transport callback, invoked after client fully disconnected.
        // the call order should always be:
        //   Disconnect() -> ask Transport -> Transport.OnDisconnected -> Cleanup
        void OnClientDisconnectInternal()
        {
            //Debug.Log("NetworkManager.OnClientDisconnectInternal");

            // Only let this run once. StopClient in Host mode changes to ServerOnly
            if (mode == NetworkManagerMode.ServerOnly || mode == NetworkManagerMode.Offline)
                return;

            // user callback
            OnClientDisconnect();

            if (authenticator != null)
            {
                authenticator.OnClientAuthenticated -= OnClientAuthenticated;
                authenticator.OnStopClient();
            }

            // set mode BEFORE changing scene so FinishStartScene doesn't re-initialize anything.
            // set mode BEFORE NetworkClient.Disconnect so StopClient only runs once.
            // set mode BEFORE OnStopClient so StopClient only runs once.
            // If we got here from StopClient in Host mode, change to ServerOnly.
            // - If StopHost was called, StopServer will put us in Offline mode.
            if (mode == NetworkManagerMode.Host)
                mode = NetworkManagerMode.ServerOnly;
            else
                mode = NetworkManagerMode.Offline;

            //Debug.Log("NetworkManager StopClient");
            OnStopClient();

            // shutdown client
            NetworkClient.Shutdown();
        }

        void OnClientNotReadyMessageInternal(NotReadyMessage msg)
        {
            //Debug.Log("NetworkManager.OnClientNotReadyMessageInternal");
            NetworkClient.ready = false;
            OnClientNotReady();

            // NOTE: clientReadyConnection is not set here! don't want OnClientConnect to be invoked again after scene changes.
        }

        /// <summary>Called on the server when a new client connects.</summary>
        public virtual void OnServerConnect(NetworkConnectionToClient conn) { }

        /// <summary>Called on the server when a client disconnects.</summary>
        // Called by NetworkServer.OnTransportDisconnect!
        public virtual void OnServerDisconnect(NetworkConnectionToClient conn) { }

        /// <summary>Called on the server when a client is ready (= loaded the scene)</summary>
        public virtual void OnServerReady(NetworkConnectionToClient conn) => NetworkServer.SetClientReady(conn);

        /// <summary>Called on server when transport raises an exception. NetworkConnection may be null.</summary>
        public virtual void OnServerError(NetworkConnectionToClient conn, TransportError error, string reason) { }

        /// <summary>Called on server when transport raises an exception. NetworkConnection may be null.</summary>
        public virtual void OnServerTransportException(NetworkConnectionToClient conn, Exception exception) { }

        /// <summary>Called from ServerChangeScene immediately before SceneManager.LoadSceneAsync is executed</summary>
        public virtual void OnServerChangeScene(string newSceneName) { }

        /// <summary>Called on server after a scene load with ServerChangeScene() is completed.</summary>
        public virtual void OnServerSceneChanged(string sceneName) { }

        /// <summary>Called on the client when connected to a server. By default it sets client as ready and adds a player.</summary>
        public virtual void OnClientConnect()
        {
            // Ready/AddPlayer is usually triggered by a scene load completing.
            // if no scene was loaded, then Ready/AddPlayer it here instead.
            if (!NetworkClient.ready)
                NetworkClient.Ready();
        }

        /// <summary>Called on clients when disconnected from a server.</summary>
        public virtual void OnClientDisconnect() { }

        /// <summary>Called on client when transport raises an exception.</summary>
        public virtual void OnClientError(TransportError error, string reason) { }

        /// <summary>Called on client when transport raises an exception.</summary>
        public virtual void OnClientTransportException(Exception exception) { }

        /// <summary>Called on clients when a servers tells the client it is no longer ready, e.g. when switching scenes.</summary>
        public virtual void OnClientNotReady() { }

        // Since there are multiple versions of StartServer, StartClient and
        // StartHost, to reliably customize their functionality, users would
        // need override all the versions. Instead these callbacks are invoked
        // from all versions, so users only need to implement this one case.

        /// <summary>This is invoked when a host is started.</summary>
        public virtual void OnStartHost() { }

        /// <summary>This is invoked when a server is started - including when a host is started.</summary>
        public virtual void OnStartServer() { }

        /// <summary>This is invoked when the client is started.</summary>
        public virtual void OnStartClient() { }

        /// <summary>This is called when a server is stopped - including when a host is stopped.</summary>
        public virtual void OnStopServer() { }

        /// <summary>This is called when a client is stopped.</summary>
        public virtual void OnStopClient() { }

        /// <summary>This is called when a host is stopped.</summary>
        public virtual void OnStopHost() { }
    }
}
