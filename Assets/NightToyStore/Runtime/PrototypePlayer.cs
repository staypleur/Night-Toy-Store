using UnityEngine;

namespace NightToyStore
{
    public enum ToyRole { Radio, Grandmother, Rabbit, TennisBall }

    // Local mechanics harness. Values below are provisional, not approved balance.
    public sealed class PrototypePlayer : MonoBehaviour
    {
        public ToyRole Role { get; private set; }
        public float Stamina { get; private set; } = 100f;
        public float TunedFrequency { get; private set; }
        public float SignalFrequency { get; private set; }
        public bool ReceivesSignal => Mathf.Abs(TunedFrequency - SignalFrequency) < 2f;
        public float PulseUntil { get; private set; }
        public Camera View { get; private set; }
        CharacterController controller;
        float pitch, verticalSpeed, signalTimer;

        void Awake()
        {
            controller = gameObject.AddComponent<CharacterController>();
            controller.height = 1.6f;
            controller.minMoveDistance = 0;
            controller.center = Vector3.up * .8f;
            var cameraObject = new GameObject("Player view");
            cameraObject.transform.SetParent(transform, false);
            View = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            View.nearClipPlane = .05f;
            var light = cameraObject.AddComponent<Light>();
            light.type = LightType.Spot;
            light.range = 15f;
            light.spotAngle = 65f;
            light.intensity = 2f;
            SetRole(ToyRole.Radio);
            Cursor.lockState = CursorLockMode.Locked;
        }

        public void SetRole(ToyRole role)
        {
            Role = role;
            View.transform.localPosition = Vector3.up * (role == ToyRole.Rabbit ? .65f : 1.5f);
            Stamina = 100f;
            PulseUntil = 0f;
        }

        void Update()
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                transform.Rotate(0f, Input.GetAxis("Mouse X") * 2f, 0f);
                pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * 2f, -85f, 85f);
                View.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
            if (Input.GetKeyDown(KeyCode.Escape)) Cursor.lockState = CursorLockMode.None;
            if (Input.GetMouseButtonDown(0)) Cursor.lockState = CursorLockMode.Locked;
            for (int i = 0; i < 4; i++)
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i))) SetRole((ToyRole)i);

            var direction = Vector3.ClampMagnitude(new Vector3(Input.GetAxisRaw("Horizontal"), 0f,
                Input.GetAxisRaw("Vertical")), 1f);
            bool running = Role != ToyRole.TennisBall && direction.sqrMagnitude > 0f &&
                Input.GetKey(KeyCode.LeftShift) && Stamina > 0f;
            Stamina = Mathf.Clamp(Stamina + (running ? -25f : 15f) * Time.deltaTime, 0f, 100f);
            float speed = Role == ToyRole.Rabbit ? 4f : Role == ToyRole.Radio ? 3.4f : 2.8f;
            if (controller.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
            verticalSpeed += Physics.gravity.y * Time.deltaTime;
            Vector3 movement = Role == ToyRole.TennisBall ? Vector3.zero :
                transform.TransformDirection(direction) * speed * (running ? 1.6f : 1f);
            controller.Move((movement + Vector3.up * verticalSpeed) * Time.deltaTime);

            if (Role == ToyRole.Grandmother && Input.GetKeyDown(KeyCode.Space))
                PulseUntil = Time.time + 1.5f;
            signalTimer -= Time.deltaTime;
            if (signalTimer <= 0f)
            {
                SignalFrequency = Random.Range(0f, 100f);
                signalTimer = Random.Range(8f, 15f);
            }
            TunedFrequency = Mathf.Clamp(TunedFrequency + Input.mouseScrollDelta.y * 2f, 0f, 100f);
        }

        void OnGUI()
        {
            if (Role == ToyRole.Grandmother && Time.time > PulseUntil)
            {
                GUI.color = Color.black;
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
            GUI.Box(new Rect(12, 12, 540, 130), "Night Toy Store — LOCAL MECHANICS TEST");
            GUI.Label(new Rect(24, 40, 510, 24), $"Role: {Role} | Stamina: {Stamina:0}");
            GUI.Label(new Rect(24, 64, 510, 24), "WASD move / Shift run / 1–4 role / Esc release mouse");
            GUI.Label(new Rect(24, 88, 510, 24), "Grandmother: Space pulse | Radio: mouse wheel tune");
            GUI.Label(new Rect(24, 112, 510, 24), Role == ToyRole.Radio ?
                $"Frequency {TunedFrequency:0} / debug target {SignalFrequency:0} / signal {ReceivesSignal}" :
                "Temporary models and balance. Multiplayer and voice are not implemented yet.");
        }
    }
}
