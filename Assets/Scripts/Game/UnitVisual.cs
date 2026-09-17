using UnityEngine;

namespace MRD.Game
{
    /// <summary>
    /// 정식 아트가 준비되기 전, 색깔 있는 정사각형으로 유닛을 표시하기 위한 임시 헬퍼.
    /// 실제 스프라이트가 생기면 이 헬퍼를 호출하는 쪽만 바꾸면 된다.
    /// </summary>
    public static class UnitVisual
    {
        private static Sprite _whiteSquareSprite;

        public static SpriteRenderer AttachSquare(Transform parent, Color color, float size)
        {
            var go = new GameObject("Visual");
            go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(size, size, 1f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = GetWhiteSquareSprite();
            renderer.color = color;
            return renderer;
        }

        private static Sprite GetWhiteSquareSprite()
        {
            if (_whiteSquareSprite != null) return _whiteSquareSprite;

            var texture = new Texture2D(4, 4);
            var pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            texture.SetPixels(pixels);
            texture.Apply();

            _whiteSquareSprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            return _whiteSquareSprite;
        }
    }
}
