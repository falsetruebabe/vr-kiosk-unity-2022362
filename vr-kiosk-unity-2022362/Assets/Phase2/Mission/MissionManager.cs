using System.Collections.Generic;
using UnityEngine;
using Phase2.Data;
using System.Text;

namespace Phase2.Mission
{
    public class MissionTarget 
    {
        public CafeMenuItem MenuItem;
        public List<CafeMenuOption> Options;
        public int Quantity;
    }

    /// <summary>
    /// Phase 2 다중 복합 미션 생성 및 역대급 엄격한 검증을 담당하는 싱글톤 매니저.
    /// - 음료 1~2종, 디저트 1종 랜덤 믹스
    /// - 수량 1~4랜덤
    /// - 자연스러운 한국어 문장 텍스트화
    /// </summary>
    public class MissionManager : MonoBehaviour
    {
        public static MissionManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public static event System.Action<string> OnMissionTextUpdated;
        public static event System.Action<bool> OnMissionValidated;

        public string CurrentMissionText { get; private set; }

        private List<MissionTarget> _activeMissions = new List<MissionTarget>();
        private CafeMenuDatabase _database;     // 힌트 위치 조회용 DB 참조 캐시
        public bool IsMissionActive => _activeMissions.Count > 0;

        /// <summary>힌트 시스템에서 미션 타겟 목록을 조회합니다.</summary>
        public IReadOnlyList<MissionTarget> GetMissionTargets() => _activeMissions;

        /// <summary>힌트 시스템에서 카테고리 탭 조회용 DB를 참조합니다.</summary>
        public CafeMenuDatabase GetDatabase() => _database;

        /// <summary>
        /// 카페 메뉴 DB에서 음료(1~2종)와 디저트(1종), 옵션, 수량(1~4)을 무작위로 추출하여 미션을 생성합니다.
        /// </summary>
        public void GenerateRandomMission(CafeMenuDatabase db)
        {
            _activeMissions.Clear();
            _database = db;  // 힌트 위치 조회용 캐시
            if (db == null || db.categories.Count == 0) return;

            // 1. 모든 아이템을 음료 / 디저트 풀로 분리
            List<CafeMenuItem> drinksPool = new List<CafeMenuItem>();
            List<CafeMenuItem> dessertsPool = new List<CafeMenuItem>();

            foreach (var cat in db.categories)
            {
                if (cat.categoryName.Contains("디저트") || cat.categoryName.Contains("빵") || cat.categoryName.Contains("케이크"))
                    dessertsPool.AddRange(cat.items);
                else
                    drinksPool.AddRange(cat.items); // 그 외는 음료
            }

            // 2. 음료 1~2종 무작위 픽업
            int drinkCount = Random.Range(1, 3); // 1 or 2
            ShuffleList(drinksPool);
            for (int i = 0; i < Mathf.Min(drinkCount, drinksPool.Count); i++)
            {
                _activeMissions.Add(CreateRandomTarget(drinksPool[i]));
            }

            // 3. 디저트 1종 무작위 픽업
            if (dessertsPool.Count > 0)
            {
                ShuffleList(dessertsPool);
                _activeMissions.Add(CreateRandomTarget(dessertsPool[0]));
            }

            // 4. 텍스트 합성 및 UI 갱신
            CurrentMissionText = BuildNaturalMissionText();
            OnMissionTextUpdated?.Invoke(CurrentMissionText);

            Debug.Log($"[MissionManager] 무작위 복합 미션 생성: {CurrentMissionText}");
        }

        private MissionTarget CreateRandomTarget(CafeMenuItem item)
        {
            var target = new MissionTarget();
            target.MenuItem = item;
            target.Quantity = Random.Range(1, 5); // 1~4
            target.Options = new List<CafeMenuOption>();

            if (item.availableOptions != null && item.availableOptions.Length > 0)
            {
                // 옵션 카테고리별로 그룹화
                var groupedOpts = new Dictionary<OptionCategoryType, List<CafeMenuOption>>();
                foreach(var opt in item.availableOptions)
                {
                    if (opt.category == OptionCategoryType.NONE) continue;
                    if (!groupedOpts.ContainsKey(opt.category)) groupedOpts[opt.category] = new List<CafeMenuOption>();
                    groupedOpts[opt.category].Add(opt);
                }

                // 각 카테고리별로 랜덤하게 1개씩 강제 옵션 지정
                foreach (var kvp in groupedOpts)
                {
                    var optList = kvp.Value;
                    target.Options.Add(optList[Random.Range(0, optList.Count)]);
                }
            }
            return target;
        }

