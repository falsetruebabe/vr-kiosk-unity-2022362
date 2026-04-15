using System.Collections.Generic;
using UnityEngine;
using Phase2.Data;

namespace Phase2.Core
{
    /// <summary>
    /// 장바구니에 담긴 단일 항목 – 메뉴 + 선택된 옵션 조합.
    /// </summary>
    [System.Serializable]
    public class CartItem
    {
        public CafeMenuItem menuItem;
        public List<CafeMenuOption> selectedOptions;
        public int quantity;

        public CartItem(CafeMenuItem item, List<CafeMenuOption> options, int qty = 1)
        {
            menuItem = item;
            selectedOptions = new List<CafeMenuOption>(options);
            quantity = qty;
        }

        /// <summary>최종 가격 = (기본가 + 옵션 합산) × 수량</summary>
        public int CalculateTotalPrice()
        {
            int total = menuItem.basePrice;
            foreach (var opt in selectedOptions)
                total += opt.additionalPrice;
            return total * quantity;
        }
    }

    /// <summary>
    /// 장바구니 데이터를 관리하는 핵심 비즈니스 로직 클래스.
    /// - Update() 내 new/GetComponent 호출 금지 (agent.md 규칙 준수)
    /// - 이벤트 기반으로 UI 컴포넌트에 변경 사항을 전파한다.
    /// </summary>
    public class CartManager : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        //  Singleton
        // -----------------------------------------------------------------------
        public static CartManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        // -----------------------------------------------------------------------
        //  Events
        // -----------------------------------------------------------------------
        public static event System.Action OnCartChanged;

        // BUG-06: 씬 전환 시 정적 이벤트 구독 정리 (싱글톤 인스턴스만 정리)
        private void OnDestroy()
        {
            if (Instance == this)
            {
                OnCartChanged = null;
                Instance = null;
            }
        }

        // -----------------------------------------------------------------------
        //  Internal State
        // -----------------------------------------------------------------------
        private readonly List<CartItem> _items = new List<CartItem>(8);

        // -----------------------------------------------------------------------
        //  Public Read-Only Access
        // -----------------------------------------------------------------------
        public const int MAX_CART_SLOTS = 10;
        public const int MAX_ITEM_QTY = 99;

        public IReadOnlyList<CartItem> Items => _items;

        public int TotalItemCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _items.Count; i++)
                    count += _items[i].quantity;
                return count;
            }
        }

        public int TotalPrice
        {
            get
            {
                int total = 0;
                for (int i = 0; i < _items.Count; i++)
                    total += _items[i].CalculateTotalPrice();
                return total;
            }
        }

        // -----------------------------------------------------------------------
        //  Public Mutators
        // -----------------------------------------------------------------------

        /// <summary>메뉴를 장바구니에 추가합니다. (수량 제한 로직 포함)</summary>
        public void AddItem(CafeMenuItem item, List<CafeMenuOption> options)
        {
            // 동일한 메뉴+옵션 조합이 이미 있으면 수량 증가
            for (int i = 0; i < _items.Count; i++)
            {
                if (IsSameCombination(_items[i], item, options))
                {
                    if (_items[i].quantity < MAX_ITEM_QTY) 
                    {
                        _items[i].quantity++;
                        OnCartChanged?.Invoke();
                    }
                    else
                    {
                        Debug.LogWarning("[CartManager] 단일 항목의 최대 구매 가능 수량(99개)을 초과할 수 없습니다.");
                    }
                    return;
                }
            }

            if (_items.Count >= MAX_CART_SLOTS)
            {
                Debug.LogWarning($"[CartManager] 장바구니가 꽉 찼습니다 (최대 {MAX_CART_SLOTS}종류).");
                return;
            }

            _items.Add(new CartItem(item, options, 1));
            OnCartChanged?.Invoke();
        }

        /// <summary>인덱스로 항목을 제거합니다.</summary>
        public void RemoveAt(int index)
        {
            if (index < 0 || index >= _items.Count) return;
            _items.RemoveAt(index);
            OnCartChanged?.Invoke();
        }

        /// <summary>장바구니 전체를 비웁니다.</summary>
        public void Clear()
        {
            _items.Clear();
            OnCartChanged?.Invoke();
        }

        /// <summary>특정 인덱스 항목의 수량을 delta만큼 조정합니다. 0 이하가 되면 자동 삭제.</summary>
        public void AdjustQuantity(int index, int delta)
        {
            if (index < 0 || index >= _items.Count) return;
            
            int newQty = _items[index].quantity + delta;
            if (newQty > MAX_ITEM_QTY) 
            {
                Debug.LogWarning("[CartManager] 최대 구매 수량을 초과할 수 없습니다.");
                newQty = MAX_ITEM_QTY;
            }

            if (newQty <= 0)
            {
                _items.RemoveAt(index);
                OnCartChanged?.Invoke();
                return;
            }

            _items[index].quantity = newQty;
            OnCartChanged?.Invoke();
        }

        // -----------------------------------------------------------------------
        //  Private Helpers
        // -----------------------------------------------------------------------

        private bool IsSameCombination(CartItem existing,
                                       CafeMenuItem newItem,
                                       List<CafeMenuOption> newOptions)
        {
            if (existing.menuItem.menuId != newItem.menuId) return false;
            if (existing.selectedOptions.Count != newOptions.Count) return false;

            // 순서 독립 비교: optionId 기반으로 모든 옵션이 일치하는지 확인
            foreach (var newOpt in newOptions)
            {
                bool found = false;
                for (int j = 0; j < existing.selectedOptions.Count; j++)
                {
                    if (existing.selectedOptions[j].optionId == newOpt.optionId)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found) return false;
            }
            return true;
        }
    }
}
