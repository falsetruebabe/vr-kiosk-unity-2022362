using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Phase2.Core;

namespace Phase2.UI
{
    /// <summary>
    /// CartItemView: 장바구니 항목 1건을 표시하는 재사용 가능한 UI 프리팹 컴포넌트.
    /// CartItemPool에 의해 풀링되어 관리된다.
    /// </summary>
    public class CartItemView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI menuNameLabel;
        [SerializeField] private TextMeshProUGUI optionsLabel;
        [SerializeField] private TextMeshProUGUI priceLabel;
        [SerializeField] private TextMeshProUGUI quantityLabel;
        [SerializeField] private Button removeButton;

        private int _cartIndex;
        private CartManager _cartManager;

        // Awake에서 버튼 리스너 등록 – Update 내 호출 없음
        private void Awake()
        {
            removeButton.onClick.AddListener(OnRemoveClicked);
        }

        public void Bind(CartItem item, int index, CartManager manager)
        {
            _cartIndex  = index;
            _cartManager = manager;

            if (menuNameLabel != null) menuNameLabel.text = item.menuItem.menuName;
            if (priceLabel    != null) priceLabel.text    = $"{item.CalculateTotalPrice():N0}원";
            if (quantityLabel != null) quantityLabel.text = $"×{item.quantity}";

            if (optionsLabel != null)
            {
                var sb = new System.Text.StringBuilder();
                foreach (var opt in item.selectedOptions)
                {
                    if (sb.Length > 0) sb.Append(", ");
                    sb.Append(opt.optionLabel);
                }
                optionsLabel.text = sb.ToString();
            }
        }

        private void OnRemoveClicked() => _cartManager?.RemoveAt(_cartIndex);
    }

    // ===========================================================================

    /// <summary>
    /// CartItemPool: CartItemView 인스턴스를 Object Pooling으로 관리.
    /// CartManager.OnCartChanged 이벤트 수신 시 풀에서 꺼내거나 반납하여
    /// 장바구니 목록 UI를 갱신한다.
    /// - Update() 내 new/GetComponent 호출 없음 (agent.md 규칙 준수)
    /// </summary>
    public class CartItemPool : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        //  Serialized References
        // -----------------------------------------------------------------------
        [Header("Pool Settings")]
        [SerializeField, Tooltip("풀에서 사용하는 CartItemView 프리팹")]
        private CartItemView itemViewPrefab;

        [SerializeField, Tooltip("CartItemView들이 생성될 부모 컨테이너 (ScrollView Content)")]
        private Transform contentParent;

        [SerializeField, Tooltip("사전 생성할 풀 크기")]
        private int initialPoolSize = 8;

        [Header("Dependencies")]
        [SerializeField] private CartManager cartManager;

        // -----------------------------------------------------------------------
        //  Pool Internal State
        // -----------------------------------------------------------------------
        private readonly Stack<CartItemView> _pool   = new Stack<CartItemView>();
        private readonly List<CartItemView>  _active = new List<CartItemView>();

        // -----------------------------------------------------------------------
        //  Lifecycle
        // -----------------------------------------------------------------------
        private void Awake()
        {
            // 풀 사전 생성
            for (int i = 0; i < initialPoolSize; i++)
            {
                CartItemView view = CreateNewView();
                view.gameObject.SetActive(false);
                _pool.Push(view);
            }
        }

        private void OnEnable()  => CartManager.OnCartChanged += RefreshView;
        private void OnDisable() => CartManager.OnCartChanged -= RefreshView;

        // -----------------------------------------------------------------------
        //  Pool Operations
        // -----------------------------------------------------------------------
        private void RefreshView()
        {
            // 활성 뷰 전부 반납
            for (int i = _active.Count - 1; i >= 0; i--)
                Return(_active[i]);
            _active.Clear();

            // 카트 항목 수만큼 풀에서 꺼내 바인드
            IReadOnlyList<CartItem> items = cartManager.Items;
            for (int i = 0; i < items.Count; i++)
            {
                CartItemView view = Rent();
                view.Bind(items[i], i, cartManager);
                _active.Add(view);
            }
        }

        private CartItemView Rent()
        {
            CartItemView view = _pool.Count > 0 ? _pool.Pop() : CreateNewView();
            view.gameObject.SetActive(true);
            return view;
        }

        private void Return(CartItemView view)
        {
            view.gameObject.SetActive(false);
            _pool.Push(view);
        }

        private CartItemView CreateNewView()
        {
            CartItemView view = Instantiate(itemViewPrefab, contentParent);
            return view;
        }
    }
}
