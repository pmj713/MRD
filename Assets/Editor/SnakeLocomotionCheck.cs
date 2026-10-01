using System;
using MRD.Control;
using UnityEditor;
using UnityEngine;

namespace MRD.Editor
{
    /// <summary>Run through Tools/MRD/Check Snake Locomotion, or call Run() from the editor.</summary>
    public static class SnakeLocomotionCheck
    {
        [MenuItem("Tools/MRD/Check Snake Locomotion")]
        public static void RunMenu() => Debug.Log(Run());

        public static string Run()
        {
            CheckRig(false);
            CheckRig(true);
            CheckLegacyMover();
            return "PASS: snake first frame, scaled sibling/hierarchical rigs, S wave, fixed segment lengths, " +
                "ground contact, stop, pause, abrupt yaw, tight turn, U-turn, destination arrival, rotated teleport, legacy mover.";
        }

        private static void CheckRig(bool hierarchical)
        {
            var root = new GameObject("Snake locomotion check");
            try
            {
                root.transform.SetPositionAndRotation(new Vector3(3f, 0f, 4f), Quaternion.Euler(0f, 37f, 0f));
                var visual = new GameObject("Visual");
                visual.transform.SetParent(root.transform, false);
                visual.transform.localScale = Vector3.one * 1.2f;
                var bones = new Transform[24];
                for (int i = 0; i < bones.Length; i++)
                {
                    bones[i] = new GameObject($"Spine{i:00}").transform;
                    bones[i].SetParent(visual.transform, false);
                    bones[i].localPosition = new Vector3(0f, 0.115f + 0.085f * Mathf.Exp(-i * 0.45f), -i * 0.12f);
                    bones[i].localRotation = Quaternion.Euler(90f, 0f, 0f);
                    if (hierarchical && i > 0) bones[i].SetParent(bones[i - 1], true);
                }
                var initial = Capture(bones);
                var initialRotations = new Quaternion[bones.Length];
                var heights = new float[bones.Length];
                for (int i = 0; i < bones.Length; i++)
                {
                    initialRotations[i] = bones[i].rotation;
                    heights[i] = bones[i].position.y;
                }
                var mover = root.AddComponent<UnitMover>();
                var snake = visual.AddComponent<SnakeLocomotion>();
                snake.Initialize(root.transform, bones);
                snake.Tick(1f / 60f);
                for (int i = 0; i < bones.Length; i++)
                {
                    Near(bones[i].position, initial[i], 0.0001f, "first frame position");
                    Require(Quaternion.Angle(bones[i].rotation, initialRotations[i]) < 0.05f, "first frame rotation");
                }

                Vector3 travelForward = root.transform.forward;
                Vector3 travelRight = root.transform.right;
                mover.MoveTo(root.transform.position + travelForward * 30f);
                for (int frame = 0; frame < 180; frame++) Step(mover, snake, bones, heights);
                float waveExtent = 0f;
                foreach (var bone in bones)
                    waveExtent = Mathf.Max(waveExtent, Mathf.Abs(Vector3.Dot(bone.position - root.transform.position, travelRight)));
                Require(waveExtent > 0.05f, "moving body must form an S wave");

                mover.Stop();
                var stopped = Capture(bones);
                for (int frame = 0; frame < 60; frame++) Step(mover, snake, bones, heights);
                for (int i = 0; i < bones.Length; i++) Near(bones[i].position, stopped[i], 0.0001f, "stopped pose");
                mover.MoveTo(root.transform.position + Vector3.right * 2f);
                var paused = Capture(bones);
                mover.Tick(0f);
                snake.Tick(0f);
                for (int i = 0; i < bones.Length; i++) Near(bones[i].position, paused[i], 0.0001f, "paused pose");

                mover.Stop();
                root.transform.rotation *= Quaternion.Euler(0f, 180f, 0f);
                snake.Tick(1f / 60f);
                for (int i = 0; i < bones.Length; i++) Near(bones[i].position, paused[i], 0.08f, "abrupt parent yaw must not rotate the body");

                Reach(root.transform.position + root.transform.forward * 4f, mover, snake, bones, heights);
                Reach(root.transform.position + root.transform.right * 0.05f, mover, snake, bones, heights);

                var beforeTeleport = Capture(bones);
                var oldPosition = root.transform.position;
                var oldRotation = root.transform.rotation;
                root.transform.SetPositionAndRotation(new Vector3(203f, 2f, -10f), Quaternion.Euler(0f, -80f, 0f));
                var deltaRotation = root.transform.rotation * Quaternion.Inverse(oldRotation);
                snake.Tick(1f / 60f);
                for (int i = 0; i < bones.Length; i++)
                    Near(bones[i].position, root.transform.position + deltaRotation * (beforeTeleport[i] - oldPosition),
                        0.002f, "teleport must transport the existing curve");
                var afterTeleport = Capture(bones);
                snake.Tick(1f / 60f);
                for (int i = 0; i < bones.Length; i++) Near(bones[i].position, afterTeleport[i], 0.03f, "teleport next frame");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void Reach(Vector3 target, UnitMover mover, SnakeLocomotion snake, Transform[] bones, float[] heights)
        {
            mover.MoveTo(target);
            float initialDistance = Vector3.Distance(mover.transform.position, target);
            for (int frame = 0; frame < 1200 && mover.IsMoving; frame++)
            {
                Step(mover, snake, bones, heights);
                float distance = Vector3.Distance(mover.transform.position, target);
                Require(distance <= initialDistance + 3f, "turn excursion must stay bounded");
            }
            Require(!mover.IsMoving, "snake must finish tight turns and U-turns");
            Near(mover.transform.position, target, 0.001f, "destination");
        }

        private static void Step(UnitMover mover, SnakeLocomotion snake, Transform[] bones, float[] heights)
        {
            mover.Tick(1f / 60f);
            snake.Tick(1f / 60f);
            for (int i = 0; i < bones.Length; i++)
            {
                Require(!float.IsNaN(bones[i].position.x), "finite bone position");
                Require(Mathf.Abs(bones[i].position.y - mover.transform.position.y - heights[i]) < 0.0001f, "ground height");
                if (i == 0) continue;
                var segment = bones[i].position - bones[i - 1].position;
                segment.y = 0f;
                Require(Mathf.Abs(segment.magnitude - 0.144f) < 0.0001f, "bone length must not stretch");
            }
        }

        private static void CheckLegacyMover()
        {
            var go = new GameObject("Legacy mover check");
            try
            {
                var mover = go.AddComponent<UnitMover>();
                mover.MoveTo(Vector3.right * 6f);
                mover.Tick(1f);
                Near(go.transform.position, Vector3.right * 3f, 0.0001f, "other units retain immediate 3m/s movement");
                mover.Tick(1f);
                Require(!mover.IsMoving, "legacy destination reached");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        private static Vector3[] Capture(Transform[] bones)
        {
            var positions = new Vector3[bones.Length];
            for (int i = 0; i < bones.Length; i++) positions[i] = bones[i].position;
            return positions;
        }

        private static void Near(Vector3 actual, Vector3 expected, float tolerance, string message)
            => Require(Vector3.Distance(actual, expected) <= tolerance, message + $": {actual} != {expected}");

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Snake locomotion check: " + message);
        }
    }
}
