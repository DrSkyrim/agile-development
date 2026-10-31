using System;
using System.Collections.Generic;

namespace UrbanGenesis.Simulation
{
    /// <summary>
    /// Made-up city names, one picked per launch until maps and saves give a city a real identity.
    /// All names are invented; any match with a real place is chance.
    /// </summary>
    public static class CityNames
    {
        private static readonly string[] Names =
        {
            "Northhaven", "Eastmere", "Westbrook", "Southgate", "Ashford Vale", "Alderbrook", "Amberfield", "Applegate",
            "Ashwick", "Aldermoor", "Bellhaven", "Birchwood", "Blackwater", "Bramblewick", "Brightwell", "Briarcliff",
            "Brookhollow", "Calder Bay", "Candlewick", "Cedarfall", "Clearwater", "Cloverdale", "Coldharbour", "Copperfield",
            "Cranmere", "Crestmoor", "Dawnport", "Deepdale", "Dunmere", "Eaglecrest", "Elderglen", "Elmstead",
            "Emberton", "Fairhaven", "Falconridge", "Fernhill", "Fieldstone", "Firthwood", "Flintmouth", "Foxhollow",
            "Frostmere", "Gladstead", "Glenmoor", "Goldcrest", "Graniton", "Greenhithe", "Greywater", "Hallowmere",
            "Harbourlight", "Hartwell", "Hawksbury", "Hazelbrook", "Heatherstone", "Highmarsh", "Hollowbrook", "Honeywell",
            "Ironbridge Vale", "Ivywood", "Juniper Bay", "Kestrel Point", "Kingsmere", "Lakeshore Reach", "Larkspur", "Lindenfield",
            "Longmeadow", "Marlowe Cross", "Maplewick", "Meadowgate", "Millbrook", "Mistral Bay", "Moonbridge", "Mossgrove",
            "Netherby", "Newhaven Quay", "Oakhurst", "Oldcastle Ford", "Orchard Hill", "Otterbrook", "Pebblecombe", "Pinecrest",
            "Plumtree", "Quarry Hill", "Quillfeather", "Ravenscar", "Redwater", "Ridgeway", "Rivermoor", "Rosewell",
            "Rushford", "Saltmarsh", "Sandhaven", "Silverbrook", "Skyline Cove", "Springvale", "Stonebridge", "Stormhaven",
            "Summerfield", "Sunderland Reach", "Swallowfield", "Thistledown", "Thornbury", "Timberlake", "Tidewater", "Valehurst",
            "Verdant Hollow", "Wentworth Green", "Whitecliff", "Whitmere", "Willowbend", "Windermoor", "Winterbourne", "Wrenfield",
            "Yarrow Bay", "Yewdale"
        };

        public static IReadOnlyList<string> All { get { return Names; } }

        /// <summary>Picks a name at random.</summary>
        public static string Pick(Random random)
        {
            return Names[random.Next(Names.Length)];
        }

        /// <summary>Picks a name with a fresh time-based seed, so each launch differs.</summary>
        public static string PickRandom()
        {
            return Pick(new Random(Environment.TickCount ^ Guid.NewGuid().GetHashCode()));
        }
    }
}
