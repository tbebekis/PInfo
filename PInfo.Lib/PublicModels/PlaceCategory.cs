/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo;

/// <summary>
/// Defines a browsable place category and its named filters using GeoNames class/code pairs.
/// The catalog uses the official feature definitions at https://www.geonames.org/export/codes.html.
/// </summary>
public sealed class PlaceCategory
{
    // ● private
    static readonly ReadOnlyCollection<PlaceCategory> fAll = CreateCategories();
    readonly ReadOnlyDictionary<string, ReadOnlyCollection<string>> fFilters;

    /// <summary>
    /// Creates the six supported categories with explicit, non-overlapping filters within each category.
    /// </summary>
    /// <returns>The read-only category catalog.</returns>
    static ReadOnlyCollection<PlaceCategory> CreateCategories()
    {
        return Array.AsReadOnly(new[]
        {
            new PlaceCategory("Mountains", new Dictionary<string, string>
            {
                ["Mountains"] = "T.MT",
                ["Mountain ranges"] = "T.MTS",
                ["Peaks"] = "T.PK,T.PKS,T.NTK,T.NTKS",
                ["Hills"] = "T.HLL,T.HLLS",
                ["Ridges"] = "T.RDGE",
                ["Passes"] = "T.PASS",
                ["Volcanoes"] = "T.VLC",
                ["Plateaus"] = "T.PLAT,T.PLATX"
            }),
            new PlaceCategory("Water", new Dictionary<string, string>
            {
                ["Rivers and streams"] = "H.STM,H.STMS,H.STMI,H.STMX,H.STMIX,H.STMSB,H.STMC,H.WTRC",
                ["Lakes"] = "H.LK,H.LKC,H.LKI,H.LKN,H.LKNI,H.LKO,H.LKOI,H.LKS,H.LKSB,H.LKSC,H.LKSI,H.LKSN,H.LKSNI,H.LKX",
                ["Waterfalls"] = "H.FLLS,H.FLLSX",
                ["Springs"] = "H.SPNG,H.SPNS,H.SPNT,H.GYSR",
                ["Reservoirs"] = "H.RSV,H.RSVI",
                ["Canals"] = "H.CNL,H.CNLA,H.CNLD,H.CNLI,H.CNLN,H.CNLX",
                ["Lagoons"] = "H.LGN,H.LGNS,H.LGNX"
            }),
            new PlaceCategory("Coasts and Islands", new Dictionary<string, string>
            {
                ["Islands"] = "T.ISL,T.ISLET,T.ISLF,T.ISLM,T.ISLS,T.ISLT,T.ISLX,T.ATOL",
                ["Beaches"] = "T.BCH,T.BCHS",
                ["Bays and gulfs"] = "H.BAY,H.BAYS,H.BGHT,H.COVE,H.GULF",
                ["Capes and headlands"] = "T.CAPE,T.HDLD,T.PT,T.PTS",
                ["Peninsulas"] = "T.PEN,T.PENX",
                ["Coasts"] = "L.CST",
                ["Straits and fjords"] = "H.STRT,H.FJD,H.FJDS,H.SD",
                ["Reefs"] = "H.RF,H.RFC,H.RFX"
            }),
            new PlaceCategory("Administrative Areas", new Dictionary<string, string>
            {
                ["First-level divisions"] = "A.ADM1",
                ["Second-level divisions"] = "A.ADM2",
                ["Third-level divisions"] = "A.ADM3",
                ["Fourth-level divisions"] = "A.ADM4",
                ["Fifth-level divisions"] = "A.ADM5",
                ["Other divisions"] = "A.ADMD"
            }),
            new PlaceCategory("Transport", new Dictionary<string, string>
            {
                ["Airports and airfields"] = "S.AIRP,S.AIRF,S.AIRH,S.AIRT,H.AIRS",
                ["Ports and harbors"] = "H.HBR,H.HBRX,L.PRT,S.MAR,S.LDNG,S.PIER,S.QUAY,S.WHRF",
                ["Railway stations"] = "S.RSTN,S.RSTP",
                ["Metro stations"] = "S.MTRO",
                ["Bus stations"] = "S.BUSTN",
                ["Ferry terminals"] = "S.FYT",
                ["Transit terminals"] = "S.TRANT"
            }),
            new PlaceCategory("Points of Interest", new Dictionary<string, string>
            {
                ["Castles and forts"] = "S.CSTL,S.FT",
                ["Monuments"] = "S.MNMT",
                ["Archaeological and historical sites"] = "S.ANS,S.HSTS,S.RUIN,S.AMTH,S.PYR,S.PYRS,L.BTL",
                ["Museums"] = "S.MUS",
                ["Parks and reserves"] = "L.PRK,L.RESN,L.RESW,L.AMUS",
                ["Gardens and zoos"] = "S.GDN,S.ZOO",
                ["Religious sites"] = "S.CH,S.MSTY,S.CVNT,S.MSQE,S.SYG,S.TMPL,S.PGDA,S.SHRN,S.RLG",
                ["Palaces"] = "S.PAL",
                ["Caves"] = "S.CAVE"
            })
        });
    }

    // ● constructor
    /// <summary>
    /// Creates a category and an All filter containing the union of its subcategories.
    /// </summary>
    /// <param name="Name">The display name.</param>
    /// <param name="Filters">Named filters containing comma-separated class/code pairs.</param>
    PlaceCategory(string Name, Dictionary<string, string> Filters)
    {
        this.Name = Name;
        Dictionary<string, ReadOnlyCollection<string>> Items = new();
        Items.Add("All", Array.AsReadOnly(Filters.Values.SelectMany(Value => Value.Split(','))
            .Distinct(StringComparer.Ordinal).ToArray()));
        foreach (var Filter in Filters)
            Items.Add(Filter.Key, Array.AsReadOnly(Filter.Value.Split(',')));
        fFilters = new ReadOnlyDictionary<string, ReadOnlyCollection<string>>(Items);
    }

    // ● public
    /// <summary>
    /// Returns the readable subcategory for a feature, or its original code if unknown.
    /// </summary>
    /// <param name="Place">The place to describe.</param>
    /// <returns>The matching subcategory name.</returns>
    public string GetFeatureName(PlaceModel Place)
    {
        if (Place == null)
            return string.Empty;
        string Feature = Place.FeatureClass + "." + Place.FeatureCode;
        foreach (var Filter in fFilters)
        {
            if (Filter.Key != "All" && Filter.Value.Contains(Feature))
                return Filter.Key;
        }
        return Place.FeatureCode;
    }

    // ● properties
    /// <summary>
    /// The six categories available on a country card.
    /// </summary>
    static public ReadOnlyCollection<PlaceCategory> All => fAll;
    /// <summary>
    /// The category display name.
    /// </summary>
    public string Name { get; }
    /// <summary>
    /// Named subcategory filters, including All, with immutable feature-key collections.
    /// </summary>
    public IReadOnlyDictionary<string, ReadOnlyCollection<string>> Filters => fFilters;
}
