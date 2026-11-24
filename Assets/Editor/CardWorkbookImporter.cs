using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Frostline
{
    [ScriptedImporter(1, "xlsx")]
    internal sealed class CardWorkbookImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext context)
        {
            var asset = ScriptableObject.CreateInstance<CardWorkbookAsset>();
            asset.SetWorkbook(File.ReadAllBytes(context.assetPath));
            context.AddObjectToAsset("workbook", asset);
            context.SetMainObject(asset);
        }

        [MenuItem("Frostline/校验卡牌配置表")]
        public static void ValidateWorkbook()
        {
            AssetDatabase.ImportAsset("Assets/Resources/Config/CardWorkbook.xlsx", ImportAssetOptions.ForceSynchronousImport);
            var campaign = Resources.Load<CampaignConfig>(CampaignConfig.ResourcePath);
            var route = campaign.CreateRoute(new Dictionary<string, Texture2D>());
            Debug.Log($"卡牌配置表校验通过。{route.Factions.Count} 个部族，图鉴 {route.Catalog.Count} 张卡。");
        }
    }
}
