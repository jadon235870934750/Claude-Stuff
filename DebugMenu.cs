using Photon.Pun;
using UnityEngine;

public class DebugMenu : MonoBehaviour
{
    private PlayerName playerName;
    private string username = "";

    private void Awake()
    {
        username = PlayerPrefs.GetString("username");
    }

    private void OnGUI()
    {
        int fps = (int)(1f / Time.smoothDeltaTime);
        GUI.Box(new Rect(10f, 10f, 100f, 90f), fps.ToString());

        username = GUI.TextField(new Rect(20f, 40f, 80f, 20f), username, 25);

        if (GUI.Button(new Rect(20f, 70f, 80f, 20f), "Change Name"))
        {
            PlayerPrefs.SetString("username", username);
            PhotonNetwork.LocalPlayer.NickName = PlayerPrefs.GetString("username");
        }
    }
}
