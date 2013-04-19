using System.Collections.Generic;
using System.Linq;
using Game;

namespace Rfvgyhn.Gnomoria.Mods
{
    public class Military
    {
        Dictionary<string, List<Character>> attackTargets;
        public static readonly string AllSquadsDisplay = "All";
        public static readonly string AllSquads = AllSquadsDisplay.ToLower();        

        public Military()
        {
            attackTargets = new Dictionary<string, List<Character>>();
            attackTargets.Add(AllSquads.ToLower(), new List<Character>());
        }

        public void AddTarget(string squadName, Character target)
        {
            var squad = squadName.ToLower();

            if (!attackTargets.ContainsKey(squad))
                attackTargets.Add(squad, new List<Character>());

            if (!attackTargets[squad].Contains(target))
                attackTargets[squad].Add(target);
        }

        public bool IsAttackTarget(Character c)
        {
            return attackTargets.Values.Any(l => l.Contains(c));
        }

        public Character FindAttackTarget(Character c)
        {
            var charSquad = c.Squad.Name.ToLower();
            var squad = attackTargets.ContainsKey(charSquad) && attackTargets[charSquad].Any() ? charSquad : AllSquads;

            return attackTargets[squad].Where(t => !c.ShouldRunFromTarget(t) && c.CanTarget(t)).FirstOrDefault();          
        }

        public void RemoveAttackTarget(Character c)
        {
            foreach (var targets in attackTargets.Values)
                targets.Remove(c);
        }
    }
}
