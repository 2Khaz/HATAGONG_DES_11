using System;
using UnityEngine;

namespace HATAGONG.Phase1
{
    [Serializable]
    public sealed class Phase1TileVisualDefinition
    {
        [SerializeField] private Phase1TileShape shape;
        [SerializeField] private Sprite normal;
        [SerializeField] private Sprite damage1;
        [SerializeField] private Sprite damage2;
        [SerializeField] private Sprite damage3;
        public Phase1TileShape Shape => shape;
        public Sprite Get(Phase1DamageState state) => state switch { Phase1DamageState.Damage1 => damage1 ? damage1 : normal, Phase1DamageState.Damage2 => damage2 ? damage2 : normal, Phase1DamageState.Damage3 => damage3 ? damage3 : normal, _ => normal };
        public Phase1TileVisualDefinition(Phase1TileShape shape) { this.shape = shape; }
    }
}
