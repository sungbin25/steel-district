using System;
using UnityEngine;

namespace SteelDistrict.Vehicles
{
    // 씬 객체 참조·현재 속도·접지 상태를 포함하지 않는 차종별 확정 능력치입니다.
    // 프리팹은 초기값, 이 데이터는 현재 플레이 세션의 메뉴/차고/월드 공통 값입니다.
    [Serializable]
    public sealed class VehicleConfiguration
    {
        public float mass;
        public VehicleDriveType driveType;
        public float maxSpeedKph, reverseSpeedKph, acceleration, brakeAcceleration, coastingDeceleration;
        public float turnRate, steeringResponse, gripRecovery, maximumGripAcceleration, oversteerStartKph;
        public float highSpeedRearGrip, handbrakeRearGrip, gripRestoreSeconds, handbrakeDeceleration;
        public Vector3 centerOfMass;
        public float wheelRadius, travel, springFrequency, dampingRatio, antiRoll;
        public int groundMask;
        public VehicleConfiguration Copy() => (VehicleConfiguration)MemberwiseClone();
        public static VehicleConfiguration Capture(GameObject vehicle)
        {
            var value = new VehicleConfiguration { mass = vehicle.GetComponent<Rigidbody>().mass };
            vehicle.GetComponent<ArcadeVehicleDrive>().CaptureConfiguration(value);
            vehicle.GetComponent<VehicleSuspension>().CaptureConfiguration(value);
            return value;
        }
        public bool IsValid
        {
            get
            {
                float[] numbers = {mass,maxSpeedKph,reverseSpeedKph,acceleration,brakeAcceleration,coastingDeceleration,
                    turnRate,steeringResponse,gripRecovery,maximumGripAcceleration,oversteerStartKph,highSpeedRearGrip,
                    handbrakeRearGrip,gripRestoreSeconds,handbrakeDeceleration,centerOfMass.x,centerOfMass.y,centerOfMass.z,
                    wheelRadius,travel,springFrequency,dampingRatio,antiRoll};
                foreach (float value in numbers) if (float.IsNaN(value) || float.IsInfinity(value)) return false;
                return (driveType == VehicleDriveType.AllWheel || driveType == VehicleDriveType.FrontWheel ||
                    driveType == VehicleDriveType.RearWheel) &&
                    mass>0 && maxSpeedKph>=1 && reverseSpeedKph>=1 && acceleration>=0 && brakeAcceleration>=0 &&
                    coastingDeceleration>=0 && turnRate>=1 && steeringResponse>=.01f && gripRecovery>=.05f &&
                    maximumGripAcceleration>=1 && oversteerStartKph>=1 && highSpeedRearGrip>=.1f && highSpeedRearGrip<=1 &&
                    handbrakeRearGrip>=.02f && handbrakeRearGrip<=.5f && gripRestoreSeconds>=.05f && handbrakeDeceleration>=0 &&
                    wheelRadius>=.05f && travel>=.05f && springFrequency>=1 && springFrequency<=5 &&
                    dampingRatio>=.2f && dampingRatio<=2 && antiRoll>=0 && antiRoll<=20 && groundMask!=0;
            }
        }
        public void Apply(GameObject vehicle)
        {
            if (!IsValid) throw new ArgumentException("차량 능력치 범위가 유효하지 않습니다.");
            vehicle.GetComponent<Rigidbody>().mass=mass;
            vehicle.GetComponent<ArcadeVehicleDrive>().ApplyConfiguration(this);
            vehicle.GetComponent<VehicleSuspension>().ApplyConfiguration(this);
        }
    }
}
