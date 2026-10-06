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
        bool twoRoleTest, captureDone;
        int roleTestStage;
        bool hostTestRoleRestored;
        string capturePath;

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
            twoRoleTest = Array.IndexOf(args, "-nts-two-test") >= 0;
            if (twoRoleTest) automated = true;
            int captureIndex = Array.IndexOf(args, "-nts-capture");
            if (captureIndex >= 0 && captureIndex + 1 < args.Length) capturePath = args[captureIndex + 1];
            Application.runInBackground = true;
            expectedPlayers = Array.IndexOf(args, "-nts-full") >= 0 ? 4 : 1;
            if (Array.IndexOf(args, "-nts-host") >= 0) Connect(true);
            else if (Array.IndexOf(args, "-nts-client") >= 0) Connect(false);
            deadline = Time.realtimeSinceStartupAsDouble + 35;
        }

        public void Connect(bool host)
        {
            ushort port = 7777;
            string[] args = Environment.GetCommandLineArgs();
            int portIndex = Array.IndexOf(args, "-nts-port");
            if (portIndex >= 0 && portIndex + 1 < args.Length) ushort.TryParse(args[portIndex + 1], out port);
            Manager.GetComponent<UnityTransport>().SetConnectionData(address, port, "0.0.0.0");
            bool started = host ? Manager.StartHost() : Manager.StartClient();
            Status = started ? "Connecting..." : "Could not start connection";
        }

        void Update()
        {
            bool ownsPlayer = Manager.LocalClient != null && Manager.LocalClient.PlayerObject != null;
            lobbyCamera.enabled = !ownsPlayer;
            if (!captureDone && capturePath != null && ownsPlayer && Time.realtimeSinceStartupAsDouble > 3)
            {
                captureDone = true;
                CaptureWorld(Manager.LocalClient.PlayerObject.GetComponentInChildren<Camera>());
            }
            if (!automated) return;
            var observed = FindObjectsByType<NetworkToyPlayer>(FindObjectsSortMode.None);
            if (twoRoleTest)
            {
                if (!Manager.IsHost && ownsPlayer)
                {
                    var owner = Manager.LocalClient.PlayerObject.GetComponent<NetworkToyPlayer>();
                    double elapsed = Time.realtimeSinceStartupAsDouble;
                    if (roleTestStage == 0 && elapsed > 2) { owner.ChangeRoleRpc(3); roleTestStage++; }
                    else if (roleTestStage == 1 && elapsed > 4) { owner.ChangeRoleRpc(0); roleTestStage++; }
                    else if (roleTestStage == 2 && elapsed > 6) { owner.ChangeRoleRpc(2); roleTestStage++; }
                    else if (roleTestStage == 3 && elapsed > 8) { owner.ChangeRoleRpc(3); roleTestStage++; }
                    if (!clientReported && elapsed > 13 && owner.Role.Value == 3 && observed.Length == 2)
                    {
                        Debug.Log("NTS_TWO_CLIENT_PASS role switch and swap replicated");
                        clientReported = true;
                    }
                }
                if (Manager.IsHost && observed.Length == 2)
                {
                    if (!hostTestRoleRestored && Time.realtimeSinceStartupAsDouble > 14)
                    {
                        Manager.LocalClient.PlayerObject.GetComponent<NetworkToyPlayer>().ChangeRoleRpc(0);
                        hostTestRoleRestored = true;
                    }
                    if (!testImpulseApplied && Time.realtimeSinceStartupAsDouble > 15)
                    {
                        foreach (var player in observed) player.ApplySmokeImpulse();
                        testImpulseApplied = true;
                    }
                    if (Time.realtimeSinceStartupAsDouble > 22)
                    {
                        bool radio = false, ball = false;
                        foreach (var player in observed) { radio |= player.Role.Value == 0; ball |= player.Role.Value == 3; }
                        if (radio && ball)
                        {
                            Debug.Log("NTS_TWO_HOST_PASS unique roles after switches and occupied-role swap");
                            Application.Quit(0);
                            automated = false;
                        }
                    }
                }
                if (Time.realtimeSinceStartupAsDouble > deadline) Application.Quit(2);
                return;
            }
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

        void CaptureWorld(Camera camera)
        {
            var target = new RenderTexture(960, 540, 24);
            var texture = new Texture2D(960, 540, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
            texture.Apply();
            System.IO.File.WriteAllBytes(capturePath, texture.EncodeToPNG());
            int pink = 0;
            foreach (var pixel in texture.GetPixels32())
                if (pixel.r > 220 && pixel.b > 220 && pixel.g < 60) pink++;
            camera.targetTexture = null;
            RenderTexture.active = previous;
            target.Release();
            Destroy(target); Destroy(texture);
            Debug.Log($"NTS_RENDER_CAPTURE magentaPixels={pink}");
            Application.Quit(pink > 500 ? 3 : 0);
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
