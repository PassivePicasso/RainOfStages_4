using BepInEx.Logging;
using PassivePicasso.RainOfStages.Monomod;
using RoR2;
using RoR2.UI;
using RoR2.UI.MainMenu;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace PassivePicasso.RainOfStages.Hooks
{
    using RainOfStages = Plugin.RainOfStages;
    internal class NonProxyHooks
    {
        static ManualLogSource Logger => RainOfStages.Instance.RoSLog;

        struct DrawOrder { public Vector3 start; public Vector3 end; public float duration; public Color color; }

        [Hook(typeof(MainMenuController), "Start")]
        private static void MainMenuController_Start(Action<MainMenuController> orig, MainMenuController self)
        {
            try
            {
                Logger.LogDebug("Adding GameModes to ExtraGameModeMenu menu");

                var mainMenu = GameObject.Find("MainMenu")?.transform;
                var weeklyButton = mainMenu.Find("MENU: Extra Game Mode/ExtraGameModeMenu/Main Panel/GenericMenuButtonPanel/JuicePanel/GenericMenuButton (Weekly)");
                Logger.LogDebug($"Found: {weeklyButton.name}");

                var juicedPanel = weeklyButton.transform.parent;
                string[] skip = new[] { "Classic", "ClassicRun" };
                var gameModes = RainOfStages.Instance.GameModes.Where(gm => !skip.Contains(gm.name));
                foreach (var gameMode in gameModes)
                {
                    var copied = Transform.Instantiate(weeklyButton);
                    copied.name = $"GenericMenuButton ({gameMode})";
                    GameObject.DestroyImmediate(copied.GetComponent<DisableIfGameModded>());

                    var tmc = copied.GetComponent<LanguageTextMeshController>();
                    tmc.token = gameMode.GetComponent<Run>().nameToken;

                    var consoleFunctions = copied.GetComponent<ConsoleFunctions>();

                    var hgbutton = copied.gameObject.GetComponent<HGButton>();
                    hgbutton.onClick = new Button.ButtonClickedEvent();

                    hgbutton.onClick.AddListener(() => consoleFunctions.SubmitCmd($"transition_command \"gamemode {gameMode}; host 0;\""));

                    copied.SetParent(juicedPanel);
                    copied.localScale = Vector3.one;
                    copied.gameObject.SetActive(true);
                }
            }
            catch (Exception e)
            {
                Logger.LogError("Error Adding GameModes to ExtraGameModeMenu menu");
                Logger.LogError(e.Message);
                Logger.LogError(e.StackTrace);
            }
            finally
            {
                Logger.LogInfo("Finished Main Menu Modifications");
                orig(self);
            }
        }
    }
}