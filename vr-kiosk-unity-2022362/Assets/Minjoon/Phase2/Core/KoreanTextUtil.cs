namespace Phase2.Core
{
    /// <summary>
    /// 한국어 텍스트 처리 공통 유틸리티.
    /// 종성(받침) 유무 판별 등 한국어 문법 처리를 중앙에서 관리한다.
    /// BUG-01: MissionManager, IdleTimeTracker 중복 코드를 통합.
    /// </summary>
    public static class KoreanTextUtil
    {
        /// <summary>
        /// 한글 문자의 종성(받침) 유무를 판별합니다.
        /// 은/는, 이/가, 을/를 등 조사 분기에 사용됩니다.
        /// </summary>
        public static bool HasJongseong(char c)
        {
            if (c >= 0xAC00 && c <= 0xD7A3)
                return (c - 0xAC00) % 28 > 0;
            return false;
        }
    }
}
