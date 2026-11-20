using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using RimWorld.Planet;
using Verse;

namespace CommRecruiter
{
    /// <summary>
    /// World Domination 2 outposts hold their pawns in a plain Occupants list (not spawned, not a caravan).
    /// Pulls hirees out of it by reflection so they don't linger there after reverting. WD2 is not a dependency.
    /// </summary>
    public static class OutpostExit
    {
        private static readonly Dictionary<Type, (PropertyInfo occupants, MethodInfo remove, MethodInfo notify)> cache =
            new Dictionary<Type, (PropertyInfo, MethodInfo, MethodInfo)>();

        /// <summary>Removes any of the pawns that sit in a WD2 outpost. Returns them grouped by outpost.</summary>
        public static Dictionary<WorldObject, List<Pawn>> Extract(IEnumerable<Pawn> pawns)
        {
            var result = new Dictionary<WorldObject, List<Pawn>>();
            if (Find.WorldObjects == null) return result;
            foreach (Pawn p in pawns)
            {
                if (p == null || p.Spawned) continue;
                foreach (WorldObject wo in Find.WorldObjects.AllWorldObjects)
                {
                    if (!Lookup(wo.GetType(), out var m)) continue;
                    try
                    {
                        if (!(m.occupants.GetValue(wo) is IList occ) || !occ.Contains(p)) continue;
                        m.remove.Invoke(wo, new object[] { p });
                        if (!result.TryGetValue(wo, out var list)) result[wo] = list = new List<Pawn>();
                        list.Add(p);
                    }
                    catch (Exception e) { Log.Warning("[Comm Recruiter] Could not remove hiree from outpost: " + e.Message); }
                    break;
                }
            }
            foreach (WorldObject wo in result.Keys)
            {
                try
                {
                    cache[wo.GetType()].notify?.Invoke(wo, null);
                    // Same as WD2's own removal helpers: an outpost that loses its last occupant goes away.
                    if (((IList)cache[wo.GetType()].occupants.GetValue(wo)).Count == 0 && !wo.Destroyed) wo.Destroy();
                }
                catch (Exception e) { Log.Warning("[Comm Recruiter] Outpost cleanup failed: " + e.Message); }
            }
            return result;
        }

        private static bool Lookup(Type t, out (PropertyInfo occupants, MethodInfo remove, MethodInfo notify) m)
        {
            if (cache.TryGetValue(t, out m)) return m.occupants != null;
            if (t.FullName == null || !t.FullName.StartsWith("TSA_WorldDomination.")) { m = default; return false; }
            PropertyInfo occ = null;
            MethodInfo remove = null, notify = null;
            for (Type c = t; c != null && c != typeof(WorldObject); c = c.BaseType)
            {
                occ = occ ?? c.GetProperty("Occupants", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                remove = remove ?? c.GetMethod("RemovePawn", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, new[] { typeof(Pawn) }, null);
                notify = notify ?? c.GetMethod("NotifyVirtualPawnsChanged", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null);
            }
            m = remove == null ? default : (occ, remove, notify);
            cache[t] = m;
            return m.occupants != null;
        }
    }
}
