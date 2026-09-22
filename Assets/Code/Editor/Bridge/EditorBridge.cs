using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace SteelDistrict.Editor.Bridge
{
    [InitializeOnLoad]
    public static class EditorBridge
    {
        public static string Root => Path.GetFullPath("Library/SteelDistrictBridge");
        private static readonly List<BridgeLog> logs = new List<BridgeLog>();
        private static readonly object logLock = new object();
        private static double nextPoll, nextHeartbeat;
        private static BridgeTestJob job;
        private static string jobFile, jobHash;
        static EditorBridge()
        {
            foreach (string name in new[] { "requests", "processing", "responses", "rejected" }) Directory.CreateDirectory(Path.Combine(Root, name));
            // 도메인 재로드/Editor 종료 중 끊긴 명령은 자동 재적용하지 않습니다.
            foreach (string file in Directory.GetFiles(Path.Combine(Root, "processing"), "*.json"))
            {
                var result = new BridgeResponse { id = Path.GetFileNameWithoutExtension(file), utc = DateTime.UtcNow.ToString("O") };
                result.Error(file, "INTERRUPTED", "이전 Editor 세션에서 중단되었습니다. 실제 에셋/시험 로그를 조회한 뒤 재시도 여부를 결정하세요.");
                result.Finish();
                Complete(file, result, Hash(File.ReadAllText(file)));
            }
            Application.logMessageReceivedThreaded += Capture;
            EditorApplication.update += Poll;
        }
        internal static string Hash(string text)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
        }
        private static void Capture(string message, string stack, LogType type)
        {
            lock (logLock)
            {
                string bounded = message.Length > 4000 ? message.Substring(0, 4000) : message;
                var previous = logs.LastOrDefault();
                if (previous != null && previous.message == bounded && previous.level == type.ToString()) { previous.count++; return; }
                logs.Add(new BridgeLog { utc = DateTime.UtcNow.ToString("O"), level = type.ToString(), message = bounded,
                    stack = stack.Length > 2000 ? stack.Substring(0, 2000) : stack });
                if (logs.Count > 500) logs.RemoveAt(0);
            }
        }
        internal static void ReadLogs(BridgeRequest request, BridgeResponse result)
        {
            result.logScope = "현재 도메인 로드 이후 수집한 최근 500개 메시지. 이전 Console 기록은 포함하지 않음.";
            lock (logLock)
            {
                var selected = logs.Where(x => string.IsNullOrEmpty(request.level) || x.level.Equals(request.level, StringComparison.OrdinalIgnoreCase)).ToArray();
                result.logs.AddRange(selected.Skip(Math.Max(0, selected.Length - Mathf.Clamp(request.limit, 1, 200))));
            }
        }
        internal static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 0.2;
            try
            {
                if (EditorApplication.timeSinceStartup > nextHeartbeat)
                {
                    nextHeartbeat = EditorApplication.timeSinceStartup + 2;
                    try { WriteAtomic(Path.Combine(Root, "heartbeat.json"), JsonUtility.ToJson(BridgeCommands.Execute(new BridgeRequest { command = "editor.status" }))); }
                    catch (IOException) { nextHeartbeat = EditorApplication.timeSinceStartup + 0.5; } // 읽는 쪽의 짧은 파일 잠금은 다음 틱에 재시도합니다.
                }
                if (job != null && job.TryFinish(out var completed))
                {
                    Complete(jobFile, completed, jobHash);
                    job = null;
                }
                string incoming = Directory.GetFiles(Path.Combine(Root, "requests"), "*.json").OrderBy(x => x, StringComparer.Ordinal).FirstOrDefault();
                if (incoming == null) return;
                string id = Path.GetFileNameWithoutExtension(incoming);
                if (!Regex.IsMatch(id, "^[a-zA-Z0-9_-]{1,64}$") || new FileInfo(incoming).Length > 65536)
                {
                    File.Move(incoming, Path.Combine(Root, "rejected", Guid.NewGuid().ToString("N") + ".json"));
                    return;
                }
                string json = File.ReadAllText(incoming);
                string hash = Hash(json);
                string responseFile = Path.Combine(Root, "responses", id + ".json");
                if (File.Exists(responseFile))
                {
                    var previous = JsonUtility.FromJson<BridgeResponse>(File.ReadAllText(responseFile));
                    if (previous.requestHash == hash) File.Delete(incoming);
                    else File.Move(incoming, Path.Combine(Root, "rejected", id + "-" + Guid.NewGuid().ToString("N") + ".json"));
                    return;
                }
                string processing = Path.Combine(Root, "processing", id + ".json");
                File.Move(incoming, processing);
                BridgeResponse response;
                try
                {
                    var request = JsonUtility.FromJson<BridgeRequest>(json);
                    if (request == null || request.id != id || request.version != 1) throw new ArgumentException("요청 id/파일명/프로토콜을 확인하세요.");
                    if (request.command == "drive.test")
                    {
                        if (job != null) throw new InvalidOperationException("다른 주행 시험이 실행 중입니다.");
                        VehicleReplayValidation.ValidateRequest(request);
                        job = BridgeTestJob.Start(request);
                        jobFile = processing;
                        jobHash = hash;
                        return;
                    }
                    response = BridgeCommands.Execute(request);
                }
                catch (Exception e)
                {
                    response = new BridgeResponse { id = id, utc = DateTime.UtcNow.ToString("O") };
                    response.Error(id, "REQUEST_FAILED", e.Message);
                    response.Finish();
                }
                Complete(processing, response, hash);
            }
            catch (Exception e) { Debug.LogError("SteelDistrict Bridge: " + e.Message); }
        }
        internal static void PumpForValidation() { nextPoll = 0; Poll(); }
        private static void Complete(string file, BridgeResponse response, string hash)
        {
            response.requestHash = hash;
            WriteAtomic(Path.Combine(Root, "responses", response.id + ".json"), JsonUtility.ToJson(response, true));
            if (File.Exists(file)) File.Delete(file);
        }
        internal static void WriteAtomic(string path, string content)
        {
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, content, new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
    }

    internal sealed class BridgeTestJob
    {
        private System.Diagnostics.Process process;
        private string project, log;
        private DateTime started;
        private BridgeRequest request;
        internal static BridgeTestJob Start(BridgeRequest request)
        {
            if (!File.Exists(VehicleTestTrack.ScenePath)) throw new InvalidOperationException("track.create로 시험장을 먼저 생성하세요.");
            var job = new BridgeTestJob { request = request, started = DateTime.UtcNow };
            // 요청별 독립 복사본: 중단/재로드 뒤에도 다른 작업이나 원본을 덮어쓰지 않습니다.
            job.project = Path.GetFullPath(Path.Combine("Temp", "BridgeTests", request.id));
            if (Directory.Exists(job.project)) throw new InvalidOperationException("이미 존재하는 시험 요청 id입니다.");
            Directory.CreateDirectory(job.project);
            foreach (string folder in new[] { "Assets", "Packages", "ProjectSettings" })
                CopyDirectory(Path.GetFullPath(folder), Path.Combine(job.project, folder));
            // 오프라인에서도 같은 설치 패키지를 사용하도록 복사본에만 로컬 참조를 설정합니다.
            string manifestPath = Path.Combine(job.project, "Packages", "manifest.json");
            var packageMap = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (string directory in Directory.Exists("Library/PackageCache") ? Directory.GetDirectories("Library/PackageCache") : Array.Empty<string>())
            {
                string package = Path.Combine(directory, "package.json");
                if (!File.Exists(package)) continue;
                var name = JsonUtility.FromJson<PackageName>(File.ReadAllText(package))?.name;
                if (!string.IsNullOrEmpty(name)) packageMap[name] = "file:" + Path.GetFullPath(directory).Replace('\\', '/');
            }
            // 내장 모듈과 원본의 직접 의존성도 유지합니다.
            string original = File.ReadAllText(manifestPath);
            foreach (Match match in Regex.Matches(original, "\"([^\"]+)\"\\s*:\\s*\"([^\"]+)\""))
                if (match.Groups[1].Value.StartsWith("com.unity.", StringComparison.Ordinal) && !packageMap.ContainsKey(match.Groups[1].Value))
                    packageMap[match.Groups[1].Value] = match.Groups[2].Value;
            // 비 Unity 의존성을 누락하지 않도록 원본 manifest에 각 캐시 패키지만 덮어씁니다.
            var dependencyStart = Regex.Match(original, "\"dependencies\"\\s*:\\s*\\{");
            if (!dependencyStart.Success) throw new InvalidOperationException("패키지 manifest의 dependencies를 찾을 수 없습니다.");
            int end = original.IndexOf('}', dependencyStart.Index + dependencyStart.Length);
            if (end < 0) throw new InvalidOperationException("지원하지 않는 manifest 구조입니다.");
            var originalDependencies = original.Substring(dependencyStart.Index + dependencyStart.Length, end - dependencyStart.Index - dependencyStart.Length);
            foreach (Match match in Regex.Matches(originalDependencies, "\"([^\"]+)\"\\s*:\\s*\"([^\"]+)\""))
                if (!packageMap.ContainsKey(match.Groups[1].Value)) packageMap[match.Groups[1].Value] = match.Groups[2].Value;
            string dependencies = string.Join(",\n", packageMap.Select(x => "    \"" + x.Key + "\": \"" + x.Value + "\""));
            File.WriteAllText(manifestPath, original.Substring(0, dependencyStart.Index + dependencyStart.Length) + "\n" + dependencies + "\n  " + original.Substring(end));
            File.WriteAllText(Path.Combine(job.project, "bridge-worker-request.json"), JsonUtility.ToJson(request));
            job.log = Path.Combine(job.project, "worker.log");
            var start = new System.Diagnostics.ProcessStartInfo(EditorApplication.applicationPath,
                "-batchmode -nographics -projectPath \"" + job.project + "\" -executeMethod SteelDistrict.Editor.Bridge.VehicleReplayValidation.RunBatch -logFile \"" + job.log + "\"")
            { UseShellExecute = false, CreateNoWindow = true, WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden, WorkingDirectory = job.project };
            job.process = System.Diagnostics.Process.Start(start);
            if (job.process == null) throw new IOException("검증 Unity를 시작하지 못했습니다.");
            return job;
        }
        [Serializable] private sealed class PackageName { public string name; }
        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (string file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
            foreach (string directory in Directory.GetDirectories(source))
            {
                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) throw new IOException("심볼릭 링크 디렉터리 복사는 지원하지 않습니다: " + directory);
                CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
            }
        }
        internal bool TryFinish(out BridgeResponse result)
        {
            result = null;
            if (!process.HasExited && (DateTime.UtcNow - started).TotalMinutes < 10) return false;
            if (!process.HasExited)
            {
                process.Kill(); // 이 요청에서 시작한 전용 검증 프로세스만 종료합니다.
                result = new BridgeResponse { id = request.id, command = request.command };
                result.Error(request.id, "TEST_TIMEOUT", "검증 전용 Unity가 10분 안에 완료되지 않았습니다.");
            }
            else
            {
                string path = Path.Combine(project, "Logs", "BridgeReplayResult.json");
                if (File.Exists(path)) result = JsonUtility.FromJson<BridgeResponse>(File.ReadAllText(path));
                else
                {
                    result = new BridgeResponse { id = request.id, command = request.command };
                    result.Error(request.id, "WORKER_FAILED", "검증 보고서가 없습니다. 종료 코드 " + process.ExitCode + ". 로그: " + log);
                    if (File.Exists(log))
                        foreach (string line in File.ReadLines(log).Where(x => x.Contains("error CS") || x.Contains("Exception") || x.Contains("error") || x.Contains("Error")).Take(5))
                            result.logs.Add(new BridgeLog { level = "Error", message = line });
                }
            }
            result.reportPath = Path.Combine(project, "Logs", "BridgeReplayResult.json");
            result.Finish();
            process.Dispose();
            return true;
        }
    }
}
