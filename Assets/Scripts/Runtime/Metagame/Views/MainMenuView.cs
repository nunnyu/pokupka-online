using UnityEngine.UIElements;
namespace Unity.Template.Multiplayer.NGO.Runtime
{
    internal class MainMenuView : View<MetagameApplication>
    {
        Button        m_PlayButton;
        Button        m_QuitButton;
        VisualElement m_Root;

        void OnEnable()
        {
            var uiDocument = GetComponent<UIDocument>();
            m_Root = uiDocument.rootVisualElement;

            m_PlayButton = m_Root.Q<Button>("singlePlayerButton");
            m_PlayButton.RegisterCallback<ClickEvent>(OnClickPlay);

            m_QuitButton = m_Root.Q<Button>("quitButton");
            m_QuitButton.RegisterCallback<ClickEvent>(OnClickQuit);

            CustomNetworkManager.OnConfigurationLoaded += OnConfigurationLoaded;
        }

        void OnDisable()
        {
            m_PlayButton?.UnregisterCallback<ClickEvent>(OnClickPlay);
            m_QuitButton?.UnregisterCallback<ClickEvent>(OnClickQuit);
            CustomNetworkManager.OnConfigurationLoaded -= OnConfigurationLoaded;
        }

        void OnConfigurationLoaded() => DisableControlsUnsupportedInAutoconnectMode();

        internal void DisableControlsUnsupportedInAutoconnectMode()
        {
            if (CustomNetworkManager.Singleton.AutoConnectOnStartup)
                m_PlayButton.SetEnabled(false);
        }

        void OnClickPlay(ClickEvent evt)    => Broadcast(new StartSinglePlayerModeEvent());
        void OnClickQuit(ClickEvent evt)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            UnityEngine.Application.Quit();
#endif
        }
    }
}
