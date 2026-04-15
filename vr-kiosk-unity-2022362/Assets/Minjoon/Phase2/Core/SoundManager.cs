using UnityEngine;

namespace Phase2.Core
{
    /// <summary>
    /// 공유 AudioSource를 보유한 중앙 사운드 매니저.
    /// 모든 UI 버튼이 개별 AudioSource를 가지는 대신
    /// 이 매니저의 단일 AudioSource를 공유하여 오버헤드를 제거한다.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class SoundManager : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        //  Singleton
        // -----------------------------------------------------------------------
        public static SoundManager Instance { get; private set; }

        // -----------------------------------------------------------------------
        //  Cached Components
        // -----------------------------------------------------------------------
        private AudioSource _audioSource;

        // -----------------------------------------------------------------------
        //  Lifecycle
        // -----------------------------------------------------------------------
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _audioSource = GetComponent<AudioSource>();
            _audioSource.playOnAwake = false;
        }

        // -----------------------------------------------------------------------
        //  Public API
        // -----------------------------------------------------------------------

        /// <summary>1회성 효과음을 재생합니다. clip이 null이면 무시합니다.</summary>
        public void PlayOneShot(AudioClip clip)
        {
            if (clip == null || _audioSource == null) return;
            _audioSource.PlayOneShot(clip);
        }

        // BUG-06: 씬 전환 시 싱글톤 참조 정리
        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
