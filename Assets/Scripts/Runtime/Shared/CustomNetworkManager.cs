using System;
using System.Threading.Tasks;
using Unity.Multiplayer;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
#if UNITY_SERVER || ENABLE_UCS_SERVER
using Unity.Services.Authentication.Server;
#endif
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace Pokupka
{
    /// <summary>
    /// A custom network manager that implements additional setup logic and rules
    /// </summary>
    [RequireComponent(typeof(NetworkManager))]
    public class CustomNetworkManager : MonoBehaviour
    {
        internal static event Action OnConfigurationLoaded;
        const string k_DefaultServerListenAddress = "0.0.0.0";
        public static CustomNetworkManager Singleton { get; private set; }
        public static ConfigurationManager Configuration { get; private set; }
#if UNITY_EDITOR
        public static bool s_AreTestsRunning = false;
#endif
        internal bool AutoConnectOnStartup
        {
            get
            {
                bool startAutomatically = Configuration.GetBool(ConfigurationManager.k_Autoconnect);
#if UNITY_EDITOR
                startAutomatically |= s_AreTestsRunning;
#endif
                return startAutomatically;
            }
        }

        internal bool IsClient => m_NetworkManager.IsClient;
        internal bool IsServer => m_NetworkManager.IsServer;
        internal bool IsHost => m_NetworkManager.IsHost;

        internal int ExpectedPlayers { get; private set; } = 2;
        NetworkManager m_NetworkManager;

        void Awake()
        {
            if (Singleton == null)
            {
                Singleton = this;
            }
            m_NetworkManager = GetComponent<NetworkManager>();
            m_NetworkManager.OnClientConnectedCallback += OnClientConnected;
            m_NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
            m_NetworkManager.OnServerStarted += OnServerStarted;
        }

        [RuntimeInitializeOnLoadMethod]
        static void OnApplicationStarted()
        {
            if (!Singleton) //this happens during PlayMode tests
            {
                return;
            }
            Configuration = new ConfigurationManager(Singleton, ConfigurationManager.k_DevConfigFile, OnConfigurationLoadedCallback);
        }

        static void OnConfigurationLoadedCallback(ConfigurationManager configurationManager)
        {
            Configuration = configurationManager;
            OnConfigurationLoaded?.Invoke();
            if (Configuration.GetMultiplayerRole() != MultiplayerRoleFlags.Server)
            {
                //note: this is a good place where to load player-specific configuration (I.E: Audio/video settings)
            }
            /* note: this is the entry point for all autoconnected instances (including standalone servers) 
            note 2: waiting a frame seems to be necessary to avoid race conditions related to serialization and network setup when using bots in Host autoconnect mode*/
            Singleton.StartCoroutine(CoroutinesHelper.WaitAndDo(CoroutinesHelper.WaitAFrame(), () => Singleton.InitializeNetworkLogic(false)));
        }

        public void SetConfiguration(ConfigurationManager configuration)
        {
            Configuration = configuration;
        }

        /// <summary>
        /// Initializes the application's network-related behaviour according to the circumstances
        /// </summary>
        /// <param name="gameMode">The game mode to initialize</param>
        /// <param name="startedByUser">Was the setup manually started by the user, I.E: when starting a game manually in single player mode?</param>
        public void InitializeNetworkLogic(bool startedByUser)
        {
            if (IsClient || IsServer)
            {
                m_NetworkManager.Shutdown();
            }

            ExpectedPlayers = Configuration.GetInt(ConfigurationManager.k_MaxPlayers);
            if (ExpectedPlayers < 1)
            {
                Debug.LogError("Can't start a match with less than 1 player, please set MaxPlayers in the configuration or the Bootstrapper to at least 1.");
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
                return;
            }

            var commandLineArgumentsParser = new CommandLineArgumentsParser();
            ushort listeningPort = commandLineArgumentsParser.ServerPort != -1 ? (ushort)commandLineArgumentsParser.ServerPort
                                                                               : (ushort)Configuration.GetInt(ConfigurationManager.k_Port);
            if (startedByUser) //single player mode!
            {
                StartClientAsSinglePlayer(listeningPort);
                return;
            }

            if (AutoConnectOnStartup)
            {
                AutoConnect(listeningPort);
            }
        }

        void StartClientAsSinglePlayer(ushort listeningPort)
        {
            Debug.Log($"Starting Host (single player mode) on port {listeningPort}, expecting {ExpectedPlayers}");
            SetNetworkPortAndAddress(listeningPort, k_DefaultServerListenAddress, k_DefaultServerListenAddress);
            m_NetworkManager.StartHost();
        }

        void AutoConnect(ushort listeningPort)
        {
            MultiplayerRoleFlags multiplayerRole = Configuration.GetMultiplayerRole();
            switch (multiplayerRole)
            {
                case MultiplayerRoleFlags.Client:
                    if (IsClient)
                    {
                        Debug.Log("Already connected!");
                        return;
                    }
                    SetNetworkPortAndAddress(listeningPort, Configuration.GetString(ConfigurationManager.k_ServerIP), k_DefaultServerListenAddress);
                    m_NetworkManager.StartClient();
                    break;
                case MultiplayerRoleFlags.Server:
                    Debug.Log($"Starting server on port {listeningPort}, expecting {ExpectedPlayers} players");
                    Application.targetFrameRate = 60; //lock framerate on dedicated servers
                    OnServerMarkServerAsReadyToAcceptPlayers(listeningPort);
                    break;
                case MultiplayerRoleFlags.ClientAndServer:
                    Debug.Log($"Starting Host on port {listeningPort}, expecting {ExpectedPlayers} players");
                    SetNetworkPortAndAddress(listeningPort, k_DefaultServerListenAddress, k_DefaultServerListenAddress);
                    m_NetworkManager.StartHost();
                    break;
                default:
                    break;
            }
        }

        void OnServerMarkServerAsReadyToAcceptPlayers(ushort listeningPort)
        {
#if UNITY_SERVER || ENABLE_UCS_SERVER
            Task.Run(() => OnServerMarkServerAsReadyToAcceptPlayersAsync(listeningPort));
            return;
#else
            SetNetworkPortAndAddress(listeningPort, k_DefaultServerListenAddress, k_DefaultServerListenAddress);
            m_NetworkManager.StartServer();
            Debug.Log("[Server] Server is ready to accept players");
#endif
        }

#if UNITY_SERVER || ENABLE_UCS_SERVER
        IMultiplaySessionManager m_SessionManager;
        async Task OnServerMarkServerAsReadyToAcceptPlayersAsync(ushort listeningPort)
        {
            if (UnityServices.Instance.GetMultiplayerService() != null)
            {
                await ServerAuthenticationService.Instance.SignInFromServerAsync();
                var token = ServerAuthenticationService.Instance.AccessToken;

                var callbacks = new MultiplaySessionManagerEventCallbacks();
                callbacks.Allocated += CallbacksOnAllocated;

                var sessionManagerOptions = new MultiplaySessionManagerOptions()
                {
                    SessionOptions = new SessionOptions()
                    {
                        MaxPlayers = 2
                    }.WithDirectNetwork(k_DefaultServerListenAddress, k_DefaultServerListenAddress, listeningPort),

                    MultiplayServerOptions = new MultiplayServerOptions(
                        serverName: "Dummy",
                        gameType: "TemplateGame",
                        buildId: "0",
                        map: "TemplateMap"
                    ),
                    Callbacks = callbacks
                };
                m_SessionManager = await MultiplayerServerService.Instance.StartMultiplaySessionManagerAsync(sessionManagerOptions);

                //continue this after the allocation happened
                async void CallbacksOnAllocated(IMultiplayAllocation obj)
                {
                    var session = m_SessionManager.Session;
                    await m_SessionManager.SetPlayerReadinessAsync(true);
                    Debug.Log("[Multiplay] Server is ready to accept players");
                }
            }
        }
#endif

        void SetNetworkPortAndAddress(ushort port, string address, string serverListenAddress)
        {
            var transport = GetComponent<UnityTransport>();
            if (transport == null) //happens during Play Mode Tests
            {
                return;
            }
            transport.SetConnectionData(address, port, serverListenAddress);
        }

        void OnServerStarted()
        {
            Debug.Log("[Server] Server started");
        }

        void OnClientDisconnected(ulong ClientId)
        {
            Debug.Log($"Client {ClientId} disconnected");
        }

        void OnClientConnected(ulong ClientId)
        {
            if (IsClient)
            {
                Debug.Log($"Local client {ClientId} connected");
            }
            else
            {
                Debug.Log($"Remote client {ClientId} connected");
            }
        }
    }
}
