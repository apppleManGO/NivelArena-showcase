// Assets/Scripts/Data/DeckSaveLoad.cs
// ─────────────────────────────────────────────────────────────────────────
// 덱 저장 파사드 — 기존 호출부(DeckSaveLoad.Save/Load/GetSavedDeckNames/Delete)는
//  그대로 두고, 실제 저장은 교체 가능한 백엔드(IDeckStore Store)에 위임한다.
//  · 기본값: LocalDeckStore(로컬 JSON) → 현재 동작 동일.
//  · 클라우드 동기화 시: DeckSaveLoad.Store = new CloudDeckStore(...) 한 줄로 교체
//    (로그인 시 클라우드 백엔드로, 로그아웃 시 다시 LocalDeckStore로).
//  실제 저장 로직은 DeckStore.cs(IDeckStore / LocalDeckStore) 참고.
// ─────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;

[Serializable]
public class CustomDeckSave
{
    // 마지막 저장 시각(UTC Ticks). 같은 이름의 덱이 기기마다 다를 때 어느 쪽이 최신인지 판단한다.
    // ※ 예전 저장 파일엔 이 필드가 없어 0으로 읽힌다 = "가장 오래된 것"으로 취급 → 안전하게 동작.
    public long savedAtUtc;

    public string       deckName;
    public string       leaderId;
    public List<string> cardIds = new();  // 중복 포함, 최대 40장
}

public static class DeckSaveLoad
{
    // 현재 저장 백엔드 (기본: 로컬). 로그인/로그아웃 시 교체.
    public static IDeckStore Store = new LocalDeckStore();

    public static void Save(CustomDeckSave deck)   => Store.Save(deck);
    public static CustomDeckSave Load(string name) => Store.Load(name);
    public static List<string> GetSavedDeckNames() => Store.GetSavedDeckNames();
    public static void Delete(string name)         => Store.Delete(name);

    // ── 덱 규칙 상수 (저장 백엔드와 무관 — 여기 유지) ──
    // 룰북 5.1.2.2: 같은 식별 번호 카드는 레어리티 무관 최대 3장
    public static int MaxCopies(string rarity) => 3;
    // 룰북 5.1.2.3: 트리거 표시 카드는 덱 전체에서 최대 8장
    public const int MaxTriggerCards = 8;
}
