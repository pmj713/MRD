using System;
using System.Collections.Generic;
using UnityEngine;

namespace MRD.Control
{
    /// <summary>머리가 남긴 지면의 S자 경로를 몸통과 꼬리가 차례로 따라간다.</summary>
    [DisallowMultipleComponent]
    public sealed class SnakeLocomotion : MonoBehaviour
    {
        [SerializeField] private Transform[] bones;
        [SerializeField, Range(0f, 0.2f)] private float waveWidth = 0.07f;
        [SerializeField, Range(0.4f, 2f)] private float waveLength = 0.9f;
        [SerializeField, Min(1f)] private float turnSpeed = 220f;

        private readonly List<Vector3> _trail = new List<Vector3>();
        private Transform _root;
        private float[] _distances;
        private float[] _heights;
        private Quaternion[] _bindRotations;
        private Vector3[] _pose;
        private Quaternion[] _rotations;
        private Vector3 _headOffset;
        private Vector3 _lastRootPosition;
        private Quaternion _lastRootRotation;
        private Quaternion _heading;
        private Vector3 _head;
        private float _length;
        private float _spacing;
        private float _sinceSample;
        private float _phase;
        private float _amplitude;
        private bool _initialized;

        private void Start()
        {
            if (_initialized) return;
            var mover = GetComponentInParent<UnitMover>();
            if (mover != null) Initialize(mover.transform);
        }

        public void Initialize(Transform movementRoot, Transform[] chain = null)
        {
            if (chain != null) bones = chain;
            if (bones == null || bones.Length < 3)
            {
                var found = new List<Transform>();
                foreach (var child in GetComponentsInChildren<Transform>())
                    if (child.name.StartsWith("Spine", StringComparison.Ordinal) &&
                        int.TryParse(child.name.Substring(5), out _)) found.Add(child);
                found.Sort((a, b) => int.Parse(a.name.Substring(5)).CompareTo(int.Parse(b.name.Substring(5))));
                bones = found.ToArray();
            }
            if (movementRoot == null || bones.Length < 3)
            {
                Debug.LogError("SnakeLocomotion requires a movement root and head-to-tail Spine bones.", this);
                enabled = false;
                return;
            }

            _root = movementRoot;
            _distances = new float[bones.Length];
            _heights = new float[bones.Length];
            _bindRotations = new Quaternion[bones.Length];
            _pose = new Vector3[bones.Length];
            _rotations = new Quaternion[bones.Length];
            _headOffset = Quaternion.Inverse(_root.rotation) * (bones[0].position - _root.position);
            float shortest = float.MaxValue;
            for (int i = 0; i < bones.Length; i++)
            {
                _heights[i] = bones[i].position.y - _root.position.y;
                _bindRotations[i] = Quaternion.Inverse(_root.rotation) * bones[i].rotation;
                if (i == 0) continue;
                var segment = bones[i - 1].position - bones[i].position;
                segment.y = 0f;
                float length = segment.magnitude;
                _distances[i] = _distances[i - 1] + length;
                shortest = Mathf.Min(shortest, length);
            }
            _length = _distances[bones.Length - 1];
            if (_length < 0.01f || shortest < 0.0001f)
            {
                Debug.LogError("SnakeLocomotion needs a straight, spaced head-to-tail bind chain.", this);
                enabled = false;
                return;
            }
            _spacing = shortest * 0.25f;
            foreach (var animator in GetComponentsInChildren<Animator>()) animator.enabled = false;
            _root.GetComponent<UnitMover>()?.EnableSnakeMovement(turnSpeed);
            foreach (var renderer in GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                // 피부가 루트의 뒤쪽 경로에 남아 있어도 기존 임포트 경계로 잘리지 않게 한다.
                renderer.updateWhenOffscreen = true;
                renderer.localBounds = new Bounds(Vector3.zero, Vector3.one * _length * 4f /
                    Mathf.Max(0.001f, Mathf.Min(renderer.transform.lossyScale.x, renderer.transform.lossyScale.z)));
            }
            _initialized = true;
            ResetPose();
        }

        public void ResetPose()
        {
            if (!_initialized) return;
            _lastRootPosition = _root.position;
            _lastRootRotation = _root.rotation;
            _heading = _root.rotation;
            _head = _root.position + _root.rotation * _headOffset;
            _head.y = _root.position.y;
            _phase = _amplitude = _sinceSample = 0f;
            _trail.Clear();
            int samples = Mathf.CeilToInt(_length / _spacing) + 3;
            for (int i = samples; i >= 0; i--)
                _trail.Add(_head - _root.forward * (i * _spacing));
            BuildPose();
        }

