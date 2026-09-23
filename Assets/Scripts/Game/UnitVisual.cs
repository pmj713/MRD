using UnityEngine;

namespace MRD.Game
{
    /// <summary>
    /// 정식 아트가 준비되기 전, 색깔 있는 3D 큐브로 유닛을 표시하기 위한 임시 헬퍼.
    /// 조명 없이도 색이 보이도록 Unlit 셰이더를 쓴다 - 정식 렌더러/아트가 갖춰지면 대체될 자리.
    /// </summary>
    public static class UnitVisual
    {
        /// <summary>visualPrefab이 있으면 그 모델을, 없으면 임시 큐브를 붙인다.
        /// 선택 시 색이 바뀌는 대상 렌더러로 모델의 첫 번째 렌더러(또는 큐브 렌더러)를 반환한다.</summary>
        public static Renderer AttachVisual(Transform parent, GameObject visualPrefab, Color fallbackColor, float fallbackSize)
        {
            if (visualPrefab == null) return AttachCube(parent, fallbackColor, fallbackSize);

            var go = GameObject.Instantiate(visualPrefab, parent, false);
            go.name = "Visual";
            return go.GetComponentInChildren<Renderer>();
        }

        public static Renderer AttachCube(Transform parent, Color color, float size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Visual";
            go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(size, size, size);
            go.transform.localPosition = new Vector3(0f, size / 2f, 0f); // 큐브 바닥이 부모의 위치(바닥)에 닿도록

            var collider = go.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider); // 선택 판정은 화면좌표 기반이라 콜라이더가 필요 없다

            var renderer = go.GetComponent<Renderer>();
            renderer.material = CreateUnlitMaterial(color);
            return renderer;
        }

        private static Material CreateUnlitMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            return new Material(shader) { color = color };
        }
    }
}
