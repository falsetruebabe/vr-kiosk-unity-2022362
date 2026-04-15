using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Phase2.Data;

namespace Phase2.Core
{
    /// <summary>
    /// 옵션 선택 팝업의 렌더링과 상호작용을 담당합니다.
    /// 자신의 풀(옵션 토글, 카테고리 행)을 소유합니다 (방법 B).
    /// BUG-08: 동일 옵션 재클릭 시 토글 OFF 지원.
    /// BUG-10: 풀 아이템에 컴포넌트 참조 캐싱.
    /// </summary>
    public class OptionPanelController
    {
        private readonly TMP_FontAsset _uiFont;
        private readonly TextMeshProUGUI _optionMenuNameLabel;
        private readonly TextMeshProUGUI _optionPriceLabel;
        private readonly Transform _optionButtonContainer;
        private readonly Button _addToCartButton;

        private CafeMenuItem _selectedMenuItem;
        private readonly List<CafeMenuOption> _selectedOptions = new List<CafeMenuOption>(4);

        // Own pools (방법 B)
        private readonly List<PooledUIItem> _poolOptToggles = new List<PooledUIItem>(8);
        private readonly List<PooledUIItem> _poolOptCategoryRows = new List<PooledUIItem>(4);

        private struct OptToggle { public PooledUIItem item; public CafeMenuOption opt; }
        private readonly List<OptToggle> _dynOptToggles = new List<OptToggle>(8);

        // Callbacks
        private readonly Action<CafeMenuItem, List<CafeMenuOption>> _onAddToCart;

        public OptionPanelController(
            TMP_FontAsset uiFont,
            TextMeshProUGUI optionMenuNameLabel,
            TextMeshProUGUI optionPriceLabel,
            Transform optionButtonContainer,
            Button addToCartButton,
            Button cancelOptionButton,
            Action<CafeMenuItem, List<CafeMenuOption>> onAddToCart,
            Action onCancel)
        {
            _uiFont = uiFont;
            _optionMenuNameLabel = optionMenuNameLabel;
            _optionPriceLabel = optionPriceLabel;
            _optionButtonContainer = optionButtonContainer;
            _addToCartButton = addToCartButton;
            _onAddToCart = onAddToCart;

            // Wire button click listeners
            addToCartButton?.onClick.AddListener(OnAddToCartClicked);
            cancelOptionButton?.onClick.AddListener(() => onCancel?.Invoke());
        }

        public void Build(CafeMenuItem menuItem)
        {
            UIPoolHelper.DeactivatePool(_poolOptToggles);
            UIPoolHelper.DeactivatePool(_poolOptCategoryRows);
            _dynOptToggles.Clear();
            _selectedOptions.Clear();

            _selectedMenuItem = menuItem;
            if (_selectedMenuItem == null) return;
            if (_optionMenuNameLabel != null) _optionMenuNameLabel.text = _selectedMenuItem.menuName;

            if (_selectedMenuItem.availableOptions != null && _selectedMenuItem.availableOptions.Length > 0)
            {
                // Group by category
                var groupedOpts = new Dictionary<OptionCategoryType, List<CafeMenuOption>>();
                foreach (var opt in _selectedMenuItem.availableOptions)
                {
                    if (opt.category == OptionCategoryType.NONE) continue;
                    if (!groupedOpts.ContainsKey(opt.category))
                        groupedOpts[opt.category] = new List<CafeMenuOption>();
                    groupedOpts[opt.category].Add(opt);
                }

                // Render each category row
                foreach (var kvp in groupedOpts)
                {
                    var rowItem = UIPoolHelper.GetPooledItem(_poolOptCategoryRows, _optionButtonContainer);
                    Transform btnContainer = SetupOptionCategoryRow(rowItem.go, CategoryToKrString(kvp.Key));

                    foreach (var captured in kvp.Value)
                    {
                        var toggleItem = UIPoolHelper.GetPooledItem(_poolOptToggles, btnContainer);
                        string btnLabel = captured.optionLabel;
                        if (captured.additionalPrice > 0)
                            btnLabel += $" (+{captured.additionalPrice})";

                        UIPoolHelper.SetupButtonUI(toggleItem, btnLabel, 24,
                            UIPoolHelper.COL_OPT_OFF, new Vector2(160, 70), _uiFont);

                        // BUG-10: 캐싱된 button 참조 사용
                        toggleItem.button.onClick.RemoveAllListeners();
                        toggleItem.button.onClick.AddListener(() => ToggleOption(captured));

                        _dynOptToggles.Add(new OptToggle { item = toggleItem, opt = captured });
                    }
                }
            }

            ValidateOptionSelection();
        }

