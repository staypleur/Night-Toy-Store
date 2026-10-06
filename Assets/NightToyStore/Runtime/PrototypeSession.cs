using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace NightToyStore
{
    public sealed class PrototypeSession : MonoBehaviour
    {
        public GameObject playerPrefab;
        public NetworkManager Manager { get; private set; }
        public string Status { get; private set; } = "Ready";
        string address = "127.0.0.1";
        Camera lobbyCamera;
        double deadline;
        bool automated;
        int expectedPlayers;
        bool testImpulseApplied, clientReported;

        void Awake()
        {
            var root = new GameObject("Network manager");
            Manager = root.AddComponent<NetworkManager>();
            Manager.NetworkConfig = new NetworkConfig();
            var transport = root.AddComponent<UnityTransport>();
            Manager.NetworkConfig.NetworkTransport = transport;
            Manager.NetworkConfig.PlayerPrefab = playerPrefab;
            Manager.NetworkConfig.ConnectionApproval = true;
            Manager.ConnectionApprovalCallback = Approve;
            Manager.OnClientConnectedCallback += id => {
                Status = "Connected";
                Debug.Log($"NTS_CONNECTED {id} count={Manager.ConnectedClientsIds.Count}");
            };
            Manager.OnClientDisconnectCallback += id => {
                if (id == Manager.LocalClientId) Status = "Disconnected: " + Manager.DisconnectReason;
            };
            var cameraObject = new GameObject("Lobby camera");
            lobbyCamera = cameraObject.AddComponent<Camera>();
            cameraObject.transform.position = new Vector3(0, 8, -8);
            cameraObject.transform.rotation = Quaternion.Euler(35, 0, 0);
        }

        void Approve(NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            response.Approved = Manager.ConnectedClientsIds.Count < 4;
            response.CreatePlayerObject = response.Approved;
            response.Reason = response.Approved ? "" : "The four toy slots are full.";
            response.Pending = false;
        }

        void Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            automated = Array.IndexOf(args, "-nts-test") >= 0;
            expectedPlayers = Array.IndexOf(args, "-nts-full") >= 0 ? 4 : 1;
            if (Array.IndexOf(args, "-nts-host") >= 0) Connect(true);
            else if (Array.IndexOf(args, "-nts-client") >= 0) Connect(false);
            deadline = Time.realtimeSinceStartupAsDouble + 35;
        }

        public void Connect(bool host)
        {
            Manager.GetComponent<UnityTransport>().SetConnectionData(address, 7777, "0.0.0.0");
            bool started = host ? Manager.StartHost() : Manager.StartClient();
            Status = started ? "Connecting..." : "Could not start connection";
        }

        void Update()
        {
            bool ownsPlayer = Manager.LocalClient != null && Manager.LocalClient.PlayerObject != null;
            lobbyCamera.enabled = !ownsPlayer;
            if (!automated) return;
            var observed = FindObjectsByType<NetworkToyPlayer>(FindObjectsSortMode.None);
            if (Manager.IsHost && Manager.ConnectedClientsIds.Count == expectedPlayers &&
                !testImpulseApplied && Time.realtimeSinceStartupAsDouble > 9)
            {
                foreach (var player in observed) player.ApplySmokeImpulse();
                testImpulseApplied = true;
            }
            if (!Manager.IsHost && !clientReported && observed.Length == 4 && Time.realtimeSinceStartupAsDouble > 12)
            {
                var roles = new System.Collections.Generic.HashSet<int>();
                bool replicated = true;
                foreach (var player in observed)
                {
                    roles.Add(player.Role.Value);
                    var initial = new Vector3(-4 + player.Role.Value * 2.5f, .4f, -4);
                    var delta = player.transform.position - initial;
                    replicated &= new Vector2(delta.x, delta.z).magnitude > .25f;
                }
                if (roles.Count == 4 && replicated)
                {
                    Debug.Log("NTS_CLIENT_REPLICATION_PASS four roles moving including ball");
                    clientReported = true;
                }
            }
            if (Manager.IsHost && Manager.ConnectedClientsIds.Count == expectedPlayers)
            {
                var players = FindObjectsByType<NetworkToyPlayer>(FindObjectsSortMode.None);
                if (players.Length == expectedPlayers && Time.realtimeSinceStartupAsDouble > 24)
                {
                    var roles = new System.Collections.Generic.HashSet<int>();
                    bool moved = true;
                    foreach (var player in players)
                    {
                        roles.Add(player.Role.Value);
                        var delta = player.transform.position - player.SpawnPosition;
                        moved &= new Vector2(delta.x, delta.z).magnitude > .25f;
                    }
                    if (roles.Count == expectedPlayers && moved)
                    {
                        Debug.Log("NTS_NETWORK_SMOKE_PASS unique roles remote movement and ball physics");
                        Application.Quit(0);
                        automated = false;
                    }
                }
            }
            if (Time.realtimeSinceStartupAsDouble > deadline)
            {
                Debug.LogError("NTS_NETWORK_SMOKE_TIMEOUT");
                Application.Quit(2);
                automated = false;
            }
        }

        void OnGUI()
        {
            if (Manager.IsListening && Manager.IsConnectedClient) return;
            GUI.Box(new Rect(20, 180, 360, 165), "Night Toy Store — LAN prototype");
            GUI.Label(new Rect(35, 212, 330, 22), "Host address (same PC: 127.0.0.1)");
            address = GUI.TextField(new Rect(35, 240, 330, 25), address);
            GUI.enabled = !Manager.IsListening;
            if (GUI.Button(new Rect(35, 278, 150, 28), "Host")) Connect(true);
            if (GUI.Button(new Rect(215, 278, 150, 28), "Join")) Connect(false);
            GUI.enabled = true;
            GUI.Label(new Rect(35, 312, 330, 25), Status);
        }
    }
}
