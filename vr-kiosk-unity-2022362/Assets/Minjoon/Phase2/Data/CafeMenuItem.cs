using UnityEngine;

namespace Phase2.Data
{
    /// <summary>
    /// 카페 메뉴 1개 아이템의 메타데이터를 보유하는 ScriptableObject.
    /// Phase1~3 전 구간에서 공통 참조된다.
    /// </summary>
    [CreateAssetMenu(fileName = "New CafeMenuItem",
                     menuName = "Kiosk/Data/CafeMenuItem",
                     order = 1)]
    public class CafeMenuItem : ScriptableObject
    {
        [Header("Menu Identity")]
        [Tooltip("UI에 표시될 메뉴 이름 (예: 아메리카노)")]
        public string menuName;

        [Tooltip("내부 식별자 – ValidateMission 비교에 사용")]
        public string menuId;

        [Header("Pricing")]
        [Tooltip("옵션 미적용 시 기본 가격 (원)")]
        public int basePrice;

        [Header("Display")]
        [Tooltip("메뉴 카드에 표시되는 썸네일 이미지")]
        public Sprite thumbnail;

        [Tooltip("메뉴 카테고리 레이블 (예: 커피, 논커피, 에이드)")]
        public string category;

        [Header("Available Options")]
        [Tooltip("이 메뉴에 적용할 수 있는 옵션 목록")]
        public CafeMenuOption[] availableOptions;
    }
}
