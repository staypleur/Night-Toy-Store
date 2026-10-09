using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace NightToyStore
{
    public enum VoiceInputMode { Automatic, HoldToTalk }

    public sealed class NetworkVoice : NetworkBehaviour
    {
        public const int SampleRate = 16000, FrameSamples = 320, FrameBytes = FrameSamples * 2;
        public VoiceInputMode Mode { get; private set; } = VoiceInputMode.HoldToTalk;
        public bool Muted { get; private set; }
        public string Status { get; private set; } = "Microphone starting";
        public float InputLevel { get; private set; }
        public float MicGain { get; private set; } = 1;
        public float ListeningVolume { get; private set; } = 1;
        public float DetectionThreshold { get; private set; } = .01f;
        public static float Amplify(float sample, float gain) => Mathf.Clamp(sample * gain, -1, 1);
        public int RelayedFrames { get; private set; }
        public int ReceivedFrames { get; private set; }
        public int RejectedRabbitFrames { get; private set; }
        NetworkToyPlayer player;
        AudioSource output;
        AudioClip capture, playback;
        string device;
        int deviceIndex = -1, readPosition, captureFrameSamples;
        float[] microphoneFrame;
        readonly float[] mono = new float[FrameSamples];
        readonly Queue<float> buffered = new Queue<float>();
        readonly object audioLock = new object();
        bool ready, automated, receivedSequence;
        uint sequence, lastSequence;
        float lastLoudFrame = -10, uploadCreditTime, uploadCredits = 5, voiceUntil;

        void Awake()
        {
            player = GetComponent<NetworkToyPlayer>();
            output = gameObject.AddComponent<AudioSource>();
            output.playOnAwake = false;
            output.loop = true;
            output.spatialBlend = 1;
            output.minDistance = 1;
            output.maxDistance = EchoVision.VoiceRange;
            output.rolloffMode = AudioRolloffMode.Linear;
        }

        public override void OnNetworkSpawn()
        {
            string[] args = Environment.GetCommandLineArgs();
            automated = Array.IndexOf(args, "-nts-test") >= 0 ||
                Array.IndexOf(args, "-nts-physics-test") >= 0 ||
                Array.IndexOf(args, "-nts-voice-test") >= 0 || Array.IndexOf(args, "-nts-jump-test") >= 0 || Array.IndexOf(args, "-nts-store-test") >= 0 || Array.IndexOf(args, "-nts-fixed-store-test") >= 0 || Array.IndexOf(args, "-nts-capture") >= 0;
            player.Role.OnValueChanged += RoleChanged;
            if (IsOwner)
            {
                MicGain = Mathf.Clamp(PlayerPrefs.GetFloat("PrototypeMicGain", 1), 1, 10);
                ListeningVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("PrototypeVoiceVolume", 1));
                DetectionThreshold = Mathf.Clamp(PlayerPrefs.GetFloat("PrototypeVoiceThreshold", .01f), .001f, .05f);
                Mode = (VoiceInputMode)Mathf.Clamp(PlayerPrefs.GetInt("PrototypeVoiceMode", 1), 0, 1);
                if (!automated && player.Role.Value != 2) StartCapture();
            }
            else
            {
                playback = AudioClip.Create("Remote voice stream", SampleRate, 1, SampleRate, true, ReadAudio);
                output.clip = playback;
            }
        }

        void RoleChanged(int previous, int current)
        {
            lock (audioLock) { buffered.Clear(); ready = false; }
            if (!IsOwner) return;
            StopCapture();
            if (!automated && !Muted && current != 2) StartCapture();
            if (current == 2) Status = "Rabbit cannot speak";
        }

        void StartCapture()
        {
            if (Microphone.devices.Length == 0) { Status = "No microphone found"; return; }
            try
            {
                capture = Microphone.Start(device, true, 1, SampleRate);
                if (capture == null) { Status = "Microphone could not start"; return; }
                captureFrameSamples = Mathf.Max(1, capture.frequency / 50);
                microphoneFrame = new float[captureFrameSamples * capture.channels];
                readPosition = 0;
                Status = "Listening: " + (device ?? "system default");
            }
            catch (Exception exception) { Status = "Microphone error: " + exception.Message; }
        }

        void StopCapture()
        {
            if (capture != null) { Microphone.End(device); Destroy(capture); capture = null; }
            InputLevel = 0;
        }

        void Update()
        {
            if (!IsSpawned) return;
            if (IsOwner)
            {
                if (!automated && Application.isFocused && Input.GetKeyDown(KeyCode.M)) ToggleMute();
                if (capture == null || Muted || player.Role.Value == 2) return;
                int position = Microphone.GetPosition(device);
                if (position < 0) { Status = "Microphone unavailable"; return; }
                int available = (position - readPosition + capture.samples) % capture.samples;
                int processed = 0;
                while (available >= captureFrameSamples && processed++ < 5)
                {
                    capture.GetData(microphoneFrame, readPosition);
                    for (int i = 0; i < FrameSamples; i++)
                    {
                        int source = Mathf.Min(captureFrameSamples - 1, i * captureFrameSamples / FrameSamples);
                        float value = 0;
                        for (int channel = 0; channel < capture.channels; channel++)
                            value += microphoneFrame[source * capture.channels + channel];
                        mono[i] = Amplify(value / capture.channels, MicGain);
                    }
                    InputLevel = Rms(mono);
                    if (InputLevel > DetectionThreshold) voiceUntil = Time.unscaledTime + .15f;
                    bool send = Mode == VoiceInputMode.Automatic ? Time.unscaledTime < voiceUntil :
                        Application.isFocused && Input.GetKey(KeyCode.V);
                    if (send) UploadRpc(Encode(mono), ++sequence);
                    readPosition = (readPosition + captureFrameSamples) % capture.samples;
                    available -= captureFrameSamples;
                }
            }
            else
            {
                var manager = NetworkManager;
                if (manager.LocalClient == null || manager.LocalClient.PlayerObject == null) return;
                var listener = manager.LocalClient.PlayerObject.GetComponent<NetworkToyPlayer>();
                bool radio = listener.Role.Value == 0;
                output.spatialBlend = radio ? 0 : 1;
                output.volume = player.Role.Value == 2 || (radio && !listener.ReceivesRadioSignal) ? 0 : listener.GetComponent<NetworkVoice>().ListeningVolume;
            }
        }

        public static byte[] Encode(float[] samples)
        {
            var bytes = new byte[samples.Length * 2];
            for (int i = 0; i < samples.Length; i++)
            {
                short value = (short)(Mathf.Clamp(samples[i], -1, 1) * 32767);
                bytes[i * 2] = (byte)value;
                bytes[i * 2 + 1] = (byte)(value >> 8);
            }
            return bytes;
        }

        public static float[] Decode(byte[] bytes)
        {
            var samples = new float[bytes.Length / 2];
            for (int i = 0; i < samples.Length; i++)
                samples[i] = (short)(bytes[i * 2] | bytes[i * 2 + 1] << 8) / 32768f;
            return samples;
        }

        public static float Rms(float[] samples)
        {
            float sum = 0;
            foreach (float value in samples) sum += value * value;
            return Mathf.Sqrt(sum / Mathf.Max(1, samples.Length));
        }

        [Rpc(SendTo.Server, RequireOwnership = true, Delivery = RpcDelivery.Unreliable)]
        public void UploadRpc(byte[] frame, uint frameSequence)
        {
            if (player.Role.Value == 2) { RejectedRabbitFrames++; return; }
            if (frame == null || frame.Length != FrameBytes) return;
            uploadCredits = Mathf.Min(5, uploadCredits + (Time.unscaledTime - uploadCreditTime) * 50);
            uploadCreditTime = Time.unscaledTime;
            if (uploadCredits < 1) return;
            uploadCredits--;
            float level = Rms(Decode(frame));
            RelayedFrames++;
            RelayRpc(frame, frameSequence);
            if (level > .001f)
            {
                bool beginning = Time.unscaledTime - lastLoudFrame > .35f;
                lastLoudFrame = Time.unscaledTime;
                if (!beginning) return;
                float mouthHeight = player.Role.Value == 3 ? NetworkToyPlayer.ViewHeight(3) : 1.2f;
                player.NoiseRpc(transform.position + Vector3.up * mouthHeight,
                    (int)EchoSoundKind.Voice, Mathf.Clamp(level * 12, .3f, 1));
            }
        }

        [Rpc(SendTo.ClientsAndHost, Delivery = RpcDelivery.Unreliable)]
        void RelayRpc(byte[] frame, uint frameSequence)
        {
            if (IsOwner || frame == null || frame.Length != FrameBytes) return;
            if (receivedSequence && unchecked((int)(frameSequence - lastSequence)) <= 0) return;
            receivedSequence = true;
            lastSequence = frameSequence;
            var samples = Decode(frame);
            lock (audioLock)
            {
                if (buffered.Count > SampleRate) { buffered.Clear(); ready = false; }
                foreach (float sample in samples) buffered.Enqueue(sample);
            }
            ReceivedFrames++;
            if (!output.isPlaying && playback != null) output.Play();
        }

        void ReadAudio(float[] data)
        {
            lock (audioLock)
            {
                if (!ready && buffered.Count >= FrameSamples * 3) ready = true;
                for (int i = 0; i < data.Length; i++)
                {
                    data[i] = ready && buffered.Count > 0 ? buffered.Dequeue() : 0;
                    if (buffered.Count == 0) ready = false;
                }
            }
        }

        public void ToggleMute()
        {
            Muted = !Muted;
            if (Muted) { StopCapture(); Status = "Microphone muted"; }
            else if (player.Role.Value != 2 && !automated) StartCapture();
        }

        void SetMode(VoiceInputMode mode)
        {
            Mode = mode;
            PlayerPrefs.SetInt("PrototypeVoiceMode", (int)mode);
            PlayerPrefs.Save();
        }

        public override void OnNetworkDespawn()
        {
            player.Role.OnValueChanged -= RoleChanged;
            if (IsOwner) StopCapture();
            output.Stop();
            lock (audioLock) buffered.Clear();
            if (playback != null) Destroy(playback);
        }

        void OnGUI()
        {
            if (!IsSpawned || !IsOwner) return;
            GUI.Box(new Rect(12, 200, 620, 192), "Voice settings — Esc releases cursor / M mutes microphone");
            GUI.enabled = Cursor.lockState != CursorLockMode.Locked;
            if (GUI.Button(new Rect(24, 227, 130, 25), "Automatic voice")) SetMode(VoiceInputMode.Automatic);
            if (GUI.Button(new Rect(164, 227, 130, 25), "Hold V to talk")) SetMode(VoiceInputMode.HoldToTalk);
            if (GUI.Button(new Rect(304, 227, 110, 25), Muted ? "Unmute mic" : "Mute mic")) ToggleMute();
            if (GUI.Button(new Rect(424, 227, 190, 25), "Change microphone"))
            {
                StopCapture();
                var devices = Microphone.devices;
                deviceIndex = devices.Length == 0 ? -1 : (deviceIndex + 1) % devices.Length;
                device = deviceIndex < 0 ? null : devices[deviceIndex];
                if (!Muted && player.Role.Value != 2 && !automated) StartCapture();
            }
            float gain = GUI.HorizontalSlider(new Rect(200, 298, 300, 20), MicGain, 1, 10);
            float volume = GUI.HorizontalSlider(new Rect(200, 326, 300, 20), ListeningVolume, 0, 1);
            float threshold = GUI.HorizontalSlider(new Rect(200, 354, 300, 20), DetectionThreshold, .001f, .05f);
            GUI.Label(new Rect(24, 294, 180, 24), $"Mic gain: {gain:F1}x");
            GUI.Label(new Rect(24, 322, 180, 24), $"Voice volume: {volume * 100:F0}%");
            GUI.Label(new Rect(24, 350, 180, 24), $"Detection: {threshold:F3}");
            if (gain != MicGain || volume != ListeningVolume || threshold != DetectionThreshold)
            {
                MicGain = gain; ListeningVolume = volume; DetectionThreshold = threshold;
                PlayerPrefs.SetFloat("PrototypeMicGain", gain);
                PlayerPrefs.SetFloat("PrototypeVoiceVolume", volume);
                PlayerPrefs.SetFloat("PrototypeVoiceThreshold", threshold);
            }
            GUI.enabled = true;
            string state = player.Role.Value == 2 ? "Rabbit cannot speak" : Muted ? "Muted" : Status;
            GUI.Label(new Rect(24, 262, 590, 25), $"{Mode} | level {InputLevel:F3} | {state}");
        }
    }
}
