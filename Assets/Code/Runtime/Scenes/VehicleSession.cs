using System;
using System.Collections;
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
        public static bool Busy { get; internal set; }
        public static string Error { get; internal set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { SelectedId = null; Busy = false; Error = null; }
        public static bool Select(VehicleEntry entry)
        {
            if (Busy || entry == null || !entry.IsValid || !entry.availableInPrototype) return false;
            SelectedId = entry.id;
            return true;
        }
        public static bool Travel(string path)
        {
            if (Busy) return false;
            Error = null;
            if (path != MenuScene && path != WorldScene) { Error = "허용되지 않은 이동 경로입니다."; return false; }
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
