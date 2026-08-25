// Assets/Scripts/UI/ToastView.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

// AI 행동 피드백을 화면 상단에 잠깐 띄우는 토스트 메시지 뷰.
// AIController / CombatManager에서 AIToast.Show("메시지") 로 호출.
public class ToastView : MonoBehaviour
{
    public static ToastView Instance { get; private set; }

    [Header("UI")]
    public TMP_Text MessageText;
    public float    DisplayDuration = 1.5f;
    public float    FadeDuration    = 0.3f;

    private CanvasGroup   _group;
    private Queue<string> _queue = new();
    private bool          _showing;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        _group   = GetComponent<CanvasGroup>();
        if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        gameObject.SetActive(true);
    }

    public static void Show(string message)
    {
        if (Instance == null) return;
        Instance._queue.Enqueue(message);
        if (!Instance._showing)
            Instance.StartCoroutine(Instance.ProcessQueue());
    }

    private IEnumerator ProcessQueue()
    {
        _showing = true;
        while (_queue.Count > 0)
        {
            string msg = _queue.Dequeue();
            if (MessageText != null) MessageText.text = msg;

            // 페이드 인
            yield return StartCoroutine(Fade(0f, 1f));
            yield return new WaitForSeconds(DisplayDuration);
            // 페이드 아웃
            yield return StartCoroutine(Fade(1f, 0f));
        }
        _showing = false;
    }

    private IEnumerator Fade(float from, float to)
    {
        float t = 0f;
        while (t < FadeDuration)
        {
            t += Time.deltaTime;
            _group.alpha = Mathf.Lerp(from, to, t / FadeDuration);
            yield return null;
        }
        _group.alpha = to;
    }
}
