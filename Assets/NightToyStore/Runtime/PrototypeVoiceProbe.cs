using System.Collections;
using UnityEngine;

namespace NightToyStore
{
    // Synthetic audio only; this explicit integration test never opens the microphone.
    public sealed class PrototypeVoiceProbe : MonoBehaviour
    {
        IEnumerator Start()
        {
            var manager = GetComponent<PrototypeSession>().Manager;
            float deadline = Time.realtimeSinceStartup + 25;
            while (FindObjectsByType<NetworkToyPlayer>(FindObjectsSortMode.None).Length != 2)
            {
                if (Time.realtimeSinceStartup > deadline) { Fail("connection timeout"); yield break; }
                yield return null;
            }
            yield return new WaitForSeconds(.5f);
            var owner = manager.LocalClient.PlayerObject.GetComponent<NetworkToyPlayer>();
            var voice = owner.GetComponent<NetworkVoice>();
            var samples = new float[NetworkVoice.FrameSamples];
            for (int i = 0; i < samples.Length; i++) samples[i] = Mathf.Sin(i * .17f) * .2f;
            byte[] frame = NetworkVoice.Encode(samples);
            var decoded = NetworkVoice.Decode(frame);
            for (int i = 0; i < samples.Length; i++)
                if (Mathf.Abs(samples[i] - decoded[i]) > .00005f) { Fail("PCM round trip"); yield break; }
            if (!manager.IsHost)
            {
                uint sequence = 0;
                while (Time.realtimeSinceStartup < deadline)
                {
                    voice.UploadRpc(frame, ++sequence);
                    yield return new WaitForSeconds(.02f);
                }
                yield break;
            }
            NetworkToyPlayer remote = null;
            foreach (var actor in FindObjectsByType<NetworkToyPlayer>(FindObjectsSortMode.None))
                if (!actor.IsOwner) remote = actor;
            var remoteVoice = remote.GetComponent<NetworkVoice>();
            yield return new WaitForSeconds(2);
            if (remoteVoice.ReceivedFrames < 20 || remote.ReceivedVoiceEvents < 3)
            { Fail("remote audio or voice waves missing"); yield break; }
            remote.Role.Value = 2;
            yield return new WaitForSeconds(.5f);
            int frames = remoteVoice.RelayedFrames, waves = remote.ReceivedVoiceEvents;
            yield return new WaitForSeconds(1);
            if (remoteVoice.RejectedRabbitFrames == 0 || remoteVoice.RelayedFrames != frames || remote.ReceivedVoiceEvents != waves)
            { Fail("rabbit transmitted voice"); yield break; }
            int noise = owner.ReceivedNoiseEvents;
            owner.CaneRpc();
            yield return new WaitForSeconds(.1f);
            if (owner.ReceivedNoiseEvents != noise) { Fail("radio used cane"); yield break; }
            owner.Role.Value = 1;
            owner.CaneRpc();
            yield return new WaitForSeconds(.1f);
            if (owner.ReceivedNoiseEvents <= noise) { Fail("grandmother cane missing"); yield break; }
            Debug.Log("NTS_VOICE_PROBE_PASS PCM fidelity, remote voice, voice waves, rabbit rejection, cane role restriction");
            Application.Quit(0);
        }
        static void Fail(string reason) { Debug.LogError("NTS_VOICE_PROBE_FAIL " + reason); Application.Quit(2); }
    }
}
