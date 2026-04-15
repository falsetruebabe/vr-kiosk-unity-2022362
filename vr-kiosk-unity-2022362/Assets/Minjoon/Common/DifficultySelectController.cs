using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Common
{

/// <summary>
/// 난이도 선택 화면 컨트롤러.
/// 1단계/3단계: 무기능 (클릭 피드백만)
/// 2단계: FrameWork01 씬 로드
/// 뒤로가기: 메인화면으로 이동
/// </summary>
public class DifficultySelectController : MonoBehaviour
{
    [Header("Difficulty Buttons")]
    [SerializeField] private Button phase1Button;
    [SerializeField] private Button phase2Button;
    [SerializeField] private Button phase3Button;

    [Header("Navigation")]
    [SerializeField] private Button backButton;

    private void Awake()
    {
        // Phase 1: 무기능 (추후 구현 예정)
        // ClickFeedbackHandler가 부착되어 있어 클릭 시 시각 피드백은 제공됨

        // Phase 2: FrameWork01 씬 로드
        phase2Button?.onClick.AddListener(() =>
        {
            SceneManager.LoadScene("FrameWork01");
        });

        // Phase 3: 무기능 (추후 구현 예정)

        // 뒤로가기: 메인화면으로
        backButton?.onClick.AddListener(() =>
        {
            SceneManager.LoadScene("Scene_Main");
        });
    }
    }
}
