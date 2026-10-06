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
            if (remoteVoice.ReceivedFrames < 20 || remote.ReceivedVoiceEvents < 1)
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
            int selfWaves = owner.ReceivedVoiceEvents, emitted = EchoVision.EmittedCount;
            voice.UploadRpc(frame, 1);
            yield return new WaitForSeconds(.1f);
            if (owner.ReceivedVoiceEvents <= selfWaves || EchoVision.EmittedCount <= emitted)
            { Fail("grandmother own voice did not produce visible pulse"); yield break; }
            noise = owner.ReceivedNoiseEvents;
            owner.CaneRpc();
            yield return new WaitForSeconds(.1f);
            if (owner.ReceivedNoiseEvents <= noise) { Fail("grandmother cane missing"); yield break; }
            int steps = owner.ReceivedFootstepEvents;
            var walkingController = owner.GetComponent<CharacterController>();
            walkingController.enabled = false;
            owner.transform.position = new Vector3(-7, .08f, -7);
            walkingController.enabled = true;
            Vector3 walkingStart = owner.transform.position;
            owner.PhysicsTestInput = Vector2.up;
            float walkDeadline = Time.realtimeSinceStartup + 4;
            while (owner.ReceivedFootstepEvents == steps && Time.realtimeSinceStartup < walkDeadline)
                yield return null;
            owner.PhysicsTestInput = Vector2.zero;
            Vector3 walked = owner.transform.position - walkingStart;
            walked.y = 0;
            if (owner.ReceivedFootstepEvents != steps + 1 || walked.magnitude < 2.9f || walked.magnitude > 3.15f)
            { Fail($"walking pulse threshold count={owner.ReceivedFootstepEvents-steps} distance={walked.magnitude} start={walkingStart} end={owner.transform.position}"); yield break; }
            noise = owner.ReceivedNoiseEvents;
            owner.CaneRpc();
            if (owner.ReceivedNoiseEvents != noise) { Fail("cane cooldown bypass"); yield break; }
            yield return new WaitForSeconds(10);
            owner.CaneRpc();
            if (owner.ReceivedNoiseEvents <= noise) { Fail("cane did not recharge"); yield break; }
            owner.Role.Value = 3;
            owner.transform.rotation = Quaternion.Euler(70, 40, 30);
            yield return null;
            yield return null;
            if (Quaternion.Angle(owner.OwnerCamera.transform.rotation, Quaternion.identity) > .1f ||
                Mathf.Abs(owner.OwnerCamera.transform.position.y - owner.transform.position.y - .95f) > .01f)
            { Fail("ball camera rolled or wrong height"); yield break; }
            if (Mathf.Abs(owner.GetComponent<SphereCollider>().radius - .65f) > .01f ||
                Mathf.Abs(NetworkVoice.Amplify(.02f, 5) - .1f) > .0001f)
            { Fail("ball size or microphone amplification"); yield break; }
            Debug.Log("NTS_TUNING_PASS cane cooldown recharge, ball camera stability/height/size, mic gain");
            Debug.Log("NTS_VOICE_PROBE_PASS PCM fidelity, remote voice, voice waves, rabbit rejection, cane role restriction");
            Application.Quit(0);
        }
        static void Fail(string reason) { Debug.LogError("NTS_VOICE_PROBE_FAIL " + reason); Application.Quit(2); }
    }
}
