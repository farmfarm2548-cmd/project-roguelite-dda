using UnityEngine;
using UnityEngine.SceneManagement; // จำเป็นมากสำหรับการเปลี่ยนหน้าด่าน

public class Ctrl_MainMenu : MonoBehaviour
{
    // 1. ฟังก์ชันสำหรับกด Play แล้ววาร์ปไปหน้า Loading
    public void StartGame()
    {
        // "Loading" ต้องสะกดให้ตรงกับชื่อไฟล์ Scene ใน Unity ของคุณเป๊ะๆ นะครับ
        SceneManager.LoadScene("Loading");
    }

    // 2. ฟังก์ชันสำหรับกดออกจากเกม (แถมไว้ให้สำหรับหน้า Main Menu)
    public void QuitGame()
    {
        Debug.Log("ปิดเกมแล้ว!");
        Application.Quit();
    }
}
