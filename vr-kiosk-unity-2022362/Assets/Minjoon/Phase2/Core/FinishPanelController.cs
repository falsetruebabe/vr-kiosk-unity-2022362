using System;
using System.Collections;
using UnityEngine;
using TMPro;

namespace Phase2.Core
{
    /// <summary>
    /// 결제 프로세스와 완료 화면(영수증 포함)을 담당합니다.
    /// BUG-09: 코루틴 참조 저장 및 상태 변경 시 StopCoroutine 호출.
    /// BUG-13: 영수증에 매장/포장 유형 표시.
    /// </summary>
    public class FinishPanelController
    {
        private readonly MonoBehaviour _coroutineHost;
        private readonly TextMeshProUGUI _paymentProgressLabel;
        private readonly TextMeshProUGUI _finishMessageLabel;
        private readonly TextMeshProUGUI _missionResultLabel;
        private readonly TextMeshProUGUI _receiptLabel;
        private readonly CartManager _cartManager;
        private readonly Action _onPaymentComplete;

        // BUG-09: 코루틴 참조 추적
        private Coroutine _paymentCoroutine;

        // BUG-12: 순차적 주문 번호
        private static int _orderCounter = 1;

        public FinishPanelController(
            MonoBehaviour coroutineHost,
            TextMeshProUGUI paymentProgressLabel,
            TextMeshProUGUI finishMessageLabel,
            TextMeshProUGUI missionResultLabel,
            TextMeshProUGUI receiptLabel,
            CartManager cartManager,
            Action onPaymentComplete)
        {
            _coroutineHost = coroutineHost;
            _paymentProgressLabel = paymentProgressLabel;
            _finishMessageLabel = finishMessageLabel;
            _missionResultLabel = missionResultLabel;
            _receiptLabel = receiptLabel;
            _cartManager = cartManager;
            _onPaymentComplete = onPaymentComplete;
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Payment Process (BUG-09: 안전한 코루틴 관리)
        // ═══════════════════════════════════════════════════════════════════
        public void StartPayment()
        {
            StopPayment(); // 기존 코루틴이 있으면 중단
            _paymentCoroutine = _coroutineHost.StartCoroutine(CoPayment());
        }

        /// <summary>
        /// BUG-09: 상태 전환 시 외부에서 호출하여 결제 코루틴을 안전하게 중단합니다.
        /// </summary>
        public void StopPayment()
        {
            if (_paymentCoroutine != null)
            {
                _coroutineHost.StopCoroutine(_paymentCoroutine);
                _paymentCoroutine = null;
            }
        }

        private IEnumerator CoPayment()
        {
            float duration = 2.5f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                if (_paymentProgressLabel != null)
                {
                    string dots = new string('.', (int)(elapsed * 4f) % 4);
                    _paymentProgressLabel.text = $"결제 처리 중{dots}\n{(duration - elapsed):F1}초";
                }
                yield return null;
            }

            _paymentCoroutine = null;
            _onPaymentComplete?.Invoke();
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Finish Panel (BUG-13: OrderType 표시)
        // ═══════════════════════════════════════════════════════════════════
        public void BuildFinish(OrderType orderType)
        {
            bool success = false;
            if (Mission.MissionManager.Instance != null && Mission.MissionManager.Instance.IsMissionActive)
                success = Mission.MissionManager.Instance.ValidateMission(_cartManager.Items);

            if (_finishMessageLabel != null)
                _finishMessageLabel.text = "주문이 완료되었습니다!";
            if (_missionResultLabel != null)
                _missionResultLabel.text = Mission.MissionManager.Instance != null && Mission.MissionManager.Instance.IsMissionActive
                    ? (success ? "<color=#2ecc71>[성공] 미션 달성!</color>" : "<color=#e74c3c>[실패] 미션 실패</color>")
                    : "";

            // FEAT-01: 영수증 텍스트 동적 생성 + BUG-13: 주문 유형 표시
            if (_receiptLabel != null && _cartManager != null)
            {
                var sb = new System.Text.StringBuilder(256);
                int orderNo = _orderCounter++; // BUG-12: 순차 증가 적용
                if (_orderCounter > 9999) _orderCounter = 1;

                sb.AppendLine($"주문번호: #{orderNo:D4}");

                string orderTypeStr = orderType == OrderType.DineIn ? "[매장]" : "[포장]";
                sb.AppendLine($"주문유형: {orderTypeStr}");

                sb.AppendLine("\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500");

                var items = _cartManager.Items;
                for (int i = 0; i < items.Count; i++)
                {
                    var ci = items[i];
                    sb.Append(ci.menuItem.menuName);

                    // 옵션 표시
                    if (ci.selectedOptions.Count > 0)
                    {
                        sb.Append("  (");
                        for (int j = 0; j < ci.selectedOptions.Count; j++)
                        {
                            if (j > 0) sb.Append(", ");
                            sb.Append(ci.selectedOptions[j].optionLabel);
                        }
                        sb.Append(")");
                    }

                    sb.Append($"  x{ci.quantity}");
                    sb.AppendLine($"    {ci.CalculateTotalPrice():N0}원");
                }

                sb.AppendLine("\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500");
                sb.Append($"총 결제금액: {_cartManager.TotalPrice:N0}원");

                _receiptLabel.text = sb.ToString();
            }
        }
    }
}
