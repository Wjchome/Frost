using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Frostline
{
    internal static class CardWorkbookTools
    {
        [MenuItem("Frostline/校验卡牌配置表")]
        public static void ValidateWorkbook()
        {
            var campaign = Resources.Load<CampaignConfig>(CampaignConfig.ResourcePath);
            var route = campaign.CreateRoute(new Dictionary<string, Texture2D>());
            Debug.Log($"卡牌配置表校验通过。{route.Factions.Count} 个部族，图鉴 {route.Catalog.Count} 张卡。");
        }

        [MenuItem("Frostline/打开卡牌设计原稿")]
        public static void OpenDesignWorkbook() => EditorUtility.OpenWithDefaultApp(CardWorkbookSource.DesignFullPath);
    }

    // Player builds cannot read Docs/, so the design workbook is embedded for the duration of the build.
    internal sealed class CardWorkbookBuildStep : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            byte[] workbook = CardWorkbookSource.ReadDesign();
            if (workbook == null || workbook.Length == 0)
                throw new BuildFailedException($"未找到 {CardWorkbookSource.DesignPath}。");
            var asset = ScriptableObject.CreateInstance<CardWorkbookAsset>();
            asset.SetWorkbook(workbook);
            AssetDatabase.DeleteAsset(CardWorkbookSource.EmbeddedAssetPath);
            AssetDatabase.CreateAsset(asset, CardWorkbookSource.EmbeddedAssetPath);
            AssetDatabase.SaveAssets();
        }

        public void OnPostprocessBuild(BuildReport report) => AssetDatabase.DeleteAsset(CardWorkbookSource.EmbeddedAssetPath);
    }
}
