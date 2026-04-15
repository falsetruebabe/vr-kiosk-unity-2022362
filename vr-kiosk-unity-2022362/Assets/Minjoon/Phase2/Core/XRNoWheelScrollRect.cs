namespace Phase2.Core
{
    /// <summary>
    /// 마우스 휠 스크롤 이벤트를 완전히 무시(삭제)하는 XR 전용 커스텀 ScrollRect.
    /// KioskUIController에서 분리된 독립 클래스.
    /// </summary>
    public class XRNoWheelScrollRect : UnityEngine.UI.ScrollRect
    {
        public override void OnScroll(UnityEngine.EventSystems.PointerEventData data)
        {
            // 마우스 휠 이벤트가 들어와도 부모 로직을 호출하지 않고 소멸시킴
        }
    }
}
