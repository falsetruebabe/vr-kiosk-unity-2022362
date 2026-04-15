using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Phase2.Core
{
    /// <summary>
    /// 인라인 장바구니 리스트 표시, 스크롤뷰 구성, 수량 조절 버튼을 담당합니다.
    /// 자신의 풀(장바구니 행)을 소유합니다 (방법 B).
    /// BUG-11: 스크롤바 Permanent 유지.
    /// </summary>
    public class CartUIRenderer
    {
        private readonly CartManager _cartManager;
        private readonly TMP_FontAsset _uiFont;
        private readonly Transform _inlineCartListContainer;
        private readonly TextMeshProUGUI _inlineTotalPriceLabel;
        private readonly Button _payButton;

        // Own pool (방법 B)
        private readonly List<PooledUIItem> _poolCartRows = new List<PooledUIItem>(16);

        public CartUIRenderer(
            CartManager cartManager,
            TMP_FontAsset uiFont,
            Transform inlineCartListContainer,
            TextMeshProUGUI inlineTotalPriceLabel,
            Button payButton)
        {
            _cartManager = cartManager;
            _uiFont = uiFont;
            _inlineCartListContainer = inlineCartListContainer;
            _inlineTotalPriceLabel = inlineTotalPriceLabel;
            _payButton = payButton;
        }

        // ═══════════════════════════════════════════════════════════════════
        //  ScrollView Setup (한 번만 호출)
        // ═══════════════════════════════════════════════════════════════════
        public void SetupScrollView()
        {
            if (_inlineCartListContainer == null) return;

            // 이미 설정된 경우 스킵
            if (_inlineCartListContainer.parent != null && _inlineCartListContainer.parent.name == "CartViewport") return;

            // 1. ScrollView 래퍼 생성
            GameObject scrollViewWrapper = new GameObject("CartScrollView", typeof(RectTransform), typeof(XRNoWheelScrollRect));
            scrollViewWrapper.transform.SetParent(_inlineCartListContainer.parent, false);
            RectTransform scrollRt = scrollViewWrapper.GetComponent<RectTransform>();

            RectTransform originalRt = _inlineCartListContainer.GetComponent<RectTransform>();
            scrollRt.anchorMin = originalRt.anchorMin;
            scrollRt.anchorMax = originalRt.anchorMax;
            scrollRt.pivot = originalRt.pivot;
            scrollRt.anchoredPosition = originalRt.anchoredPosition;
            scrollRt.sizeDelta = originalRt.sizeDelta;

            // 2. Viewport 및 Mask
            GameObject viewport = new GameObject("CartViewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(scrollViewWrapper.transform, false);
            RectTransform viewRt = viewport.GetComponent<RectTransform>();
            viewRt.anchorMin = Vector2.zero;
            viewRt.anchorMax = Vector2.one;
            viewRt.offsetMin = Vector2.zero;
            viewRt.offsetMax = Vector2.zero;

            Image viewImg = viewport.GetComponent<Image>();
            viewImg.color = new Color(0, 0, 0, 0.01f);
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            // 3. Content 설정
            _inlineCartListContainer.SetParent(viewport.transform, false);
            originalRt.anchorMin = new Vector2(0, 1);
            originalRt.anchorMax = new Vector2(1, 1);
            originalRt.pivot = new Vector2(0.5f, 1);
            originalRt.anchoredPosition = Vector2.zero;

            VerticalLayoutGroup vlg = _inlineCartListContainer.GetComponent<VerticalLayoutGroup>();
            if (vlg == null) vlg = _inlineCartListContainer.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childControlHeight = false;
            vlg.childControlWidth = true;
            vlg.spacing = 10;
            vlg.padding = new RectOffset(5, 5, 5, 5);

            ContentSizeFitter csf = _inlineCartListContainer.GetComponent<ContentSizeFitter>();
            if (csf == null) csf = _inlineCartListContainer.gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 4. Scrollbar
            GameObject scrollbarGo = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            scrollbarGo.transform.SetParent(scrollViewWrapper.transform, false);
            RectTransform sbRt = scrollbarGo.GetComponent<RectTransform>();
            sbRt.anchorMin = new Vector2(1, 0);
            sbRt.anchorMax = new Vector2(1, 1);
            sbRt.pivot = new Vector2(1, 0.5f);
            sbRt.sizeDelta = new Vector2(25, 0);
            sbRt.anchoredPosition = new Vector2(-5, 0);

            scrollbarGo.GetComponent<Image>().color = new Color(0.88f, 0.88f, 0.90f, 1f); // 라이트 테마 스크롤바

            GameObject slidingArea = new GameObject("Sliding Area", typeof(RectTransform));
            slidingArea.transform.SetParent(scrollbarGo.transform, false);
            RectTransform saRt = slidingArea.GetComponent<RectTransform>();
            saRt.anchorMin = Vector2.zero; saRt.anchorMax = Vector2.one;
            saRt.offsetMin = new Vector2(2, 2); saRt.offsetMax = new Vector2(-2, -2);

            GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(slidingArea.transform, false);
            RectTransform hRt = handle.GetComponent<RectTransform>();
            hRt.anchorMin = Vector2.zero;
            hRt.anchorMax = Vector2.one;
            hRt.sizeDelta = Vector2.zero;
            hRt.anchoredPosition = Vector2.zero;
            handle.GetComponent<Image>().color = new Color(0.5f, 0.5f, 0.5f, 1f);

            Scrollbar sbar = scrollbarGo.GetComponent<Scrollbar>();
            sbar.handleRect = hRt;
            sbar.direction = Scrollbar.Direction.BottomToTop;

            viewRt.offsetMax = new Vector2(-35, 0);

            // 5. ScrollRect 최종 연결
            XRNoWheelScrollRect sr = scrollViewWrapper.GetComponent<XRNoWheelScrollRect>();
            sr.content = originalRt;
            sr.viewport = viewRt;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Elastic;
            sr.verticalScrollbar = sbar;
            sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent; // BUG-11: 항상 보임
            sr.verticalScrollbarSpacing = 2f;
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Cart Refresh (이벤트 핸들러로 등록됨)
        // ═══════════════════════════════════════════════════════════════════
        public void Refresh()
        {
            UIPoolHelper.DeactivatePool(_poolCartRows);
            if (_cartManager == null) return;

            var items = _cartManager.Items;
            for (int i = 0; i < items.Count; i++)
            {
                int index = i;
                var ci = items[i];
                string optStr = "";
                foreach (var o in ci.selectedOptions) optStr += o.optionLabel + " ";
                string text = $"{ci.menuItem.menuName}  {optStr}   {ci.CalculateTotalPrice():N0}원";

                var rowItem = UIPoolHelper.GetPooledItem(_poolCartRows, _inlineCartListContainer);
                SetupCartRowUI(rowItem, text, ci.quantity, index);
            }

            if (_inlineTotalPriceLabel != null)
                _inlineTotalPriceLabel.text = items.Count > 0
                    ? $"합계: {_cartManager.TotalPrice:N0}원"
                    : "메뉴를 담아주세요.";

            if (_payButton != null)
                _payButton.interactable = items.Count > 0;
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Cart Row UI
        // ═══════════════════════════════════════════════════════════════════
        private void SetupCartRowUI(PooledUIItem rowItem, string text, int qty, int cartIndex)
        {
            var go = rowItem.go;
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(940, 70);

            if (rowItem.image == null)
            {
                rowItem.image = go.GetComponent<Image>();
                if (rowItem.image == null) rowItem.image = go.AddComponent<Image>();
            }
            rowItem.image.color = UIPoolHelper.COL_CART_ROW;

            // Text Label
            var labelGO = go.transform.Find("Label")?.gameObject;
            if (labelGO == null)
            {
                labelGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                labelGO.transform.SetParent(go.transform, false);
                var lrt = labelGO.GetComponent<RectTransform>();
                lrt.anchorMin = new Vector2(0, 0); lrt.anchorMax = new Vector2(0.6f, 1);
                lrt.offsetMin = new Vector2(20, 0); lrt.offsetMax = Vector2.zero;
            }
            if (rowItem.label == null)
                rowItem.label = labelGO.GetComponent<TextMeshProUGUI>();
            rowItem.label.text = text; rowItem.label.fontSize = 26; rowItem.label.color = new Color(0.15f, 0.15f, 0.15f);
            rowItem.label.alignment = TextAlignmentOptions.Left;
            if (_uiFont != null) rowItem.label.font = _uiFont;

            // Quantity controls
            CreateQtyButton(go, "BtnMinus", "-", new Vector2(0.65f, 0.5f), () => {
                _cartManager.AdjustQuantity(cartIndex, -1);
            });
            var qtyGO = go.transform.Find("QtyText")?.gameObject;
            if (qtyGO == null)
            {
                qtyGO = new GameObject("QtyText", typeof(RectTransform), typeof(TextMeshProUGUI));
                qtyGO.transform.SetParent(go.transform, false);
                var qrt = qtyGO.GetComponent<RectTransform>();
                qrt.anchorMin = qrt.anchorMax = new Vector2(0.72f, 0.5f);
                qrt.sizeDelta = new Vector2(50, 50);
            }
            var qtmp = qtyGO.GetComponent<TextMeshProUGUI>();
            qtmp.text = qty.ToString(); qtmp.fontSize = 28; qtmp.color = new Color(0.15f, 0.15f, 0.15f);
            qtmp.alignment = TextAlignmentOptions.Center;
            if (_uiFont != null) qtmp.font = _uiFont;

            CreateQtyButton(go, "BtnPlus", "+", new Vector2(0.79f, 0.5f), () => {
                _cartManager.AdjustQuantity(cartIndex, 1);
            });

            // Delete Button
            CreateQtyButton(go, "BtnDel", "X", new Vector2(0.92f, 0.5f), () => {
                _cartManager.RemoveAt(cartIndex);
            }, new Color(0.8f, 0.3f, 0.3f));
        }

        private void CreateQtyButton(GameObject parent, string name, string label,
            Vector2 anchorPivot, UnityEngine.Events.UnityAction action, Color? color = null)
        {
            var btnGo = parent.transform.Find(name)?.gameObject;
            if (btnGo == null)
            {
                btnGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(ClickFeedbackHandler));
                btnGo.transform.SetParent(parent.transform, false);
                var rt = btnGo.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = rt.pivot = anchorPivot;
                rt.sizeDelta = new Vector2(50, 50);

                var txtGo = new GameObject("txt", typeof(RectTransform), typeof(TextMeshProUGUI));
                txtGo.transform.SetParent(btnGo.transform, false);
                var trt = txtGo.GetComponent<RectTransform>();
                trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
                trt.offsetMin = trt.offsetMax = Vector2.zero;
            }

            btnGo.GetComponent<Image>().color = color ?? new Color(0.82f, 0.82f, 0.85f); // 라이트 테마 버튼 배경
            var btn = btnGo.GetComponent<Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(action);

            // BUG-10: Find("txt") 사용 — GetComponentInChildren 제거
            var t = btnGo.transform.Find("txt").GetComponent<TextMeshProUGUI>();
            t.text = label; t.fontSize = 24; t.color = new Color(0.15f, 0.15f, 0.15f); t.alignment = TextAlignmentOptions.Center;
            if (_uiFont != null) t.font = _uiFont;
        }
    }
}
