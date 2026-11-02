using Xunit;

namespace ACLocalTrafficStudio.Tests;

public class CarClassifierTests
{
    [Fact]
    public void NoPhysics_IsIncomplete()
    {
        Assert.Equal(TrafficSuitability.Incomplete, CarClassifier.Classify("A", "street", hasPhysics: false));
    }

    [Fact]
    public void RaceTags_AreRaceOnly()
    {
        Assert.Equal(TrafficSuitability.RaceOnly, CarClassifier.Classify("GT3", "race,gt3", hasPhysics: true));
        Assert.Equal(TrafficSuitability.RaceOnly, CarClassifier.Classify("", "formula", hasPhysics: true));
    }

    [Fact]
    public void StreetTags_AreGood()
    {
        Assert.Equal(TrafficSuitability.Good, CarClassifier.Classify("B", "street,production", hasPhysics: true));
    }

    [Fact]
    public void UnknownTags_ArePossible()
    {
        Assert.Equal(TrafficSuitability.Possible, CarClassifier.Classify("", "", hasPhysics: true));
    }
}
