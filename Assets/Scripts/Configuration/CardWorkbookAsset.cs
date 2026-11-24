using UnityEngine;

namespace Frostline
{
    public sealed class CardWorkbookAsset : ScriptableObject
    {
        [SerializeField, HideInInspector] private byte[] workbook;

        public byte[] Workbook => workbook;

#if UNITY_EDITOR
        public void SetWorkbook(byte[] bytes) => workbook = bytes;
#endif
    }
}
