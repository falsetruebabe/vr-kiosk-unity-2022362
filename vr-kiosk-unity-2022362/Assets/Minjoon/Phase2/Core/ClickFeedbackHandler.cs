using UnityEngine;
using UnityEngine.EventSystems;

namespace Phase2.Core
{
    /// <summary>
    /// Phase 2 클릭 피드백 핸들러.
    /// - PointerClick 시에만 사운드 재생
    /// - 개별 AudioSource 대신 SoundManager의 공유 AudioSource 사용
    /// - Update() 내 GetComponent/new 호출 없음
    /// </summary>
    public class ClickFeedbackHandler : MonoBehaviour, IPointerClickHandler
    {
        [Header("Sound")]
        [SerializeField, Tooltip("클릭 시 재생할 효과음")]
        private AudioClip clickSoundClip;

        // -----------------------------------------------------------------------
        //  IPointerClickHandler – 마우스 다운(클릭) 이벤트에만 반응
        // -----------------------------------------------------------------------
        public void OnPointerClick(PointerEventData eventData)
        {
            if (clickSoundClip == null) return;
            if (SoundManager.Instance != null)
                SoundManager.Instance.PlayOneShot(clickSoundClip);
        }
    }
}
