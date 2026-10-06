using System;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace NightToyStore
{
    [RequireComponent(typeof(NetworkObject), typeof(NetworkTransform))]
    public sealed class NetworkToyPlayer : NetworkBehaviour
    {
        public readonly NetworkVariable<int> Role = new NetworkVariable<int>(0);
        public readonly NetworkVariable<float> Stamina = new NetworkVariable<float>(100);
        public readonly NetworkVariable<float> SignalFrequency = new NetworkVariable<float>(0);
        public Vector3 SpawnPosition { get; private set; }
        CharacterController controller;
        Rigidbody body;
        SphereCollider ballCollider;
        Camera view;
        GameObject visual;
        Vector2 movement;
        float yaw, pitch, serverYaw, verticalSpeed, lastInput, nextSend, pulseUntil;
        bool running, automated;
        float tunedFrequency, signalTimer, nextPush;
        Material visualMaterial;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            body = GetComponent<Rigidbody>();
            ballCollider = GetComponent<SphereCollider>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                var used = new bool[4];
                foreach (var player in FindObjectsByType<NetworkToyPlayer>(FindObjectsSortMode.None))
                    if (player != this && player.IsSpawned) used[player.Role.Value] = true;
                int slot = Array.FindIndex(used, value => !value);
                Role.Value = Mathf.Max(0, slot);
                SpawnPosition = new Vector3(-4 + Role.Value * 2.5f, .4f, -4);
                transform.position = SpawnPosition;
            }
            Role.OnValueChanged += OnRoleChanged;
            ApplyRole();
            if (IsOwner)
            {
                var cameraObject = new GameObject("Owner camera");
                cameraObject.transform.SetParent(transform, false);
                cameraObject.transform.localPosition = Vector3.up * (Role.Value == 2 ? .65f : 1.5f);
                view = cameraObject.AddComponent<Camera>();
                view.nearClipPlane = .05f;
                view.backgroundColor = Color.black;
                cameraObject.AddComponent<AudioListener>();
                var light = cameraObject.AddComponent<Light>();
                light.type = LightType.Spot;
                light.range = 15;
                light.spotAngle = 65;
                light.intensity = 2;
                visual.GetComponent<Renderer>().enabled = false;
                automated = Array.IndexOf(Environment.GetCommandLineArgs(), "-nts-test") >= 0;
            }
            Debug.Log($"NTS_ROLE owner={OwnerClientId} role={(ToyRole)Role.Value}");
        }

        void OnRoleChanged(int previous, int current)
        {
            ApplyRole();
            if (view != null)
            {
                view.transform.localPosition = Vector3.up * (current == 2 ? .65f : 1.5f);
                Debug.Log($"NTS_CAMERA_HEIGHT role={current} height={view.transform.localPosition.y}");
            }
            pulseUntil = 0;
            Debug.Log($"NTS_ROLE_CHANGED owner={OwnerClientId} role={(ToyRole)current}");
        }

        void ApplyRole()
        {
            bool ball = Role.Value == 3;
            controller.enabled = IsServer && !ball;
            ballCollider.enabled = IsServer && ball;
            body.isKinematic = !(IsServer && ball);
            body.constraints = RigidbodyConstraints.FreezeRotation;
            if (visual != null) Destroy(visual);
            if (visualMaterial != null) Destroy(visualMaterial);
            visual = GameObject.CreatePrimitive(ball ? PrimitiveType.Sphere : PrimitiveType.Capsule);
            Destroy(visual.GetComponent<Collider>());
            visual.transform.SetParent(transform, false);
            visual.transform.localPosition = Vector3.up * (ball ? .3f : Role.Value == 2 ? .5f : .8f);
            visual.transform.localScale = ball ? Vector3.one * .6f :
                new Vector3(.65f, Role.Value == 2 ? .5f : .8f, .65f);
            var colors = new[] { Color.cyan, Color.gray, Color.magenta, Color.yellow };
            colors[2] = new Color(.85f, .45f, .22f);
            visualMaterial = PrototypeMaterials.Create(colors[Role.Value]);
            visual.GetComponent<Renderer>().sharedMaterial = visualMaterial;
            if (IsOwner) visual.GetComponent<Renderer>().enabled = false;
        }

        void Update()
        {
            if (!IsSpawned) return;
            if (IsOwner)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) Cursor.lockState = CursorLockMode.None;
                if (Input.GetMouseButtonDown(0) && !automated) Cursor.lockState = CursorLockMode.Locked;
                if (Cursor.lockState == CursorLockMode.Locked)
                {
                    yaw += Input.GetAxis("Mouse X") * 2f;
                    pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * 2f, -85, 85);
                }
                view.transform.rotation = Quaternion.Euler(pitch, yaw, 0);
                if (Time.time >= nextSend)
                {
                    var input = automated ? new Vector2(0, Time.realtimeSinceStartup < 8 ? 1 : 0) :
                        new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
                    InputRpc(input, yaw, !automated && Input.GetKey(KeyCode.LeftShift));
                    nextSend = Time.time + .05f;
                }
                if (Role.Value == 1 && Input.GetKeyDown(KeyCode.Space)) pulseUntil = Time.time + 1.5f;
                if (Role.Value == 0)
                    tunedFrequency = Mathf.Clamp(tunedFrequency + Input.mouseScrollDelta.y * 2, 0, 100);
                if (Input.GetKeyDown(KeyCode.E) && Role.Value != 3) PushBallRpc();
                for (int i = 0; i < 4; i++)
                    if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i))) ChangeRoleRpc(i);
            }
            if (!IsServer || Role.Value == 3) return;
            if (Role.Value == 0 && (signalTimer -= Time.deltaTime) <= 0)
            {
                SignalFrequency.Value = UnityEngine.Random.Range(0f, 100f);
                signalTimer = UnityEngine.Random.Range(8f, 15f);
            }
            if (Time.time - lastInput > .3f) { movement = Vector2.zero; running = false; }
            bool sprint = running && movement.sqrMagnitude > 0 && Stamina.Value > 0;
            Stamina.Value = Mathf.Clamp(Stamina.Value + (sprint ? -25 : 15) * Time.deltaTime, 0, 100);
            if (controller.isGrounded && verticalSpeed < 0) verticalSpeed = -2;
            verticalSpeed += Physics.gravity.y * Time.deltaTime;
            float speed = Role.Value == 2 ? 4 : Role.Value == 0 ? 3.4f : 2.8f;
            var delta = Quaternion.Euler(0, serverYaw, 0) * new Vector3(movement.x, 0, movement.y);
            controller.Move((delta * speed * (sprint ? 1.6f : 1) + Vector3.up * verticalSpeed) * Time.deltaTime);
        }

        [Rpc(SendTo.Server, RequireOwnership = true)]
        void InputRpc(Vector2 input, float heading, bool sprint)
        {
            if (!float.IsFinite(input.x) || !float.IsFinite(input.y) || !float.IsFinite(heading)) return;
            movement = Vector2.ClampMagnitude(input, 1);
            serverYaw = heading;
            running = sprint;
            lastInput = Time.time;
        }

        [Rpc(SendTo.Server, RequireOwnership = true)]
        public void ChangeRoleRpc(int requestedRole)
        {
            if (requestedRole < 0 || requestedRole > 3 || requestedRole == Role.Value) return;
            int previous = Role.Value;
            foreach (var player in FindObjectsByType<NetworkToyPlayer>(FindObjectsSortMode.None))
                if (player != this && player.IsSpawned && player.Role.Value == requestedRole)
                    player.ResetRole(previous);
            ResetRole(requestedRole);
        }

        void ResetRole(int role)
        {
            if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            movement = Vector2.zero;
            verticalSpeed = 0;
            running = false;
            Stamina.Value = 100;
            Role.Value = role;
        }

        [Rpc(SendTo.Server, RequireOwnership = true)]
        void PushBallRpc()
        {
            if (Role.Value == 3 || Time.time < nextPush) return;
            nextPush = Time.time + .3f;
            foreach (var player in FindObjectsByType<NetworkToyPlayer>(FindObjectsSortMode.None))
            {
                if (player.Role.Value != 3 || Vector3.Distance(transform.position, player.transform.position) > 2) continue;
                Vector3 direction = player.transform.position - transform.position;
                direction.y = 0;
                if (direction.sqrMagnitude < .01f) direction = Quaternion.Euler(0, serverYaw, 0) * Vector3.forward;
                player.body.AddForce(direction.normalized * 3f, ForceMode.Impulse);
            }
        }

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (!IsServer) return;
            var ball = hit.collider.GetComponent<NetworkToyPlayer>();
            if (ball == null || ball.Role.Value != 3) return;
            var direction = hit.moveDirection;
            direction.y = 0;
            ball.body.AddForce(direction * 12f, ForceMode.Force);
        }

        public override void OnNetworkDespawn()
        {
            Role.OnValueChanged -= OnRoleChanged;
            if (IsOwner) Cursor.lockState = CursorLockMode.None;
        }

        public void ApplySmokeImpulse()
        {
            if (IsServer && Role.Value == 3 &&
                Array.IndexOf(Environment.GetCommandLineArgs(), "-nts-test") >= 0)
                body.AddForce(Vector3.right * 3, ForceMode.Impulse);
        }

        void OnGUI()
        {
            if (!IsSpawned || !IsOwner) return;
            if (Role.Value == 1 && Time.time > pulseUntil)
            {
                GUI.color = Color.black;
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
            GUI.Box(new Rect(12, 12, 620, 180), "Night Toy Store — NETWORK TEST");
            GUI.Label(new Rect(24, 40, 530, 24), $"Role: {(ToyRole)Role.Value} | Stamina: {Stamina.Value:0}");
            GUI.Label(new Rect(24, 64, 530, 24), "WASD / Shift run / E roll nearby ball / Space temporary pulse / Esc cursor");
            GUI.Label(new Rect(24, 88, 530, 24), "Connection-order roles. Temporary models. Voice and real echolocation pending.");
            if (Role.Value == 0)
                GUI.Label(new Rect(24, 112, 530, 24), $"Tune {tunedFrequency:0} / debug target {SignalFrequency.Value:0} / signal {Mathf.Abs(tunedFrequency - SignalFrequency.Value) < 2}");
            GUI.Label(new Rect(24, 140, 590, 24), "TEST: 1 Radio / 2 Grandmother / 3 Rabbit / 4 Ball (occupied roles swap)");
            foreach (var player in FindObjectsByType<NetworkToyPlayer>(FindObjectsSortMode.None))
            {
                if (player == this || view == null || Role.Value == 1 && Time.time > pulseUntil) continue;
                var point = view.WorldToScreenPoint(player.transform.position + Vector3.up * .9f);
                if (point.z > 0)
                    GUI.Label(new Rect(point.x - 65, Screen.height - point.y, 180, 25),
                        $"{(ToyRole)player.Role.Value} ({Vector3.Distance(transform.position, player.transform.position):0.0}m)");
            }
        }
    }
}
