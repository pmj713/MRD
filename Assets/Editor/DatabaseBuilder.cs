using UnityEngine;
using UnityEditor;
using MRD.Data;
using MRD.Gacha;

namespace MRD.EditorTools
{
    /// <summary>
    /// 프로젝트 안의 모든 CharacterData 에셋을 찾아 CharacterDatabase 에셋 하나에 모아준다.
    /// 캐릭터를 추가/삭제한 뒤에는 이 메뉴를 다시 실행해서 최신 상태로 갱신해야 한다.
    /// </summary>
    public static class DatabaseBuilder
    {
        private const string DatabasePath = "Assets/Data/CharacterDatabase.asset";

        [MenuItem("MRD/Build Character Database")]
        public static void BuildCharacterDatabase()
        {
            var database = AssetDatabase.LoadAssetAtPath<CharacterDatabase>(DatabasePath);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<CharacterDatabase>();
                AssetDatabase.CreateAsset(database, DatabasePath);
            }

            var guids = AssetDatabase.FindAssets("t:CharacterData");
            database.allCharacters.Clear();

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var data = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
                if (data != null)
                    database.allCharacters.Add(data);
            }

            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();

            Debug.Log($"[DatabaseBuilder] CharacterDatabase 갱신 완료: {database.allCharacters.Count}개");
        }
    }
}
