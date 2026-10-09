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
        float yaw, pitch, serverYaw, verticalSpeed, lastInput, nextSend;
        bool running, automated;
        float tunedFrequency, signalTimer;

        Vector3 prePhysicsVelocity;
        bool wantsMouseLook;
        public int WallBounceCount { get; private set; }
        public float LastWallImpactSpeed { get; private set; }
        public float LastWallReboundSpeed { get; private set; }
        public int BodyPushCount { get; private set; }
        public Vector2 PhysicsTestInput { get; set; }
        bool physicsAutomated;
        public bool IsAutomated=>automated;
        public NetworkControlRoom RoomControl=>GetComponent<NetworkControlRoom>();
        Vector3 desiredMovementVelocity;
        float nextCane, footstepDistance;
        public bool ReceivesRadioSignal => Mathf.Abs(tunedFrequency - SignalFrequency.Value) < 2;
        public Camera OwnerCamera => view;
        public static float ViewHeight(int role) => role == 2 ? .65f : role == 3 ? .95f : 1.5f;
        public const float BallRadius = .65f;
        public readonly NetworkVariable<float> LookHeading = new NetworkVariable<float>(0);
        public readonly NetworkVariable<float> CaneReadyAt = new NetworkVariable<float>(0);
        public readonly NetworkVariable<bool> Captured = new NetworkVariable<bool>(false);
        public readonly NetworkVariable<bool> PermanentDeath = new NetworkVariable<bool>(false);
        public bool HasInfiniteHealth => Role.Value == (int)ToyRole.TennisBall;
        public int AcceptedJumps { get; private set; }
        bool jumpPending;
        public int TestObserverEvidence { get; private set; }
        [Rpc(SendTo.Server, RequireOwnership = true)]
        public void ReportJumpTestRpc(int evidence)
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-nts-jump-test") >= 0)
                TestObserverEvidence |= evidence & 31;
        }
        public int ReceivedNoiseEvents { get; private set; }
        public int ReceivedVoiceEvents { get; private set; }
        public int ReceivedFootstepEvents { get; private set; }

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
                var session = UnityEngine.Object.FindFirstObjectByType<PrototypeSession>();
                if(session.UseProceduralStore) SpawnPosition = ProceduralStore.Instance.Layout.Spawn(Role.Value);
                transform.position = SpawnPosition;
            }
            Role.OnValueChanged += OnRoleChanged;
            ApplyRole();
            if (IsOwner)
            {
                var cameraObject = new GameObject("Owner camera");
                cameraObject.transform.SetParent(transform, false);
                cameraObject.transform.localPosition = Vector3.up * ViewHeight(Role.Value);
                view = cameraObject.AddComponent<Camera>();
                view.nearClipPlane = .05f;
                view.backgroundColor = Color.black;
                cameraObject.AddComponent<EchoVision>().Player = this;
                cameraObject.AddComponent<AudioListener>();
                var light = cameraObject.AddComponent<Light>();
                light.type = LightType.Spot;
                light.range = 15;
                light.spotAngle = 65;
                light.intensity = 2;
                foreach (var renderer in visual.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
                automated = Array.IndexOf(Environment.GetCommandLineArgs(), "-nts-test") >= 0;
                physicsAutomated = Array.IndexOf(Environment.GetCommandLineArgs(), "-nts-physics-test") >= 0;
                automated |= physicsAutomated;
                if (Array.IndexOf(Environment.GetCommandLineArgs(), "-nts-voice-test") >= 0 || Array.IndexOf(Environment.GetCommandLineArgs(), "-nts-room-test") >= 0 || Array.IndexOf(Environment.GetCommandLineArgs(), "-nts-store-test") >= 0 || Array.IndexOf(Environment.GetCommandLineArgs(), "-nts-fixed-store-test") >= 0 || Array.IndexOf(Environment.GetCommandLineArgs(), "-nts-jump-test") >= 0)
                { automated = true; physicsAutomated = true; }
                wantsMouseLook = !automated && Array.IndexOf(Environment.GetCommandLineArgs(), "-nts-capture") < 0;
                if (wantsMouseLook && Application.isFocused) CaptureMouse();
            }
            Debug.Log($"NTS_ROLE owner={OwnerClientId} role={(ToyRole)Role.Value}");
        }

        protected override void OnNetworkPostSpawn()
        {
            if (IsServer)
            {
                controller.enabled=false;
                body.position=SpawnPosition;
                GetComponent<NetworkTransform>().Teleport(SpawnPosition, Quaternion.identity, Vector3.one);
                Physics.SyncTransforms();
                controller.enabled=Role.Value!=3;
            }
        }

        void OnRoleChanged(int previous, int current)
        {
            ApplyRole();
            if (view != null)
            {
                view.transform.localPosition = Vector3.up * ViewHeight(current);
                Debug.Log($"NTS_CAMERA_HEIGHT role={current} height={view.transform.localPosition.y}");
            }
            Debug.Log($"NTS_ROLE_CHANGED owner={OwnerClientId} role={(ToyRole)current}");
        }

        void ApplyRole()
        {
            footstepDistance = 0;
            bool ball = Role.Value == 3;
            ballCollider.radius = BallRadius;
            ballCollider.center = Vector3.up * BallRadius;
            controller.enabled = IsServer && !ball;
            ballCollider.enabled = IsServer && ball;
            body.isKinematic = !(IsServer && ball);
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.collisionDetectionMode = IsServer && ball ? CollisionDetectionMode.ContinuousDynamic :
                CollisionDetectionMode.Discrete;
            if (visual != null) Destroy(visual);

            visual = ToyVisuals.Create(Role.Value, transform);
            if (IsOwner) foreach (var renderer in visual.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
        }

        void Update()
        {
            if (!IsSpawned) return;
            if (visual != null) visual.transform.rotation = Quaternion.Euler(0, LookHeading.Value, 0);
            if (IsOwner)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                { wantsMouseLook = false; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
                var guiMouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
                bool clickingVoiceSettings = Cursor.lockState != CursorLockMode.Locked &&
                    new Rect(12, 200, 620, 192).Contains(guiMouse);
                bool boardOpen=RoomControl!=null && RoomControl.BoardFocus.Value;
                if (Input.GetMouseButtonDown(0) && !automated && !clickingVoiceSettings && !boardOpen)
                { wantsMouseLook = true; CaptureMouse(); }
                if (Application.isFocused && Cursor.lockState == CursorLockMode.Locked && !boardOpen)
                    ApplyLookDelta(new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")));
                view.transform.rotation = Quaternion.Euler(pitch, yaw, 0);
                if (Time.time >= nextSend)
                {
                    var input = physicsAutomated ? PhysicsTestInput : automated ? new Vector2(0, Time.realtimeSinceStartup < 8 ? 1 : 0) :
                        Application.isFocused ? new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")) : Vector2.zero;
                    InputRpc(input, yaw, !automated && Input.GetKey(KeyCode.LeftShift));
                    nextSend = Time.time + .05f;
                }
                if (!automated && Application.isFocused && Input.GetKeyDown(KeyCode.Space)) JumpRpc();
                if (!automated && Application.isFocused && Role.Value == 1 && Input.GetKeyDown(KeyCode.Q)) CaneRpc();
                if (Role.Value == 0)
                    tunedFrequency = Mathf.Clamp(tunedFrequency + Input.mouseScrollDelta.y * 2, 0, 100);
                for (int i = 0; i < 4; i++)
                    if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i))) ChangeRoleRpc(i);
            }
        }

        void SimulateServerMovement()
        {
            if (!IsServer || Role.Value == 3) return;
            if(RoomControl!=null && RoomControl.MovementBlocked) { movement=Vector2.zero;running=false;jumpPending=false;desiredMovementVelocity=Vector3.zero;return; }
            if (Captured.Value) { movement = Vector2.zero; running = false; jumpPending = false; }
            if (Role.Value == 0 && (signalTimer -= Time.fixedDeltaTime) <= 0)
            {
                SignalFrequency.Value = UnityEngine.Random.Range(0f, 100f);
                signalTimer = UnityEngine.Random.Range(8f, 15f);
            }
            if (Time.time - lastInput > .3f) { movement = Vector2.zero; running = false; }
            bool sprint = running && movement.sqrMagnitude > 0 && Stamina.Value > 0;
            Stamina.Value = Mathf.Clamp(Stamina.Value + (sprint ? -25 : 15) * Time.fixedDeltaTime, 0, 100);
            if (controller.isGrounded && verticalSpeed < 0) verticalSpeed = -2;
            if (jumpPending)
            {
                if (controller.isGrounded && !Captured.Value)
                {
                    verticalSpeed = Mathf.Sqrt(-2 * Physics.gravity.y * 1f); // Provisional 1m jump height.
                    AcceptedJumps++;
                }
                jumpPending = false;
            }
            verticalSpeed += Physics.gravity.y * Time.fixedDeltaTime;
            float speed = Role.Value == 2 ? 4 : Role.Value == 0 ? 3.4f : 2.8f;
            var delta = Quaternion.Euler(0, serverYaw, 0) * new Vector3(movement.x, 0, movement.y);
            desiredMovementVelocity = delta * speed * (sprint ? 1.6f : 1);
            var previousPosition = transform.position;
            controller.Move((desiredMovementVelocity + Vector3.up * verticalSpeed) * Time.fixedDeltaTime);
            var traveled = transform.position - previousPosition;
            traveled.y = 0;
            if (controller.isGrounded) footstepDistance += traveled.magnitude;
            if (footstepDistance >= 3f && controller.isGrounded)
            {
                footstepDistance -= 3f;
                NoiseRpc(transform.position + Vector3.up * .05f, (int)EchoSoundKind.Footstep, .22f);
            }
        }

        [Rpc(SendTo.Server, RequireOwnership = true)]
        public void JumpRpc()
        {
            if (Role.Value == 3 || Captured.Value || (RoomControl!=null && RoomControl.MovementBlocked) || !controller.enabled || !controller.isGrounded || verticalSpeed > 0) return;
            jumpPending = true;
        }

        // Monster AI calls this on the server. Instant death and revival eligibility are independent.
        public bool TryCapture(bool instantKill, bool preventsRevival = false)
        {
            if (!IsSpawned || !IsServer || Captured.Value || (HasInfiniteHealth && !instantKill)) return false;
            Captured.Value = true;
            if(RoomControl!=null) RoomControl.ReleaseTools();
            PermanentDeath.Value = preventsRevival;
            movement = Vector2.zero;
            running = false;
            jumpPending = false;
            return true;
        }

        [Rpc(SendTo.Server, RequireOwnership = true)]
        public void CaneRpc()
        {
            if (Captured.Value || Role.Value != 1 || Time.time < nextCane) return;
            nextCane = Time.time + 10;
            CaneReadyAt.Value = (float)NetworkManager.ServerTime.Time + 10;
            NoiseRpc(transform.position + Vector3.up * .05f, (int)EchoSoundKind.Cane, 1);
        }

        [Rpc(SendTo.ClientsAndHost)]
        public void NoiseRpc(Vector3 position, int kind, float strength)
        {
            EchoVision.Emit(position, (EchoSoundKind)kind, strength);
            ReceivedNoiseEvents++;
            if (kind == (int)EchoSoundKind.Voice) ReceivedVoiceEvents++;
            if (kind == (int)EchoSoundKind.Footstep) ReceivedFootstepEvents++;
        }

        [Rpc(SendTo.Server, RequireOwnership = true)]
        void InputRpc(Vector2 input, float heading, bool sprint)
        {
            if (!float.IsFinite(input.x) || !float.IsFinite(input.y) || !float.IsFinite(heading)) return;
            if (Captured.Value) { movement = Vector2.zero; running = false; return; }
            movement = Vector2.ClampMagnitude(input, 1);
            serverYaw = heading;
            LookHeading.Value = heading;
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
            if(RoomControl!=null) RoomControl.ReleaseTools();
            if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            movement = Vector2.zero;
            verticalSpeed = 0;
            running = false;
            jumpPending = false;
            Captured.Value = false; // Debug role switching starts a fresh test character.
            PermanentDeath.Value = false;
            Stamina.Value = 100;
            Role.Value = role;
        }

        public void ApplyLookDelta(Vector2 delta)
        {
            yaw += delta.x * 2;
            pitch = Mathf.Clamp(pitch - delta.y * 2, -85, 85);
            if (view != null) view.transform.rotation = Quaternion.Euler(pitch, yaw, 0);
        }

        public void MoveToSeat(Vector3 position,bool seated)
        {
            if(!IsServer) return;
            controller.enabled=false;body.position=position;
            GetComponent<NetworkTransform>().Teleport(position,Quaternion.identity,Vector3.one);
            verticalSpeed=0;movement=Vector2.zero;running=false;jumpPending=false;
            Physics.SyncTransforms();controller.enabled=!seated && Role.Value!=3;
        }
        [Rpc(SendTo.Owner)]
        public void FaceConsoleRpc() { yaw=180;pitch=0; }
        public void SetInteractionCursor(bool open)
        {
            wantsMouseLook=!open;
            if(open) { Cursor.lockState=CursorLockMode.None;Cursor.visible=true; }
            else if(Application.isFocused && !automated) CaptureMouse();
        }

        void CaptureMouse()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void OnApplicationFocus(bool focused)
        {
            if (!IsSpawned || !IsOwner) return;
            if (focused && wantsMouseLook && !automated) CaptureMouse();
            if (!focused) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        }

        void LateUpdate()
        {
            if (IsSpawned && IsOwner && view != null)
            {
                view.transform.position = transform.position + Vector3.up * ViewHeight(Role.Value);
                view.transform.rotation = Quaternion.Euler(pitch, yaw, 0);
            }
        }

        void FixedUpdate()
        {
            if (IsSpawned && IsServer && Role.Value == 3) prePhysicsVelocity = body.linearVelocity;
            else if (IsSpawned && IsServer) SimulateServerMovement();
        }

        void OnCollisionEnter(Collision collision)
        {
            if (!IsSpawned || !IsServer || Role.Value != 3) return;
            foreach (var contact in collision.contacts)
            {
                if (Mathf.Abs(contact.normal.y) > .5f) continue; // Floor uses its own material response.
                Vector3 normal = new Vector3(contact.normal.x, 0, contact.normal.z).normalized;
                float approach = -Vector3.Dot(prePhysicsVelocity, normal);
                if (approach <= .05f) continue;
                const float restitution = .8f; // Provisional tennis-ball test coefficient.
                Vector3 rebound = prePhysicsVelocity + (1 + restitution) * approach * normal;
                rebound.y = body.linearVelocity.y;
                body.linearVelocity = rebound;
                LastWallImpactSpeed = approach;
                LastWallReboundSpeed = Vector3.Dot(rebound, normal);
                WallBounceCount++;
                Debug.Log($"NTS_WALL_BOUNCE incoming={approach:F3} outgoing={LastWallReboundSpeed:F3}");
                break;
            }
        }

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (!IsServer) return;
            var ball = hit.collider.GetComponent<NetworkToyPlayer>();
            if (ball == null || ball.Role.Value != 3) return;
            var direction = hit.moveDirection;
            direction.y = 0;
            direction.Normalize();
            float playerSpeed = Mathf.Max(0, Vector3.Dot(desiredMovementVelocity, direction));
            float targetSpeed = playerSpeed * 1.15f;
            float existingSpeed = Vector3.Dot(ball.body.linearVelocity, direction);
            if (physicsAutomated)
                Debug.Log($"NTS_BODY_CONTACT direction={direction} desired={desiredMovementVelocity} existing={existingSpeed:F3} target={targetSpeed:F3}");
            if (targetSpeed <= existingSpeed) return;
            ball.body.AddForce(direction * (targetSpeed - existingSpeed), ForceMode.VelocityChange);
            ball.BodyPushCount++;
        }

        public override void OnNetworkDespawn()
        {
            Role.OnValueChanged -= OnRoleChanged;
            if (IsOwner) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
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
            GUI.Box(new Rect(12, 12, 620, 180), "Night Toy Store — NETWORK TEST");
            GUI.Label(new Rect(24, 40, 590, 24), $"Role: {(ToyRole)Role.Value} | Stamina: {Stamina.Value:0}" + (Captured.Value ? " | Captured" : ""));
            GUI.Label(new Rect(24, 64, 590, 24), "WASD / Shift run / walk into ball to push / Mouse look / Esc cursor");
            GUI.Label(new Rect(24, 88, 590, 24), "Space: jump (except ball). Grandmother Q: cane. Sound reveals waves.");
            if (Role.Value == 1)
                GUI.Label(new Rect(24, 112, 530, 24), $"Cane: {Mathf.Max(0, CaneReadyAt.Value - (float)NetworkManager.ServerTime.Time):F1}s until ready");
            if (HasInfiniteHealth) GUI.Label(new Rect(24, 112, 590, 24), "HP: infinite. Ordinary attacks cannot kill the ball; instant kill can.");
            if (Role.Value == 0)
                GUI.Label(new Rect(24, 112, 530, 24), $"Tune {tunedFrequency:0} / debug target {SignalFrequency.Value:0} / signal {Mathf.Abs(tunedFrequency - SignalFrequency.Value) < 2}");
            GUI.Label(new Rect(24, 140, 590, 24), "TEST: 1 Radio / 2 Grandmother / 3 Rabbit / 4 Ball (occupied roles swap)");
            if (Cursor.lockState != CursorLockMode.Locked)
                GUI.Label(new Rect(Screen.width / 2 - 150, Screen.height / 2, 300, 30), "Click inside this window to look around");
            foreach (var player in FindObjectsByType<NetworkToyPlayer>(FindObjectsSortMode.None))
            {
                if (player == this || view == null || Role.Value == 1) continue;
                var point = view.WorldToScreenPoint(player.transform.position + Vector3.up * .9f);
                if (point.z > 0)
                    GUI.Label(new Rect(point.x - 65, Screen.height - point.y, 180, 25),
                        $"{(ToyRole)player.Role.Value} ({Vector3.Distance(transform.position, player.transform.position):0.0}m)");
            }
        }
    }
}
