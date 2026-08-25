// Assets/Scripts/UI/CardBackGenerator.cs
// 니벨아레나 카드 뒷면 스프라이트 제공
using UnityEngine;

public class CardBackGenerator : MonoBehaviour
{
    public static CardBackGenerator Instance { get; private set; }

    [Header("카드 뒷면 이미지 (Inspector에서 연결)")]
    public Sprite CardBackSprite;

    [Header("이미지 없을 때 폴백 색상")]
    public Color BackgroundColor = new Color(0.1f, 0.1f, 0.18f, 1f);
    public Color BorderColor     = new Color(0.6f, 0.5f, 0.2f, 1f);

    private Sprite _fallbackSprite;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    // 카드 뒷면 스프라이트 반환
    public Sprite GetCardBackSprite(int width = 200, int height = 280)
    {
        if (CardBackSprite != null) return CardBackSprite;

        // 폴백: 이미지 없으면 기존 방식으로 생성
        if (_fallbackSprite != null) return _fallbackSprite;

        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[width * height];
        int border = 8;

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                bool isBorder = x < border || x >= width - border
                             || y < border || y >= height - border;
                pixels[y * width + x] = isBorder ? BorderColor : BackgroundColor;
            }

        tex.SetPixels(pixels);
        tex.Apply();

        _fallbackSprite = Sprite.Create(tex,
            new Rect(0, 0, width, height),
            new Vector2(0.5f, 0.5f));

        return _fallbackSprite;
    }
}
