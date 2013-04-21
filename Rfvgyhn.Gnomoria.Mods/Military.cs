using System.Collections.Generic;
using System.Linq;
using Game;
using System;
using System.Diagnostics;

namespace Rfvgyhn.Gnomoria.Mods
{
    public class Military
    {
        Dictionary<int, List<Character>> attackTargets;
        public static readonly string AllSquadsDisplay = "All";
        public static readonly int AllSquads = -1;        

        public Military()
        {
            attackTargets = new Dictionary<int, List<Character>>();
            attackTargets.Add(AllSquads, new List<Character>());
        }

        public void AddTarget(IEnumerable<int> squadIndexes, Character target)
        {
            foreach (var index in squadIndexes)
            {
                if (!attackTargets.ContainsKey(index))
                    attackTargets.Add(index, new List<Character>());

                if (!attackTargets[index].Contains(target))
                    attackTargets[index].Add(target);
            }
        }

        public void AddTarget(Character target)
        {
            attackTargets[AllSquads].Add(target);
        }

        public bool IsAttackTarget(Character c)
        {
            return attackTargets.Values.Any(l => l.Contains(c));
        }

        public Character FindAttackTarget(Character c)
        {
            var charSquad = c.Squad.Name.ToLower();
            var index = GetSquadIndex(c.Squad);

            if (!attackTargets.ContainsKey(index))
                index = AllSquads;

            return attackTargets[index].Where(t => !c.ShouldRunFromTarget(t) && c.CanTarget(t)).FirstOrDefault();
        }

        public void RemoveAttackTarget(Character c)
        {
            foreach (var targets in attackTargets.Values)
                targets.Remove(c);
        }

        public void RemoveSquad(Squad squad)
        {
            var index = GetSquadIndex(squad);
            attackTargets.Remove(index);

            var needToMove = attackTargets.Where(d => d.Key > index).ToArray();

            for (var i = 0; i < needToMove.Length; i++)
            {
                var kvp = needToMove[i];
                attackTargets.Add(kvp.Key - 1, kvp.Value);
                attackTargets.Remove(kvp.Key);
            }
        }

        private int GetSquadIndex(Squad squad)
        {
            return GnomanEmpire.Instance.Fortress.Military.Squads.FindIndex(s => s.Name == squad.Name);
        }
    }
}
