using UnityEngine;
using UnityEngine.SceneManagement;

//  RoomExit  (Proposal §9.3)
//  เมื่อผู้เล่นถึงประตูทางออก: ปิด telemetry ห้องนี้ → ประมาณ Skill →
//  เตรียม Challenge ห้องถัดไป (adaptive) → โหลดซีนถัดไป
//  ถ้าห้องปัจจุบันเป็น Boss จะปิด session และ export dataset แทน
public class RoomExit : MonoBehaviour
{
    [Header("ชื่อซีนถัดไป (ใส่ให้ตรงกับไฟล์ Scene)")]
    public string nextSceneName = "Room2";

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (GetComponent<WarpDoor>() != null) return;
        if (!collision.CompareTag("Player")) return;

        if (string.IsNullOrEmpty(nextSceneName))
        {
            Debug.LogError("[RoomExit] กรุณาใส่ชื่อซีนถัดไปใน Inspector");
            return;
        }

        if (DDAManager.Instance != null)
        {
            if (DDAManager.Instance.currentIsBoss)
                DDAManager.Instance.EndSession(true);   // จบ Boss → export dataset
            else
                DDAManager.Instance.EndRoom(true);      // ผ่านห้อง → คำนวณห้องถัดไป
        }

        SceneManager.LoadScene(nextSceneName);
    }
}
