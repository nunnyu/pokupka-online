using Unity.Netcode;
using UnityEngine;

namespace Pokupka
{
    // Dev-only buttons to start a host, client or server.
    public class DevConnectionHud : MonoBehaviour
    {
        void OnGUI()
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(10, 10, 160, 120));
            if (!networkManager.IsClient && !networkManager.IsServer)
            {
                if (GUILayout.Button("Host"))
                {
                    networkManager.StartHost();
                }
                if (GUILayout.Button("Client"))
                {
                    networkManager.StartClient();
                }
                if (GUILayout.Button("Server"))
                {
                    networkManager.StartServer();
                }
            }
            else
            {
                string role = networkManager.IsHost ? "Host" : networkManager.IsServer ? "Server" : "Client";
                GUILayout.Label($"{role} (right-click to move)");
            }
            GUILayout.EndArea();
        }
    }
}
