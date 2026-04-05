using UnityEngine;
using UnityEditor;
using Phase2.Data;
using System.Collections.Generic;

namespace Phase2.EditorScripts
{
    public class MenuDataUpdater
    {
        [MenuItem("Tools/Kiosk Menu Setup")]
        public static void CreateAndSetupMenus()
        {
            string dbPath = "Assets/Phase2/Data/SampleData/CafeMenuDatabase.asset";
            CafeMenuDatabase db = AssetDatabase.LoadAssetAtPath<CafeMenuDatabase>(dbPath);

            if (db == null)
            {
                Debug.LogError($"[MenuDataUpdater] Database 못 찾음: {dbPath}. 경로를 확인하세요.");
                return;
            }

            string targetFolder = "Assets/Phase2/Data/SampleData";
            
            // 1. 공통 옵션 에셋 생성 (가장 중요)
            var optHot = CreateOption(targetFolder, "hot", "HOT", OptionCategoryType.TEMPERATURE, 0);
            var optIce = CreateOption(targetFolder, "ice", "ICE", OptionCategoryType.TEMPERATURE, 0);
            
            var optReg = CreateOption(targetFolder, "size_reg", "레귤러", OptionCategoryType.SIZE, 0);
            var optLarge = CreateOption(targetFolder, "size_large", "라지", OptionCategoryType.SIZE, 500);

            // 커피용 농도
            var optSoft = CreateOption(targetFolder, "den_soft", "연하게", OptionCategoryType.DENSITY, 0);
            var optShot = CreateOption(targetFolder, "den_shot", "샷추가", OptionCategoryType.DENSITY, 500);
            
            // 공통/논커피용 농도
            var optDefault = CreateOption(targetFolder, "den_default", "기본", OptionCategoryType.DENSITY, 0);
            var optLessSweet = CreateOption(targetFolder, "den_less", "덜달게", OptionCategoryType.DENSITY, 0);

            var coffeeOpts = new CafeMenuOption[] { optHot, optIce, optReg, optLarge, optSoft, optDefault, optShot };
            var adeOpts = new CafeMenuOption[] { optReg, optLarge, optLessSweet, optDefault };
            var hotChocoOpts = new CafeMenuOption[] { optHot, optIce, optReg, optLarge, optLessSweet, optDefault };

            db.categories.Clear();

            // 2. 커피 (기존 아메리카노 등도 강제 매핑 덮어쓰기)
            var coffeeCat = EnsureCategory(db, "커피");
            CreateMenuItem(db, coffeeCat, targetFolder, "아메리카노", "americano", 4500, "커피", coffeeOpts);
            CreateMenuItem(db, coffeeCat, targetFolder, "카페라떼", "cafelatte", 5000, "커피", coffeeOpts);
            CreateMenuItem(db, coffeeCat, targetFolder, "바닐라라떼", "vanilla", 6000, "커피", coffeeOpts);
            CreateMenuItem(db, coffeeCat, targetFolder, "카라멜마끼아또", "caramel", 6000, "커피", coffeeOpts);

            // 3. 논커피 (우유 들어간 핫 가능 / 에이드 핫 불가)
            var nonCoffeeCat = EnsureCategory(db, "논커피");
            CreateMenuItem(db, nonCoffeeCat, targetFolder, "초코라떼", "choco", 6000, "논커피", hotChocoOpts); // 온도가 있음
            CreateMenuItem(db, nonCoffeeCat, targetFolder, "녹차라떼", "greentea", 6000, "논커피", hotChocoOpts);
            CreateMenuItem(db, nonCoffeeCat, targetFolder, "레몬 에이드", "lemon_ade", 5000, "논커피", adeOpts); // 온도가 아예 없음
            CreateMenuItem(db, nonCoffeeCat, targetFolder, "자몽 에이드", "grapefruit_ade", 5000, "논커피", adeOpts);
            CreateMenuItem(db, nonCoffeeCat, targetFolder, "청포도 에이드", "greengrape_ade", 5000, "논커피", adeOpts);

            // 4. 빵 / 케이크 (옵션 없음)
            var breadCat = EnsureCategory(db, "빵");
            CreateMenuItem(db, breadCat, targetFolder, "소금빵", "saltbread", 3500, "빵", new CafeMenuOption[0]);
            CreateMenuItem(db, breadCat, targetFolder, "스콘", "scone", 3000, "빵", new CafeMenuOption[0]);
            CreateMenuItem(db, breadCat, targetFolder, "크루아상", "croissant", 3500, "빵", new CafeMenuOption[0]);
            CreateMenuItem(db, breadCat, targetFolder, "베이글", "bagel", 4000, "빵", new CafeMenuOption[0]);

            var cakeCat = EnsureCategory(db, "케이크");
            CreateMenuItem(db, cakeCat, targetFolder, "딸기 케이크", "strawberry_cake", 6500, "케이크", new CafeMenuOption[0]);
            CreateMenuItem(db, cakeCat, targetFolder, "초코 케이크", "choco_cake", 6000, "케이크", new CafeMenuOption[0]);
            CreateMenuItem(db, cakeCat, targetFolder, "치즈 케이크", "cheese_cake", 6000, "케이크", new CafeMenuOption[0]);

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[MenuDataUpdater] 메뉴 및 옵션(온도/사이즈/농도) 제분배가 완료되었습니다!");
        }

        private static CafeMenuOption CreateOption(string folder, string id, string label, OptionCategoryType cat, int price)
        {
            string path = $"{folder}/{id}.asset";
            CafeMenuOption opt = AssetDatabase.LoadAssetAtPath<CafeMenuOption>(path);
            if (opt == null)
            {
                opt = ScriptableObject.CreateInstance<CafeMenuOption>();
                AssetDatabase.CreateAsset(opt, path);
            }
            opt.optionId = id;
            opt.optionLabel = label;
            opt.category = cat;
            opt.additionalPrice = price;
            EditorUtility.SetDirty(opt);
            return opt;
        }

        private static CafeMenuDatabase.MenuCategory EnsureCategory(CafeMenuDatabase db, string catName)
        {
            foreach (var c in db.categories)
            {
                if (c.categoryName == catName) return c;
            }
            var newCat = new CafeMenuDatabase.MenuCategory { categoryName = catName, items = new List<CafeMenuItem>() };
            db.categories.Add(newCat);
            return newCat;
        }

        private static void CreateMenuItem(CafeMenuDatabase db, CafeMenuDatabase.MenuCategory cat, string folderPath, string mName, string mId, int mPrice, string mCatName, CafeMenuOption[] options)
        {
            string assetPath = $"{folderPath}/{mId}.asset";
            CafeMenuItem existingAsset = AssetDatabase.LoadAssetAtPath<CafeMenuItem>(assetPath);

            if (existingAsset == null)
            {
                existingAsset = ScriptableObject.CreateInstance<CafeMenuItem>();
                AssetDatabase.CreateAsset(existingAsset, assetPath);
            }

            existingAsset.menuName = mName;
            existingAsset.menuId = mId;
            existingAsset.basePrice = mPrice;
            existingAsset.category = mCatName;
            existingAsset.availableOptions = options; 

            EditorUtility.SetDirty(existingAsset);
            cat.items.Add(existingAsset);
        }
    }
}
