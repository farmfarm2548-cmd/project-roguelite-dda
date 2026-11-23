using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("ลากตัว Player มาใส่ช่องนี้")]
    public Transform target;

    [Header("ความนุ่มนวลของกล้อง (ยิ่งน้อยยิ่งหนืด)")]
    public float smoothSpeed = 5f;

    // ออฟเซ็ตของกล้อง (แกน Z ต้องติดลบไว้เสมอสำหรับเกม 2D กล้องจะได้ไม่จมไปในฉาก)
    public Vector3 offset = new Vector3(0f, 0f, -10f);

    void LateUpdate()
    {
        if (target != null)
        {
            // คำนวณตำแหน่งเป้าหมายที่กล้องควรจะไปอยู่
            Vector3 desiredPosition = target.position + offset;

            // ใช้ Vector3.Lerp ทำให้กล้องค่อยๆ เลื่อนตามแบบนุ่มนวล ไม่กระตุก
            transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        }
    }
}