        private void ShuffleList<T>(List<T> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                T temp = list[i];
                int randomIndex = Random.Range(i, list.Count);
                list[i] = list[randomIndex];
                list[randomIndex] = temp;
            }
        }

        /// <summary>
        /// 장바구니 내역이 미션 목표와 수량, 옵션 등 단 1개의 오차도 없이 1:1로 일치해야만 성공 반환
        /// </summary>
        public bool ValidateMission(IReadOnlyList<Core.CartItem> finalOrder)
        {
            if (!IsMissionActive) return false;

            // 카트 정보를 통합 (같은 메뉴, 같은 옵션을 2번에 나눠 담은 경우 대응)
            List<MissionTarget> cartSummary = DistillCart(finalOrder);

            // 종류 불일치시 즉시 실패 (초가 주문, 누락 불허용)
            if (cartSummary.Count != _activeMissions.Count) 
            {
                Debug.Log($"[MissionManager] 미션 실패: 항목 개수 불일치 (미션:{_activeMissions.Count}개, 장바구니:{cartSummary.Count}개)");
                OnMissionValidated?.Invoke(false);
                return false;
            }

            // 모든 미션 타겟이 카트에 정확한 옵션과 수량으로 존재하는지 검사
            foreach (var mission in _activeMissions)
            {
                var matchedCart = cartSummary.Find(c => 
                    c.MenuItem.menuId == mission.MenuItem.menuId &&
                    OptionsMatch(c.Options, mission.Options));

                if (matchedCart == null)
                {
                    Debug.Log($"[MissionManager] 미션 실패: {mission.MenuItem.menuName} 항목 또는 옵션 누락");
                    OnMissionValidated?.Invoke(false);
                    return false;
                }

                if (matchedCart.Quantity != mission.Quantity)
                {
                    Debug.Log($"[MissionManager] 미션 실패: {mission.MenuItem.menuName} 수량 불일치 (기대:{mission.Quantity}, 싲제:{matchedCart.Quantity})");
                    OnMissionValidated?.Invoke(false);
                    return false;
                }
            }

            // 완전 일치
            OnMissionValidated?.Invoke(true);
            return true;
        }

        private List<MissionTarget> DistillCart(IReadOnlyList<Core.CartItem> finalOrder)
        {
            var summary = new List<MissionTarget>();
            foreach (var item in finalOrder)
            {
                var existing = summary.Find(s => s.MenuItem.menuId == item.menuItem.menuId && OptionsMatch(s.Options, item.selectedOptions));
                if (existing != null)
                {
                    existing.Quantity += item.quantity;
                }
                else
                {
                    summary.Add(new MissionTarget 
                    {
                        MenuItem = item.menuItem,
                        Options = new List<CafeMenuOption>(item.selectedOptions),
                        Quantity = item.quantity
                    });
                }
            }
            return summary;
        }

        private bool OptionsMatch(List<CafeMenuOption> a, List<CafeMenuOption> b)
        {
            if (a.Count != b.Count) return false;
            foreach (var optB in b)
            {
                if (a.Find(optA => optA.optionId == optB.optionId) == null) return false;
            }
            return true;
        }

        private string BuildNaturalMissionText()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("──── 주문서 ────");
            
            for (int i = 0; i < _activeMissions.Count; i++)
            {
                var t = _activeMissions[i];
                sb.Append($"{i + 1}. {t.MenuItem.menuName}");
                
                foreach (var opt in t.Options)
                {
                    sb.Append($" {opt.optionLabel}");
                }

                // 음료vs디저트 별 단위 명사 (카테고리 필드 + 이름 기반 이중 체크)
                bool isDessert = false;
                string cat = t.MenuItem.category ?? "";
                string mName = t.MenuItem.menuName ?? "";
                if (cat.Contains("디저트") || cat.Contains("빵") || cat.Contains("케이크")
                    || mName.Contains("케이크") || mName.Contains("스콘") || mName.Contains("베이글")
                    || mName.Contains("빵") || mName.Contains("쿠키") || mName.Contains("타르트")
                    || mName.Contains("크로와상") || mName.Contains("머핀")
                    || (t.MenuItem.availableOptions != null && t.MenuItem.availableOptions.Length == 0))
                {
                    isDessert = true;
                }
                string unit = isDessert ? "개" : "잔";
                sb.Append($"  {t.Quantity}{unit}");

                if (i < _activeMissions.Count - 1)
                    sb.AppendLine();
            }
            
            sb.AppendLine();
            sb.Append("────────────");
            return sb.ToString();
        }

        // 종성(받침) 유무 확인
        private bool HasJongseong(char c)
        {
            if (c >= 0xAC00 && c <= 0xD7A3) return (c - 0xAC00) % 28 > 0;
            return false;
        }
    }
}
