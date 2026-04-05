using System.Collections.Generic;
using UnityEngine;

namespace Phase2.Data
{
    /// <summary>
    /// 전체 카페 메뉴를 카테고리 단위로 집계하는 최상위 Database ScriptableObject.
    /// Phase1~3 전 구간에서 단 1개 에셋을 공통 참조한다.
    /// </summary>
    [CreateAssetMenu(fileName = "CafeMenuDatabase",
                     menuName = "Kiosk/Data/CafeMenuDatabase",
                     order = 0)]
    public class CafeMenuDatabase : ScriptableObject
    {
        [System.Serializable]
        public class MenuCategory
        {
            [Tooltip("카테고리 탭 레이블 (예: 커피, 논커피, 에이드)")]
            public string categoryName;

            [Tooltip("해당 카테고리에 속하는 메뉴 목록")]
            public List<CafeMenuItem> items = new List<CafeMenuItem>();
        }

        [Header("Category Definitions")]
        public List<MenuCategory> categories = new List<MenuCategory>();

        // -----------------------------------------------------------------------
        //  Runtime Helpers (allocation-free lookup)
        // -----------------------------------------------------------------------

        /// <summary>menuId로 메뉴를 검색합니다. O(n) – DB 규모가 소규모이므로 Dictionary 불필요.</summary>
        public CafeMenuItem FindById(string menuId)
        {
            foreach (var category in categories)
            {
                foreach (var item in category.items)
                {
                    if (item.menuId == menuId)
                        return item;
                }
            }
            return null;
        }

        /// <summary>카테고리 이름으로 아이템 리스트를 반환합니다.</summary>
        public List<CafeMenuItem> GetByCategory(string categoryName)
        {
            foreach (var category in categories)
            {
                if (category.categoryName == categoryName)
                    return category.items;
            }
            return null;
        }
    }
}
