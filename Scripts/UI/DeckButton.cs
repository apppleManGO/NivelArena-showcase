// Assets/Scripts/UI/DeckButton.cs
using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DeckButton : MonoBehaviour
{
    [Header("설정")]
    public string DeckId;        // "ST01", "ST02", "ST03"

    [Header("UI")]
    public Image    LeaderImage;  // 리더 카드 이미지
    public TMP_Text DeckNameText;
    public TMP_Text AttributeText;
    public Image    SelectBorder; // 선택됐을 때 테두리 (없어도 됨)

    public Color NormalColor   = new Color(1f, 1f, 1f, 0f);
    public Color SelectedColor = new Color(1f, 0.85f, 0f, 1f); // 금색 테두리

    private UnityEngine.UI.Outline _outline;

    public event Action<string> OnSelected;

    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
        if (_button == null)
            _button = gameObject.AddComponent<Button>();

        _button.onClick.AddListener(() => OnSelected?.Invoke(DeckId));

        // SelectBorder가 없으면 Outline으로 대체
        if (SelectBorder == null)
        {
            _outline = GetComponent<UnityEngine.UI.Outline>();
            if (_outline == null)
                _outline = gameObject.AddComponent<UnityEngine.UI.Outline>();
            _outline.effectColor = new Color(0, 0, 0, 0);
            _outline.effectDistance = new Vector2(4, -4);
        }
    }

    private void Start()
    {
        // DeckData에서 리더 이미지/덱 이름/속성 자동 로드 (DeckId만 설정하면 됨)
        var deck = Resources.Load<DeckData>($"Decks/{DeckId}");

        if (LeaderImage != null && deck != null && deck.Leader != null && deck.Leader.BaseArtwork != null)
            LeaderImage.sprite = deck.Leader.BaseArtwork;

        if (DeckNameText != null)
            DeckNameText.text = deck != null ? deck.DeckName : DeckId;

        if (AttributeText != null)
            AttributeText.text = deck != null && deck.Leader != null ? deck.Leader.Attribute : "";

        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        if (SelectBorder != null)
        {
            SelectBorder.color = selected ? SelectedColor : NormalColor;
        }
        else if (_outline != null)
        {
            _outline.effectColor = selected ? SelectedColor : new Color(0, 0, 0, 0);
        }
    }
}
