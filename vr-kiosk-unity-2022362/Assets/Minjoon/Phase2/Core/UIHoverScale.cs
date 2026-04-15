using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Phase2.Core
{
    /// <summary>
    /// 포인터 진입 시 1.08x 확대 + 주황 테두리 활성화, 이탈 시 복귀.
    /// - Update 내 new/GetComponent 호출 없음 (agent.md 규칙 준수)
    /// - OnEnable에서 스케일 리셋하여 풀링 재활용 시 잔류 방지
    /// </summary>
    public class UIHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private float hoverScale = 1.08f;
        [SerializeField] private float lerpSpeed = 10f;

        private Vector3 _originalScale;
        private Vector3 _targetScale;
        private Transform _transform;
        private Outline _outline;

        private void Awake()
        {
            _transform = transform;
            _originalScale = _transform.localScale;
            _targetScale = _originalScale;

            // 주황색 테두리 Outline 컴포넌트 자동 설정
            _outline = GetComponent<Outline>();
            if (_outline == null) _outline = gameObject.AddComponent<Outline>();
            _outline.effectColor = UIPoolHelper.COL_PRIMARY; // 주황/다홍 테마색
            _outline.effectDistance = new Vector2(4, 4);
            _outline.enabled = false;
        }

        private void OnEnable()
        {
            // 풀링 재활용 시 스케일/테두리 잔류 방지
            if (_transform != null)
            {
                _targetScale = _originalScale;
                _transform.localScale = _originalScale;
            }
            if (_outline != null)
                _outline.enabled = false;
        }

        private void Update()
        {
            // 부드러운 스케일 전환 (Vector3.Lerp — GC-free)
            if (Vector3.Distance(_transform.localScale, _targetScale) > 0.001f)
                _transform.localScale = Vector3.Lerp(
                    _transform.localScale, _targetScale, lerpSpeed * Time.deltaTime);
            else
                _transform.localScale = _targetScale;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _targetScale = _originalScale * hoverScale;
            if (_outline != null) _outline.enabled = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _targetScale = _originalScale;
            if (_outline != null) _outline.enabled = false;
        }
    }
}
