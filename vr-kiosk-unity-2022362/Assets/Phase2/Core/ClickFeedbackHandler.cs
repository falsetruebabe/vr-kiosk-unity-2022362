using UnityEngine;
using UnityEngine.EventSystems;

namespace Phase2.Core
{
    /// <summary>
    /// Phase 2 클릭 피드백 핸들러.
    /// - PointerClick 시에만 사운드 재생 (호버링 피드백 없음 – Phase 2 규칙)
    /// - 버튼별로 부착 가능하도록 IPointerClickHandler 구현
    /// - Update() 내 GetComponent/new 호출 없음
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class ClickFeedbackHandler : MonoBehaviour, IPointerClickHandler
    {
        [Header("Sound")]
        [SerializeField, Tooltip("클릭 시 재생할 효과음")]
        private AudioClip clickSoundClip;

        // -----------------------------------------------------------------------
        //  Cached Components (Awake에서 1회만)
        // -----------------------------------------------------------------------
        private AudioSource _audioSource;

        // -----------------------------------------------------------------------
        //  Lifecycle
        // -----------------------------------------------------------------------
        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            _audioSource.playOnAwake = false;
        }

        // -----------------------------------------------------------------------
        //  IPointerClickHandler – 마우스 다운(클릭) 이벤트에만 반응
        // -----------------------------------------------------------------------
        public void OnPointerClick(PointerEventData eventData)
        {
            if (clickSoundClip == null || _audioSource == null) return;
            _audioSource.PlayOneShot(clickSoundClip);
        }
    }
}
