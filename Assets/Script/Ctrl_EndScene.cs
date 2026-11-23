using UnityEngine;
using UnityEngine.SceneManagement; // สำคัญมากสำหรับการโหลดด่าน

public class Ctrl_EndScene : MonoBehaviour
{
    // 1. ฟังก์ชันสำหรับปุ่ม Play (กดแล้วไปหน้า MainMenu)
    public void PlayGame()
    {
        // "MainMenu" ต้องสะกดให้ตรงกับชื่อ Scene ในโฟลเดอร์ Project นะครับ
        SceneManager.LoadScene("MainMenu");
    }

    // 2. ฟังก์ชันสำหรับปุ่ม Quit (กดแล้วออกจากเกม)
    public void QuitGame()
    {
        Debug.Log("ออกจากเกมเรียบร้อย!");
        Application.Quit();
    }
}
