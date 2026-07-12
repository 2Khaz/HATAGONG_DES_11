using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace HATAGONG.Phase1
{
    public static class Phase1LayoutHash
    {
        public static string Compute(Phase1Difficulty difficulty, string bagId, IEnumerable<Phase1TilePlacement> tiles)
        {
            var ordered=tiles.OrderBy(t=>t.GridY).ThenBy(t=>t.GridX).ThenBy(t=>t.Shape).ThenBy(t=>t.GridWidth).ThenBy(t=>t.GridHeight);
            var sb=new StringBuilder().Append(difficulty).Append('|').Append(bagId);
            foreach(var t in ordered) sb.Append('|').Append(t.Shape).Append(':').Append(t.GridX).Append(',').Append(t.GridY).Append(',').Append(t.GridWidth).Append(',').Append(t.GridHeight).Append(',').Append(t.IsRotated?1:0);
            using var sha=SHA256.Create(); return System.BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()))).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
