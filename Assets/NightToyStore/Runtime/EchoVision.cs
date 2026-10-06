using UnityEngine;

namespace NightToyStore
{
    public enum EchoSoundKind { Cane, Footstep, Voice }

    public sealed class EchoVision : MonoBehaviour
    {
        public const float VoiceRange = 3, FootstepRange = 2;
        const int Capacity = 24;
        static readonly Vector4[] pulses = new Vector4[Capacity];
        static readonly Vector4[] settings = new Vector4[Capacity];
        static int next, count;
        public NetworkToyPlayer Player;
        Camera camera;
        static EchoVision listener;

        void Awake() { camera = GetComponent<Camera>(); listener = this; }
        void OnDestroy() { if (listener == this) listener = null; }

        public static void Emit(Vector3 origin, EchoSoundKind kind, float strength = 1)
        {
            float range = kind == EchoSoundKind.Cane ? 24 : kind == EchoSoundKind.Voice ? VoiceRange : FootstepRange;
            if (listener != null && Vector3.Distance(listener.transform.position, origin) > range) return;
            pulses[next] = new Vector4(origin.x, origin.y, origin.z, Time.time);
            settings[next] = kind == EchoSoundKind.Cane ? new Vector4(14, 24, .65f, strength) :
                kind == EchoSoundKind.Footstep ? new Vector4(8, FootstepRange, .12f, strength) :
                new Vector4(10, VoiceRange, .3f, strength);
            next = (next + 1) % Capacity;
            count = Mathf.Min(count + 1, Capacity);
        }

        void OnPreRender()
        {
            bool active = Player != null && Player.Role.Value == (int)ToyRole.Grandmother;
            camera.clearFlags = active ? CameraClearFlags.SolidColor : CameraClearFlags.Skybox;
            camera.backgroundColor = Color.black;
            Shader.SetGlobalFloat("_EchoMode", active ? 1 : 0);
            Shader.SetGlobalFloat("_EchoNow", Time.time);
            Shader.SetGlobalInt("_EchoCount", count);
            Shader.SetGlobalVectorArray("_EchoPulses", pulses);
            Shader.SetGlobalVectorArray("_EchoSettings", settings);
        }

        void OnPostRender() { Shader.SetGlobalFloat("_EchoMode", 0); }
        void OnDisable() { Shader.SetGlobalFloat("_EchoMode", 0); }
    }
}
