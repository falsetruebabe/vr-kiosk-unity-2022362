using UnityEngine;
using UnityEditor;
using Phase2.Data;
using System.Collections.Generic;

namespace Phase2.EditorScripts
{
    public class MenuDataUpdater
    {
        /// <summary>
        /// DB를 로드하거나 새로 생성하고, 전체 메뉴/옵션 데이터를 구성한 뒤 반환합니다.
        /// Phase2UIBuilder에서도 호출하여 데이터 소스를 통합합니다 (BUG-03).
        /// 카테고리: 커피/논커피/빵/케이크 (BUG-04: "디저트" 카테고리 제거).
        /// ICE 가격: 0원으로 통일 (BUG-02).
        /// </summary>
        public static CafeMenuDatabase EnsureDatabase()
        {
            string targetFolder = "Assets/Phase2/Data/SampleData";
            string dbPath = $"{targetFolder}/CafeMenuDatabase.asset";

            // 폴더 보장
            if (!AssetDatabase.IsValidFolder("Assets/Phase2/Data"))
                AssetDatabase.CreateFolder("Assets/Phase2", "Data");
            if (!AssetDatabase.IsValidFolder(targetFolder))
                AssetDatabase.CreateFolder("Assets/Phase2/Data", "SampleData");

            CafeMenuDatabase db = AssetDatabase.LoadAssetAtPath<CafeMenuDatabase>(dbPath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<CafeMenuDatabase>();
                AssetDatabase.CreateAsset(db, dbPath);
            }

            PopulateDatabase(db, targetFolder);

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[MenuDataUpdater] 메뉴 데이터베이스 구성 완료!");
            return db;
        }

        [MenuItem("Tools/Kiosk Menu Setup")]
        public static void CreateAndSetupMenus()
        {
            EnsureDatabase();
        }

        private static void PopulateDatabase(CafeMenuDatabase db, string targetFolder)
        {
            // 1. 공통 옵션 에셋 생성 (기본 프리셋)
            var optHot = CreateOption(targetFolder, "hot", "HOT", OptionCategoryType.TEMPERATURE, 0);
            var optIce = CreateOption(targetFolder, "ice", "ICE", OptionCategoryType.TEMPERATURE, 0); 
            
            var optReg = CreateOption(targetFolder, "size_reg", "레귤러", OptionCategoryType.SIZE, 0);
            var optLarge = CreateOption(targetFolder, "size_large", "라지", OptionCategoryType.SIZE, 500);

            // 커피용 농도
            var optSoft = CreateOption(targetFolder, "den_soft", "연하게", OptionCategoryType.DENSITY, 0);
            var optShot = CreateOption(targetFolder, "den_shot", "샷추가", OptionCategoryType.DENSITY, 500);
            
            // 논커피용 농도
            var optDefault = CreateOption(targetFolder, "den_default", "기본", OptionCategoryType.DENSITY, 0);
            var optLessSweet = CreateOption(targetFolder, "den_less", "덜달게", OptionCategoryType.DENSITY, 0);

            var coffeeOpts = new CafeMenuOption[] { optHot, optIce, optReg, optLarge, optSoft, optShot };
            var adeOpts = new CafeMenuOption[] { optReg, optLarge, optLessSweet, optDefault };
            var hotChocoOpts = new CafeMenuOption[] { optHot, optIce, optReg, optLarge, optLessSweet, optDefault };
            var noOpts = new CafeMenuOption[0];

            db.categories.Clear();

            // 2. 외부 CSV 파일로부터 동적 파싱 (Data-Driven Architecture)
            string csvPath = "Assets/Phase2/Data/MenuDB.csv";
            TextAsset csvData = AssetDatabase.LoadAssetAtPath<TextAsset>(csvPath);
            
            if (csvData == null)
            {
                Debug.LogError($"[MenuDataUpdater] CSV 파일을 찾을 수 없습니다: {csvPath}");
                return;
            }

            string[] lines = csvData.text.Split('\n');
            for (int i = 1; i < lines.Length; i++) // 인덱스 1부터 시작하여 헤더(0) 생략
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                string[] cols = line.Split(',');
                if (cols.Length < 5) continue;

                string catName = cols[0].Trim();
                string menuName = cols[1].Trim();
                string menuId = cols[2].Trim();
                int price = 0;
                int.TryParse(cols[3].Trim(), out price);
                string optsType = cols[4].Trim().ToLower();

                var category = EnsureCategory(db, catName);
                
                CafeMenuOption[] selectedOpts = noOpts;
                if (optsType == "coffee") selectedOpts = coffeeOpts;
                else if (optsType == "ade") selectedOpts = adeOpts;
                else if (optsType == "hotchoco") selectedOpts = hotChocoOpts;

                CreateMenuItem(db, category, targetFolder, menuName, menuId, price, catName, selectedOpts);
            }
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
            
            // 썸네일 자동 연동 (Texture Type을 Sprite로 변경 후 할당)
            string[] texExts = { ".jpg", ".png" };
            foreach (var ext in texExts)
            {
                string texPath = $"Assets/Phase2/Textures/{mId}{ext}";
                if (System.IO.File.Exists(texPath))
                {
                    TextureImporter importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
                    if (importer != null && importer.textureType != TextureImporterType.Sprite)
                    {
                        importer.textureType = TextureImporterType.Sprite;
                        importer.SaveAndReimport();
                    }
                    existingAsset.thumbnail = AssetDatabase.LoadAssetAtPath<Sprite>(texPath);
                    break;
                }
            }

            EditorUtility.SetDirty(existingAsset);
            cat.items.Add(existingAsset);
        }
    }
}