        // ═══════════════════════════════════════════════════════════════════
        //  BUG-08: Toggle OFF 지원
        // ═══════════════════════════════════════════════════════════════════
        private void ToggleOption(CafeMenuOption opt)
        {
            // 이미 선택된 옵션인지 확인
            bool alreadySelected = _selectedOptions.Contains(opt);

            // 같은 카테고리 내 기존 선택 제거 (상호 배타)
            if (opt.category != OptionCategoryType.NONE)
                _selectedOptions.RemoveAll(o => o.category == opt.category);

            // BUG-08: 이미 선택된 상태에서 재클릭 → 해제(토글 OFF)
            // 선택되지 않은 상태에서 클릭 → 추가(토글 ON)
            if (!alreadySelected)
                _selectedOptions.Add(opt);

            // Visual refresh using cached image references (BUG-10)
            foreach (var t in _dynOptToggles)
            {
                if (t.item.image != null)
                    t.item.image.color = _selectedOptions.Contains(t.opt)
                        ? UIPoolHelper.COL_OPT_ON : UIPoolHelper.COL_OPT_OFF;
            }
            ValidateOptionSelection();
        }

        private void ValidateOptionSelection()
        {
            // 금액 업데이트
            if (_optionPriceLabel != null && _selectedMenuItem != null)
            {
                int price = _selectedMenuItem.basePrice;
                foreach (var o in _selectedOptions) price += o.additionalPrice;
                _optionPriceLabel.text = $"가격: {price:N0}원";
            }

            // 모든 제공된 카테고리를 1개씩 선택해야 담기 허용
            bool allSelected = true;
            if (_selectedMenuItem != null && _selectedMenuItem.availableOptions != null)
            {
                var reqCategories = new HashSet<OptionCategoryType>();
                foreach (var o in _selectedMenuItem.availableOptions)
                {
                    if (o.category != OptionCategoryType.NONE)
                        reqCategories.Add(o.category);
                }

                foreach (var reqCat in reqCategories)
                {
                    bool found = false;
                    foreach (var sel in _selectedOptions)
                    {
                        if (sel.category == reqCat) { found = true; break; }
                    }
                    if (!found) { allSelected = false; break; }
                }
            }

            if (_addToCartButton != null)
                _addToCartButton.interactable = allSelected;
        }

        private void OnAddToCartClicked()
        {
            if (_selectedMenuItem == null) return;
            _onAddToCart?.Invoke(_selectedMenuItem, new List<CafeMenuOption>(_selectedOptions));
        }

        // ═══════════════════════════════════════════════════════════════════
        //  UI Helpers
        // ═══════════════════════════════════════════════════════════════════
        private string CategoryToKrString(OptionCategoryType cat)
        {
            switch (cat)
            {
                case OptionCategoryType.TEMPERATURE: return "온도";
                case OptionCategoryType.SIZE:        return "사이즈";
                case OptionCategoryType.DENSITY:     return "농도";
                default:                             return "옵션";
            }
        }

        private Transform SetupOptionCategoryRow(GameObject row, string title)
        {
            var rt = row.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(600, 80);

            var hlg = row.GetComponent<HorizontalLayoutGroup>();
            if (hlg == null) hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.childControlHeight = true;
            hlg.childControlWidth = true;
            hlg.childForceExpandHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.spacing = 20;
            hlg.padding = new RectOffset(10, 10, 5, 5);
            hlg.childAlignment = TextAnchor.MiddleLeft;

            var titleGo = row.transform.Find("Title")?.gameObject;
            if (titleGo == null)
            {
                titleGo = new GameObject("Title", typeof(RectTransform),
                    typeof(TextMeshProUGUI), typeof(LayoutElement));
                titleGo.transform.SetParent(row.transform, false);
                titleGo.GetComponent<RectTransform>().sizeDelta = new Vector2(120, 70);
                var tle = titleGo.GetComponent<LayoutElement>();
                tle.preferredWidth = 120;
                tle.preferredHeight = 70;
            }
            var tmp = titleGo.GetComponent<TextMeshProUGUI>();
            tmp.text = title; tmp.fontSize = 28; tmp.color = new Color(0.15f, 0.15f, 0.15f);
            tmp.alignment = TextAlignmentOptions.Left;
            if (_uiFont != null) tmp.font = _uiFont;

            var btnCont = row.transform.Find("BtnContainer")?.gameObject;
            if (btnCont == null)
            {
                btnCont = new GameObject("BtnContainer", typeof(RectTransform),
                    typeof(HorizontalLayoutGroup), typeof(LayoutElement));
                btnCont.transform.SetParent(row.transform, false);
                var blg = btnCont.GetComponent<HorizontalLayoutGroup>();
                blg.childControlHeight = true;
                blg.childControlWidth = true;
                blg.childForceExpandHeight = false;
                blg.childForceExpandWidth = false;
                blg.spacing = 15;
                var le = btnCont.GetComponent<LayoutElement>();
                le.flexibleWidth = 1f;
                le.flexibleHeight = 1f;
            }
            return btnCont.transform;
        }
    }
}
