using Xunit;

namespace ACLocalTrafficStudio.Tests;

public class MapClassifierTests
{
    [Theory]
    [InlineData("shutoko_revival_project", "", MapKind.HighwayShutoko)]
    [InlineData("SRP", "", MapKind.HighwayShutoko)]
    [InlineData("ks_nordschleife", "", MapKind.TrackDayNordschleife)]
    [InlineData("nordschleife", "touristenfahrten", MapKind.TrackDayNordschleife)]
    [InlineData("nurburgring", "", MapKind.TrackDayNordschleife)]
    [InlineData("nurburgring_gp", "", MapKind.Generic)]
    [InlineData("nurburgring_24h", "", MapKind.Generic)]
    [InlineData("monza", "monza_1966", MapKind.Generic)]
    public void Classify_Works(string folder, string layout, MapKind expected)
    {
        Assert.Equal(expected, MapClassifier.Classify(folder, layout));
    }

    [Theory]
    [InlineData(MapKind.HighwayShutoko, "builtin-shutoko-highway")]
    [InlineData(MapKind.TrackDayNordschleife, "builtin-nordschleife-trackday")]
    [InlineData(MapKind.Generic, "builtin-urban-street")]
    public void SuggestedProfile_MatchesKind(MapKind kind, string expectedId)
    {
        Assert.Equal(expectedId, MapClassifier.SuggestedProfileId(kind));
    }
}
