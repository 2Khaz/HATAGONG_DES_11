using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace HATAGONG.Phase1
{
    public static class Phase1VariantHash
    {
            public static string Compute(Phase1BoardState state){var sb=new StringBuilder(state.LayoutHash);foreach(var t in state.Tiles.OrderBy(x=>x.TileId))sb.Append('|').Append(t.TileId).Append(':').Append(t.GradeId).Append(',').Append(t.GradeHpModifier).Append(',').Append(t.BalanceHpModifier).Append(',').Append(t.RequestHpModifier).Append(',').Append(t.MaxHp).Append(',').Append(t.VisualSetId).Append(',').Append(t.MinimumHpValid?1:0).Append(',').Append(t.IsSuperTile?1:0);using var sha=SHA256.Create();return System.BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()))).Replace("-",string.Empty).ToLowerInvariant();}
    }
}
