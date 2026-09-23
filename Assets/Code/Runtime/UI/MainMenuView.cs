using System;
using UnityEngine;
using UnityEngine.UI;

namespace SteelDistrict.UI
{
    // 씬에 저장한 UI 참조입니다. 런타임은 위치·색·폰트·레이아웃을 다시 만들지 않습니다.
    public sealed class MainMenuView : MonoBehaviour
    {
        [Serializable] public sealed class Card
        {
            public Button button;
            public RawImage image;
            public Text name, hint;
        }
        [Serializable] public sealed class Stat
        {
            public Text value;
            public Image fill;
            [Tooltip("막대가 가득 차는 기준값입니다. 숫자는 실제 설정값을 그대로 표시합니다.")] public float maximum;
        }
        [Header("화면 영역 — RectTransform으로 배치 편집")]
        public RectTransform list, detail;
        public RawImage display;
        [Header("내용이 바뀌는 텍스트 — 값은 실행 시 갱신")]
        public Text title,position,status,selected,heading,pageLabel,empty;
        [Tooltip("현재 골드와 구매 버튼 가격 표시입니다.")] public Text gold, purchaseLabel;
        [Header("동작 버튼 — 실행 시 컨트롤러가 연결")]
        public Button ownedTab,shopTab,back,select,enter,previous,next,pagePrevious,pageNext;
        [Tooltip("상점에서만 표시할 차량 구매 버튼입니다.")] public Button purchase;
        [Header("페이지 카드 8개 — 위치·크기·글꼴을 개별 편집")]
        public Card[] cards;
        [Header("성능 6개 — 무게·속도·가속·제동·조향·접지 순서")]
        public Stat[] stats;
        public bool IsValid => list!=null && detail!=null && display!=null && title!=null && position!=null && status!=null &&
            selected!=null && heading!=null && pageLabel!=null && empty!=null && ownedTab!=null && shopTab!=null && back!=null &&
            select!=null && enter!=null && previous!=null && next!=null && pagePrevious!=null && pageNext!=null &&
            gold!=null && purchaseLabel!=null && purchase!=null && cards!=null && cards.Length==8 && Array.TrueForAll(cards,x=>x!=null && x.button!=null && x.image!=null && x.name!=null && x.hint!=null) &&
            stats!=null && stats.Length==6 && Array.TrueForAll(stats,x=>x!=null && x.value!=null && x.fill!=null && x.maximum>0);
    }
}
