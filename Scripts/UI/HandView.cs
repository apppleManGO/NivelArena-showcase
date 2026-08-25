// Assets/Script/UI/HandView.cs
using System.Collections.Generic;
using UnityEngine;

public class HandView : MonoBehaviour
{
    public static HandView Instance { get; private set; }

    [Header("설정")]
    public GameObject CardViewPrefab; // CardView 프리팹
    public Transform  HandContainer;  // 손패 카드들이 들어갈 부모

    private List<CardView> _cardViews = new();

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        // TurnManager 이벤트 구독 — 드로우 페이즈마다 갱신
        TurnManager.Instance.OnPhaseChanged += OnPhaseChanged;
    }

    private void OnPhaseChanged(PhaseType phase)
    {
        if (phase == PhaseType.Draw && TurnManager.Instance.IsPlayerTurn)
            RefreshHand();
    }

    // 손패 UI 전체 새로고침
    public void RefreshHand()
    {
        // 기존 카드뷰 전부 제거
        foreach (var cv in _cardViews)
            if (cv != null) Destroy(cv.gameObject);
        _cardViews.Clear();

        // 현재 손패로 다시 생성
        foreach (var card in PlayerController.PlayerInstance.Hand)
        {
            var go = Instantiate(CardViewPrefab, HandContainer);
            var cv = go.GetComponent<CardView>();
            cv.Setup(card);
            _cardViews.Add(cv);
        }
    }
}