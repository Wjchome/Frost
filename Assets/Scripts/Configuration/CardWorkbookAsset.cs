using System.IO;
using UnityEngine;

namespace Frostline
{
    // Holds a copy of the design workbook inside player builds. The editor reads the design file directly.
    public sealed class CardWorkbookAsset : ScriptableObject
    {
        [SerializeField, HideInInspector] private byte[] workbook;

        public byte[] Workbook => workbook;

#if UNITY_EDITOR
        public void SetWorkbook(byte[] bytes) => workbook = bytes;
#endif
    }

    public static class CardWorkbookSource
    {
        public const string DesignPath = "Docs/Design/卡牌设计原稿.xlsx";
        public const string EmbeddedAssetPath = "Assets/Resources/Config/EmbeddedCardWorkbook.asset";
        private const string EmbeddedResourcePath = "Config/EmbeddedCardWorkbook";

        public static string DesignFullPath => Path.GetFullPath(Path.Combine(Application.dataPath, "..", DesignPath));

        public static byte[] Load()
        {
#if UNITY_EDITOR
            return ReadDesign();
#else
            var asset = Resources.Load<CardWorkbookAsset>(EmbeddedResourcePath);
            return asset != null ? asset.Workbook : null;
#endif
        }

        public static byte[] ReadDesign()
        {
            string path = DesignFullPath;
            if (!File.Exists(path))
                return null;
            // Excel keeps an open workbook locked for writing. A shared read still returns the last saved version.
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            return copy.ToArray();
        }
    }
}
