using UnityEditor;
using UnityEngine;

namespace Frostline
{
    [CustomEditor(typeof(CardConfig))]
    internal sealed class CardConfigEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            Draw("displayName", "卡名");
            Draw("portrait", "插画");
            Draw("action", "卡牌类型");
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
            }
            else if (action == CardAction.ApplyEffect)
            {
                Draw("effect", "状态");
                Draw("amount", "层数");
                if ((EffectKind)serializedObject.FindProperty("effect").enumValueIndex != EffectKind.AttackGrowth)
                    Draw("targetsEnemy", "以敌方为目标");
            }
            else
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
