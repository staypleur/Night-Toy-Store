using System.Collections;
using UnityEngine;

namespace NightToyStore
{
    // Explicit opt-in integration probe; never captures the microphone.
    public sealed class PrototypeJumpProbe : MonoBehaviour
    {
        IEnumerator Start()
        {
            var session = GetComponent<PrototypeSession>();
            float deadline = Time.realtimeSinceStartup + 25;
            NetworkToyPlayer owner = null, remote = null;
            while (owner == null || remote == null)
            {
                foreach (var player in FindObjectsByType<NetworkToyPlayer>(FindObjectsSortMode.None))
                    if (player.IsSpawned) { if (player.IsOwner) owner = player; else remote = player; }
                if (Time.realtimeSinceStartup > deadline) { Fail("connection timeout"); yield break; }
                yield return null;
            }
            if (!session.Manager.IsHost)
            {
                yield return new WaitForSeconds(.4f);
                owner.JumpRpc(); // Exercise client-to-server ownership RPC, not only host calls.
                int observed = 0;
                while (Time.realtimeSinceStartup < deadline)
                {
                    if (remote.Role.Value < 3 && remote.transform.position.y > .5f)
                        observed |= 1 << remote.Role.Value;
                    if (remote.Captured.Value && remote.Role.Value == 3)
                        observed |= remote.PermanentDeath.Value ? 16 : 8;
                    owner.ReportJumpTestRpc(observed);
                    if (observed == 31) { Debug.Log("NTS_JUMP_CLIENT_PASS jumps and death flags replicated"); yield break; }
                    yield return new WaitForSeconds(.05f);
                }
                Fail("client observation incomplete " + observed); yield break;
            }
            yield return new WaitForSeconds(.4f);
            float requestDeadline = Time.time + 3;
            while (remote.AcceptedJumps == 0 && Time.time < requestDeadline) yield return null;
            if (remote.AcceptedJumps != 1) { Fail("client-owned jump RPC not accepted"); yield break; }
            foreach (int role in new[] { 0, 1, 2 })
            {
                owner.ChangeRoleRpc(role);
                yield return new WaitForSeconds(.2f);
                var controller = owner.GetComponent<CharacterController>();
                controller.enabled = false;
                owner.GetComponent<Unity.Netcode.Components.NetworkTransform>().Teleport(new Vector3(-7,.05f,-7), Quaternion.identity, Vector3.one);
                controller.enabled = true;
                Physics.SyncTransforms();
                yield return new WaitForSeconds(.25f);
                float ground = owner.transform.position.y, apex = ground;
                int before = owner.AcceptedJumps;
                owner.JumpRpc(); owner.JumpRpc(); // Duplicate grounded requests coalesce.
                yield return new WaitForFixedUpdate();
                float end = Time.time + 1.4f;
                while (Time.time < end)
                {
                    apex = Mathf.Max(apex, owner.transform.position.y);
                    if (owner.transform.position.y > ground + .1f) owner.JumpRpc(); // No airborne jumps.
                    yield return new WaitForFixedUpdate();
                }
                if (owner.AcceptedJumps != before + 1 || apex-ground < .8f || apex-ground > 1.2f || !controller.isGrounded)
                { Fail($"role={role} jumps={owner.AcceptedJumps-before} height={apex-ground} grounded={controller.isGrounded}"); yield break; }
                if ((remote.TestObserverEvidence & 1<<role) == 0) { Fail("remote jump not observed role " + role); yield break; }
                Debug.Log($"NTS_JUMP_ROLE_PASS role={role} height={apex-ground:F3}");
            }
            owner.ChangeRoleRpc(3);
            yield return new WaitForSeconds(.3f);
            int jumps = owner.AcceptedJumps;
            owner.JumpRpc();
            for (int i=0;i<100;i++)
                if (owner.TryCapture(false)) { Fail("ordinary attack killed ball"); yield break; }
            yield return new WaitForSeconds(.2f);
            if (owner.AcceptedJumps != jumps || owner.Captured.Value) { Fail("ball jumped or lost immunity"); yield break; }
            if (!owner.TryCapture(true) || owner.PermanentDeath.Value) { Fail("instant capture / revival flags"); yield break; }
            yield return new WaitForSeconds(.4f);
            owner.ChangeRoleRpc(0); owner.ChangeRoleRpc(3);
            if (!owner.TryCapture(true, true)) { Fail("permanent instant capture"); yield break; }
            yield return new WaitForSeconds(.4f);
            if (remote.TestObserverEvidence != 31) { Fail("death flags not observed " + remote.TestObserverEvidence); yield break; }
            owner.ChangeRoleRpc(0);
            yield return new WaitForSeconds(.3f);
            if (!owner.TryCapture(false)) { Fail("ordinary capture should affect radio"); yield break; }
            Vector3 start = owner.transform.position;
            owner.PhysicsTestInput = Vector2.right;
            jumps = owner.AcceptedJumps;
            owner.JumpRpc(); owner.CaneRpc();
            yield return new WaitForSeconds(.4f);
            if (Mathf.Abs(owner.transform.position.x-start.x)>.01f || owner.AcceptedJumps!=jumps)
            { Fail("captured actor moved or jumped"); yield break; }
            Debug.Log("NTS_JUMP_HOST_PASS three roles jump, no air jump, ball no jump / ordinary immunity / instant death, remote replication");
            Application.Quit(0);
        }
        void Fail(string message) { Debug.LogError("NTS_JUMP_FAIL " + message); Application.Quit(4); }
    }
}
