using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Phase2.Core
{
    /// <summary>
    /// 풀링된 UI 요소의 컴포넌트 참조를 캐싱하는 래퍼 클래스.
    /// BUG-10: GetComponent 반복 호출을 방지하여 성능 최적화.
    /// </summary>
    public class PooledUIItem
    {
        public GameObject go;
        public Button button;
        public Image image;
        public TextMeshProUGUI label;
    }

    /// <summary>
    /// UI 오브젝트 풀링 및 공용 UI 셋업을 위한 정적 헬퍼.
    /// 각 패널 컨트롤러가 자신의 풀을 소유하되, 공용 풀 조작 로직을 공유한다.
    /// </summary>
    public static class UIPoolHelper
    {
        // ═══════════════════════════════════════════════════════════════════
        //  Shared Color Constants
        // ═══════════════════════════════════════════════════════════════════
        public static readonly Color COL_CARD_BG   = Color.white; // 카드 배경은 흰색
        public static readonly Color COL_PRIMARY   = new Color(0.95f, 0.40f, 0.25f); // 메인 컬러 (주황/빨강 계열)
        public static readonly Color COL_SUCCESS   = new Color(0.15f, 0.65f, 0.40f);
        public static readonly Color COL_TAB_OFF   = new Color(0.90f, 0.90f, 0.92f); // 탭 비활성 (밝은 회색)
        public static readonly Color COL_OPT_ON    = new Color(0.95f, 0.40f, 0.25f); // 주황색 토글 ON
        public static readonly Color COL_OPT_OFF   = new Color(0.85f, 0.85f, 0.88f); // 토글 OFF
        public static readonly Color COL_CART_ROW  = new Color(0.95f, 0.95f, 0.97f); // 장바구니 줄

        // ═══════════════════════════════════════════════════════════════════
        //  Pool Operations
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 풀에서 비활성화된 아이템을 찾아 재활용하거나 새로 생성합니다.
        /// 캐싱된 컴포넌트 참조는 Setup 호출 후에 유효합니다.
        /// </summary>
        public static PooledUIItem GetPooledItem(List<PooledUIItem> pool, Transform parent)
        {
            for (int i = 0; i < pool.Count; i++)
            {
                if (!pool[i].go.activeSelf)
                {
                    var item = pool[i];
                    item.go.transform.SetParent(parent, false);
                    item.go.SetActive(true);
                    item.go.transform.SetAsLastSibling();
                    return item;
                }
            }
            var newGo = new GameObject("PooledItem", typeof(RectTransform));
            newGo.transform.SetParent(parent, false);
            var newItem = new PooledUIItem { go = newGo };
            pool.Add(newItem);
            return newItem;
        }

        /// <summary>풀의 모든 아이템을 비활성화합니다.</summary>
        public static void DeactivatePool(List<PooledUIItem> pool)
        {
            for (int i = 0; i < pool.Count; i++)
                pool[i].go.SetActive(false);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  UI Setup Utilities (캐싱된 참조 활용으로 GetComponent 호출 제거)
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 범용 버튼 UI를 구성하고, 캐싱된 참조를 PooledUIItem에 기록합니다.
        /// 2회차 이후 호출에서는 GetComponent 없이 캐싱된 참조를 사용합니다.
        /// </summary>
        public static void SetupButtonUI(PooledUIItem item, string label, float fontSize,
            Color bgColor, Vector2 size, TMP_FontAsset font)
        {
            var go = item.go;
            go.GetComponent<RectTransform>().sizeDelta = size;

            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            le.preferredWidth = size.x;
            le.preferredHeight = size.y;

            if (item.image == null)
            {
                item.image = go.GetComponent<Image>();
                if (item.image == null) item.image = go.AddComponent<Image>();
            }
            item.image.color = bgColor;

            if (item.button == null)
            {
                item.button = go.GetComponent<Button>();
                if (item.button == null) item.button = go.AddComponent<Button>();
            }

            if (go.GetComponent<ClickFeedbackHandler>() == null)
                go.AddComponent<ClickFeedbackHandler>();

            var labelGO = go.transform.Find("Label")?.gameObject;
            if (labelGO == null)
            {
                labelGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                labelGO.transform.SetParent(go.transform, false);
                var lrt = labelGO.GetComponent<RectTransform>();
                lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
                lrt.offsetMin = new Vector2(8, 4); lrt.offsetMax = new Vector2(-8, -4);
            }

            if (item.label == null)
                item.label = labelGO.GetComponent<TextMeshProUGUI>();

            item.label.text = label;
            item.label.fontSize = fontSize;
            item.label.color = new Color(0.15f, 0.15f, 0.15f); // 텍스트 어둡게
            item.label.alignment = TextAlignmentOptions.Center;
            if (font != null) item.label.font = font;
        }

        /// <summary>
        /// 메뉴 카드 UI를 구성합니다. 썸네일, 이름, 가격 요소를 포함합니다.
        /// </summary>
        public static void SetupMenuCardUI(PooledUIItem item, Data.CafeMenuItem menuItem, TMP_FontAsset font)
        {
            var card = item.go;
            card.GetComponent<RectTransform>().sizeDelta = new Vector2(280, 200);

            if (item.image == null)
            {
                item.image = card.GetComponent<Image>();
                if (item.image == null) item.image = card.AddComponent<Image>();
            }
            item.image.color = COL_CARD_BG;

            if (item.button == null)
            {
                item.button = card.GetComponent<Button>();
                if (item.button == null) item.button = card.AddComponent<Button>();
            }

            if (card.GetComponent<ClickFeedbackHandler>() == null)
                card.AddComponent<ClickFeedbackHandler>();
            if (card.GetComponent<UIHoverScale>() == null)
                card.AddComponent<UIHoverScale>();

            // Thumbnail
            var thumbGO = card.transform.Find("Thumb")?.gameObject;
            if (thumbGO == null)
            {
                thumbGO = new GameObject("Thumb", typeof(RectTransform), typeof(Image));
                thumbGO.transform.SetParent(card.transform, false);
                var trt = thumbGO.GetComponent<RectTransform>();
                trt.anchorMin = new Vector2(0.5f, 1); trt.anchorMax = new Vector2(0.5f, 1);
                trt.pivot = new Vector2(0.5f, 1);
                trt.sizeDelta = new Vector2(100, 100);
                trt.anchoredPosition = new Vector2(0, -10);
            }
            var tImg = thumbGO.GetComponent<Image>();
            if (menuItem.thumbnail != null) { tImg.sprite = menuItem.thumbnail; tImg.color = Color.white; }
            else { tImg.color = new Color(0.9f, 0.9f, 0.95f); }

            // Name
            var nameGO = card.transform.Find("Name")?.gameObject;
            if (nameGO == null)
            {
                nameGO = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI));
                nameGO.transform.SetParent(card.transform, false);
                var nrt = nameGO.GetComponent<RectTransform>();
                nrt.anchorMin = new Vector2(0, 0.25f); nrt.anchorMax = new Vector2(1, 0.45f);
                nrt.offsetMin = new Vector2(5, 0); nrt.offsetMax = new Vector2(-5, 0);
            }
            var ntmp = nameGO.GetComponent<TextMeshProUGUI>();
            ntmp.text = menuItem.menuName; ntmp.fontSize = 28; ntmp.color = new Color(0.1f, 0.1f, 0.1f);
            ntmp.alignment = TextAlignmentOptions.Center;
            if (font != null) ntmp.font = font;

            // Price
            var priceGO = card.transform.Find("Price")?.gameObject;
            if (priceGO == null)
            {
                priceGO = new GameObject("Price", typeof(RectTransform), typeof(TextMeshProUGUI));
                priceGO.transform.SetParent(card.transform, false);
                var prt = priceGO.GetComponent<RectTransform>();
                prt.anchorMin = new Vector2(0, 0); prt.anchorMax = new Vector2(1, 0.25f);
                prt.offsetMin = new Vector2(5, 0); prt.offsetMax = new Vector2(-5, 0);
            }
            var ptmp = priceGO.GetComponent<TextMeshProUGUI>();
            ptmp.text = $"{menuItem.basePrice:N0}원"; ptmp.fontSize = 24;
            ptmp.color = COL_PRIMARY; ptmp.alignment = TextAlignmentOptions.Center;
            if (font != null) ptmp.font = font;
        }
    }
}
