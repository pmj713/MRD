using UnityEngine;
using UnityEditor;
using MRD.Data;
using MRD.Battle;

namespace MRD.EditorTools
{
    /// <summary>
    /// CharacterData 에셋과 BattleUnit 스탯 계산이 정상 동작하는지 확인하는 헤드리스 점검 스크립트.
    /// 유니티 에디터를 열지 않고도 `-executeMethod`로 배치 모드에서 실행할 수 있다.
    /// </summary>
    public static class SmokeTestRunner
    {
        [MenuItem("MRD/Run Character Smoke Test")]
        public static void RunSmokeTest()
        {
            var spartan = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Data/Characters/Olympus/SpartanShieldman.asset");
            var heracles = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Data/Characters/Olympus/Heracles.asset");
            var zeus = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Data/Characters/Olympus/Zeus.asset");

            bool ok = true;
            ok &= Check(spartan, "스파르타 방패병");
            ok &= Check(heracles, "헤라클레스");
            ok &= Check(zeus, "제우스 (봉인 해제)");

            if (zeus != null && zeus.fusionRecipe.requiredCharacters != null && zeus.fusionRecipe.requiredCharacters.Length > 0)
            {
                var material = zeus.fusionRecipe.requiredCharacters[0];
                ok &= LogAndCheck("제우스 조합 재료 참조", material != null, $"{material?.characterName}");
            }
            else
            {
                Debug.LogError("[SmokeTest] 제우스 fusionRecipe.requiredCharacters 참조 실패");
                ok = false;
            }

            // BattleUnit이 시너지 보너스를 실제로 반영해서 계산하는지 검증
            var go = new GameObject("SmokeTest_BattleUnit");
            try
            {
                var unit = go.AddComponent<BattleUnit>();
                var bonus = new StatModifier { physicalAttackPercent = 15f, attackSpeedPercent = 15f };
                unit.Initialize(heracles, bonus);

                float expectedAttack = heracles.stats.physicalAttack * 1.15f;
                float expectedSpeed = heracles.stats.attackSpeed * 1.15f;

                ok &= ApproxLog("EffectiveStats.physicalAttack", unit.EffectiveStats.physicalAttack, expectedAttack);
                ok &= ApproxLog("EffectiveStats.attackSpeed", unit.EffectiveStats.attackSpeed, expectedSpeed);
                ok &= ApproxLog("CurrentHealth(초기값 == 기본 체력)", unit.CurrentHealth, heracles.stats.health);

                bool usedSkill = unit.TryUseActiveSkill();
                ok &= LogAndCheck("마나 부족 상태에서 스킬 사용 실패해야 함", !usedSkill, usedSkill.ToString());

                unit.AddMana(heracles.activeSkill.manaCost);
                usedSkill = unit.TryUseActiveSkill();
                ok &= LogAndCheck("마나 충전 후 스킬 사용 성공해야 함", usedSkill, usedSkill.ToString());
            }
            finally
            {
                Object.DestroyImmediate(go);
            }

            if (ok)
                Debug.Log("[SmokeTest] 모든 검증 통과");
            else
                Debug.LogError("[SmokeTest] 일부 검증 실패 - 위 로그 확인");
        }

        private static bool Check(CharacterData data, string expectedName)
        {
            if (data == null)
            {
                Debug.LogError($"[SmokeTest] 에셋 로드 실패: {expectedName}");
                return false;
            }

            Debug.Log($"[SmokeTest] 로드됨: {data.characterName} (진영={data.faction}, 등급={data.rarity}, 체력={data.stats.health})");
            return true;
        }

        private static bool ApproxLog(string label, float actual, float expected)
        {
            bool pass = Mathf.Abs(actual - expected) < 0.01f;
            Debug.Log($"[SmokeTest] {label}: actual={actual}, expected={expected}, pass={pass}");
            return pass;
        }

        private static bool LogAndCheck(string label, bool pass, string detail)
        {
            Debug.Log($"[SmokeTest] {label}: {detail}, pass={pass}");
            return pass;
        }
    }
}
