using System;
using System.IO;
using System.Text;
using SteelDistrict.Vehicles;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SteelDistrict.Editor
{
    // Run in a disposable project copy with -executeMethod; never saves the test scene.
    public static class VehicleDrivingValidation
    {
        private static readonly StringBuilder report = new StringBuilder();
        private const float Dt = 0.02f;

        public static void RunBatch()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("Run validation in a disposable batch-mode project copy.");
            var previousMode = Physics.simulationMode;
            try
            {
                EditorSceneManager.OpenScene("Assets/Scene/GameScene.unity", OpenSceneMode.Single);
                var car = GameObject.Find("Simple Retro Car");
                Require(car != null, "GameScene contains Simple Retro Car");
                var drive = car.GetComponent<ArcadeVehicleDrive>();
                var body = car.GetComponent<Rigidbody>();
                var input = car.GetComponent<VehicleDriveInput>();
                Require(drive != null && body != null && input != null, "Serialized driving components resolve");
                Require(body.interpolation == RigidbodyInterpolation.Interpolate,
                    "Scene Rigidbody enables render interpolation");
                // Manual simulation has no player loop to publish interpolated Transform poses.
                body.interpolation = RigidbodyInterpolation.None;
                var camera = UnityEngine.Object.FindFirstObjectByType<VehicleFollowCamera>();
                Require(camera != null && camera.Target == car.transform, "Camera references the actual scene car");
                Require(car.GetComponentsInChildren<Collider>().Length == 2, "Existing body colliders preserved");
                Require(car.GetComponentsInChildren<WheelCollider>().Length == 0, "No duplicate WheelCollider drive");
                foreach (var root in car.scene.GetRootGameObjects())
                    foreach (var child in root.GetComponentsInChildren<Transform>(true))
                        Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) == 0,
                            "No missing scripts: " + child.name);

                drive.SendMessage("Awake");
                camera.SendMessage("Awake");
                input.enabled = false;
                Physics.simulationMode = SimulationMode.Script;
                Physics.SyncTransforms();
                Step(drive, default, 75);
                report.AppendLine($"Scene rest: {body.position}, contacts {drive.GroundedWheels}");
                Require(drive.GroundedWheels >= 3 && body.position.y > -1f,
                    "Actual scene floor supports the car at its saved spawn");

                var ground = new GameObject("Validation floor");
                var box = ground.AddComponent<BoxCollider>();
                box.size = new Vector3(500, 1, 500);
                ground.transform.position = new Vector3(0, -0.5f, 1000);
                Reset(body, drive);
                report.AppendLine($"Fixture rest: {body.position}, contacts {drive.GroundedWheels}, velocity {body.linearVelocity}");
                Require(drive.GroundedWheels == 4 && Mathf.Abs(body.position.y) < 0.1f,
                    "Four-point suspension settles at the wheel height");
                ValidateSuspensionAndDrift(body, drive);
                Reset(body, drive);
                Step(drive, new VehicleDriveCommand { Throttle = 1 }, 100);
                Require(drive.ForwardSpeed > 10 && body.position.z > 1010, "Throttle accelerates forward");
                Step(drive, new VehicleDriveCommand { Brake = 1, BrakePressed = true }, 1);
                Step(drive, new VehicleDriveCommand { Brake = 1 }, 150);
                Require(Mathf.Abs(drive.ForwardSpeed) < 0.3f && !drive.IsReversing,
                    "Holding S stops without automatically reversing");
                Step(drive, default, 1);
                Step(drive, new VehicleDriveCommand { Brake = 1, BrakePressed = true }, 1);
                Step(drive, new VehicleDriveCommand { Brake = 1 }, 70);
                Require(drive.IsReversing && drive.ForwardSpeed < -2, "A fresh S press engages reverse");

                Reset(body, drive);
                Step(drive, new VehicleDriveCommand { Steering = 1 }, 50);
                Require(Quaternion.Angle(body.rotation, Quaternion.identity) < 2, "No stationary pivot steering");
                Step(drive, new VehicleDriveCommand { Throttle = 1 }, 60);
                Step(drive, new VehicleDriveCommand { Throttle = 1, Steering = 0.65f }, 45);
                Require(body.rotation.eulerAngles.y > 5 && body.rotation.eulerAngles.y < 170,
                    "Steering changes the moving vehicle heading");

                Reset(body, drive);
                body.position = new Vector3(0, 20, 1000);
                body.linearVelocity = Vector3.zero;
                Physics.SyncTransforms();
                Step(drive, new VehicleDriveCommand { Throttle = 1, Steering = 1 }, 10);
                Require(drive.GroundedWheels == 0 && Mathf.Abs(body.linearVelocity.z) < 0.01f,
                    "Airborne throttle does not generate ground propulsion");

                Reset(body, drive);
                camera.UpdateCamera(Dt);
                Require(camera.Mode == VehicleFollowCamera.ViewMode.TopDown && camera.transform.position.y > 15,
                    "Top-down camera starts above the target");
                camera.ToggleView();
                for (int i = 0; i < 150; i++) camera.UpdateCamera(Dt);
                Require(camera.Mode == VehicleFollowCamera.ViewMode.Chase && camera.transform.position.y < 8 &&
                    camera.transform.position.z < car.transform.position.z, "V-mode chase camera settles behind the car");
                Vector3 viewport = camera.GetComponent<Camera>().WorldToViewportPoint(car.transform.position + Vector3.up);
                Require(viewport.z > 0 && viewport.x > 0 && viewport.x < 1 && viewport.y > 0 && viewport.y < 1,
                    "Vehicle stays inside the chase camera viewport");
                camera.ToggleView();
                for (int i = 0; i < 150; i++) camera.UpdateCamera(Dt);
                Require(camera.Mode == VehicleFollowCamera.ViewMode.TopDown && camera.transform.position.y > 15,
                    "Camera can return to top-down view");
                report.AppendLine("ALL CHECKS PASSED");
                WriteReport();
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                report.AppendLine(error.ToString());
                WriteReport();
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
            finally { Physics.simulationMode = previousMode; }
        }

        private static void Step(ArcadeVehicleDrive drive, VehicleDriveCommand command, int count)
        {
            for (int i = 0; i < count; i++)
            {
                drive.SimulateStep(command, Dt);
                Physics.Simulate(Dt);
            }
        }

        private static void ValidateSuspensionAndDrift(Rigidbody body, ArcadeVehicleDrive drive)
        {
            var suspension = drive.Suspension;
            var effects = drive.GetComponent<VehicleTireEffects>();
            Require(effects != null, "Slip effects component is attached");
            effects.Initialize();
            Reset(body, drive);
            effects.UpdateEffects();
            Require(effects.EmittingWheelCount == 0, "Stationary tires emit no effects");
            Step(drive, new VehicleDriveCommand { Handbrake = true }, 5);
            effects.UpdateEffects();
            Require(effects.EmittingWheelCount == 0, "Space while stationary creates no false smoke");

            var bump = new GameObject("One-wheel suspension bump");
            var bumpCollider = bump.AddComponent<BoxCollider>();
            bumpCollider.size = new Vector3(0.6f, 0.12f, 0.6f);
            bump.transform.position = suspension.Contacts[0].Point + Vector3.up * 0.06f;
            Physics.SyncTransforms();
            drive.SimulateStep(default, Dt);
            Require(suspension.Contacts[0].Compression > suspension.Contacts[1].Compression + 0.08f,
                "A one-wheel bump compresses that suspension independently");
            suspension.UpdateVisuals(1f);
            var frontLeft = body.transform.Find("FL");
            var frontRight = body.transform.Find("FR");
            Require(frontLeft.localPosition.y > frontRight.localPosition.y + 0.06f,
                "Wheel meshes visibly follow independent suspension compression");
            UnityEngine.Object.DestroyImmediate(bump);
            Reset(body, drive);

            float slowSlip = CornerSlip(body, drive, 8f, false);
            float fastSlip = CornerSlip(body, drive, 25f, false);
            float fastGrip = drive.RearGrip;
            float handbrakeSlip = CornerSlip(body, drive, 18f, true);
            report.AppendLine($"Corner slip degrees: slow={slowSlip:F2}, fast={fastSlip:F2}, handbrake={handbrakeSlip:F2}, fast rear grip={fastGrip:F2}");
            Require(fastSlip > slowSlip + 4 && fastGrip < 0.9f, "Fast steering produces rear grip loss and oversteer");
            Require(handbrakeSlip > 10 && handbrakeSlip < 65 && drive.RearGrip < 0.25f,
                "Handbrake produces a controlled rear slide without a full spin");
            effects.UpdateEffects();
            Require(effects.EmittingWheelCount >= 2, "Slipping tires emit reused smoke and skid effects");
            int particleCount = 0;
            foreach (var ps in effects.GetComponentsInChildren<ParticleSystem>())
            {
                ps.Simulate(0.15f, true, false);
                particleCount += ps.particleCount;
            }
            Require(particleCount > 0, "Reused smoke systems actually emit live particles");
            Step(drive, new VehicleDriveCommand { Throttle = 0.25f }, 200);
            effects.UpdateEffects();
            report.AppendLine($"Recovery: slip={drive.SlipAngle:F2}, rear grip={drive.RearGrip:F2}");
            Require(drive.RearGrip > 0.95f && Mathf.Abs(drive.SlipAngle) < 5,
                "Releasing handbrake restores grip and control");

            Reset(body, drive);
            body.linearVelocity = new Vector3(5, 0, 12);
            Step(drive, default, 1);
            effects.UpdateEffects();
            Require(effects.EmittingWheelCount > 0, "External sideways sliding emits without pressing Space");
            body.position += Vector3.up * 5;
            Physics.SyncTransforms();
            Step(drive, new VehicleDriveCommand { Handbrake = true }, 1);
            effects.UpdateEffects();
            Require(effects.EmittingWheelCount == 0, "Airborne tires stop emitting smoke and skid marks");
            Require(suspension.Contacts[0].Compression <= -suspension.Travel + 0.001f,
                "Airborne suspension extends to droop limit");
            Reset(body, drive);
        }

        private static float CornerSlip(Rigidbody body, ArcadeVehicleDrive drive, float speed, bool handbrake)
        {
            Reset(body, drive);
            body.linearVelocity = Vector3.forward * speed;
            float maximumSlip = 0;
            for (int i = 0; i < 45; i++)
            {
                Step(drive, new VehicleDriveCommand { Throttle = 0.5f, Steering = 0.85f, Handbrake = handbrake }, 1);
                maximumSlip = Mathf.Max(maximumSlip, Mathf.Abs(drive.SlipAngle));
            }
            return maximumSlip;
        }

        private static void Reset(Rigidbody body, ArcadeVehicleDrive drive)
        {
            drive.enabled = false;
            body.position = new Vector3(0, 0.02f, 1000);
            body.rotation = Quaternion.identity;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            drive.enabled = true;
            Physics.SyncTransforms();
            Step(drive, default, 100);
        }

        private static void Require(bool condition, string message)
        {
            report.AppendLine((condition ? "PASS " : "FAIL ") + message);
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void WriteReport()
        {
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/VehicleDrivingValidation.txt", report.ToString());
            Debug.Log(report.ToString());
        }
    }
}
