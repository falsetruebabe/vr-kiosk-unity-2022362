using UnityEngine;

namespace Phase2.Data
{
    public enum OptionCategoryType { NONE, TEMPERATURE, SIZE, DENSITY }

    /// <summary>
    /// 메뉴 1건에 적용 가능한 단일 옵션 노드 (온도, 사이즈, 농도 등 범용 호환)
    /// </summary>
    [CreateAssetMenu(fileName = "New CafeMenuOption",
                     menuName = "Kiosk/Data/CafeMenuOption",
                     order = 2)]
    public class CafeMenuOption : ScriptableObject
    {
        [Header("Option Identity")]
        [Tooltip("UI에 표시될 옵션 레이블 (예: HOT, 라지, 샷 추가)")]
        public string optionLabel;

        [Tooltip("내부 식별자 – ValidateMission 등에 사용")]
        public string optionId;

        [Header("Option Type")]
        [Tooltip("이 옵션이 속한 카테고리 탭 (배타적 선택 기준이 됨)")]
        public OptionCategoryType category = OptionCategoryType.NONE;

        [Header("Pricing")]
        [Tooltip("이 옵션 선택 시 추가되는 금액 (원)")]
        public int additionalPrice;

        public override bool Equals(object obj)
        {
            if (obj is CafeMenuOption other)
                return optionId == other.optionId;
            return false;
        }

        public override int GetHashCode()
        {
            return optionId != null ? optionId.GetHashCode() : 0;
        }
    }
}
