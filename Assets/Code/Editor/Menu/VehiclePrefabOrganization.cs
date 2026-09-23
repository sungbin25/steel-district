using System;
using System.Collections.Generic;
using System.IO;
using SteelDistrict.Vehicles;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SteelDistrict.Editor.Bridge
{
    // GUID와 튜닝을 그대로 보존하는 고정 범위 이동입니다. 씬을 열거나 저장하지 않습니다.
    public static class VehiclePrefabOrganization
    {
        public static void Run(BridgeRequest request, BridgeResponse result)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage()!=null)
                throw new InvalidOperationException("Edit Mode에서 Prefab Stage를 닫고 실행하세요.");
            var moves=new List<(string from,string to,string guid)>();
            foreach(string car in new[]{"SimpleRetro","Prometheus"})
            foreach(string kind in new[]{"Drive","Preview"})
            {
                string from=MainMenuSetup.Root+"/"+car+"_"+kind+".prefab";
                string to=MainMenuSetup.PrefabRoot+"/"+car+"/"+car+"_"+kind+".prefab";
                bool oldExists=File.Exists(from), newExists=File.Exists(to);
                if(oldExists==newExists) throw new InvalidOperationException("원본/대상 충돌 또는 누락: "+from+" → "+to);
                string current=oldExists?from:to;
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(current);
                if(prefab==null || EditorUtility.IsDirty(prefab)) throw new InvalidOperationException("프리팹 누락 또는 미저장 변경: "+current);
                if(kind=="Drive")
                {
                    BridgeCommands.InspectVehicle(prefab,current,result);
                    if(!VehicleConfiguration.Capture(prefab).IsValid) throw new InvalidOperationException("유효하지 않은 능력치: "+current);
                }
                result.checkedCount++;
                result.values.Add(new BridgeValue{target=current,before=current,after=to,changed=oldExists});
                if(oldExists) moves.Add((from,to,AssetDatabase.AssetPathToGUID(from)));
            }
            if(result.issues.Exists(x=>x.severity=="error")) throw new InvalidOperationException("차량 참조 검사 실패");
            if(!request.apply) return;
            var completed=new List<(string from,string to,string guid)>();
            try
            {
                foreach(var move in moves)
                {
                    EnsureFolder(Path.GetDirectoryName(move.to).Replace('\\','/'));
                    string error=AssetDatabase.MoveAsset(move.from,move.to);
                    if(!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
                    completed.Add(move);
                    if(AssetDatabase.AssetPathToGUID(move.to)!=move.guid) throw new InvalidOperationException("GUID 변경 감지: "+move.to);
                    result.changedCount++;
                }
                result.passedCount=result.checkedCount;
            }
            catch
            {
                for(int i=completed.Count-1;i>=0;i--)
                {
                    string error=AssetDatabase.MoveAsset(completed[i].to,completed[i].from);
                    if(!string.IsNullOrEmpty(error)) result.Error(completed[i].to,"ROLLBACK_FAILED",error);
                }
                result.changedCount=0;
                throw;
            }
        }
        private static void EnsureFolder(string path)
        {
            if(AssetDatabase.IsValidFolder(path)) return;
            string parent=Path.GetDirectoryName(path).Replace('\\','/');
            EnsureFolder(parent);
            string guid=AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
            if(string.IsNullOrEmpty(guid)) throw new IOException("폴더 생성 실패: "+path);
        }
    }
}
