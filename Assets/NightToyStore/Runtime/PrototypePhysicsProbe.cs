using System.Collections;
using UnityEngine;

namespace NightToyStore
{
    // Only activated by the explicit test command-line flag on a local test host.
    public sealed class PrototypePhysicsProbe : MonoBehaviour
    {
        IEnumerator Start()
        {
            var session = GetComponent<PrototypeSession>();
            float timeout = Time.realtimeSinceStartup + 20;
            while (session.Manager.ConnectedClientsIds.Count != 2)
            {
                if (Time.realtimeSinceStartup > timeout) { Fail("two-player connection timeout"); yield break; }
                yield return null;
            }
            yield return new WaitForSeconds(.5f);
            NetworkToyPlayer mover = null, ball = null;
            foreach (var player in FindObjectsByType<NetworkToyPlayer>(FindObjectsSortMode.None))
                if (player.OwnerClientId == session.Manager.LocalClientId) mover = player; else ball = player;
            if (mover == null || ball == null) { Fail("players missing"); yield break; }
            ball.Role.Value = (int)ToyRole.TennisBall;
            yield return new WaitForSeconds(.2f);

            var camera = mover.GetComponentInChildren<Camera>();
            mover.ApplyLookDelta(new Vector2(10, 5));
            if (Mathf.Abs(Mathf.DeltaAngle(camera.transform.eulerAngles.y, 20)) > .1f ||
                Mathf.Abs(Mathf.DeltaAngle(camera.transform.eulerAngles.x, -10)) > .1f)
            { Fail("look delta did not rotate camera"); yield break; }
            mover.ApplyLookDelta(new Vector2(0, 10000));
            if (Mathf.Abs(Mathf.DeltaAngle(camera.transform.eulerAngles.x, -85)) > .1f)
            { Fail("camera pitch limit"); yield break; }
            mover.ApplyLookDelta(new Vector2(-10, -42.5f));
            Debug.Log("NTS_LOOK_DELTA_PASS camera yaw pitch and limit");

            var rigidbody = ball.GetComponent<Rigidbody>();
            var controller = mover.GetComponent<CharacterController>();
            controller.enabled = false;
            mover.transform.position = new Vector3(-4, .05f, -7);
            controller.enabled = true;
            rigidbody.position = new Vector3(-3, .05f, -7);
            rigidbody.linearVelocity = Vector3.zero;
            Physics.SyncTransforms();
            int pushes = ball.BodyPushCount;
            mover.PhysicsTestInput = Vector2.right;
            yield return new WaitForSeconds(1.5f);
            mover.PhysicsTestInput = Vector2.zero;
            yield return new WaitForSeconds(.15f);
            if (ball.BodyPushCount <= pushes || rigidbody.position.x < -2.7f)
            { Fail($"body contact did not push ball count={ball.BodyPushCount} mover={mover.transform.position} ball={rigidbody.position}"); yield break; }
            Debug.Log("NTS_BODY_PUSH_PASS physical controller contact");

            float lowRebound = 0, highRebound = 0;
            foreach (float speed in new[] { 2f, 6f, 14f })
            {
                rigidbody.position = new Vector3(10.8f, .02f, -7);
                rigidbody.linearVelocity = Vector3.right * speed;
                Physics.SyncTransforms();
                int before = ball.WallBounceCount;
                float end = Time.realtimeSinceStartup + 2;
                while (ball.WallBounceCount == before && Time.realtimeSinceStartup < end)
                    yield return new WaitForFixedUpdate();
                if (ball.WallBounceCount == before) { Fail("wall impact missing at speed " + speed); yield break; }
                float ratio = ball.LastWallReboundSpeed / ball.LastWallImpactSpeed;
                if (ratio < .79f || ratio > .81f || rigidbody.linearVelocity.x >= 0)
                { Fail("wall did not rebound proportionally at speed " + speed); yield break; }
                if (speed == 2) lowRebound = ball.LastWallReboundSpeed;
                if (speed == 14) highRebound = ball.LastWallReboundSpeed;
                Debug.Log($"NTS_BOUNCE_SPEED_PASS requested={speed} incoming={ball.LastWallImpactSpeed:F3} rebound={ball.LastWallReboundSpeed:F3}");
            }
            if (highRebound <= lowRebound * 4) { Fail("fast impact not stronger"); yield break; }
            Debug.Log("NTS_PHYSICS_PROBE_PASS body push speed-proportional wall bounce and look delta");
            Application.Quit(0);
        }

        void Fail(string message)
        {
            Debug.LogError("NTS_PHYSICS_PROBE_FAIL " + message);
            Application.Quit(4);
        }
    }
}
