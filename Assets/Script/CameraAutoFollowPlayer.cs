using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CameraAutoFollowPlayer : MonoBehaviour
{
    private static bool hooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        if (hooked) return;
        hooked = true;
        BindCameraToPlayer();
        SceneManager.sceneLoaded += (_, __) => BindCameraToPlayer();
    }

    private static void BindCameraToPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        bool bound = false;

        // 1) ผูกเข้ากับ CinemachineCamera (Unity 6 / v3) หรือ CinemachineVirtualCamera (v2)
#pragma warning disable CS0618
        foreach (var comp in Object.FindObjectsOfType<MonoBehaviour>())
#pragma warning restore CS0618
        {
            if (comp == null) continue;
            string typeName = comp.GetType().Name;
            if (typeName == "CinemachineCamera" || typeName == "CinemachineVirtualCamera")
            {
                PropertyInfo followProp = comp.GetType().GetProperty("Follow");
                if (followProp != null && followProp.CanWrite)
                {
                    followProp.SetValue(comp, player.transform);
                    bound = true;
                }
            }
            else if (comp is CameraFollow cf)
            {
                cf.target = player.transform;
                bound = true;
            }
        }

        // 2) ถ้าไม่มีตัวตามกล้องเลย ให้ติด CameraFollow บน Main Camera ให้อัตโนมัติ
        if (!bound && Camera.main != null)
        {
            var cf = Camera.main.GetComponent<CameraFollow>();
            if (cf == null) cf = Camera.main.gameObject.AddComponent<CameraFollow>();
            cf.target = player.transform;
        }
    }
}
