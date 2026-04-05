#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using TMPro;
using System.IO;

namespace Phase2.Editor
{
    /// <summary>
    /// Windows 시스템 한글 폰트(맑은 고딕)를 TMP Dynamic SDF 폰트로 변환하고
    /// TMP 기본 폰트 + 씬 내 모든 TMP 컴포넌트에 자동 적용하는 에디터 도구.
    /// </summary>
    public static class Phase2FontSetup
    {
        private const string FONT_DIR  = "Assets/Phase2/Fonts";
        private const string TTF_PATH  = FONT_DIR + "/MalgunGothic.ttf";
        private const string SDF_PATH  = FONT_DIR + "/MalgunGothic SDF.asset";
        private const string SYS_FONT  = "C:/Windows/Fonts/malgun.ttf";

        [MenuItem("Tools/Setup Korean Font")]
        public static void Setup()
        {
            // ── 1. 폴더 확인 ─────────────────────────────────────────────
            if (!AssetDatabase.IsValidFolder("Assets/Phase2/Fonts"))
                AssetDatabase.CreateFolder("Assets/Phase2", "Fonts");

            // ── 2. 시스템 폰트 복사 ──────────────────────────────────────
            if (!File.Exists(SYS_FONT))
            {
                EditorUtility.DisplayDialog("Font Not Found",
                    "맑은 고딕(malgun.ttf)을 찾을 수 없습니다.\n" +
                    "C:\\Windows\\Fonts\\malgun.ttf 경로를 확인하세요.", "확인");
                return;
            }

            if (!File.Exists(TTF_PATH))
            {
                FileUtil.CopyFileOrDirectory(SYS_FONT, TTF_PATH);
                AssetDatabase.ImportAsset(TTF_PATH);
                AssetDatabase.Refresh();
            }

            // ── 3. TMP Dynamic SDF 폰트 에셋 생성 ────────────────────────
            Font font = AssetDatabase.LoadAssetAtPath<Font>(TTF_PATH);
            if (font == null)
            {
                Debug.LogError("[FontSetup] TTF 로드 실패: " + TTF_PATH);
                return;
            }

            // 기존 에셋이 있으면 삭제 후 재생성
            if (File.Exists(SDF_PATH))
                AssetDatabase.DeleteAsset(SDF_PATH);

            TMP_FontAsset sdfFont = TMP_FontAsset.CreateFontAsset(
                font, 44, 4, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 2048, 2048);
            sdfFont.atlasPopulationMode = AtlasPopulationMode.Dynamic;

            AssetDatabase.CreateAsset(sdfFont, SDF_PATH);

            // Material & Atlas 서브에셋 저장
            if (sdfFont.material != null)
            {
                sdfFont.material.name = "MalgunGothic SDF Material";
                AssetDatabase.AddObjectToAsset(sdfFont.material, sdfFont);
            }
            if (sdfFont.atlasTexture != null)
            {
                sdfFont.atlasTexture.name = "MalgunGothic SDF Atlas";
                AssetDatabase.AddObjectToAsset(sdfFont.atlasTexture, sdfFont);
            }

            AssetDatabase.SaveAssets();

            // ── 4. TMP Settings 기본 폰트 변경 ───────────────────────────
            SetTMPDefault(sdfFont);

            // ── 5. 씬 내 모든 TMP 컴포넌트에 폰트 적용 ──────────────────
            int count = 0;
            var allTMP = Object.FindObjectsByType<TextMeshProUGUI>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var tmp in allTMP)
            {
                Undo.RecordObject(tmp, "Apply Korean Font");
                tmp.font = sdfFont;
                EditorUtility.SetDirty(tmp);
                count++;
            }

            // ── 6. KioskUIController에 폰트 참조 연결 ───────────────────
            var uiCtrl = Object.FindFirstObjectByType<Core.KioskUIController>();
            if (uiCtrl != null)
            {
                var so = new SerializedObject(uiCtrl);
                var prop = so.FindProperty("uiFont");
                if (prop != null)
                {
                    prop.objectReferenceValue = sdfFont;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            AssetDatabase.SaveAssets();

            Debug.Log($"[FontSetup] ✅ 한글 폰트 설정 완료! (TMP 컴포넌트 {count}개 업데이트)");
            EditorUtility.DisplayDialog("Korean Font Setup",
                $"맑은 고딕 SDF 폰트 생성 완료!\n\n" +
                $"• TMP 기본 폰트 변경 완료\n" +
                $"• 씬 내 TMP 컴포넌트 {count}개 적용\n\n" +
                "Play 모드를 다시 시작하세요.", "확인");
        }

        private static void SetTMPDefault(TMP_FontAsset font)
        {
            // TMP Settings 에셋 찾기
            string[] guids = AssetDatabase.FindAssets("t:TMP_Settings");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var settings = AssetDatabase.LoadAssetAtPath<Object>(path);
                if (settings == null) continue;

                var so = new SerializedObject(settings);
                var prop = so.FindProperty("m_defaultFontAsset");
                if (prop != null)
                {
                    prop.objectReferenceValue = font;
                    so.ApplyModifiedProperties();
                    Debug.Log("[FontSetup] TMP 기본 폰트 변경 완료: " + path);
                    return;
                }
            }
            Debug.LogWarning("[FontSetup] TMP Settings 에셋을 찾을 수 없습니다. 수동으로 설정해주세요.");
        }
    }
}
#endif
