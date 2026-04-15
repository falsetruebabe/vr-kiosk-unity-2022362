using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Common
{

/// <summary>
/// 범용 확인/취소 팝업 컨트롤러.
/// 종료 팝업, 주문 취소 확인 등 재사용 가능.
/// MonoBehaviour가 아닌 순수 C# 클래스로, CanvasGroup 기반 표시/숨김을 제어한다.
/// </summary>
public class ConfirmPopupController
{
    private readonly CanvasGroup _popupGroup;
    private readonly Button _confirmButton;
    private readonly Button _cancelButton;

    private System.Action _onConfirm;

    public ConfirmPopupController(CanvasGroup popupGroup, Button confirmButton, Button cancelButton)
    {
        _popupGroup = popupGroup;
        _confirmButton = confirmButton;
        _cancelButton = cancelButton;

        _confirmButton?.onClick.AddListener(OnConfirm);
        _cancelButton?.onClick.AddListener(Hide);

        Hide();
    }

    /// <summary>팝업을 표시합니다. 확인 버튼 클릭 시 onConfirm 콜백이 실행됩니다.</summary>
    public void Show(System.Action onConfirm)
    {
        _onConfirm = onConfirm;
        SetGroup(true);
    }

    public void Hide()
    {
        _onConfirm = null;
        SetGroup(false);
    }

    private void OnConfirm()
    {
        var callback = _onConfirm;
        Hide();
        callback?.Invoke();
    }

    private void SetGroup(bool visible)
    {
        if (_popupGroup == null) return;
        _popupGroup.alpha = visible ? 1f : 0f;
        _popupGroup.interactable = visible;
        _popupGroup.blocksRaycasts = visible;
    }
    }
}
