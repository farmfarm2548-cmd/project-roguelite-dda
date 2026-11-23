using UnityEngine;

//  RoomController  (Proposal §7.1) — [ทางเลือก / OPTIONAL]
//
//  ปกติ "ไม่จำเป็นต้องใช้" แล้ว เพราะ DDAManager อ่านชื่อซีนเอง
//  (Room1/Room2/Room3 → Normal, ชื่อที่มี "Boss" → Boss)
//
//  ใช้ตัวนี้เมื่อ:
//   - ชื่อซีนไม่ตรงรูปแบบ (เช่นตั้งชื่อเอง) และต้องการระบุหมายเลขห้องเอง
//   - มีศัตรู "วางไว้ในซีนล่วงหน้า" (ไม่ได้เกิดจาก Spawner) — ตัวนี้ (ExecOrder -100)
//     จะรันก่อน EnemyStats จึงมั่นใจว่าศัตรูได้ค่าพารามิเตอร์ครบ
[DefaultExecutionOrder(-100)]
public class RoomController : MonoBehaviour
{
    [Header("ระบุห้อง")]
    [Tooltip("1 = Room1, 2 = Room2, 3 = Room3")]
    public int roomIndex = 1;
    [Tooltip("ติ๊กถ้าเป็นห้องบอส (ใช้ Fixed Challenge, อยู่นอก adaptive loop)")]
    public bool isBoss = false;

    private void Awake()
    {
        if (DDAManager.Instance != null)
        {
            DDAManager.Instance.BeginRoom(roomIndex, isBoss);
        }
        else
        {
            Debug.LogWarning("[RoomController] ไม่พบ DDAManager — ทดสอบซีนเดี่ยว จะใช้ค่า default");
        }
    }
}
