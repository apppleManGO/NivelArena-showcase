// Assets/Scripts/UI/DefensePopup.cs
using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DefensePopup : MonoBehaviour
{
    public static DefensePopup Instance { get; private set; }

    [Header("UI")]
    public TMP_Text AttackerText;   // "바이퍼 (4500)"
    public TMP_Text DefenderText;   // "네온 (3000)"
    public Button   BlockButton;    // 방어
    public Button   PassButton;     // 패스 (대미지 받기)

    private Action<bool> _callback; // true = 방어, false = 패스

    private Canvas _canvas;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        // 자신 또는 부모 Canvas에 override 추가해서 최상위로 띄움
        _canvas = GetComponent<Canvas>();
        if (_canvas == null) _canvas = gameObject.AddComponent<Canvas>();
        _canvas.overrideSorting = true;
        _canvas.sortingOrder    = 200;

        var raycaster = GetComponent<UnityEngine.UI.GraphicRaycaster>();
        if (raycaster == null) gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        BlockButton.onClick.AddListener(() => OnChoice(true));
        PassButton.onClick.AddListener(()  => OnChoice(false));

        gameObject.SetActive(false);
    }

    // 팝업 표시 — callback(true) = 방어, callback(false) = 패스
    //  · attackerPower/defenderPower: 버프·디버프·리더 패시브·디펜더 보너스가 모두 반영된 "실제 전투 수치".
    //    권위 측(호스트/오프라인 CombatManager)이 계산해 넘겨준다. -1이면 정보 없음으로 보고 인쇄 파워로 폴백.
    public void Show(CardData attacker, CardData defender, Action<bool> callback,
                     int attackerPower = -1, int defenderPower = -1)
    {
        _callback = callback;
        ReparentToRootCanvas();
        gameObject.SetActive(true);
        Time.timeScale = 0f; // 팝업 뜨는 동안 게임 일시정지

        if (AttackerText != null)
            AttackerText.text = $"Attack: {attacker.CardName} ({PowerLabel(attackerPower, attacker)})";

        if (DefenderText != null)
            DefenderText.text = defender != null
                ? $"Defense: {defender.CardName} ({PowerLabel(defenderPower, defender)})"
                : "No defender";
    }

    // 실제 수치를 보여주고, 인쇄된 파워와 다르면 증감분을 색으로 덧붙임 (레인 라벨과 같은 색 규칙)
    private string PowerLabel(int effectivePower, CardData card)
    {
        if (card == null) return "0";
        int shown = effectivePower >= 0 ? effectivePower : card.AttackPower;
        int delta = shown - card.AttackPower;
        if (delta == 0) return shown.ToString();
        string hex = delta > 0 ? "5CFF5C" : "FF5A5A";
        return $"{shown} <color=#{hex}>{(delta > 0 ? "+" : "")}{delta}</color>";
    }

    private void ReparentToRootCanvas()
    {
        var root = GetComponentInParent<Canvas>();
        if (root == null) return;
        while (root.transform.parent != null &&
               root.transform.parent.GetComponentInParent<Canvas>() != null)
            root = root.transform.parent.GetComponentInParent<Canvas>();
        transform.SetParent(root.transform, true);
        transform.SetAsLastSibling();
    }

    private void OnChoice(bool block)
    {
        gameObject.SetActive(false);
        Time.timeScale = 1f;
        _callback?.Invoke(block);
    }
}
