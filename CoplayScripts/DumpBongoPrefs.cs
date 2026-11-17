using UnityEngine;

/// <summary>Logs the legacy shared Bongo board and the per-song boards from PlayerPrefs.</summary>
public static class DumpBongoPrefs
{
    public static void Execute()
    {
        Debug.Log("[BongoPrefs] GlitchyHS=" + PlayerPrefs.GetInt("GlitchyHS", -1)
                  + " BongoHS_ComputerBoy=" + PlayerPrefs.GetInt("BongoHS_ComputerBoy", -1)
                  + " BongoHS_Boppy=" + PlayerPrefs.GetInt("BongoHS_Boppy", -1)
                  + " BongoHS_Delve=" + PlayerPrefs.GetInt("BongoHS_Delve", -1)
                  + " FlashCardsClassicHS=" + PlayerPrefs.GetInt("FlashCardsClassicHS", -1));
    }
}
