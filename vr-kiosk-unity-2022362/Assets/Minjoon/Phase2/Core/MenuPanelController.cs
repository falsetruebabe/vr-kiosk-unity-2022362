using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Phase2.Data;

namespace Phase2.Core
{
    /// <summary>
    /// 메뉴 패널의 카테고리 탭과 메뉴 카드 렌더링을 담당합니다.
    /// 자신의 풀(카테고리 탭, 메뉴 카드)을 소유합니다 (방법 B).
    /// BUG-10: 캐싱된 PooledUIItem으로 GetComponentInChildren 호출 제거.
    /// </summary>
    public class MenuPanelController
    {
        private readonly CafeMenuDatabase _menuDatabase;
        private readonly TMP_FontAsset _uiFont;
        private readonly Transform _categoryTabContainer;
        private readonly Transform _menuCardContainer;
        private readonly Action<CafeMenuItem> _onMenuItemSelected;

        private string _currentCategory;

        // Own pools (방법 B: 각 패널이 자신의 풀 소유)
        private readonly List<PooledUIItem> _poolCatTabs = new List<PooledUIItem>(8);
        private readonly List<PooledUIItem> _poolMenuCards = new List<PooledUIItem>(16);

        public MenuPanelController(
            CafeMenuDatabase menuDatabase,
            TMP_FontAsset uiFont,
            Transform categoryTabContainer,
            Transform menuCardContainer,
            Action<CafeMenuItem> onMenuItemSelected)
        {
            _menuDatabase = menuDatabase;
            _uiFont = uiFont;
            _categoryTabContainer = categoryTabContainer;
            _menuCardContainer = menuCardContainer;
            _onMenuItemSelected = onMenuItemSelected;
        }

        public void Build()
        {
            UIPoolHelper.DeactivatePool(_poolCatTabs);
            UIPoolHelper.DeactivatePool(_poolMenuCards);

            if (_menuDatabase == null || _menuDatabase.categories.Count == 0) return;
            if (string.IsNullOrEmpty(_currentCategory))
                _currentCategory = _menuDatabase.categories[0].categoryName;

            // Category tabs
            for (int i = 0; i < _menuDatabase.categories.Count; i++)
            {
                var cat = _menuDatabase.categories[i];
                string name = cat.categoryName;
                bool active = name == _currentCategory;

                var item = UIPoolHelper.GetPooledItem(_poolCatTabs, _categoryTabContainer);
                UIPoolHelper.SetupButtonUI(item, name, 28,
                    active ? UIPoolHelper.COL_PRIMARY : UIPoolHelper.COL_TAB_OFF,
                    new Vector2(200, 70), _uiFont);

                // FEAT-04: 활성 탭 밑줄 표시
                var underline = item.go.transform.Find("Underline")?.gameObject;
                if (underline == null)
                {
                    underline = new GameObject("Underline", typeof(RectTransform), typeof(Image));
                    underline.transform.SetParent(item.go.transform, false);
                    var urt = underline.GetComponent<RectTransform>();
                    urt.anchorMin = new Vector2(0.1f, 0f);
                    urt.anchorMax = new Vector2(0.9f, 0f);
                    urt.pivot = new Vector2(0.5f, 0f);
                    urt.sizeDelta = new Vector2(0, 4);
                }
                underline.GetComponent<Image>().color = UIPoolHelper.COL_PRIMARY;
                underline.SetActive(active);

                // BUG-10: 캐싱된 button 참조 사용 — GetComponentInChildren 제거
                item.button.onClick.RemoveAllListeners();
                item.button.onClick.AddListener(() =>
                {
                    _currentCategory = name;
                    Build();
                });
            }

            // Menu cards
            var items = _menuDatabase.GetByCategory(_currentCategory);
            if (items != null)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    CafeMenuItem captured = items[i];
                    var item = UIPoolHelper.GetPooledItem(_poolMenuCards, _menuCardContainer);
                    UIPoolHelper.SetupMenuCardUI(item, captured, _uiFont);

                    // BUG-10: 캐싱된 button 참조 사용 — GetComponentInChildren 제거
                    item.button.onClick.RemoveAllListeners();
                    item.button.onClick.AddListener(() => _onMenuItemSelected?.Invoke(captured));
                }
            }
        }

        public void ResetCategory() { _currentCategory = null; }
    }
}
