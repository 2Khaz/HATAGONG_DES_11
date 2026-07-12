using System;
using UnityEngine;

namespace HATAGONG.Phase1
{
    [Serializable]
    public sealed class Phase1TileBagDefinition
    {
        [SerializeField] private string id;
        [SerializeField] private Phase1Difficulty difficulty;
        [SerializeField] private int oneByOne;
        [SerializeField] private int oneByTwo;
        [SerializeField] private int oneByThree;
        [SerializeField] private int twoByTwo;
        [SerializeField] private int twoByThree;
        [SerializeField] private int threeByThree;
        [SerializeField] private int expectedTileCount;
        [SerializeField] private int expectedArea;
        [SerializeField] private int expectedHp;

        public string Id => id;
        public Phase1Difficulty Difficulty => difficulty;
        public int ExpectedTileCount => expectedTileCount;
        public int ExpectedArea => expectedArea;
        public int ExpectedHp => expectedHp;

        public Phase1TileBagDefinition(string id, Phase1Difficulty difficulty, int a, int b, int c, int d, int e, int f, int tiles, int area, int hp)
        { this.id=id; this.difficulty=difficulty; oneByOne=a; oneByTwo=b; oneByThree=c; twoByTwo=d; twoByThree=e; threeByThree=f; expectedTileCount=tiles; expectedArea=area; expectedHp=hp; }

        public int Count(Phase1TileShape shape) => shape switch
        {
            Phase1TileShape.OneByOne => oneByOne, Phase1TileShape.OneByTwo => oneByTwo,
            Phase1TileShape.OneByThree => oneByThree, Phase1TileShape.TwoByTwo => twoByTwo,
            Phase1TileShape.TwoByThree => twoByThree, Phase1TileShape.ThreeByThree => threeByThree, _ => 0
        };
    }
}
