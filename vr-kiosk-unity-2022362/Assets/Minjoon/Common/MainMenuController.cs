using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace Common
{

/// <summary>
/// 메인화면 컨트롤러.
/// 시작하기 → 난이도 선택 씬 이동
/// 종료하기 → 확인 팝업 → Application.Quit()
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button quitButton;

    [Header("Confirm Popup")]
    [SerializeField] private CanvasGroup confirmPopupGroup;
    [SerializeField] private Button confirmYesButton;
    [SerializeField] private Button confirmNoButton;

    private ConfirmPopupController _confirmPopup;

    private void Awake()
    {
        _confirmPopup = new ConfirmPopupController(confirmPopupGroup, confirmYesButton, confirmNoButton);

        startButton?.onClick.AddListener(() =>
        {
            SceneManager.LoadScene("Scene_DifficultySelect");
        });

        quitButton?.onClick.AddListener(() =>
        {
            _confirmPopup.Show(() =>
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            });
        });
    }
    }
}
