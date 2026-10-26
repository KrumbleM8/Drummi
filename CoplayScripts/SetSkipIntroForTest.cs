using KrumbleHut.Drummi.UI;

/// <summary>EDIT MODE (domain reload off): flag the next play session to skip the Home title intro.</summary>
public static class SetSkipIntroForTest
{
    public static string Execute()
    {
        HomeFlowController.SkipIntro = true;
        return "HomeFlowController.SkipIntro = true";
    }
}
