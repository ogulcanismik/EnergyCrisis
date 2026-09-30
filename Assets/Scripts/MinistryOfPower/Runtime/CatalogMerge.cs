using System.Collections.Generic;
using MinistryOfPower.Data;
using MinistryOfPower.Simulation;

namespace MinistryOfPower.Runtime
{
    /// <summary>
    /// Unions a partial SO / preferred build list with the factory full catalog by Id.
    /// Preferred entries win on collision; factory order is preserved, then SO-only ids append.
    /// </summary>
    /// <remarks>
    /// StartNew one-liner:
    /// <c>builds = CatalogMerge.Merge(assetBuilds, builds);</c>
    /// or <c>builds = CatalogMerge.Merge(scenarioSo);</c>
    /// </remarks>
    public static class CatalogMerge
    {
        public static List<BuildDefinitionConfig> Merge(ScenarioDefinition scenario)
        {
            List<BuildDefinitionConfig> preferred =
                scenario != null ? scenario.ToBuildConfigs() : null;
            return Merge(preferred, PrototypeContentFactory.CreateFullCatalog());
        }

        /// <param name="preferred">SO or other authoring list (may be partial).</param>
        /// <param name="fallbackCatalog">Usually factory CreateFullCatalog(); null → factory.</param>
        public static List<BuildDefinitionConfig> Merge(
            List<BuildDefinitionConfig> preferred,
            List<BuildDefinitionConfig> fallbackCatalog)
        {
            if (fallbackCatalog == null || fallbackCatalog.Count == 0)
            {
                fallbackCatalog = PrototypeContentFactory.CreateFullCatalog();
            }

            var byId = new Dictionary<string, BuildDefinitionConfig>(fallbackCatalog.Count + 4);
            for (int i = 0; i < fallbackCatalog.Count; i++)
            {
                BuildDefinitionConfig b = fallbackCatalog[i];
                if (b == null || string.IsNullOrEmpty(b.Id)) continue;
                byId[b.Id] = b;
            }

            if (preferred != null)
            {
                for (int i = 0; i < preferred.Count; i++)
                {
                    BuildDefinitionConfig b = preferred[i];
                    if (b == null || string.IsNullOrEmpty(b.Id)) continue;
                    byId[b.Id] = b;
                }
            }

            var result = new List<BuildDefinitionConfig>(byId.Count);
            var used = new HashSet<string>();
            for (int i = 0; i < fallbackCatalog.Count; i++)
            {
                BuildDefinitionConfig order = fallbackCatalog[i];
                if (order == null || string.IsNullOrEmpty(order.Id)) continue;
                if (!used.Add(order.Id)) continue;
                if (byId.TryGetValue(order.Id, out BuildDefinitionConfig merged))
                {
                    result.Add(merged);
                }
            }

            if (preferred != null)
            {
                for (int i = 0; i < preferred.Count; i++)
                {
                    BuildDefinitionConfig b = preferred[i];
                    if (b == null || string.IsNullOrEmpty(b.Id)) continue;
                    if (!used.Add(b.Id)) continue;
                    result.Add(b);
                }
            }

            return result;
        }
    }
}
