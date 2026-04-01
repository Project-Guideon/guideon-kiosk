using System.Collections.Generic;
using UnityEngine;

namespace Guideon.Mascot
{
    /// <summary>
    /// GLB 모델에서 본을 자동 탐색하여 매핑하는 유틸리티.
    /// Tripo AI 리깅 네이밍 (spine, head, L_upperarm 등) 기준.
    /// </summary>
    public class BoneRig
    {
        public Transform Root { get; private set; }
        public Transform Hips { get; private set; }
        public Transform Spine { get; private set; }
        public Transform Spine1 { get; private set; }
        public Transform Spine2 { get; private set; }
        public Transform Neck { get; private set; }
        public Transform Head { get; private set; }
        public Transform Jaw { get; private set; }

        public Transform LeftUpperArm { get; private set; }
        public Transform LeftForearm { get; private set; }
        public Transform LeftHand { get; private set; }
        public Transform RightUpperArm { get; private set; }
        public Transform RightForearm { get; private set; }
        public Transform RightHand { get; private set; }

        public Transform LeftThigh { get; private set; }
        public Transform LeftCalf { get; private set; }
        public Transform LeftFoot { get; private set; }
        public Transform RightThigh { get; private set; }
        public Transform RightCalf { get; private set; }
        public Transform RightFoot { get; private set; }

        // 초기 로컬 회전값 저장 (리셋용)
        public Dictionary<Transform, Quaternion> InitialRotations { get; } = new();

        public bool IsValid => Head != null && Spine != null;

        public static BoneRig Build(GameObject modelRoot)
        {
            var rig = new BoneRig { Root = modelRoot.transform };
            var allTransforms = modelRoot.GetComponentsInChildren<Transform>();

            foreach (var t in allTransforms)
            {
                string n = t.name.ToLower();

                // Hips
                if (rig.Hips == null && (n.Contains("hip") || n == "pelvis"))
                    rig.Hips = t;

                // Spine chain
                if (n == "spine" || n == "spine0")
                    rig.Spine = t;
                else if (n == "spine1" || n == "spine01")
                    rig.Spine1 = t;
                else if (n == "spine2" || n == "spine02" || n == "chest")
                    rig.Spine2 = t;

                // Head/Neck
                if (rig.Neck == null && n.Contains("neck"))
                    rig.Neck = t;
                if (rig.Head == null && n.Contains("head") && !n.Contains("headtop"))
                    rig.Head = t;
                if (rig.Jaw == null && (n.Contains("jaw") || n.Contains("chin")))
                    rig.Jaw = t;

                // Left arm
                if (rig.LeftUpperArm == null && (n.Contains("l_upperarm") || n.Contains("leftupper") || n.Contains("l_arm") || n == "leftarm"))
                    rig.LeftUpperArm = t;
                if (rig.LeftForearm == null && (n.Contains("l_forearm") || n.Contains("leftforearm") || n.Contains("l_elbow") || n == "leftforearm"))
                    rig.LeftForearm = t;
                if (rig.LeftHand == null && (n.Contains("l_hand") || n.Contains("lefthand")))
                    rig.LeftHand = t;

                // Right arm
                if (rig.RightUpperArm == null && (n.Contains("r_upperarm") || n.Contains("rightupper") || n.Contains("r_arm") || n == "rightarm"))
                    rig.RightUpperArm = t;
                if (rig.RightForearm == null && (n.Contains("r_forearm") || n.Contains("rightforearm") || n.Contains("r_elbow") || n == "rightforearm"))
                    rig.RightForearm = t;
                if (rig.RightHand == null && (n.Contains("r_hand") || n.Contains("righthand")))
                    rig.RightHand = t;

                // Left leg
                if (rig.LeftThigh == null && (n.Contains("l_thigh") || n.Contains("leftupperleg") || n.Contains("l_leg")))
                    rig.LeftThigh = t;
                if (rig.LeftCalf == null && (n.Contains("l_calf") || n.Contains("leftlowerleg") || n.Contains("l_knee")))
                    rig.LeftCalf = t;
                if (rig.LeftFoot == null && (n.Contains("l_foot") || n.Contains("leftfoot")))
                    rig.LeftFoot = t;

                // Right leg
                if (rig.RightThigh == null && (n.Contains("r_thigh") || n.Contains("rightupperleg") || n.Contains("r_leg")))
                    rig.RightThigh = t;
                if (rig.RightCalf == null && (n.Contains("r_calf") || n.Contains("rightlowerleg") || n.Contains("r_knee")))
                    rig.RightCalf = t;
                if (rig.RightFoot == null && (n.Contains("r_foot") || n.Contains("rightfoot")))
                    rig.RightFoot = t;
            }

            // Spine fallback: 못 찾으면 Spine1, Spine2 중 하나라도 사용
            if (rig.Spine == null && rig.Spine1 != null) rig.Spine = rig.Spine1;

            // 초기 회전값 저장
            foreach (var t in allTransforms)
            {
                rig.InitialRotations[t] = t.localRotation;
            }

            return rig;
        }

        /// <summary>
        /// 모든 본을 초기 회전값으로 리셋
        /// </summary>
        public void ResetAll()
        {
            foreach (var kvp in InitialRotations)
            {
                if (kvp.Key != null)
                    kvp.Key.localRotation = kvp.Value;
            }
        }

        public string GetDebugInfo()
        {
            return $"Hips: {Name(Hips)} | Spine: {Name(Spine)} | Neck: {Name(Neck)} | Head: {Name(Head)}\n" +
                   $"Jaw: {Name(Jaw)}\n" +
                   $"L_Arm: {Name(LeftUpperArm)}/{Name(LeftForearm)}/{Name(LeftHand)}\n" +
                   $"R_Arm: {Name(RightUpperArm)}/{Name(RightForearm)}/{Name(RightHand)}\n" +
                   $"L_Leg: {Name(LeftThigh)}/{Name(LeftCalf)}/{Name(LeftFoot)}\n" +
                   $"R_Leg: {Name(RightThigh)}/{Name(RightCalf)}/{Name(RightFoot)}";
        }

        private static string Name(Transform t) => t != null ? t.name : "-";
    }
}
