using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using SteelDistrict.Vehicles;

namespace SteelDistrict.Scenes
{
    // 이번 단계에서는 플레이 세션 안에서만 유지합니다. 계정 소유권/영구 저장과 구분합니다.
    public static class VehicleSession
    {
        public const string MenuScene = "Assets/Scene/MainMenuScene.unity";
        public const string WorldScene = "Assets/Scene/GameScene.unity";
        public static string SelectedId { get; private set; }
        private sealed class Loadout { public VehicleEntry entry; public VehicleConfiguration configuration; public bool owned; public int price; }
        private static int gold;
        private static bool walletInitialized;
        public static int Gold => gold;
        public static void InitializeWallet(VehicleCatalog catalog)
        {
            if(walletInitialized || catalog==null) return;
            gold=Mathf.Max(0,catalog.initialGold); walletInitialized=true;
        }
        private static readonly Dictionary<string, Loadout> loadouts = new Dictionary<string, Loadout>();
        public static VehicleEntry SelectedVehicle => SelectedId != null && loadouts.TryGetValue(SelectedId, out var value) ? value.entry : null;
        public static bool Busy { get; internal set; }
        public static string Error { get; internal set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { SelectedId = null; Busy = false; Error = null; loadouts.Clear(); gold=0; walletInitialized=false; }
        private static Loadout GetOrCreate(VehicleEntry entry)
        {
            if (entry == null || !entry.IsValid) return null;
            if (!loadouts.TryGetValue(entry.id, out var value))
            {
                value = new Loadout { entry=entry, configuration=VehicleConfiguration.Capture(entry.drivePrefab), owned=entry.initiallyOwned, price=entry.purchasePrice };
                loadouts.Add(entry.id,value);
            }
            return value;
        }
        public static VehicleConfiguration GetConfiguration(VehicleEntry entry) => GetOrCreate(entry)?.configuration.Copy();
        public static bool IsOwned(VehicleEntry entry) => GetOrCreate(entry)?.owned == true;
        public static int GetPrice(VehicleEntry entry) => GetOrCreate(entry)?.price ?? -1;
        public static bool TryPurchase(VehicleEntry entry,out string error)
        {
            if(Busy || !walletInitialized) { error="지금은 구매할 수 없습니다."; return false; }
            var value=GetOrCreate(entry);
            if(value==null || !value.entry.availableInPrototype) { error="구매할 수 없는 차량입니다."; return false; }
            return VehiclePurchaseTransaction.TryCommit(ref gold,ref value.owned,value.price,out error);
        }
        // 구매/보상 처리 성공 후 호출할 세션 API입니다. 재화 결제나 영구 저장은 수행하지 않습니다.
        public static bool GrantOwnership(VehicleEntry entry)
        {
            if (Busy) return false;
            var value=GetOrCreate(entry); if(value==null) return false;
            value.owned=true; return true;
        }
        // 차고의 적용 버튼에서 호출합니다. 사본을 저장하여 편집 중인 초안이 확정값을 바꾸지 못하게 합니다.
        public static bool CommitConfiguration(VehicleEntry entry, VehicleConfiguration configuration)
        {
            if (Busy || entry == null || !entry.availableInPrototype || !IsOwned(entry) || configuration == null || !configuration.IsValid) return false;
            var value=GetOrCreate(entry); if(value==null) return false;
            value.configuration=configuration.Copy(); return true;
        }
        public static VehicleEntry ResolveSelected(VehicleCatalog fallback)
        {
            InitializeWallet(fallback);
            if (SelectedVehicle != null) return SelectedVehicle;
            var candidate=fallback != null ? Array.Find(fallback.vehicles, x=>x!=null && x.IsValid && x.availableInPrototype && IsOwned(x)) : null;
            var value=GetOrCreate(candidate);
            if(value==null) return null;
            SelectedId=value.entry.id; return value.entry;
        }
        public static bool Select(VehicleEntry entry)
        {
            if (Busy || entry == null || !entry.IsValid || !entry.availableInPrototype || !IsOwned(entry)) return false;
            if (GetOrCreate(entry) == null) return false;
            SelectedId = entry.id;
            return true;
        }
        public static bool Travel(string path)
        {
            if (Busy) return false;
            Error = null;
            if (string.IsNullOrEmpty(path)) { Error = "이동할 씬 경로가 없습니다."; return false; }
            if (!Application.CanStreamedLevelBeLoaded(path)) { Error = "씬이 빌드 목록에 없습니다."; return false; }
            Busy = true;
            var host = new GameObject("Scene Transition");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<SceneTransition>().Begin(path);
            return true;
        }
    }

    internal sealed class SceneTransition : MonoBehaviour
    {
        internal void Begin(string path) => StartCoroutine(Load(path));
        private IEnumerator Load(string path)
        {
            AsyncOperation operation;
            try { operation = SceneManager.LoadSceneAsync(path, LoadSceneMode.Single); }
            catch (Exception e) { VehicleSession.Error = "씬 로드 실패: " + e.Message; VehicleSession.Busy = false; Destroy(gameObject); yield break; }
            if (operation == null) { VehicleSession.Error = "씬 로드를 시작하지 못했습니다."; VehicleSession.Busy = false; Destroy(gameObject); yield break; }
            while (!operation.isDone) yield return null;
            VehicleSession.Busy = false;
            Destroy(gameObject);
        }
        private void OnDestroy() { VehicleSession.Busy = false; }
    }
}
