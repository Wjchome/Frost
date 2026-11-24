using UnityEditor;
using UnityEngine;

namespace Frostline
{
    [CustomEditor(typeof(CardConfig))]
    internal sealed class CardConfigEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("运行时按这个资产的名称查找 Excel 中的卡牌 ID。卡牌数据请在 CardWorkbook.xlsx 里编辑。",
                MessageType.Info);
            if (GUILayout.Button("在 Project 中定位卡牌配置表"))
                EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath("Assets/Resources/Config/CardWorkbook.xlsx"));
            using var legacy = new EditorGUI.DisabledScope(true);
            serializedObject.Update();
            Draw("displayName", "卡名");
            Draw("portrait", "插画");
            Draw("action", "卡牌类型");
            Draw("catalogGroup", "图鉴分类");
            var action = (CardAction)serializedObject.FindProperty("action").enumValueIndex;
            if (action == CardAction.Deploy)
            {
                Draw("health", "生命");
                Draw("power", "攻击");
                Draw("period", "倒计时");
                Draw("frenzy", "攻击次数");
                Draw("pattern", "攻击方式");
                Draw("resistSnow", "抗雪球");
                Draw("trigger", "能力触发时机");
                Draw("abilityEffect", "能力效果");
                Draw("abilityAmount", "能力数值");
                var effect = (EffectKind)serializedObject.FindProperty("abilityEffect").enumValueIndex;
                if (effect is EffectKind.MaxCountdownUp or EffectKind.HurtForFrenzy)
                    Draw("abilitySecondaryAmount", "第二效果数值");
            }
            else if (action == CardAction.ApplyEffect)
            {
                Draw("effect", "状态");
                Draw("amount", "层数");
                var effect = (EffectKind)serializedObject.FindProperty("effect").enumValueIndex;
                if (effect is not EffectKind.AttackGrowth and not EffectKind.HealthGrowth)
                    Draw("targetsEnemy", "以敌方为目标");
            }
            else if (action == CardAction.Technique)
            {
                Draw("damage", "伤害");
                Draw("effect", "效果");
                Draw("amount", "效果数值");
                EditorGUILayout.PropertyField(serializedObject.FindProperty("technique"), new GUIContent("目标和使用规则"), true);
            }
            else if (action is not CardAction.Shift and not CardAction.Copy)
            {
                if (action is CardAction.Strike or CardAction.Freeze)
                    Draw("damage", "伤害");
                if (action != CardAction.Strike)
                    Draw("amount", "效果数值");
                if (action == CardAction.Strike)
                {
                    Draw("bonusAgainst", "增伤所需状态");
                    Draw("bonusDamage", "额外伤害");
                }
            }
            serializedObject.ApplyModifiedProperties();
        }

        private void Draw(string property, string caption) =>
            EditorGUILayout.PropertyField(serializedObject.FindProperty(property), new GUIContent(caption));
    }
}
