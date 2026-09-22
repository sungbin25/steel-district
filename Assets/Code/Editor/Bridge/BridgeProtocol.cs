using System;
using System.Collections.Generic;
using UnityEngine;

namespace SteelDistrict.Editor.Bridge
{
    [Serializable] public sealed class BridgeTarget
    {
        public string assetPath;
        public string objectId;
        public int sceneHandle;
        public string hierarchyPath;
    }
    [Serializable] public sealed class BridgeChange
    {
        public string component;
        public string property;
        public string value;
        public bool checkExpected;
        public string expectedValue;
    }
    // scene.edit의 단일 작업. 사용하는 필드는 op마다 다르며 Docs/UnityEditorBridge.md에 정리되어 있습니다.
    [Serializable] public sealed class BridgeOperation
    {
        public string op;
        public string path;
        public string parent;
        public string name;
        public string prefab;
        public string primitive;
        public string component;
        public int index;
        public string property;
        public string value;
        public string position;
        public string rotation;
        public string scale;
        public bool optional;
    }
    [Serializable] public sealed class BridgeRequest
    {
        public int version = 1;
        public string id;
        public string command;
        public BridgeTarget[] targets = Array.Empty<BridgeTarget>();
        public BridgeChange[] changes = Array.Empty<BridgeChange>();
        public string[] fields = Array.Empty<string>();
        public bool apply;
        public int limit = 50;
        public string level;
        public string scenario = "suite";
        public int repeat = 2;
        public SteelDistrict.Testing.VehicleReplayStep[] steps;
        public BridgeOperation[] operations = Array.Empty<BridgeOperation>();
        public bool save;
        public string mode;
    }
    [Serializable] public sealed class BridgeIssue
    {
        public string target, code, message;
        public string severity = "error";
    }
    [Serializable] public sealed class BridgeValue
    {
        public string target, component, property, before, after;
        public bool changed;
    }
    [Serializable] public sealed class BridgeObject
    {
        public string target, objectId, name;
        public int layer;
        public string layerName;
        public bool dirty, active;
        public List<string> components = new List<string>();
    }
    [Serializable] public sealed class BridgeScene
    {
        public int handle;
        public string path, name;
        public bool dirty, loaded, active;
    }
    [Serializable] public sealed class BridgeLog
    {
        public string utc, level, message, stack;
        public int count = 1;
    }
    [Serializable] public sealed class ReplayResult
    {
        public string scenario;
        public int run, ticks, groundedTicks;
        public float maxSpeedKph, finalSpeedKph, distance, maxHeight, maxSlip, maxCompression, endZ;
        public bool passed;
        public string error;
    }
    [Serializable] public sealed class BridgeResponse
    {
        public int version = 1;
        public string id, command, requestHash, utc, editorVersion;
        public bool ok, dryRun, compiling, updating, playing, paused;
        public int checkedCount, changedCount, passedCount, editorPid;
        public string logScope, reportPath;
        public List<BridgeIssue> issues = new List<BridgeIssue>();
        public List<BridgeValue> values = new List<BridgeValue>();
        public List<BridgeObject> objects = new List<BridgeObject>();
        public List<BridgeScene> scenes = new List<BridgeScene>();
        public List<BridgeLog> logs = new List<BridgeLog>();
        public List<ReplayResult> tests = new List<ReplayResult>();
        public List<string> commands = new List<string>();
        public void Error(string target, string code, string message)
        {
            issues.Add(new BridgeIssue { target = target, code = code, message = message });
        }
        public void Warning(string target, string code, string message)
        {
            issues.Add(new BridgeIssue { target = target, code = code, message = message, severity = "warning" });
        }
        public void Finish() => ok = !issues.Exists(x => x.severity == "error");
    }
}
