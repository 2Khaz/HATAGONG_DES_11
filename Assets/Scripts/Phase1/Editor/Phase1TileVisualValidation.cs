#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace HATAGONG.Phase1.Editor
{
    public static class Phase1TileVisualValidation
    {
        [MenuItem("Tools/HATAGONG/Phase1/Validate Tile Visual Resources")]
        public static void Validate()
        {
            if (!Phase1TileVisualResources.ValidateAllResources(out string error))
                throw new InvalidOperationException("[Phase1][VisualValidation] " + error);

            foreach (Phase1DamageState state in Enum.GetValues(typeof(Phase1DamageState)))
                Require(Enum.IsDefined(typeof(Phase1DamageState), state), "Invalid shared damage state: " + state);

            Require(Phase1TileView.CalculateState(20, 20) == Phase1DamageState.Normal, "Normal threshold failed.");
            Require(Phase1TileView.CalculateState(20, 15) == Phase1DamageState.Damage1, "Damage1 threshold failed.");
            Require(Phase1TileView.CalculateState(20, 10) == Phase1DamageState.Damage2, "Damage2 threshold failed.");
            Require(Phase1TileView.CalculateState(20, 5) == Phase1DamageState.Damage3, "Damage3 threshold failed.");
            Require(Phase1TileView.CalculateState(20, 0) == Phase1DamageState.Destroyed, "Destroyed threshold failed.");
            Debug.Log("[Phase1][VisualValidation] PASS: 6 sizes x 4 damage sprites, 4 material textures, shared 5-state damage model.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
#endif