        private void LateUpdate() => Tick(Time.deltaTime);

        public void Tick(float deltaTime)
        {
            if (!_initialized) return;
            if (deltaTime <= 0f)
            {
                ApplyPose();
                return;
            }

            var displacement = _root.position - _lastRootPosition;
            float distance = new Vector2(displacement.x, displacement.z).magnitude;
            if (displacement.magnitude > Mathf.Max(5f, _length * 2f))
            {
                // 포탈은 기존 곡선을 통째로 옮긴다. 출발지와 도착지를 잇는 긴 몸이 생기지 않는다.
                var rotation = _root.rotation * Quaternion.Inverse(_lastRootRotation);
                for (int i = 0; i < _trail.Count; i++)
                    _trail[i] = _root.position + rotation * (_trail[i] - _lastRootPosition);
                _head = _root.position + rotation * (_head - _lastRootPosition);
                _heading = rotation * _heading;
            }
            else
            {
                _heading = Quaternion.RotateTowards(_heading, _root.rotation, turnSpeed * deltaTime);
                // 멈추면 위상과 머리의 좌우 위치를 보존한다. 정지/일시정지 시 몸이 펴지지 않는다.
                if (distance > 0.000001f)
                {
                    _phase = Mathf.Repeat(_phase + distance * Mathf.PI * 2f / (_length * waveLength), Mathf.PI * 2f);
                    float target = _length * waveWidth * Mathf.Clamp01(distance / deltaTime / 2f);
                    _amplitude = Mathf.Lerp(_amplitude, target, 1f - Mathf.Exp(-deltaTime * 8f));
                }
                var nextHead = _root.position + _heading * _headOffset +
                    _heading * Vector3.right * (Mathf.Sin(_phase) * _amplitude);
                nextHead.y = _root.position.y;
                RecordHead(nextHead);
            }
            _lastRootPosition = _root.position;
            _lastRootRotation = _root.rotation;
            BuildPose();
        }

        private void RecordHead(Vector3 next)
        {
            var delta = next - _head;
            float distance = delta.magnitude;
            if (distance > 0.000001f)
            {
                for (float d = _spacing - _sinceSample; d <= distance; d += _spacing)
                    _trail.Add(_head + delta * (d / distance));
                _sinceSample = Mathf.Repeat(_sinceSample + distance, _spacing);
            }
            _head = next;
            int maxSamples = Mathf.CeilToInt(_length / _spacing) + 8;
            if (_trail.Count > maxSamples) _trail.RemoveRange(0, _trail.Count - maxSamples);
        }

        private Vector3 Sample(float behind)
        {
            var previous = _head;
            for (int i = _trail.Count - 1; i >= 0; i--)
            {
                float length = Vector3.Distance(previous, _trail[i]);
                if (length > 0.000001f && behind <= length)
                    return Vector3.Lerp(previous, _trail[i], behind / length);
                behind -= length;
                previous = _trail[i];
            }
            return previous;
        }

        private void BuildPose()
        {
            for (int i = 0; i < bones.Length; i++)
            {
                _pose[i] = Sample(_distances[i]);
                _pose[i].y = _root.position.y;
                if (i > 0)
                {
                    var segment = _pose[i] - _pose[i - 1];
                    segment.y = 0f;
                    if (segment.sqrMagnitude < 0.000001f) segment = -_root.forward;
                    _pose[i] = _pose[i - 1] + segment.normalized * (_distances[i] - _distances[i - 1]);
                    _pose[i].y = _root.position.y;
                }
            }
            for (int i = 0; i < bones.Length; i++)
            {
                var direction = _pose[Mathf.Max(0, i - 1)] - _pose[Mathf.Min(bones.Length - 1, i + 1)];
                direction.y = 0f;
                _rotations[i] = Quaternion.LookRotation(direction, Vector3.up) * _bindRotations[i];
                _pose[i].y += _heights[i];
            }
            ApplyPose();
        }

        private void ApplyPose()
        {
            // 부모 관절을 먼저 설정한다. 루트가 회전해도 꼬리는 저장된 월드 경로를 유지한다.
            for (int i = 0; i < bones.Length; i++) bones[i].SetPositionAndRotation(_pose[i], _rotations[i]);
        }
    }
}
