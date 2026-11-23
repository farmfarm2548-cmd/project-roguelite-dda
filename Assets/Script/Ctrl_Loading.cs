using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro;

public class Ctrl_Loading : MonoBehaviour
{
    [Header("ตั้งค่าด่านและเวลา")]
    public string sceneToLoad = "Room1";
    public float minLoadingTime = 3.0f; // 🕒 อยากให้โหลดนานกี่วินาที ตั้งค่าตรงนี้ได้เลย (เช่น 3 หรือ 5)

    [Header("UI Elements")]
    public Slider loadingBar;
    public TextMeshProUGUI progressText;

    void Start()
    {
        StartCoroutine(LoadSceneAsync());
    }

    IEnumerator LoadSceneAsync()
    {
        // 1. สั่งโหลดด่านเบื้องหลัง แต่ "ยังไม่ให้เปิดด่านใหม่ทันที" แม้จะโหลดเสร็จแล้ว
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneToLoad);
        operation.allowSceneActivation = false;

        float timer = 0;

        // 2. ลูปจนกว่าจะครบเวลาที่ตั้งไว้ หรือโหลดด่านเสร็จจริง
        while (timer < minLoadingTime || operation.progress < 0.9f)
        {
            timer += Time.deltaTime; // นับเวลาไปเรื่อยๆ

            // คำนวณความคืบหน้า (ใช้ค่าที่น้อยกว่าระหว่าง เวลาที่ผ่านไป กับ การโหลดจริง)
            float progress = Mathf.Clamp01(timer / minLoadingTime);

            // 3. อัปเดต UI
            if (loadingBar != null) loadingBar.value = progress;
            if (progressText != null) progressText.text = "Loading... " + (progress * 100f).ToString("F0") + "%";

            yield return null;
        }

        // 4. เมื่อครบเวลาแล้ว ค่อยสั่งให้เปิดด่านใหม่
        if (loadingBar != null) loadingBar.value = 1f;
        if (progressText != null) progressText.text = "Loading... 100%";

        yield return new WaitForSeconds(0.5f); // รออีกนิดให้คนเห็นว่า 100% แล้วนะ
        operation.allowSceneActivation = true;
    }
}
