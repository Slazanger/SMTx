using DataProcessor.Models;

namespace DataProcessor.Services;

public class LinkProcessor
{
    public class StargateLink
    {
        public int SourceSystemId { get; set; }
        public int DestinationSystemId { get; set; }
        public string LinkType { get; set; } = "regular";
    }

    public class ConstellationLink
    {
        public int SourceConstellationId { get; set; }
        public int DestinationConstellationId { get; set; }
    }

    public List<StargateLink> ProcessStargateLinks(
        List<Stargate> stargates,
        Dictionary<int, SolarSystem> systemLookup)
    {
        var links = new Dictionary<(int, int), StargateLink>();

        foreach (var stargate in stargates)
        {
            if (!systemLookup.TryGetValue(stargate.SourceSystemId, out var sourceSystem) ||
                !systemLookup.TryGetValue(stargate.DestinationSystemId, out var destSystem))
            {
                continue;
            }

            // Create unique pair (always use smaller ID first to avoid duplicates)
            var key = stargate.SourceSystemId < stargate.DestinationSystemId
                ? (stargate.SourceSystemId, stargate.DestinationSystemId)
                : (stargate.DestinationSystemId, stargate.SourceSystemId);

            if (links.ContainsKey(key))
                continue;

            // Classify link type
            string linkType = "regular";
            
            if (sourceSystem.RegionId != destSystem.RegionId)
            {
                linkType = "regional";
            }
            else if (sourceSystem.ConstellationId != destSystem.ConstellationId)
            {
                linkType = "constellation";
            }

            links[key] = new StargateLink
            {
                SourceSystemId = key.Item1,
                DestinationSystemId = key.Item2,
                LinkType = linkType
            };
        }

        return links.Values.ToList();
    }

    public List<ConstellationLink> CalculateConstellationLinks(
        List<StargateLink> stargateLinks,
        Dictionary<int, SolarSystem> systemLookup)
    {
        var constellationLinks = new Dictionary<(int, int), ConstellationLink>();

        foreach (var link in stargateLinks)
        {
            if (!systemLookup.TryGetValue(link.SourceSystemId, out var sourceSystem) ||
                !systemLookup.TryGetValue(link.DestinationSystemId, out var destSystem))
            {
                continue;
            }

            if (!sourceSystem.ConstellationId.HasValue || !destSystem.ConstellationId.HasValue)
                continue;

            var sourceConstId = sourceSystem.ConstellationId.Value;
            var destConstId = destSystem.ConstellationId.Value;

            if (sourceConstId == destConstId)
                continue;

            // Create unique pair (always use smaller ID first)
            var key = sourceConstId < destConstId
                ? (sourceConstId, destConstId)
                : (destConstId, sourceConstId);

            if (!constellationLinks.ContainsKey(key))
            {
                constellationLinks[key] = new ConstellationLink
                {
                    SourceConstellationId = key.Item1,
                    DestinationConstellationId = key.Item2
                };
            }
        }

        return constellationLinks.Values.ToList();
    }
}

