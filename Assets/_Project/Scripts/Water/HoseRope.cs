using UnityEngine;

namespace Firehouse.Water
{
    /// <summary>
    /// Local-only hose visual: a verlet rope from the reel to the nozzle that sags and lies on the ground,
    /// drawn with a LineRenderer. Pure cosmetics; the hose length limit is enforced on the holder's body.
    /// </summary>
    public sealed class HoseRope : MonoBehaviour
    {
        private Vector3[] _points, _prev;
        private LineRenderer _line;
        private int _count;
        private readonly RaycastHit[] _hits = new RaycastHit[1];

        public void Init(Material baseMaterial, int points)
        {
            _count = Mathf.Max(4, points);
            _points = new Vector3[_count];
            _prev = new Vector3[_count];
            _line = gameObject.AddComponent<LineRenderer>();
            _line.positionCount = _count;
            _line.widthMultiplier = 0.09f;
            _line.numCornerVertices = 2;
            _line.generateLightingData = true;
            _line.useWorldSpace = true;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            var mat = new Material(baseMaterial) { name = "M_Hose" };
            mat.SetFloat("_UseVertexColor", 0f);
            mat.SetColor("_BaseColor", new Color(0.95f, 0.8f, 0.25f));
            _line.sharedMaterial = mat;
        }

        public void ResetRope(Vector3 a, Vector3 b)
        {
            if (_points == null) return;
            for (int i = 0; i < _count; i++)
            {
                _points[i] = Vector3.Lerp(a, b, i / (float)(_count - 1));
                _prev[i] = _points[i];
            }
        }

        public void Simulate(Vector3 start, Vector3 end, float maxLength)
        {
            if (_points == null) return;
            if ((_points[0] - start).sqrMagnitude > 100f) ResetRope(start, end);

            float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
            // The rope is never shorter than the gap (so it doesn't fight the length limit) and never longer than the line.
            float length = Mathf.Clamp(Vector3.Distance(start, end) * 1.25f + 1.5f, 2f, maxLength);
            float seg = length / (_count - 1);

            for (int i = 1; i < _count - 1; i++)
            {
                var p = _points[i];
                var v = (p - _prev[i]) * 0.96f;
                _prev[i] = p;
                _points[i] = p + v + Physics.gravity * dt * dt;
            }
            for (int iter = 0; iter < 10; iter++)
            {
                _points[0] = start;
                _points[_count - 1] = end;
                for (int i = 0; i < _count - 1; i++)
                {
                    var d = _points[i + 1] - _points[i];
                    float m = d.magnitude;
                    if (m < 1e-4f || m <= seg) continue;
                    var corr = d * ((m - seg) / m * 0.5f);
                    if (i != 0) _points[i] += corr;
                    if (i + 1 != _count - 1) _points[i + 1] -= corr;
                }
            }
            // Lie on the ground instead of sinking through it.
            for (int i = 1; i < _count - 1; i++)
            {
                var p = _points[i];
                if (Physics.RaycastNonAlloc(p + Vector3.up * 1.5f, Vector3.down, _hits, 1.6f, 1 << 0, QueryTriggerInteraction.Ignore) > 0)
                {
                    float gy = _hits[0].point.y + 0.05f;
                    if (p.y < gy) { p.y = gy; _points[i] = p; _prev[i] = Vector3.Lerp(_prev[i], p, 0.5f); }
                }
            }
            _line.SetPositions(_points);
        }
    }
}
